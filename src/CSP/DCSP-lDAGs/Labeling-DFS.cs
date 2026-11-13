namespace DelayConstrainedShortestPath;

using DataStructures;

#region helper classes

using IntLabelHeap = DataStructures.Heap<Label<int>, MaxIntLabelComparer
#if INSTRUMENTATION
    , DataStructures.HeapOpsCounter<Label<int>>
#endif
>;

using SortedIntervalArray = DataStructures.SortedArray<Interval, IntervalLowComparer
#if INSTRUMENTATION
    , DataStructures.SortedArrayOpsCounter<Interval>
#endif
    >;
using SortedLabelArray = DataStructures.SortedArray<Distances, DistancesComparer
#if INSTRUMENTATION
    , DataStructures.SortedArrayOpsCounter<Distances>
#endif
    >;

/// <summary>
/// Representation of a equivalence interval
///     Interval [low_delay, high_delay): low_delay is inclusive, high exclusive
///     Tracks that: any delay from source that falls within this interval can complete its path to terminal with weight_to_terminal
///                  where weight_to_terminal is the best weight completion possible
///     Reversely this means: if a delay from source is smaller than low_delay, a better weight path to terminal (might) become available
///                                 (is infeasible for delay in interval but feasible when delay drops below low_delay)
///                           if a delay from source is bigger or equal to high_delay, the best weight completion to terminal (weight_to_terminal) becomes infeasible
/// </summary>
struct Interval
{
    public int low_delay;
    public int high_delay;
    public int weight_to_terminal;
}
readonly struct IntervalLowComparer : IComparer<Interval>
{
    public int Compare(Interval x, Interval y)
    {
        if (x.low_delay < y.low_delay) return -1;
        if (x.low_delay > y.low_delay) return 1;
        return 0;
    }
}
readonly struct MaxIntLabelComparer : IComparer<Label<int>>
{
    public int Compare(Label<int> x, Label<int> y)
    {
        // we reverse the comparison, as it is intended for a max_heap
        // but heap implementation supports min_heap!
        if (x.dist < y.dist) return 1;
        if (x.dist > y.dist) return -1;
        return 0;
    }
}
readonly struct IntervalLowDelayComparer : IComparer<Interval, int>
{
    public int Compare(Interval x, int y)
    {
        if (x.low_delay < y) return -1;
        if (x.low_delay > y) return 1;
        return 0;
    }
    public int Compare(int y, Interval x)
    {
        if (x.low_delay < y) return 1;
        if (x.low_delay > y) return -1;
        return 0;
    }
}
readonly struct DistancesComparer : IComparer<Distances>
{
    public int Compare(Distances x, Distances y)
    {
        if (x.delay < y.delay) return -1;
        if (x.delay > y.delay) return 1;
        return 0;
    }
}
readonly struct DistancesDelayComparer : IComparer<Distances, int>
{
    public int Compare(Distances x, int y)
    {
        if (x.delay < y) return -1;
        if (x.delay > y) return 1;
        return 0;
    }
    public int Compare(int y, Distances x)
    {
        if (x.delay < y) return 1;
        if (x.delay > y) return -1;
        return 0;
    }
}

#endregion

public sealed class LabelingDFSSolver : BaseSolver
{
    public LabelingDFSSolver(Input input
#if TEST
        , bool verbose
        , bool check
#endif
        ) : base(input
#if TEST
            , verbose: verbose
            , check: check
#endif
            )
    {
    }

    #region optimal solvers

    /// <summary>
    /// Does a depth first traversal carrying accumulated (weight,delay) from source as in label setting
    /// Keeps a set of the none-dominated (weight,delay) pairs derived after all children of a node are visited in dfs, before backtracking
    /// from successor domination to prune future labels on dominance
    /// </summary>
    /// <param name="incumbent">the best solution found so far</param>
    public Solution run_dfs_back_dominance(int incumbent, CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 0;
#if INSTRUMENTATION
        counters.number_of_labels = 0;
#endif
        // data structures
        int[] delay_to_terminal = new int[graph.number_of_nodes];  // shortest distance from each node to the terminal for cost (delay)
        int[] weight_to_terminal = new int[graph.number_of_nodes]; // shortest distance from each node to the terminal for cost (weight)
        Edge[] path = new Edge[graph.number_of_nodes];             // track edges to reconstruct the actual path during the depth first traversal

        // calculate the shortest node-to-terminal distances
        // in distance metric (delay)
        // to be used for lower bound pruning (label's delay + least_delay_to_terminal > max_delay)
        (int delay_path, int weight_of_delay_path) = least_delay_from_terminal(delay_to_terminal, path);

        if (delay_path > max_delay)
        {
            // infeasible, there is no path with a delay <= max_delay
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, delay_path, Feasibility.infeasible);
#endif
            return new Solution(_feasibility, null, null
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
            );
        }
        _feasibility = Feasibility.feasible;

        // update the incumbent with the weight of the least delay path or the precalculated incumbent if it is smaller
        if (weight_of_delay_path < incumbent)
        {
            set_primal_solution(weight_of_delay_path, delay_path, get_path_from_source(path));
        }
        else
        {
            _upper_bound_weight = incumbent;
            _upper_bound_path = new Edge[graph.number_of_layers]; // reserve space to store a better path when it is found
        }

        // calculate the shortest node-to-terminal distances
        // in distance metric (weight)
        // to be used for weight bound pruning (label's weight + least_weight_to_terminal > _upper_bound_weight)
        (int weight_path, int delay_of_weight_path) = shortest_weight_from_terminal(weight_to_terminal, path);

        if (delay_of_weight_path <= max_delay)
        {
            // least weight path is feasible, so also optimal
            path_from_source(path, _upper_bound_path);
            set_primal_solution(weight_path, delay_of_weight_path, _upper_bound_path);
#if TEST
            if (check)
            {
                if (get_path_weight(_upper_bound_path) != _upper_bound_weight) { PrintFailedCheck($"Path weight {get_path_weight(_upper_bound_path)} differs from {_upper_bound_weight}"); }
                if (get_path_delay(_upper_bound_path) != _upper_bound_delay) { PrintFailedCheck($"Path delay {get_path_delay(_upper_bound_path)} differs from {_upper_bound_delay}"); }
            }
#endif
            return new Solution(_feasibility, null, new PrimalSolution(weight_path, delay_of_weight_path, _upper_bound_path)
#if INSTRUMENTATION
                                , counters with { iterations = _iteration }
#endif
                                );
        }

        // dominator set
        SortedLabelArray[] node_dominators = new SortedLabelArray[graph.number_of_nodes];
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            node_dominators[n] = new SortedLabelArray(0, 1, new DistancesComparer()
#if INSTRUMENTATION
                , new SortedArrayOpsCounter<Distances>((int n) => counters.number_of_ops += n, () => ++counters.number_of_ops)
#endif
#if TEST
                , check
#endif
            );
        }
        // initialize each dominator set with a dominator representing the infeasible case
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            node_dominators[n].insert(new Distances()
            {
                delay = max_delay - delay_to_terminal[n] + 1,
                weight = int.MinValue
            });
        }

        // workspaces for storing temporary data during dfs without reallocating memory at every recursion
        int[][] workspaces = new int[graph.number_of_layers][];
        for (int l = 0; l < graph.number_of_layers; ++l) workspaces[l] = new int[graph.max_edges_out_of_node];

#if INSTRUMENTATION
        ++counters.number_of_labels; // label initialized at source
#endif
        dfs_back_dominance(graph.source, new Distances() { weight = 0, delay = 0 }, node_dominators, delay_to_terminal, weight_to_terminal, path, workspaces, token);
#if TEST
        if (check)
        {
            if (incumbent > _upper_bound_weight && get_path_weight(_upper_bound_path) != _upper_bound_weight) { PrintFailedCheck($"Final path weight {get_path_weight(_upper_bound_path)} differs from reported final weight {_upper_bound_weight}"); }
            if (incumbent > _upper_bound_weight && get_path_delay(_upper_bound_path) != _upper_bound_delay) { PrintFailedCheck($"Final path delay {get_path_delay(_upper_bound_path)} differs from reported final delay {_upper_bound_delay}"); }
        }
#endif
        return new Solution(_feasibility, null, new PrimalSolution(_upper_bound_weight, _upper_bound_delay, _upper_bound_path)
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
    }
    public Solution run_dfs_back_dominance(CancellationToken token)
    {
        return run_dfs_back_dominance(int.MaxValue, token);
    }

    /// <summary>
    /// Does a depth first traversal carrying accumulated (weight,delay) from source as in label setting
    /// After all children of a node are visited in dfs, before backtracking
    /// a delay interval [L,U) is calculated for which all incoming delays in this interval
    /// have the same feasible delay completion path to terminal
    /// Future labels visiting this node that fall in [L,U) can immediately backtrack
    ///     base case: U = min(successor U), L = max(successor L)
    /// </summary>
    public Solution run_dfs_delay_completion_equivalence(CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 0;
#if INSTRUMENTATION
        counters.number_of_labels = 0;
#endif
        // data structures
        int[] delay_to_terminal = new int[graph.number_of_nodes];                // shortest distance from each node to the terminal for cost (delay)
        Predecessor[][] predecessors = new Predecessor[graph.number_of_nodes][]; // predecessor structures for path reconstruction, stores the edge and successor interval that led to the best weight completion to terminal for each interval at each node

        // set of intervals per node
        SortedIntervalArray[] intervals = new SortedIntervalArray[graph.number_of_nodes];
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            intervals[n] = new SortedIntervalArray(0, 1, new IntervalLowComparer()
#if INSTRUMENTATION
                , new SortedArrayOpsCounter<Interval>((int n) => counters.number_of_ops += n, () => ++counters.number_of_ops)
#endif
#if TEST
                , check
#endif
            );
        }

        // calculate the shortest node-to-terminal distances
        // in distance metric (delay)
        // to be used for lower bound pruning (label's delay + least_delay_to_terminal > max_delay)
        Edge[] path = new Edge[graph.number_of_nodes]; // todo: when we prune on _upper_bound_weight, we'll use this as the best path so far
        (int delay_path, _) = least_delay_from_terminal(delay_to_terminal, path);

        if (delay_path > max_delay)
        {
            // infeasible, there is no path with a delay <= max_delay
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, delay_path, Feasibility.infeasible);
#endif
            return new Solution(_feasibility, null, null
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
            );
        }
        _feasibility = Feasibility.feasible;
        _upper_bound_weight = int.MaxValue;

        // initialize every node with an interval that captures infeasible partial paths
        // where delay from source + least delay to terminal > max_delay
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            intervals[n].insert(new Interval()
            {
                high_delay = int.MaxValue,
                low_delay = max_delay - delay_to_terminal[n] + 1,
                weight_to_terminal = int.MaxValue
            });
        }
        // add the feasible interval to the terminal as well:
        // where delay from source to terminal <= max_delay
        ensure(ref intervals[graph.terminal.index_in_graph].sorted_array, 2);
        intervals[graph.terminal.index_in_graph].insert(new Interval()
        {
            high_delay = max_delay + 1,
            low_delay = int.MinValue,
            weight_to_terminal = 0
        });
#if INSTRUMENTATION
        ++counters.number_of_labels; // label initialized at source
#endif
        dfs_delay_completion_equivalence(graph.source, new Distances() { weight = 0, delay = 0 }, intervals, delay_to_terminal, predecessors, token);

        // set the solution to the best path from source that reached the terminal
#if TEST
        if (check)
        {
            if (intervals[graph.terminal.index_in_graph].array_size != 2) { PrintFailedCheck("Terminal should have exactly two intervals"); }
            if (intervals[graph.source.index_in_graph].array_size != 2) { PrintFailedCheck("Source should have exactly two intervals"); }
            if (predecessors[graph.source.index_in_graph].Length == 0) { PrintFailedCheck("Source should have exactly one predecessor"); }
            if (intervals[graph.source.index_in_graph].sorted_array[0].high_delay - 1 > max_delay) { PrintFailedCheck("First interval at source has higher than maximum delay: infeasible solution!"); }
            if (intervals[graph.source.index_in_graph].sorted_array[0].weight_to_terminal != _upper_bound_weight) { PrintFailedCheck("First interval at source does not have the best weight to terminal: did we forget to update _upper_bound_weight?"); }
        }
#endif
        // reconstruct the best path from source to terminal using the predecessor structures
        _upper_bound_path = get_path_from_interval_predecessors(predecessors, intervals);
#if TEST
        if (check)
        {
            if (get_path_weight(_upper_bound_path) != _upper_bound_weight) { PrintFailedCheck($"Final path weight {get_path_weight(_upper_bound_path)} differs from reported final weight {_upper_bound_weight}"); }
            if (get_path_delay(_upper_bound_path) > max_delay) { PrintFailedCheck($"Final path delay {get_path_delay(_upper_bound_path)} infeasible {max_delay}"); }
        }
#endif
        return new Solution(_feasibility, null, new PrimalSolution(_upper_bound_weight, get_path_delay(_upper_bound_path), _upper_bound_path)
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
    }

    /// <summary>
    /// Does a depth first traversal carrying accumulated (weight,delay) from source as in label setting
    /// After all children of a node are visited in dfs, before backtracking
    /// an delay interval [L,U) is calculated for which all incoming delays in this interval
    /// have the same delay feasible, shortest weight path to terminal
    /// Future labels visiting this node that fall in [L,U) can immediately backtrack
    ///     case with dominance on partial paths node-to-terminal during backtrack:
    ///         U = U of interval with smallest feasible-weight-to-terminal (above U best weight completion becomes infeasible)
    ///         L = max(successor L) whose weight-to-terminal is smaller than smallest feasible-weight-to-terminal (below L a better weight completion becomes feasible)
    /// </summary>
    public Solution run_dfs_weight_completion_equivalence(CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 0;
#if INSTRUMENTATION
        counters.number_of_labels = 0;
#endif
        // data structures
        int[] delay_to_terminal = new int[graph.number_of_nodes];  // shortest distance from each node to the terminal for cost (delay)
        int[] weight_to_terminal = new int[graph.number_of_nodes]; // shortest distance from each node to the terminal for cost (weight)
        Predecessor[][] predecessors = new Predecessor[graph.number_of_nodes][]; // predecessor structures for path reconstruction, stores the edge and successor interval that led to the best weight completion to terminal for each interval at each node

        IntLabelHeap[] heaps = new IntLabelHeap[graph.number_of_layers];      // workspace for each node to store intermediate results from the recursive calls to children, so we don't have to allocate memory for each recursive call
        for (int l = 0; l < graph.number_of_layers; ++l) heaps[l] = new IntLabelHeap(graph.max_edges_out_of_node, new MaxIntLabelComparer()
#if INSTRUMENTATION
            , new HeapOpsCounter<Label<int>>(() => ++counters.number_of_ops)
#endif
#if TEST
            , check
#endif
            );

        // set of intervals per node
        SortedIntervalArray[] intervals = new SortedIntervalArray[graph.number_of_nodes];
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            intervals[n] = new SortedIntervalArray(0, 1, new IntervalLowComparer()
#if INSTRUMENTATION
                , new SortedArrayOpsCounter<Interval>((int n) => counters.number_of_ops += n, () => ++counters.number_of_ops)
#endif
#if TEST
                , check
#endif
            );
        }

        // calculate the shortest node-to-terminal distances
        // in distance metric (delay)
        // to be used for lower bound pruning (label's delay + least_delay_to_terminal > max_delay)
        Edge[] path = new Edge[graph.number_of_nodes]; // todo: when we prune on _upper_bound_weight, we'll use this as the best path so far
        (int delay_path, _) = least_delay_from_terminal(delay_to_terminal, path);

        if (delay_path > max_delay)
        {
            // infeasible, there is no path with a delay <= max_delay
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, delay_path, Feasibility.infeasible);
#endif
            return new Solution(_feasibility, null, null
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
        }
        _feasibility = Feasibility.feasible;
        _upper_bound_weight = int.MaxValue;

        // calculate the shortest node-to-terminal distances
        // in distance metric (weight)
        // to be used as lower bound on the distance to terminal
        shortest_weight_from_terminal(weight_to_terminal, new Edge[graph.number_of_nodes]); // todo: is the path reconstruction needed here?

        // initialize every node with an interval that captures infeasible partial paths
        // where delay from source + least delay to terminal > max_delay
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            intervals[n].insert(new Interval()
            {
                high_delay = int.MaxValue,
                low_delay = max_delay - delay_to_terminal[n] + 1,
                weight_to_terminal = int.MaxValue
            });
        }
        // add the feasible interval to the terminal as well:
        // where delay from source to terminal <= max_delay
        ensure(ref intervals[graph.terminal.index_in_graph].sorted_array, 2);
        intervals[graph.terminal.index_in_graph].insert(new Interval()
        {
            high_delay = max_delay + 1,
            low_delay = int.MinValue,
            weight_to_terminal = 0
        });
#if INSTRUMENTATION
        ++counters.number_of_labels; // label initialized at source
#endif
        dfs_weight_completion_equivalence(graph.source, new Distances() { weight = 0, delay = 0 }, intervals, delay_to_terminal, weight_to_terminal, heaps, predecessors, token);

        // set the solution to the best path from source that reached the terminal
#if TEST
        if (check)
        {
            if (intervals[graph.terminal.index_in_graph].array_size != 2) { PrintFailedCheck("Terminal should have exactly two intervals"); }
            if (intervals[graph.source.index_in_graph].array_size != 2) { PrintFailedCheck("Source should have exactly two intervals"); }
            if (predecessors[graph.source.index_in_graph].Length == 0) { PrintFailedCheck("Source should have exactly one predecessor"); }
            if (intervals[graph.source.index_in_graph].sorted_array[0].high_delay - 1 > max_delay) { PrintFailedCheck("First interval at source has higher than maximum delay: infeasible solution!"); }
            if (intervals[graph.source.index_in_graph].sorted_array[0].weight_to_terminal != _upper_bound_weight) { PrintFailedCheck("First interval at source does not have the best weight to terminal: did we forget to update _upper_bound_weight?"); }
        }
#endif
        // reconstruct the best path from source to terminal using the predecessor structures
        _upper_bound_path = get_path_from_interval_predecessors(predecessors, intervals);
#if TEST
        if (check)
        {
            if (get_path_weight(_upper_bound_path) != _upper_bound_weight) { PrintFailedCheck($"Final path weight {get_path_weight(_upper_bound_path)} differs from reported final weight {_upper_bound_weight}"); }
            if (get_path_delay(_upper_bound_path) > max_delay) { PrintFailedCheck($"Final path delay {get_path_delay(_upper_bound_path)} infeasible {max_delay}"); }
        }
#endif
        return new Solution(_feasibility, null, new PrimalSolution(_upper_bound_weight, get_path_delay(_upper_bound_path), _upper_bound_path)
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
    }

    #endregion

    #region algorithmic step functions

    /// <summary>
    /// Does a depth first traversal carrying accumulated (weight,delay) from source as in label setting
    /// Keeps a set of the none-dominated (weight,delay) pairs derived after all children of a node are visited in dfs, before backtracking
    /// from successor domination to prune future labels on dominance
    /// </summary>
    /// <param name="node">the node currently being visited</param>
    /// <param name="label">the accumulated (weight,delay) from source to this node</param>
    /// <param name="node_dominators">the array of none-dominated (weight,delay) pairs for each node</param>
    /// <param name="least_delay_to_terminal">the least delay from each node to the terminal</param>
    /// <param name="shortest_weight_to_terminal">the shortest weight from each node to the terminal</param>
    /// <param name="path">the current path from source to this node</param>
    /// <param name="workspaces">the workspaces for temporary storage during DFS</param>
    /// <param name="token">the cancellation token to observe</param>
    int dfs_back_dominance(Node node, Distances label, SortedLabelArray[] node_dominators, int[] least_delay_to_terminal, int[] shortest_weight_to_terminal, Edge[] path, int[][] workspaces, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#if TEST
        if (verbose)
        {
            Console.WriteLine($"> Label ({label.weight},{label.delay}) entering node {node.index_in_graph}");
        }
#endif
        ref SortedLabelArray dominators = ref node_dominators[node.index_in_graph];
        int idx = label.delay + least_delay_to_terminal[node.index_in_graph] > max_delay  // quick check for infeasibility pruning
                ? dominators.array_size - 1                                               // infeasible interval always last one
                : dominators.predecessor(label.delay, new DistancesDelayComparer());      // else do a binary search
        if (idx >= 0 && dominators.sorted_array[idx].weight <= label.weight)
        {
#if TEST
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} dominated by ({dominators.sorted_array[idx].weight},{dominators.sorted_array[idx].delay})");
            }
#endif
            // label is dominated
            return idx;
        }

        // construct a (weight,delay) pair that will be used to prune future labels,
        // it will be calculated from the successor dominators
        Distances dominator = new Distances()
        {
            delay = int.MinValue,
            weight = int.MinValue
        };

        if (node == graph.terminal)
        {
            // terminal reached
            ++_iteration;

            // at the terminal, each none-dominated label is returned as itself
            dominator = label;

            // update the best solution so far...
            if (label.weight < _upper_bound_weight)
            {
#if TEST
                if (check)
                {
                    if (get_path_weight(path) != label.weight) { PrintFailedCheck($"DFS path weight {get_path_weight(path)} differs from {label.weight}"); }
                    if (get_path_delay(path) != label.delay) { PrintFailedCheck($"DFS path delay {get_path_delay(path)} differs from {label.delay}"); }
                }
#endif
                Array.Copy(path, _upper_bound_path, graph.number_of_layers);
                set_primal_solution(label.weight, label.delay, _upper_bound_path);
            }
#if PRINT_PROGRESS
            else
            {
                PrintIteration(label.weight, label.delay, Feasibility.feasible);
            }
#endif
        }
        else // none terminal case
        {
            int[] workspace = workspaces[node.index_of_layer];

            // recurse
            for (int e = 0; e < node.number_of_edges_out; ++e)
            {
                Edge edge = node.edges_out[e];
                Node node_bottom = edge.node_out(graph);
#if INSTRUMENTATION
                ++counters.number_of_labels; // label created, immediately discarded by lower delay bound, or expanded
#endif
                // weight pruning
                if (label.weight + weight[node.index_in_graph][edge.index_in_node_in] + shortest_weight_to_terminal[node_bottom.index_in_graph] >= _upper_bound_weight)
                {
                    continue;
                }
                else
                {
                    // go into successor node, and store the index of the dominator returned
                    path[node_bottom.index_of_layer] = edge; // track the edge to reconstruct the path when reaching the terminal

                    workspace[e] = dfs_back_dominance(node_bottom,
                        new Distances()
                        {
                            delay = label.delay + delay[node.index_in_graph][edge.index_in_node_in],
                            weight = label.weight + weight[node.index_in_graph][edge.index_in_node_in]
                        }, node_dominators, least_delay_to_terminal, shortest_weight_to_terminal, path, workspaces, token);
                }
            }

            for (int e = 0; e < node.number_of_edges_out; ++e)
            {
                Edge edge = node.edges_out[e];

                // weight pruned?
                if (label.weight + weight[node.index_in_graph][edge.index_in_node_in] + shortest_weight_to_terminal[edge.index_in_graph_of_node_out] >= _upper_bound_weight)
                {
                    // act as if it is dominated by (_upper_bound_weight- shortest_weight_to_terminal, int.MinValue)
                    if (dominator.weight < _upper_bound_weight - weight[node.index_in_graph][edge.index_in_node_in] - shortest_weight_to_terminal[edge.index_in_graph_of_node_out])
                    {
                        dominator.weight = _upper_bound_weight - weight[node.index_in_graph][edge.index_in_node_in] - shortest_weight_to_terminal[edge.index_in_graph_of_node_out];
                    }
                }
                else
                {
                    // shift back to the correct index after the underlying sorted array may have changed if the same node is visited by multiple edges
                    int successor_index = workspace[e];
                    ref SortedLabelArray successor_dominators = ref node_dominators[edge.index_in_graph_of_node_out];
                    while (successor_index >= 0 && (successor_index >= successor_dominators.array_size || successor_dominators.sorted_array[successor_index].delay > label.delay + delay[node.index_in_graph][edge.index_in_node_in])) --successor_index;
                    while (successor_index + 1 < successor_dominators.array_size && successor_dominators.sorted_array[successor_index + 1].delay <= label.delay + delay[node.index_in_graph][edge.index_in_node_in]) ++successor_index;
                    ref Distances successor_label = ref successor_dominators.sorted_array[successor_index];

                    // construct the dominator as the piece-wise max of the successor dominators
                    // dominator.delay = max(successor delay - edge delay)
                    // dominator.weight = max(successor weight - edge weight)
                    // example: label (5,6) at node v with edge (1,2) to node u is dominated by (4,2) at node u
                    //                                with edge (3,1) to node u is dominated by (4,2) at node u
                    //          then we know that any label arriving at node v dominated by (max[4-1,4-3],max[2-2,2-1]) = (3,1)
                    //          will be dominated at node u by every expansion from v to u: (3,1)+(1,2)>=(4,2) and (3,1)+(3,1)>=(4,2)  
                    if (dominator.delay < successor_label.delay - delay[node.index_in_graph][edge.index_in_node_in])
                    {
                        dominator.delay = successor_label.delay - delay[node.index_in_graph][edge.index_in_node_in];
                    }
                    if (dominator.weight + weight[node.index_in_graph][edge.index_in_node_in] < successor_label.weight)
                    {
                        dominator.weight = successor_label.weight - weight[node.index_in_graph][edge.index_in_node_in];
                    }
                }
            }
        }

        // add the dominator to the dominating set
        idx = dominators.predecessor(dominator.delay, new DistancesDelayComparer());
        if (idx >= 0 && dominators.sorted_array[idx].delay == dominator.delay)
        {
            dominators.sorted_array[idx] = dominator;
        }
        else
        {
            ensure(ref dominators.sorted_array, dominators.array_size + 1);
            dominators.insert(++idx, dominator);
        }
        ++idx;
        while (idx < dominators.array_size && dominators.sorted_array[idx].weight >= dominator.weight)
        {
            dominators.remove(idx);
        }
#if TEST
        if (verbose)
        {
            Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} with dominator ({dominator.weight},{dominator.delay})");
            Console.Write($"Dominators:");
            for (int k = 0; k < dominators.array_size; ++k)
                Console.Write($" ({dominators.sorted_array[k].weight},{dominators.sorted_array[k].delay})");
            Console.Write(Environment.NewLine);
        }
        if (check)
        {
            if (dominator.delay > label.delay || dominator.weight > label.weight) { PrintFailedCheck($"Label ({label.weight},{label.delay}) should be dominated by dominator ({dominator.weight},{dominator.delay})"); }
            if (dominator.delay != dominators.sorted_array[idx - 1].delay || dominator.weight != dominators.sorted_array[idx - 1].weight) { PrintFailedCheck($"Returned index ({idx - 1}) should point to the just constructed dominator ({dominator.weight},{dominator.delay})"); }
            for (int k = 1; k < dominators.array_size; ++k)
            {
                if (dominators.sorted_array[k - 1].delay >= dominators.sorted_array[k].delay) { PrintFailedCheck("Node labels not properly sorted on delay"); }
                if (dominators.sorted_array[k - 1].weight <= dominators.sorted_array[k].weight) { PrintFailedCheck("Dominated label: increasing delay should mean decreasing weight"); }
            }
        }
#endif
        return idx - 1;
    }

    /// <summary>
    /// Recursively explore the graph from node to terminal, computing equivalence classes of labels that can reach the terminal with the same best weight
    /// </summary>
    /// <param name="node">next node to explore</param>
    /// <param name="label">label representing a partial path from source to the current node</param>
    /// <param name="node_intervals">cache of previously computed intervals for each node</param>
    /// <param name="least_delay_to_terminal">array of the least delay from each node to the terminal</param>
    /// <param name="node_predecessors">predecessor structures for path reconstruction</param>
    /// <param name="token">cancellation token</param>
    /// <returns>index of the interval that the label falls into</returns>
    private int dfs_delay_completion_equivalence(Node node, Distances label, SortedIntervalArray[] node_intervals, int[] least_delay_to_terminal, Predecessor[][] node_predecessors, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#if TEST
        if (verbose)
        {
            Console.WriteLine($"> Label ({label.weight},{label.delay}) entering node {node.index_in_graph}");
        }
#endif
        ref SortedIntervalArray intervals = ref node_intervals[node.index_in_graph];

        // check if label.delay falls in an already known interval
        int index = label.delay + least_delay_to_terminal[node.index_in_graph] > max_delay // quick check for infeasibility pruning
            ? intervals.array_size - 1                                                     // infeasible interval always last one
            : intervals.predecessor(label.delay, new IntervalLowDelayComparer());          // else do a binary search
        if (index >= 0 && label.delay < intervals.sorted_array[index].high_delay)
        {
#if TEST
            if (check)
            {
                if (label.delay < intervals.sorted_array[index].low_delay) { PrintFailedCheck($"Label predecessor search returned an invalid interval: {intervals.sorted_array[index].low_delay} <= {label.delay} < {intervals.sorted_array[index].high_delay}"); }
            }
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} with label delay in interval [{intervals.sorted_array[index].low_delay},{intervals.sorted_array[index].high_delay})");
            }
#endif
            // update the best solution so far...
            if (index != intervals.array_size - 1 // infeasible case, last interval, no need to update the solution
                && label.weight + intervals.sorted_array[index].weight_to_terminal < _upper_bound_weight)
            {
                ++_iteration;
                set_primal_solution(label.weight + intervals.sorted_array[index].weight_to_terminal, label.delay + intervals.sorted_array[index].high_delay - 1);
            }

            return index;
        }
#if TEST
        if (check)
        {
            if (node == graph.terminal) { PrintFailedCheck($"All labels reaching the terminal should fall in the pre-initialized intervals: {intervals.sorted_array[0].low_delay} <= {label.delay} < {intervals.sorted_array[0].high_delay}"); }
        }
#endif
        {
            // Construct a new interval from the children
            Interval interval = new Interval()
            {
                high_delay = int.MaxValue,
                low_delay = int.MinValue,
                weight_to_terminal = int.MaxValue
            };
            Predecessor pred = new Predecessor();

            // recurse to children
            for (int e = 0; e < node.number_of_edges_out; ++e)
            {
                Edge edge = node.edges_out[e];
                Node node_bottom = edge.node_out(graph);
#if INSTRUMENTATION
                ++counters.number_of_labels; // label expanded along edge
#endif
                int successor_index = dfs_delay_completion_equivalence(node_bottom,
                    new Distances()
                    {
                        weight = label.weight + weight[node.index_in_graph][edge.index_in_node_in],
                        delay = label.delay + delay[node.index_in_graph][edge.index_in_node_in]
                    }, node_intervals, least_delay_to_terminal, node_predecessors, token);
                ref Interval successor_interval = ref node_intervals[node_bottom.index_in_graph].sorted_array[successor_index];

                // set U to the min U of all children
                if (interval.high_delay > successor_interval.high_delay - delay[node.index_in_graph][edge.index_in_node_in])
                {
                    interval.high_delay = successor_interval.high_delay - delay[node.index_in_graph][edge.index_in_node_in];
                }
                // set weight to terminal to min weight to terminal of all children
                if (successor_interval.weight_to_terminal < interval.weight_to_terminal - weight[node.index_in_graph][edge.index_in_node_in])
                {
                    interval.weight_to_terminal = successor_interval.weight_to_terminal + weight[node.index_in_graph][edge.index_in_node_in];

                    // store the edge and successor interval that led to the best weight completion to terminal for this interval
                    pred = new Predecessor() {
                        index_of_edge = edge.index_in_node_in,
                        k_pred = successor_interval.low_delay   // store the low_delay of the successor interval as this is a stable identifier, the index changes as new intervals are added to the successor node
                    };
                }
                // set L to the max L of all children
                if (successor_interval.low_delay > interval.low_delay + delay[node.index_in_graph][edge.index_in_node_in])
                {
                    interval.low_delay = successor_interval.low_delay - delay[node.index_in_graph][edge.index_in_node_in];
                }
            }

            // cache interval and return it
            index = intervals.successor(interval);

            ensure(ref intervals.sorted_array, intervals.array_size + 1);
            intervals.insert(index, interval);
#if TEST
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} with label in newly created interval [{interval.low_delay},{interval.high_delay}) and {interval.weight_to_terminal} weight to terminal");
            }
            if (check)
            {
                if (label.delay < interval.low_delay || label.delay >= interval.high_delay) { PrintFailedCheck($"Label delay should always fall in created interval {interval.low_delay} <= {label.delay} < {interval.high_delay}"); }
                for (int k = 1; k < intervals.array_size; ++k)
                {
                    if (intervals.sorted_array[k - 1].low_delay > intervals.sorted_array[k].low_delay) { PrintFailedCheck("intervals not properly sorted on low_delay"); }
                    if (intervals.sorted_array[k - 1].high_delay > intervals.sorted_array[k].low_delay) { PrintFailedCheck("intervals overlapping"); }
                }
            }
#endif
            // update the best solution so far...
            if (label.weight + intervals.sorted_array[index].weight_to_terminal < _upper_bound_weight)
            {
                ++_iteration;
                set_primal_solution(label.weight + intervals.sorted_array[index].weight_to_terminal, label.delay + intervals.sorted_array[index].high_delay - 1);
            }

            // cache the predecessor for path reconstruction
            ref Predecessor[] predecessors = ref node_predecessors[node.index_in_graph];
            ensure(ref predecessors, intervals.array_size - 1);
            insert(ref predecessors, intervals.array_size - 2, index, pred);

            return index;
        }
    }

    /// <summary>
    /// Recursively explore the graph from node to terminal, computing equivalence classes of labels that can reach the terminal with the same best weight
    /// </summary>
    /// <param name="node">next node to explore</param>
    /// <param name="label">label representing a partial path from source to the current node</param>
    /// <param name="node_intervals">cache of previously computed intervals for each node</param>
    /// <param name="least_delay_to_terminal">array of the least delay from each node to the terminal</param>
    /// <param name="shortest_weight_to_terminal">array of the shortest weight from each node to the terminal</param>
    /// <param name="node_heaps">workspace for each node to store intermediate results from the recursive calls to children</param>
    /// <returns>index of the interval that the label falls into</returns>
    private int dfs_weight_completion_equivalence(Node node, Distances label, SortedIntervalArray[] node_intervals, int[] least_delay_to_terminal, int[] shortest_weight_to_terminal, IntLabelHeap[] node_heaps, Predecessor[][] node_predecessors, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#if TEST
        if (verbose)
        {
            Console.WriteLine($"> Label ({label.weight},{label.delay}) entering node {node.index_in_graph}");
        }
#endif
        ref SortedIntervalArray intervals = ref node_intervals[node.index_in_graph];

        // check if label.delay falls in an already known interval
        int index = label.delay + least_delay_to_terminal[node.index_in_graph] > max_delay // quick check for infeasibility pruning
            ? intervals.array_size - 1                                                     // infeasible interval always last one
            : intervals.predecessor(label.delay, new IntervalLowDelayComparer());          // else do a binary search
        if (index >= 0 && label.delay < intervals.sorted_array[index].high_delay)
        {
#if TEST
            if (check)
            {
                if (label.delay < intervals.sorted_array[index].low_delay) { PrintFailedCheck($"Label predecessor search returned an invalid interval: {intervals.sorted_array[index].low_delay} <= {label.delay} < {intervals.sorted_array[index].high_delay}"); }
            }
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} with label delay in interval [{intervals.sorted_array[index].low_delay},{intervals.sorted_array[index].high_delay})->{intervals.sorted_array[index].weight_to_terminal}");
            }
#endif
            // update the best solution so far...
            if (index != intervals.array_size - 1 // infeasible case, last interval, no need to update the solution
                && label.weight + intervals.sorted_array[index].weight_to_terminal < _upper_bound_weight)
            {
                ++_iteration;
                set_primal_solution(label.weight + intervals.sorted_array[index].weight_to_terminal, label.delay + intervals.sorted_array[index].high_delay - 1);
            }

            return index;
        }
#if TEST
        if (check)
        {
            if (node == graph.terminal) { PrintFailedCheck($"All labels reaching the terminal should fall in the pre-initialized intervals: {intervals.sorted_array[0].low_delay} <= {label.delay} < {intervals.sorted_array[0].high_delay}"); }
        }
#endif
        {
            // Construct a new interval from the children
            Interval interval = new Interval()
            {
                high_delay = int.MinValue,
                low_delay = int.MinValue,
                weight_to_terminal = int.MaxValue
            };
            Predecessor pred = new Predecessor();

            // place each out edge into a max heap
            // that has the highest low_delay on top
            ref IntLabelHeap max_heap = ref node_heaps[node.index_of_layer];
            max_heap.heap_size = node.number_of_edges_out;

            // recurse to children
            for (int e = 0; e < node.number_of_edges_out; ++e)
            {
                Edge edge = node.edges_out[e];
                Node node_bottom = edge.node_out(graph);
#if INSTRUMENTATION
                ++counters.number_of_labels; // label expanded along edge
#endif
                int successor_index = dfs_weight_completion_equivalence(node_bottom,
                    new Distances()
                    {
                        weight = label.weight + weight[node.index_in_graph][edge.index_in_node_in],
                        delay = label.delay + delay[node.index_in_graph][edge.index_in_node_in]
                    }, node_intervals, least_delay_to_terminal, shortest_weight_to_terminal, node_heaps, node_predecessors, token);
                ref Interval successor_interval = ref node_intervals[node_bottom.index_in_graph].sorted_array[successor_index];

                // set U to the U of the feasible path with the lowest completion weight to terminal
                // as above U the lowest feasible completion weight for this label becomes infeasible
                if (successor_interval.weight_to_terminal < interval.weight_to_terminal - weight[node.index_in_graph][edge.index_in_node_in]
                    || (successor_interval.weight_to_terminal == interval.weight_to_terminal - weight[node.index_in_graph][edge.index_in_node_in]
                        && interval.high_delay < successor_interval.high_delay - delay[node.index_in_graph][edge.index_in_node_in]))
                {
                    interval.high_delay = successor_interval.high_delay - delay[node.index_in_graph][edge.index_in_node_in];
                    interval.weight_to_terminal = successor_interval.weight_to_terminal + weight[node.index_in_graph][edge.index_in_node_in];

                    // store the edge and successor interval that led to the best weight completion to terminal for this interval
                    pred = new Predecessor()
                    {
                        index_of_edge = edge.index_in_node_in,
                        k_pred = successor_interval.low_delay   // store the low_delay of the successor interval as this is a stable identifier, the index changes as new intervals are added to the successor node
                    };
                }

                // cache the successor identifier for later processing,
                // to get the lower bound on the low_delay of the interval we first need the best feasible completion weight to terminal,
                // which we only know after the first loop
                max_heap.heap[e] = new Label<int>()
                {
                    // interval ordered by low_delay
                    dist = successor_interval.low_delay == int.MinValue ? int.MinValue : successor_interval.low_delay - delay[node.index_in_graph][edge.index_in_node_in],
                    pred = { index_of_edge = e, k_pred = successor_index }  // keep which edge and interval at node_bottom this was derived from
                };
            }
            max_heap.heapify();

            // iterate over each edge again to get the lower bound from weights to terminal that are better than the best feasible completion found in previous loop
            // but that are not feasible for this label's delay
            while (max_heap.heap_size > 0)
            {
                // peek top of heap
                ref Label<int> max_low = ref max_heap.heap[0];

                // get the corresponding interval
                Edge edge = node.edges_out[max_low.pred.index_of_edge];
                Node node_bottom = edge.node_out(graph);
                int successor_index = max_low.pred.k_pred;
                Interval[] successor_intervals = node_intervals[node_bottom.index_in_graph].sorted_array;

                // when the weight to terminal of the preceding interval drops below the smallest completion weight found in the first loop
                // we found the largest L low_delay
                if (interval.weight_to_terminal > (successor_index <= 0
                        ? shortest_weight_to_terminal[node_bottom.index_in_graph]
                        : successor_intervals[successor_index - 1].weight_to_terminal) + weight[node.index_in_graph][edge.index_in_node_in])
                {
                    interval.low_delay = max_low.dist;
                    break;
                }

                // enqueue the next interval along the same edge
                if (--max_low.pred.k_pred >= 0)
                {
                    ref Interval successor_interval = ref node_intervals[node_bottom.index_in_graph].sorted_array[max_low.pred.k_pred];
                    max_low.dist = successor_interval.low_delay == int.MinValue ? int.MinValue : successor_interval.low_delay - delay[node.index_in_graph][edge.index_in_node_in];
                    max_heap.sift_down(0);
                }
                else
                {
                    // no more intervals
                    max_heap.dequeue();
                }
            }

            // cache interval and return it
            index = intervals.successor(interval);
#if TEST
            if (check)
            {
                if (index >= intervals.array_size) { PrintFailedCheck("At least the infeasible interval should be succeeding the new interval"); }
            }
#endif
            if (interval.high_delay == intervals.sorted_array[index].high_delay)
            {
                // this can happen because we base the lower bound of an interval on an estimate of the possible least weight to terminal
                // if this estimate later turns out to have been too positive, the first interval can be extended to an even lower bound
                // merge the intervals by removing the old underestimated interval and having the new interval supersede it
#if TEST
                if (check)
                {
                    if (interval.weight_to_terminal != intervals.sorted_array[index].weight_to_terminal) { PrintFailedCheck($"Merging intervals with different weights to terminal: {interval.weight_to_terminal} != {intervals.sorted_array[index].weight_to_terminal}"); }
                    if (interval.low_delay >= intervals.sorted_array[index].low_delay) { PrintFailedCheck($"Merging intervals with wrong order: {interval.low_delay} >= {intervals.sorted_array[index].low_delay}"); }
                }
#endif
                intervals.sorted_array[index] = interval;

                // cache the predecessor for path reconstruction
                ref Predecessor[] predecessors = ref node_predecessors[node.index_in_graph];
                predecessors[index] = pred;

            }
            else
            {
                ensure(ref intervals.sorted_array, intervals.array_size + 1);
                intervals.insert(index, interval);

                // cache the predecessor for path reconstruction
                ref Predecessor[] predecessors = ref node_predecessors[node.index_in_graph];
                ensure(ref predecessors, intervals.array_size - 1);
                insert(ref predecessors, intervals.array_size - 2, index, pred);
            }
#if TEST
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} with label in newly created interval [{interval.low_delay},{interval.high_delay})->{interval.weight_to_terminal}");
                Console.Write($"Intervals:");
                for (int k = 0; k < intervals.array_size; ++k)
                    Console.Write($" ([{intervals.sorted_array[k].low_delay},{intervals.sorted_array[k].high_delay})->{intervals.sorted_array[k].weight_to_terminal})");
                Console.Write(Environment.NewLine);
            }
            if (check)
            {
                if (label.delay < interval.low_delay || label.delay >= interval.high_delay) { PrintFailedCheck($"Label delay should always fall in created interval {interval.low_delay} <= {label.delay} < {interval.high_delay}"); }
                for (int k = 0; k < intervals.array_size; ++k)
                {
                    if (intervals.sorted_array[k].low_delay > intervals.sorted_array[k].high_delay) { PrintFailedCheck("low delay should be smaller than high delay"); }

                    if (k == intervals.array_size - 1) continue; // skip the infeasible interval
                    int best_feasible_weight = find_best_feasible_weight(node, new Distances() { delay = intervals.sorted_array[k].low_delay, weight = 0 });
                    if (best_feasible_weight != intervals.sorted_array[k].weight_to_terminal) { PrintFailedCheck($"Best feasible weight {best_feasible_weight} should be {intervals.sorted_array[k].weight_to_terminal}"); }
                    if (best_feasible_weight != find_best_feasible_weight(node, new Distances() { delay = intervals.sorted_array[k].high_delay - 1, weight = 0 })) { PrintFailedCheck($"Best feasible {best_feasible_weight} should be consistent across interval [{intervals.sorted_array[k].low_delay},{intervals.sorted_array[k].high_delay})"); }
                    if (best_feasible_weight >= find_best_feasible_weight(node, new Distances() { delay = intervals.sorted_array[k].high_delay, weight = 0 })) { PrintFailedCheck($"Best feasible {best_feasible_weight} should get worse if delay increases beyond {intervals.sorted_array[k].high_delay}"); }
                }
                for (int k = 1; k < intervals.array_size; ++k)
                {
                    if (intervals.sorted_array[k - 1].low_delay > intervals.sorted_array[k].low_delay) { PrintFailedCheck("intervals not properly sorted on low_delay"); }
                    if (intervals.sorted_array[k - 1].high_delay > intervals.sorted_array[k].low_delay) { PrintFailedCheck("intervals overlapping"); }
                    if (intervals.sorted_array[k - 1].weight_to_terminal >= intervals.sorted_array[k].weight_to_terminal) { PrintFailedCheck("Interval dominated: decreasing delay should mean increasing weight"); }
                }
            }
#endif
            // update the best solution so far...
            if (label.weight + intervals.sorted_array[index].weight_to_terminal < _upper_bound_weight)
            {
                ++_iteration;
                set_primal_solution(label.weight + intervals.sorted_array[index].weight_to_terminal, label.delay + intervals.sorted_array[index].high_delay - 1);
            }

            return index;
        }
    }

    /// <summary>
    /// Recursively reconstruct the path from source to terminal using the predecessor information stored in the intervals
    /// </summary>
    /// <param name="predecessors"></param>
    /// <param name="node_intervals"></param>
    /// <param name="path"></param>
    private void path_from_interval_predecessors(Predecessor[][] predecessors, SortedIntervalArray[] node_intervals, Edge[] path)
    {
        Node node = graph.source;
        int index = 0;

        for (int l = 1; l < graph.number_of_layers; ++l)
        {
            ref Predecessor pred = ref predecessors[node.index_in_graph][index];
            Edge edge = node.edges_out[pred.index_of_edge];
            path[l] = edge;
            node = edge.node_out(graph);
            index = node_intervals[node.index_in_graph].predecessor(pred.k_pred, new IntervalLowDelayComparer());
        }
    }
    private Edge[] get_path_from_interval_predecessors(Predecessor[][] node_predecessors, SortedIntervalArray[] node_intervals)
    {
        Edge[] path = new Edge[graph.number_of_layers];
        path_from_interval_predecessors(node_predecessors, node_intervals, path);
        return path;
    }

    private void insert<T>(ref T[] array, int array_size, int index, T target)
    {
        Array.Copy(array, index, array, index + 1, array_size - index);
        array[index] = target;
    }

#if TEST
    /// <summary>
    /// find the best feasible weight to terminal starting at node with label (weight, delay)
    /// a simple dynamic programming implementation...
    /// </summary>
    /// <param name="node"></param>
    /// <param name="label"></param>
    /// <returns></returns>
    int find_best_feasible_weight(Node node, Distances label)
    {
        if (node == graph.terminal)
        {
            return label.weight;
        }

        int best_feasible_weight = int.MaxValue;

        for (int e = 0; e < node.number_of_edges_out; ++e)
        {
            Edge edge = node.edges_out[e];
            Node node_bottom = edge.node_out(graph);

            if (label.delay == int.MinValue || label.delay + delay[node.index_in_graph][edge.index_in_node_in] <= max_delay)
            {
                int best_feasible_weight_along_edge = find_best_feasible_weight(node_bottom, new Distances()
                {
                    delay = label.delay == int.MinValue ? int.MinValue : label.delay + delay[node.index_in_graph][edge.index_in_node_in],
                    weight = label.weight + weight[node.index_in_graph][edge.index_in_node_in]
                });
                if (best_feasible_weight_along_edge < best_feasible_weight)
                {
                    best_feasible_weight = best_feasible_weight_along_edge;
                }
            }
        }

        return best_feasible_weight;
    }
#endif
    #endregion
}
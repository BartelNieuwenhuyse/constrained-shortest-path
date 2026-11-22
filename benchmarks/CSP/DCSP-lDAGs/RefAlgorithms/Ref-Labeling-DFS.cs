namespace DelayConstrainedShortestPath.References;

using DataStructures;
using DelayConstrainedShortestPath;

#region helper classes

using SortedLabelArray = DataStructures.SortedArray<Distances, DistancesComparer
#if INSTRUMENTATION
    , DataStructures.SortedArrayOpsCounter<Distances>
#endif
    >;

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
    /// Keeps a set of the none-dominated labels that reached a node so far to prune future labels on dominance
    /// </summary>
    /// <param name="incumbent">the best solution found so far</param>
    public Solution run_dfs_dominance(int incumbent, CancellationToken token)
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
        // to be used for feasibility pruning (label's delay + least_delay_to_terminal > max_delay)
        (int delay_path, int weight_of_delay_path) = least_delay_from_terminal(delay_to_terminal, path);

        if (delay_path > max_delay)
        {
            // infeasible, there is no path with a delay <= max_delay
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, delay_path, Feasibility.infeasible);
#endif
            return new Solution(Feasibility.infeasible, null, null
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

        // none-dominated labels
        SortedLabelArray[] node_labels = new SortedLabelArray[graph.number_of_nodes];
        for (int n = 0; n < graph.number_of_nodes; ++n)
        {
            node_labels[n] = new SortedLabelArray(0, new DistancesComparer()
#if INSTRUMENTATION
                , new SortedArrayOpsCounter<Distances>((int n) => counters.number_of_ops += n, () => ++counters.number_of_ops)
#endif
#if TEST
                , check
#endif
            );
        }

        // start the depth first traversal from the source node with label (0,0)
#if INSTRUMENTATION
        ++counters.number_of_labels; // label initialized at source
#endif
        dfs_dominance(graph.source, new Distances() { weight = 0, delay = 0 }, node_labels, delay_to_terminal, weight_to_terminal, path, token);
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
    public Solution run_dfs_dominance(CancellationToken token)
    {
        return run_dfs_dominance(int.MaxValue, token);
    }

    /// <summary>
    /// find the optimal solution (or proof optimality of the solution)
    /// by enumerating paths in depth first traversal order
    ///     whose lower bound is smaller than the upper bound
    ///         lower bound = aggregate cost (weight + lambda * (delay - lambda * max_delay) 
    ///         upper bound = smallest weight found so far
    ///     with feasible delay (delay <= max_delay)
    ///     and better primal weight (weight <= smallest weight found so far)
    /// </summary>
    /// <param name="incumbent">the best solution found so far</param>
    /// <param name="lambda">the Lagrangian multiplier for the delay constraint</param>
    public Solution run_dfs_dual(double lambda, int incumbent, CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 0;
#if INSTRUMENTATION
        counters.number_of_labels = 0;
#endif
        // data structures
        double[] cost_to_terminal = new double[graph.number_of_nodes];      // shortest distance from each node to the terminal for cost (weight + lambda * delay)
        int[] delay_to_terminal = new int[graph.number_of_nodes];           // shortest distance from each node to the terminal for cost (delay)
        int[] weight_to_terminal = new int[graph.number_of_nodes];          // shortest distance from each node to the terminal for cost (weight)
        Edge[] path = new Edge[graph.number_of_nodes];                      // track edges to reconstruct the actual path during the depth first traversal

        // calculate the shortest node-to-terminal distances
        // in distance metric (delay)
        // to be used for feasibility pruning (label's delay + least_delay_to_terminal > max_delay)
        (int delay_path, int weight_of_delay_path) = least_delay_from_terminal(delay_to_terminal, path);

        if (delay_path > max_delay)
        {
            // infeasible, there is no path with a delay <= max_delay
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, delay_path, Feasibility.infeasible);
#endif
            return new Solution(Feasibility.infeasible, null, null
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

        // calcate the shortest node-to-terminal distances
        // in distance metric (weight + lambda * delay)
        // to be used for Lagrangian bound pruning (label's weight + lambda * label's delay + least_cost_to_terminal > _upper_bound_weight + lambda * max_delay)
        (_, int weight_of_cost_path, int delay_of_cost_path) = shortest_cost_from_terminal(lambda, cost_to_terminal, path);

        if (delay_of_cost_path <= max_delay && weight_of_cost_path <= _upper_bound_weight)
        {
            // least cost path is feasible, update the incumbent
            path_from_source(path, _upper_bound_path);
            set_primal_solution(weight_of_cost_path, delay_of_cost_path, _upper_bound_path);
#if TEST
            if (check)
            {
                if (get_path_weight(_upper_bound_path) != _upper_bound_weight) { PrintFailedCheck($"Path weight {get_path_weight(_upper_bound_path)} differs from {_upper_bound_weight}"); }
                if (get_path_delay(_upper_bound_path) != _upper_bound_delay) { PrintFailedCheck($"Path delay {get_path_delay(_upper_bound_path)} differs from {_upper_bound_delay}"); }
            }
#endif
        }
#if INSTRUMENTATION
        ++counters.number_of_labels; // label (0,0) at the source node
#endif
        // enumerate all paths using depth-first traversal
        // within weight, delay and aggregated cost bounds
        dfs_dual(graph.source, -lambda * max_delay // lower_bound = (weight + lambda * delay) - lambda * max_delay, so we already subtract the constant factor
                , 0, 0, lambda, cost_to_terminal, delay_to_terminal, weight_to_terminal, path, token);
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

    #endregion

    #region algorithmic step functions

    /// <summary>
    /// Does a depth first traversal carrying accumulated (weight,delay) from source as in label setting
    /// Keeps a set of the none-dominated labels that reached a node so far to prune future labels on dominance
    /// </summary>
    /// <param name="node">The current node being visited</param>
    /// <param name="label">The accumulated (weight, delay) label</param>
    /// <param name="node_labels">The sorted array of none-dominated labels for each node (for dominance pruning)</param>
    /// <param name="least_delay_to_terminal">The least delay to the terminal for each node (for feasibility pruning)</param>
    /// <param name="shortest_weight_to_terminal">The shortest weight to the terminal for each node (for weight bound pruning)</param>
    /// <param name="path">The array to store the predecessor edges for the current path</param>
    /// <param name="token">The cancellation token</param>
    void dfs_dominance(Node node, Distances label, SortedLabelArray[] node_labels, int[] least_delay_to_terminal, int[] shortest_weight_to_terminal, Edge[] path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#if TEST
        if (verbose)
        {
            Console.WriteLine($"> Label ({label.weight},{label.delay}) entering node {node.index_in_graph}");
        }
#endif
        ref SortedLabelArray labels = ref node_labels[node.index_in_graph];
        int idx = labels.predecessor(label.delay, new DistancesDelayComparer());
        if (idx >= 0 && labels.sorted_array[idx].weight <= label.weight)
        {
            // label is dominated
#if TEST
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph} dominated by ({labels.sorted_array[idx].weight},{labels.sorted_array[idx].delay})");
            }
#endif
            return;
        }

        // add the label to the pareto front
        if (idx >= 0 && labels.sorted_array[idx].delay == label.delay)
        {
            labels.sorted_array[idx] = label;
        }
        else
        {
            ensure(ref labels.sorted_array, labels.array_size + 1);
            labels.insert(++idx, label);
        }
        ++idx;
        while (idx < labels.array_size && labels.sorted_array[idx].weight >= label.weight)
        {
            labels.remove(idx);
        }
#if TEST
        if (check)
        {
            for (int k = 1; k < labels.array_size; ++k)
            {
                if (labels.sorted_array[k - 1].delay >= labels.sorted_array[k].delay) { PrintFailedCheck("Node labels not properly sorted on delay"); }
                if (labels.sorted_array[k - 1].weight <= labels.sorted_array[k].weight) { PrintFailedCheck("Dominated label: increasing delay should mean decreasing weight"); }
            }
        }
#endif
        if (node == graph.terminal)
        {
            // terminal reached
            ++_iteration;

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
#if TEST
            if (verbose)
            {
                Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph}");
                Console.Write($"None dominated labels:");
                for (int k = 0; k < labels.array_size; ++k)
                    Console.Write($" ({labels.sorted_array[k].weight},{labels.sorted_array[k].delay})");
                Console.Write(Environment.NewLine);
            }
#endif
            return;
        }

        // recurse
        for (int e = 0; e < node.number_of_edges_out; ++e)
        {
            Edge edge = node.edges_out[e];
            Node node_bottom = edge.node_out(graph);
#if INSTRUMENTATION
            ++counters.number_of_labels; // label created, immediately discarded by lower delay bound, or expanded
#endif
            // feasibilty pruning:
            if (label.delay + delay[node.index_in_graph][edge.index_in_node_in] + least_delay_to_terminal[node_bottom.index_in_graph] <= max_delay
                // weight pruning:
                && label.weight + weight[node.index_in_graph][edge.index_in_node_in] + shortest_weight_to_terminal[node_bottom.index_in_graph] < _upper_bound_weight)
            {
                path[node_bottom.index_of_layer] = edge; // track the edge to reconstruct the path when reaching the terminal

                dfs_dominance(node_bottom,
                    new Distances()
                    {
                        delay = label.delay + delay[node.index_in_graph][edge.index_in_node_in],
                        weight = label.weight + weight[node.index_in_graph][edge.index_in_node_in]
                    }, node_labels, least_delay_to_terminal, shortest_weight_to_terminal, path, token);
            }
#if TEST
            else if (verbose)
            {
                Console.WriteLine($"Label ({label.weight + weight[node.index_in_graph][edge.index_in_node_in]},{label.delay + delay[node.index_in_graph][edge.index_in_node_in]}) not reaching node {node_bottom.index_in_graph} pruned by feasibility/incumbent");
            }
#endif
        }
#if TEST
        if (verbose)
        {
            Console.WriteLine($"< Label ({label.weight},{label.delay}) leaving node {node.index_in_graph}");
            Console.Write($"None dominated labels:");
            for (int k = 0; k < labels.array_size; ++k)
                Console.Write($" ({labels.sorted_array[k].weight},{labels.sorted_array[k].delay})");
            Console.Write(Environment.NewLine);
        }
#endif
    }

    /// <summary>
    /// Enumerates all paths whose
    ///     delay <= max_delay
    ///     weight <= max_weigth = upper_bound
    ///     lower_bound = cost - lambda * max_delay <= upper_bound
    /// Update the upper_bound each time a path is found with weight < max_weight
    /// Do this by depth first traversal while only extending paths from any node
    /// if there is at least one weight and delay_to_terminal within the bounds
    /// </summary>
    /// <param name="node">root node for recursion</param>
    /// <param name="lambda">factor in aggregated cost (weight + lambda * delay)</param>
    /// <param name="least_cost_to_terminal">the shortest distance (weight + lambda * delay) from each node to the terminal</param>
    /// <param name="least_delay_to_terminal">the shortest distance (delay) from each node to the terminal</param>
    /// <param name="least_weight_to_terminal">the shortest distance (weight) from each node to the terminal</param>
    /// <param name="path">stores the edges to reconstruct the shortest path</param>
    private void dfs_dual(Node node_top, double dist_cost, int dist_weight, int dist_delay,
        double lambda, double[] least_cost_to_terminal, int[] least_delay_to_terminal, int[] least_weight_to_terminal, Edge[] path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        // only extend path when it promises to give a path within the cost\weight\delay bounds
        if (IsBigger(dist_cost + least_cost_to_terminal[node_top.index_in_graph], _upper_bound_weight)
            || dist_weight + least_weight_to_terminal[node_top.index_in_graph] >= _upper_bound_weight
            || dist_delay + least_delay_to_terminal[node_top.index_in_graph] > max_delay)
        {
            // backtrack, path is infeasible or cannot improve best solution
            return;
        }
#if TEST
        if (verbose)
        {
            Console.WriteLine($"Enter node ({node_top.index_of_layer}, {node_top.index_in_layer}) with cost: {dist_cost}, weight: {dist_weight}, delay: {dist_delay}");
        }
#endif
        if (node_top == graph.terminal)
        {
#if TEST
            if (check)
            {
                if (dist_delay > max_delay) { PrintFailedCheck($"Path enumeration reached terminal, but path delay {dist_delay} is infeasible (> {max_delay})"); }
            }
#endif
            // terminal reached
            ++_iteration;

            // did we find a smaller weight path?
            // then update the upper bound
            if (dist_weight < _upper_bound_weight)
            {
#if TEST
                if (check)
                {
                    if (!IsEqual(get_path_cost(lambda, path), dist_cost + lambda * max_delay)) { PrintFailedCheck($"DFS path cost {get_path_cost(lambda, path)} differs from {dist_cost + lambda * max_delay}"); }
                    if (get_path_weight(path) != dist_weight) { PrintFailedCheck($"DFS path weight {get_path_weight(path)} differs from {dist_weight}"); }
                    if (get_path_delay(path) != dist_delay) { PrintFailedCheck($"DFS path delay {get_path_delay(path)} differs from {dist_delay}"); }
                }
#endif
                Array.Copy(path, _upper_bound_path, graph.number_of_layers);
                set_primal_solution(dist_weight, dist_delay, _upper_bound_path);
            }
#if PRINT_PROGRESS
            else
            {
                PrintIteration(dist_weight, dist_delay, Feasibility.feasible);
            }
#endif
            return; // end recursion
        }

        for (int e = 0; e < node_top.number_of_edges_out; ++e)
        {
            Edge edge = node_top.edges_out[e];
            Node node_bottom = edge.node_out(graph);
#if INSTRUMENTATION
            ++counters.number_of_labels; // label extended along edge
#endif
            path[node_bottom.index_of_layer] = edge; // track the edge to reconstruct the path when reaching the terminal

            // recurse into bottom node
            dfs_dual(node_bottom,
                dist_cost + weight[node_top.index_in_graph][edge.index_in_node_in] + lambda * delay[node_top.index_in_graph][edge.index_in_node_in], // with updated cost
                dist_weight + weight[node_top.index_in_graph][edge.index_in_node_in], // with updated weight
                dist_delay + delay[node_top.index_in_graph][edge.index_in_node_in], // with updated delay
                lambda, least_cost_to_terminal, least_delay_to_terminal, least_weight_to_terminal, path, token);
        }

        return;
    }

    #endregion
}
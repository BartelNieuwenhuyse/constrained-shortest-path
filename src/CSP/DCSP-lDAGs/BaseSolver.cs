using DataStructures;

namespace DelayConstrainedShortestPath;

/// <summary>
/// Base class for solvers of the Delay Constrained Shortest Path problem
/// </summary>
public abstract class BaseSolver
{
    #region data

    /// <summary>
    /// Problem data:
    /// min weight[i,j] * x[i,j]
    /// s.t. delay[i,j] * x[i,j] < max_delay
    /// </summary>
    protected Graph graph;
    protected int[][] weight;
    protected int[][] delay;
    protected int max_delay;

    /// <summary>
    /// Solution
    /// </summary>
    protected Feasibility _feasibility;

    // primal solution
    protected int _upper_bound_weight = int.MaxValue;
    protected int _upper_bound_delay;

    protected void set_primal_solution(int path_weight, int path_delay)
    {
#if PRINT_PROGRESS
        PrintIteration(path_weight, path_delay, Feasibility.feasible);
#endif
        this._upper_bound_weight = path_weight;
        this._upper_bound_delay = path_delay;
    }
#if TEST
    /// <summary>
    /// debuging by automatic checks and printing the problem
    /// </summary>
    protected bool verbose = false;
    protected bool check = false;
#endif
    /// <summary>
    /// Instrumentation counters
    /// </summary>
    protected int _iteration;
#if INSTRUMENTATION
    protected InstrumentationCounters counters = new InstrumentationCounters();
#endif
    #endregion

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="input">The problem input data for the solver</param>
    public BaseSolver(Input input
#if TEST
        , bool verbose
        , bool check
#endif
        )
    {
        this.graph = input.graph;
        this.weight = input.weight;
        this.delay = input.delay;
        this.max_delay = input.max_delay;
#if TEST
        this.verbose = verbose;
        this.check = check;

        if (verbose) { PrintProblem(); }
#endif
    }

    #region algorithmic helpers

    /// <summary>
    /// Shortest distances from each node to the terminal
    /// for distance metric cost (weight + lambda * delay)
    /// </summary>
    /// <param name="lambda">the Lagrangian multiplier for the delay constraint</param>
    /// <param name="shortest_cost_per_node">stores the shortest distance (cost) to the terminal for each node</param>
    /// <returns>shortest source to terminal cost, weight along the shortest cost path, delay along the shortest cost path</returns>
    protected (double, int, int) shortest_cost_from_terminal(double lambda, double[] shortest_cost_per_node)
    {
        // keep distance arrays (least delay and shortest weight)
        int[] delay_top = new int[graph.max_width];       // least delay distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];    // least delay distance to nodes in layer l
        int[] weight_top = new int[graph.max_width];      // shortest weight distance to nodes in layer l + 1 
        int[] weight_bottom = new int[graph.max_width];   // shortest weight distance to nodes in layer l

        weight_bottom[graph.terminal.index_in_layer] = 0; // initialize shortest weight distance of terminal node to 0
        delay_bottom[graph.terminal.index_in_layer] = 0;  // initialize least delay distance of terminal node to 0

        // initialize distances of all nodes to large
        for (int n = 0; n < graph.number_of_nodes; ++n) shortest_cost_per_node[n] = double.MaxValue;

        // initialize shortest path distance of terminal node to 0
        Node terminal = graph.terminal;
        shortest_cost_per_node[terminal.index_in_graph] = 0.0;

        for (int l = graph.number_of_layers - 2, n, e; l >= 0; --l)
        {
            Layer layer_top = graph.layers[l];

            // iterate over each edge from layer l to layer l - 1
            for (n = 0; n < layer_top.number_of_nodes; ++n)
            {
                Node node_top = layer_top.nodes[n];

                for (e = 0; e < node_top.number_of_edges_out; ++e)
                {
                    Edge edge = node_top.edges_out[e];
                    Node node_bottom = edge.node_out(graph);

                    // calculate the shortest path
                    double dist = weight[node_top.index_in_graph][edge.index_in_node_in] + lambda * delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (shortest_cost_per_node[node_bottom.index_in_graph] + dist < shortest_cost_per_node[node_top.index_in_graph])
                    {
                        // shorter path found
                        shortest_cost_per_node[node_top.index_in_graph] = shortest_cost_per_node[node_bottom.index_in_graph] + dist;

                        weight_top[node_top.index_in_layer] = weight_bottom[node_bottom.index_in_layer] + weight[node_top.index_in_graph][edge.index_in_node_in];
                        delay_top[node_top.index_in_layer] = delay_bottom[node_bottom.index_in_layer] + delay[node_top.index_in_graph][edge.index_in_node_in];
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Cost distances layer {l}:");
                for (n = 0; n < layer_top.number_of_nodes; ++n) Console.Write($" {shortest_cost_per_node[layer_top.nodes[n].index_in_graph]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            (weight_top, weight_bottom) = (weight_bottom, weight_top);
            (delay_top, delay_bottom) = (delay_bottom, delay_top);
        }

        return (shortest_cost_per_node[graph.source.index_in_graph], weight_bottom[graph.source.index_in_layer], delay_bottom[graph.source.index_in_layer]);
    }

    /// <summary>
    /// Shortest distances from each node to the terminal
    /// for distance metric weight
    /// </summary>
    /// <param name="shortest_weight_per_node">stores the shortest distance (weight) to the terminal for each node</param>
    /// <returns>shortest source to terminal weight and the delay along the shortest weight path</returns>
    protected (int, int) shortest_weight_from_terminal(int[] shortest_weight_per_node)
    {
        // keep two distance arrays (least delay)
        int[] delay_top = new int[graph.max_width];      // least delay distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];   // least delay distance to nodes in layer l
        delay_bottom[graph.terminal.index_in_layer] = 0; // initialize least delay distance of terminal node to 0

        // initialize distances of all nodes to large
        for (int n = 0; n < graph.number_of_nodes; ++n) shortest_weight_per_node[n] = int.MaxValue;

        // initialize shortest path distance of terminal node to 0
        Node terminal = graph.terminal;
        shortest_weight_per_node[terminal.index_in_graph] = 0;

        for (int l = graph.number_of_layers - 2, n, e; l >= 0; --l)
        {
            Layer layer_top = graph.layers[l];

            // iterate over each edge from layer l to layer l - 1
            for (n = 0; n < layer_top.number_of_nodes; ++n)
            {
                Node node_top = layer_top.nodes[n];

                for (e = 0; e < node_top.number_of_edges_out; ++e)
                {
                    Edge edge = node_top.edges_out[e];
                    Node node_bottom = edge.node_out(graph);

                    // calculate the shortest path
                    int dist = weight[node_top.index_in_graph][edge.index_in_node_in];
                    if (shortest_weight_per_node[node_bottom.index_in_graph] + dist < shortest_weight_per_node[node_top.index_in_graph])
                    {
                        // shorter path found
                        shortest_weight_per_node[node_top.index_in_graph] = shortest_weight_per_node[node_bottom.index_in_graph] + dist;
                        delay_top[node_top.index_in_layer] = delay_bottom[node_bottom.index_in_layer] + delay[node_top.index_in_graph][edge.index_in_node_in];
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Weight distances layer {l}:");
                for (n = 0; n < layer_top.number_of_nodes; ++n) Console.Write($" {shortest_weight_per_node[layer_top.nodes[n].index_in_graph]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            (delay_bottom, delay_top) = (delay_top, delay_bottom);
        }

        return (shortest_weight_per_node[graph.source.index_in_graph], delay_bottom[graph.source.index_in_layer]);
    }


    /// <summary>
    /// Shortest distances from each node to the terminal
    /// for distance metric delay
    /// </summary>
    /// <param name="least_delay_per_node">stores the shortest distances (delay) to the terminal for each node</param>
    /// <returns>shortest source to terminal distance and shortest weight along the least delay path</returns>
    protected (int, int) least_delay_from_terminal(int[] least_delay_per_node)
    {
        // keep two distance arrays (shortest weight)
        int[] weight_top = new int[graph.max_width];      // shortest weight distance to nodes in layer l + 1 
        int[] weight_bottom = new int[graph.max_width];   // shortest weight distance to nodes in layer l
        weight_bottom[graph.terminal.index_in_layer] = 0; // initialize shortest weight distance of terminal node to 0

        // initialize distances of all nodes to large
        for (int n = 0; n < graph.number_of_nodes; ++n) least_delay_per_node[n] = int.MaxValue;

        // initialize shortest path distance of terminal node to 0
        Node terminal = graph.terminal;
        least_delay_per_node[terminal.index_in_graph] = 0;

        for (int l = graph.number_of_layers - 2, n, e; l >= 0; --l)
        {
            Layer layer_top = graph.layers[l];

            // iterate over each edge from layer l to layer l - 1
            for (n = 0; n < layer_top.number_of_nodes; ++n)
            {
                Node node_top = layer_top.nodes[n];

                for (e = 0; e < node_top.number_of_edges_out; ++e)
                {
                    Edge edge = node_top.edges_out[e];
                    Node node_bottom = edge.node_out(graph);

                    // calculate the shortest path
                    int dist = delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (least_delay_per_node[node_bottom.index_in_graph] + dist < least_delay_per_node[node_top.index_in_graph])
                    {
                        // shorter path found
                        least_delay_per_node[node_top.index_in_graph] = least_delay_per_node[node_bottom.index_in_graph] + dist;
                        weight_top[node_top.index_in_layer] = weight_bottom[node_bottom.index_in_layer] + weight[node_top.index_in_graph][edge.index_in_node_in];
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Delay distances layer {l}:");
                for (n = 0; n < layer_top.number_of_nodes; ++n) Console.Write($" {least_delay_per_node[layer_top.nodes[n].index_in_graph]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            (weight_top, weight_bottom) = (weight_bottom, weight_top);
        }

        return (least_delay_per_node[graph.source.index_in_graph], weight_bottom[graph.source.index_in_layer]);
    }

    /// <summary>
    /// Resize the array to have at least K capacity, but not more than 2 * K capacity
    /// </summary>
    /// <param name="arr">Array to be resized</param>
    /// <param name="min_cap">Minimum required capacity</param>
    public void ensure<T>(ref T[] arr, int min_cap)
    {
#if _TEST // enable during testing to catch bugs with array resizing, forcing IndexOutOfRangeException when the array is indexed outside of its size but within its capacity
        T[] new_arr = new T[min_cap];
        if (arr != null)
        {
            Array.Copy(arr, new_arr, arr.Length > min_cap ? min_cap : arr.Length);
            Array.Fill(arr, default(T));
        }
        arr = new_arr;
#else
        arr ??= new T[min_cap > 4 ? min_cap : 4];

        if (arr.Length >= min_cap && arr.Length < 2 * min_cap) return; // already have enough capacity and not too much extra capacity

        // calculate new capacity: double in size if not enough, halve in size if too much
        int capacity = arr.Length < min_cap ? arr.Length * 2 : arr.Length / 2;
        if (capacity < min_cap) { capacity = min_cap; }
        if (capacity < 4) { capacity = 4; }
        
        // resize keeps the data of the original array
        Array.Resize(ref arr, capacity);
#endif
    }
    #endregion

    #region precision
    /// <summary>
    /// Double precision, consider two doubles equal when they are within precision of one another
    /// </summary>
    protected readonly DoubleComparer comparer = new DoubleComparer();
    protected bool IsEqual(double a, double b)
    {
        return comparer.Compare(a, b) == 0;
    }
    protected bool IsEqualSmaller(double a, double b)
    {
        return comparer.Compare(a, b) <= 0;
    }
    protected bool IsEqualBigger(double a, double b)
    {
        return comparer.Compare(a, b) >= 0;
    }
    protected bool IsBigger(double a, double b)
    {
        return comparer.Compare(a, b) > 0;
    }
    protected bool IsSmaller(double a, double b)
    {
        return comparer.Compare(a, b) < 0;
    }
    #endregion

    #region print
    public void PrintProblem()
    {
        for (int l = 0; l < graph.layers.Length; ++l)
        {
            Console.WriteLine($"Layer {l}");
            Layer layer = graph.layers[l];
            for (int n = 0; n < layer.number_of_nodes; ++n)
            {
                Node node = layer.nodes[n];
                Console.WriteLine($"Node {node.index_in_graph}");
                for (int e = 0; e < node.number_of_edges_in; ++e)
                {
                    Edge edge = node.edges_in[e];
                    Console.WriteLine($"Edge from {edge.index_in_graph_of_node_in} to {edge.index_in_graph_of_node_out} with weight {weight[edge.index_in_graph_of_node_in][edge.index_in_node_in]} and delay {delay[edge.index_in_graph_of_node_in][edge.index_in_node_in]}");
                }
            }
        }
    }

    public void PrintInput()
    {
        Console.WriteLine($"int[][] layer = new int[{graph.number_of_layers}][];");
        for (int l = 0; l < graph.number_of_layers; ++l)
        {
            Console.Write($"layer[{l}] = new int[] {{ ");

            Layer layer = graph.layers[l];
            for (int n = 0; n < layer.number_of_nodes; ++n)
            {
                if (n > 0) Console.Write(", ");
                Console.Write($"{layer.nodes[n].index_in_graph}");
            }
            Console.Write(" };" + Environment.NewLine);
        }

        Console.WriteLine($"int[][] graph = new int[{graph.number_of_nodes}][];");
        for (int l = 1; l < graph.number_of_layers; ++l)
        {
            Layer layer_top = graph.layers[l - 1];
            for (int n = 0; n < layer_top.number_of_nodes; ++n)
            {
                Node node_top = layer_top.nodes[n];
                Console.Write($"graph[{node_top.index_in_graph}] = new int[] {{ ");
                for (int e = 0; e < node_top.number_of_edges_out; ++e)
                {
                    if (e > 0) Console.Write(", ");
                    Console.Write(node_top.edges_out[e].index_in_graph_of_node_out);
                }
                Console.Write(" };" + Environment.NewLine);
            }
        }
        Console.WriteLine($"graph[{graph.number_of_nodes - 1}] = new int[] {{ }};");

        Console.WriteLine($"int[][] weight = new int[{graph.number_of_nodes}][];");
        for (int n = 0; n < weight.Length; ++n)
        {
            Console.Write($"weight[{n}] = new int[] {{ ");
            for (int e = 0; e < weight[n].Length; ++e)
            {
                if (e > 0) Console.Write(", ");
                Console.Write($"{weight[n][e]}");
            }
            Console.Write(" };" + Environment.NewLine);
        }

        Console.WriteLine($"int[][] delay = new int[{graph.number_of_nodes}][];");
        for (int n = 0; n < delay.Length; ++n)
        {
            Console.Write($"delay[{n}] = new int[] {{ ");
            for (int e = 0; e < delay[n].Length; ++e)
            {
                if (e > 0) Console.Write(", ");
                Console.Write($"{delay[n][e]}");
            }
            Console.Write(" };" + Environment.NewLine);
        }
        Console.WriteLine($"int max_delay = {max_delay};");
    }

    protected void PrintFailedCheck(string message)
    {
        PrintInput();
        Console.WriteLine(message); Console.ReadLine();
    }

#if PRINT_PROGRESS
    protected virtual void PrintStart()
    {
#if TEST
        if (verbose) return;
#endif
        Console.WriteLine("     | weight        | delay         ");
        Console.WriteLine("-------------------------------------");
    }

    protected void PrintIteration(int? path_weight, int? path_delay, Feasibility feasibility)
    {
#if TEST
        if (verbose) return;
#endif
        string path_weight_ = path_weight != null ? $"{path_weight}" : "-";
        string path_delay_ = path_delay != null ? $"{path_delay} <= {max_delay}" : "-";
        Console.WriteLine($"{_iteration.ToString().PadRight(5, ' ')}| {path_weight_.PadRight(14, ' ')}| {path_delay_.PadRight(14, ' ')}");
    }
#endif
    #endregion
}

/// <summary>
/// Base class for dual solvers of the Delay Constrained Shortest Path problem
/// </summary>
public abstract class BaseDualSolver : BaseSolver
{
    // dual solution
    protected int _dual_path_weight;
    protected int _dual_path_delay;
    protected double _dual_lambda;

    protected void set_dual_solution(int path_weight, int path_delay, double lambda)
    {
        this._dual_path_weight = path_weight;
        this._dual_path_delay = path_delay;
        this._dual_lambda = lambda;
#if PRINT_PROGRESS
        PrintIteration(lambda, null, null, path_weight + lambda * path_delay, path_weight, path_delay, Feasibility.feasible);
#endif
        // if it is better than the best primal solution so far
        if (_upper_bound_weight > path_weight)
        {
            // update the best primal solution found so far
            this._upper_bound_weight = path_weight;
            this._upper_bound_delay = path_delay;
        }
    }

    public BaseDualSolver(Input input
#if TEST
        , bool verbose = false, bool check = false
#endif
        ) : base(input
#if TEST
            , verbose, check
#endif
            )
    {
    }

    #region algorithmic helpers

    /// <summary>
    /// calculate the shortest path for aggregated cost (weight + lambda * delay)
    /// </summary>
    /// <param name="lambda">factor in aggregated cost (weight + lambda * delay)</param>
    /// <returns>aggregated cost of shortest path</returns>
    protected double shortest_cost(double lambda)
    {
        // keep two distance arrays (shortest distance for aggregated cost (weight + lambda * delay))
        double[] dist_top = new double[graph.max_width];     // shortest path distance to nodes in layer l 
        double[] dist_bottom = new double[graph.max_width];  // shortest path distance to nodes in layer l + 1
        dist_top[0] = 0;                                     // initialize shortest path distance of source node to 0                        

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_bottom[n] = double.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest path for cost (weight + lambda * delay)
                    double aggregated_cost = weight[node_top.index_in_graph][edge.index_in_node_in] + lambda * delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (dist_top[node_top.index_in_layer] + aggregated_cost < dist_bottom[n])
                    {
                        // shorter path for cost (weight + lambda * delay) found
                        dist_bottom[n] = dist_top[node_top.index_in_layer] + aggregated_cost;

                        // make this edge part of the shortest path
                        node_bottom.edges_in[e] = node_bottom.edges_in[0];
                        node_bottom.edges_in[0] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Aggregated cost distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {dist_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_top[n] = dist_bottom[n];
        }
#if TEST
        if (check && !IsEqual(path_weight_from_first_edge() + lambda * path_delay_from_first_edge(), dist_bottom[0])) { PrintFailedCheck($"Path reconstruction {path_weight_from_first_edge() + lambda * path_delay_from_first_edge()} gives a different path {dist_bottom[0]} after shortest path calculation"); }
#endif
        return dist_bottom[0];
    }

    /// <summary>
    /// calculate the shortest path for cost (weight)
    /// </summary>
    /// <returns>cost (weight) of shortest path</returns>
    protected int shortest_weight()
    {
        // keep two distance arrays (shortest distance for cost (weight))
        int[] dist_top = new int[graph.max_width];     // shortest path distance to nodes in layer l 
        int[] dist_bottom = new int[graph.max_width];  // shortest path distance to nodes in layer l + 1
        dist_top[0] = 0;                               // initialize shortest path distance of source node to 0

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest path for cost (weight)
                    int cost_edge = weight[node_top.index_in_graph][edge.index_in_node_in];
                    if (dist_top[node_top.index_in_layer] + cost_edge < dist_bottom[n])
                    {
                        // shorter path for cost (weight) found
                        dist_bottom[n] = dist_top[node_top.index_in_layer] + cost_edge;

                        // make this edge part of the shortest path
                        node_bottom.edges_in[e] = node_bottom.edges_in[0];
                        node_bottom.edges_in[0] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Aggregated cost distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {dist_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            (dist_bottom, dist_top) = (dist_top, dist_bottom);
        }

        // after the last swap, dist_top contains the shortest distances to the terminal for cost (weight)
        return dist_top[0];
    }

    /// <summary>
    /// calculate the path with least delay
    /// i.e. the shortest path for cost (delay)
    /// </summary>
    /// <returns>the delay of the least delay path</returns>
    protected int least_delay()
    {
        // keep two distance arrays (least delay)
        int[] delay_top = new int[graph.max_width];     // least delay distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];  // least delay distance to nodes in layer l
        delay_top[0] = 0;                               // initialize least delay distance of source node to 0            

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the least delay path
                    int cost = delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (delay_top[node_top.index_in_layer] + cost < delay_bottom[n])
                    {
                        // shorter delay path found
                        delay_bottom[n] = delay_top[node_top.index_in_layer] + cost;

                        // make this edge part of the shortest path
                        node_bottom.edges_in[e] = node_bottom.edges_in[0];
                        node_bottom.edges_in[0] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Delay distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {delay_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_top[n] = delay_bottom[n];
        }
#if TEST
        if (check && path_delay_from_first_edge() != delay_bottom[0]) { PrintFailedCheck($"Path reconstruction {path_delay_from_first_edge()} gives a different path {delay_bottom[0]} after least delay calculation"); }
#endif
        // least delay of terminal node
        return delay_bottom[0];
    }

    /// <summary>
    /// calculate the equality graph,
    /// i.e. the set of edges whose slack (weight + (dual_node_out - dual_node_in)) is zero
    /// the duals are calculated as the shortest path for aggregated cost (weight + lambda * delay) to each node
    /// therefore the slack is zero if the edge is part of a shortest distance to any node
    /// </summary>
    /// <param name="lambda">factor in aggregated cost (weight + lambda * delay)</param>
    /// <param name="equality_graph">for each node, number of edges in the equality graph
    /// the first equality_graph[i] edges of node i are part of the equality graph</param>
    /// <returns>aggregated cost of shortest path</returns>
    protected double equality_graph(double lambda, int[] equality_graph)
    {
        // keep two distance arrays (shortest distance for cost (weight + lambda * delay))
        double[] dist_top = new double[graph.max_width];     // shortest path distance to nodes in layer l + 1 
        double[] dist_bottom = new double[graph.max_width];  // shortest path distance to nodes in layer l
        dist_top[0] = 0;                                     // initialize shortest path distance of source node to 0                        
        Node source = graph.source;
        equality_graph[source.index_in_graph] = 0;     // source node does not have edges_in in equality graph

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_bottom[n] = double.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];

                // initialize: zero edges to node_bottom are part of equality graph
                equality_graph[node_bottom.index_in_graph] = 0;

                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest path for cost (weight + lambda * delay)
                    double aggregated_cost = weight[node_top.index_in_graph][edge.index_in_node_in] + lambda * delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (IsSmaller(dist_top[node_top.index_in_layer] + aggregated_cost, dist_bottom[node_bottom.index_in_layer]))
                    {
                        // shorter path for cost (weight + lambda * delay) found
                        dist_bottom[node_bottom.index_in_layer] = dist_top[node_top.index_in_layer] + aggregated_cost;

                        // empty the equality graph and add this edge as the first edge of the equality graph
                        equality_graph[node_bottom.index_in_graph] = 0;
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                    else if (IsEqual(dist_top[node_top.index_in_layer] + aggregated_cost, dist_bottom[node_bottom.index_in_layer]))
                    {
                        // add this edge to the equality graph                            
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Aggregated cost distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {dist_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_top[n] = dist_bottom[n];
        }
#if TEST
        if (verbose) { Console.WriteLine($"lower bound: {dist_bottom[0] - lambda * max_delay}"); }
#endif
        return dist_bottom[0];
    }


    /// <summary>
    /// calculate the equality graph,
    /// i.e. the set of edges whose slack (weight + (dual_node_out - dual_node_in)) is zero
    /// the duals are calculated as the shortest path for cost (weight) to each node
    /// therefore the slack is zero if the edge is part of a shortest distance to any node
    /// </summary>
    /// <param name="equality_graph">for each node, number of edges in the equality graph
    /// the first equality_graph[i] edges of node i are part of the equality graph</param>
    /// <returns>cost of least weight path</returns>
    protected int equality_weight_graph(int[] equality_graph)
    {
        // keep two distance arrays (shortest distance for cost (weight))
        int[] weight_top = new int[graph.max_width];     // shortest path distance to nodes in layer l + 1 
        int[] weight_bottom = new int[graph.max_width];  // shortest path distance to nodes in layer l
        weight_top[0] = 0;                               // initialize shortest path distance of source node to 0                        
        Node source = graph.source;
        equality_graph[source.index_in_graph] = 0;     // source node does not have edges_in in equality graph

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) weight_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];

                // initialize: zero edges to node_bottom are part of equality graph
                equality_graph[node_bottom.index_in_graph] = 0;

                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest path for cost (weight)
                    int cost = weight[node_top.index_in_graph][edge.index_in_node_in];
                    if (weight_top[node_top.index_in_layer] + cost < weight_bottom[node_bottom.index_in_layer])
                    {
                        // shorter path for cost (weight) found
                        weight_bottom[node_bottom.index_in_layer] = weight_top[node_top.index_in_layer] + cost;

                        // empty the equality graph and add this edge as the first edge of the equality graph
                        equality_graph[node_bottom.index_in_graph] = 0;
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                    else if (weight_top[node_top.index_in_layer] + cost == weight_bottom[node_bottom.index_in_layer])
                    {
                        // add this edge to the equality graph                            
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Weight cost distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {weight_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            (weight_bottom, weight_top) = (weight_top, weight_bottom);
        }

        // after the last swap, weight_top contains the shortest distances to the terminal for cost (weight)
        return weight_top[0];
    }

    /// <summary>
    /// calculate the equality graph,
    /// i.e. the set of edges whose slack (delay + (dual_node_out - dual_node_in)) is zero
    /// the duals are calculated as the shortest path for delay cost (delay) to each node
    /// therefore the slack is zero if the edge is part of a shortest distance to any node
    /// </summary>
    /// <param name="equality_graph">for each node, number of edges in the equality graph
    /// the first equality_graph[i] edges of node i are part of the equality graph</param>
    /// <returns>aggregated cost of least delay path</returns>
    protected int equality_delay_graph(int[] equality_graph)
    {
        // keep two distance arrays (shortest distance for cost (delay))
        int[] delay_top = new int[graph.max_width];     // shortest path distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];  // shortest path distance to nodes in layer l
        delay_top[0] = 0;                               // initialize shortest path distance of source node to 0                        
        Node source = graph.source;
        equality_graph[source.index_in_graph] = 0;     // source node does not have edges_in in equality graph

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];

                // initialize: zero edges to node_bottom are part of equality graph
                equality_graph[node_bottom.index_in_graph] = 0;

                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest path for cost (weight + lambda * delay)
                    int cost = delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (delay_top[node_top.index_in_layer] + cost < delay_bottom[node_bottom.index_in_layer])
                    {
                        // shorter path for cost (delay) found
                        delay_bottom[node_bottom.index_in_layer] = delay_top[node_top.index_in_layer] + cost;

                        // empty the equality graph and add this edge as the first edge of the equality graph
                        equality_graph[node_bottom.index_in_graph] = 0;
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                    else if (delay_top[node_top.index_in_layer] + cost == delay_bottom[node_bottom.index_in_layer])
                    {
                        // add this edge to the equality graph                            
                        node_bottom.edges_in[e] = node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]];
                        node_bottom.edges_in[equality_graph[node_bottom.index_in_graph]++] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Delay cost distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {delay_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_top[n] = delay_bottom[n];
        }

        return delay_bottom[0];
    }


    /// <summary>
    /// calculate the path with shortest weight
    /// i.e. the shortest path for cost (weight)
    /// on the subset of edges part of the equality graph
    /// </summary>
    /// <param name="equality_graph">encodes the subset of edges to use: the equality_graph[i] first edges of node i are used</param>
    /// <returns>the weight of the shortest weight path</returns>
    protected int shortest_weight_on_equality_graph(int[] equality_graph)
    {
        // keep two distance arrays (shortest distance for cost (weight))
        int[] dist_top = new int[graph.max_width];     // shortest path distance to nodes in layer l + 1 
        int[] dist_bottom = new int[graph.max_width];  // shortest path distance to nodes in layer l
        dist_top[0] = 0;                                     // initialize shortest path distance of source node to 0                        

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < equality_graph[node_bottom.index_in_graph]; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest weight path
                    int cost = weight[node_top.index_in_graph][edge.index_in_node_in];
                    if (dist_top[node_top.index_in_layer] + cost < dist_bottom[n])
                    {
                        // shorter path for cost (weight) found
                        dist_bottom[n] = dist_top[node_top.index_in_layer] + cost;

                        // make this edge part of the shortest path
                        node_bottom.edges_in[e] = node_bottom.edges_in[0];
                        node_bottom.edges_in[0] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Weight distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {dist_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) dist_top[n] = dist_bottom[n];
        }
#if TEST
        if (check && path_weight_from_first_edge() != dist_bottom[0]) { PrintFailedCheck($"Path reconstruction {path_weight_from_first_edge()} gives a different path {dist_bottom[0]} after shortest weight calculation"); }
#endif
        return dist_bottom[0];
    }

    /// <summary>
    /// calculate the path with least delay
    /// i.e. the shortest path for cost (delay)
    /// on the subset of edges part of the equality graph
    /// </summary>
    /// <param name="equality_graph">encodes the subset of edges to use: the equality_graph[i] first edges of node i are used</param>
    /// <returns>the delay of the least delay path</returns>
    protected int least_delay_on_equality_graph(int[] equality_graph)
    {
        // keep two distance arrays (least delay)
        int[] delay_top = new int[graph.max_width];     // least delay distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];  // least delay distance to nodes in layer l
        delay_top[0] = 0;                               // initialize least delay distance of source node to 0            

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < equality_graph[node_bottom.index_in_graph]; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the least delay path
                    int cost = delay[node_top.index_in_graph][edge.index_in_node_in];
                    if (delay_top[node_top.index_in_layer] + cost < delay_bottom[n])
                    {
                        // shorter delay path found
                        delay_bottom[n] = delay_top[node_top.index_in_layer] + cost;

                        // make this edge part of the shortest path
                        node_bottom.edges_in[e] = node_bottom.edges_in[0];
                        node_bottom.edges_in[0] = edge;
                    }
                }
            }
#if TEST
            if (verbose)
            {
                Console.Write($"Delay distances layer {l}:");
                for (n = 0; n < layer_bottom.number_of_nodes; ++n) Console.Write($" {delay_bottom[n]}");
                Console.Write(Environment.NewLine);
            }
#endif
            // make top bottom for next iteration
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) delay_top[n] = delay_bottom[n];
        }
#if TEST
        if (check && path_delay_from_first_edge() != delay_bottom[0]) { PrintFailedCheck($"Path reconstruction {path_delay_from_first_edge()} gives a different path {delay_bottom[0]} after least delay calculation"); }
#endif
        // least delay of terminal node
        return delay_bottom[0];
    }

    /// <summary>
    /// returns the weight of the last calculated path
    /// presuming the path is encoded in the incoming edges 
    /// (first incoming edge of each node is edge on path to that node) 
    /// </summary>
    /// <returns>weight of path</returns>
    protected int path_weight_from_first_edge()
    {
        // initialize the path weight
        int path_weight = 0;

        // get the terminal node
        Node node = graph.terminal;

        for (int l = graph.layers.Length - 2; l >= 0; l--)
        {
            // backtrack the path by each time taking the first edge of the node
            Edge edge = node.edges_in[0];
            node = edge.node_in(graph);
            path_weight += weight[node.index_in_graph][edge.index_in_node_in];
        }

        return path_weight;
    }

    /// <summary>
    /// returns the delay of the last calculated path
    /// presuming the path is encoded in the incoming edges 
    /// (first incoming edge of each node is edge on path to that node) 
    /// </summary>
    /// <returns>delay of path</returns>
    protected int path_delay_from_first_edge()
    {
        // initialize the path delay
        int path_delay = 0;

        // get the terminal node
        Node node = graph.terminal;

        for (int l = graph.layers.Length - 2; l >= 0; l--)
        {
            // backtrack the path by each time taking the first edge of the node
            Edge edge = node.edges_in[0];
            node = edge.node_in(graph);
            path_delay += delay[node.index_in_graph][edge.index_in_node_in];
        }
        return path_delay;
    }

    #endregion

    #region print
#if PRINT_PROGRESS
    protected override void PrintStart()
    {
#if TEST
        if (verbose) return;
#endif
        Console.WriteLine("     | upper bound   | lower bound   | lambda_lower  | lambda        | lambda_upper  | weight        | delay         ");
        Console.WriteLine("---------------------------------------------------------------------------------------------------------------------");
    }

    protected void PrintIteration(double? lambda, double? lambda_lower, double? lambda_upper, double? aggregated_cost, int? path_weight, int? path_delay, Feasibility feasibility)
    {
#if TEST
        if (verbose) return;
#endif
        string upper_bound = feasibility == Feasibility.feasible && path_weight != null ? $"{path_weight}" : "-";
        string lower_bound = lambda != null && aggregated_cost != null ? $"{(aggregated_cost - lambda * max_delay)}" :
                             lambda != null && path_weight != null && path_delay != null ? $"{path_weight + lambda * (path_delay - max_delay)}" :
                             "-";
        lower_bound = lower_bound.Length > 14 ? lower_bound.Substring(0, 14) : lower_bound.PadRight(14, ' ');
        string lambda_ = lambda != null ? $"{lambda}" : "-";
        lambda_ = lambda_.Length > 14 ? (lambda_.IndexOf('E') >= 0 ? lambda_.Substring(0, 14 - lambda_.Length + lambda_.IndexOf('E')) + lambda_.Substring(lambda_.IndexOf('E')) : lambda_.Substring(0, 14)) : lambda_.PadRight(14, ' ');
        string lambdaU_ = lambda_upper != null ? $"{lambda_upper}" : "-";
        lambdaU_ = lambdaU_.Length > 14 ? (lambdaU_.IndexOf('E') >= 0 ? lambdaU_.Substring(0, 14 - lambdaU_.Length + lambdaU_.IndexOf('E')) + lambdaU_.Substring(lambdaU_.IndexOf('E')) : lambdaU_.Substring(0, 14)) : lambdaU_.PadRight(14, ' ');
        string lambdaL_ = lambda_lower != null ? $"{lambda_lower}" : "-";
        lambdaL_ = lambdaL_.Length > 14 ? (lambdaL_.IndexOf('E') >= 0 ? lambdaL_.Substring(0, 14 - lambdaL_.Length + lambdaL_.IndexOf('E')) + lambdaL_.Substring(lambdaL_.IndexOf('E')) : lambdaL_.Substring(0, 14)) : lambdaL_.PadRight(14, ' ');
        string path_weight_ = path_weight != null ? $"{path_weight}" : "-";
        string path_delay_ = path_delay != null ? $"{path_delay} <= {max_delay}" : "-";
        Console.WriteLine($"{_iteration.ToString().PadRight(5, ' ')}| {upper_bound.PadRight(14, ' ')}| {lower_bound}| {lambdaL_}| {lambda_}| {lambdaU_}| {path_weight_.PadRight(14, ' ')}| {path_delay_.PadRight(14, ' ')}");
    }
#endif
    #endregion
}

#region helper structs

/// <summary>
/// Predecessor: back pointers to reconstruct the path
/// </summary>
public struct Predecessor
{
    /// <summary>
    /// The edge along which the predecessor reached a edge.node_out
    /// contains the index of the edge in node_out.edges_in[this.index_of_edge]
    /// </summary>
    public int index_of_edge;

    /// <summary>
    /// a pointer to which path of edge.node_in is this path's predecessor
    /// </summary>
    public int k_pred;
}

/// <summary>
/// A label holding a partial path from source
/// </summary>
public struct Label<T>
{
    /// <summary>
    /// predecessor (back pointer) of partial path
    /// </summary>
    public Predecessor pred;

    /// <summary>
    /// The distance in some metric of the partial path
    /// </summary>
    public T dist;
}

/// <summary>
/// Distance struct for Restricted shortest path:
/// keep track of the weight and delay
/// </summary>
public struct Distances
{
    public int weight;
    public int delay;

    public double cost(double lambda) => weight + lambda * delay;
}

/// <summary>
/// Use this comparer to control the precision with which doubles are compared
/// </summary>
public readonly struct DoubleComparer : IComparer<double>
{
    private const double PRECISION = 1e-7;

    public int Compare(double x, double y)
    {
        double diff = x - y;
        if (diff < -PRECISION) return -1;
        if (diff > PRECISION) return 1;
        return 0;
    }
}

#endregion
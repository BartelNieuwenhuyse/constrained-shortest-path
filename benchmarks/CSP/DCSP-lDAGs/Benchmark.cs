using DataStructures;
using InputGenerator;
using InputGenerator.GraphGenerator;
using DelayConstrainedShortestPath;
using Ref = DelayConstrainedShortestPath.References;
using System.Diagnostics;

namespace CSP.DCSP_lDAGs;

internal sealed class RunParameters
{
    public int? Seed { get; set; } = null;
    public int MaxRuns { get; set; } = int.MaxValue;
    public string[] Solvers { get; set; } = [
#if TEST
        "dfs_dual", "dfs_dom", "dfs_back_dom", "dfs_eq", "dfs_eq_weight"
#endif
        ];
    public int VarsMin { get; set; } =
#if TEST
        5;
#else
        50;
#endif
    public int VarsMax { get; set; } =
#if TEST
        10;
#else
        100;
#endif
    public string OutputPath { get; set; } = "./";
    public int CancellationSeconds { get; set; } = 600;
    public int MinMillisecondsToElapse { get; set; } = 1000;
    public int MinTimesToRun { get; set; } = 3;
}

internal class Benchmark
{
    const string CsvFile = "DelayConstraintShortestPath.csv";

    /// <summary>
    /// Runs the benchmark for the Delay Constrained Shortest Path (DCSP) on layered acyclic digraph (lDAG) (DCSPonlDAGs) problem.
    /// The program generates random lDAGs and measures the performance of various DCSP algorithms
    /// </summary>
    /// <param name="parameters">The run parameters.</param>
    /// <param name="measure">Whether to measure the performance of the algorithms.</param>
    /// <param name="writeOutput">Whether to write the output to a CSV file.</param>
    /// <param name="verbose">Whether to enable verbose output.</param>
    /// <param name="check">Whether to perform checks.</param>
    internal static void Run(RunParameters parameters, bool writeOutput = false, bool measure = false
#if TEST
        , bool verbose = false, bool check = true
#endif
        )
    {
        var rand = parameters.Seed.HasValue ? new Random(parameters.Seed.Value) : new Random();
        var generator = new Generator(rand);
        TimeSpan timespan_before_cancellation = TimeSpan.FromSeconds(parameters.CancellationSeconds);
        Stopwatch timer = new Stopwatch();
        Solution? solution = null;
        BaseSolver? baseSolver = null;

        void PrintFailedCheck(string message)
        {
            baseSolver?.PrintInput();
            Console.WriteLine(message);
            Console.ReadLine();
        }

        int graph_id = 0;
        while (graph_id++ < parameters.MaxRuns)
        {
            try
            {
                // Generate a random layered DAG
                int vars = rand.Next(parameters.VarsMin, parameters.VarsMax + 1);
                var graphData = generator.GenerateLayeredDAG(vars + 1, vars, rand.Next(3, 2 * vars + 1));

                Graph DAG = GraphBuilder.Build(graphData);

                // Generate random weights and delays for the edges of the DAG
                int weight_delay_correlation = rand.Next(0, 100);
                (int[][] weight, int[][] delay) = generator.GenerateCosts(DAG,
                    max_edge_weight: rand.Next(1, Math.Min(5000, DAG.number_of_edges)),
                    max_edge_delay: rand.Next(1, Math.Min(5000, DAG.number_of_edges)),
                    correlation: weight_delay_correlation / 100.0
                    );

                // Calculate a delay threshold such that in most cases at least the shortest weight path is infeasible and at least one path is feasible.
                // This ensures that the problem is non-trivial and requires the algorithm to find a solution within the given constraint.
                // But also make it possible that the shortest weight path is feasible or that no path is feasible, to test the algorithm's ability to handle edge cases.
                int ld = ShortestPath(DAG, delay);
                int lw = ShortestPath(DAG, weight);
                int dasw = DelayAlongShortestWeight(DAG, weight, delay);
                int max_delay = rand.Next(ld - 1, dasw + 1);

                Console.WriteLine($"Running graph with {DAG.number_of_layers} layers and max width {DAG.max_width}: {DAG.number_of_nodes} nodes");

                string output_file = parameters.OutputPath + CsvFile;

                if (writeOutput && graph_id == 1)
                {
                    // graph topology
                    string headers = "nodes;edges;width;height";
                    // problem difficulty: weight - delay correlation, delay tightness
                    headers += ";weight delay correlation;max delay;least delay;delay least weight path";
                    // solution quality: optimality and duality gap
                    headers += ";unconstrained least weight;dual solution;dual time;dual runs;dual timed out;optimal solution";
                    foreach (string solver in parameters.Solvers)
                    {
#if INSTRUMENTATION
                        // instrumentation: number of labels and number of operations
                        headers += $";{solver} iterations;{solver} number of labels;{solver} number of paths;{solver} number of ops";
#endif
                        // performance: time and runs
                        headers += $";{solver} time;{solver} runs;{solver} timed out";
                    }
                    File.WriteAllText(output_file, headers);
                }

                // graph topology
                string output_line = $"{DAG.number_of_nodes};{DAG.number_of_edges};{DAG.max_width};{DAG.number_of_layers}";
                // problem difficulty: weight - delay
                output_line += $";{weight_delay_correlation};{max_delay};{ld};{dasw}";
                // solution quality: optimality and duality gap
                output_line += $";{lw}";

                // solve the dual problem as input for solvers that use the optimal dual multiplier
                // and close the duality gap as phase two rather than working directly on weight
                double lambda;
                int incumbent = int.MaxValue;
                {
                    Console.WriteLine("Run kelley primal dual do determine optimal lambda for duality gap closing.");

                    // measure the number of times the problem can be solved within min_milliseconds_to_elapse
                    int number_of_runs = 0;
                    bool IsCancelled = false;
                    do
                    {
                        Input input = new Input()
                        {
                            graph = GraphBuilder.Build(graphData),
                            weight = weight,
                            delay = delay,
                            max_delay = max_delay
                        };

                        var csp = new Ref.DualCuttingPlaneSolver(input
#if TEST
                            , verbose: verbose, check: check
#endif
                            );
                        baseSolver = csp;

                        var cts = new CancellationTokenSource(timespan_before_cancellation);
                        try
                        {
                            timer.Start();
                            solution = csp.run_kelley_pd(cts.Token);
                            timer.Stop();
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("Operation was cancelled");
                            timer.Stop();
                            IsCancelled = true;
                        }
                        cts.TryReset();

                        number_of_runs++;
                    } while (measure && (timer.ElapsedMilliseconds <= parameters.MinMillisecondsToElapse || number_of_runs < parameters.MinTimesToRun) && !IsCancelled);

                    lambda = solution?.dual?.lambda ?? 0;
                    incumbent = solution?.primal?.weight ?? incumbent;
                    int dual_weight = solution?.dual?.weight ?? 0;

                    Console.WriteLine($"Solved to lambda {lambda} and upper bound {dual_weight} in {timer.ElapsedMilliseconds / 1000.0}");

                    if (writeOutput)
                    {
                        output_line += $";{dual_weight};{timer.ElapsedMilliseconds};{number_of_runs};{(IsCancelled ? 1 : 0)}";
                    }

                    timer.Reset();
                }

                // solve the primal problem
                int? f = null;
                double? opt_weight = null;
                foreach (string solver in parameters.Solvers)
                {
                    Console.WriteLine($"Delay constrained least cost using {solver}");

                    // measure the number of times the problem can be solved within a second
                    int number_of_runs = 0;
                    bool IsCancelled = false;
                    do
                    {
                        Input input = new Input()
                        {
                            graph = GraphBuilder.Build(graphData),
                            weight = weight,
                            delay = delay,
                            max_delay = max_delay
                        };

                        var cts = new CancellationTokenSource(timespan_before_cancellation);
                        try
                        {
                            switch (solver)
                            {
                                case "dfs_dual":
                                    {
                                        var csp = new Ref.LabelingDFSSolver(input
#if TEST
                                            , verbose: verbose, check: check
#endif
                                            );
                                        baseSolver = csp;
                                        timer.Start();
                                        solution = csp.run_dfs_dual(lambda, incumbent, cts.Token);
                                        timer.Stop();
                                        break;
                                    }
                                case "dfs_dom":
                                    {
                                        var csp = new Ref.LabelingDFSSolver(input
#if TEST
                                            , verbose: verbose, check: check
#endif
                                            );
                                        baseSolver = csp;
                                        timer.Start();
                                        solution = csp.run_dfs_dominance(cts.Token);
                                        timer.Stop();
                                        break;
                                    }
                                case "dfs_back_dom":
                                    {
                                        var csp = new LabelingDFSSolver(input
#if TEST
                                            , verbose: verbose, check: check
#endif
                                            );
                                        baseSolver = csp;
                                        timer.Start();
                                        solution = csp.run_dfs_back_dominance(cts.Token);
                                        timer.Stop();
                                        break;
                                    }
                                case "dfs_eq":
                                    {
                                        var csp = new LabelingDFSSolver(input
#if TEST
                                            , verbose: verbose, check: check
#endif
                                            );
                                        baseSolver = csp;
                                        timer.Start();
                                        solution = csp.run_dfs_delay_completion_equivalence(cts.Token);
                                        timer.Stop();
                                        break;
                                    }
                                case "dfs_eq_weight":
                                    {
                                        var csp = new LabelingDFSSolver(input
#if TEST
                                            , verbose: verbose, check: check
#endif
                                            );
                                        baseSolver = csp;
                                        timer.Start();
                                        solution = csp.run_dfs_weight_completion_equivalence(cts.Token);
                                        timer.Stop();
                                        break;
                                    }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("Operation was cancelled");
                            timer.Stop();
                            IsCancelled = true;
                        }
                        cts.TryReset();

                        number_of_runs++;
                    }
                    while (measure && (timer.ElapsedMilliseconds <= parameters.MinMillisecondsToElapse || number_of_runs < parameters.MinTimesToRun) && !IsCancelled);

                    Console.WriteLine($"{solver} found optimal path with weight {solution?.primal?.weight ?? 0} and delay no greater than {max_delay} in {timer.ElapsedMilliseconds / 1000.0 / number_of_runs}"
#if INSTRUMENTATION
                        + " using:"
                        + ((solution?.counters?.iteration ?? 0) != 0
                        ? $" {solution?.counters?.iteration} iterations"
                        : "")
                        + ((solution?.counters?.number_of_paths ?? 0) != 0
                        ? $" {solution?.counters?.number_of_paths} paths"
                        : "")
                        + ((solution?.counters?.number_of_labels ?? 0) != 0
                        ? $" {solution?.counters?.number_of_labels} labels"
                        : "")
                        + ((solution?.counters?.number_of_ops ?? 0) != 0
                        ? $" {solution?.counters?.number_of_ops} ops"
                        : "")
#endif
                        );

                    // validate that all solvers return the same solution
                    if (!f.HasValue && !IsCancelled)
                    {
                        f = (int?)solution?.feasibility ?? (int)Feasibility.undetermined;
                        opt_weight = solution?.primal?.weight ?? 0;
                        if (writeOutput)
                        {
                            output_line += $";{opt_weight}";
                        }
                    }
                    else if (!IsCancelled)
                    {
                        if (f != ((int?)solution?.feasibility ?? (int)Feasibility.undetermined))
                        {
                            PrintFailedCheck("One solver found solution while the other did not?!");
                        }

                        if (f == (int)Feasibility.feasible)
                        {
                            if (opt_weight != (solution?.primal?.weight ?? 0))
                            {
                                PrintFailedCheck($"Two solvers return different solution: {opt_weight} != {solution?.primal?.weight ?? 0}");
                            }
                        }
                    }
                    if (writeOutput)
                    {
#if INSTRUMENTATION
                        output_line += $";{solution?.counters?.iteration.ToString() ?? ""};{solution?.counters?.number_of_labels.ToString() ?? ""};{solution?.counters?.number_of_paths.ToString() ?? ""};{solution?.counters?.number_of_ops.ToString() ?? ""}";
#endif
                        output_line += $";{timer.ElapsedMilliseconds};{number_of_runs};{(IsCancelled ? 1 : 0)}";
                    }
                    timer.Reset();
                }

                if (writeOutput)
                {
                    File.AppendAllText(output_file, Environment.NewLine + output_line);
                }
            }
            catch (OverflowException ex)
            {
                Console.WriteLine($"Overflow exception caught: {ex}");
            }
        }
    }

    /// <summary>
    /// calculate the path with least cost
    /// i.e. the shortest path for cost
    /// </summary>
    /// <returns>the cost of the least cost path</returns>
    private static int ShortestPath(Graph graph, int[][] cost)
    {
        // keep two distance arrays (least cost)
        int[] cost_top = new int[graph.max_width];     // least cost distance to nodes in layer l + 1 
        int[] cost_bottom = new int[graph.max_width];  // least cost distance to nodes in layer l
        cost_top[0] = 0;                               // initialize least cost distance of source node to 0            

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) cost_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the least cost path
                    int cost_edge = cost[node_top.index_in_graph][edge.index_in_node_in];
                    if (cost_top[node_top.index_in_layer] + cost_edge < cost_bottom[n])
                    {
                        // shorter cost path found
                        cost_bottom[n] = cost_top[node_top.index_in_layer] + cost_edge;
                    }
                }
            }

            // make top bottom for next iteration
            (cost_bottom, cost_top) = (cost_top, cost_bottom);
        }

        // least cost of terminal node
        return cost_top[0]; // due to the swap above, the least cost of the terminal node is now in cost_top[0]
    }

    /// <summary>
    /// calculate the path with shortest weight
    /// i.e. the shortest path for cost (weight)
    /// </summary>
    /// <returns>the delay of the shortest weight path</returns>
    private static int DelayAlongShortestWeight(Graph graph, int[][] weight, int[][] delay)
    {
        // keep two distance arrays (shortest weight)
        int[] weight_top = new int[graph.max_width];     // shortest weight distance to nodes in layer l + 1 
        int[] weight_bottom = new int[graph.max_width];  // shortest weight distance to nodes in layer l
        weight_top[graph.source.index_in_layer] = 0;     // initialize shortest weight distance of source node to 0

        int[] delay_top = new int[graph.max_width];      // delay along shortest weight distance to nodes in layer l + 1 
        int[] delay_bottom = new int[graph.max_width];   // delay along shortest weight distance to nodes in layer l
        delay_top[graph.source.index_in_layer] = 0;      // initialize delay distance of source node to 0

        for (int l = 1, n, e; l < graph.layers.Length; ++l)
        {
            Layer layer_bottom = graph.layers[l];

            // initialize distance layer l + 1 to large
            for (n = 0; n < layer_bottom.number_of_nodes; ++n) weight_bottom[n] = int.MaxValue;

            // iterate over each edge from layer l to layer l + 1
            for (n = 0; n < layer_bottom.number_of_nodes; ++n)
            {
                Node node_bottom = layer_bottom.nodes[n];
                for (e = 0; e < node_bottom.number_of_edges_in; ++e)
                {
                    Edge edge = node_bottom.edges_in[e];
                    Node node_top = edge.node_in(graph);

                    // calculate the shortest weight path
                    int cost = weight[node_top.index_in_graph][edge.index_in_node_in];
                    if (weight_top[node_top.index_in_layer] + cost < weight_bottom[n])
                    {
                        // shorter delay path found
                        weight_bottom[n] = weight_top[node_top.index_in_layer] + cost;

                        // update the delay along the shortest weight path
                        delay_bottom[n] = delay_top[node_top.index_in_layer] + delay[node_top.index_in_graph][edge.index_in_node_in];
                    }
                }
            }

            // make top bottom for next iteration
            (weight_bottom, weight_top) = (weight_top, weight_bottom);
            (delay_bottom, delay_top) = (delay_top, delay_bottom);
        }

        // delay along the shortest weight path
        return delay_top[graph.terminal.index_in_layer];
    }
}

namespace DelayConstrainedShortestPath.References;

using DataStructures;

public sealed class DualCuttingPlaneSolver : BaseDualSolver
{
    public DualCuttingPlaneSolver(Input input
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
    /// solve the restricted shortest path dual problem by kelley's cutting plane method
    /// this is the original LARAC solver, which uses an arbitrary shortest aggregated cost path
    /// in case multiple equaly short paths are present
    /// </summary>
    public Solution run_kelley(CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 1;

        // start with the shortest weight path
        int weight_path_weight = shortest_weight();
        int weight_path_delay = path_delay_from_first_edge();
#if INSTRUMENTATION
		++counters.number_of_paths; // shortest weight path
#endif
        if (weight_path_delay <= max_delay)
        {
            // shortest weight path is feasible: we are done
            _feasibility = Feasibility.feasible;
            set_dual_solution(weight_path_weight, weight_path_delay, 0);
#if TEST
            if (check)
            {
                if (_upper_bound_weight != get_path_weight(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound weight {_upper_bound_weight} does not match path weight {get_path_weight(get_path_from_first_edge())}"); }
                if (_upper_bound_delay != get_path_delay(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound delay {_upper_bound_delay} does not match path delay {get_path_delay(get_path_from_first_edge())}"); }
            }
#endif
            return new Solution(_feasibility,
                new DualSolution(_dual_path_weight, _dual_path_delay, _dual_lambda),
                new PrimalSolution(_upper_bound_weight, _upper_bound_delay, get_path_from_first_edge())
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
        }
#if PRINT_PROGRESS
        PrintIteration(0, 0, null, weight_path_weight, weight_path_weight, weight_path_delay, Feasibility.infeasible);
#endif
        // caclulate the least delay path
        _iteration++;
        int delay_path_delay = least_delay();
#if INSTRUMENTATION
		++counters.number_of_paths; // least delay path
#endif
        if (delay_path_delay > max_delay)
        {
            // least delay path is infeasible: we are done
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, null, null, null, null, delay_path_delay, Feasibility.infeasible);
#endif
            return new Solution(_feasibility, null, null
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
        }
        _feasibility = Feasibility.feasible;
        _upper_bound_path = get_path_from_first_edge();

        int delay_path_weight = path_weight_from_first_edge();
#if PRINT_PROGRESS
        PrintIteration(null, null, null, null, delay_path_weight, delay_path_delay, Feasibility.feasible);
#endif
        double lambda_lower = 0;
        double? lambda_upper = null;
#if TEST
        double lower_bound_lower = weight_path_weight;
        double lower_bound_upper = double.MinValue;
#endif
        int delay;
        do
        {
            token.ThrowIfCancellationRequested();

            _iteration++;

            // update lambda
            double lambda = (double)(weight_path_weight - delay_path_weight) / (delay_path_delay - weight_path_delay);
#if TEST
            if (verbose) { Console.WriteLine(); Console.WriteLine($"lambda: {lambda}"); }
#endif
            // calculate shortest aggregated cost path with new lambda
            double path_aggregated_cost = shortest_cost(lambda);
#if INSTRUMENTATION
	    	++counters.number_of_paths; // path for new lambda estimate
#endif
            delay = path_delay_from_first_edge();
            int weight = path_weight_from_first_edge();
#if TEST
            if (verbose) { Console.WriteLine($"lower bound: {path_aggregated_cost - lambda * max_delay}"); }
#endif
            if (IsEqual(weight + lambda * delay, weight_path_weight + lambda * weight_path_delay))
            {
                // optimum reached
                set_dual_solution(delay_path_weight, delay_path_delay, lambda);
                break;
            }
#if TEST
            if (verbose) { Console.WriteLine($"feasibility: {delay} <= {max_delay}"); }
            if (check)
            {
                // lambda_lower < lambda < lambda_upper
                if (IsEqualSmaller(lambda, lambda_lower) || (lambda_upper.HasValue && IsEqualBigger(lambda, lambda_upper.Value))) { PrintFailedCheck($"Lambda {lambda} should be between {lambda_lower} and {lambda_upper}"); }
            }
#endif
            // check if path is feasible or infeasible
            if (delay > max_delay)
            {
                // new path is infeasible, update weight path
                weight_path_delay = delay;
                weight_path_weight = weight;
#if TEST
                if (check)
                {
                    if (!IsSmaller(lower_bound_lower, path_aggregated_cost - lambda * max_delay)) { PrintFailedCheck($"Lower bound {lower_bound_lower} did not increase {path_aggregated_cost - lambda * max_delay}"); }
                    lower_bound_lower = path_aggregated_cost - lambda * max_delay;
                }
#endif
#if PRINT_PROGRESS
                PrintIteration(lambda, lambda_lower, lambda_upper, path_aggregated_cost, weight_path_weight, weight_path_delay, Feasibility.infeasible);
#endif
                lambda_lower = lambda;
            }
            else
            {
                // new path is feasible, update delay path
                delay_path_delay = delay;
                delay_path_weight = weight;

                // and store the actual path
                path_from_first_edge(_upper_bound_path);
#if TEST
                if (verbose) { Console.WriteLine($"upper bound: {delay_path_weight}"); }
#endif
                if (delay == max_delay)
                {
                    // upper bound = lower bound, we are done
                    set_dual_solution(delay_path_weight, delay_path_delay, lambda);
                    break;
                }
#if TEST
                if (check)
                {
                    if (!IsSmaller(lower_bound_upper, path_aggregated_cost - lambda * max_delay)) { PrintFailedCheck($"Lower bound {lower_bound_upper} did not increase {path_aggregated_cost - lambda * max_delay}"); }
                    lower_bound_upper = path_aggregated_cost - lambda * max_delay;
                }
#endif
#if PRINT_PROGRESS
                PrintIteration(lambda, lambda_lower, lambda_upper, path_aggregated_cost, delay_path_weight, delay_path_delay, Feasibility.feasible);
#endif
                lambda_upper = lambda;
            }
        }
        while (delay != max_delay);
#if TEST
        if (check)
        {
            if (_upper_bound_weight != get_path_weight(_upper_bound_path)) { PrintFailedCheck($"Upper bound weight {_upper_bound_weight} does not match path weight {get_path_weight(_upper_bound_path)}"); }
            if (_upper_bound_delay != get_path_delay(_upper_bound_path)) { PrintFailedCheck($"Upper bound delay {_upper_bound_delay} does not match path delay {get_path_delay(_upper_bound_path)}"); }
        }
#endif
        return new Solution(_feasibility,
                            new DualSolution(_dual_path_weight, _dual_path_delay, _dual_lambda),
                            new PrimalSolution(_upper_bound_weight, _upper_bound_delay, _upper_bound_path)
#if INSTRUMENTATION
                            , counters with { iterations = _iteration }
#endif
                            );
    }

    /// <summary>
    /// solve the restricted shortest path dual problem by kelley's cutting plane method
    /// this is an adaptation of the original LARAC solver, which uses the least complementary slack violating path
    /// in case of multiple equally short paths for the aggregated cost
    /// </summary>
    public Solution run_kelley_pd(CancellationToken token)
    {
#if PRINT_PROGRESS
        PrintStart();
#endif
        _iteration = 1;

        // data structures 
        int[] equality_subgraph = new int[graph.number_of_nodes]; // for the equality graph (first n edges of node are in equality graph)

        // start with the shortest weight path
        int weight_path_weight = equality_weight_graph(equality_subgraph);
        int weight_path_delay = least_delay_on_equality_graph(equality_subgraph);
#if INSTRUMENTATION
        counters.number_of_paths += 2; // shortest weight path
#endif
        if (weight_path_delay <= max_delay)
        {
            // shortest weight path is feasible: we are done
            _feasibility = Feasibility.feasible;
            set_dual_solution(weight_path_weight, weight_path_delay, 0);
#if TEST
            if (check)
            {
                if (_upper_bound_weight != get_path_weight(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound weight {_upper_bound_weight} does not match path weight {get_path_weight(get_path_from_first_edge())}"); }
                if (_upper_bound_delay != get_path_delay(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound delay {_upper_bound_delay} does not match path delay {get_path_delay(get_path_from_first_edge())}"); }
            }
#endif
            return new Solution(_feasibility,
                new DualSolution(_dual_path_weight, _dual_path_delay, _dual_lambda),
                new PrimalSolution(_upper_bound_weight, _upper_bound_delay, get_path_from_first_edge())
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
        }
#if PRINT_PROGRESS
        PrintIteration(0, 0, null, weight_path_weight, weight_path_weight, weight_path_delay, Feasibility.infeasible);
#endif
        // calculate the least delay path
        _iteration++;
        int delay_path_delay = equality_delay_graph(equality_subgraph);
#if INSTRUMENTATION
        ++counters.number_of_paths; // least delay path
#endif
        if (delay_path_delay > max_delay)
        {
            // least delay path is infeasible: we are done
            _feasibility = Feasibility.infeasible;
#if PRINT_PROGRESS
            PrintIteration(null, null, null, null, null, delay_path_delay, Feasibility.infeasible);
#endif
            return new Solution(_feasibility, null, null
#if INSTRUMENTATION
                                , counters with { iterations = _iteration }
#endif
                                );
        }
        _feasibility = Feasibility.feasible;
        int delay_path_weight = shortest_weight_on_equality_graph(equality_subgraph);
#if INSTRUMENTATION
        ++counters.number_of_paths; // shortest weight path on equality graph
#endif
        if (delay_path_delay == max_delay)
        {
            // lower bound = upper bound: we are done
            set_dual_solution(delay_path_weight, delay_path_delay, 0);
#if TEST
            if (check)
            {
                if (_upper_bound_weight != get_path_weight(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound weight {_upper_bound_weight} does not match path weight {get_path_weight(get_path_from_first_edge())}"); }
                if (_upper_bound_delay != get_path_delay(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound delay {_upper_bound_delay} does not match path delay {get_path_delay(get_path_from_first_edge())}"); }
            }
#endif
            return new Solution(_feasibility,
                new DualSolution(_dual_path_weight, _dual_path_delay, _dual_lambda),
                new PrimalSolution(_upper_bound_weight, _upper_bound_delay, get_path_from_first_edge())
#if INSTRUMENTATION
                , counters with { iterations = _iteration }
#endif
                );
        }
#if PRINT_PROGRESS
        PrintIteration(null, null, null, null, delay_path_weight, delay_path_delay, Feasibility.feasible);
#endif
        double lambda_lower = 0;
        double? lambda_upper = null;
#if TEST
        double lower_bound_lower = weight_path_weight;
        double lower_bound_upper = double.MinValue;
#endif
        int delay;
        do
        {
            token.ThrowIfCancellationRequested();

            _iteration++;

            // update lambda
            double lambda = (double)(weight_path_weight - delay_path_weight) / (delay_path_delay - weight_path_delay);
#if TEST
            if (verbose) { Console.WriteLine(); Console.WriteLine($"lambda: {lambda}"); }
            if (check)
            {
                if (IsEqualSmaller(lambda, lambda_lower) || (lambda_upper.HasValue && IsEqualBigger(lambda, lambda_upper.Value))) { PrintFailedCheck($"Lambda {lambda} should be between {lambda_lower} and {lambda_upper}"); }
            }
#endif
            // calculate shortest aggregated cost path with new lambda
            double path_aggregated_cost = equality_graph(lambda, equality_subgraph);
#if INSTRUMENTATION
            ++counters.number_of_paths; // path for new lambda estimate
#endif
#if TEST
            if (verbose) { Console.WriteLine($"lower bound: {path_aggregated_cost - lambda * max_delay}"); }
#endif
            if (IsEqual(path_aggregated_cost, weight_path_weight + lambda * weight_path_delay))
            {
                // optimum reached
                if (path_delay_from_first_edge() > max_delay)
                {
                    least_delay_on_equality_graph(equality_subgraph); // get (back to) the feasible solution on the equality graph
#if INSTRUMENTATION
                    ++counters.number_of_paths; // path on equality graph
#endif
                }
                set_dual_solution(delay_path_weight, delay_path_delay, lambda);
                break;
            }

            // check if path is feasible or infeasible
            delay = path_delay_from_first_edge();

            if (delay > max_delay)
            {
                // new path is infeasible
                // get the least infeasible path on the equality graph
                delay = least_delay_on_equality_graph(equality_subgraph);
#if INSTRUMENTATION
                ++counters.number_of_paths; // path on equality graph
#endif
#if TEST
                if (verbose) { Console.WriteLine($"feasibility: {delay} <= {max_delay}"); }
#endif
                if (delay <= max_delay)
                {
                    // there is a infeasible and feasible path on the equality graph: optimum reached
                    set_dual_solution(path_weight_from_first_edge(), delay, lambda);
                    break;
                }

                // update weight path
                weight_path_delay = delay;
                weight_path_weight = path_weight_from_first_edge();
#if TEST
                if (check)
                {
                    if (!IsSmaller(lower_bound_lower, path_aggregated_cost - lambda * max_delay)) { PrintFailedCheck($"Lower bound {lower_bound_lower} did not increase {path_aggregated_cost - lambda * max_delay}"); }
                    lower_bound_lower = path_aggregated_cost - lambda * max_delay;
                }
#endif
#if PRINT_PROGRESS
                PrintIteration(lambda, lambda_lower, lambda_upper, path_aggregated_cost, weight_path_weight, weight_path_delay, Feasibility.infeasible);
#endif
                lambda_lower = lambda;
            }
            else
            {
                // new path is feasible
                // get the lowest cost path on the equality graph
                int weight = shortest_weight_on_equality_graph(equality_subgraph);
                delay = path_delay_from_first_edge();
#if INSTRUMENTATION
                ++counters.number_of_paths; // path on equality graph
#endif
#if TEST
                if (verbose) { Console.WriteLine($"feasibility: {delay} <= {max_delay}"); }
#endif
                if (delay >= max_delay)
                {
                    // there is a infeasible and feasible path on the equality graph: optimum reached
                    if (delay > max_delay)
                    {
                        // get (back to) the feasible solution on the equality graph
                        delay = least_delay_on_equality_graph(equality_subgraph);
                        weight = path_weight_from_first_edge();
#if INSTRUMENTATION
                        ++counters.number_of_paths; // path on equality graph
#endif
                    }
                    set_dual_solution(weight, delay, lambda);
                    break;
                }

                // update delay path
                delay_path_delay = delay;
                delay_path_weight = weight;
#if TEST
                if (verbose) { Console.WriteLine($"upper bound: {delay_path_weight}"); }
                if (check)
                {
                    if (!IsSmaller(lower_bound_upper, path_aggregated_cost - lambda * max_delay)) { PrintFailedCheck($"Lower bound {lower_bound_upper} did not increase {path_aggregated_cost - lambda * max_delay}"); }
                    lower_bound_upper = path_aggregated_cost - lambda * max_delay;
                }
#endif
#if PRINT_PROGRESS
                PrintIteration(lambda, lambda_lower, lambda_upper, path_aggregated_cost, delay_path_weight, delay_path_delay, Feasibility.feasible);
#endif
                lambda_upper = lambda;
            }
        }
        while (delay != max_delay);
#if TEST
        if (check)
        {
            if (_upper_bound_weight != get_path_weight(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound weight {_upper_bound_weight} does not match path weight {get_path_weight(get_path_from_first_edge())}"); }
            if (_upper_bound_delay != get_path_delay(get_path_from_first_edge())) { PrintFailedCheck($"Upper bound delay {_upper_bound_delay} does not match path delay {get_path_delay(get_path_from_first_edge())}"); }
        }
#endif
        return new Solution(_feasibility,
                            new DualSolution(_dual_path_weight, _dual_path_delay, _dual_lambda),
                            new PrimalSolution(_upper_bound_weight, _upper_bound_delay, get_path_from_first_edge())
#if INSTRUMENTATION
                            , counters with { iterations = _iteration }
#endif
                            );
    }

#endregion
}

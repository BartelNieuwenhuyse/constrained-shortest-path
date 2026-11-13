using System.Numerics;
using DataStructures;

namespace DelayConstrainedShortestPath;

/// <summary>
/// Problem data:
/// min weight[i,j] * x[i,j]
/// s.t. delay[i,j] * x[i,j] < max_delay
/// </summary>
public class Input
{
    public required Graph graph;
    public required int[][] weight;
    public required int[][] delay;
    public required int max_delay;
}

public enum Feasibility
{
    infeasible = -1,
    undetermined = 0,
    feasible = 1
}

/// <summary>
/// Solution
/// </summary>
public sealed record DualSolution(int weight, int delay, double lambda);

public sealed record PrimalSolution(int weight, int delay, Edge[] path);
#if INSTRUMENTATION
public sealed record InstrumentationCounters
{
    public BigInteger iterations = 0;
    public BigInteger number_of_paths = 0;
    public BigInteger number_of_labels = 0;
    public BigInteger number_of_ops = 0;
}
#endif
public sealed record Solution(
    Feasibility feasibility
    , DualSolution? dual
    , PrimalSolution? primal
#if INSTRUMENTATION
    , InstrumentationCounters counters
#endif
)
{
    public bool HasDual => dual is not null;
    public bool HasPrimal => primal is not null;
}

## Introduction

Research into the constrained shortest path problem.

Work in progress code for experimenting and benchmarking new propositions.

## Problem statement and terminology

The constrained shortest path problem (CSP) in this context is the problem to find a minimum weight path from a source node to a terminal node. The weight of a path is the sum of the weights of the path's edges. Beside weights, each edge is associated with one or more additional scalars. The constrained shortest path problem places a constraint on the sum of each of these scalars along the optimal path. For one additional constraint, this additional dimension is called the delay of the path, as in the delay constrained least cost (DCLC) problem. This problem is also known as the restricted shortest path problem and weight constrained shortest path problem (WCSPP). Note the ambiguity with the latter: in our terminology, weight is the objective function to minimize and delay the constraint.  For two or more such constraints, the additional dimensions are called resources, as in the resource constrained shortest path problem (RCSPP).

Solution strategies used in this research are label setting, lagrange relaxation and path ranking.

### Label setting 

Based on dynamic programming, a label is a tuple that encodes a partial path from the source to some node in the graph and is repeatedly expanded along edges. Pruning techniques fathom labels early if it can be proven that the partial path can not be completed to an optimal path. [Read more](docs/CSP/Labeling.md) about label setting and our contributions to it.

### Lagrange relaxation followed by path ranking. 

Lagrange relaxation reduced the bi- or multi dimensional constrained problem into a single cost function:

```
cost = weight + λ * delay
```

For any none-negative value of the multiplier λ, the constrained shortest path is reduced to a simple shortest path problem. The solution of this shortest path gives a lower bound on the optimum of the original constrained shortest path problem. The problem of finding the highest lower bound is the Lagrange dual problem. Even with the best lower bound, there may exist a duality gap. Hence this technique is not an exact solver.

To obtain the exact primal optimum, traditional methods closes the duality gap by path ranking. Path ranking enumerates k shortest paths of increasing cost until a path is found that can be proven to be optimal.

The k-shortest path problem is the problem of not finding the minimum cost path, but finding k paths of least cost. 

The shortest path with forbidden paths problem is...

Currently the research targets a single constraint (WCSPP) on directed acyclic graphs (DAG). More particularly on layered DAGs. Why layered DAGs? Because they have the form of Multivalued Decision Diagrams. See the motivating section for info. Extensions are planned to general graphs and multiple constraints where applicable. In our experiments, weight and the delay are both none negative integers.

## Contributions

We introduce the following contributions to solve the constrained shortest path problem. These are to are knowledge novel and not yet introduced in the research literature:

We introduce two new pruning techniques that are used in a depth first label setting algorithm: a **tighter dominance pruning** that is calculated after all successors are visited and before backtracking and **completion equivalence pruning** which divides the delay space into intervals which have the same optimal path completion to the terminal.

An **incremental k-shortest path on DAGs** is proposed. Where incremental means that the kth path can be generated from the solution of the k-1th path. The incremental idea of this algorithm is used in the constrained shortest path problem to device **two pull labeling algorithms**: one that traverses the DAG in topological  ordering, and the other in best-first A* ordering.

Using the idea behind the incremental k-shortest path on DAGs, we also device a solver for the shortest path with forbidden paths problem: first a minimal acyclic deterministic finite automaton (madfa) is created from the set of forbidden paths. This madfa structure is then used to find the shortest none forbidden path. We extend this to not only forbid sets of full source-to-terminal paths, but to **forbid sets of edge combinations**. This can be used as the backbone for path ranking: instead of ranking k paths, we propose to derive **cover cuts** from the paths: edge combinations that cannot be used together in the optimal path.

## Project, configurations and benchmark setup

Console projects are available that generate random acyclic layered graphs with random weights and delays on edges. The projects come in three configurations: Debug, Release and Benchmark. 

- Debug is used for testing. When running in debug, assertions are checking the correctness of the algorithms and the invariants and assumptions. Only relative small problems can be run in debug, as some test assertions may be heavy.
- Release is used for instrumentation: e.g. counting the number of labels created during the algorithm run
- Benchmark is used to measure clock time.
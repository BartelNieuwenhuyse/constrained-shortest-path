# Source

Implementation code for the novel algorithmic contributions described in [docs/](../docs/). This folder contains libraries only — there is no standalone entry point; build and run instructions live under [benchmarks/](../benchmarks/README.md).

This is a research codebase under active development: expect APIs and internal structure to change as algorithms are added and refined.

## Structure

- `DataStructures/` — generic building blocks shared across solvers: the layered-DAG graph representation (`Graph.cs`), a binary heap (`Heap.cs`), and a sorted array (`SortedArray.cs`). Each supports optional operation-counting instrumentation used by the benchmarks.
- `CSP/DCSP-lDAGs/` — solvers for the delay-constrained shortest path problem on layered DAGs (see [docs/CSP](../docs/CSP/Labeling-DFS.md)).

## Build configurations

Build configurations (`Debug`, `Release`, `Benchmark`) are defined in [Directory.Build.props](Directory.Build.props) and control compile-time diagnostic/instrumentation flags; see the [benchmarks README](../benchmarks/README.md#build-configurations) for what each one enables.

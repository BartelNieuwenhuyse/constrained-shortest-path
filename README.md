# Constrained Shortest Path

Research codebase for developing and benchmarking novel exact algorithms for the Constrained Shortest Path Problem (CSP).

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
![Status: research prototype](https://img.shields.io/badge/status-research%20prototype-blue)

## Overview

This repository is dedicated to original algorithmic contributions to constrained shortest path optimization. It is not limited to reimplementing existing methods: the goal is to develop, validate, and benchmark new ideas that improve exact solvers.

The starting point is the delay-constrained least-cost problem (DCLC) — a single delay constraint — on layered acyclic multi-digraphs (DAG).  Extension to general graphs and multiple resources (RCSPP) is planned where the techniques permit.

## Research

Original contributions contained in this repository include:

|Category|Contributions|Status|
|--------------|---------------------|----------|
|[DFS label-setting](docs/CSP/Labeling-DFS.md)|Backwards dominance pruning tightening traditional dominance|In progress| 
||Completion equivalence pruning to fathom labels that share the same completion value|In progress|

Formal problem definitions and detailed algorithm descriptions live in `docs/`. The implementation of each novel contribution lives in `src/`.

## Status

Actively under development, and best understood as a research prototype rather than a production-ready library. APIs and algorithmic components will evolve as the methods are refined and validated.

## Repository structure

- `src/` — implementation code of the novel contributions
- `benchmarks/` — console apps for benchmarking, plus reference implementations
- `docs/` — formal problem definitions and algorithm documentation

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (LTS)

### Build

Use the solution in benchmarks/ as the entry point:

```bash
dotnet build benchmarks/Benchmarks.slnx
```

### Run a benchmark

Each benchmark console generates random layered DAGs with random edge weights and delays, and writes results as CSV to a file.

```bash
dotnet run --project benchmarks/ConstrainedShortestPath -- results.csv
```

### Build configurations

|Configuration|Intended use|
|---------------------|---------------------|
|`Debug`|Validation and correctness checks with assertions; suitable for smaller instances|
|`Release`|Instrumentation and internal algorithm statistics, such as label counts|
|`Benchmark`|Timing-oriented runs for comparing algorithm performance|

## Contributing

This is a research project maintained on a best-effort basis. There is no response-time guarantee and no roadmap commitment. That said, several kinds of contribution are genuinely useful.

- *counter examples*: an instance where the solver returns a suboptimal or infeasible path or where the implementation disagrees with theory
- *reference algorithms and comparisons*: if you know of a method this project ought to be compared against, or prior art the documentation overlooks, say so
- *corrections to the documentation*: If an argument or explanation looks wrong or underspecified, an issue describing the gap is a real contribution.

For anything beyond a small fix, open an issue before opening a pull request.

## Citing

There is no publication or DOI yet. If you need to cite this code or part of the docs, please open an issue describing your intended use, and a DOI will be considered by e.g. a tagged release via Zenodo.

## License

MIT — see [LICENSE](LICENSE).

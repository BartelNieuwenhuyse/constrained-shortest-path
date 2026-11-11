# Benchmark runner for layered-DAG DCSP

This directory contains the benchmarking harness for the delay-constrained shortest path solvers implemented for layered DAGs. The goal of the runner is to generate many random instances, solve them with several algorithm variants, and record timings and quality metrics in a reproducible way.

This is a measurement harness, not the reference algorithm implementation. The actual solver logic lives in the project under the CSP sources; this tool is responsible for parameter generation, execution, and result collection.

## What the runner does

For each benchmark run, the harness:

- generates a random layered DAG
- assigns random edge weights and delays
- picks a delay bound that makes the instance non-trivial but still meaningful
- solves a dual relaxation to obtain a reference lambda / incumbent bound
- runs each configured solver on the same generated instance
- compares the returned feasibility and optimal weight across solvers
- writes one semicolon-delimited CSV row per graph instance
- stores run metadata in a per-run directory under the configured output path

The benchmark is designed for empirical evaluation and performance comparison rather than formal proof.

## Build

Use the project file under the benchmark folder:

```bash
dotnet build benchmarks/CSP/DCSP-lDAGs/DCSP-lDAG.csproj -c Release
```

For a validation-oriented build with extra assertions and verbose diagnostics, use:

```bash
dotnet build benchmarks/CSP/DCSP-lDAGs/DCSP-lDAG.csproj -c Debug
```

The build configuration controls the compile-time flags defined in [benchmarks/Directory.Build.props](benchmarks/Directory.Build.props):

- Debug: `TEST`, `PRINT_PROGRESS`, `INSTRUMENTATION`
- Release: `PRINT_PROGRESS`, `INSTRUMENTATION`

## Run

The runner accepts an optional configuration path as its first positional argument. If no path is supplied, it looks for `runsettings.json` in the current working directory.

```bash
cd benchmarks/CSP/DCSP-lDAGs
dotnet run -c Release --
```

or, from the repository root:

```bash
dotnet run --project benchmarks/CSP/DCSP-lDAGs/DCSP-lDAG.csproj -c Release -- benchmarks/CSP/DCSP-lDAGs/runsettings.json
```

Optional CLI flag:

- `--verbose`: enables extra console output; supported only in `TEST` builds (`Debug`)

## Configuration file

The runner reads a JSON object using case-insensitive property names, so `camelCase` and `PascalCase` keys both work. The shipped [benchmarks/CSP/DCSP-lDAGs/runsettings.json](benchmarks/CSP/DCSP-lDAGs/runsettings.json) contains the default values:

```json
{
  "seed": null,
  "solvers": [
    "dfs_dual",
    "dfs_dom",
    "dfs_back_dom",
    "dfs_eq",
    "dfs_eq_weight"
  ],
  "outputPath": "./",
  "cancellationSeconds": 600,
  "minMillisecondsToElapse": 1000,
  "minTimesToRun": 3
}
```

Key fields:

- `seed`: random seed used for reproducible generation. If omitted or set to null, a random seed is chosen.
- `maxRuns`: maximum number of graph instances to generate; default is `int.MaxValue` when not specified.
- `solvers`: list of solver names to benchmark.
- `varsMin`: minimum graph size for generated instances.
- `varsMax`: maximum graph size for generated instances.
- `outputPath`: directory where the CSV output and run metadata are written.
- `cancellationSeconds`: timeout for each solver invocation.
- `minMillisecondsToElapse`: minimum elapsed measurement before the runner stops repeating a solver.
- `minTimesToRun`: minimum repeat count before the runner stops repeating a solver.

The available solver names are:

- `dfs_dual`
- `dfs_dom`
- `dfs_back_dom`
- `dfs_eq`
- `dfs_eq_weight`

## Output

The runner writes a semicolon-separated CSV file named `DelayConstraintShortestPath.csv` into the configured `outputPath`.

Each row records:

- graph topology: node count, edge count, layer width, number of layers
- instance difficulty: weight-delay correlation, max delay, least delay, and delay along the shortest-weight path
- solution quality: unconstrained shortest weight, dual solution metrics, and the optimal solution reported by the solvers
- solver timings: per solver elapsed time, run count, and timeout flag

When `INSTRUMENTATION` is enabled, additional columns are appended for:

- iterations
- number of labels
- number of paths
- number of operations

The harness also creates a unique run directory under `outputPath` containing `metadata.json`. That metadata file includes:

- run ID
- UTC timestamp
- Git revision (`SourceRevisionId`)
- build configuration
- target framework
- runtime and OS information
- CPU count and available memory
- the full parameter set used for that run
- preprocessor defines active for the build

## Validation model

The benchmark is not a formal unit-test suite. It relies on randomized validation:

- differential testing: all exact solvers for the same instance must agree on feasibility and optimal cost
- invariant checks: in `TEST` builds, the code asserts structural properties of the label set during execution

This is intended to catch logic errors and inconsistent pruning semantics during development; it is still an empirical validation strategy rather than a mathematical proof.

## Notes

- Use `Debug` for correctness-oriented runs and local validation.
- Use `Release` for larger benchmark sweeps and performance measurement.
- For reproducibility, keep the seed, generator settings, and metadata together with each output directory.
- The solver theory and algorithmic rationale belong in the repository docs, while this file documents the harness itself.

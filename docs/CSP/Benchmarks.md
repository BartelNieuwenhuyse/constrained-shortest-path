# Benchmark results

Results of benchmark runs of the solvers in this repository on randomly generated layered DAG instances. This page is updated as runs are added.

## Solver overview

### Depth first label setting

Solvers in the category depth first label setting are described in [Labeling-DFS](Labeling-DFS.md).

| Solver name | Variant | Pruning | Code entry point |
| ----------- | ------- | ------- | ----------- |
| dfs_dom | Pulse-style | f + wb + dom | run_dfs_dominance |
| dfs_dual | LRE-style | f + wb + lb | run_dfs_dual |
| dfs_back_dom | Backward dominance | f + wb + bd | run_dfs_back_dominance |
| dfs_eq | Delay completion equivalence | f + dce | run_dfs_delay_completion_equivalence |
| dfs_eq_weight | Weight completion equivalence | f + wce | run_dfs_weight_completion_equivalence |

The pruning-rule ids are the ones defined in [Labeling.md](Labeling.md): `f` = feasibility, `wb` = weight bound, `lb` = Lagrangian bound, `dom` = dominance, `bd` = backward dominance, `dce` = delay completion equivalence, `wce` = weight completion equivalence.

## Results


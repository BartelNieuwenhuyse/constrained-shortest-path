# Benchmark results

Results of benchmark runs of the solvers in this repository on randomly generated layered DAG instances. This page is updated as runs are added.

## Solver overview

### Dual solvers

Solvers that solve the dual problem are described in [Lagrange-Dual](Lagrange-Dual.md).

| Solver name | Variant | Code entry point |
| ----------- | ------- | ---------------- |
| kelley | LARAC | run_kelley |
| kelley_pd | LARAC with tie-breaking | run_kelley_pd |

### Depth first label setting

Solvers in the category depth first label setting are described in [Labeling-DFS](Labeling-DFS.md).

| Solver name | Variant | Pruning | Code entry point |
| ----------- | ------- | ------- | ----------- |
| dfs_dom | Pulse-style | f + wb + dom | run_dfs_dominance |
| dfs_dual | LRE-style | f + wb + lb | run_dfs_dual |
| dfs_back_dom | Backward dominance | f + wb + bd | run_dfs_back_dominance |
| dfs_eq | Delay completion equivalence | f + dce | run_dfs_delay_completion_equivalence |
| dfs_eq_weight | Weight completion equivalence | f + wce | run_dfs_weight_completion_equivalence |

The pruning rules are defined in [Pruning](Labeling.md#pruning): `f` = feasibility, `wb` = weight bound, `lb` = Lagrangian bound, `dom` = dominance, `bd` = backward dominance, `dce` = delay completion equivalence, `wce` = weight completion equivalence.

## Results

The full results can be found in the [result](../../benchmarks/results/raw/) folder. There you can also find benchmark parameters and the environment on which it was run.

### Graph instances

The following metrics are expressions of the problem size.

| Id | nodes | edges | width | height |
| -- | ----- | ----- | ----- | ------ |
| 2 | 8 827 | 13 322 | 125 | 95 |
| 3 | 2 506 | 3 748 | 46 | 75 |
| 4 | 637 | 936 | 7 | 154 |
| 9 | 10 927 | 16 349 | 107 | 127 |
| 12 | 10 144 | 15 223 | 121 | 111 |
| 14 | 22 731 | 33 982 | 170 | 163 |
| 18 | 893 | 1 343 | 12 | 115 |

The following metrics express the problem difficulty:

- *correlation*: weight and delay are randomly generated on every edge. A correlation factor is used during the generation: near 1 means that weight and delay are reversely correlated, i.e. when an edge has high weight it has low delay and vice versa. Correlation near 0 means that weight and delay are uniformly random and there is no relation between them.
- *tightness*: the tightness of the constraint is reported as $(D - \underline{d}({p_{s \rightarrow t}}))/(\overline{d}({p_{s \rightarrow t}}) - \underline{d}({p_{s \rightarrow t}}))$ where $D$ is the constraint limit, $\underline{d}({p_{s \rightarrow t}})$ is the least delay path and $\overline{d}({p_{s \rightarrow t}})$ is the delay along the shortest weight path. High values near or above 1 mean the constraint is loose (shortest weight path is near feasible), while low values mean the constraint is tight and forces feasible paths close to the least-delay path.
- *opt gap*: equals $(w^* - \underline{w})/w^*$ where $w^*$ is the weight of the optimal feasible path and $\underline{w}$ is the shortest unconstrained weight path. It measures the cost of the constraint.
- *dual gap*: $(w^* - d^*)/w^*$ where $w^*$ is the weight of the optimal feasible path and $d^*$ is the optimum of the dual problem. It measures how much of the constraint cost is left to explain after the Lagrangian dual. A value of 0 indicates the dual has solved the problem to optimality.

| Id | correlation | tightness | opt gap | dual gap |
| -- | ----------- | --------- | ------- | -------- |
| 2 | 38 | 0.9059 | 0.02108 | 0.01010 |
| 3 | 34 | 0.1059 | 0.3715 | 0.03731 |
| 4 | 76 | 0.7881 | 0.2238 | 0.002623 |
| 9 | 50 | 0.2540 | 0.4458 | 0.0006467 |
| 12 | 99 | 0.4009 | 0.5962 | 0.003175 |
| 14 | 30 | 0.3161 | 0.1974 | 0.002841 |
| 18 | 13 | 0.9792 | 0.001705 | 0.001409 |

### Dual solvers

Run times in milliseconds of the dual solvers:

| Id | kelley pd |
| -- | --------- |
| 2 | 3.64 |
| 3 | 0.8978 |
| 4 | 0.2403 |
| 9 | 4.836 |
| 12 | 4.476 |
| 14 | 10.93 |
| 18 | 0.275 |

Number of paths:

| Id | kelley pd |
| -- | --------- |
| 2 | 14 |
| 3 | 15 |
| 4 | 17 |
| 9 | 17 |
| 12 | 14 |
| 14 | 13 |
| 18 | 13 |

### Depth first label setting

Run times in milliseconds of the dfs solvers:

| Id | dfs dual | dfs dom | dfs back dom | dfs eq | dfs eq weight |
| -- | -------- | ------- | ------------ | ------ | ------------- |
| 2 | 4.617 | 4.580 | 4.744 | - | 25.45 |
| 3 | 1.289 | 2.499 | 2.176 | 7.664 | 2.4 |
| 4 | 9.187 | 233.8 | 96 | 1 718 | 31.69 |
| 9 | 11.14 | 9 548 | 3 165 | - | 953 |
| 12 | 17.80 | 10 713 | 3 028 | - | 1003 |
| 14 | 13.60 | 1 786 | 830.3 | - | 587.7 |
| 18 | 0.3615 | 1.850 | 2.123 | - | 3.562 |

Note: the times reported for dfs dual are the combined time for solving the dual and the enumeration phase to close the remaining duality gap.

Label count:

| Id | dfs dual | dfs dom | dfs back dom | dfs eq | dfs eq weight |
| -- | -------- | ------- | ------------ | ------ | ------------- |
| 2 | 960 | 23 743 | 18 876 | - | 103 365 |
| 3 | 1 918 | 21 430 | 12 862 | 39 377 | 12 886 |
| 4 | 433 898 | 1 704 515 | 546 241 | 3 184 987 | 166 676 |
| 9 | 154 297 | 41 408 345 | 12 115 000 | - | 2 144 634 |
| 12 | 334 799 | 39 540 323 | 11 807 681 | - | 2 412 322 |
| 14 | 2 137 | 8 976 433 | 3 408 107 | - | 1 655 053 |
| 18 | 171 | 25 125 | 21 484 | - | 21 032 |

The **dual-based solver** (`kelley_pd` combined with `dfs_dual`)
outperforms the direct search variants, often by orders of magnitude,
in our delay-constrained setting. Solving the dual over a single
constraint is a relatively easy problem: Kelley's method converges in
13–17 cuts on *all* instances, independent of size, and the remaining
duality gap is small. Together with the data-structure-free depth-first
search, this explains `dfs_dual`'s performance. Open questions remain
in how far the other solvers can close the gap when combined with
Lagrangian pruning (or merely warm-started with the dual incumbent),
and in how far this carries over to multiple resources, where the dual
becomes harder to solve and duality gaps grow — though label setting
suffers from exponential growth there as well.

Comparing the two **dominance-based solvers**, backward dominance
uses 1.2–3.4× fewer labels, with the largest gains on the hardest
instances (#4, #9, #12, #14). On the two easiest instances (#2, #18)
fewer labels does not translate into faster runs — the post-recursion implementation (first recurse all edges, then derive the dominators)  adds overhead.

Comparing the two **dominance based solvers** (`dfs_dom` and `dfs_back_dom`), backward dominance consistently beats forward dominance (~2–3× fewer labels). Showing the strength of back propagating dominance and earlier pruning.

**Delay completion equivalence** (`dfs_eq`) is the weakest of the dfs
group: it exceeds the timeout on 5 of 7 instances, including the two
smallest ones. It is conceptually clean but not competitive.

**Weight completion equivalence** (`dfs_eq_weight`) is the strongest
direct solver, 5–19× fewer labels than forward dominance. It is
outperformed on #2 and #18, with #3 a near-tie. These are the loosest
tightness values in the set (δ ≥ 0.79), and the cases expose the
missing incumbent-based weight pruning: with a small *absolute*
optimality gap a good incumbent prunes the feasible space aggressively,
while `dfs_eq_weight` must map the entire feasible space. Adding
incumbent-driven interval truncation is the obvious next step.

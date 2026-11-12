# Label setting for constrained shortest paths

Label setting is one of the standard exact methods for the constrained shortest path problem (CSP). Early work dealing with CSP was based on dynamic programming  [Joksch, 1966]. Label setting can be seen as an improvement of dynamic programming methods [Aneja et al. 1983] and remains an actively studied state-of-the-art technique to solve CSP. See for example the recent work of [Ahmadi et al., 2025].

For the CSP with a single constraint, a label $l$ is a tuple $(w(p_{s \rightarrow v}), d(p_{s \rightarrow v}))$ associated with a node $v$ where $w(p_{s \rightarrow v})$ is the weight and $d(p_{s \rightarrow v})$ the delay of a partial path $p_{s \rightarrow v}$ from source $s$ to node $v$. In the remainder, a label may also be written as $(w,d,v)$ with the third element $v$ indicating the node the label belongs to.

Label setting starts with label $(0,0)$ at the source $s$ and repeatedly expands the label $l=(w(p_{s \rightarrow v}), d(p_{s \rightarrow v}))$ along outgoing edges of $v$. Expansion of label $l$ along edge $e_{vu}$ from node $v$ to its successor node $u$, generates a new label $l'$ by adding the edge's weight $w(e_{vu})$ and delay $d(e_{vu})$ giving the new label $l'=(w(p_{s \rightarrow v}) + w(e_{vu}), d(p_{s \rightarrow v}) + d(e_{vu}))$ at node $u$.

## Ordering

The order in which labels are expanded, varies across implementations [Desrochers and Soumis, 1988]. In this project we consider the following ordering variants:

- [Depth first ordering](Labeling-DFS.md)

## Pruning

If we would naively expand every label, the number of partial paths can grow exponentially. To make label setting efficient, the algorithm relies on pruning to discard labels that cannot participate in an optimal feasible solution. See [Dumitrescu and Boland, 2003], [Carlyle et al., 2008] or [Lozano  and Medaglia, 2013] for standard pruning rules.

The standard often used pruning rules include:

| Pruning | Description |
| ------------ | ------------------ |
| Feasibility | A label $l$ is infeasible if its current delay $d(p_{s \rightarrow v})$ plus the minimum possible remaining delay to the terminal $\underline{d}(p_{v \rightarrow t})$ already exceeds the maximum allowed delay $D$. |
| | $d(p_{s \rightarrow v}) + \underline{d}(p_{v \rightarrow t}) \gt D$ |
| Weight bound | A label $l$ cannot lead to a better solution whenever the current weight $w(p_{s \rightarrow v})$ plus the minimum possible remaining weight to the terminal $\underline{w}(p_{v \rightarrow t})$ does not improve on the best known solution $\overline{w}(p_{s \rightarrow t})$. Whenever the incumbent improves, the bound tightens. |
| | $w(p_{s \rightarrow v})+ \underline{w}(p_{v \rightarrow t}) \ge \overline{w}(p_{s \rightarrow t})$ |
| Lagrangian bound | A lower bound is obtained by combining weight and delay using a Lagrange multiplier $\lambda$: $c_{\lambda} = w + \lambda * d$. A label $l$ cannot lead to a better solution whenever its Lagrangianized cost $c_{\lambda}(p_{s \rightarrow v})$ shifted by the constant $\lambda * D$ plus the minimum possible Lagrangianized cost to terminal $\underline{c}_{\lambda}(p_{v \rightarrow t})$ exceeds the incumbent $\overline{w}(p_{s \rightarrow t})$. Whenever the incumbent improves, the bound tightens. |
| | $c_{\lambda}(p_{s \rightarrow v}) - \lambda * D + \underline{c}_{\lambda}(p_{v \rightarrow t}) \ge \overline{w}(p_{s \rightarrow t})$ |
| Dominance | Label $l$ is dominated by label $l′$ at the same node if $l′$ is strictly better or equal on both weight and delay, so any feasible extension from $l$ is also achievable from $l′$ with lower or equal total weight.|
| | $l' \preceq l \Leftrightarrow w(p'_{s \rightarrow v}) \le w(p_{s \rightarrow v})\ and\ d(p'_{s \rightarrow v}) \le d(p_{s \rightarrow v})$ |

We propose two novel pruning rules specifically in the context of depth first ordering (DFS).

| Pruning (DFS) | Description |
| ------------- | ----------- |
| Completion equivalence | Let $[L,U)$ be an interval such that all labels with a delay in this interval $L \leq d(p_{s \rightarrow v}) \lt U$ share the same feasible shortest weight completion to the terminal node $t$. Once such interval is identified, the solution of continuing any label $l$ falling in the interval is already known and does not need to be computed again. |
| |$d(p_{s \rightarrow v}) \in [L,U)$ |
| Backward dominance | When a label $l$ at node $v$ is expanded along edge $e$ and dominated at node $u$ by (weight, delay) pair $(w',d')$, then any label dominated by that pair's residual (weight, delay) after subtracting the edge $e$ is guaranteed to be dominated at node u, enabling stronger pruning. |
| | $(w',d',u) \preceq l_{u} \Rightarrow (w' - w(e_{vu}), d' - d(e_{vu})) \preceq l_{v}$ |

[Read more](Labeling-DFS.md) about these new pruning techniques.

Note that pruning on bounds and feasibility require a backward shortest-path calculations to get the least delay and shortest weight paths from each node to the terminal. This initialization phase preceeds the actual label setting recursion:

```text
initialization:
1  compute least delay from each node to the terminal
2  if (least delay from s to t) > D:
3    problem is infeasible:
4      stop
5
6  compute shortest weight from each node to the terminal
7  if (delay of shortest weight path from s to t) <= D:
8    unconstrained shortest weight path is optimal:
9      stop
```

## References

Ahmadi, S., Raith, A., Tack, G. & Jalili, M., 2025, Proceedings of the AAAI Conference on Artificial Intelligence. Walsh, T., Shah, J. & Kolter, Z. (eds.). Washington DC USA: Association for the Advancement of Artificial Intelligence (AAAI), p. 26878-26885 8 p. (Proceedings of the AAAI Conference on Artificial Intelligence; vol. 39, no. 25).

Aneja, Y., Aggarwal, V., and Nair, K., (1983), “Shortest Chain Subject to Side Conditions,” Networks, 13, pp. 295–302.

Carlyle, W.M., Royset, J.O. and Kevin Wood, R. (2008), Lagrangian relaxation and enumeration for solving constrained shortest-path problems†. Networks, 52: 256-270. https://doi.org/10.1002/net.20247

Desrochers, M., & Soumis, F. (1988). A Generalized Permanent Labelling Algorithm For The Shortest Path Problem With Time Windows. INFOR: Information Systems and Operational Research, 26(3), 191–212. https://doi.org/10.1080/03155986.1988.11732063

Dumitrescu, I. and Boland, N. (2003), Improved preprocessing, labeling and scaling algorithms for the Weight-Constrained Shortest Path Problem. Networks, 42: 135-153. https://doi.org/10.1002/net.10090

Joksch, H., (1966), “The Shortest Route Problem with Constraints,” Journal of Mathematical Analysis and Application, 14, pp. 191–197.

Lozano L., Medaglia A. L. (2013). On an exact method for the constrained shortest path problem Computers & Operations Research, Volume 40, Issue 1, Pages 378-384, https://doi.org/10.1016/j.cor.2012.07.008.
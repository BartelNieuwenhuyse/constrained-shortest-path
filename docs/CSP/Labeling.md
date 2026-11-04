# Label setting for constrained shortest paths

Label setting is one of the standard exact methods for the constrained shortest path problem (CSP). Early work dealing with CSP was based on dynamic programming  [Joksch, 1966]. Label setting can be seen as an improvement of dynamic programming methods [Aneja et al. 1983] and remains an actively studied state-of-the-art technique to solve CSP. See for example the recent work of [Ahmadi et al., 2025].

For the CSP with a single constraint, a label $l$ is a tuple $(w(p_{s \rightarrow v}), d(p_{s \rightarrow v}),v)$ associated with a node $v$ where $w(p_{s \rightarrow v})$ is the weight and $d(p_{s \rightarrow v})$ the delay of a partial path $p_{s \rightarrow v}$ from source $s$ to node $v$.

Label setting starts with label $(0,0,s)$ at the source $s$ and repeatedly expands the label $l=(w(p_{s \rightarrow v}), d(p_{s \rightarrow v}),v)$ along outgoing edges of $v$. Expansion of label $l$ along edge  $e_{vu}$ from node $v$ to its successor node $u$, generates a new label $l'$ by adding the edge's weight $w(e_{vu})$ and delay $d(e_{vu})$: $l'=(w(p_{s \rightarrow v}) + w(e_{vu}), d(p_{s \rightarrow v}) + d(e_{vu}), u)$.

The order in which labels are expanded, varies across implementations. In this project we consider the following ordering variants:

- [Depth first ordering](Labeling-DFS.md)

If we would naively expand every label, the number of partial paths can grow exponentially. To make label setting efficient, the algorithm relies on pruning to discard labels that cannot participate in an optimal feasible solution. See [Dumitrescu and Boland, 2003] for an overview of standard pruning rules.

The standard often used pruning rules include:

| Pruning | Description | Rule |
| ------------ | ------------------ | ------- |
| Feasibility | A label $l$ is infeasible if its current delay $d(p_{s \rightarrow v})$ plus the minimum possible remaining delay to the terminal $\lfloor d(p_{v \rightarrow t})\rfloor$ already exceeds the maximum allowed delay $D$. | $d(p_{s \rightarrow v}) + \lfloor d(p_{v \rightarrow t})\rfloor \gt D$ |
| Incumbent | A label $l$ cannot lead to a better solution whenever the current weight $w(p_{s \rightarrow v})$ plus the minimum possible remaining weight to the terminal $\lfloor w(p_{v \rightarrow t})\rfloor$ does not improve on the best known solution $\lceil w^*(p_{s \rightarrow t})\rceil$  | $w(p_{s \rightarrow v})+ \lfloor w(p_{v \rightarrow t})\rfloor \ge \lceil w^*(p_{s \rightarrow t})\rceil$ |
| Lower bound | A lower bound is obtained by combining weight and delay using a Lagrange multiplier: $c_{\lambda} = w + \lambda * d$. A label $l$ cannot lead to a better solution whenever it Lagrangianized cost $c_{\lambda}(p_{s \rightarrow v})$ shifted by the constant $\lambda * D$ plus the minimum possible Lagrangianized cost to terminal $\lfloor c_{\lambda}(p_{t \rightarrow t})\rfloor$ exceeds the incumbent $\lceil w^*(p_{s \rightarrow t})\rceil$ | $c_{\lambda}(p_{s \rightarrow v}) - \lambda * D + \lfloor c_{\lambda}(p_{t \rightarrow t})\rfloor \ge \lceil w^*(p_{s \rightarrow t})\rceil$ |
| Dominance | Label $l$ is dominated by label $l′$ at the same node if $l′$ is strictly better or equal on both weight and delay, so any feasible extension from $l$ is also achievable from $l′$ with lower or equal total weight.| $l \preceq l' \Leftrightarrow\\ w(p_{s \rightarrow v}) \le w(p'_{s \rightarrow v})\ \&\ d(p_{s \rightarrow v}) \le d(p'_{s \rightarrow v})$ |

We propose two novel pruning rules specifically in the context of depth first ordering.

| Pruning | Description |
| ------------ | ------------------ |
| Completion equivalence | Prunes labels that have equivalent weight or delay completion to terminal ||
| Backwards dominance | Tighter dominance pruning when all expansions of a label along all edges will be dominated somewhere in the subgraph ||

[Read more](Labeling-DFS.md) about these new pruning techniques.

## References

Ahmadi, S., Raith, A., Tack, G. & Jalili, M., 2025, Proceedings of the AAAI Conference on Artificial Intelligence. Walsh, T., Shah, J. & Kolter, Z. (eds.). Washington DC USA: Association for the Advancement of Artificial Intelligence (AAAI), p. 26878-26885 8 p. (Proceedings of the AAAI Conference on Artificial Intelligence; vol. 39, no. 25).

Aneja, Y., Aggarwal, V., and Nair, K., (1983), “Shortest Chain Subject to Side Conditions,” Networks, 13, pp. 295–302.

Dumitrescu, I. and Boland, N. (2003), Improved preprocessing, labeling and scaling algorithms for the Weight-Constrained Shortest Path Problem. Networks, 42: 135-153. https://doi.org/10.1002/net.10090

Joksch, H., (1966), “The Shortest Route Problem with Constraints,” Journal of Mathematical Analysis and Application, 14, pp. 191–197.
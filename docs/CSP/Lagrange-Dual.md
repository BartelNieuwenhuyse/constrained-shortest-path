# Lagrange dual for constrained shortest paths

Lagrangian relaxation is a widely used relaxation method in constrained optimization. It has a long history in the constrained shortest path literature and remains a popular heuristic as well as a often used phase 1 to strengthen exact solving techniques.

Lagrangian relaxation removes constraints and instead puts them into the objective function by applying a multiplier $\lambda$ that penalizes constraint violations. In the case of the constrained shortest path problem, this turns the problem into an unconstrained shortest path problem with modified cost structure.

If we define the cost $c_{\lambda}$ of an edge or path in terms of the weight $w$ and delay $d$ as:

$$
\begin{align}
c_{\lambda}(e_{vu}) = w(e_{vu}) + \lambda \cdot d(e_{vu})
\end{align}
$$

Then the relaxed problem

$$
\begin{align}
min_{p \in G}\ w(p_{s \rightarrow t}) + \lambda \cdot (d(p_{s \rightarrow t}) - D)
\end{align}
$$

becomes the unconstrained shortest path problem:

$$
\begin{align}
min_{p \in G}\ c_{\lambda}(p_{s \rightarrow t})\\
\end{align}
$$

The solution of this relaxed unconstrained shortest path problem gives a lower bound on the optimum of the original constrained shortest path problem for any value of $\lambda \ge 0$. The problem of finding the highest lower bound is the Lagrange dual problem. An overview and thorough description of the primal and dual interpretations can be found in [Ziegelmann, 2007].

Solving the Lagrange dual, does not mean solving the original constrained shortest path. Although algorithms can often find -next to the dual solution- a good primal incumbent, a duality gap may exist or the optimal path may not have been found. Algorithms in the Lagrangian relaxation category are therefore not exact. Nevertheless, solving the Lagrange dual often gives useful information that can be exploited by other techniques to find the exact solution.

This documentation describes the algorithms in this repository that solve the Lagrange dual problem. The solution of the Lagrange dual problem can be used by exact solvers. These exact solvers are described elsewhere. See the [overview](../README.md).

## Reference algorithms

### LARAC

This dual iteration algorithm was introduced by [Handler and Zang, 1980] for DCLC. It later became widely known under the name LARAC, after [Jüttner et al., 2001] independently rediscovered it. It is the default standard algorithm to solve the dual problem in the delay-constraint shortest path literature. It's a specialization of kelley's cutting plane method.

#### Base

The base LARAC algorithm start with computing the two extreme paths: the shortest unconstrained weight path and the least delay path.

```text
initialization:
1  weight_path = compute shortest weight from source to the terminal
2  if d(weight_path) <= D:
3    unconstrained shortest weight path is optimal:
4      stop
5
6  delay_path = compute least delay from source to the terminal
7  if d(delay_path) > D:
8    problem is infeasible:
9      stop
```

It then continues by repeatedly calculating the intersection of the two bounding paths as a new estimate for the dual multiplier lambda. After having calculated a new shortest cost path worth this lambda, it either establishes optimality or updates one of the two bounding paths to the new path.

```text
repeat:
1  lambda = (w(weight_path) - w(delay_path)) / (d(delay_path) - d(weight_path))
2  cost_path = compute shortest cost path for lambda
3  if c_λ(cost_path) == c_λ(weight_path):
4    lambda is optimal:
5      stop
6  if d(cost_path) <= D:
7    delay_path = cost_path
8  if d(cost_path) > D:
9    weight_path = cost_path
```

Observe that there is no tie-breaking in the algorithm: if multiple paths have the same cost, any path can be returned by the shortest cost path computation at line 2.

#### Tie-breaking

Stabilizing LARAC by tie-breaking on the two extremes (least-delay and shortest-weight) is not new: [Kou et al. 2020] used a modified dijkstra algorithm to compute the two extremes among all equal cost paths.

We propose a slightly different algorithmic approach, acknowledging that only one of the two extremes is required: first compute an arbitrary shortest cost path just like the algorithm without tie-breaking. If this path is delay-feasible, only the shortest weight extreme among all equal cost paths is relevant for the advance of the algorithm, while if the path is delay-infeasible, only the least delay extreme among all equal cost paths is relevant. After having solved the shortest cost path, we can extract the tie-breaking path (least delay or shortest weight) by solving a shortest path computation over the equality graph: only consider edges whose slack is zero. The slack of an edge is $`s(e_{vu}) = c_{\lambda}(e_{vu}) - (c_{\lambda}(p_{s \rightarrow u}) - c_{\lambda}(p_{s \rightarrow v}))`$ which is zero whever the edge $e_{vu}$ is a part of a shortest cost path from source to node $u$.

```text
6   if d(cost_path) <= D:
7     tie_path = shortest weight on equality graph
8     if d(tie_path) > D:
9       lambda is optimal:
10        stop
11    delay_path = tie_path
12
13  if d(cost_path) > D:
14    tie_path = least delay on equality graph
15    if d(tie_path) <= D:
16      lambda is optimal:
17        stop
18    weight_path = tie_path
```

Tie breaking can decrease the number of iterations needed to solve the dual by more closely tracking the hull. It can also improve the incumbent: at the optimal lambda, the non-tie-breaking variant may only find an infeasible path, while with tie-breaking a feasible path is always found with the optimal lambda (which is better than any feasible path found for larger lambda during the iteration). 

A worked out example can be found in [example-2](examples/Example-2-Dual.md).

## References

Handler, G.Y. and Zang, I. (1980), A dual algorithm for the constrained shortest path problem. Networks, 10: 293-309. https://doi.org/10.1002/net.3230100403

Juttner A., Szviatovski B., Mecs I. and Rajko Z. (2001). Lagrange relaxation based method for the QoS routing problem. Proceedings IEEE INFOCOM 2001. Conference on Computer Communications, pp. 859-868 vol.2. https://doi.org/10.1109/INFCOM.2001.916277

Kou C., Hu D., Yuan J. and Ai W. (2020). Bisection and Exact Algorithms Based on the Lagrangian Dual for a Single-Constrained Shortest Path Problem. In: IEEE/ACM Transactions on Networking, vol. 28, no. 1, pp. 224-233. https://doi.org/10.1109/TNET.2019.2955451

Ziegelmann, M. (2007). Constrained shortest paths and related problems. https://doi.org/10.22028/D291-23753

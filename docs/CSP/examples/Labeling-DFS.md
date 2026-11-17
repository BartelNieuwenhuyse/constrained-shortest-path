# Example

We'll use the following example in which edges are annotated with (weight,delay) pairs and the delay constraint is $\le 23$

```mermaid
flowchart LR
  %% Source
  S((s))

  %% Layer 1
  N1((1))

  %% Layer 2
  N2((2))
  N3((3))

  %% Terminal
  T((T))

  %% Edges
  S -->|1,9| N1
  S -->|0,4| N1
  
  N1 -->|2,1| N2
  N1 -->|3,4| N2
  N1 -->|2,4| N3
  
  N2 -->|3,13| T
  N3 -->|2,16| T
  N3 -->|5,5| T
```

The least delay path goes via (0,4) edge over nodes 1 and 3 along edge (5,5) and has 13 delay and weight 7. The shortest weight path also goes along (0,4) edge via nodes 1 and 3 but ends with the (2,16) edge to give a weight of 4 and a delay of 24. Hence we can start with an incumbent of 7.

## Dominance vs. backward dominance

Below is a overview of the label transition graph of the Pulse-style dfs algorithm pruning on feasibility, weight bound and dominance. Each node from the original graph is replaced by a subgraph containing nodes corresponding to the non-dominated label set stored at that node for future dominance pruning. Edges represent the labels and are annotated with their accumulated (weight, delay). They point at the non-dominated label in the node's storage which they either created (if they were not dominated at the time of arrival) or by which they are dominated. A special X node corresponds to pruning by feasiblity, weight bound or both.

```mermaid
flowchart LR
  %% Nodes and there non-dominated set
  subgraph S
  S_1((0,0))
  end
  subgraph 1
  N1_1((1,9))
  N1_2((0,4))
  end
  subgraph 2
  N2_1((3,10))
  N2_2((2,5))
  X2((X))
  end
  subgraph 3
  N3_1((3,13))
  N3_2((2,8))
  end
  subgraph T
  T_1((6,23))
  T_2((5,18))
  XT((X))
  end

  %% Labels
  S_1 -->|1,9| N1_1
  N1_1 -->|3,10| N2_1
  N2_1 -->|6,23| T_1
  N1_1 -->|4,13| X2
  N1_1 -->|3,13| N3_1
  N3_1 -->|5,29| XT
  N3_1 -->|8,18| XT
  S_1 -->|0,4| N1_2
  N1_2 -->|2,5| N2_2
  N2_2 -->|5,18| T_2
  N1_2 -->|3,8| N2_2
  N1_2-->|2,8| N3_2
  N3_2 -->|4,24| XT
  N3_2 -->|7,13| XT  
```

The algorithm proceeds as follows:

A first pulse follows the (1,9) edge to the terminal through nodes 1 and 2, reaching the terminal with the feasible label (6,23) and improving the incumbent at T. When the same pulse branches to node 3 with label (3,13), it is pruned along both outgoing arcs: the (2,16) edge is infeasible, and the (5,5) edge is rejected by the incumbent's weight bound. The label (3,13) is then stored and the search backtracks.

A second pulse follows the (0,4) edge and dominates the first. It then repeats almost the same traversal, replacing the stored dominators at each node as it goes.

In total, the search expands 14 labels.

Compare to the back dominance label transition graph drawn below. Where nodes correspond to the dominators stored at each node of the original graph and edges again represent the labels.

```mermaid
flowchart LR
  %% Nodes and there non-dominated set
  subgraph S
  S_1((0,0))
  end
  subgraph 1
  N1_1((1,9))
  N1_2((0,4))
  end
  subgraph 2
  N2_1((3,10))
  N2_2((2,5))
  X2((X))
  end
  subgraph 3
  N3_1((1,8))
  end
  subgraph T
  T_1((6,23))
  T_2((5,18))
  XT((X))
  end

  %% Labels
  S_1 -->|1,9| N1_1
  N1_1 -->|3,10| N2_1
  N2_1 -->|6,23| T_1
  N1_1 -->|4,13| X2
  N1_1 -->|3,13| N3_1
  N3_1 -->|5,29| XT
  N3_1 -->|8,18| XT
  S_1 -->|0,4| N1_2
  N1_2 -->|2,5| N2_2
  N2_2 -->|5,18| T_2
  N1_2 -->|3,8| N2_2
  N1_2-->|2,8| N3_1
```

The key difference appears at node 3. When the first pulse arrives with label (3,13) and is pruned along both extensions, we can derive a stronger backward dominator and store it at node 3: (1,8). Any label dominated by (1,8) will then be pruned along both continuations: along the (5,5) edge it cannot improve on the incumbent weight 6, and along the (2,16) edge it will be infeasible. Below this threshold, the behavior changes: a label with weight 0 can still beat the incumbent on the (5,5) edge, and a label with delay 7 remains feasible on the (2,16) edge.

When the second pulse reaches node 3 with label (2,8), it is immediately discarded by dominance because the stored dominator (1,8) already covers it. It therefore does not need to be expanded further.

This version reduces the total number of expanded labels by 2: 12 in total instead of 14 with standard dominance.

## Completion equivalence

### Delay completion equivalence

The graph below shows the label transition graph of the delay completion equivalence algorithm. Each node from the original graph is replaced by a subgraph containing nodes corresponding to the delay intervals stored at that node for future equivalence pruning. An interval [L,U) is succeeded by its completion weight: `[L,U)_completion weight`. Edges represent the labels and are annotated with their accumulated (weight, delay). They point at the interval in the node's storage which they either created (if they were the first label within that interval) or to which they are equivalent.

```mermaid
flowchart LR
  %% Nodes and their intervals
  subgraph S
  S1(("[0,1)_5"))
  end
  subgraph 1
  N1_1(("[7,10)_5"))
  N1_2(("[4,7)_5"))
  end
  subgraph 2
  N2_1(("[-inf,11)_3"))
  X2(("[11,+inf)_+inf"))
  end
  subgraph 3
  N3_1(("[8,19)_5"))
  end
  subgraph T
  T1(("[-inf,24)_0"))
  XT(("[24,+inf)_+inf"))
  end

  %% Label transitions
  S1 -->|1,9| N1_1
  N1_1 -->|3,10| N2_1
  N2_1 -->|6,23| T1
  N1_1 -->|4,13| X2
  N1_1 -->|3,13| N3_1
  N3_1 -->|5,29| XT
  N3_1 -->|8,18| T1
  S1 --> |0,4| N1_2
  N1_2 -->|2,5| N2_1
  N1_2 -->|3,8| N2_1
  N1_2 -->|2,8| N3_1
```

If we redraw the label transition graph with the infeasible intervals removed and the original edge weights restored, the result is an exact representation of all feasible paths. In particular, the construction removes infeasible paths by splitting node 1 so that one copy has no (3,4) edge to node 2, and by deleting the (2,16) edge between node 3 and the terminal. A shortest path on this transformed graph therefore yields the optimal feasible solution as well.

```mermaid
flowchart LR
  %% Nodes and their intervals
  S1((S))
  N1_1((1))
  N1_2((1))
  N2_1((2))
  N3_1((3))
  T1((T))

  %% Label transitions
  S1 -->|1| N1_1
  S1 --> |0| N1_2
  N1_1 -->|2| N2_1
  N2_1 -->|3| T1
  N1_1 -->|2| N3_1
  N3_1 -->|5| T1  
  N1_2 -->|2| N2_1
  N1_2 -->|3| N2_1
  N1_2 -->|2| N3_1
```

### Weight completion equivalence

Weight completion reduces the label transition graph further by observing that splitting node 1 at delay 7 is unnecessary for the weight-optimal case. Along the (3,4) edge to node 2, a path is infeasible for delays $\ge 7$ and feasible for delays $< 7$, but this does not improve the best completion weight. The resulting label transition graph is shown below:

```mermaid
flowchart LR
  %% Nodes and their intervals
  subgraph S
  S1(("[0,1)_5"))
  end
  subgraph 1
  N1_1(("[4,10)_5"))
  end
  subgraph 2
  N2_1(("[-inf,11)_3"))
  X2(("[11,+inf)_+inf"))
  end
  subgraph 3
  N3_1(("[8,19)_5"))
  end
  subgraph T
  T1(("[-inf,24)_0"))
  XT(("[24,+inf)_+inf"))
  end

  %% Label transitions
  S1 -->|1,9| N1_1
  N1_1 -->|3,10| N2_1
  N2_1 -->|6,23| T1
  N1_1 -->|4,13| X2
  N1_1 -->|3,13| N3_1
  N3_1 -->|5,29| XT
  N3_1 -->|8,18| T1
  S1 --> |0,4| N1_1
```

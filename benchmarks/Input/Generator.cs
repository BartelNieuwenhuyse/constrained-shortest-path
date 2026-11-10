using DataStructures;
using InputGenerator.GraphGenerator;

namespace InputGenerator;

public class Generator(Random rand)
{
    public GraphData GenerateLayeredDAG(int vars_max = 15, int vars_min = 5, int max_width = 10, int out_degree_max = 5)
    {
        // create a DAG
        int vars = rand.Next(vars_min, vars_max);  // number of layers - 1

        int[] buckets = new int[max_width];
        List<List<int>> arcs = new List<List<int>>();
        int[][] layers = new int[vars + 1][];
        int number_of_nodes = 0;

        // start with a single source node
        layers[0] = new int[1];
        layers[0][0] = number_of_nodes++;
        arcs.Add(new List<int>());

        // iterate over the layers and create edges to the next layer
        // making nodes on the next layer as edges come in
        for (int l = 1, n, k; l < vars; ++l)
        {
            for (n = 0; n < max_width; ++n) buckets[n] = -1;

            int number_of_nodes_layer = 0;
            for (n = 0; n < layers[l - 1].Length; ++n)
            {
                int out_degree = rand.Next(1, out_degree_max);
                for (k = 0; k < out_degree; ++k)
                {
                    int dest = rand.Next(0, max_width);
                    if (buckets[dest] < 0)
                    {
                        buckets[dest] = number_of_nodes + number_of_nodes_layer++;
                        arcs.Add(new List<int>());
                    }
                    arcs[layers[l - 1][n]].Add(buckets[dest]);
                }
            }

            layers[l] = new int[number_of_nodes_layer];
            for (n = 0; n < number_of_nodes_layer; ++n)
            {
                layers[l][n] = number_of_nodes++;
            }
        }

        // connect everything to the terminal
        for (int n = 0; n < layers[vars - 1].Length; ++n)
        {
            arcs[layers[vars - 1][n]].Add(number_of_nodes);
        }
        layers[vars] = new int[1];
        layers[vars][0] = number_of_nodes++;
        arcs.Add(new List<int>());

        // convert arcs list to edges
        int[][] edges = new int[number_of_nodes][];
        for (int n = 0, k; n < number_of_nodes; ++n)
        {
            edges[n] = new int[arcs[n].Count];
            for (k = 0; k < arcs[n].Count; ++k)
            {
                edges[n][k] = arcs[n][k];
            }
        }

        return new GraphData(edges, layers);
    }

    public (int[][], int[][]) GenerateCosts(Graph graph, int max_edge_weight, int max_edge_delay, double correlation = 0.0)
    {
        // assign random weights on edges
        int[][] weight = new int[graph.number_of_nodes][];
        for (int i = 0, j; i < weight.Length; i++)
        {
            weight[i] = new int[graph.nodes[i].number_of_edges_out];
            for (j = 0; j < weight[i].Length; ++j)
            {
                weight[i][j] = rand.Next(0, max_edge_weight);
            }
        }

        // assign random delays on edges
        int[][] delay = new int[graph.number_of_nodes][];
        for (int i = 0, j; i < delay.Length; i++)
        {
            delay[i] = new int[graph.nodes[i].number_of_edges_out];
            for (j = 0; j < delay[i].Length; ++j)
            {
                delay[i][j] = (int)(correlation * (max_edge_weight - weight[i][j]) + (1 - correlation) * rand.Next(0, max_edge_delay));
            }
        }

        int pow_edge_weight = rand.Next(1, 3);
        for (int k = 1, i, j; k < pow_edge_weight; k++)
            for (i = 0; i < weight.Length; ++i)
                for (j = 0; j < weight[i].Length; ++j)
                    weight[i][j] *= weight[i][j];

        int pow_edge_delay = rand.Next(1, 3);
        for (int k = 1, i, j; k < pow_edge_delay; k++)
            for (i = 0; i < delay.Length; ++i)
                for (j = 0; j < delay[i].Length; ++j)
                    delay[i][j] *= delay[i][j];

        return (weight, delay);
    }
}

namespace InputGenerator.GraphGenerator;

using DataStructures;

/// <summary>
/// Builds a layered acyclic digraph (lDAG) from a given set of edges and layers.
/// </summary>
public static class GraphBuilder
{
    public static Graph Build(GraphData graphData)
    {
        int[][] edges = graphData.Edges;
        int[][] layers = graphData.Layers;

        // initialize the graph with number of layers
        Layer[] graph = new Layer[layers.Length];
        Node[] nodes = new Node[edges.Length];

        int[] node_layer = new int[edges.Length];       // node_layer[i] = j: node i is in layer j
        int[] node_index = new int[edges.Length];       // node_index[i] = j: node i is the jth node in its layer
        int[] node_edges = new int[edges.Length];       // node_edges[i] = j: node i has j incoming edges

        for (int i = 0, j, k; i < layers.Length; i++)
        {
            // create the ith layer with number of nodes in this layer
            graph[i] = new Layer(i, layers[i].Length);

            // calculat which nodes are in this layer and how many incoming edges it has
            for (j = 0; j < layers[i].Length; j++)
            {
                node_layer[layers[i][j]] = i; // store the index of the node's layer
                node_index[layers[i][j]] = j; // store the index of the node in its layer

                for (k = 0; k < edges[layers[i][j]].Length; ++k)
                {
                    node_edges[edges[layers[i][j]][k]] += 1;
                }
            }
        }

        // create the nodes
        for (int i = 0; i < edges.Length; ++i)
        {
            Node node = new Node(i, node_layer[i], node_index[i], node_edges[i], edges[i].Length);
            graph[node_layer[i]].nodes[node_index[i]] = node;
            nodes[i] = node;
            node_edges[i] = 0;
        }

        // create the edges
        for (int i = 0, j; i < edges.Length; ++i)
        {
            for (j = 0; j < edges[i].Length; ++j)
            {
                Node node_in = graph[node_layer[i]].nodes[node_index[i]];
                Node node_out = graph[node_layer[edges[i][j]]].nodes[node_index[edges[i][j]]];

                Edge edge = new Edge(j,node_edges[node_out.index_in_graph], node_in, node_out);

                node_out.edges_in[node_edges[node_out.index_in_graph]] = edge;
                node_edges[node_out.index_in_graph]++;

                node_in.edges_out[j] = edge;
            }
        }

        return new Graph(graph, nodes);
    }
}
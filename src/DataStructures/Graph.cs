namespace DataStructures;

/// <summary>
/// Represents a layered acyclic digraph (lDAG) with nodes and edges organized into layers.
/// </summary>
public readonly struct Graph
{
    public readonly Layer[] layers;
    public readonly Node[] nodes;
    public readonly Node source => layers[0].nodes[0];
    public readonly Node terminal => layers[number_of_layers - 1].nodes[0];

    public readonly int number_of_layers;      // number of layers in the graph
    public readonly int number_of_nodes;       // total number of nodes in the graph
    public readonly int number_of_edges;       // total number of edges in the graph
    public readonly int max_width;             // max number of nodes on one layer
    public readonly int max_edges_in_of_node;  // max number of edges between one layer and the previous layer
    public readonly int max_edges_out_of_node; // max number of edges between one layer and the next layer

    public Graph(Layer[] layers, Node[] nodes)
    {
        this.layers = layers;
        this.nodes = nodes;
        this.number_of_layers = layers.Length;

        this.max_width = 0;
        this.max_edges_in_of_node = 0;
        this.max_edges_out_of_node = 0;
        this.number_of_nodes = 0;
        this.number_of_edges = 0;

        for (int i = 0, j; i < layers.Length; ++i)
        {
            int widthOfLayer = layers[i].nodes.Length;
            this.number_of_nodes += widthOfLayer;
            if (widthOfLayer > this.max_width) { this.max_width = widthOfLayer; }

            for (j = 0; j < widthOfLayer; ++j)
            {
                int number_of_edges_in = layers[i].nodes[j].number_of_edges_in;
                number_of_edges += number_of_edges_in;
                if (number_of_edges_in > this.max_edges_in_of_node) { this.max_edges_in_of_node = number_of_edges_in; }

                int number_of_edges_out = layers[i].nodes[j].number_of_edges_out;
                if (number_of_edges_out > this.max_edges_out_of_node) { this.max_edges_out_of_node = number_of_edges_out; }
            }
        }
    }
}

public readonly struct Layer
{
    public readonly int index;
    public readonly Node[] nodes;
    public readonly int number_of_nodes;

    public Layer(int index, int number_of_nodes)
    {
        this.index = index;
        this.number_of_nodes = number_of_nodes;
        this.nodes = new Node[number_of_nodes];
    }
}

public readonly struct Node
{
    public readonly int index_of_layer;
    public readonly int index_in_graph;
    public readonly int index_in_layer;
    public readonly Edge[] edges_in;
    public readonly Edge[] edges_out;
    public readonly int number_of_edges_in;
    public readonly int number_of_edges_out;

    public Node(int index, int index_of_layer, int index_in_layer, int number_of_edges_in, int number_of_edges_out)
    {
        this.index_in_graph = index;
        this.index_of_layer = index_of_layer;
        this.index_in_layer = index_in_layer;
        this.number_of_edges_in = number_of_edges_in;
        this.edges_in = new Edge[number_of_edges_in];
        this.number_of_edges_out = number_of_edges_out;
        this.edges_out = new Edge[number_of_edges_out];
    }

    public static bool operator ==(Node x, Node y)
    {
        return x.index_in_graph == y.index_in_graph;
    }
    public static bool operator !=(Node x, Node y)
    {
        return x.index_in_graph != y.index_in_graph;
    }
    public override bool Equals(object? obj)
    {
        return obj is Node x && (this == x);
    }
    public override int GetHashCode()
    {
        return this.index_in_graph;
    }
}

public readonly struct Edge
{
    public readonly int index_in_node_in;
    public readonly int index_in_graph_of_node_in;
    public readonly int index_in_layer_of_node_in;
    public ref readonly Node node_in(in Graph graph) => ref graph.nodes[index_in_graph_of_node_in];
    public ref readonly Node node_in(in Layer layer) => ref layer.nodes[index_in_layer_of_node_in];

    public readonly int index_in_node_out;
    public readonly int index_in_graph_of_node_out;
    public readonly int index_in_layer_of_node_out;
    public ref readonly Node node_out(in Graph graph) => ref graph.nodes[index_in_graph_of_node_out];
    public ref readonly Node node_out(in Layer layer) => ref layer.nodes[index_in_layer_of_node_out];

    public Edge(int index_in_node_in, int index_in_node_out, Node node_in, Node node_out)
    {
        this.index_in_node_in = index_in_node_in;
        this.index_in_node_out = index_in_node_out;
        this.index_in_graph_of_node_in = node_in.index_in_graph;
        this.index_in_layer_of_node_in = node_in.index_in_layer;
        this.index_in_graph_of_node_out = node_out.index_in_graph;
        this.index_in_layer_of_node_out = node_out.index_in_layer;
    }

    public static bool operator ==(Edge x, Edge y)
    {
        return x.index_in_graph_of_node_in == y.index_in_graph_of_node_in && x.index_in_node_in == y.index_in_node_in;
    }
    public static bool operator !=(Edge x, Edge y)
    {
        return x.index_in_graph_of_node_in != y.index_in_graph_of_node_in || x.index_in_node_in != y.index_in_node_in;
    }
    public override bool Equals(object? obj)
    {
        return obj is Edge x && (this == x);
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(this.index_in_graph_of_node_in, this.index_in_node_in);
    }
}

﻿using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

namespace C2M2.NeuronalDynamics.UGX
{
    /// <summary>
    /// This is the Neuron Class, it allows the user to initialize the different quantities of the geometry
    /// and gives some ease of access to the the components of the geometry
    /// </summary>
    public class Neuron
    {
        /// <summary>
        /// This is the list of nodes that the 1d graph geometry has
        /// </summary>
        public List<NodeData> nodes = new List<NodeData>();
        /// <summary>
        /// this is the list of edges in the 1d graph geometry
        /// </summary>
        public List<Edge> edges = new List<Edge>();
        /// <summary>
        /// this is the a list of the nodes that are the ends of the dendrites, they only have one neighbor
        /// </summary>
        public List<NodeData> boundaryID = new List<NodeData>();
        /// <summary>
        /// this is the soma index, this is good know, it is a list because in possible cases
        /// a soma maybe a segment of points, not a singular point
        /// </summary>
        public List<int> somaIDs = new List<int>();
        /// <summary>
        /// This is the NodeData structure, it will group information together corresponding to each node in the 1D graph geometry
        /// the information stored is analogous to what would be found in the 1D .swc file of the neuron geometry
        /// </summary>
        public struct NodeData
        {
            public int Id { get; set; }
            public int Pid { get; set; }
            public int NodeType { get; set; }
            public double NodeRadius { get; set; }
            public double Xcoords { get; set; }
            public double Ycoords { get; set; }
            public double Zcoords { get; set; }
            public List<int> NeighborIDs { get; set; }
        }

        /// <summary>
        /// The Edge Structure represents an edge in the 1D graph geometry. It contains properties for the from node, to node, and length.
        /// </summary>
        public struct Edge
        {
            public NodeData From { get; set; }
            public NodeData To { get; set; }
            public double Length { get; set; }
        }
        /// <summary>
        /// This will initialize the Neuron, the grid parameter is read in
        /// the grid parameter contains the geometric information from the .vrn files
        /// </summary>
        /// <param name="grid"></param>
        public Neuron(Grid grid)
        {
            AttachmentHandler.Available();
            VertexAttachementAccessor<DiameterData> accessor = new VertexAttachementAccessor<DiameterData>(grid);

            int count = 0;

            foreach (DiameterData diam in accessor)
            {
                NodeData tempNode = new NodeData
                {
                    /// this gets the radius by dividing the diameter by 2
                    NodeRadius = diam.Diameter / 2,

                    /// this the node id --> these may not be consecutive
                    Id = grid.Vertices[count].Id,

                    /// these are the actual coordinates of the geometry --> NOTE: these are in [um] already!
                    /// Currently uses Unity Mesh vertices. Should be changed to directly use geometry/Grid vertices
                    Xcoords = grid.Mesh.vertices[count].x,
                    Ycoords = grid.Mesh.vertices[count].y,
                    Zcoords = grid.Mesh.vertices[count].z
                };

                /// this initializes an empty list for the neighbor id nodes
                tempNode.NeighborIDs = new List<int>();

                /// this adds the neigbor ids to the list for this node
                foreach (Vertex neighborVertex in grid.Vertices[count].Neighbors)
                {
                    tempNode.NeighborIDs.Add(neighborVertex.Id);
                }

                /// this for loop is for setting the pid, parent id
                for (int i = 0; i < grid.Edges.Count; i++)
                {
                    /// the zero-th node has parent -1 --> NOTE: is this alway true, need to check this!!
                    if (tempNode.Id == 0) tempNode.Pid = -1;
                    else if (tempNode.Id == grid.Edges[i].To.Id) tempNode.Pid = grid.Edges[i].From.Id;
                }

                nodes.Add(tempNode);
                count = count + 1;
            }

            /// this loop collects the edges
            for (int i = 0; i < grid.Edges.Count; i++)
            {
                NodeData fromNode = nodes[grid.Edges[i].From.Id];
                NodeData toNode = nodes[grid.Edges[i].To.Id];
                edges.Add(new Edge {
                    From = fromNode,
                    To = toNode,
                    Length = GetEdgeLength(fromNode, toNode)
                }
                );
            }

            /// if a node has only one neighbor then it is a boundary node
            foreach (NodeData node in nodes)
            {
                /// add the boundary node to the list
                if (node.NeighborIDs.Count == 1) boundaryID.Add(node);
            }
            somaIDs = grid.Subsets["soma"].Indices.ToList();
        }
        /// <summary>
        /// This small routine writes the geometry file that is used to an output file in .swc format
        /// </summary>
        /// <param name="outFile"></param>
        public void writeSWC(string outFile)
        {
            StreamWriter file = File.AppendText(outFile);
            for (int i = 0; i < nodes.Count; i++)
            {
                file.WriteLine(nodes[i].Id.ToString() + " " + 1.ToString() + " " + nodes[i].Xcoords.ToString() + " " + nodes[i].Ycoords.ToString() + " " + nodes[i].Zcoords.ToString() + " " + nodes[i].NodeRadius.ToString() + " " + nodes[i].Pid.ToString());
            }

            file.Close();
        }

        static string cellFormatString = 
            "1D Vert count = {0}\n" +
            "Edge count = {1}\n" +
            "Max edge length = {2}\n" +
            "Avg edge length = {3}\n" +
            "Min edge length = {4}\n" + 
            "Soma ID(s) = ({5})\n" + 
            "Boundary ID(s) = ({6})";
        public override string ToString()
        {
            IEnumerable<double> edgeLengths = edges.Select(edge => edge.Length);
            return String.Format(
            cellFormatString,
            nodes.Count,
            edges.Count,
            edgeLengths.Max(),
            edgeLengths.Average(),
            edgeLengths.Min(),
            String.Join(", ", somaIDs.Select(c => "'" + c + "'")),
            String.Join(", ", boundaryID.Select(c => "'" + c + "'")));
        }
        /// <summary>
        /// this calculates the euclidean distance between node Start and node End
        /// </summary>
        /// <param name="startNode"></param> start node
        /// <param name="endNode"></param> end node
        /// <returns></returns>
        public double GetEdgeLength(NodeData startNode, NodeData endNode)
        {
            /// get the coordinates of the start node
            double x1 = startNode.Xcoords;
            double y1 = startNode.Ycoords;
            double z1 = startNode.Zcoords;
            /// get the coordinates of the end node
            double x2 = endNode.Xcoords;
            double y2 = endNode.Ycoords;
            double z2 = endNode.Zcoords;
            /// calculate the square of the differences
            double dx2 = (x1 - x2) * (x1 - x2);
            double dy2 = (y1 - y2) * (y1 - y2);
            double dz2 = (z1 - z2) * (z1 - z2);
            /// return the square root
            return Math.Sqrt(dx2 + dy2 + dz2);
        }

    }
}

using System.Collections.Generic;
using UnityEngine;

namespace FieldSim.Unity.Environment.Tunnels
{
    public sealed class TunnelNode : MonoBehaviour
    {
        [SerializeField] private string nodeId;
        public string NodeId => nodeId;

        public void Initialize(string id)
        {
            nodeId = id;
            gameObject.name = id;
        }
    }

    [System.Serializable]
    public sealed class TunnelEdge
    {
        public TunnelNode a;
        public TunnelNode b;
        public bool blocked;

        public bool Connects(TunnelNode node) => a == node || b == node;
        public TunnelNode Other(TunnelNode node) => a == node ? b : (b == node ? a : null);
    }

    public sealed class TunnelGraph : MonoBehaviour
    {
        [SerializeField] private List<TunnelNode> nodes = new List<TunnelNode>();
        [SerializeField] private List<TunnelEdge> edges = new List<TunnelEdge>();

        public IReadOnlyList<TunnelNode> Nodes => nodes;
        public IReadOnlyList<TunnelEdge> Edges => edges;

        public void AddNode(TunnelNode node)
        {
            if (node != null && !nodes.Contains(node)) nodes.Add(node);
        }

        public TunnelEdge AddEdge(TunnelNode a, TunnelNode b)
        {
            TunnelEdge edge = new TunnelEdge { a = a, b = b, blocked = false };
            edges.Add(edge);
            return edge;
        }

        public bool HasPath(TunnelNode start, TunnelNode goal)
        {
            if (start == null || goal == null) return false;
            if (start == goal) return true;

            Queue<TunnelNode> open = new Queue<TunnelNode>();
            HashSet<TunnelNode> visited = new HashSet<TunnelNode>();
            open.Enqueue(start);
            visited.Add(start);

            while (open.Count > 0)
            {
                TunnelNode current = open.Dequeue();
                foreach (TunnelEdge edge in edges)
                {
                    if (edge == null || edge.blocked || !edge.Connects(current)) continue;
                    TunnelNode next = edge.Other(current);
                    if (next == null || visited.Contains(next)) continue;
                    if (next == goal) return true;
                    visited.Add(next);
                    open.Enqueue(next);
                }
            }

            return false;
        }
    }
}

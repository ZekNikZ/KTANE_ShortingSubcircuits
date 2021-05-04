using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShortingSubcircuits {
    public class PuzzleGenerator {
        private readonly int _numNodes;
        private readonly int _minComponents;
        private readonly int _maxComponents;
        private readonly int _minNodesPerComponent;
        private int _minInterComponentConnections;
        private int _maxInterComponentConnections; // max value = _minNodesPerComponent

        private PuzzleGenerator(
            int numNodes,
            int minComponents,
            int maxComponents,
            int minNodesPerComponent,
            int minInterComponentConnections,
            int maxInterComponentConnections
        ) {
            this._numNodes = numNodes;
            this._minComponents = minComponents;
            this._maxComponents = maxComponents;
            this._minNodesPerComponent = minNodesPerComponent;
            this._minInterComponentConnections = minInterComponentConnections;
            this._maxInterComponentConnections = maxInterComponentConnections;
        }

        public static PuzzleGenerator GetDefaultPuzzleGenerator(int numNodes) {
            return new PuzzleGenerator(numNodes, 3, 4, 3, 1, 2);
        }

        public Puzzle GeneratePuzzle() {
            // Create graph
            var graph = Util.ListOf(this._numNodes, () => new List<int>());

            // Create components
            var numComponents = Random.Range(this._minComponents, this._maxComponents + 1);
            var components = Enumerable.Range(0, this._numNodes).ToList().Shuffle().Batch(this._minNodesPerComponent)
                .Select(l => l.ToList()).ToList();
            if (components.Count < numComponents) {
                throw new Exception("Invalid state for components");
            }
            if (components.Count > numComponents) {
                components.Skip(numComponents).SelectMany(x => x)
                    .Peek((v, i) => components[Random.Range(0, numComponents)].Add(v));
                components.RemoveRange(numComponents, components.Count - numComponents);
            }

            // Choose inter-component connections
            var shortOrder = Util.ListOf(numComponents, () => new List<Pair<int, int>>());
            var componentConnections = Util.ListOf(numComponents, () => new List<int>());
            for (var i = 1; i < numComponents; i++) {
                for (var j = 0; j < numComponents - i; j++) {
                    componentConnections[j].Add(j + i);
                }
            }

            // Make it so that only 6 solution shorts are required
            // (2 each for 3 nodes, 1 each for 4 nodes)
            switch (numComponents) {
                case 3:
                    this._maxInterComponentConnections = this._minInterComponentConnections = 2;
                    break;
                case 4:
                    this._maxInterComponentConnections = this._minInterComponentConnections = 1;
                    break;
            }

            // Create inter-component connections
            for (var me = 0; me < numComponents; me++) {
                foreach (var other in componentConnections[me]) {
                    for (var i = 0; i < Random.Range(
                        this._minInterComponentConnections, this._maxInterComponentConnections + 1); i++) {
                        // Randomly select the edge
                        int a, b;
                        do {
                            a = components[me].PickRandom();
                            b = components[other].PickRandom();
                        } while (graph[a].Contains(b));

                        // Create the edge
                        graph[a].Add(b);
                        shortOrder[other].Add(new Pair<int, int>(a, b));
                    }
                }
            }

            // Find topological sort of strongly-connected components
            shortOrder.RemoveAt(0);
            shortOrder.Reverse();

            // Create intra-component connections
            foreach (var component in components) {
                // Start by making a loop
                var edges = new List<Pair<int, int>>();
                component.Shuffle();
                graph[component.Last()].Add(component[0]);
                edges.Add(new Pair<int, int>(component.Last(), component[0]));
                for (var i = 0; i < component.Count - 1; i++) {
                    graph[component[i]].Add(component[i + 1]);
                    edges.Add(new Pair<int, int>(component[i], component[i + 1]));
                }

                // Add an additional, smaller, inner loop
                if (component.Count >= 4) {
                    for (var i = 0; i < component.Count - 2; i++) {
                        graph[component[i]].Add(component[i + 2]);
                        edges.Add(new Pair<int, int>(component[i], component[i + 2]));
                    }
                }

                // Try flipping around some edges and/or removing them
                // TODO: I don't think that this is even necessary, it is hard enough
                const float probFlip = 0.45f;
                const float probRemove = 0.2f;
                foreach (var edge in edges) {
                    var rand = Random.Range(0f, 1f);

                    if (rand < probRemove) {
                        // Remove the edge
                        graph[edge.First].Remove(edge.Second);

                        // If the component is no longer connected, flip it back
                        if (!IsConnected(graph, component)) {
                            graph[edge.First].Add(edge.Second);
                        }
                    } else if (rand < probFlip + probRemove) {
                        // Flip the edge
                        graph[edge.First].Remove(edge.Second);
                        graph[edge.Second].Add(edge.First);

                        // If the component is no longer connected, flip it back
                        if (!IsConnected(graph, component)) {
                            graph[edge.First].Add(edge.Second);
                            graph[edge.Second].Remove(edge.First);
                        }
                    }
                }
            }

            return new Puzzle(graph, shortOrder, components);
        }

        private static bool IsConnected(List<List<int>> graph, List<int> component) {
            // Mark all vertices as not visited
            var visited = new bool[graph.Count];
            for (var i = 0; i < graph.Count; i++) {
                visited[i] = false;
            }

            // Run DFS
            DFS(graph, visited);

            // Check if all nodes were visited
            if (visited.Where((b, i) => component.Contains(i)).Any(x => !x)) {
                return false;
            }

            // Reverse the graph
            var newGraph = Util.ListOf(graph.Count, () => new List<int>());
            foreach (var me in component) {
                foreach (var other in graph[me]) {
                    newGraph[other].Add(me);
                }
            }

            // Mark all vertices as not visited
            for (var i = 0; i < newGraph.Count; i++) {
                visited[i] = false;
            }

            // Run DFS
            DFS(newGraph, visited);

            // Check if all nodes were visited
            return visited.Where((b, i) => component.Contains(i)).All(x => x);
        }

        private static void DFS(List<List<int>> graph, bool[] visited) {
            // Run DFS starting at each node
            for (var i = 0; i < graph.Count; i++) {
                if (!visited[i]) {
                    DFS(graph, visited, i);
                }
            }
        }

        private static void DFS(List<List<int>> graph, bool[] visited, int start) {
            // Initialize the stack
            var stack = new Stack<int>();
            stack.Push(start);

            // Keep going until nothing else is on the stack
            while (stack.Count > 0) {
                // Poke all neighbors
                graph[stack.Pop()].Where(adj => !visited[adj]).Peek(adj => {
                    visited[adj] = true;
                    stack.Push(adj);
                });
            }
        }
    }
}

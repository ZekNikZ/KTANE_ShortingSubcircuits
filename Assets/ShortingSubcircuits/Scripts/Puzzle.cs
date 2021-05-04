using System.Collections.Generic;

namespace ShortingSubcircuits {
    public class Puzzle {
        private readonly List<List<int>> _graph;
        private readonly List<List<Pair<int, int>>> _shortOrder;
        private readonly List<List<int>> _components;
        
        public Puzzle(List<List<int>> graph, List<List<Pair<int, int>>> shortOrder, List<List<int>> components) {
            this._graph = graph;
            this._shortOrder = shortOrder;
            this._components = components;
        }

        public List<int> GetConnections(int node) {
            return this._graph[node];
        }

        public List<List<Pair<int, int>>> GetShortOrder() {
            return this._shortOrder;
        }

        public List<List<int>> GetComponents() {
            return this._components;
        }
    }
}

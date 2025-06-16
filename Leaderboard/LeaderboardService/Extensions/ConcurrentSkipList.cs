using LeaderboardService.Model;
using System.Collections.Concurrent;

namespace LeaderboardService.Extensions 
{
    public class ConcurrentSkipList<K, V> where V : IComparable<V>
    {
        private readonly int _maxLevel = 32;
        private readonly double _probability = 0.5;
        private readonly Node _head;
        private int _count;
        private readonly Random _random = new();

        public int Count
        {
            get { return Volatile.Read(ref _count); }
        }

        private class Node
        {
            public K Key { get; }
            public V Value { get; set; }
            public Node[] Next { get; }
            public int Level { get; }

            public Node(K key, V value, int level)
            {
                Key = key;
                Value = value;
                Level = level;
                Next = new Node[level];
            }
        }

        public ConcurrentSkipList()
        {
            _head = new Node(default, default, _maxLevel);
            for (int i = 0; i < _maxLevel; i++)
                _head.Next[i] = null;
        }

        // Try to update the value of an existing node with the given key
        public bool TryUpdate(K key, V value)
        {
            // Array to store the update path at each level
            var update = new Node[_maxLevel];
            var current = _head;

            // Traverse from the highest level to the lowest
            for (int i = _maxLevel - 1; i >= 0; i--)
            {
                // Move forward while the next node's value is less than the target value
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                    current = current.Next[i];

                // Record the last node at this level
                update[i] = current;
            }

            // Move to the node that might contain the key
            current = current.Next[0];

            // If the node exists and keys match, update the value
            if (current != null && current.Key.Equals(key))
            {
                current.Value = value;
                return true;
            }
            return false;
        }

        // Try to add a new node with the given key-value pair
        public bool TryAdd(K key, V value)
        {
            // Array to store the update path at each level
            var update = new Node[_maxLevel];
            var current = _head;

            // Traverse from the highest level to the lowest
            for (int i = _maxLevel - 1; i >= 0; i--)
            {
                // Move forward while the next node's value is less than the target value
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                    current = current.Next[i];
                update[i] = current;
            }

            // Check if the key already exists
            current = current.Next[0];
            if (current != null && current.Key.Equals(key))
            {
                return false; // Key already exists
            }

            // Create new node with random level
            var newNode = new Node(key, value, RandomLevel());

            // Insert the new node at each level up to its randomly assigned level
            for (int i = 0; i < newNode.Level; i++)
            {
                newNode.Next[i] = update[i].Next[i];
                update[i].Next[i] = newNode;
            }

            // Atomically increment the count
            Interlocked.Increment(ref _count);
            return true;
        }
        public void AddOrUpdate(K key, V value)
        {
            var update = new Node[_maxLevel];
            var current = _head;

            for (int i = _maxLevel - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                    current = current.Next[i];
                update[i] = current;
            }

            current = current.Next[0];
            if (current != null && current.Key.Equals(key))
            {
                current.Value = value;
                return;
            }

            var newNode = new Node(key, value, RandomLevel());
            for (int i = 0; i < newNode.Level; i++)
            {
                newNode.Next[i] = update[i].Next[i];
                update[i].Next[i] = newNode;
            }
            Interlocked.Increment(ref _count);
        }

        public bool TryGetValue(K key, V value)
        {
            var current = _head;
            for (int i = _maxLevel - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                    current = current.Next[i];
            }

            current = current.Next[0];
            if (current != null && current.Key.Equals(key))
            {
                value = current.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool RemoveByKey(K key)
        {
            Node[] update = new Node[_maxLevel];
            Node current = _head;

            // Find the node to delete and its predecessors at each level
            for (int i = _maxLevel - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && !current.Next[i].Key.Equals(key))
                    current = current.Next[i];
                update[i] = current;
            }

            current = current.Next[0];
            if (current == null || !current.Key.Equals(key))
                return false; // Node with matching key not found

            // Remove the node from all levels
            for (int i = 0; i < current.Level; i++)
            {
                update[i].Next[i] = current.Next[i];
            }

            Interlocked.Decrement(ref _count);
            return true;
        }


        public List<V> GetRange(int startIndex, int count)
        {
            var result = new List<V>();

            // Start from the first node (skip the head node)
            var current = _head.Next[0];
            int index = 0; // Track current position in the list

            // Traverse the list until we reach the end or the requested range end
            while (current != null && index < startIndex + count)
            {
                // Only add values that fall within the requested range
                if (index >= startIndex)
                    result.Add(current.Value);

                // Move to next node and increment position counter
                current = current.Next[0];
                index++;
            }
            return result;
        }


        public List<V> GetNeighbors(K key, int beforeCount, int afterCount, out int rank)
        {
            var result = new List<V>();
            var current = _head.Next[0];
            var targetNode = (Node)null;
            var nodesBefore = new List<V>();
            var nodesAfter = new List<V>();
            // Initialize rank
            rank = 0; 

            // Find target node and calculate its rank
            while (current != null)
            {
                rank++; // Increment rank for each node passed
                if (current.Key.Equals(key))
                {
                    targetNode = current;
                    break;
                }
                current = current.Next[0];
            }

            if (targetNode == null)
            {
                rank = -1; // Return -1 if target node not found
                return result;
            }

            // Collect N items before the target
            current = _head.Next[0];
            var tempList = new List<V>();
            while (current != targetNode)
            {
                tempList.Add(current.Value);
                current = current.Next[0];
            }
            nodesBefore = tempList.Skip(Math.Max(0, tempList.Count - beforeCount)).ToList();

            // Collect M items after the target
            current = targetNode.Next[0];
            for (int i = 0; i < afterCount && current != null; i++)
            {
                nodesAfter.Add(current.Value);
                current = current.Next[0];
            }

            // Combine results
            result.AddRange(nodesBefore);
            result.Add(targetNode.Value);
            result.AddRange(nodesAfter);

            return result;
        }


        private int RandomLevel()
        {
            int level = 1;
            while (_random.NextDouble() < _probability && level < _maxLevel)
                level++;
            return level;
        }
    }

}

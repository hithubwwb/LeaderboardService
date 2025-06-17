using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace LeaderboardService.Extensions
{
    public class ConcurrentSkipList2<TKey, TValue> where TKey : IComparable<TKey> where TValue : IComparable<TValue>
    {
        private const int MaxLevel = 32;
        private const double Probability = 0.5;
        private readonly Node _head = new Node(default, default, MaxLevel);
        private readonly Random _random = new Random();
        private int _currentLevel = 1;
        private long _version;
        
        private readonly IComparer<TKey> comparerTKey;
        private readonly IComparer<TValue> comparer;
        public int Count
        {
            get
            {
                int count = 0;
                Node current = _head.Next[0];
                while (current != null)
                {
                    if (!current.IsDeleted)
                        count++;
                    current = current.Next[0];
                }
                return count;
            }
        }

        public ConcurrentSkipList2(IComparer<TKey> comparerTKey = null, IComparer < TValue> comparer = null)
        {
            this.comparerTKey = comparerTKey ?? Comparer<TKey>.Default;
            this.comparer = comparer ?? Comparer<TValue>.Default;
        }

        private class Node
        {
            public readonly TKey Key;
            public TValue Value;
            public readonly Node[] Next;
            public readonly int Level;
            public volatile bool IsDeleted;
            public long Version;
            public object NodeLock = new object();

            public Node(TKey key, TValue value, int level)
            {
                Key = key;
                Value = value;
                Level = level;
                Next = new Node[level];
                Version = 0;
            }
        }

        public bool TryAdd(TKey key, TValue value)
        {
            int topLevel = RandomLevel();
            Node[] preds = new Node[MaxLevel];
            Node[] succs = new Node[MaxLevel];

            while (true)
            {
                long readVersion = FindNode(value, ref preds, ref succs);

                // 检查是否已存在相同key的节点
                if (succs[0] != null && succs[0].Key.Equals(key))
                {
                    lock (succs[0].NodeLock)
                    {
                        if (succs[0].IsDeleted) continue;
                        // 仅当值不同时才更新
                        if (!succs[0].Value.Equals(value))
                        {
                            succs[0].Value = value;
                            Interlocked.Increment(ref _version);
                            succs[0].Version = _version;
                        }
                        return true;
                    }
                }


                // 验证排序规则：前驱 ≤ 新节点 ≤ 后继
                if (preds[0] != null)
                {
                    // 头节点处理（假设头节点的Value为null表示最小值）
                    if (preds[0].Value == null)
                    {
                        // 头节点应该满足 preds[0] ≤ newNode
                        // 对于降序排序，头节点被视为最小值
                        if (succs[0] != null &&
                            (succs[0].Value.CompareTo(value) > 0 ||
                             (succs[0].Value.CompareTo(value) == 0 &&
                              succs[0].Key.CompareTo(key) <= 0)))
                            continue;
                    }
                    else
                    {
                        // 正常节点验证
                        if ((preds[0].Value.CompareTo(value) < 0 ||
                             (preds[0].Value.CompareTo(value) == 0 &&
                              preds[0].Key.CompareTo(key) >= 0)) ||
                            (succs[0] != null &&
                             (succs[0].Value.CompareTo(value) > 0 ||
                              (succs[0].Value.CompareTo(value) == 0 &&
                               succs[0].Key.CompareTo(key) <= 0))))
                            continue;
                    }
                }

                Node newNode = new Node(key, value, topLevel);

                // 先设置下层指针（不修改跳表结构）
                for (int level = 0; level < topLevel; level++)
                {
                    newNode.Next[level] = succs[level];
                }

                // 锁定最低层前驱节点
                lock (preds[0].NodeLock)
                {
                    // 验证前驱后继未改变
                    if (preds[0].Next[0] != succs[0] || preds[0].IsDeleted)
                        continue;

                    // 锁定并验证所有层的前驱节点
                    for (int level = 1; level < topLevel; level++)
                    {
                        if (preds[level] == null) break;
                        lock (preds[level].NodeLock)
                        {
                            if (preds[level].Next[level] != succs[level] ||
                                preds[level].IsDeleted)
                            {
                                // 解锁已获取的锁
                                for (int l = 1; l < level; l++)
                                {
                                    Monitor.Exit(preds[l].NodeLock);
                                }
                                continue;
                            }
                        }
                    }

                    // 正式插入节点
                    for (int level = 0; level < topLevel; level++)
                    {
                        if (preds[level] == null) break;
                        preds[level].Next[level] = newNode;
                    }

                    Interlocked.Increment(ref _version);
                    newNode.Version = _version;
                    return true;
                }
            }
        }

        //public bool TryAdd(TKey key, TValue value)
        //{
        //    int topLevel = RandomLevel();
        //    Node[] preds = new Node[MaxLevel];
        //    Node[] succs = new Node[MaxLevel];

        //    while (true)
        //    {
        //        long readVersion = FindNode(value, ref preds, ref succs);
        //        if (succs[0] != null && succs[0].Value.CompareTo(value) == 0)
        //        {
        //            lock (succs[0].NodeLock)
        //            {
        //                if (succs[0].IsDeleted)
        //                    continue;
        //                succs[0].Value = value;
        //                Interlocked.Increment(ref _version);
        //                succs[0].Version = _version;
        //                return true;
        //            }
        //        }

        //        Node newNode = new Node(key, value, topLevel);
        //        for (int level = 0; level < topLevel; level++)
        //        {
        //            newNode.Next[level] = succs[level];
        //        }

        //        lock (preds[0].NodeLock)
        //        {
        //            if (preds[0].Next[0] != succs[0] || preds[0].IsDeleted)
        //                continue;

        //            for (int level = 0; level < topLevel; level++)
        //            {
        //                if (preds[level] == null) break;
        //                newNode.Next[level] = succs[level];
        //                preds[level].Next[level] = newNode;
        //            }

        //            Interlocked.Increment(ref _version);
        //            newNode.Version = _version;
        //            return true;
        //        }
        //    }
        //}

        public bool TryUpdate(TKey key, TValue newValue)
        {
            // 先查找并锁定要更新的节点
            Node nodeToUpdate = FindNode(key);
            if (nodeToUpdate == null) return false;

            lock (nodeToUpdate.NodeLock)
            {
                if (nodeToUpdate.IsDeleted || !nodeToUpdate.Key.Equals(key))
                    return false;

                // 如果score没有变化，只需更新value
                if (nodeToUpdate.Value.Equals(newValue))
                {
                    //nodeToUpdate.Value = newValue;
                    //Interlocked.Increment(ref _version);
                    //nodeToUpdate.Version = _version;
                    //return true;
                    return false;
                }

                // 如果score变化，需要删除并重新插入
                if (TryRemoveByKey(key))
                {
                    return TryAdd(key, newValue);
                }
                return false;
            }
        }

        //public bool TryUpdate(TKey key, TValue newValue)
        //{
        //    Node[] preds = new Node[MaxLevel];
        //    Node[] succs = new Node[MaxLevel];

        //    while (true)
        //    {
        //        Node curr = _head;
        //        for (int level = _currentLevel - 1; level >= 0; level--)
        //        {
        //            while (curr.Next[level] != null && !curr.Next[level].Key.Equals(key))
        //            {
        //                curr = curr.Next[level];
        //            }
        //        }

        //        Node nodeToUpdate = curr.Next[0];
        //        if (nodeToUpdate == null || !nodeToUpdate.Key.Equals(key))
        //            return false;

        //        lock (nodeToUpdate.NodeLock)
        //        {
        //            if (nodeToUpdate.IsDeleted || !nodeToUpdate.Key.Equals(key))
        //                continue;

        //            nodeToUpdate.Value = newValue;
        //            Interlocked.Increment(ref _version);
        //            nodeToUpdate.Version = _version;
        //            return true;
        //        }
        //    }
        //}

        public List<KeyValuePair<TKey, TValue>> GetRange(int start, int end)
        {
            if (start < 0 || end < start)
                throw new ArgumentException("Invalid range parameters");

            start--;
            end--;

            var result = new List<KeyValuePair<TKey, TValue>>();
            Node current = _head.Next[0];
            int currentRank = 0;
            int collectedCount = 0;

            // 先移动到起始位置
            while (current != null && currentRank < start)
            {
                if (!current.IsDeleted)
                {
                    currentRank++;
                }
                current = current.Next[0];
            }

            // 收集范围内的元素
            while (current != null && collectedCount <= (end - start))
            {
                if (!current.IsDeleted)
                {
                    result.Add(new KeyValuePair<TKey, TValue>(current.Key, current.Value));
                    collectedCount++;
                }
                current = current.Next[0];
            }

            return result;
        }

        public List<TValue> GetNeighbors(TKey key, int beforeCount, int afterCount, out int rank)
        {
            var result = new List<TValue>();
            var current = _head.Next[0];
            var targetNode = (Node)null;
            var nodesBefore = new List<TValue>();
            var nodesAfter = new List<TValue>();
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
            var tempList = new List<TValue>();
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

        public bool TryGetValue(TValue value, out TKey key)
        {
            Node curr = _head;
            for (int level = _currentLevel - 1; level >= 0; level--)
            {
                while (curr.Next[level] != null && curr.Next[level].Value.CompareTo(value) < 0)
                {
                    curr = curr.Next[level];
                }
            }

            curr = curr.Next[0];
            if (curr != null && curr.Value.CompareTo(value) == 0 && !curr.IsDeleted)
            {
                key = curr.Key;
                return true;
            }

            key = default;
            return false;
        }

        public bool TryRemove(TValue value)
        {
            Node[] preds = new Node[MaxLevel];
            Node[] succs = new Node[MaxLevel];

            while (true)
            {
                long readVersion = FindNode(value, ref preds, ref succs);
                Node nodeToRemove = succs[0];

                if (nodeToRemove == null || nodeToRemove.Value.CompareTo(value) != 0)
                    return false;

                lock (nodeToRemove.NodeLock)
                {
                    if (nodeToRemove.IsDeleted)
                        continue;

                    nodeToRemove.IsDeleted = true;
                    Interlocked.Increment(ref _version);
                    nodeToRemove.Version = _version;

                    for (int level = 0; level < nodeToRemove.Level; level++)
                    {
                        preds[level].Next[level] = nodeToRemove.Next[level];
                    }

                    return true;
                }
            }
        }

        public bool TryRemoveByKey(TKey key)
        {
            Node curr = _head;
            for (int level = _currentLevel - 1; level >= 0; level--)
            {
                while (curr.Next[level] != null && !curr.Next[level].Key.Equals(key))
                {
                    curr = curr.Next[level];
                }
            }

            Node nodeToRemove = curr.Next[0];
            if (nodeToRemove == null || !nodeToRemove.Key.Equals(key))
            {
                //value = default;
                return false;
            }

            lock (nodeToRemove.NodeLock)
            {
                if (nodeToRemove.IsDeleted)
                {
                    //value = default;
                    return false;
                }

                nodeToRemove.IsDeleted = true;
                //value = nodeToRemove.Value;
                Interlocked.Increment(ref _version);
                nodeToRemove.Version = _version;

                for (int level = 0; level < nodeToRemove.Level; level++)
                {
                    Node pred = _head;

                    if (pred == null) break;

                    while (pred != null && pred.Next[level] != null && pred.Next[level] != nodeToRemove)
                    {
                        pred = pred.Next[level];
                    }
                    if (pred == null || pred.Next[level] == null)
                    {
                        continue;
                    }
                    pred.Next[level] = nodeToRemove.Next[level];
                }

                return true;
            }
        }

        private  Node FindNode(TKey key)
        {
            Node curr = _head;
            for (int level = _currentLevel - 1; level >= 0; level--)
            {
                while (curr.Next[level] != null && curr.Next[level].Key.CompareTo(key) < 0)
                {
                    curr = curr.Next[level];
                }
            }
            return curr.Next[0]?.Key.Equals(key) == true ? curr.Next[0] : null;
        }


        private long FindNode(TValue value, ref Node[] preds, ref Node[] succs)
        {
            long version;
            Node pred;
            do
            {
                version = Interlocked.Read(ref _version);
                pred = _head;
                for (int level = _currentLevel - 1; level >= 0; level--)
                {
                    Node curr = pred.Next[level];
                    while (curr != null && curr.Value.CompareTo(value) < 0)
                    {
                        pred = curr;
                        curr = curr.Next[level];
                    }
                    preds[level] = pred;
                    succs[level] = curr;
                }
            } while (version != Interlocked.Read(ref _version));

            return version;
        }

        private int RandomLevel()
        {
            int level = 1;
            while (_random.NextDouble() < Probability && level < MaxLevel)
            {
                level++;
            }
            return level;
        }
    }
}

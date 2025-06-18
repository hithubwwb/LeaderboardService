using System.Collections.Concurrent;
using System.Xml.Linq;

namespace LeaderboardService.Extensions
{
    public class ConcurrentSkipList4<TKey, TValue> where TKey : IComparable<TKey> where TValue : IComparable<TValue>
    {
        private class Node<TKey, TValue>
        {
            public TKey Key { get; }
            public TValue Value { get; set; }
            public Node<TKey, TValue>[] Next { get; set; }

            public Node<TKey, TValue> Prev; // 新增前驱指针

            public object NodeLock = new object();
            public int Height => Next?.Length ?? 0;

            public Node(TKey key, TValue value, int height)
            {
                Key = key;
                Value = value;
                Next = new Node<TKey, TValue>[height];
            }
        }
        private const int MaxHeight = 32;
        private readonly Node<TKey, TValue> _head = new Node<TKey, TValue>(default, default, MaxHeight);
        private readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() =>
            new Random(Guid.NewGuid().GetHashCode()));
        private readonly ConcurrentQueue<KeyValuePair<TKey, TValue>> _bufferQueue = new ConcurrentQueue<KeyValuePair<TKey, TValue>>();
        private readonly Timer _batchTimer;
        private int _isProcessing = 0;
        private long _totalNodes = 0;
        private readonly int _currentLevel = 1;
        private readonly Dictionary<TKey, Node<TKey, TValue>> _keyIndex = new Dictionary<TKey, Node<TKey, TValue>>();

        public ConcurrentSkipList4() => _batchTimer = new Timer(ProcessBatch, null, 0, 1);

        // 新增的获取长度方法
        public long Count
        {
            get
            {
                return Interlocked.Read(ref _totalNodes);
            }
        }

        public void Add(TKey key, TValue value) => _bufferQueue.Enqueue(new KeyValuePair<TKey, TValue>(key, value));

        private void ProcessBatch(object state)
        {
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

            try
            {
                var batch = new List<KeyValuePair<TKey, TValue>>();
                while (_bufferQueue.TryDequeue(out var item) && batch.Count < 5000)
                    batch.Add(item);

                if (batch.Count > 0)
                {
                    batch.Sort((x, y) => x.Value.CompareTo(y.Value));
                    foreach (var item in batch)
                        InternalAdd(item.Key, item.Value);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        public void InternalAdd(TKey key, TValue value)
        {
            var update = new Node<TKey, TValue>[MaxHeight];
            var current = _head;

            for (int i = MaxHeight - 1; i >= 0; i--)
            {
                while (current.Next[i] != null &&
                      (current.Next[i].Value.CompareTo(value) < 0 ||
                      (current.Next[i].Value.CompareTo(value) == 0 &&
                       current.Next[i].Key.CompareTo(key) < 0)))
                    current = current.Next[i];
                update[i] = current;
            }

            if (current.Next[0] != null && current.Next[0].Key.CompareTo(key) == 0)
            {
                lock (current.Next[0].NodeLock)
                {
                    current.Next[0].Value = value;
                }
                return;
            }

            int height = RandomHeight();
            var newNode = new Node<TKey, TValue>(key, value, height);

            for (int i = 0; i < height; i++)
            {
                lock (update[i].NodeLock)
                {
                    newNode.Next[i] = update[i].Next[i];
                    update[i].Next[i] = newNode;
                }
            }

            lock (_keyIndex)
            {
                _keyIndex[key] = newNode;
            }
            Interlocked.Increment(ref _totalNodes);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            // 快速失败检查
            if (_head == null || key == null)
            {
                value = default;
                return false;
            }

            value = default;
            Node<TKey, TValue> current = _head;
            IComparer<TKey> comparer = Comparer<TKey>.Default;

            // 从最高层开始搜索
            for (int i = _currentLevel - 1; i >= 0; i--)
            {
                // 预取下一个节点减少缓存未命中
                while (current.Next[i] != null)
                {
                    int cmp = comparer.Compare(current.Next[i].Key, key);
                    if (cmp < 0)
                    {
                        current = current.Next[i];
                    }
                    else if (cmp > 0)
                    {
                        break;
                    }
                    else // cmp == 0
                    {
                        value = current.Next[i].Value;
                        return true;
                    }
                }
            }

            // 检查最底层节点
            if (current.Next[0] != null &&
                comparer.Compare(current.Next[0].Key, key) == 0)
            {
                value = current.Next[0].Value;
                return true;
            }

            return false;
        }

        public List<KeyValuePair<TKey, TValue>> GetRangeByRank(int startRank, int endRank)
        {
            var result = new List<KeyValuePair<TKey, TValue>>();
            int currentRank = 0;
            var current = _head.Next[0];

            while (current != null && currentRank < endRank)
            {
                currentRank++;
                if (currentRank >= startRank)
                {
                    result.Add(new KeyValuePair<TKey, TValue>(
                        current.Key,
                        current.Value
                    ));
                }
                current = current.Next[0];
            }
            return result;
        }

        public List<KeyValuePair<TKey, TValue>> GetNeighbors(TKey key, int prevCount, int nextCount, out int rank)
        {
            rank = -1;
            var result = new List<KeyValuePair<TKey, TValue>>();
            var current = _head;
            int currentRank = 0;

            // 查找目标节点并计算排名
            for (int i = MaxHeight - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Key.CompareTo(key) <= 0)
                {
                    currentRank += (1 << i);
                    current = current.Next[i];
                }
            }

            // 精确匹配检查
            if (current.Key.CompareTo(key) != 0)
            {
                return result;
            }
            rank = currentRank;

            // 获取前N项
            var prevNodes = new Stack<KeyValuePair<TKey, TValue>>();
            var temp = current;
            int count = 0;
            while (temp.Prev != null && count < prevCount)
            {
                prevNodes.Push(new KeyValuePair<TKey, TValue>(temp.Prev.Key, temp.Prev.Value));
                temp = temp.Prev;
                count++;
            }

            // 获取后N项
            var nextNodes = new List<KeyValuePair<TKey, TValue>>();
            temp = current.Next[0];
            count = 0;
            while (temp != null && count < nextCount)
            {
                nextNodes.Add(new KeyValuePair<TKey, TValue>(temp.Key, temp.Value));
                temp = temp.Next[0];
                count++;
            }

            // 合并结果
            while (prevNodes.Count > 0)
            {
                result.Add(prevNodes.Pop());
            }
            result.Add(new KeyValuePair<TKey, TValue>(current.Key, current.Value));
            result.AddRange(nextNodes);

            return result;
        }

        public bool Remove(TKey key)
        {
            if (!_keyIndex.TryGetValue(key, out var node))
                return false;

            var update = new Node<TKey, TValue>[MaxHeight];
            var current = _head;

            for (int i = MaxHeight - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(node.Value) < 0)
                    current = current.Next[i];
                update[i] = current;
            }

            for (int i = 0; i < node.Height; i++)
            {
                lock (update[i].NodeLock)
                {
                    if (update[i].Next[i] != node) break;
                    update[i].Next[i] = node.Next[i];
                }
            }

            lock (_keyIndex)
            {
                _keyIndex.Remove(key);
            }
            Interlocked.Decrement(ref _totalNodes);
            return true;
        }

        private int RandomHeight()
        {
            int height = 1;
            while (_random.Value.NextDouble() < 0.5 && height < MaxHeight)
                height++;
            return height;
        }
    }
}

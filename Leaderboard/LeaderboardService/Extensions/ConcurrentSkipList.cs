using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace LeaderboardService.Extensions
{
    public class ConcurrentSkipList<T> where T : IComparable<T>
    {
        private class Node
        {
            public T Value { get; }
            public Node[] Next { get; set; }
            public object NodeLock = new object();
            public readonly object[] LevelLocks;
            public int Height => Next?.Length ?? 0;

            public Node(T value, int height)
            {
                Value = value;
                Next = new Node[height];

                LevelLocks = new object[height];
                for (int i = 0; i < height; i++)
                    LevelLocks[i] = new object();
            }
        }

        private const int MaxHeight = 64;
        private const double Probability = 0.5;
        private readonly Node _head = new Node(default!, MaxHeight);
        private readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() =>
            new Random(Guid.NewGuid().GetHashCode()));
        private readonly ConcurrentQueue<T> _bufferQueue = new ConcurrentQueue<T>();
        private readonly Timer _batchTimer;
        private int _isProcessing = 0;
        private long _totalNodes = 0;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public ConcurrentSkipList() => _batchTimer = new Timer(ProcessBatch!, null, 100, 100);

        // 新增的获取长度方法
        public long Count
        {
            get
            {
                return Interlocked.Read(ref _totalNodes);
            }
        }

        public void Add(T value) => _bufferQueue.Enqueue(value);

        private void ProcessBatch(object state)
        {
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0) return;

            try
            {
                var batch = new List<T>();
                while (_bufferQueue.TryDequeue(out var item) && batch.Count < 5000)
                    batch.Add(item);

                if (batch.Count > 0)
                {
                    batch.Sort();
                    foreach (var item in batch)
                        InternalAdd(item);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        public void InternalAdd(T value)
        {
            try 
            {
                _rwLock.EnterWriteLock();
                var update = new Node[MaxHeight];
                var current = _head;

                for (int i = MaxHeight - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                        current = current.Next[i];
                    update[i] = current;
                }

                int height = RandomHeight();
                var newNode = new Node(value, height);

                for (int i = 0; i < height; i++)
                {
                    lock (update[i].NodeLock)
                    {
                        newNode.Next[i] = update[i].Next[i];
                        update[i].Next[i] = newNode;
                    }
                }
                Interlocked.Increment(ref _totalNodes);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

        }

        public bool Update(T oldValue, T newValue)
        {
            if (oldValue.CompareTo(newValue) == 0)
                return true;

            try
            {
                _rwLock.EnterWriteLock();
                var update = new Node[MaxHeight];
                var current = _head;

                for (int i = MaxHeight - 1; i >= 0; i--)
                {
                    while (current.Next[i] != null && current.Next[i].Value.CompareTo(oldValue) < 0)
                        current = current.Next[i];
                    update[i] = current;
                }

                for (int i = MaxHeight - 1; i >= 0; i--)
                {
                    if (update[i] != null)
                        Monitor.Enter(update[i].NodeLock);
                }

                try
                {
                    current = update[0].Next[0];
                    if (current == null || current.Value.CompareTo(oldValue) != 0)
                        return false;

                    if (!Remove(oldValue))
                        return false;

                    InternalAdd(newValue);
                    return true;
                }
                finally
                {
                    for (int i = 0; i < MaxHeight; i++)
                    {
                        if (update[i] != null)
                            Monitor.Exit(update[i].NodeLock);
                    }
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }


        public List<T> GetRangeByRank(int startRank, int endRank)
        {
            // 参数校验
            if (startRank > endRank)
                throw new ArgumentException("startRank cannot be greater than endRank");
            if (startRank < 1)
                throw new ArgumentOutOfRangeException(nameof(startRank), "Rank must start from 1");

            var result = new List<T>(endRank - startRank + 1);
            int currentRank = 0;
            var current = _head.Next[0];

            while (current != null && currentRank < endRank)
            {
                currentRank++;
                if (currentRank >= startRank)
                    result.Add(current.Value);
                if (currentRank >= endRank)  // 提前终止
                    break;
                current = current.Next[0];
            }
            return result;
        }

        public List<T> GetNeighbors(T entity, int prevCount, int nextCount, out int rank)
        {
            rank = -1;
            var result = new List<T>();
            var current = _head;
            int currentRank = 0;

            // 查找目标节点并计算排名（无锁遍历）
            for (int i = MaxHeight - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(entity) <= 0)
                {
                    currentRank += (1 << i); // 跳跃层级的步长计算
                    current = current.Next[i];
                }
            }

            // 精确匹配检查
            if (current.Value.CompareTo(entity) != 0)
            {
                return result;
            }
            rank = currentRank;

            // 获取前N项（修正部分）
            var prevNodes = new Stack<T>();
            var temp = current;
            int count = 0;
            while (temp != _head && count < prevCount)
            {
                // 需要从当前节点向前遍历
                var prev = _head;
                while (prev.Next[0] != temp)
                {
                    prev = prev.Next[0];
                }
                prevNodes.Push(prev.Value);
                temp = prev;
                count++;
            }

            // 获取后M项（保持不变）
            var nextNodes = new List<T>();
            temp = current.Next[0];
            count = 0;
            while (temp != null && count < nextCount)
            {
                nextNodes.Add(temp.Value);
                temp = temp.Next[0];
                count++;
            }

            // 合并结果（前N项需要反转）
            while (prevNodes.Count > 0)
            {
                result.Add(prevNodes.Pop());
            }
            result.Add(current.Value); // 添加当前节点
            result.AddRange(nextNodes);

            return result;
        }

        public bool Remove(T value)
        {
            var update = new Node[MaxHeight];
            var current = _head;

            for (int i = MaxHeight - 1; i >= 0; i--)
            {
                while (current.Next[i] != null && current.Next[i].Value.CompareTo(value) < 0)
                    current = current.Next[i];
                update[i] = current;
            }

            current = current.Next[0];
            if (current == null || current.Value.CompareTo(value) != 0)
                return false;

            for (int i = 0; i < current.Height; i++)
            {
                lock (update[i].NodeLock)
                {
                    if (update[i].Next[i] != current) break;
                    update[i].Next[i] = current.Next[i];
                }
            }
            Interlocked.Decrement(ref _totalNodes);
            return true;
        }

        [ThreadStatic]
        private static Random _localRandom;

        private static int RandomHeight()
        {
            if (_localRandom == null)
                _localRandom = new Random(Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId);

            int level = 1;
            while (_localRandom.NextDouble() < 0.25 && level < MaxHeight)
                level++;
            return level;
        }
    }
}

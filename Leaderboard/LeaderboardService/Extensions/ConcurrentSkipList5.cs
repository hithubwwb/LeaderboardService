using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace LeaderboardService.Extensions
{

    // 双向链表节点（含跳表层级指针）
    public class DoubleLinkedNode<T> where T : IComparable<T>
    {
        public T Value { get; set; }
        public DoubleLinkedNode<T> Next { get; set; }     // 后向指针
        public DoubleLinkedNode<T> Prev { get; set; }     // 前向指针
        public DoubleLinkedNode<T>[] HigherLevels { get; set; } // 跳表层级指针

        public DoubleLinkedNode(T value, int level = 32)
        {
            Value = value;
            HigherLevels = new DoubleLinkedNode<T>[level + 1];
        }
    }

    public class ConcurrentSkipList5<T> where T : IComparable<T>, IScorable
    {
        private DoubleLinkedNode<T> Head { get; set; }    // 头节点（虚拟节点）
        private DoubleLinkedNode<T> Tail { get; set; }    // 尾节点（虚拟节点）

        private readonly Dictionary<string, DoubleLinkedNode<T>> _index = new();   // 索引字典

        private long _totalNodes = 0;                     // 计数

        private readonly ReaderWriterLockSlim rwLock;     // 读写锁


        public long Count                                 // 新增的获取长度方法
        {
            get
            {
                return Interlocked.Read(ref _totalNodes);
            }
        }
        public ConcurrentSkipList5()
        {
            // 初始化虚拟头尾节点，简化边界判断
            Head = new DoubleLinkedNode<T>(default, 32);
            Tail = new DoubleLinkedNode<T>(default, 32);
            Head.Next = Tail;
            Tail.Prev = Head;
            rwLock = new ReaderWriterLockSlim();
        }

        // 插入节点（保持有序）
        public void Add(T value)
        {
            rwLock.EnterWriteLock();
            try
            {
                DoubleLinkedNode<T> newNode = new DoubleLinkedNode<T>(value, 0);
                DoubleLinkedNode<T> current = Head.Next;

                // 找到插入位置（按比较规则排序）
                while (current != Tail && current.Value.CompareTo(value) > 0)
                {
                    current = current.Next;
                }

                // 插入节点（双向指针更新）
                newNode.Next = current;
                newNode.Prev = current.Prev;
                current.Prev.Next = newNode;
                current.Prev = newNode;
                Interlocked.Increment(ref _totalNodes);
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public void AddOrUpdate(T item, Func<T, T, T> updateFactory)
        {
            string key = GetItemKey(item);
            rwLock.EnterWriteLock();
            try
            {
                if (_index.TryGetValue(key, out var existing))
                {
                    existing.Value = updateFactory(existing.Value, item);
                    existing.Value.Timestamp = DateTime.UtcNow.Ticks;
                    MoveToCorrectPosition(existing);
                }
                else
                {
                    // 创建节点实例
                    var newNode = new DoubleLinkedNode<T>(item);
                    // 设置时间戳属性
                    newNode.Value.Timestamp = DateTime.UtcNow.Ticks;


                    // 尾插法保持插入顺序
                    newNode.Prev = Tail.Prev;
                    newNode.Next = Tail;
                    Tail.Prev.Next = newNode;
                    Tail.Prev = newNode;

                    _index.Add(key, newNode);
                    Interlocked.Increment(ref _totalNodes);
                    MoveToCorrectPosition(newNode);
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        private void MoveToCorrectPosition(DoubleLinkedNode<T> node)
        {
            // 向左移动直到找到正确位置
            while (node.Prev != Head &&
                   (node.Value.Score > node.Prev.Value.Score ||
                   (node.Value.Score == node.Prev.Value.Score &&
                    node.Value.Timestamp < node.Prev.Value.Timestamp)))
            {
                SwapNodes(node.Prev, node);
            }

            // 向右移动直到找到正确位置
            while (node.Next != Tail &&
                   (node.Value.Score < node.Next.Value.Score ||
                   (node.Value.Score == node.Next.Value.Score &&
                    node.Value.Timestamp > node.Next.Value.Timestamp)))
            {
                SwapNodes(node, node.Next);
            }
        }

        private void SwapNodes(DoubleLinkedNode<T> left, DoubleLinkedNode<T>  right)
        {
            left.Next = right.Next;
            right.Prev = left.Prev;
            left.Prev.Next = right;
            right.Next.Prev = left;
            right.Next = left;
            left.Prev = right;
        }

        private string GetItemKey(T item)
        {
            dynamic dynamicItem = item;
            return dynamicItem?.CustomerId?.ToString() ?? Guid.NewGuid().ToString();
        }

        public List<T> GetRangeByRank(int startRank, int endRank)
        {
            // 参数校验
            if (startRank > endRank)
                throw new ArgumentException("startRank cannot be greater than endRank");
            if (startRank < 1)
                throw new ArgumentOutOfRangeException(nameof(startRank), "Rank must start from 1");

            var result = new List<T>(endRank - startRank + 1);
            if (startRank < 1 || endRank < startRank)
                return result;

            int currentRank = 0;
            var currentNode = Head.Next;

            // 定位起始节点
            while (currentNode != Tail && currentRank < startRank - 1)
            {
                currentNode = currentNode.Next;
                currentRank++;
            }

            // 收集范围内的节点
            while (currentNode != Tail && currentRank <= endRank - 1)
            {
                result.Add(currentNode.Value);
                currentNode = currentNode.Next;
                currentRank++;
            }

            return result;
        }

        // 查询前N项,后M项及当前项
        public List<T> GetNeighbors(T target, int prevCount, int nextCount, out int rank)
        {
            // 参数验证
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (prevCount < 0)
                throw new ArgumentOutOfRangeException(nameof(prevCount), "The previous count cannot be negative.");
            if (nextCount < 0)
                throw new ArgumentOutOfRangeException(nameof(nextCount), "The next count cannot be negative.");

            rank = -1;
            var result = new List<T>();
            int currentRank = 0;
            DoubleLinkedNode<T> targetNode = null;

            // 查找目标节点并计算排名
            for (var node = Head.Next; node != Tail; node = node.Next)
            {
                currentRank++;
                if (node.Value.Equals(target))
                {
                    targetNode = node;
                    rank = currentRank;
                    break;
                }
            }

            if (targetNode == null) return result;

            // 收集前驱节点
            var prevNode = targetNode.Prev;
            for (int i = 0; i < prevCount && prevNode != Head; i++)
            {
                result.Insert(0, prevNode.Value);
                prevNode = prevNode.Prev;
            }

            // 添加目标节点
            result.Add(targetNode.Value);

            // 收集后继节点
            var nextNode = targetNode.Next;
            for (int i = 0; i < nextCount && nextNode != Tail; i++)
            {
                result.Add(nextNode.Value);
                nextNode = nextNode.Next;
            }

            return result;
        }

        public bool TryGetValue<K>(Func<T, K> keySelector, K key, out T value) where K : IEquatable<K>
        {
            value = default;
            for (var node = Head.Next; node != Tail; node = node.Next)
            {
                if (keySelector(node.Value).Equals(key))
                {
                    value = node.Value;
                    return true;
                }
            }
            return false;
        }

    }

    public interface IScorable
    {
        long CustomerId { get; }
        decimal Score { get; set; }
        long Timestamp { get; set; }
    }


}

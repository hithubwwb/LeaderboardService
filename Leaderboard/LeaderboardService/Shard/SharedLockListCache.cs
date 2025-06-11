using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LeaderboardService.Shard
{
    // Thread-safe registry for storing and ranking customer scores
    public class SharedLockListCache
    {
        private readonly object _cacheLock = new object();
        private long _dataVersion = 0;
        private long _cacheVersion = 0;
        // task
        private readonly Timer _cacheTimer;

        // Concurrent storage of customerID-score pairs
        private readonly ConcurrentDictionary<long, decimal> _concurrentDictionary = [];

        // Cache list
        private List<KeyValuePair<long, decimal>> _cacheList = [];

        public SharedLockListCache() 
        {
            _cacheTimer = new Timer(RefreshCache, null, TimeSpan.Zero, TimeSpan.FromSeconds(20));
        }

        public ConcurrentDictionary<long, decimal> GetConcurrentDictionaryData => _concurrentDictionary;

        public List<KeyValuePair<long, decimal>> GetCacheList => _cacheList;

        // Update dataVersion
        public void NotifyDataChanged()
        {
            Interlocked.Increment(ref _dataVersion);
        }

        // Get cache
        public List<KeyValuePair<long, decimal>> GetDataCache()
        {
            // Check if the cache is expired only
            if (_cacheList.Count == 0 || _cacheVersion != Interlocked.Read(ref _dataVersion))
            {
                lock (_cacheLock)
                {
                    // Recheck if the cache is expired (possibly updated by other threads)
                    if (_cacheList.Count == 0 || _cacheVersion != Interlocked.Read(ref _dataVersion))
                    {
                        RefreshCache(default);
                    }
                }
            }
            return _cacheList;
        }

        // Refresh cache
        public void RefreshCache(object? state)
        {
            //_cacheList = _concurrentDictionary.ToList();
            //_cacheList.SortByScoreAndId();

            // 使用ToArray()替代ToList()避免动态扩容问题
            var snapshot = _concurrentDictionary.ToArray();

            // 预分配目标列表容量
            _cacheList = new List<KeyValuePair<long, decimal>>(snapshot.Length);
            _cacheList.AddRange(snapshot);

            _cacheList.SortByScoreAndId();

            Interlocked.Exchange(ref _cacheVersion, _dataVersion);
        }
    }

    public static class ListExtensions
    {
        public static void SortByScoreAndId(this List<KeyValuePair<long, decimal>> list)
        {
            list.Sort((x, y) =>
            {
                int scoreComparison = y.Value.CompareTo(x.Value); // Score sort desc
                return scoreComparison != 0 ? scoreComparison : x.Key.CompareTo(y.Key); // Id sort asc
            });
        }
    }

}

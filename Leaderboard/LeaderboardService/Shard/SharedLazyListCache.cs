using System.Collections.Concurrent;

namespace LeaderboardService.Shard
{
    // 
    public class SharedLazyListCache
    {
        private Lazy<List<KeyValuePair<long, decimal>>> _cachedLazy;

        // Concurrent storage of customerID-score pairs
        public readonly ConcurrentDictionary<long, decimal> _concurrentDictionary = [];

        // task
        private readonly Timer _cacheTimer;

        public SharedLazyListCache() 
        {
            // init
            _cachedLazy = new Lazy<List<KeyValuePair<long, decimal>>>(LoadData, LazyThreadSafetyMode.ExecutionAndPublication);

            // task
            _cacheTimer = new Timer(RefreshCache, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
        }

        // Lazy List load data and sort
        private List<KeyValuePair<long, decimal>> LoadData()
        {
            var list = _concurrentDictionary.ToList();
            list.SortByScoreAndId();
            return list;
        }

        // get cache data
        public List<KeyValuePair<long, decimal>> GetCacheData()
        {
            if (!_cachedLazy.IsValueCreated || _cachedLazy.Value.Count == 0)
            {
                RefreshCache(default);
            }
            return _cachedLazy.Value;
        }

        // refresh cache
        public void RefreshCache(object? state)
        {
            Interlocked.Exchange(ref _cachedLazy, 
                new Lazy<List<KeyValuePair<long, decimal>>>(LoadData, LazyThreadSafetyMode.ExecutionAndPublication));
        }

    }
}

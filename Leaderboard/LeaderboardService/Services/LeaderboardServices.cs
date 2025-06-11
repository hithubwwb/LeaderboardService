using LeaderboardService.Model;
using LeaderboardService.Shard;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace LeaderboardService.Services
{
    public class LeaderboardServices
    {
        // Basic data collection
        private readonly ConcurrentDictionary<long, decimal> _concurrentDictionary = [];

        // Cache data collection
        private List<KeyValuePair<long, decimal>> _sortedListCache = [];

        private SharedLockListCache _sharedCustomerScoreRegistry;

        public LeaderboardServices(SharedLockListCache sharedCustomerScoreRegistry)
        {
            _sharedCustomerScoreRegistry = sharedCustomerScoreRegistry;

            _concurrentDictionary = _sharedCustomerScoreRegistry.GetConcurrentDictionaryData;
            _sortedListCache = _sharedCustomerScoreRegistry.GetCacheList;
        }

        /// <summary>
        /// update score
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="changesScore"></param>
        /// <returns></returns>
        public decimal AddOrUpdateScore(long customerId, decimal changesScore)
        {
            // Insert or update target data
            var newScore = _concurrentDictionary.AddOrUpdate(customerId, changesScore, (key, oldScore) => oldScore + changesScore);
            if (newScore <= 0 || newScore > 1000)
            {
                // Remove data with scores ≤ 0 or > 1000 from the leaderboard collection, and only sort positive-score data to save storage space.
                _concurrentDictionary.TryRemove(customerId, out _);

                if (newScore <= 0)
                    changesScore = 1; newScore = _concurrentDictionary.AddOrUpdate(customerId, changesScore, (key, oldScore) => changesScore);

                if (newScore > 1000)
                    changesScore = 1000; newScore = _concurrentDictionary.AddOrUpdate(customerId, changesScore, (key, oldScore) => changesScore);
            }

            // Update data vesion
            //_sharedCustomerScoreRegistry.NotifyDataChanged();

            return newScore;
        }

        /// <summary>
        /// get customers by rank
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public List<CustomerRankOM> GetCustomersByRank(int start, int end)
        {
            // Get cache
            _sortedListCache = _sharedCustomerScoreRegistry.GetDataCache();
            if (_sortedListCache.Count == 0) return [];

            // ‌Calculation range‌
            var startRank = Math.Max(1, Math.Min(start, _sortedListCache.Count));
            var endRank = Math.Max(startRank, Math.Min(end, _sortedListCache.Count));

            // Return
            return _sortedListCache
                .Skip(startRank - 1)
                .Take(endRank - startRank + 1)
                .Select((x, i) => new CustomerRankOM(
                    x.Key,
                    x.Value,
                    startRank + i))
                .ToList();
        }

        /// <summary>
        /// get around customers
        /// </summary>
        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low)
        {
            // Get cache
            _sortedListCache = _sharedCustomerScoreRegistry.GetDataCache();
            if (_sortedListCache.Count == 0) return [];

            // Find target customers
            var targetIndex = _sortedListCache.FindIndex(x => x.Key == customerid);
            if (targetIndex == -1) return [];

            // ‌Calculation range‌
            int start = Math.Max(0, targetIndex - high);
            int end = Math.Min(_sortedListCache.Count - 1, targetIndex + low);

            // Return
            return _sortedListCache.GetRange(start, end - start + 1)
                .Select((item, index) =>
                    new CustomerRankOM(item.Key, item.Value, start + index + 1))
                .ToList();
        }

        public int AddTestData()
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            Parallel.ForEach(Enumerable.Range(1, 1000000), parallelOptions, customerId =>
            {
                this.AddOrUpdateScore(customerId, 50);
            });
            this.AddOrUpdateScore(1000001, 768);

            return this._concurrentDictionary.Count();
        }

    }
}

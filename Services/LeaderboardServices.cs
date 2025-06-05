using LeaderboardService.Model;
using LeaderboardService.Shard;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LeaderboardService.Services
{
    public class LeaderboardServices
    {
        // Basic data collection
        private readonly ConcurrentDictionary<long, decimal> _concurrentDictionary = [];
        // ‌Sorted collection
        private readonly SortedSet<(decimal Score, long CustomerId)> _sortedSet = [];
        // ‌ReadWriteLock‌
        private readonly ReaderWriterLockSlim _lock = new();

        public LeaderboardServices(SharedCustomerScoreRegistry sharedCustomerScoreRegistry)
        {
            // ‌Inject Shared Resources
            _concurrentDictionary = sharedCustomerScoreRegistry._concurrentDictionary;
            _sortedSet = sharedCustomerScoreRegistry._sortedSet;
        }

        /// <summary>
        /// update score
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="changesScore"></param>
        /// <returns></returns>
        public decimal UpdateScore(long customerId, decimal changesScore)
        {
            //‌ Add or update basic collection data
            var newScore = _concurrentDictionary.AddOrUpdate(customerId, changesScore, (key, oldScore) => oldScore + changesScore);

            // Acquire write lock to prevent SortedSet resource contention
            _lock.EnterWriteLock();
            try
            {
                var oldEntry = (newScore - changesScore, customerId);
                if (newScore <= 0)
                {
                    // Remove data with scores ≤ 0 from the leaderboard collection, and only sort positive-score data to save storage space.
                    _concurrentDictionary.TryRemove(customerId, out _);
                    _sortedSet.Remove(oldEntry);
                    return newScore;
                }

                // ‌Update sorted collection
                var newEntry = (newScore, customerId);
                _sortedSet.Remove(oldEntry);
                _sortedSet.Add(newEntry);
            }
            finally
            {
                // ‌Release lock
                _lock.ExitWriteLock();
            }

            // Return newScore
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
            // ‌Acquire read lock (enabling concurrent read operations)
            _lock.EnterReadLock();
            try
            {
                int count = _sortedSet.Count;
                if (count == 0) return new();

                // Calculate safe range (to avoid out-of-bounds access）
                int clampedStart = Math.Clamp(start, 1, count);
                int clampedEnd = Math.Clamp(end, clampedStart, count);

                // Get boundary elements
                var minElement = _sortedSet.ElementAt(clampedStart - 1);
                var maxElement = _sortedSet.ElementAt(clampedEnd - 1);

                // ‌Use GetViewBetween to get subset views
                var rangeView = _sortedSet.GetViewBetween(minElement, maxElement);

                // Return result
                return rangeView.Select((item, i) => new CustomerRankOM(
                    item.CustomerId,
                    item.Score,
                    clampedStart + i))
                .ToList();
            }
            finally
            {
                // ‌Release lock
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get around customers
        /// </summary>
        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low)
        {
            // ‌Acquire read lock (enabling concurrent read operations)
            _lock.EnterReadLock();
            try
            {
                // Get target
                var target = _sortedSet.FirstOrDefault(x => x.CustomerId == customerid);
                if (target == default) return new();

                // Get target index
                int index = _sortedSet.GetViewBetween(_sortedSet.Min, target).Count - 1;

                // Calculate the starting and ending element index
                int start = Math.Clamp(index - high, 0, _sortedSet.Count - 1);
                int end = Math.Clamp(index + low, start, _sortedSet.Count - 1);

                // ‌Use GetViewBetween to get subset views
                var rangeView = _sortedSet.GetViewBetween(_sortedSet.ElementAt(start),_sortedSet.ElementAt(end));

                // Return result
                return rangeView.Select((item, i) =>
                    new CustomerRankOM(item.CustomerId, item.Score, start + i + 1))
                    .ToList();
            }
            finally
            {
                // Release lock
                _lock.ExitReadLock();
            }
        }

    }
}

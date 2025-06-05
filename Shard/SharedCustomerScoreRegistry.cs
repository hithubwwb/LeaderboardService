using System.Collections.Concurrent;

namespace LeaderboardService.Shard
{
    // Thread-safe registry for storing and ranking customer scores
    public class SharedCustomerScoreRegistry
    {
        // Concurrent storage of customerID-score pairs
        public readonly ConcurrentDictionary<long, decimal> _concurrentDictionary = [];

        // Sorted set for ranked access (descending by score, ascending by customerID)
        public readonly SortedSet<(decimal Score, long CustomerId)> _sortedSet = new(new CustomerComparer());

        // Custom comparer for sorting rules
        private class CustomerComparer : IComparer<(decimal Score, long CustomerId)>
        {
            public int Compare((decimal Score, long CustomerId) x, (decimal Score, long CustomerId) y)
            {
                int scoreComparison = y.Score.CompareTo(x.Score); // Higher scores first
                return scoreComparison != 0 ? scoreComparison : x.CustomerId.CompareTo(y.CustomerId); // Tie-breaker
            }
        }
    }

}

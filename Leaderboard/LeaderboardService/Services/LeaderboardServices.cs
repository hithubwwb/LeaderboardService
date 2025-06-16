using LeaderboardService.Extensions;
using LeaderboardService.Model;
using LeaderboardService.Shard;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LeaderboardService.Services
{
    public class LeaderboardServices
    {
        private readonly SharedCollection _sharedCollection;

        // Basic data collection
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // Select data collection
        private readonly ConcurrentSkipList<long, RankItem> _skipList;

        // LeaderboardServices
        public LeaderboardServices(SharedCollection sharedSkipList)
        {            
            _sharedCollection = sharedSkipList;
            _concurrentDictionary = _sharedCollection.GetConcurrentDictionary;
            _skipList = _sharedCollection.GetSkipList;
        }

        // AddOrUpdateScore
        public decimal AddOrUpdateScore(long customerId, decimal changesScore)
        {
            // Check data
            var isHave = _concurrentDictionary.TryGetValue(customerId, out _);

            // AddOrUpdate from concurrentDictionary
            var item = _concurrentDictionary.AddOrUpdate(customerId, new RankItem(customerId, changesScore), (key, value) => new RankItem(customerId, value.Score + changesScore));
            if (item.Score <= 0 || item.Score > 1000)
            {
                // Remove data with scores ≤ 0 or > 1000 from the leaderboard collection, and only sort positive-score data to save storage space.
                _concurrentDictionary.TryRemove(customerId, out item);
                _skipList.RemoveByKey(item.CustomerId);
                return 0;
            }

            // AddOrUpdate from skipList
            if (isHave)
                _skipList.TryUpdate(customerId, item);
            else 
                _skipList.TryAdd(customerId, item);
            
            return item.Score;
        }

        // GetCustomersByRank
        public List<CustomerRankOM> GetCustomersByRank(int start, int end)
        {
            var items = _skipList.GetRange(start - 1, end - start + 1);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId,x.Score, start + i)).ToList();
        }

        // GetAroundCustomers
        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low) 
        {
            int rank;
            var items = _skipList.GetNeighbors(customerid, high, low, out rank);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, rank + i)).ToList();
        }

        // Temp test function
        public int AddTestData()
        {
            int currentUsers = 5;
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };

            Parallel.ForEach(Enumerable.Range(1, currentUsers), parallelOptions, customerId =>
            {
                for (int j = 0; j < 5; j++)
                {
                    this.AddOrUpdateScore(customerId, 10m);
                }
            });
            //this.AddOrUpdateScore(1000001, 768);
            return this._concurrentDictionary.Count();
        }

    }
}

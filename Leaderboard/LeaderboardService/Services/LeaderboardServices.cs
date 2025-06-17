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

        // data index
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // base data collection
        private readonly ConcurrentSkipList<RankItem> _skipList = new ConcurrentSkipList<RankItem>();

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
            var isHave = _concurrentDictionary.TryGetValue(customerId, out var oldData);

            // AddOrUpdate from concurrentDictionary
            var item = _concurrentDictionary.AddOrUpdate(customerId, new RankItem(customerId, changesScore), (key, value) => new RankItem(customerId, value.Score + changesScore));
            if (item.Score <= 0 || item.Score > 1000)
            {
                // Remove data with scores ≤ 0 or > 1000 from the leaderboard collection, and only sort positive-score data to save storage space.
                _concurrentDictionary.TryRemove(customerId, out item);
                _skipList.Remove(item);
                return 0;
            }

            // AddOrUpdate from skipList
            if (isHave)
            {
                //_skipList.Update(oldData,item);
                //_skipList.TryUpdate(customerId, item);
            }
            else
            {
                _skipList.Add(item);
                //_skipList.TryAdd(customerId, item);
            }

            return item.Score;
        }

        // GetCustomersByRank
        public List<CustomerRankOM> GetCustomersByRank(int start, int end)
        {
            var items = _skipList.GetRangeByRank(start, end);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId,x.Score, start + i)).ToList();
        }

        // GetAroundCustomers
        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low) 
        {
            if (!_concurrentDictionary.TryGetValue(customerid, out var entity)) return [];
            int rank;
            var items = _skipList.GetNeighbors(entity, high, low, out rank);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, rank + i)).ToList();
        }

        // Temp test function
        public long AddTestData()
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            int textCount = 1000000;
            var random = new Random();

            Parallel.For(1, textCount + 1, parallelOptions, i =>
            {
                for(int j =0;j <= 5; j++)
                {
                    this.AddOrUpdateScore(i, i);
                    //_concurrentDictionary.AddOrUpdate(i, new RankItem(i, 1), (key, value) => new RankItem(i, value.Score + 1));
                    //_skipList.Add(new RankItem(i, 1));
                }
            });

            return _skipList.Count;
        }

    }
}

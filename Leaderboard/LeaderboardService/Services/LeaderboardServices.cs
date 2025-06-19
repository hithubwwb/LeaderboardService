using LeaderboardService.Extensions;
using LeaderboardService.Model;
using LeaderboardService.Shard;
using System.Collections.Concurrent;

namespace LeaderboardService.Services
{
    public class LeaderboardServices
    {
        private readonly SharedCollection _sharedCollection;

        // data index
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // base data collection
        private readonly ConcurrentSkipList<RankItem> _skipList = new ConcurrentSkipList<RankItem>();

        public LeaderboardServices(SharedCollection sharedSkipList)
        {
            _sharedCollection = sharedSkipList;
            _concurrentDictionary = _sharedCollection.GetConcurrentDictionary;
            _skipList = _sharedCollection.GetSkipList;
        }

        // AddOrUpdateScore
        public decimal AddOrUpdateScore(long customerId, decimal changesScore)
        {
            var returnScore = changesScore;
            _skipList.AddOrUpdate(
                       new RankItem(customerId, changesScore),
                       (existing, newItem) => {
                           decimal newScore = existing.Score + newItem.Score;
                           // Control score limit 1 - 1000
                           if (newScore < 1 || newScore > 1000)
                           {
                               returnScore = existing.Score;
                               return existing;
                           }

                           returnScore = newScore;
                           existing.Score = newScore;
                           return existing;
                       });

            return returnScore;
        }

        // GetCustomersByRank
        public List<CustomerRankOM> GetCustomersByRank(int start, int end)
        {
            var items = _skipList.GetRangeByRank(start, end);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, start + i)).ToList();
        }

        // GetAroundCustomers
        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low)
        {
            if (_skipList.TryGetValue(customerid, out var orginEntity))
            {
                int rank;
                var items = _skipList.GetNeighbors(orginEntity!, high, low, out rank);
                return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, rank + i)).ToList();
            }
            return [];
        }

        // AddTestData
        public long AddTestData()
        {
            //if (_skipList.Count > 0)
            //    return _skipList.Count;

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            int textCount = 1000000;

            Parallel.For(1, textCount + 1, parallelOptions, i =>
            {
                var customerId = i;
                var changesScore = 1;
                //for (int j = 0; j < 5; j++)
                {
                    //_skipList.AddOrUpdate(new RankItem(i, changesScore),(existing, newItem) => {existing.Score += newItem.Score;
                    //        return existing;
                    //    });

                    this.AddOrUpdateScore(i, 1);

                    //_skipList.Add(new RankItem(i,1));
                }
            });

            return _skipList.Count;

        }
    }
}

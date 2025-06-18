using LeaderboardService.Extensions;
using LeaderboardService.Model;
using LeaderboardService.Shard;
using System.Collections.Concurrent;

namespace LeaderboardService.Services
{
    public class LeaderboardServices2
    {
        private readonly SharedCollection _sharedCollection;

        // data index
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // base data collection
        private readonly ConcurrentSkipList5<RankItem> _skipList = new ConcurrentSkipList5<RankItem>();

        public LeaderboardServices2(SharedCollection sharedSkipList)
        {
            _sharedCollection = sharedSkipList;
            _concurrentDictionary = _sharedCollection.GetConcurrentDictionary;
            _skipList = _sharedCollection.GetSkipList5;
        }

        public List<CustomerRankOM> GetCustomersByRank(int start, int end)
        {
            var items = _skipList.GetRangeByRank(start, end);
            return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, start + i)).ToList();
        }

        public List<CustomerRankOM> GetAroundCustomers(long customerid, int high, int low)
        {
            if (_skipList.TryGetValue(p => p.CustomerId, customerid, out var orginEntity)) 
            {
                int rank;
                var items = _skipList.GetNeighbors(orginEntity, high, low, out rank);
                return items.Select((x, i) => new CustomerRankOM(x.CustomerId, x.Score, rank + i)).ToList();
            }
            return [];
        }

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
                for (int j = 0; j < 5; j++)
                {
                    //_skipList.Add(new RankItem(i,1));

                    _skipList.AddOrUpdate(new RankItem(i, changesScore),(existing, newItem) => {existing.Score += newItem.Score;
                            return existing;
                        });
                }
            });

            return _skipList.Count;

        }
    }
}

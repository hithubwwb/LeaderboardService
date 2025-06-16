using LeaderboardService.Extensions;
using LeaderboardService.Model;
using System.Collections.Concurrent;

namespace LeaderboardService.Shard
{
    public class SharedCollection
    {
        // Basic data collection
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // Select data collection
        private readonly ConcurrentSkipList<long, RankItem> _skipList;

        public SharedCollection() 
        {
            _skipList = new ConcurrentSkipList<long, RankItem>();
        }

        public ConcurrentDictionary<long, RankItem> GetConcurrentDictionary => _concurrentDictionary;

        public ConcurrentSkipList<long, RankItem> GetSkipList => _skipList;

    }
}

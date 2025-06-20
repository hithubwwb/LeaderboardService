﻿using LeaderboardService.Extensions;
using LeaderboardService.Model;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;

namespace LeaderboardService.Shard
{
    public class SharedCollection
    {
        // Basic data collection
        private readonly ConcurrentDictionary<long, RankItem> _concurrentDictionary = [];

        // Select data collection
        private readonly ConcurrentSkipList<RankItem> _skipList = new ConcurrentSkipList<RankItem>();


        public ConcurrentDictionary<long, RankItem> GetConcurrentDictionary => _concurrentDictionary;

        public ConcurrentSkipList<RankItem> GetSkipList => _skipList;

    }
}

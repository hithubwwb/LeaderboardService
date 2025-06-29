﻿using LeaderboardService.Services;
using LeaderboardService.Shard;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LeaderboardService.Tests
{
    [TestClass]
    public sealed class FunctionTest
    {
        private static SharedCollection _shard;
        private static LeaderboardServices _leaderboardServices;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _shard = new SharedCollection();
            _leaderboardServices = new LeaderboardServices(_shard);
            InitData();
        }

        // Init data
        private static void InitData() 
        {
            _leaderboardServices.AddOrUpdateScore(1, 1);
            _leaderboardServices.AddOrUpdateScore(2, 2);
            _leaderboardServices.AddOrUpdateScore(3, 3);
            _leaderboardServices.AddOrUpdateScore(4, 4);
            _leaderboardServices.AddOrUpdateScore(5, 5);
            _leaderboardServices.AddOrUpdateScore(6, 5);
            // _leaderboardServices.AddOrUpdateScore(7, 150);

            // Wait mq sync
            while (true) { 
                if(_shard.GetSkipList.Count == 6)
                    break;
            }
        }

        [TestMethod]
        public void AddOrUpdateScore_Test()
        {
            // Arrange
            long customerId = 7;
            decimal initialScore = 100m;

            // +100
            var result = _leaderboardServices.AddOrUpdateScore(customerId, initialScore);

            // Assert
            Assert.AreEqual(100m, result);

            // +200
            var result2 = _leaderboardServices.AddOrUpdateScore(customerId, 200m);
            // Assert
            Assert.AreEqual(300m, result2);

            // -150
            var result3 = _leaderboardServices.AddOrUpdateScore(customerId, -150m);
            // Assert
            Assert.AreEqual(150m, result3);

            // -150
            var result4 = _leaderboardServices.AddOrUpdateScore(customerId, -150m);

            // when the result is 150 means the target was unchange
            Assert.AreEqual(150, result4);
        }

        [TestMethod]
        public void GetCustomersByRank_Test()
        {
            var result = _leaderboardServices.GetCustomersByRank(3, 4);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(6, result[0].CustomerId);
            Assert.AreEqual(4, result[1].CustomerId);

            var result2 = _leaderboardServices.GetCustomersByRank(2, 5);
            Assert.AreEqual(4, result2.Count);
            Assert.AreEqual(5, result2.First().CustomerId);
            Assert.AreEqual(3, result2.Last().CustomerId);
        }

        [TestMethod]
        public void GetAroundCustomers_Test()
        {
            var result = _leaderboardServices.GetAroundCustomers(4, 2, 1);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(5, result.First().CustomerId);
            Assert.AreEqual(3, result.Last().CustomerId);
        }

    }
}

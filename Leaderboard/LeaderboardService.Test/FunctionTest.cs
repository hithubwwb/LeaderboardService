using LeaderboardService.Services;
using LeaderboardService.Shard;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LeaderboardService.Tests
{
    [TestClass]
    public sealed class FunctionTest
    {
        private static SharedLockListCache _shard = new SharedLockListCache();
        private static LeaderboardServices _leaderboardServices = new LeaderboardServices(_shard);

        //[TestMethod]
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

            // Assert if result <= 0 is 1 
            Assert.AreEqual(1, result4);
        }

        [TestMethod]
        public void GetCustomersByRank_Test()
        {
            // Init data
            _leaderboardServices.AddOrUpdateScore(1, 1);
            _leaderboardServices.AddOrUpdateScore(2, 2);
            _leaderboardServices.AddOrUpdateScore(3, 3);
            _leaderboardServices.AddOrUpdateScore(4, 4);
            _leaderboardServices.AddOrUpdateScore(5, 5);
            _leaderboardServices.AddOrUpdateScore(6, 5);

            var result = _leaderboardServices.GetCustomersByRank(3, 4);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(4, result[0].CustomerId);
            Assert.AreEqual(3, result[1].CustomerId);

            var result2 = _leaderboardServices.GetCustomersByRank(2, 5);
            Assert.AreEqual(4, result2.Count);
            Assert.AreEqual(6, result2.First().CustomerId);
            Assert.AreEqual(2, result2.Last().CustomerId);
        }

        [TestMethod]
        public void GetAroundCustomers_Test()
        {
            // Init data
            _leaderboardServices.AddOrUpdateScore(1, 1);
            _leaderboardServices.AddOrUpdateScore(2, 2);
            _leaderboardServices.AddOrUpdateScore(3, 3);
            _leaderboardServices.AddOrUpdateScore(4, 4);
            _leaderboardServices.AddOrUpdateScore(5, 5);
            _leaderboardServices.AddOrUpdateScore(6, 5);

            var result = _leaderboardServices.GetAroundCustomers(4, 2, 1);
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(5, result.First().CustomerId);
            Assert.AreEqual(3, result.Last().CustomerId);
        }

    }
}

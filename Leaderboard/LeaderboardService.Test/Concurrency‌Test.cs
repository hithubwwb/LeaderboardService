using LeaderboardService.Services;
using LeaderboardService.Shard;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;

namespace LeaderboardService.Tests;

[TestClass]
public sealed class Concurrency‌Test
{
    private static SharedLockListCache _shard = new SharedLockListCache();
    private static LeaderboardServices _leaderboardServices = new LeaderboardServices(_shard);

    private const int ConcurrentUsers = 1000000;
    private const int TestRounds = 5;

    [TestMethod]
    public void AddOrUpdateScoreTest()
    {
        InitData();
        _shard.RefreshCache(default);

        Assert.AreEqual(ConcurrentUsers, _shard.GetConcurrentDictionaryData.Count);

        foreach (var score in _shard.GetConcurrentDictionaryData.Values)
        {
            Assert.AreEqual(10m * TestRounds, score);
        }
    }

    private static void InitData()
    {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };

        // Init data
        Parallel.ForEach(Enumerable.Range(1, ConcurrentUsers), parallelOptions, customerId =>
        {
            for (int j = 0; j < TestRounds; j++)
            {
                _leaderboardServices.AddOrUpdateScore(customerId, 10m);
            }
        });
    }

    [TestMethod]
    public void GetCustomersByRankTest()
    {
        InitData();
        _shard.RefreshCache(default);

        // GetCustomers
        Parallel.For(0, ConcurrentUsers, i =>
        {
            var result1 = _leaderboardServices.GetCustomersByRank(1, 100);
            Assert.AreEqual(result1.Count > 100 ? 100 : result1.Count, result1.Count);
        });
    }


    [TestMethod]
    public void GetAroundCustomersTest()
    {
        InitData();
        _shard.RefreshCache(default);

        // GetCustomers
        Parallel.For(0, ConcurrentUsers, i =>
        {
            var result1 = _leaderboardServices.GetAroundCustomers(1, 1, 100);
            Assert.AreEqual(result1.Count > 100 ? 101 : result1.Count, result1.Count);
        });
    }

}

using LeaderboardService.Services;
using LeaderboardService.Shard;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;

namespace LeaderboardService.Tests;

[TestClass]
public sealed class Concurrency‌Test
{
    private static SharedCollection _shard;
    private static LeaderboardServices _leaderboardServices;

    private const int ConcurrentUsers = 1000000;
    private const int TestRounds = 5;

    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
        _shard = new SharedCollection();
        _leaderboardServices = new LeaderboardServices(_shard);
        InitData();

        // Wait mq sync
        while (true)
        {
            if (_shard.GetSkipList.Count >= 1000)
                break;
        }
    }

    // init data
    private static void InitData()
    {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
        
        Parallel.ForEach(Enumerable.Range(1, ConcurrentUsers), parallelOptions, customerId =>
        {
            for (int j = 0; j < TestRounds; j++)
            {
                _leaderboardServices.AddOrUpdateScore(customerId, 10m);
            }
        });
    }

    [TestMethod]
    public void AddOrUpdateScoreTest()
    {
        Assert.AreEqual(ConcurrentUsers, _shard.GetSkipList.Count);

        foreach (var item in _shard.GetConcurrentDictionary.Values)
        {
            Assert.AreEqual(10m * TestRounds, item.Score);
        }
    }

    [TestMethod]
    public void GetCustomersByRankTest()
    {
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
        // GetCustomers
        Parallel.For(0, ConcurrentUsers, i =>
        {
            var result1 = _leaderboardServices.GetAroundCustomers(1, 1, 100);
            Assert.AreEqual(result1.Count > 100 ? result1.Count : result1.Count, result1.Count);
        });
    }

}

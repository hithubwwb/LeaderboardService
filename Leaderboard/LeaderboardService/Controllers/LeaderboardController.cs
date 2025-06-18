using LeaderboardService.Model;
using LeaderboardService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace LeaderboardService.Controllers
{
    [ApiController]
    public class LeaderboardController : ControllerBase
    {
        private readonly LeaderboardServices2 _leaderboardServices;
        public LeaderboardController(LeaderboardServices2 leaderboardServices)
        {
            _leaderboardServices = leaderboardServices;
        }

        [HttpPost("customer/{customerid}/score/{score}")]
        [ProducesResponseType(typeof(decimal), (int)HttpStatusCode.OK)]
        public IActionResult Post(long customerid, decimal score = 0)
        {
            if (customerid <= 0)
                return BadRequest("The customerid must be > 0.");

            if (score < -999 || score > 999)
                return BadRequest("The score must be between -1000 and 1000.");

            if (score == 0)
                return BadRequest("The data is unchanges.");

            var result = _leaderboardServices.AddOrUpdateScore(customerid, score);
            return Ok(result);
        }

        [HttpGet("leaderboard")]
        [ProducesResponseType(typeof(List<CustomerRankOM>), (int)HttpStatusCode.OK)]
        public IActionResult GetCustombersByRank(int start, int end)
        {
            if (start < 1 || end < start)
                return BadRequest("Invalid range.");

            var limitCount = end - start;
            if (limitCount > 10000)
                return BadRequest($"Invalid range: {limitCount} exceeds maximum limit of 10000");

            var result = _leaderboardServices.GetCustomersByRank(start, end);
            return Ok(result);
        }


        [HttpGet("leaderboard/{customerid}")]
        [ProducesResponseType(typeof(List<CustomerRankOM>), (int)HttpStatusCode.OK)]
        public IActionResult GetAroundCustomers(long customerid, int high = 0, int low = 0)
        {
            if (customerid <= 0)
                return BadRequest("The customerid must be > 0.");

            if (high < 0 || low < 0)
                return BadRequest("Invalid range.");

            var limitCount = high + 1 + low;
            if (limitCount > 10000)
                return BadRequest($"Invalid range: {limitCount} exceeds maximum limit of 10000");

            var result = _leaderboardServices.GetAroundCustomers(customerid, high, low);
            return Ok(result);
        }

        [HttpGet("addtestdata")]
        [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
        public IActionResult AddTestData()
        {
            var result = _leaderboardServices.AddTestData();
            return Ok(result);
        }
    }
}

namespace LeaderboardService.Model
{
    public class CustomerRankOM
    {
        public CustomerRankOM(long customerId, decimal score, int rank)
        {
            CustomerId = customerId;
            Score = score;
            Rank = rank;
        }

        public long CustomerId { get; set; }
        public decimal Score { get; set; }
        public int Rank { get; set; }

    }
}

namespace LeaderboardService.Model
{
    public class RankItem : IComparable<RankItem>
    {
        public long CustomerId { get; }
        public decimal Score { get; }

        public RankItem() { }

        public RankItem(long customerId, decimal score)
        {
            CustomerId = customerId;
            Score = score;
        }

        public int CompareTo(RankItem other)
        {
            var scoreCompare = other.Score.CompareTo(Score);
            return scoreCompare != 0 ? scoreCompare : CustomerId.CompareTo(other.CustomerId);
        }
    }

}

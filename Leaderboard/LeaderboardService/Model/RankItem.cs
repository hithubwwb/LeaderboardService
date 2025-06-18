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
            if (other == null) return 1;

            // 先按分数降序
            int scoreComparison = other.Score.CompareTo(this.Score);
            if (scoreComparison != 0)
                return scoreComparison;

            // 分数相同时按ID升序
            return this.CustomerId.CompareTo(other.CustomerId);
        }

        public override bool Equals(object obj) =>
        obj is RankItem other && CustomerId == other.CustomerId && Score == other.Score;

        public override int GetHashCode() =>
            HashCode.Combine(CustomerId, Score);
    }

}

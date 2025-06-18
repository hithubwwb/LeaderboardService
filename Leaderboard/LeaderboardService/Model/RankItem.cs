using LeaderboardService.Extensions;

namespace LeaderboardService.Model
{
    public class RankItem : IComparable<RankItem>, IScorable
    {
        private readonly object _lock = new object();
        public long CustomerId { get; }

        public decimal Score { get; set; }

        private long _timestamp;
        public long Timestamp
        {
            get
            {
                lock (_lock)
                {
                    return _timestamp;
                }
            }
            set
            {
                lock (_lock)
                {
                    _timestamp = value;
                }
            }
        }

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

        //public override bool Equals(object obj) =>
        //obj is RankItem other && CustomerId == other.CustomerId && Score == other.Score;

        //public override int GetHashCode() =>
        //    HashCode.Combine(CustomerId, Score);
    }

}

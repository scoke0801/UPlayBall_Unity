using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 한 타순 역할에서 교타·장타·주력·정신력이 차지하는 평가 비중을 보관한다.
    /// </summary>
    public readonly struct BattingOrderScoreWeights
    {
        public BattingOrderScoreWeights(
            double contact,
            double power,
            double speed,
            double mental)
        {
            if (contact < 0d || power < 0d || speed < 0d || mental < 0d)
                throw new ArgumentOutOfRangeException(nameof(contact));
            if (Math.Abs(contact + power + speed + mental - 1d) > 0.000001d)
                throw new ArgumentException("타순 평가 가중치 합은 1이어야 합니다.");

            Contact = contact;
            Power = power;
            Speed = speed;
            Mental = mental;
        }

        public double Contact { get; }
        public double Power { get; }
        public double Speed { get; }
        public double Mental { get; }
    }

    /// <summary>
    /// 감독 AI가 상위·중심·하위 타선을 편성할 때 사용하는 역할별 능력치 가중치다.
    /// </summary>
    public readonly struct ManagerLineupBalance
    {
        public ManagerLineupBalance(
            BattingOrderScoreWeights leadoff,
            BattingOrderScoreWeights tableSetter,
            BattingOrderScoreWeights runProducer,
            BattingOrderScoreWeights cleanup,
            BattingOrderScoreWeights lowerOrder)
        {
            Leadoff = leadoff;
            TableSetter = tableSetter;
            RunProducer = runProducer;
            Cleanup = cleanup;
            LowerOrder = lowerOrder;
        }

        public BattingOrderScoreWeights Leadoff { get; }
        public BattingOrderScoreWeights TableSetter { get; }
        public BattingOrderScoreWeights RunProducer { get; }
        public BattingOrderScoreWeights Cleanup { get; }
        public BattingOrderScoreWeights LowerOrder { get; }

        /// <summary>
        /// 출루 능력은 상위 타선, 장타력은 중심 타선에서 더 크게 보이는 최초 감독 편성값을 만든다.
        /// </summary>
        public static ManagerLineupBalance CreateDefault()
        {
            return new ManagerLineupBalance(
                leadoff: new BattingOrderScoreWeights(0.45d, 0d, 0.30d, 0.25d),
                tableSetter: new BattingOrderScoreWeights(0.50d, 0.10d, 0.15d, 0.25d),
                runProducer: new BattingOrderScoreWeights(0.35d, 0.45d, 0d, 0.20d),
                cleanup: new BattingOrderScoreWeights(0.25d, 0.60d, 0d, 0.15d),
                lowerOrder: new BattingOrderScoreWeights(0.35d, 0.35d, 0.15d, 0.15d));
        }
    }
}

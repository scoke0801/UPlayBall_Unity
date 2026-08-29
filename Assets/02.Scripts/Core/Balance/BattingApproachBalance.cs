using System;
using Baseball.Core.Players;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 한 타격 접근 방식이 투구와 타구 확률에 더하는 보정값을 보관한다.
    /// </summary>
    public readonly struct BattingApproachModifier
    {
        public BattingApproachModifier(
            double strikeSwingAdjustment,
            double chaseAdjustment,
            double contactAdjustment,
            double fairContactAdjustment,
            double homeRunAdjustment,
            double nonHomeRunHitAdjustment,
            double doubleShareAdjustment)
        {
            StrikeSwingAdjustment = strikeSwingAdjustment;
            ChaseAdjustment = chaseAdjustment;
            ContactAdjustment = contactAdjustment;
            FairContactAdjustment = fairContactAdjustment;
            HomeRunAdjustment = homeRunAdjustment;
            NonHomeRunHitAdjustment = nonHomeRunHitAdjustment;
            DoubleShareAdjustment = doubleShareAdjustment;
        }

        public double StrikeSwingAdjustment { get; }
        public double ChaseAdjustment { get; }
        public double ContactAdjustment { get; }
        public double FairContactAdjustment { get; }
        public double HomeRunAdjustment { get; }
        public double NonHomeRunHitAdjustment { get; }
        public double DoubleShareAdjustment { get; }
    }

    /// <summary>
    /// 균형·컨택·장타·신중 타격의 확률 보정을 한 밸런스 묶음으로 제공한다.
    /// </summary>
    public readonly struct BattingApproachBalance
    {
        public BattingApproachBalance(
            BattingApproachModifier balanced,
            BattingApproachModifier contact,
            BattingApproachModifier power,
            BattingApproachModifier patient)
            : this(balanced, contact, power, patient, balanced, balanced)
        {
        }

        public BattingApproachBalance(
            BattingApproachModifier balanced,
            BattingApproachModifier contact,
            BattingApproachModifier power,
            BattingApproachModifier patient,
            BattingApproachModifier aggressive,
            BattingApproachModifier bunt)
        {
            Balanced = balanced;
            Contact = contact;
            Power = power;
            Patient = patient;
            Aggressive = aggressive;
            Bunt = bunt;
        }

        public BattingApproachModifier Balanced { get; }
        public BattingApproachModifier Contact { get; }
        public BattingApproachModifier Power { get; }
        public BattingApproachModifier Patient { get; }
        public BattingApproachModifier Aggressive { get; }
        public BattingApproachModifier Bunt { get; }

        /// <summary>
        /// 선택한 타격 방식에 대응하는 보정값을 반환한다.
        /// </summary>
        public BattingApproachModifier GetModifier(BattingApproach approach)
        {
            return approach switch
            {
                BattingApproach.Balanced => Balanced,
                BattingApproach.Contact => Contact,
                BattingApproach.Power => Power,
                BattingApproach.Patient => Patient,
                BattingApproach.Aggressive => Aggressive,
                BattingApproach.Bunt => Bunt,
                _ => throw new ArgumentOutOfRangeException(nameof(approach))
            };
        }

        /// <summary>
        /// 선택마다 명확한 장단점이 생기되 기존 균형 타격 통계는 바꾸지 않는 초기값을 만든다.
        /// </summary>
        public static BattingApproachBalance CreateDefault()
        {
            return new BattingApproachBalance(
                balanced: new BattingApproachModifier(0d, 0d, 0d, 0d, 0d, 0d, 0d),
                contact: new BattingApproachModifier(0.03d, -0.02d, 0.10d, -0.02d, -0.025d, 0.015d, -0.06d),
                power: new BattingApproachModifier(0.05d, 0.05d, -0.12d, 0.02d, 0.035d, -0.015d, 0.06d),
                patient: new BattingApproachModifier(-0.18d, -0.12d, 0.02d, 0d, -0.005d, 0d, -0.01d),
                aggressive: new BattingApproachModifier(0.08d, 0.04d, -0.04d, 0.01d, 0.010d, 0d, 0.01d),
                bunt: new BattingApproachModifier(-0.22d, -0.18d, 0.04d, -0.08d, -0.04d, -0.03d, -0.08d));
        }
    }
}

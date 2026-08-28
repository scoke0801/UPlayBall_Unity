namespace Baseball.Core.Balance
{
    /// <summary>
    /// 계약 상여 조건의 목표와 연봉 대비 보상 비율을 보관한다.
    /// </summary>
    public readonly struct ContractBonusBalance
    {
        public ContractBonusBalance(
            double appearanceTargetRate,
            int batterHomeRunTarget,
            int batterRunsBattedInTarget,
            double batterOpsTarget,
            int pitcherAppearanceTarget,
            int pitcherOutsTarget,
            int pitcherStrikeoutTarget,
            double pitcherEraTarget,
            double appearanceSalaryRate,
            double countingStatSalaryRate,
            double rateStatSalaryRate,
            double individualAwardSalaryRate,
            double championshipSalaryRate)
        {
            if (appearanceTargetRate <= 0d || appearanceTargetRate > 1d)
                throw new System.ArgumentOutOfRangeException(nameof(appearanceTargetRate));
            if (batterHomeRunTarget <= 0 || batterRunsBattedInTarget <= 0 || batterOpsTarget <= 0d)
                throw new System.ArgumentOutOfRangeException(nameof(batterHomeRunTarget));
            if (pitcherAppearanceTarget <= 0 || pitcherOutsTarget <= 0 ||
                pitcherStrikeoutTarget <= 0 || pitcherEraTarget <= 0d)
            {
                throw new System.ArgumentOutOfRangeException(nameof(pitcherAppearanceTarget));
            }
            if (appearanceSalaryRate < 0d || countingStatSalaryRate < 0d ||
                rateStatSalaryRate < 0d || individualAwardSalaryRate < 0d ||
                championshipSalaryRate < 0d)
            {
                throw new System.ArgumentOutOfRangeException(nameof(appearanceSalaryRate));
            }

            AppearanceTargetRate = appearanceTargetRate;
            BatterHomeRunTarget = batterHomeRunTarget;
            BatterRunsBattedInTarget = batterRunsBattedInTarget;
            BatterOpsTarget = batterOpsTarget;
            PitcherAppearanceTarget = pitcherAppearanceTarget;
            PitcherOutsTarget = pitcherOutsTarget;
            PitcherStrikeoutTarget = pitcherStrikeoutTarget;
            PitcherEraTarget = pitcherEraTarget;
            AppearanceSalaryRate = appearanceSalaryRate;
            CountingStatSalaryRate = countingStatSalaryRate;
            RateStatSalaryRate = rateStatSalaryRate;
            IndividualAwardSalaryRate = individualAwardSalaryRate;
            ChampionshipSalaryRate = championshipSalaryRate;
        }

        public double AppearanceTargetRate { get; }
        public int BatterHomeRunTarget { get; }
        public int BatterRunsBattedInTarget { get; }
        public double BatterOpsTarget { get; }
        public int PitcherAppearanceTarget { get; }
        public int PitcherOutsTarget { get; }
        public int PitcherStrikeoutTarget { get; }
        public double PitcherEraTarget { get; }
        public double AppearanceSalaryRate { get; }
        public double CountingStatSalaryRate { get; }
        public double RateStatSalaryRate { get; }
        public double IndividualAwardSalaryRate { get; }
        public double ChampionshipSalaryRate { get; }

        /// <summary>
        /// 80경기 Rookie 시즌에서 백업에게는 도전 목표, 주전에게는 복수 달성 목표가 되도록 한 초기값이다.
        /// </summary>
        public static ContractBonusBalance CreateDefault()
        {
            return new ContractBonusBalance(
                appearanceTargetRate: 0.375d,
                batterHomeRunTarget: 4,
                batterRunsBattedInTarget: 15,
                batterOpsTarget: 0.700d,
                pitcherAppearanceTarget: 30,
                pitcherOutsTarget: 135,
                pitcherStrikeoutTarget: 50,
                pitcherEraTarget: 4.00d,
                appearanceSalaryRate: 0.06d,
                countingStatSalaryRate: 0.08d,
                rateStatSalaryRate: 0.08d,
                individualAwardSalaryRate: 0.10d,
                championshipSalaryRate: 0.12d);
        }
    }
}

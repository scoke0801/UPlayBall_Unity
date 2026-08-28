namespace Baseball.Core.Balance
{
    /// <summary>시즌 수상·포스트시즌 Money 배율과 계약 평가 가산점을 보관한다.</summary>
    public readonly struct SeasonSettlementBalance
    {
        public SeasonSettlementBalance(
            long minimumAwardMoney,
            double regularSeasonMvpSalaryRate,
            double postseasonMvpSalaryRate,
            double rookieOfYearSalaryRate,
            double goldGloveSalaryRate,
            double recordAwardSalaryRate,
            double postseasonQualificationSalaryRate,
            double championshipSalaryRate,
            int regularSeasonMvpContractBonus,
            int postseasonMvpContractBonus,
            int rookieOfYearContractBonus,
            int goldGloveContractBonus,
            int recordAwardContractBonus,
            int championshipContractBonus,
            int runnerUpContractBonus,
            int maximumAwardContractBonus)
        {
            MinimumAwardMoney = minimumAwardMoney;
            RegularSeasonMvpSalaryRate = regularSeasonMvpSalaryRate;
            PostseasonMvpSalaryRate = postseasonMvpSalaryRate;
            RookieOfYearSalaryRate = rookieOfYearSalaryRate;
            GoldGloveSalaryRate = goldGloveSalaryRate;
            RecordAwardSalaryRate = recordAwardSalaryRate;
            PostseasonQualificationSalaryRate = postseasonQualificationSalaryRate;
            ChampionshipSalaryRate = championshipSalaryRate;
            RegularSeasonMvpContractBonus = regularSeasonMvpContractBonus;
            PostseasonMvpContractBonus = postseasonMvpContractBonus;
            RookieOfYearContractBonus = rookieOfYearContractBonus;
            GoldGloveContractBonus = goldGloveContractBonus;
            RecordAwardContractBonus = recordAwardContractBonus;
            ChampionshipContractBonus = championshipContractBonus;
            RunnerUpContractBonus = runnerUpContractBonus;
            MaximumAwardContractBonus = maximumAwardContractBonus;
        }

        public long MinimumAwardMoney { get; }
        public double RegularSeasonMvpSalaryRate { get; }
        public double PostseasonMvpSalaryRate { get; }
        public double RookieOfYearSalaryRate { get; }
        public double GoldGloveSalaryRate { get; }
        public double RecordAwardSalaryRate { get; }
        public double PostseasonQualificationSalaryRate { get; }
        public double ChampionshipSalaryRate { get; }
        public int RegularSeasonMvpContractBonus { get; }
        public int PostseasonMvpContractBonus { get; }
        public int RookieOfYearContractBonus { get; }
        public int GoldGloveContractBonus { get; }
        public int RecordAwardContractBonus { get; }
        public int ChampionshipContractBonus { get; }
        public int RunnerUpContractBonus { get; }
        public int MaximumAwardContractBonus { get; }

        public static SeasonSettlementBalance CreateDefault()
        {
            return new SeasonSettlementBalance(
                100L,
                0.20d, 0.12d, 0.08d, 0.06d, 0.04d, 0.02d, 0.08d,
                20, 12, 10, 8, 5, 8, 4, 30);
        }
    }
}

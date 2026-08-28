namespace Baseball.Core.Balance
{
    /// <summary>수상 후보 자격과 역할별 평가 가중치를 한 데이터 묶음으로 보관한다.</summary>
    public readonly struct SeasonAwardBalance
    {
        public SeasonAwardBalance(
            double batterMinimumPlateAppearancesPerTeamGame,
            double batterMinimumStartRate,
            double starterMinimumInningsPerTeamGame,
            double relieverMinimumAppearanceRate,
            double rookieEligibilityFactor,
            double reliefPitcherScoreAdjustment,
            double hitterOpsWeight,
            double hitterTotalBasesWeight,
            double hitterHomeRunsWeight,
            double hitterRunProductionWeight,
            double hitterDisciplineWeight,
            double hitterBaserunningWeight,
            double hitterFieldingWeight,
            double hitterPlayingTimeWeight,
            double hitterTeamWeight,
            double starterEraWeight,
            double starterWhipWeight,
            double starterStrikeoutWalkWeight,
            double starterHomeRunPreventionWeight,
            double starterInningsWeight,
            double starterResultsWeight,
            double starterTeamWeight,
            double relieverEraWeight,
            double relieverWhipWeight,
            double relieverStrikeoutWalkWeight,
            double relieverResultsWeight,
            double relieverInningsWeight,
            double relieverTeamWeight,
            double goldGloveMinimumInningsPerTeamGame,
            double catcherGoldGloveMinimumInningsPerTeamGame,
            double goldGloveRunsSavedWeight,
            double goldGloveStabilityWeight,
            double goldGloveDifficultPlayWeight,
            double goldGlovePositionWeight,
            double goldGloveInningsWeight,
            double fieldingRegressionOpportunities)
        {
            BatterMinimumPlateAppearancesPerTeamGame = batterMinimumPlateAppearancesPerTeamGame;
            BatterMinimumStartRate = batterMinimumStartRate;
            StarterMinimumInningsPerTeamGame = starterMinimumInningsPerTeamGame;
            RelieverMinimumAppearanceRate = relieverMinimumAppearanceRate;
            RookieEligibilityFactor = rookieEligibilityFactor;
            ReliefPitcherScoreAdjustment = reliefPitcherScoreAdjustment;
            HitterOpsWeight = hitterOpsWeight;
            HitterTotalBasesWeight = hitterTotalBasesWeight;
            HitterHomeRunsWeight = hitterHomeRunsWeight;
            HitterRunProductionWeight = hitterRunProductionWeight;
            HitterDisciplineWeight = hitterDisciplineWeight;
            HitterBaserunningWeight = hitterBaserunningWeight;
            HitterFieldingWeight = hitterFieldingWeight;
            HitterPlayingTimeWeight = hitterPlayingTimeWeight;
            HitterTeamWeight = hitterTeamWeight;
            StarterEraWeight = starterEraWeight;
            StarterWhipWeight = starterWhipWeight;
            StarterStrikeoutWalkWeight = starterStrikeoutWalkWeight;
            StarterHomeRunPreventionWeight = starterHomeRunPreventionWeight;
            StarterInningsWeight = starterInningsWeight;
            StarterResultsWeight = starterResultsWeight;
            StarterTeamWeight = starterTeamWeight;
            RelieverEraWeight = relieverEraWeight;
            RelieverWhipWeight = relieverWhipWeight;
            RelieverStrikeoutWalkWeight = relieverStrikeoutWalkWeight;
            RelieverResultsWeight = relieverResultsWeight;
            RelieverInningsWeight = relieverInningsWeight;
            RelieverTeamWeight = relieverTeamWeight;
            GoldGloveMinimumInningsPerTeamGame = goldGloveMinimumInningsPerTeamGame;
            CatcherGoldGloveMinimumInningsPerTeamGame = catcherGoldGloveMinimumInningsPerTeamGame;
            GoldGloveRunsSavedWeight = goldGloveRunsSavedWeight;
            GoldGloveStabilityWeight = goldGloveStabilityWeight;
            GoldGloveDifficultPlayWeight = goldGloveDifficultPlayWeight;
            GoldGlovePositionWeight = goldGlovePositionWeight;
            GoldGloveInningsWeight = goldGloveInningsWeight;
            FieldingRegressionOpportunities = fieldingRegressionOpportunities;
        }

        public double BatterMinimumPlateAppearancesPerTeamGame { get; }
        public double BatterMinimumStartRate { get; }
        public double StarterMinimumInningsPerTeamGame { get; }
        public double RelieverMinimumAppearanceRate { get; }
        public double RookieEligibilityFactor { get; }
        public double ReliefPitcherScoreAdjustment { get; }
        public double HitterOpsWeight { get; }
        public double HitterTotalBasesWeight { get; }
        public double HitterHomeRunsWeight { get; }
        public double HitterRunProductionWeight { get; }
        public double HitterDisciplineWeight { get; }
        public double HitterBaserunningWeight { get; }
        public double HitterFieldingWeight { get; }
        public double HitterPlayingTimeWeight { get; }
        public double HitterTeamWeight { get; }
        public double StarterEraWeight { get; }
        public double StarterWhipWeight { get; }
        public double StarterStrikeoutWalkWeight { get; }
        public double StarterHomeRunPreventionWeight { get; }
        public double StarterInningsWeight { get; }
        public double StarterResultsWeight { get; }
        public double StarterTeamWeight { get; }
        public double RelieverEraWeight { get; }
        public double RelieverWhipWeight { get; }
        public double RelieverStrikeoutWalkWeight { get; }
        public double RelieverResultsWeight { get; }
        public double RelieverInningsWeight { get; }
        public double RelieverTeamWeight { get; }
        public double GoldGloveMinimumInningsPerTeamGame { get; }
        public double CatcherGoldGloveMinimumInningsPerTeamGame { get; }
        public double GoldGloveRunsSavedWeight { get; }
        public double GoldGloveStabilityWeight { get; }
        public double GoldGloveDifficultPlayWeight { get; }
        public double GoldGlovePositionWeight { get; }
        public double GoldGloveInningsWeight { get; }
        public double FieldingRegressionOpportunities { get; }

        public static SeasonAwardBalance CreateDefault()
        {
            return new SeasonAwardBalance(
                2d, 0.5d, 0.5d, 0.25d, 0.7d, 0.96d,
                0.30d, 0.15d, 0.10d, 0.10d, 0.05d, 0.05d, 0.10d, 0.10d, 0.05d,
                0.25d, 0.15d, 0.15d, 0.10d, 0.20d, 0.10d, 0.05d,
                0.25d, 0.15d, 0.20d, 0.20d, 0.15d, 0.05d,
                4.5d, 3.6d, 0.40d, 0.25d, 0.20d, 0.10d, 0.05d, 20d);
        }
    }
}

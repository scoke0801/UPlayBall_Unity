using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 정규 시즌 일정·기용 판단·경기 후 상태 변화에 쓰는 조정 가능 계수를 보관한다.
    /// </summary>
    public readonly struct CareerSeasonBalance
    {
        public CareerSeasonBalance(
            int regularSeasonGamesPerTeam,
            int startingRotationSize,
            int reliefStartInning,
            double managerDecisionVariance,
            int startingCompetitionBonus,
            int rosterCompetitionBonus,
            int benchCompetitionBonus,
            int reliefOpportunityMargin,
            double benchSubstitutionOpportunityProbability,
            int benchSubstitutionEarliestInning,
            int benchSubstitutionMaximumScoreDifference,
            int startingCompetitionEvaluationInterval,
            int rosterCompetitionEvaluationInterval,
            int benchCompetitionEvaluationInterval,
            int evaluationOpportunityMinimumCondition,
            int initialCondition,
            int initialManagerEvaluation,
            int playingConditionCost,
            int restingConditionRecovery,
            int minimumCondition,
            int maximumManagerEvaluationChange,
            double conditionDecisionWeight,
            double managerEvaluationDecisionWeight,
            int productiveBattingHits,
            int excellentBattingHits,
            int poorBattingAtBats,
            int qualityPitchingMaximumEarnedRuns,
            int poorPitchingMinimumEarnedRuns,
            int positiveEvaluationChange,
            int excellentEvaluationChange,
            int poorEvaluationChange,
            int veryPoorEvaluationChange,
            int seasonOpeningMonth,
            int seasonOpeningDay,
            int gamesBetweenRestDays)
        {
            if (regularSeasonGamesPerTeam <= 0)
                throw new ArgumentOutOfRangeException(nameof(regularSeasonGamesPerTeam));
            if (startingRotationSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(startingRotationSize));
            if (reliefStartInning < 2 || reliefStartInning > 9)
                throw new ArgumentOutOfRangeException(nameof(reliefStartInning));
            if (managerDecisionVariance < 0d)
                throw new ArgumentOutOfRangeException(nameof(managerDecisionVariance));
            if (benchSubstitutionOpportunityProbability < 0d || benchSubstitutionOpportunityProbability > 1d)
                throw new ArgumentOutOfRangeException(nameof(benchSubstitutionOpportunityProbability));
            if (benchSubstitutionEarliestInning <= 0 || benchSubstitutionEarliestInning > 9)
                throw new ArgumentOutOfRangeException(nameof(benchSubstitutionEarliestInning));
            if (benchSubstitutionMaximumScoreDifference < 0)
                throw new ArgumentOutOfRangeException(nameof(benchSubstitutionMaximumScoreDifference));
            if (startingCompetitionEvaluationInterval <= 0 ||
                rosterCompetitionEvaluationInterval < startingCompetitionEvaluationInterval ||
                benchCompetitionEvaluationInterval < rosterCompetitionEvaluationInterval)
            {
                throw new ArgumentOutOfRangeException(nameof(startingCompetitionEvaluationInterval));
            }
            if (evaluationOpportunityMinimumCondition < 0 || evaluationOpportunityMinimumCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(evaluationOpportunityMinimumCondition));
            if (initialCondition < 0 || initialCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(initialCondition));
            if (initialManagerEvaluation < 0 || initialManagerEvaluation > 100)
                throw new ArgumentOutOfRangeException(nameof(initialManagerEvaluation));
            if (playingConditionCost < 0 || restingConditionRecovery < 0)
                throw new ArgumentOutOfRangeException(nameof(playingConditionCost));
            if (minimumCondition < 0 || minimumCondition > initialCondition)
                throw new ArgumentOutOfRangeException(nameof(minimumCondition));
            if (evaluationOpportunityMinimumCondition < minimumCondition ||
                evaluationOpportunityMinimumCondition > initialCondition)
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationOpportunityMinimumCondition));
            }
            if (maximumManagerEvaluationChange <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumManagerEvaluationChange));
            if (conditionDecisionWeight < 0d || managerEvaluationDecisionWeight < 0d)
                throw new ArgumentOutOfRangeException(nameof(conditionDecisionWeight));
            if (productiveBattingHits <= 0 || excellentBattingHits < productiveBattingHits)
                throw new ArgumentOutOfRangeException(nameof(productiveBattingHits));
            if (poorBattingAtBats <= 0)
                throw new ArgumentOutOfRangeException(nameof(poorBattingAtBats));
            if (qualityPitchingMaximumEarnedRuns < 0 ||
                poorPitchingMinimumEarnedRuns <= qualityPitchingMaximumEarnedRuns)
            {
                throw new ArgumentOutOfRangeException(nameof(qualityPitchingMaximumEarnedRuns));
            }
            if (positiveEvaluationChange <= 0 || excellentEvaluationChange < positiveEvaluationChange ||
                poorEvaluationChange >= 0 || veryPoorEvaluationChange > poorEvaluationChange)
            {
                throw new ArgumentOutOfRangeException(nameof(positiveEvaluationChange));
            }
            if (seasonOpeningMonth < 1 || seasonOpeningMonth > 12 || seasonOpeningDay < 1 || seasonOpeningDay > 28)
                throw new ArgumentOutOfRangeException(nameof(seasonOpeningMonth));
            if (gamesBetweenRestDays <= 0)
                throw new ArgumentOutOfRangeException(nameof(gamesBetweenRestDays));

            RegularSeasonGamesPerTeam = regularSeasonGamesPerTeam;
            StartingRotationSize = startingRotationSize;
            ReliefStartInning = reliefStartInning;
            ManagerDecisionVariance = managerDecisionVariance;
            StartingCompetitionBonus = startingCompetitionBonus;
            RosterCompetitionBonus = rosterCompetitionBonus;
            BenchCompetitionBonus = benchCompetitionBonus;
            ReliefOpportunityMargin = reliefOpportunityMargin;
            BenchSubstitutionOpportunityProbability = benchSubstitutionOpportunityProbability;
            BenchSubstitutionEarliestInning = benchSubstitutionEarliestInning;
            BenchSubstitutionMaximumScoreDifference = benchSubstitutionMaximumScoreDifference;
            StartingCompetitionEvaluationInterval = startingCompetitionEvaluationInterval;
            RosterCompetitionEvaluationInterval = rosterCompetitionEvaluationInterval;
            BenchCompetitionEvaluationInterval = benchCompetitionEvaluationInterval;
            EvaluationOpportunityMinimumCondition = evaluationOpportunityMinimumCondition;
            InitialCondition = initialCondition;
            InitialManagerEvaluation = initialManagerEvaluation;
            PlayingConditionCost = playingConditionCost;
            RestingConditionRecovery = restingConditionRecovery;
            MinimumCondition = minimumCondition;
            MaximumManagerEvaluationChange = maximumManagerEvaluationChange;
            ConditionDecisionWeight = conditionDecisionWeight;
            ManagerEvaluationDecisionWeight = managerEvaluationDecisionWeight;
            ProductiveBattingHits = productiveBattingHits;
            ExcellentBattingHits = excellentBattingHits;
            PoorBattingAtBats = poorBattingAtBats;
            QualityPitchingMaximumEarnedRuns = qualityPitchingMaximumEarnedRuns;
            PoorPitchingMinimumEarnedRuns = poorPitchingMinimumEarnedRuns;
            PositiveEvaluationChange = positiveEvaluationChange;
            ExcellentEvaluationChange = excellentEvaluationChange;
            PoorEvaluationChange = poorEvaluationChange;
            VeryPoorEvaluationChange = veryPoorEvaluationChange;
            SeasonOpeningMonth = seasonOpeningMonth;
            SeasonOpeningDay = seasonOpeningDay;
            GamesBetweenRestDays = gamesBetweenRestDays;
        }

        public int RegularSeasonGamesPerTeam { get; }
        public int StartingRotationSize { get; }
        public int ReliefStartInning { get; }
        public double ManagerDecisionVariance { get; }
        public int StartingCompetitionBonus { get; }
        public int RosterCompetitionBonus { get; }
        public int BenchCompetitionBonus { get; }
        public int ReliefOpportunityMargin { get; }
        public double BenchSubstitutionOpportunityProbability { get; }
        public int BenchSubstitutionEarliestInning { get; }
        public int BenchSubstitutionMaximumScoreDifference { get; }
        public int StartingCompetitionEvaluationInterval { get; }
        public int RosterCompetitionEvaluationInterval { get; }
        public int BenchCompetitionEvaluationInterval { get; }
        public int EvaluationOpportunityMinimumCondition { get; }
        public int InitialCondition { get; }
        public int InitialManagerEvaluation { get; }
        public int PlayingConditionCost { get; }
        public int RestingConditionRecovery { get; }
        public int MinimumCondition { get; }
        public int MaximumManagerEvaluationChange { get; }
        public double ConditionDecisionWeight { get; }
        public double ManagerEvaluationDecisionWeight { get; }
        public int ProductiveBattingHits { get; }
        public int ExcellentBattingHits { get; }
        public int PoorBattingAtBats { get; }
        public int QualityPitchingMaximumEarnedRuns { get; }
        public int PoorPitchingMinimumEarnedRuns { get; }
        public int PositiveEvaluationChange { get; }
        public int ExcellentEvaluationChange { get; }
        public int PoorEvaluationChange { get; }
        public int VeryPoorEvaluationChange { get; }
        public int SeasonOpeningMonth { get; }
        public int SeasonOpeningDay { get; }
        public int GamesBetweenRestDays { get; }

        /// <summary>
        /// 8구단 80경기 Rookie League의 첫 검증용 시즌 계수를 만든다.
        /// </summary>
        public static CareerSeasonBalance CreateDefault()
        {
            // 평소 기용은 실력 경쟁으로 정하되, 2군이 없는 MVP에서 신인이 평가조차 받지 못하는 고착은
            // 역할별 1/2/3 로테이션 주기의 최소 평가 기회로 막는다. 무작위 폭은 비슷한 선수의 고정을 막는다.
            return new CareerSeasonBalance(
                regularSeasonGamesPerTeam: 80,
                startingRotationSize: 5,
                reliefStartInning: 7,
                managerDecisionVariance: 7d,
                startingCompetitionBonus: 9,
                rosterCompetitionBonus: 4,
                benchCompetitionBonus: -1,
                reliefOpportunityMargin: 4,
                benchSubstitutionOpportunityProbability: 0.35d,
                benchSubstitutionEarliestInning: 7,
                benchSubstitutionMaximumScoreDifference: 3,
                startingCompetitionEvaluationInterval: 1,
                rosterCompetitionEvaluationInterval: 2,
                benchCompetitionEvaluationInterval: 3,
                evaluationOpportunityMinimumCondition: 70,
                initialCondition: 90,
                initialManagerEvaluation: 50,
                playingConditionCost: 2,
                restingConditionRecovery: 1,
                minimumCondition: 55,
                maximumManagerEvaluationChange: 3,
                // 45시즌 회귀 표본에서 StartingCompetition 신인의 선발률을 98.5%에서 86.1%로 낮춰
                // 낮은 컨디션일 때 경쟁자에게 실제 휴식 기회가 돌아가도록 한 값이다.
                conditionDecisionWeight: 0.30d,
                managerEvaluationDecisionWeight: 0.10d,
                productiveBattingHits: 2,
                excellentBattingHits: 3,
                poorBattingAtBats: 4,
                qualityPitchingMaximumEarnedRuns: 1,
                poorPitchingMinimumEarnedRuns: 4,
                positiveEvaluationChange: 1,
                excellentEvaluationChange: 2,
                poorEvaluationChange: -1,
                veryPoorEvaluationChange: -2,
                seasonOpeningMonth: 4,
                seasonOpeningDay: 1,
                gamesBetweenRestDays: 6);
        }
    }
}

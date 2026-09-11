namespace Baseball.Core.Balance
{
    /// <summary>
    /// 직접 플레이 입력을 투구·컨택 데이터로 바꾸는 조정 가능 계수를 정의한다.
    /// </summary>
    public sealed class MiniGameBalance
    {
        public MiniGameBalance(
            double targetHorizontalLimit,
            double targetVerticalLimit,
            double baseCommandDeviation,
            double controlDeviationWeight,
            double minimumCommandDeviation,
            double maximumCommandDeviation,
            double baseBatRadiusX,
            double baseBatRadiusY,
            double contactRadiusWeight,
            double perfectTimingMilliseconds,
            double validTimingMilliseconds,
            double foulTimingMilliseconds,
            double contactTimingWeight,
            double swingImpactLeadMilliseconds,
            double contactIntentRadiusMultiplier,
            double powerIntentRadiusMultiplier,
            double contactIntentExitVelocityPenalty,
            double powerIntentExitVelocityBonus,
            double baseExitVelocity,
            double powerExitVelocityWeight,
            double outOfZoneQualityPenalty,
            double aiWastePitchProbability,
            double aiTwoStrikeWasteProbability,
            double aiThreeBallChallengeProbability,
            double aiWastePitchDistance,
            double aiInsideWasteProbability,
            double aiLocationErrorScale,
            double aiTimingErrorMilliseconds,
            double contactQualityBase,
            double launchAngleBaseDegrees,
            double launchAngleLocationScale,
            double homeRunMinimumExitVelocity,
            double homeRunMinimumLaunchAngle,
            double homeRunMaximumLaunchAngle,
            double homeRunProbabilityMultiplier,
            double repeatRecognitionBase,
            double repeatRecognitionMentalWeight,
            double repeatChaseReduction,
            double repeatExecutionErrorReduction,
            double aiThreeBallTargetHorizontal = 0.96d,
            double aiPitchQualityDifficultyWeight = .003d,
            double contactPitchQualityWeight = .75d, double contactBatterQualityWeight = .65d,
            // 제구가 높은 역사 로스터의 사구 부족을 줄이되 중립 전력의 과다 사구를 피한 대량 검증값이다.
            // 근거: docs/reports/plate-discipline-20260911.md. 팀별 실적은 계수에 입력하지 않는다.
            double hitByPitchMinimumInsideLocation = 1.18d,
            double hitByPitchMaximumHeight = 1.05d,
            double hitByPitchContactProbability = .18d,
            double aiMentalChaseWeight = .0045d)
        {
            if (!(aiMentalChaseWeight >= 0d) || aiMentalChaseWeight > .05d)
                throw new System.ArgumentOutOfRangeException(nameof(aiMentalChaseWeight));
            AiMentalChaseWeight = aiMentalChaseWeight;
            if (!(hitByPitchMinimumInsideLocation > 1d) || hitByPitchMinimumInsideLocation > 1.8d)
                throw new System.ArgumentOutOfRangeException(nameof(hitByPitchMinimumInsideLocation));
            if (!(hitByPitchMaximumHeight > 0d) || hitByPitchMaximumHeight > 1.7d)
                throw new System.ArgumentOutOfRangeException(nameof(hitByPitchMaximumHeight));
            ValidateProbability(hitByPitchContactProbability, nameof(hitByPitchContactProbability));
            HitByPitchMinimumInsideLocation = hitByPitchMinimumInsideLocation;
            HitByPitchMaximumHeight = hitByPitchMaximumHeight;
            HitByPitchContactProbability = hitByPitchContactProbability;
            ValidateProbability(aiThreeBallChallengeProbability, nameof(aiThreeBallChallengeProbability));
            ValidateProbability(aiWastePitchProbability, nameof(aiWastePitchProbability));
            ValidateProbability(aiTwoStrikeWasteProbability, nameof(aiTwoStrikeWasteProbability));
            ValidateProbability(aiInsideWasteProbability, nameof(aiInsideWasteProbability));
            ValidateProbability(aiThreeBallTargetHorizontal, nameof(aiThreeBallTargetHorizontal));
            if (!(minimumCommandDeviation > 0d) || !(maximumCommandDeviation >= minimumCommandDeviation) ||
                !(baseBatRadiusX > 0d) || !(baseBatRadiusY > 0d) || !(aiTimingErrorMilliseconds > 0d))
                throw new System.ArgumentException("제구·타격 오차와 접촉 범위는 양수여야 합니다.");
            TargetHorizontalLimit = targetHorizontalLimit;
            TargetVerticalLimit = targetVerticalLimit;
            BaseCommandDeviation = baseCommandDeviation;
            ControlDeviationWeight = controlDeviationWeight;
            MinimumCommandDeviation = minimumCommandDeviation;
            MaximumCommandDeviation = maximumCommandDeviation;
            BaseBatRadiusX = baseBatRadiusX;
            BaseBatRadiusY = baseBatRadiusY;
            ContactRadiusWeight = contactRadiusWeight;
            PerfectTimingMilliseconds = perfectTimingMilliseconds;
            ValidTimingMilliseconds = validTimingMilliseconds;
            FoulTimingMilliseconds = foulTimingMilliseconds;
            ContactTimingWeight = contactTimingWeight;
            SwingImpactLeadMilliseconds = swingImpactLeadMilliseconds;
            ContactIntentRadiusMultiplier = contactIntentRadiusMultiplier;
            PowerIntentRadiusMultiplier = powerIntentRadiusMultiplier;
            ContactIntentExitVelocityPenalty = contactIntentExitVelocityPenalty;
            PowerIntentExitVelocityBonus = powerIntentExitVelocityBonus;
            BaseExitVelocity = baseExitVelocity;
            PowerExitVelocityWeight = powerExitVelocityWeight;
            OutOfZoneQualityPenalty = outOfZoneQualityPenalty;
            AiWastePitchProbability = aiWastePitchProbability;
            AiTwoStrikeWasteProbability = aiTwoStrikeWasteProbability;
            AiThreeBallChallengeProbability = aiThreeBallChallengeProbability;
            AiWastePitchDistance = aiWastePitchDistance;
            AiInsideWasteProbability = aiInsideWasteProbability;
            AiLocationErrorScale = aiLocationErrorScale;
            AiTimingErrorMilliseconds = aiTimingErrorMilliseconds;
            ContactQualityBase = contactQualityBase;
            LaunchAngleBaseDegrees = launchAngleBaseDegrees;
            LaunchAngleLocationScale = launchAngleLocationScale;
            HomeRunMinimumExitVelocity = homeRunMinimumExitVelocity;
            HomeRunMinimumLaunchAngle = homeRunMinimumLaunchAngle;
            HomeRunMaximumLaunchAngle = homeRunMaximumLaunchAngle;
            HomeRunProbabilityMultiplier = homeRunProbabilityMultiplier;
            RepeatRecognitionBase = repeatRecognitionBase;
            RepeatRecognitionMentalWeight = repeatRecognitionMentalWeight;
            RepeatChaseReduction = repeatChaseReduction;
            RepeatExecutionErrorReduction = repeatExecutionErrorReduction;
            AiThreeBallTargetHorizontal = aiThreeBallTargetHorizontal;
            if (!(aiPitchQualityDifficultyWeight > 0d) || double.IsInfinity(aiPitchQualityDifficultyWeight))
                throw new System.ArgumentOutOfRangeException(nameof(aiPitchQualityDifficultyWeight));
            AiPitchQualityDifficultyWeight = aiPitchQualityDifficultyWeight;
            if (!(contactPitchQualityWeight >= 0d) || double.IsInfinity(contactPitchQualityWeight))
                throw new System.ArgumentOutOfRangeException(nameof(contactPitchQualityWeight));
            ContactPitchQualityWeight = contactPitchQualityWeight;
            if (!(contactBatterQualityWeight >= 0d) || double.IsInfinity(contactBatterQualityWeight))
                throw new System.ArgumentOutOfRangeException(nameof(contactBatterQualityWeight));
            ContactBatterQualityWeight = contactBatterQualityWeight;
        }

        public double TargetHorizontalLimit { get; }
        public double TargetVerticalLimit { get; }
        public double BaseCommandDeviation { get; }
        public double ControlDeviationWeight { get; }
        public double MinimumCommandDeviation { get; }
        public double MaximumCommandDeviation { get; }
        public double BaseBatRadiusX { get; }
        public double BaseBatRadiusY { get; }
        public double ContactRadiusWeight { get; }
        public double PerfectTimingMilliseconds { get; }
        public double ValidTimingMilliseconds { get; }
        public double FoulTimingMilliseconds { get; }
        public double ContactTimingWeight { get; }
        public double SwingImpactLeadMilliseconds { get; }
        public double ContactIntentRadiusMultiplier { get; }
        public double PowerIntentRadiusMultiplier { get; }
        public double ContactIntentExitVelocityPenalty { get; }
        public double PowerIntentExitVelocityBonus { get; }
        public double BaseExitVelocity { get; }
        public double PowerExitVelocityWeight { get; }
        public double OutOfZoneQualityPenalty { get; }
        public double AiWastePitchProbability { get; }
        public double AiTwoStrikeWasteProbability { get; }
        public double AiThreeBallChallengeProbability { get; }
        public double AiThreeBallTargetHorizontal { get; }
        public double AiPitchQualityDifficultyWeight { get; }
        public double ContactPitchQualityWeight { get; }
        public double ContactBatterQualityWeight { get; }
        public double AiWastePitchDistance { get; }
        public double AiInsideWasteProbability { get; }
        public double AiLocationErrorScale { get; }
        public double AiTimingErrorMilliseconds { get; }
        public double ContactQualityBase { get; }
        public double LaunchAngleBaseDegrees { get; }
        public double LaunchAngleLocationScale { get; }
        public double HomeRunMinimumExitVelocity { get; }
        public double HomeRunMinimumLaunchAngle { get; }
        public double HomeRunMaximumLaunchAngle { get; }
        public double HomeRunProbabilityMultiplier { get; }
        public double RepeatRecognitionBase { get; }
        public double RepeatRecognitionMentalWeight { get; }
        public double RepeatChaseReduction { get; }
        public double RepeatExecutionErrorReduction { get; }
        /// <summary>몸쪽 위험구 영역과 그 안에서 회피하지 못할 확률을 분리해 사구를 저작한다.</summary>
        public double HitByPitchMinimumInsideLocation { get; }
        public double HitByPitchMaximumHeight { get; }
        public double HitByPitchContactProbability { get; }
        /// <summary>Mental 1점당 AI 타자가 존 밖 공을 쫓는 확률의 감소량이다.</summary>
        // 기본값 .0045는 역사 개인 기록의 팀당 볼넷 3.372를 목표로 검증했다. 팀 승률은 입력하지 않는다.
        public double AiMentalChaseWeight { get; }

        private static void ValidateProbability(double value, string name)
        {
            if (double.IsNaN(value) || value < 0d || value > 1d)
                throw new System.ArgumentOutOfRangeException(name);
        }

        /// <summary>표준 난도의 평균 입력이 자동 진행과 가까워지도록 잡은 최초 검증값을 만든다.</summary>
        public static MiniGameBalance CreateDefault()
        {
            // 위치 범위는 스트라이크 존을 ±1로 정규화한 기획 계약이다.
            // 타이밍 35/80/140ms는 입력 피드백의 의미가 실제 조작 감각과 일치하도록 기획 기준을 그대로 쓴다.
            return new MiniGameBalance(
                targetHorizontalLimit: 1.30d,
                targetVerticalLimit: 1.25d,
                baseCommandDeviation: 0.16d,
                controlDeviationWeight: 0.0021d,
                minimumCommandDeviation: 0.055d,
                maximumCommandDeviation: 0.30d,
                baseBatRadiusX: 0.28d,
                baseBatRadiusY: 0.19d,
                contactRadiusWeight: 0.0022d,
                perfectTimingMilliseconds: 35d,
                validTimingMilliseconds: 80d,
                foulTimingMilliseconds: 140d,
                contactTimingWeight: 0.45d,
                swingImpactLeadMilliseconds: 42d,
                contactIntentRadiusMultiplier: 1.18d,
                powerIntentRadiusMultiplier: 0.82d,
                contactIntentExitVelocityPenalty: 4d,
                powerIntentExitVelocityBonus: 6d,
                baseExitVelocity: 94d,
                powerExitVelocityWeight: 0.25d,
                outOfZoneQualityPenalty: 18d,
                aiWastePitchProbability: 0.48d,
                aiTwoStrikeWasteProbability: 0.45d,
                aiThreeBallChallengeProbability: 0.70d,
                aiWastePitchDistance: 1.14d,
                aiInsideWasteProbability: 0.40d,
                aiLocationErrorScale: 0.92d,
                aiTimingErrorMilliseconds: 63d,
                contactQualityBase: 16.75d,
                launchAngleBaseDegrees: 10d,
                launchAngleLocationScale: 145d,
                homeRunMinimumExitVelocity: 90d,
                homeRunMinimumLaunchAngle: 15d,
                homeRunMaximumLaunchAngle: 42d,
                homeRunProbabilityMultiplier: 2.60d,
                repeatRecognitionBase: 0.12d,
                repeatRecognitionMentalWeight: 0.002d,
                repeatChaseReduction: 0.18d,
                repeatExecutionErrorReduction: 0.30d);
        }
    }
}

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 역할별 투구 용량과 피로 구간의 실효 능력치 하락 곡선을 보관한다.
    /// </summary>
    public readonly struct PitcherFatigueBalance
    {
        public PitcherFatigueBalance(
            double starterBaseCapacity,
            double starterStaminaWeight,
            double relieverBaseCapacity,
            double relieverStaminaWeight,
            double longReliefMultiplier,
            double closerMultiplier,
            double penaltyStartRatio,
            double overloadRatio,
            double maximumVelocityPenalty,
            double maximumStuffPenalty,
            double maximumBreakingPenalty,
            double maximumControlPenalty)
        {
            StarterBaseCapacity = starterBaseCapacity;
            StarterStaminaWeight = starterStaminaWeight;
            RelieverBaseCapacity = relieverBaseCapacity;
            RelieverStaminaWeight = relieverStaminaWeight;
            LongReliefMultiplier = longReliefMultiplier;
            CloserMultiplier = closerMultiplier;
            PenaltyStartRatio = penaltyStartRatio;
            OverloadRatio = overloadRatio;
            MaximumVelocityPenalty = maximumVelocityPenalty;
            MaximumStuffPenalty = maximumStuffPenalty;
            MaximumBreakingPenalty = maximumBreakingPenalty;
            MaximumControlPenalty = maximumControlPenalty;
        }

        public double StarterBaseCapacity { get; }
        public double StarterStaminaWeight { get; }
        public double RelieverBaseCapacity { get; }
        public double RelieverStaminaWeight { get; }
        public double LongReliefMultiplier { get; }
        public double CloserMultiplier { get; }
        public double PenaltyStartRatio { get; }
        public double OverloadRatio { get; }
        public double MaximumVelocityPenalty { get; }
        public double MaximumStuffPenalty { get; }
        public double MaximumBreakingPenalty { get; }
        public double MaximumControlPenalty { get; }
    }

    /// <summary>
    /// 한 이닝 안의 단기 압박 누적과 Mental 회복 보정을 정의한다.
    /// </summary>
    public readonly struct PitcherStressBalance
    {
        public PitcherStressBalance(
            double walkStress,
            double hitStress,
            double extraBaseHitStress,
            double runStress,
            double scoringPositionStress,
            double outRecovery,
            double inningRecovery,
            double maximumControlPenalty,
            double mentalMitigationWeight)
        {
            WalkStress = walkStress;
            HitStress = hitStress;
            ExtraBaseHitStress = extraBaseHitStress;
            RunStress = runStress;
            ScoringPositionStress = scoringPositionStress;
            OutRecovery = outRecovery;
            InningRecovery = inningRecovery;
            MaximumControlPenalty = maximumControlPenalty;
            MentalMitigationWeight = mentalMitigationWeight;
        }

        public double WalkStress { get; }
        public double HitStress { get; }
        public double ExtraBaseHitStress { get; }
        public double RunStress { get; }
        public double ScoringPositionStress { get; }
        public double OutRecovery { get; }
        public double InningRecovery { get; }
        public double MaximumControlPenalty { get; }
        public double MentalMitigationWeight { get; }
    }

    /// <summary>
    /// 같은 타자와 반복 대면할 때 생기는 적응 보정과 투수 완화 비율을 정의한다.
    /// </summary>
    public readonly struct TimesThroughOrderBalance
    {
        public TimesThroughOrderBalance(
            double secondContactBonus,
            double thirdContactBonus,
            double fourthContactBonus,
            double secondHardHitBonus,
            double thirdHardHitBonus,
            double fourthHardHitBonus,
            double maximumPitcherMitigation)
        {
            SecondContactBonus = secondContactBonus;
            ThirdContactBonus = thirdContactBonus;
            FourthContactBonus = fourthContactBonus;
            SecondHardHitBonus = secondHardHitBonus;
            ThirdHardHitBonus = thirdHardHitBonus;
            FourthHardHitBonus = fourthHardHitBonus;
            MaximumPitcherMitigation = maximumPitcherMitigation;
        }

        public double SecondContactBonus { get; }
        public double ThirdContactBonus { get; }
        public double FourthContactBonus { get; }
        public double SecondHardHitBonus { get; }
        public double ThirdHardHitBonus { get; }
        public double FourthHardHitBonus { get; }
        public double MaximumPitcherMitigation { get; }
    }

    /// <summary>
    /// 투수 교체 점수와 불펜 후보 평가의 공통 임계값을 정의한다.
    /// </summary>
    public readonly struct BullpenManagementBalance
    {
        public BullpenManagementBalance(
            double pullThreshold,
            double maximumFatigueRisk,
            double maximumCurrentDanger,
            double maximumTimesThroughOrderRisk,
            double maximumPerformanceDamage,
            double maximumLeverageMismatch,
            double maximumStarterTrust,
            double maximumBullpenConservation,
            double recentLoadDayTwoWeight,
            double recentLoadDayThreeWeight,
            double unavailableRecentLoad,
            int lowLeverageCloserPenalty)
        {
            PullThreshold = pullThreshold;
            MaximumFatigueRisk = maximumFatigueRisk;
            MaximumCurrentDanger = maximumCurrentDanger;
            MaximumTimesThroughOrderRisk = maximumTimesThroughOrderRisk;
            MaximumPerformanceDamage = maximumPerformanceDamage;
            MaximumLeverageMismatch = maximumLeverageMismatch;
            MaximumStarterTrust = maximumStarterTrust;
            MaximumBullpenConservation = maximumBullpenConservation;
            RecentLoadDayTwoWeight = recentLoadDayTwoWeight;
            RecentLoadDayThreeWeight = recentLoadDayThreeWeight;
            UnavailableRecentLoad = unavailableRecentLoad;
            LowLeverageCloserPenalty = lowLeverageCloserPenalty;
        }

        public double PullThreshold { get; }
        public double MaximumFatigueRisk { get; }
        public double MaximumCurrentDanger { get; }
        public double MaximumTimesThroughOrderRisk { get; }
        public double MaximumPerformanceDamage { get; }
        public double MaximumLeverageMismatch { get; }
        public double MaximumStarterTrust { get; }
        public double MaximumBullpenConservation { get; }
        public double RecentLoadDayTwoWeight { get; }
        public double RecentLoadDayThreeWeight { get; }
        public double UnavailableRecentLoad { get; }
        public int LowLeverageCloserPenalty { get; }
    }

    /// <summary>
    /// 범주형 타구의 도달·포구·송구 판정 계수를 정의한다.
    /// </summary>
    public readonly struct DetailedFieldingBalance
    {
        public DetailedFieldingBalance(
            double rangeProbabilityWeight,
            double maximumRangeAdjustment,
            double positionProficiencyWeight,
            double groundBallReachBase,
            double lineDriveReachBase,
            double flyBallReachBase,
            double popUpReachBase,
            double qualityReachPenalty,
            double normalGroundHandleFailure,
            double normalFlyHandleFailure,
            double normalThrowFailure,
            double difficultThrowFailure,
            double handsErrorWeight)
        {
            RangeProbabilityWeight = rangeProbabilityWeight;
            MaximumRangeAdjustment = maximumRangeAdjustment;
            PositionProficiencyWeight = positionProficiencyWeight;
            GroundBallReachBase = groundBallReachBase;
            LineDriveReachBase = lineDriveReachBase;
            FlyBallReachBase = flyBallReachBase;
            PopUpReachBase = popUpReachBase;
            QualityReachPenalty = qualityReachPenalty;
            NormalGroundHandleFailure = normalGroundHandleFailure;
            NormalFlyHandleFailure = normalFlyHandleFailure;
            NormalThrowFailure = normalThrowFailure;
            DifficultThrowFailure = difficultThrowFailure;
            HandsErrorWeight = handsErrorWeight;
        }

        public double RangeProbabilityWeight { get; }
        public double MaximumRangeAdjustment { get; }
        public double PositionProficiencyWeight { get; }
        public double GroundBallReachBase { get; }
        public double LineDriveReachBase { get; }
        public double FlyBallReachBase { get; }
        public double PopUpReachBase { get; }
        public double QualityReachPenalty { get; }
        public double NormalGroundHandleFailure { get; }
        public double NormalFlyHandleFailure { get; }
        public double NormalThrowFailure { get; }
        public double DifficultThrowFailure { get; }
        public double HandsErrorWeight { get; }
    }

    /// <summary>
    /// 도루·번트·추가 진루와 기대값 판단에 쓰이는 계수를 정의한다.
    /// </summary>
    public readonly struct TacticalMatchBalance
    {
        public TacticalMatchBalance(
            double stealBaseSuccess,
            double stealSpeedWeight,
            double stealMentalWeight,
            double catcherArmWeight,
            double pitcherHoldWeight,
            double minimumStealSuccess,
            double maximumStealSuccess,
            double fairBuntBase,
            double buntAbilityWeight,
            double buntMentalWeight,
            double stealAttemptUtilityThreshold,
            double buntUtilityThreshold,
            double intentionalWalkUtilityThreshold)
        {
            StealBaseSuccess = stealBaseSuccess;
            StealSpeedWeight = stealSpeedWeight;
            StealMentalWeight = stealMentalWeight;
            CatcherArmWeight = catcherArmWeight;
            PitcherHoldWeight = pitcherHoldWeight;
            MinimumStealSuccess = minimumStealSuccess;
            MaximumStealSuccess = maximumStealSuccess;
            FairBuntBase = fairBuntBase;
            BuntAbilityWeight = buntAbilityWeight;
            BuntMentalWeight = buntMentalWeight;
            StealAttemptUtilityThreshold = stealAttemptUtilityThreshold;
            BuntUtilityThreshold = buntUtilityThreshold;
            IntentionalWalkUtilityThreshold = intentionalWalkUtilityThreshold;
        }

        public double StealBaseSuccess { get; }
        public double StealSpeedWeight { get; }
        public double StealMentalWeight { get; }
        public double CatcherArmWeight { get; }
        public double PitcherHoldWeight { get; }
        public double MinimumStealSuccess { get; }
        public double MaximumStealSuccess { get; }
        public double FairBuntBase { get; }
        public double BuntAbilityWeight { get; }
        public double BuntMentalWeight { get; }
        public double StealAttemptUtilityThreshold { get; }
        public double BuntUtilityThreshold { get; }
        public double IntentionalWalkUtilityThreshold { get; }
    }

    /// <summary>
    /// 세부 경기 V2에서 사용하는 하위 밸런스 표를 한 객체로 묶는다.
    /// </summary>
    public sealed class MatchBalanceTable
    {
        public MatchBalanceTable(
            PitcherFatigueBalance pitcherFatigue,
            PitcherStressBalance pitcherStress,
            TimesThroughOrderBalance timesThroughOrder,
            BullpenManagementBalance bullpenManagement,
            DetailedFieldingBalance fielding,
            TacticalMatchBalance tactical)
        {
            PitcherFatigue = pitcherFatigue;
            PitcherStress = pitcherStress;
            TimesThroughOrder = timesThroughOrder;
            BullpenManagement = bullpenManagement;
            Fielding = fielding;
            Tactical = tactical;
        }

        public PitcherFatigueBalance PitcherFatigue { get; }
        public PitcherStressBalance PitcherStress { get; }
        public TimesThroughOrderBalance TimesThroughOrder { get; }
        public BullpenManagementBalance BullpenManagement { get; }
        public DetailedFieldingBalance Fielding { get; }
        public TacticalMatchBalance Tactical { get; }

        public static MatchBalanceTable CreateDefault()
        {
            return new MatchBalanceTable(
                new PitcherFatigueBalance(
                    65d, 0.55d, 18d, 0.22d, 1.45d, 0.90d, 0.55d, 1.05d,
                    6d, 10d, 8d, 14d),
                new PitcherStressBalance(
                    0.12d, 0.10d, 0.16d, 0.18d, 0.06d, 0.07d, 0.72d, 5d, 0.006d),
                new TimesThroughOrderBalance(
                    1d, 3d, 5d, 0.006d, 0.016d, 0.026d, 0.40d),
                // 40점은 5~6회 피로와 세 번째 타순 대면이 함께 쌓일 때 교체가 발생하도록 맞춘 값이다.
                // 45시즌 회귀 표본에서 SP 6.3 IP/App, RP 1.6 IP/App를 만든다.
                new BullpenManagementBalance(
                    40d, 40d, 15d, 12d, 20d, 15d, 10d, 15d,
                    0.55d, 0.25d, 55d, 20),
                new DetailedFieldingBalance(
                    0.0008d, 0.04d, 0.0007d, 0.735d, 0.42d, 0.79d, 0.96d,
                    0.0032d, 0.015d, 0.008d, 0.007d, 0.020d, 0.012d),
                new TacticalMatchBalance(
                    0.68d, 0.004d, 0.0015d, 0.003d, 0.0015d, 0.35d, 0.92d,
                    0.58d, 0.004d, 0.001d, -0.025d, 0.005d, 0.08d));
        }
    }
}

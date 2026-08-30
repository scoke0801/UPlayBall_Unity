using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 나이에 따른 훈련 효율을 생애 구간별로 제공한다.
    /// </summary>
    public readonly struct AgeGrowthCurveTable
    {
        public AgeGrowthCurveTable(double growth, double prime, double skilled, double decline, double lateCareer)
        {
            Growth = Validate(growth, nameof(growth));
            Prime = Validate(prime, nameof(prime));
            Skilled = Validate(skilled, nameof(skilled));
            Decline = Validate(decline, nameof(decline));
            LateCareer = Validate(lateCareer, nameof(lateCareer));
        }

        public double Growth { get; }
        public double Prime { get; }
        public double Skilled { get; }
        public double Decline { get; }
        public double LateCareer { get; }

        public double GetMultiplier(int age)
        {
            if (age <= 22) return Growth;
            if (age <= 27) return Prime;
            if (age <= 31) return Skilled;
            if (age <= 34) return Decline;
            return LateCareer;
        }

        private static double Validate(double value, string parameterName)
        {
            if (value < 0d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public readonly struct PotentialGapMultiplierTable
    {
        public PotentialGapMultiplierTable(
            double largeGap,
            double normalGap,
            double smallGap,
            double nearLimit,
            double overPotential)
        {
            LargeGap = largeGap;
            NormalGap = normalGap;
            SmallGap = smallGap;
            NearLimit = nearLimit;
            OverPotential = overPotential;
        }

        public double LargeGap { get; }
        public double NormalGap { get; }
        public double SmallGap { get; }
        public double NearLimit { get; }
        public double OverPotential { get; }

        public double GetMultiplier(int baseAbility, int potential)
        {
            int gap = potential - baseAbility;
            if (gap >= 15) return LargeGap;
            if (gap >= 8) return NormalGap;
            if (gap >= 3) return SmallGap;
            if (gap >= 0) return NearLimit;
            if (gap > -3) return OverPotential;
            return 0d;
        }
    }

    public readonly struct WorkEthicMultiplierTable
    {
        public WorkEthicMultiplierTable(double inconsistent, double normal, double diligent, double veryDiligent)
        {
            Inconsistent = inconsistent;
            Normal = normal;
            Diligent = diligent;
            VeryDiligent = veryDiligent;
        }

        public double Inconsistent { get; }
        public double Normal { get; }
        public double Diligent { get; }
        public double VeryDiligent { get; }

        public double GetMultiplier(WorkEthicGrade grade)
        {
            return grade switch
            {
                WorkEthicGrade.Inconsistent => Inconsistent,
                WorkEthicGrade.Normal => Normal,
                WorkEthicGrade.Diligent => Diligent,
                WorkEthicGrade.VeryDiligent => VeryDiligent,
                _ => throw new ArgumentOutOfRangeException(nameof(grade))
            };
        }
    }

    public readonly struct TrainingFitMultiplierTable
    {
        public TrainingFitMultiplierTable(double low, double normal, double high, double veryHigh)
        {
            Low = low;
            Normal = normal;
            High = high;
            VeryHigh = veryHigh;
        }

        public double Low { get; }
        public double Normal { get; }
        public double High { get; }
        public double VeryHigh { get; }

        public double GetMultiplier(TrainingFitGrade grade)
        {
            return grade switch
            {
                TrainingFitGrade.Low => Low,
                TrainingFitGrade.Normal => Normal,
                TrainingFitGrade.High => High,
                TrainingFitGrade.VeryHigh => VeryHigh,
                _ => throw new ArgumentOutOfRangeException(nameof(grade))
            };
        }
    }

    public readonly struct ConditionMultiplierTable
    {
        public ConditionMultiplierTable(int normalMinimum, int reducedMinimum, int warningMinimum, double normal, double reduced, double warning)
        {
            if (normalMinimum <= reducedMinimum || reducedMinimum <= warningMinimum || warningMinimum < 0)
                throw new ArgumentOutOfRangeException(nameof(normalMinimum));
            NormalMinimum = normalMinimum;
            ReducedMinimum = reducedMinimum;
            WarningMinimum = warningMinimum;
            Normal = normal;
            Reduced = reduced;
            Warning = warning;
        }

        public int NormalMinimum { get; }
        public int ReducedMinimum { get; }
        public int WarningMinimum { get; }
        public double Normal { get; }
        public double Reduced { get; }
        public double Warning { get; }

        public double GetMultiplier(int condition)
        {
            if (condition >= NormalMinimum) return Normal;
            if (condition >= ReducedMinimum) return Reduced;
            if (condition >= WarningMinimum) return Warning;
            return 0d;
        }
    }

    public readonly struct RepetitionMultiplierTable
    {
        public RepetitionMultiplierTable(
            double first,
            double second,
            double thirdOrMore,
            double secondConsecutiveStudy,
            double thirdConsecutiveStudy)
        {
            First = first;
            Second = second;
            ThirdOrMore = thirdOrMore;
            SecondConsecutiveStudy = secondConsecutiveStudy;
            ThirdConsecutiveStudy = thirdConsecutiveStudy;
        }

        public double First { get; }
        public double Second { get; }
        public double ThirdOrMore { get; }
        public double SecondConsecutiveStudy { get; }
        public double ThirdConsecutiveStudy { get; }

        public double GetMultiplier(int priorSelections, bool isStudy)
        {
            if (priorSelections < 0)
                throw new ArgumentOutOfRangeException(nameof(priorSelections));
            if (isStudy)
            {
                if (priorSelections == 0) return First;
                if (priorSelections == 1) return SecondConsecutiveStudy;
                return ThirdConsecutiveStudy;
            }
            if (priorSelections == 0) return First;
            if (priorSelections == 1) return Second;
            return ThirdOrMore;
        }
    }

    public readonly struct NaturalGrowthBalanceTable
    {
        public NaturalGrowthBalanceTable(
            double growthAgeBudget,
            double primeAgeBudget,
            double skilledAgeBudget,
            double noUsage,
            double limitedUsage,
            double normalUsage,
            double excessiveUsage)
        {
            GrowthAgeBudget = growthAgeBudget;
            PrimeAgeBudget = primeAgeBudget;
            SkilledAgeBudget = skilledAgeBudget;
            NoUsage = noUsage;
            LimitedUsage = limitedUsage;
            NormalUsage = normalUsage;
            ExcessiveUsage = excessiveUsage;
        }

        public double GrowthAgeBudget { get; }
        public double PrimeAgeBudget { get; }
        public double SkilledAgeBudget { get; }
        public double NoUsage { get; }
        public double LimitedUsage { get; }
        public double NormalUsage { get; }
        public double ExcessiveUsage { get; }

        public double GetAgeBudget(int age)
        {
            if (age <= 22) return GrowthAgeBudget;
            if (age <= 27) return PrimeAgeBudget;
            if (age <= 31) return SkilledAgeBudget;
            return 0d;
        }

        public double GetUsageMultiplier(double usageRatio)
        {
            if (usageRatio < 0d)
                throw new ArgumentOutOfRangeException(nameof(usageRatio));
            if (usageRatio < 0.15d) return NoUsage;
            if (usageRatio < 0.65d) return LimitedUsage;
            if (usageRatio <= 1.20d) return NormalUsage;
            return ExcessiveUsage;
        }
    }

    public readonly struct AgingDeclineBalanceTable
    {
        public AgingDeclineBalanceTable(
            double skilledPhysical,
            double declinePhysical,
            double declineTechnical,
            double latePhysical,
            double lateTechnical,
            double lateMental)
        {
            SkilledPhysical = skilledPhysical;
            DeclinePhysical = declinePhysical;
            DeclineTechnical = declineTechnical;
            LatePhysical = latePhysical;
            LateTechnical = lateTechnical;
            LateMental = lateMental;
        }

        public double SkilledPhysical { get; }
        public double DeclinePhysical { get; }
        public double DeclineTechnical { get; }
        public double LatePhysical { get; }
        public double LateTechnical { get; }
        public double LateMental { get; }

        public double GetBudget(int age, AbilityFamily family)
        {
            if (age <= 27) return 0d;
            if (age <= 31) return family == AbilityFamily.Physical ? SkilledPhysical : 0d;
            if (age <= 34)
            {
                if (family == AbilityFamily.Physical) return DeclinePhysical;
                return family == AbilityFamily.Technical ? DeclineTechnical : 0d;
            }
            if (family == AbilityFamily.Physical) return LatePhysical;
            if (family == AbilityFamily.Technical) return LateTechnical;
            return LateMental;
        }
    }

    /// <summary>
    /// 한 구매 등급의 최소 보장·가격·공개 확률·오프시즌 구매 제한을 보관한다.
    /// </summary>
    public readonly struct SkillGachaOfferBalance
    {
        public SkillGachaOfferBalance(
            SkillGachaPurchaseTier tier,
            SkillBlockRarity minimumRarity,
            long price,
            int maxPurchasesPerOffseason,
            double normalProbability,
            double rareProbability,
            double eliteProbability,
            double uniqueProbability,
            double legendaryProbability)
        {
            if (price <= 0L)
                throw new ArgumentOutOfRangeException(nameof(price));
            if ((int)tier != (int)minimumRarity)
                throw new ArgumentException("구매 상품과 최소 보장 등급은 같아야 합니다.");
            if (maxPurchasesPerOffseason < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPurchasesPerOffseason));
            if (normalProbability < 0d || rareProbability < 0d || eliteProbability < 0d ||
                uniqueProbability < 0d || legendaryProbability < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(normalProbability));
            }
            if (Math.Abs(
                    normalProbability + rareProbability + eliteProbability +
                    uniqueProbability + legendaryProbability - 1d) > 0.000001d)
            {
                throw new ArgumentException("등급 확률 합은 1이어야 합니다.");
            }

            double[] probabilities =
            {
                normalProbability, rareProbability, eliteProbability,
                uniqueProbability, legendaryProbability
            };
            for (int rarity = 0; rarity < (int)minimumRarity; rarity++)
            {
                if (probabilities[rarity] > 0d)
                    throw new ArgumentException("선택 등급보다 낮은 블록 확률은 0이어야 합니다.");
            }

            Tier = tier;
            MinimumRarity = minimumRarity;
            Price = price;
            MaxPurchasesPerOffseason = maxPurchasesPerOffseason;
            NormalProbability = normalProbability;
            RareProbability = rareProbability;
            EliteProbability = eliteProbability;
            UniqueProbability = uniqueProbability;
            LegendaryProbability = legendaryProbability;
        }

        public SkillGachaPurchaseTier Tier { get; }
        public SkillBlockRarity MinimumRarity { get; }
        public long Price { get; }
        public int MaxPurchasesPerOffseason { get; }
        public double NormalProbability { get; }
        public double RareProbability { get; }
        public double EliteProbability { get; }
        public double UniqueProbability { get; }
        public double LegendaryProbability { get; }
        public bool SupportsFivePull => MaxPurchasesPerOffseason == 0 || MaxPurchasesPerOffseason >= 5;

        public double GetProbability(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => NormalProbability,
                SkillBlockRarity.Rare => RareProbability,
                SkillBlockRarity.Elite => EliteProbability,
                SkillBlockRarity.Unique => UniqueProbability,
                SkillBlockRarity.Legendary => LegendaryProbability,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity))
            };
        }
    }

    public readonly struct SkillGachaBalanceTable
    {
        public SkillGachaBalanceTable(
            SkillGachaOfferBalance normal,
            SkillGachaOfferBalance rare,
            SkillGachaOfferBalance elite,
            SkillGachaOfferBalance unique,
            SkillGachaOfferBalance legendary,
            double fivePullDiscountRate,
            int elitePity,
            int uniquePity,
            int legendaryPity,
            int legendaryMinimumCareerAwards,
            bool highTierPurchasesRequireOffseason)
        {
            if (normal.Tier != SkillGachaPurchaseTier.Normal ||
                rare.Tier != SkillGachaPurchaseTier.Rare ||
                elite.Tier != SkillGachaPurchaseTier.Elite ||
                unique.Tier != SkillGachaPurchaseTier.Unique ||
                legendary.Tier != SkillGachaPurchaseTier.Legendary)
            {
                throw new ArgumentException("구매 등급별 확률표가 올바른 Tier에 연결되어야 합니다.");
            }
            if (normal.Price >= rare.Price || rare.Price >= elite.Price ||
                elite.Price >= unique.Price || unique.Price >= legendary.Price)
                throw new ArgumentOutOfRangeException(nameof(legendary));
            if (fivePullDiscountRate < 0d || fivePullDiscountRate >= 1d)
                throw new ArgumentOutOfRangeException(nameof(fivePullDiscountRate));
            if (elitePity <= 0 || uniquePity <= elitePity || legendaryPity <= uniquePity)
                throw new ArgumentOutOfRangeException(nameof(elitePity));
            if (legendaryMinimumCareerAwards < 0)
                throw new ArgumentOutOfRangeException(nameof(legendaryMinimumCareerAwards));

            Normal = normal;
            Rare = rare;
            Elite = elite;
            Unique = unique;
            Legendary = legendary;
            FivePullDiscountRate = fivePullDiscountRate;
            ElitePity = elitePity;
            UniquePity = uniquePity;
            LegendaryPity = legendaryPity;
            LegendaryMinimumCareerAwards = legendaryMinimumCareerAwards;
            HighTierPurchasesRequireOffseason = highTierPurchasesRequireOffseason;
        }

        public SkillGachaOfferBalance Normal { get; }
        public SkillGachaOfferBalance Rare { get; }
        public SkillGachaOfferBalance Elite { get; }
        public SkillGachaOfferBalance Unique { get; }
        public SkillGachaOfferBalance Legendary { get; }
        public double FivePullDiscountRate { get; }
        public long SinglePrice => Normal.Price;
        public long RarePrice => Rare.Price;
        public long ElitePrice => Elite.Price;
        public long UniquePrice => Unique.Price;
        public long LegendaryPrice => Legendary.Price;
        public int ElitePity { get; }
        public int UniquePity { get; }
        public int LegendaryPity { get; }
        public int LegendaryMinimumCareerAwards { get; }
        public bool HighTierPurchasesRequireOffseason { get; }

        public long GetPrice(SkillGachaPurchaseTier tier)
        {
            return GetOffer(tier).Price;
        }

        public double GetProbability(SkillGachaPurchaseTier tier, SkillBlockRarity rarity)
        {
            return GetOffer(tier).GetProbability(rarity);
        }

        public SkillGachaOfferBalance GetOffer(SkillGachaPurchaseTier tier)
        {
            return tier switch
            {
                SkillGachaPurchaseTier.Normal => Normal,
                SkillGachaPurchaseTier.Rare => Rare,
                SkillGachaPurchaseTier.Elite => Elite,
                SkillGachaPurchaseTier.Unique => Unique,
                SkillGachaPurchaseTier.Legendary => Legendary,
                _ => throw new ArgumentOutOfRangeException(nameof(tier))
            };
        }

        public long GetFivePullPrice(SkillGachaPurchaseTier tier)
        {
            double discounted = GetPrice(tier) * 5d * (1d - FivePullDiscountRate);
            const long RoundingUnit = 100_000L;
            return (long)Math.Round(
                discounted / RoundingUnit,
                MidpointRounding.AwayFromZero) * RoundingUnit;
        }
    }

    /// <summary>
    /// 개인 훈련 강도가 기간·비용·성장력·컨디션·위험을 어떻게 교환하는지 정의한다.
    /// </summary>
    public readonly struct TrainingIntensityRule
    {
        public TrainingIntensityRule(
            int durationAdjustment,
            double moneyMultiplier,
            double programPowerMultiplier,
            double conditionChangeMultiplier,
            double injuryRiskMultiplier,
            int minimumConditionAdjustment,
            int maxTotalGainAdjustment,
            int maxGainPerAbilityAdjustment)
        {
            if (moneyMultiplier <= 0d || programPowerMultiplier <= 0d ||
                conditionChangeMultiplier <= 0d || injuryRiskMultiplier < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(moneyMultiplier));
            }

            DurationAdjustment = durationAdjustment;
            MoneyMultiplier = moneyMultiplier;
            ProgramPowerMultiplier = programPowerMultiplier;
            ConditionChangeMultiplier = conditionChangeMultiplier;
            InjuryRiskMultiplier = injuryRiskMultiplier;
            MinimumConditionAdjustment = minimumConditionAdjustment;
            MaxTotalGainAdjustment = maxTotalGainAdjustment;
            MaxGainPerAbilityAdjustment = maxGainPerAbilityAdjustment;
        }

        public int DurationAdjustment { get; }
        public double MoneyMultiplier { get; }
        public double ProgramPowerMultiplier { get; }
        public double ConditionChangeMultiplier { get; }
        public double InjuryRiskMultiplier { get; }
        public int MinimumConditionAdjustment { get; }
        public int MaxTotalGainAdjustment { get; }
        public int MaxGainPerAbilityAdjustment { get; }

        public TrainingProgramDefinition Apply(
            TrainingProgramDefinition program,
            TrainingIntensity intensity)
        {
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (!program.SupportsIntensity && intensity != TrainingIntensity.Standard)
                throw new InvalidOperationException("개인 훈련만 강도를 조절할 수 있습니다.");

            int duration = Clamp(program.DurationWeeks + DurationAdjustment, 1, 12);
            long moneyCost = RoundMoney(program.MoneyCost * MoneyMultiplier);
            int minimumCondition = Clamp(
                program.MinimumCondition + MinimumConditionAdjustment,
                0,
                100);
            double injuryRisk = Math.Min(1d, program.InjuryRisk * InjuryRiskMultiplier);
            int maxTotalGain = Math.Max(0, program.MaxTotalGain + MaxTotalGainAdjustment);
            int maxGainPerAbility = Math.Max(
                0,
                program.MaxGainPerAbility + MaxGainPerAbilityAdjustment);
            int minimumGuaranteedGain = Math.Min(program.MinimumGuaranteedGain, maxTotalGain);
            int conditionChange = (int)Math.Round(
                program.ConditionChange * ConditionChangeMultiplier,
                MidpointRounding.AwayFromZero);

            return new TrainingProgramDefinition(
                program.ProgramId,
                program.ActivityType,
                program.Category,
                program.TargetPlayerType,
                duration,
                moneyCost,
                program.ProgramPower * ProgramPowerMultiplier,
                program.TargetAbilityWeights,
                minimumCondition,
                injuryRisk,
                maxTotalGain,
                maxGainPerAbility,
                conditionChange,
                minimumGuaranteedGain,
                program.PartnerId,
                program.CanRaisePotential,
                intensity,
                program.MinimumAccessTier,
                program.PotentialBreakthroughChanceMultiplier,
                program.MinimumPotentialBreakthroughsWhenCapped);
        }

        private static long RoundMoney(double value)
        {
            const long RoundingUnit = 100_000L;
            return (long)Math.Round(value / RoundingUnit, MidpointRounding.AwayFromZero) * RoundingUnit;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }

    /// <summary>
    /// 표준 훈련을 안정·집중 훈련으로 변환하는 데이터화된 규칙 집합이다.
    /// </summary>
    public sealed class TrainingIntensityBalanceTable
    {
        public TrainingIntensityBalanceTable(
            TrainingIntensityRule safe,
            TrainingIntensityRule standard,
            TrainingIntensityRule intensive)
        {
            Safe = safe;
            Standard = standard;
            Intensive = intensive;
        }

        public TrainingIntensityRule Safe { get; }
        public TrainingIntensityRule Standard { get; }
        public TrainingIntensityRule Intensive { get; }

        public TrainingProgramDefinition Apply(
            TrainingProgramDefinition program,
            TrainingIntensity intensity)
        {
            return intensity switch
            {
                TrainingIntensity.Safe => Safe.Apply(program, intensity),
                TrainingIntensity.Standard => Standard.Apply(program, intensity),
                TrainingIntensity.Intensive => Intensive.Apply(program, intensity),
                _ => throw new ArgumentOutOfRangeException(nameof(intensity))
            };
        }

        public static TrainingIntensityBalanceTable CreateDefault()
        {
            return new TrainingIntensityBalanceTable(
                new TrainingIntensityRule(+1, 0.77d, 0.85d, 0.56d, 0.50d, -10, 0, 0),
                new TrainingIntensityRule(0, 1.00d, 1.00d, 1.00d, 1.00d, 0, 0, 0),
                new TrainingIntensityRule(-1, 1.385d, 1.25d, 1.56d, 2.00d, +10, +1, +1));
        }
    }

    /// <summary>
    /// 성장·자연 성장·노쇠·스킬 뽑기의 1차 밸런스값과 프로그램 정의를 묶는다.
    /// </summary>
    public sealed class GrowthBalanceTable
    {
        public GrowthBalanceTable(
            AgeGrowthCurveTable ageGrowth,
            PotentialGapMultiplierTable potentialGap,
            WorkEthicMultiplierTable workEthic,
            TrainingFitMultiplierTable trainingFit,
            ConditionMultiplierTable condition,
            RepetitionMultiplierTable repetition,
            NaturalGrowthBalanceTable naturalGrowth,
            AgingDeclineBalanceTable agingDecline,
            SkillGachaBalanceTable skillGacha,
            double minimumQualityRoll,
            double maximumQualityRoll,
            double potentialBreakthroughProbability,
            int trainingInjuryConditionPenalty,
            int defaultPotentialGap,
            int offseasonWeeks,
            TrainingProgramDefinition[] programs,
            SkillBoardDefinition skillBoard = null,
            SkillBlockDefinition[] skillBlocks = null,
            long skillBoardRedesignCost = MoneyAmount.WonPerTenThousand * 1_500L,
            TrainingIntensityBalanceTable trainingIntensity = null)
        {
            if (minimumQualityRoll <= 0d || maximumQualityRoll < minimumQualityRoll)
                throw new ArgumentOutOfRangeException(nameof(minimumQualityRoll));
            if (offseasonWeeks <= 0)
                throw new ArgumentOutOfRangeException(nameof(offseasonWeeks));
            if (potentialBreakthroughProbability < 0d || potentialBreakthroughProbability > 1d)
                throw new ArgumentOutOfRangeException(nameof(potentialBreakthroughProbability));
            if (trainingInjuryConditionPenalty < 0)
                throw new ArgumentOutOfRangeException(nameof(trainingInjuryConditionPenalty));
            if (defaultPotentialGap <= 0)
                throw new ArgumentOutOfRangeException(nameof(defaultPotentialGap));
            if (skillBoardRedesignCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(skillBoardRedesignCost));
            AgeGrowth = ageGrowth;
            PotentialGap = potentialGap;
            WorkEthic = workEthic;
            TrainingFit = trainingFit;
            Condition = condition;
            Repetition = repetition;
            NaturalGrowth = naturalGrowth;
            AgingDecline = agingDecline;
            SkillGacha = skillGacha;
            MinimumQualityRoll = minimumQualityRoll;
            MaximumQualityRoll = maximumQualityRoll;
            PotentialBreakthroughProbability = potentialBreakthroughProbability;
            TrainingInjuryConditionPenalty = trainingInjuryConditionPenalty;
            DefaultPotentialGap = defaultPotentialGap;
            OffseasonWeeks = offseasonWeeks;
            Programs = programs ?? throw new ArgumentNullException(nameof(programs));
            SkillBoard = skillBoard ?? GrowthSkillContent.CreateDefaultBoard();
            SkillBlocks = skillBlocks ?? GrowthSkillContent.CreateDefaultBlocks();
            SkillBoardRedesignCost = skillBoardRedesignCost;
            TrainingIntensities = trainingIntensity ?? TrainingIntensityBalanceTable.CreateDefault();
        }

        public AgeGrowthCurveTable AgeGrowth { get; }
        public PotentialGapMultiplierTable PotentialGap { get; }
        public WorkEthicMultiplierTable WorkEthic { get; }
        public TrainingFitMultiplierTable TrainingFit { get; }
        public ConditionMultiplierTable Condition { get; }
        public RepetitionMultiplierTable Repetition { get; }
        public NaturalGrowthBalanceTable NaturalGrowth { get; }
        public AgingDeclineBalanceTable AgingDecline { get; }
        public SkillGachaBalanceTable SkillGacha { get; }
        public double MinimumQualityRoll { get; }
        public double MaximumQualityRoll { get; }
        public double PotentialBreakthroughProbability { get; }
        public int TrainingInjuryConditionPenalty { get; }
        public int DefaultPotentialGap { get; }
        public int OffseasonWeeks { get; }
        public TrainingProgramDefinition[] Programs { get; }
        public SkillBoardDefinition SkillBoard { get; }
        public SkillBlockDefinition[] SkillBlocks { get; }
        public long SkillBoardRedesignCost { get; }
        public TrainingIntensityBalanceTable TrainingIntensities { get; }

        public TrainingProgramDefinition FindProgram(string programId)
        {
            for (int index = 0; index < Programs.Length; index++)
            {
                if (string.Equals(Programs[index].ProgramId, programId, StringComparison.Ordinal))
                    return Programs[index];
            }
            return null;
        }

        public TrainingProgramDefinition GetProgram(
            string programId,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            TrainingProgramDefinition program = FindProgram(programId) ??
                                                throw new ArgumentException(
                                                    "존재하지 않는 프로그램입니다.",
                                                    nameof(programId));
            return TrainingIntensities.Apply(program, intensity);
        }

        public static GrowthBalanceTable CreateDefault()
        {
            return new GrowthBalanceTable(
                new AgeGrowthCurveTable(1.20d, 1.00d, 0.80d, 0.60d, 0.40d),
                new PotentialGapMultiplierTable(1.20d, 1.00d, 0.65d, 0.30d, 0.10d),
                new WorkEthicMultiplierTable(0.90d, 1.00d, 1.10d, 1.15d),
                new TrainingFitMultiplierTable(0.85d, 1.00d, 1.10d, 1.15d),
                new ConditionMultiplierTable(80, 60, 40, 1.00d, 0.90d, 0.75d),
                new RepetitionMultiplierTable(1.00d, 0.85d, 0.70d, 0.90d, 0.80d),
                new NaturalGrowthBalanceTable(0.80d, 0.40d, 0.15d, 0.55d, 0.75d, 1.00d, 0.95d),
                new AgingDeclineBalanceTable(0.35d, 0.90d, 0.20d, 1.50d, 0.60d, 0.15d),
                new SkillGachaBalanceTable(
                    new SkillGachaOfferBalance(
                        SkillGachaPurchaseTier.Normal,
                        SkillBlockRarity.Normal,
                        MoneyAmount.FromTenThousandWon(600L),
                        0,
                        0.55d, 0.28d, 0.13d, 0.04d, 0.00d),
                    new SkillGachaOfferBalance(
                        SkillGachaPurchaseTier.Rare,
                        SkillBlockRarity.Rare,
                        MoneyAmount.FromTenThousandWon(1_500L),
                        0,
                        0.00d, 0.70d, 0.22d, 0.07d, 0.01d),
                    new SkillGachaOfferBalance(
                        SkillGachaPurchaseTier.Elite,
                        SkillBlockRarity.Elite,
                        MoneyAmount.FromTenThousandWon(4_000L),
                        0,
                        0.00d, 0.00d, 0.75d, 0.20d, 0.05d),
                    new SkillGachaOfferBalance(
                        SkillGachaPurchaseTier.Unique,
                        SkillBlockRarity.Unique,
                        MoneyAmount.FromTenThousandWon(10_000L),
                        2,
                        0.00d, 0.00d, 0.00d, 0.85d, 0.15d),
                    new SkillGachaOfferBalance(
                        SkillGachaPurchaseTier.Legendary,
                        SkillBlockRarity.Legendary,
                        MoneyAmount.FromTenThousandWon(25_000L),
                        1,
                        0.00d, 0.00d, 0.00d, 0.00d, 1.00d),
                    fivePullDiscountRate: 0.05d,
                    elitePity: 10,
                    uniquePity: 30,
                    legendaryPity: 60,
                    legendaryMinimumCareerAwards: 1,
                    highTierPurchasesRequireOffseason: true),
                0.90d,
                1.10d,
                0.02d,
                15,
                12,
                12,
                CreateDefaultPrograms());
        }

        private static TrainingProgramDefinition[] CreateDefaultPrograms()
        {
            return new[]
            {
                new TrainingProgramDefinition("rest", OffseasonActivityType.Rest, TrainingCategory.Rest, null,
                    1, 0L, 0d, Array.Empty<AbilityWeight>(), 0, 0d, 0, 0, 15),
                new TrainingProgramDefinition("rehab_general", OffseasonActivityType.Rehabilitation, TrainingCategory.Rehabilitation, null,
                    2, MoneyAmount.FromTenThousandWon(200L), 0d, Array.Empty<AbilityWeight>(), 0, 0d, 0, 0, 25),
                // 스포츠 사이언스는 성장량이 아니라 1주라는 시간 절약에 높은 비용을 지불하는 회복 선택이다.
                new TrainingProgramDefinition("sports_science_recovery", OffseasonActivityType.Rehabilitation, TrainingCategory.Rehabilitation, null,
                    1, MoneyAmount.FromTenThousandWon(1_500L), 0d, Array.Empty<AbilityWeight>(), 0, 0d, 0, 0, 30,
                    minimumAccessTier: TrainingAccessTier.Advanced),
                new TrainingProgramDefinition("weight_batter", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Batter,
                    4, MoneyAmount.FromTenThousandWon(150L), 1.0d, new[] { new AbilityWeight(PlayerAbility.Power, 0.7d), new AbilityWeight(PlayerAbility.Speed, 0.3d) }, 40, 0.01d, 2, 2, -12),
                new TrainingProgramDefinition("weight_pitcher", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Pitcher,
                    4, MoneyAmount.FromTenThousandWon(150L), 1.0d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.5d), new AbilityWeight(PlayerAbility.Stamina, 0.5d) }, 40, 0.01d, 2, 2, -12),
                new TrainingProgramDefinition("personal_batting", OffseasonActivityType.PersonalTraining, TrainingCategory.Batting, PlayerType.Batter,
                    3, MoneyAmount.FromTenThousandWon(300L), 0.9d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.7d), new AbilityWeight(PlayerAbility.BatterMental, 0.3d) }, 40, 0.01d, 2, 2, -10),
                new TrainingProgramDefinition("personal_pitching", OffseasonActivityType.PersonalTraining, TrainingCategory.Pitching, PlayerType.Pitcher,
                    3, MoneyAmount.FromTenThousandWon(300L), 0.9d, new[] { new AbilityWeight(PlayerAbility.Breaking, 0.5d), new AbilityWeight(PlayerAbility.Control, 0.5d) }, 40, 0.01d, 2, 2, -10),
                new TrainingProgramDefinition("bat_balance_training", OffseasonActivityType.PersonalTraining, TrainingCategory.Batting, PlayerType.Batter,
                    2, MoneyAmount.FromTenThousandWon(350L), 0.75d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.25d), new AbilityWeight(PlayerAbility.Power, 0.20d), new AbilityWeight(PlayerAbility.Speed, 0.15d), new AbilityWeight(PlayerAbility.Defense, 0.20d), new AbilityWeight(PlayerAbility.BatterMental, 0.20d) }, 40, 0.005d, 2, 1, -8, 1),
                new TrainingProgramDefinition("bat_power_camp", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Batter,
                    3, MoneyAmount.FromTenThousandWon(650L), 1.15d, new[] { new AbilityWeight(PlayerAbility.Power, 0.65d), new AbilityWeight(PlayerAbility.Contact, 0.20d), new AbilityWeight(PlayerAbility.BatterMental, 0.15d) }, 45, 0.015d, 3, 2, -18, 1),
                new TrainingProgramDefinition("bat_contact_training", OffseasonActivityType.PersonalTraining, TrainingCategory.Batting, PlayerType.Batter,
                    2, MoneyAmount.FromTenThousandWon(420L), 0.90d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.70d), new AbilityWeight(PlayerAbility.BatterMental, 0.30d) }, 40, 0.0075d, 2, 2, -10, 1),
                new TrainingProgramDefinition("bat_speed_defense_camp", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Batter,
                    4, MoneyAmount.FromTenThousandWon(780L), 1.10d, new[] { new AbilityWeight(PlayerAbility.Speed, 0.45d), new AbilityWeight(PlayerAbility.Defense, 0.30d), new AbilityWeight(PlayerAbility.Arm, 0.15d), new AbilityWeight(PlayerAbility.BatterMental, 0.10d) }, 45, 0.01d, 3, 2, -14, 1),
                // 엘리트 랩은 일반 캠프보다 짧지만 비용·컨디션·부상 부담이 커서 자금으로 시간을 사는 선택이다.
                new TrainingProgramDefinition("bat_elite_hitting_lab", OffseasonActivityType.PersonalTraining, TrainingCategory.Batting, PlayerType.Batter,
                    2, MoneyAmount.FromTenThousandWon(1_400L), 1.45d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.45d), new AbilityWeight(PlayerAbility.Power, 0.35d), new AbilityWeight(PlayerAbility.BatterMental, 0.20d) }, 50, 0.025d, 4, 3, -22, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                new TrainingProgramDefinition("pitch_velocity_camp", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Pitcher,
                    3, MoneyAmount.FromTenThousandWon(650L), 1.15d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.50d), new AbilityWeight(PlayerAbility.Stuff, 0.35d), new AbilityWeight(PlayerAbility.Stamina, 0.15d) }, 45, 0.015d, 3, 2, -18, 1),
                new TrainingProgramDefinition("pitch_control_training", OffseasonActivityType.PersonalTraining, TrainingCategory.Pitching, PlayerType.Pitcher,
                    2, MoneyAmount.FromTenThousandWon(420L), 0.90d, new[] { new AbilityWeight(PlayerAbility.Control, 0.65d), new AbilityWeight(PlayerAbility.PitcherMental, 0.35d) }, 40, 0.0075d, 2, 2, -10, 1),
                new TrainingProgramDefinition("pitch_stamina_camp", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Pitcher,
                    4, MoneyAmount.FromTenThousandWon(780L), 1.10d, new[] { new AbilityWeight(PlayerAbility.Stamina, 0.60d), new AbilityWeight(PlayerAbility.PitcherMental, 0.25d), new AbilityWeight(PlayerAbility.Stuff, 0.15d) }, 45, 0.01d, 3, 2, -14, 1),
                new TrainingProgramDefinition("pitch_breaking_training", OffseasonActivityType.PersonalTraining, TrainingCategory.Pitching, PlayerType.Pitcher,
                    3, MoneyAmount.FromTenThousandWon(700L), 1.05d, new[] { new AbilityWeight(PlayerAbility.Breaking, 0.55d), new AbilityWeight(PlayerAbility.Stuff, 0.30d), new AbilityWeight(PlayerAbility.Control, 0.15d) }, 45, 0.0125d, 3, 2, -16, 1),
                new TrainingProgramDefinition("pitch_elite_biomechanics", OffseasonActivityType.PersonalTraining, TrainingCategory.Pitching, PlayerType.Pitcher,
                    2, MoneyAmount.FromTenThousandWon(1_400L), 1.45d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.35d), new AbilityWeight(PlayerAbility.Stuff, 0.30d), new AbilityWeight(PlayerAbility.Control, 0.20d), new AbilityWeight(PlayerAbility.Breaking, 0.15d) }, 50, 0.025d, 4, 3, -22, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                new TrainingProgramDefinition("partner_batter_default", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Batter,
                    3, MoneyAmount.FromTenThousandWon(800L), 1.4d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.65d), new AbilityWeight(PlayerAbility.BatterMental, 0.35d) }, 40, 0.015d, 3, 3, -12, partnerId: "partner_batter_default", canRaisePotential: true),
                new TrainingProgramDefinition("partner_pitcher_default", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Pitcher,
                    3, MoneyAmount.FromTenThousandWon(800L), 1.4d, new[] { new AbilityWeight(PlayerAbility.Control, 0.6d), new AbilityWeight(PlayerAbility.PitcherMental, 0.4d) }, 40, 0.015d, 3, 3, -12, partnerId: "partner_pitcher_default", canRaisePotential: true),
                new TrainingProgramDefinition("private_batting_coach", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Batter,
                    3, MoneyAmount.FromTenThousandWon(1_800L), 1.8d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.50d), new AbilityWeight(PlayerAbility.Power, 0.25d), new AbilityWeight(PlayerAbility.BatterMental, 0.25d) }, 45, 0.01d, 4, 3, -10, 1, "private_batting_coach", true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                new TrainingProgramDefinition("private_pitching_coach", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Pitcher,
                    3, MoneyAmount.FromTenThousandWon(1_800L), 1.8d, new[] { new AbilityWeight(PlayerAbility.Control, 0.40d), new AbilityWeight(PlayerAbility.Breaking, 0.30d), new AbilityWeight(PlayerAbility.PitcherMental, 0.30d) }, 45, 0.01d, 4, 3, -10, 1, "private_pitching_coach", true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                new TrainingProgramDefinition("japan_batting_camp", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Batter,
                    6, MoneyAmount.FromTenThousandWon(3_200L), 2.4d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.55d), new AbilityWeight(PlayerAbility.BatterMental, 0.25d), new AbilityWeight(PlayerAbility.Defense, 0.20d) }, 40, 0.02d, 4, 4, -20, 1,
                    canRaisePotential: true,
                    potentialBreakthroughChanceMultiplier: 2d),
                new TrainingProgramDefinition("japan_pitch_design", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Pitcher,
                    6, MoneyAmount.FromTenThousandWon(3_200L), 2.4d, new[] { new AbilityWeight(PlayerAbility.Breaking, 0.6d), new AbilityWeight(PlayerAbility.Control, 0.4d) }, 40, 0.02d, 4, 4, -20, 1,
                    canRaisePotential: true,
                    potentialBreakthroughChanceMultiplier: 2d),
                new TrainingProgramDefinition("usa_power_center", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Batter,
                    8, MoneyAmount.FromTenThousandWon(4_500L), 3.0d, new[] { new AbilityWeight(PlayerAbility.Power, 0.7d), new AbilityWeight(PlayerAbility.Speed, 0.3d) }, 40, 0.03d, 5, 5, -25, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                new TrainingProgramDefinition("usa_velocity_center", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Pitcher,
                    8, MoneyAmount.FromTenThousandWon(4_500L), 3.0d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.6d), new AbilityWeight(PlayerAbility.Stamina, 0.4d) }, 40, 0.03d, 5, 5, -25, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 3d),
                // 엘리트 아카데미는 오프시즌 대부분과 큰 자금을 쓰는 대신 성장 상한과 최소 보장을 높인다.
                new TrainingProgramDefinition("usa_elite_batting_academy", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Batter,
                    9, MoneyAmount.FromTenThousandWon(7_200L), 3.8d, new[] { new AbilityWeight(PlayerAbility.Power, 0.45d), new AbilityWeight(PlayerAbility.Contact, 0.35d), new AbilityWeight(PlayerAbility.BatterMental, 0.20d) }, 55, 0.04d, 6, 5, -28, 2,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Elite,
                    potentialBreakthroughChanceMultiplier: 5d,
                    minimumPotentialBreakthroughsWhenCapped: 1),
                new TrainingProgramDefinition("usa_elite_pitching_academy", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Pitcher,
                    9, MoneyAmount.FromTenThousandWon(7_200L), 3.8d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.40d), new AbilityWeight(PlayerAbility.Stuff, 0.35d), new AbilityWeight(PlayerAbility.Control, 0.25d) }, 55, 0.04d, 6, 5, -28, 2,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Elite,
                    potentialBreakthroughChanceMultiplier: 5d,
                    minimumPotentialBreakthroughsWhenCapped: 1),
                new TrainingProgramDefinition("caribbean_batting_league", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Batter,
                    7, MoneyAmount.FromTenThousandWon(3_800L), 2.7d, new[] { new AbilityWeight(PlayerAbility.Power, 0.45d), new AbilityWeight(PlayerAbility.BatterMental, 0.30d), new AbilityWeight(PlayerAbility.Contact, 0.25d) }, 40, 0.025d, 5, 4, -22, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 2d),
                new TrainingProgramDefinition("europe_batting_balance", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Batter,
                    5, MoneyAmount.FromTenThousandWon(2_500L), 1.9d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.40d), new AbilityWeight(PlayerAbility.Defense, 0.35d), new AbilityWeight(PlayerAbility.BatterMental, 0.25d) }, 40, 0.015d, 3, 3, -16, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 2d),
                new TrainingProgramDefinition("caribbean_pitch_league", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Pitcher,
                    7, MoneyAmount.FromTenThousandWon(3_800L), 2.7d, new[] { new AbilityWeight(PlayerAbility.Stuff, 0.45d), new AbilityWeight(PlayerAbility.PitcherMental, 0.30d), new AbilityWeight(PlayerAbility.Stamina, 0.25d) }, 40, 0.025d, 5, 4, -22, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 2d),
                new TrainingProgramDefinition("europe_pitch_balance", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Pitcher,
                    5, MoneyAmount.FromTenThousandWon(2_500L), 1.9d, new[] { new AbilityWeight(PlayerAbility.Control, 0.45d), new AbilityWeight(PlayerAbility.Stamina, 0.30d), new AbilityWeight(PlayerAbility.PitcherMental, 0.25d) }, 40, 0.015d, 3, 3, -16, 1,
                    canRaisePotential: true,
                    minimumAccessTier: TrainingAccessTier.Advanced,
                    potentialBreakthroughChanceMultiplier: 2d)
            };
        }
    }
}

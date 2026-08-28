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

    public readonly struct SkillGachaBalanceTable
    {
        public SkillGachaBalanceTable(
            long singlePrice,
            long bundlePrice,
            double commonProbability,
            double uncommonProbability,
            double rareProbability,
            double epicProbability,
            int rarePity,
            int epicPity)
        {
            if (singlePrice <= 0L || bundlePrice <= 0L)
                throw new ArgumentOutOfRangeException(nameof(singlePrice));
            double probabilitySum = commonProbability + uncommonProbability + rareProbability + epicProbability;
            if (Math.Abs(probabilitySum - 1d) > 0.000001d)
                throw new ArgumentException("등급 확률 합은 1이어야 합니다.");
            if (rarePity <= 0 || epicPity <= rarePity)
                throw new ArgumentOutOfRangeException(nameof(rarePity));
            SinglePrice = singlePrice;
            BundlePrice = bundlePrice;
            CommonProbability = commonProbability;
            UncommonProbability = uncommonProbability;
            RareProbability = rareProbability;
            EpicProbability = epicProbability;
            RarePity = rarePity;
            EpicPity = epicPity;
        }

        public long SinglePrice { get; }
        public long BundlePrice { get; }
        public double CommonProbability { get; }
        public double UncommonProbability { get; }
        public double RareProbability { get; }
        public double EpicProbability { get; }
        public int RarePity { get; }
        public int EpicPity { get; }
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
            TrainingProgramDefinition[] programs)
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

        public TrainingProgramDefinition FindProgram(string programId)
        {
            for (int index = 0; index < Programs.Length; index++)
            {
                if (string.Equals(Programs[index].ProgramId, programId, StringComparison.Ordinal))
                    return Programs[index];
            }
            return null;
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
                new SkillGachaBalanceTable(600L, 2700L, 0.55d, 0.28d, 0.13d, 0.04d, 10, 30),
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
                    2, 200L, 0d, Array.Empty<AbilityWeight>(), 0, 0d, 0, 0, 25),
                new TrainingProgramDefinition("weight_batter", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Batter,
                    4, 150L, 1.0d, new[] { new AbilityWeight(PlayerAbility.Power, 0.7d), new AbilityWeight(PlayerAbility.Speed, 0.3d) }, 40, 0.01d, 2, 2, -12),
                new TrainingProgramDefinition("weight_pitcher", OffseasonActivityType.PersonalTraining, TrainingCategory.Strength, PlayerType.Pitcher,
                    4, 150L, 1.0d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.5d), new AbilityWeight(PlayerAbility.Stamina, 0.5d) }, 40, 0.01d, 2, 2, -12),
                new TrainingProgramDefinition("personal_batting", OffseasonActivityType.PersonalTraining, TrainingCategory.Batting, PlayerType.Batter,
                    3, 300L, 0.9d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.7d), new AbilityWeight(PlayerAbility.BatterMental, 0.3d) }, 40, 0.01d, 2, 2, -10),
                new TrainingProgramDefinition("personal_pitching", OffseasonActivityType.PersonalTraining, TrainingCategory.Pitching, PlayerType.Pitcher,
                    3, 300L, 0.9d, new[] { new AbilityWeight(PlayerAbility.Breaking, 0.5d), new AbilityWeight(PlayerAbility.Control, 0.5d) }, 40, 0.01d, 2, 2, -10),
                new TrainingProgramDefinition("partner_batter_default", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Batter,
                    3, 800L, 1.4d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.65d), new AbilityWeight(PlayerAbility.BatterMental, 0.35d) }, 40, 0.015d, 3, 3, -12, partnerId: "partner_batter_default", canRaisePotential: true),
                new TrainingProgramDefinition("partner_pitcher_default", OffseasonActivityType.TrainingPartner, TrainingCategory.Partner, PlayerType.Pitcher,
                    3, 800L, 1.4d, new[] { new AbilityWeight(PlayerAbility.Control, 0.6d), new AbilityWeight(PlayerAbility.PitcherMental, 0.4d) }, 40, 0.015d, 3, 3, -12, partnerId: "partner_pitcher_default", canRaisePotential: true),
                new TrainingProgramDefinition("japan_batting_camp", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Batter,
                    6, 3200L, 2.4d, new[] { new AbilityWeight(PlayerAbility.Contact, 0.55d), new AbilityWeight(PlayerAbility.BatterMental, 0.25d), new AbilityWeight(PlayerAbility.Defense, 0.20d) }, 40, 0.02d, 4, 4, -20, 1, canRaisePotential: true),
                new TrainingProgramDefinition("japan_pitch_design", OffseasonActivityType.Study, TrainingCategory.StudyTechnical, PlayerType.Pitcher,
                    6, 3200L, 2.4d, new[] { new AbilityWeight(PlayerAbility.Breaking, 0.6d), new AbilityWeight(PlayerAbility.Control, 0.4d) }, 40, 0.02d, 4, 4, -20, 1, canRaisePotential: true),
                new TrainingProgramDefinition("usa_power_center", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Batter,
                    8, 4500L, 3.0d, new[] { new AbilityWeight(PlayerAbility.Power, 0.7d), new AbilityWeight(PlayerAbility.Speed, 0.3d) }, 40, 0.03d, 5, 5, -25, 1, canRaisePotential: true),
                new TrainingProgramDefinition("usa_velocity_center", OffseasonActivityType.Study, TrainingCategory.StudyPhysical, PlayerType.Pitcher,
                    8, 4500L, 3.0d, new[] { new AbilityWeight(PlayerAbility.Velocity, 0.6d), new AbilityWeight(PlayerAbility.Stamina, 0.4d) }, 40, 0.03d, 5, 5, -25, 1, canRaisePotential: true)
            };
        }
    }
}

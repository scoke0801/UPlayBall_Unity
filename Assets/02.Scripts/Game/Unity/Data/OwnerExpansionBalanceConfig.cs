using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>09~12 구단주 확장 시스템의 직렬화 JSON을 순수 C# Balance 계약으로 변환한다.</summary>
    internal static class OwnerExpansionBalanceConfig
    {
        public const int CurrentSchemaVersion = 1;

        public static OwnerExpansionBalanceTables Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("OwnerExpansionBalance Config가 비어 있습니다.");

            OwnerExpansionBalanceData data;
            try
            {
                data = JsonUtility.FromJson<OwnerExpansionBalanceData>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("OwnerExpansionBalance Config JSON을 읽을 수 없습니다.", exception);
            }

            if (data == null)
                throw new InvalidOperationException("OwnerExpansionBalance Config JSON 루트가 없습니다.");
            if (data.schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"OwnerExpansionBalance Config SchemaVersion이 올바르지 않습니다. expected={CurrentSchemaVersion}, actual={data.schemaVersion}");
            }
            if (string.IsNullOrWhiteSpace(data.contentId))
                throw new InvalidOperationException("OwnerExpansionBalance Config ContentId가 비어 있습니다.");
            if (data.conditionChemistry == null || data.clubOperation == null ||
                data.staff == null || data.scoutingConfidence == null)
            {
                throw new InvalidOperationException("OwnerExpansionBalance Config에 09~12 시스템 섹션이 모두 필요합니다.");
            }

            try
            {
                return new OwnerExpansionBalanceTables(
                    data.conditionChemistry.Build(),
                    data.clubOperation.Build(),
                    data.staff.Build(),
                    data.scoutingConfidence.Build(),
                    CreateContentHash(json));
            }
            catch (Exception exception) when (!(exception is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"OwnerExpansionBalance Config 계약이 유효하지 않습니다: {data.contentId.Trim()}",
                    exception);
            }
        }

        private static string CreateContentHash(string json)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            var result = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
                result.Append(bytes[index].ToString("x2"));
            return result.ToString();
        }
    }

    /// <summary>Production 새 게임에 주입할 네 확장 시스템 Balance와 버전 해시를 묶는다.</summary>
    internal readonly struct OwnerExpansionBalanceTables
    {
        public OwnerExpansionBalanceTables(
            ConditionChemistryBalanceTable conditionChemistry,
            ClubOperationBalanceTable clubOperation,
            StaffBalanceTable staff,
            ScoutingConfidenceDefinition scoutingConfidence,
            string contentHash)
        {
            ConditionChemistry = conditionChemistry ?? throw new ArgumentNullException(nameof(conditionChemistry));
            ClubOperation = clubOperation ?? throw new ArgumentNullException(nameof(clubOperation));
            Staff = staff ?? throw new ArgumentNullException(nameof(staff));
            ScoutingConfidence = scoutingConfidence ?? throw new ArgumentNullException(nameof(scoutingConfidence));
            ContentHash = string.IsNullOrWhiteSpace(contentHash)
                ? throw new ArgumentException("ContentHash가 필요합니다.", nameof(contentHash))
                : contentHash.Trim();
        }

        public ConditionChemistryBalanceTable ConditionChemistry { get; }
        public ClubOperationBalanceTable ClubOperation { get; }
        public StaffBalanceTable Staff { get; }
        public ScoutingConfidenceDefinition ScoutingConfidence { get; }
        public string ContentHash { get; }
    }

    [Serializable]
    internal sealed class OwnerExpansionBalanceData
    {
        public int schemaVersion;
        public string contentId;
        public ConditionChemistryBalanceData conditionChemistry;
        public ClubOperationBalanceData clubOperation;
        public StaffBalanceData staff;
        public ScoutingConfidenceBalanceData scoutingConfidence;
    }

    [Serializable]
    internal sealed class ConditionPresentationBandData
    {
        public int minimumCondition;
        public string labelKey;
        public string iconKey;

        public ConditionPresentationBand Build() =>
            new ConditionPresentationBand(minimumCondition, labelKey, iconKey);
    }

    [Serializable]
    internal sealed class ConditionChemistryBalanceData
    {
        public ConditionPresentationBandData[] presentationBands;
        public int familiarityCap;
        public int lineupSharedStartGain;
        public int batterySharedInningGain;
        public double familiarityScoreWeight;
        public double styleComplementScore;
        public double styleConflictScore;
        public double catcherDefenseWeight;
        public double catcherMentalWeight;
        public double pitcherMentalStabilityWeight;
        public double goodScoreThreshold;
        public double badScoreThreshold;
        public int conditionLevelStep;
        public int maximumChemistryLevelDelta;
        public int tableSetterLeadThreshold;
        public int powerLeadThreshold;
        public int neutralMatchCondition;
        public int conditionPointsPerRating;
        public int maximumConditionRatingModifier;
        public int weeklyBaseRecovery;
        public int startingHitterConditionCost;
        public int pitcherConditionCostPerThirtyPitches;

        public ConditionChemistryBalanceTable Build()
        {
            if (presentationBands == null)
                throw new InvalidOperationException("Condition 표시 단계가 없습니다.");
            var bands = new ConditionPresentationBand[presentationBands.Length];
            for (int index = 0; index < bands.Length; index++)
            {
                bands[index] = presentationBands[index]?.Build() ??
                    throw new InvalidOperationException("null Condition 표시 단계가 있습니다.");
            }
            return new ConditionChemistryBalanceTable(
                new ConditionPresentationTable(bands),
                familiarityCap,
                lineupSharedStartGain,
                batterySharedInningGain,
                familiarityScoreWeight,
                styleComplementScore,
                styleConflictScore,
                catcherDefenseWeight,
                catcherMentalWeight,
                pitcherMentalStabilityWeight,
                goodScoreThreshold,
                badScoreThreshold,
                conditionLevelStep,
                maximumChemistryLevelDelta,
                tableSetterLeadThreshold,
                powerLeadThreshold,
                neutralMatchCondition,
                conditionPointsPerRating,
                maximumConditionRatingModifier,
                weeklyBaseRecovery,
                startingHitterConditionCost,
                pitcherConditionCostPerThirtyPitches);
        }
    }

    [Serializable]
    internal sealed class AttendanceBalanceData
    {
        public double minimumBaseDemand;
        public double maximumBaseDemand;
        public double minimumPopularityFactor;
        public double maximumPopularityFactor;
        public double minimumRecentPerformanceFactor;
        public double maximumRecentPerformanceFactor;
        public double minimumOpponentAttractionFactor;
        public double maximumOpponentAttractionFactor;
        public double minimumSeasonImportanceFactor;
        public double maximumSeasonImportanceFactor;
        public double minimumMomentumFactor;
        public double maximumMomentumFactor;
        public double rivalryAttractionWeight;
        public double varianceMinimum;
        public double varianceMaximum;

        public AttendanceBalanceDefinition Build() => new AttendanceBalanceDefinition(
            minimumBaseDemand,
            maximumBaseDemand,
            minimumPopularityFactor,
            maximumPopularityFactor,
            minimumRecentPerformanceFactor,
            maximumRecentPerformanceFactor,
            minimumOpponentAttractionFactor,
            maximumOpponentAttractionFactor,
            minimumSeasonImportanceFactor,
            maximumSeasonImportanceFactor,
            minimumMomentumFactor,
            maximumMomentumFactor,
            rivalryAttractionWeight,
            varianceMinimum,
            varianceMaximum);
    }

    [Serializable]
    internal sealed class HomeGameFinanceBalanceData
    {
        public long otherRevenuePerAttendee;
        public long baseGameDayOperatingCost;

        public HomeGameFinanceBalanceDefinition Build() =>
            new HomeGameFinanceBalanceDefinition(otherRevenuePerAttendee, baseGameDayOperatingCost);
    }

    [Serializable]
    internal sealed class FanPopularityBalanceData
    {
        public double winFanBaseDelta;
        public double drawFanBaseDelta;
        public double lossFanBaseDelta;
        public double winPopularityDelta;
        public double drawPopularityDelta;
        public double lossPopularityDelta;
        public double seasonImportanceOutcomeScale;
        public double attendanceFanBaseDeltaAtEmpty;
        public double attendanceFanBaseDeltaAtFull;
        public double popularityDecayPerHomeGame;
        public double momentumTargetCapacityRate;
        public double momentumDeltaScale;

        public FanPopularityBalanceDefinition Build() => new FanPopularityBalanceDefinition(
            winFanBaseDelta,
            drawFanBaseDelta,
            lossFanBaseDelta,
            winPopularityDelta,
            drawPopularityDelta,
            lossPopularityDelta,
            seasonImportanceOutcomeScale,
            attendanceFanBaseDeltaAtEmpty,
            attendanceFanBaseDeltaAtFull,
            popularityDecayPerHomeGame,
            momentumTargetCapacityRate,
            momentumDeltaScale);
    }

    [Serializable]
    internal sealed class TicketPolicyBalanceData
    {
        public TicketPriceTier priceTier;
        public double demandMultiplier;
        public long revenuePerAttendee;

        public TicketPolicyDefinition Build() =>
            new TicketPolicyDefinition(priceTier, demandMultiplier, revenuePerAttendee);
    }

    [Serializable]
    internal sealed class LeagueOperationBalanceData
    {
        public LeagueGrade leagueGrade;
        public double demandMultiplier;
        public double operatingCostMultiplier;

        public LeagueOperationDefinition Build() =>
            new LeagueOperationDefinition(leagueGrade, demandMultiplier, operatingCostMultiplier);
    }

    [Serializable]
    internal sealed class FacilityLevelBalanceData
    {
        public FacilityType type;
        public int level;
        public long upgradeMoneyCost;
        public long weeklyOperatingCost;
        public long homeGameOperatingCost;
        public bool hasRequiredLeagueGrade;
        public LeagueGrade requiredLeagueGrade;
        public double minimumFanBase;
        public long minimumSeasonAttendance;
        public int weeklyScoutingPointProduction;
        public bool hasScoutingPointStorageCapacity;
        public int scoutingPointStorageCapacity;
        public int weeklyDevelopmentPointProduction;
        public bool hasDevelopmentPointStorageCapacity;
        public int developmentPointStorageCapacity;
        public double conditionRecoveryEfficiencyModifier;
        public double scoutingConfidenceModifier;
        public double tacticResearchEfficiencyModifier;
        public long fanShopRevenuePerAttendee;
        public double fanShopPopularityRetention;

        public FacilityLevelDefinition Build() => new FacilityLevelDefinition(
            type,
            level,
            upgradeMoneyCost,
            weeklyOperatingCost,
            homeGameOperatingCost,
            hasRequiredLeagueGrade ? requiredLeagueGrade : (LeagueGrade?)null,
            minimumFanBase,
            minimumSeasonAttendance,
            weeklyScoutingPointProduction,
            hasScoutingPointStorageCapacity ? scoutingPointStorageCapacity : (int?)null,
            weeklyDevelopmentPointProduction,
            hasDevelopmentPointStorageCapacity ? developmentPointStorageCapacity : (int?)null,
            conditionRecoveryEfficiencyModifier,
            scoutingConfidenceModifier,
            tacticResearchEfficiencyModifier,
            fanShopRevenuePerAttendee,
            fanShopPopularityRetention);
    }

    [Serializable]
    internal sealed class StadiumLevelBalanceData
    {
        public int level;
        public int capacity;
        public long upgradeMoneyCost;
        public long homeGameOperatingCost;
        public bool hasRequiredLeagueGrade;
        public LeagueGrade requiredLeagueGrade;
        public double minimumFanBase;
        public long minimumSeasonAttendance;

        public StadiumLevelDefinition Build() => new StadiumLevelDefinition(
            level,
            capacity,
            upgradeMoneyCost,
            homeGameOperatingCost,
            hasRequiredLeagueGrade ? requiredLeagueGrade : (LeagueGrade?)null,
            minimumFanBase,
            minimumSeasonAttendance);
    }

    [Serializable]
    internal sealed class ClubOperationBalanceData
    {
        public AttendanceBalanceData attendance;
        public HomeGameFinanceBalanceData homeGameFinance;
        public FanPopularityBalanceData fanPopularity;
        public TicketPolicyBalanceData[] ticketPolicies;
        public LeagueOperationBalanceData[] leagueOperations;
        public FacilityLevelBalanceData[] facilityLevels;
        public StadiumLevelBalanceData[] stadiumLevels;

        public ClubOperationBalanceTable Build()
        {
            return new ClubOperationBalanceTable(
                Require(attendance, nameof(attendance)).Build(),
                Require(homeGameFinance, nameof(homeGameFinance)).Build(),
                Require(fanPopularity, nameof(fanPopularity)).Build(),
                BuildRows(ticketPolicies, row => row.Build(), nameof(ticketPolicies)),
                BuildRows(leagueOperations, row => row.Build(), nameof(leagueOperations)),
                BuildRows(facilityLevels, row => row.Build(), nameof(facilityLevels)),
                BuildRows(stadiumLevels, row => row.Build(), nameof(stadiumLevels)));
        }

        private static T Require<T>(T value, string name) where T : class =>
            value ?? throw new InvalidOperationException($"ClubOperation.{name}가 없습니다.");

        internal static TResult[] BuildRows<TData, TResult>(
            TData[] rows,
            Func<TData, TResult> build,
            string name) where TData : class
        {
            if (rows == null)
                throw new InvalidOperationException($"{name} 행이 없습니다.");
            var result = new TResult[rows.Length];
            for (int index = 0; index < rows.Length; index++)
            {
                TData row = rows[index] ?? throw new InvalidOperationException($"{name}에 null 행이 있습니다.");
                result[index] = build(row);
            }
            return result;
        }
    }

    [Serializable]
    internal sealed class StaffQualityBalanceData
    {
        public int qualityTier;
        public StaffSalaryBand salaryBand;
        public double effectBonus;
        public long baseAnnualSalary;
        public double marketWeight;

        public StaffQualityBalance Build() =>
            new StaffQualityBalance(qualityTier, salaryBand, effectBonus, baseAnnualSalary, marketWeight);
    }

    [Serializable]
    internal sealed class StaffSalaryBandBalanceData
    {
        public StaffSalaryBand salaryBand;
        public double salaryMultiplier;

        public StaffSalaryBandBalance Build() => new StaffSalaryBandBalance(salaryBand, salaryMultiplier);
    }

    [Serializable]
    internal sealed class StaffRoleBalanceData
    {
        public StaffRole role;
        public double effectMultiplier;
        public double salaryMultiplier;
        public StaffSpecialtyTag[] specialties;
        public double[] aiClubDnaWeights;

        public StaffRoleBalance Build() =>
            new StaffRoleBalance(role, effectMultiplier, salaryMultiplier, specialties, aiClubDnaWeights);
    }

    [Serializable]
    internal sealed class StaffSpecialtyBalanceData
    {
        public StaffSpecialtyTag specialty;
        public StaffRole role;
        public double effectBonus;

        public StaffSpecialtyBalance Build() => new StaffSpecialtyBalance(specialty, role, effectBonus);
    }

    [Serializable]
    internal sealed class StaffPhilosophyBalanceData
    {
        public StaffPhilosophyTag philosophy;
        public double[] effectBonusByRole;

        public StaffPhilosophyBalance Build() => new StaffPhilosophyBalance(philosophy, effectBonusByRole);
    }

    [Serializable]
    internal sealed class StaffMarketBalanceData
    {
        public int offseasonOfferCount;
        public int midseasonOfferCount;
        public int minimumContractYears;
        public int maximumContractYears;
        public int[] preferredContractYears;
        public double[] contractPreferenceWeights;
        public double[] marketSalaryMultipliers;
        public double minimumSalaryVariance;
        public double maximumSalaryVariance;
        public double signingCostRate;
        public double replacementPenaltyRate;
        public double leagueQualityBiasPerGrade;
        public long salaryRoundingUnit;

        public StaffMarketBalance Build() => new StaffMarketBalance(
            offseasonOfferCount,
            midseasonOfferCount,
            minimumContractYears,
            maximumContractYears,
            preferredContractYears,
            contractPreferenceWeights,
            marketSalaryMultipliers,
            minimumSalaryVariance,
            maximumSalaryVariance,
            signingCostRate,
            replacementPenaltyRate,
            leagueQualityBiasPerGrade,
            salaryRoundingUnit);
    }

    [Serializable]
    internal sealed class AiStaffBalanceData
    {
        public double[] gradeEffectBonus;
        public double managerQualityCoefficient;
        public double clubDnaCoefficient;
        public double seedVariance;

        public AiStaffBalance Build() => new AiStaffBalance(
            gradeEffectBonus,
            managerQualityCoefficient,
            clubDnaCoefficient,
            seedVariance);
    }

    [Serializable]
    internal sealed class StaffBalanceData
    {
        public StaffQualityBalanceData[] qualities;
        public StaffSalaryBandBalanceData[] salaryBands;
        public StaffRoleBalanceData[] roles;
        public StaffSpecialtyBalanceData[] specialties;
        public StaffPhilosophyBalanceData[] philosophies;
        public StaffMarketBalanceData market;
        public AiStaffBalanceData ai;
        public double maximumEffectBonus;
        public double maximumScoutingConfidenceModifier;

        public StaffBalanceTable Build() => new StaffBalanceTable(
            ClubOperationBalanceData.BuildRows(qualities, row => row.Build(), nameof(qualities)),
            ClubOperationBalanceData.BuildRows(salaryBands, row => row.Build(), nameof(salaryBands)),
            ClubOperationBalanceData.BuildRows(roles, row => row.Build(), nameof(roles)),
            ClubOperationBalanceData.BuildRows(specialties, row => row.Build(), nameof(specialties)),
            ClubOperationBalanceData.BuildRows(philosophies, row => row.Build(), nameof(philosophies)),
            market?.Build() ?? throw new InvalidOperationException("Staff.market이 없습니다."),
            ai?.Build() ?? throw new InvalidOperationException("Staff.ai가 없습니다."),
            maximumEffectBonus,
            maximumScoutingConfidenceModifier);
    }

    [Serializable]
    internal sealed class ScoutingConfidenceBalanceData
    {
        public double lowConfidenceThreshold;
        public double estimatedThreshold;
        public double highConfidenceThreshold;
        public double maximumInferredConfidence;
        public double maximumCombinedModifier;
        public double publicRosterEvidenceQuality;
        public double publicRosterRecencyFactor;
        public double publicRosterSampleFactor;
        public int bullpenFreshMaximumRecentPitches;
        public int bullpenTiredMinimumRecentPitches;
        public int bullpenVeryTiredMinimumRecentPitches;
        public int bullpenFreshMinimumRestDays;

        public ScoutingConfidenceDefinition Build() => new ScoutingConfidenceDefinition(
            lowConfidenceThreshold,
            estimatedThreshold,
            highConfidenceThreshold,
            maximumInferredConfidence,
            maximumCombinedModifier,
            publicRosterEvidenceQuality,
            publicRosterRecencyFactor,
            publicRosterSampleFactor,
            bullpenFreshMaximumRecentPitches,
            bullpenTiredMinimumRecentPitches,
            bullpenVeryTiredMinimumRecentPitches,
            bullpenFreshMinimumRestDays);
    }
}

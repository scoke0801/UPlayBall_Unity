using System;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 전용 세이브 루트 DTO이며 CareerState와 합성하지 않는다.</summary>
    [Serializable]
    public sealed class ManagerHistoricalSaveData
    {
        public int saveVersion;
        public HistoricalContentReferenceSaveData contentReference;
        public string playerTeamSeasonKey;
        public WorldIdentityRegistrySaveData identityRegistry;
        public WorldHistorySaveData worldHistory;
        public LeagueInstanceSaveData league;
        public CurrentRosterSaveData[] rosters;
        public OwnedPlayerCardSaveData[] ownedCards;
        public ManagerEconomySaveData economy;
        public ManagerModeSaveData managerMode;
    }

    [Serializable]
    public sealed class WorldIdentityRegistrySaveData
    {
        public string identityGeneratorVersion;
        public ulong identitySeed;
        public WorldPlayerIdentitySaveData[] players;
        public WorldFranchiseIdentitySaveData[] franchises;
    }

    [Serializable]
    public sealed class WorldPlayerIdentitySaveData
    {
        public string playerPersonId;
        public string displayName;
    }

    [Serializable]
    public sealed class WorldFranchiseIdentitySaveData
    {
        public string franchiseId;
        public string displayName;
    }

    [Serializable]
    public sealed class LeagueInstanceSaveData
    {
        public string leagueInstanceId;
        public int grade;
        public string[] regularTeamSeasonKeys;
        public SpecialCompositeTeamRegistrationSaveData[] specialCompositeTeams;
    }

    [Serializable]
    public sealed class SpecialCompositeTeamRegistrationSaveData
    {
        public string teamSeasonKey;
        public int originYear;
        public int teamType;
    }

    [Serializable]
    public sealed class CurrentRosterSaveData
    {
        public string teamSeasonKey;
        public ActiveRosterEntrySaveData[] entries;
    }

    [Serializable]
    public sealed class ActiveRosterEntrySaveData
    {
        public string cardId;
        public string playerSeasonId;
        public string playerPersonId;
        public int registrationType;
        public int role;
    }

    [Serializable]
    public sealed class OwnedPlayerCardSaveData
    {
        public string cardId;
        public int enhancementLevel;
        public int duplicateCount;
        public bool isLocked;
        public bool isFavorite;
        public int[] trainingBonuses;
    }

    [Serializable]
    public sealed class ManagerEconomySaveData
    {
        public long money;
        public int scoutingPoints;
        public int developmentPoints;
        public int pityGauge;
    }

    /// <summary>구단주 모드 확장 시스템의 원본 상태만 보관하는 v4 DTO다.</summary>
    [Serializable]
    public sealed class ManagerModeSaveData
    {
        public string staffCatalogVersion;
        public ulong staffCatalogSeed;
        public int staffCountPerRole;
        public ClubOperationSaveData clubOperation;
        public StaffContractSaveData[] staffContracts;
        public TeamStaffAssignmentSaveData staffAssignment;
        public LineupPresetSaveData[] lineupPresets;
        public string selectedLineupPresetId;
        public TeamSeasonPlayerStatusSaveData[] playerStatuses;
        public TeamChemistryFamiliaritySaveData[] familiarities;
        public ManagerLiveSeasonSaveData liveSeason;
    }

    [Serializable]
    public sealed class ClubOperationSaveData
    {
        public string teamSeasonKey;
        public double fanBase;
        public double popularity;
        public double attendanceMomentum;
        public int stadiumLevel;
        public int stadiumCapacity;
        public FacilitySaveData[] facilities;
        public int ticketPriceTier;
        public WeeklyOperationLedgerSaveData currentWeek;
        public SeasonFinanceSummarySaveData currentSeason;
        public OperationReceiptSaveData[] receipts;
    }

    [Serializable]
    public sealed class FacilitySaveData
    {
        public int type;
        public int level;
    }

    [Serializable]
    public sealed class WeeklyOperationLedgerSaveData
    {
        public string seasonId;
        public int weekIndex;
        public long moneyIncome;
        public long moneyExpense;
        public int scoutingPointProduction;
        public int developmentPointProduction;
        public int homeGames;
        public long attendance;
        public int receiptCount;
    }

    [Serializable]
    public sealed class SeasonFinanceSummarySaveData
    {
        public string seasonId;
        public int homeGames;
        public long attendance;
        public long ticketRevenue;
        public long fanShopRevenue;
        public long otherGameRevenue;
        public long gameOperatingCost;
        public long moneyIncome;
        public long moneyExpense;
        public int scoutingPointProduction;
        public int developmentPointProduction;
    }

    [Serializable]
    public sealed class OperationReceiptSaveData
    {
        public string receiptId;
        public int kind;
        public string seasonId;
        public int weekIndex;
        public string sourceId;
        public long money;
        public int scoutingPoints;
        public int developmentPoints;
    }

    [Serializable]
    public sealed class StaffContractSaveData
    {
        public string contractId;
        public string staffId;
        public string teamSeasonKey;
        public int startSeason;
        public int remainingSeasons;
        public long annualSalary;
        public bool hasLastSalaryPaidSeason;
        public int lastSalaryPaidSeason;
    }

    [Serializable]
    public sealed class TeamStaffAssignmentSaveData
    {
        public string teamSeasonKey;
        public string hittingCoachStaffId;
        public string pitchingCoachStaffId;
        public string developmentCoachStaffId;
        public string conditioningCoachStaffId;
        public string scoutingDirectorStaffId;
    }

    [Serializable]
    public sealed class LineupPresetSaveData
    {
        public string presetId;
        public string name;
        public LineupPresetSlotSaveData[] startingLineupSlots;
        public string[] battingOrderCardIds;
        public string[] benchPriorityCardIds;
        public string[] starterRotationCardIds;
        public string[] bullpenAssignmentCardIds;
        public string setupPitcherCardId;
        public string closerPitcherCardId;
        public string[] teamColorIds;
        public string[] defaultTacticCardIds;
    }

    [Serializable]
    public sealed class LineupPresetSlotSaveData
    {
        public string cardId;
        public int position;
    }

    [Serializable]
    public sealed class TeamSeasonPlayerStatusSaveData
    {
        public string teamSeasonKey;
        public PlayerStatusSaveData[] players;
    }

    [Serializable]
    public sealed class PlayerStatusSaveData
    {
        public string playerPersonId;
        public int storedBaseCondition;
        public int availability;
        public int previousDayPitches;
        public int twoDaysAgoPitches;
        public int threeDaysAgoPitches;
    }

    [Serializable]
    public sealed class TeamChemistryFamiliaritySaveData
    {
        public string teamSeasonKey;
        public ChemistryFamiliarityEntrySaveData[] entries;
    }

    [Serializable]
    public sealed class ChemistryFamiliarityEntrySaveData
    {
        public string firstPlayerPersonId;
        public string secondPlayerPersonId;
        public int lineupFamiliarity;
        public int batteryFamiliarity;
    }

    [Serializable]
    public sealed class ManagerLiveSeasonSaveData
    {
        public string seasonId;
        public int seasonNumber;
        public int originYear;
        public int currentWeekIndex;
        public int playerTeamId;
        public ManagerTeamReferenceSaveData[] teams;
        public ManagerScheduledGameSaveData[] games;
    }

    [Serializable]
    public sealed class ManagerTeamReferenceSaveData
    {
        public int teamId;
        public string teamSeasonKey;
    }

    [Serializable]
    public sealed class ManagerScheduledGameSaveData
    {
        public int gameId;
        public int round;
        public ulong randomSeed;
        public int awayTeamId;
        public int homeTeamId;
        public bool isCompleted;
        public int awayRuns;
        public int homeRuns;
        public bool hasPlayerRolePlan;
        public int plannedPlayerRole;
        public bool hasPlayerRoleDecision;
        public int playerRoleDecisionReason;
        public double conditionAdjustment;
        public double managerEvaluationAdjustment;
        public double decisionScore;
        public double requiredScore;
    }
}

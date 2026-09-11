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
        public OwnerLeagueWorldSaveData leagueWorld;
        public CurrentRosterSaveData[] rosters;
        public OwnedPlayerCardSaveData[] ownedCards;
        public CardCollectionHistorySaveData cardCollectionHistory;
        public WishlistSaveData wishlist;
        public SpecialCardTransactionSaveData[] specialCardTransactions;
        public ManagerEconomySaveData economy;
        public ManagerModeSaveData managerMode;
        public TacticCollectionSaveData tacticCollection;
        public ShopPurchaseHistorySaveData shopPurchaseHistory;
        public Baseball.Game.Guide.GuideRepeatStateData guideRepeatState;
        public Baseball.Game.Guide.GuideProgressData guideProgress;
        public OwnerProfileSaveData ownerProfile;
        public OwnerNewGameReceiptSaveData newGameReceipt;
        public OwnerOnboardingSaveData onboarding;
        public OwnerPlayerGrowthSaveData playerGrowth;
    }

    [Serializable]
    public sealed class CardCollectionHistorySaveData
    {
        public string[] everAcquiredCardIds;
    }

    [Serializable]
    public sealed class WishlistSaveData
    {
        public WishlistEntrySaveData[] entries;
        public long nextAddedSequence;
    }

    [Serializable]
    public sealed class WishlistEntrySaveData
    {
        public string cardId;
        public long addedSequence;
    }

    [Serializable]
    public sealed class OwnerProfileSaveData
    {
        public string clubName;
        public string nickname;
        public string frontManagerId;
    }

    [Serializable]
    public sealed class OwnerNewGameReceiptSaveData
    {
        public string[] mainCardIds;
        public string[] fillerCardIds;
        public int fillerRerollCount;
        public ulong starterRosterSeed;
    }

    [Serializable]
    public sealed class OwnerOnboardingSaveData
    {
        public int currentStep;
        public bool isCompleted;
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
        public bool isPooledGroup;
        public string leagueInstanceId;
        public int grade;
        public string[] regularTeamSeasonKeys;
        public SpecialCompositeTeamRegistrationSaveData[] specialCompositeTeams;
        public string[] fillerTeamSeasonKeys;
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
        public int[] studyBonuses;
        public OwnerPlacedSkillBlockSaveData[] skillBoard;
        public int lastStudySeason;
    }

    [Serializable]
    public sealed class OwnerLeagueWorldSaveData
    {
        public OwnerLeagueGroupSaveData[] groups;
        public OwnerLeagueGroupSaveData[] completedGroups;
        public OwnerPostseasonSaveData playerPostseason;
        public CurrentRosterSaveData[] rosters;
        public OwnerLeaguePlayerIdSaveData[] playerIds;
    }

    [Serializable]
    public sealed class OwnerLeaguePlayerIdSaveData
    {
        public string key;
        public int playerId;
    }

    [Serializable]
    public sealed class OwnerLeagueGroupSaveData
    {
        public LeagueInstanceSaveData league;
        public ManagerLiveSeasonSaveData season;
        public OwnerPostseasonSaveData postseason;
    }

    [Serializable]
    public sealed class OwnerPostseasonSaveData
    {
        public string seasonId;
        public int[] seedTeamIds;
        public OwnerPostseasonSeriesSaveData[] series;
    }

    [Serializable]
    public sealed class OwnerPostseasonSeriesSaveData
    {
        public string seriesId;
        public int round;
        public int higherSeedTeamId;
        public int lowerSeedTeamId;
        public int seriesGames;
        public int higherSeedWins;
        public int lowerSeedWins;
        public ManagerScheduledGameSaveData[] games;
    }

    [Serializable]
    public sealed class OwnerPlayerGrowthSaveData
    {
        public OwnerSkillBlockInventorySaveData inventory;
        public CardStudyProjectSaveData[] studyProjects;
    }

    [Serializable]
    public sealed class OwnerSkillBlockInventorySaveData
    {
        public OwnerSkillBlockInstanceSaveData[] blocks;
        public int pityEliteCount;
        public int pityUniqueCount;
        public int pityLegendaryCount;
        public int totalPullCount;
    }

    [Serializable]
    public sealed class OwnerSkillBlockInstanceSaveData
    {
        public int instanceId;
        public string definitionId;
    }

    [Serializable]
    public sealed class OwnerPlacedSkillBlockSaveData
    {
        public int instanceId;
        public string definitionId;
        public int originX;
        public int originY;
        public int rotationQuarterTurns;
    }

    [Serializable]
    public sealed class CardStudyProjectSaveData
    {
        public string cardId;
        public string programId;
        public int startedSeason;
        public int remainingWeeks;
    }

    [Serializable]
    public sealed class ManagerEconomySaveData
    {
        public long money;
        public long contractArrears;
        public int scoutingPoints;
        public int developmentPoints;
        public int pityGauge;
    }

    [Serializable]
    public sealed class TacticCollectionSaveData
    {
        public TacticCollectionEntrySaveData[] entries;
    }

    [Serializable]
    public sealed class TacticCollectionEntrySaveData
    {
        public string cardId;
        public int count;
    }

    [Serializable]
    public sealed class ShopPurchaseHistorySaveData
    {
        public int totalPurchaseCount;
        public ShopPurchaseCountSaveData[] entries;
    }

    [Serializable]
    public sealed class ShopPurchaseCountSaveData
    {
        public string productId;
        public int count;
    }

    /// <summary>구단주 모드 확장 시스템의 원본 상태만 보관하는 DTO다.</summary>
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
        public ManagerCompletedSeasonSaveData[] completedSeasons;
        public DugoutManagementSaveData dugout;
        public OwnerPlayerContractSaveData[] playerContracts;
    }

    [Serializable]
    public sealed class OwnerPlayerContractSaveData
    {
        public string contractId;
        public string cardId;
        public int startSeason;
        public int remainingSeasons;
        public long annualSalary;
        public bool hasLastSalaryPaidSeason;
        public int lastSalaryPaidSeason;
    }

    /// <summary>운영 이력을 생성 당시 WorldHistory와 분리해 저장하는 완료 시즌 DTO다.</summary>
    [Serializable]
    public sealed class ManagerCompletedSeasonSaveData
    {
        public int leagueGrade;
        public ManagerLiveSeasonSaveData season;
    }

    [Serializable]
    public sealed class DugoutManagementSaveData
    {
        public string managerId;
        public string headCoachId;
        public int managerTrust;
        public int battingApproach;
        public int runningAggression;
        public int smallBallPreference;
        public int pinchHitAggression;
        public int hookSpeed;
        public int bullpenAggression;
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
        public LeagueSeasonStatisticsSaveData statistics;
    }

    [Serializable]
    public sealed class LeagueSeasonStatisticsSaveData
    {
        public int schemaVersion;
        public PlayerSeasonRecordSaveData[] regularSeason;
        public PlayerSeasonRecordSaveData[] postseason;
    }

    [Serializable]
    public sealed class PlayerSeasonRecordSaveData
    {
        public int playerId;
        public string playerName;
        public int teamId;
        public int primaryPosition;
        public int teamGames;
        public BattingRecordSaveData batting;
        public PitchingRecordSaveData pitching;
        public FieldingRecordSaveData[] fielding;
    }

    [Serializable]
    public sealed class BattingRecordSaveData
    {
        public int games;
        public int gamesStarted;
        public int plateAppearances;
        public int atBats;
        public int runs;
        public int hits;
        public int doubles;
        public int triples;
        public int homeRuns;
        public int runsBattedIn;
        public int walks;
        public int hitByPitches;
        public int strikeouts;
        public int stolenBases;
        public int caughtStealing;
        public int sacrificeBunts;
        public int sacrificeFlies;
        public int intentionalWalks;
        public int reachedOnErrors;
        public int groundedIntoDoublePlays;
    }

    [Serializable]
    public sealed class PitchingRecordSaveData
    {
        public int appearances;
        public int starts;
        public int outsRecorded;
        public int pitchesThrown;
        public int wins;
        public int losses;
        public int saves;
        public int holds;
        public int blownSaves;
        public int hitsAllowed;
        public int homeRunsAllowed;
        public int walksAllowed;
        public int hitBatters;
        public int strikeouts;
        public int runsAllowed;
        public int earnedRuns;
        public int battersFaced;
        public int inheritedRunners;
        public int inheritedRunnersScored;
        public int qualityStarts;
    }

    [Serializable]
    public sealed class FieldingRecordSaveData
    {
        public int position;
        public int defensiveOuts;
        public int opportunities;
        public int successfulPlays;
        public int putouts;
        public int assists;
        public int errors;
        public int doublePlays;
        public int difficultPlayAttempts;
        public int difficultPlaysMade;
        public double expectedOuts;
        public double estimatedRunsSaved;
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
        public bool hasTacticPlan;
        public string[] plannedTacticCardIds;
    }
}

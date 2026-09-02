using System;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 전용 세이브 루트 DTO이며 CareerState와 합성하지 않는다.</summary>
    [Serializable]
    public sealed class ManagerHistoricalSaveData
    {
        public int saveVersion;
        public string playerTeamSeasonKey;
        public WorldHistorySaveData worldHistory;
        public WorldCardCatalogSaveData worldCardCatalog;
        public LeagueInstanceSaveData league;
        public CurrentRosterSaveData[] rosters;
        public OwnedPlayerCardSaveData[] ownedCards;
        public ManagerEconomySaveData economy;
    }

    [Serializable]
    public sealed class WorldHistorySaveData
    {
        public int recordMode;
        public ulong worldHistorySeed;
        public SeasonStatisticsSaveData[] statistics;
        public WorldAwardEntrySaveData[] awards;
    }

    [Serializable]
    public sealed class SeasonStatisticsSaveData
    {
        public string playerSeasonId;
        public string teamSeasonKey;
        public int seasonYear;
        public int position;
        public int plateAppearances;
        public int hits;
        public int homeRuns;
        public int walks;
        public int strikeouts;
        public int stolenBases;
        public int pitchingOuts;
        public int earnedRuns;
        public int pitchingStrikeouts;
        public int defensiveChances;
        public int defensiveOutsAboveAverage;
        public int fieldingErrors;
        public bool isFirstHalf;
        public bool isPostseason;
        public bool isAllStarGame;
    }

    [Serializable]
    public sealed class WorldAwardEntrySaveData
    {
        public int seasonYear;
        public int awardType;
        public string playerSeasonId;
        public int position;
    }

    [Serializable]
    public sealed class WorldCardCatalogSaveData
    {
        public PlayerSeasonSaveData[] playerSeasons;
        public PlayerCardSaveData[] cards;
    }

    [Serializable]
    public sealed class PlayerSeasonSaveData
    {
        public string playerSeasonId;
        public string playerPersonId;
        public int originYear;
        public string originFranchiseId;
        public string originTeamSeasonKey;
        public int position;
        public int pitcherRole;
        public int playerType;
        public int registrationType;
        public int[] baseAttributes;
        public int cost;
        public int[] trainingCeiling;
    }

    [Serializable]
    public sealed class PlayerCardSaveData
    {
        public string cardId;
        public string playerSeasonId;
        public int edition;
        public int[] editionStatModifiers;
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
}

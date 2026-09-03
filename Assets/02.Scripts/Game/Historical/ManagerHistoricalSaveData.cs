using System;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 전용 세이브 루트 DTO이며 CareerState와 합성하지 않는다.</summary>
    [Serializable]
    public sealed class ManagerHistoricalSaveData
    {
        public int saveVersion;
        public HistoricalContentReferenceSaveData contentReference;
        public string playerTeamSeasonKey;
        public WorldHistorySaveData worldHistory;
        public LeagueInstanceSaveData league;
        public CurrentRosterSaveData[] rosters;
        public OwnedPlayerCardSaveData[] ownedCards;
        public ManagerEconomySaveData economy;
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

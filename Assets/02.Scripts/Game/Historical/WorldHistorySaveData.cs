using System;

namespace Baseball.Game.Historical
{
    /// <summary>두 게임 모드가 공유하는 게임 시작 이전 World History 저장 DTO다.</summary>
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
}

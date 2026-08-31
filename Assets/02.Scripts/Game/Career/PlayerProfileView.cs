using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>선수 상세 화면에 표시할 한 능력치의 현재값과 성장 근거다.</summary>
    public readonly struct PlayerProfileAbilityView
    {
        public PlayerProfileAbilityView(
            PlayerAbility ability,
            int baseValue,
            int stableValue,
            int boardBonus,
            int potential)
        {
            Ability = ability;
            BaseValue = baseValue;
            StableValue = stableValue;
            BoardBonus = boardBonus;
            Potential = potential;
        }

        public PlayerAbility Ability { get; }
        public int BaseValue { get; }
        public int StableValue { get; }
        public int BoardBonus { get; }
        public int Potential { get; }
        public int GrowthRoom => Math.Max(0, Potential - BaseValue);
    }

    /// <summary>선수 화면이 원본 누적기를 노출하지 않고 표시할 현재 시즌 기록 스냅샷이다.</summary>
    public readonly struct PlayerProfileStatisticsView
    {
        public PlayerProfileStatisticsView(PlayerSeasonStatisticsState statistics)
        {
            GamesPlayed = statistics?.GamesPlayed ?? 0;
            AtBats = statistics?.AtBats ?? 0;
            Hits = statistics?.Hits ?? 0;
            HomeRuns = statistics?.HomeRuns ?? 0;
            RunsBattedIn = statistics?.RunsBattedIn ?? 0;
            BattingAverage = statistics?.BattingAverage ?? 0d;
            OnBasePlusSlugging = statistics?.OnBasePlusSlugging ?? 0d;
            PitchingAppearances = statistics?.PitchingAppearances ?? 0;
            PitchingStarts = statistics?.PitchingStarts ?? 0;
            Wins = statistics?.Wins ?? 0;
            Losses = statistics?.Losses ?? 0;
            Saves = statistics?.Saves ?? 0;
            OutsRecorded = statistics?.OutsRecorded ?? 0;
            PitchingStrikeouts = statistics?.PitchingStrikeouts ?? 0;
            EarnedRunAverage = statistics?.EarnedRunAverage ?? 0d;
            WalksHitsPerInningPitched = statistics?.WalksHitsPerInningPitched ?? 0d;
        }

        public int GamesPlayed { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public double BattingAverage { get; }
        public double OnBasePlusSlugging { get; }
        public int PitchingAppearances { get; }
        public int PitchingStarts { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Saves { get; }
        public int OutsRecorded { get; }
        public int PitchingStrikeouts { get; }
        public double EarnedRunAverage { get; }
        public double WalksHitsPerInningPitched { get; }
    }

    /// <summary>선수 상세 화면이 한 번의 Render에서 소비하는 읽기 전용 값 모음이다.</summary>
    public sealed class PlayerProfileView
    {
        public int PlayerId { get; internal set; }
        public string PlayerName { get; internal set; }
        public string Nationality { get; internal set; }
        public int Age { get; internal set; }
        public PlayerType PlayerType { get; internal set; }
        public PlayerPosition Position { get; internal set; }
        public Handedness BattingHand { get; internal set; }
        public Handedness ThrowingHand { get; internal set; }
        public string TeamName { get; internal set; }
        public TeamColor TeamColor { get; internal set; }
        public int SeasonYear { get; internal set; }
        public LeagueLevel LeagueLevel { get; internal set; }
        public int Overall { get; internal set; }
        public int Condition { get; internal set; }
        public int Fatigue { get; internal set; }
        public int ManagerEvaluation { get; internal set; }
        public int Durability { get; internal set; }
        public WorkEthicGrade WorkEthic { get; internal set; }
        public CareerPhase CareerPhase { get; internal set; }
        public int InjuryHistoryCount { get; internal set; }
        public int JoinedYear { get; internal set; }
        public int ProfessionalYears { get; internal set; }
        public int ContractEndYear { get; internal set; }
        public long AnnualSalary { get; internal set; }
        public ExpectedRole ExpectedRole { get; internal set; }
        public PlayerGameRole PlannedRole { get; internal set; }
        public PlayerProfileAbilityView[] Abilities { get; internal set; }
        public GrowthBoardCellView[] BoardCells { get; internal set; }
        public GrowthSkillBlockView[] OwnedBlocks { get; internal set; }
        public GrowthSkillBlockView[] PlacedBlocks { get; internal set; }
        public GrowthBoardLayoutPlacement[] AppliedLayout { get; internal set; }
        public PlayerProfileStatisticsView SeasonStatistics { get; internal set; }
        public CareerRecordMetricValue[] CareerTotals { get; internal set; }
        public PlayerGameLogState[] RecentGames { get; internal set; }
    }
}

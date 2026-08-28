using System;

namespace Baseball.Game.Career
{
    public enum CareerRecordsPage
    {
        Personal,
        Season,
        Career,
        Awards,
        Highlights
    }

    public enum CareerRecordCategory
    {
        Batting,
        Pitching,
        Fielding,
        Baserunning
    }

    public enum CareerRecordMetric
    {
        Games,
        AtBats,
        Runs,
        Hits,
        Doubles,
        Triples,
        HomeRuns,
        RunsBattedIn,
        Walks,
        BattingStrikeouts,
        BattingAverage,
        OnBasePercentage,
        SluggingPercentage,
        OnBasePlusSlugging,
        PitchingAppearances,
        PitchingStarts,
        OutsRecorded,
        Wins,
        Losses,
        Saves,
        Holds,
        HitsAllowed,
        EarnedRuns,
        WalksAllowed,
        PitchingStrikeouts,
        EarnedRunAverage,
        WalksHitsPerInningPitched,
        FieldingOpportunities,
        SuccessfulFieldingPlays,
        Putouts,
        Assists,
        Errors,
        DoublePlays,
        EstimatedRunsSaved,
        FieldingSuccessRate,
        StolenBases,
        CaughtStealing,
        StolenBasePercentage
    }

    /// <summary>한 화면 지표의 값과 규정 자격 내 순위를 함께 전달한다.</summary>
    public readonly struct CareerRecordMetricValue
    {
        public CareerRecordMetricValue(CareerRecordMetric metric, double value, int rank = 0)
        {
            Metric = metric;
            Value = value;
            Rank = rank;
        }

        public CareerRecordMetric Metric { get; }
        public double Value { get; }
        public int Rank { get; }
        public bool HasRank => Rank > 0;
    }

    /// <summary>현재 시즌 리그 기록표의 선수 한 줄이다.</summary>
    public readonly struct CareerRecordLeaderboardRow
    {
        public CareerRecordLeaderboardRow(
            int rank,
            int playerId,
            string playerName,
            int teamId,
            string teamName,
            bool isMyPlayer,
            CareerRecordMetricValue[] metrics)
        {
            Rank = rank;
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            TeamId = teamId;
            TeamName = teamName ?? string.Empty;
            IsMyPlayer = isMyPlayer;
            Metrics = metrics ?? Array.Empty<CareerRecordMetricValue>();
        }

        public int Rank { get; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public bool IsMyPlayer { get; }
        public CareerRecordMetricValue[] Metrics { get; }
    }

    /// <summary>현재 시즌과 완료 시즌을 같은 형식으로 비교하는 선수 시즌 한 줄이다.</summary>
    public readonly struct CareerRecordSeasonRow
    {
        public CareerRecordSeasonRow(
            int year,
            LeagueLevel leagueLevel,
            string teamName,
            bool isCurrent,
            CareerRecordMetricValue[] metrics)
        {
            Year = year;
            LeagueLevel = leagueLevel;
            TeamName = teamName ?? string.Empty;
            IsCurrent = isCurrent;
            Metrics = metrics ?? Array.Empty<CareerRecordMetricValue>();
        }

        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public string TeamName { get; }
        public bool IsCurrent { get; }
        public CareerRecordMetricValue[] Metrics { get; }
    }

    /// <summary>여러 시즌에 걸친 대표 지표의 실제 변화 한 점이다.</summary>
    public readonly struct CareerRecordTrendPoint
    {
        public CareerRecordTrendPoint(int year, double value, bool isCurrent)
        {
            Year = year;
            Value = value;
            IsCurrent = isCurrent;
        }

        public int Year { get; }
        public double Value { get; }
        public bool IsCurrent { get; }
    }

    /// <summary>내 선수가 실제로 받은 한 시즌 수상 기록이다.</summary>
    public readonly struct CareerAwardRecordView
    {
        public CareerAwardRecordView(
            int year,
            LeagueLevel leagueLevel,
            AwardCategory category,
            Baseball.Core.Players.PlayerPosition position,
            bool isCurrent)
        {
            Year = year;
            LeagueLevel = leagueLevel;
            Category = category;
            Position = position;
            IsCurrent = isCurrent;
        }

        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public AwardCategory Category { get; }
        public Baseball.Core.Players.PlayerPosition Position { get; }
        public bool IsCurrent { get; }
    }

    /// <summary>최근 경기에서 내 선수에게 발생한 기록 하이라이트 한 줄이다.</summary>
    public readonly struct CareerRecordHighlightView
    {
        public CareerRecordHighlightView(PlayerGameLogState game, string opponentName)
        {
            Game = game;
            OpponentName = opponentName ?? string.Empty;
        }

        public PlayerGameLogState Game { get; }
        public string OpponentName { get; }
    }

    /// <summary>한 시즌 안에서 특정 구단 소속으로 쌓은 분할 기록 한 줄이다.</summary>
    public readonly struct CareerTeamStatisticsSplitView
    {
        public CareerTeamStatisticsSplitView(
            int year,
            int teamId,
            string teamName,
            int teamGames,
            bool isCurrentSeason,
            CareerRecordMetricValue[] metrics)
        {
            Year = year;
            TeamId = teamId;
            TeamName = teamName ?? string.Empty;
            TeamGames = teamGames;
            IsCurrentSeason = isCurrentSeason;
            Metrics = metrics ?? Array.Empty<CareerRecordMetricValue>();
        }

        public int Year { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public int TeamGames { get; }
        public bool IsCurrentSeason { get; }
        public CareerRecordMetricValue[] Metrics { get; }
    }

    /// <summary>선수 커리어 기록 화면에 표시할 확정 트레이드 이력 한 줄이다.</summary>
    public readonly struct CareerTradeHistoryView
    {
        public CareerTradeHistoryView(
            int year,
            int gameIndex,
            string previousTeamName,
            string newTeamName,
            Baseball.Core.Teams.ExpectedRole previousRole,
            Baseball.Core.Teams.ExpectedRole projectedRole)
        {
            Year = year;
            GameIndex = gameIndex;
            PreviousTeamName = previousTeamName ?? string.Empty;
            NewTeamName = newTeamName ?? string.Empty;
            PreviousRole = previousRole;
            ProjectedRole = projectedRole;
        }

        public int Year { get; }
        public int GameIndex { get; }
        public string PreviousTeamName { get; }
        public string NewTeamName { get; }
        public Baseball.Core.Teams.ExpectedRole PreviousRole { get; }
        public Baseball.Core.Teams.ExpectedRole ProjectedRole { get; }
    }

    /// <summary>기록 화면 전체가 한 번의 Render에서 소비할 읽기 전용 값 모음이다.</summary>
    public sealed class CareerRecordsView
    {
        public int SeasonYear { get; internal set; }
        public LeagueLevel LeagueLevel { get; internal set; }
        public string PlayerName { get; internal set; }
        public CareerRecordCategory Category { get; internal set; }
        public CareerRecordMetric PrimaryMetric { get; internal set; }
        public CareerRecordMetric[] LeaderboardColumns { get; internal set; }
        public CareerRecordLeaderboardRow[] Leaderboard { get; internal set; }
        public CareerRecordMetricValue[] MyRecordMetrics { get; internal set; }
        public CareerRecordSeasonRow[] Seasons { get; internal set; }
        public CareerRecordMetricValue[] CareerTotals { get; internal set; }
        public CareerRecordTrendPoint[] Trend { get; internal set; }
        public CareerAwardRecordView[] Awards { get; internal set; }
        public CareerRecordHighlightView[] Highlights { get; internal set; }
        public CareerTeamStatisticsSplitView[] TeamSplits { get; internal set; }
        public CareerTradeHistoryView[] TradeHistory { get; internal set; }
        public bool IsMyPlayerQualified { get; internal set; }
        public int QualifiedPlayerCount { get; internal set; }
    }
}

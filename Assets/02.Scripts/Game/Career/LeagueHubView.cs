using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>리그 타자 순위표에서 선택할 기록 부문이다.</summary>
    public enum LeagueBattingCategory
    {
        BattingAverage,
        HomeRuns,
        RunsBattedIn,
        StolenBases,
        OnBasePlusSlugging
    }

    /// <summary>리그 투수 순위표에서 선택할 기록 부문이다.</summary>
    public enum LeaguePitchingCategory
    {
        EarnedRunAverage,
        Wins,
        Saves,
        Strikeouts,
        WalksHitsPerInningPitched
    }

    /// <summary>리그 화면의 구단 비교 지표다.</summary>
    public enum LeagueTeamMetric
    {
        BattingAverage,
        HomeRuns,
        EarnedRunAverage,
        Strikeouts
    }

    /// <summary>최근 경기 결과와 연승·연패 상태를 공통으로 표현한다.</summary>
    public enum TeamGameOutcome
    {
        Win,
        Loss,
        Tie
    }

    public enum LeagueStandingZone
    {
        Promotion,
        PostseasonRetention,
        Retention,
        Relegation
    }

    /// <summary>리그 순위표 한 구단의 확정된 표시 값이다.</summary>
    public readonly struct LeagueStandingView
    {
        public LeagueStandingView(
            int rank,
            int teamId,
            string teamName,
            TeamColor teamColor,
            int gamesPlayed,
            int wins,
            int losses,
            int ties,
            double winningPercentage,
            double gamesBehind,
            TeamGameOutcome? streakOutcome,
            int streakLength,
            TeamGameOutcome[] recentForm,
            bool isPostseasonPosition,
            bool isMyTeam,
            LeagueStandingZone zone = LeagueStandingZone.Retention,
            int emblemId = 0)
        {
            Rank = rank;
            TeamId = teamId;
            TeamName = teamName;
            TeamColor = teamColor;
            GamesPlayed = gamesPlayed;
            Wins = wins;
            Losses = losses;
            Ties = ties;
            WinningPercentage = winningPercentage;
            GamesBehind = gamesBehind;
            StreakOutcome = streakOutcome;
            StreakLength = streakLength;
            RecentForm = recentForm ?? Array.Empty<TeamGameOutcome>();
            IsPostseasonPosition = isPostseasonPosition;
            IsMyTeam = isMyTeam;
            Zone = zone;
            EmblemId = emblemId;
        }

        public int Rank { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public TeamColor TeamColor { get; }
        public int GamesPlayed { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public double WinningPercentage { get; }
        public double GamesBehind { get; }
        public TeamGameOutcome? StreakOutcome { get; }
        public int StreakLength { get; }
        public IReadOnlyList<TeamGameOutcome> RecentForm { get; }
        public bool IsPostseasonPosition { get; }
        public bool IsMyTeam { get; }
        public LeagueStandingZone Zone { get; }
        public int EmblemId { get; }
    }

    /// <summary>타자 리더보드 한 줄에 필요한 전체 시즌 기록이다.</summary>
    public readonly struct LeagueBattingLeaderView
    {
        public LeagueBattingLeaderView(
            int rank,
            int playerId,
            string playerName,
            int teamId,
            string teamName,
            PlayerPosition position,
            int games,
            int plateAppearances,
            double battingAverage,
            int homeRuns,
            int runsBattedIn,
            int stolenBases,
            double onBasePlusSlugging,
            bool isMyPlayer)
        {
            Rank = rank;
            PlayerId = playerId;
            PlayerName = playerName;
            TeamId = teamId;
            TeamName = teamName;
            Position = position;
            Games = games;
            PlateAppearances = plateAppearances;
            BattingAverage = battingAverage;
            HomeRuns = homeRuns;
            RunsBattedIn = runsBattedIn;
            StolenBases = stolenBases;
            OnBasePlusSlugging = onBasePlusSlugging;
            IsMyPlayer = isMyPlayer;
        }

        public int Rank { get; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public PlayerPosition Position { get; }
        public int Games { get; }
        public int PlateAppearances { get; }
        public double BattingAverage { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public int StolenBases { get; }
        public double OnBasePlusSlugging { get; }
        public bool IsMyPlayer { get; }
    }

    /// <summary>투수 리더보드 한 줄에 필요한 전체 시즌 기록이다.</summary>
    public readonly struct LeaguePitchingLeaderView
    {
        public LeaguePitchingLeaderView(
            int rank,
            int playerId,
            string playerName,
            int teamId,
            string teamName,
            PlayerPosition position,
            int appearances,
            int outsRecorded,
            int wins,
            int losses,
            int saves,
            int strikeouts,
            double earnedRunAverage,
            double walksHitsPerInningPitched,
            bool isMyPlayer)
        {
            Rank = rank;
            PlayerId = playerId;
            PlayerName = playerName;
            TeamId = teamId;
            TeamName = teamName;
            Position = position;
            Appearances = appearances;
            OutsRecorded = outsRecorded;
            Wins = wins;
            Losses = losses;
            Saves = saves;
            Strikeouts = strikeouts;
            EarnedRunAverage = earnedRunAverage;
            WalksHitsPerInningPitched = walksHitsPerInningPitched;
            IsMyPlayer = isMyPlayer;
        }

        public int Rank { get; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public PlayerPosition Position { get; }
        public int Appearances { get; }
        public int OutsRecorded { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Saves { get; }
        public int Strikeouts { get; }
        public double EarnedRunAverage { get; }
        public double WalksHitsPerInningPitched { get; }
        public bool IsMyPlayer { get; }
    }

    /// <summary>선택한 타격 부문의 상위권과 내 선수 위치를 함께 전달한다.</summary>
    public sealed class LeagueBattingLeaderboardView
    {
        public LeagueBattingLeaderboardView(
            LeagueBattingCategory category,
            LeagueBattingLeaderView[] leaders,
            LeagueBattingLeaderView? myPlayer)
        {
            Category = category;
            Leaders = leaders ?? Array.Empty<LeagueBattingLeaderView>();
            MyPlayer = myPlayer;
        }

        public LeagueBattingCategory Category { get; }
        public IReadOnlyList<LeagueBattingLeaderView> Leaders { get; }
        public LeagueBattingLeaderView? MyPlayer { get; }
    }

    /// <summary>선택한 투수 부문의 상위권과 내 선수 위치를 함께 전달한다.</summary>
    public sealed class LeaguePitchingLeaderboardView
    {
        public LeaguePitchingLeaderboardView(
            LeaguePitchingCategory category,
            LeaguePitchingLeaderView[] leaders,
            LeaguePitchingLeaderView? myPlayer)
        {
            Category = category;
            Leaders = leaders ?? Array.Empty<LeaguePitchingLeaderView>();
            MyPlayer = myPlayer;
        }

        public LeaguePitchingCategory Category { get; }
        public IReadOnlyList<LeaguePitchingLeaderView> Leaders { get; }
        public LeaguePitchingLeaderView? MyPlayer { get; }
    }

    /// <summary>리그 최고·평균·내 구단 값을 같은 축에서 비교한다.</summary>
    public readonly struct LeagueTeamMetricView
    {
        public LeagueTeamMetricView(
            LeagueTeamMetric metric,
            bool hasData,
            string bestTeamName,
            double bestValue,
            double leagueAverage,
            double myTeamValue,
            int myTeamRank)
        {
            Metric = metric;
            HasData = hasData;
            BestTeamName = bestTeamName;
            BestValue = bestValue;
            LeagueAverage = leagueAverage;
            MyTeamValue = myTeamValue;
            MyTeamRank = myTeamRank;
        }

        public LeagueTeamMetric Metric { get; }
        public bool HasData { get; }
        public string BestTeamName { get; }
        public double BestValue { get; }
        public double LeagueAverage { get; }
        public double MyTeamValue { get; }
        public int MyTeamRank { get; }
    }

    /// <summary>리그 화면의 최근 결과와 다음 라운드 대진 한 줄이다.</summary>
    public readonly struct LeagueScheduleGameView
    {
        public LeagueScheduleGameView(
            int gameId,
            int round,
            DateTime date,
            int awayTeamId,
            string awayTeamName,
            int homeTeamId,
            string homeTeamName,
            bool isCompleted,
            int awayRuns,
            int homeRuns,
            bool includesMyTeam)
        {
            GameId = gameId;
            Round = round;
            Date = date;
            AwayTeamId = awayTeamId;
            AwayTeamName = awayTeamName;
            HomeTeamId = homeTeamId;
            HomeTeamName = homeTeamName;
            IsCompleted = isCompleted;
            AwayRuns = awayRuns;
            HomeRuns = homeRuns;
            IncludesMyTeam = includesMyTeam;
        }

        public int GameId { get; }
        public int Round { get; }
        public DateTime Date { get; }
        public int AwayTeamId { get; }
        public string AwayTeamName { get; }
        public int HomeTeamId { get; }
        public string HomeTeamName { get; }
        public bool IsCompleted { get; }
        public int AwayRuns { get; }
        public int HomeRuns { get; }
        public bool IncludesMyTeam { get; }
    }

    /// <summary>리그 탭이 한 번의 상태 스냅샷으로 그릴 모든 읽기 전용 정보를 묶는다.</summary>
    public sealed class LeagueHubView
    {
        private readonly LeagueBattingLeaderboardView[] _battingLeaderboards;
        private readonly LeaguePitchingLeaderboardView[] _pitchingLeaderboards;

        public LeagueHubView(
            int seasonYear,
            LeagueLevel leagueLevel,
            SeasonPhase seasonPhase,
            DateTime currentDate,
            int gamesPlayedPerTeam,
            int regularSeasonGamesPerTeam,
            int playoffTeamCount,
            int myTeamId,
            string myTeamName,
            int myPlayerId,
            LeagueStandingView[] standings,
            LeagueBattingLeaderboardView[] battingLeaderboards,
            LeaguePitchingLeaderboardView[] pitchingLeaderboards,
            LeagueTeamMetricView[] teamMetrics,
            LeagueScheduleGameView[] recentResults,
            LeagueScheduleGameView[] nextRoundGames,
            LeagueDefinition currentDefinition = null,
            LeagueDefinition previousDefinition = null,
            LeagueDefinition nextDefinition = null,
            LeagueLevel highestReachedTier = LeagueLevel.Rookie)
        {
            SeasonYear = seasonYear;
            LeagueLevel = leagueLevel;
            SeasonPhase = seasonPhase;
            CurrentDate = currentDate;
            GamesPlayedPerTeam = gamesPlayedPerTeam;
            RegularSeasonGamesPerTeam = regularSeasonGamesPerTeam;
            PlayoffTeamCount = playoffTeamCount;
            MyTeamId = myTeamId;
            MyTeamName = myTeamName;
            MyPlayerId = myPlayerId;
            Standings = standings ?? Array.Empty<LeagueStandingView>();
            _battingLeaderboards = battingLeaderboards ?? Array.Empty<LeagueBattingLeaderboardView>();
            _pitchingLeaderboards = pitchingLeaderboards ?? Array.Empty<LeaguePitchingLeaderboardView>();
            TeamMetrics = teamMetrics ?? Array.Empty<LeagueTeamMetricView>();
            RecentResults = recentResults ?? Array.Empty<LeagueScheduleGameView>();
            NextRoundGames = nextRoundGames ?? Array.Empty<LeagueScheduleGameView>();
            CurrentDefinition = currentDefinition ??
                WorldGenerationConfiguration.GetDefaultDefinition(leagueLevel);
            PreviousDefinition = previousDefinition;
            NextDefinition = nextDefinition;
            HighestReachedTier = highestReachedTier;
        }

        public int SeasonYear { get; }
        public LeagueLevel LeagueLevel { get; }
        public SeasonPhase SeasonPhase { get; }
        public DateTime CurrentDate { get; }
        public int GamesPlayedPerTeam { get; }
        public int RegularSeasonGamesPerTeam { get; }
        public int PlayoffTeamCount { get; }
        public int MyTeamId { get; }
        public string MyTeamName { get; }
        public int MyPlayerId { get; }
        public IReadOnlyList<LeagueStandingView> Standings { get; }
        public IReadOnlyList<LeagueTeamMetricView> TeamMetrics { get; }
        public IReadOnlyList<LeagueScheduleGameView> RecentResults { get; }
        public IReadOnlyList<LeagueScheduleGameView> NextRoundGames { get; }
        public LeagueDefinition CurrentDefinition { get; }
        public LeagueDefinition PreviousDefinition { get; }
        public LeagueDefinition NextDefinition { get; }
        public LeagueLevel HighestReachedTier { get; }

        public LeagueBattingLeaderboardView GetBattingLeaderboard(LeagueBattingCategory category)
        {
            return _battingLeaderboards[(int)category];
        }

        public LeaguePitchingLeaderboardView GetPitchingLeaderboard(LeaguePitchingCategory category)
        {
            return _pitchingLeaderboards[(int)category];
        }
    }
}

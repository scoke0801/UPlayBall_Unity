using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 대시보드의 팀 내 포지션 경쟁 한 줄이다.
    /// </summary>
    public readonly struct PositionCompetitionView
    {
        public PositionCompetitionView(string name, int overall, bool isMyPlayer)
        {
            Name = name;
            Overall = overall;
            IsMyPlayer = isMyPlayer;
        }

        public string Name { get; }
        public int Overall { get; }
        public bool IsMyPlayer { get; }
    }

    /// <summary>
    /// 대시보드에 표시할 미진행 경기 한 줄이다.
    /// </summary>
    public readonly struct UpcomingGameView
    {
        public UpcomingGameView(DateTime date, string opponentName, bool isHome, bool isCurrent)
        {
            Date = date;
            OpponentName = opponentName;
            IsHome = isHome;
            IsCurrent = isCurrent;
        }

        public DateTime Date { get; }
        public string OpponentName { get; }
        public bool IsHome { get; }
        public bool IsCurrent { get; }
    }

    /// <summary>
    /// 다음 경기 CTA에 필요한 대진과 감독 기용 계획이다.
    /// </summary>
    public readonly struct NextCareerGameView
    {
        public NextCareerGameView(
            int gameId,
            DateTime date,
            string awayTeamName,
            string homeTeamName,
            string opponentName,
            bool isHome,
            PlayerGameRole plannedRole)
        {
            GameId = gameId;
            Date = date;
            AwayTeamName = awayTeamName;
            HomeTeamName = homeTeamName;
            OpponentName = opponentName;
            IsHome = isHome;
            PlannedRole = plannedRole;
        }

        public int GameId { get; }
        public DateTime Date { get; }
        public string AwayTeamName { get; }
        public string HomeTeamName { get; }
        public string OpponentName { get; }
        public bool IsHome { get; }
        public PlayerGameRole PlannedRole { get; }
    }

    /// <summary>
    /// 타격 또는 투구 시즌 누적 기록을 Presentation에 전달한다.
    /// </summary>
    public readonly struct PlayerSeasonStatisticsView
    {
        public PlayerSeasonStatisticsView(PlayerSeasonStatisticsState state, bool isPitcher)
        {
            IsPitcher = isPitcher;
            TeamGames = state.TeamGames;
            GamesPlayed = state.GamesPlayed;
            GamesStarted = state.GamesStarted;
            PlateAppearances = state.PlateAppearances;
            AtBats = state.AtBats;
            Hits = state.Hits;
            HomeRuns = state.HomeRuns;
            RunsBattedIn = state.RunsBattedIn;
            Walks = state.Walks;
            BattingStrikeouts = state.BattingStrikeouts;
            StolenBases = state.StolenBases;
            CaughtStealing = state.CaughtStealing;
            FieldingErrors = state.FieldingErrors;
            BattingAverage = state.BattingAverage;
            OnBasePlusSlugging = state.OnBasePlusSlugging;
            PitchingAppearances = state.PitchingAppearances;
            PitchingStarts = state.PitchingStarts;
            OutsRecorded = state.OutsRecorded;
            Wins = state.Wins;
            Losses = state.Losses;
            HitsAllowed = state.HitsAllowed;
            HomeRunsAllowed = state.HomeRunsAllowed;
            EarnedRuns = state.EarnedRuns;
            WalksAllowed = state.WalksAllowed;
            PitchingStrikeouts = state.PitchingStrikeouts;
            EarnedRunAverage = state.EarnedRunAverage;
            WalksHitsPerInningPitched = state.WalksHitsPerInningPitched;
        }

        public bool IsPitcher { get; }
        public int TeamGames { get; }
        public int GamesPlayed { get; }
        public int GamesStarted { get; }
        public int PlateAppearances { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public int Walks { get; }
        public int BattingStrikeouts { get; }
        public int StolenBases { get; }
        public int CaughtStealing { get; }
        public int FieldingErrors { get; }
        public double BattingAverage { get; }
        public double OnBasePlusSlugging { get; }
        public int PitchingAppearances { get; }
        public int PitchingStarts { get; }
        public int OutsRecorded { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int HitsAllowed { get; }
        public int HomeRunsAllowed { get; }
        public int EarnedRuns { get; }
        public int WalksAllowed { get; }
        public int PitchingStrikeouts { get; }
        public double EarnedRunAverage { get; }
        public double WalksHitsPerInningPitched { get; }
    }

    /// <summary>
    /// 정규시즌 뒤 포스트시즌·결산·오프시즌 CTA가 소비할 진행 요약이다.
    /// </summary>
    public readonly struct CareerSeasonProgressView
    {
        public CareerSeasonProgressView(
            bool isPlayerTeamPostseasonQualified,
            bool canPlayNextPostseasonGame,
            string championTeamName,
            PlayerTeamPostseasonResult playerTeamPostseasonResult,
            int postseasonGamesPlayed,
            int playerAwardCount,
            long salaryIncome,
            long bonusIncome,
            int offseasonRemainingWeeks,
            bool requiresContractDecision)
        {
            IsPlayerTeamPostseasonQualified = isPlayerTeamPostseasonQualified;
            CanPlayNextPostseasonGame = canPlayNextPostseasonGame;
            ChampionTeamName = championTeamName ?? string.Empty;
            PlayerTeamPostseasonResult = playerTeamPostseasonResult;
            PostseasonGamesPlayed = postseasonGamesPlayed;
            PlayerAwardCount = playerAwardCount;
            SalaryIncome = salaryIncome;
            BonusIncome = bonusIncome;
            OffseasonRemainingWeeks = offseasonRemainingWeeks;
            RequiresContractDecision = requiresContractDecision;
        }

        public bool IsPlayerTeamPostseasonQualified { get; }
        public bool CanPlayNextPostseasonGame { get; }
        public string ChampionTeamName { get; }
        public PlayerTeamPostseasonResult PlayerTeamPostseasonResult { get; }
        public int PostseasonGamesPlayed { get; }
        public int PlayerAwardCount { get; }
        public long SalaryIncome { get; }
        public long BonusIncome { get; }
        public int OffseasonRemainingWeeks { get; }
        public bool RequiresContractDecision { get; }
    }

    /// <summary>
    /// 선수 중심 메인 화면이 한 번의 Render에서 소비할 읽기 전용 값 모음이다.
    /// </summary>
    public sealed class CareerDashboardView
    {
        public string PlayerName { get; internal set; }
        public int Age { get; internal set; }
        public PlayerPosition Position { get; internal set; }
        public Handedness BattingHand { get; internal set; }
        public Handedness ThrowingHand { get; internal set; }
        public BatterAttributes BatterAttributes { get; internal set; }
        public PitcherAttributes PitcherAttributes { get; internal set; }
        public int Overall { get; internal set; }
        public int Condition { get; internal set; }
        public int ManagerEvaluation { get; internal set; }
        public ExpectedRole ExpectedRole { get; internal set; }
        public string TeamName { get; internal set; }
        public int SeasonYear { get; internal set; }
        public LeagueLevel LeagueLevel { get; internal set; }
        public SeasonPhase SeasonPhase { get; internal set; }
        public long AvailableMoney { get; internal set; }
        public int TeamRank { get; internal set; }
        public int TeamWins { get; internal set; }
        public int TeamLosses { get; internal set; }
        public int TeamTies { get; internal set; }
        public NextCareerGameView? NextGame { get; internal set; }
        public PlayerSeasonStatisticsView Statistics { get; internal set; }
        public PositionCompetitionView[] Competition { get; internal set; }
        public UpcomingGameView[] UpcomingGames { get; internal set; }
        public PlayerGameLogState[] RecentGames { get; internal set; }
        public CareerGameAdvanceResult? LastGame { get; internal set; }
        public int RemainingRegularSeasonGames { get; internal set; }
        public CareerSeasonAutoCompletionResult? LastSeasonAutoCompletion { get; internal set; }
        public CareerSeasonProgressView SeasonProgress { get; internal set; }
    }
}

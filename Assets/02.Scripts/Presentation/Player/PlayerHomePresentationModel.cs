using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// 선수 모드 Home의 선수·소속 구단·시즌 정보를 묶는다.
    /// </summary>
    public sealed class PlayerHomeIdentityModel
    {
        /// <summary>선수와 현재 소속·시즌의 실제 표시 값을 묶는다.</summary>
        public PlayerHomeIdentityModel(
            string playerName,
            int age,
            PlayerPosition position,
            int overall,
            string teamName,
            int teamEmblemId,
            int seasonYear,
            LeagueLevel leagueLevel,
            SeasonPhase seasonPhase)
        {
            PlayerName = playerName ?? string.Empty;
            Age = age;
            Position = position;
            Overall = overall;
            TeamName = teamName ?? string.Empty;
            TeamEmblemId = teamEmblemId;
            SeasonYear = seasonYear;
            LeagueLevel = leagueLevel;
            SeasonPhase = seasonPhase;
        }

        public string PlayerName { get; }
        public int Age { get; }
        public PlayerPosition Position { get; }
        public int Overall { get; }
        public string TeamName { get; }
        public int TeamEmblemId { get; }
        public int SeasonYear { get; }
        public LeagueLevel LeagueLevel { get; }
        public SeasonPhase SeasonPhase { get; }
    }

    /// <summary>
    /// 감독의 시즌 역할 평가와 다음 경기 기용 계획을 함께 표시한다.
    /// </summary>
    public sealed class PlayerUsageModel
    {
        /// <summary>감독 평가와 다음 경기의 계획된 기용을 묶는다.</summary>
        public PlayerUsageModel(
            ExpectedRole expectedRole,
            int managerEvaluation,
            PlayerGameRole? plannedGameRole,
            int battingOrder,
            DecisionReasonCode? decisionReasonCode)
        {
            ExpectedRole = expectedRole;
            ManagerEvaluation = managerEvaluation;
            PlannedGameRole = plannedGameRole;
            BattingOrder = battingOrder;
            DecisionReasonCode = decisionReasonCode;
        }

        public ExpectedRole ExpectedRole { get; }
        public int ManagerEvaluation { get; }
        public PlayerGameRole? PlannedGameRole { get; }
        public int BattingOrder { get; }
        public DecisionReasonCode? DecisionReasonCode { get; }
        public bool HasManagerDecisionReason => DecisionReasonCode.HasValue;
    }

    /// <summary>
    /// 다음 경기의 확정 대진과 감독 계획을 표현한다.
    /// </summary>
    public sealed class PlayerNextMatchModel
    {
        /// <summary>Game 레이어가 확정한 다음 경기 View를 복사한다.</summary>
        public PlayerNextMatchModel(NextCareerGameView game)
        {
            GameId = game.GameId;
            Date = game.Date;
            AwayTeamName = game.AwayTeamName ?? string.Empty;
            HomeTeamName = game.HomeTeamName ?? string.Empty;
            OpponentName = game.OpponentName ?? string.Empty;
            IsHome = game.IsHome;
            PlannedRole = game.PlannedRole;
            BattingOrder = game.BattingOrder;
            AwayTeamEmblemId = game.AwayTeamEmblemId;
            HomeTeamEmblemId = game.HomeTeamEmblemId;
        }

        public int GameId { get; }
        public DateTime Date { get; }
        public string AwayTeamName { get; }
        public string HomeTeamName { get; }
        public string OpponentName { get; }
        public bool IsHome { get; }
        public PlayerGameRole PlannedRole { get; }
        public int BattingOrder { get; }
        public int AwayTeamEmblemId { get; }
        public int HomeTeamEmblemId { get; }
    }

    /// <summary>
    /// 최근 개인 경기 한 건을 Home에서 빠르게 읽을 수 있는 값으로 복사한다.
    /// </summary>
    public sealed class PlayerRecentGameModel
    {
        /// <summary>저장된 개인 경기 로그를 Home용 한 줄 모델로 복사한다.</summary>
        public PlayerRecentGameModel(PlayerGameLogState game)
        {
            GameId = game.GameId;
            DidWin = game.DidWin;
            TeamRuns = game.TeamRuns;
            OpponentRuns = game.OpponentRuns;
            Role = game.Role;
            AtBats = game.AtBats;
            Hits = game.Hits;
            HomeRuns = game.HomeRuns;
            RunsBattedIn = game.RunsBattedIn;
            OutsRecorded = game.OutsRecorded;
            EarnedRuns = game.EarnedRuns;
            Strikeouts = game.Strikeouts;
        }

        public int GameId { get; }
        public bool DidWin { get; }
        public int TeamRuns { get; }
        public int OpponentRuns { get; }
        public PlayerGameRole Role { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public int OutsRecorded { get; }
        public int EarnedRuns { get; }
        public int Strikeouts { get; }
    }

    /// <summary>
    /// 기존 성장 View에서 Home에 필요한 진행 상태만 투영한다.
    /// </summary>
    public sealed class PlayerGrowthStatusModel
    {
        public static PlayerGrowthStatusModel Unavailable { get; } = new PlayerGrowthStatusModel();

        private PlayerGrowthStatusModel()
        {
            IsAvailable = false;
            ActiveProgramId = string.Empty;
        }

        /// <summary>성장 화면 View에서 Home에 필요한 진행 상태만 복사한다.</summary>
        public PlayerGrowthStatusModel(CareerGrowthView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            IsAvailable = true;
            IsOffseason = view.IsOffseason;
            CanEditBoard = view.CanEditBoard;
            HasUncommittedBoardChanges = view.HasUncommittedBoardChanges;
            IsActivityInProgress = view.IsActivityInProgress;
            ActiveProgramId = view.ActiveProgramId ?? string.Empty;
            CurrentWeek = view.CurrentWeek;
            TotalWeeks = view.TotalWeeks;
            RemainingWeeks = view.RemainingWeeks;
            CurrentRole = view.CurrentRole;
            RoleScore = view.RoleScore;
            CompetitorRoleScore = view.CompetitorRoleScore;
        }

        public bool IsAvailable { get; }
        public bool IsOffseason { get; }
        public bool CanEditBoard { get; }
        public bool HasUncommittedBoardChanges { get; }
        public bool IsActivityInProgress { get; }
        public string ActiveProgramId { get; }
        public int CurrentWeek { get; }
        public int TotalWeeks { get; }
        public int RemainingWeeks { get; }
        public ExpectedRole CurrentRole { get; }
        public double RoleScore { get; }
        public double CompetitorRoleScore { get; }
    }

    /// <summary>
    /// 개인 계약 View에서 Home에 필요한 현재 계약 상태만 투영한다.
    /// </summary>
    public sealed class PlayerContractStatusModel
    {
        public static PlayerContractStatusModel Unavailable { get; } = new PlayerContractStatusModel();

        private PlayerContractStatusModel()
        {
            IsAvailable = false;
            TeamName = string.Empty;
        }

        /// <summary>개인 계약 View에서 Home에 필요한 현재 계약만 복사한다.</summary>
        public PlayerContractStatusModel(CareerContractView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            IsAvailable = true;
            TeamName = view.CurrentContract.TeamName ?? string.Empty;
            RemainingSeasons = view.CurrentContract.RemainingSeasons;
            EndYear = view.CurrentContract.EndYear;
            AnnualSalary = view.CurrentContract.AnnualSalary;
            ExpectedRole = view.CurrentContract.ExpectedRole;
            NegotiationStatus = view.NegotiationStatus;
            CanBeginNegotiation = view.CanBeginNegotiation;
        }

        public bool IsAvailable { get; }
        public string TeamName { get; }
        public int RemainingSeasons { get; }
        public int EndYear { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
        public ContractNegotiationStatus NegotiationStatus { get; }
        public bool CanBeginNegotiation { get; }
    }

    /// <summary>
    /// 선수 모드 Home Workspace가 한 번의 Render에서 소비하는 불변 Presentation Model이다.
    /// </summary>
    public sealed class PlayerHomePresentationModel
    {
        private readonly PlayerRecentGameModel[] _recentGames;

        /// <summary>선수 Home Workspace가 소비할 모든 읽기 전용 섹션을 묶는다.</summary>
        public PlayerHomePresentationModel(
            PlayerHomeIdentityModel identity,
            PlayerUsageModel usage,
            PlayerNextMatchModel nextMatch,
            PlayerSeasonStatisticsView seasonStatistics,
            PlayerRecentGameModel[] recentGames,
            int condition,
            long availableMoney,
            int teamRank,
            int teamWins,
            int teamLosses,
            int teamTies,
            PlayerGrowthStatusModel growth,
            PlayerContractStatusModel contract)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Usage = usage ?? throw new ArgumentNullException(nameof(usage));
            NextMatch = nextMatch;
            SeasonStatistics = seasonStatistics;
            _recentGames = recentGames == null
                ? Array.Empty<PlayerRecentGameModel>()
                : (PlayerRecentGameModel[])recentGames.Clone();
            Condition = condition;
            AvailableMoney = availableMoney;
            TeamRank = teamRank;
            TeamWins = teamWins;
            TeamLosses = teamLosses;
            TeamTies = teamTies;
            Growth = growth ?? PlayerGrowthStatusModel.Unavailable;
            Contract = contract ?? PlayerContractStatusModel.Unavailable;
        }

        public PlayerHomeIdentityModel Identity { get; }
        public PlayerUsageModel Usage { get; }
        public PlayerNextMatchModel NextMatch { get; }
        public bool HasNextMatch => NextMatch != null;
        public PlayerSeasonStatisticsView SeasonStatistics { get; }
        public IReadOnlyList<PlayerRecentGameModel> RecentGames => _recentGames;
        public int Condition { get; }

        /// <summary>
        /// 현재 Career State가 공급하는 선수 개인 자금이다.
        /// </summary>
        public long AvailableMoney { get; }

        /// <summary>
        /// 현재 Dashboard 계약에는 피로 수치가 없으므로 데이터가 생길 때까지 null을 유지한다.
        /// </summary>
        public int? Fatigue => null;

        public int TeamRank { get; }
        public int TeamWins { get; }
        public int TeamLosses { get; }
        public int TeamTies { get; }
        public PlayerGrowthStatusModel Growth { get; }
        public PlayerContractStatusModel Contract { get; }
    }
}

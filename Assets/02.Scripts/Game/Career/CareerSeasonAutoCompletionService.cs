using System;
using Baseball.Core.Balance;
using Baseball.Game.Career.News;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 시즌 자동 진행이 처리한 정규시즌과 포스트시즌 범위를 전달한다.
    /// </summary>
    public readonly struct CareerSeasonAutoCompletionResult
    {
        public CareerSeasonAutoCompletionResult(
            SeasonPhase completedPhase,
            int regularSeasonGames,
            int postseasonGames,
            int championTeamId,
            bool isPlayerTeamChampion)
        {
            CompletedPhase = completedPhase;
            RegularSeasonGames = regularSeasonGames;
            PostseasonGames = postseasonGames;
            ChampionTeamId = championTeamId;
            IsPlayerTeamChampion = isPlayerTeamChampion;
        }

        public SeasonPhase CompletedPhase { get; }
        public int RegularSeasonGames { get; }
        public int PostseasonGames { get; }
        public int ChampionTeamId { get; }
        public bool IsPlayerTeamChampion { get; }
    }

    /// <summary>
    /// 호출 시점의 시즌 단계만 기존 경기 집계 경로로 자동 진행한다.
    /// </summary>
    public sealed class CareerSeasonAutoCompletionService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly CareerNewsConfiguration _newsConfiguration;

        public CareerSeasonAutoCompletionService(
            CareerState career,
            BalanceTable balance,
            CareerNewsConfiguration newsConfiguration = null)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _newsConfiguration = newsConfiguration;
        }

        /// <summary>
        /// 선수 개입 없이 현재 단계의 남은 경기만 진행하고 다음 시즌 단계에서 정지한다.
        /// </summary>
        public CareerSeasonAutoCompletionResult CompleteCurrentPhase()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            return season.Phase switch
            {
                SeasonPhase.RegularSeason => CompleteRegularSeason(season),
                SeasonPhase.Postseason => CompletePostseason(season),
                _ => throw new InvalidOperationException(
                    "정규시즌 또는 포스트시즌에서만 현재 단계를 자동 완료할 수 있습니다.")
            };
        }

        private CareerSeasonAutoCompletionResult CompleteRegularSeason(SeasonState season)
        {
            int regularSeasonGamesBefore = season.PlayerStatistics.TeamGames;
            var regularSeason = new CareerSeasonService(
                _career,
                _balance,
                _newsConfiguration);
            while (regularSeason.NextPlayerGame != null)
                regularSeason.AdvanceNextRound();

            int regularSeasonGames = season.PlayerStatistics.TeamGames - regularSeasonGamesBefore;
            if (season.Phase != SeasonPhase.Postseason || season.Postseason == null)
                throw new InvalidOperationException("정규시즌 자동 완료가 포스트시즌 진입 단계에 도달하지 못했습니다.");

            return new CareerSeasonAutoCompletionResult(
                SeasonPhase.RegularSeason,
                regularSeasonGames,
                postseasonGames: 0,
                championTeamId: 0,
                isPlayerTeamChampion: false);
        }

        private CareerSeasonAutoCompletionResult CompletePostseason(SeasonState season)
        {
            int postseasonGamesBefore = CountPostseasonGames(season);
            new CareerPostseasonService(_career, _balance, _newsConfiguration).AdvanceToChampion();

            if (season.Phase != SeasonPhase.SeasonReview || season.Postseason?.IsCompleted != true)
                throw new InvalidOperationException("포스트시즌 자동 완료가 시즌 결산 단계에 도달하지 못했습니다.");

            int postseasonGames = CountPostseasonGames(season) - postseasonGamesBefore;
            return new CareerSeasonAutoCompletionResult(
                SeasonPhase.Postseason,
                regularSeasonGames: 0,
                postseasonGames,
                season.Postseason.ChampionTeamId,
                season.Postseason.ChampionTeamId == _career.MyPlayer.CurrentTeamId);
        }

        private static int CountPostseasonGames(SeasonState season)
        {
            if (season.Postseason == null)
                return 0;

            int count = 0;
            for (int index = 0; index < season.Postseason.Series.Count; index++)
                count += season.Postseason.Series[index].Games.Count;
            return count;
        }
    }
}

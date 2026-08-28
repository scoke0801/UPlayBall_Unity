using System;
using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 시즌 자동 진행이 처리한 정규시즌과 포스트시즌 범위를 전달한다.
    /// </summary>
    public readonly struct CareerSeasonAutoCompletionResult
    {
        public CareerSeasonAutoCompletionResult(
            int regularSeasonGames,
            int postseasonGames,
            int championTeamId,
            bool isPlayerTeamChampion)
        {
            RegularSeasonGames = regularSeasonGames;
            PostseasonGames = postseasonGames;
            ChampionTeamId = championTeamId;
            IsPlayerTeamChampion = isPlayerTeamChampion;
        }

        public int RegularSeasonGames { get; }
        public int PostseasonGames { get; }
        public int ChampionTeamId { get; }
        public bool IsPlayerTeamChampion { get; }
    }

    /// <summary>
    /// 남은 정규시즌과 포스트시즌을 기존 경기 집계 경로로 진행하고 시즌 결산에서 멈춘다.
    /// </summary>
    public sealed class CareerSeasonAutoCompletionService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public CareerSeasonAutoCompletionService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>
        /// 선수 개입 없이 남은 경기를 진행하되 계약·성장 선택이 시작되는 SeasonReview에서 정지한다.
        /// </summary>
        public CareerSeasonAutoCompletionResult CompleteToSeasonReview()
        {
            SeasonState season = _career.League.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (season.Phase != SeasonPhase.RegularSeason && season.Phase != SeasonPhase.Postseason)
                throw new InvalidOperationException("정규시즌 또는 포스트시즌에서만 시즌을 자동 완료할 수 있습니다.");

            int regularSeasonGamesBefore = season.PlayerStatistics.TeamGames;
            if (season.Phase == SeasonPhase.RegularSeason)
            {
                var regularSeason = new CareerSeasonService(_career, _balance);
                while (regularSeason.NextPlayerGame != null)
                    regularSeason.AdvanceNextRound();
            }

            int regularSeasonGames = season.PlayerStatistics.TeamGames - regularSeasonGamesBefore;
            int postseasonGamesBefore = CountPostseasonGames(season);
            if (season.Phase == SeasonPhase.Postseason)
                new CareerPostseasonService(_career, _balance).AdvanceToChampion();

            if (season.Phase != SeasonPhase.SeasonReview || season.Postseason?.IsCompleted != true)
                throw new InvalidOperationException("시즌 자동 완료가 결산 단계에 도달하지 못했습니다.");

            int postseasonGames = CountPostseasonGames(season) - postseasonGamesBefore;
            return new CareerSeasonAutoCompletionResult(
                regularSeasonGames,
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

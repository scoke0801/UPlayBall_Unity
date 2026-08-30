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
            return new SeasonFastForwardSession(
                    _career,
                    _balance,
                    _newsConfiguration)
                .Complete();
        }
    }
}

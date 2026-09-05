using System;
using Baseball.Core.Growth;
using Baseball.Game.Career;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// Game 레이어의 읽기 전용 Career View들을 선수 Home 전용 모델로 변환한다.
    /// </summary>
    public sealed class PlayerHomePresentationModelBuilder
    {
        /// <summary>
        /// Dashboard를 필수로, 성장·계약 View를 선택적으로 받아 Home Snapshot을 만든다.
        /// </summary>
        public PlayerHomePresentationModel Build(
            CareerDashboardView dashboard,
            CareerGrowthView growth = null,
            CareerContractView contract = null)
        {
            if (dashboard == null)
                throw new ArgumentNullException(nameof(dashboard));

            NextCareerGameView? nextGameView = dashboard.NextGame;
            DecisionReasonCode? decisionReason = growth?.RoleExplanation?.SummaryReasonCode;
            var identity = new PlayerHomeIdentityModel(
                dashboard.PlayerName,
                dashboard.Age,
                dashboard.Position,
                dashboard.Overall,
                dashboard.TeamName,
                dashboard.TeamEmblemId,
                dashboard.SeasonYear,
                dashboard.LeagueLevel,
                dashboard.SeasonPhase);
            var usage = new PlayerUsageModel(
                dashboard.ExpectedRole,
                dashboard.ManagerEvaluation,
                nextGameView?.PlannedRole,
                nextGameView?.BattingOrder ?? 0,
                decisionReason);

            return new PlayerHomePresentationModel(
                identity,
                usage,
                nextGameView.HasValue ? new PlayerNextMatchModel(nextGameView.Value) : null,
                dashboard.Statistics,
                BuildRecentGames(dashboard.RecentGames),
                dashboard.Condition,
                dashboard.AvailableMoney,
                dashboard.TeamRank,
                dashboard.TeamWins,
                dashboard.TeamLosses,
                dashboard.TeamTies,
                growth == null ? PlayerGrowthStatusModel.Unavailable : new PlayerGrowthStatusModel(growth),
                contract == null ? PlayerContractStatusModel.Unavailable : new PlayerContractStatusModel(contract));
        }

        private static PlayerRecentGameModel[] BuildRecentGames(PlayerGameLogState[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<PlayerRecentGameModel>();

            var result = new PlayerRecentGameModel[source.Length];
            for (int index = 0; index < source.Length; index++)
                result[index] = new PlayerRecentGameModel(source[index]);
            return result;
        }
    }
}

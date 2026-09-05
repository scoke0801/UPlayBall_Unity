using System;
using System.Globalization;
using Baseball.Game.Career;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 선수 Career 일정의 포커스 결과를 재계산하지 않고 공용 일정 Snapshot으로 변환한다.
    /// </summary>
    public static class CareerScheduleSnapshotAdapter
    {
        /// <summary>
        /// 전체 경기와 현재 구단 관점을 모드 중립 일정 Snapshot으로 복사한다.
        /// </summary>
        public static ScheduleScreenSnapshot Create(CareerScheduleView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            var games = new ScheduleGameSnapshot[view.Games.Count];
            for (int i = 0; i < view.Games.Count; i++)
                games[i] = CreateGame(view.Games[i]);

            return new ScheduleScreenSnapshot(
                view.SeasonYear.ToString(CultureInfo.InvariantCulture),
                CareerSharedSnapshotFormatters.FormatLeague(view.LeagueLevel),
                view.CurrentDate,
                CareerSharedSnapshotFormatters.FormatId(view.PlayerTeamId),
                games);
        }

        private static ScheduleGameSnapshot CreateGame(CareerScheduleGameView game)
        {
            var away = new ScheduleTeamSnapshot(
                CareerSharedSnapshotFormatters.FormatId(game.AwayTeamId),
                game.AwayTeamName,
                CareerSharedSnapshotFormatters.FormatTeamEmblemKey(game.AwayTeamEmblemId),
                CareerSharedSnapshotFormatters.FormatTeamColor(game.AwayTeamColor));
            var home = new ScheduleTeamSnapshot(
                CareerSharedSnapshotFormatters.FormatId(game.HomeTeamId),
                game.HomeTeamName,
                CareerSharedSnapshotFormatters.FormatTeamEmblemKey(game.HomeTeamEmblemId),
                CareerSharedSnapshotFormatters.FormatTeamColor(game.HomeTeamColor));
            ScheduleFocusSide focusSide = !game.IsPlayerGame
                ? ScheduleFocusSide.None
                : game.IsPlayerHome ? ScheduleFocusSide.Home : ScheduleFocusSide.Away;
            return new ScheduleGameSnapshot(
                CareerSharedSnapshotFormatters.FormatId(game.GameId),
                game.Round,
                game.Date,
                away,
                home,
                game.IsCompleted,
                game.AwayRuns,
                game.HomeRuns,
                focusSide,
                ConvertOutcome(game.Outcome));
        }

        private static ScheduleFocusOutcome ConvertOutcome(CareerScheduleOutcome outcome)
        {
            return outcome switch
            {
                CareerScheduleOutcome.Win => ScheduleFocusOutcome.Win,
                CareerScheduleOutcome.Loss => ScheduleFocusOutcome.Loss,
                CareerScheduleOutcome.Tie => ScheduleFocusOutcome.Tie,
                _ => ScheduleFocusOutcome.Pending
            };
        }
    }
}

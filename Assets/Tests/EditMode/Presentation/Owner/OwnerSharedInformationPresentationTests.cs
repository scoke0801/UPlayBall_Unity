using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>Owner 공용 정보 화면이 실제 Round 일정과 읽기 전용 Action 경계를 보존하는지 검증한다.</summary>
    public sealed class OwnerSharedInformationPresentationTests
    {
        [Test]
        public void ScheduleFactory_달력날짜를발명하지않고RuntimeRound와점수를복사한다()
        {
            var completed = new ScheduledGameState(1, 2, 10UL, 1, 2);
            completed.Complete(4, 6);
            var pending = new ScheduledGameState(2, 1, 11UL, 2, 1);
            var liveSeason = new ManagerLiveSeasonState(
                "owner:2028:1",
                1,
                2028,
                3,
                2,
                new[]
                {
                    new ManagerTeamReference(1, "away-team"),
                    new ManagerTeamReference(2, "owner-team")
                },
                new SeasonScheduleState(new[] { completed, pending }));

            ScheduleScreenSnapshot snapshot = new OwnerSharedInformationSnapshotFactory().CreateSchedule(
                liveSeason,
                "루키 리그",
                teamId => teamId == "owner-team" ? "내 구단" : "상대 구단");

            Assert.That(snapshot.HasCalendarDate, Is.False);
            Assert.That(snapshot.CurrentPeriodLabel, Is.EqualTo("4주차"));
            Assert.That(snapshot.FocusTeamId, Is.EqualTo("owner-team"));
            Assert.That(snapshot.Games[0].HasCalendarDate, Is.False);
            Assert.That(snapshot.Games[0].PeriodLabel, Is.EqualTo("2라운드"));
            Assert.That(snapshot.Games[0].HomeRuns, Is.EqualTo(6));
            Assert.That(snapshot.Games[0].AwayTeam.EmblemAssetKey, Is.EqualTo("TeamEmblem/1"));
            Assert.That(snapshot.Games[0].HomeTeam.EmblemAssetKey, Is.EqualTo("TeamEmblem/2"));
            Assert.That(snapshot.Games[0].FocusOutcome, Is.EqualTo(ScheduleFocusOutcome.Pending));
        }

        [Test]
        public void ScheduleFactory_모든구단에원본연도를붙인다()
        {
            var game = new ScheduledGameState(1, 1, 10UL, 1, 2);
            var liveSeason = new ManagerLiveSeasonState(
                "owner:2028:1",
                1,
                2028,
                0,
                2,
                new[]
                {
                    new ManagerTeamReference(1, "rival-team"),
                    new ManagerTeamReference(2, "owner-team")
                },
                new SeasonScheduleState(new[] { game }));

            ScheduleScreenSnapshot snapshot = new OwnerSharedInformationSnapshotFactory().CreateSchedule(
                liveSeason,
                "루키 리그",
                teamId => teamId == "owner-team" ? "서울 마리너스" : "LG 트윈스",
                teamId => teamId == "owner-team" ? 2023 : 2024);

            Assert.That(snapshot.Games[0].AwayTeam.DisplayName, Is.EqualTo("2024 LG 트윈스"));
            Assert.That(snapshot.Games[0].HomeTeam.DisplayName, Is.EqualTo("2023 서울 마리너스"));

            var league = new OwnerLeaguePresentationModel(snapshot);
            Assert.That(
                league.Standings.Single(team => team.Id == "rival-team").Name,
                Is.EqualTo("2024 LG 트윈스"));
            Assert.That(
                league.Standings.Single(team => team.Id == "owner-team").Name,
                Is.EqualTo("2023 서울 마리너스"));
        }

        [Test]
        public void ScheduleFactory_이미연도가포함된합성팀이름을중복하지않는다()
        {
            var game = new ScheduledGameState(1, 1, 10UL, 1, 2);
            var liveSeason = new ManagerLiveSeasonState(
                "owner:2028:1",
                1,
                2028,
                0,
                2,
                new[]
                {
                    new ManagerTeamReference(1, "composite-team"),
                    new ManagerTeamReference(2, "owner-team")
                },
                new SeasonScheduleState(new[] { game }));

            ScheduleScreenSnapshot snapshot = new OwnerSharedInformationSnapshotFactory().CreateSchedule(
                liveSeason,
                "루키 리그",
                teamId => teamId == "owner-team" ? "서울 마리너스" : "2024 올스타",
                _ => 2024);

            Assert.That(snapshot.Games[0].AwayTeam.DisplayName, Is.EqualTo("2024 올스타"));
        }

        [Test]
        public void ClubSeasonHistoryFactory_사전WorldHistory대신실제진행시즌만표시한다()
        {
            var teams = new[]
            {
                new ManagerTeamReference(1, "owner-team"),
                new ManagerTeamReference(2, "rival-team")
            };
            var completedGame = new ScheduledGameState(1, 1, 101UL, 1, 2);
            completedGame.Complete(5, 3);
            var completedSeason = new ManagerLiveSeasonState(
                "owner:2024:1",
                1,
                2024,
                1,
                1,
                teams,
                new SeasonScheduleState(new[] { completedGame }));
            var completed = new ManagerCompletedSeasonState(completedSeason, LeagueGrade.Rookie);

            var currentGame = new ScheduledGameState(2, 1, 201UL, 1, 2);
            currentGame.Complete(1, 4);
            var pendingGame = new ScheduledGameState(3, 2, 202UL, 2, 1);
            var liveSeason = new ManagerLiveSeasonState(
                "owner:2024:2",
                2,
                2024,
                1,
                1,
                teams,
                new SeasonScheduleState(new[] { currentGame, pendingGame }));

            RecordsScreenSnapshot snapshot =
                new OwnerSharedInformationSnapshotFactory().CreateClubSeasonHistoryRecords(
                    liveSeason,
                    new[] { completed },
                    LeagueGrade.Minor,
                    "내 구단");

            Assert.That(snapshot.SeasonLabel, Is.EqualTo("구단 역사"));
            Assert.That(snapshot.CategoryLabel, Is.EqualTo("시즌 성적"));
            Assert.That(snapshot.QualificationText, Does.Contain("실제 진행 시즌만"));
            Assert.That(snapshot.Table.Rows.Count, Is.EqualTo(2));
            Assert.That(snapshot.Table.Columns.Count, Is.EqualTo(15));
            Assert.That(snapshot.Table.Columns[0].ColumnId, Is.EqualTo("Season"));
            Assert.That(snapshot.Table.Rows[0].RowId, Is.EqualTo("club-season:owner:2024:2"));
            Assert.That(snapshot.FocusedRowId, Is.EqualTo(snapshot.Table.Rows[0].RowId));
            Assert.That(snapshot.Table.Rows[0].FindCell("Status").DisplayValue, Is.EqualTo("진행 중"));
            Assert.That(snapshot.Table.Rows[0].FindCell("Wins").DisplayValue, Is.EqualTo("0"));
            Assert.That(snapshot.Table.Rows[0].FindCell("Losses").DisplayValue, Is.EqualTo("1"));
            Assert.That(snapshot.Table.Rows[1].FindCell("Status").DisplayValue, Is.EqualTo("완료"));
            Assert.That(snapshot.Table.Rows[1].FindCell("Wins").DisplayValue, Is.EqualTo("1"));
            Assert.That(snapshot.Table.Rows[1].FindCell("RS").DisplayValue, Is.EqualTo("5"));
            Assert.That(snapshot.Table.Rows[1].FindCell("League").DisplayValue, Is.EqualTo("루키 리그"));
            Assert.That(snapshot.Table.Rows[0].FindCell("Player"), Is.Null);
        }

        [Test]
        public void ReadOnlyActionProvider_가짜OwnerCommand를노출하거나실행하지않는다()
        {
            var context = new SharedScreenContext(
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId);

            IReadOnlyList<SharedScreenActionModel> actions =
                OwnerReadOnlySharedScreenActionProvider.Instance.GetActions(context);

            Assert.That(actions, Is.Empty);
            Assert.That(
                OwnerReadOnlySharedScreenActionProvider.Instance.TryExecute("fake", context),
                Is.False);
        }

        [Test]
        public void Profile_리그에일정과기록을포함한일곱탭을제공한다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            NavigationEntry league = profile.Navigation.FindEntry("Shared.League");
            NavigationEntry standings = profile.Navigation.FindEntry("Shared.League.Standings");
            Assert.That(league.IsEnabled, Is.True);
            Assert.That(league.Children.Count, Is.EqualTo(7));
            Assert.That(standings.IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.LeagueTeamResults).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.LeagueMatchups).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(OwnerNavigationRoutes.LeagueRankHistory).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(
                OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId).IsEnabled, Is.True);
            Assert.That(profile.Navigation.FindEntry(
                OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId).IsEnabled, Is.True);
        }

        [Test]
        public void ScheduleWorkspace_다음경기분석을Context진입요청으로전달한다()
        {
            var root = new GameObject("OwnerScheduleContextTests_Root", typeof(RectTransform));
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                OwnerSharedInformationWorkspaceCoordinator coordinator =
                    shell.gameObject.AddComponent<OwnerSharedInformationWorkspaceCoordinator>();
                coordinator.Initialize(shell);
                var owner = new ScheduleTeamSnapshot("owner", "내 구단");
                var opponent = new ScheduleTeamSnapshot("opponent", "상대 구단");
                var snapshot = new ScheduleScreenSnapshot(
                    "2028 시즌",
                    "루키 리그",
                    "3주차",
                    "owner",
                    new[]
                    {
                        new ScheduleGameSnapshot(
                            "game-3",
                            3,
                            "3라운드",
                            opponent,
                            owner,
                            false,
                            0,
                            0,
                            ScheduleFocusSide.Home)
                    });
                bool requested = false;
                coordinator.NextMatchAnalysisRequested += () => requested = true;

                coordinator.BindSchedule(snapshot, OwnerModeUiProfileFactory.Create().Capabilities);
                Assert.That(coordinator.TryShowRoute(
                    OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId), Is.True);
                Button button = shell.transform.Find(
                    "MainWorkspaceHost/UI_Scene_OwnerSchedule/InformationHeader/NextMatchAnalysisButton")
                    .GetComponent<Button>();
                RecordTableView table = shell.transform.Find(
                    "MainWorkspaceHost/UI_Scene_OwnerSchedule/RecordTableHost/SharedRecordTable")
                    .GetComponent<RecordTableView>();

                Assert.That(button.interactable, Is.True);
                Assert.That(table.VisualStyle, Is.EqualTo(RecordTableVisualStyle.ReferenceLight));
                button.onClick.Invoke();
                Assert.That(requested, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

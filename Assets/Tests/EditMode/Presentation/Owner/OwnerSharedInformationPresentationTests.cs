using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;

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
                "Rookie",
                teamId => teamId == "owner-team" ? "내 구단" : "상대 구단");

            Assert.That(snapshot.HasCalendarDate, Is.False);
            Assert.That(snapshot.CurrentPeriodLabel, Is.EqualTo("4주차"));
            Assert.That(snapshot.FocusTeamId, Is.EqualTo("owner-team"));
            Assert.That(snapshot.Games[0].HasCalendarDate, Is.False);
            Assert.That(snapshot.Games[0].PeriodLabel, Is.EqualTo("2R"));
            Assert.That(snapshot.Games[0].HomeRuns, Is.EqualTo(6));
            Assert.That(snapshot.Games[0].FocusOutcome, Is.EqualTo(ScheduleFocusOutcome.Pending));
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
        public void Profile_일정과역사기록만열고현재순위는정확한사유로잠근다()
        {
            GameModeUiProfile profile = OwnerModeUiProfileFactory.Create();

            NavigationEntry league = profile.Navigation.FindEntry("Shared.League");
            NavigationEntry standings = profile.Navigation.FindEntry("Shared.League.Standings");
            NavigationEntry schedule = profile.Navigation.FindEntry(
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId);
            NavigationEntry records = profile.Navigation.FindEntry(
                OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId);

            Assert.That(league.IsEnabled, Is.True);
            Assert.That(schedule.IsEnabled, Is.True);
            Assert.That(records.IsEnabled, Is.True);
            Assert.That(standings.IsEnabled, Is.False);
            Assert.That(standings.DisabledReason, Does.Contain("누적 승패"));
        }
    }
}

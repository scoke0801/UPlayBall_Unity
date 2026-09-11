using System;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Guide
{
    /// <summary>안내가 실제 적용·공개·저장 경계를 넘어서 성공을 만들어내지 않는지 검증한다.</summary>
    public sealed class GuideProgressTests
    {
        private static GuideGoal Problem(string key = "slot", bool required = true) =>
            new GuideGoal(key, GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, required, "MissingAssignment");

        [Test]
        public void 이동과대상선택만으로수정과제를해결하지않는다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 0, new[] { Problem() }); state.Track("slot");
            Assert.That(state.RecordArrival("slot", GuideArrivalStatus.TargetReady), Is.False);
            state.Reconcile("game1", 0, new[] { Problem() });
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Tracking));
            state.Reconcile("game1", 0, Array.Empty<GuideGoal>());
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Resolved));
        }

        [Test]
        public void 무관한변경은새회차가아니며해소후재발만새회차다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 0, new[] { Problem() }); state.Snooze("slot", 2);
            state.Reconcile("game1", 1, new[] { Problem(), Problem("other") });
            Assert.That(state.GetOccurrence("slot"), Is.EqualTo(1));
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Snoozed));
            state.Reconcile("game1", 1, Array.Empty<GuideGoal>());
            state.Reconcile("game1", 1, new[] { Problem() });
            Assert.That(state.GetOccurrence("slot"), Is.EqualTo(2));
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Pending));
        }

        [Test]
        public void 필수위반은유지할수없고선택경고유지는해결과다르다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 0, new[] { Problem(), Problem("optional", false) });
            Assert.That(state.AcceptAsIs("slot"), Is.False);
            Assert.That(state.AcceptAsIs("optional"), Is.True);
            var restored = GuideProgressState.Restore(state.Capture());
            restored.Reconcile("game1", 0, new[] { Problem(), Problem("optional", false) });
            Assert.That(restored.GetStatus("optional"), Is.EqualTo(GuideGoalStatus.AcceptedAsIs));
            Assert.That(restored.GetVisibleGoals().Count, Is.EqualTo(1));
        }

        [Test]
        public void 저장복원후게임주차경계에서만보류가해제된다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 3, new[] { Problem() }); state.Snooze("slot", 4);
            state = GuideProgressState.Restore(state.Capture());
            state.Reconcile("game1", 3, new[] { Problem() });
            Assert.That(state.GetVisibleGoals().Count, Is.Zero);
            Assert.That(state.GetVisibleGoals(true).Count, Is.EqualTo(1));
            state.Reconcile("game1", 4, new[] { Problem() });
            Assert.That(state.GetVisibleGoals().Count, Is.EqualTo(1));
        }

        [Test]
        public void 주간보류는같은주다음경기에도유지되고새시즌에는만료된다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 3, new[] { Problem() }, "season1"); state.Snooze("slot", 4);
            state = GuideProgressState.Restore(state.Capture());
            state.Reconcile("game2", 3, new[] { Problem() }, "season1");
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Snoozed));
            state.Reconcile("game3", 4, new[] { Problem() }, "season1");
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Pending));
            state.Snooze("slot", 5);
            state.Reconcile("game4", 0, new[] { Problem() }, "season2");
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Pending));
        }

        [Test]
        public void 조회실패와대상누락은열람완료가아니다()
        {
            var goal = new GuideGoal("analysis", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "");
            var state = new GuideProgressState(); state.Reconcile("game1", 0, new[] { goal });
            foreach (var arrival in new[] { GuideArrivalStatus.Requested, GuideArrivalStatus.RouteReady,
                GuideArrivalStatus.Cancelled, GuideArrivalStatus.Unsupported, GuideArrivalStatus.TargetMissing })
                Assert.That(state.RecordArrival(goal.Key, arrival), Is.False);
            Assert.That(state.RecordArrival(goal.Key, GuideArrivalStatus.TargetReady), Is.True);
            state = GuideProgressState.Restore(state.Capture()); state.Reconcile("game1", 0, new[] { goal });
            Assert.That(state.GetVisibleGoals().Count, Is.Zero);
        }

        [Test]
        public void 준비화면도착과실제경기확정은별개다()
        {
            var goal = new GuideGoal("plan-confirmation", GuideGoalKind.PlanConfirmation, GuideTargetKind.PlanConfirmation, false, "");
            var state = new GuideProgressState(); state.Reconcile("game1", 0, new[] { goal });
            Assert.That(state.RecordArrival(goal.Key, GuideArrivalStatus.TargetReady), Is.False);
            state.RecordPlanConfirmed();
            Assert.That(state.GetStatus(goal.Key), Is.EqualTo(GuideGoalStatus.Resolved));
        }

        [Test]
        public void 새범위에서과거추적을만료하고20시즌상세이력이누적되지않는다()
        {
            var state = new GuideProgressState();
            for (int season = 0; season < 20; season++)
                for (int game = 0; game < 144; game++)
                {
                    state.Reconcile(season + ":" + game, 0, new[] { Problem() }); state.Track("slot");
                    state = GuideProgressState.Restore(state.Capture());
                }
            state.Reconcile("next", 0, new[] { Problem("new") });
            Assert.That(state.TrackedKey, Is.Empty);
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Expired));
            Assert.That(state.Capture().entries.Length, Is.EqualTo(1));
        }

        [Test]
        public void 필수조건이추적중인선택사항보다우선하고동점은안정적이다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game1", 0, new[] { Problem("z"), Problem("a"), Problem("optional", false) });
            state.Track("optional");
            var goals = state.GetVisibleGoals();
            Assert.That(goals[0].Key, Is.EqualTo("a")); Assert.That(goals[1].Key, Is.EqualTo("z"));
            Assert.That(goals[2].Key, Is.EqualTo("optional"));
        }

        [Test]
        public void 캡처데이터를바꿔도현재이력이바뀌지않는다()
        {
            var state = new GuideProgressState(); state.Reconcile("game1", 0, new[] { Problem() });
            var captured = state.Capture(); captured.entries[0].status = GuideGoalStatus.Resolved;
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Pending));
        }

        [Test]
        public void 공개전에는경기후목표가없고공개점수만복원된다()
        {
            var state = new GuideProgressState();
            Assert.That(OwnerGuideGoalProvider.Create(null, null, false, state.PublishedMatchKey).Count, Is.Zero);
            state.PublishMatch("season:1", 2, 3);
            state = GuideProgressState.Restore(state.Capture());
            var goals = OwnerGuideGoalProvider.Create(null, null, false, state.PublishedMatchKey);
            Assert.That(goals.Count, Is.EqualTo(1)); Assert.That(goals[0].Kind, Is.EqualTo(GuideGoalKind.Debrief));
            Assert.That(state.HomeScore, Is.EqualTo(2)); Assert.That(state.AwayScore, Is.EqualTo(3));
        }

        [Test]
        public void 원본검증의경고와차단및안정카드대상을보존한다()
        {
            var validation = new LineupPresetValidationResult("preset", new[] {
                new LineupPresetValidationIssue(LineupPresetValidationIssueCode.OffPositionAssignment,
                    LineupPresetIssueSeverity.Warning, LineupPresetAssignmentGroup.StartingLineup, 1, "cardA", ""),
                new LineupPresetValidationIssue(LineupPresetValidationIssueCode.MissingAssignment,
                    LineupPresetIssueSeverity.Incomplete, LineupPresetAssignmentGroup.StarterRotation, 2, null, "") });
            var goals = OwnerGuideGoalProvider.Create(new RosterValidationResult(Array.Empty<RosterValidationIssue>()), validation, true, "");
            Assert.That(goals[0].IsRequired, Is.False); Assert.That(goals[0].CardId, Is.EqualTo("cardA"));
            Assert.That(goals[1].IsRequired, Is.True); Assert.That(goals[1].SlotIndex, Is.EqualTo(2));
            Assert.That(goals.Count, Is.EqualTo(3));
        }

        [Test]
        public void 잘못된저장이력과중복키는조용히복구하지않는다()
        {
            Assert.Throws<ArgumentException>(() => GuideProgressState.Restore(new GuideProgressData { trackedKey = "missing" }));
            Assert.Throws<ArgumentException>(() => GuideProgressState.Restore(new GuideProgressData { entries = new[] {
                new GuideGoalEntryData { key = "x", occurrence = 1 }, new GuideGoalEntryData { key = "x", occurrence = 1 } } }));
        }

        [Test]
        public void 확인한경기후안내는다음경기와복원뒤다시생기지않는다()
        {
            var state = new GuideProgressState(); state.PublishMatch("match1", 3, 2);
            var goal = new GuideGoal("debrief:match1", GuideGoalKind.Debrief, GuideTargetKind.Condition, false, "");
            state.Reconcile("game2", 0, new[] { goal });
            state.RecordArrival(goal.Key, GuideArrivalStatus.TargetReady);
            state = GuideProgressState.Restore(state.Capture());
            state.Reconcile("game3", 1, Array.Empty<GuideGoal>());
            Assert.That(state.PendingMatchKey, Is.Empty);
            state.PublishMatch("match2", 4, 1);
            Assert.That(state.PendingMatchKey, Is.EqualTo("match2"));
        }

        [Test]
        public void 유지했던경고가필수위반으로바뀌면다시안내한다()
        {
            var state = new GuideProgressState();
            state.Reconcile("game", 0, new[] { Problem("slot", false) }); state.AcceptAsIs("slot");
            state.Reconcile("game", 0, new[] { Problem("slot", true) });
            Assert.That(state.GetVisibleGoals().Count, Is.EqualTo(1));
            Assert.That(state.GetStatus("slot"), Is.EqualTo(GuideGoalStatus.Pending));
        }
    }
}

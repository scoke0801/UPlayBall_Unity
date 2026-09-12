using System;
using System.Linq;
using Baseball.Game.Guide;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Guide
{
    /// <summary>리포트 기록과 실제 경고 해결의 독립성·중복 방지·시즌 보존을 검증한다.</summary>
    public sealed class ManagerReportTests
    {
        private static GuideGoal Goal(string key, bool required = false) =>
            new GuideGoal(key, GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, required, "MissingAssignment");

        [Test]
        public void 읽음과보관은문제를해결하지않고복원후추천에서제외된다()
        {
            var state = new GuideProgressState();
            var goals = new[] { Goal("a", true), Goal("b") };
            state.Reconcile("match1", 0, goals, "season1");
            state.MarkReportRead(state.FindReportId("a"));
            state.SetReportBookmark(state.FindReportId("b"), true);
            state = GuideProgressState.Restore(state.Capture());
            state.Reconcile("match2", 0, goals, "season1");
            Assert.That(state.GetReports().Count, Is.EqualTo(2));
            Assert.That(state.GetSuggestionGoals(), Is.Empty);
            Assert.That(state.GetVisibleGoals().Count, Is.EqualTo(2));
            Assert.That(state.GetStatus("a"), Is.EqualTo(GuideGoalStatus.Pending));
        }

        [Test]
        public void 미확인중복은근거를갱신하고시즌변경은만료기록을남긴다()
        {
            var state = new GuideProgressState();
            state.Reconcile("match1", 0, new[] { Goal("a") }, "season1");
            string id = state.FindReportId("a");
            state.Reconcile("match2", 1, new[] { new GuideGoal("a", GuideGoalKind.PresetIssue,
                GuideTargetKind.PresetSlot, false, "OffPositionAssignment", cardId: "card", slotIndex: 3) }, "season1");
            var report = state.GetReports().Single();
            Assert.That(report.reportId, Is.EqualTo(id));
            Assert.That(report.createdWeek, Is.EqualTo(1));
            Assert.That(report.ToGoal().CardId, Is.EqualTo("card"));
            Assert.That(report.ToGoal().SlotIndex, Is.EqualTo(3));
            state.SetReportBookmark(id, true);
            state.Reconcile("match3", 0, Array.Empty<GuideGoal>(), "season2");
            report = GuideProgressState.Restore(state.Capture()).GetReports().Single();
            Assert.That(report.isExpired && report.isRead && report.isBookmarked, Is.True);
        }

        [Test]
        public void 추천후보의중요도상한이전체경고를삭제하지않는다()
        {
            var state = new GuideProgressState();
            state.Reconcile("match", 0, new[] { Goal("c1", true), Goal("c2", true), Goal("i1"), Goal("i2"), Goal("i3"), Goal("i4") });
            Assert.That(state.GetSuggestionGoals().Count, Is.EqualTo(4));
            Assert.That(state.GetSuggestionGoals().Count(item => item.IsRequired), Is.EqualTo(1));
            Assert.That(state.GetSuggestionGoals()[0].IsRequired, Is.True);
            Assert.That(state.GetReports().Count, Is.EqualTo(6));
        }

        [Test]
        public void 선택경고가필수차단으로바뀌면다시읽어야한다()
        {
            var state = new GuideProgressState();
            state.Reconcile("match", 0, new[] { Goal("a") });
            state.MarkReportRead(state.FindReportId("a"));
            state.Reconcile("match", 0, new[] { Goal("a", true) });
            Assert.That(state.GetSuggestionGoals().Single().IsRequired, Is.True);
        }
    }
}

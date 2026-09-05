using System;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 공용 일정표 투영이 월·포커스 필터와 Adapter가 확정한 결과를 보존하는지 검증한다.
    /// </summary>
    public sealed class ScheduleRecordTableBuilderTests
    {
        [Test]
        public void CreateFocusedMonth_포커스경기만날짜순으로만든다()
        {
            ScheduleScreenSnapshot snapshot = CreateSnapshot();

            RecordTableModel table = ScheduleRecordTableBuilder.CreateFocusedMonth(snapshot, 2028, 4);

            Assert.That(table.Rows.Count, Is.EqualTo(2));
            Assert.That(table.Rows[0].RowId, Is.EqualTo("game-early"));
            Assert.That(table.Rows[1].RowId, Is.EqualTo("game-late"));
            Assert.That(table.Rows[1].FindCell("Opponent").DisplayValue, Is.EqualTo("서울"));
        }

        [Test]
        public void CreateFocusedMonth_점수로Outcome을재계산하지않는다()
        {
            RecordTableModel table = ScheduleRecordTableBuilder.CreateFocusedMonth(CreateSnapshot(), 2028, 4);

            Assert.That(table.Rows[1].FindCell("Result").DisplayValue, Is.EqualTo("L  9:1"));
        }

        [Test]
        public void CreateFocusedSchedule_날짜없는Round와확정점수를그대로표시한다()
        {
            var away = new ScheduleTeamSnapshot("away", "원정");
            var home = new ScheduleTeamSnapshot("home", "홈");
            var snapshot = new ScheduleScreenSnapshot(
                "2028 시즌",
                "Rookie",
                "3주차",
                "home",
                new[]
                {
                    new ScheduleGameSnapshot(
                        "late", 3, "3R", away, home,
                        true, 7, 2, ScheduleFocusSide.Home),
                    new ScheduleGameSnapshot(
                        "early", 1, "1R", home, away,
                        false, 0, 0, ScheduleFocusSide.Away)
                });

            RecordTableModel table = ScheduleRecordTableBuilder.CreateFocusedSchedule(snapshot);

            Assert.That(table.Rows[0].RowId, Is.EqualTo("game-early"));
            Assert.That(table.Rows[1].FindCell("Date").DisplayValue, Is.EqualTo("3R"));
            Assert.That(table.Rows[1].FindCell("Result").DisplayValue, Is.EqualTo("2:7"));
        }

        [Test]
        public void CreateFocusedMonth_날짜없는일정을월별로위장하지않는다()
        {
            var away = new ScheduleTeamSnapshot("away", "원정");
            var home = new ScheduleTeamSnapshot("home", "홈");
            var snapshot = new ScheduleScreenSnapshot(
                "2028 시즌",
                "Rookie",
                "1주차",
                "home",
                new[]
                {
                    new ScheduleGameSnapshot(
                        "round", 1, "1R", away, home,
                        false, 0, 0, ScheduleFocusSide.Home)
                });

            Assert.Throws<InvalidOperationException>(() =>
                ScheduleRecordTableBuilder.CreateFocusedMonth(snapshot, 2028, 4));
        }

        private static ScheduleScreenSnapshot CreateSnapshot()
        {
            var seoul = new ScheduleTeamSnapshot("10", "서울");
            var busan = new ScheduleTeamSnapshot("20", "부산");
            return new ScheduleScreenSnapshot(
                "2028",
                "1부",
                new DateTime(2028, 4, 10),
                "20",
                new[]
                {
                    new ScheduleGameSnapshot(
                        "late", 3, new DateTime(2028, 4, 20), seoul, busan,
                        true, 1, 9, ScheduleFocusSide.Home, ScheduleFocusOutcome.Loss),
                    new ScheduleGameSnapshot(
                        "other", 2, new DateTime(2028, 4, 15), seoul, busan,
                        false, 0, 0, ScheduleFocusSide.None, ScheduleFocusOutcome.Pending),
                    new ScheduleGameSnapshot(
                        "early", 1, new DateTime(2028, 4, 2), busan, seoul,
                        false, 0, 0, ScheduleFocusSide.Away, ScheduleFocusOutcome.Pending),
                    new ScheduleGameSnapshot(
                        "next-month", 4, new DateTime(2028, 5, 1), busan, seoul,
                        false, 0, 0, ScheduleFocusSide.Away, ScheduleFocusOutcome.Pending)
                });
        }
    }
}

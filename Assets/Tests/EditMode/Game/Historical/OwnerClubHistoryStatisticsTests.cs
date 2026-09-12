using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단 역사의 비율·규정·소속 분할과 읽기 전용 조회를 검증한다.</summary>
    public sealed class OwnerClubHistoryStatisticsTests
    {
        [Test]
        public void 통산비율과이닝은원본분모를합산한다()
        {
            var a = Season(true); var b = Season(true);
            Player(a, 1, 10, 4, 1, 0); Player(b, 1, 90, 18, 26, 3);
            var total = OwnerClubHistoryStatistics.CalculateTotals(a); total.Add(OwnerClubHistoryStatistics.CalculateTotals(b));
            Assert.That(total.BattingAverage, Is.EqualTo(.22).Within(.000001));
            Assert.That(total.EarnedRunAverage, Is.EqualTo(3)); Assert.That(total.OutsRecorded, Is.EqualTo(27));
        }

        [Test]
        public void 현재소속이다르더라도우리구단에서남긴기록만포함한다()
        {
            var season = Season(true); var player = Player(season, 2, 100, 50, 300, 20);
            var split = (PlayerTeamStatisticsSplitState)typeof(PlayerCompetitionStatisticsState)
                .GetMethod("GetOrCreateTeamSplit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { 1 });
            Set(split.Batting, "AtBats", 20); Set(split.Batting, "PlateAppearances", 22); Set(split.Batting, "Hits", 5);
            Set(split.Pitching, "OutsRecorded", 30); Set(split.Pitching, "EarnedRuns", 1);
            var totals = OwnerClubHistoryStatistics.CalculateTotals(season);
            Assert.That(totals.Hits, Is.EqualTo(5)); Assert.That(totals.OutsRecorded, Is.EqualTo(30));
            Assert.That(OwnerClubHistoryStatistics.CollectRecords(season).Single(r => r.Metric == CareerRecordMetric.BattingAverage).Value, Is.EqualTo(.25));
        }

        [Test]
        public void 미완료시즌과규정미달비율은최고기록후보에서제외한다()
        {
            var pending = Season(false); Player(pending, 1, 100, 40, 90, 0);
            Assert.That(OwnerClubHistoryStatistics.CollectRecords(pending), Is.Empty);
            var completed = Season(true); var player = Player(completed, 1, 1, 1, 1, 0);
            Set(player.Batting, "PlateAppearances", 1);
            var records = OwnerClubHistoryStatistics.CollectRecords(completed);
            Assert.That(records.Any(r => r.Metric == CareerRecordMetric.BattingAverage || r.Metric == CareerRecordMetric.EarnedRunAverage), Is.False);
            Assert.That(records.Any(r => r.Metric == CareerRecordMetric.Hits), Is.True);
        }

        [Test]
        public void 반복조회는기록을변경하지않으며선수순서가고정된다()
        {
            var season = Season(true); Player(season, 1, 100, 30, 54, 0);
            var first = OwnerClubHistoryStatistics.CollectRecords(season);
            var second = OwnerClubHistoryStatistics.CollectRecords(season);
            Assert.That(first.Select(r => (r.PlayerId, r.Metric, r.Value)), Is.EqualTo(second.Select(r => (r.PlayerId, r.Metric, r.Value))));
            Assert.That(season.Statistics.RegularSeason.GetPlayer(1).Batting.Hits, Is.EqualTo(30));
        }

        private static ManagerLiveSeasonState Season(bool completed)
        {
            var game = new ScheduledGameState(1, 1, 42, 1, 2); if (completed) game.Complete(3, 1);
            return new ManagerLiveSeasonState("history", 1, 2026, 0, 1,
                new[] { new ManagerTeamReference(1, "owner"), new ManagerTeamReference(2, "rival") }, new SeasonScheduleState(new[] { game }));
        }
        private static PlayerCompetitionStatisticsState Player(ManagerLiveSeasonState season, int team, int atBats, int hits, int outs, int earned)
        {
            var player = season.Statistics.RegularSeason.GetOrCreate(1, "기록선수", team, PlayerPosition.FirstBase);
            Set(player.Batting, "AtBats", atBats); Set(player.Batting, "PlateAppearances", atBats + 2); Set(player.Batting, "Hits", hits);
            Set(player.Pitching, "OutsRecorded", outs); Set(player.Pitching, "EarnedRuns", earned); return player;
        }
        private static void Set(object target, string name, int value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}

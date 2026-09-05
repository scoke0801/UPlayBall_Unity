using Baseball.Presentation.SharedScreens;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 읽기 전용 Roster 필터가 선수 ID와 편집 불가 계약을 보존하는지 검증한다.
    /// </summary>
    public sealed class ReadOnlyRosterModelFilterTests
    {
        [Test]
        public void FilterByKind_지정선수만남기고읽기전용을유지한다()
        {
            var batter = new ReadOnlyRosterPlayerModel(
                "batter-1", "타자", "SS", "주전", "80", "90", ".300",
                kind: RosterPlayerKind.Batter);
            var pitcher = new ReadOnlyRosterPlayerModel(
                "pitcher-1", "투수", "SP", "선발진", "82", "85", "2.80",
                kind: RosterPlayerKind.Pitcher);
            var source = new ReadOnlyRosterModel(
                "team-1", "서울", "2028", "등록 2명 · 읽기 전용",
                new[] { new ReadOnlyRosterGroupModel("Roster", "선수단", new[] { batter, pitcher }) });

            ReadOnlyRosterModel filtered = source.FilterByKind(RosterPlayerKind.Pitcher);

            Assert.That(filtered.IsReadOnly, Is.True);
            Assert.That(filtered.Groups.Count, Is.EqualTo(1));
            Assert.That(filtered.Groups[0].Players.Count, Is.EqualTo(1));
            Assert.That(filtered.Groups[0].Players[0].PlayerId, Is.EqualTo("pitcher-1"));
            StringAssert.Contains("1명 / 2명", filtered.Summary);
        }
    }
}

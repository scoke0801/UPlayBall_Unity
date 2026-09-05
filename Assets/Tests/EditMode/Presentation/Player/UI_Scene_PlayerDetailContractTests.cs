using System;
using System.Linq;
using System.Reflection;
using Baseball.Game.Career;
using Baseball.Presentation.Career;
using Baseball.Presentation.SharedScreens;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>선수 화면이 모드 중립 상세 계약만 공개하고 구단주 전용 동작을 노출하지 않는지 검증한다.</summary>
    public sealed class UI_Scene_PlayerDetailContractTests
    {
        [Test]
        public void CurrentDetailSnapshot_공용선수상세계약을읽기전용으로노출한다()
        {
            PropertyInfo property = typeof(UI_Scene_Player).GetProperty(
                nameof(UI_Scene_Player.CurrentDetailSnapshot));

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(PlayerDetailSnapshot)));
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.GetSetMethod(nonPublic: true)?.IsPublic, Is.False);
        }

        [Test]
        public void CareerAdapter_공용Snapshot계약으로변환한다()
        {
            MethodInfo create = typeof(CareerPlayerDetailSnapshotAdapter).GetMethod(
                nameof(CareerPlayerDetailSnapshotAdapter.Create),
                new[] { typeof(PlayerProfileView) });

            Assert.That(create, Is.Not.Null);
            Assert.That(create.ReturnType, Is.EqualTo(typeof(PlayerDetailSnapshot)));
        }

        [TestCase(CareerRecordMetric.PlateAppearances, "타석")]
        [TestCase(CareerRecordMetric.Hits, "안타")]
        [TestCase(CareerRecordMetric.HomeRuns, "홈런")]
        [TestCase(CareerRecordMetric.Walks, "볼넷")]
        [TestCase(CareerRecordMetric.BattingStrikeouts, "삼진")]
        [TestCase(CareerRecordMetric.BattingAverage, "타율")]
        [TestCase(CareerRecordMetric.OnBasePlusSlugging, "출루+장타")]
        [TestCase(CareerRecordMetric.WalksHitsPerInningPitched, "이닝당출루")]
        public void 기록지표_사용자용한국어이름을제공한다(CareerRecordMetric metric, string expected)
        {
            Assert.That(CareerSharedSnapshotFormatters.FormatMetricLabel(metric), Is.EqualTo(expected));
        }

        [Test]
        public void Player화면공개Api_구단주전용동작을노출하지않는다()
        {
            string[] forbiddenTerms =
            {
                "OwnedPlayerCardState",
                "Enhancement",
                "Scout",
                "TeamColorEquip",
                "LineupEdit"
            };
            MemberInfo[] publicMembers = typeof(UI_Scene_Player).GetMembers(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

            foreach (MemberInfo member in publicMembers)
            {
                Assert.That(
                    forbiddenTerms.Any(term =>
                        member.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    member.Name);
            }
        }
    }
}

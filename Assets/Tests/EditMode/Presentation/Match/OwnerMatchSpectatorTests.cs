using System;
using System.Linq;
using Baseball.Presentation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>구단주 관전 화면의 배속과 운영 권한 비노출을 검증한다.</summary>
    public sealed class OwnerMatchSpectatorTests
    {
        [Test]
        public void 배속은자동재생간격에실제로반영된다()
        {
            float normal = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.Normal);
            float fast = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.Fast);
            float veryFast = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.VeryFast);

            Assert.That(normal, Is.EqualTo(0.8f));
            Assert.That(fast, Is.EqualTo(0.4f));
            Assert.That(veryFast, Is.EqualTo(0.16f));
        }

        [Test]
        public void 잘못된배속은거부한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds((OwnerMatchPlaybackSpeed)3));
        }

        [Test]
        public void 실제Owner관전View는구단운영명령을노출하지않는다()
        {
            string[] names = typeof(UI_Scene_OwnerMatchSpectator)
                .GetMembers()
                .Select(member => member.Name)
                .ToArray();
            string[] forbidden = { "Lineup", "Substitution", "Tactic", "Bullpen", "TeamColor", "Scout" };

            foreach (string token in forbidden)
            {
                Assert.That(
                    names.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    token);
            }
        }
    }
}

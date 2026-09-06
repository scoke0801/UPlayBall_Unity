using System;
using System.Linq;
using Baseball.Presentation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>구단주 관전 화면의 배속과 운영 권한 비노출을 검증한다.</summary>
    public sealed class OwnerMatchSpectatorTests
    {
        [Test]
        public void 관전화면은중계와결과에필요한계층을구성한다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            UI_Scene_OwnerMatchSpectator view = null;
            try
            {
                view = UI_Scene_OwnerMatchSpectator.CreateRuntime(hostObject.GetComponent<RectTransform>());

                Transform canvas = view.transform.Find("BroadcastCanvas");
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.Find("Field/StadiumArtwork"), Is.Not.Null);
                Assert.That(canvas.Find("Field/StadiumArtworkBlend"), Is.Not.Null);
                Assert.That(canvas.Find("InningOverlay/LineScore"), Is.Not.Null);
                Assert.That(canvas.Find("MatchResult/FinalLineScore"), Is.Not.Null);
                Assert.That(canvas.Find("MatchResult/RecordViewport"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 관전용야구장이미지는Resources에서불러온다()
        {
            string[] paths =
            {
                "stadium_pitch", "stadium_pitch_release", "stadium_swing_miss", "stadium_bat_contact",
                "stadium_ball_flight", "stadium_ball_caught", "stadium_safe_hit", "stadium_overview"
            };
            foreach (string path in paths)
            {
                Assert.That(
                    Resources.LoadAll("UI/OwnerMatch/" + path)
                        .Any(asset => asset is Texture2D or Sprite),
                    Is.True,
                    path);
            }
        }

        [TestCase(PlateAppearanceResult.Strikeout, OwnerMatchVisualSequenceKind.SwingMiss)]
        [TestCase(PlateAppearanceResult.FlyOut, OwnerMatchVisualSequenceKind.BallCaught)]
        [TestCase(PlateAppearanceResult.Single, OwnerMatchVisualSequenceKind.SafeHit)]
        [TestCase(PlateAppearanceResult.Double, OwnerMatchVisualSequenceKind.SafeHit)]
        [TestCase(PlateAppearanceResult.HomeRun, OwnerMatchVisualSequenceKind.ContactOnly)]
        [TestCase(PlateAppearanceResult.Walk, OwnerMatchVisualSequenceKind.PitchOnly)]
        public void 타석결과에맞는이미지연출을선택한다(
            PlateAppearanceResult result,
            OwnerMatchVisualSequenceKind expected)
        {
            Assert.That(OwnerMatchVisualSequenceResolver.Resolve(result), Is.EqualTo(expected));
        }

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

        [TestCase("", false, "상대 구단")]
        [TestCase("   ", true, "우리 구단")]
        [TestCase("2024 올스타", false, "2024 올스타")]
        [TestCase("부산 마리너스", false, "부산 마리너스")]
        public void 관전구단명_빈이름만역할기반이름으로대체한다(
            string teamName,
            bool isPlayerTeam,
            string expected)
        {
            Assert.That(
                OwnerMatchSpectatorSession.FormatTeamDisplayName(teamName, isPlayerTeam),
                Is.EqualTo(expected));
        }
    }
}

using System;
using System.Linq;
using Baseball.Core.Players;
using Baseball.Presentation.Match;
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
                Assert.That(canvas.Find("Field/StadiumBackground"), Is.Not.Null);
                Assert.That(canvas.Find("Field/StadiumActors"), Is.Not.Null);
                Assert.That(canvas.Find("Field/StadiumActorsBlend"), Is.Not.Null);
                Assert.That(canvas.Find("ViewingModeEveryMoment"), Is.Not.Null);
                Assert.That(canvas.Find("ViewingModeKeyMoments"), Is.Not.Null);
                Assert.That(canvas.Find("ViewingModeResultOnly"), Is.Not.Null);
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
                "stadium_pitch_background",
                "stadium_overlay_set_rr", "stadium_overlay_windup_rr", "stadium_overlay_pitch1_rr",
                "stadium_overlay_pitch2_rr", "stadium_overlay_flight_rr", "stadium_overlay_hit_rr",
                "stadium_overlay_miss_rr", "stadium_overlay_take_rr",
                "stadium_overlay_set_rl", "stadium_overlay_windup_rl", "stadium_overlay_pitch1_rl",
                "stadium_overlay_pitch2_rl", "stadium_overlay_flight_rl", "stadium_overlay_hit_rl",
                "stadium_overlay_miss_rl", "stadium_overlay_take_rl"
            };
            foreach (string path in paths)
            {
                Assert.That(
                    Resources.LoadAll("UI/OwnerMatch/" + path)
                        .Any(asset => asset is Texture2D or Sprite),
                    Is.True,
                    path);
            }
            Assert.That(Resources.Load<Shader>("UI/OwnerMatch/OwnerMatchOverlayKey"), Is.Not.Null);
        }

        [TestCase(Handedness.Right, Handedness.Right, false, false)]
        [TestCase(Handedness.Right, Handedness.Left, false, true)]
        [TestCase(Handedness.Left, Handedness.Left, true, false)]
        [TestCase(Handedness.Left, Handedness.Right, true, true)]
        public void 투타조합에맞는오버레이와반전방향을선택한다(
            Handedness throwingHand,
            Handedness battingHand,
            bool expectedMirror,
            bool expectedLeftBatterSet)
        {
            var handedness = new OwnerMatchHandedness(throwingHand, battingHand);

            Assert.That(handedness.IsPitcherLeftHanded, Is.EqualTo(expectedMirror));
            Assert.That(handedness.UsesRightPitcherLeftBatterSet, Is.EqualTo(expectedLeftBatterSet));
        }

        [TestCase(OwnerMatchViewingMode.EveryMoment)]
        [TestCase(OwnerMatchViewingMode.KeyMoments)]
        [TestCase(OwnerMatchViewingMode.ResultOnly)]
        public void 관전상태는세가지보기옵션을보존한다(OwnerMatchViewingMode mode)
        {
            var state = new OwnerMatchOverlayState(0, 10, false, OwnerMatchPlaybackSpeed.Normal, "안내", mode);

            Assert.That(state.ViewingMode, Is.EqualTo(mode));
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

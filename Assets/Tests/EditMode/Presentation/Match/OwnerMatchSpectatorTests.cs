using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Presentation.Match;
using Baseball.Presentation.SharedScreens;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
                Transform compact = canvas.Find("CompactLineScore");
                RectTransform awayName = (RectTransform)compact.Find("AwayTeam");
                RectTransform homeName = (RectTransform)compact.Find("HomeTeam");
                Assert.That(compact.Find("Title").GetComponent<Text>().text, Is.EqualTo("이닝별 득점"));
                Assert.That(awayName.anchoredPosition.x, Is.EqualTo(homeName.anchoredPosition.x));
                Assert.That(awayName.sizeDelta.x, Is.EqualTo(homeName.sizeDelta.x));
                Assert.That(awayName.anchoredPosition.y, Is.LessThan(0));
                Assert.That(homeName.anchoredPosition.y, Is.LessThan(awayName.anchoredPosition.y));
                Transform markers = canvas.Find("Field/Ground/GameCastMarkers");
                Assert.That(markers, Is.Not.Null);
                Assert.That(markers.Find("StadiumBackground"), Is.Not.Null);
                Transform fieldBall = markers.Find("Ball");
                Assert.That(fieldBall, Is.Not.Null);
                Assert.That(fieldBall.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(fieldBall.GetComponent<Baseball.Presentation.UI.UICircleGraphic>(), Is.Null);
                Assert.That(canvas.Find("GameCastSidebar/PitchContext/StrikeZone"), Is.Not.Null);
                Transform zoneBall = canvas.Find("GameCastSidebar/PitchContext/StrikeZone/PitchInFlight");
                Assert.That(zoneBall.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(zoneBall.GetComponent<Baseball.Presentation.UI.UICircleGraphic>(), Is.Null);
                Assert.That(canvas.Find("GameCastSidebar/PitchContext/StrikeZone/Pitch0/Baseball")
                    .GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(canvas.Find("Field/StadiumActors"), Is.Null);
                Assert.That(canvas.Find("ViewingModeEveryMoment"), Is.Not.Null);
                Assert.That(canvas.Find("ViewingModeKeyMoments"), Is.Not.Null);
                Assert.That(canvas.Find("ViewingModeResultOnly"), Is.Not.Null);
                Assert.That(canvas.Find("InningOverlay/LineScore"), Is.Not.Null);
                Assert.That(canvas.Find("MatchResult/FinalLineScore"), Is.Not.Null);
                Transform recordViewport = canvas.Find("MatchResult/RecordViewport");
                Transform recordScrollbar = canvas.Find("MatchResult/RecordScrollbar");
                Assert.That(recordViewport, Is.Not.Null);
                Assert.That(recordScrollbar, Is.Not.Null);
                ScrollRect recordScroll = recordViewport.GetComponent<ScrollRect>();
                Assert.That(recordViewport.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(recordScroll.vertical, Is.True);
                Assert.That(recordScroll.horizontal, Is.False);
                Assert.That(recordScroll.verticalScrollbar, Is.SameAs(recordScrollbar.GetComponent<Scrollbar>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 경기종료상태에서는다음타석버튼을숨긴다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            try
            {
                UI_Scene_OwnerMatchSpectator view =
                    UI_Scene_OwnerMatchSpectator.CreateRuntime(hostObject.GetComponent<RectTransform>());
                MethodInfo method = typeof(UI_Scene_OwnerMatchSpectator).GetMethod(
                    "SetCompletionControlVisibility",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                method.Invoke(view, new object[] { true });

                Transform canvas = view.transform.Find("BroadcastCanvas");
                Assert.That(canvas.Find("Pause").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("RevealAll").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("Advance").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("Result").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("ReturnHome").gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 경기중에는관전범위와배속과즉시결과만조작할수있다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            try
            {
                UI_Scene_OwnerMatchSpectator view =
                    UI_Scene_OwnerMatchSpectator.CreateRuntime(hostObject.GetComponent<RectTransform>());
                Transform canvas = view.transform.Find("BroadcastCanvas");

                Assert.That(canvas.Find("Pause").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("RevealAll").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("Advance").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("Result").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("ReturnHome").gameObject.activeSelf, Is.False);
                Assert.That(canvas.Find("ViewingModeEveryMoment").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("ViewingModeKeyMoments").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("ViewingModeResultOnly").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("Speed1").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("Speed2").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("Speed4").gameObject.activeSelf, Is.True);
                Assert.That(canvas.Find("Speed4").GetComponentInChildren<Text>().text, Is.EqualTo("4배"));
                Assert.That(canvas.Find("Speed5"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 즉시결과는경기오디오상태를변경하지않는다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            OwnerMatchPresentationOptions originalSettings = OwnerMatchPresentationSettings.Load();
            try
            {
                OwnerMatchPresentationSettings.ResetToDefaults();
                GameBootstrap.EnsureRuntimeManagers();
                Assert.That(GameManager.Instance.TryGetManager(out OwnerModeManager manager), Is.True);
                Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
                UI_Scene_OwnerMatchSpectator view =
                    UI_Scene_OwnerMatchSpectator.CreateRuntime(hostObject.GetComponent<RectTransform>());
                int audioStateChangeCount = 0;
                view.MatchAudioEnabledChanged += _ => audioStateChangeCount++;
                view.PlayNextGame(manager);
                Assert.That(view.IsComplete, Is.False);

                view.transform.Find("BroadcastCanvas/RevealAll").GetComponent<Button>().onClick.Invoke();

                Assert.That(view.IsComplete, Is.True);
                Assert.That(audioStateChangeCount, Is.Zero);
            }
            finally
            {
                OwnerMatchPresentationSettings.SetPlaybackSpeed(originalSettings.PlaybackSpeed);
                OwnerMatchPresentationSettings.SetViewingMode(originalSettings.ViewingMode);
                UnityEngine.Object.DestroyImmediate(hostObject);
                if (GameManager.HasInstance)
                    UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);
            }
        }

        [Test]
        public void 관전용야구장이미지는Resources에서불러온다()
        {
            MatchGameCastConfig config = MatchGameCastConfig.Load();
            Texture2D texture = Resources.Load<Texture2D>(config.fieldTexture);
            Sprite baseball = config.LoadBaseballSprite();
            Assert.That(texture, Is.Not.Null);
            Assert.That(baseball, Is.Not.Null);
            Assert.That((float)texture.width / texture.height, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(Resources.Load<TextAsset>("UI/OwnerMatch/GameCastPresentation"), Is.Not.Null);
        }

        [Test]
        public void 경기결과기록표는타자포지션과투수보직및결정을노출한다()
        {
            FieldInfo battingHeaders = typeof(UI_Scene_OwnerMatchSpectator).GetField(
                "BattingRecordHeaders",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo pitchingHeaders = typeof(UI_Scene_OwnerMatchSpectator).GetField(
                "PitchingRecordHeaders",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(battingHeaders, Is.Not.Null);
            Assert.That(pitchingHeaders, Is.Not.Null);
            Assert.That((string[])battingHeaders.GetValue(null), Does.Contain("포지션"));
            Assert.That((string[])pitchingHeaders.GetValue(null), Is.EqualTo(new[]
            {
                "선수", "보직", "이닝", "피안타", "실점", "볼넷", "탈삼진", "승리", "패전", "홀드", "세이브"
            }));
        }

        [TestCase(PlayerPosition.Catcher, "포수")]
        [TestCase(PlayerPosition.FirstBase, "1루수")]
        [TestCase(PlayerPosition.SecondBase, "2루수")]
        [TestCase(PlayerPosition.ThirdBase, "3루수")]
        [TestCase(PlayerPosition.Shortstop, "유격수")]
        [TestCase(PlayerPosition.LeftField, "좌익수")]
        [TestCase(PlayerPosition.CenterField, "중견수")]
        [TestCase(PlayerPosition.RightField, "우익수")]
        [TestCase(PlayerPosition.DesignatedHitter, "지명타자")]
        public void 경기결과타자포지션은한글로표시한다(PlayerPosition position, string expected)
        {
            Assert.That(CareerSharedSnapshotFormatters.FormatPositionName(position), Is.EqualTo(expected));
        }

        [TestCase(PitcherRole.Starter, "선발")]
        [TestCase(PitcherRole.Swingman, "불펜")]
        [TestCase(PitcherRole.LongRelief, "불펜")]
        [TestCase(PitcherRole.MiddleRelief, "불펜")]
        [TestCase(PitcherRole.Setup, "셋업")]
        [TestCase(PitcherRole.Closer, "마무리")]
        public void 경기결과투수보직은네가지표시범주로정리한다(PitcherRole role, string expected)
        {
            MethodInfo method = typeof(UI_Scene_OwnerMatchSpectator).GetMethod(
                "FormatPitcherRole",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { role }), Is.EqualTo(expected));
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
        public void 중요순간은초반의평범한단타와삼진을제외한다()
        {
            MatchEvent[] events =
            {
                Event(0, MatchEventType.Pitch, 1, InningHalf.Top, outs: 0),
                Event(1, MatchEventType.PlateAppearanceEnded, 1, InningHalf.Top,
                    result: Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Single, outs: 0),
                Event(2, MatchEventType.Pitch, 1, InningHalf.Top, outs: 0),
                Event(3, MatchEventType.PlateAppearanceEnded, 1, InningHalf.Top,
                    result: Baseball.Simulation.PlateAppearance.PlateAppearanceResult.Strikeout, outs: 1),
                Event(4, MatchEventType.HalfInningEnded, 1, InningHalf.Top, outs: 3),
                Event(5, MatchEventType.Pitch, 9, InningHalf.Bottom, outs: 2, awayScore: 3, homeScore: 3),
                Event(6, MatchEventType.PlateAppearanceEnded, 9, InningHalf.Bottom,
                    result: Baseball.Simulation.PlateAppearance.PlateAppearanceResult.GroundOut,
                    outs: 3, awayScore: 3, homeScore: 3),
                Event(7, MatchEventType.HalfInningEnded, 9, InningHalf.Bottom,
                    outs: 3, awayScore: 3, homeScore: 3),
                Event(8, MatchEventType.MatchEndedAsDraw, 9, InningHalf.Bottom,
                    awayScore: 3, homeScore: 3)
            };

            OwnerMatchHighlightSegment[] highlights = OwnerMatchHighlightSelector.Select(
                events,
                new MatchGameCastConfig());

            Assert.That(highlights.Select(value => value.EndEventIndex), Is.EqualTo(new[] { 6, 8 }));
        }

        [Test]
        public void 중요순간은승부처표식부터역전타석끝까지한장면으로묶는다()
        {
            MatchEvent[] events =
            {
                Event(0, MatchEventType.RunnerAdvance, 8, InningHalf.Bottom,
                    playerId: 31, fromBase: 0, toBase: 1, outs: 1, awayScore: 4, homeScore: 3),
                Event(1, MatchEventType.HighLeverageSituationStarted, 8, InningHalf.Bottom,
                    awayScore: 4, homeScore: 3),
                Event(2, MatchEventType.Pitch, 8, InningHalf.Bottom,
                    outs: 1, awayScore: 4, homeScore: 3),
                Event(3, MatchEventType.RunnerAdvance, 8, InningHalf.Bottom,
                    playerId: 31, fromBase: 1, toBase: 4, outs: 1, awayScore: 4, homeScore: 4),
                Event(4, MatchEventType.Score, 8, InningHalf.Bottom,
                    playerId: 31, fromBase: 1, toBase: 4, outs: 1, awayScore: 4, homeScore: 4),
                Event(5, MatchEventType.RunnerAdvance, 8, InningHalf.Bottom,
                    playerId: 11, fromBase: 0, toBase: 4, outs: 1, awayScore: 4, homeScore: 5),
                Event(6, MatchEventType.Score, 8, InningHalf.Bottom,
                    playerId: 11, fromBase: 0, toBase: 4, outs: 1, awayScore: 4, homeScore: 5),
                Event(7, MatchEventType.PlateAppearanceEnded, 8, InningHalf.Bottom,
                    result: Baseball.Simulation.PlateAppearance.PlateAppearanceResult.HomeRun,
                    outs: 1, awayScore: 4, homeScore: 5),
                Event(8, MatchEventType.MatchEnded, 9, InningHalf.Bottom,
                    awayScore: 4, homeScore: 5)
            };

            OwnerMatchHighlightSegment[] highlights = OwnerMatchHighlightSelector.Select(
                events,
                new MatchGameCastConfig());

            Assert.That(highlights[0].StartEventIndex, Is.EqualTo(1));
            Assert.That(highlights[0].EndEventIndex, Is.EqualTo(7));
        }

        [Test]
        public void 배속은자동재생간격에실제로반영된다()
        {
            float normal = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.Normal);
            float fast = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.Fast);
            float fourTimes = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.FourTimes);
            float veryFast = OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(OwnerMatchPlaybackSpeed.VeryFast);

            Assert.That(normal, Is.EqualTo(0.8f));
            Assert.That(fast, Is.EqualTo(0.4f));
            Assert.That(fourTimes, Is.EqualTo(0.2f));
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

        [Test]
        public void 타자아웃은타석결과에서만강조하고주루아웃은별도로강조한다()
        {
            var method = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("IsEmphasized", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method.Invoke(null, new object[] { Event(1, MatchEventType.Out, 1, InningHalf.Top) }), Is.False);
            Assert.That(method.Invoke(null, new object[] { Event(2, MatchEventType.PlateAppearanceEnded, 1, InningHalf.Top, PlateAppearanceResult.FlyOut) }), Is.True);
            Assert.That(method.Invoke(null, new object[] { Event(3, MatchEventType.RunnerThrownOut, 1, InningHalf.Top) }), Is.True);
        }

        private static MatchEvent Event(
            int sequence,
            MatchEventType type,
            int inning,
            InningHalf half,
            Baseball.Simulation.PlateAppearance.PlateAppearanceResult result = default,
            int playerId = 11,
            int fromBase = 0,
            int toBase = 0,
            int outs = 0,
            int awayScore = 0,
            int homeScore = 0)
        {
            return new MatchEvent(
                sequence,
                type,
                inning,
                half,
                11,
                21,
                playerId,
                PitchResult.None,
                result,
                fromBase,
                toBase,
                0,
                0,
                outs,
                awayScore,
                homeScore);
        }
    }
}

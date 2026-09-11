using System.Reflection;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Presentation.Career;
using Baseball.Presentation.Match;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>확정된 마지막 카운트를 일반 투구로 축약하지 않고 타석 결과로 읽는지 검증한다.</summary>
    public sealed class PitchOutcomePresentationTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void 실제경기의삼진과볼넷결과는다음투구전에읽는시간동안유지된다(int speed)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var host = new GameObject("PitchOutcomeHost", typeof(RectTransform));
            var original = OwnerMatchPresentationSettings.Load();
            try
            {
                OwnerMatchPresentationSettings.ResetToDefaults();
                GameBootstrap.EnsureRuntimeManagers();
                Assert.That(GameManager.Instance.TryGetManager(out OwnerModeManager manager), Is.True);
                Assert.That(manager.StartNewGame(), Is.True, manager.LastError);
                var view = UI_Scene_OwnerMatchSpectator.CreateRuntime(host.GetComponent<RectTransform>());
                view.PlayNextGame(manager);
                var session = (OwnerMatchSpectatorSession)typeof(UI_Scene_OwnerMatchSpectator).GetField("_session", flags).GetValue(view);
                session.TrySetPlaybackSpeed((OwnerMatchPlaybackSpeed)speed);
                session.TrySetViewingMode(OwnerMatchViewingMode.EveryMoment);
                var update = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("UpdateGameCast", flags);
                var pending = typeof(UI_Scene_OwnerMatchSpectator).GetField("_pendingEvent", flags);
                var revealed = typeof(UI_Scene_OwnerMatchSpectator).GetField("_pendingEventRevealed", flags);
                var hasPending = typeof(UI_Scene_OwnerMatchSpectator).GetField("_hasPendingEvent", flags);
                var announcement = (Text)typeof(UI_Scene_OwnerMatchSpectator).GetField("_announcement", flags).GetValue(view);
                bool strikeout = false, walk = false;
                for (int frame = 0; frame < 100000 && !view.IsComplete && !(strikeout && walk); frame++)
                {
                    update.Invoke(view, new object[] { 0.01f });
                    var value = (MatchEvent)pending.GetValue(view);
                    if (!(bool)hasPending.GetValue(view) || !(bool)revealed.GetValue(view) ||
                        value.EventType != MatchEventType.PlateAppearanceEnded) continue;
                    if (value.PlateAppearanceResult != PlateAppearanceResult.Strikeout &&
                        value.PlateAppearanceResult != PlateAppearanceResult.Walk) continue;
                    if ((strikeout && value.PlateAppearanceResult == PlateAppearanceResult.Strikeout) ||
                        (walk && value.PlateAppearanceResult == PlateAppearanceResult.Walk)) continue;
                    string expected = value.PlateAppearanceResult == PlateAppearanceResult.Strikeout ? "삼진 아웃" : "볼넷";
                    Assert.That(announcement.text, Is.EqualTo(expected));
                    int visible = session.State.VisibleEventCount;
                    update.Invoke(view, new object[] { 0.02f });
                    Assert.That(session.State.VisibleEventCount, Is.EqualTo(visible), "결과를 표시하자마자 다음 투구로 넘어가면 안 된다.");
                    Assert.That(announcement.text, Is.EqualTo(expected));
                    strikeout |= value.PlateAppearanceResult == PlateAppearanceResult.Strikeout;
                    walk |= value.PlateAppearanceResult == PlateAppearanceResult.Walk;
                }
                Assert.That(strikeout && walk, Is.True, "실제 경기의 삼진과 볼넷을 모두 검증해야 한다.");
            }
            finally
            {
                OwnerMatchPresentationSettings.SetPlaybackSpeed(original.PlaybackSpeed);
                OwnerMatchPresentationSettings.SetViewingMode(original.ViewingMode);
                Object.DestroyImmediate(host);
                if (GameManager.HasInstance) Object.DestroyImmediate(GameManager.Instance.gameObject);
            }
        }

        [TestCase(PlayResolutionCueType.PlateCall, PlateAppearanceResult.Walk, "볼넷")]
        [TestCase(PlayResolutionCueType.PlateCall, PlateAppearanceResult.Strikeout, "삼진 아웃")]
        [TestCase(PlayResolutionCueType.SwingAndMiss, PlateAppearanceResult.Strikeout, "삼진 아웃")]
        public void 선수모드도마지막투구판정부터확정된타석결과를표시한다(
            PlayResolutionCueType cue, PlateAppearanceResult result, string expected)
        {
            var sequence = new PlayResolutionSequence(System.Array.Empty<PlayResolutionCue>(),
                0, 0, 1, 11, 21, 0, default, default, result, result == PlateAppearanceResult.Strikeout ? 1 : 0,
                0, 0, 1);
            var format = typeof(PlayResolutionPresenter).GetMethod("GetCallLabel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(format.Invoke(null, new object[] { cue, sequence }), Is.EqualTo(expected));
        }

        [TestCase(PitchResult.Ball, 3, 2, "볼")]
        [TestCase(PitchResult.Ball, 4, 2, "볼넷")]
        [TestCase(PitchResult.CalledStrike, 0, 2, "스트라이크")]
        [TestCase(PitchResult.CalledStrike, 0, 3, "삼진 아웃")]
        [TestCase(PitchResult.SwingingStrike, 3, 2, "헛스윙")]
        [TestCase(PitchResult.SwingingStrike, 3, 3, "삼진 아웃")]
        [TestCase(PitchResult.Foul, 3, 2, "파울")]
        [TestCase(PitchResult.Foul, 3, 3, "삼진 아웃")]
        public void 마지막투구와계속되는투구를구분한다(PitchResult pitch, int balls, int strikes, string expected)
        {
            var value = new MatchEvent(1, MatchEventType.Pitch, 1, InningHalf.Top,
                11, 21, 11, pitch, PlateAppearanceResult.None, 0, 0, balls, strikes, 0, 0, 0);
            var format = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("FormatEventResult", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(format.Invoke(null, new object[] { value }), Is.EqualTo(expected));
        }
    }
}

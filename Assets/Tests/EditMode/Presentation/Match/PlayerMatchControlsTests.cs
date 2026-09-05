using System;
using System.Linq;
using Baseball.Presentation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Match
{
    /// <summary>Player 경기 입력 권한과 Owner 명령 비노출을 검증한다.</summary>
    public sealed class PlayerMatchControlsTests
    {
        [Test]
        public void 입력권한이없으면CommandSink를호출하지않는다()
        {
            var sink = new RecordingSink();
            var controls = new PlayerMatchControls(sink);

            Assert.That(controls.TrySelectBattingIntent(PlayerBattingIntent.Power), Is.False);
            Assert.That(controls.TryConfirmBattingIntent(), Is.False);
            Assert.That(controls.TrySelectPitchingIntent(PlayerPitchingIntent.ControlFirst), Is.False);
            Assert.That(controls.TryConfirmPitchingIntent(), Is.False);
            Assert.That(controls.TryTogglePause(), Is.False);
            Assert.That(controls.TryAutoCompleteCurrentPlayerMoment(), Is.False);
            Assert.That(sink.InvocationCount, Is.Zero);
        }

        [Test]
        public void 허용된Player입력만CommandSink로전달한다()
        {
            var sink = new RecordingSink();
            var controls = new PlayerMatchControls(sink);
            controls.UpdateAvailability(
                new PlayerMatchControlAvailability(
                    canTogglePause: true,
                    canChooseBattingIntent: true,
                    canConfirmBattingIntent: true,
                    canChoosePitchingIntent: true,
                    canConfirmPitchingIntent: true,
                    canAutoCompleteCurrentPlayerMoment: true,
                    canUseBattingMiniGame: true,
                    canUsePitchingMiniGame: false));

            Assert.That(controls.TrySelectBattingIntent(PlayerBattingIntent.Contact), Is.True);
            Assert.That(controls.TryConfirmBattingIntent(), Is.True);
            Assert.That(controls.TrySelectPitchingIntent(PlayerPitchingIntent.InduceChase), Is.True);
            Assert.That(controls.TryConfirmPitchingIntent(), Is.True);
            Assert.That(controls.TryTogglePause(), Is.True);
            Assert.That(controls.TryAutoCompleteCurrentPlayerMoment(), Is.True);
            Assert.That(sink.InvocationCount, Is.EqualTo(6));
            Assert.That(sink.BattingIntent, Is.EqualTo(PlayerBattingIntent.Contact));
            Assert.That(sink.PitchingIntent, Is.EqualTo(PlayerPitchingIntent.InduceChase));
            Assert.That(controls.Availability.CanUseBattingMiniGame, Is.True);
            Assert.That(controls.Availability.CanUsePitchingMiniGame, Is.False);
        }

        [Test]
        public void Player계약에Owner운영명령이노출되지않는다()
        {
            string[] memberNames = typeof(IPlayerMatchControls)
                .GetMembers()
                .Select(member => member.Name)
                .ToArray();
            string[] forbiddenTokens =
            {
                "Owner",
                "Lineup",
                "Substitution",
                "Tactic",
                "Bullpen",
                "TeamColor",
                "Scout"
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(
                    memberNames.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    token);
            }
        }

        [Test]
        public void 정의되지않은Intent는CommandSink로전달하지않는다()
        {
            var sink = new RecordingSink();
            var controls = new PlayerMatchControls(sink);
            controls.UpdateAvailability(
                new PlayerMatchControlAvailability(
                    canTogglePause: false,
                    canChooseBattingIntent: true,
                    canConfirmBattingIntent: false,
                    canChoosePitchingIntent: true,
                    canConfirmPitchingIntent: false,
                    canAutoCompleteCurrentPlayerMoment: false,
                    canUseBattingMiniGame: false,
                    canUsePitchingMiniGame: false));

            Assert.That(controls.TrySelectBattingIntent((PlayerBattingIntent)999), Is.False);
            Assert.That(controls.TrySelectPitchingIntent((PlayerPitchingIntent)999), Is.False);
            Assert.That(sink.InvocationCount, Is.Zero);
        }

        [Test]
        public void OwnerOverlay계약은관전제어외운영명령을정의하지않는다()
        {
            string[] memberNames = typeof(IOwnerMatchOverlay)
                .GetMembers()
                .Select(member => member.Name)
                .ToArray();
            string[] forbiddenTokens =
            {
                "Lineup",
                "Substitution",
                "Tactic",
                "Bullpen",
                "TeamColor",
                "Scout"
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(
                    memberNames.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    token);
            }

            Assert.That(memberNames, Does.Contain(nameof(IOwnerMatchOverlay.TryAdvance)));
            Assert.That(memberNames, Does.Contain(nameof(IOwnerMatchOverlay.TryTogglePause)));
            Assert.That(memberNames, Does.Contain(nameof(IOwnerMatchOverlay.TrySetPlaybackSpeed)));
        }

        [Test]
        public void 빈OwnerOverlay는관전제어를거부하고권한안내를제공한다()
        {
            IOwnerMatchOverlay overlay = EmptyOwnerMatchOverlay.Instance;

            Assert.That(overlay.State.HasMatch, Is.False);
            Assert.That(overlay.State.CanAdvance, Is.False);
            Assert.That(overlay.State.PermissionMessage, Is.Not.Empty);
            Assert.That(overlay.CurrentHud, Is.Null);
            Assert.That(overlay.TryAdvance(), Is.False);
            Assert.That(overlay.TryTogglePause(), Is.False);
            Assert.That(overlay.TrySetPlaybackSpeed(OwnerMatchPlaybackSpeed.Fast), Is.False);
            Assert.That(overlay.TryRevealAll(), Is.False);
        }

        private sealed class RecordingSink : IPlayerMatchControlCommandSink
        {
            public int InvocationCount { get; private set; }
            public PlayerBattingIntent BattingIntent { get; private set; }
            public PlayerPitchingIntent PitchingIntent { get; private set; }

            public void SelectBattingIntent(PlayerBattingIntent intent)
            {
                BattingIntent = intent;
                InvocationCount++;
            }

            public void ConfirmBattingIntent()
            {
                InvocationCount++;
            }

            public void SelectPitchingIntent(PlayerPitchingIntent intent)
            {
                PitchingIntent = intent;
                InvocationCount++;
            }

            public void ConfirmPitchingIntent()
            {
                InvocationCount++;
            }

            public void TogglePause()
            {
                InvocationCount++;
            }

            public void AutoCompleteCurrentPlayerMoment()
            {
                InvocationCount++;
            }
        }
    }
}

using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 경기 진행 화면의 흐름 상태와 주 행동이 내 선수 상태에 따라 하나로 결정되는지 검증한다.
    /// </summary>
    public sealed class MatchProgressViewStateTests
    {
        private const int ControlledPlayerId = 99;

        [Test]
        public void Create_세번째아웃이후에는공수교대흐름으로전환한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 3,
                    plateAppearanceResult: PlateAppearanceResult.Strikeout),
                CreateEvent(1, MatchEventType.HalfInningEnded, 0, outs: 3)
            };

            MatchProgressViewState view = Create(events, events.Length, CreateFlow(isAutomaticPlaybackActive: true));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.SideChange));
            Assert.That(view.IsStageTakeover, Is.True);
        }

        [Test]
        public void Create_내선수교체출전직후에는감독호출을주행동으로둔다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 1),
                CreateEvent(1, MatchEventType.PlayerSubstitution, ControlledPlayerId, playerId: 10)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isDecisionInputReady: true),
                CreatePlayer(PlayerGameRole.Bench));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.PlayerCallUp));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.EnterPlateAppearance));
            Assert.That(view.PlayerState, Is.EqualTo(PlayerMatchState.AtBat));
        }

        [Test]
        public void Create_감독호출을확인하면타석흐름으로넘어간다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlayerSubstitution, ControlledPlayerId, playerId: 10)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isDecisionInputReady: true, isCallUpAcknowledged: true),
                CreatePlayer(PlayerGameRole.Bench));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.PlayerAtBat));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.NextPitch));
            Assert.That(view.IsPlaybackControlHidden, Is.True);
        }

        [Test]
        public void Create_벤치선수가일시정지하면출전까지진행을주행동으로둔다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 1)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isAutomaticPlaybackActive: true, isPaused: true),
                CreatePlayer(PlayerGameRole.Bench));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.Paused));
            Assert.That(view.PlayerState, Is.EqualTo(PlayerMatchState.Bench));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.AdvanceToPlayerEntry));
        }

        [Test]
        public void Create_선발타자가일시정지하면다음타석까지진행을주행동으로둔다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 1),
                CreateEvent(1, MatchEventType.PlateAppearanceEnded, 20, outs: 2),
                CreateEvent(2, MatchEventType.PlateAppearanceEnded, ControlledPlayerId, outs: 2)
            };

            MatchProgressViewState view = Create(
                events,
                visibleEventCount: 1,
                CreateFlow(isAutomaticPlaybackActive: true, isPaused: true));

            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.AdvanceToPlayerAtBat));
            Assert.That(view.PlateAppearancesUntilPlayerAtBat, Is.EqualTo(1));
        }

        [Test]
        public void Create_자동중계중에는일시정지를주행동으로둔다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 1)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isAutomaticPlaybackActive: true));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.AutoRunning));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.Pause));
        }

        [Test]
        public void Create_경기가끝나면결과확인을주행동으로둔다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.MatchEnded, 0)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(phase: CareerMatchPhase.Completed));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.GameEnded));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.ViewResult));
        }

        [Test]
        public void Create_교체되어나간선수는남은경기를끝까지진행하게한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlayerSubstitution, 30, playerId: ControlledPlayerId)
            };

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isAutomaticPlaybackActive: true, isPaused: true));

            Assert.That(view.Flow, Is.EqualTo(MatchFlowState.PlayerSubstitutedOut));
            Assert.That(view.PrimaryAction, Is.EqualTo(MatchPrimaryAction.FinishMatch));
        }

        [Test]
        public void Create_공격팀은이닝방향과홈원정을함께반영한다()
        {
            MatchEvent[] events =
            {
                CreateEvent(0, MatchEventType.PlateAppearanceEnded, 10, outs: 1)
            };
            MatchProgressPlayerContext player = CreatePlayer(PlayerGameRole.StartingBatter);
            player.IsPlayerTeamHome = true;

            MatchProgressViewState view = Create(
                events,
                events.Length,
                CreateFlow(isAutomaticPlaybackActive: true),
                player);

            Assert.That(view.IsAwayTeamBatting, Is.True);
            Assert.That(view.IsPlayerTeamBatting, Is.False);
        }

        private static MatchProgressViewState Create(
            MatchEvent[] events,
            int visibleEventCount,
            MatchProgressFlowContext flow)
        {
            return Create(events, visibleEventCount, flow, CreatePlayer(PlayerGameRole.StartingBatter));
        }

        private static MatchProgressViewState Create(
            MatchEvent[] events,
            int visibleEventCount,
            MatchProgressFlowContext flow,
            MatchProgressPlayerContext player)
        {
            CareerMatchPlaybackSnapshot snapshot = CareerMatchPlaybackSnapshot.Create(
                events,
                visibleEventCount,
                null);
            return MatchProgressViewState.Create(events, visibleEventCount, snapshot, player, flow);
        }

        private static MatchProgressPlayerContext CreatePlayer(PlayerGameRole role)
        {
            return new MatchProgressPlayerContext
            {
                ControlledPlayerId = ControlledPlayerId,
                Role = role,
                Position = PlayerPosition.FirstBase,
                IsPlayerTeamHome = false,
                CanReceiveBattingDecisions = true
            };
        }

        private static MatchProgressFlowContext CreateFlow(
            CareerMatchPhase phase = CareerMatchPhase.Playing,
            bool isDecisionInputReady = false,
            bool hasControlledResult = false,
            bool isAutomaticPlaybackActive = false,
            bool isPaused = false,
            bool isCallUpAcknowledged = false)
        {
            return new MatchProgressFlowContext
            {
                Phase = phase,
                IsDecisionInputReady = isDecisionInputReady,
                HasControlledResult = hasControlledResult,
                IsAutomaticPlaybackActive = isAutomaticPlaybackActive,
                IsPaused = isPaused,
                IsCallUpAcknowledged = isCallUpAcknowledged
            };
        }

        private static MatchEvent CreateEvent(
            int sequence,
            MatchEventType eventType,
            int batterId,
            int playerId = 0,
            int outs = 0,
            PlateAppearanceResult plateAppearanceResult = PlateAppearanceResult.None,
            int inning = 1,
            InningHalf half = InningHalf.Top,
            int awayScore = 0,
            int homeScore = 0)
        {
            return new MatchEvent(
                sequence,
                eventType,
                inning,
                half,
                batterId,
                pitcherId: 500,
                playerId,
                PitchResult.None,
                plateAppearanceResult,
                fromBase: 0,
                toBase: 0,
                balls: 0,
                strikes: 0,
                outs,
                awayScore,
                homeScore);
        }
    }
}

using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>직접 투구·타격 명령이 공통 Resolver와 결정론적 경기 세션을 통과하는지 검증한다.</summary>
    public sealed class MiniGameSimulationTests
    {
        [Test]
        public void PitchExecution_같은Seed와명령은같은실제궤적을만든다()
        {
            PlateAppearanceMatchup matchup = CreateMatchup(55, 62);
            var command = new PitchSelectionCommand(
                0,
                PitchType.Slider,
                new PlatePoint(0.72d, -0.64d));

            PitchFlightDescriptor first = new PitchExecutionResolver(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(912345UL))
                .Resolve(matchup, command);
            PitchFlightDescriptor second = new PitchExecutionResolver(
                    BalanceTable.CreateDefault(),
                    new Pcg32Random(912345UL))
                .Resolve(matchup, command);

            Assert.That(second.PlatePoint, Is.EqualTo(first.PlatePoint));
            Assert.That(second.VelocityMph, Is.EqualTo(first.VelocityMph));
            Assert.That(second.HorizontalBreak, Is.EqualTo(first.HorizontalBreak));
            Assert.That(second.VerticalBreak, Is.EqualTo(first.VerticalBreak));
            Assert.That(second.IsHitByPitch, Is.EqualTo(first.IsHitByPitch));
        }

        [Test]
        public void PitchExecution_Control이높으면예상제구타원이작다()
        {
            var resolver = new PitchExecutionResolver(
                BalanceTable.CreateDefault(),
                new Pcg32Random(1UL));

            CommandEllipse low = resolver.CalculateCommandEllipse(
                CreateMatchup(50, 25),
                PitchType.FourSeamFastball);
            CommandEllipse high = resolver.CalculateCommandEllipse(
                CreateMatchup(50, 85),
                PitchType.FourSeamFastball);

            Assert.That(high.RadiusX, Is.LessThan(low.RadiusX));
            Assert.That(high.RadiusY, Is.LessThan(low.RadiusY));
        }

        [Test]
        public void SwingContact_정확한위치와시점은안타가아닌인플레이프로필을만든다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            var resolver = new SwingContactResolver(balance);
            PlateAppearanceMatchup matchup = CreateMatchup(60, 55);
            PitchFlightDescriptor pitch = CreatePitch(new PlatePoint(0.24d, -0.18d));
            double idealTime = resolver.GetIdealSwingTime01(pitch);
            var command = new SwingCommand(
                0,
                true,
                pitch.PlatePoint,
                idealTime,
                BattingApproach.Balanced);

            ContactProfile contact = resolver.Resolve(matchup, pitch, command, 1);

            Assert.That(contact.PitchResult, Is.EqualTo(PitchResult.InPlay));
            Assert.That(contact.Grade, Is.GreaterThanOrEqualTo(ContactGrade.Solid));
            Assert.That(contact.ExitVelocityMph, Is.GreaterThan(0d));
        }

        [Test]
        public void SwingContact_위치를크게놓치면헛스윙이고지켜보면실제위치로판정한다()
        {
            var resolver = new SwingContactResolver(BalanceTable.CreateDefault());
            PlateAppearanceMatchup matchup = CreateMatchup(50, 50);
            PitchFlightDescriptor strikePitch = CreatePitch(new PlatePoint(0d, 0d));
            double idealTime = resolver.GetIdealSwingTime01(strikePitch);
            ContactProfile miss = resolver.Resolve(
                matchup,
                strikePitch,
                new SwingCommand(
                    0,
                    true,
                    new PlatePoint(1.2d, 1.1d),
                    idealTime,
                    BattingApproach.Balanced),
                1);
            PitchFlightDescriptor ballPitch = CreatePitch(new PlatePoint(1.18d, -0.2d));
            ContactProfile take = resolver.Resolve(
                matchup,
                ballPitch,
                new SwingCommand(
                    0,
                    false,
                    default,
                    resolver.GetIdealSwingTime01(ballPitch),
                    BattingApproach.Patient),
                1);

            Assert.That(miss.PitchResult, Is.EqualTo(PitchResult.SwingingStrike));
            Assert.That(take.PitchResult, Is.EqualTo(PitchResult.Ball));
        }

        [Test]
        public void SwingExecutionAi_같은구종반복은타자의위치와타이밍예측을개선한다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            PlateAppearanceMatchup matchup = CreateMatchup(55, 55);
            var firstAi = new SwingExecutionAi(
                balance,
                new SequenceRandom(0.1d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d));
            var repeatedAi = new SwingExecutionAi(
                balance,
                new SequenceRandom(0.1d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d, 0.5d));
            BatterMiniGameRequest firstRequest = CreateBatterRequest(1);
            BatterMiniGameRequest repeatedRequest = CreateBatterRequest(3);

            SwingCommand first = firstAi.Select(firstRequest, matchup);
            SwingCommand repeated = repeatedAi.Select(repeatedRequest, matchup);
            double firstLocationError = Distance(first.BatPoint, firstRequest.Pitch.PlatePoint);
            double repeatedLocationError = Distance(repeated.BatPoint, repeatedRequest.Pitch.PlatePoint);
            double firstTimingError = System.Math.Abs(
                first.SwingInputTime01 - firstRequest.IdealSwingTime01);
            double repeatedTimingError = System.Math.Abs(
                repeated.SwingInputTime01 - repeatedRequest.IdealSwingTime01);

            Assert.That(first.DidSwing, Is.True);
            Assert.That(repeated.DidSwing, Is.True);
            Assert.That(repeatedLocationError, Is.LessThan(firstLocationError));
            Assert.That(repeatedTimingError, Is.LessThan(firstTimingError));
        }

        [Test]
        public void MatchSession_타자미니게임은스윙입력까지기다린뒤공식PitchEvent를낸다()
        {
            MatchInput input = CreateInput(775511UL);
            int controlledBatterId = input.AwayTeam.Lineup[0].Player.PlayerId;
            var session = new MatchSession(
                input,
                BalanceTable.CreateDefault(),
                controlledBatterId,
                controlsBatting: true,
                controlsPitching: false,
                InterventionLevel.FullControl,
                interactionMode: MatchInteractionMode.MiniGame,
                miniGameScope: MiniGameInterventionScope.ManualIntervention);

            MatchSessionStep decision = AdvanceToDecision(session);

            Assert.That(decision.SwingExecution.HasValue, Is.True);
            Assert.That(decision.SwingExecution.Value.BatterId, Is.EqualTo(controlledBatterId));
            Assert.That(decision.SwingExecution.Value.PitchNumber, Is.EqualTo(1));
            session.SubmitSwingExecution(decision.SwingExecution.Value.SuggestedSwing);

            MatchEvent pitchEvent = AdvanceToPitchEvent(session, controlledBatterId);
            Assert.That(pitchEvent.PitchPlayData.HasValue, Is.True);
            Assert.That(pitchEvent.PitchPlayData.Swing.RequestId, Is.Zero);
        }

        [Test]
        public void MatchSession_투수미니게임은보유구종과목표위치를요청한다()
        {
            MatchInput input = CreateInput(775512UL);
            int controlledPitcherId = input.HomeRoster.StartingPitcher.Player.PlayerId;
            var session = new MatchSession(
                input,
                BalanceTable.CreateDefault(),
                controlledPitcherId,
                controlsBatting: false,
                controlsPitching: true,
                InterventionLevel.FullControl,
                interactionMode: MatchInteractionMode.MiniGame,
                miniGameScope: MiniGameInterventionScope.AllInvolvement);

            MatchSessionStep decision = AdvanceToDecision(session);

            Assert.That(decision.PitchSelection.HasValue, Is.True);
            Assert.That(decision.PitchSelection.Value.PitcherId, Is.EqualTo(controlledPitcherId));
            Assert.That(decision.PitchSelection.Value.AvailablePitches.Count, Is.GreaterThanOrEqualTo(3));
            PitchSelectionCommand command = decision.PitchSelection.Value.SuggestedPitch;
            session.SubmitPitchSelection(command);

            MatchEvent pitchEvent = AdvanceToPitchEvent(session, decision.PitchSelection.Value.BatterId);
            Assert.That(pitchEvent.PitchPlayData.PitchSelection.RequestId, Is.EqualTo(command.RequestId));
            Assert.That(pitchEvent.PitchPlayData.PitchSelection.PitchType, Is.EqualTo(command.PitchType));
            Assert.That(pitchEvent.PitchPlayData.PitchSelection.TargetPoint, Is.EqualTo(command.TargetPoint));
        }

        [Test]
        public void MatchSession_같은Seed와같은추천입력은이벤트스트림이완전히같다()
        {
            SessionRun first = RunSuggestedInputSession(884422UL);
            SessionRun second = RunSuggestedInputSession(884422UL);
            MatchInput automaticInput = CreateInput(884422UL);
            MatchResult automatic = new MatchSimulator(
                    BalanceTable.CreateDefault(),
                    MatchRandomStreams.Create(automaticInput.RandomSeed))
                .Simulate(automaticInput);

            Assert.That(second.AwayRuns, Is.EqualTo(first.AwayRuns));
            Assert.That(second.HomeRuns, Is.EqualTo(first.HomeRuns));
            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (int index = 0; index < first.Events.Count; index++)
                Assert.That(second.Events[index], Is.EqualTo(first.Events[index]), $"Event {index}");
            Assert.That(first.AwayRuns, Is.EqualTo(automatic.AwayBoxScore.Runs));
            Assert.That(first.HomeRuns, Is.EqualTo(automatic.HomeBoxScore.Runs));
            Assert.That(first.Events.Count, Is.EqualTo(automatic.Events.Count));
            for (int index = 0; index < automatic.Events.Count; index++)
                AssertOfficialEventEqual(first.Events[index], automatic.Events[index], index);
        }

        private static MatchSessionStep AdvanceToDecision(MatchSession session)
        {
            for (int safety = 0; safety < 20000; safety++)
            {
                MatchSessionStep step = session.Advance();
                if (step.Kind == MatchSessionStepKind.DecisionRequired)
                    return step;
                if (step.Kind == MatchSessionStepKind.MatchEnded)
                    Assert.Fail("입력 요청 전에 경기가 끝났습니다.");
            }
            Assert.Fail("입력 요청까지 안전 한도를 초과했습니다.");
            return default;
        }

        private static MatchEvent AdvanceToPitchEvent(MatchSession session, int batterId)
        {
            for (int safety = 0; safety < 20000; safety++)
            {
                MatchSessionStep step = session.Advance();
                if (step.Kind == MatchSessionStepKind.EventProduced &&
                    step.Event.EventType == MatchEventType.Pitch &&
                    step.Event.BatterId == batterId)
                    return step.Event;
                if (step.Kind == MatchSessionStepKind.DecisionRequired)
                {
                    if (step.PitchSelection.HasValue)
                        session.SubmitPitchSelection(step.PitchSelection.Value.SuggestedPitch);
                    else if (step.SwingExecution.HasValue)
                        session.SubmitSwingExecution(step.SwingExecution.Value.SuggestedSwing);
                }
            }
            Assert.Fail("PitchEvent까지 안전 한도를 초과했습니다.");
            return default;
        }

        private static SessionRun RunSuggestedInputSession(ulong seed)
        {
            MatchInput input = CreateInput(seed);
            int playerId = input.AwayTeam.Lineup[0].Player.PlayerId;
            var session = new MatchSession(
                input,
                BalanceTable.CreateDefault(),
                playerId,
                controlsBatting: true,
                controlsPitching: false,
                InterventionLevel.FullControl,
                interactionMode: MatchInteractionMode.MiniGame,
                miniGameScope: MiniGameInterventionScope.AllInvolvement);
            var events = new List<MatchEvent>(512);
            for (int safety = 0; safety < 100000; safety++)
            {
                MatchSessionStep step = session.Advance();
                if (step.Kind == MatchSessionStepKind.EventProduced ||
                    step.Kind == MatchSessionStepKind.HalfInningEnded)
                {
                    events.Add(step.Event);
                    continue;
                }
                if (step.Kind == MatchSessionStepKind.DecisionRequired)
                {
                    if (step.SwingExecution.HasValue)
                    {
                        session.SubmitSwingExecution(step.SwingExecution.Value.SuggestedSwing);
                        continue;
                    }
                    Assert.Fail("타자 직접 플레이 세션에서 예상하지 않은 입력을 요청했습니다.");
                }
                if (step.Kind == MatchSessionStepKind.MatchEnded)
                {
                    return new SessionRun(
                        events,
                        step.Result.AwayBoxScore.Runs,
                        step.Result.HomeBoxScore.Runs);
                }
            }
            Assert.Fail("직접 플레이 경기 완료까지 안전 한도를 초과했습니다.");
            return default;
        }

        private static MatchInput CreateInput(ulong seed)
        {
            Team away = SimulationTestFactory.CreateTeam(1, 50, 50);
            Team home = SimulationTestFactory.CreateTeam(2, 50, 50);
            return new MatchInput(1, 1, seed, away, home);
        }

        private static PlateAppearanceMatchup CreateMatchup(int batterRating, int pitcherControl)
        {
            var batter = new Player(
                1,
                "미니게임 테스트 타자",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(
                    batterRating,
                    batterRating,
                    50,
                    50,
                    50,
                    batterRating),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
            var pitcher = new Player(
                2,
                "미니게임 테스트 투수",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 20, 20),
                new PitcherAttributes(55, 55, 55, 55, pitcherControl, 55));
            return new PlateAppearanceMatchup(batter, pitcher, 50d, false);
        }

        private static PitchFlightDescriptor CreatePitch(PlatePoint platePoint)
        {
            return new PitchFlightDescriptor(
                PitchType.FourSeamFastball,
                new PlatePoint(0.42d, 1.22d),
                platePoint,
                platePoint,
                91d,
                0.03d,
                0.10d,
                0.72d,
                453d,
                50d,
                false);
        }

        private static BatterMiniGameRequest CreateBatterRequest(int consecutivePitchTypeUses)
        {
            PitchFlightDescriptor pitch = CreatePitch(new PlatePoint(1.12d, -0.15d));
            const double idealSwingTime = 0.91d;
            return new BatterMiniGameRequest(
                0,
                0,
                1,
                1,
                InningHalf.Top,
                1,
                2,
                1,
                0,
                0,
                0,
                0,
                0,
                default,
                pitch,
                consecutivePitchTypeUses,
                idealSwingTime,
                BattingApproach.Balanced,
                MiniGameAssistRule.Standard,
                new SwingCommand(
                    0,
                    false,
                    default,
                    idealSwingTime,
                    BattingApproach.Balanced));
        }

        private static double Distance(PlatePoint first, PlatePoint second)
        {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            return System.Math.Sqrt(x * x + y * y);
        }

        private static void AssertOfficialEventEqual(
            MatchEvent direct,
            MatchEvent automatic,
            int index)
        {
            string message = $"Automatic Event {index}";
            Assert.That(direct.EventType, Is.EqualTo(automatic.EventType), message);
            Assert.That(direct.Inning, Is.EqualTo(automatic.Inning), message);
            Assert.That(direct.Half, Is.EqualTo(automatic.Half), message);
            Assert.That(direct.BatterId, Is.EqualTo(automatic.BatterId), message);
            Assert.That(direct.PitcherId, Is.EqualTo(automatic.PitcherId), message);
            Assert.That(direct.PlayerId, Is.EqualTo(automatic.PlayerId), message);
            Assert.That(direct.PitchResult, Is.EqualTo(automatic.PitchResult), message);
            Assert.That(direct.PlateAppearanceResult, Is.EqualTo(automatic.PlateAppearanceResult), message);
            Assert.That(direct.Balls, Is.EqualTo(automatic.Balls), message);
            Assert.That(direct.Strikes, Is.EqualTo(automatic.Strikes), message);
            Assert.That(direct.Outs, Is.EqualTo(automatic.Outs), message);
            Assert.That(direct.AwayScore, Is.EqualTo(automatic.AwayScore), message);
            Assert.That(direct.HomeScore, Is.EqualTo(automatic.HomeScore), message);
            Assert.That(direct.PitchPlayData.HasValue, Is.EqualTo(automatic.PitchPlayData.HasValue), message);
            if (!direct.PitchPlayData.HasValue)
                return;
            Assert.That(
                direct.PitchPlayData.PitchSelection.PitchType,
                Is.EqualTo(automatic.PitchPlayData.PitchSelection.PitchType),
                message);
            Assert.That(
                direct.PitchPlayData.Pitch.PlatePoint,
                Is.EqualTo(automatic.PitchPlayData.Pitch.PlatePoint),
                message);
            Assert.That(
                direct.PitchPlayData.Swing.BatPoint,
                Is.EqualTo(automatic.PitchPlayData.Swing.BatPoint),
                message);
            Assert.That(
                direct.PitchPlayData.Contact.Quality,
                Is.EqualTo(automatic.PitchPlayData.Contact.Quality),
                message);
        }

        private readonly struct SessionRun
        {
            public SessionRun(List<MatchEvent> events, int awayRuns, int homeRuns)
            {
                Events = events;
                AwayRuns = awayRuns;
                HomeRuns = homeRuns;
            }

            public List<MatchEvent> Events { get; }
            public int AwayRuns { get; }
            public int HomeRuns { get; }
        }

    }
}

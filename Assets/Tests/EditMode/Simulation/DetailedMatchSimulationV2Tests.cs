using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>V2 경기 흐름의 피로·전술·교체·결정론 계약을 검증한다.</summary>
    public sealed class DetailedMatchSimulationV2Tests
    {
        [Test]
        public void Fatigue_Stamina가높으면같은투구수에서피로가낮다()
        {
            PitcherFatigueResolver resolver = CreateFatigueResolver();
            PitcherGameState low = resolver.CreateState(new PitcherRosterEntry(
                CreatePitcher(1, stamina: 30), PitcherRole.Starter));
            PitcherGameState high = resolver.CreateState(new PitcherRosterEntry(
                CreatePitcher(2, stamina: 90), PitcherRole.Starter));

            for (int index = 0; index < 80; index++)
            {
                low.RecordPitch();
                high.RecordPitch();
            }

            Assert.That(low.FatigueRatio, Is.GreaterThan(high.FatigueRatio));
        }

        [Test]
        public void Fatigue_55퍼센트이하는하락없고한계에서는제구가더크게하락한다()
        {
            PitcherFatigueResolver resolver = CreateFatigueResolver();
            Player pitcher = CreatePitcher(3, stamina: 50);
            PitcherGameState state = resolver.CreateState(new PitcherRosterEntry(pitcher, PitcherRole.Starter));
            while (state.FatigueRatio < 0.50d) state.RecordPitch();
            EffectivePitcherRatings fresh = resolver.Resolve(state, PitchingApproach.Balanced);
            Assert.That(fresh.Control, Is.EqualTo(pitcher.PitcherAttributes.Control).Within(0.001d));

            while (state.FatigueRatio < 1d) state.RecordPitch();
            EffectivePitcherRatings tired = resolver.Resolve(state, PitchingApproach.Balanced);
            double velocityLoss = pitcher.PitcherAttributes.Velocity - tired.Velocity;
            double controlLoss = pitcher.PitcherAttributes.Control - tired.Control;
            Assert.That(controlLoss, Is.GreaterThan(velocityLoss));
        }

        [Test]
        public void TacticalAi_2아웃번트와1루주자있는고의사구를선택하지않는다()
        {
            TacticalAiResolver ai = new TacticalAiResolver(
                BalanceTable.CreateDefault().Match.Tactical,
                RunExpectancy24.CreateDefault());
            DecisionContext context = CreateDecisionContext(
                outs: 2,
                new BaseStateSnapshot(first: true, second: false, third: false));

            Assert.That(ai.ShouldSacrificeBunt(context), Is.False);
            Assert.That(ai.ShouldIntentionalWalk(context), Is.False);
        }

        [Test]
        public void TacticalAi_1루가비고다음타자가약하면고의사구를선택할수있다()
        {
            TacticalAiResolver ai = new TacticalAiResolver(
                BalanceTable.CreateDefault().Match.Tactical,
                RunExpectancy24.CreateDefault());
            Player pitcher = CreatePitcher(93, 55);
            PitcherGameState state = CreateFatigueResolver().CreateState(
                new PitcherRosterEntry(pitcher, PitcherRole.Starter));
            var context = new DecisionContext(
                inning: 9,
                InningHalf.Bottom,
                scoreDifference: 0,
                outs: 2,
                new BaseStateSnapshot(first: false, second: true, third: false),
                CreateThreatBatter(94, contact: 95, power: 100),
                pitcher,
                CreateThreatBatter(95, contact: 15, power: 10),
                LeverageTier.Critical,
                state,
                MatchRules.CreateDefault(requiresWinner: false),
                ManagerTacticalProfile.Balanced);

            Assert.That(ai.ShouldIntentionalWalk(context), Is.True);
        }

        [Test]
        public void Fielding_높은Range는도달확률을높이고낮은Hands만루틴실책을낸다()
        {
            DetailedFieldingBalance balance = BalanceTable.CreateDefault().Match.Fielding;
            var ball = new BattedBallDescriptor(
                BattedBallType.GroundBall,
                BattedBallDirection.Center,
                FieldZone.Shortstop,
                quality: 50d,
                BallFlightBand.Short,
                BallPaceBand.Medium,
                isHomeRun: false);
            Player low = CreateBatter(96, 50, 0, 50);
            Player high = CreateBatter(97, 50, 100, 50);
            FieldingPlayOutcome lowOutcome = new FieldingPlayResolver(
                balance, new SequenceRandom(0d, 0.01d, 0.5d, 0.5d)).Resolve(
                ball, low, PlayerPosition.Shortstop, DefensiveAlignment.Standard, 50, 50, false);
            FieldingPlayOutcome highOutcome = new FieldingPlayResolver(
                balance, new SequenceRandom(0d, 0.01d, 0.5d, 0.5d)).Resolve(
                ball, high, PlayerPosition.Shortstop, DefensiveAlignment.Standard, 50, 50, false);

            Assert.That(highOutcome.ReachChance, Is.GreaterThan(lowOutcome.ReachChance));
            Assert.That(lowOutcome.Result, Is.EqualTo(PlateAppearanceResult.ReachedOnError));
            Assert.That(highOutcome.Result, Is.EqualTo(PlateAppearanceResult.GroundOut));
        }

        [Test]
        public void Fielding_도달실패는실책이아닌안타다()
        {
            var ball = new BattedBallDescriptor(
                BattedBallType.GroundBall,
                BattedBallDirection.Opposite,
                FieldZone.Shortstop,
                quality: 55d,
                BallFlightBand.Short,
                BallPaceBand.Medium,
                isHomeRun: false);
            FieldingPlayOutcome outcome = new FieldingPlayResolver(
                BalanceTable.CreateDefault().Match.Fielding,
                new SequenceRandom(0.999d)).Resolve(
                ball,
                CreateBatter(98, 50, 50, 50),
                PlayerPosition.Shortstop,
                DefensiveAlignment.Standard,
                50,
                50,
                false);

            Assert.That(outcome.FailureType, Is.EqualTo(FieldingFailureType.Reach));
            Assert.That(outcome.Result, Is.Not.EqualTo(PlateAppearanceResult.ReachedOnError));
        }

        [Test]
        public void Fielding_송구능력은Defense와독립적으로Arm프로필에반영된다()
        {
            Player weakArm = CreateBatter(99, 50, 60, 50, arm: 20);
            Player strongArm = CreateBatter(99, 50, 60, 50, arm: 90);

            FieldingProfile weakProfile = FieldingProfile.Derive(weakArm, PlayerPosition.Shortstop);
            FieldingProfile strongProfile = FieldingProfile.Derive(strongArm, PlayerPosition.Shortstop);

            Assert.That(strongProfile.Range, Is.EqualTo(weakProfile.Range));
            Assert.That(strongProfile.Hands, Is.EqualTo(weakProfile.Hands));
            Assert.That(strongProfile.Arm, Is.GreaterThan(weakProfile.Arm));
        }

        [Test]
        public void SubstitutionLedger_퇴장선수는재출전할수없다()
        {
            var ledger = new SubstitutionLedger();
            ledger.RegisterStarter(1);
            ledger.Record(new SubstitutionRecord(
                7, InningHalf.Top, 2, 1, SubstitutionType.PinchHitter, DecisionReasonCode.ExpectedValue));

            Assert.Throws<InvalidOperationException>(() => ledger.Record(new SubstitutionRecord(
                8, InningHalf.Bottom, 1, 2, SubstitutionType.DefensiveReplacement,
                DecisionReasonCode.DefensiveStrategy)));
        }

        [Test]
        public void Steal_Speed는성공률을높이고포수Arm은낮춘다()
        {
            TacticalAiResolver ai = new TacticalAiResolver(
                BalanceTable.CreateDefault().Match.Tactical,
                RunExpectancy24.CreateDefault());
            Player slow = CreateBatter(10, 35, 50, 50);
            Player fast = CreateBatter(11, 85, 50, 50);
            Player weakCatcher = CreateBatter(12, 50, 25, 50, PlayerPosition.Catcher);
            Player strongCatcher = CreateBatter(13, 50, 90, 50, PlayerPosition.Catcher);
            Player pitcher = CreatePitcher(14, 50);

            Assert.That(
                ai.CalculateStealSuccess(fast, weakCatcher, pitcher),
                Is.GreaterThan(ai.CalculateStealSuccess(slow, weakCatcher, pitcher)));
            Assert.That(
                ai.CalculateStealSuccess(fast, strongCatcher, pitcher),
                Is.LessThan(ai.CalculateStealSuccess(fast, weakCatcher, pitcher)));
        }

        [Test]
        public void Match_같은Seed와V2입력은이벤트스트림이완전히같다()
        {
            MatchInput input = CreateDetailedInput(19031UL, MatchRules.CreateDefault(requiresWinner: false));
            MatchResult first = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);
            MatchResult second = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);

            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (int index = 0; index < first.Events.Count; index++)
                Assert.That(second.Events[index], Is.EqualTo(first.Events[index]), $"Event {index}");
        }

        [Test]
        public void Match_다인불펜에서한경기세명이상등판할수있다()
        {
            bool found = false;
            for (ulong seed = 1; seed <= 24 && !found; seed++)
            {
                MatchInput input = CreateDetailedInput(seed, MatchRules.CreateDefault(requiresWinner: false));
                MatchResult result = new MatchSimulator(
                    BalanceTable.CreateDefault(), MatchRandomStreams.Create(seed)).Simulate(input);
                int awayPitchers = CountUsedPitchers(result, input.AwayRoster.TeamId);
                int homePitchers = CountUsedPitchers(result, input.HomeRoster.TeamId);
                found = awayPitchers >= 3 || homePitchers >= 3;
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void MatchSession_타격선택은다음투구전까지진행한다()
        {
            MatchInput input = CreateDetailedInput(77551UL, MatchRules.CreateDefault(requiresWinner: false));
            int controlledId = input.AwayRoster.StartingLineup[0].Player.PlayerId;
            var session = new MatchSession(
                input,
                BalanceTable.CreateDefault(),
                controlledId,
                controlsBatting: true,
                controlsPitching: false,
                InterventionLevel.FullControl);

            MatchSessionStep first = AdvanceToDecision(session);
            Assert.That(first.BattingDecision.HasValue, Is.True);
            Assert.That(first.BattingDecision.Value.DecisionIndex, Is.Zero);
            session.SubmitBattingApproach(BattingApproach.Power);
            MatchSessionStep second = AdvanceToDecision(session);
            Assert.That(second.BattingDecision.HasValue, Is.True);
            Assert.That(second.BattingDecision.Value.DecisionIndex, Is.EqualTo(1));
            Assert.That(second.BattingDecision.Value.BatterId, Is.EqualTo(controlledId));
            Assert.That(second.BattingDecision.Value.PitchNumber, Is.EqualTo(2));
            Assert.That(
                second.BattingDecision.Value.Balls + second.BattingDecision.Value.Strikes,
                Is.EqualTo(1));
        }

        [Test]
        public void ExtraInnings_정규시즌제한동점은무승부이고승자필요규칙은계속한다()
        {
            ulong tiedSeed = FindOneInningTieSeed();
            MatchInput drawInput = CreateDetailedInput(tiedSeed, new MatchRules(
                regulationInnings: 1,
                maximumRegulationExtraInnings: 0,
                extraInningPolicy: ExtraInningPolicy.DrawAtLimit,
                automaticRunnerStartInning: 2,
                usesDesignatedHitter: true,
                intentionalWalkPitchCount: 0));
            MatchResult draw = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(tiedSeed)).Simulate(drawInput);
            Assert.That(draw.IsTie, Is.True);
            Assert.That(ContainsEvent(draw, MatchEventType.MatchEndedAsDraw), Is.True);

            MatchInput winnerInput = CreateDetailedInput(tiedSeed, new MatchRules(
                regulationInnings: 1,
                maximumRegulationExtraInnings: 0,
                extraInningPolicy: ExtraInningPolicy.ContinueUntilWinner,
                automaticRunnerStartInning: 2,
                usesDesignatedHitter: true,
                intentionalWalkPitchCount: 0));
            MatchResult winner = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(tiedSeed)).Simulate(winnerInput);
            Assert.That(winner.IsTie, Is.False);
            Assert.That(ContainsEvent(winner, MatchEventType.MatchEndedAsDraw), Is.False);
        }

        private static PitcherFatigueResolver CreateFatigueResolver()
        {
            return new PitcherFatigueResolver(BalanceTable.CreateDefault().Match);
        }

        private static MatchSessionStep AdvanceToDecision(MatchSession session)
        {
            for (int safety = 0; safety < 5000; safety++)
            {
                MatchSessionStep step = session.Advance();
                if (step.Kind == MatchSessionStepKind.DecisionRequired ||
                    step.Kind == MatchSessionStepKind.MatchEnded)
                    return step;
            }
            Assert.Fail("결정 지점까지 안전 한도 안에 도달하지 못했습니다.");
            return default;
        }

        private static ulong FindOneInningTieSeed()
        {
            for (ulong seed = 1; seed <= 500; seed++)
            {
                MatchInput input = CreateDetailedInput(seed, new MatchRules(
                    regulationInnings: 1,
                    maximumRegulationExtraInnings: 0,
                    extraInningPolicy: ExtraInningPolicy.DrawAtLimit,
                    automaticRunnerStartInning: 2,
                    usesDesignatedHitter: true,
                    intentionalWalkPitchCount: 0));
                MatchResult result = new MatchSimulator(
                    BalanceTable.CreateDefault(), MatchRandomStreams.Create(seed)).Simulate(input);
                if (result.IsTie) return seed;
            }
            Assert.Fail("1이닝 동점 Seed를 찾지 못했습니다.");
            return 0;
        }

        private static bool ContainsEvent(MatchResult result, MatchEventType type)
        {
            for (int index = 0; index < result.Events.Count; index++)
            {
                if (result.Events[index].EventType == type) return true;
            }
            return false;
        }

        private static int CountUsedPitchers(MatchResult result, int teamId)
        {
            TeamBoxScore box = result.AwayBoxScore.TeamId == teamId
                ? result.AwayBoxScore
                : result.HomeBoxScore;
            int count = 0;
            for (int index = 0; index < box.PitchingLines.Count; index++)
            {
                if (box.PitchingLines[index].BattersFaced > 0) count++;
            }
            return count;
        }

        private static MatchInput CreateDetailedInput(ulong seed, MatchRules rules)
        {
            return new MatchInput(
                1,
                (int)(seed % int.MaxValue) + 1,
                seed,
                CreateRoster(1),
                CreateRoster(2),
                rules);
        }

        private static MatchRosterSnapshot CreateRoster(int teamId)
        {
            Team team = SimulationTestFactory.CreateTeam(teamId, 50, 50);
            var bullpen = new List<PitcherRosterEntry>
            {
                new PitcherRosterEntry(CreatePitcher(teamId * 1000 + 1, 58), PitcherRole.LongRelief),
                new PitcherRosterEntry(CreatePitcher(teamId * 1000 + 2, 55), PitcherRole.MiddleRelief),
                new PitcherRosterEntry(CreatePitcher(teamId * 1000 + 3, 62), PitcherRole.Setup),
                new PitcherRosterEntry(CreatePitcher(teamId * 1000 + 4, 65), PitcherRole.Closer)
            };
            return new MatchRosterSnapshot(
                team.TeamId,
                team.Name,
                team.Lineup,
                new PitcherRosterEntry(team.StartingPitcher, PitcherRole.Starter),
                bullpen,
                Array.Empty<Player>(),
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static DecisionContext CreateDecisionContext(int outs, BaseStateSnapshot bases)
        {
            Player pitcher = CreatePitcher(90, 50);
            PitcherFatigueResolver resolver = CreateFatigueResolver();
            PitcherGameState state = resolver.CreateState(new PitcherRosterEntry(pitcher, PitcherRole.Starter));
            return new DecisionContext(
                inning: 8,
                InningHalf.Top,
                scoreDifference: 0,
                outs,
                bases,
                CreateBatter(91, 45, 50, 45),
                pitcher,
                CreateBatter(92, 40, 50, 40),
                LeverageTier.High,
                state,
                MatchRules.CreateDefault(requiresWinner: false),
                ManagerTacticalProfile.Balanced);
        }

        private static Player CreatePitcher(int id, int stamina)
        {
            return new Player(
                id,
                $"투수 {id}",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 45, 20),
                new PitcherAttributes(stamina, 60, 60, 60, 60, 60));
        }

        private static Player CreateBatter(
            int id,
            int speed,
            int defense,
            int mental,
            PlayerPosition position = PlayerPosition.Shortstop,
            int arm = 40)
        {
            return new Player(
                id,
                $"타자 {id}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(50, 50, speed, arm, defense, mental),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
        }

        private static Player CreateThreatBatter(int id, int contact, int power)
        {
            return new Player(
                id,
                $"타자 {id}",
                PlayerPosition.DesignatedHitter,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(contact, power, 50, 30, 40, 60),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
        }
    }
}

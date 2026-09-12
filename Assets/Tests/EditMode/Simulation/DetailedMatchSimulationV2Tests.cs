using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;
using NUnit.Framework;
using GrowthSkillTraitIds = Baseball.Core.Growth.SkillTraitIds;

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
        public void Fatigue_역사투구량배율은경기용량과최근부하를각각보존한다()
        {
            PitcherFatigueResolver resolver = CreateFatigueResolver();
            Player pitcher = CreatePitcher(101, stamina: 50);
            var workload = new RecentPitchingWorkload(40, 20, 10);
            double baseline = resolver.CalculateEffectiveCapacity(new PitcherRosterEntry(
                pitcher,
                PitcherRole.MiddleRelief,
                recentWorkload: workload));
            double durable = resolver.CalculateEffectiveCapacity(new PitcherRosterEntry(
                pitcher,
                PitcherRole.MiddleRelief,
                recentWorkload: workload,
                capacityMultiplier: 2d,
                recoveryMultiplier: 2d));

            Assert.That(durable, Is.GreaterThan(baseline * 2d));
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
        public void Fielding_외야도달실패가항상2루타가되지않고Power와Speed를소비한다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            var ball = new BattedBallDescriptor(
                BattedBallType.FlyBall,
                BattedBallDirection.Center,
                FieldZone.CenterField,
                quality: 65d,
                BallFlightBand.Medium,
                BallPaceBand.Fast,
                isHomeRun: false);
            Player fielder = CreateBatter(981, 50, 50, 50);

            PlateAppearanceResult triple = new FieldingPlayResolver(
                balance.Match.Fielding, new SequenceRandom(0.999d, 0.01d)).Resolve(
                ball, fielder, PlayerPosition.CenterField, DefensiveAlignment.Standard,
                batterSpeed: 75, leadRunnerSpeed: 50, canAttemptDoublePlay: false, batterPower: 50).Result;
            PlateAppearanceResult extraBaseHit = new FieldingPlayResolver(
                balance.Match.Fielding, new SequenceRandom(0.999d, 0.25d)).Resolve(
                ball, fielder, PlayerPosition.CenterField, DefensiveAlignment.Standard,
                batterSpeed: 50, leadRunnerSpeed: 50, canAttemptDoublePlay: false, batterPower: 70).Result;
            PlateAppearanceResult single = new FieldingPlayResolver(
                balance.Match.Fielding, new SequenceRandom(0.999d, 0.90d)).Resolve(
                ball, fielder, PlayerPosition.CenterField, DefensiveAlignment.Standard,
                batterSpeed: 50, leadRunnerSpeed: 50, canAttemptDoublePlay: false, batterPower: 70).Result;

            Assert.That(triple, Is.EqualTo(PlateAppearanceResult.Triple));
            Assert.That(extraBaseHit, Is.EqualTo(PlateAppearanceResult.Double));
            Assert.That(single, Is.EqualTo(PlateAppearanceResult.Single));
        }

        [Test]
        public void Fielding_번트차이는수비프로필에영향을주지않는다()
        {
            Player weakBunt = CreateBatter(99, 50, 60, 50, bunt: 20);
            Player strongBunt = CreateBatter(99, 50, 60, 50, bunt: 90);

            FieldingProfile weakProfile = FieldingProfile.Derive(weakBunt, PlayerPosition.Shortstop);
            FieldingProfile strongProfile = FieldingProfile.Derive(strongBunt, PlayerPosition.Shortstop);

            Assert.That(strongProfile.Range, Is.EqualTo(weakProfile.Range));
            Assert.That(strongProfile.Hands, Is.EqualTo(weakProfile.Hands));
            Assert.That(strongProfile.Arm, Is.EqualTo(weakProfile.Arm));
        }

        [Test]
        public void Fielding_수비보너스는송구에한번만적용되고번트는보존된다()
        {
            Player player = CreateBatter(99, 50, 60, 50, bunt: 20);
            FieldingProfile baseline = FieldingProfile.Derive(player, PlayerPosition.Shortstop);
            FieldingProfile enhanced = FieldingProfile.Derive(player, PlayerPosition.Shortstop, 7);
            Assert.That(enhanced.Range - baseline.Range, Is.EqualTo(7));
            Assert.That(enhanced.Hands - baseline.Hands, Is.EqualTo(7));
            Assert.That(enhanced.Arm - baseline.Arm, Is.EqualTo(7));
            Assert.That(player.BatterAttributes.Bunt, Is.EqualTo(20));
        }

        [Test]
        public void Bunt_수비차이는번트타구에영향을주지않는다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            Player pitcher = CreatePitcher(100, 60);
            var weak = new PlateAppearanceMatchup(CreateBatter(99, 50, 20, 50, bunt: 70), pitcher, 50, false);
            var strong = new PlateAppearanceMatchup(CreateBatter(99, 50, 90, 50, bunt: 70), pitcher, 50, false);
            var left = new BattedBallResolver(balance, new Pcg32Random(8123UL));
            var right = new BattedBallResolver(balance, new Pcg32Random(8123UL));
            for (int index = 0; index < 1000; index++)
                Assert.That(left.Resolve(weak, BattingApproach.Bunt), Is.EqualTo(right.Resolve(strong, BattingApproach.Bunt)));
        }

        [Test]
        public void SkillTrait_공격적주루는경계상황에서추가진루선택을연다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            var resolver = new BaserunningResolver(
                balance.BaseRunning,
                new SequenceRandom(0d),
                balance.Growth.SkillTraits);
            Player normal = CreateBatter(901, 50, 50, 50);
            Player aggressive = CreateBatter(
                902,
                50,
                50,
                50,
                traitIds: new[] { GrowthSkillTraitIds.AggressiveBaserunning });

            BaserunningDecision normalDecision = resolver.DecideExtraBase(
                0.65d, normal, 50, 1, 5, 0, RunningApproach.Balanced);
            BaserunningDecision traitDecision = resolver.DecideExtraBase(
                0.65d, aggressive, 50, 1, 5, 0, RunningApproach.Balanced);

            Assert.That(normalDecision.ShouldAttempt, Is.False);
            Assert.That(traitDecision.ShouldAttempt, Is.True);
            Assert.That(traitDecision.SuccessChance, Is.EqualTo(normalDecision.SuccessChance));
        }

        [Test]
        public void Baserunning_목표진루율과송구세이프율을분리한다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            Player runner = CreateBatter(910, 50, 50, 50);
            var holdResolver = new BaserunningResolver(balance.BaseRunning, new SequenceRandom(0.99d));
            var safeResolver = new BaserunningResolver(balance.BaseRunning, new SequenceRandom(0d, 0d));
            var outResolver = new BaserunningResolver(balance.BaseRunning, new SequenceRandom(0d, 0.99d));

            ExtraBaseOutcome hold = holdResolver.ResolveExtraBase(
                0.58d, runner, 50, 0, 5, 0, RunningApproach.Balanced);
            ExtraBaseOutcome safe = safeResolver.ResolveExtraBase(
                0.58d, runner, 50, 0, 5, 0, RunningApproach.Balanced);
            ExtraBaseOutcome thrownOut = outResolver.ResolveExtraBase(
                0.58d, runner, 50, 0, 5, 0, RunningApproach.Balanced);

            Assert.That(hold, Is.EqualTo(ExtraBaseOutcome.Hold));
            Assert.That(safe, Is.EqualTo(ExtraBaseOutcome.Safe));
            Assert.That(thrownOut, Is.EqualTo(ExtraBaseOutcome.Out));
        }

        [Test]
        public void SkillTrait_수비집중은같은타구의도달확률을높인다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            var ball = new BattedBallDescriptor(
                BattedBallType.GroundBall,
                BattedBallDirection.Center,
                FieldZone.Shortstop,
                quality: 55d,
                BallFlightBand.Short,
                BallPaceBand.Medium,
                isHomeRun: false);
            Player normal = CreateBatter(903, 50, 60, 50);
            Player focused = CreateBatter(
                903,
                50,
                60,
                50,
                traitIds: new[] { GrowthSkillTraitIds.DefensiveFocus });

            FieldingPlayOutcome normalOutcome = new FieldingPlayResolver(
                balance.Match.Fielding,
                new SequenceRandom(0d, 0.5d, 0.5d),
                balance.Growth.SkillTraits).Resolve(
                ball, normal, PlayerPosition.Shortstop, DefensiveAlignment.Standard, 50, 50, false);
            FieldingPlayOutcome focusedOutcome = new FieldingPlayResolver(
                balance.Match.Fielding,
                new SequenceRandom(0d, 0.5d, 0.5d),
                balance.Growth.SkillTraits).Resolve(
                ball, focused, PlayerPosition.Shortstop, DefensiveAlignment.Standard, 50, 50, false);

            Assert.That(focusedOutcome.ReachChance, Is.GreaterThan(normalOutcome.ReachChance));
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
        public void Match_무관전자Profile은표현출력없이같은BoxScore를만든다()
        {
            MatchInput input = CreateDetailedInput(19032UL, MatchRules.CreateDefault(requiresWinner: false));
            BalanceTable balance = BalanceTable.CreateDefault();
            MatchResult full = new MatchSimulator(balance, MatchRandomStreams.Create(input.RandomSeed))
                .Simulate(input);
            MatchResult background = new MatchSimulator(balance, MatchRandomStreams.Create(input.RandomSeed))
                .Simulate(
                    input,
                    NullMatchEventSink.Instance,
                    MatchExecutionProfile.DetailedBackground);

            Assert.That(background.Events, Is.Empty);
            Assert.That(background.DecisionTrace, Is.Empty);
            Assert.That(background.InningsPlayed, Is.EqualTo(full.InningsPlayed));
            AssertBoxScoreEqual(full.AwayBoxScore, background.AwayBoxScore);
            AssertBoxScoreEqual(full.HomeBoxScore, background.HomeBoxScore);
            Assert.That(background.PitcherUsage.Count, Is.EqualTo(full.PitcherUsage.Count));
            for (int index = 0; index < full.PitcherUsage.Count; index++)
                AssertPublicScalarPropertiesEqual(full.PitcherUsage[index], background.PitcherUsage[index]);
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
            Assert.That(CountPitchingWins(draw), Is.Zero);
            Assert.That(CountPitchingLosses(draw), Is.Zero);

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
            Assert.That(CountPitchingWins(winner), Is.EqualTo(1));
            Assert.That(CountPitchingLosses(winner), Is.EqualTo(1));
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

        private static int CountPitchingWins(MatchResult result)
        {
            return CountPitchingWins(result.AwayBoxScore) + CountPitchingWins(result.HomeBoxScore);
        }

        private static int CountPitchingWins(TeamBoxScore boxScore)
        {
            int count = 0;
            for (int index = 0; index < boxScore.PitchingLines.Count; index++)
            {
                if (boxScore.PitchingLines[index].HasWin)
                    count++;
            }
            return count;
        }

        private static int CountPitchingLosses(MatchResult result)
        {
            return CountPitchingLosses(result.AwayBoxScore) + CountPitchingLosses(result.HomeBoxScore);
        }

        private static int CountPitchingLosses(TeamBoxScore boxScore)
        {
            int count = 0;
            for (int index = 0; index < boxScore.PitchingLines.Count; index++)
            {
                if (boxScore.PitchingLines[index].HasLoss)
                    count++;
            }
            return count;
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

        private static void AssertBoxScoreEqual(TeamBoxScore expected, TeamBoxScore actual)
        {
            Assert.That(actual.TeamId, Is.EqualTo(expected.TeamId));
            Assert.That(actual.Runs, Is.EqualTo(expected.Runs));
            Assert.That(actual.Hits, Is.EqualTo(expected.Hits));
            Assert.That(actual.Errors, Is.EqualTo(expected.Errors));
            Assert.That(actual.RunsByInning, Is.EqualTo(expected.RunsByInning));
            Assert.That(actual.BattingLines.Count, Is.EqualTo(expected.BattingLines.Count));
            for (int index = 0; index < expected.BattingLines.Count; index++)
                AssertPublicScalarPropertiesEqual(expected.BattingLines[index], actual.BattingLines[index]);
            Assert.That(actual.PitchingLines.Count, Is.EqualTo(expected.PitchingLines.Count));
            for (int index = 0; index < expected.PitchingLines.Count; index++)
                AssertPublicScalarPropertiesEqual(expected.PitchingLines[index], actual.PitchingLines[index]);
            Assert.That(actual.FieldingLines.Count, Is.EqualTo(expected.FieldingLines.Count));
            for (int index = 0; index < expected.FieldingLines.Count; index++)
                AssertPublicScalarPropertiesEqual(expected.FieldingLines[index], actual.FieldingLines[index]);
        }

        private static void AssertPublicScalarPropertiesEqual<T>(T expected, T actual)
        {
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                if (property.GetIndexParameters().Length != 0)
                    continue;
                Assert.That(
                    property.GetValue(actual),
                    Is.EqualTo(property.GetValue(expected)),
                    property.Name);
            }
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

        [Test]
        public void TacticalAi_2아웃도루도성공확률의기대값으로판단한다()
        {
            var balance = BalanceTable.CreateDefault().Match.Tactical;
            var expectancy = RunExpectancy24.CreateDefault();
            var ai = new TacticalAiResolver(balance, expectancy);
            DecisionContext context = CreateDecisionContext(2, new BaseStateSnapshot(true, false, false));
            Player runner = CreateBatter(301, 100, 50, 100);
            Player catcher = CreateBatter(302, 50, 50, 50, PlayerPosition.Catcher);
            TacticalDecision decision = ai.EvaluateSteal(context, runner, catcher);
            double success = ai.CalculateStealSuccess(runner, catcher, context.Pitcher);
            Assert.That(decision.Score, Is.EqualTo(success * expectancy.Get(2, 2) - expectancy.Get(2, 1) + 0.035d).Within(1e-12));
            Assert.That(decision.ShouldAct, Is.True);
        }

        [Test]
        public void TacticalAi_경계도루는항상실행되지않고이득에따라빈도가증가한다()
        {
            var balance = BalanceTable.CreateDefault().Match.Tactical;
            var ai = new TacticalAiResolver(balance, RunExpectancy24.CreateDefault());
            double threshold = balance.StealAttemptUtilityThreshold;
            Assert.That(ai.CalculateStealAttemptProbability(new TacticalDecision(false, threshold - 1d, threshold)), Is.Zero);
            double small = ai.CalculateStealAttemptProbability(new TacticalDecision(true, threshold + 0.01d, threshold));
            double large = ai.CalculateStealAttemptProbability(new TacticalDecision(true, threshold + 0.08d, threshold));
            Assert.That(small, Is.InRange(0.01d, 0.99d));
            Assert.That(large, Is.GreaterThan(small));
            Assert.That(ai.CalculateStealAttemptProbability(new TacticalDecision(true, threshold + 10d, threshold)), Is.EqualTo(1d));
        }

        [Test]
        public void TacticalAi_1루도루기대값에서3루주자를보존한다()
        {
            var balance = BalanceTable.CreateDefault().Match.Tactical;
            var expectancy = RunExpectancy24.CreateDefault();
            var ai = new TacticalAiResolver(balance, expectancy);
            DecisionContext context = CreateDecisionContext(0, new BaseStateSnapshot(true, false, true));
            Player runner = CreateBatter(301, 85, 50, 85);
            Player catcher = CreateBatter(302, 50, 50, 50, PlayerPosition.Catcher);
            double success = ai.CalculateStealSuccess(runner, catcher, context.Pitcher);
            double expected = success * expectancy.Get(0, 6) + (1d - success) * expectancy.Get(1, 4) - expectancy.Get(0, 5) + 0.035d;
            Assert.That(ai.EvaluateSteal(context, runner, catcher).Score, Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void PitcherChange_더약한불펜으로교체하는비용도평가한다()
        {
            var ai = new PitcherManagementAi(BalanceTable.CreateDefault().Match.BullpenManagement);
            DecisionContext context = CreateDecisionContext(1, new BaseStateSnapshot(true, false, false));
            double weaker = ai.Evaluate(context, 3, 1d, -15d).PullScore;
            double equal = ai.Evaluate(context, 3, 1d, 0d).PullScore;
            double stronger = ai.Evaluate(context, 3, 1d, 15d).PullScore;
            Assert.That(weaker, Is.LessThan(equal));
            Assert.That(stronger, Is.GreaterThan(equal));
        }

        [Test]
        public void RelieverSelection_다이닝회복여유가있으면마무리보존비용이낮다()
        {
            var ai = new PitcherManagementAi(BalanceTable.CreateDefault().Match.BullpenManagement);
            Player pitcher = CreatePitcher(90, 60);
            var normal = new PitcherGameState(new PitcherRosterEntry(pitcher, PitcherRole.Closer), 30);
            var durable = new PitcherGameState(new PitcherRosterEntry(pitcher, PitcherRole.Closer,
                capacityMultiplier: 2, recoveryMultiplier: 2), 60);
            Assert.That(ai.ScoreReliever(durable, LeverageTier.Medium, 3, ManagerTacticalProfile.Balanced),
                Is.GreaterThan(ai.ScoreReliever(normal, LeverageTier.Medium, 3, ManagerTacticalProfile.Balanced)));
            Assert.That(ai.ScoreReliever(durable, LeverageTier.Critical, 1, ManagerTacticalProfile.Balanced),
                Is.EqualTo(ai.ScoreReliever(normal, LeverageTier.Critical, 1, ManagerTacticalProfile.Balanced)));
        }

        [Test]
        public void TacticalAi_빈베이스에서는강타자라는이유만으로고의사구를주지않는다()
        {
            var ai = new TacticalAiResolver(BalanceTable.CreateDefault().Match.Tactical,
                RunExpectancy24.CreateDefault());
            Assert.That(ai.ShouldIntentionalWalk(CreateDecisionContext(2, new BaseStateSnapshot(false, false, false),
                CreateThreatBatter(91, 100, 100))), Is.False);
        }

        private static DecisionContext CreateDecisionContext(int outs, BaseStateSnapshot bases, Player batter = null)
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
                batter ?? CreateBatter(91, 45, 50, 45),
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
            int bunt = 40,
            string[] traitIds = null)
        {
            return new Player(
                id,
                $"타자 {id}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(50, 50, speed, bunt, defense, mental),
                new PitcherAttributes(20, 20, 20, 20, 20, 20),
                traitIds: traitIds);
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

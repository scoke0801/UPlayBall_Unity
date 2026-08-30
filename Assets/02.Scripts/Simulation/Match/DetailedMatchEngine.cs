using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 피로·감독 판단·타구·수비·주루를 하나의 결정론적 경기 흐름으로 실행한다.
    /// </summary>
    internal sealed partial class DetailedMatchEngine
    {
        private readonly BalanceTable _balance;
        private readonly MatchRandomStreams _random;
        private readonly IPlateAppearanceSimulator _plateAppearanceSimulator;
        private readonly IMatchDecisionSource _recordedDecisionSource;
        private readonly IMatchPitchingDecisionSource _recordedPitchingDecisionSource;
        private readonly IPitchSelectionDecisionSource _pitchSelectionDecisionSource;
        private readonly ISwingExecutionDecisionSource _swingExecutionDecisionSource;
        private readonly MatchDecisionCoordinator _decisionCoordinator;
        private readonly PitcherFatigueResolver _fatigueResolver;
        private readonly PitcherManagementAi _pitcherManagementAi;
        private readonly BattedBallResolver _battedBallResolver;
        private readonly FieldingPlayResolver _fieldingResolver;
        private readonly BaserunningResolver _baserunningResolver;
        private readonly TacticalAiResolver _tacticalAi;
        private readonly WinExpectancyModel _winExpectancy;
        private readonly PitchExecutionResolver _pitchExecutionResolver;
        private readonly SwingContactResolver _swingContactResolver;
        private readonly PitchSelectionAi _pitchSelectionAi;
        private readonly SwingExecutionAi _swingExecutionAi;

        public DetailedMatchEngine(
            BalanceTable balance,
            MatchRandomStreams random,
            IPlateAppearanceSimulator plateAppearanceSimulator,
            IMatchDecisionSource recordedDecisionSource,
            IMatchPitchingDecisionSource recordedPitchingDecisionSource,
            IPitchSelectionDecisionSource pitchSelectionDecisionSource,
            ISwingExecutionDecisionSource swingExecutionDecisionSource,
            MatchDecisionCoordinator decisionCoordinator)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _plateAppearanceSimulator = plateAppearanceSimulator ??
                                        throw new ArgumentNullException(nameof(plateAppearanceSimulator));
            _recordedDecisionSource = recordedDecisionSource;
            _recordedPitchingDecisionSource = recordedPitchingDecisionSource;
            _pitchSelectionDecisionSource = pitchSelectionDecisionSource;
            _swingExecutionDecisionSource = swingExecutionDecisionSource;
            _decisionCoordinator = decisionCoordinator ?? MatchDecisionCoordinator.CreateAutomatic();
            _fatigueResolver = new PitcherFatigueResolver(balance.Match);
            _pitcherManagementAi = new PitcherManagementAi(balance.Match.BullpenManagement);
            _battedBallResolver = new BattedBallResolver(balance, random.BattedBall);
            _fieldingResolver = new FieldingPlayResolver(balance.Match.Fielding, random.Fielding);
            _baserunningResolver = new BaserunningResolver(balance.BaseRunning, random.Baserunning);
            RunExpectancy24 runExpectancy = RunExpectancy24.CreateDefault();
            _tacticalAi = new TacticalAiResolver(balance.Match.Tactical, runExpectancy);
            _winExpectancy = new WinExpectancyModel(runExpectancy);
            _pitchExecutionResolver = new PitchExecutionResolver(balance, random.PitchOutcome);
            _swingContactResolver = new SwingContactResolver(balance);
            _pitchSelectionAi = new PitchSelectionAi(balance, random.Contact);
            _swingExecutionAi = new SwingExecutionAi(balance, random.SwingDecision);
        }

        public MatchResult Simulate(
            MatchInput input,
            IMatchEventSink eventSink,
            MatchEventBuffer capturedEvents)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var state = new DetailedMatchState(input, eventSink, _fatigueResolver);
            int inning = 1;
            while (true)
            {
                SimulateHalfInning(state, inning, InningHalf.Top);
                if (inning >= input.Rules.RegulationInnings &&
                    state.Home.BoxScore.Runs > state.Away.BoxScore.Runs)
                {
                    break;
                }

                SimulateHalfInning(state, inning, InningHalf.Bottom);
                if (inning >= input.Rules.RegulationInnings &&
                    state.Home.BoxScore.Runs != state.Away.BoxScore.Runs)
                {
                    break;
                }

                if (inning == input.Rules.RegulationInnings &&
                    state.Home.BoxScore.Runs == state.Away.BoxScore.Runs)
                {
                    Emit(state, MatchEventType.GameTiedAtRegulationLimit, inning, InningHalf.Bottom);
                }

                if (input.Rules.ExtraInningPolicy == ExtraInningPolicy.DrawAtLimit &&
                    inning >= input.Rules.DrawInningLimit)
                {
                    Emit(state, MatchEventType.MatchEndedAsDraw, inning, InningHalf.Bottom);
                    break;
                }

                inning++;
            }

            Emit(state, MatchEventType.MatchEnded, inning, InningHalf.Bottom);
            int runMargin = Math.Abs(state.Away.BoxScore.Runs - state.Home.BoxScore.Runs);
            state.Away.FinalizeReliefDecisions(
                state.Away.BoxScore.Runs > state.Home.BoxScore.Runs,
                runMargin);
            state.Home.FinalizeReliefDecisions(
                state.Home.BoxScore.Runs > state.Away.BoxScore.Runs,
                runMargin);
            MatchEvent[] events = capturedEvents == null ? Array.Empty<MatchEvent>() : capturedEvents.ToArray();
            PitcherUsageReport[] usage = CombineUsage(
                state.Away.BuildUsageReports(),
                state.Home.BuildUsageReports());
            return new MatchResult(
                input,
                inning,
                state.Away.BoxScore.Build(inning),
                state.Home.BoxScore.Build(inning),
                events,
                usage,
                state.Trace.ToArray());
        }

        private void SimulateHalfInning(DetailedMatchState state, int inning, InningHalf half)
        {
            DetailedTeamGameState offense = half == InningHalf.Top ? state.Away : state.Home;
            DetailedTeamGameState defense = half == InningHalf.Top ? state.Home : state.Away;
            TryApplyDefensiveReplacements(state, inning, half, offense, defense);
            defense.ActivePitcherState.StartInning();
            int outs = 0;
            var bases = new DetailedBaseState();
            var earnedRunTracker = new EarnedRunTracker();
            PlaceAutomaticRunnerIfNeeded(state, inning, half, offense, defense, bases);

            while (outs < BaseballRules.OutsPerHalfInning)
            {
                int battingOrderIndex = offense.NextBattingOrderIndex;
                DetailedLineupReference batter = offense.GetBatter(battingOrderIndex);
                Player onDeck = offense.GetOnDeckBatter(battingOrderIndex);
                LeverageTier leverage = GetLeverage(state, inning, half, offense, defense, outs, bases);
                UpdateLeverageEvent(state, inning, half, leverage);

                if (offense.TryFindPinchHitter(battingOrderIndex, inning, leverage, out int benchIndex))
                {
                    Player leaving = offense.SubstitutePositionPlayer(
                        battingOrderIndex,
                        benchIndex,
                        inning,
                        half,
                        SubstitutionType.PinchHitter,
                        DecisionReasonCode.ExpectedValue);
                    batter = offense.GetBatter(battingOrderIndex);
                    offense.BoxScore.GetBattingLine(batter.Player.PlayerId).AppearedAsPinchHitter = true;
                    Emit(
                        state,
                        MatchEventType.PinchHitterEntered,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        leaving.PlayerId,
                        reasonCode: DecisionReasonCode.ExpectedValue);
                }

                DecisionContext context = CreateContext(
                    state,
                    inning,
                    half,
                    offense,
                    defense,
                    batter.Player,
                    onDeck,
                    outs,
                    bases,
                    leverage);
                TryChangePitcher(state, inning, half, defense, context, bases.CountOccupied());
                context = CreateContext(
                    state,
                    inning,
                    half,
                    offense,
                    defense,
                    batter.Player,
                    onDeck,
                    outs,
                    bases,
                    leverage);

                DefensiveAlignment alignment = _tacticalAi.SelectAlignment(context);
                if (alignment != defense.Alignment)
                {
                    defense.Alignment = alignment;
                    Emit(
                        state,
                        MatchEventType.DefensiveAlignmentChanged,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        0,
                        fromBase: (int)alignment,
                        reasonCode: DecisionReasonCode.DefensiveStrategy);
                }

                if (TryStealBeforePlateAppearance(
                        state,
                        inning,
                        half,
                        offense,
                        defense,
                        context,
                        bases,
                        earnedRunTracker,
                        ref outs))
                {
                    if (outs >= BaseballRules.OutsPerHalfInning)
                        break;
                    context = CreateContext(
                        state,
                        inning,
                        half,
                        offense,
                        defense,
                        batter.Player,
                        onDeck,
                        outs,
                        bases,
                        GetLeverage(state, inning, half, offense, defense, outs, bases));
                }

                DetailedPlateAppearanceOutcome outcome;
                if (_tacticalAi.ShouldIntentionalWalk(context))
                {
                    for (int pitch = 0; pitch < state.Input.Rules.IntentionalWalkPitchCount; pitch++)
                    {
                        defense.ActivePitcherState.RecordPitch();
                        defense.ActivePitchingLine.PitchesThrown++;
                    }
                    Emit(
                        state,
                        MatchEventType.IntentionalWalk,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        batter.Player.PlayerId,
                        plateAppearanceResult: PlateAppearanceResult.IntentionalWalk,
                        reasonCode: DecisionReasonCode.ExpectedValue);
                    outcome = DetailedPlateAppearanceOutcome.IntentionalWalk;
                }
                else
                {
                    BattingApproach strategicApproach = _tacticalAi.ShouldSacrificeBunt(context)
                        ? BattingApproach.Bunt
                        : _decisionCoordinator.GetBattingApproach(context);
                    PitchingApproach pitchingApproach = GetPitchingApproach(
                        state,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        outs,
                        bases,
                        leverage,
                        context);
                    outcome = SimulatePlateAppearance(
                        state,
                        inning,
                        half,
                        offense,
                        defense,
                        batter,
                        context,
                        strategicApproach,
                        pitchingApproach,
                        leverage,
                        outs,
                        bases);
                }

                int activePitcherRunsBefore = defense.ActivePitchingLine.RunsAllowed;
                ApplyPlateAppearanceResult(
                    state,
                    inning,
                    half,
                    offense,
                    defense,
                    batter,
                    outcome,
                    bases,
                    earnedRunTracker,
                    ref outs);
                defense.UpdateReliefDecisionState(
                    defense.BoxScore.Runs,
                    offense.BoxScore.Runs);
                if (bases.ContainsRunner(batter.Player.PlayerId) &&
                    offense.TryFindPinchRunner(
                        battingOrderIndex,
                        inning,
                        offense.BoxScore.Runs - defense.BoxScore.Runs,
                        leverage,
                        out int pinchRunnerBenchIndex))
                {
                    Player leaving = offense.SubstitutePositionPlayer(
                        battingOrderIndex,
                        pinchRunnerBenchIndex,
                        inning,
                        half,
                        SubstitutionType.PinchRunner,
                        DecisionReasonCode.ExpectedValue);
                    DetailedLineupReference entering = offense.GetBatter(battingOrderIndex);
                    bases.ReplaceRunner(
                        leaving.PlayerId,
                        entering.Player,
                        entering.BattingLineIndex);
                    offense.BoxScore.GetBattingLine(entering.Player.PlayerId).AppearedAsPinchRunner = true;
                    Emit(
                        state,
                        MatchEventType.PinchRunnerEntered,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        entering.Player.PlayerId,
                        reasonCode: DecisionReasonCode.ExpectedValue);
                }
                int runsChargedToActive = defense.ActivePitchingLine.RunsAllowed - activePitcherRunsBefore;
                UpdatePitcherAfterPlateAppearance(
                    defense,
                    outcome.Result,
                    runsChargedToActive,
                    bases,
                    leverage);

                offense.NextBattingOrderIndex = (battingOrderIndex + 1) % BaseballRules.BattingOrderSize;
                if (inning >= state.Input.Rules.RegulationInnings &&
                    half == InningHalf.Bottom &&
                    state.Home.BoxScore.Runs > state.Away.BoxScore.Runs)
                {
                    break;
                }
            }

            defense.ActivePitcherState.EndInning(_balance.Match.PitcherStress);
            Emit(
                state,
                MatchEventType.HalfInningEnded,
                inning,
                half,
                pitcherId: defense.ActivePitcher.PlayerId,
                outs: outs);
        }

        private DetailedPlateAppearanceOutcome SimulatePlateAppearance(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            DetailedLineupReference batter,
            DecisionContext context,
            BattingApproach strategicApproach,
            PitchingApproach pitchingApproach,
            LeverageTier leverage,
            int outs,
            DetailedBaseState bases)
        {
            int plateAppearanceIndex = state.NextPlateAppearanceIndex++;
            int timesFaced = defense.ActivePitcherState.BeginPlateAppearance(batter.Player.PlayerId);
            double contactBonus;
            double hardHitBonus;
            ResolveTimesThroughOrderBonus(defense.ActivePitcher, timesFaced, out contactBonus, out hardHitBonus);
            int balls = 0;
            int strikes = 0;
            int pitchNumber = 0;
            var recentPitches = new PitchType[8];
            int recentPitchCount = 0;
            bool requiresRecordedBattingDecision = strategicApproach != BattingApproach.Bunt &&
                                                   _recordedDecisionSource != null &&
                                                   _recordedDecisionSource.RequiresBattingDecision(
                                                       batter.Player.PlayerId);
            BattingApproach lastEmittedBattingApproach = strategicApproach;
            bool hasEmittedBattingApproach = false;
            if (!requiresRecordedBattingDecision)
            {
                Emit(
                    state,
                    MatchEventType.BattingApproachSelected,
                    inning,
                    half,
                    batter.Player.PlayerId,
                    defense.ActivePitcher.PlayerId,
                    batter.Player.PlayerId,
                    toBase: (int)strategicApproach);
                Emit(
                    state,
                    MatchEventType.PitchingApproachSelected,
                    inning,
                    half,
                    batter.Player.PlayerId,
                    defense.ActivePitcher.PlayerId,
                    defense.ActivePitcher.PlayerId,
                    toBase: (int)pitchingApproach);
            }
            if (strategicApproach == BattingApproach.Bunt)
            {
                Emit(state, MatchEventType.BuntAttempted, inning, half,
                    batter.Player.PlayerId, defense.ActivePitcher.PlayerId, batter.Player.PlayerId);
            }

            while (true)
            {
                pitchNumber++;
                BattingApproach selectedApproach = requiresRecordedBattingDecision
                    ? ResolveRecordedBattingApproach(
                        state,
                        inning,
                        half,
                        batter.Player,
                        defense.ActivePitcher.PlayerId,
                        strategicApproach,
                        pitchNumber,
                        balls,
                        strikes,
                        outs,
                        bases)
                    : strategicApproach;
                if (requiresRecordedBattingDecision &&
                    (!hasEmittedBattingApproach || selectedApproach != lastEmittedBattingApproach))
                {
                    Emit(
                        state,
                        MatchEventType.BattingApproachSelected,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        batter.Player.PlayerId,
                        toBase: (int)selectedApproach);
                    lastEmittedBattingApproach = selectedApproach;
                    hasEmittedBattingApproach = true;
                }
                if (requiresRecordedBattingDecision && pitchNumber == 1)
                {
                    Emit(
                        state,
                        MatchEventType.PitchingApproachSelected,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        toBase: (int)pitchingApproach);
                }
                EffectivePitcherRatings effective = _fatigueResolver.Resolve(
                    defense.ActivePitcherState,
                    pitchingApproach);
                var matchup = new PlateAppearanceMatchup(
                    batter.Player,
                    defense.ActivePitcher,
                    defense.CalculateDefenseRating(),
                    bases.Second.IsOccupied || bases.Third.IsOccupied,
                    effective.Velocity,
                    effective.Stuff,
                    effective.Breaking,
                    effective.Control,
                    effective.Mental,
                    contactBonus,
                    hardHitBonus,
                    pitchingApproach);
                BattingApproach pitchApproach = GetPitchBattingApproach(
                    selectedApproach,
                    balls,
                    strikes);
                PitcherFatigueBand bandBefore = _fatigueResolver.GetBand(defense.ActivePitcherState.FatigueRatio);
                PitchPlayData pitchPlayData = default;
                ContactProfile contactProfile = default;
                PitchResult pitchResult;
                if (_plateAppearanceSimulator is PlateAppearanceSimulator)
                {
                    pitchResult = ResolveCommandPitch(
                        state,
                        inning,
                        half,
                        batter.Player,
                        defense,
                        matchup,
                        pitchApproach,
                        pitchingApproach,
                        leverage,
                        plateAppearanceIndex,
                        pitchNumber,
                        balls,
                        strikes,
                        outs,
                        bases,
                        recentPitches,
                        ref recentPitchCount,
                        out pitchPlayData,
                        out contactProfile);
                    pitchApproach = pitchPlayData.Swing.IsBunt
                        ? BattingApproach.Bunt
                        : pitchPlayData.Swing.Intent;
                }
                else
                {
                    pitchResult = _plateAppearanceSimulator.SimulatePitch(
                        matchup,
                        balls,
                        strikes,
                        pitchNumber,
                        pitchApproach);
                }
                defense.ActivePitcherState.RecordPitch();
                defense.ActivePitchingLine.PitchesThrown++;
                if (defense.ActivePitcherState.FatigueRatio >= 1.05d)
                    defense.RecordOverloadPitch();
                PitcherFatigueBand bandAfter = _fatigueResolver.GetBand(defense.ActivePitcherState.FatigueRatio);
                if (bandAfter != bandBefore)
                {
                    Emit(
                        state,
                        MatchEventType.PitcherFatigueBandChanged,
                        inning,
                        half,
                        batter.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        fromBase: (int)bandBefore,
                        toBase: (int)bandAfter,
                        reasonCode: DecisionReasonCode.Fatigue);
                }

                switch (pitchResult)
                {
                    case PitchResult.Ball:
                        balls++;
                        break;
                    case PitchResult.CalledStrike:
                    case PitchResult.SwingingStrike:
                        strikes++;
                        break;
                    case PitchResult.Foul:
                        if (strikes < BaseballRules.StrikesForStrikeout - 1)
                            strikes++;
                        else if (pitchApproach == BattingApproach.Bunt)
                            strikes++;
                        break;
                }

                Emit(
                    state,
                    MatchEventType.Pitch,
                    inning,
                    half,
                    batter.Player.PlayerId,
                    defense.ActivePitcher.PlayerId,
                    batter.Player.PlayerId,
                    pitchResult,
                    balls: balls,
                    strikes: strikes,
                    outs: outs,
                    pitchPlayData: pitchPlayData);

                if (pitchResult == PitchResult.HitByPitch)
                    return new DetailedPlateAppearanceOutcome(PlateAppearanceResult.HitByPitch, default, default);
                if (balls >= BaseballRules.BallsForWalk)
                    return new DetailedPlateAppearanceOutcome(PlateAppearanceResult.Walk, default, default);
                if (strikes >= BaseballRules.StrikesForStrikeout)
                    return new DetailedPlateAppearanceOutcome(PlateAppearanceResult.Strikeout, default, default);
                if (pitchResult != PitchResult.InPlay)
                    continue;

                Emit(state, MatchEventType.Contact, inning, half,
                    batter.Player.PlayerId, defense.ActivePitcher.PlayerId, batter.Player.PlayerId,
                    pitchResult, outs: outs, pitchPlayData: pitchPlayData);
                if (_plateAppearanceSimulator is IPreResolvedBallInPlaySimulator preResolved)
                {
                    PlateAppearanceResult scripted = preResolved.ResolveBallInPlay(matchup, pitchApproach);
                    return new DetailedPlateAppearanceOutcome(scripted, default, default);
                }
                BattedBallDescriptor ball = pitchPlayData.HasValue
                    ? _battedBallResolver.Resolve(matchup, pitchApproach, contactProfile)
                    : _battedBallResolver.Resolve(matchup, pitchApproach);
                if (ball.IsHomeRun)
                    return new DetailedPlateAppearanceOutcome(PlateAppearanceResult.HomeRun, ball, default);

                Player fielder = defense.GetFielderForZone(ball.FieldZone, out PlayerPosition position);
                Emit(state, MatchEventType.FieldingPlayStarted, inning, half,
                    batter.Player.PlayerId, defense.ActivePitcher.PlayerId, fielder.PlayerId, outs: outs);
                int leadRunnerSpeed = bases.First.IsOccupied ? bases.First.Player.BatterAttributes.Speed : 50;
                FieldingPlayOutcome fielding = _fieldingResolver.Resolve(
                    ball,
                    fielder,
                    position,
                    defense.Alignment,
                    batter.Player.BatterAttributes.Speed,
                    leadRunnerSpeed,
                    bases.First.IsOccupied && outs < 2);
                return new DetailedPlateAppearanceOutcome(fielding.Result, ball, fielding);
            }
        }

        private PitchResult ResolveCommandPitch(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            Player batter,
            DetailedTeamGameState defense,
            in PlateAppearanceMatchup matchup,
            BattingApproach battingApproach,
            PitchingApproach pitchingApproach,
            LeverageTier leverage,
            int plateAppearanceIndex,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            DetailedBaseState bases,
            PitchType[] recentPitches,
            ref int recentPitchCount,
            out PitchPlayData pitchPlayData,
            out ContactProfile contactProfile)
        {
            PitchOption[] options = _pitchExecutionResolver.BuildPitchOptions(matchup);
            PitchType[] recentSequence = CopyRecentPitches(recentPitches, recentPitchCount);
            var placeholderPitch = new PitchSelectionCommand(
                state.NextPitchSelectionIndex,
                options[0].PitchType,
                default,
                pitchingApproach);
            var request = new PitchSelectionRequest(
                state.NextPitchSelectionIndex,
                plateAppearanceIndex,
                state.Input.GameId,
                inning,
                half,
                batter.PlayerId,
                defense.ActivePitcher.PlayerId,
                pitchNumber,
                balls,
                strikes,
                outs,
                state.Away.BoxScore.Runs,
                state.Home.BoxScore.Runs,
                bases.Snapshot,
                defense.ActivePitcherState.FatigueRatio,
                leverage,
                options,
                recentSequence,
                placeholderPitch);
            PitchSelectionCommand suggestedPitch = _pitchSelectionAi.Select(request, pitchingApproach);
            request = new PitchSelectionRequest(
                request.RequestId,
                request.PlateAppearanceIndex,
                request.MatchId,
                request.Inning,
                request.Half,
                request.BatterId,
                request.PitcherId,
                request.PitchNumber,
                request.Balls,
                request.Strikes,
                request.Outs,
                request.AwayScore,
                request.HomeScore,
                request.Bases,
                request.CurrentFatigue,
                request.Leverage,
                options,
                recentSequence,
                suggestedPitch);

            PitchSelectionCommand pitchSelection = suggestedPitch;
            if (_pitchSelectionDecisionSource != null &&
                _pitchSelectionDecisionSource.RequiresPitchSelection(request))
            {
                if (!_pitchSelectionDecisionSource.TryGetPitchSelection(request, out pitchSelection))
                    throw new PitchSelectionRequiredSignal(request);
                if (!ContainsPitch(options, pitchSelection.PitchType))
                    throw new InvalidOperationException("보유하지 않은 구종은 선택할 수 없습니다.");
                state.NextPitchSelectionIndex++;
            }

            PitchFlightDescriptor pitch = _pitchExecutionResolver.Resolve(matchup, pitchSelection);
            int consecutivePitchTypeUses = CountConsecutivePitchTypeUses(
                recentSequence,
                pitchSelection.PitchType);
            AddRecentPitch(recentPitches, ref recentPitchCount, pitchSelection.PitchType);
            double idealSwingTime = _swingContactResolver.GetIdealSwingTime01(pitch);
            var placeholderSwing = new SwingCommand(
                state.NextSwingExecutionIndex,
                false,
                default,
                idealSwingTime,
                battingApproach,
                battingApproach == BattingApproach.Bunt);
            var swingRequest = new BatterMiniGameRequest(
                state.NextSwingExecutionIndex,
                plateAppearanceIndex,
                state.Input.GameId,
                inning,
                half,
                batter.PlayerId,
                defense.ActivePitcher.PlayerId,
                pitchNumber,
                balls,
                strikes,
                outs,
                state.Away.BoxScore.Runs,
                state.Home.BoxScore.Runs,
                bases.Snapshot,
                pitch,
                consecutivePitchTypeUses,
                idealSwingTime,
                battingApproach,
                MiniGameAssistRule.Standard,
                placeholderSwing);
            SwingCommand suggestedSwing = _swingExecutionAi.Select(swingRequest, matchup);
            swingRequest = new BatterMiniGameRequest(
                swingRequest.RequestId,
                swingRequest.PlateAppearanceIndex,
                swingRequest.MatchId,
                swingRequest.Inning,
                swingRequest.Half,
                swingRequest.BatterId,
                swingRequest.PitcherId,
                swingRequest.PitchNumber,
                swingRequest.Balls,
                swingRequest.Strikes,
                swingRequest.Outs,
                swingRequest.AwayScore,
                swingRequest.HomeScore,
                swingRequest.Bases,
                pitch,
                consecutivePitchTypeUses,
                idealSwingTime,
                battingApproach,
                MiniGameAssistRule.Standard,
                suggestedSwing);

            SwingCommand swing = suggestedSwing;
            if (_swingExecutionDecisionSource != null &&
                _swingExecutionDecisionSource.RequiresSwingExecution(swingRequest))
            {
                if (!_swingExecutionDecisionSource.TryGetSwingExecution(swingRequest, out swing))
                    throw new SwingExecutionRequiredSignal(swingRequest);
                state.NextSwingExecutionIndex++;
            }

            contactProfile = _swingContactResolver.Resolve(matchup, pitch, swing, pitchNumber);
            pitchPlayData = new PitchPlayData(pitchSelection, pitch, swing, contactProfile);
            return contactProfile.PitchResult;
        }

        private static int CountConsecutivePitchTypeUses(
            PitchType[] recentSequence,
            PitchType current)
        {
            int count = 1;
            for (int index = recentSequence.Length - 1; index >= 0; index--)
            {
                if (recentSequence[index] != current)
                    break;
                count++;
            }
            return count;
        }

        private static bool ContainsPitch(PitchOption[] options, PitchType pitchType)
        {
            for (int index = 0; index < options.Length; index++)
            {
                if (options[index].PitchType == pitchType)
                    return true;
            }
            return false;
        }

        private static PitchType[] CopyRecentPitches(PitchType[] pitches, int count)
        {
            var result = new PitchType[count];
            int first = count < pitches.Length ? 0 : count - pitches.Length;
            for (int index = 0; index < result.Length; index++)
                result[index] = pitches[first + index];
            return result;
        }

        private static void AddRecentPitch(PitchType[] pitches, ref int count, PitchType pitchType)
        {
            if (count < pitches.Length)
            {
                pitches[count++] = pitchType;
                return;
            }
            for (int index = 1; index < pitches.Length; index++)
                pitches[index - 1] = pitches[index];
            pitches[pitches.Length - 1] = pitchType;
        }

        private BattingApproach ResolveRecordedBattingApproach(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            Player batter,
            int pitcherId,
            BattingApproach strategicApproach,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            DetailedBaseState bases)
        {
            // 번트는 감독이 내린 팀 작전이므로 선수의 일반 타격 접근법으로 덮어쓰지 않는다.
            if (strategicApproach == BattingApproach.Bunt)
                return strategicApproach;
            if (_recordedDecisionSource != null &&
                _recordedDecisionSource.RequiresBattingDecision(batter.PlayerId))
            {
                var request = new MatchDecisionRequest(
                    state.NextDecisionIndex,
                    inning,
                    half,
                    batter.PlayerId,
                    pitcherId,
                    pitchNumber,
                    balls,
                    strikes,
                    outs,
                    state.Away.BoxScore.Runs,
                    state.Home.BoxScore.Runs,
                    bases.First.IsOccupied,
                    bases.Second.IsOccupied,
                    bases.Third.IsOccupied);
                if (!_recordedDecisionSource.TryGetBattingApproach(request, out BattingApproach recorded))
                    throw new MatchDecisionRequiredSignal(request);
                state.NextDecisionIndex++;
                return recorded;
            }

            return strategicApproach;
        }

        private static BattingApproach GetPitchBattingApproach(
            BattingApproach strategicApproach,
            int balls,
            int strikes)
        {
            if (strikes >= 2 && strategicApproach != BattingApproach.Bunt)
                return BattingApproach.Contact;
            if (balls == 3 && strategicApproach == BattingApproach.Balanced)
                return BattingApproach.Patient;
            return strategicApproach;
        }

        private PitchingApproach GetPitchingApproach(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int outs,
            DetailedBaseState bases,
            LeverageTier leverage,
            DecisionContext context)
        {
            if (_recordedPitchingDecisionSource != null &&
                _recordedPitchingDecisionSource.RequiresPitchingDecision(pitcherId))
            {
                var request = new MatchPitchingDecisionRequest(
                    state.NextPitchingDecisionIndex,
                    inning,
                    half,
                    batterId,
                    pitcherId,
                    outs,
                    state.Away.BoxScore.Runs,
                    state.Home.BoxScore.Runs,
                    bases.First.IsOccupied,
                    bases.Second.IsOccupied,
                    bases.Third.IsOccupied,
                    leverage);
                if (!_recordedPitchingDecisionSource.TryGetPitchingApproach(
                        request,
                        out PitchingApproach recorded))
                {
                    throw new MatchPitchingDecisionRequiredSignal(request);
                }
                state.NextPitchingDecisionIndex++;
                return recorded;
            }

            return _decisionCoordinator.GetPitchingApproach(context);
        }

        private void ResolveTimesThroughOrderBonus(
            Player pitcher,
            int timesFaced,
            out double contactBonus,
            out double hardHitBonus)
        {
            TimesThroughOrderBalance balance = _balance.Match.TimesThroughOrder;
            if (timesFaced <= 1)
            {
                contactBonus = 0d;
                hardHitBonus = 0d;
                return;
            }
            if (timesFaced == 2)
            {
                contactBonus = balance.SecondContactBonus;
                hardHitBonus = balance.SecondHardHitBonus;
            }
            else if (timesFaced == 3)
            {
                contactBonus = balance.ThirdContactBonus;
                hardHitBonus = balance.ThirdHardHitBonus;
            }
            else
            {
                contactBonus = balance.FourthContactBonus;
                hardHitBonus = balance.FourthHardHitBonus;
            }
            double quality = (pitcher.PitcherAttributes.Stuff + pitcher.PitcherAttributes.Breaking) / 2d;
            double mitigation = Math.Min(
                balance.MaximumPitcherMitigation,
                Math.Max(0d, quality - 50d) / 50d * balance.MaximumPitcherMitigation);
            contactBonus *= 1d - mitigation;
            hardHitBonus *= 1d - mitigation;
        }

        private DecisionContext CreateContext(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            Player batter,
            Player onDeck,
            int outs,
            DetailedBaseState bases,
            LeverageTier leverage)
        {
            return new DecisionContext(
                inning,
                half,
                offense.BoxScore.Runs - defense.BoxScore.Runs,
                outs,
                bases.Snapshot,
                batter,
                defense.ActivePitcher,
                onDeck,
                leverage,
                defense.ActivePitcherState,
                state.Input.Rules,
                defense.Roster.ManagerProfile);
        }

        private LeverageTier GetLeverage(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            DetailedTeamGameState offense,
            DetailedTeamGameState defense,
            int outs,
            DetailedBaseState bases)
        {
            return _winExpectancy.GetLeverage(
                inning,
                half,
                offense.BoxScore.Runs - defense.BoxScore.Runs,
                outs,
                bases.Snapshot.OccupancyMask);
        }

        private void UpdateLeverageEvent(
            DetailedMatchState state,
            int inning,
            InningHalf half,
            LeverageTier leverage)
        {
            bool high = leverage >= LeverageTier.High;
            if (high && !state.IsHighLeverageActive)
                Emit(state, MatchEventType.HighLeverageSituationStarted, inning, half);
            state.IsHighLeverageActive = high;
        }

        private static PitcherUsageReport[] CombineUsage(
            PitcherUsageReport[] away,
            PitcherUsageReport[] home)
        {
            var result = new PitcherUsageReport[away.Length + home.Length];
            Array.Copy(away, 0, result, 0, away.Length);
            Array.Copy(home, 0, result, away.Length, home.Length);
            return result;
        }

        private readonly struct DetailedPlateAppearanceOutcome
        {
            public DetailedPlateAppearanceOutcome(
                PlateAppearanceResult result,
                BattedBallDescriptor battedBall,
                FieldingPlayOutcome fielding)
            {
                Result = result;
                BattedBall = battedBall;
                Fielding = fielding;
            }

            public PlateAppearanceResult Result { get; }
            public BattedBallDescriptor BattedBall { get; }
            public FieldingPlayOutcome Fielding { get; }
            public static DetailedPlateAppearanceOutcome IntentionalWalk => new DetailedPlateAppearanceOutcome(
                PlateAppearanceResult.IntentionalWalk,
                default,
                default);
        }
    }

    internal sealed class MatchDecisionRequiredSignal : Exception
    {
        public MatchDecisionRequiredSignal(MatchDecisionRequest request)
        {
            Request = request;
        }

        public MatchDecisionRequest Request { get; }
    }

    internal sealed class MatchPitchingDecisionRequiredSignal : Exception
    {
        public MatchPitchingDecisionRequiredSignal(MatchPitchingDecisionRequest request)
        {
            Request = request;
        }

        public MatchPitchingDecisionRequest Request { get; }
    }

    internal sealed class PitchSelectionRequiredSignal : Exception
    {
        public PitchSelectionRequiredSignal(PitchSelectionRequest request)
        {
            Request = request;
        }

        public PitchSelectionRequest Request { get; }
    }

    internal sealed class SwingExecutionRequiredSignal : Exception
    {
        public SwingExecutionRequiredSignal(BatterMiniGameRequest request)
        {
            Request = request;
        }

        public BatterMiniGameRequest Request { get; }
    }
}

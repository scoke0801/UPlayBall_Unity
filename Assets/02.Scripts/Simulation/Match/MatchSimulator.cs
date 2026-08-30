using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 두 팀의 타순과 선발 투수로 9이닝 이상 경기를 결정론적으로 진행한다.
    /// </summary>
    public sealed partial class MatchSimulator
    {
        private readonly BalanceTable _balance;
        private readonly IRandomSource _random;
        private readonly MatchRandomStreams _randomStreams;
        private readonly IPlateAppearanceSimulator _plateAppearanceSimulator;
        private readonly IMatchDecisionSource _decisionSource;
        private readonly IMatchPitchingDecisionSource _pitchingDecisionSource;
        private readonly IPitchSelectionDecisionSource _pitchSelectionDecisionSource;
        private readonly ISwingExecutionDecisionSource _swingExecutionDecisionSource;
        private readonly MatchDecisionCoordinator _decisionCoordinator;

        /// <summary>
        /// 경기 시뮬레이터를 밸런스 데이터와 결정론적 RNG로 구성한다.
        /// </summary>
        public MatchSimulator(BalanceTable balance, IRandomSource random)
            : this(
                balance,
                random,
                MatchRandomStreams.Shared(random),
                new PlateAppearanceSimulator(balance, random),
                null,
                null,
                null,
                null,
                null)
        {
        }

        /// <summary>
        /// 플레이어 타격 결정을 입력받아 중단 가능한 경기 시뮬레이터를 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            IRandomSource random,
            IMatchDecisionSource decisionSource)
            : this(
                balance,
                random,
                MatchRandomStreams.Shared(random),
                new PlateAppearanceSimulator(balance, random),
                decisionSource,
                null,
                null,
                null,
                null)
        {
        }

        /// <summary>
        /// 규칙 테스트나 대체 타석 모델을 위해 타석 시뮬레이터까지 주입해 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            IRandomSource random,
            IPlateAppearanceSimulator plateAppearanceSimulator)
            : this(
                balance,
                random,
                MatchRandomStreams.Shared(random),
                plateAppearanceSimulator,
                null,
                null,
                null,
                null,
                null)
        {
        }

        /// <summary>
        /// V2 도메인 RNG와 자동/사용자 판단 공급자를 주입해 경기 시뮬레이터를 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            MatchRandomStreams randomStreams,
            MatchDecisionCoordinator decisionCoordinator = null)
            : this(
                balance,
                randomStreams?.PitchOutcome,
                randomStreams,
                new PlateAppearanceSimulator(balance, randomStreams),
                null,
                null,
                null,
                null,
                decisionCoordinator)
        {
        }

        /// <summary>
        /// 도메인 RNG와 저장된 타자 결정을 함께 재생하는 V2 시뮬레이터를 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            MatchRandomStreams randomStreams,
            IMatchDecisionSource decisionSource)
            : this(
                balance,
                randomStreams?.PitchOutcome,
                randomStreams,
                new PlateAppearanceSimulator(balance, randomStreams),
                decisionSource,
                null,
                null,
                null,
                null)
        {
        }

        /// <summary>
        /// 타자·투수의 저장된 선택을 함께 재생하는 V2 시뮬레이터를 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            MatchRandomStreams randomStreams,
            IMatchDecisionSource decisionSource,
            IMatchPitchingDecisionSource pitchingDecisionSource,
            MatchDecisionCoordinator decisionCoordinator = null,
            IPitchSelectionDecisionSource pitchSelectionDecisionSource = null,
            ISwingExecutionDecisionSource swingExecutionDecisionSource = null)
            : this(
                balance,
                randomStreams?.PitchOutcome,
                randomStreams,
                new PlateAppearanceSimulator(balance, randomStreams),
                decisionSource,
                pitchingDecisionSource,
                pitchSelectionDecisionSource,
                swingExecutionDecisionSource,
                decisionCoordinator)
        {
        }

        /// <summary>
        /// 같은 결정론적 RNG 위에서 선수 입력 중단과 자동 방침을 함께 사용한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            MatchRandomStreams randomStreams,
            IMatchDecisionSource decisionSource,
            MatchDecisionCoordinator decisionCoordinator)
            : this(
                balance,
                randomStreams?.PitchOutcome,
                randomStreams,
                new PlateAppearanceSimulator(balance, randomStreams),
                decisionSource,
                null,
                null,
                null,
                decisionCoordinator)
        {
        }

        private MatchSimulator(
            BalanceTable balance,
            IRandomSource random,
            MatchRandomStreams randomStreams,
            IPlateAppearanceSimulator plateAppearanceSimulator,
            IMatchDecisionSource decisionSource,
            IMatchPitchingDecisionSource pitchingDecisionSource,
            IPitchSelectionDecisionSource pitchSelectionDecisionSource,
            ISwingExecutionDecisionSource swingExecutionDecisionSource,
            MatchDecisionCoordinator decisionCoordinator)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _randomStreams = randomStreams ?? throw new ArgumentNullException(nameof(randomStreams));
            _plateAppearanceSimulator = plateAppearanceSimulator ??
                                        throw new ArgumentNullException(nameof(plateAppearanceSimulator));
            _decisionSource = decisionSource;
            _pitchingDecisionSource = pitchingDecisionSource;
            _pitchSelectionDecisionSource = pitchSelectionDecisionSource;
            _swingExecutionDecisionSource = swingExecutionDecisionSource;
            _decisionCoordinator = decisionCoordinator;
        }

        /// <summary>
        /// 저장된 결정을 재생해 다음 입력 대기 지점 또는 경기 종료까지 진행한다.
        /// </summary>
        public MatchSimulationProgress SimulateUntilDecision(MatchInput input)
        {
            var eventBuffer = new MatchEventBuffer();
            try
            {
                MatchResult result = SimulateInternal(input, eventBuffer, eventBuffer);
                return new MatchSimulationProgress(
                    result,
                    null,
                    null,
                    null,
                    null,
                    result.Events as MatchEvent[] ?? eventBuffer.ToArray());
            }
            catch (MatchDecisionRequiredSignal signal)
            {
                return new MatchSimulationProgress(null, signal.Request, null, null, null, eventBuffer.ToArray());
            }
            catch (MatchPitchingDecisionRequiredSignal signal)
            {
                return new MatchSimulationProgress(null, null, signal.Request, null, null, eventBuffer.ToArray());
            }
            catch (PitchSelectionRequiredSignal signal)
            {
                return new MatchSimulationProgress(null, null, null, signal.Request, null, eventBuffer.ToArray());
            }
            catch (SwingExecutionRequiredSignal signal)
            {
                return new MatchSimulationProgress(null, null, null, null, signal.Request, eventBuffer.ToArray());
            }
        }

        /// <summary>
        /// 한 경기를 실행하고 BoxScore와 전체 이벤트 스트림을 반환한다.
        /// </summary>
        public MatchResult Simulate(MatchInput input)
        {
            var eventBuffer = new MatchEventBuffer();
            return SimulateInternal(input, eventBuffer, eventBuffer);
        }

        /// <summary>
        /// 한 경기를 실행하고 이벤트는 전달된 소비자가 즉시 처리하게 한다.
        /// </summary>
        public MatchResult Simulate(MatchInput input, IMatchEventSink eventSink)
        {
            if (eventSink == null)
                throw new ArgumentNullException(nameof(eventSink));

            return SimulateInternal(input, eventSink, null);
        }

        private MatchResult SimulateInternal(
            MatchInput input,
            IMatchEventSink eventSink,
            MatchEventBuffer capturedEvents)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            return new DetailedMatchEngine(
                    _balance,
                    _randomStreams,
                    _plateAppearanceSimulator,
                    _decisionSource,
                    _pitchingDecisionSource,
                    _pitchSelectionDecisionSource,
                    _swingExecutionDecisionSource,
                    _decisionCoordinator)
                .Simulate(input, eventSink, capturedEvents);
        }

        private void SimulateHalfInning(MatchSimulationState state, int inning, InningHalf half)
        {
            TeamMatchState offense = half == InningHalf.Top ? state.Away : state.Home;
            TeamMatchState defense = half == InningHalf.Top ? state.Home : state.Away;
            defense.SelectPitcher(inning);
            int battingOrderIndex = offense.NextBattingOrderIndex;
            int outs = 0;
            var bases = new BaseState();

            while (outs < BaseballRules.OutsPerHalfInning)
            {
                if (offense.TryApplyPositionPlayerSubstitution(
                        battingOrderIndex,
                        inning,
                        defense.BoxScore.Runs,
                        out Player replacedPlayer))
                {
                    LineupSlotReference substitute = offense.GetBatter(battingOrderIndex);
                    Emit(
                        state,
                        MatchEventType.PlayerSubstitution,
                        inning,
                        half,
                        substitute.Player.PlayerId,
                        defense.ActivePitcher.PlayerId,
                        replacedPlayer.PlayerId,
                        PitchResult.None,
                        PlateAppearanceResult.None,
                        0,
                        0,
                        0,
                        0,
                        outs);
                }

                LineupSlotReference batter = offense.GetBatter(battingOrderIndex);
                PlateAppearanceResult result = SimulatePlateAppearance(
                    state,
                    inning,
                    half,
                    defense,
                    batter,
                    bases,
                    outs);

                ApplyPlateAppearanceResult(
                    state,
                    inning,
                    half,
                    offense,
                    defense,
                    batter,
                    result,
                    bases,
                    ref outs);

                battingOrderIndex++;
                if (battingOrderIndex >= BaseballRules.BattingOrderSize)
                    battingOrderIndex = 0;

                if (inning >= BaseballRules.RegulationInnings &&
                    half == InningHalf.Bottom &&
                    state.Home.BoxScore.Runs > state.Away.BoxScore.Runs)
                {
                    break;
                }
            }

            offense.NextBattingOrderIndex = battingOrderIndex;
            Emit(
                state,
                MatchEventType.HalfInningEnded,
                inning,
                half,
                0,
                defense.ActivePitcher.PlayerId,
                0,
                PitchResult.None,
                PlateAppearanceResult.None,
                0,
                0,
                0,
                0,
                outs);
        }

        private PlateAppearanceResult SimulatePlateAppearance(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            TeamMatchState defense,
            LineupSlotReference batter,
            BaseState bases,
            int outs)
        {
            var matchup = new PlateAppearanceMatchup(
                batter.Player,
                defense.ActivePitcher,
                defense.DefenseRating,
                bases.Second.IsOccupied || bases.Third.IsOccupied);
            int balls = 0;
            int strikes = 0;
            int pitchNumber = 0;

            while (true)
            {
                pitchNumber++;
                BattingApproach approach = GetBattingApproach(
                    state,
                    inning,
                    half,
                    batter,
                    defense.ActivePitcher.PlayerId,
                    pitchNumber,
                    balls,
                    strikes,
                    outs,
                    bases);
                PitchResult pitchResult = _plateAppearanceSimulator.SimulatePitch(
                    matchup,
                    balls,
                    strikes,
                    pitchNumber,
                    approach);
                defense.ActivePitchingLine.PitchesThrown++;

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
                        break;
                    case PitchResult.HitByPitch:
                    case PitchResult.InPlay:
                        break;
                    default:
                        throw new InvalidOperationException("지원하지 않는 PitchResult입니다.");
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
                    PlateAppearanceResult.None,
                    0,
                    0,
                    balls,
                    strikes,
                    outs);

                if (pitchResult == PitchResult.HitByPitch)
                    return PlateAppearanceResult.HitByPitch;
                if (balls >= BaseballRules.BallsForWalk)
                    return PlateAppearanceResult.Walk;
                if (strikes >= BaseballRules.StrikesForStrikeout)
                    return PlateAppearanceResult.Strikeout;
                if (pitchResult != PitchResult.InPlay)
                    continue;

                Emit(
                    state,
                    MatchEventType.Contact,
                    inning,
                    half,
                    batter.Player.PlayerId,
                    defense.ActivePitcher.PlayerId,
                    batter.Player.PlayerId,
                    pitchResult,
                    PlateAppearanceResult.None,
                    0,
                    0,
                    balls,
                    strikes,
                    outs);
                return _plateAppearanceSimulator.ResolveBallInPlay(matchup, approach);
            }
        }

        private BattingApproach GetBattingApproach(
            MatchSimulationState state,
            int inning,
            InningHalf half,
            LineupSlotReference batter,
            int pitcherId,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            BaseState bases)
        {
            if (_decisionSource == null || !_decisionSource.RequiresBattingDecision(batter.Player.PlayerId))
                return BattingApproach.Balanced;

            var request = new MatchDecisionRequest(
                state.NextDecisionIndex,
                inning,
                half,
                batter.Player.PlayerId,
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
            if (!_decisionSource.TryGetBattingApproach(request, out BattingApproach approach))
                throw new MatchDecisionRequiredSignal(request);

            state.NextDecisionIndex++;
            return approach;
        }

    }
}

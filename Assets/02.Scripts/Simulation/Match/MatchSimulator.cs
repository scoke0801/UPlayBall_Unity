using System;
using Baseball.Core.Balance;
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
        private readonly IPlateAppearanceSimulator _plateAppearanceSimulator;

        /// <summary>
        /// 경기 시뮬레이터를 밸런스 데이터와 결정론적 RNG로 구성한다.
        /// </summary>
        public MatchSimulator(BalanceTable balance, IRandomSource random)
            : this(balance, random, new PlateAppearanceSimulator(balance, random))
        {
        }

        /// <summary>
        /// 규칙 테스트나 대체 타석 모델을 위해 타석 시뮬레이터까지 주입해 구성한다.
        /// </summary>
        public MatchSimulator(
            BalanceTable balance,
            IRandomSource random,
            IPlateAppearanceSimulator plateAppearanceSimulator)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _plateAppearanceSimulator = plateAppearanceSimulator ??
                                        throw new ArgumentNullException(nameof(plateAppearanceSimulator));
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

            var state = new MatchSimulationState(input, eventSink);
            int inningsPlayed = BaseballRules.RegulationInnings;

            for (int inning = 1; inning <= BaseballRules.MaximumInnings; inning++)
            {
                SimulateHalfInning(state, inning, InningHalf.Top);
                inningsPlayed = inning;

                if (inning >= BaseballRules.RegulationInnings && state.Home.BoxScore.Runs > state.Away.BoxScore.Runs)
                    break;

                SimulateHalfInning(state, inning, InningHalf.Bottom);

                if (inning >= BaseballRules.RegulationInnings && state.Home.BoxScore.Runs != state.Away.BoxScore.Runs)
                    break;
            }

            if (input.RequiresWinner && state.Home.BoxScore.Runs == state.Away.BoxScore.Runs)
            {
                // 최대 연장까지 동점인 극희귀 상황은 저장된 경기 RNG로 한 점을 부여한다.
                // 포스트시즌에 무승부를 남기지 않으면서 무한 이닝을 막기 위한 안전장치다.
                if (_random.NextDouble() < 0.5d)
                    state.Away.BoxScore.AddRun(inningsPlayed);
                else
                    state.Home.BoxScore.AddRun(inningsPlayed);
            }

            Emit(
                state,
                MatchEventType.MatchEnded,
                inningsPlayed,
                InningHalf.Bottom,
                0,
                0,
                0,
                PitchResult.None,
                PlateAppearanceResult.None,
                0,
                0,
                0,
                0,
                0);

            MatchEvent[] events = capturedEvents == null ? Array.Empty<MatchEvent>() : capturedEvents.ToArray();
            return new MatchResult(
                input,
                inningsPlayed,
                state.Away.BoxScore.Build(inningsPlayed),
                state.Home.BoxScore.Build(inningsPlayed),
                events);
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
                LineupSlotReference batter = new LineupSlotReference(
                    offense.Team.Lineup[battingOrderIndex].Player,
                    battingOrderIndex);
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
                PitchResult pitchResult = _plateAppearanceSimulator.SimulatePitch(
                    matchup,
                    balls,
                    strikes,
                    pitchNumber);
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
                return _plateAppearanceSimulator.ResolveBallInPlay(matchup);
            }
        }
    }
}

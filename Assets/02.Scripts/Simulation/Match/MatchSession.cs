using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Match
{
    /// <summary>프레젠테이션이 한 번의 호출로 소비할 수 있는 경기 진행 경계를 정의한다.</summary>
    public enum MatchSessionStepKind
    {
        EventProduced = 0,
        DecisionRequired = 1,
        HalfInningEnded = 2,
        MatchEnded = 3
    }

    /// <summary>경기 이벤트 한 건, 선수 결정 요청 또는 최종 결과를 전달한다.</summary>
    public readonly struct MatchSessionStep
    {
        internal MatchSessionStep(
            MatchSessionStepKind kind,
            MatchEvent matchEvent,
            MatchDecisionRequest? battingDecision,
            MatchPitchingDecisionRequest? pitchingDecision,
            MatchResult result)
        {
            Kind = kind;
            Event = matchEvent;
            BattingDecision = battingDecision;
            PitchingDecision = pitchingDecision;
            Result = result;
        }

        public MatchSessionStepKind Kind { get; }
        public MatchEvent Event { get; }
        public MatchDecisionRequest? BattingDecision { get; }
        public MatchPitchingDecisionRequest? PitchingDecision { get; }
        public MatchResult Result { get; }
    }

    /// <summary>
    /// Unity 호출 없이 이벤트 소비와 선수 결정을 분리하는 결정론적 경기 세션이다.
    /// </summary>
    public sealed class MatchSession
    {
        private readonly MatchInput _input;
        private readonly BalanceTable _balance;
        private readonly int _controlledPlayerId;
        private readonly bool _controlsBatting;
        private readonly bool _controlsPitching;
        private readonly InterventionLevel _interventionLevel;
        private readonly MatchDecisionCoordinator _decisionCoordinator;
        private readonly List<BattingApproach> _battingDecisions = new List<BattingApproach>(8);
        private readonly List<PitchingApproach> _pitchingDecisions = new List<PitchingApproach>(32);
        private MatchSimulationProgress _progress;
        private int _deliveredEventCount;

        public MatchSession(
            MatchInput input,
            BalanceTable balance,
            int controlledPlayerId,
            bool controlsBatting,
            bool controlsPitching,
            InterventionLevel interventionLevel = InterventionLevel.KeyMoments,
            MatchDecisionCoordinator decisionCoordinator = null)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if ((controlsBatting || controlsPitching) && controlledPlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));
            _controlledPlayerId = controlledPlayerId;
            _controlsBatting = controlsBatting;
            _controlsPitching = controlsPitching;
            _interventionLevel = interventionLevel;
            _decisionCoordinator = decisionCoordinator ?? MatchDecisionCoordinator.CreateAutomatic();
        }

        public MatchResult Result => _progress?.Result;
        public bool IsComplete => _progress?.IsComplete == true &&
                                  _deliveredEventCount >= _progress.Events.Count;

        /// <summary>이벤트 한 건 또는 다음 선수 결정 경계까지 경기를 진행한다.</summary>
        public MatchSessionStep Advance()
        {
            if (_progress == null)
                ReplayToBoundary();

            if (_deliveredEventCount < _progress.Events.Count)
            {
                MatchEvent current = _progress.Events[_deliveredEventCount++];
                MatchSessionStepKind kind = current.EventType == MatchEventType.HalfInningEnded
                    ? MatchSessionStepKind.HalfInningEnded
                    : MatchSessionStepKind.EventProduced;
                return new MatchSessionStep(kind, current, null, null, null);
            }

            if (_progress.PendingDecision.HasValue || _progress.PendingPitchingDecision.HasValue)
            {
                return new MatchSessionStep(
                    MatchSessionStepKind.DecisionRequired,
                    default,
                    _progress.PendingDecision,
                    _progress.PendingPitchingDecision,
                    null);
            }

            return new MatchSessionStep(
                MatchSessionStepKind.MatchEnded,
                default,
                null,
                null,
                _progress.Result);
        }

        /// <summary>대기 중인 현재 투구의 타격 접근법을 제출한다.</summary>
        public void SubmitBattingApproach(BattingApproach approach)
        {
            if (_progress?.PendingDecision.HasValue != true)
                throw new InvalidOperationException("타격 결정을 기다리는 상태가 아닙니다.");
            _battingDecisions.Add(approach);
            ReplayToBoundary();
        }

        /// <summary>대기 중인 타석 단위 투수 승부 방침을 제출한다.</summary>
        public void SubmitPitchingApproach(PitchingApproach approach)
        {
            if (_progress?.PendingPitchingDecision.HasValue != true)
                throw new InvalidOperationException("투구 결정을 기다리는 상태가 아닙니다.");
            _pitchingDecisions.Add(approach);
            ReplayToBoundary();
        }

        private void ReplayToBoundary()
        {
            bool acceptsInput = _interventionLevel != InterventionLevel.Auto;
            IMatchDecisionSource battingSource = acceptsInput && _controlsBatting
                ? new RecordedMatchDecisionSource(_controlledPlayerId, _battingDecisions)
                : null;
            IMatchPitchingDecisionSource pitchingSource = acceptsInput && _controlsPitching
                ? new RecordedMatchPitchingDecisionSource(_controlledPlayerId, _pitchingDecisions)
                : null;
            var simulator = new MatchSimulator(
                _balance,
                MatchRandomStreams.Create(_input.RandomSeed),
                battingSource,
                pitchingSource,
                _decisionCoordinator);
            _progress = simulator.SimulateUntilDecision(_input);
        }
    }
}

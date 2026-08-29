using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 한 타석을 시작하기 전에 플레이어에게 필요한 전략적 타격 결정을 설명한다.
    /// </summary>
    public readonly struct MatchDecisionRequest
    {
        public MatchDecisionRequest(
            int decisionIndex,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int pitchNumber,
            int balls,
            int strikes,
            int outs,
            int awayScore,
            int homeScore,
            bool hasRunnerOnFirst,
            bool hasRunnerOnSecond,
            bool hasRunnerOnThird)
        {
            DecisionIndex = decisionIndex;
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            PitchNumber = pitchNumber;
            Balls = balls;
            Strikes = strikes;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
            HasRunnerOnFirst = hasRunnerOnFirst;
            HasRunnerOnSecond = hasRunnerOnSecond;
            HasRunnerOnThird = hasRunnerOnThird;
        }

        public int DecisionIndex { get; }
        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int PitchNumber { get; }
        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }
        public bool HasRunnerOnFirst { get; }
        public bool HasRunnerOnSecond { get; }
        public bool HasRunnerOnThird { get; }
        public bool HasRunnerInScoringPosition => HasRunnerOnSecond || HasRunnerOnThird;
    }

    /// <summary>
    /// 시뮬레이터가 타석 단위 타격 결정을 읽는 순수 C# 입력 계약이다.
    /// </summary>
    public interface IMatchDecisionSource
    {
        bool RequiresBattingDecision(int batterId);
        bool TryGetBattingApproach(
            in MatchDecisionRequest request,
            out BattingApproach approach);
    }

    /// <summary>
    /// 한 타석 시작 전에 플레이어 투수가 선택할 승부 방침을 요청한다.
    /// </summary>
    public readonly struct MatchPitchingDecisionRequest
    {
        public MatchPitchingDecisionRequest(
            int decisionIndex,
            int inning,
            InningHalf half,
            int batterId,
            int pitcherId,
            int outs,
            int awayScore,
            int homeScore,
            bool hasRunnerOnFirst,
            bool hasRunnerOnSecond,
            bool hasRunnerOnThird,
            LeverageTier leverage)
        {
            DecisionIndex = decisionIndex;
            Inning = inning;
            Half = half;
            BatterId = batterId;
            PitcherId = pitcherId;
            Outs = outs;
            AwayScore = awayScore;
            HomeScore = homeScore;
            HasRunnerOnFirst = hasRunnerOnFirst;
            HasRunnerOnSecond = hasRunnerOnSecond;
            HasRunnerOnThird = hasRunnerOnThird;
            Leverage = leverage;
        }

        public int DecisionIndex { get; }
        public int Inning { get; }
        public InningHalf Half { get; }
        public int BatterId { get; }
        public int PitcherId { get; }
        public int Outs { get; }
        public int AwayScore { get; }
        public int HomeScore { get; }
        public bool HasRunnerOnFirst { get; }
        public bool HasRunnerOnSecond { get; }
        public bool HasRunnerOnThird { get; }
        public LeverageTier Leverage { get; }
    }

    public interface IMatchPitchingDecisionSource
    {
        bool RequiresPitchingDecision(int pitcherId);
        bool TryGetPitchingApproach(
            in MatchPitchingDecisionRequest request,
            out PitchingApproach approach);
    }

    /// <summary>
    /// 저장된 선택을 순서대로 재생하고 다음 선택이 없으면 시뮬레이션을 정지시킨다.
    /// </summary>
    public sealed class RecordedMatchDecisionSource : IMatchDecisionSource
    {
        private readonly int _controlledPlayerId;
        private readonly IReadOnlyList<BattingApproach> _decisions;

        public RecordedMatchDecisionSource(
            int controlledPlayerId,
            IReadOnlyList<BattingApproach> decisions)
        {
            if (controlledPlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));
            _controlledPlayerId = controlledPlayerId;
            _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        }

        public bool RequiresBattingDecision(int batterId)
        {
            return batterId == _controlledPlayerId;
        }

        public bool TryGetBattingApproach(
            in MatchDecisionRequest request,
            out BattingApproach approach)
        {
            if (request.DecisionIndex < _decisions.Count)
            {
                approach = _decisions[request.DecisionIndex];
                return true;
            }

            approach = BattingApproach.Balanced;
            return false;
        }
    }

    /// <summary>
    /// 저장된 투수 승부 방침을 타석 단위로 재생한다.
    /// </summary>
    public sealed class RecordedMatchPitchingDecisionSource : IMatchPitchingDecisionSource
    {
        private readonly int _controlledPlayerId;
        private readonly IReadOnlyList<PitchingApproach> _decisions;

        public RecordedMatchPitchingDecisionSource(
            int controlledPlayerId,
            IReadOnlyList<PitchingApproach> decisions)
        {
            if (controlledPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));
            _controlledPlayerId = controlledPlayerId;
            _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        }

        public bool RequiresPitchingDecision(int pitcherId) => pitcherId == _controlledPlayerId;

        public bool TryGetPitchingApproach(
            in MatchPitchingDecisionRequest request,
            out PitchingApproach approach)
        {
            if (request.DecisionIndex < _decisions.Count)
            {
                approach = _decisions[request.DecisionIndex];
                return true;
            }
            approach = PitchingApproach.Balanced;
            return false;
        }
    }

    /// <summary>
    /// 완료 결과 또는 다음 입력 대기 지점까지의 결정론적 경기 진행 결과다.
    /// </summary>
    public sealed class MatchSimulationProgress
    {
        internal MatchSimulationProgress(
            MatchResult result,
            MatchDecisionRequest? pendingDecision,
            MatchPitchingDecisionRequest? pendingPitchingDecision,
            MatchEvent[] events)
        {
            Result = result;
            PendingDecision = pendingDecision;
            PendingPitchingDecision = pendingPitchingDecision;
            Events = events ?? Array.Empty<MatchEvent>();
        }

        public MatchResult Result { get; }
        public MatchDecisionRequest? PendingDecision { get; }
        public MatchPitchingDecisionRequest? PendingPitchingDecision { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public bool IsComplete => Result != null;
    }
}

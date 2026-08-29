using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Match
{
    public enum DecisionReasonCode
    {
        None = 0,
        Fatigue = 1,
        PitchLimit = 2,
        TimesThroughOrder = 3,
        Performance = 4,
        HighLeverage = 5,
        Matchup = 6,
        Injury = 7,
        ScheduledUsage = 8,
        DefensiveStrategy = 9,
        Emergency = 10,
        ExpectedValue = 11,
        PlayerPolicy = 12
    }

    /// <summary>
    /// 개발 도구와 설명 UI가 감독 판단의 주요 점수와 이유를 재구성할 수 있게 한다.
    /// </summary>
    public readonly struct DecisionTraceEntry
    {
        public DecisionTraceEntry(
            int inning,
            InningHalf half,
            int actorId,
            string action,
            DecisionReasonCode reasonCode,
            double score,
            double threshold)
        {
            Inning = inning;
            Half = half;
            ActorId = actorId;
            Action = action ?? string.Empty;
            ReasonCode = reasonCode;
            Score = score;
            Threshold = threshold;
        }

        public int Inning { get; }
        public InningHalf Half { get; }
        public int ActorId { get; }
        public string Action { get; }
        public DecisionReasonCode ReasonCode { get; }
        public double Score { get; }
        public double Threshold { get; }
    }

    /// <summary>
    /// 경기 핫패스에서는 배열에 순차 추가하고 종료 시 읽기 전용 결과로 고정한다.
    /// </summary>
    public sealed class DecisionTrace
    {
        private readonly List<DecisionTraceEntry> _entries = new List<DecisionTraceEntry>(32);

        public IReadOnlyList<DecisionTraceEntry> Entries => _entries;

        public void Add(in DecisionTraceEntry entry)
        {
            _entries.Add(entry);
        }

        internal DecisionTraceEntry[] ToArray()
        {
            return _entries.ToArray();
        }
    }
}

using System;

namespace Baseball.Simulation.Match
{
    public enum SimulationEngineKind
    {
        Detailed = 0
    }

    public enum MatchDecisionMode
    {
        ExternalInputAllowed = 0,
        InternalAiOnly = 1
    }

    public enum MatchEventMode
    {
        Full = 0,
        None = 1
    }

    public enum MatchDecisionTraceMode
    {
        Full = 0,
        None = 1
    }

    public enum MatchStatisticsMode
    {
        FullBoxScore = 0
    }

    /// <summary>
    /// 경기 해상도와 외부 입력·표현 출력을 분리해 한 경기의 실행 계약을 고정한다.
    /// </summary>
    public readonly struct MatchExecutionProfile : IEquatable<MatchExecutionProfile>
    {
        public MatchExecutionProfile(
            SimulationEngineKind engineKind,
            MatchDecisionMode decisionMode,
            MatchEventMode eventMode,
            MatchDecisionTraceMode decisionTraceMode,
            MatchStatisticsMode statisticsMode)
        {
            EngineKind = engineKind;
            DecisionMode = decisionMode;
            EventMode = eventMode;
            DecisionTraceMode = decisionTraceMode;
            StatisticsMode = statisticsMode;
        }

        public SimulationEngineKind EngineKind { get; }
        public MatchDecisionMode DecisionMode { get; }
        public MatchEventMode EventMode { get; }
        public MatchDecisionTraceMode DecisionTraceMode { get; }
        public MatchStatisticsMode StatisticsMode { get; }

        public static MatchExecutionProfile DetailedInteractive => new MatchExecutionProfile(
            SimulationEngineKind.Detailed,
            MatchDecisionMode.ExternalInputAllowed,
            MatchEventMode.Full,
            MatchDecisionTraceMode.Full,
            MatchStatisticsMode.FullBoxScore);

        public static MatchExecutionProfile DetailedBackground => new MatchExecutionProfile(
            SimulationEngineKind.Detailed,
            MatchDecisionMode.InternalAiOnly,
            MatchEventMode.None,
            MatchDecisionTraceMode.None,
            MatchStatisticsMode.FullBoxScore);

        public bool Equals(MatchExecutionProfile other)
        {
            return EngineKind == other.EngineKind &&
                   DecisionMode == other.DecisionMode &&
                   EventMode == other.EventMode &&
                   DecisionTraceMode == other.DecisionTraceMode &&
                   StatisticsMode == other.StatisticsMode;
        }

        public override bool Equals(object obj) => obj is MatchExecutionProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)EngineKind;
                hash = hash * 397 ^ (int)DecisionMode;
                hash = hash * 397 ^ (int)EventMode;
                hash = hash * 397 ^ (int)DecisionTraceMode;
                return hash * 397 ^ (int)StatisticsMode;
            }
        }
    }
}

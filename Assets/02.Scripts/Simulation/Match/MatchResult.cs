using System.Collections.Generic;
using System;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 완료된 경기의 메타데이터, BoxScore, 이벤트 스트림을 보관한다.
    /// </summary>
    public sealed class MatchResult
    {
        internal MatchResult(
            MatchInput input,
            int inningsPlayed,
            TeamBoxScore awayBoxScore,
            TeamBoxScore homeBoxScore,
            MatchEvent[] events)
            : this(
                input,
                inningsPlayed,
                awayBoxScore,
                homeBoxScore,
                events,
                Array.Empty<PitcherUsageReport>(),
                Array.Empty<DecisionTraceEntry>())
        {
        }

        internal MatchResult(
            MatchInput input,
            int inningsPlayed,
            TeamBoxScore awayBoxScore,
            TeamBoxScore homeBoxScore,
            MatchEvent[] events,
            PitcherUsageReport[] pitcherUsage,
            DecisionTraceEntry[] decisionTrace)
        {
            Input = input;
            InningsPlayed = inningsPlayed;
            AwayBoxScore = awayBoxScore;
            HomeBoxScore = homeBoxScore;
            Events = events;
            PitcherUsage = pitcherUsage ?? Array.Empty<PitcherUsageReport>();
            DecisionTrace = decisionTrace ?? Array.Empty<DecisionTraceEntry>();
        }

        public MatchInput Input { get; }
        public int InningsPlayed { get; }
        public TeamBoxScore AwayBoxScore { get; }
        public TeamBoxScore HomeBoxScore { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public IReadOnlyList<PitcherUsageReport> PitcherUsage { get; }
        public IReadOnlyList<DecisionTraceEntry> DecisionTrace { get; }
        public bool IsTie => AwayBoxScore.Runs == HomeBoxScore.Runs;
        public int WinnerTeamId => IsTie
            ? 0
            : AwayBoxScore.Runs > HomeBoxScore.Runs
                ? AwayBoxScore.TeamId
                : HomeBoxScore.TeamId;
    }
}

using System.Collections.Generic;
using System;

namespace Baseball.Simulation.Match
{
    /// <summary>실제로 같은 수비 아웃을 함께 만든 투수-포수 Pair의 사용량이다.</summary>
    public readonly struct BatteryUsageReport
    {
        public BatteryUsageReport(int teamId, int pitcherPlayerId, int catcherPlayerId, int defensiveOuts)
        {
            if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));
            if (pitcherPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(pitcherPlayerId));
            if (catcherPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(catcherPlayerId));
            if (defensiveOuts <= 0) throw new ArgumentOutOfRangeException(nameof(defensiveOuts));
            TeamId = teamId;
            PitcherPlayerId = pitcherPlayerId;
            CatcherPlayerId = catcherPlayerId;
            DefensiveOuts = defensiveOuts;
        }

        public int TeamId { get; }
        public int PitcherPlayerId { get; }
        public int CatcherPlayerId { get; }
        public int DefensiveOuts { get; }
    }

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
                Array.Empty<BatteryUsageReport>(),
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
            BatteryUsageReport[] batteryUsage,
            DecisionTraceEntry[] decisionTrace)
        {
            Input = input;
            InningsPlayed = inningsPlayed;
            AwayBoxScore = awayBoxScore;
            HomeBoxScore = homeBoxScore;
            Events = events;
            PitcherUsage = pitcherUsage ?? Array.Empty<PitcherUsageReport>();
            BatteryUsage = batteryUsage ?? Array.Empty<BatteryUsageReport>();
            DecisionTrace = decisionTrace ?? Array.Empty<DecisionTraceEntry>();
        }

        public MatchInput Input { get; }
        public int InningsPlayed { get; }
        public TeamBoxScore AwayBoxScore { get; }
        public TeamBoxScore HomeBoxScore { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public IReadOnlyList<PitcherUsageReport> PitcherUsage { get; }
        public IReadOnlyList<BatteryUsageReport> BatteryUsage { get; }
        public IReadOnlyList<DecisionTraceEntry> DecisionTrace { get; }
        public bool IsTie => AwayBoxScore.Runs == HomeBoxScore.Runs;
        public int WinnerTeamId => IsTie
            ? 0
            : AwayBoxScore.Runs > HomeBoxScore.Runs
                ? AwayBoxScore.TeamId
                : HomeBoxScore.TeamId;
    }
}

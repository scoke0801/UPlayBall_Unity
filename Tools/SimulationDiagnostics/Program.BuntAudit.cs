using System;
using System.Collections.Generic;
using System.Text.Json;
using Baseball.Core.Balance;
using Baseball.Core.Rules;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>번트만 바꾼 동일 전력 표본에서 번트 타석 결과와 수비 사건을 별도 집계한다.</summary>
        private static int RunBuntAudit(string[] args)
        {
            int games = ParseCount(args, 1, 10000);
            // 간이 경로는 이벤트를 내보내지 않는 계약이므로 사건별 감사는 상세 경기에서 수행한다.
            var engine = SimulationEngineKind.Detailed;
            foreach (int bunt in new[] { 20, 50, 90 })
            {
                var sink = new BuntAuditSink();
                var away = CreateRoster(1, 50, 50, 50, bunt);
                var home = CreateRoster(2, 50, 50, 50, bunt);
                var balance = BalanceTable.CreateDefault();
                var profile = new MatchExecutionProfile(engine, MatchDecisionMode.InternalAiOnly,
                    MatchEventMode.Full, MatchDecisionTraceMode.None, MatchStatisticsMode.FullBoxScore);
                for (int index = 0; index < games; index++)
                {
                    sink.BeginMatch();
                    ulong seed = DeterministicSeed.Derive(0xB017A0D17UL, (ulong)index);
                    var input = new MatchInput(1, index + 1, seed, away, home, MatchRules.CreateDefault(false));
                    new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input, sink, profile);
                }
                Console.WriteLine(JsonSerializer.Serialize(new { engine = engine.ToString(), bunt, games,
                    sink.Attempts, sink.CompletedBuntPlateAppearances, sink.Results,
                    sink.ThrowingErrors, sink.FieldingErrors, sink.Steals, sink.CaughtStealing }));
            }
            return 0;
        }

        private sealed class BuntAuditSink : IMatchEventSink
        {
            private bool _hasBuntAttempt;
            public int Attempts, CompletedBuntPlateAppearances, ThrowingErrors, FieldingErrors, Steals, CaughtStealing;
            public Dictionary<string, int> Results { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

            public void BeginMatch() => _hasBuntAttempt = false;

            public void Record(in MatchEvent value)
            {
                if (value.EventType == MatchEventType.BuntAttempted) { Attempts++; _hasBuntAttempt = true; }
                if (value.EventType == MatchEventType.ThrowingError) ThrowingErrors++;
                if (value.EventType == MatchEventType.FieldingError) FieldingErrors++;
                if (value.EventType == MatchEventType.StealSucceeded) Steals++;
                if (value.EventType == MatchEventType.CaughtStealing) CaughtStealing++;
                if (value.EventType == MatchEventType.PlateAppearanceEnded)
                {
                    if (_hasBuntAttempt)
                    {
                        CompletedBuntPlateAppearances++;
                        string key = value.PlateAppearanceResult.ToString();
                        Results.TryGetValue(key, out int previous);
                        Results[key] = previous + 1;
                    }
                    _hasBuntAttempt = false;
                }
                if (value.EventType == MatchEventType.HalfInningEnded) _hasBuntAttempt = false;
            }
        }
    }
}

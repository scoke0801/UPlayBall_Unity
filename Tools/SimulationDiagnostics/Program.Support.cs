using System;
using System.IO;
using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>실제 서포트 JSON의 능력치 효과를 동일 시드·홈원정 교대로 상세 경기에서 비교한다.</summary>
        private static int RunSupportComparison(string[] args)
        {
            int games = ParseCount(args, 1, 1000);
            var catalog = JsonSerializer.Deserialize<SupportDiagnosticCatalog>(File.ReadAllText(
                "Assets/10.Datas/Resources/NewGame/OwnerSupportCards.json"), new JsonSerializerOptions { IncludeFields = true });
            var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
            foreach (var card in catalog.cards) card.Validate();
            for (int variant = -1; variant < catalog.cards.Length; variant++)
            {
                var definition = variant < 0 ? null : catalog.cards[variant];
                // 기존 연령·컨디션 카드는 역사 나이와 컨디션 입력이 필요한 별도 실험이다.
                if (definition != null && definition.maximumAge > 0) continue;
                var team = CreateSupportDiagnosticRoster(definition);
                var opponent = CreateRoster(2, 50, 50, 50);
                var statistics = new AggregateStatistics();
                int wins = 0, draws = 0;
                for (int i = 0; i < games; i++)
                {
                    bool home = i % 2 == 1;
                    ulong seed = DeterministicSeed.Derive(0x5A770FUL, (ulong)i);
                    var input = new MatchInput(1, i + 1, seed, home ? opponent : team, home ? team : opponent, MatchRules.CreateDefault(false));
                    var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                        .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                    statistics.Add(result);
                    int own = home ? result.HomeBoxScore.Runs : result.AwayBoxScore.Runs;
                    int other = home ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
                    if (own > other) wins++;
                    if (own == other) draws++;
                }
                Console.WriteLine($"SUPPORT={definition?.id ?? "baseline"} Wins={wins} Draws={draws}");
                Console.WriteLine(statistics.Format(games));
            }
            return 0;
        }

        private static MatchRosterSnapshot CreateSupportDiagnosticRoster(OwnerSupportDefinition definition)
        {
            var source = CreateRoster(1, 50, 50, 50);
            var slots = new LineupSlot[source.StartingLineup.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LineupSlot(ApplySupportDiagnosticBonus(source.StartingLineup[i].Player, definition, true, i == 0), source.StartingLineup[i].FieldingPosition);
            var bench = new Player[source.Bench.Count];
            for (int i = 0; i < bench.Length; i++) bench[i] = ApplySupportDiagnosticBonus(source.Bench[i], definition, true, false);
            var bullpen = new PitcherRosterEntry[source.Bullpen.Count];
            for (int i = 0; i < bullpen.Length; i++)
                bullpen[i] = new PitcherRosterEntry(ApplySupportDiagnosticBonus(source.Bullpen[i].Player, definition, false, false), source.Bullpen[i].Role);
            return new MatchRosterSnapshot(source.TeamId, source.TeamName, new Lineup(slots),
                new PitcherRosterEntry(ApplySupportDiagnosticBonus(source.StartingPitcher.Player, definition, false, true), source.StartingPitcher.Role),
                bullpen, bench, source.ManagerProfile, source.RunningApproach);
        }

        private static Player ApplySupportDiagnosticBonus(Player player, OwnerSupportDefinition definition, bool batter, bool personalTarget)
        {
            if (definition == null || definition.scope == OwnerSupportScope.Player && !personalTarget
                || definition.target == OwnerSupportTarget.Batter && !batter || definition.target == OwnerSupportTarget.Pitcher && batter) return player;
            var b = player.BatterAttributes;
            var p = player.PitcherAttributes;
            int B(int index) => batter ? definition.bonuses[index] : 0;
            int P(int index) => batter ? 0 : definition.bonuses[index];
            return new Player(player.PlayerId, player.Name, player.PrimaryPosition, player.BattingHand, player.ThrowingHand,
                new BatterAttributes(b.Contact + B(0), b.Power + B(1), b.Speed + B(2), b.Bunt + B(3), b.Defense + B(4), b.Mental + B(5)),
                new PitcherAttributes(p.Stamina + P(6), p.Velocity + P(7), p.Stuff + P(8), p.Breaking + P(9), p.Control + P(10), p.Mental + P(11)),
                secondaryPositions: player.SecondaryPositions, pitchRepertoire: player.PitchRepertoire);
        }

        private sealed class SupportDiagnosticCatalog { public OwnerSupportDefinition[] cards = Array.Empty<OwnerSupportDefinition>(); }
    }
}

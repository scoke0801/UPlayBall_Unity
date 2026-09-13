using System;
using System.IO;
using System.Collections.Generic;
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
            var catalog = JsonSerializer.Deserialize<SupportDiagnosticCatalog>(File.ReadAllText(args.Length > 2 ? args[2] :
                "Assets/10.Datas/Resources/NewGame/OwnerSupportCards.json"), new JsonSerializerOptions { IncludeFields = true });
            int rating = ParseCount(args, 3, 50);
            int opponentRating = ParseCount(args, 5, rating);
            var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
            foreach (var card in catalog.cards) card.Validate();
            var variants = new List<(string Name, OwnerSupportDefinition Team, OwnerSupportDefinition Personal)>();
            variants.Add(("baseline", null, null));
            if (args.Length > 4 && args[4] == "loadouts")
            {
                foreach (int grade in new[] { 0, 2, 4, 6, 8, 9 })
                foreach (var target in new[] { OwnerSupportTarget.Batter, OwnerSupportTarget.Pitcher })
                    variants.Add(($"loadout_{grade}_{target}", SelectSupportDiagnosticCard(catalog.cards, grade, target, OwnerSupportScope.Team),
                        SelectSupportDiagnosticCard(catalog.cards, grade, target, OwnerSupportScope.Player)));
            }
            else foreach (var card in catalog.cards)
                if (card.maximumAge == 0) variants.Add((card.id, card, null));
            foreach (var variant in variants)
            {
                var definition = variant.Team;
                // 기존 연령·컨디션 카드는 역사 나이와 컨디션 입력이 필요한 별도 실험이다.
                if (definition != null && definition.maximumAge > 0) continue;
                var team = CreateSupportDiagnosticRoster(definition, rating, variant.Personal);
                var opponent = CreateRoster(2, opponentRating, opponentRating, opponentRating);
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
                Console.WriteLine($"SUPPORT={variant.Name} Wins={wins} Draws={draws}");
                Console.WriteLine(statistics.Format(games));
            }
            return 0;
        }

        private static OwnerSupportDefinition SelectSupportDiagnosticCard(OwnerSupportDefinition[] cards, int grade,
            OwnerSupportTarget target, OwnerSupportScope scope)
        {
            OwnerSupportDefinition selected = null;
            int highest = 0;
            foreach (var card in cards)
            {
                if ((int)card.unlockGrade > grade || card.target != target || card.scope != scope || card.maximumAge > 0) continue;
                int total = 0;
                foreach (int bonus in card.bonuses) total += bonus;
                if (total <= highest) continue;
                selected = card; highest = total;
            }
            return selected;
        }

        private static MatchRosterSnapshot CreateSupportDiagnosticRoster(OwnerSupportDefinition definition, int rating = 50,
            OwnerSupportDefinition personal = null)
        {
            var source = CreateRoster(1, rating, rating, rating);
            Player Apply(Player player, bool batter, bool primary, bool personalTarget) => ApplySupportDiagnosticBonus(
                ApplySupportDiagnosticBonus(player, definition, batter, primary), personal, batter, personalTarget);
            var slots = new LineupSlot[source.StartingLineup.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LineupSlot(Apply(source.StartingLineup[i].Player, true, i == 0, i < 3), source.StartingLineup[i].FieldingPosition);
            var bench = new Player[source.Bench.Count];
            for (int i = 0; i < bench.Length; i++) bench[i] = ApplySupportDiagnosticBonus(source.Bench[i], definition, true, false);
            var bullpen = new PitcherRosterEntry[source.Bullpen.Count];
            for (int i = 0; i < bullpen.Length; i++)
                bullpen[i] = new PitcherRosterEntry(Apply(source.Bullpen[i].Player, false, false, i < 2), source.Bullpen[i].Role);
            return new MatchRosterSnapshot(source.TeamId, source.TeamName, new Lineup(slots),
                new PitcherRosterEntry(Apply(source.StartingPitcher.Player, false, true, true), source.StartingPitcher.Role),
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

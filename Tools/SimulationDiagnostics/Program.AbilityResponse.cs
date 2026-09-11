using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>동일 상대·시드에서 한 선수의 한 능력치만 바꿔 경기 결과의 반응을 측정한다.</summary>
        private static int RunAbilityResponse(string[] args)
        {
            int count = ParseCount(args, 1, 10000);
            string[] attributes = { "Contact", "Power", "Speed", "Mental", "Control", "Stuff", "Breaking", "Velocity" };
            BalanceTable balance = Baseball.Tools.CommonMatchBalanceInput.Load(
                args.Length > 3 ? args[3] : Baseball.Tools.CommonMatchBalanceInput.DefaultPath);
            Console.WriteLine($"Balance={balance.ContentHash}");
            foreach (string attribute in attributes)
            {
                if (args.Length > 2 && !string.Equals(args[2], attribute, StringComparison.OrdinalIgnoreCase)) continue;
                bool pitcher = Array.IndexOf(attributes, attribute) >= 4;
                foreach (int rating in new[] { 30, 50, 80 })
                {
                    int Value(string name) => attribute == name ? rating : 50;
                    int playerId = pitcher ? 1900 : 1002;
                    var selected = new Player(playerId, "능력치 검증 선수",
                        pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.FirstBase,
                        Handedness.Right, Handedness.Right,
                        new BatterAttributes(Value("Contact"), Value("Power"), Value("Speed"), 50, 50, Value("Mental")),
                        new PitcherAttributes(58, Value("Velocity"), Value("Stuff"), Value("Breaking"), Value("Control"), 50),
                        pitchRepertoire: new[] { new PitchRepertoireEntry(PitchType.FourSeamFastball, 50, true),
                            new PitchRepertoireEntry(PitchType.Slider, 50, false), new PitchRepertoireEntry(PitchType.Changeup, 50, false) });
                    MatchRosterSnapshot baseline = CreateRoster(1, 50, 50, 50);
                    var slots = new LineupSlot[9];
                    for (int i = 0; i < slots.Length; i++) slots[i] = baseline.StartingLineup[i];
                    if (!pitcher) slots[1] = new LineupSlot(selected, PlayerPosition.FirstBase);
                    var team = new MatchRosterSnapshot(1, "가상 검증팀", new Lineup(slots),
                        pitcher ? new PitcherRosterEntry(selected, PitcherRole.Starter) : baseline.StartingPitcher,
                        baseline.Bullpen, Array.Empty<Player>(), ManagerTacticalProfile.Balanced, RunningApproach.Balanced);
                    MatchRosterSnapshot opponent = CreateRoster(2, 50, 50, 50);
                    var aggregate = new ControlledStatistics();
                    for (int i = 0; i < count; i++)
                    {
                        ulong seed = DeterministicSeed.Derive(0xAB1117UL, (ulong)i);
                        var input = new MatchInput(1, i + 1, seed, team, opponent, MatchRules.CreateDefault(false));
                        MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed)).Simulate(input, NullMatchEventSink.Instance);
                        aggregate.Add(result, playerId, pitcher);
                    }
                    Console.WriteLine($"{attribute}={rating} Games={count} {aggregate.Format(count, pitcher)}");
                }
            }
            return 0;
        }
    }
}

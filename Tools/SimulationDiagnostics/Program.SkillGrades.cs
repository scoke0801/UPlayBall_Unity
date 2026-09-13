using System;
using System.Linq;
using Baseball.Core;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>실제 4칸 블록 네 개를 장착한 구단의 등급별 전력 효과를 동일 시드로 비교한다.</summary>
        private static int RunSkillGradeComparison(string[] args)
        {
            int games = ParseCount(args, 1, 10000);
            int blockCount = Math.Min(4, ParseCount(args, 2, 4));
            var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
            var growth = GrowthBalanceTable.CreateDefault();
            foreach (var issue in new GrowthContentValidator().Validate(growth))
                if (issue.Severity == ContentValidationSeverity.Error)
                    throw new InvalidOperationException(issue.Code + ": " + issue.Message);
            for (int grade = -1; grade < SkillBlockGradeCatalog.Count; grade++)
            {
                var team = CreateSkillGradeRoster(growth, grade, blockCount);
                var opponent = CreateRoster(2, 50, 50, 50);
                var statistics = new AggregateStatistics();
                int wins = 0, draws = 0;
                for (int index = 0; index < games; index++)
                {
                    bool home = index % 2 == 1;
                    ulong seed = DeterministicSeed.Derive(0x5B10C6UL, (ulong)index);
                    var input = new MatchInput(1, index + 1, seed, home ? opponent : team,
                        home ? team : opponent, MatchRules.CreateDefault(false));
                    var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                        .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                    statistics.Add(result);
                    int own = home ? result.HomeBoxScore.Runs : result.AwayBoxScore.Runs;
                    int other = home ? result.AwayBoxScore.Runs : result.HomeBoxScore.Runs;
                    if (own > other) wins++;
                    if (own == other) draws++;
                }
                Console.WriteLine($"Grade={(grade < 0 ? "baseline" : SkillBlockGradeCatalog.GetLabel((SkillBlockRarity)grade))} Wins={wins} Draws={draws}");
                Console.WriteLine(statistics.Format(games));
            }
            return 0;
        }

        private static MatchRosterSnapshot CreateSkillGradeRoster(GrowthBalanceTable growth, int grade, int blockCount)
        {
            var baseline = CreateRoster(1, 50, 50, 50);
            if (grade < 0) return baseline;
            var service = new SkillBoardService(growth.SkillBoard, growth.SkillBlocks);
            SkillBoardState Board(params SkillBlockCategory[] categories)
            {
                var board = new SkillBoardState(growth.SkillBoard.BoardDefinitionId);
                for (int row = 0; row < Math.Min(categories.Length, blockCount); row++)
                {
                    var definition = growth.SkillBlocks.First(block => (int)block.Rarity == grade &&
                        block.Category == categories[row] && block.ShapeCells.All(cell => cell.Y == 0));
                    var instance = board.AddOwnedBlock(definition.BlockId);
                    service.PlaceBlock(board, instance.InstanceId, 0, row, 0);
                }
                return board;
            }
            var batterBoard = Board(SkillBlockCategory.Contact, SkillBlockCategory.Power,
                SkillBlockCategory.Defense, SkillBlockCategory.BatterMental);
            var pitcherBoard = Board(SkillBlockCategory.Velocity, SkillBlockCategory.Control,
                SkillBlockCategory.Stuff, SkillBlockCategory.PitcherPhysical);
            Player Apply(Player source, SkillBoardState board)
            {
                int Bonus(PlayerAbility ability) => service.GetAbilityBonus(board, ability);
                var bat = source.BatterAttributes;
                var pitch = source.PitcherAttributes;
                return new Player(source.PlayerId, source.Name, source.PrimaryPosition,
                    source.BattingHand, source.ThrowingHand,
                    new BatterAttributes(bat.Contact + Bonus(PlayerAbility.Contact), bat.Power + Bonus(PlayerAbility.Power),
                        bat.Speed + Bonus(PlayerAbility.Speed), bat.Bunt + Bonus(PlayerAbility.Bunt),
                        bat.Defense + Bonus(PlayerAbility.Defense), bat.Mental + Bonus(PlayerAbility.BatterMental)),
                    new PitcherAttributes(pitch.Stamina + Bonus(PlayerAbility.Stamina), pitch.Velocity + Bonus(PlayerAbility.Velocity),
                        pitch.Stuff + Bonus(PlayerAbility.Stuff), pitch.Breaking + Bonus(PlayerAbility.Breaking),
                        pitch.Control + Bonus(PlayerAbility.Control), pitch.Mental + Bonus(PlayerAbility.PitcherMental)),
                    source.SecondaryPositions, pitchRepertoire: source.PitchRepertoire,
                    traitIds: service.GetActiveTraitIds(board));
            }
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = new LineupSlot(Apply(baseline.StartingLineup[index].Player, batterBoard), baseline.StartingLineup[index].FieldingPosition);
            var bullpen = baseline.Bullpen.Select(entry => new PitcherRosterEntry(Apply(entry.Player, pitcherBoard), entry.Role)).ToArray();
            var bench = baseline.Bench.Select(player => Apply(player, batterBoard)).ToArray();
            return new MatchRosterSnapshot(1, "블록 등급 검증", new Lineup(slots),
                new PitcherRosterEntry(Apply(baseline.StartingPitcher.Player, pitcherBoard), PitcherRole.Starter),
                bullpen, bench, ManagerTacticalProfile.Balanced, RunningApproach.Balanced);
        }
    }
}

using System;
using Baseball.Core.Growth;
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
        /// <summary>동일 시드와 상대에서 한 선수에게 유학 보상을 적용해 경기 지표를 비교한다.</summary>
        private static int RunStudyComparison(string[] args)
        {
            int count = ParseCount(args, 1, 1000);
            var balance = Baseball.Tools.CommonMatchBalanceInput.Load();
            var growth = OwnerCardGrowthBalanceTable.CreateDefault();
            if (args.Length > 2 && args[2] == "development")
            {
                var development = System.Text.Json.JsonSerializer.Deserialize<OwnerDevelopmentBalance>(
                    System.IO.File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerDevelopment.json"),
                    new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
                growth = development.ApplyStudyTiers(growth);
            }
            var programs = growth.StudyPrograms;
            for (int variant = -2; variant < programs.Count; variant++)
            {
                CardStudyProgramDefinition program = variant < 0 ? null : programs[variant];
                bool pitcher = variant == -1 || program?.PlayerType == PlayerType.Pitcher;
                var ratings = new int[PlayerAbilityCatalog.AbilityCount];
                Array.Fill(ratings, 50);
                if (program != null)
                {
                    foreach (AbilityChange reward in program.Rewards) ratings[(int)reward.Ability] += reward.Amount;
                    // 대성공 상한을 비교해 기본 보상보다 큰 영향까지 보수적으로 검사한다.
                    ratings[(int)program.Rewards[0].Ability] += program.GreatSuccessBonus;
                }
                int Value(PlayerAbility ability) => ratings[(int)ability];
                var player = new Player(pitcher ? 1900 : 1002, "유학 검증 선수",
                    pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.FirstBase,
                    Handedness.Right, Handedness.Right,
                    new BatterAttributes(Value(PlayerAbility.Contact), Value(PlayerAbility.Power), Value(PlayerAbility.Speed),
                        Value(PlayerAbility.Bunt), Value(PlayerAbility.Defense), Value(PlayerAbility.BatterMental)),
                    new PitcherAttributes(Value(PlayerAbility.Stamina), Value(PlayerAbility.Velocity), Value(PlayerAbility.Stuff),
                        Value(PlayerAbility.Breaking), Value(PlayerAbility.Control), Value(PlayerAbility.PitcherMental)),
                    pitchRepertoire: new[] { new PitchRepertoireEntry(PitchType.FourSeamFastball, 50, true),
                        new PitchRepertoireEntry(PitchType.Slider, 50, false), new PitchRepertoireEntry(PitchType.Changeup, 50, false) });
                var baseline = CreateRoster(1, 50, 50, 50);
                var slots = new LineupSlot[9];
                for (int index = 0; index < slots.Length; index++) slots[index] = baseline.StartingLineup[index];
                if (!pitcher) slots[1] = new LineupSlot(player, PlayerPosition.FirstBase);
                var team = new MatchRosterSnapshot(1, "유학 검증팀", new Lineup(slots),
                    pitcher ? new PitcherRosterEntry(player, PitcherRole.Starter) : baseline.StartingPitcher,
                    baseline.Bullpen, Array.Empty<Player>(), ManagerTacticalProfile.Balanced, RunningApproach.Balanced);
                var opponent = CreateRoster(2, 50, 50, 50);
                var totals = new AggregateStatistics();
                int wins = 0;
                for (int index = 0; index < count; index++)
                {
                    ulong seed = DeterministicSeed.Derive(0x57AD1UL, (ulong)index);
                    var input = new MatchInput(1, index + 1, seed, team, opponent, MatchRules.CreateDefault(false));
                    var result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                        .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
                    totals.Add(result);
                    if (result.AwayBoxScore.Runs > result.HomeBoxScore.Runs) wins++;
                }
                Console.WriteLine($"Program={program?.ProgramId ?? (pitcher ? "baseline_pitcher" : "baseline_batter")} Games={count} Wins={wins}");
                Console.WriteLine(totals.Format(count));
            }
            return 0;
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>TeamColor 최대 중첩이 DetailedMatchEngine 지표에 주는 영향을 대칭 경기로 실측한다.</summary>
        private static int RunTeamColorBalance(string[] args)
        {
            int gameCount = ParseCount(args, 1, 10000);
            if (gameCount <= 0 || (gameCount & 1) != 0)
                throw new ArgumentOutOfRangeException(nameof(gameCount), "TeamColor 대칭 경기 수는 양의 짝수여야 합니다.");

            TeamColorBalanceTable teamColorBalance = TeamColorBalanceTable.CreateInitial();
            ValidateTeamColorContracts(teamColorBalance);
            Console.WriteLine("TeamColor ContractChecks=passed");
            TeamColorDefinition[] identity = ToArray(
                InitialTeamColorDefinitionFactory.CreateYearFranchise(2011, "DIAGNOSTIC", teamColorBalance));
            TeamColorDefinition tier20 = FindByRequiredCount(identity, 20);
            TeamColorDefinition tier25 = FindByRequiredCount(identity, 25);
            TeamColorStatBonus hitterBonus = AddBonuses(tier20.HitterBonus, tier25.HitterBonus);
            TeamColorStatBonus pitcherBonus = AddBonuses(tier20.PitcherBonus, tier25.PitcherBonus);

            BalanceTable balance = BalanceTable.CreateDefault();
            MatchRosterSnapshot boosted = CreateRosterWithTeamColor(1, hitterBonus, pitcherBonus);
            MatchRosterSnapshot baseline = CreateRosterWithTeamColor(
                2,
                TeamColorStatBonus.Create(),
                TeamColorStatBonus.Create());
            Console.WriteLine("TeamColor 최대 정체성 중첩: YearFranchise 20 + 25, 홈/원정 동일 Seed 대칭 표집");
            Console.WriteLine($"Hitter ALL=+{hitterBonus.Get(PlayerAbility.Contact)}, Pitcher ALL=+{pitcherBonus.Get(PlayerAbility.Stamina)}");
            RunTeamColorScenario(balance, gameCount, 0x7EA4C010UL, boosted, baseline);

            System.Collections.Generic.IReadOnlyList<TeamColorDefinition> profiles =
                InitialTeamColorDefinitionFactory.CreateReferenceInspiredProfiles(teamColorBalance);
            TeamColorDefinition fourTool = FindById(profiles, "HitterProfile:FourTool");
            TeamColorDefinition contactSpeed = FindById(profiles, "HitterProfile:ContactSpeed");
            TeamColorStatBonus profileBonus = AddBonuses(fourTool.HitterBonus, contactSpeed.HitterBonus);
            MatchRosterSnapshot profileBoosted = CreateHitterProfileRoster(3, profileBonus);
            MatchRosterSnapshot profileBaseline = CreateHitterProfileRoster(4, TeamColorStatBonus.Create());
            Console.WriteLine();
            Console.WriteLine("TeamColor 신규 타자 조합 중첩: 네 갈래 재능 + 맞히고 먼저 닿는다, 선발 7명 적용");
            Console.WriteLine("대상자 Contact/Speed=+9, Power/Defense=+2");
            RunTeamColorScenario(balance, gameCount, 0x7EA4C011UL, profileBoosted, profileBaseline);
            return 0;
        }

        private static void ValidateTeamColorContracts(TeamColorBalanceTable balance)
        {
            var roster = new TeamColorRosterCard[25];
            for (int index = 0; index < roster.Length; index++)
            {
                int[] values = new AbilityRatings(55).ToArray();
                values[(int)PlayerAbility.Contact] = index < 4 ? 45 : 55;
                roster[index] = new TeamColorRosterCard(
                    $"contract-card-{index}",
                    new TeamColorEligibilityKey(
                        index < 12 ? 2010 : 2011,
                        index < 12 ? "BEARS" : "COMETS",
                        index < 12 ? "2010:BEARS" : "2011:COMETS",
                        PlayerCardEdition.Normal),
                    index < 14 ? PlayerRole.Hitter : PlayerRole.Pitcher,
                    cost: 5,
                    ageAtOriginSeason: 30,
                    registrationType: RegistrationType.Domestic,
                    bats: Handedness.Right,
                    throws: Handedness.Right,
                    naturalPitcherRole: index < 14
                        ? (PitcherRole?)null
                        : index < 19 ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                    activeRosterRole: null,
                    baseAttributes: new AbilityRatings(values));
            }

            IReadOnlyList<TeamColorDefinition> definitions =
                InitialTeamColorDefinitionFactory.CreateForRoster(roster, balance);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var descriptions = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < definitions.Count; index++)
            {
                TeamColorDefinition definition = definitions[index];
                if (!ids.Add(definition.TeamColorId) ||
                    !names.Add(definition.DisplayName) ||
                    !descriptions.Add(definition.Description))
                    throw new InvalidOperationException("TeamColor ID, 이름과 설명은 각각 고유해야 합니다.");
            }

            TeamColorDefinition prospect = FindById(definitions, "HitterProfile:BatPath:Prospect");
            var resolver = new TeamColorResolver();
            IReadOnlyList<TeamColorCandidate> candidates = resolver.Resolve(roster, new[] { prospect });
            if (candidates.Count != 1 || candidates[0].EligibleCardIds.Count != 4)
                throw new InvalidOperationException("낮은 BaseStat TeamColor 자격 판정이 기대와 다릅니다.");
            PerCardBonusMap bonuses = resolver.ApplyEquipped(roster, new[] { prospect }, prospect, null);
            if (bonuses.Get("contract-card-0", PlayerAbility.Contact) != 10 ||
                bonuses.Get("contract-card-4", PlayerAbility.Contact) != 0)
                throw new InvalidOperationException("TeamColor 카드별 대상 보너스가 기대와 다릅니다.");
        }

        private static void RunTeamColorScenario(
            BalanceTable balance,
            int gameCount,
            ulong seedBase,
            MatchRosterSnapshot boosted,
            MatchRosterSnapshot baseline)
        {
            var overall = new AggregateStatistics();
            var boostedStatistics = new TeamColorSideStatistics();
            var baselineStatistics = new TeamColorSideStatistics();

            int pairCount = gameCount / 2;
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                ulong seed = DeterministicSeed.Derive(seedBase, (ulong)pairIndex);
                SimulateTeamColorMatch(
                    balance, seed, pairIndex * 2 + 1,
                    boosted, baseline, true,
                    overall, boostedStatistics, baselineStatistics);
                SimulateTeamColorMatch(
                    balance, seed, pairIndex * 2 + 2,
                    baseline, boosted, false,
                    overall, boostedStatistics, baselineStatistics);
            }

            Console.WriteLine(overall.Format(gameCount));
            Console.WriteLine(boostedStatistics.Format("TeamColor ON"));
            Console.WriteLine(baselineStatistics.Format("TeamColor OFF"));
        }

        private static void SimulateTeamColorMatch(
            BalanceTable balance,
            ulong seed,
            int gameId,
            MatchRosterSnapshot away,
            MatchRosterSnapshot home,
            bool boostedIsAway,
            AggregateStatistics overall,
            TeamColorSideStatistics boostedStatistics,
            TeamColorSideStatistics baselineStatistics)
        {
            var input = new MatchInput(1, gameId, seed, away, home, MatchRules.CreateDefault(false));
            MatchResult result = new MatchSimulator(balance, MatchRandomStreams.Create(seed))
                .Simulate(input, NullMatchEventSink.Instance, MatchExecutionProfile.DetailedBackground);
            overall.Add(result);
            if (boostedIsAway)
            {
                boostedStatistics.Add(result.AwayBoxScore, result.HomeBoxScore);
                baselineStatistics.Add(result.HomeBoxScore, result.AwayBoxScore);
            }
            else
            {
                boostedStatistics.Add(result.HomeBoxScore, result.AwayBoxScore);
                baselineStatistics.Add(result.AwayBoxScore, result.HomeBoxScore);
            }
        }

        private static MatchRosterSnapshot CreateRosterWithTeamColor(
            int teamId,
            TeamColorStatBonus hitterBonus,
            TeamColorStatBonus pitcherBonus)
        {
            var slots = new LineupSlot[9];
            var bench = new Player[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                slots[index] = new LineupSlot(
                    CreateTeamColorBatter(teamId * 1000 + index + 1, position, 50, 50, hitterBonus),
                    position);
                bench[index] = CreateTeamColorBatter(
                    teamId * 1000 + 100 + index,
                    position,
                    46,
                    56,
                    hitterBonus);
            }

            var starter = new PitcherRosterEntry(
                CreateTeamColorPitcher(teamId * 1000 + 900, PlayerPosition.StartingPitcher, 50, 58, pitcherBonus),
                PitcherRole.Starter);
            var bullpen = new[]
            {
                new PitcherRosterEntry(
                    CreateTeamColorPitcher(teamId * 1000 + 901, PlayerPosition.StartingPitcher, 47, 62, pitcherBonus),
                    PitcherRole.Swingman),
                new PitcherRosterEntry(
                    CreateTeamColorPitcher(teamId * 1000 + 902, PlayerPosition.ReliefPitcher, 48, 48, pitcherBonus),
                    PitcherRole.MiddleRelief),
                new PitcherRosterEntry(
                    CreateTeamColorPitcher(teamId * 1000 + 903, PlayerPosition.ReliefPitcher, 53, 44, pitcherBonus),
                    PitcherRole.Setup),
                new PitcherRosterEntry(
                    CreateTeamColorPitcher(teamId * 1000 + 904, PlayerPosition.ReliefPitcher, 55, 40, pitcherBonus),
                    PitcherRole.Closer)
            };
            return new MatchRosterSnapshot(
                teamId,
                $"TeamColor 진단 {teamId}팀",
                new Lineup(slots),
                starter,
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static MatchRosterSnapshot CreateHitterProfileRoster(
            int teamId,
            TeamColorStatBonus qualifyingHitterBonus)
        {
            TeamColorStatBonus none = TeamColorStatBonus.Create();
            var slots = new LineupSlot[9];
            var bench = new Player[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                bool isQualifyingHitter = index < 7;
                slots[index] = new LineupSlot(
                    CreateTeamColorBatter(
                        teamId * 1000 + index + 1,
                        position,
                        isQualifyingHitter ? 70 : 50,
                        isQualifyingHitter ? 70 : 50,
                        isQualifyingHitter ? qualifyingHitterBonus : none),
                    position);
                bench[index] = CreateTeamColorBatter(
                    teamId * 1000 + 100 + index,
                    position,
                    46,
                    56,
                    none);
            }

            var starter = new PitcherRosterEntry(
                CreateTeamColorPitcher(teamId * 1000 + 900, PlayerPosition.StartingPitcher, 50, 58, none),
                PitcherRole.Starter);
            var bullpen = new[]
            {
                new PitcherRosterEntry(CreateTeamColorPitcher(teamId * 1000 + 901, PlayerPosition.StartingPitcher, 47, 62, none), PitcherRole.Swingman),
                new PitcherRosterEntry(CreateTeamColorPitcher(teamId * 1000 + 902, PlayerPosition.ReliefPitcher, 48, 48, none), PitcherRole.MiddleRelief),
                new PitcherRosterEntry(CreateTeamColorPitcher(teamId * 1000 + 903, PlayerPosition.ReliefPitcher, 53, 44, none), PitcherRole.Setup),
                new PitcherRosterEntry(CreateTeamColorPitcher(teamId * 1000 + 904, PlayerPosition.ReliefPitcher, 55, 40, none), PitcherRole.Closer)
            };
            return new MatchRosterSnapshot(
                teamId,
                $"TeamColor 타자 진단 {teamId}팀",
                new Lineup(slots),
                starter,
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static Player CreateTeamColorBatter(
            int playerId,
            PlayerPosition position,
            int batting,
            int defense,
            TeamColorStatBonus bonus)
        {
            int Value(PlayerAbility ability, int baseValue) => ClampRating(baseValue + bonus.Get(ability));
            return new Player(
                playerId,
                $"타자 {playerId}",
                position,
                playerId % 2 == 0 ? Handedness.Left : Handedness.Right,
                Handedness.Right,
                new BatterAttributes(
                    Value(PlayerAbility.Contact, batting),
                    Value(PlayerAbility.Power, batting),
                    Value(PlayerAbility.Speed, batting),
                    Value(PlayerAbility.Bunt, batting),
                    Value(PlayerAbility.Defense, defense),
                    Value(PlayerAbility.BatterMental, batting)),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
        }

        private static Player CreateTeamColorPitcher(
            int playerId,
            PlayerPosition position,
            int pitching,
            int stamina,
            TeamColorStatBonus bonus)
        {
            int Value(PlayerAbility ability, int baseValue) => ClampRating(baseValue + bonus.Get(ability));
            return new Player(
                playerId,
                $"투수 {playerId}",
                position,
                Handedness.Right,
                playerId % 2 == 0 ? Handedness.Left : Handedness.Right,
                new BatterAttributes(20, 20, 35, 20, 48, 50),
                new PitcherAttributes(
                    Value(PlayerAbility.Stamina, stamina),
                    Value(PlayerAbility.Velocity, pitching),
                    Value(PlayerAbility.Stuff, pitching),
                    Value(PlayerAbility.Breaking, pitching),
                    Value(PlayerAbility.Control, pitching),
                    Value(PlayerAbility.PitcherMental, pitching)));
        }

        private static TeamColorStatBonus AddBonuses(TeamColorStatBonus first, TeamColorStatBonus second)
        {
            var bonuses = new AbilityBonus[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < bonuses.Length; index++)
            {
                var ability = (PlayerAbility)index;
                bonuses[index] = new AbilityBonus(ability, first.Get(ability) + second.Get(ability));
            }
            return TeamColorStatBonus.Create(bonuses);
        }

        private static TeamColorDefinition FindByRequiredCount(
            TeamColorDefinition[] definitions,
            int requiredCount)
        {
            for (int index = 0; index < definitions.Length; index++)
                if (definitions[index].RequiredCount == requiredCount)
                    return definitions[index];
            throw new InvalidOperationException($"필요 인원 {requiredCount} TeamColor를 찾지 못했습니다.");
        }

        private static TeamColorDefinition FindById(
            System.Collections.Generic.IReadOnlyList<TeamColorDefinition> definitions,
            string teamColorId)
        {
            for (int index = 0; index < definitions.Count; index++)
                if (string.Equals(definitions[index].TeamColorId, teamColorId, StringComparison.Ordinal))
                    return definitions[index];
            throw new InvalidOperationException($"TeamColor {teamColorId}를 찾지 못했습니다.");
        }

        private static TeamColorDefinition[] ToArray(System.Collections.Generic.IReadOnlyList<TeamColorDefinition> source)
        {
            var result = new TeamColorDefinition[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }

        private sealed class TeamColorSideStatistics
        {
            private long _games;
            private long _wins;
            private long _draws;
            private long _runs;
            private long _runsAllowed;
            private long _atBats;
            private long _hits;
            private long _walks;
            private long _strikeouts;
            private long _homeRuns;
            private long _earnedRuns;
            private long _pitchingOuts;

            public void Add(TeamBoxScore own, TeamBoxScore opponent)
            {
                _games++;
                _runs += own.Runs;
                _runsAllowed += opponent.Runs;
                if (own.Runs > opponent.Runs) _wins++;
                else if (own.Runs == opponent.Runs) _draws++;

                for (int index = 0; index < own.BattingLines.Count; index++)
                {
                    PlayerBattingLine line = own.BattingLines[index];
                    _atBats += line.AtBats;
                    _hits += line.Hits;
                    _walks += line.Walks;
                    _strikeouts += line.Strikeouts;
                    _homeRuns += line.HomeRuns;
                }
                for (int index = 0; index < own.PitchingLines.Count; index++)
                {
                    PlayerPitchingLine line = own.PitchingLines[index];
                    _earnedRuns += line.EarnedRuns;
                    _pitchingOuts += line.OutsRecorded;
                }
            }

            public string Format(string label)
            {
                return $"{label}: W%={Ratio(_wins, _games) * 100d:F2}, Draw%={Ratio(_draws, _games) * 100d:F2}, " +
                       $"R/G={Ratio(_runs, _games):F3}, RA/G={Ratio(_runsAllowed, _games):F3}, " +
                       $"AVG={Ratio(_hits, _atBats):F3}, ERA={Ratio(_earnedRuns * 27d, _pitchingOuts):F3}, " +
                       $"HR/G={Ratio(_homeRuns, _games):F3}, BB/K={Ratio(_walks, _strikeouts):F3}";
            }

            private static double Ratio(double numerator, double denominator)
            {
                return denominator <= 0d ? 0d : numerator / denominator;
            }
        }
    }
}

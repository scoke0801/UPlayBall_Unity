using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 다중 리그 월드 생성·진행·마이그레이션의 핵심 불변 조건을 검증한다.
    /// </summary>
    public sealed class MultiLeagueWorldTests
    {
        [Test]
        public void SignSelectedOffer_세리그와24구단을동시에생성한다()
        {
            CareerState career = CreateCareer(280828UL, startSeason: false);

            Assert.That(default(LeagueId).IsAssigned, Is.False);
            Assert.That(career.SaveVersion, Is.EqualTo(8));
            Assert.That(career.World.Leagues.Count, Is.EqualTo(3));
            Assert.That(career.World.Teams.Count, Is.EqualTo(24));
            Assert.That(career.World.Players.Count, Is.EqualTo(529));
            Assert.That(career.World.Contracts.Count, Is.EqualTo(529));
            Assert.That(career.CurrentLeague.LeagueId, Is.EqualTo(LeagueId.RookieMain));
            Assert.That(career.MyPlayer.CurrentLeagueId, Is.EqualTo(LeagueId.RookieMain));
            Assert.That(career.MyPlayer.ActiveContractId, Is.EqualTo(career.CurrentContract.ContractId));
            Assert.That(career.World.MovementLedger.Records.Count, Is.EqualTo(1));
            Assert.DoesNotThrow(career.World.ValidateInvariants);

            AssertLeague(career.World.GetLeague(LeagueId.RookieMain), LeagueLevel.Rookie, 8);
            AssertLeague(career.World.GetLeague(LeagueId.MinorMain), LeagueLevel.Minor, 8);
            AssertLeague(career.World.GetLeague(LeagueId.MajorMain), LeagueLevel.Major, 8);
            Assert.That(career.World.GetLeague(LeagueId.RookieMain).CompetitionOverallBonus, Is.EqualTo(0));
            Assert.That(career.World.GetLeague(LeagueId.MinorMain).CompetitionOverallBonus, Is.EqualTo(10));
            Assert.That(career.World.GetLeague(LeagueId.MajorMain).CompetitionOverallBonus, Is.EqualTo(20));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MajorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain))));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.RookieMain))));
            AssertAiPopulation(career);
        }

        [Test]
        public void AdvanceWorldSeasons_은퇴승격신인유입후에도로스터와계약불변조건을유지한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(8261021UL, startSeason: true);

            for (int season = 0; season < 10; season++)
                AdvanceWorldToNextSeason(career, configuration);

            int activePlayers = 0;
            int retiredPlayers = 0;
            int activeContracts = 0;
            int promotions = 0;
            int rookieEntries = 0;
            for (int index = 0; index < career.World.Players.Count; index++)
            {
                PlayerState player = career.World.Players[index];
                if (player.CareerStatus == PlayerCareerStatus.ActiveRoster) activePlayers++;
                if (player.CareerStatus == PlayerCareerStatus.Retired) retiredPlayers++;
            }
            for (int index = 0; index < career.World.Contracts.Count; index++)
            {
                if (career.World.Contracts[index].IsActive) activeContracts++;
            }
            for (int index = 0; index < career.World.MovementLedger.Records.Count; index++)
            {
                PlayerMovementRecord record = career.World.MovementLedger.Records[index];
                if (record.MovementType == PlayerMovementType.Promotion) promotions++;
                if (record.MovementType == PlayerMovementType.InitialSigning &&
                    record.PlayerId != career.MyPlayerId)
                {
                    rookieEntries++;
                }
            }

            Assert.That(activePlayers, Is.EqualTo(529));
            Assert.That(activeContracts, Is.EqualTo(activePlayers));
            Assert.That(retiredPlayers, Is.GreaterThan(0));
            Assert.That(rookieEntries, Is.EqualTo(retiredPlayers));
            Assert.That(promotions, Is.GreaterThan(0));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MajorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain))));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.RookieMain))));
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void AdvanceNextRound_모든리그를같은라운드까지진행한다()
        {
            CareerState career = CreateCareer(310731UL, startSeason: true);
            var service = new CareerSeasonService(
                career,
                NewGameConfiguration.CreateDefault().Balance,
                CareerNewsConfiguration.CreateDefault());

            service.AdvanceNextRound();

            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                LeagueState league = career.World.Leagues[leagueIndex];
                for (int teamIndex = 0; teamIndex < league.CurrentSeason.TeamRecords.Count; teamIndex++)
                {
                    Assert.That(
                        league.CurrentSeason.TeamRecords[teamIndex].GamesPlayed,
                        Is.EqualTo(1),
                        $"{league.LeagueId} TeamId {league.CurrentSeason.TeamRecords[teamIndex].TeamId}");
                }
            }
            Assert.That(career.World.Calendar.CurrentDate.Year, Is.EqualTo(2028));
        }

        [Test]
        public void CreateNewWorld_같은Seed의월드지문이일치한다()
        {
            CareerState first = CreateCareer(777777UL, startSeason: true);
            CareerState second = CreateCareer(777777UL, startSeason: true);

            Assert.That(BuildFingerprint(second), Is.EqualTo(BuildFingerprint(first)));

            new CareerSeasonService(
                first,
                NewGameConfiguration.CreateDefault().Balance,
                CareerNewsConfiguration.CreateDefault()).AdvanceNextRound();
            new CareerSeasonService(
                second,
                NewGameConfiguration.CreateDefault().Balance,
                CareerNewsConfiguration.CreateDefault()).AdvanceNextRound();
            Assert.That(BuildFingerprint(second), Is.EqualTo(BuildFingerprint(first)));
        }

        [Test]
        public void MigrateV7ToV8_기존선수구단계약을보존하고누락리그를생성한다()
        {
            CareerState generated = CreateCareer(90125UL, startSeason: false);
            PlayerState player = generated.MyPlayer;
            PlayerContractState contract = generated.CurrentContract;
            LeagueState legacyLeague = generated.CurrentLeague;
            int teamId = player.CurrentTeamId;
            long salary = contract.AnnualSalary;
            var legacy = new CareerState(7, player, legacyLeague, contract, generated.AvailableMoney);

            new CareerSaveMigrationService(NewGameConfiguration.CreateDefault())
                .MigrateV7ToV8(legacy, migrationSeed: 1234567UL);

            Assert.That(legacy.SaveVersion, Is.EqualTo(8));
            Assert.That(legacy.World.Leagues.Count, Is.EqualTo(3));
            Assert.That(legacy.World.Teams.Count, Is.EqualTo(24));
            Assert.That(legacy.MyPlayer, Is.SameAs(player));
            Assert.That(legacy.MyPlayer.CurrentTeamId, Is.EqualTo(teamId));
            Assert.That(legacy.CurrentContract, Is.SameAs(contract));
            Assert.That(legacy.CurrentContract.AnnualSalary, Is.EqualTo(salary));
            Assert.That(legacy.World.HistoryStartYear, Is.EqualTo(legacyLeague.LeagueYear));
            Assert.DoesNotThrow(legacy.World.ValidateInvariants);
        }

        [Test]
        public void MigrateV8ToV9_월드상태를유지하고세이브버전을승격한다()
        {
            CareerState generated = CreateCareer(90225UL, startSeason: false);
            var legacy = new CareerState(
                8,
                generated.MyPlayer,
                generated.World,
                generated.CurrentContract,
                generated.AvailableMoney);

            new CareerSaveMigrationService(NewGameConfiguration.CreateDefault())
                .MigrateV8ToV9(legacy);

            Assert.That(legacy.SaveVersion, Is.EqualTo(9));
            Assert.That(legacy.World, Is.SameAs(generated.World));
            Assert.That(legacy.MyPlayer, Is.SameAs(generated.MyPlayer));
            Assert.That(legacy.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Preseason));
        }

        [Test]
        public void MigrateV9ToV10_월드상태를유지하고내러티브상태를활성화한다()
        {
            CareerState generated = CreateCareer(90325UL, startSeason: false);
            var legacy = new CareerState(
                9,
                generated.MyPlayer,
                generated.World,
                generated.CurrentContract,
                generated.AvailableMoney);

            new CareerSaveMigrationService(NewGameConfiguration.CreateDefault())
                .MigrateV9ToV10(legacy);

            Assert.That(legacy.SaveVersion, Is.EqualTo(10));
            Assert.That(legacy.World, Is.SameAs(generated.World));
            Assert.That(legacy.Narrative, Is.Not.Null);
            Assert.That(legacy.Narrative.SaveVersion, Is.EqualTo(10));
            Assert.That(legacy.Narrative.Confidence, Is.EqualTo(50));
        }

        [Test]
        public void MigrateV10ToV11_기본경기운영설정을보존하며세이브버전을승격한다()
        {
            CareerState generated = CreateCareer(90425UL, startSeason: false);
            var legacy = new CareerState(
                10,
                generated.MyPlayer,
                generated.World,
                generated.CurrentContract,
                generated.AvailableMoney);

            new CareerSaveMigrationService(NewGameConfiguration.CreateDefault())
                .MigrateV10ToV11(legacy);

            Assert.That(legacy.SaveVersion, Is.EqualTo(11));
            Assert.That(legacy.CreationProfile, Is.Not.Null);
            Assert.That(legacy.GameSettings.MatchProgressMode, Is.EqualTo(MatchProgressMode.InterveneOnPlayer));
            Assert.That(legacy.GameSettings.GameSpeed, Is.EqualTo(2));
        }

        [Test]
        public void MigrateV7ToV8_만료계약을포함한계약이력을보존한다()
        {
            CareerState generated = CreateCareer(91403UL, startSeason: false);
            var legacy = new CareerState(
                7,
                generated.MyPlayer,
                generated.CurrentLeague,
                generated.CurrentContract,
                generated.AvailableMoney);
            PlayerContractState previousContract = legacy.CurrentContract;
            var renewal = new PlayerContractState(
                7,
                previousContract.TeamId,
                previousContract.SignedYear + 1,
                2,
                300,
                previousContract.AnnualSalary + 100,
                previousContract.ExpectedRole);
            legacy.RenewContract(renewal);

            new CareerSaveMigrationService(NewGameConfiguration.CreateDefault())
                .MigrateV7ToV8(legacy, migrationSeed: 7654321UL);

            Assert.That(legacy.ContractHistory.Count, Is.EqualTo(2));
            Assert.That(legacy.World.Contracts.Count, Is.EqualTo(2));
            Assert.That(previousContract.IsActive, Is.False);
            Assert.That(renewal.IsActive, Is.True);
            Assert.That(legacy.MyPlayer.ActiveContractId, Is.EqualTo(renewal.ContractId));
            Assert.DoesNotThrow(legacy.World.ValidateInvariants);
        }

        [Test]
        public void AdvanceWorldSeason_세리그우승시상역사를보존하고같은연도로전환한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(8260828UL, startSeason: true);
            var autoCompletion = new CareerSeasonAutoCompletionService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());

            autoCompletion.CompleteCurrentPhase();
            for (int index = 0; index < career.World.Leagues.Count; index++)
                Assert.That(career.World.Leagues[index].CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Postseason));

            autoCompletion.CompleteCurrentPhase();
            var champions = new Dictionary<LeagueId, int>();
            for (int index = 0; index < career.World.Leagues.Count; index++)
            {
                LeagueState league = career.World.Leagues[index];
                Assert.That(league.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
                Assert.That(league.CurrentSeason.Postseason.IsCompleted, Is.True);
                Assert.That(league.CurrentSeason.Awards, Is.Not.Null);
                Assert.That(league.CurrentSeason.Awards.Results.Count, Is.GreaterThan(0));
                champions.Add(league.LeagueId, league.CurrentSeason.Postseason.ChampionTeamId);
            }
            int championEventCount = 0;
            for (int index = 0; index < career.World.DomainEvents.Events.Count; index++)
            {
                if (career.World.DomainEvents.Events[index].EventType == "LeagueChampion")
                    championEventCount++;
            }
            Assert.That(championEventCount, Is.EqualTo(3));

            new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());
            for (int index = 0; index < career.World.Leagues.Count; index++)
                Assert.That(career.World.Leagues[index].CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));

            int previousYear = career.CurrentLeague.CurrentSeason.Year;
            new CareerSeasonTransitionService(career, configuration.Balance).AdvanceToNextSeason();

            for (int index = 0; index < career.World.Leagues.Count; index++)
            {
                LeagueState league = career.World.Leagues[index];
                Assert.That(league.LeagueYear, Is.EqualTo(previousYear + 1));
                Assert.That(league.CurrentSeason.Year, Is.EqualTo(previousYear + 1));
                Assert.That(league.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
                Assert.That(league.CompletedSeasonSummaries.Count, Is.EqualTo(1));
                Assert.That(
                    league.CompletedSeasonSummaries[0].ChampionTeamId,
                    Is.EqualTo(champions[league.LeagueId]));
                Assert.That(league.CompletedSeasonSummaries[0].Standings.Count, Is.EqualTo(8));
            }
            Assert.That(career.World.Calendar.CurrentDate.Year, Is.EqualTo(previousYear + 1));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MajorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain))));
            Assert.That(
                GetAverageOverall(career.World.GetLeague(LeagueId.MinorMain)),
                Is.GreaterThan(GetAverageOverall(career.World.GetLeague(LeagueId.RookieMain))));
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void AdvanceWorldSeason_같은Seed의전체시즌결과와다음일정이일치한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState first = CreateCareer(8260901UL, startSeason: true);
            CareerState second = CreateCareer(8260901UL, startSeason: true);

            AdvanceWorldToNextSeason(first, configuration);
            AdvanceWorldToNextSeason(second, configuration);

            Assert.That(BuildFingerprint(second), Is.EqualTo(BuildFingerprint(first)));
        }

        [Test]
        public void AdvanceWorldSeason_AI선수의성장나이커리어합계를영속한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(8260917UL, startSeason: true);
            var autoCompletion = new CareerSeasonAutoCompletionService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());
            autoCompletion.CompleteCurrentPhase();
            autoCompletion.CompleteCurrentPhase();

            LeagueState rookieLeague = career.World.GetLeague(LeagueId.RookieMain);
            TeamState team = rookieLeague.Teams[0];
            RosterCompetitorState catcher = team.GetStrongestCompetitor(PlayerPosition.Catcher);
            PlayerState player = career.World.GetPlayer(catcher.PlayerId);
            int previousAge = player.Age;

            new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());

            Assert.That(player.RegisteredSeasons, Is.EqualTo(1));
            Assert.That(player.CareerPlateAppearances, Is.GreaterThan(0));
            Assert.That(player.GrowthState.GrowthHistory.Count, Is.EqualTo(2));
            Assert.That(player.Age, Is.EqualTo(previousAge));
            Assert.DoesNotThrow(career.World.ValidateInvariants);

            new CareerSeasonTransitionService(career, configuration.Balance)
                .AdvanceToNextSeason();

            TeamState nextTeam = career.World.GetTeam(team.TeamId);
            RosterCompetitorState nextCatcher = FindCompetitor(nextTeam, catcher.PlayerId);
            int expectedOverall = new PlayerValueEvaluator(configuration.Balance.PlayerEvaluation)
                .CalculatePositionValue(player.ToPlayer());
            Assert.That(player.Age, Is.EqualTo(previousAge + 1));
            Assert.That(player.GrowthState.Age, Is.EqualTo(previousAge + 1));
            Assert.That(nextCatcher.Overall, Is.EqualTo(expectedOverall));
            Assert.That(nextCatcher.CareerPlateAppearances, Is.EqualTo(player.CareerPlateAppearances));
            Assert.That(nextCatcher.RegisteredSeasons, Is.EqualTo(player.RegisteredSeasons));
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        private static CareerState CreateCareer(ulong seed, bool startSeason)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("월드 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            if (startSeason)
                flow.StartRookieSeason();
            return flow.Career;
        }

        private static SeasonUsageSummary CreateBatterUsage()
        {
            return new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.3d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.2d)
                });
        }

        private static void AdvanceWorldToNextSeason(
            CareerState career,
            NewGameConfiguration configuration)
        {
            var autoCompletion = new CareerSeasonAutoCompletionService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());
            autoCompletion.CompleteCurrentPhase();
            autoCompletion.CompleteCurrentPhase();
            new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());
            new CareerSeasonTransitionService(career, configuration.Balance)
                .AdvanceToNextSeason();
        }

        private static void AssertLeague(LeagueState league, LeagueLevel level, int teamCount)
        {
            Assert.That(league.LeagueLevel, Is.EqualTo(level));
            Assert.That(league.Teams.Count, Is.EqualTo(teamCount));
        }

        private static void AssertAiPopulation(CareerState career)
        {
            WorldGenerationConfiguration generation = NewGameConfiguration.CreateDefault().WorldGeneration;
            for (int index = 0; index < career.World.Players.Count; index++)
            {
                PlayerState player = career.World.Players[index];
                if (player.PlayerId == career.MyPlayer.PlayerId)
                    continue;
                LeagueLevel level = career.World.GetLeague(player.CurrentLeagueId).LeagueLevel;
                Assert.That(player.Age, Is.InRange(
                    generation.GetMinimumAge(level),
                    generation.GetMaximumAge(level)));
                Assert.That(player.GrowthState, Is.Not.Null);
                Assert.That(player.GrowthState.Age, Is.EqualTo(player.Age));
                Assert.That(player.RegisteredSeasons, Is.EqualTo(0));
                Assert.That(player.CareerStatus, Is.EqualTo(PlayerCareerStatus.ActiveRoster));
                Assert.That(player.ActiveContractId, Is.GreaterThan(0));
            }
        }

        private static RosterCompetitorState FindCompetitor(TeamState team, int playerId)
        {
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                if (team.RosterCompetitors[index].PlayerId == playerId)
                    return team.RosterCompetitors[index];
            }
            throw new System.InvalidOperationException($"PlayerId {playerId}를 로스터에서 찾을 수 없습니다.");
        }

        private static double GetAverageOverall(LeagueState league)
        {
            int total = 0;
            int count = 0;
            for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
            {
                IReadOnlyList<RosterCompetitorState> roster = league.Teams[teamIndex].RosterCompetitors;
                for (int playerIndex = 0; playerIndex < roster.Count; playerIndex++)
                {
                    total += roster[playerIndex].Overall;
                    count++;
                }
            }
            return total / (double)count;
        }

        private static string BuildFingerprint(CareerState career)
        {
            var result = new StringBuilder(32_768);
            result.Append(career.World.WorldSeed).Append('|');
            for (int playerIndex = 0; playerIndex < career.World.Players.Count; playerIndex++)
            {
                PlayerState player = career.World.Players[playerIndex];
                result.Append("player:")
                    .Append(player.PlayerId).Append(',')
                    .Append((int)player.CareerStatus).Append(',')
                    .Append(player.CurrentTeamId).Append(',')
                    .Append(player.ActiveContractId).Append(',')
                    .Append(player.Age).Append(',')
                    .Append(player.CareerPlateAppearances).Append(',')
                    .Append(player.CareerPitchingOuts).Append(',')
                    .Append(player.RegisteredSeasons).Append('[');
                if (player.GrowthState != null)
                {
                    int[] abilities = player.GrowthState.BaseAbilities.ToArray();
                    for (int abilityIndex = 0; abilityIndex < abilities.Length; abilityIndex++)
                        result.Append(abilities[abilityIndex]).Append(',');
                }
                result.Append(']');
            }
            for (int contractIndex = 0; contractIndex < career.World.Contracts.Count; contractIndex++)
            {
                PlayerContractState contract = career.World.Contracts[contractIndex];
                result.Append("contract:")
                    .Append(contract.ContractId).Append(',')
                    .Append(contract.PlayerId).Append(',')
                    .Append(contract.TeamId).Append(',')
                    .Append(contract.SignedYear).Append(',')
                    .Append(contract.IsActive).Append(';');
            }
            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                LeagueState league = career.World.Leagues[leagueIndex];
                result.Append(league.LeagueId).Append(':').Append(league.RandomSeed).Append('|');
                for (int teamIndex = 0; teamIndex < league.Teams.Count; teamIndex++)
                {
                    TeamState team = league.Teams[teamIndex];
                    result.Append(team.TeamId).Append(':').Append(team.Name).Append('[');
                    for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                    {
                        RosterCompetitorState player = team.RosterCompetitors[playerIndex];
                        result.Append(player.PlayerId).Append(',').Append(player.Overall).Append(';');
                    }
                    result.Append(']');
                }
                if (league.CurrentSeason.Schedule != null)
                {
                    for (int gameIndex = 0; gameIndex < league.CurrentSeason.Schedule.Games.Count; gameIndex++)
                    {
                        ScheduledGameState game = league.CurrentSeason.Schedule.Games[gameIndex];
                        result.Append(game.GameId).Append(',')
                            .Append(game.RandomSeed).Append(',')
                            .Append(game.AwayRuns).Append(',')
                            .Append(game.HomeRuns).Append(';');
                    }
                }
                for (int summaryIndex = 0;
                     summaryIndex < league.CompletedSeasonSummaries.Count;
                     summaryIndex++)
                {
                    LeagueSeasonSummaryState summary = league.CompletedSeasonSummaries[summaryIndex];
                    result.Append("summary:")
                        .Append(summary.Year).Append(',')
                        .Append(summary.ChampionTeamId).Append(',')
                        .Append(summary.RunnerUpTeamId).Append(',')
                        .Append(summary.Awards.Results.Count).Append(';');
                    for (int standingIndex = 0; standingIndex < summary.Standings.Count; standingIndex++)
                    {
                        TeamSeasonSummaryState standing = summary.Standings[standingIndex];
                        result.Append(standing.Rank).Append(',')
                            .Append(standing.TeamId).Append(',')
                            .Append(standing.Wins).Append(',')
                            .Append(standing.Losses).Append(';');
                    }
                }
            }
            return result.ToString();
        }
    }
}

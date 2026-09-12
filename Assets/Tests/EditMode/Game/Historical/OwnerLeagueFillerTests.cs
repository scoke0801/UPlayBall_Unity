using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 월드의 모든 조가 CPU 임시 구단으로 10팀 체제를 채우고, 임시 구단은 한 시즌 뒤 사라지는지 검증한다.</summary>
    public sealed class OwnerLeagueFillerTests
    {
        private const int GroupTeamCount = 10;

        [Test]
        public void AdvanceSeason_모든조가10팀이고CPU구단은매시즌새로만든다()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime(includeSpecialCompositeTeams: false,
                out ManagerHistoricalSaveAdapter adapter);
            var balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);
            var previousFillers = new HashSet<string>(StringComparer.Ordinal);

            for (int season = 1; season <= 4; season++)
            {
                CompleteWorld(runtime, balance, FillerFirstResult.Tie);
                ManagerSeasonAdvanceResult advance = coordinator.AdvanceSeason(runtime);
                Assert.That(advance.IsApplied, Is.True, $"시즌 {season}: {advance.Status}");
                runtime = adapter.Restore(adapter.CreateSaveData(runtime));

                var currentFillers = AssertFullGroups(runtime, runtime.ManagerMode.LiveSeason.SeasonNumber);
                foreach (string filler in currentFillers)
                    Assert.That(previousFillers.Contains(filler), Is.False, "CPU 구단은 다음 시즌으로 이어지지 않는다.");
                Assert.That(runtime.ManagerMode.PlayerStatuses.Count, Is.EqualTo(runtime.LeagueWorld.Rosters.Count),
                    "사라진 임시 구단의 컨디션 상태가 남으면 세이브가 시즌마다 커진다.");
                previousFillers = currentFillers;
            }
        }

        [Test]
        public void AdvanceSeason_첫시즌특수합성팀은다음시즌월드에서빠진다()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime(includeSpecialCompositeTeams: true, out _);
            var balance = BalanceTable.CreateDefault();
            var composites = new List<string>();
            foreach (var special in runtime.League.SpecialCompositeTeams) composites.Add(special.TeamSeasonKey);
            Assert.That(composites, Is.Not.Empty);

            CompleteWorld(runtime, balance, FillerFirstResult.Tie);
            Assert.That(new ManagerModeCoordinator(balance).AdvanceSeason(runtime).IsApplied, Is.True);

            foreach (string composite in composites)
                foreach (var roster in runtime.LeagueWorld.Rosters)
                    Assert.That(roster.TeamSeasonKey, Is.Not.EqualTo(composite));
            AssertFullGroups(runtime, runtime.ManagerMode.LiveSeason.SeasonNumber);
        }

        [Test]
        public void ResolveNextGrade_CPU구단이차지한승격순위는실제구단에게넘어가지않는다()
        {
            ManagerHistoricalRuntimeState runtime = CreateRuntime(includeSpecialCompositeTeams: false, out _);
            var balance = BalanceTable.CreateDefault();
            var service = new OwnerLeagueWorldService(balance);
            CompleteWorld(runtime, balance, FillerFirstResult.Tie);
            Assert.That(new ManagerModeCoordinator(balance).AdvanceSeason(runtime).IsApplied, Is.True);

            // CPU 구단이 모든 실제 구단을 이기게 해 순위표 상단을 CPU가 차지하게 한다.
            CompleteWorld(runtime, balance, FillerFirstResult.FillerWins);
            var resolver = new OwnerLeagueAllocationResolver();
            int checkedGroups = 0;
            foreach (OwnerLeagueGroupState group in runtime.LeagueWorld.Groups)
            {
                int fillerCount = group.League.FillerTeamSeasonKeys.Count;
                if (fillerCount == 0) continue;
                checkedGroups++;
                OwnerLeagueStanding[] ranking = service.Rank(group.Season);
                for (int rank = 1; rank <= fillerCount; rank++)
                    Assert.That(LeagueFillerTeamKey.IsFillerKey(ranking[rank - 1].TeamKey), Is.True);
                for (int index = fillerCount; index < ranking.Length; index++)
                {
                    LeagueGrade expected = resolver.ResolveGrade(group.League.Grade, index + 1, ranking.Length,
                        balance.LeaguePromotion);
                    Assert.That(service.ResolveNextGrade(runtime, ranking[index].TeamKey), Is.EqualTo(expected),
                        $"{group.League.LeagueInstanceId} {index + 1}위");
                }
            }
            Assert.That(checkedGroups, Is.GreaterThan(0));
        }

        [Test]
        public void AdvanceSeason_같은월드면같은CPU구단과로스터를만든다()
        {
            var balance = BalanceTable.CreateDefault();
            var first = CreateRuntime(includeSpecialCompositeTeams: false, out _);
            var second = CreateRuntime(includeSpecialCompositeTeams: false, out _);
            foreach (var runtime in new[] { first, second })
            {
                CompleteWorld(runtime, balance, FillerFirstResult.Tie);
                Assert.That(new ManagerModeCoordinator(balance).AdvanceSeason(runtime).IsApplied, Is.True);
            }

            Assert.That(DescribeRosters(second), Is.EqualTo(DescribeRosters(first)));
        }

        private static HashSet<string> AssertFullGroups(ManagerHistoricalRuntimeState runtime, int seasonNumber)
        {
            var fillers = new HashSet<string>(StringComparer.Ordinal);
            var validator = new ActiveRosterValidator();
            foreach (OwnerLeagueGroupState group in runtime.LeagueWorld.Groups)
            {
                Assert.That(group.Season.Teams.Count, Is.EqualTo(GroupTeamCount), group.League.LeagueInstanceId);
                foreach (string filler in group.League.FillerTeamSeasonKeys)
                {
                    Assert.That(filler, Does.StartWith($"FILLER:{seasonNumber}:"));
                    CurrentRosterState roster = FindRoster(runtime, filler);
                    Assert.That(validator.Validate(roster).IsValid, Is.True, filler);
                    fillers.Add(filler);
                }
            }
            return fillers;
        }

        private static CurrentRosterState FindRoster(ManagerHistoricalRuntimeState runtime, string key)
        {
            foreach (var roster in runtime.LeagueWorld.Rosters)
                if (roster.TeamSeasonKey == key) return roster;
            throw new KeyNotFoundException(key);
        }

        private static List<string> DescribeRosters(ManagerHistoricalRuntimeState runtime)
        {
            var result = new List<string>();
            foreach (var roster in runtime.LeagueWorld.Rosters)
            {
                if (!LeagueFillerTeamKey.IsFillerKey(roster.TeamSeasonKey)) continue;
                var cards = new List<string>();
                foreach (var entry in roster.Entries) cards.Add(entry.Role + "=" + entry.CardId);
                result.Add(roster.TeamSeasonKey + "|" + string.Join(",", cards));
            }
            return result;
        }

        private enum FillerFirstResult
        {
            Tie,
            FillerWins
        }

        private static void CompleteWorld(ManagerHistoricalRuntimeState runtime, BalanceTable balance, FillerFirstResult result)
        {
            Assert.That(runtime.LeagueWorld, Is.Not.Null, "저장 복원 경로가 구단주 월드를 초기화해야 한다.");
            foreach (OwnerLeagueGroupState group in runtime.LeagueWorld.Groups)
            {
                ManagerLiveSeasonState season = group.Season;
                foreach (ScheduledGameState game in season.Schedule.Games)
                {
                    if (game.IsCompleted) continue;
                    if (result == FillerFirstResult.Tie) { game.Complete(0, 0); continue; }
                    bool isAwayFiller = LeagueFillerTeamKey.IsFillerKey(season.GetTeamSeasonKey(game.AwayTeamId));
                    bool isHomeFiller = LeagueFillerTeamKey.IsFillerKey(season.GetTeamSeasonKey(game.HomeTeamId));
                    bool isAwayWinner = isAwayFiller != isHomeFiller ? isAwayFiller : game.AwayTeamId < game.HomeTeamId;
                    game.Complete(isAwayWinner ? 1 : 0, isAwayWinner ? 0 : 1);
                }
            }
            CompleteOwnerPostseason(runtime, balance);
        }

        private static void CompleteOwnerPostseason(ManagerHistoricalRuntimeState runtime, BalanceTable balance)
        {
            new OwnerPostseasonService(balance).EnsureInitialized(runtime);
            int gameId = 1_900_000;
            foreach (OwnerLeagueGroupState group in runtime.LeagueWorld.Groups)
            {
                OwnerPostseasonState postseason = group.Postseason;
                while (!postseason.IsCompleted)
                {
                    OwnerPostseasonSeriesState series = postseason.EnsureCurrentSeries();
                    while (!series.IsCompleted)
                    {
                        ScheduledGameState game = series.AppendNextGame(gameId, (ulong)gameId);
                        gameId++;
                        bool higherSeedIsHome = game.HomeTeamId == series.HigherSeedTeamId;
                        game.Complete(higherSeedIsHome ? 0 : 1, higherSeedIsHome ? 1 : 0);
                        series.RecordCompletedGame(game);
                    }
                }
            }
        }

        private static ManagerHistoricalRuntimeState CreateRuntime(bool includeSpecialCompositeTeams,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, includeSpecialCompositeTeams });
            Type fixtureDataType = fixture.GetType();
            var original = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            return adapter.Restore(adapter.CreateSaveData(original));
        }
    }
}

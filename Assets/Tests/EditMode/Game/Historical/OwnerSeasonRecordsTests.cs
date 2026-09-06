using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 모드의 현재 시즌 개인 기록 집계·저장·리더보드 경로를 검증한다.</summary>
    public sealed class OwnerSeasonRecordsTests
    {
        [Test]
        public void 경기를진행하면양구단선수기록이BoxScore합계와일치한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider, out _);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());

            ManagerModeMatchResult result = service.PlayNextGame(runtime);

            CompetitionStatisticsState competition =
                runtime.ManagerMode.LiveSeason.Statistics.RegularSeason;
            Assert.That(competition.Players.Count, Is.GreaterThan(0));

            int awayTeamId = result.Match.Input.AwayRoster.TeamId;
            int homeTeamId = result.Match.Input.HomeRoster.TeamId;
            Assert.That(
                SumHits(competition, awayTeamId),
                Is.EqualTo(result.Match.AwayBoxScore.Hits),
                "원정 구단 안타 합계가 BoxScore와 다르다.");
            Assert.That(
                SumHits(competition, homeTeamId),
                Is.EqualTo(result.Match.HomeBoxScore.Hits),
                "홈 구단 안타 합계가 BoxScore와 다르다.");
            Assert.That(
                SumEarnedRuns(competition, awayTeamId) + SumEarnedRuns(competition, homeTeamId),
                Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AI구단끼리의경기도리그기록에집계된다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider, out _);
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;

            new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime);

            var teamsWithRecords = new HashSet<int>();
            foreach (PlayerCompetitionStatisticsState player in season.Statistics.RegularSeason.Players.Values)
                teamsWithRecords.Add(player.TeamId);

            // 한 라운드가 끝나면 bye가 아닌 모든 구단에 기록이 있어야 한다.
            Assert.That(teamsWithRecords.Count, Is.GreaterThan(2),
                "플레이어 구단과 상대 구단 외에는 기록이 쌓이지 않았다.");
            Assert.That(teamsWithRecords.Contains(season.PlayerTeamId), Is.True);
        }

        [Test]
        public void 같은Seed두런타임의시즌기록이완전히일치한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState first, out IHistoricalContentProvider firstProvider, out _);
            CreateRuntime(out ManagerHistoricalRuntimeState second, out IHistoricalContentProvider secondProvider, out _);

            for (int round = 0; round < 3; round++)
            {
                new ManagerModeMatchService(firstProvider, BalanceTable.CreateDefault()).PlayNextGame(first);
                new ManagerModeMatchService(secondProvider, BalanceTable.CreateDefault()).PlayNextGame(second);
            }

            AssertSameStatistics(
                first.ManagerMode.LiveSeason.Statistics,
                second.ManagerMode.LiveSeason.Statistics);
        }

        [Test]
        public void 저장복원왕복에서시즌누적이보존된다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out ManagerHistoricalSaveAdapter adapter);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            for (int round = 0; round < 3; round++) service.PlayNextGame(runtime);

            ManagerHistoricalSaveData save = adapter.CreateSaveData(runtime);
            Assert.That(save.saveVersion, Is.EqualTo(ManagerHistoricalSaveAdapter.CurrentSaveVersion));
            ManagerHistoricalRuntimeState restored = adapter.Restore(save);

            AssertSameStatistics(
                runtime.ManagerMode.LiveSeason.Statistics,
                restored.ManagerMode.LiveSeason.Statistics);
        }

        [Test]
        public void 기록이없는이전세이브도빈기록으로복원된다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out ManagerHistoricalSaveAdapter adapter);
            new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime);

            ManagerHistoricalSaveData save = adapter.CreateSaveData(runtime);
            save.managerMode.liveSeason.statistics = null;
            ManagerHistoricalRuntimeState restored = adapter.Restore(save);

            Assert.That(restored.ManagerMode.LiveSeason.Statistics.RegularSeason.Players.Count, Is.Zero);
        }

        [Test]
        public void 리더보드는규정미달선수를제외하고내구단선수를강조한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider, out _);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            for (int round = 0; round < 5; round++) service.PlayNextGame(runtime);

            OwnerSeasonRecordsView view = new OwnerSeasonRecordsService().Build(
                runtime,
                teamSeasonKey => teamSeasonKey);

            Assert.That(view.HasAnyRecord, Is.True);
            Assert.That(view.Categories.Count, Is.EqualTo(4));

            OwnerSeasonRecordsCategoryView batting = FindCategory(view, CareerRecordCategory.Batting);
            Assert.That(batting.Leaderboard.Length, Is.GreaterThan(0), "타격 리더보드가 비어 있다.");
            Assert.That(batting.QualifiedPlayerCount, Is.GreaterThanOrEqualTo(batting.Leaderboard.Length));

            CompetitionStatisticsState competition =
                runtime.ManagerMode.LiveSeason.Statistics.RegularSeason;
            for (int index = 0; index < batting.Leaderboard.Length; index++)
            {
                PlayerCompetitionStatisticsState player =
                    competition.GetPlayer(batting.Leaderboard[index].PlayerId);
                int teamGames = runtime.ManagerMode.LiveSeason.GetCompletedGameCount(player.TeamId);
                Assert.That(
                    LeagueLeaderboardService.IsQualified(
                        player,
                        CareerRecordCategory.Batting,
                        teamGames,
                        CompetitionScope.RegularSeason),
                    Is.True,
                    "규정 미달 선수가 리더보드에 있다.");
                Assert.That(
                    batting.Leaderboard[index].IsMyPlayer,
                    Is.EqualTo(player.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId));
            }

            // 대표 지표는 내림차순(타율)이어야 한다.
            for (int index = 1; index < batting.Leaderboard.Length; index++)
            {
                Assert.That(
                    batting.Leaderboard[index].Metrics[0].Value,
                    Is.LessThanOrEqualTo(batting.Leaderboard[index - 1].Metrics[0].Value));
            }
        }

        private static OwnerSeasonRecordsCategoryView FindCategory(
            OwnerSeasonRecordsView view,
            CareerRecordCategory category)
        {
            for (int index = 0; index < view.Categories.Count; index++)
                if (view.Categories[index].Category == category) return view.Categories[index];
            throw new InvalidOperationException($"{category} 부문이 없습니다.");
        }

        private static int SumHits(CompetitionStatisticsState competition, int teamId)
        {
            int total = 0;
            foreach (PlayerCompetitionStatisticsState player in competition.Players.Values)
                if (player.TeamId == teamId) total += player.Batting.Hits;
            return total;
        }

        private static int SumEarnedRuns(CompetitionStatisticsState competition, int teamId)
        {
            int total = 0;
            foreach (PlayerCompetitionStatisticsState player in competition.Players.Values)
                if (player.TeamId == teamId) total += player.Pitching.EarnedRuns;
            return total;
        }

        private static void AssertSameStatistics(
            LeagueSeasonStatisticsState expected,
            LeagueSeasonStatisticsState actual)
        {
            Assert.That(
                actual.RegularSeason.Players.Count,
                Is.EqualTo(expected.RegularSeason.Players.Count),
                "기록이 있는 선수 수가 다르다.");
            foreach (KeyValuePair<int, PlayerCompetitionStatisticsState> pair in expected.RegularSeason.Players)
            {
                PlayerCompetitionStatisticsState other = actual.RegularSeason.GetPlayer(pair.Key);
                Assert.That(other, Is.Not.Null, $"PlayerId {pair.Key} 기록이 없다.");
                Assert.That(other.TeamId, Is.EqualTo(pair.Value.TeamId));
                Assert.That(other.TeamGames, Is.EqualTo(pair.Value.TeamGames));
                Assert.That(other.Batting.PlateAppearances, Is.EqualTo(pair.Value.Batting.PlateAppearances));
                Assert.That(other.Batting.Hits, Is.EqualTo(pair.Value.Batting.Hits));
                Assert.That(other.Batting.HomeRuns, Is.EqualTo(pair.Value.Batting.HomeRuns));
                Assert.That(other.Batting.RunsBattedIn, Is.EqualTo(pair.Value.Batting.RunsBattedIn));
                Assert.That(other.Pitching.OutsRecorded, Is.EqualTo(pair.Value.Pitching.OutsRecorded));
                Assert.That(other.Pitching.EarnedRuns, Is.EqualTo(pair.Value.Pitching.EarnedRuns));
                Assert.That(other.Pitching.Strikeouts, Is.EqualTo(pair.Value.Pitching.Strikeouts));
                Assert.That(other.Pitching.Wins, Is.EqualTo(pair.Value.Pitching.Wins));
                Assert.That(other.Pitching.Saves, Is.EqualTo(pair.Value.Pitching.Saves));
            }
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            var state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            provider = (IHistoricalContentProvider)fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }
    }
}

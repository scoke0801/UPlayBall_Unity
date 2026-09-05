using System;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Rules;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerModeMatchServiceTests
    {
        [Test]
        public void PlayNextGame_UsesDetailedPathAndUpdatesConditionFamiliarityAndSchedule()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            TeamSeasonPlayerStatus firstHitter = runtime.ManagerMode
                .GetPlayerStatus(runtime.PlayerTeamSeasonKey)
                .Players[0];
            int conditionBefore = firstHitter.StoredBaseCondition;
            ScheduledGameState scheduled = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());

            ManagerModeMatchResult result = service.PlayNextGame(runtime);

            Assert.That(result.Match.Input.RulesVersion, Is.EqualTo(SimulationRulesVersion.DetailedV2));
            Assert.That(result.Match.Input.GameId, Is.EqualTo(scheduled.GameId));
            Assert.That(scheduled.IsCompleted, Is.True);
            Assert.That(firstHitter.StoredBaseCondition, Is.LessThan(conditionBefore));
            Assert.That(
                runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey).Entries.Count,
                Is.GreaterThan(0));
            Assert.That(result.Match.BatteryUsage.Count, Is.GreaterThan(0));
            Assert.That(
                SumBatteryFamiliarity(runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey)),
                Is.GreaterThan(0));
            Assert.That(result.PlayerPlan.ScheduledGameId, Is.EqualTo(scheduled.GameId));
        }

        private static int SumBatteryFamiliarity(TeamChemistryFamiliarityState state)
        {
            int total = 0;
            for (int index = 0; index < state.Entries.Count; index++)
                total += state.Entries[index].BatteryFamiliarity;
            return total;
        }

        [Test]
        public void SameSaveAndSeed_ReproducesMatchAndAttendance()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState first, out IHistoricalContentProvider firstProvider);
            CreateRuntime(out ManagerHistoricalRuntimeState second, out IHistoricalContentProvider secondProvider);

            ManagerModeMatchResult firstResult = new ManagerModeMatchService(
                firstProvider,
                BalanceTable.CreateDefault()).PlayNextGame(first);
            ManagerModeMatchResult secondResult = new ManagerModeMatchService(
                secondProvider,
                BalanceTable.CreateDefault()).PlayNextGame(second);

            Assert.That(firstResult.Match.AwayBoxScore.Runs, Is.EqualTo(secondResult.Match.AwayBoxScore.Runs));
            Assert.That(firstResult.Match.HomeBoxScore.Runs, Is.EqualTo(secondResult.Match.HomeBoxScore.Runs));
            Assert.That(firstResult.Match.PitcherUsage.Count, Is.EqualTo(secondResult.Match.PitcherUsage.Count));
            Assert.That(firstResult.HomeFinance.Status, Is.EqualTo(secondResult.HomeFinance.Status));
            Assert.That(firstResult.HomeFinance.Attendance, Is.EqualTo(secondResult.HomeFinance.Attendance));
        }

        [Test]
        public void SelectedPreset_IsRevalidatedImmediatelyBeforeMatch()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            LineupPresetState original = runtime.ManagerMode.GetSelectedLineupPreset();
            var invalidBattingOrder = new string[original.BattingOrderCardIds.Count];
            for (int index = 0; index < invalidBattingOrder.Length; index++)
                invalidBattingOrder[index] = original.BattingOrderCardIds[index];
            invalidBattingOrder[1] = invalidBattingOrder[0];
            var invalid = new LineupPresetState(
                "preset:invalid",
                "잘못된 프리셋",
                original.StartingLineupSlots,
                invalidBattingOrder,
                original.BenchPriorityCardIds,
                original.StarterRotationCardIds,
                original.BullpenAssignmentCardIds,
                original.SetupPitcherCardId,
                original.CloserPitcherCardId,
                original.TeamColorIds,
                original.DefaultTacticCardIds);
            runtime.ManagerMode.UpsertLineupPreset(invalid);
            runtime.ManagerMode.SelectLineupPreset(invalid.PresetId);
            ScheduledGameState scheduled = runtime.ManagerMode.LiveSeason.NextPlayerGame;

            Assert.Throws<InvalidOperationException>(() =>
                new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime));
            Assert.That(scheduled.IsCompleted, Is.False);
        }

        [Test]
        public void PitcherAndCatcherBatteryLookup_UsesCurrentPairWithoutAccumulation()
        {
            var balance = BalanceTable.CreateDefault();
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);

            ManagerModeMatchResult result = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime);
            MatchRosterSnapshot roster = result.Match.Input.AwayRoster.TeamId ==
                                         runtime.ManagerMode.LiveSeason.PlayerTeamId
                ? result.Match.Input.AwayRoster
                : result.Match.Input.HomeRoster;
            int catcherId = roster.StartingLineup[0].Player.PlayerId;
            Assert.That(
                roster.TryGetBatteryConditionModifier(
                    roster.StartingPitcher.Player.PlayerId,
                    catcherId,
                    out int starterModifier),
                Is.True);
            Assert.That(
                roster.TryGetBatteryConditionModifier(
                    roster.Bullpen[0].Player.PlayerId,
                    catcherId,
                    out int reliefModifier),
                Is.True);
            Assert.That(starterModifier, Is.InRange(-balance.ConditionChemistry.ConditionLevelStep,
                balance.ConditionChemistry.ConditionLevelStep));
            Assert.That(reliefModifier, Is.InRange(-balance.ConditionChemistry.ConditionLevelStep,
                balance.ConditionChemistry.ConditionLevelStep));
        }

        [Test]
        public void HomeGame_AppliesAttendanceRevenueAndFanChangeOnlyOnPlayerHomeGame()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            ScheduledGameState next = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            while (next.HomeTeamId != runtime.ManagerMode.LiveSeason.PlayerTeamId)
            {
                ManagerModeMatchResult away = service.PlayNextGame(runtime);
                Assert.That(away.HomeFinance.Status, Is.EqualTo(HomeGameFinanceStatus.NotHomeGame));
                next = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            }

            long moneyBefore = runtime.Economy.Money;
            double fanBaseBefore = runtime.ManagerMode.ClubOperation.FanBase;
            ManagerModeMatchResult home = service.PlayNextGame(runtime);

            Assert.That(home.HomeFinanceStatus, Is.EqualTo(ManagerModeTransactionStatus.Applied));
            Assert.That(home.HomeFinance.Attendance, Is.InRange(0, home.HomeFinance.Capacity));
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore + home.HomeFinance.NetGameIncome));
            Assert.That(runtime.ManagerMode.ClubOperation.CurrentSeason.HomeGames, Is.EqualTo(1));
            Assert.That(runtime.ManagerMode.ClubOperation.FanBase, Is.Not.EqualTo(fanBaseBefore));
        }

        [Test]
        public void PreviewNextHomeAttendance_실제경기와같은Context와Seed를사용한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            while (runtime.ManagerMode.LiveSeason.NextPlayerGame.HomeTeamId !=
                   runtime.ManagerMode.LiveSeason.PlayerTeamId)
                service.PlayNextGame(runtime);

            AttendanceResult? preview = service.PreviewNextHomeAttendance(runtime);
            ManagerModeMatchResult result = service.PlayNextGame(runtime);

            Assert.That(preview.HasValue, Is.True);
            Assert.That(preview.Value.Attendance, Is.EqualTo(result.HomeFinance.Attendance));
            Assert.That(preview.Value.Attendance, Is.LessThanOrEqualTo(preview.Value.Capacity));
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider)
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
            var adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            object rawProvider = fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            provider = (IHistoricalContentProvider)rawProvider;
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }
    }
}

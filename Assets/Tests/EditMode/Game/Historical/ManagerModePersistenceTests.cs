using System;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerModePersistenceTests
    {
        [Test]
        public void V4DtoRoundTrip_PreservesRawStateAndIdempotencyMarkers()
        {
            CreateLegacyFixture(out ManagerHistoricalRuntimeState state, out ManagerHistoricalSaveAdapter adapter);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(state);
            ManagerModeSaveData manager = save.managerMode;
            string seasonId = manager.clubOperation.currentSeason.seasonId;
            string teamKey = manager.clubOperation.teamSeasonKey;
            string receiptId = OperationReceipt.CreateWeeklyFacilityReceiptId(teamKey, seasonId, 0);
            manager.clubOperation.receipts = new[]
            {
                new OperationReceiptSaveData
                {
                    receiptId = receiptId,
                    kind = (int)OperationReceiptKind.FacilityProduction,
                    seasonId = seasonId,
                    weekIndex = 0,
                    sourceId = teamKey,
                    money = -123L,
                    scoutingPoints = 4,
                    developmentPoints = 5
                }
            };
            manager.clubOperation.currentWeek.moneyExpense = 123L;
            manager.clubOperation.currentWeek.scoutingPointProduction = 4;
            manager.clubOperation.currentWeek.developmentPointProduction = 5;
            manager.clubOperation.currentWeek.receiptCount = 1;
            manager.clubOperation.currentSeason.moneyExpense = 123L;
            manager.clubOperation.currentSeason.scoutingPointProduction = 4;
            manager.clubOperation.currentSeason.developmentPointProduction = 5;

            string staffId = StaffCatalogGenerator.CreateStableStaffId(
                manager.staffCatalogSeed,
                StaffRole.HittingCoach,
                0);
            manager.staffContracts = new[]
            {
                new StaffContractSaveData
                {
                    contractId = "contract:persisted",
                    staffId = staffId,
                    teamSeasonKey = teamKey,
                    startSeason = 1,
                    remainingSeasons = 2,
                    annualSalary = 1200L,
                    hasLastSalaryPaidSeason = true,
                    lastSalaryPaidSeason = 1
                }
            };
            manager.staffAssignment.hittingCoachStaffId = staffId;

            PlayerStatusSaveData first = manager.playerStatuses[0].players[0];
            PlayerStatusSaveData second = manager.playerStatuses[0].players[1];
            first.storedBaseCondition = 63;
            first.availability = (int)PlayerAvailabilityStatus.DayToDay;
            first.previousDayPitches = 37;
            first.twoDaysAgoPitches = 11;
            manager.familiarities[0].entries = new[]
            {
                new ChemistryFamiliarityEntrySaveData
                {
                    firstPlayerPersonId = first.playerPersonId,
                    secondPlayerPersonId = second.playerPersonId,
                    lineupFamiliarity = 7,
                    batteryFamiliarity = 3
                }
            };
            manager.liveSeason.currentWeekIndex = 2;
            manager.liveSeason.games[0].isCompleted = true;
            manager.liveSeason.games[0].awayRuns = 3;
            manager.liveSeason.games[0].homeRuns = 5;

            ManagerHistoricalRuntimeState restored = adapter.Restore(save);
            ManagerModeRuntimeState restoredManager = restored.ManagerMode;

            Assert.That(restoredManager.ClubOperation.HasReceipt(receiptId), Is.True);
            Assert.That(restoredManager.ClubOperation.CurrentWeek.ReceiptCount, Is.EqualTo(1));
            Assert.That(restoredManager.ClubOperation.CurrentSeason.MoneyExpense, Is.EqualTo(123L));
            Assert.That(restoredManager.StaffContracts.Count, Is.EqualTo(1));
            Assert.That(restoredManager.StaffContracts[0].LastSalaryPaidSeason, Is.EqualTo(1));
            Assert.That(restoredManager.StaffAssignment.HittingCoachStaffId, Is.EqualTo(staffId));
            TeamSeasonPlayerStatus playerStatus = restoredManager.PlayerStatuses[0]
                .GetRequiredPlayer(first.playerPersonId);
            Assert.That(playerStatus.StoredBaseCondition, Is.EqualTo(63));
            Assert.That(playerStatus.Availability, Is.EqualTo(PlayerAvailabilityStatus.DayToDay));
            Assert.That(playerStatus.PitchingWorkload.PreviousDayPitches, Is.EqualTo(37));
            var pair = new PlayerPersonPairKey(first.playerPersonId, second.playerPersonId);
            Assert.That(restoredManager.Familiarities[0].GetLineupFamiliarity(pair), Is.EqualTo(7));
            Assert.That(restoredManager.Familiarities[0].GetBatteryFamiliarity(pair), Is.EqualTo(3));
            Assert.That(restoredManager.LiveSeason.CurrentWeekIndex, Is.EqualTo(2));
            Assert.That(restoredManager.LiveSeason.Schedule.Games[0].IsCompleted, Is.True);
            Assert.That(restoredManager.LiveSeason.Schedule.Games[0].HomeRuns, Is.EqualTo(5));

            var production = new WeeklyFacilityProductionResolver(BalanceTable.CreateDefault().ClubOperation)
                .Resolve(
                    restoredManager.ClubOperation,
                    new WeeklyFacilityProductionContext(seasonId, 0, LeagueGrade.Rookie, 999999L, 0, 0));
            Assert.That(production.Status, Is.EqualTo(WeeklyFacilityProductionStatus.AlreadyApplied));
            StaffSalarySettlementResult salary = new StaffContractService().SettleSalaries(
                new StaffSalarySettlementCommand("salary:retry", teamKey, 1, 999999L),
                restoredManager.StaffContracts);
            Assert.That(salary.Status, Is.EqualTo(StaffServiceStatus.NoChange));
            Assert.That(salary.MoneyCommand, Is.Null);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Restore_V1ToV3_ExplicitlyCreatesInitialManagerMode(int legacyVersion)
        {
            CreateLegacyFixture(out ManagerHistoricalRuntimeState state, out ManagerHistoricalSaveAdapter adapter);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(state);
            save.saveVersion = legacyVersion;
            save.managerMode = null;
            if (legacyVersion == 1) save.identityRegistry = null;

            ManagerHistoricalRuntimeState restored = adapter.Restore(save);

            Assert.That(restored.HasManagerMode, Is.True);
            Assert.That(restored.ManagerMode.StaffContracts, Is.Empty);
            Assert.That(restored.ManagerMode.ClubOperation.Receipts, Is.Empty);
            Assert.That(restored.ManagerMode.LiveSeason.CurrentWeekIndex, Is.Zero);
            Assert.That(restored.ManagerMode.LineupPresets.Count, Is.EqualTo(1));
            for (int index = 0; index < restored.ManagerMode.LiveSeason.Schedule.Games.Count; index++)
                Assert.That(restored.ManagerMode.LiveSeason.Schedule.Games[index].IsCompleted, Is.False);
        }

        [Test]
        public void V4Save_DoesNotPersistDefinitionsReportsOrDerivedChemistry()
        {
            FieldInfo[] fields = typeof(ManagerModeSaveData).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int index = 0; index < fields.Length; index++)
            {
                Assert.That(fields[index].FieldType, Is.Not.EqualTo(typeof(StaffCatalog)));
                Assert.That(fields[index].FieldType, Is.Not.EqualTo(typeof(StaffDefinition[])));
                Assert.That(fields[index].Name, Does.Not.Contain("scoutingReport").IgnoreCase);
                Assert.That(fields[index].Name, Does.Not.Contain("chemistryScore").IgnoreCase);
            }
        }

        [Test]
        public void Restore_V4WithUnknownStaffCatalogVersion_IsRejected()
        {
            CreateLegacyFixture(out ManagerHistoricalRuntimeState state, out ManagerHistoricalSaveAdapter adapter);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(state);
            save.managerMode.staffCatalogVersion = "unknown-staff-catalog";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Restore(save));

            Assert.That(exception.Message, Does.Contain("StaffCatalog version"));
        }

        private static void CreateLegacyFixture(
            out ManagerHistoricalRuntimeState state,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
        }
    }
}

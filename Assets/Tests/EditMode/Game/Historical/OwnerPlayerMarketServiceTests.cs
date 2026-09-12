using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>선수 계약 Command가 Aggregate와 Save를 원자적으로 갱신하는지 검증한다.</summary>
    public sealed class OwnerPlayerMarketServiceTests
    {

        [Test]
        public void EnsureInitialized_PartialContractState_RejectsCorruptedAggregate()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            OwnerPlayerContractState first = runtime.ManagerMode.PlayerContracts[0];
            runtime.ManagerMode.ReplacePlayerContractState(new[] { first });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => service.EnsureInitialized(runtime));

            Assert.That(exception.Message, Does.Contain("로스터와 선수 계약 수"));
        }

        [Test]
        public void EnsureInitialized_계약수만25개인구버전Roster불일치는현재CardId로복구한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            string missingCardId = roster.Entries[0].CardId;
            var mismatched = new OwnerPlayerContractState[runtime.ManagerMode.PlayerContracts.Count];
            for (int index = 0; index < mismatched.Length; index++)
                mismatched[index] = runtime.ManagerMode.PlayerContracts[index];
            int replacedIndex = Array.FindIndex(
                mismatched,
                contract => string.Equals(contract.CardId, missingCardId, StringComparison.Ordinal));
            Assert.That(replacedIndex, Is.GreaterThanOrEqualTo(0));
            mismatched[replacedIndex] = new OwnerPlayerContractState(
                "legacy-mismatched-contract",
                "LEGACY-NOT-ON-ACTIVE-ROSTER",
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                2,
                1L);
            runtime.ManagerMode.ReplacePlayerContractState(mismatched);

            service.EnsureInitialized(runtime);

            Assert.That(runtime.ManagerMode.PlayerContracts.Count, Is.EqualTo(roster.Entries.Count));
            Assert.That(runtime.ManagerMode.GetPlayerContract(missingCardId), Is.Not.Null);
            Assert.Throws<KeyNotFoundException>(() =>
                runtime.ManagerMode.GetPlayerContract("LEGACY-NOT-ON-ACTIVE-ROSTER"));
        }



        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            ManagerHistoricalRuntimeState original = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(original);
            save.economy.money = MoneyAmount.FromTenThousandWon(500_000L);
            runtime = adapter.Restore(save);
        }
    }
}

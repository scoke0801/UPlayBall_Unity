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
        public void Renew_ValidPreview_SpendsSigningCostAndExtendsRemainingTerm()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            OwnerPlayerContractState contract = runtime.ManagerMode.PlayerContracts[0];
            Assert.That(runtime.TryGetOwnedCard(contract.CardId, out OwnedPlayerCardState owned), Is.True);
            owned.AddDuplicate(3);
            int duplicateCount = owned.DuplicateCount;
            long moneyBefore = runtime.Economy.Money;
            int remainingBefore = contract.RemainingSeasons;

            OwnerContractRenewalPreview preview = service.PreviewRenewal(runtime, contract.CardId, 3);
            OwnerContractRenewalPreview result = service.Renew(runtime, contract.CardId, 3);

            Assert.That(preview.CanCommit, Is.True);
            Assert.That(result.CanCommit, Is.True);
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore - result.SigningCost));
            Assert.That(contract.RemainingSeasons, Is.EqualTo(remainingBefore + 3));
            Assert.That(contract.AnnualSalary, Is.EqualTo(result.AnnualSalary));
            Assert.That(owned.DuplicateCount, Is.EqualTo(duplicateCount));
            Assert.That(runtime.ManagerMode.PlayerContracts.Count, Is.EqualTo(25));
        }

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

        [Test]
        public void RenewExpiringContracts_전체비용차감후만료임박선수만연장하고저장한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out ManagerHistoricalSaveAdapter adapter);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            var first = runtime.ManagerMode.PlayerContracts[0];
            var second = runtime.ManagerMode.PlayerContracts[1];
            first.Renew(1, 1, first.AnnualSalary);
            second.Renew(1, 1, second.AnnualSalary);
            int unchanged = runtime.ManagerMode.PlayerContracts[2].RemainingSeasons;
            long money = runtime.Economy.Money;
            OwnerContractBatchPreview preview = service.PreviewExpiringRenewals(runtime, 1);
            Assert.That(preview.Renewals.Count, Is.EqualTo(2));
            Assert.That(runtime.Economy.Money, Is.EqualTo(money));

            OwnerContractBatchPreview result = service.RenewExpiringContracts(runtime, 1);

            Assert.That(result.CanCommit, Is.True);
            Assert.That(runtime.Economy.Money, Is.EqualTo(money - preview.SigningCost));
            Assert.That(first.RemainingSeasons, Is.EqualTo(2));
            Assert.That(second.RemainingSeasons, Is.EqualTo(2));
            Assert.That(runtime.ManagerMode.PlayerContracts[2].RemainingSeasons, Is.EqualTo(unchanged));
            Assert.That(runtime.ManagerMode.GetAnnualPlayerSalaryTotal(), Is.EqualTo(preview.AnnualSalaryTotal));
            Assert.That(service.PreviewExpiringRenewals(runtime, 1).CanCommit, Is.False);
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(restored.ManagerMode.GetPlayerContract(first.CardId).RemainingSeasons, Is.EqualTo(2));
        }

        [Test]
        public void RenewExpiringContracts_개별비용만충당가능하면아무계약도변경하지않는다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            foreach (var contract in runtime.ManagerMode.PlayerContracts)
                contract.Renew(1, 1, contract.AnnualSalary);
            long total = service.PreviewExpiringRenewals(runtime, 2).SigningCost;
            runtime.Economy.TrySpendMoney(runtime.Economy.Money - total + 1);
            long before = runtime.Economy.Money;

            var result = service.RenewExpiringContracts(runtime, 2);

            Assert.That(result.CanCommit, Is.False);
            Assert.That(runtime.Economy.Money, Is.EqualTo(before));
            foreach (var contract in runtime.ManagerMode.PlayerContracts)
                Assert.That(contract.RemainingSeasons, Is.EqualTo(1));
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

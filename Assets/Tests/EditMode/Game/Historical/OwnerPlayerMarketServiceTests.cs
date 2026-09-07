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
    /// <summary>선수 계약·트레이드 Command가 Aggregate와 Save를 원자적으로 갱신하는지 검증한다.</summary>
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
        public void CommitTrade_ValidProposal_UpdatesBothRostersContractReceiptAndSave()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out ManagerHistoricalSaveAdapter adapter);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            OwnerTradePreview preview = FindValidTrade(runtime, service);
            int ownedCountBefore = runtime.OwnedCards.Count;

            OwnerTradePreview result = service.CommitTrade(
                runtime,
                preview.PartnerTeamSeasonKey,
                preview.OutgoingCardId,
                preview.IncomingCardId);

            Assert.That(result.CanCommit, Is.True);
            Assert.That(ContainsCard(runtime.GetRoster(runtime.PlayerTeamSeasonKey), preview.OutgoingCardId), Is.False);
            Assert.That(ContainsCard(runtime.GetRoster(runtime.PlayerTeamSeasonKey), preview.IncomingCardId), Is.True);
            Assert.That(ContainsCard(runtime.GetRoster(preview.PartnerTeamSeasonKey), preview.OutgoingCardId), Is.True);
            Assert.That(runtime.ManagerMode.PlayerContracts.Count, Is.EqualTo(25));
            Assert.That(runtime.ManagerMode.GetPlayerContract(preview.IncomingCardId).RemainingSeasons, Is.EqualTo(2));
            Assert.That(runtime.ManagerMode.TradeReceipts.Count, Is.EqualTo(1));
            Assert.That(runtime.TryGetOwnedCard(preview.IncomingCardId, out _), Is.True);
            Assert.That(runtime.OwnedCards.Count, Is.InRange(ownedCountBefore, ownedCountBefore + 1));

            ManagerHistoricalRuntimeState restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(ContainsCard(restored.GetRoster(restored.PlayerTeamSeasonKey), preview.IncomingCardId), Is.True);
            Assert.That(restored.ManagerMode.TradeReceipts.Count, Is.EqualTo(1));
            Assert.That(restored.ManagerMode.GetPlayerContract(preview.IncomingCardId).RemainingSeasons, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("UNKNOWN_TEAM")]
        public void PreviewTrade_InvalidPartner_ReturnsReasonWithoutMutation(string partnerTeamSeasonKey)
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            string outgoingCardId = playerRoster.Entries[0].CardId;

            OwnerTradePreview preview = service.PreviewTrade(
                runtime,
                partnerTeamSeasonKey,
                outgoingCardId,
                outgoingCardId);

            Assert.That(preview.Status, Is.EqualTo(OwnerPlayerMarketStatus.InvalidSelection));
            Assert.That(preview.CanCommit, Is.False);
            Assert.That(preview.Reason, Does.Contain("상대 구단"));
            Assert.That(runtime.ManagerMode.TradeReceipts, Is.Empty);
            Assert.That(runtime.GetRoster(runtime.PlayerTeamSeasonKey), Is.SameAs(playerRoster));
        }

        [Test]
        public void PreviewTrade_PlayerTeamAsPartner_ReturnsReasonWithoutMutation()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);

            OwnerTradePreview preview = service.PreviewTrade(
                runtime,
                runtime.PlayerTeamSeasonKey,
                playerRoster.Entries[0].CardId,
                playerRoster.Entries[1].CardId);

            Assert.That(preview.Status, Is.EqualTo(OwnerPlayerMarketStatus.InvalidSelection));
            Assert.That(preview.CanCommit, Is.False);
            Assert.That(runtime.ManagerMode.TradeReceipts, Is.Empty);
        }

        [Test]
        public void EnsureInitialized_PartialContractState_RejectsCorruptedAggregate()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            OwnerPlayerContractState first = runtime.ManagerMode.PlayerContracts[0];
            runtime.ManagerMode.ReplacePlayerMarketState(
                new[] { first },
                runtime.ManagerMode.TradeReceipts);

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
            runtime.ManagerMode.ReplacePlayerMarketState(mismatched, runtime.ManagerMode.TradeReceipts);

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

        private static OwnerTradePreview FindValidTrade(
            ManagerHistoricalRuntimeState runtime,
            OwnerPlayerMarketService service)
        {
            CurrentRosterState player = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            for (int rosterIndex = 0; rosterIndex < runtime.Rosters.Count; rosterIndex++)
            {
                CurrentRosterState partner = runtime.Rosters[rosterIndex];
                if (string.Equals(partner.TeamSeasonKey, player.TeamSeasonKey, StringComparison.Ordinal)) continue;
                for (int outgoingIndex = 0; outgoingIndex < player.Entries.Count; outgoingIndex++)
                for (int incomingIndex = 0; incomingIndex < partner.Entries.Count; incomingIndex++)
                {
                    OwnerTradePreview preview = service.PreviewTrade(
                        runtime,
                        partner.TeamSeasonKey,
                        player.Entries[outgoingIndex].CardId,
                        partner.Entries[incomingIndex].CardId);
                    if (preview.CanCommit) return preview;
                }
            }
            throw new AssertionException("검증 가능한 1:1 트레이드 조합을 찾지 못했습니다.");
        }

        private static bool ContainsCard(CurrentRosterState roster, string cardId)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
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

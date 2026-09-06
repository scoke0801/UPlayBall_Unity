using System;
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
        public void Renew_ValidPreview_SpendsSigningCostAndReplacesTerms()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            var service = new OwnerPlayerMarketService(BalanceTable.CreateDefault());
            service.EnsureInitialized(runtime);
            OwnerPlayerContractState contract = runtime.ManagerMode.PlayerContracts[0];
            long moneyBefore = runtime.Economy.Money;

            OwnerContractRenewalPreview preview = service.PreviewRenewal(runtime, contract.CardId, 3);
            OwnerContractRenewalPreview result = service.Renew(runtime, contract.CardId, 3);

            Assert.That(preview.CanCommit, Is.True);
            Assert.That(result.CanCommit, Is.True);
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore - result.SigningCost));
            Assert.That(contract.RemainingSeasons, Is.EqualTo(3));
            Assert.That(contract.AnnualSalary, Is.EqualTo(result.AnnualSalary));
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

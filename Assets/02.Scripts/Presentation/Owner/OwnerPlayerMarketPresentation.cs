using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>계약 목록의 한 선수와 현재 계약 조건을 표시한다.</summary>
    public sealed class OwnerContractPlayerRow
    {
        public OwnerContractPlayerRow(string cardId, string name, string role, int cost, int remainingSeasons, long annualSalary)
        {
            CardId = cardId;
            Name = name;
            Role = role;
            Cost = cost;
            RemainingSeasons = remainingSeasons;
            AnnualSalary = annualSalary;
        }

        public string CardId { get; }
        public string Name { get; }
        public string Role { get; }
        public int Cost { get; }
        public int RemainingSeasons { get; }
        public long AnnualSalary { get; }
    }

    public sealed class OwnerContractSnapshot
    {
        public OwnerContractSnapshot(
            IReadOnlyList<OwnerContractPlayerRow> players,
            string selectedCardId,
            int selectedTerm,
            long money,
            long annualSalaryTotal,
            OwnerContractRenewalPreview preview,
            OwnerContractBatchPreview batchPreview = null)
        {
            Players = players ?? throw new ArgumentNullException(nameof(players));
            SelectedCardId = selectedCardId ?? string.Empty;
            SelectedTerm = selectedTerm;
            Money = money;
            AnnualSalaryTotal = annualSalaryTotal;
            Preview = preview;
            BatchPreview = batchPreview;
        }

        public IReadOnlyList<OwnerContractPlayerRow> Players { get; }
        public string SelectedCardId { get; }
        public int SelectedTerm { get; }
        public long Money { get; }
        public long AnnualSalaryTotal { get; }
        public OwnerContractRenewalPreview Preview { get; }
        public OwnerContractBatchPreview BatchPreview { get; }
    }

    public sealed partial class OwnerModeRuntimeSnapshotFactory
    {
        public OwnerContractSnapshot CreatePlayerContracts(OwnerModeManager manager, string selectedCardId, int selectedTerm)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            IReadOnlyList<OwnerPlayerContractState> contracts = manager.GetPlayerContracts();
            var rows = new OwnerContractPlayerRow[contracts.Count];
            for (int index = 0; index < rows.Length; index++)
            {
                OwnerPlayerContractState contract = contracts[index];
                PlayerCardDefinition card = GetCard(runtime, contract.CardId);
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                rows[index] = new OwnerContractPlayerRow(
                    contract.CardId,
                    runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                    FormatRole(season),
                    season.Cost,
                    contract.RemainingSeasons,
                    contract.AnnualSalary);
            }
            Array.Sort(rows, (left, right) =>
            {
                int expiry = left.RemainingSeasons.CompareTo(right.RemainingSeasons);
                return expiry != 0 ? expiry : string.CompareOrdinal(left.Name, right.Name);
            });
            string selected = ResolveSelection(rows, selectedCardId);
            int term = Math.Max(1, Math.Min(manager.Balance.OwnerPlayerMarket.MaximumContractSeasons, selectedTerm));
            OwnerContractRenewalPreview preview = string.IsNullOrEmpty(selected)
                ? null
                : manager.PreviewPlayerContractRenewal(selected, term);
            return new OwnerContractSnapshot(
                rows,
                selected,
                term,
                runtime.Economy.Money,
                runtime.ManagerMode.GetAnnualPlayerSalaryTotal(),
                preview,
                manager.PreviewExpiringPlayerContractRenewals(term));
        }

        private static PlayerCardDefinition GetCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card)) return card;
            throw new InvalidOperationException($"CardId {cardId} 원본이 없습니다.");
        }

        private static string FormatRole(PlayerSeasonDefinition season) =>
            OwnerCollectionPresentationBuilder.FormatPlayerRole(
                season.Position,
                season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null);

        private static string ResolveSelection(IReadOnlyList<OwnerContractPlayerRow> rows, string requested)
        {
            for (int index = 0; index < rows.Count; index++)
                if (string.Equals(rows[index].CardId, requested, StringComparison.Ordinal)) return requested;
            return rows.Count == 0 ? string.Empty : rows[0].CardId;
        }

    }
}

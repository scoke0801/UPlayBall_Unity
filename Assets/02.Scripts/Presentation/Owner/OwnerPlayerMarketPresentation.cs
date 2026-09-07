using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
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

    public sealed class OwnerTradePlayerRow
    {
        public OwnerTradePlayerRow(string teamSeasonKey, string teamName, string cardId, string name, string role, int cost, int value)
        {
            TeamSeasonKey = teamSeasonKey;
            TeamName = teamName;
            CardId = cardId;
            Name = name;
            Role = role;
            Cost = cost;
            Value = value;
        }

        public string TeamSeasonKey { get; }
        public string TeamName { get; }
        public string CardId { get; }
        public string Name { get; }
        public string Role { get; }
        public int Cost { get; }
        public int Value { get; }
    }

    public sealed class OwnerTradeSnapshot
    {
        public OwnerTradeSnapshot(
            IReadOnlyList<OwnerTradePlayerRow> ownedPlayers,
            IReadOnlyList<OwnerTradePlayerRow> targetPlayers,
            string selectedPartnerTeamSeasonKey,
            string selectedOutgoingCardId,
            string selectedIncomingCardId,
            int tradesUsed,
            int tradeLimit,
            OwnerTradePreview preview)
        {
            OwnedPlayers = ownedPlayers ?? throw new ArgumentNullException(nameof(ownedPlayers));
            TargetPlayers = targetPlayers ?? throw new ArgumentNullException(nameof(targetPlayers));
            SelectedPartnerTeamSeasonKey = selectedPartnerTeamSeasonKey ?? string.Empty;
            SelectedOutgoingCardId = selectedOutgoingCardId ?? string.Empty;
            SelectedIncomingCardId = selectedIncomingCardId ?? string.Empty;
            TradesUsed = tradesUsed;
            TradeLimit = tradeLimit;
            Preview = preview;
        }

        public IReadOnlyList<OwnerTradePlayerRow> OwnedPlayers { get; }
        public IReadOnlyList<OwnerTradePlayerRow> TargetPlayers { get; }
        public string SelectedPartnerTeamSeasonKey { get; }
        public string SelectedOutgoingCardId { get; }
        public string SelectedIncomingCardId { get; }
        public int TradesUsed { get; }
        public int TradeLimit { get; }
        public OwnerTradePreview Preview { get; }
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

        public OwnerTradeSnapshot CreatePlayerTrade(
            OwnerModeManager manager,
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            var resolver = new OwnerPlayerMarketResolver(manager.Balance.OwnerPlayerMarket);
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            OwnerTradePlayerRow[] owned = CreateTradeRows(manager, runtime, resolver, playerRoster);
            string partner = ResolvePartner(runtime, partnerTeamSeasonKey);
            var targets = new List<OwnerTradePlayerRow>();
            for (int index = 0; index < runtime.Rosters.Count; index++)
            {
                CurrentRosterState roster = runtime.Rosters[index];
                if (string.Equals(roster.TeamSeasonKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal)) continue;
                targets.AddRange(CreateTradeRows(manager, runtime, resolver, roster));
            }
            targets.Sort((left, right) =>
            {
                int team = string.CompareOrdinal(left.TeamName, right.TeamName);
                return team != 0 ? team : right.Value.CompareTo(left.Value);
            });

            string outgoing = ResolveSelection(owned, outgoingCardId);
            string incoming = ResolveIncoming(targets, partner, incomingCardId);
            OwnerTradePreview preview = string.IsNullOrEmpty(partner) || string.IsNullOrEmpty(outgoing) || string.IsNullOrEmpty(incoming)
                ? null
                : manager.PreviewPlayerTrade(partner, outgoing, incoming);
            int season = runtime.ManagerMode.LiveSeason.SeasonNumber;
            return new OwnerTradeSnapshot(
                owned,
                targets,
                partner,
                outgoing,
                incoming,
                runtime.ManagerMode.CountTrades(season),
                manager.Balance.OwnerPlayerMarket.MaximumTradesPerSeason,
                preview);
        }

        private static OwnerTradePlayerRow[] CreateTradeRows(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            OwnerPlayerMarketResolver resolver,
            CurrentRosterState roster)
        {
            var rows = new OwnerTradePlayerRow[roster.Entries.Count];
            string teamName = manager.GetTeamDisplayName(roster.TeamSeasonKey);
            for (int index = 0; index < rows.Length; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                PlayerCardDefinition card = GetCard(runtime, entry.CardId);
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                int enhancement = runtime.TryGetOwnedCard(entry.CardId, out OwnedPlayerCardState owned)
                    ? owned.EnhancementLevel
                    : 0;
                rows[index] = new OwnerTradePlayerRow(
                    roster.TeamSeasonKey,
                    teamName,
                    entry.CardId,
                    runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId),
                    FormatRole(season),
                    season.Cost,
                    resolver.ResolveValue(runtime.WorldCardCatalog, entry.CardId, enhancement));
            }
            Array.Sort(rows, (left, right) => right.Value.CompareTo(left.Value));
            return rows;
        }

        private static PlayerCardDefinition GetCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card)) return card;
            throw new InvalidOperationException($"CardId {cardId} 원본이 없습니다.");
        }

        private static string FormatRole(PlayerSeasonDefinition season) =>
            OwnerCollectionPresentationBuilder.FormatPosition(season.Position);

        private static string ResolveSelection(IReadOnlyList<OwnerContractPlayerRow> rows, string requested)
        {
            for (int index = 0; index < rows.Count; index++)
                if (string.Equals(rows[index].CardId, requested, StringComparison.Ordinal)) return requested;
            return rows.Count == 0 ? string.Empty : rows[0].CardId;
        }

        private static string ResolveSelection(IReadOnlyList<OwnerTradePlayerRow> rows, string requested)
        {
            for (int index = 0; index < rows.Count; index++)
                if (string.Equals(rows[index].CardId, requested, StringComparison.Ordinal)) return requested;
            return rows.Count == 0 ? string.Empty : rows[0].CardId;
        }

        private static string ResolvePartner(ManagerHistoricalRuntimeState runtime, string requested)
        {
            for (int index = 0; index < runtime.Rosters.Count; index++)
            {
                string key = runtime.Rosters[index].TeamSeasonKey;
                if (string.Equals(key, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal)) continue;
                if (string.Equals(key, requested, StringComparison.Ordinal)) return key;
            }
            for (int index = 0; index < runtime.Rosters.Count; index++)
                if (!string.Equals(runtime.Rosters[index].TeamSeasonKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                    return runtime.Rosters[index].TeamSeasonKey;
            return string.Empty;
        }

        private static string ResolveIncoming(IReadOnlyList<OwnerTradePlayerRow> rows, string partner, string requested)
        {
            for (int index = 0; index < rows.Count; index++)
                if (string.Equals(rows[index].TeamSeasonKey, partner, StringComparison.Ordinal) &&
                    string.Equals(rows[index].CardId, requested, StringComparison.Ordinal)) return requested;
            for (int index = 0; index < rows.Count; index++)
                if (string.Equals(rows[index].TeamSeasonKey, partner, StringComparison.Ordinal)) return rows[index].CardId;
            return string.Empty;
        }
    }
}

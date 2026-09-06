using System;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 선수 계약과 1:1 트레이드를 Preview/Validate/Command 경계로 조정한다.</summary>
    public sealed class OwnerPlayerMarketService
    {
        private readonly BalanceTable _balance;
        private readonly OwnerPlayerMarketResolver _resolver;

        public OwnerPlayerMarketService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _resolver = new OwnerPlayerMarketResolver(balance.OwnerPlayerMarket);
        }

        public void EnsureInitialized(ManagerHistoricalRuntimeState runtime)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            if (mode.PlayerContracts.Count == 0)
            {
                OwnerPlayerContractState[] contracts = _resolver.CreateInitialContracts(
                    playerRoster,
                    runtime.WorldCardCatalog,
                    mode.LiveSeason.SeasonNumber);
                mode.ReplacePlayerMarketState(contracts, mode.TradeReceipts);
            }

            ValidateContractCoverage(mode, playerRoster);
        }

        public OwnerContractRenewalPreview PreviewRenewal(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int seasons)
        {
            EnsureInitialized(runtime);
            OwnerPlayerContractState contract = runtime.ManagerMode.GetPlayerContract(cardId);
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new ArgumentException("WorldCardCatalog에 없는 계약 선수입니다.", nameof(cardId));
            return _resolver.PreviewRenewal(
                contract,
                card,
                runtime.WorldCardCatalog.GetPlayerSeason(card),
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                seasons,
                runtime.Economy.Money);
        }

        public OwnerContractRenewalPreview Renew(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int seasons)
        {
            OwnerContractRenewalPreview preview = PreviewRenewal(runtime, cardId, seasons);
            if (!preview.CanCommit) return preview;
            if (preview.SigningCost > 0L && !runtime.Economy.TrySpendMoney(preview.SigningCost))
                throw new InvalidOperationException("검증된 선수 계약금을 반영할 수 없습니다.");
            runtime.ManagerMode.RenewPlayerContract(
                preview.CardId,
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                preview.Seasons,
                preview.AnnualSalary);
            return preview;
        }

        public OwnerTradePreview PreviewTrade(
            ManagerHistoricalRuntimeState runtime,
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId)
        {
            EnsureInitialized(runtime);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            CurrentRosterState partnerRoster = FindPartnerRoster(runtime, partnerTeamSeasonKey);
            if (partnerRoster == null)
            {
                return new OwnerTradePreview(
                    OwnerPlayerMarketStatus.InvalidSelection,
                    partnerTeamSeasonKey,
                    outgoingCardId,
                    incomingCardId,
                    0,
                    0,
                    playerRoster,
                    null,
                    "같은 리그의 상대 구단을 선택해야 합니다.");
            }
            if (mode.CountTrades(mode.LiveSeason.SeasonNumber) >= _balance.OwnerPlayerMarket.MaximumTradesPerSeason)
            {
                return new OwnerTradePreview(
                    OwnerPlayerMarketStatus.SeasonalLimitReached,
                    partnerTeamSeasonKey,
                    outgoingCardId,
                    incomingCardId,
                    0,
                    0,
                    playerRoster,
                    partnerRoster,
                    "이번 시즌 트레이드 횟수를 모두 사용했습니다.");
            }
            int enhancement = runtime.TryGetOwnedCard(outgoingCardId, out OwnedPlayerCardState owned)
                ? owned.EnhancementLevel
                : 0;
            return _resolver.PreviewTrade(
                playerRoster,
                partnerRoster,
                outgoingCardId,
                incomingCardId,
                runtime.WorldCardCatalog,
                enhancement);
        }

        public OwnerTradePreview CommitTrade(
            ManagerHistoricalRuntimeState runtime,
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId)
        {
            OwnerTradePreview preview = PreviewTrade(
                runtime,
                partnerTeamSeasonKey,
                outgoingCardId,
                incomingCardId);
            if (!preview.CanCommit) return preview;

            ManagerModeRuntimeState mode = runtime.ManagerMode;
            if (!runtime.WorldCardCatalog.TryGetCard(preview.IncomingCardId, out PlayerCardDefinition incomingCard))
                throw new InvalidOperationException("교환 대상 카드 원본이 없습니다.");
            PlayerSeasonDefinition incomingSeason = runtime.WorldCardCatalog.GetPlayerSeason(incomingCard);
            int contractSeasons = Math.Min(2, _balance.OwnerPlayerMarket.MaximumContractSeasons);
            var contract = new OwnerPlayerContractState(
                $"player-contract:{incomingCard.CardId}:{mode.LiveSeason.SeasonNumber:D4}",
                incomingCard.CardId,
                mode.LiveSeason.SeasonNumber,
                contractSeasons,
                _balance.OwnerPlayerMarket.GetAnnualSalary(incomingSeason.Cost, incomingCard.Edition, contractSeasons));
            int sequence = mode.CountTrades(mode.LiveSeason.SeasonNumber) + 1;
            var receipt = new OwnerTradeReceipt(
                $"trade:{mode.LiveSeason.SeasonNumber:D4}:{sequence:D2}:{preview.OutgoingCardId}:{preview.IncomingCardId}",
                mode.LiveSeason.SeasonNumber,
                preview.PartnerTeamSeasonKey,
                preview.OutgoingCardId,
                preview.IncomingCardId,
                preview.OutgoingValue,
                preview.IncomingValue);
            runtime.ApplyRosterTrade(preview.PlayerRoster, preview.PartnerRoster, contract, receipt);
            return preview;
        }

        private static ManagerModeRuntimeState RequireMode(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("구단주 확장 상태가 없습니다.");
            return runtime.ManagerMode;
        }

        private static void ValidateContractCoverage(
            ManagerModeRuntimeState mode,
            CurrentRosterState playerRoster)
        {
            if (mode.PlayerContracts.Count != playerRoster.Entries.Count)
                throw new InvalidOperationException("현재 25인 로스터와 선수 계약 수가 일치하지 않습니다.");

            for (int rosterIndex = 0; rosterIndex < playerRoster.Entries.Count; rosterIndex++)
            {
                string cardId = playerRoster.Entries[rosterIndex].CardId;
                bool hasContract = false;
                for (int contractIndex = 0; contractIndex < mode.PlayerContracts.Count; contractIndex++)
                {
                    if (!string.Equals(mode.PlayerContracts[contractIndex].CardId, cardId, StringComparison.Ordinal))
                        continue;
                    hasContract = true;
                    break;
                }
                if (!hasContract)
                    throw new InvalidOperationException($"현재 로스터 CardId {cardId}의 선수 계약이 없습니다.");
            }
        }

        private static CurrentRosterState FindPartnerRoster(
            ManagerHistoricalRuntimeState runtime,
            string partnerTeamSeasonKey)
        {
            if (string.IsNullOrWhiteSpace(partnerTeamSeasonKey) ||
                string.Equals(partnerTeamSeasonKey.Trim(), runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                return null;

            string key = partnerTeamSeasonKey.Trim();
            for (int index = 0; index < runtime.Rosters.Count; index++)
            {
                CurrentRosterState roster = runtime.Rosters[index];
                if (string.Equals(roster.TeamSeasonKey, key, StringComparison.Ordinal))
                    return roster;
            }
            return null;
        }
    }
}

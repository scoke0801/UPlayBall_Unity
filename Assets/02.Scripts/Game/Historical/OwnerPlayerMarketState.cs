using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 선수 계약·트레이드의 저장 상태와 Aggregate 교체 연산을 확장한다.</summary>
    public sealed partial class ManagerModeRuntimeState
    {
        private readonly List<OwnerPlayerContractState> _playerContracts = new List<OwnerPlayerContractState>();
        private readonly List<OwnerTradeReceipt> _tradeReceipts = new List<OwnerTradeReceipt>();

        public IReadOnlyList<OwnerPlayerContractState> PlayerContracts => _playerContracts;
        public IReadOnlyList<OwnerTradeReceipt> TradeReceipts => _tradeReceipts;

        public void ReplacePlayerMarketState(
            IReadOnlyList<OwnerPlayerContractState> contracts,
            IReadOnlyList<OwnerTradeReceipt> receipts)
        {
            if (contracts == null || receipts == null) throw new ArgumentNullException(nameof(contracts));
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            var validatedContracts = new List<OwnerPlayerContractState>(contracts.Count);
            for (int index = 0; index < contracts.Count; index++)
            {
                OwnerPlayerContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 선수 계약이 있습니다.", nameof(contracts));
                if (!contractIds.Add(contract.ContractId) || !cardIds.Add(contract.CardId))
                    throw new ArgumentException("선수 계약 ID와 CardId는 중복될 수 없습니다.", nameof(contracts));
                validatedContracts.Add(contract);
            }
            var receiptIds = new HashSet<string>(StringComparer.Ordinal);
            var validatedReceipts = new List<OwnerTradeReceipt>(receipts.Count);
            for (int index = 0; index < receipts.Count; index++)
            {
                OwnerTradeReceipt receipt = receipts[index]
                    ?? throw new ArgumentException("null 트레이드 영수증이 있습니다.", nameof(receipts));
                if (!receiptIds.Add(receipt.ReceiptId))
                    throw new ArgumentException("트레이드 ReceiptId는 중복될 수 없습니다.", nameof(receipts));
                validatedReceipts.Add(receipt);
            }
            validatedContracts.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            validatedReceipts.Sort((left, right) => string.CompareOrdinal(left.ReceiptId, right.ReceiptId));
            _playerContracts.Clear();
            _playerContracts.AddRange(validatedContracts);
            _tradeReceipts.Clear();
            _tradeReceipts.AddRange(validatedReceipts);
        }

        public OwnerPlayerContractState GetPlayerContract(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            for (int index = 0; index < _playerContracts.Count; index++)
                if (string.Equals(_playerContracts[index].CardId, cardId.Trim(), StringComparison.Ordinal))
                    return _playerContracts[index];
            throw new KeyNotFoundException($"CardId {cardId}의 선수 계약이 없습니다.");
        }

        public int CountTrades(int season)
        {
            int count = 0;
            for (int index = 0; index < _tradeReceipts.Count; index++)
                if (_tradeReceipts[index].Season == season) count++;
            return count;
        }

        public long GetAnnualPlayerSalaryTotal()
        {
            long total = 0L;
            for (int index = 0; index < _playerContracts.Count; index++)
                total = checked(total + _playerContracts[index].AnnualSalary);
            return total;
        }

        public bool HasExpiringPlayerContracts()
        {
            for (int index = 0; index < _playerContracts.Count; index++)
                if (_playerContracts[index].IsExpiring) return true;
            return false;
        }

        public void RenewPlayerContract(string cardId, int season, int seasons, long annualSalary)
        {
            OwnerPlayerContractState contract = GetPlayerContract(cardId);
            contract.Renew(season, checked(contract.RemainingSeasons + seasons), annualSalary);
        }

        public void SettleAndAdvancePlayerContracts(int completedSeason)
        {
            for (int index = 0; index < _playerContracts.Count; index++)
                if (!_playerContracts[index].TrySettleAndAdvance(completedSeason))
                    throw new InvalidOperationException("이미 마감한 선수 연봉을 다시 반영할 수 없습니다.");
        }

        internal void ApplyPlayerTrade(
            ActiveRosterEntry outgoing,
            ActiveRosterEntry incoming,
            string playerTeamSeasonKey,
            string partnerTeamSeasonKey,
            OwnerPlayerContractState incomingContract,
            OwnerTradeReceipt receipt)
        {
            if (outgoing == null || incoming == null || incomingContract == null || receipt == null)
                throw new ArgumentNullException(nameof(outgoing));
            ReplacePlayerStatus(playerTeamSeasonKey, partnerTeamSeasonKey, outgoing.PlayerPersonId, incoming.PlayerPersonId);
            ReplaceLineupCardId(outgoing.CardId, incoming.CardId);

            for (int index = _playerContracts.Count - 1; index >= 0; index--)
                if (string.Equals(_playerContracts[index].CardId, outgoing.CardId, StringComparison.Ordinal))
                    _playerContracts.RemoveAt(index);
            _playerContracts.Add(incomingContract);
            _playerContracts.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            _tradeReceipts.Add(receipt);
            _tradeReceipts.Sort((left, right) => string.CompareOrdinal(left.ReceiptId, right.ReceiptId));
        }

        private void ReplacePlayerStatus(
            string playerTeamSeasonKey,
            string partnerTeamSeasonKey,
            string outgoingPersonId,
            string incomingPersonId)
        {
            int playerIndex = FindStatusIndex(playerTeamSeasonKey);
            int partnerIndex = FindStatusIndex(partnerTeamSeasonKey);
            TeamSeasonPlayerStatusState player = _playerStatuses[playerIndex];
            TeamSeasonPlayerStatusState partner = _playerStatuses[partnerIndex];
            TeamSeasonPlayerStatus outgoing = player.GetRequiredPlayer(outgoingPersonId);
            TeamSeasonPlayerStatus incoming = partner.GetRequiredPlayer(incomingPersonId);
            _playerStatuses[playerIndex] = ReplaceStatus(player, outgoingPersonId, incoming);
            _playerStatuses[partnerIndex] = ReplaceStatus(partner, incomingPersonId, outgoing);
        }

        private int FindStatusIndex(string teamSeasonKey)
        {
            for (int index = 0; index < _playerStatuses.Length; index++)
                if (string.Equals(_playerStatuses[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return index;
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 선수 상태가 없습니다.");
        }

        private static TeamSeasonPlayerStatusState ReplaceStatus(
            TeamSeasonPlayerStatusState source,
            string removedPersonId,
            TeamSeasonPlayerStatus added)
        {
            var players = new TeamSeasonPlayerStatus[source.Players.Count];
            for (int index = 0; index < players.Length; index++)
            {
                TeamSeasonPlayerStatus current = source.Players[index];
                players[index] = string.Equals(current.PlayerPersonId, removedPersonId, StringComparison.Ordinal)
                    ? added
                    : current;
            }
            return new TeamSeasonPlayerStatusState(source.TeamSeasonKey, players);
        }

        private void ReplaceLineupCardId(string outgoingCardId, string incomingCardId)
        {
            for (int presetIndex = 0; presetIndex < _lineupPresets.Count; presetIndex++)
            {
                LineupPresetState source = _lineupPresets[presetIndex];
                var slots = new LineupPresetSlot[source.StartingLineupSlots.Count];
                for (int index = 0; index < slots.Length; index++)
                {
                    LineupPresetSlot slot = source.StartingLineupSlots[index];
                    slots[index] = new LineupPresetSlot(ReplaceId(slot.CardId, outgoingCardId, incomingCardId), slot.Position);
                }
                _lineupPresets[presetIndex] = new LineupPresetState(
                    source.PresetId,
                    source.Name,
                    slots,
                    ReplaceIds(source.BattingOrderCardIds, outgoingCardId, incomingCardId),
                    ReplaceIds(source.BenchPriorityCardIds, outgoingCardId, incomingCardId),
                    ReplaceIds(source.StarterRotationCardIds, outgoingCardId, incomingCardId),
                    ReplaceIds(source.BullpenAssignmentCardIds, outgoingCardId, incomingCardId),
                    ReplaceId(source.SetupPitcherCardId, outgoingCardId, incomingCardId),
                    ReplaceId(source.CloserPitcherCardId, outgoingCardId, incomingCardId),
                    source.TeamColorIds,
                    source.DefaultTacticCardIds);
            }
        }

        private static string[] ReplaceIds(IReadOnlyList<string> source, string oldId, string newId)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = ReplaceId(source[index], oldId, newId);
            return result;
        }

        private static string ReplaceId(string value, string oldId, string newId) =>
            string.Equals(value, oldId, StringComparison.Ordinal) ? newId : value;
    }

    /// <summary>검증된 1:1 트레이드가 두 구단 로스터를 한 번에 교체하도록 Aggregate를 확장한다.</summary>
    public sealed partial class ManagerHistoricalRuntimeState
    {
        internal void ApplyRosterTrade(
            CurrentRosterState playerRoster,
            CurrentRosterState partnerRoster,
            OwnerPlayerContractState incomingContract,
            OwnerTradeReceipt receipt)
        {
            if (playerRoster == null || partnerRoster == null)
                throw new ArgumentNullException(nameof(playerRoster));
            int playerIndex = FindRosterIndex(playerRoster.TeamSeasonKey);
            int partnerIndex = FindRosterIndex(partnerRoster.TeamSeasonKey);
            ActiveRosterEntry outgoing = FindChangedEntry(_rosters[playerIndex], playerRoster);
            ActiveRosterEntry incoming = FindChangedEntry(_rosters[partnerIndex], partnerRoster);

            if (!TryGetOwnedCard(incoming.CardId, out _))
                AcquireCard(incoming.CardId);
            ManagerMode.ApplyPlayerTrade(
                outgoing,
                incoming,
                playerRoster.TeamSeasonKey,
                partnerRoster.TeamSeasonKey,
                incomingContract,
                receipt);
            _rosters[playerIndex] = playerRoster;
            _rosters[partnerIndex] = partnerRoster;
            _rostersByTeamSeasonKey[playerRoster.TeamSeasonKey] = playerRoster;
            _rostersByTeamSeasonKey[partnerRoster.TeamSeasonKey] = partnerRoster;
        }

        private int FindRosterIndex(string teamSeasonKey)
        {
            for (int index = 0; index < _rosters.Length; index++)
                if (string.Equals(_rosters[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return index;
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 로스터가 없습니다.");
        }

        private static ActiveRosterEntry FindChangedEntry(CurrentRosterState before, CurrentRosterState after)
        {
            for (int index = 0; index < before.Entries.Count; index++)
            {
                ActiveRosterEntry previous = before.Entries[index];
                bool remains = false;
                for (int nextIndex = 0; nextIndex < after.Entries.Count; nextIndex++)
                    if (string.Equals(previous.CardId, after.Entries[nextIndex].CardId, StringComparison.Ordinal))
                    {
                        remains = true;
                        break;
                    }
                if (!remains) return previous;
            }
            throw new InvalidOperationException("교체 전후 로스터에서 변경된 선수를 찾지 못했습니다.");
        }
    }
}

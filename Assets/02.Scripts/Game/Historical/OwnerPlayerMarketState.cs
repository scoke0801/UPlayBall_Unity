using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 선수 계약의 저장 상태를 확장한다.</summary>
    public sealed partial class ManagerModeRuntimeState
    {
        private readonly List<OwnerPlayerContractState> _playerContracts = new List<OwnerPlayerContractState>();

        public IReadOnlyList<OwnerPlayerContractState> PlayerContracts => _playerContracts;

        public void ReplacePlayerContractState(IReadOnlyList<OwnerPlayerContractState> contracts)
        {
            if (contracts == null) throw new ArgumentNullException(nameof(contracts));
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
            validatedContracts.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            _playerContracts.Clear();
            _playerContracts.AddRange(validatedContracts);
        }

        public OwnerPlayerContractState GetPlayerContract(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            for (int index = 0; index < _playerContracts.Count; index++)
                if (string.Equals(_playerContracts[index].CardId, cardId.Trim(), StringComparison.Ordinal))
                    return _playerContracts[index];
            throw new KeyNotFoundException($"CardId {cardId}의 선수 계약이 없습니다.");
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

    }
}

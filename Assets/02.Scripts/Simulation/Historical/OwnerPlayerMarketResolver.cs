using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Simulation.Historical
{
    public enum OwnerPlayerMarketStatus
    {
        Available,
        InvalidSelection,
        InsufficientMoney,
        ContractExpired
    }

    /// <summary>선수 계약 갱신을 실행하기 전에 비용과 차단 근거를 고정한다.</summary>
    public sealed class OwnerContractRenewalPreview
    {
        public OwnerContractRenewalPreview(
            OwnerPlayerMarketStatus status,
            string cardId,
            int seasons,
            long annualSalary,
            long signingCost,
            string reason)
        {
            Status = status;
            CardId = cardId ?? string.Empty;
            Seasons = seasons;
            AnnualSalary = annualSalary;
            SigningCost = signingCost;
            Reason = reason ?? string.Empty;
        }

        public OwnerPlayerMarketStatus Status { get; }
        public string CardId { get; }
        public int Seasons { get; }
        public long AnnualSalary { get; }
        public long SigningCost { get; }
        public string Reason { get; }
        public bool CanCommit => Status == OwnerPlayerMarketStatus.Available;
    }

    /// <summary>구단주 모드 선수 계약 비용을 순수 C#로 계산한다.</summary>
    public sealed class OwnerPlayerMarketResolver
    {
        private readonly OwnerPlayerMarketBalanceTable _balance;

        public OwnerPlayerMarketResolver(OwnerPlayerMarketBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public OwnerPlayerContractState[] CreateInitialContracts(
            CurrentRosterState roster,
            WorldCardCatalog catalog,
            int season)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var result = new OwnerPlayerContractState[roster.Entries.Count];
            for (int index = 0; index < result.Length; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                result[index] = CreateInitialContract(entry.CardId, catalog, season);
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            return result;
        }

        /// <summary>1군 등록 변경 뒤 기존 선수 계약은 보존하고 새 등록 선수만 1년 계약으로 채운다.</summary>
        public OwnerPlayerContractState[] CreateActiveRosterContracts(
            CurrentRosterState roster,
            WorldCardCatalog catalog,
            int season,
            IReadOnlyList<OwnerPlayerContractState> existingContracts)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (existingContracts == null) throw new ArgumentNullException(nameof(existingContracts));

            var result = new OwnerPlayerContractState[roster.Entries.Count];
            for (int rosterIndex = 0; rosterIndex < result.Length; rosterIndex++)
            {
                string cardId = roster.Entries[rosterIndex].CardId;
                OwnerPlayerContractState existing = null;
                for (int contractIndex = 0; contractIndex < existingContracts.Count; contractIndex++)
                {
                    OwnerPlayerContractState candidate = existingContracts[contractIndex] ??
                        throw new ArgumentException("null 선수 계약이 있습니다.", nameof(existingContracts));
                    if (string.Equals(candidate.CardId, cardId, StringComparison.Ordinal))
                    {
                        existing = candidate;
                        break;
                    }
                    // 연도·Edition이 달라도 동일 인물의 계약은 카드 교체로 초기화하지 않는다.
                    if (!catalog.TryGetCard(candidate.CardId, out PlayerCardDefinition previousCard) ||
                        !catalog.TryGetCard(cardId, out PlayerCardDefinition nextCard) ||
                        !string.Equals(catalog.GetPlayerSeason(previousCard).PlayerPersonId,
                            catalog.GetPlayerSeason(nextCard).PlayerPersonId, StringComparison.Ordinal)) continue;
                    existing = new OwnerPlayerContractState(candidate.ContractId, cardId,
                        candidate.StartSeason, candidate.RemainingSeasons, candidate.AnnualSalary,
                        candidate.LastSalaryPaidSeason);
                    break;
                }
                result[rosterIndex] = existing ?? CreateRosterRegistrationContract(cardId, catalog, season);
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            return result;
        }

        private OwnerPlayerContractState CreateInitialContract(
            string cardId,
            WorldCardCatalog catalog,
            int season)
        {
            PlayerCardDefinition card = GetCard(catalog, cardId);
            PlayerSeasonDefinition playerSeason = catalog.GetPlayerSeason(card);
            // 첫 시즌은 기존 로스터를 평가할 시간으로 보장하고, 이후 만료 시점만 결정론적으로 분산한다.
            int years = _balance.MaximumContractSeasons == 1
                ? 1
                : 2 + PositiveStableHash(cardId) % (_balance.MaximumContractSeasons - 1);
            long salary = _balance.GetAnnualSalary(playerSeason.Cost, card.Edition, years);
            return new OwnerPlayerContractState(
                $"player-contract:{cardId}:{season:D4}",
                cardId,
                season,
                years,
                salary);
        }

        private OwnerPlayerContractState CreateRosterRegistrationContract(
            string cardId,
            WorldCardCatalog catalog,
            int season)
        {
            PlayerCardDefinition card = GetCard(catalog, cardId);
            PlayerSeasonDefinition playerSeason = catalog.GetPlayerSeason(card);
            const int registrationContractSeasons = 1;
            return new OwnerPlayerContractState(
                $"player-contract:{cardId}:{season:D4}",
                cardId,
                season,
                registrationContractSeasons,
                _balance.GetAnnualSalary(playerSeason.Cost, card.Edition, registrationContractSeasons));
        }

        public OwnerContractRenewalPreview PreviewRenewal(
            OwnerPlayerContractState contract,
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            int currentSeason,
            int contractSeasons,
            long availableMoney)
        {
            if (contract == null || card == null || season == null ||
                !string.Equals(contract.CardId, card.CardId, StringComparison.Ordinal))
                return new OwnerContractRenewalPreview(OwnerPlayerMarketStatus.InvalidSelection, string.Empty, 0, 0L, 0L, "계약 선수를 확인할 수 없습니다.");
            if (currentSeason < contract.StartSeason)
                return new OwnerContractRenewalPreview(OwnerPlayerMarketStatus.InvalidSelection, card.CardId, contractSeasons, 0L, 0L, "아직 시작하지 않은 계약입니다.");
            if (contract.RemainingSeasons <= 0)
                return new OwnerContractRenewalPreview(OwnerPlayerMarketStatus.ContractExpired, card.CardId, contractSeasons, 0L, 0L, "이미 만료된 계약입니다.");
            if (contractSeasons < 1 || contractSeasons > _balance.MaximumContractSeasons)
                return new OwnerContractRenewalPreview(OwnerPlayerMarketStatus.InvalidSelection, card.CardId, contractSeasons, 0L, 0L, "계약 기간은 1~3년이어야 합니다.");

            long annualSalary = _balance.GetAnnualSalary(season.Cost, card.Edition, contractSeasons);
            long signingCost = (long)Math.Round(
                annualSalary * contractSeasons * _balance.RenewalSigningCostRate,
                MidpointRounding.AwayFromZero);
            if (availableMoney < signingCost)
            {
                return new OwnerContractRenewalPreview(
                    OwnerPlayerMarketStatus.InsufficientMoney,
                    card.CardId,
                    contractSeasons,
                    annualSalary,
                    signingCost,
                    "계약금이 부족합니다.");
            }
            return new OwnerContractRenewalPreview(
                OwnerPlayerMarketStatus.Available,
                card.CardId,
                contractSeasons,
                annualSalary,
                signingCost,
                $"{contractSeasons}년 연장하여 잔여 {contract.RemainingSeasons + contractSeasons}년이 됩니다. 연봉은 즉시 변경됩니다.");
        }

        private static PlayerCardDefinition GetCard(WorldCardCatalog catalog, string cardId)
        {
            if (catalog.TryGetCard(cardId, out PlayerCardDefinition card)) return card;
            throw new ArgumentException($"CardId {cardId}를 찾을 수 없습니다.", nameof(cardId));
        }

        private static int PositiveStableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                    hash = (hash ^ value[index]) * 16777619u;
                return (int)(hash & 0x7fffffffu);
            }
        }
    }
}

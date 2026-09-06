using System;
using Baseball.Core.Historical;

namespace Baseball.Simulation.Historical
{
    public enum OwnerPlayerMarketStatus
    {
        Available,
        InvalidSelection,
        RosterViolation,
        InsufficientValue,
        SeasonalLimitReached,
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

    /// <summary>두 구단의 25인 로스터를 바꾸지 않은 채 1:1 트레이드 결과를 미리 계산한다.</summary>
    public sealed class OwnerTradePreview
    {
        public OwnerTradePreview(
            OwnerPlayerMarketStatus status,
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId,
            int outgoingValue,
            int incomingValue,
            CurrentRosterState playerRoster,
            CurrentRosterState partnerRoster,
            string reason)
        {
            Status = status;
            PartnerTeamSeasonKey = partnerTeamSeasonKey ?? string.Empty;
            OutgoingCardId = outgoingCardId ?? string.Empty;
            IncomingCardId = incomingCardId ?? string.Empty;
            OutgoingValue = outgoingValue;
            IncomingValue = incomingValue;
            PlayerRoster = playerRoster;
            PartnerRoster = partnerRoster;
            Reason = reason ?? string.Empty;
        }

        public OwnerPlayerMarketStatus Status { get; }
        public string PartnerTeamSeasonKey { get; }
        public string OutgoingCardId { get; }
        public string IncomingCardId { get; }
        public int OutgoingValue { get; }
        public int IncomingValue { get; }
        public int ValueDifference => OutgoingValue - IncomingValue;
        public CurrentRosterState PlayerRoster { get; }
        public CurrentRosterState PartnerRoster { get; }
        public string Reason { get; }
        public bool CanCommit => Status == OwnerPlayerMarketStatus.Available;
    }

    /// <summary>구단주 모드 계약 비용과 AI 트레이드 수락을 순수 C#로 계산한다.</summary>
    public sealed class OwnerPlayerMarketResolver
    {
        private readonly OwnerPlayerMarketBalanceTable _balance;
        private readonly ActiveRosterValidator _rosterValidator = new ActiveRosterValidator();

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
                PlayerCardDefinition card = GetCard(catalog, entry.CardId);
                PlayerSeasonDefinition playerSeason = catalog.GetPlayerSeason(card);
                // 첫 시즌은 기존 로스터를 평가할 시간으로 보장하고, 이후 만료 시점만 결정론적으로 분산한다.
                int years = _balance.MaximumContractSeasons == 1
                    ? 1
                    : 2 + PositiveStableHash(entry.CardId) % (_balance.MaximumContractSeasons - 1);
                long salary = _balance.GetAnnualSalary(playerSeason.Cost, card.Edition, years);
                result[index] = new OwnerPlayerContractState(
                    $"player-contract:{entry.CardId}:{season:D4}",
                    entry.CardId,
                    season,
                    years,
                    salary);
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            return result;
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
                $"{contractSeasons}년 동안 연봉과 계약금을 구단 재정에 반영합니다.");
        }

        public OwnerTradePreview PreviewTrade(
            CurrentRosterState playerRoster,
            CurrentRosterState partnerRoster,
            string outgoingCardId,
            string incomingCardId,
            WorldCardCatalog catalog,
            int outgoingEnhancementLevel = 0)
        {
            if (playerRoster == null || partnerRoster == null || catalog == null)
                throw new ArgumentNullException(nameof(playerRoster));
            ActiveRosterEntry outgoing = Find(playerRoster, outgoingCardId);
            ActiveRosterEntry incoming = Find(partnerRoster, incomingCardId);
            if (outgoing == null || incoming == null)
                return Invalid(playerRoster, partnerRoster, outgoingCardId, incomingCardId, "양 구단의 1군 선수를 각각 선택해야 합니다.");

            bool outgoingIsHitter = ActiveRosterCompositionRule.Standard.IsHitterRole(outgoing.Role);
            bool incomingIsHitter = ActiveRosterCompositionRule.Standard.IsHitterRole(incoming.Role);
            if (outgoingIsHitter != incomingIsHitter)
                return Invalid(playerRoster, partnerRoster, outgoingCardId, incomingCardId, "야수와 투수는 서로 교환할 수 없습니다.");

            CurrentRosterState proposedPlayer = Replace(playerRoster, outgoing, incoming);
            CurrentRosterState proposedPartner = Replace(partnerRoster, incoming, outgoing);
            RosterValidationResult playerValidation = _rosterValidator.Validate(proposedPlayer);
            RosterValidationResult partnerValidation = _rosterValidator.Validate(proposedPartner);
            int outgoingValue = ResolveValue(catalog, outgoing.CardId, outgoingEnhancementLevel);
            int incomingValue = ResolveValue(catalog, incoming.CardId, 0);
            if (!playerValidation.IsValid || !partnerValidation.IsValid)
            {
                return new OwnerTradePreview(
                    OwnerPlayerMarketStatus.RosterViolation,
                    partnerRoster.TeamSeasonKey,
                    outgoing.CardId,
                    incoming.CardId,
                    outgoingValue,
                    incomingValue,
                    proposedPlayer,
                    proposedPartner,
                    "25인 구성·외국인 제한·중복 선수 규칙을 통과하지 못했습니다.");
            }
            if (outgoingValue < incomingValue * _balance.MinimumTradeAcceptanceRatio)
            {
                return new OwnerTradePreview(
                    OwnerPlayerMarketStatus.InsufficientValue,
                    partnerRoster.TeamSeasonKey,
                    outgoing.CardId,
                    incoming.CardId,
                    outgoingValue,
                    incomingValue,
                    proposedPlayer,
                    proposedPartner,
                    "상대 구단이 받는 가치가 부족합니다.");
            }
            return new OwnerTradePreview(
                OwnerPlayerMarketStatus.Available,
                partnerRoster.TeamSeasonKey,
                outgoing.CardId,
                incoming.CardId,
                outgoingValue,
                incomingValue,
                proposedPlayer,
                proposedPartner,
                "양 구단의 전력 가치와 로스터 규칙을 통과했습니다.");
        }

        public int ResolveValue(WorldCardCatalog catalog, string cardId, int enhancementLevel)
        {
            PlayerCardDefinition card = GetCard(catalog, cardId);
            PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
            return checked(season.Cost * 100 + (int)card.Edition * 35 + Math.Max(0, enhancementLevel) * 12);
        }

        private static CurrentRosterState Replace(
            CurrentRosterState source,
            ActiveRosterEntry removed,
            ActiveRosterEntry added)
        {
            var entries = new ActiveRosterEntry[source.Entries.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                ActiveRosterEntry entry = source.Entries[index];
                entries[index] = ReferenceEquals(entry, removed)
                    ? new ActiveRosterEntry(
                        added.CardId,
                        added.PlayerSeasonId,
                        added.PlayerPersonId,
                        added.RegistrationType,
                        removed.Role)
                    : entry;
            }
            return new CurrentRosterState(source.TeamSeasonKey, entries);
        }

        private static ActiveRosterEntry Find(CurrentRosterState roster, string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId.Trim(), StringComparison.Ordinal))
                    return roster.Entries[index];
            return null;
        }

        private static PlayerCardDefinition GetCard(WorldCardCatalog catalog, string cardId)
        {
            if (catalog.TryGetCard(cardId, out PlayerCardDefinition card)) return card;
            throw new ArgumentException($"CardId {cardId}를 찾을 수 없습니다.", nameof(cardId));
        }

        private static OwnerTradePreview Invalid(
            CurrentRosterState playerRoster,
            CurrentRosterState partnerRoster,
            string outgoingCardId,
            string incomingCardId,
            string reason) => new OwnerTradePreview(
                OwnerPlayerMarketStatus.InvalidSelection,
                partnerRoster?.TeamSeasonKey,
                outgoingCardId,
                incomingCardId,
                0,
                0,
                playerRoster,
                partnerRoster,
                reason);

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

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>구단주 새 게임의 메인 카드 제한과 결정론적 15인 보충 로스터를 확정한다.</summary>
    public sealed class OwnerStarterRosterResolver
    {
        private readonly OwnerStarterRosterRule _rule;

        public OwnerStarterRosterResolver(OwnerStarterRosterRule rule)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        public OwnerMainCardSelectionStatus ValidateMainCards(
            string franchiseId,
            IReadOnlyList<string> cardIds,
            WorldCardCatalog catalog)
        {
            return ValidateMainCards(franchiseId, cardIds, catalog, requireCompleteSelection: true);
        }

        /// <summary>선택 중인 카드가 이후 선택으로 복구할 수 없는 제한을 위반했는지 검사한다.</summary>
        public OwnerMainCardSelectionStatus ValidatePartialMainCards(
            string franchiseId,
            IReadOnlyList<string> cardIds,
            WorldCardCatalog catalog)
        {
            return ValidateMainCards(franchiseId, cardIds, catalog, requireCompleteSelection: false);
        }

        private OwnerMainCardSelectionStatus ValidateMainCards(
            string franchiseId,
            IReadOnlyList<string> cardIds,
            WorldCardCatalog catalog,
            bool requireCompleteSelection)
        {
            if (string.IsNullOrWhiteSpace(franchiseId))
                throw new ArgumentException("FranchiseId가 필요합니다.", nameof(franchiseId));
            if (cardIds == null)
                throw new ArgumentNullException(nameof(cardIds));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            int hitters = 0;
            int pitchers = 0;
            int totalCost = 0;
            int eliteCount = 0;
            int premiumCount = 0;
            int foreignCount = 0;
            var cardIdSet = new HashSet<string>(StringComparer.Ordinal);
            var personIdSet = new HashSet<string>(StringComparer.Ordinal);
            var premiumCountByYear = new Dictionary<int, int>();

            for (int index = 0; index < cardIds.Count; index++)
            {
                string cardId = cardIds[index];
                if (string.IsNullOrWhiteSpace(cardId) || !cardIdSet.Add(cardId.Trim()))
                    return Invalid("DUPLICATE_CARD", "같은 카드는 두 번 선택할 수 없습니다.", cardIds.Count, hitters, pitchers, totalCost);
                if (!catalog.TryGetCard(cardId, out PlayerCardDefinition card) || card.Edition != PlayerCardEdition.Normal)
                    return Invalid("INVALID_CARD", "Normal 선수 카드만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);

                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (!string.Equals(season.OriginFranchiseId, franchiseId.Trim(), StringComparison.Ordinal))
                    return Invalid("WRONG_FRANCHISE", "선택한 구단 소속 이력의 카드만 고를 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
                if (!personIdSet.Add(season.PlayerPersonId))
                    return Invalid("DUPLICATE_PERSON", "같은 선수의 다른 연도 카드는 함께 선택할 수 없습니다.", cardIds.Count, hitters, pitchers, totalCost);

                if (season.PlayerType == PlayerType.Pitcher) pitchers++;
                else hitters++;
                if (season.RegistrationType == RegistrationType.Foreign)
                {
                    foreignCount++;
                    if (foreignCount > ActiveRosterCompositionRule.MaxForeignPlayers)
                        return Invalid("FOREIGN_LIMIT", "외국인 선수 카드는 세 장까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
                }
                totalCost += season.Cost;
                if (season.Cost >= _rule.EliteCostThreshold) eliteCount++;
                if (season.Cost >= _rule.PremiumCostThreshold)
                {
                    premiumCount++;
                    premiumCountByYear.TryGetValue(season.OriginYear, out int sameYearCount);
                    sameYearCount++;
                    premiumCountByYear[season.OriginYear] = sameYearCount;
                    if (sameYearCount > _rule.MaximumSameYearPremiumCards)
                        return Invalid("SAME_YEAR_PREMIUM_LIMIT", "같은 연도의 고Cost 핵심 카드는 두 장까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
                }
            }

            if (cardIds.Count > _rule.MainCardCount)
                return Invalid("CARD_COUNT", $"메인 선수 카드는 {_rule.MainCardCount}장까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
            if (hitters > _rule.MainHitterCount || pitchers > _rule.MainPitcherCount)
                return Invalid("PLAYER_TYPE_COUNT", $"타자 {_rule.MainHitterCount}명과 투수 {_rule.MainPitcherCount}명까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
            if (totalCost > _rule.MaximumMainCost)
                return Invalid("TOTAL_COST", $"메인 카드 Cost 합계는 {_rule.MaximumMainCost} 이하여야 합니다.", cardIds.Count, hitters, pitchers, totalCost);
            if (eliteCount > _rule.MaximumEliteCards)
                return Invalid("ELITE_LIMIT", $"Cost {_rule.EliteCostThreshold} 이상 카드는 {_rule.MaximumEliteCards}장까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);
            if (premiumCount > _rule.MaximumPremiumCards)
                return Invalid("PREMIUM_LIMIT", $"Cost {_rule.PremiumCostThreshold} 이상 카드는 {_rule.MaximumPremiumCards}장까지만 선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);

            if (!requireCompleteSelection)
                return new OwnerMainCardSelectionStatus(true, string.Empty, "선택할 수 있습니다.", cardIds.Count, hitters, pitchers, totalCost);

            if (cardIds.Count != _rule.MainCardCount)
                return Invalid("CARD_COUNT", $"메인 선수 카드는 정확히 {_rule.MainCardCount}장을 선택해야 합니다.", cardIds.Count, hitters, pitchers, totalCost);
            if (hitters != _rule.MainHitterCount || pitchers != _rule.MainPitcherCount)
                return Invalid("PLAYER_TYPE_COUNT", $"타자 {_rule.MainHitterCount}명과 투수 {_rule.MainPitcherCount}명이 필요합니다.", cardIds.Count, hitters, pitchers, totalCost);

            return new OwnerMainCardSelectionStatus(true, string.Empty, "선택 조건을 충족했습니다.", cardIds.Count, hitters, pitchers, totalCost);
        }

        public OwnerStarterRosterResult Resolve(
            TeamSeasonDefinition selectedTeam,
            IReadOnlyList<string> mainCardIds,
            WorldCardCatalog catalog,
            ulong starterRosterSeed,
            int rerollIndex)
        {
            if (selectedTeam == null)
                throw new ArgumentNullException(nameof(selectedTeam));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (rerollIndex < 0 || rerollIndex > _rule.MaximumFillerRerolls)
                throw new ArgumentOutOfRangeException(nameof(rerollIndex));

            OwnerMainCardSelectionStatus status = ValidateMainCards(selectedTeam.FranchiseId, mainCardIds, catalog);
            if (!status.IsValid)
                throw new InvalidOperationException(status.Message);

            ulong selectionFingerprint = CreateSelectionFingerprint(mainCardIds);
            ulong resultSeed = DeterministicSeed.Derive(
                starterRosterSeed ^ selectionFingerprint,
                checked((ulong)rerollIndex));
            var selectedPersons = new HashSet<string>(StringComparer.Ordinal);
            int selectedForeignCount = 0;
            for (int index = 0; index < mainCardIds.Count; index++)
            {
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(GetCard(catalog, mainCardIds[index]));
                selectedPersons.Add(season.PlayerPersonId);
                if (season.RegistrationType == RegistrationType.Foreign) selectedForeignCount++;
            }

            string[] fillerCardIds = null;
            for (int attempt = 0; attempt < _rule.DuplicateRetryCount; attempt++)
            {
                ulong attemptSeed = DeterministicSeed.Derive(resultSeed, checked((ulong)attempt));
                if (TrySelectFiller(catalog, selectedPersons, selectedForeignCount, attemptSeed, out fillerCardIds))
                    break;
            }
            if (fillerCardIds == null)
                throw new InvalidOperationException("현재 카드 카탈로그로 15인 저가치 보충 로스터를 구성할 수 없습니다.");

            CurrentRosterState roster = AssignRoles(selectedTeam.TeamSeasonKey, mainCardIds, fillerCardIds, catalog);
            RosterValidationResult validation = new ActiveRosterValidator().Validate(roster);
            if (!validation.IsValid)
                throw new InvalidOperationException("자동 배정한 스타터 로스터가 25인 구성 계약을 만족하지 않습니다.");
            return new OwnerStarterRosterResult(mainCardIds, fillerCardIds, roster, rerollIndex, resultSeed);
        }

        private bool TrySelectFiller(
            WorldCardCatalog catalog,
            HashSet<string> selectedPersons,
            int selectedForeignCount,
            ulong seed,
            out string[] result)
        {
            var random = new Pcg32Random(seed);
            var usedPersons = new HashSet<string>(selectedPersons, StringComparer.Ordinal);
            var selected = new List<string>(_rule.FillerCardCount);
            int remainingHitters = _rule.FillerHitterCount;
            int remainingSlots = _rule.FillerCardCount;
            int foreignCount = selectedForeignCount;

            for (int cost = _rule.FillerMinimumCost; cost <= _rule.FillerMaximumCost; cost++)
            {
                int countAtCost = _rule.GetFillerCount(cost);
                int remainingAfter = remainingSlots - countAtCost;
                int minimumHitters = Math.Max(0, remainingHitters - remainingAfter);
                int maximumHitters = Math.Min(countAtCost, remainingHitters);
                int desiredHitters = remainingSlots == 0
                    ? 0
                    : (int)Math.Round(countAtCost * remainingHitters / (double)remainingSlots, MidpointRounding.AwayFromZero);
                desiredHitters = Math.Max(minimumHitters, Math.Min(maximumHitters, desiredHitters));
                int desiredPitchers = countAtCost - desiredHitters;

                if (!TryPick(catalog, cost, PlayerType.Batter, desiredHitters, random, usedPersons, ref foreignCount, selected) ||
                    !TryPick(catalog, cost, PlayerType.Pitcher, desiredPitchers, random, usedPersons, ref foreignCount, selected))
                {
                    result = null;
                    return false;
                }
                remainingHitters -= desiredHitters;
                remainingSlots -= countAtCost;
            }

            result = selected.ToArray();
            return result.Length == _rule.FillerCardCount;
        }

        private static bool TryPick(
            WorldCardCatalog catalog,
            int cost,
            PlayerType playerType,
            int count,
            IRandomSource random,
            HashSet<string> usedPersons,
            ref int foreignCount,
            List<string> selected)
        {
            if (count == 0)
                return true;
            var candidates = new List<PlayerCardDefinition>();
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                if (card.Edition != PlayerCardEdition.Normal)
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (season.Cost == cost && season.PlayerType == playerType && !usedPersons.Contains(season.PlayerPersonId))
                    candidates.Add(card);
            }
            candidates.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            Shuffle(candidates, random);

            int added = 0;
            for (int index = 0; index < candidates.Count && added < count; index++)
            {
                PlayerCardDefinition card = candidates[index];
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (usedPersons.Contains(season.PlayerPersonId))
                    continue;
                if (season.RegistrationType == RegistrationType.Foreign &&
                    foreignCount >= ActiveRosterCompositionRule.MaxForeignPlayers)
                    continue;
                usedPersons.Add(season.PlayerPersonId);
                selected.Add(card.CardId);
                if (season.RegistrationType == RegistrationType.Foreign) foreignCount++;
                added++;
            }
            return added == count;
        }

        private static CurrentRosterState AssignRoles(
            string teamSeasonKey,
            IReadOnlyList<string> mainCardIds,
            IReadOnlyList<string> fillerCardIds,
            WorldCardCatalog catalog)
        {
            var hitters = new List<PlayerCardDefinition>(ActiveRosterCompositionRule.HitterCount);
            var pitchers = new List<PlayerCardDefinition>(ActiveRosterCompositionRule.PitcherCount);
            AddCards(mainCardIds, catalog, hitters, pitchers);
            AddCards(fillerCardIds, catalog, hitters, pitchers);
            if (hitters.Count != ActiveRosterCompositionRule.HitterCount ||
                pitchers.Count != ActiveRosterCompositionRule.PitcherCount)
                throw new InvalidOperationException("스타터 로스터의 타자·투수 구성이 ActiveRoster 계약과 다릅니다.");

            SortForAssignment(hitters, catalog);
            SortForAssignment(pitchers, catalog);
            var entries = new List<ActiveRosterEntry>(ActiveRosterCompositionRule.ActiveRosterSize);
            ActiveRosterRole[] hitterRoles =
            {
                ActiveRosterRole.StartingCatcher, ActiveRosterRole.StartingFirstBase,
                ActiveRosterRole.StartingSecondBase, ActiveRosterRole.StartingThirdBase,
                ActiveRosterRole.StartingShortstop, ActiveRosterRole.StartingLeftField,
                ActiveRosterRole.StartingCenterField, ActiveRosterRole.StartingRightField,
                ActiveRosterRole.StartingDesignatedHitter
            };
            for (int index = 0; index < hitterRoles.Length; index++)
            {
                PlayerPosition position = ActiveRosterCompositionRule.Standard.GetAssignedPosition(hitterRoles[index]);
                PlayerCardDefinition card = RemoveBestHitter(hitters, position, catalog);
                entries.Add(CreateEntry(card, hitterRoles[index], catalog));
            }
            while (hitters.Count > 0)
            {
                PlayerCardDefinition card = hitters[0];
                hitters.RemoveAt(0);
                entries.Add(CreateEntry(card, ActiveRosterRole.BenchHitter, catalog));
            }

            for (int index = 0; index < ActiveRosterCompositionRule.StartingPitcherCount; index++)
            {
                PlayerCardDefinition card = RemoveBestPitcher(pitchers, PitcherRole.Starter, catalog);
                entries.Add(CreateEntry(card, (ActiveRosterRole)((int)ActiveRosterRole.StartingPitcher1 + index), catalog));
            }
            PlayerCardDefinition closer = RemoveBestPitcher(pitchers, PitcherRole.Closer, catalog);
            PlayerCardDefinition setup = RemoveBestPitcher(pitchers, PitcherRole.Setup, catalog);
            for (int index = 0; index < ActiveRosterCompositionRule.BullpenPitcherCount; index++)
            {
                PlayerCardDefinition card = pitchers[0];
                pitchers.RemoveAt(0);
                entries.Add(CreateEntry(card, (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index), catalog));
            }
            entries.Add(CreateEntry(setup, ActiveRosterRole.Setup, catalog));
            entries.Add(CreateEntry(closer, ActiveRosterRole.Closer, catalog));
            return new CurrentRosterState(teamSeasonKey, entries);
        }

        private static void AddCards(
            IReadOnlyList<string> cardIds,
            WorldCardCatalog catalog,
            List<PlayerCardDefinition> hitters,
            List<PlayerCardDefinition> pitchers)
        {
            for (int index = 0; index < cardIds.Count; index++)
            {
                PlayerCardDefinition card = GetCard(catalog, cardIds[index]);
                if (catalog.GetPlayerSeason(card).PlayerType == PlayerType.Pitcher) pitchers.Add(card);
                else hitters.Add(card);
            }
        }

        private static void SortForAssignment(List<PlayerCardDefinition> cards, WorldCardCatalog catalog)
        {
            cards.Sort((left, right) =>
            {
                int cost = catalog.GetPlayerSeason(right).Cost.CompareTo(catalog.GetPlayerSeason(left).Cost);
                return cost != 0 ? cost : string.CompareOrdinal(left.CardId, right.CardId);
            });
        }

        private static PlayerCardDefinition RemoveBestHitter(
            List<PlayerCardDefinition> cards,
            PlayerPosition position,
            WorldCardCatalog catalog)
        {
            int selected = 0;
            for (int index = 0; index < cards.Count; index++)
            {
                if (catalog.GetPlayerSeason(cards[index]).Position == position)
                {
                    selected = index;
                    break;
                }
            }
            PlayerCardDefinition result = cards[selected];
            cards.RemoveAt(selected);
            return result;
        }

        private static PlayerCardDefinition RemoveBestPitcher(
            List<PlayerCardDefinition> cards,
            PitcherRole role,
            WorldCardCatalog catalog)
        {
            int selected = 0;
            for (int index = 0; index < cards.Count; index++)
            {
                if (catalog.GetPlayerSeason(cards[index]).PitcherRole == role)
                {
                    selected = index;
                    break;
                }
            }
            PlayerCardDefinition result = cards[selected];
            cards.RemoveAt(selected);
            return result;
        }

        private static ActiveRosterEntry CreateEntry(
            PlayerCardDefinition card,
            ActiveRosterRole role,
            WorldCardCatalog catalog)
        {
            PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
            return new ActiveRosterEntry(
                card.CardId,
                season.PlayerSeasonId,
                season.PlayerPersonId,
                season.RegistrationType,
                role);
        }

        private static PlayerCardDefinition GetCard(WorldCardCatalog catalog, string cardId)
        {
            if (!catalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new ArgumentException($"WorldCardCatalog에 없는 CardId입니다: {cardId}", nameof(cardId));
            return card;
        }

        private static void Shuffle<T>(List<T> values, IRandomSource random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = (int)(random.NextDouble() * (index + 1));
                T value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private static ulong CreateSelectionFingerprint(IReadOnlyList<string> cardIds)
        {
            var sorted = new string[cardIds.Count];
            for (int index = 0; index < sorted.Length; index++) sorted[index] = cardIds[index];
            Array.Sort(sorted, StringComparer.Ordinal);
            ulong hash = 14695981039346656037UL;
            for (int index = 0; index < sorted.Length; index++)
            {
                string value = sorted[index] ?? string.Empty;
                for (int charIndex = 0; charIndex < value.Length; charIndex++)
                {
                    hash ^= value[charIndex];
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        private static OwnerMainCardSelectionStatus Invalid(
            string code,
            string message,
            int selectedCount,
            int hitterCount,
            int pitcherCount,
            int totalCost) =>
            new OwnerMainCardSelectionStatus(false, code, message, selectedCount, hitterCount, pitcherCount, totalCost);
    }
}

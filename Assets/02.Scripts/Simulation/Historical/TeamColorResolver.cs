using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;

namespace Baseball.Simulation.Historical
{
    /// <summary>검증된 25인 로스터에서 팀컬러 후보와 카드별 2슬롯 보너스를 계산한다.</summary>
    public sealed class TeamColorResolver
    {
        public IReadOnlyList<TeamColorCandidate> Resolve(
            CurrentRosterState activeRoster,
            WorldCardCatalog catalog,
            IReadOnlyList<TeamColorDefinition> definitions)
        {
            return Resolve(CreateValidatedInputs(activeRoster, catalog), definitions);
        }

        public IReadOnlyList<TeamColorCandidate> Resolve(
            IReadOnlyList<TeamColorRosterCard> activeRoster,
            IReadOnlyList<TeamColorDefinition> definitions)
        {
            ValidateRoster(activeRoster);
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            var active = new List<TeamColorCandidate>();
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                TeamColorDefinition definition = definitions[definitionIndex]
                    ?? throw new ArgumentException("null TeamColor Definition이 있습니다.", nameof(definitions));
                if (!definitionIds.Add(definition.TeamColorId))
                    throw new ArgumentException("TeamColorId는 중복될 수 없습니다.", nameof(definitions));
                var eligibleCardIds = new List<string>();
                for (int rosterIndex = 0; rosterIndex < activeRoster.Count; rosterIndex++)
                {
                    TeamColorRosterCard card = activeRoster[rosterIndex];
                    if (definition.IsEligible(card.Eligibility))
                        eligibleCardIds.Add(card.CardId);
                }
                if (eligibleCardIds.Count >= definition.RequiredCount)
                    active.Add(new TeamColorCandidate(definition, eligibleCardIds));
            }

            var strongestByGroup = new Dictionary<string, TeamColorCandidate>(StringComparer.Ordinal);
            for (int index = 0; index < active.Count; index++)
            {
                TeamColorCandidate candidate = active[index];
                TeamColorDefinition definition = candidate.Definition;
                if (definition.StackPolicy != TeamColorStackPolicy.HighestOnly)
                    continue;
                if (!strongestByGroup.TryGetValue(definition.UpgradeGroupId, out TeamColorCandidate current) ||
                    IsStronger(definition, current.Definition))
                    strongestByGroup[definition.UpgradeGroupId] = candidate;
            }

            var result = new List<TeamColorCandidate>(active.Count);
            for (int index = 0; index < active.Count; index++)
            {
                TeamColorCandidate candidate = active[index];
                TeamColorDefinition definition = candidate.Definition;
                if (definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                    !ReferenceEquals(strongestByGroup[definition.UpgradeGroupId], candidate))
                    continue;
                result.Add(candidate);
            }
            return result;
        }

        public PerCardBonusMap ApplyEquipped(
            IReadOnlyList<TeamColorRosterCard> activeRoster,
            IReadOnlyList<TeamColorDefinition> definitions,
            TeamColorDefinition slot0,
            TeamColorDefinition slot1)
        {
            IReadOnlyList<TeamColorCandidate> active = Resolve(activeRoster, definitions);
            if (slot0 != null && slot1 != null &&
                string.Equals(slot0.TeamColorId, slot1.TeamColorId, StringComparison.Ordinal))
                throw new InvalidOperationException("동일 TeamColor를 두 슬롯에 중복 장착할 수 없습니다.");
            if (slot0 != null && slot1 != null &&
                slot0.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                slot1.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                string.Equals(slot0.UpgradeGroupId, slot1.UpgradeGroupId, StringComparison.Ordinal))
                throw new InvalidOperationException("HighestOnly 그룹은 한 단계만 장착할 수 있습니다.");

            TeamColorCandidate candidate0 = FindActive(active, slot0);
            TeamColorCandidate candidate1 = FindActive(active, slot1);
            var bonuses = new Dictionary<string, int[]>(StringComparer.Ordinal);
            ApplyCandidate(activeRoster, candidate0, bonuses);
            ApplyCandidate(activeRoster, candidate1, bonuses);
            return new PerCardBonusMap(bonuses);
        }

        public PerCardBonusMap ApplyEquipped(
            CurrentRosterState activeRoster,
            WorldCardCatalog catalog,
            IReadOnlyList<TeamColorDefinition> definitions,
            TeamColorDefinition slot0,
            TeamColorDefinition slot1)
        {
            return ApplyEquipped(CreateValidatedInputs(activeRoster, catalog), definitions, slot0, slot1);
        }

        private static void ApplyCandidate(
            IReadOnlyList<TeamColorRosterCard> activeRoster,
            TeamColorCandidate candidate,
            Dictionary<string, int[]> bonuses)
        {
            if (candidate == null)
                return;
            TeamColorDefinition definition = candidate.Definition;
            for (int index = 0; index < activeRoster.Count; index++)
            {
                TeamColorRosterCard rosterCard = activeRoster[index];
                if (!definition.IsEligible(rosterCard.Eligibility))
                    continue;
                if (!bonuses.TryGetValue(rosterCard.CardId, out int[] values))
                {
                    values = new int[PlayerAbilityCatalog.AbilityCount];
                    bonuses.Add(rosterCard.CardId, values);
                }
                TeamColorStatBonus bonus = definition.GetBonus(rosterCard.Role);
                for (int abilityIndex = 0; abilityIndex < values.Length; abilityIndex++)
                    values[abilityIndex] += bonus.Get((PlayerAbility)abilityIndex);
            }
        }

        private static TeamColorCandidate FindActive(
            IReadOnlyList<TeamColorCandidate> active,
            TeamColorDefinition equipped)
        {
            if (equipped == null)
                return null;
            for (int index = 0; index < active.Count; index++)
                if (string.Equals(active[index].Definition.TeamColorId, equipped.TeamColorId, StringComparison.Ordinal))
                    return active[index];
            throw new InvalidOperationException("발동하지 않은 TeamColor는 장착할 수 없습니다.");
        }

        private static bool IsStronger(TeamColorDefinition candidate, TeamColorDefinition current)
        {
            if (candidate.Priority != current.Priority)
                return candidate.Priority > current.Priority;
            if (candidate.StrengthScore != current.StrengthScore)
                return candidate.StrengthScore > current.StrengthScore;
            if (candidate.RequiredCount != current.RequiredCount)
                return candidate.RequiredCount > current.RequiredCount;
            return string.CompareOrdinal(candidate.TeamColorId, current.TeamColorId) < 0;
        }

        private static void ValidateRoster(IReadOnlyList<TeamColorRosterCard> activeRoster)
        {
            if (activeRoster == null)
                throw new ArgumentNullException(nameof(activeRoster));
            if (activeRoster.Count != 25)
                throw new ArgumentException("TeamColor는 검증된 ActiveRoster 25인만 판정합니다.", nameof(activeRoster));
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < activeRoster.Count; index++)
                if (!cardIds.Add(activeRoster[index].CardId))
                    throw new ArgumentException("ActiveRoster CardId는 중복될 수 없습니다.", nameof(activeRoster));
        }

        private static IReadOnlyList<TeamColorRosterCard> CreateValidatedInputs(
            CurrentRosterState activeRoster,
            WorldCardCatalog catalog)
        {
            if (activeRoster == null)
                throw new ArgumentNullException(nameof(activeRoster));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            RosterValidationResult validation = new ActiveRosterValidator().Validate(activeRoster);
            if (!validation.IsValid)
                throw new ArgumentException("공통 ActiveRosterCompositionRule 검증을 통과해야 합니다.", nameof(activeRoster));

            var inputs = new TeamColorRosterCard[activeRoster.Entries.Count];
            for (int index = 0; index < activeRoster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = activeRoster.Entries[index];
                if (!catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException("ActiveRoster 카드가 WorldCardCatalog에 없습니다.", nameof(activeRoster));
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (!string.Equals(entry.PlayerSeasonId, season.PlayerSeasonId, StringComparison.Ordinal) ||
                    !string.Equals(entry.PlayerPersonId, season.PlayerPersonId, StringComparison.Ordinal) ||
                    entry.RegistrationType != season.RegistrationType)
                    throw new ArgumentException("ActiveRoster 항목과 공통 카드 원본이 일치하지 않습니다.", nameof(activeRoster));

                inputs[index] = new TeamColorRosterCard(
                    card.CardId,
                    new TeamColorEligibilityKey(
                        season.OriginYear,
                        season.OriginFranchiseId,
                        season.OriginTeamSeasonKey,
                        card.Edition),
                    season.PlayerType == Core.Players.PlayerType.Batter ? PlayerRole.Hitter : PlayerRole.Pitcher);
            }
            return inputs;
        }
    }

    /// <summary>BaseStat을 변조하지 않고 레이어 합산, HardCap, SoftCap 확률 곡선을 계산한다.</summary>
    public static class EffectiveRatingResolver
    {
        public static EffectiveRatingResult Resolve(
            int baseStat,
            int editionModifier,
            int cardTrainingBonus,
            int enhancementBonus,
            int teamColorBonus,
            int conditionBonus,
            int tacticBonus,
            EffectiveRatingCapTable capTable)
        {
            if (baseStat < 1 || baseStat > 99)
                throw new ArgumentOutOfRangeException(nameof(baseStat));
            if (cardTrainingBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(cardTrainingBonus));
            if (enhancementBonus < 0 || enhancementBonus > OwnedPlayerCardState.MaximumEnhancementLevel)
                throw new ArgumentOutOfRangeException(nameof(enhancementBonus));
            if (capTable == null)
                throw new ArgumentNullException(nameof(capTable));

            int raw = checked(baseStat + editionModifier + cardTrainingBonus + enhancementBonus +
                              teamColorBonus + conditionBonus + tacticBonus);
            int rating = Math.Max(1, Math.Min(capTable.HardCap, raw));
            double curveRating = rating <= capTable.SoftCap
                ? rating
                : capTable.SoftCap + (rating - capTable.SoftCap) * capTable.PostSoftCapSlope;
            return new EffectiveRatingResult(rating, curveRating);
        }
    }
}

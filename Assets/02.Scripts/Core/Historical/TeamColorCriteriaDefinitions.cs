using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    public enum TeamColorRosterScope
    {
        Any,
        Hitters,
        StartingHitters,
        Pitchers,
        StartingPitchers,
        BullpenPitchers,
        LateInningPitchers
    }

    public enum TeamColorNaturalPitcherGroup
    {
        Any,
        Starter,
        Relief,
        LateInning
    }

    /// <summary>TeamColor가 BaseStat 구간을 판정할 때 사용하는 닫힌 범위다.</summary>
    public readonly struct TeamColorAbilityRequirement
    {
        public TeamColorAbilityRequirement(PlayerAbility ability, int minimum, int maximum = AbilityRatings.Maximum)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (minimum < AbilityRatings.Minimum || maximum > AbilityRatings.Maximum || maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(minimum));
            Ability = ability;
            Minimum = minimum;
            Maximum = maximum;
        }

        public PlayerAbility Ability { get; }
        public int Minimum { get; }
        public int Maximum { get; }

        public bool IsMatch(TeamColorRosterCard card)
        {
            return card.TryGetBaseAttribute(Ability, out int value) && value >= Minimum && value <= Maximum;
        }
    }

    /// <summary>Origin·Edition 외의 로스터 구성 조건을 불변 데이터로 표현한다.</summary>
    public sealed class TeamColorCardCriteria
    {
        private readonly TeamColorAbilityRequirement[] _abilityRequirements;

        public TeamColorCardCriteria(
            TeamColorRosterScope rosterScope = TeamColorRosterScope.Any,
            int? minimumAge = null,
            int? maximumAge = null,
            int? minimumCost = null,
            int? maximumCost = null,
            RegistrationType? registrationType = null,
            Handedness? bats = null,
            Handedness? throws = null,
            TeamColorNaturalPitcherGroup naturalPitcherGroup = TeamColorNaturalPitcherGroup.Any,
            bool requiresAssignedPitcherRoleMatch = false,
            IReadOnlyList<TeamColorAbilityRequirement> abilityRequirements = null)
        {
            if (!Enum.IsDefined(typeof(TeamColorRosterScope), rosterScope))
                throw new ArgumentOutOfRangeException(nameof(rosterScope));
            if (!Enum.IsDefined(typeof(TeamColorNaturalPitcherGroup), naturalPitcherGroup))
                throw new ArgumentOutOfRangeException(nameof(naturalPitcherGroup));
            ValidateRange(minimumAge, maximumAge, 1, int.MaxValue, nameof(minimumAge));
            ValidateRange(minimumCost, maximumCost, 1, 10, nameof(minimumCost));

            RosterScope = rosterScope;
            MinimumAge = minimumAge;
            MaximumAge = maximumAge;
            MinimumCost = minimumCost;
            MaximumCost = maximumCost;
            RegistrationType = registrationType;
            Bats = bats;
            Throws = throws;
            NaturalPitcherGroup = naturalPitcherGroup;
            RequiresAssignedPitcherRoleMatch = requiresAssignedPitcherRoleMatch;
            _abilityRequirements = CopyRequirements(abilityRequirements);
        }

        public static TeamColorCardCriteria Any { get; } = new TeamColorCardCriteria();

        public TeamColorRosterScope RosterScope { get; }
        public int? MinimumAge { get; }
        public int? MaximumAge { get; }
        public int? MinimumCost { get; }
        public int? MaximumCost { get; }
        public RegistrationType? RegistrationType { get; }
        public Handedness? Bats { get; }
        public Handedness? Throws { get; }
        public TeamColorNaturalPitcherGroup NaturalPitcherGroup { get; }
        public bool RequiresAssignedPitcherRoleMatch { get; }
        public IReadOnlyList<TeamColorAbilityRequirement> AbilityRequirements => _abilityRequirements;

        public bool IsMatch(TeamColorRosterCard card)
        {
            if (!IsRosterScopeMatch(card))
                return false;
            if (!IsRangeMatch(card.AgeAtOriginSeason, MinimumAge, MaximumAge) ||
                !IsRangeMatch(card.Cost, MinimumCost, MaximumCost))
                return false;
            if (RegistrationType.HasValue && card.RegistrationType != RegistrationType)
                return false;
            if (Bats.HasValue && card.Bats != Bats)
                return false;
            if (Throws.HasValue && card.Throws != Throws)
                return false;
            if (!IsNaturalPitcherGroupMatch(card))
                return false;
            if (RequiresAssignedPitcherRoleMatch && !IsAssignedPitcherRoleMatch(card))
                return false;
            for (int index = 0; index < _abilityRequirements.Length; index++)
                if (!_abilityRequirements[index].IsMatch(card))
                    return false;
            return true;
        }

        private bool IsRosterScopeMatch(TeamColorRosterCard card)
        {
            switch (RosterScope)
            {
                case TeamColorRosterScope.Any:
                    return true;
                case TeamColorRosterScope.Hitters:
                    return card.Role == PlayerRole.Hitter;
                case TeamColorRosterScope.StartingHitters:
                    return card.Role == PlayerRole.Hitter && card.ActiveRosterRole.HasValue &&
                           ActiveRosterCompositionRule.Standard.IsStartingHitterRole(card.ActiveRosterRole.Value);
                case TeamColorRosterScope.Pitchers:
                    return card.Role == PlayerRole.Pitcher;
                case TeamColorRosterScope.StartingPitchers:
                    return card.Role == PlayerRole.Pitcher && card.ActiveRosterRole.HasValue &&
                           ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(card.ActiveRosterRole.Value);
                case TeamColorRosterScope.BullpenPitchers:
                    return card.Role == PlayerRole.Pitcher && card.ActiveRosterRole.HasValue &&
                           card.ActiveRosterRole.Value >= ActiveRosterRole.Bullpen1 &&
                           card.ActiveRosterRole.Value <= ActiveRosterRole.Bullpen4;
                case TeamColorRosterScope.LateInningPitchers:
                    return card.Role == PlayerRole.Pitcher && card.ActiveRosterRole.HasValue &&
                           (card.ActiveRosterRole.Value == ActiveRosterRole.Setup ||
                            card.ActiveRosterRole.Value == ActiveRosterRole.Closer);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool IsNaturalPitcherGroupMatch(TeamColorRosterCard card)
        {
            if (NaturalPitcherGroup == TeamColorNaturalPitcherGroup.Any)
                return true;
            if (card.Role != PlayerRole.Pitcher || !card.NaturalPitcherRole.HasValue)
                return false;
            PitcherRole role = card.NaturalPitcherRole.Value;
            return NaturalPitcherGroup switch
            {
                TeamColorNaturalPitcherGroup.Starter => role == PitcherRole.Starter,
                TeamColorNaturalPitcherGroup.Relief => role != PitcherRole.Starter,
                TeamColorNaturalPitcherGroup.LateInning => role == PitcherRole.Setup || role == PitcherRole.Closer,
                _ => false
            };
        }

        private static bool IsAssignedPitcherRoleMatch(TeamColorRosterCard card)
        {
            if (!card.ActiveRosterRole.HasValue || !card.NaturalPitcherRole.HasValue)
                return false;
            ActiveRosterRole assigned = card.ActiveRosterRole.Value;
            PitcherRole natural = card.NaturalPitcherRole.Value;
            if (ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(assigned))
                return natural == PitcherRole.Starter;
            if (assigned == ActiveRosterRole.Setup)
                return natural == PitcherRole.Setup;
            if (assigned == ActiveRosterRole.Closer)
                return natural == PitcherRole.Closer;
            return assigned >= ActiveRosterRole.Bullpen1 && assigned <= ActiveRosterRole.Bullpen4 &&
                   natural != PitcherRole.Starter;
        }

        private static bool IsRangeMatch(int? value, int? minimum, int? maximum)
        {
            if (!minimum.HasValue && !maximum.HasValue)
                return true;
            return value.HasValue &&
                   (!minimum.HasValue || value.Value >= minimum.Value) &&
                   (!maximum.HasValue || value.Value <= maximum.Value);
        }

        private static void ValidateRange(int? minimum, int? maximum, int lowerBound, int upperBound, string parameterName)
        {
            if (minimum.HasValue && (minimum.Value < lowerBound || minimum.Value > upperBound))
                throw new ArgumentOutOfRangeException(parameterName);
            if (maximum.HasValue && (maximum.Value < lowerBound || maximum.Value > upperBound))
                throw new ArgumentOutOfRangeException(parameterName);
            if (minimum.HasValue && maximum.HasValue && maximum.Value < minimum.Value)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static TeamColorAbilityRequirement[] CopyRequirements(
            IReadOnlyList<TeamColorAbilityRequirement> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<TeamColorAbilityRequirement>();
            var result = new TeamColorAbilityRequirement[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }
    }
}

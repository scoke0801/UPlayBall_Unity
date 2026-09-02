using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    public enum TeamColorFamily
    {
        YearFranchise,
        Franchise,
        Year,
        AllStar,
        GoldenGlove,
        Mvp
    }

    public enum TeamColorStackPolicy
    {
        Stackable,
        HighestOnly
    }

    public enum PlayerRole
    {
        Hitter,
        Pitcher
    }

    /// <summary>팀컬러 자격 판정에 허용된 Origin과 Edition 값만 보관한다.</summary>
    public readonly struct TeamColorEligibilityKey
    {
        public TeamColorEligibilityKey(
            int originYear,
            string originFranchiseId,
            string originTeamSeasonKey,
            PlayerCardEdition edition)
        {
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (string.IsNullOrWhiteSpace(originFranchiseId))
                throw new ArgumentException("OriginFranchiseId는 비어 있을 수 없습니다.", nameof(originFranchiseId));
            if (string.IsNullOrWhiteSpace(originTeamSeasonKey))
                throw new ArgumentException("OriginTeamSeasonKey는 비어 있을 수 없습니다.", nameof(originTeamSeasonKey));
            OriginYear = originYear;
            OriginFranchiseId = originFranchiseId.Trim();
            OriginTeamSeasonKey = originTeamSeasonKey.Trim();
            Edition = edition;
        }

        public int OriginYear { get; }
        public string OriginFranchiseId { get; }
        public string OriginTeamSeasonKey { get; }
        public PlayerCardEdition Edition { get; }
    }

    /// <summary>팀컬러 판정용 1군 카드 입력이다.</summary>
    public readonly struct TeamColorRosterCard
    {
        public TeamColorRosterCard(string cardId, TeamColorEligibilityKey eligibility, PlayerRole role)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (role != PlayerRole.Hitter && role != PlayerRole.Pitcher)
                throw new ArgumentOutOfRangeException(nameof(role));
            CardId = cardId.Trim();
            Eligibility = eligibility;
            Role = role;
        }

        public string CardId { get; }
        public TeamColorEligibilityKey Eligibility { get; }
        public PlayerRole Role { get; }
    }

    /// <summary>한 역할에 적용할 능력치별 팀컬러 보너스다.</summary>
    public sealed class TeamColorStatBonus
    {
        private readonly int[] _values;

        public TeamColorStatBonus(IReadOnlyList<int> values)
        {
            if (values == null || values.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 보너스가 필요합니다.", nameof(values));
            _values = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(values));
                _values[index] = values[index];
            }
        }

        public int Get(PlayerAbility ability) => _values[(int)ability];

        public int Total
        {
            get
            {
                int total = 0;
                for (int index = 0; index < _values.Length; index++)
                    total += _values[index];
                return total;
            }
        }

        public static TeamColorStatBonus AllForRole(PlayerRole role, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++)
            {
                var ability = (PlayerAbility)index;
                if ((role == PlayerRole.Hitter && PlayerAbilityCatalog.IsBatterAbility(ability)) ||
                    (role == PlayerRole.Pitcher && PlayerAbilityCatalog.IsPitcherAbility(ability)))
                    values[index] = amount;
            }
            return new TeamColorStatBonus(values);
        }

        public static TeamColorStatBonus Create(params AbilityBonus[] bonuses)
        {
            if (bonuses == null)
                throw new ArgumentNullException(nameof(bonuses));
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < bonuses.Length; index++)
            {
                if (bonuses[index].Amount < 0)
                    throw new ArgumentOutOfRangeException(nameof(bonuses));
                values[(int)bonuses[index].Ability] += bonuses[index].Amount;
            }
            return new TeamColorStatBonus(values);
        }
    }

    public readonly struct AbilityBonus
    {
        public AbilityBonus(PlayerAbility ability, int amount)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            Ability = ability;
            Amount = amount;
        }

        public PlayerAbility Ability { get; }
        public int Amount { get; }
    }

    /// <summary>한 단계 팀컬러의 대상, 필요 인원과 역할별 효과를 정의한다.</summary>
    public sealed class TeamColorDefinition
    {
        public TeamColorDefinition(
            string teamColorId,
            TeamColorFamily family,
            int requiredCount,
            TeamColorStatBonus hitterBonus,
            TeamColorStatBonus pitcherBonus,
            int? originYear = null,
            string originFranchiseId = null,
            string originTeamSeasonKey = null,
            PlayerCardEdition? requiredEdition = null,
            string upgradeGroupId = null,
            TeamColorStackPolicy stackPolicy = TeamColorStackPolicy.Stackable,
            int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(teamColorId))
                throw new ArgumentException("TeamColorId는 비어 있을 수 없습니다.", nameof(teamColorId));
            if (requiredCount <= 0 || requiredCount > 25)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (originYear.HasValue && originYear.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (stackPolicy == TeamColorStackPolicy.HighestOnly && string.IsNullOrWhiteSpace(upgradeGroupId))
                throw new ArgumentException("HighestOnly에는 UpgradeGroupId가 필요합니다.", nameof(upgradeGroupId));

            ValidateFamilyTarget(family, originYear, originFranchiseId, requiredEdition);
            TeamColorId = teamColorId.Trim();
            Family = family;
            RequiredCount = requiredCount;
            HitterBonus = hitterBonus ?? throw new ArgumentNullException(nameof(hitterBonus));
            PitcherBonus = pitcherBonus ?? throw new ArgumentNullException(nameof(pitcherBonus));
            OriginYear = originYear;
            OriginFranchiseId = Normalize(originFranchiseId);
            OriginTeamSeasonKey = Normalize(originTeamSeasonKey);
            RequiredEdition = requiredEdition;
            UpgradeGroupId = Normalize(upgradeGroupId);
            StackPolicy = stackPolicy;
            Priority = priority;
        }

        public string TeamColorId { get; }
        public TeamColorFamily Family { get; }
        public int RequiredCount { get; }
        public TeamColorStatBonus HitterBonus { get; }
        public TeamColorStatBonus PitcherBonus { get; }
        public int? OriginYear { get; }
        public string OriginFranchiseId { get; }
        public string OriginTeamSeasonKey { get; }
        public PlayerCardEdition? RequiredEdition { get; }
        public string UpgradeGroupId { get; }
        public TeamColorStackPolicy StackPolicy { get; }
        public int Priority { get; }

        public bool IsEligible(TeamColorEligibilityKey key)
        {
            if (OriginYear.HasValue && OriginYear.Value != key.OriginYear)
                return false;
            if (OriginFranchiseId != null &&
                !string.Equals(OriginFranchiseId, key.OriginFranchiseId, StringComparison.Ordinal))
                return false;
            if (OriginTeamSeasonKey != null &&
                !string.Equals(OriginTeamSeasonKey, key.OriginTeamSeasonKey, StringComparison.Ordinal))
                return false;
            return !RequiredEdition.HasValue || RequiredEdition.Value == key.Edition;
        }

        public TeamColorStatBonus GetBonus(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Hitter => HitterBonus,
                PlayerRole.Pitcher => PitcherBonus,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
        }

        public int StrengthScore => Math.Max(HitterBonus.Total, PitcherBonus.Total);

        private static void ValidateFamilyTarget(
            TeamColorFamily family,
            int? originYear,
            string originFranchiseId,
            PlayerCardEdition? requiredEdition)
        {
            bool hasFranchise = !string.IsNullOrWhiteSpace(originFranchiseId);
            switch (family)
            {
                case TeamColorFamily.YearFranchise:
                    if (!originYear.HasValue || !hasFranchise)
                        throw new ArgumentException("YearFranchise에는 연도와 구단이 필요합니다.");
                    break;
                case TeamColorFamily.Franchise:
                    if (!hasFranchise)
                        throw new ArgumentException("Franchise에는 구단이 필요합니다.");
                    break;
                case TeamColorFamily.Year:
                    if (!originYear.HasValue)
                        throw new ArgumentException("Year에는 연도가 필요합니다.");
                    break;
                case TeamColorFamily.AllStar:
                    RequireEdition(requiredEdition, PlayerCardEdition.AllStar);
                    break;
                case TeamColorFamily.GoldenGlove:
                    RequireEdition(requiredEdition, PlayerCardEdition.GoldenGlove);
                    break;
                case TeamColorFamily.Mvp:
                    RequireEdition(requiredEdition, PlayerCardEdition.Mvp);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
        }

        private static void RequireEdition(PlayerCardEdition? actual, PlayerCardEdition expected)
        {
            if (!actual.HasValue || actual.Value != expected)
                throw new ArgumentException("명예 TeamColor Family와 Edition이 일치해야 합니다.");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>발동한 팀컬러와 실제 적용 대상 CardId를 보관한다.</summary>
    public sealed class TeamColorCandidate
    {
        private readonly string[] _eligibleCardIds;

        public TeamColorCandidate(TeamColorDefinition definition, IReadOnlyList<string> eligibleCardIds)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (eligibleCardIds == null || eligibleCardIds.Count < definition.RequiredCount)
                throw new ArgumentException("필요 인원보다 적은 적용 대상입니다.", nameof(eligibleCardIds));
            _eligibleCardIds = new string[eligibleCardIds.Count];
            for (int index = 0; index < eligibleCardIds.Count; index++)
                _eligibleCardIds[index] = eligibleCardIds[index];
        }

        public TeamColorDefinition Definition { get; }
        public IReadOnlyList<string> EligibleCardIds => _eligibleCardIds;
    }

    /// <summary>2026-09-01 확정 수치를 TeamColorDefinition 데이터로 만드는 초기 밸런스 팩토리다.</summary>
    public static class InitialTeamColorDefinitionFactory
    {
        public const string AllStarUpgradeGroupId = "AllStar_SamePool";
        public const string GoldenGloveUpgradeGroupId = "GoldenGlove_SamePool";

        public static IReadOnlyList<TeamColorDefinition> CreateYearFranchise(int originYear, string franchiseId)
        {
            return new[]
            {
                CreateAllDefinition("YearFranchise:" + originYear + ":" + franchiseId + ":10", TeamColorFamily.YearFranchise, 10, 5, 3, originYear, franchiseId),
                CreateAllDefinition("YearFranchise:" + originYear + ":" + franchiseId + ":20", TeamColorFamily.YearFranchise, 20, 7, 5, originYear, franchiseId),
                CreateAllDefinition("YearFranchise:" + originYear + ":" + franchiseId + ":25", TeamColorFamily.YearFranchise, 25, 10, 7, originYear, franchiseId)
            };
        }

        public static IReadOnlyList<TeamColorDefinition> CreateFranchise(string franchiseId)
        {
            return new[]
            {
                CreateAllDefinition("Franchise:" + franchiseId + ":10", TeamColorFamily.Franchise, 10, 3, 2, null, franchiseId),
                CreateAllDefinition("Franchise:" + franchiseId + ":20", TeamColorFamily.Franchise, 20, 4, 2, null, franchiseId),
                CreateAllDefinition("Franchise:" + franchiseId + ":25", TeamColorFamily.Franchise, 25, 6, 3, null, franchiseId)
            };
        }

        public static TeamColorDefinition CreateYear(int originYear)
        {
            return CreateAllDefinition("Year:" + originYear + ":25", TeamColorFamily.Year, 25, 4, 2, originYear, null);
        }

        public static IReadOnlyList<TeamColorDefinition> CreateAllStar(int originYear)
        {
            return new[]
            {
                new TeamColorDefinition(
                    "AllStar:Any:10", TeamColorFamily.AllStar, 10,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Power, 2), new AbilityBonus(PlayerAbility.BatterMental, 2)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Velocity, 1), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 1)),
                    requiredEdition: PlayerCardEdition.AllStar, upgradeGroupId: AllStarUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 10),
                new TeamColorDefinition(
                    "AllStar:Any:20", TeamColorFamily.AllStar, 20,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Power, 3), new AbilityBonus(PlayerAbility.BatterMental, 3)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Velocity, 1), new AbilityBonus(PlayerAbility.Breaking, 3), new AbilityBonus(PlayerAbility.PitcherMental, 1)),
                    requiredEdition: PlayerCardEdition.AllStar, upgradeGroupId: AllStarUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 20),
                new TeamColorDefinition(
                    "AllStar:" + originYear + ":20", TeamColorFamily.AllStar, 20,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Power, 5), new AbilityBonus(PlayerAbility.BatterMental, 5)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Velocity, 2), new AbilityBonus(PlayerAbility.Breaking, 5), new AbilityBonus(PlayerAbility.PitcherMental, 3)),
                    originYear: originYear, requiredEdition: PlayerCardEdition.AllStar,
                    upgradeGroupId: AllStarUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 30)
            };
        }

        public static IReadOnlyList<TeamColorDefinition> CreateGoldenGlove(int originYear)
        {
            return new[]
            {
                new TeamColorDefinition(
                    "GoldenGlove:Any:10", TeamColorFamily.GoldenGlove, 10,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.BatterMental, 2)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Stuff, 1), new AbilityBonus(PlayerAbility.Breaking, 1), new AbilityBonus(PlayerAbility.PitcherMental, 2)),
                    requiredEdition: PlayerCardEdition.GoldenGlove, upgradeGroupId: GoldenGloveUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 10),
                new TeamColorDefinition(
                    "GoldenGlove:Any:20", TeamColorFamily.GoldenGlove, 20,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Contact, 3), new AbilityBonus(PlayerAbility.BatterMental, 3)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Stuff, 2), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 3)),
                    requiredEdition: PlayerCardEdition.GoldenGlove, upgradeGroupId: GoldenGloveUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 20),
                new TeamColorDefinition(
                    "GoldenGlove:" + originYear + ":8", TeamColorFamily.GoldenGlove, 8,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Contact, 4), new AbilityBonus(PlayerAbility.BatterMental, 4)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Stuff, 2), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 4)),
                    originYear: originYear, requiredEdition: PlayerCardEdition.GoldenGlove,
                    upgradeGroupId: GoldenGloveUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 30),
                new TeamColorDefinition(
                    "GoldenGlove:" + originYear + ":10", TeamColorFamily.GoldenGlove, 10,
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Contact, 5), new AbilityBonus(PlayerAbility.BatterMental, 5)),
                    TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Stuff, 3), new AbilityBonus(PlayerAbility.Breaking, 3), new AbilityBonus(PlayerAbility.PitcherMental, 5)),
                    originYear: originYear, requiredEdition: PlayerCardEdition.GoldenGlove,
                    upgradeGroupId: GoldenGloveUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 40)
            };
        }

        public static IReadOnlyList<TeamColorDefinition> CreateMvp()
        {
            return new[]
            {
                new TeamColorDefinition(
                    "Mvp:10", TeamColorFamily.Mvp, 10,
                    TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 2),
                    TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2),
                    requiredEdition: PlayerCardEdition.Mvp),
                new TeamColorDefinition(
                    "Mvp:20", TeamColorFamily.Mvp, 20,
                    TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 3),
                    TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 3),
                    requiredEdition: PlayerCardEdition.Mvp)
            };
        }

        private static TeamColorDefinition CreateAllDefinition(
            string id,
            TeamColorFamily family,
            int requiredCount,
            int hitterAmount,
            int pitcherAmount,
            int? originYear,
            string franchiseId)
        {
            return new TeamColorDefinition(
                id,
                family,
                requiredCount,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, hitterAmount),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, pitcherAmount),
                originYear: originYear,
                originFranchiseId: franchiseId);
        }
    }

    /// <summary>카드별로 합산된 팀컬러 보너스를 제공한다.</summary>
    public sealed class PerCardBonusMap
    {
        private readonly Dictionary<string, int[]> _bonuses;

        public PerCardBonusMap(Dictionary<string, int[]> bonuses)
        {
            _bonuses = bonuses ?? throw new ArgumentNullException(nameof(bonuses));
        }

        public int Get(string cardId, PlayerAbility ability)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            return _bonuses.TryGetValue(cardId, out int[] values) ? values[(int)ability] : 0;
        }
    }

    /// <summary>EffectiveRating의 변곡점과 절대 상한을 정의한다.</summary>
    public sealed class EffectiveRatingCapTable
    {
        public EffectiveRatingCapTable(int softCap, int hardCap, double postSoftCapSlope)
        {
            if (softCap <= 0 || hardCap <= softCap)
                throw new ArgumentOutOfRangeException(nameof(softCap));
            if (postSoftCapSlope <= 0d || postSoftCapSlope >= 1d)
                throw new ArgumentOutOfRangeException(nameof(postSoftCapSlope));
            SoftCap = softCap;
            HardCap = hardCap;
            PostSoftCapSlope = postSoftCapSlope;
        }

        public int SoftCap { get; }
        public int HardCap { get; }
        public double PostSoftCapSlope { get; }

        public static EffectiveRatingCapTable CreateInitial() => new EffectiveRatingCapTable(120, 140, 0.5d);
    }

    /// <summary>HardCap 적용 수치와 확률 곡선 입력 수치를 함께 반환한다.</summary>
    public readonly struct EffectiveRatingResult
    {
        public EffectiveRatingResult(int rating, double curveRating)
        {
            Rating = rating;
            CurveRating = curveRating;
        }

        public int Rating { get; }
        public double CurveRating { get; }
    }
}

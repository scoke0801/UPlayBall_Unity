using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    public enum TeamColorFamily
    {
        YearFranchise,
        Franchise,
        Year,
        AllStar,
        GoldenGlove,
        Mvp,
        Generation,
        CostBand,
        HitterProfile,
        PitcherProfile,
        RosterComposition
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
            PlayerCardEdition edition,
            IReadOnlyList<string> wildcardFranchiseIds = null)
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
            WildcardFranchiseIds = wildcardFranchiseIds == null ? Array.Empty<string>() :
                new List<string>(wildcardFranchiseIds).AsReadOnly();
        }

        public int OriginYear { get; }
        public string OriginFranchiseId { get; }
        public IReadOnlyList<string> WildcardFranchiseIds { get; }
        public string OriginTeamSeasonKey { get; }
        public PlayerCardEdition Edition { get; }
    }

    /// <summary>팀컬러 판정용 1군 카드 입력이다.</summary>
    public readonly struct TeamColorRosterCard
    {
        private readonly int[] _baseAttributes;

        public TeamColorRosterCard(string cardId, TeamColorEligibilityKey eligibility, PlayerRole role)
            : this(
                cardId,
                eligibility,
                role,
                cost: null,
                ageAtOriginSeason: null,
                registrationType: null,
                bats: null,
                throws: null,
                naturalPitcherRole: null,
                activeRosterRole: null,
                baseAttributes: null)
        {
        }

        public TeamColorRosterCard(
            string cardId,
            TeamColorEligibilityKey eligibility,
            PlayerRole role,
            int? cost,
            int? ageAtOriginSeason,
            RegistrationType? registrationType,
            Handedness? bats,
            Handedness? throws,
            PitcherRole? naturalPitcherRole,
            ActiveRosterRole? activeRosterRole,
            AbilityRatings baseAttributes)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (role != PlayerRole.Hitter && role != PlayerRole.Pitcher)
                throw new ArgumentOutOfRangeException(nameof(role));
            if (cost.HasValue && (cost.Value < 1 || cost.Value > 10))
                throw new ArgumentOutOfRangeException(nameof(cost));
            if (ageAtOriginSeason.HasValue && ageAtOriginSeason.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(ageAtOriginSeason));
            CardId = cardId.Trim();
            Eligibility = eligibility;
            Role = role;
            Cost = cost;
            AgeAtOriginSeason = ageAtOriginSeason;
            RegistrationType = registrationType;
            Bats = bats;
            Throws = throws;
            NaturalPitcherRole = naturalPitcherRole;
            ActiveRosterRole = activeRosterRole;
            _baseAttributes = null;
            if (baseAttributes != null)
            {
                _baseAttributes = new int[PlayerAbilityCatalog.AbilityCount];
                for (int index = 0; index < _baseAttributes.Length; index++)
                    _baseAttributes[index] = baseAttributes.Get((PlayerAbility)index);
            }
        }

        public string CardId { get; }
        public TeamColorEligibilityKey Eligibility { get; }
        public PlayerRole Role { get; }
        public int? Cost { get; }
        public int? AgeAtOriginSeason { get; }
        public RegistrationType? RegistrationType { get; }
        public Handedness? Bats { get; }
        public Handedness? Throws { get; }
        public PitcherRole? NaturalPitcherRole { get; }
        public ActiveRosterRole? ActiveRosterRole { get; }

        public bool TryGetBaseAttribute(PlayerAbility ability, out int value)
        {
            if (_baseAttributes == null)
            {
                value = 0;
                return false;
            }
            value = _baseAttributes[(int)ability];
            return true;
        }
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
            int priority = 0,
            TeamColorCardCriteria criteria = null,
            string displayName = null,
            string description = null)
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
            Criteria = criteria ?? TeamColorCardCriteria.Any;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? TeamColorId : displayName.Trim();
            Description = string.IsNullOrWhiteSpace(description)
                ? "조건을 만족한 선수에게 팀컬러 효과를 적용합니다."
                : description.Trim();
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
        public TeamColorCardCriteria Criteria { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public bool IsEligible(TeamColorEligibilityKey key)
        {
            if ((Family == TeamColorFamily.Franchise || Family == TeamColorFamily.YearFranchise) &&
                (key.Edition == PlayerCardEdition.CareerHigh || key.Edition == PlayerCardEdition.Legend) &&
                key.WildcardFranchiseIds != null)
            {
                foreach (string franchiseId in key.WildcardFranchiseIds)
                    if (string.Equals(franchiseId, OriginFranchiseId, StringComparison.Ordinal))
                        return !RequiredEdition.HasValue || RequiredEdition.Value == key.Edition;
            }
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

        public bool IsEligible(TeamColorRosterCard card)
        {
            return IsEligible(card.Eligibility) && Criteria.IsMatch(card);
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
                case TeamColorFamily.Generation:
                case TeamColorFamily.CostBand:
                case TeamColorFamily.HitterProfile:
                case TeamColorFamily.PitcherProfile:
                case TeamColorFamily.RosterComposition:
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
    public static partial class InitialTeamColorDefinitionFactory
    {
        public const string AllStarUpgradeGroupId = "AllStar_SamePool";
        public const string GoldenGloveUpgradeGroupId = "GoldenGlove_SamePool";

        /// <summary>
        /// 한 구단주 세이브가 사용할 초기 TeamColor 전체 집합이다.
        /// 정체성 3계열(YearFranchise·Franchise·Year)은 플레이어 구단과 진행 연도를 기준으로 만들고,
        /// 명예 3계열(AllStar·GoldenGlove·Mvp)은 Edition만 보므로 구단과 무관하게 항상 포함한다.
        /// 어떤 TeamColor가 존재하는지는 밸런스 결정이므로 Unity 레이어가 아니라 여기서 소유한다.
        /// </summary>
        public static IReadOnlyList<TeamColorDefinition> CreateAll(
            int originYear,
            string franchiseId,
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            var definitions = new List<TeamColorDefinition>();
            definitions.AddRange(CreateYearFranchise(originYear, franchiseId, balance));
            definitions.AddRange(CreateFranchise(franchiseId, balance));
            definitions.Add(CreateYear(originYear, balance));
            definitions.AddRange(CreateAllStar(originYear, balance));
            definitions.AddRange(CreateGoldenGlove(originYear, balance));
            definitions.AddRange(CreateMvp(balance));
            definitions.AddRange(CreateReferenceInspiredProfiles(balance));
            return definitions;
        }

        public static IReadOnlyList<TeamColorDefinition> CreateYearFranchise(
            int originYear,
            string franchiseId,
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            var result = new TeamColorDefinition[balance.YearFranchiseTiers.Count];
            for (int index = 0; index < result.Length; index++)
            {
                TeamColorIdentityTierBalance tier = balance.YearFranchiseTiers[index];
                string stage = tier.RequiredCount == 25 ? "완성된 연대기" :
                    tier.RequiredCount == 20 ? "한 시즌의 중심" : "같은 계절의 시작";
                result[index] = CreateAllDefinition(
                    "YearFranchise:" + originYear + ":" + franchiseId + ":" + tier.RequiredCount,
                    TeamColorFamily.YearFranchise,
                    tier.RequiredCount,
                    tier.HitterAll,
                    tier.PitcherAll,
                    originYear,
                    franchiseId,
                    $"{originYear} {franchiseId} · {stage}",
                    $"{originYear}년 {franchiseId} 출신 {tier.RequiredCount}명이 모여 당시의 호흡을 되살립니다.");
            }
            return result;
        }

        public static IReadOnlyList<TeamColorDefinition> CreateFranchise(
            string franchiseId,
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            var result = new TeamColorDefinition[balance.FranchiseTiers.Count];
            for (int index = 0; index < result.Length; index++)
            {
                TeamColorIdentityTierBalance tier = balance.FranchiseTiers[index];
                string stage = tier.RequiredCount == 25 ? "이어지는 유니폼 III" :
                    tier.RequiredCount == 20 ? "이어지는 유니폼 II" : "이어지는 유니폼 I";
                result[index] = CreateAllDefinition(
                    "Franchise:" + franchiseId + ":" + tier.RequiredCount,
                    TeamColorFamily.Franchise,
                    tier.RequiredCount,
                    tier.HitterAll,
                    tier.PitcherAll,
                    null,
                    franchiseId,
                    $"{franchiseId} · {stage}",
                    $"서로 다른 시대를 건너 {franchiseId}의 계보를 잇는 선수 {tier.RequiredCount}명이 힘을 합칩니다.");
            }
            return result;
        }

        public static TeamColorDefinition CreateYear(int originYear, TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            TeamColorRuleBalance rule = balance.Year;
            return new TeamColorDefinition(
                "Year:" + originYear + ":" + rule.RequiredCount,
                TeamColorFamily.Year,
                rule.RequiredCount,
                rule.HitterBonus,
                rule.PitcherBonus,
                originYear: originYear,
                displayName: $"{originYear} · 동시대의 야구",
                description: $"{originYear}년을 함께 통과한 {rule.RequiredCount}명이 그 시대의 경기 감각을 공유합니다.");
        }

        public static IReadOnlyList<TeamColorDefinition> CreateAllStar(
            int originYear,
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            return new[]
            {
                CreateRuleDefinition(
                    "AllStar:Any:10", TeamColorFamily.AllStar, balance.AllStarAny10,
                    requiredEdition: PlayerCardEdition.AllStar, upgradeGroupId: AllStarUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 10,
                    displayName: "별빛의 합류", description: "시대를 가리지 않고 선택받은 별들이 모여 승부의 밀도를 높입니다."),
                CreateRuleDefinition(
                    "AllStar:Any:20", TeamColorFamily.AllStar, balance.AllStarAny20,
                    requiredEdition: PlayerCardEdition.AllStar, upgradeGroupId: AllStarUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 20,
                    displayName: "별빛의 물결", description: "수많은 선정 선수가 한 로스터에 모여 경기의 흐름을 주도합니다."),
                CreateRuleDefinition(
                    "AllStar:" + originYear + ":20", TeamColorFamily.AllStar, balance.AllStarSameYear20,
                    originYear: originYear, requiredEdition: PlayerCardEdition.AllStar,
                    upgradeGroupId: AllStarUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 30,
                    displayName: $"{originYear} · 그해의 별자리", description: $"{originYear}년 선정 선수들이 다시 모여 당시의 빛나는 조합을 완성합니다.")
            };
        }

        public static IReadOnlyList<TeamColorDefinition> CreateGoldenGlove(
            int originYear,
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            return new[]
            {
                CreateRuleDefinition(
                    "GoldenGlove:Any:10", TeamColorFamily.GoldenGlove, balance.GoldenGloveAny10,
                    requiredEdition: PlayerCardEdition.GoldenGlove, upgradeGroupId: GoldenGloveUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 10,
                    displayName: "황금 궤적", description: "수비로 인정받은 선수들이 안정된 경기 흐름을 만듭니다."),
                CreateRuleDefinition(
                    "GoldenGlove:Any:20", TeamColorFamily.GoldenGlove, balance.GoldenGloveAny20,
                    requiredEdition: PlayerCardEdition.GoldenGlove, upgradeGroupId: GoldenGloveUpgradeGroupId,
                    stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 20,
                    displayName: "황금 장벽", description: "수비의 기준이 된 선수들이 서로의 빈틈을 지웁니다."),
                CreateRuleDefinition(
                    "GoldenGlove:" + originYear + ":8", TeamColorFamily.GoldenGlove, balance.GoldenGloveSameYear8,
                    originYear: originYear, requiredEdition: PlayerCardEdition.GoldenGlove,
                    upgradeGroupId: GoldenGloveUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 30,
                    displayName: $"{originYear} · 같은 해의 황금선", description: $"{originYear}년 수비 수상자 8명이 같은 리듬으로 실점을 억제합니다."),
                CreateRuleDefinition(
                    "GoldenGlove:" + originYear + ":10", TeamColorFamily.GoldenGlove, balance.GoldenGloveSameYear10,
                    originYear: originYear, requiredEdition: PlayerCardEdition.GoldenGlove,
                    upgradeGroupId: GoldenGloveUpgradeGroupId, stackPolicy: TeamColorStackPolicy.HighestOnly, priority: 40,
                    displayName: $"{originYear} · 완성된 황금선", description: $"{originYear}년 수비 수상자 전원이 모여 한 단계 높은 안정감을 만듭니다.")
            };
        }

        public static IReadOnlyList<TeamColorDefinition> CreateMvp(TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            return new[]
            {
                CreateRuleDefinition(
                    "Mvp:10", TeamColorFamily.Mvp, balance.Mvp10,
                    requiredEdition: PlayerCardEdition.Mvp,
                    displayName: "정상들의 회합", description: "최고의 시즌을 증명한 선수들이 서로의 기준을 끌어올립니다."),
                CreateRuleDefinition(
                    "Mvp:20", TeamColorFamily.Mvp, balance.Mvp20,
                    requiredEdition: PlayerCardEdition.Mvp,
                    displayName: "왕좌의 연쇄", description: "수많은 최정상 시즌이 한 로스터에서 이어져 압도적인 집중력을 만듭니다.")
            };
        }

        private static TeamColorDefinition CreateAllDefinition(
            string id,
            TeamColorFamily family,
            int requiredCount,
            int hitterAmount,
            int pitcherAmount,
            int? originYear,
            string franchiseId,
            string displayName,
            string description)
        {
            return new TeamColorDefinition(
                id,
                family,
                requiredCount,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, hitterAmount),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, pitcherAmount),
                originYear: originYear,
                originFranchiseId: franchiseId,
                displayName: displayName,
                description: description);
        }

        private static TeamColorDefinition CreateRuleDefinition(
            string id,
            TeamColorFamily family,
            TeamColorRuleBalance rule,
            int? originYear = null,
            string originFranchiseId = null,
            PlayerCardEdition? requiredEdition = null,
            string upgradeGroupId = null,
            TeamColorStackPolicy stackPolicy = TeamColorStackPolicy.Stackable,
            int priority = 0,
            TeamColorCardCriteria criteria = null,
            string displayName = null,
            string description = null)
        {
            return new TeamColorDefinition(
                id,
                family,
                rule.RequiredCount,
                rule.HitterBonus,
                rule.PitcherBonus,
                originYear,
                originFranchiseId,
                requiredEdition: requiredEdition,
                upgradeGroupId: upgradeGroupId,
                stackPolicy: stackPolicy,
                priority: priority,
                criteria: criteria,
                displayName: displayName,
                description: description);
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

        public static EffectiveRatingCapTable CreateInitial() => new EffectiveRatingCapTable(150, AbilityRatings.Maximum, 0.5d);
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

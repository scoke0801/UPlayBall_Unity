using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>한 TeamColor 단계의 필요 인원과 역할별 효과를 보관한다.</summary>
    public sealed class TeamColorRuleBalance
    {
        public TeamColorRuleBalance(
            int requiredCount,
            TeamColorStatBonus hitterBonus,
            TeamColorStatBonus pitcherBonus)
        {
            if (requiredCount <= 0 || requiredCount > ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            RequiredCount = requiredCount;
            HitterBonus = hitterBonus ?? throw new ArgumentNullException(nameof(hitterBonus));
            PitcherBonus = pitcherBonus ?? throw new ArgumentNullException(nameof(pitcherBonus));
        }

        public int RequiredCount { get; }
        public TeamColorStatBonus HitterBonus { get; }
        public TeamColorStatBonus PitcherBonus { get; }
    }

    /// <summary>동일 구단·연도 계열의 단계별 ALL 보너스를 보관한다.</summary>
    public readonly struct TeamColorIdentityTierBalance
    {
        public TeamColorIdentityTierBalance(int requiredCount, int hitterAll, int pitcherAll)
        {
            if (requiredCount <= 0 || requiredCount > ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (hitterAll < 0 || pitcherAll < 0)
                throw new ArgumentOutOfRangeException(nameof(hitterAll));
            RequiredCount = requiredCount;
            HitterAll = hitterAll;
            PitcherAll = pitcherAll;
        }

        public int RequiredCount { get; }
        public int HitterAll { get; }
        public int PitcherAll { get; }
    }

    /// <summary>낮은 BaseStat 카드도 조합 가치가 생기도록 능력치 구간별 필요 인원과 보너스를 정의한다.</summary>
    public readonly struct TeamColorAbilityBandBalance
    {
        public TeamColorAbilityBandBalance(
            string id,
            string displaySuffix,
            int minimum,
            int maximum,
            int requiredCount,
            int bonus)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displaySuffix))
                throw new ArgumentException("능력치 구간 식별자와 표시명이 필요합니다.");
            if (minimum < AbilityRatings.Minimum || maximum > AbilityRatings.Maximum || maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(minimum));
            if (requiredCount <= 0 || requiredCount > ActiveRosterCompositionRule.ActiveRosterSize || bonus < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            Id = id.Trim();
            DisplaySuffix = displaySuffix.Trim();
            Minimum = minimum;
            Maximum = maximum;
            RequiredCount = requiredCount;
            Bonus = bonus;
        }

        public string Id { get; }
        public string DisplaySuffix { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public int RequiredCount { get; }
        public int Bonus { get; }
    }

    /// <summary>Reference에서 가져온 TeamColor 수치를 한 버전의 순수 C# 밸런스로 묶는다.</summary>
    public sealed class TeamColorBalanceTable
    {
        private readonly TeamColorIdentityTierBalance[] _yearFranchiseTiers;
        private readonly TeamColorIdentityTierBalance[] _franchiseTiers;
        private readonly TeamColorAbilityBandBalance[] _abilityBands;

        public TeamColorBalanceTable(
            IReadOnlyList<TeamColorIdentityTierBalance> yearFranchiseTiers,
            IReadOnlyList<TeamColorIdentityTierBalance> franchiseTiers,
            TeamColorRuleBalance year,
            TeamColorRuleBalance allStarAny10,
            TeamColorRuleBalance allStarAny20,
            TeamColorRuleBalance allStarSameYear20,
            TeamColorRuleBalance goldenGloveAny10,
            TeamColorRuleBalance goldenGloveAny20,
            TeamColorRuleBalance goldenGloveSameYear8,
            TeamColorRuleBalance goldenGloveSameYear10,
            TeamColorRuleBalance mvp10,
            TeamColorRuleBalance mvp20,
            TeamColorRuleBalance youngCore,
            TeamColorRuleBalance primeCore,
            TeamColorRuleBalance veteranCore,
            TeamColorRuleBalance lowCostCore,
            TeamColorRuleBalance costFourCore,
            TeamColorRuleBalance costFiveCore,
            IReadOnlyList<TeamColorAbilityBandBalance> abilityBands,
            TeamColorRuleBalance fourToolHitters,
            TeamColorRuleBalance contactSpeedHitters,
            TeamColorRuleBalance threeToolHitters,
            TeamColorRuleBalance foreignHitters,
            TeamColorRuleBalance leftStartingHitters,
            TeamColorRuleBalance switchHitters,
            TeamColorRuleBalance domesticHitters,
            TeamColorRuleBalance foreignPitchers,
            TeamColorRuleBalance lateInningRoleFit,
            TeamColorRuleBalance startingRotationRoleFit,
            TeamColorRuleBalance bullpenRoleFit,
            TeamColorRuleBalance leftPitchers,
            TeamColorRuleBalance domesticPitchers)
        {
            _yearFranchiseTiers = Copy(yearFranchiseTiers, nameof(yearFranchiseTiers));
            _franchiseTiers = Copy(franchiseTiers, nameof(franchiseTiers));
            _abilityBands = Copy(abilityBands, nameof(abilityBands));
            Year = Require(year, nameof(year));
            AllStarAny10 = Require(allStarAny10, nameof(allStarAny10));
            AllStarAny20 = Require(allStarAny20, nameof(allStarAny20));
            AllStarSameYear20 = Require(allStarSameYear20, nameof(allStarSameYear20));
            GoldenGloveAny10 = Require(goldenGloveAny10, nameof(goldenGloveAny10));
            GoldenGloveAny20 = Require(goldenGloveAny20, nameof(goldenGloveAny20));
            GoldenGloveSameYear8 = Require(goldenGloveSameYear8, nameof(goldenGloveSameYear8));
            GoldenGloveSameYear10 = Require(goldenGloveSameYear10, nameof(goldenGloveSameYear10));
            Mvp10 = Require(mvp10, nameof(mvp10));
            Mvp20 = Require(mvp20, nameof(mvp20));
            YoungCore = Require(youngCore, nameof(youngCore));
            PrimeCore = Require(primeCore, nameof(primeCore));
            VeteranCore = Require(veteranCore, nameof(veteranCore));
            LowCostCore = Require(lowCostCore, nameof(lowCostCore));
            CostFourCore = Require(costFourCore, nameof(costFourCore));
            CostFiveCore = Require(costFiveCore, nameof(costFiveCore));
            FourToolHitters = Require(fourToolHitters, nameof(fourToolHitters));
            ContactSpeedHitters = Require(contactSpeedHitters, nameof(contactSpeedHitters));
            ThreeToolHitters = Require(threeToolHitters, nameof(threeToolHitters));
            ForeignHitters = Require(foreignHitters, nameof(foreignHitters));
            LeftStartingHitters = Require(leftStartingHitters, nameof(leftStartingHitters));
            SwitchHitters = Require(switchHitters, nameof(switchHitters));
            DomesticHitters = Require(domesticHitters, nameof(domesticHitters));
            ForeignPitchers = Require(foreignPitchers, nameof(foreignPitchers));
            LateInningRoleFit = Require(lateInningRoleFit, nameof(lateInningRoleFit));
            StartingRotationRoleFit = Require(startingRotationRoleFit, nameof(startingRotationRoleFit));
            BullpenRoleFit = Require(bullpenRoleFit, nameof(bullpenRoleFit));
            LeftPitchers = Require(leftPitchers, nameof(leftPitchers));
            DomesticPitchers = Require(domesticPitchers, nameof(domesticPitchers));
        }

        public IReadOnlyList<TeamColorIdentityTierBalance> YearFranchiseTiers => _yearFranchiseTiers;
        public IReadOnlyList<TeamColorIdentityTierBalance> FranchiseTiers => _franchiseTiers;
        public IReadOnlyList<TeamColorAbilityBandBalance> AbilityBands => _abilityBands;
        public TeamColorRuleBalance Year { get; }
        public TeamColorRuleBalance AllStarAny10 { get; }
        public TeamColorRuleBalance AllStarAny20 { get; }
        public TeamColorRuleBalance AllStarSameYear20 { get; }
        public TeamColorRuleBalance GoldenGloveAny10 { get; }
        public TeamColorRuleBalance GoldenGloveAny20 { get; }
        public TeamColorRuleBalance GoldenGloveSameYear8 { get; }
        public TeamColorRuleBalance GoldenGloveSameYear10 { get; }
        public TeamColorRuleBalance Mvp10 { get; }
        public TeamColorRuleBalance Mvp20 { get; }
        public TeamColorRuleBalance YoungCore { get; }
        public TeamColorRuleBalance PrimeCore { get; }
        public TeamColorRuleBalance VeteranCore { get; }
        public TeamColorRuleBalance LowCostCore { get; }
        public TeamColorRuleBalance CostFourCore { get; }
        public TeamColorRuleBalance CostFiveCore { get; }
        public TeamColorRuleBalance FourToolHitters { get; }
        public TeamColorRuleBalance ContactSpeedHitters { get; }
        public TeamColorRuleBalance ThreeToolHitters { get; }
        public TeamColorRuleBalance ForeignHitters { get; }
        public TeamColorRuleBalance LeftStartingHitters { get; }
        public TeamColorRuleBalance SwitchHitters { get; }
        public TeamColorRuleBalance DomesticHitters { get; }
        public TeamColorRuleBalance ForeignPitchers { get; }
        public TeamColorRuleBalance LateInningRoleFit { get; }
        public TeamColorRuleBalance StartingRotationRoleFit { get; }
        public TeamColorRuleBalance BullpenRoleFit { get; }
        public TeamColorRuleBalance LeftPitchers { get; }
        public TeamColorRuleBalance DomesticPitchers { get; }

        public static TeamColorBalanceTable CreateInitial()
        {
            TeamColorStatBonus none = TeamColorStatBonus.Create();
            TeamColorRuleBalance Rule(int count, TeamColorStatBonus hitter, TeamColorStatBonus pitcher) =>
                new TeamColorRuleBalance(count, hitter, pitcher);
            TeamColorStatBonus Hitter(params AbilityBonus[] bonuses) => TeamColorStatBonus.Create(bonuses);
            TeamColorStatBonus Pitcher(params AbilityBonus[] bonuses) => TeamColorStatBonus.Create(bonuses);

            return new TeamColorBalanceTable(
                new[]
                {
                    new TeamColorIdentityTierBalance(10, 5, 3),
                    new TeamColorIdentityTierBalance(20, 7, 5),
                    new TeamColorIdentityTierBalance(25, 10, 7)
                },
                new[]
                {
                    new TeamColorIdentityTierBalance(10, 3, 2),
                    new TeamColorIdentityTierBalance(20, 4, 2),
                    new TeamColorIdentityTierBalance(25, 6, 3)
                },
                Rule(25, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 4), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2)),
                Rule(10, Hitter(new AbilityBonus(PlayerAbility.Power, 2), new AbilityBonus(PlayerAbility.BatterMental, 2)), Pitcher(new AbilityBonus(PlayerAbility.Velocity, 1), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 1))),
                Rule(20, Hitter(new AbilityBonus(PlayerAbility.Power, 3), new AbilityBonus(PlayerAbility.BatterMental, 3)), Pitcher(new AbilityBonus(PlayerAbility.Velocity, 1), new AbilityBonus(PlayerAbility.Breaking, 3), new AbilityBonus(PlayerAbility.PitcherMental, 1))),
                Rule(20, Hitter(new AbilityBonus(PlayerAbility.Power, 5), new AbilityBonus(PlayerAbility.BatterMental, 5)), Pitcher(new AbilityBonus(PlayerAbility.Velocity, 2), new AbilityBonus(PlayerAbility.Breaking, 5), new AbilityBonus(PlayerAbility.PitcherMental, 3))),
                Rule(10, Hitter(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.BatterMental, 2)), Pitcher(new AbilityBonus(PlayerAbility.Stuff, 1), new AbilityBonus(PlayerAbility.Breaking, 1), new AbilityBonus(PlayerAbility.PitcherMental, 2))),
                Rule(20, Hitter(new AbilityBonus(PlayerAbility.Contact, 3), new AbilityBonus(PlayerAbility.BatterMental, 3)), Pitcher(new AbilityBonus(PlayerAbility.Stuff, 2), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 3))),
                Rule(8, Hitter(new AbilityBonus(PlayerAbility.Contact, 4), new AbilityBonus(PlayerAbility.BatterMental, 4)), Pitcher(new AbilityBonus(PlayerAbility.Stuff, 2), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 4))),
                Rule(10, Hitter(new AbilityBonus(PlayerAbility.Contact, 5), new AbilityBonus(PlayerAbility.BatterMental, 5)), Pitcher(new AbilityBonus(PlayerAbility.Stuff, 3), new AbilityBonus(PlayerAbility.Breaking, 3), new AbilityBonus(PlayerAbility.PitcherMental, 5))),
                Rule(10, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 2), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2)),
                Rule(20, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 3), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 3)),
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.BatterMental, 4)), Pitcher(new AbilityBonus(PlayerAbility.PitcherMental, 4))),
                Rule(6, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 1)),
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.BatterMental, 4)), Pitcher(new AbilityBonus(PlayerAbility.PitcherMental, 4))),
                Rule(6, TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 2), TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2)),
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.Speed, 2), new AbilityBonus(PlayerAbility.Defense, 2)), Pitcher(new AbilityBonus(PlayerAbility.Stamina, 2), new AbilityBonus(PlayerAbility.Stuff, 2), new AbilityBonus(PlayerAbility.Control, 2))),
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.Power, 2), new AbilityBonus(PlayerAbility.Defense, 2), new AbilityBonus(PlayerAbility.BatterMental, 2)), Pitcher(new AbilityBonus(PlayerAbility.Velocity, 2), new AbilityBonus(PlayerAbility.Breaking, 2), new AbilityBonus(PlayerAbility.PitcherMental, 2))),
                new[]
                {
                    new TeamColorAbilityBandBalance("Elite", "정점", 85, AbilityRatings.Maximum, 4, 4),
                    new TeamColorAbilityBandBalance("Prime", "완성", 80, 84, 6, 4),
                    new TeamColorAbilityBandBalance("Core", "주축", 70, 79, 7, 4),
                    new TeamColorAbilityBandBalance("Rising", "도약", 60, 69, 6, 5),
                    new TeamColorAbilityBandBalance("Raw", "원석", 50, 59, 5, 8),
                    new TeamColorAbilityBandBalance("Prospect", "새싹", 40, 49, 4, 10)
                },
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.Power, 2), new AbilityBonus(PlayerAbility.Speed, 2), new AbilityBonus(PlayerAbility.Defense, 2)), none),
                Rule(7, Hitter(new AbilityBonus(PlayerAbility.Contact, 7), new AbilityBonus(PlayerAbility.Speed, 7)), none),
                Rule(6, Hitter(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.Speed, 2), new AbilityBonus(PlayerAbility.Defense, 2)), none),
                Rule(2, Hitter(new AbilityBonus(PlayerAbility.Power, 4), new AbilityBonus(PlayerAbility.BatterMental, 2)), none),
                Rule(9, Hitter(new AbilityBonus(PlayerAbility.Contact, 2), new AbilityBonus(PlayerAbility.Speed, 1)), none),
                Rule(5, Hitter(new AbilityBonus(PlayerAbility.Contact, 1), new AbilityBonus(PlayerAbility.Speed, 1), new AbilityBonus(PlayerAbility.BatterMental, 1)), none),
                Rule(14, Hitter(new AbilityBonus(PlayerAbility.Speed, 1), new AbilityBonus(PlayerAbility.Defense, 1)), none),
                Rule(2, none, Pitcher(new AbilityBonus(PlayerAbility.Velocity, 2), new AbilityBonus(PlayerAbility.Breaking, 4))),
                Rule(2, none, Pitcher(new AbilityBonus(PlayerAbility.Stuff, 1), new AbilityBonus(PlayerAbility.PitcherMental, 2))),
                Rule(5, none, Pitcher(new AbilityBonus(PlayerAbility.Stamina, 2), new AbilityBonus(PlayerAbility.Stuff, 1), new AbilityBonus(PlayerAbility.PitcherMental, 1))),
                Rule(4, none, Pitcher(new AbilityBonus(PlayerAbility.Control, 1), new AbilityBonus(PlayerAbility.PitcherMental, 2))),
                Rule(7, none, Pitcher(new AbilityBonus(PlayerAbility.Stuff, 1), new AbilityBonus(PlayerAbility.Breaking, 2))),
                Rule(11, none, Pitcher(new AbilityBonus(PlayerAbility.Control, 1), new AbilityBonus(PlayerAbility.PitcherMental, 1))));
        }

        private static TeamColorRuleBalance Require(TeamColorRuleBalance value, string parameterName)
        {
            return value ?? throw new ArgumentNullException(parameterName);
        }

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("하나 이상의 단계가 필요합니다.", parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }
    }
}

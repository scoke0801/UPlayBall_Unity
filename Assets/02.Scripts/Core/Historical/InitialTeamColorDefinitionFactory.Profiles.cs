using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    public static partial class InitialTeamColorDefinitionFactory
    {
        private readonly struct YearFranchiseKey
        {
            public YearFranchiseKey(int year, string franchiseId)
            {
                Year = year;
                FranchiseId = franchiseId;
            }

            public int Year { get; }
            public string FranchiseId { get; }
        }

        /// <summary>현재 25인 로스터의 Origin 조합에서 가능한 모든 TeamColor Definition을 만든다.</summary>
        public static IReadOnlyList<TeamColorDefinition> CreateForRoster(
            IReadOnlyList<TeamColorRosterCard> roster,
            TeamColorBalanceTable balance = null)
        {
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));
            if (roster.Count != ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentException("TeamColor Definition은 25인 ActiveRoster에서 생성합니다.", nameof(roster));
            balance ??= TeamColorBalanceTable.CreateInitial();

            var years = new List<int>();
            var franchises = new List<string>();
            var pairs = new List<YearFranchiseKey>();
            for (int index = 0; index < roster.Count; index++)
            {
                TeamColorEligibilityKey key = roster[index].Eligibility;
                AddUnique(years, key.OriginYear);
                AddUnique(franchises, key.OriginFranchiseId);
                AddUnique(pairs, key.OriginYear, key.OriginFranchiseId);
            }
            years.Sort();
            franchises.Sort(StringComparer.Ordinal);
            pairs.Sort(CompareYearFranchise);

            var definitions = new List<TeamColorDefinition>();
            for (int index = 0; index < pairs.Count; index++)
                definitions.AddRange(CreateYearFranchise(pairs[index].Year, pairs[index].FranchiseId, balance));
            for (int index = 0; index < franchises.Count; index++)
                definitions.AddRange(CreateFranchise(franchises[index], balance));
            for (int index = 0; index < years.Count; index++)
                definitions.Add(CreateYear(years[index], balance));

            if (years.Count > 0)
            {
                definitions.AddRange(CreateAllStar(years[0], balance));
                definitions.AddRange(CreateGoldenGlove(years[0], balance));
                for (int index = 1; index < years.Count; index++)
                {
                    IReadOnlyList<TeamColorDefinition> allStar = CreateAllStar(years[index], balance);
                    definitions.Add(allStar[2]);
                    IReadOnlyList<TeamColorDefinition> goldenGlove = CreateGoldenGlove(years[index], balance);
                    definitions.Add(goldenGlove[2]);
                    definitions.Add(goldenGlove[3]);
                }
            }
            definitions.AddRange(CreateMvp(balance));
            definitions.AddRange(CreateReferenceInspiredProfiles(balance));
            return definitions;
        }

        /// <summary>Reference의 나이·Cost·능력치·투타·보직 조합을 프로젝트 고유 콘텐츠로 만든다.</summary>
        public static IReadOnlyList<TeamColorDefinition> CreateReferenceInspiredProfiles(
            TeamColorBalanceTable balance = null)
        {
            balance ??= TeamColorBalanceTable.CreateInitial();
            var definitions = new List<TeamColorDefinition>();
            AddGenerationDefinitions(definitions, balance);
            AddCostDefinitions(definitions, balance);
            AddAbilityBandDefinitions(definitions, balance);
            AddHitterCompositionDefinitions(definitions, balance);
            AddPitcherCompositionDefinitions(definitions, balance);
            return definitions;
        }

        private static void AddGenerationDefinitions(List<TeamColorDefinition> result, TeamColorBalanceTable balance)
        {
            result.Add(CreateRuleDefinition(
                "Generation:YoungCore", TeamColorFamily.Generation, balance.YoungCore,
                criteria: new TeamColorCardCriteria(maximumAge: 25),
                displayName: "다가오는 파도",
                description: "원시즌 나이 25세 이하 선수 6명이 두려움 없는 경기 감각을 나눕니다."));
            result.Add(CreateRuleDefinition(
                "Generation:PrimeCore", TeamColorFamily.Generation, balance.PrimeCore,
                criteria: new TeamColorCardCriteria(minimumAge: 26, maximumAge: 34),
                displayName: "한복판의 시간",
                description: "원시즌 나이 26~34세 선수 6명이 완성된 기량으로 로스터의 중심을 잡습니다."));
            result.Add(CreateRuleDefinition(
                "Generation:VeteranCore", TeamColorFamily.Generation, balance.VeteranCore,
                criteria: new TeamColorCardCriteria(minimumAge: 35),
                displayName: "긴 계절의 지혜",
                description: "원시즌 나이 35세 이상 선수 6명이 수많은 승부에서 얻은 침착함을 전합니다."));
        }

        private static void AddCostDefinitions(List<TeamColorDefinition> result, TeamColorBalanceTable balance)
        {
            result.Add(CreateRuleDefinition(
                "CostBand:1-3", TeamColorFamily.CostBand, balance.LowCostCore,
                criteria: new TeamColorCardCriteria(maximumCost: 3),
                displayName: "숨은 가능성",
                description: "Cost 1~3 선수 6명이 서로의 장점을 끌어내 값 이상의 전력을 만듭니다."));
            result.Add(CreateRuleDefinition(
                "CostBand:4", TeamColorFamily.CostBand, balance.CostFourCore,
                criteria: new TeamColorCardCriteria(minimumCost: 4, maximumCost: 4),
                displayName: "도약을 준비한 선수들",
                description: "Cost 4 선수 6명이 공수의 빈틈을 함께 메우며 다음 단계로 도약합니다."));
            result.Add(CreateRuleDefinition(
                "CostBand:5", TeamColorFamily.CostBand, balance.CostFiveCore,
                criteria: new TeamColorCardCriteria(minimumCost: 5, maximumCost: 5),
                displayName: "단단한 중추",
                description: "Cost 5 선수 6명이 역할을 분담해 흔들리지 않는 중심선을 만듭니다."));
        }

        private static void AddAbilityBandDefinitions(List<TeamColorDefinition> result, TeamColorBalanceTable balance)
        {
            AddAbilityBands(result, balance, PlayerAbility.Contact, PlayerRole.Hitter, "BatPath", "정교한 궤적");
            AddAbilityBands(result, balance, PlayerAbility.Power, PlayerRole.Hitter, "Impact", "깊은 타구의 물결");
            AddAbilityBands(result, balance, PlayerAbility.Speed, PlayerRole.Hitter, "Run", "한 베이스 앞선 발");
            AddAbilityBands(result, balance, PlayerAbility.Defense, PlayerRole.Hitter, "Glove", "빈틈을 닫는 수비");
            AddAbilityBands(result, balance, PlayerAbility.BatterMental, PlayerRole.Hitter, "Poise", "흔들림 없는 타석");
            AddAbilityBands(result, balance, PlayerAbility.Velocity, PlayerRole.Pitcher, "Heat", "마운드의 압력");
            AddAbilityBands(result, balance, PlayerAbility.Stamina, PlayerRole.Pitcher, "Endurance", "긴 이닝의 호흡");
            AddAbilityBands(result, balance, PlayerAbility.Stuff, PlayerRole.Pitcher, "Life", "끝에서 살아나는 공");
            AddAbilityBands(result, balance, PlayerAbility.Control, PlayerRole.Pitcher, "Command", "포수 미트의 좌표");
            AddAbilityBands(result, balance, PlayerAbility.PitcherMental, PlayerRole.Pitcher, "Nerve", "위기를 잠그는 심장");
            AddAbilityBands(result, balance, PlayerAbility.Breaking, PlayerRole.Pitcher, "Break", "궤도를 바꾸는 손끝");
        }

        private static void AddAbilityBands(
            List<TeamColorDefinition> result,
            TeamColorBalanceTable balance,
            PlayerAbility ability,
            PlayerRole role,
            string idPrefix,
            string displayPrefix)
        {
            TeamColorRosterScope scope = role == PlayerRole.Hitter
                ? TeamColorRosterScope.Hitters
                : TeamColorRosterScope.Pitchers;
            for (int index = 0; index < balance.AbilityBands.Count; index++)
            {
                TeamColorAbilityBandBalance band = balance.AbilityBands[index];
                TeamColorStatBonus hitterBonus = role == PlayerRole.Hitter
                    ? TeamColorStatBonus.Create(new AbilityBonus(ability, band.Bonus))
                    : TeamColorStatBonus.Create();
                TeamColorStatBonus pitcherBonus = role == PlayerRole.Pitcher
                    ? TeamColorStatBonus.Create(new AbilityBonus(ability, band.Bonus))
                    : TeamColorStatBonus.Create();
                result.Add(new TeamColorDefinition(
                    $"{role}Profile:{idPrefix}:{band.Id}",
                    role == PlayerRole.Hitter ? TeamColorFamily.HitterProfile : TeamColorFamily.PitcherProfile,
                    band.RequiredCount,
                    hitterBonus,
                    pitcherBonus,
                    criteria: new TeamColorCardCriteria(
                        rosterScope: scope,
                        abilityRequirements: new[]
                        {
                            new TeamColorAbilityRequirement(ability, band.Minimum, band.Maximum)
                        }),
                    displayName: $"{displayPrefix} · {band.DisplaySuffix}",
                    description: $"BaseStat {band.Minimum}~{band.Maximum}의 해당 능력을 지닌 {RoleText(role)} {band.RequiredCount}명이 서로의 {AbilityText(ability)}을 {band.Bonus}만큼 끌어올립니다."));
            }
        }

        private static void AddHitterCompositionDefinitions(List<TeamColorDefinition> result, TeamColorBalanceTable balance)
        {
            result.Add(CreateRuleDefinition(
                "HitterProfile:FourTool", TeamColorFamily.HitterProfile, balance.FourToolHitters,
                criteria: HitterAbilities(60, PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Defense),
                displayName: "네 갈래 재능",
                description: "Contact·Power·Speed·Defense BaseStat이 모두 60 이상인 야수 6명이 공수의 폭을 넓힙니다."));
            result.Add(CreateRuleDefinition(
                "HitterProfile:ContactSpeed", TeamColorFamily.HitterProfile, balance.ContactSpeedHitters,
                criteria: HitterAbilities(70, PlayerAbility.Contact, PlayerAbility.Speed),
                displayName: "맞히고 먼저 닿는다",
                description: "Contact와 Speed BaseStat이 모두 70 이상인 야수 7명이 출루 뒤의 압박까지 이어갑니다."));
            result.Add(CreateRuleDefinition(
                "HitterProfile:ThreeTool", TeamColorFamily.HitterProfile, balance.ThreeToolHitters,
                criteria: HitterAbilities(60, PlayerAbility.Contact, PlayerAbility.Speed, PlayerAbility.Defense),
                displayName: "공수주 연결선",
                description: "Contact·Speed·Defense BaseStat이 모두 60 이상인 야수 6명이 끊김 없는 야구를 만듭니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:ForeignHitters", TeamColorFamily.RosterComposition, balance.ForeignHitters,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Hitters, registrationType: RegistrationType.Foreign),
                displayName: "먼 곳에서 온 중심타선",
                description: "외국인 야수 2명이 장타 부담을 나누며 타선의 무게를 키웁니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:LeftStartingHitters", TeamColorFamily.RosterComposition, balance.LeftStartingHitters,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.StartingHitters, bats: Handedness.Left),
                displayName: "왼쪽에서 여는 길",
                description: "선발 야수 9명을 모두 좌타자로 구성해 타구 방향과 주루 압박을 한쪽 리듬으로 묶습니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:SwitchHitters", TeamColorFamily.RosterComposition, balance.SwitchHitters,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Hitters, bats: Handedness.Switch),
                displayName: "양쪽 타석의 해답",
                description: "Switch Hitter 5명이 상대 투수의 손에 흔들리지 않는 타선을 구성합니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:DomesticHitters", TeamColorFamily.RosterComposition, balance.DomesticHitters,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Hitters, registrationType: RegistrationType.Domestic),
                displayName: "익숙한 야구의 결",
                description: "등록 야수 14명을 국내 선수로 채워 수비와 주루의 공통 감각을 살립니다."));
        }

        private static void AddPitcherCompositionDefinitions(List<TeamColorDefinition> result, TeamColorBalanceTable balance)
        {
            result.Add(CreateRuleDefinition(
                "RosterComposition:ForeignPitchers", TeamColorFamily.RosterComposition, balance.ForeignPitchers,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Pitchers, registrationType: RegistrationType.Foreign),
                displayName: "낯선 각도의 원투펀치",
                description: "외국인 투수 2명이 서로 다른 힘과 궤도로 마운드의 선택지를 넓힙니다."));
            result.Add(CreateRuleDefinition(
                "PitcherProfile:LateInningRoleFit", TeamColorFamily.PitcherProfile, balance.LateInningRoleFit,
                criteria: new TeamColorCardCriteria(
                    rosterScope: TeamColorRosterScope.LateInningPitchers,
                    minimumCost: 7,
                    naturalPitcherGroup: TeamColorNaturalPitcherGroup.LateInning,
                    requiresAssignedPitcherRoleMatch: true),
                displayName: "마지막 두 문의 주인",
                description: "Cost 7 이상 Setup과 Closer를 각자의 Natural Role에 배치해 경기 후반을 잠급니다."));
            result.Add(CreateRuleDefinition(
                "PitcherProfile:StartingRotationRoleFit", TeamColorFamily.PitcherProfile, balance.StartingRotationRoleFit,
                criteria: new TeamColorCardCriteria(
                    rosterScope: TeamColorRosterScope.StartingPitchers,
                    minimumCost: 7,
                    naturalPitcherGroup: TeamColorNaturalPitcherGroup.Starter,
                    requiresAssignedPitcherRoleMatch: true),
                displayName: "다섯 날의 기둥",
                description: "Cost 7 이상 Natural Starter 5명이 Rotation을 빈틈없이 이어 갑니다."));
            result.Add(CreateRuleDefinition(
                "PitcherProfile:BullpenRoleFit", TeamColorFamily.PitcherProfile, balance.BullpenRoleFit,
                criteria: new TeamColorCardCriteria(
                    rosterScope: TeamColorRosterScope.BullpenPitchers,
                    minimumCost: 6,
                    naturalPitcherGroup: TeamColorNaturalPitcherGroup.Relief,
                    requiresAssignedPitcherRoleMatch: true),
                displayName: "네 개의 징검다리",
                description: "Cost 6 이상 Natural Relief 투수 4명이 선발에서 승리조까지 흐름을 잇습니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:LeftPitchers", TeamColorFamily.RosterComposition, balance.LeftPitchers,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Pitchers, throws: Handedness.Left),
                displayName: "왼손이 만든 낯선 궤도",
                description: "좌투수 7명이 반복되는 타석 대결에 다른 각도와 움직임을 더합니다."));
            result.Add(CreateRuleDefinition(
                "RosterComposition:DomesticPitchers", TeamColorFamily.RosterComposition, balance.DomesticPitchers,
                criteria: new TeamColorCardCriteria(rosterScope: TeamColorRosterScope.Pitchers, registrationType: RegistrationType.Domestic),
                displayName: "같은 리그의 마운드",
                description: "등록 투수 11명을 국내 선수로 구성해 제구와 위기 대응의 공통 감각을 살립니다."));
        }

        private static TeamColorCardCriteria HitterAbilities(int minimum, params PlayerAbility[] abilities)
        {
            var requirements = new TeamColorAbilityRequirement[abilities.Length];
            for (int index = 0; index < abilities.Length; index++)
                requirements[index] = new TeamColorAbilityRequirement(abilities[index], minimum);
            return new TeamColorCardCriteria(
                rosterScope: TeamColorRosterScope.Hitters,
                abilityRequirements: requirements);
        }

        private static string RoleText(PlayerRole role) => role == PlayerRole.Hitter ? "야수" : "투수";

        private static string AbilityText(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "Contact",
                PlayerAbility.Power => "Power",
                PlayerAbility.Speed => "Speed",
                PlayerAbility.Defense => "Defense",
                PlayerAbility.BatterMental => "BatterMental",
                PlayerAbility.Stamina => "Stamina",
                PlayerAbility.Velocity => "Velocity",
                PlayerAbility.Stuff => "Stuff",
                PlayerAbility.Breaking => "Breaking",
                PlayerAbility.Control => "Control",
                PlayerAbility.PitcherMental => "PitcherMental",
                _ => ability.ToString()
            };
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
                values.Add(value);
        }

        private static void AddUnique(List<YearFranchiseKey> values, int year, string franchiseId)
        {
            for (int index = 0; index < values.Count; index++)
                if (values[index].Year == year &&
                    string.Equals(values[index].FranchiseId, franchiseId, StringComparison.Ordinal))
                    return;
            values.Add(new YearFranchiseKey(year, franchiseId));
        }

        private static int CompareYearFranchise(YearFranchiseKey left, YearFranchiseKey right)
        {
            int yearComparison = left.Year.CompareTo(right.Year);
            return yearComparison != 0
                ? yearComparison
                : string.CompareOrdinal(left.FranchiseId, right.FranchiseId);
        }
    }
}

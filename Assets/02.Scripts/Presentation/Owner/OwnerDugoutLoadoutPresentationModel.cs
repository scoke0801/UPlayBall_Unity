using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>팀컬러 한 단계의 발동 조건, 적용 대상과 장착 충돌 정보를 표시한다.</summary>
    public sealed class OwnerTeamColorCandidateSnapshot
    {
        public OwnerTeamColorCandidateSnapshot(
            TeamColorDefinition definition,
            int eligibleCount,
            IReadOnlyList<string> eligiblePlayerNames,
            bool isActive)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            EligibleCount = eligibleCount;
            EligiblePlayerNames = Copy(eligiblePlayerNames);
            IsActive = isActive;
        }

        public TeamColorDefinition Definition { get; }
        public string Id => Definition.TeamColorId;
        public string Name => Definition.DisplayName;
        public int EligibleCount { get; }
        public IReadOnlyList<string> EligiblePlayerNames { get; }
        public bool IsActive { get; }
        public string ProgressText => $"{EligibleCount}/{Definition.RequiredCount}명";
        public string StackGroup => Definition.StackPolicy == TeamColorStackPolicy.HighestOnly
            ? Definition.UpgradeGroupId
            : string.Empty;

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>팀컬러 두 슬롯과 전체 발동 진행도를 독립 화면에 전달한다.</summary>
    public sealed class OwnerTeamColorSnapshot
    {
        public OwnerTeamColorSnapshot(
            string presetName,
            IReadOnlyList<string> equippedIds,
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> candidates)
        {
            PresetName = presetName ?? string.Empty;
            EquippedIds = Copy(equippedIds, LineupPresetState.TeamColorSlotCount);
            Candidates = Copy(candidates);
        }

        public string PresetName { get; }
        public IReadOnlyList<string> EquippedIds { get; }
        public IReadOnlyList<OwnerTeamColorCandidateSnapshot> Candidates { get; }

        private static string[] Copy(IReadOnlyList<string> source, int count)
        {
            if (source == null || source.Count != count) throw new ArgumentException("팀컬러 슬롯 수가 올바르지 않습니다.");
            var result = new string[count];
            for (int index = 0; index < count; index++) result[index] = source[index];
            return result;
        }

        private static OwnerTeamColorCandidateSnapshot[] Copy(IReadOnlyList<OwnerTeamColorCandidateSnapshot> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new OwnerTeamColorCandidateSnapshot[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>작전카드의 발동 조건과 실제 효과를 표시용 문구로 보관한다.</summary>
    public sealed class OwnerTacticCardSnapshot
    {
        public OwnerTacticCardSnapshot(TacticCardDefinition definition, int ownedCount)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            OwnedCount = ownedCount;
            TriggerText = OwnerDugoutLoadoutPresentationBuilder.DescribeTriggers(definition.TriggerConditions);
            EffectText = OwnerDugoutLoadoutPresentationBuilder.DescribeEffects(definition);
        }

        public TacticCardDefinition Definition { get; }
        public string Id => Definition.CardId;
        public string Name => Definition.Name;
        public int OwnedCount { get; }
        public string ArtworkKey => TacticCardArtwork.GetKey(Definition.Category);
        public string TriggerText { get; }
        public string EffectText { get; }
    }

    /// <summary>보유 작전과 두 장의 경기 기본 장착 상태를 독립 화면에 전달한다.</summary>
    public sealed class OwnerTacticsSnapshot
    {
        public OwnerTacticsSnapshot(
            string presetName,
            IReadOnlyList<string> equippedIds,
            IReadOnlyList<OwnerTacticCardSnapshot> cards)
        {
            PresetName = presetName ?? string.Empty;
            EquippedIds = CopyIds(equippedIds);
            Cards = CopyCards(cards);
        }

        public string PresetName { get; }
        public IReadOnlyList<string> EquippedIds { get; }
        public IReadOnlyList<OwnerTacticCardSnapshot> Cards { get; }

        private static string[] CopyIds(IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static OwnerTacticCardSnapshot[] CopyCards(IReadOnlyList<OwnerTacticCardSnapshot> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new OwnerTacticCardSnapshot[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>현재 Runtime을 팀컬러·작전 전용 화면 Snapshot으로 투영한다.</summary>
    public static class OwnerDugoutLoadoutPresentationBuilder
    {
        public static OwnerTeamColorSnapshot BuildTeamColor(Baseball.Game.Historical.OwnerModeManager manager)
        {
            if (manager == null || !manager.HasActiveRuntime) throw new InvalidOperationException("활성 구단주 Runtime이 없습니다.");
            ManagerHistoricalRuntimeState runtime = manager.Runtime;
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            IReadOnlyList<TeamColorRosterCard> rosterCards = TeamColorResolver.CreateRosterCards(roster, runtime.WorldCardCatalog);
            IReadOnlyList<TeamColorDefinition> definitions = manager.GetTeamColorCatalog();
            IReadOnlyList<TeamColorCandidate> active = new TeamColorResolver().Resolve(rosterCards, definitions);
            var playersByCard = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < roster.Entries.Count; index++)
                playersByCard[roster.Entries[index].CardId] = runtime.IdentityRegistry.GetPlayerDisplayName(roster.Entries[index].PlayerPersonId);

            var candidates = new OwnerTeamColorCandidateSnapshot[definitions.Count];
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                TeamColorDefinition definition = definitions[definitionIndex];
                var names = new List<string>();
                for (int rosterIndex = 0; rosterIndex < rosterCards.Count; rosterIndex++)
                    if (definition.IsEligible(rosterCards[rosterIndex])) names.Add(playersByCard[rosterCards[rosterIndex].CardId]);
                bool isActive = false;
                for (int activeIndex = 0; activeIndex < active.Count; activeIndex++)
                    if (string.Equals(active[activeIndex].Definition.TeamColorId, definition.TeamColorId, StringComparison.Ordinal)) isActive = true;
                candidates[definitionIndex] = new OwnerTeamColorCandidateSnapshot(definition, names.Count, names, isActive);
            }
            Array.Sort(candidates, CompareTeamColors);
            LineupPresetState preset = runtime.ManagerMode.GetSelectedLineupPreset();
            return new OwnerTeamColorSnapshot(preset.Name, preset.TeamColorIds, candidates);
        }

        public static OwnerTacticsSnapshot BuildTactics(Baseball.Game.Historical.OwnerModeManager manager)
        {
            if (manager == null || !manager.HasActiveRuntime) throw new InvalidOperationException("활성 구단주 Runtime이 없습니다.");
            ManagerHistoricalRuntimeState runtime = manager.Runtime;
            IReadOnlyList<TacticCardDefinition> definitions = manager.GetAvailableTacticCards();
            var cards = new OwnerTacticCardSnapshot[definitions.Count];
            for (int index = 0; index < definitions.Count; index++)
                cards[index] = new OwnerTacticCardSnapshot(definitions[index], runtime.TacticCollection.GetCount(definitions[index].CardId));
            Array.Sort(cards, CompareTactics);
            LineupPresetState preset = runtime.ManagerMode.GetSelectedLineupPreset();
            return new OwnerTacticsSnapshot(preset.Name, preset.DefaultTacticCardIds, cards);
        }

        public static string DescribeTeamColorEffect(TeamColorDefinition definition)
        {
            return $"야수 보너스 합 {definition.HitterBonus.Total} · 투수 보너스 합 {definition.PitcherBonus.Total}\n" +
                   $"중첩: {(definition.StackPolicy == TeamColorStackPolicy.Stackable ? "동시 적용" : "동일 계열 최고 단계만")}";
        }

        public static string DescribeTriggers(IReadOnlyList<TacticTriggerCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0) return "조건 없음";
            var builder = new StringBuilder();
            for (int index = 0; index < conditions.Count; index++)
            {
                if (index > 0) builder.Append(" · ");
                TacticTriggerCondition item = conditions[index];
                builder.Append(GetTriggerFieldName(item.Field)).Append(' ').Append(GetComparisonText(item));
            }
            return builder.ToString();
        }

        public static string DescribeEffects(TacticCardDefinition definition)
        {
            var builder = new StringBuilder();
            for (int index = 0; index < definition.StatModifiers.Count; index++)
            {
                if (builder.Length > 0) builder.Append(" · ");
                TacticStatModifier item = definition.StatModifiers[index];
                builder.Append(GetAbilityName(item.Ability)).Append(' ').Append(item.Amount >= 0 ? "+" : string.Empty).Append(item.Amount);
            }
            for (int index = 0; index < definition.BehaviorModifiers.Count; index++)
            {
                if (builder.Length > 0) builder.Append(" · ");
                TacticBehaviorModifier item = definition.BehaviorModifiers[index];
                builder.Append(item.BehaviorId).Append(' ').Append(item.Amount >= 0 ? "+" : string.Empty).Append(item.Amount.ToString("0.##"));
            }
            if (builder.Length == 0) builder.Append(definition.ProjectBalanceValue);
            builder.Append("\n대상 ").Append(GetTargetName(definition.TargetRule));
            builder.Append(" · 지속 ").Append(GetDurationName(definition.DurationRule));
            if (definition.IsDisruption) builder.Append(" · 방해카드");
            return builder.ToString();
        }

        private static int CompareTeamColors(OwnerTeamColorCandidateSnapshot left, OwnerTeamColorCandidateSnapshot right)
        {
            if (left.IsActive != right.IsActive) return left.IsActive ? -1 : 1;
            int family = left.Definition.Family.CompareTo(right.Definition.Family);
            return family != 0 ? family : right.Definition.RequiredCount.CompareTo(left.Definition.RequiredCount);
        }

        private static int CompareTactics(OwnerTacticCardSnapshot left, OwnerTacticCardSnapshot right)
        {
            int category = left.Definition.Category.CompareTo(right.Definition.Category);
            return category != 0 ? category : string.CompareOrdinal(left.Id, right.Id);
        }

        private static string GetTriggerFieldName(TacticTriggerField value) => value switch
        {
            TacticTriggerField.Inning => "이닝",
            TacticTriggerField.ScoreDifference => "점수차",
            TacticTriggerField.AbsoluteRunDifference => "절대 점수차",
            TacticTriggerField.BatterOrder => "타순",
            TacticTriggerField.RunnerOnSecondOrThird => "득점권 주자",
            TacticTriggerField.OpponentPitcherHand => "상대 투수 손",
            TacticTriggerField.PitcherRole => "투수 역할",
            _ => "판정 조건"
        };

        private static string GetComparisonText(TacticTriggerCondition value) => value.Comparison switch
        {
            TacticComparison.Equal => $"= {value.Value}",
            TacticComparison.NotEqual => $"≠ {value.Value}",
            TacticComparison.LessOrEqual => $"≤ {value.Value}",
            TacticComparison.GreaterOrEqual => $"≥ {value.Value}",
            TacticComparison.BetweenInclusive => $"{value.Value}~{value.MaximumValue}",
            _ => value.Value.ToString()
        };

        private static string GetTargetName(TacticTargetRule value) => value switch
        {
            TacticTargetRule.CurrentBatter => "현재 타자",
            TacticTargetRule.CurrentPitcher => "현재 투수",
            TacticTargetRule.BattingTeam => "공격팀",
            TacticTargetRule.PitchingTeam => "수비팀",
            TacticTargetRule.Bullpen => "불펜",
            TacticTargetRule.Opponent => "상대팀",
            _ => "적용 대상"
        };

        private static string GetDurationName(TacticDurationRule value) => value switch
        {
            TacticDurationRule.CurrentPlateAppearance => "현재 타석",
            TacticDurationRule.UntilInningEnd => "이닝 종료",
            TacticDurationRule.UntilPitcherRemoved => "투수 교체",
            TacticDurationRule.RestOfGame => "경기 종료",
            _ => "지속 시간"
        };

        private static string GetAbilityName(PlayerAbility value) => value switch
        {
            PlayerAbility.Contact => "컨택",
            PlayerAbility.Power => "장타",
            PlayerAbility.Speed => "주루",
            PlayerAbility.Arm => "송구",
            PlayerAbility.Defense => "수비",
            PlayerAbility.BatterMental => "타자 정신력",
            PlayerAbility.Stamina => "체력",
            PlayerAbility.Velocity => "구속",
            PlayerAbility.Stuff => "구위",
            PlayerAbility.Breaking => "변화구",
            PlayerAbility.Control => "제구",
            PlayerAbility.PitcherMental => "투수 정신력",
            _ => "능력치 정보 없음"
        };
    }
}

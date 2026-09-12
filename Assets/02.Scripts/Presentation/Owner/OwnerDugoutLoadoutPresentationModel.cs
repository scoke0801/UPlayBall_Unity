using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Career;
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
            : this(
                definition,
                eligibleCount,
                eligiblePlayerNames,
                isActive,
                definition?.DisplayName,
                definition?.Description)
        {
        }

        public OwnerTeamColorCandidateSnapshot(
            TeamColorDefinition definition,
            int eligibleCount,
            IReadOnlyList<string> eligiblePlayerNames,
            bool isActive,
            string displayName,
            string description)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            EligibleCount = eligibleCount;
            EligiblePlayerNames = Copy(eligiblePlayerNames);
            IsActive = isActive;
            Name = OwnerTeamColorDisplayFormatter.FormatName(definition, displayName);
            Description = OwnerTeamColorDisplayFormatter.FormatDescription(definition, description);
            Grade = OwnerTeamColorDisplayFormatter.FormatGrade(definition);
        }

        public TeamColorDefinition Definition { get; }
        public string Id => Definition.TeamColorId;
        public string Name { get; }
        public string Description { get; }
        public string Grade { get; }
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
            ActiveEffectSummary = OwnerDugoutLoadoutPresentationBuilder.DescribeActiveTeamColorEffects(
                EquippedIds,
                Candidates);
        }

        public string PresetName { get; }
        public IReadOnlyList<string> EquippedIds { get; }
        public IReadOnlyList<OwnerTeamColorCandidateSnapshot> Candidates { get; }
        public string ActiveEffectSummary { get; }

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

    /// <summary>작전 화면의 일정표 한 행에 필요한 상대·결과·편집 가능 여부를 보관한다.</summary>
    public sealed class OwnerTacticScheduleRowSnapshot
    {
        public OwnerTacticScheduleRowSnapshot(
            int round,
            string opponentName,
            bool isHome,
            bool isCompleted,
            int teamRuns,
            int opponentRuns,
            bool isConfigurable)
            : this(round, round, opponentName, isHome, isCompleted, teamRuns, opponentRuns,
                isConfigurable, Array.Empty<string>())
        {
        }

        public OwnerTacticScheduleRowSnapshot(
            int gameId,
            int round,
            string opponentName,
            bool isHome,
            bool isCompleted,
            int teamRuns,
            int opponentRuns,
            bool isConfigurable,
            IReadOnlyList<string> equippedIds)
        {
            if (gameId <= 0) throw new ArgumentOutOfRangeException(nameof(gameId));
            if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
            GameId = gameId;
            Round = round;
            OpponentName = string.IsNullOrWhiteSpace(opponentName) ? "상대 구단" : opponentName.Trim();
            IsHome = isHome;
            IsCompleted = isCompleted;
            TeamRuns = teamRuns;
            OpponentRuns = opponentRuns;
            IsConfigurable = isConfigurable;
            EquippedIds = CopyIds(equippedIds);
        }

        public int GameId { get; }
        public int Round { get; }
        public string OpponentName { get; }
        public bool IsHome { get; }
        public bool IsCompleted { get; }
        public int TeamRuns { get; }
        public int OpponentRuns { get; }
        public bool IsConfigurable { get; }
        public IReadOnlyList<string> EquippedIds { get; }

        private static string[] CopyIds(IReadOnlyList<string> source)
        {
            var result = new string[source?.Count ?? 0];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>보유 작전과 두 장의 경기 기본 장착 상태를 독립 화면에 전달한다.</summary>
    public sealed class OwnerTacticsSnapshot
    {
        public OwnerTacticsSnapshot(
            string presetName,
            IReadOnlyList<string> equippedIds,
            IReadOnlyList<OwnerTacticCardSnapshot> cards)
            : this(presetName, equippedIds, cards, Array.Empty<OwnerTacticScheduleRowSnapshot>())
        {
        }

        public OwnerTacticsSnapshot(
            string presetName,
            IReadOnlyList<string> equippedIds,
            IReadOnlyList<OwnerTacticCardSnapshot> cards,
            IReadOnlyList<OwnerTacticScheduleRowSnapshot> scheduleRows)
        {
            PresetName = presetName ?? string.Empty;
            EquippedIds = CopyIds(equippedIds);
            Cards = CopyCards(cards);
            ScheduleRows = CopyScheduleRows(scheduleRows);
        }

        public string PresetName { get; }
        public IReadOnlyList<string> EquippedIds { get; }
        public IReadOnlyList<OwnerTacticCardSnapshot> Cards { get; }
        public IReadOnlyList<OwnerTacticScheduleRowSnapshot> ScheduleRows { get; }

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

        private static OwnerTacticScheduleRowSnapshot[] CopyScheduleRows(
            IReadOnlyList<OwnerTacticScheduleRowSnapshot> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var result = new OwnerTacticScheduleRowSnapshot[source.Count];
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
                playersByCard[roster.Entries[index].CardId] = runtime.IdentityRegistry.GetPresentationPlayerName(roster.Entries[index].PlayerPersonId);

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
                candidates[definitionIndex] = new OwnerTeamColorCandidateSnapshot(
                    definition,
                    names.Count,
                    names,
                    isActive,
                    OwnerTeamColorDisplayFormatter.FormatWorldName(
                        definition,
                        definition.DisplayName,
                        franchiseId => OwnerClubDisplayNameFormatter.Format(
                            runtime.IdentityRegistry.GetPresentationFranchiseName(franchiseId),
                            definition.OriginYear),
                        manager.GetClubDisplayName),
                    OwnerTeamColorDisplayFormatter.FormatWorldDescription(
                        definition,
                        definition.Description,
                        franchiseId => OwnerClubDisplayNameFormatter.Format(
                            runtime.IdentityRegistry.GetPresentationFranchiseName(franchiseId),
                            definition.OriginYear),
                        manager.GetClubDisplayName));
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
            return new OwnerTacticsSnapshot(
                preset.Name,
                preset.DefaultTacticCardIds,
                cards,
                BuildTacticScheduleRows(manager, runtime.ManagerMode.LiveSeason, preset));
        }

        private static OwnerTacticScheduleRowSnapshot[] BuildTacticScheduleRows(
            Baseball.Game.Historical.OwnerModeManager manager,
            ManagerLiveSeasonState liveSeason,
            LineupPresetState preset)
        {
            var playerGames = new List<ScheduledGameState>();
            IReadOnlyList<ScheduledGameState> schedule = liveSeason.Schedule.Games;
            for (int index = 0; index < schedule.Count; index++)
                if (schedule[index].IncludesTeam(liveSeason.PlayerTeamId)) playerGames.Add(schedule[index]);

            int nextIndex = playerGames.Count;
            for (int index = 0; index < playerGames.Count; index++)
            {
                if (playerGames[index].IsCompleted) continue;
                nextIndex = index;
                break;
            }

            int start = nextIndex < playerGames.Count
                ? nextIndex
                : Math.Max(0, playerGames.Count - Baseball.Game.Historical.OwnerModeManager.MaximumTacticPlanningGames);
            int count = Math.Min(
                Baseball.Game.Historical.OwnerModeManager.MaximumTacticPlanningGames,
                playerGames.Count - start);
            var rows = new OwnerTacticScheduleRowSnapshot[count];
            for (int index = 0; index < count; index++)
            {
                ScheduledGameState game = playerGames[start + index];
                bool isHome = game.HomeTeamId == liveSeason.PlayerTeamId;
                int opponentId = isHome ? game.AwayTeamId : game.HomeTeamId;
                int teamRuns = isHome ? game.HomeRuns : game.AwayRuns;
                int opponentRuns = isHome ? game.AwayRuns : game.HomeRuns;
                IReadOnlyList<string> equippedIds = game.HasTacticPlan
                    ? game.PlannedTacticCardIds
                    : start + index == nextIndex
                        ? preset.DefaultTacticCardIds
                        : Array.Empty<string>();
                rows[index] = new OwnerTacticScheduleRowSnapshot(
                    game.GameId,
                    game.Round,
                    manager.GetClubDisplayName(liveSeason.GetTeamSeasonKey(opponentId)),
                    isHome,
                    game.IsCompleted,
                    teamRuns,
                    opponentRuns,
                    !game.IsCompleted,
                    equippedIds);
            }
            return rows;
        }

        /// <summary>한 TeamColor가 적용 대상 선수 1명에게 주는 역할별 능력치 효과를 설명한다.</summary>
        public static string DescribeTeamColorEffect(TeamColorDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var builder = new StringBuilder();
            AppendPerPlayerBonus(builder, "타자", PlayerRole.Hitter, definition.HitterBonus);
            builder.Append('\n');
            AppendPerPlayerBonus(builder, "투수", PlayerRole.Pitcher, definition.PitcherBonus);
            builder.Append("\n중첩: ").Append(
                definition.StackPolicy == TeamColorStackPolicy.Stackable
                    ? "동시 적용"
                    : "동일 계열 최고 단계만");
            return builder.ToString();
        }

        /// <summary>저장된 슬롯 가운데 실제 발동 중인 TeamColor의 능력치 효과를 역할별로 합산한다.</summary>
        public static string DescribeActiveTeamColorEffects(
            IReadOnlyList<string> equippedIds,
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> candidates)
        {
            if (equippedIds == null) throw new ArgumentNullException(nameof(equippedIds));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var hitterBonuses = new int[PlayerAbilityCatalog.AbilityCount];
            var pitcherBonuses = new int[PlayerAbilityCatalog.AbilityCount];
            var activeNames = new List<string>(LineupPresetState.TeamColorSlotCount);
            var countedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int slotIndex = 0; slotIndex < equippedIds.Count; slotIndex++)
            {
                string equippedId = equippedIds[slotIndex];
                if (string.IsNullOrEmpty(equippedId) || !countedIds.Add(equippedId))
                    continue;
                OwnerTeamColorCandidateSnapshot candidate = FindActiveTeamColor(candidates, equippedId);
                if (candidate == null)
                    continue;

                activeNames.Add(candidate.Name);
                AddTeamColorBonus(candidate.Definition.HitterBonus, hitterBonuses);
                AddTeamColorBonus(candidate.Definition.PitcherBonus, pitcherBonuses);
            }

            if (activeNames.Count == 0)
                return "현재 활성 효과 없음\n장착 슬롯이 비어 있거나 발동 인원을 충족하지 못했습니다.";

            var builder = new StringBuilder();
            builder.Append("현재 활성 효과 ").Append(activeNames.Count).Append("개 · ");
            for (int index = 0; index < activeNames.Count; index++)
            {
                if (index > 0) builder.Append(" + ");
                builder.Append(activeNames[index]);
            }
            builder.Append('\n');
            AppendRoleBonus(builder, "야수", PlayerRole.Hitter, hitterBonuses);
            builder.Append('\n');
            AppendRoleBonus(builder, "투수", PlayerRole.Pitcher, pitcherBonuses);
            return builder.ToString();
        }

        private static OwnerTeamColorCandidateSnapshot FindActiveTeamColor(
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> candidates,
            string equippedId)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = candidates[index];
                if (candidate.IsActive && string.Equals(candidate.Id, equippedId, StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        private static void AddTeamColorBonus(TeamColorStatBonus source, int[] destination)
        {
            for (int abilityIndex = 0; abilityIndex < destination.Length; abilityIndex++)
                destination[abilityIndex] += source.Get((PlayerAbility)abilityIndex);
        }

        private static void AppendRoleBonus(
            StringBuilder builder,
            string roleName,
            PlayerRole role,
            int[] bonuses)
        {
            if (TryGetUniformRoleBonus(role, bonuses, out int uniformAmount))
            {
                builder.Append(roleName).Append(": 올 스탯 +").Append(uniformAmount);
                return;
            }

            builder.Append(roleName).Append(' ');
            int effectCount = 0;
            for (int abilityIndex = 0; abilityIndex < bonuses.Length; abilityIndex++)
            {
                int amount = bonuses[abilityIndex];
                if (amount == 0)
                    continue;
                if (effectCount > 0) builder.Append(" · ");
                builder.Append(GetAbilityName((PlayerAbility)abilityIndex)).Append(" +").Append(amount);
                effectCount++;
            }
            if (effectCount == 0) builder.Append("효과 없음");
        }

        private static bool TryGetUniformRoleBonus(PlayerRole role, int[] bonuses, out int uniformAmount)
        {
            uniformAmount = 0;
            bool hasRoleAbility = false;
            for (int abilityIndex = 0; abilityIndex < bonuses.Length; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                bool belongsToRole = role == PlayerRole.Hitter
                    ? PlayerAbilityCatalog.IsBatterAbility(ability)
                    : PlayerAbilityCatalog.IsPitcherAbility(ability);
                int amount = bonuses[abilityIndex];
                if (!belongsToRole)
                {
                    if (amount != 0) return false;
                    continue;
                }

                if (!hasRoleAbility)
                {
                    uniformAmount = amount;
                    hasRoleAbility = true;
                    continue;
                }

                if (amount != uniformAmount) return false;
            }

            return hasRoleAbility && uniformAmount > 0;
        }

        private static void AppendPerPlayerBonus(
            StringBuilder builder,
            string roleName,
            PlayerRole role,
            TeamColorStatBonus bonuses)
        {
            builder.Append(roleName).Append(" 1명당 ");
            int uniformAmount = -1;
            bool isUniform = true;
            for (int abilityIndex = 0; abilityIndex < PlayerAbilityCatalog.AbilityCount; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                bool belongsToRole = role == PlayerRole.Hitter
                    ? PlayerAbilityCatalog.IsBatterAbility(ability)
                    : PlayerAbilityCatalog.IsPitcherAbility(ability);
                if (!belongsToRole)
                {
                    if (bonuses.Get(ability) != 0)
                        isUniform = false;
                    continue;
                }

                int amount = bonuses.Get(ability);
                if (uniformAmount < 0)
                    uniformAmount = amount;
                else if (uniformAmount != amount)
                    isUniform = false;
            }

            if (isUniform && uniformAmount > 0)
            {
                builder.Append("전체 능력치 +").Append(uniformAmount);
                return;
            }

            int effectCount = 0;
            for (int abilityIndex = 0; abilityIndex < PlayerAbilityCatalog.AbilityCount; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                int amount = bonuses.Get(ability);
                if (amount == 0)
                    continue;
                if (effectCount > 0) builder.Append(" · ");
                builder.Append(GetAbilityName(ability)).Append(" +").Append(amount);
                effectCount++;
            }
            if (effectCount == 0) builder.Append("효과 없음");
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
            PlayerAbility.Bunt => "번트",
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

    /// <summary>TeamColor 내부 판정 키를 화면 문자열로 사용하지 않고 효과 강도를 등급화한다.</summary>
    public static class OwnerTeamColorDisplayFormatter
    {
        /// <summary>World Identity 표시명으로 내부 Key를 치환한 팀컬러 이름을 만든다.</summary>
        public static string FormatWorldName(
            TeamColorDefinition definition,
            string displayName,
            Func<string, string> franchiseDisplayNameResolver,
            Func<string, string> teamDisplayNameResolver)
        {
            return FormatName(
                definition,
                ReplaceWorldKeys(
                    definition,
                    displayName,
                    franchiseDisplayNameResolver,
                    teamDisplayNameResolver));
        }

        /// <summary>World Identity 표시명으로 내부 Key를 치환한 팀컬러 설명을 만든다.</summary>
        public static string FormatWorldDescription(
            TeamColorDefinition definition,
            string description,
            Func<string, string> franchiseDisplayNameResolver,
            Func<string, string> teamDisplayNameResolver)
        {
            return FormatDescription(
                definition,
                ReplaceWorldKeys(
                    definition,
                    description,
                    franchiseDisplayNameResolver,
                    teamDisplayNameResolver));
        }

        public static string FormatName(TeamColorDefinition definition, string displayName)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string value = ReplaceKnownKeys(definition, displayName);
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value.Trim(), definition.TeamColorId, StringComparison.Ordinal) ||
                value.IndexOf(':') >= 0)
                return GetFamilyName(definition.Family);
            return value.Trim();
        }

        public static string FormatDescription(TeamColorDefinition definition, string description)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string value = ReplaceKnownKeys(definition, description);
            return string.IsNullOrWhiteSpace(value)
                ? "조건을 만족한 선수에게 팀 컬러 효과를 적용합니다."
                : value.Trim();
        }

        public static string FormatGrade(TeamColorDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            int effectTotal = Math.Max(definition.HitterBonus.Total, definition.PitcherBonus.Total);
            if (effectTotal >= 40) return "S";
            if (effectTotal >= 24) return "A";
            if (effectTotal >= 12) return "B";
            return "C";
        }

        private static string ReplaceKnownKeys(TeamColorDefinition definition, string value)
        {
            string result = value ?? string.Empty;
            if (!string.IsNullOrEmpty(definition.TeamColorId))
                result = result.Replace(definition.TeamColorId, string.Empty);
            if (!string.IsNullOrEmpty(definition.OriginFranchiseId))
                result = result.Replace(definition.OriginFranchiseId, "구단");
            if (!string.IsNullOrEmpty(definition.OriginTeamSeasonKey))
                result = result.Replace(definition.OriginTeamSeasonKey, "해당 시즌");
            if (!string.IsNullOrEmpty(definition.UpgradeGroupId))
                result = result.Replace(definition.UpgradeGroupId, string.Empty);
            return result;
        }

        private static string ReplaceWorldKeys(
            TeamColorDefinition definition,
            string value,
            Func<string, string> franchiseDisplayNameResolver,
            Func<string, string> teamDisplayNameResolver)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string result = value ?? string.Empty;
            result = ReplaceWorldKey(
                result,
                definition.OriginFranchiseId,
                franchiseDisplayNameResolver);
            return ReplaceWorldKey(
                result,
                definition.OriginTeamSeasonKey,
                teamDisplayNameResolver);
        }

        private static string ReplaceWorldKey(
            string value,
            string key,
            Func<string, string> displayNameResolver)
        {
            if (string.IsNullOrEmpty(key) || displayNameResolver == null) return value;
            string displayName = displayNameResolver(key);
            return string.IsNullOrWhiteSpace(displayName) ||
                   string.Equals(displayName, key, StringComparison.Ordinal)
                ? value
                : value.Replace(key, displayName);
        }

        private static string GetFamilyName(TeamColorFamily family) => family switch
        {
            TeamColorFamily.YearFranchise => "같은 해의 구단",
            TeamColorFamily.Franchise => "구단의 계보",
            TeamColorFamily.Year => "동시대의 야구",
            TeamColorFamily.AllStar => "올스타 조합",
            TeamColorFamily.GoldenGlove => "수비 수상 조합",
            TeamColorFamily.Mvp => "MVP 조합",
            TeamColorFamily.Generation => "세대 조합",
            TeamColorFamily.CostBand => "선수 구성 조합",
            TeamColorFamily.HitterProfile => "타선 조합",
            TeamColorFamily.PitcherProfile => "투수진 조합",
            TeamColorFamily.RosterComposition => "로스터 조합",
            _ => "팀 컬러"
        };
    }
}

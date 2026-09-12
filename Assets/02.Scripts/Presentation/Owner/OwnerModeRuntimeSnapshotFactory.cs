using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Owner
{
    /// <summary>Game에서 확정된 상태와 Resolver 결과를 A/B/C/D 불변 UI Snapshot으로 투영한다.</summary>
    public sealed partial class OwnerModeRuntimeSnapshotFactory
    {
        /// <summary>진행 중인 구단 Runtime 없이 새 게임 후보의 기본 능력치와 원본 시즌 기록을 표시한다.</summary>
        public static OwnerCollectionCardSnapshot CreateNewGameCard(OwnerModeManager manager, OwnerNewGameFlow flow, string cardId)
        {
            if (!flow.CardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new ArgumentException("새 게임 후보 카드가 없습니다.", nameof(cardId));
            PlayerSeasonDefinition season = flow.CardCatalog.GetPlayerSeason(card);
            manager.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person);
            AbilityRatings abilities = new OwnerCardAbilityResolver(manager.Balance.Growth)
                .ResolvePermanent(season, card, null);
            return new OwnerCollectionCardSnapshot(cardId, season.PlayerPersonId,
                flow.Identities.GetPresentationPlayerName(season.PlayerPersonId), season.OriginYear,
                season.Position, season.Cost, card.Edition, 0, 0, false, false, abilities,
                season.OriginYear + "년 · 카드 시즌", season.PlayerSeasonId,
                season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null,
                person?.Throws, person?.Bats, CreatePitchSnapshots(manager, season, abilities),
                CreateSeasonRecord(flow.WorldHistory, season, season.OriginTeamSeasonKey, season.OriginYear),
                abilityGraphMaximum: manager.Balance.MatchRatingCurve.Caps.HardCap, isOwnedCard: false,
                preferredBattingOrder: card.PreferredBattingOrder, isPositionEvidenceMissing: season.IsPositionEvidenceMissing);
        }

        /// <summary>현재 감독·수석코치·방침과 실제 경기 적용값을 덕아웃 Snapshot으로 만든다.</summary>
        public OwnerDugoutSnapshot CreateDugout(OwnerModeManager manager)
        {
            RequireRuntime(manager);
            return OwnerDugoutPresentationBuilder.Build(manager);
        }

        /// <summary>전체 팀컬러의 발동 진행도와 현재 두 슬롯을 상세 화면용으로 만든다.</summary>
        public OwnerTeamColorSnapshot CreateTeamColor(OwnerModeManager manager)
        {
            RequireRuntime(manager);
            return OwnerDugoutLoadoutPresentationBuilder.BuildTeamColor(manager);
        }

        /// <summary>보유 작전카드의 조건·효과와 현재 두 슬롯을 상세 화면용으로 만든다.</summary>
        public OwnerTacticsSnapshot CreateTactics(OwnerModeManager manager)
        {
            RequireRuntime(manager);
            return OwnerDugoutLoadoutPresentationBuilder.BuildTactics(manager);
        }

        public OwnerHomeSnapshot CreateHome(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            OwnerModeRosterStatus roster = manager.BuildRosterStatus();
            ScheduledGameState game = mode.LiveSeason.NextPlayerGame;
            string nextMatch = game == null ? "시즌 일정 종료" : CreateNextMatchText(manager, mode, game);
            return new OwnerHomeSnapshot(
                mode.LiveSeason.OriginYear + " 시즌",
                $"{mode.LiveSeason.CurrentWeekIndex + 1}주차",
                OwnerLeagueDisplayNameFormatter.FormatFull(runtime.League.Grade),
                FormatTeamDisplayName(manager.GetClubDisplayName(runtime.PlayerTeamSeasonKey), "내 구단"),
                string.Empty,
                nextMatch,
                runtime.Economy.Money,
                runtime.Economy.ScoutingPoints,
                runtime.Economy.DevelopmentPoints,
                runtime.Economy.PityGauge,
                roster.ActiveRosterCount,
                roster.ActiveRosterCapacity,
                roster.HitterCount,
                roster.RequiredHitterCount,
                roster.PitcherCount,
                roster.RequiredPitcherCount,
                roster.ForeignPlayerCount,
                roster.ForeignPlayerLimit,
                runtime.OwnedCards.Count,
                roster.Validation.IsValid,
                roster.Validation.IsValid ? string.Empty : FormatRosterIssue(roster.Validation.Issues[0]),
                roster.Strength,
                roster.Cost,
                game == null ? string.Empty : CreateOpponentStrengthText(manager, mode, game),
                runtime.League.Grade,
                runtime.Economy.ContractArrears);
        }

        /// <summary>현재 1군·선택 프리셋·Resolver 검증을 규칙 재계산 없이 선수단 화면에 투영한다.</summary>
        public OwnerRosterLineupSnapshot CreateRosterLineup(OwnerModeManager manager)
        {
            return CreateRosterLineupInternal(manager, null);
        }

        /// <summary>저장 전 1군 교체 후보를 실제 카드·상태 정보와 함께 선수단 화면에 투영한다.</summary>
        public OwnerRosterLineupSnapshot CreateRosterLineup(
            OwnerModeManager manager,
            OwnerActiveRosterChangePreview rosterChange)
        {
            if (rosterChange == null) throw new ArgumentNullException(nameof(rosterChange));
            return CreateRosterLineupInternal(manager, rosterChange, null);
        }

        /// <summary>1군 교체 Preview에서는 변하지 않은 보유 카드 요약을 재사용한다.</summary>
        public OwnerRosterLineupSnapshot CreateRosterLineup(
            OwnerModeManager manager,
            OwnerActiveRosterChangePreview rosterChange,
            IReadOnlyList<OwnerCollectionCardSnapshot> reusableOwnedPlayers)
        {
            if (rosterChange == null) throw new ArgumentNullException(nameof(rosterChange));
            return CreateRosterLineupInternal(manager, rosterChange, reusableOwnedPlayers);
        }

        private OwnerRosterLineupSnapshot CreateRosterLineupInternal(
            OwnerModeManager manager,
            OwnerActiveRosterChangePreview rosterChange,
            IReadOnlyList<OwnerCollectionCardSnapshot> reusableOwnedPlayers = null)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            CurrentRosterState roster = rosterChange?.Roster ?? runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            TeamSeasonPlayerStatusState statuses = rosterChange?.PlayerStatus ??
                mode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            var players = new OwnerRosterPlayerSnapshot[roster.Entries.Count];
            for (int index = 0; index < players.Length; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!runtime.WorldCardCatalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {entry.CardId} 원본이 없습니다.");
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                TeamSeasonPlayerStatus playerStatus = statuses.GetRequiredPlayer(entry.PlayerPersonId);
                ConditionPresentationTable conditionPresentation = manager.Balance.ConditionChemistry.Presentation;
                ConditionPresentationBand conditionBand = conditionPresentation.GetBand(playerStatus.StoredBaseCondition);
                players[index] = new OwnerRosterPlayerSnapshot(
                    entry.CardId,
                    runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId),
                    season.OriginYear,
                    season.Position,
                    season.PitcherRole,
                    card.Edition,
                    season.Cost,
                    entry.RegistrationType,
                    entry.Role,
                    playerStatus.Availability,
                    playerStatus.StoredBaseCondition,
                    conditionPresentation.GetLevel(playerStatus.StoredBaseCondition),
                    FormatConditionLabel(conditionBand.LabelKey),
                    playerStatus.PitchingWorkload, season.IsPositionEvidenceMissing);
            }

            var ownedPlayers = new OwnerCollectionCardSnapshot[runtime.OwnedCards.Count];
            PerCardBonusMap teamColorBonuses = CreateCurrentTeamColorBonuses(
                manager,
                runtime,
                roster,
                rosterChange?.Preset ?? mode.GetSelectedLineupPreset());
            int availableSkillBlockCount = CountAvailableSkillBlocks(runtime);
            var activeRosterCardIds = new HashSet<string>(StringComparer.Ordinal);
            var teamDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, OwnerCollectionCardSnapshot> reusableByCardId =
                CreateReusableCardMap(reusableOwnedPlayers);
            for (int index = 0; index < roster.Entries.Count; index++)
                activeRosterCardIds.Add(roster.Entries[index].CardId);
            for (int index = 0; index < ownedPlayers.Length; index++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[index];
                if (!activeRosterCardIds.Contains(owned.CardId) &&
                    reusableByCardId != null &&
                    reusableByCardId.TryGetValue(owned.CardId, out OwnerCollectionCardSnapshot reusable))
                {
                    ownedPlayers[index] = reusable;
                    continue;
                }
                if (!runtime.WorldCardCatalog.TryGetCard(owned.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {owned.CardId} 원본이 없습니다.");
                // 선수단 목록은 필터·배치에 필요한 요약만 만든다. 상세 계산은 1군 카드와 상세 팝업 요청에만 수행한다.
                ownedPlayers[index] = activeRosterCardIds.Contains(owned.CardId)
                    ? CreateCollectionCard(
                        manager, runtime, owned, card, teamColorBonuses, availableSkillBlockCount)
                    : CreateRosterCardSummary(manager, runtime, owned, card, teamDisplayNames);
            }

            ManagerPregamePreparation preparation = null;
            string unavailableReason = string.Empty;
            if (mode.LiveSeason.NextPlayerGame == null)
            {
                unavailableReason = "이번 시즌 경기가 모두 끝났습니다.";
            }
            else
            {
                preparation = manager.CurrentPregame ?? manager.PrepareNextGame();
            }

            var presetSnapshots = new OwnerRosterPresetSnapshot[mode.LineupPresets.Count];
            for (int index = 0; index < presetSnapshots.Length; index++)
            {
                bool isSelected = string.Equals(
                    mode.LineupPresets[index].PresetId,
                    mode.SelectedLineupPresetId,
                    StringComparison.Ordinal);
                LineupPresetState preset = rosterChange != null && isSelected
                    ? rosterChange.Preset
                    : mode.LineupPresets[index];
                LineupPresetValidationResult validation = rosterChange != null && isSelected
                    ? rosterChange.Validation
                    : preparation == null
                        ? null
                        : isSelected
                            ? preparation.PresetValidation
                            : manager.ValidateLineupPreset(preset);
                presetSnapshots[index] = new OwnerRosterPresetSnapshot(preset, validation, unavailableReason);
            }

            IReadOnlyList<TeamColorDefinition> teamColors = manager.GetAvailableTeamColors();
            var teamColorCandidates = new OwnerLoadoutCandidateSnapshot[teamColors.Count];
            for (int index = 0; index < teamColorCandidates.Length; index++)
            {
                TeamColorDefinition definition = teamColors[index];
                teamColorCandidates[index] = new OwnerLoadoutCandidateSnapshot(
                    definition.TeamColorId,
                    FormatTeamColor(definition));
            }
            IReadOnlyList<TacticCardDefinition> tactics = manager.GetAvailableTacticCards();
            var tacticCandidates = new OwnerLoadoutCandidateSnapshot[tactics.Count];
            for (int index = 0; index < tacticCandidates.Length; index++)
            {
                TacticCardDefinition tactic = tactics[index];
                int ownedCount = runtime.TacticCollection.GetCount(tactic.CardId);
                tacticCandidates[index] = new OwnerLoadoutCandidateSnapshot(
                    tactic.CardId,
                    $"{tactic.Name} ×{ownedCount}",
                    TacticCardArtwork.GetKey(tactic.Category),
                    ownedCount);
            }

            return new OwnerRosterLineupSnapshot(
                manager.BuildRosterStatus(roster),
                players,
                presetSnapshots,
                mode.SelectedLineupPresetId,
                teamColorCandidates,
                tacticCandidates,
                ownedPlayers);
        }

        private static Dictionary<string, OwnerCollectionCardSnapshot> CreateReusableCardMap(
            IReadOnlyList<OwnerCollectionCardSnapshot> reusableOwnedPlayers)
        {
            if (reusableOwnedPlayers == null) return null;
            var cards = new Dictionary<string, OwnerCollectionCardSnapshot>(
                reusableOwnedPlayers.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < reusableOwnedPlayers.Count; index++)
            {
                OwnerCollectionCardSnapshot card = reusableOwnedPlayers[index];
                if (card != null) cards[card.CardId] = card;
            }
            return cards;
        }

        /// <summary>구단 집계에 필요한 카드 요약만 만들고 능력치·구종·성적 상세 조회는 생략한다.</summary>
        public OwnerCollectionSnapshot CreateCollectionSummary(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            var cards = new OwnerCollectionCardSnapshot[runtime.OwnedCards.Count];
            var teamDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < cards.Length; index++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[index];
                if (!runtime.WorldCardCatalog.TryGetCard(owned.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {owned.CardId} 원본이 없습니다.");
                cards[index] = CreateRosterCardSummary(manager, runtime, owned, card, teamDisplayNames);
            }
            return new OwnerCollectionSnapshot(cards,
                OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock),
                CreateStudySchedulePermission(manager, runtime));
        }

        /// <summary>현재 Save의 OwnedCards와 WorldCardCatalog를 보유 선수 화면 Snapshot으로 투영한다.</summary>
        public OwnerCollectionSnapshot CreateCollection(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            var cards = new OwnerCollectionCardSnapshot[runtime.OwnedCards.Count];
            PerCardBonusMap teamColorBonuses = CreateCurrentTeamColorBonuses(
                manager,
                runtime,
                runtime.GetRoster(runtime.PlayerTeamSeasonKey),
                runtime.ManagerMode.GetSelectedLineupPreset());
            int availableSkillBlockCount = CountAvailableSkillBlocks(runtime);
            for (int index = 0; index < cards.Length; index++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[index];
                if (!runtime.WorldCardCatalog.TryGetCard(owned.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {owned.CardId} 원본이 없습니다.");
                cards[index] = CreateCollectionCard(
                    manager, runtime, owned, card, teamColorBonuses, availableSkillBlockCount);
            }
            return new OwnerCollectionSnapshot(cards,
                OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock),
                CreateStudySchedulePermission(manager, runtime));
        }

        private static OwnerSchedulePermission CreateStudySchedulePermission(OwnerModeManager manager, ManagerHistoricalRuntimeState runtime)
        {
            int minimumWeeks = int.MaxValue;
            foreach (var program in manager.Balance.OwnerCardGrowth.StudyPrograms)
                minimumWeeks = Math.Min(minimumWeeks, program.DurationWeeks);
            return OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.OverseasTraining, minimumWeeks);
        }

        /// <summary>선수단에서 실제로 연 카드만 상세 Snapshot으로 계산한다.</summary>
        public IReadOnlyList<OwnerCollectionCardSnapshot> CreateCollectionCardDetails(
            OwnerModeManager manager,
            IReadOnlyList<string> cardIds)
        {
            if (cardIds == null) throw new ArgumentNullException(nameof(cardIds));
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            var ownedByCardId = new Dictionary<string, OwnedPlayerCardState>(
                runtime.OwnedCards.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < runtime.OwnedCards.Count; index++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[index];
                ownedByCardId[owned.CardId] = owned;
            }

            PerCardBonusMap teamColorBonuses = CreateCurrentTeamColorBonuses(
                manager,
                runtime,
                runtime.GetRoster(runtime.PlayerTeamSeasonKey),
                runtime.ManagerMode.GetSelectedLineupPreset());
            int availableSkillBlockCount = CountAvailableSkillBlocks(runtime);
            var details = new OwnerCollectionCardSnapshot[cardIds.Count];
            for (int index = 0; index < details.Length; index++)
            {
                string cardId = cardIds[index];
                if (!ownedByCardId.TryGetValue(cardId, out OwnedPlayerCardState owned) ||
                    !runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                    throw new ArgumentException($"보유 중인 CardId {cardId} 원본이 없습니다.", nameof(cardIds));
                details[index] = CreateCollectionCard(
                    manager, runtime, owned, card, teamColorBonuses, availableSkillBlockCount);
            }
            return details;
        }

        private static OwnerCollectionCardSnapshot CreateRosterCardSummary(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            OwnedPlayerCardState owned,
            PlayerCardDefinition card,
            IDictionary<string, string> teamDisplayNames)
        {
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            if (!teamDisplayNames.TryGetValue(season.OriginTeamSeasonKey, out string teamDisplayName))
            {
                teamDisplayName = manager.GetTeamIdentityName(season.OriginTeamSeasonKey);
                teamDisplayNames[season.OriginTeamSeasonKey] = teamDisplayName;
            }
            return new OwnerCollectionCardSnapshot(
                owned.CardId,
                season.PlayerPersonId,
                runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                season.OriginYear,
                season.Position,
                season.Cost,
                card.Edition,
                owned.EnhancementLevel,
                owned.DuplicateCount,
                owned.IsLocked,
                owned.IsFavorite,
                  pitcherRole: season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null,
                  isActiveRoster: IsActiveRoster(runtime, owned.CardId),
                  studyStatus: GetStudyStatus(runtime, owned.CardId),
                  growthBadges: OwnerCardGrowthBadgeBuilder.Build(owned, runtime.PlayerGrowth, manager.Balance.Growth, manager.TraitBalance),
                  teamDisplayName: teamDisplayName, preferredBattingOrder: card.PreferredBattingOrder,
                  isPositionEvidenceMissing: season.IsPositionEvidenceMissing,
                  originFranchiseId: season.OriginFranchiseId,
                  franchiseHistoryDisplayName: runtime.IdentityRegistry.GetPresentationFranchiseHistoryName(
                      season.OriginFranchiseId));
        }

        private static OwnerCollectionCardSnapshot CreateCollectionCard(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            OwnedPlayerCardState owned,
            PlayerCardDefinition card,
            PerCardBonusMap teamColorBonuses)
        {
            return CreateCollectionCard(
                manager,
                runtime,
                owned,
                card,
                teamColorBonuses,
                CountAvailableSkillBlocks(runtime));
        }

        private static OwnerCollectionCardSnapshot CreateCollectionCard(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            OwnedPlayerCardState owned,
            PlayerCardDefinition card,
            PerCardBonusMap teamColorBonuses,
            int availableSkillBlockCount)
        {
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            manager.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person);
            var abilityResolver = new OwnerCardAbilityResolver(manager.Balance.Growth);
            AbilityRatings permanent = abilityResolver.ResolvePermanent(season, card, owned);
            var abilityBreakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
            for (int abilityIndex = 0; abilityIndex < abilityBreakdowns.Length; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                OwnerCardAbilityContribution contribution = abilityResolver.ResolveContribution(
                    season, card, owned, ability);
                abilityBreakdowns[abilityIndex] = new OwnerAbilityBreakdownSnapshot(
                    contribution.BaseCard,
                    contribution.Training,
                    contribution.SkillBlock,
                    teamColorBonuses.Get(owned.CardId, ability),
                    contribution.Study,
                    contribution.Enhancement, contribution.Mentoring, contribution.Correction,
                    contribution.Support, contribution.Slogan, contribution.Staff);
            }
            TeamSeasonPlayerStatusState statuses = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            int? condition = statuses.TryGetPlayer(season.PlayerPersonId, out TeamSeasonPlayerStatus playerStatus)
                ? playerStatus.StoredBaseCondition : null;
            if (condition.HasValue)
                foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
                    if (entry.CardId == owned.CardId)
                    {
                        condition = Math.Min(100, condition.Value + OwnerSupportService.GetConditionBonus(runtime, owned.CardId)
                            + ManagerModeMatchService.ResolveHeadCoachConditionBonus(runtime, runtime.PlayerTeamSeasonKey, manager.Balance.ConditionChemistry));
                        break;
                    }
            string conditionLabel = condition.HasValue
                ? FormatConditionLabel(manager.Balance.ConditionChemistry.Presentation.GetBand(condition.Value).LabelKey)
                : "정보 없음";
            return new OwnerCollectionCardSnapshot(
                owned.CardId,
                season.PlayerPersonId,
                        runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                season.OriginYear,
                season.Position,
                season.Cost,
                card.Edition,
                owned.EnhancementLevel,
                owned.DuplicateCount,
                owned.IsLocked,
                owned.IsFavorite,
                permanent,
                OwnerLeagueDisplayNameFormatter.FormatFull(runtime.League.Grade) + " · 현재 시즌",
                season.PlayerSeasonId,
                season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null,
                person?.Throws,
                person?.Bats,
                CreatePitchSnapshots(manager, season, permanent),
                CreateCurrentSeasonRecord(runtime, season, runtime.PlayerTeamSeasonKey),
                GetTrainingBonusTotal(owned),
                owned.SkillBoard.Placements.Count,
                availableSkillBlockCount,
                IsActiveRoster(runtime, owned.CardId),
                GetStudyStatus(runtime, owned.CardId),
                manager.GetTeamIdentityName(season.OriginTeamSeasonKey),
                CreateSkillBlockPlacements(manager.Balance.Growth.SkillBlocks, owned.SkillBoard.Placements),
                condition,
                conditionLabel,
                abilityBreakdowns,
                manager.Balance.MatchRatingCurve.Caps.HardCap, preferredBattingOrder: card.PreferredBattingOrder, isPositionEvidenceMissing: season.IsPositionEvidenceMissing,
                conditionLevel: condition.HasValue ? manager.Balance.ConditionChemistry.Presentation.GetLevel(condition.Value) : (int?)null,
                growthBadges: OwnerCardGrowthBadgeBuilder.Build(owned, runtime.PlayerGrowth, manager.Balance.Growth, manager.TraitBalance),
                originFranchiseId: season.OriginFranchiseId,
                franchiseHistoryDisplayName: runtime.IdentityRegistry.GetPresentationFranchiseHistoryName(
                    season.OriginFranchiseId), growthHistory: OwnerGrowthHistoryFormatter.Format(owned, manager.Balance.Growth));
        }

        private static PerCardBonusMap CreateCurrentTeamColorBonuses(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            LineupPresetState preset)
        {
            IReadOnlyList<TeamColorDefinition> definitions = manager.GetTeamColorCatalog();
            var resolver = new TeamColorResolver();
            IReadOnlyList<TeamColorCandidate> active = resolver.Resolve(
                roster,
                runtime.WorldCardCatalog,
                definitions);
            TeamColorDefinition first = FindTeamColor(definitions, preset.TeamColorIds[0]);
            TeamColorDefinition second = FindTeamColor(definitions, preset.TeamColorIds[1]);
            // 1군 교체 Preview로 기존 팀컬러가 비활성화되면 저장 전 표시를 실제 적용 0으로 유지한다.
            if (!ContainsTeamColor(active, first)) first = null;
            if (!ContainsTeamColor(active, second)) second = null;
            return resolver.ApplyEquipped(
                roster, runtime.WorldCardCatalog, definitions, first, second);
        }

        private static bool ContainsTeamColor(
            IReadOnlyList<TeamColorCandidate> active,
            TeamColorDefinition definition)
        {
            if (definition == null) return false;
            for (int index = 0; index < active.Count; index++)
                if (string.Equals(
                    active[index].Definition.TeamColorId,
                    definition.TeamColorId,
                    StringComparison.Ordinal)) return true;
            return false;
        }

        private static TeamColorDefinition FindTeamColor(
            IReadOnlyList<TeamColorDefinition> definitions,
            string teamColorId)
        {
            if (string.IsNullOrWhiteSpace(teamColorId)) return null;
            for (int index = 0; index < definitions.Count; index++)
                if (string.Equals(definitions[index].TeamColorId, teamColorId, StringComparison.Ordinal))
                    return definitions[index];
            throw new InvalidOperationException($"TeamColor Definition {teamColorId}을 찾을 수 없습니다.");
        }

        private static OwnerSkillBlockPlacementSnapshot[] CreateSkillBlockPlacements(
            IReadOnlyList<SkillBlockDefinition> definitions,
            IReadOnlyList<PlacedSkillBlock> placements)
        {
            var result = new OwnerSkillBlockPlacementSnapshot[placements.Count];
            for (int index = 0; index < placements.Count; index++)
            {
                PlacedSkillBlock placement = placements[index];
                SkillBlockDefinition definition = FindSkillBlockDefinition(
                    definitions, placement.Instance.DefinitionId);
                result[index] = new OwnerSkillBlockPlacementSnapshot(
                    definition.ShapeCells,
                    placement.OriginX,
                    placement.OriginY,
                    placement.RotationQuarterTurns,
                    definition.Rarity);
            }
            return result;
        }

        private static SkillBlockDefinition FindSkillBlockDefinition(
            IReadOnlyList<SkillBlockDefinition> definitions,
            string definitionId)
        {
            for (int index = 0; index < definitions.Count; index++)
                if (string.Equals(definitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return definitions[index];
            throw new InvalidOperationException($"SkillBlock Definition {definitionId}을 찾을 수 없습니다.");
        }

        private static int GetTrainingBonusTotal(OwnedPlayerCardState owned)
        {
            int total = 0;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
                total += owned.Training.GetBonus((PlayerAbility)index);
            return total;
        }

        private static string FormatConditionLabel(string labelKey)
        {
            return labelKey switch
            {
                "condition.worst" => "최악",
                "condition.very_bad" => "매우 나쁨",
                "condition.bad" => "나쁨",
                "condition.somewhat_bad" => "다소 나쁨",
                "condition.normal" => "보통",
                "condition.somewhat_good" => "다소 좋음",
                "condition.good" => "좋음",
                "condition.very_good" => "매우 좋음",
                "condition.excellent" => "최상",
                "condition.peak" => "절정",
                _ => labelKey
            };
        }

        private static int CountAvailableSkillBlocks(ManagerHistoricalRuntimeState runtime)
        {
            int equipped = 0;
            for (int index = 0; index < runtime.OwnedCards.Count; index++)
                equipped += runtime.OwnedCards[index].SkillBoard.Placements.Count;
            return Math.Max(0, runtime.PlayerGrowth.Inventory.Blocks.Count - equipped);
        }

        private static bool IsActiveRoster(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string GetStudyStatus(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            for (int index = 0; index < runtime.PlayerGrowth.StudyProjects.Count; index++)
            {
                CardStudyProjectState project = runtime.PlayerGrowth.StudyProjects[index];
                if (string.Equals(project.CardId, cardId, StringComparison.Ordinal))
                    return $"유학 중 · {project.RemainingWeeks}주 남음";
            }
            return string.Empty;
        }

        private static OwnerPitchCardSnapshot[] CreatePitchSnapshots(
            OwnerModeManager manager,
            PlayerSeasonDefinition season,
            AbilityRatings permanentRatings)
        {
            if (season.PlayerType != PlayerType.Pitcher || season.PitchRepertoire.Count == 0)
                return Array.Empty<OwnerPitchCardSnapshot>();
            AbilityRatings source = season.CreateBaseAttributes();
            var baked = new PitcherAttributes(
                source.Get(PlayerAbility.Stamina), source.Get(PlayerAbility.Velocity),
                source.Get(PlayerAbility.Stuff), source.Get(PlayerAbility.Breaking),
                source.Get(PlayerAbility.Control), source.Get(PlayerAbility.PitcherMental));
            var permanent = new PitcherAttributes(
                permanentRatings.Get(PlayerAbility.Stamina), permanentRatings.Get(PlayerAbility.Velocity),
                permanentRatings.Get(PlayerAbility.Stuff), permanentRatings.Get(PlayerAbility.Breaking),
                permanentRatings.Get(PlayerAbility.Control), permanentRatings.Get(PlayerAbility.PitcherMental));
            PitchArsenalBalance balance = manager.Balance.PitchArsenal;
            var result = new OwnerPitchCardSnapshot[season.PitchRepertoire.Count];
            for (int index = 0; index < result.Length; index++)
            {
                PitchRepertoireEntry entry = season.PitchRepertoire[index];
                double quality = PitchEffectivenessResolver.ResolveStableQuality(
                    entry,
                    permanent,
                    balance,
                    baked,
                    result.Length,
                    index);
                result[index] = new OwnerPitchCardSnapshot(
                    entry.PitchType,
                    balance.Get(entry.PitchType).DisplayName,
                    balance.Grade.GetGrade(quality),
                    PitchEffectivenessResolver.ResolveVelocityKph(entry, permanent.Velocity, balance));
            }
            return result;
        }

        private static OwnerCardRecordFieldSnapshot[] CreateCurrentSeasonRecord(
            ManagerHistoricalRuntimeState runtime, PlayerSeasonDefinition season, string teamSeasonKey)
        {
            PlayerCompetitionStatisticsState record = OwnerSeasonRecordsService.GetCurrentPlayerRecord(
                runtime, teamSeasonKey, season.PlayerSeasonId);
            if (record == null) return Array.Empty<OwnerCardRecordFieldSnapshot>();
            bool pitcher = season.PlayerType == PlayerType.Pitcher;
            if (pitcher ? record.Pitching.Appearances == 0 : record.Batting.PlateAppearances == 0)
                return Array.Empty<OwnerCardRecordFieldSnapshot>();
            CareerRecordMetric[] metrics = pitcher
                ? new[] { CareerRecordMetric.OutsRecorded, CareerRecordMetric.EarnedRunAverage,
                    CareerRecordMetric.PitchingStrikeouts }
                : new[] { CareerRecordMetric.PlateAppearances, CareerRecordMetric.BattingAverage,
                    CareerRecordMetric.Hits, CareerRecordMetric.HomeRuns, CareerRecordMetric.Walks,
                    CareerRecordMetric.BattingStrikeouts, CareerRecordMetric.StolenBases };
            var fields = new OwnerCardRecordFieldSnapshot[metrics.Length];
            for (int index = 0; index < metrics.Length; index++)
            {
                CareerRecordMetric metric = metrics[index];
                fields[index] = new OwnerCardRecordFieldSnapshot(
                    SharedScreens.CareerSharedSnapshotFormatters.FormatMetricLabel(metric),
                    SharedScreens.CareerSharedSnapshotFormatters.FormatMetricValue(
                        metric, LeagueLeaderboardService.GetMetricValue(record, metric)));
            }
            return fields;
        }

        private static OwnerCardRecordFieldSnapshot[] CreateSeasonRecord(
            WorldHistorySnapshot history,
            PlayerSeasonDefinition season,
            string currentTeamSeasonKey,
            int currentSeasonYear)
        {
            SeasonStatistics record = null;
            for (int index = 0; index < history.Statistics.Count; index++)
            {
                SeasonStatistics candidate = history.Statistics[index];
                if (candidate.PlayerSeasonId == season.PlayerSeasonId &&
                    candidate.TeamSeasonKey == currentTeamSeasonKey &&
                    candidate.SeasonYear == currentSeasonYear &&
                    !candidate.IsFirstHalf && !candidate.IsPostseason && !candidate.IsAllStarGame)
                {
                    record = candidate;
                    break;
                }
            }
            if (record == null) return Array.Empty<OwnerCardRecordFieldSnapshot>();
            if (season.PlayerType == PlayerType.Pitcher)
            {
                return new[]
                {
                    new OwnerCardRecordFieldSnapshot("이닝", FormatInnings(record.PitchingOuts)),
                    new OwnerCardRecordFieldSnapshot("평균자책점", record.EarnedRunAverage.ToString("0.00", CultureInfo.InvariantCulture)),
                    new OwnerCardRecordFieldSnapshot("탈삼진", record.PitchingStrikeouts.ToString(CultureInfo.InvariantCulture))
                };
            }
            return new[]
            {
                new OwnerCardRecordFieldSnapshot("타석", record.PlateAppearances.ToString(CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("타율", record.BattingAverage.ToString("0.000", CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("안타", record.Hits.ToString(CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("홈런", record.HomeRuns.ToString(CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("볼넷", record.Walks.ToString(CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("삼진", record.Strikeouts.ToString(CultureInfo.InvariantCulture)),
                new OwnerCardRecordFieldSnapshot("도루", record.StolenBases.ToString(CultureInfo.InvariantCulture))
            };
        }

        private static string FormatInnings(int pitchingOuts) =>
            (pitchingOuts / 3).ToString(CultureInfo.InvariantCulture) + "." +
            (pitchingOuts % 3).ToString(CultureInfo.InvariantCulture);

        public OwnerClubOperationSnapshot CreateClubOperation(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ClubOperationState operation = runtime.ManagerMode.ClubOperation;
            ClubOperationBalanceTable balance = manager.Balance.ClubOperation;
            FacilityType[] types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            var facilities = new OwnerFacilitySnapshot[types.Length];
            for (int index = 0; index < types.Length; index++)
            {
                FacilityType type = types[index];
                FacilityState state = operation.GetFacility(type);
                FacilityLevelDefinition definition = balance.GetFacilityLevel(type, state.Level);
                FacilityUpgradeResult preview = manager.PreviewFacilityUpgrade(type);
                int maximumLevel = GetMaximumFacilityLevel(balance, type, state.Level);
                facilities[index] = new OwnerFacilitySnapshot(
                    type,
                    state.Level,
                    maximumLevel,
                    preview.Status == ClubUpgradeStatus.MaximumLevel ? null : preview.MoneyCost,
                    preview.IsApproved,
                    preview.IsApproved ? string.Empty : FormatUpgradeStatus(preview.Status),
                    definition.WeeklyScoutingPointProduction,
                    definition.ScoutingPointStorageCapacity,
                    definition.WeeklyDevelopmentPointProduction,
                    definition.DevelopmentPointStorageCapacity,
                    definition.ConditionRecoveryEfficiencyModifier,
                    definition.ScoutingConfidenceModifier,
                    definition.TacticResearchEfficiencyModifier,
                    definition.FanShopRevenuePerAttendee,
                    definition.FanShopPopularityRetention);
            }

            StadiumUpgradeResult stadiumPreview = manager.PreviewStadiumUpgrade();
            WeeklyOperationLedger week = operation.CurrentWeek;
            SeasonFinanceSummary season = operation.CurrentSeason;
            int? recentAttendance = week.HomeGames == 0
                ? null
                : checked((int)(week.Attendance / week.HomeGames));
            return new OwnerClubOperationSnapshot(
                operation.Stadium.Level,
                operation.Stadium.Capacity,
                stadiumPreview.Status == ClubUpgradeStatus.MaximumLevel ? null : stadiumPreview.MoneyCost,
                stadiumPreview.IsApproved,
                stadiumPreview.IsApproved ? string.Empty : FormatUpgradeStatus(stadiumPreview.Status),
                operation.FanBase,
                operation.Popularity,
                manager.PreviewNextHomeAttendance(),
                recentAttendance,
                operation.TicketPolicy.PriceTier,
                facilities,
                new OwnerFinanceSnapshot(
                    week.MoneyIncome,
                    week.MoneyExpense,
                    week.ScoutingPointProduction,
                    week.DevelopmentPointProduction,
                    week.HomeGames,
                    week.Attendance),
                new OwnerFinanceSnapshot(
                    season.MoneyIncome,
                    season.MoneyExpense,
                    season.ScoutingPointProduction,
                    season.DevelopmentPointProduction,
                    season.HomeGames,
                    season.Attendance),
                runtime.Economy.ContractArrears);
        }

        public OwnerStaffOfficeSnapshot CreateStaffOffice(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            IReadOnlyList<StaffMarketOffer> offers = manager.GetStaffMarketOffers();
            var snapshots = new OwnerStaffMarketOfferSnapshot[offers.Count];
            for (int index = 0; index < offers.Count; index++)
            {
                StaffMarketOffer offer = offers[index];
                StaffSigningResult signing = manager.PreviewStaffSigning(offer);
                // 자금 부족이어도 서비스가 계산한 교체 비용과 영입 후 효과를 비교할 수 있다.
                StaffSigningResult proposal = signing.IsSuccess ? signing : manager.PreviewStaffSigning(offer, long.MaxValue);
                TeamStaffEffectProfile effects = manager.PreviewStaffEffects(proposal);
                snapshots[index] = new OwnerStaffMarketOfferSnapshot(
                    offer,
                    signing.IsSuccess,
                    signing.IsSuccess ? string.Empty : FormatStaffStatus(signing.Status),
                    proposal.IsSuccess ? FormatStaffEffect(mode.StaffCatalog.Get(offer.StaffId).Role, effects) : "예상 효과 확인 불가",
                    immediateCost: proposal.IsSuccess ? proposal.MoneyCommand?.Amount ?? 0L : (long?)null);
            }
            return new OwnerStaffOfficeSnapshot(
                UiContentStateModel.Ready,
                mode.StaffCatalog,
                mode.StaffContracts,
                mode.StaffAssignment,
                manager.GetStaffEffects(),
                snapshots);
        }

        public OwnerPregameSnapshot CreatePregame(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerPregamePreparation preparation = manager.CurrentPregame ?? manager.PrepareNextGame();
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            ScheduledGameState scheduledGame = preparation.ScheduledGame;
            int ownTeamId = mode.LiveSeason.PlayerTeamId;
            int opponentTeamId = scheduledGame.HomeTeamId == ownTeamId
                ? scheduledGame.AwayTeamId
                : scheduledGame.HomeTeamId;
            LineupPresetState preset = mode.GetSelectedLineupPreset();
            IReadOnlyList<OwnerModeConditionEntry> conditionEntries = manager.BuildConditionEntries();
            var conditions = new Dictionary<string, OwnerModeConditionEntry>(StringComparer.Ordinal);
            for (int index = 0; index < conditionEntries.Count; index++)
                conditions.Add(conditionEntries[index].PlayerPersonId, conditionEntries[index]);

            ConditionPresentationTable presentation = manager.Balance.ConditionChemistry.Presentation;
            var lineup = new OwnerPregamePlayerSnapshot[preset.StartingLineupSlots.Count];
            for (int index = 0; index < lineup.Length; index++)
            {
                LineupPresetSlot slot = preset.StartingLineupSlots[index];
                PlayerSeasonDefinition season = GetPlayerSeason(runtime, slot.CardId);
                OwnerModeConditionEntry condition = conditions[season.PlayerPersonId];
                EffectiveMatchCondition effective = condition.EffectiveCondition;
                lineup[index] = new OwnerPregamePlayerSnapshot(
                    slot.CardId,
                    condition.DisplayName,
                    FormatPosition(slot.Position),
                    FormatConditionLabel(presentation.GetBand(effective.StoredBaseCondition).LabelKey),
                    FormatChemistry(effective.LineupChemistryModifier),
                    condition.IsPitcher ? FormatChemistry(effective.BatteryChemistryModifier) : "해당 없음",
                    FormatConditionLabel(presentation.GetBand(effective.Value).LabelKey));
            }

            var displayTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            AddRosterDisplayNames(runtime, runtime.GetRoster(runtime.PlayerTeamSeasonKey), displayTexts);
            AddRosterDisplayNames(runtime, runtime.GetRoster(preparation.OpponentTeamSeasonKey), displayTexts);
            OwnerOpponentAnalysisData.Populate(manager, preparation, displayTexts);
            PregameCardSet cards = ResolvePregameCards(manager, preparation, preset);
            var tactics = new string[preset.DefaultTacticCardIds.Count];
            for (int index = 0; index < tactics.Length; index++)
                tactics[index] = manager.GetTacticDisplayName(preset.DefaultTacticCardIds[index]);
            string[] selectedTeamColors = CreateSelectedTeamColorTexts(manager, preset);
            var presetSnapshots = new OwnerPregamePresetSnapshot[mode.LineupPresets.Count];
            for (int index = 0; index < presetSnapshots.Length; index++)
            {
                LineupPresetState candidate = mode.LineupPresets[index];
                LineupPresetValidationResult validation = string.Equals(
                        candidate.PresetId,
                        preset.PresetId,
                        StringComparison.Ordinal)
                    ? preparation.PresetValidation
                    : manager.ValidateLineupPreset(candidate);
                presetSnapshots[index] = new OwnerPregamePresetSnapshot(
                    candidate.PresetId,
                    candidate.Name,
                    validation);
            }
            return new OwnerPregameSnapshot(
                UiContentStateModel.Ready,
                CreateNextMatchText(manager, mode, preparation.ScheduledGame),
                FormatTeamDisplayName(manager.GetClubDisplayName(preparation.OpponentTeamSeasonKey), "상대 구단"),
                preparation.ScoutingReport,
                presetSnapshots,
                preset.PresetId,
                lineup,
                selectedTeamColors,
                tactics,
                displayTexts,
                preparation.CanStartGame,
                preparation.CanStartGame ? string.Empty : "선수 배치를 확인한 뒤 다시 시작해 주세요.",
                ownTeamId,
                opponentTeamId,
                cards.OwnStarterCard,
                cards.OwnStarterDetail,
                cards.OpponentStarterCard,
                cards.OpponentStarterDetail,
                cards.RosterCards);
        }

        /// <summary>선발 카드와 상대 분석 표 카드가 같은 팀컬러 보정을 한 번만 계산해 쓰도록 묶는다.</summary>
        private sealed class PregameCardSet
        {
            public PlayerMiniCardModel OwnStarterCard;
            public OwnerCollectionCardSnapshot OwnStarterDetail;
            public PlayerMiniCardModel OpponentStarterCard;
            public OwnerCollectionCardSnapshot OpponentStarterDetail;
            public OwnerPregameRosterCardSnapshot[] RosterCards;
        }

        private PregameCardSet ResolvePregameCards(
            OwnerModeManager manager,
            ManagerPregamePreparation preparation,
            LineupPresetState selectedPlan)
        {
            ManagerHistoricalRuntimeState runtime = manager.Runtime;
            OpponentScoutingReport report = preparation.ScoutingReport;
            var result = new PregameCardSet();
            var rosterCards = new List<OwnerPregameRosterCardSnapshot>();

            IReadOnlyList<string> rotation = preparation.PlanSnapshot?.StarterRotationCardIds;
            int ownRotationIndex = rotation == null || rotation.Count == 0
                ? -1
                : (preparation.ScheduledGame.Round - 1) % rotation.Count;
            string ownCardId = ownRotationIndex >= 0 ? rotation[ownRotationIndex] : null;
            CurrentRosterState ownRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            PerCardBonusMap ownBonuses = CreateCurrentTeamColorBonuses(
                manager,
                runtime,
                ownRoster,
                selectedPlan);
            result.OwnStarterCard = CreatePublicLineupCard(
                manager,
                ownRoster,
                ownCardId,
                FormatRotationOrder(ownRotationIndex),
                "선발");
            result.OwnStarterDetail = CreateLineupDetail(
                manager,
                runtime,
                ownRoster,
                ownCardId,
                runtime.PlayerTeamSeasonKey,
                true,
                ownBonuses);

            // 상대 분석 표에 실제로 나오는 선수만 만든다. 로스터 전원을 만들면 화면 진입 비용만 늘어난다.
            for (int index = 0; index < selectedPlan.StartingLineupSlots.Count; index++)
                AddRosterCard(rosterCards, CreateRosterCard(
                    manager, runtime, ownRoster, runtime.PlayerTeamSeasonKey, true, ownBonuses,
                    selectedPlan.StartingLineupSlots[index].CardId));
            for (int index = 0; index < ownRoster.Entries.Count; index++)
                AddRosterCard(rosterCards, CreateRosterCard(
                    manager, runtime, ownRoster, runtime.PlayerTeamSeasonKey, true, ownBonuses,
                    ownRoster.Entries[index].CardId, pitchersOnly: true));

            CurrentRosterState opponentRoster = runtime.GetRoster(preparation.OpponentTeamSeasonKey);
            PerCardBonusMap opponentBonuses = ManagerModeMatchService.ResolveAiTeamColorBonuses(
                opponentRoster,
                runtime.WorldCardCatalog,
                manager.Balance.TeamColor,
                out _);
            for (int index = 0; index < report.ExpectedLineup.Count; index++)
            {
                ScoutedValue<ExpectedLineupEntry> value = report.ExpectedLineup[index];
                if (!value.HasValue) continue;
                AddRosterCard(rosterCards, CreateRosterCard(
                    manager, runtime, opponentRoster, preparation.OpponentTeamSeasonKey, false, opponentBonuses,
                    value.Value.Player.CardId));
            }
            for (int index = 0; index < report.BullpenReadiness.Count; index++)
            {
                ScoutedValue<BullpenReadinessEntry> value = report.BullpenReadiness[index];
                if (!value.HasValue) continue;
                AddRosterCard(rosterCards, CreateRosterCard(
                    manager, runtime, opponentRoster, preparation.OpponentTeamSeasonKey, false, opponentBonuses,
                    value.Value.Player.CardId));
            }
            result.RosterCards = rosterCards.ToArray();

            if (!report.ProbableStarter.HasValue) return result;

            string opponentCardId = report.ProbableStarter.Value.Player.CardId;
            LineupPresetState opponentPlan = ManagerModeMatchService.CreateRosterRolePlan(opponentRoster, runtime.WorldCardCatalog);
            int opponentRotationIndex = FindCardIndex(opponentPlan.StarterRotationCardIds, opponentCardId);
            result.OpponentStarterCard = CreatePublicLineupCard(
                manager,
                opponentRoster,
                opponentCardId,
                FormatRotationOrder(opponentRotationIndex),
                "선발");
            result.OpponentStarterDetail = CreateLineupDetail(
                manager,
                runtime,
                opponentRoster,
                opponentCardId,
                preparation.OpponentTeamSeasonKey,
                false,
                opponentBonuses);
            return result;
        }

        /// <summary>상대 분석 표 한 행이 우클릭으로 열 카드를 만든다. 원본을 찾지 못하면 null을 돌려준다.</summary>
        private OwnerPregameRosterCardSnapshot CreateRosterCard(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            string teamSeasonKey,
            bool isOwnTeam,
            PerCardBonusMap teamColorBonuses,
            string cardId,
            bool pitchersOnly = false)
        {
            if (string.IsNullOrWhiteSpace(cardId)) return null;
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card)) return null;
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            bool isPitcher = season.PlayerType == PlayerType.Pitcher;
            if (pitchersOnly && !isPitcher) return null;
            return new OwnerPregameRosterCardSnapshot(
                cardId,
                runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                isPitcher ? "투수" : OwnerCollectionPresentationBuilder.FormatPosition(season.Position, season.IsPositionEvidenceMissing),
                isOwnTeam,
                isPitcher,
                CreateLineupDetail(manager, runtime, roster, cardId, teamSeasonKey, isOwnTeam, teamColorBonuses));
        }

        private static void AddRosterCard(
            List<OwnerPregameRosterCardSnapshot> output,
            OwnerPregameRosterCardSnapshot card)
        {
            if (card == null) return;
            for (int index = 0; index < output.Count; index++)
                if (string.Equals(output[index].CardId, card.CardId, StringComparison.Ordinal)) return;
            output.Add(card);
        }

        private static int FindCardIndex(IReadOnlyList<string> cardIds, string cardId)
        {
            if (cardIds == null || string.IsNullOrWhiteSpace(cardId)) return -1;
            for (int index = 0; index < cardIds.Count; index++)
                if (string.Equals(cardIds[index], cardId, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static string FormatRotationOrder(int index) => index >= 0 ? (index + 1) + "선발" : "선발";

        public IReadOnlyList<OwnerConditionPlayerSnapshot> CreateConditionChemistry(OwnerModeManager manager)
        {
            IReadOnlyList<OwnerModeConditionEntry> entries = manager.BuildConditionEntries();
            var ratingResolver = new MatchConditionRatingResolver(manager.Balance.ConditionChemistry);
            var result = new OwnerConditionPlayerSnapshot[entries.Count];
            for (int index = 0; index < result.Length; index++)
            {
                OwnerModeConditionEntry entry = entries[index];
                result[index] = new OwnerConditionPlayerSnapshot(
                    entry.PlayerPersonId,
                    entry.DisplayName,
                    OwnerCollectionPresentationBuilder.FormatPosition(entry.NaturalPosition, entry.IsPositionEvidenceMissing),
                    entry.IsPitcher,
                    entry.Availability,
                    entry.EffectiveCondition,
                    ratingResolver.ResolveRatingModifier(entry.EffectiveCondition.Value));
            }
            return result;
        }

        private static ManagerHistoricalRuntimeState RequireRuntime(OwnerModeManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.Runtime ?? throw new InvalidOperationException("활성 구단주 운영 데이터가 없습니다.");
        }

        private static string CreateOpponentStrengthText(
            OwnerModeManager manager,
            ManagerModeRuntimeState mode,
            ScheduledGameState game)
        {
            int opponentId = game.AwayTeamId == mode.LiveSeason.PlayerTeamId ? game.HomeTeamId : game.AwayTeamId;
            string opponentKey = mode.LiveSeason.GetTeamSeasonKey(opponentId);
            return "상대 " + OwnerRosterEvaluationFormatter.FormatStrength(manager.BuildTeamStrength(opponentKey));
        }

        private static string CreateNextMatchText(
            OwnerModeManager manager,
            ManagerModeRuntimeState mode,
            ScheduledGameState game)
        {
            int opponentId = game.AwayTeamId == mode.LiveSeason.PlayerTeamId
                ? game.HomeTeamId
                : game.AwayTeamId;
            string opponentKey = mode.LiveSeason.GetTeamSeasonKey(opponentId);
            bool isHome = game.HomeTeamId == mode.LiveSeason.PlayerTeamId;
            string opponentName = FormatTeamDisplayName(
                manager.GetClubDisplayName(opponentKey),
                "상대 구단");
            return $"{game.Round}라운드 · {(isHome ? "홈" : "원정")} · {opponentName}";
        }

        /// <summary>구단명이 비어 있을 때만 호출부가 정한 대체 이름으로 바꾼다.</summary>
        public static string FormatTeamDisplayName(string teamName, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(teamName))
                return teamName.Trim();

            return string.IsNullOrWhiteSpace(fallback) ? "구단" : fallback;
        }

        private static PlayerSeasonDefinition GetPlayerSeason(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException($"CardId {cardId} 원본이 없습니다.");
            return runtime.WorldCardCatalog.GetPlayerSeason(card);
        }

        private static void AddRosterDisplayNames(
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            IDictionary<string, string> output)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                output[entry.CardId] = runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId);
            }
        }

        private static int GetMaximumFacilityLevel(
            ClubOperationBalanceTable balance,
            FacilityType type,
            int currentLevel)
        {
            int maximum = currentLevel;
            while (balance.TryGetNextFacilityLevel(type, maximum, out FacilityLevelDefinition next))
                maximum = next.Level;
            return maximum;
        }

        private static string FormatUpgradeStatus(ClubUpgradeStatus status)
        {
            return status switch
            {
                ClubUpgradeStatus.MaximumLevel => "최대 레벨",
                ClubUpgradeStatus.InsufficientMoney => "자금 부족",
                ClubUpgradeStatus.LeagueGradeLocked => "리그 등급 조건 미달",
                ClubUpgradeStatus.FanBaseLocked => "팬 기반 조건 미달",
                ClubUpgradeStatus.SeasonAttendanceLocked => "누적 관중 조건 미달",
                ClubUpgradeStatus.AlreadyApplied => "이미 반영됨",
                _ => "현재 상태에서 업그레이드 불가"
            };
        }

        private static string FormatStaffStatus(StaffServiceStatus status)
        {
            return status switch
            {
                StaffServiceStatus.InsufficientMoney => "자금 부족",
                StaffServiceStatus.StaffUnavailable => "해당 코칭스태프는 이미 계약 중입니다.",
                StaffServiceStatus.SalaryNotSettled => "기존 급여 정산 필요",
                _ => "현재 계약 불가"
            };
        }

        private static string FormatStaffEffect(StaffRole role, TeamStaffEffectProfile effects)
        {
            return role switch
            {
                StaffRole.HittingCoach => $"타자 훈련 효율 {(effects.HittingTrainingEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.PitchingCoach => $"투수 훈련 효율 {(effects.PitchingTrainingEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.DevelopmentCoach => $"육성 포인트 사용 효율 {(effects.DevelopmentPointEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.ConditioningCoach => $"회복 효율 {(effects.ConditionRecoveryEfficiency - 1d):+0%;-0%;0%}",
                _ => $"상대 분석 신뢰도 {effects.ScoutingConfidenceModifier:+0%;-0%;0%}"
            };
        }

        private static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                _ => "포지션 확인 필요"
            };
        }

        private static string FormatChemistry(int modifier)
        {
            if (modifier >= 2) return "매우 좋음";
            if (modifier == 1) return "좋음";
            if (modifier == 0) return "보통";
            if (modifier == -1) return "다소 불안";
            return "불안";
        }

        private static string FormatTeamColor(TeamColorDefinition definition)
        {
            string family = definition.Family switch
            {
                TeamColorFamily.YearFranchise => "연도·구단",
                TeamColorFamily.Franchise => "구단",
                TeamColorFamily.Year => "연도",
                TeamColorFamily.AllStar => "올스타",
                TeamColorFamily.GoldenGlove => "골든글러브",
                TeamColorFamily.Mvp => "MVP",
                _ => "팀컬러"
            };
            return $"{family} {definition.RequiredCount}명 · 효과 {definition.StrengthScore}";
        }

        private static string[] CreateSelectedTeamColorTexts(
            OwnerModeManager manager,
            LineupPresetState preset)
        {
            IReadOnlyList<TeamColorDefinition> candidates = manager.GetAvailableTeamColors();
            var result = new string[LineupPresetState.TeamColorSlotCount];
            for (int slotIndex = 0; slotIndex < result.Length; slotIndex++)
            {
                string selectedId = preset.TeamColorIds[slotIndex];
                result[slotIndex] = "선택 없음";
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    if (!string.Equals(candidates[candidateIndex].TeamColorId, selectedId,
                            StringComparison.Ordinal))
                        continue;
                    result[slotIndex] = FormatTeamColor(candidates[candidateIndex]);
                    break;
                }
                if (!string.IsNullOrEmpty(selectedId) && result[slotIndex] == "선택 없음")
                    result[slotIndex] = $"{selectedId} · 사용 불가";
            }
            return result;
        }

        private static string FormatModifier(int value) => value > 0 ? "+" + value : value.ToString();

        private static string FormatRosterIssue(RosterValidationIssue issue)
        {
            string label = issue.Code switch
            {
                RosterValidationIssueCode.TotalCount => "1군 총원",
                RosterValidationIssueCode.HitterCount => "야수 인원",
                RosterValidationIssueCode.StartingHitterCount => "주전 야수 인원",
                RosterValidationIssueCode.BenchHitterCount => "벤치 인원",
                RosterValidationIssueCode.PitcherCount => "투수 인원",
                RosterValidationIssueCode.StartingPitcherCount => "선발투수 인원",
                RosterValidationIssueCode.BullpenPitcherCount => "불펜 인원",
                RosterValidationIssueCode.SetupPitcherCount => "셋업 투수 인원",
                RosterValidationIssueCode.CloserPitcherCount => "마무리 투수 인원",
                RosterValidationIssueCode.ForeignPlayerCount => "외국인 등록",
                RosterValidationIssueCode.SpecialCardCount => "레전드·커리어 하이 카드",
                RosterValidationIssueCode.SpecialHitterCardCount => "레전드·커리어 하이 타자",
                RosterValidationIssueCode.SpecialPitcherCardCount => "레전드·커리어 하이 투수",
                RosterValidationIssueCode.DuplicatePlayerPersonId => "동일 선수 중복",
                RosterValidationIssueCode.FixedRoleCount => "고정 역할 인원",
                _ => "로스터 구성"
            };
            return $"{label}: 필요 {issue.Expected}, 현재 {issue.Actual}";
        }
    }

    /// <summary>Owner 모드의 내부 리그 등급을 사용자 표시용 한글명으로 변환한다.</summary>
    internal static class OwnerLeagueDisplayNameFormatter
    {
        public static string FormatFull(LeagueGrade grade) => FormatShort(grade) + " 리그";

        private static string FormatShort(LeagueGrade grade) => grade switch
        {
            LeagueGrade.Rookie => "루키",
            LeagueGrade.Minor => "마이너",
            LeagueGrade.Major => "메이저",
            LeagueGrade.World => "월드",
            LeagueGrade.AllStar => "올스타",
            LeagueGrade.Classic => "클래식",
            LeagueGrade.Winners => "위너스",
            LeagueGrade.Champion => "챔피언",
            LeagueGrade.Master => "마스터",
            LeagueGrade.Galaxy => "갤럭시",
            _ => "알 수 없는"
        };
    }
}

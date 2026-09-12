using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>공개 로스터와 기용 순서를 읽기 전용 카드 보드에 전달한다. 상대의 숨은 컨디션은 포함하지 않는다.</summary>
    public sealed class OwnerTeamLineupSnapshot
    {
        public string TeamName { get; }
        public string LineupName { get; }
        public string CostLabel { get; }
        public IReadOnlyList<PlayerMiniCardModel> Hitters { get; }
        public IReadOnlyList<PlayerMiniCardModel> Pitchers { get; }
        public IReadOnlyList<OwnerCollectionCardSnapshot> HitterDetails { get; }
        public IReadOnlyList<OwnerCollectionCardSnapshot> PitcherDetails { get; }
        public IReadOnlyList<string> TeamColors { get; }
        public IReadOnlyList<OwnerTeamColorCandidateSnapshot> TeamColorCards { get; }

        /// <summary>원본 목록을 복사하여 화면 밖의 변경과 분리한다.</summary>
        public OwnerTeamLineupSnapshot(string teamName, string lineupName, string costLabel,
            IReadOnlyList<PlayerMiniCardModel> hitters, IReadOnlyList<PlayerMiniCardModel> pitchers,
            IReadOnlyList<string> teamColors,
            IReadOnlyList<OwnerCollectionCardSnapshot> hitterDetails = null,
            IReadOnlyList<OwnerCollectionCardSnapshot> pitcherDetails = null,
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> teamColorCards = null)
        {
            TeamName = teamName ?? string.Empty;
            LineupName = lineupName ?? string.Empty;
            CostLabel = costLabel ?? string.Empty;
            Hitters = Array.AsReadOnly(new List<PlayerMiniCardModel>(hitters).ToArray());
            Pitchers = Array.AsReadOnly(new List<PlayerMiniCardModel>(pitchers).ToArray());
            HitterDetails = CopyDetails(hitterDetails, Hitters.Count, nameof(hitterDetails));
            PitcherDetails = CopyDetails(pitcherDetails, Pitchers.Count, nameof(pitcherDetails));
            TeamColors = Array.AsReadOnly(new List<string>(teamColors).ToArray());
            TeamColorCards = CopyTeamColorCards(teamColorCards, TeamColors.Count);
        }

        private static IReadOnlyList<OwnerCollectionCardSnapshot> CopyDetails(
            IReadOnlyList<OwnerCollectionCardSnapshot> source,
            int expectedCount,
            string parameterName)
        {
            if (source == null)
                return Array.AsReadOnly(new OwnerCollectionCardSnapshot[expectedCount]);
            if (source.Count != expectedCount)
                throw new ArgumentException("카드 표시와 상세 정보의 수가 같아야 합니다.", parameterName);
            return Array.AsReadOnly(new List<OwnerCollectionCardSnapshot>(source).ToArray());
        }

        private static IReadOnlyList<OwnerTeamColorCandidateSnapshot> CopyTeamColorCards(
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> source,
            int expectedCount)
        {
            if (source == null)
                return Array.AsReadOnly(new OwnerTeamColorCandidateSnapshot[expectedCount]);
            if (source.Count != expectedCount)
                throw new ArgumentException("팀컬러 문구와 카드 정보의 수가 같아야 합니다.", nameof(source));
            return Array.AsReadOnly(new List<OwnerTeamColorCandidateSnapshot>(source).ToArray());
        }
    }

    public sealed partial class OwnerModeRuntimeSnapshotFactory
    {
        /// <summary>경기 준비와 리그 순위표에서 같은 구단 공개 라인업을 조회한다.</summary>
        public OwnerTeamLineupSnapshot CreateTeamLineup(OwnerModeManager manager, string teamSeasonKey)
        {
            var runtime = RequireRuntime(manager);
            var roster = runtime.GetRoster(teamSeasonKey);
            bool isOwnTeam = string.Equals(teamSeasonKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal);
            var plan = isOwnTeam ? runtime.ManagerMode.GetSelectedLineupPreset() :
                ManagerModeMatchService.CreateRosterRolePlan(roster, runtime.WorldCardCatalog);
            TeamColorDefinition[] aiTeamColors = null;
            PerCardBonusMap teamColorBonuses = isOwnTeam
                ? CreateCurrentTeamColorBonuses(manager, runtime, roster, plan)
                : ManagerModeMatchService.ResolveAiTeamColorBonuses(
                    roster, runtime.WorldCardCatalog, manager.Balance.TeamColor, out aiTeamColors);
            var hitters = new List<PlayerMiniCardModel>();
            var pitchers = new List<PlayerMiniCardModel>();
            var hitterDetails = new List<OwnerCollectionCardSnapshot>();
            var pitcherDetails = new List<OwnerCollectionCardSnapshot>();
            for (int i = 0; i < plan.BattingOrderCardIds.Count; i++)
            {
                string id = plan.BattingOrderCardIds[i];
                string position = "야수";
                foreach (var slot in plan.StartingLineupSlots)
                    if (string.Equals(slot.CardId, id, StringComparison.Ordinal)) position = FormatPosition(slot.Position);
                hitters.Add(CreatePublicLineupCard(manager, roster, id, (i + 1) + "번", position));
                hitterDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, id, teamSeasonKey, isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.BenchPriorityCardIds.Count; i++)
            {
                hitters.Add(CreatePublicLineupCard(manager, roster, plan.BenchPriorityCardIds[i], (i + 1) + "번", "벤치"));
                hitterDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.BenchPriorityCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.StarterRotationCardIds.Count; i++)
            {
                pitchers.Add(CreatePublicLineupCard(manager, roster, plan.StarterRotationCardIds[i], (i + 1) + "선발", "선발"));
                pitcherDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.StarterRotationCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.BullpenAssignmentCardIds.Count; i++)
            {
                pitchers.Add(CreatePublicLineupCard(manager, roster, plan.BullpenAssignmentCardIds[i], (i + 1) + "번", "중계"));
                pitcherDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.BullpenAssignmentCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            pitchers.Add(CreatePublicLineupCard(manager, roster, plan.SetupPitcherCardId, "셋업", "셋업"));
            pitcherDetails.Add(CreateLineupDetail(
                manager, runtime, roster, plan.SetupPitcherCardId, teamSeasonKey,
                isOwnTeam, teamColorBonuses));
            pitchers.Add(CreatePublicLineupCard(manager, roster, plan.CloserPitcherCardId, "마무리", "마무리"));
            pitcherDetails.Add(CreateLineupDetail(
                manager, runtime, roster, plan.CloserPitcherCardId, teamSeasonKey,
                isOwnTeam, teamColorBonuses));
            var cost = new RosterCostResolver().Resolve(roster, runtime.WorldCardCatalog);
            OwnerTeamColorCandidateSnapshot[] teamColorCards = isOwnTeam
                ? CreateSelectedTeamColorCards(manager, plan)
                : CreateAiTeamColorCards(manager, runtime, roster, aiTeamColors);
            string[] colors = CreateLineupTeamColorTexts(plan.TeamColorIds, teamColorCards, isOwnTeam);
            return new OwnerTeamLineupSnapshot(manager.GetClubDisplayName(teamSeasonKey),
                isOwnTeam ? plan.Name : "공개 등록 기준 라인업", OwnerRosterEvaluationFormatter.FormatCost(cost),
                hitters, pitchers, colors, hitterDetails, pitcherDetails, teamColorCards);
        }

        /// <summary>보강된 경기 입력의 실제 타순과 25인 편성을 공개 카드 보드로 변환한다.</summary>
        public OwnerTeamLineupSnapshot CreatePracticeLineup(OwnerModeManager manager, string challengeId,
            Baseball.Simulation.Match.MatchRosterSnapshot[] opponents, TeamColorDefinition[] selectedColors)
        {
            var runtime = RequireRuntime(manager);
            var team = manager.GetPracticeTeam(challengeId);
            var cards = manager.GetPracticeCards(challengeId);
            var entries = new ActiveRosterEntry[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                var season = manager.GetPracticePlayerSeason(cards[i]);
                var role = i < 9 ? (ActiveRosterRole)i : i < 14 ? ActiveRosterRole.BenchHitter :
                    (ActiveRosterRole)((int)ActiveRosterRole.StartingPitcher1 + i - 14);
                entries[i] = new ActiveRosterEntry(cards[i].CardId, season.PlayerSeasonId,
                    season.PlayerPersonId, season.RegistrationType, role);
            }
            var roster = new CurrentRosterState(team.TeamSeasonKey, entries);
            var bonuses = ManagerModeMatchService.ResolveAiTeamColorBonuses(
                roster, runtime.WorldCardCatalog, manager.Balance.TeamColor, out _);
            var hitters = new List<PlayerMiniCardModel>();
            var pitchers = new List<PlayerMiniCardModel>();
            var hitterDetails = new List<OwnerCollectionCardSnapshot>();
            var pitcherDetails = new List<OwnerCollectionCardSnapshot>();
            void Add(Baseball.Core.Players.Player player, string order, string role, bool pitcher)
            {
                // 경기 입력의 선수 번호는 SelectCards 순서에 대응하며 이름으로 선수를 매칭하지 않는다.
                string cardId = cards[player.PlayerId - OwnerModeManager.PracticePlayerIdBase - 1].CardId;
                var development = manager.GetPracticeCardDevelopment(challengeId,
                    cards[player.PlayerId - OwnerModeManager.PracticePlayerIdBase - 1]);
                (pitcher ? pitchers : hitters).Add(CreatePublicLineupCard(manager, roster, cardId, order, role, false, development));
                (pitcher ? pitcherDetails : hitterDetails).Add(CreateLineupDetail(
                    manager, runtime, roster, cardId, team.TeamSeasonKey, false, bonuses, true, development));
            }
            var opponent = opponents[0];
            for (int i = 0; i < opponent.StartingLineup.Count; i++)
                Add(opponent.StartingLineup[i].Player, (i + 1) + "번", FormatPosition(opponent.StartingLineup[i].FieldingPosition), false);
            for (int i = 0; i < opponent.Bench.Count; i++) Add(opponent.Bench[i], (i + 1) + "번", "벤치", false);
            for (int i = 0; i < opponents.Length; i++) Add(opponents[i].StartingPitcher.Player, (i + 1) + "선발", "선발", true);
            for (int i = 0; i < opponent.Bullpen.Count; i++)
            {
                string role = i < 4 ? "중계" : i == 4 ? "셋업" : "마무리";
                Add(opponent.Bullpen[i].Player, i < 4 ? (i + 1) + "번" : role, role, true);
            }
            var colorCards = CreateAiTeamColorCards(manager, runtime, roster, selectedColors);
            return new OwnerTeamLineupSnapshot(manager.GetTeamIdentityDisplayName(team.TeamSeasonKey),
                "역대 강팀 도전 라인업", OwnerRosterEvaluationFormatter.FormatCost(new RosterCostResolver().Resolve(roster, runtime.WorldCardCatalog)),
                hitters, pitchers, CreateLineupTeamColorTexts(null, colorCards, false), hitterDetails, pitcherDetails, colorCards);
        }

        private OwnerTeamColorCandidateSnapshot[] CreateSelectedTeamColorCards(
            OwnerModeManager manager,
            LineupPresetState preset)
        {
            OwnerTeamColorSnapshot teamColors = CreateTeamColor(manager);
            var result = new OwnerTeamColorCandidateSnapshot[LineupPresetState.TeamColorSlotCount];
            for (int slotIndex = 0; slotIndex < result.Length; slotIndex++)
            {
                string selectedId = preset.TeamColorIds[slotIndex];
                for (int candidateIndex = 0; candidateIndex < teamColors.Candidates.Count; candidateIndex++)
                {
                    OwnerTeamColorCandidateSnapshot candidate = teamColors.Candidates[candidateIndex];
                    if (!string.Equals(candidate.Id, selectedId, StringComparison.Ordinal)) continue;
                    result[slotIndex] = candidate;
                    break;
                }
            }
            return result;
        }

        private static OwnerTeamColorCandidateSnapshot[] CreateAiTeamColorCards(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            IReadOnlyList<TeamColorDefinition> selected)
        {
            var result = new OwnerTeamColorCandidateSnapshot[LineupPresetState.TeamColorSlotCount];
            IReadOnlyList<TeamColorRosterCard> rosterCards = TeamColorResolver.CreateRosterCards(
                roster,
                runtime.WorldCardCatalog);
            int selectedCount = Math.Min(selected?.Count ?? 0, result.Length);
            for (int index = 0; index < selectedCount; index++)
            {
                TeamColorDefinition definition = selected[index];
                if (definition == null) continue;
                string ResolveFranchiseName(string franchiseId)
                {
                    // 연도 팀컬러는 현재 브랜드가 아니라 해당 시즌의 구단명을 사용한다.
                    for (int cardIndex = 0; cardIndex < rosterCards.Count; cardIndex++)
                    {
                        var origin = rosterCards[cardIndex].Eligibility;
                        if (origin.OriginYear == definition.OriginYear && origin.OriginFranchiseId == franchiseId)
                            return manager.GetTeamIdentityName(origin.OriginTeamSeasonKey);
                    }
                    return runtime.IdentityRegistry.GetPresentationFranchiseName(franchiseId);
                }
                int eligibleCount = 0;
                for (int cardIndex = 0; cardIndex < rosterCards.Count; cardIndex++)
                    if (definition.IsEligible(rosterCards[cardIndex])) eligibleCount++;
                result[index] = new OwnerTeamColorCandidateSnapshot(
                    definition,
                    eligibleCount,
                    Array.Empty<string>(),
                    true,
                    OwnerTeamColorDisplayFormatter.FormatWorldName(
                        definition,
                        definition.DisplayName,
                        ResolveFranchiseName,
                        manager.GetTeamIdentityName),
                    OwnerTeamColorDisplayFormatter.FormatWorldDescription(
                        definition,
                        definition.Description,
                        ResolveFranchiseName,
                        manager.GetTeamIdentityName));
            }
            return result;
        }

        private static string[] CreateLineupTeamColorTexts(
            IReadOnlyList<string> selectedIds,
            IReadOnlyList<OwnerTeamColorCandidateSnapshot> cards,
            bool isOwnTeam)
        {
            var result = new string[LineupPresetState.TeamColorSlotCount];
            for (int index = 0; index < result.Length; index++)
            {
                if (cards[index] != null)
                    result[index] = cards[index].Name;
                else if (isOwnTeam && !string.IsNullOrEmpty(selectedIds[index]))
                    result[index] = "사용할 수 없는 팀컬러";
                else
                    result[index] = isOwnTeam ? "선택 없음" : "팀컬러 적용 없음";
            }
            return result;
        }

        private static PlayerMiniCardModel CreatePublicLineupCard(OwnerModeManager manager,
            CurrentRosterState roster, string cardId, string order, string role, bool showOwnedGrowth = true,
            OwnedPlayerCardState development = null)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            var runtime = manager.Runtime;
            ActiveRosterEntry entry = null;
            foreach (var candidate in roster.Entries)
                if (string.Equals(candidate.CardId, cardId, StringComparison.Ordinal)) { entry = candidate; break; }
            if (entry == null || !runtime.WorldCardCatalog.TryGetCard(cardId, out var definition)) return null;
            var season = runtime.WorldCardCatalog.GetPlayerSeason(definition);
            return new PlayerMiniCardModel(cardId, runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId),
                order, (season.OriginYear % 100).ToString("00"), "C " + season.Cost,
                development == null ? string.Empty : "+" + development.EnhancementLevel, role, portraitAssetKey: season.PlayerSeasonId, teamAccentHex: "#B1A858", isInteractable: false, frameEdition: definition.Edition, cost: season.Cost,
                growthBadges: development != null ? OwnerCardGrowthBadgeBuilder.Build(development, null, manager.Balance.Growth, manager.TraitBalance)
                    : showOwnedGrowth && roster.TeamSeasonKey == runtime.PlayerTeamSeasonKey
                    ? OwnerCardGrowthBadgeBuilder.Build(runtime, cardId, manager.Balance.Growth, manager.TraitBalance) : PlayerCardGrowthBadgeModel.Empty);
        }

        private static OwnerCollectionCardSnapshot CreateLineupDetail(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            string cardId,
            string currentTeamSeasonKey,
            bool isOwnTeam,
            PerCardBonusMap teamColorBonuses,
            bool isPractice = false, OwnedPlayerCardState development = null)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            ActiveRosterEntry entry = null;
            foreach (ActiveRosterEntry candidate in roster.Entries)
                if (string.Equals(candidate.CardId, cardId, StringComparison.Ordinal)) { entry = candidate; break; }
            if (entry == null || !runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                return null;
            if (isOwnTeam && runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned))
                return CreateCollectionCard(manager, runtime, owned, card, teamColorBonuses);

            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            manager.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person);
            var abilityResolver = new OwnerCardAbilityResolver(manager.Balance.Growth);
            AbilityRatings abilities = abilityResolver.ResolvePermanent(season, card, development);
            var abilityBreakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
            for (int abilityIndex = 0; abilityIndex < abilityBreakdowns.Length; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                OwnerCardAbilityContribution contribution = abilityResolver.ResolveContribution(
                    season, card, development, ability);
                abilityBreakdowns[abilityIndex] = new OwnerAbilityBreakdownSnapshot(
                    contribution.BaseCard,
                    contribution.Training,
                    contribution.SkillBlock,
                    teamColorBonuses.Get(cardId, ability),
                    contribution.Study,
                    contribution.Enhancement);
            }
            return new OwnerCollectionCardSnapshot(
                cardId,
                season.PlayerPersonId,
                runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId),
                season.OriginYear,
                season.Position,
                season.Cost,
                card.Edition,
                development?.EnhancementLevel ?? 0,
                0,
                false,
                false,
                abilities,
                isPractice ? "역대 강팀 도전 선수" : OwnerLeagueDisplayNameFormatter.FormatFull(runtime.League.Grade) + " · 현재 시즌",
                season.PlayerSeasonId,
                season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null,
                person?.Throws,
                person?.Bats,
                CreatePitchSnapshots(manager, season, abilities),
                isPractice ? null : CreateCurrentSeasonRecord(runtime, season, currentTeamSeasonKey),
                teamDisplayName: manager.GetTeamIdentityName(season.OriginTeamSeasonKey),
                conditionLabel: "비공개",
                placedSkillBlockCount: development?.SkillBoard.Placements.Count ?? 0,
                skillBlockPlacements: development == null ? null : CreateSkillBlockPlacements(manager, development),
                studyStatus: development == null ? "" : "유학 완료",
                growthBadges: OwnerCardGrowthBadgeBuilder.Build(development, null, manager.Balance.Growth, manager.TraitBalance),
                abilityBreakdowns: abilityBreakdowns,
                abilityGraphMaximum: manager.Balance.MatchRatingCurve.Caps.HardCap,
                isOwnedCard: false, preferredBattingOrder: card.PreferredBattingOrder, isPositionEvidenceMissing: season.IsPositionEvidenceMissing);
        }
    }
}

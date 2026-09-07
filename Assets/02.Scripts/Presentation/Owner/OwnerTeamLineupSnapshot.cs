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

        /// <summary>원본 목록을 복사하여 화면 밖의 변경과 분리한다.</summary>
        public OwnerTeamLineupSnapshot(string teamName, string lineupName, string costLabel,
            IReadOnlyList<PlayerMiniCardModel> hitters, IReadOnlyList<PlayerMiniCardModel> pitchers,
            IReadOnlyList<string> teamColors,
            IReadOnlyList<OwnerCollectionCardSnapshot> hitterDetails = null,
            IReadOnlyList<OwnerCollectionCardSnapshot> pitcherDetails = null)
        {
            TeamName = teamName ?? string.Empty;
            LineupName = lineupName ?? string.Empty;
            CostLabel = costLabel ?? string.Empty;
            Hitters = Array.AsReadOnly(new List<PlayerMiniCardModel>(hitters).ToArray());
            Pitchers = Array.AsReadOnly(new List<PlayerMiniCardModel>(pitchers).ToArray());
            HitterDetails = CopyDetails(hitterDetails, Hitters.Count, nameof(hitterDetails));
            PitcherDetails = CopyDetails(pitcherDetails, Pitchers.Count, nameof(pitcherDetails));
            TeamColors = Array.AsReadOnly(new List<string>(teamColors).ToArray());
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
                ManagerModeMatchService.CreateRosterRolePlan(roster);
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
                hitters.Add(CreatePublicLineupCard(runtime, roster, id, (i + 1) + "번", position));
                hitterDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, id, teamSeasonKey, isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.BenchPriorityCardIds.Count; i++)
            {
                hitters.Add(CreatePublicLineupCard(runtime, roster, plan.BenchPriorityCardIds[i], (i + 1) + "번", "벤치"));
                hitterDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.BenchPriorityCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.StarterRotationCardIds.Count; i++)
            {
                pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.StarterRotationCardIds[i], (i + 1) + "선발", "선발"));
                pitcherDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.StarterRotationCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            for (int i = 0; i < plan.BullpenAssignmentCardIds.Count; i++)
            {
                pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.BullpenAssignmentCardIds[i], (i + 1) + "번", "중계"));
                pitcherDetails.Add(CreateLineupDetail(
                    manager, runtime, roster, plan.BullpenAssignmentCardIds[i], teamSeasonKey,
                    isOwnTeam, teamColorBonuses));
            }
            pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.SetupPitcherCardId, "셋업", "셋업"));
            pitcherDetails.Add(CreateLineupDetail(
                manager, runtime, roster, plan.SetupPitcherCardId, teamSeasonKey,
                isOwnTeam, teamColorBonuses));
            pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.CloserPitcherCardId, "마무리", "마무리"));
            pitcherDetails.Add(CreateLineupDetail(
                manager, runtime, roster, plan.CloserPitcherCardId, teamSeasonKey,
                isOwnTeam, teamColorBonuses));
            var cost = new RosterCostResolver().Resolve(roster, runtime.WorldCardCatalog);
            string[] colors = isOwnTeam ? CreateSelectedTeamColorTexts(manager, plan) :
                CreateAiTeamColorTexts(manager, aiTeamColors);
            return new OwnerTeamLineupSnapshot(manager.GetTeamDisplayName(teamSeasonKey),
                isOwnTeam ? plan.Name : "공개 등록 기준 라인업", OwnerRosterEvaluationFormatter.FormatCost(cost),
                hitters, pitchers, colors, hitterDetails, pitcherDetails);
        }

        private static string[] CreateAiTeamColorTexts(
            OwnerModeManager manager,
            IReadOnlyList<TeamColorDefinition> selected)
        {
            var texts = new string[selected.Count];
            for (int index = 0; index < selected.Count; index++)
            {
                TeamColorDefinition definition = selected[index];
                texts[index] = definition == null
                    ? "팀컬러 적용 없음"
                    : OwnerTeamColorDisplayFormatter.FormatWorldName(
                        definition,
                        definition.DisplayName,
                        manager.Runtime.IdentityRegistry.GetFranchiseDisplayName,
                        manager.GetTeamDisplayName);
            }
            return texts;
        }

        private static PlayerMiniCardModel CreatePublicLineupCard(ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster, string cardId, string order, string role)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            ActiveRosterEntry entry = null;
            foreach (var candidate in roster.Entries)
                if (string.Equals(candidate.CardId, cardId, StringComparison.Ordinal)) { entry = candidate; break; }
            if (entry == null || !runtime.WorldCardCatalog.TryGetCard(cardId, out var definition)) return null;
            var season = runtime.WorldCardCatalog.GetPlayerSeason(definition);
            return new PlayerMiniCardModel(cardId, runtime.IdentityRegistry.GetPlayerDisplayName(entry.PlayerPersonId),
                order, (season.OriginYear % 100).ToString("00"), "C " + season.Cost,
                string.Empty, role, teamAccentHex: "#B1A858", isInteractable: false, frameEdition: definition.Edition, cost: season.Cost);
        }

        private static OwnerCollectionCardSnapshot CreateLineupDetail(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            string cardId,
            string currentTeamSeasonKey,
            bool isOwnTeam,
            PerCardBonusMap teamColorBonuses)
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
            AbilityRatings abilities = abilityResolver.ResolvePermanent(season, card, null);
            var abilityBreakdowns = new OwnerAbilityBreakdownSnapshot[PlayerAbilityCatalog.AbilityCount];
            for (int abilityIndex = 0; abilityIndex < abilityBreakdowns.Length; abilityIndex++)
            {
                var ability = (PlayerAbility)abilityIndex;
                OwnerCardAbilityContribution contribution = abilityResolver.ResolveContribution(
                    season, card, null, ability);
                abilityBreakdowns[abilityIndex] = new OwnerAbilityBreakdownSnapshot(
                    contribution.BaseCard,
                    0,
                    0,
                    teamColorBonuses.Get(cardId, ability),
                    0,
                    0);
            }
            return new OwnerCollectionCardSnapshot(
                cardId,
                season.PlayerPersonId,
                runtime.IdentityRegistry.GetPlayerDisplayName(entry.PlayerPersonId),
                season.OriginYear,
                season.Position,
                season.Cost,
                card.Edition,
                0,
                0,
                false,
                false,
                abilities,
                OwnerLeagueDisplayNameFormatter.FormatFull(runtime.League.Grade) + " · 현재 시즌",
                season.PlayerSeasonId,
                season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null,
                person?.Throws,
                person?.Bats,
                CreatePitchSnapshots(manager, season, abilities),
                CreateCurrentSeasonRecord(runtime, season, currentTeamSeasonKey),
                teamDisplayName: manager.GetTeamDisplayName(season.OriginTeamSeasonKey),
                conditionLabel: "비공개",
                abilityBreakdowns: abilityBreakdowns,
                abilityGraphMaximum: manager.Balance.MatchRatingCurve.Caps.HardCap,
                isOwnedCard: false);
        }
    }
}

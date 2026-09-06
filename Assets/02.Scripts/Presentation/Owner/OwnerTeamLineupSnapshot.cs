using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
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
        public IReadOnlyList<string> TeamColors { get; }

        /// <summary>원본 목록을 복사하여 화면 밖의 변경과 분리한다.</summary>
        public OwnerTeamLineupSnapshot(string teamName, string lineupName, string costLabel,
            IReadOnlyList<PlayerMiniCardModel> hitters, IReadOnlyList<PlayerMiniCardModel> pitchers,
            IReadOnlyList<string> teamColors)
        {
            TeamName = teamName ?? string.Empty;
            LineupName = lineupName ?? string.Empty;
            CostLabel = costLabel ?? string.Empty;
            Hitters = Array.AsReadOnly(new List<PlayerMiniCardModel>(hitters).ToArray());
            Pitchers = Array.AsReadOnly(new List<PlayerMiniCardModel>(pitchers).ToArray());
            TeamColors = Array.AsReadOnly(new List<string>(teamColors).ToArray());
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
            var hitters = new List<PlayerMiniCardModel>();
            var pitchers = new List<PlayerMiniCardModel>();
            for (int i = 0; i < plan.BattingOrderCardIds.Count; i++)
            {
                string id = plan.BattingOrderCardIds[i];
                string position = "야수";
                foreach (var slot in plan.StartingLineupSlots)
                    if (string.Equals(slot.CardId, id, StringComparison.Ordinal)) position = FormatPosition(slot.Position);
                hitters.Add(CreatePublicLineupCard(runtime, roster, id, (i + 1) + "번", position));
            }
            for (int i = 0; i < plan.BenchPriorityCardIds.Count; i++)
                hitters.Add(CreatePublicLineupCard(runtime, roster, plan.BenchPriorityCardIds[i], (i + 1) + "번", "벤치"));
            for (int i = 0; i < plan.StarterRotationCardIds.Count; i++)
                pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.StarterRotationCardIds[i], (i + 1) + "선발", "선발"));
            for (int i = 0; i < plan.BullpenAssignmentCardIds.Count; i++)
                pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.BullpenAssignmentCardIds[i], (i + 1) + "번", "중계"));
            pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.SetupPitcherCardId, "셋업", "셋업"));
            pitchers.Add(CreatePublicLineupCard(runtime, roster, plan.CloserPitcherCardId, "마무리", "마무리"));
            var cost = new RosterCostResolver().Resolve(roster, runtime.WorldCardCatalog);
            string[] colors = isOwnTeam ? CreateSelectedTeamColorTexts(manager, plan) :
                CreateAiTeamColorTexts(manager, roster, runtime.WorldCardCatalog, manager.Balance.TeamColor);
            return new OwnerTeamLineupSnapshot(manager.GetTeamDisplayName(teamSeasonKey),
                isOwnTeam ? plan.Name : "공개 등록 기준 라인업", OwnerRosterEvaluationFormatter.FormatCost(cost),
                hitters, pitchers, colors);
        }

        private static string[] CreateAiTeamColorTexts(OwnerModeManager manager, CurrentRosterState roster,
            WorldCardCatalog catalog, TeamColorBalanceTable balance)
        {
            TeamColorDefinition[] selected = ManagerModeMatchService.ResolveAiTeamColors(roster, catalog, balance);
            var texts = new string[selected.Length];
            for (int index = 0; index < selected.Length; index++)
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
    }
}

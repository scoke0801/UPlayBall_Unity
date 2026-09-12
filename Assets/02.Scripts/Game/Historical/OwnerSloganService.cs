using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Game.Historical
{
    /// <summary>수집 이력으로 해금을 보존하고 선택 시 확정한 효과를 1군 대상에 적용한다.</summary>
    public static class OwnerSloganService
    {
        private static bool Matches(PlayerSeasonDefinition season, OwnerSloganDefinition definition, int level = 0)
        {
            if (definition.target == OwnerSupportTarget.Batter && season.PlayerType != PlayerType.Batter
                || definition.target == OwnerSupportTarget.Pitcher && season.PlayerType != PlayerType.Pitcher
                || definition.reliefOnly && season.Position != PlayerPosition.ReliefPitcher) return false;
            return season.CreateBaseAttributes().Get(definition.requiredAbility) >= (level == 0 ? definition.minimumAbility : definition.GetMinimumAbility(level));
        }
        public static int CountProgress(ManagerHistoricalRuntimeState runtime, OwnerSloganDefinition definition)
        {
            definition.Validate(); int count = 0;
            foreach (var id in runtime.CollectionHistory.EverAcquiredCardIds)
                if (runtime.WorldCardCatalog.TryGetCard(id, out var card) && Matches(runtime.WorldCardCatalog.GetPlayerSeason(card), definition)) count++;
            return count;
        }
        public static int GetLevel(ManagerHistoricalRuntimeState runtime, OwnerSloganDefinition definition)
        {
            int count = CountProgress(runtime, definition), level = 0;
            while (level < 6 && count >= definition.cardsRequired[level]) level++;
            return level;
        }
        public static void Select(ManagerHistoricalRuntimeState runtime, OwnerSloganDefinition definition)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.Slogan).RequireAllowed();
            int level = GetLevel(runtime, definition);
            if (level == 0) throw new InvalidOperationException("슬로건 해금에 필요한 카드 수집 조건을 확인하세요.");
            if (runtime.PlayerGrowth.Slogan?.Definition.id == definition.id && runtime.PlayerGrowth.Slogan.Level == level) return;
            runtime.PlayerGrowth.Slogan = new OwnerSloganState(definition, level, (runtime.PlayerGrowth.Slogan?.Revision ?? 0) + 1);
            SynchronizeRoster(runtime);
        }
        public static void SynchronizeRoster(ManagerHistoricalRuntimeState runtime)
        {
            var state = runtime.PlayerGrowth.Slogan;
            if (state == null) return;
            var targetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
                if (runtime.WorldCardCatalog.TryGetCard(entry.CardId, out var card) && Matches(runtime.WorldCardCatalog.GetPlayerSeason(card), state.Definition, state.Level)) targetIds.Add(entry.CardId);
            foreach (var owned in runtime.OwnedCards)
            {
                var ledger = owned.Training.Ledger;
                bool unchanged = false;
                foreach (var modifier in ledger.Entries)
                    if (modifier.Source == OwnerGrowthSource.Slogan && modifier.IsActive && targetIds.Contains(owned.CardId)
                        && modifier.SourceId.StartsWith("slogan_" + state.Revision + "_", StringComparison.Ordinal)) unchanged = true;
                if (unchanged) continue;
                ledger.Expire(OwnerGrowthSource.Slogan);
                if (!targetIds.Contains(owned.CardId)) continue;
                var values = new int[PlayerAbilityCatalog.AbilityCount];
                values[(int)state.Definition.bonusAbility] += state.Definition.bonusByLevel[state.Level - 1];
                values[(int)state.Definition.penaltyAbility] += state.Definition.penaltyByLevel[state.Level - 1];
                ledger.Add(new OwnerGrowthModifier("slogan_" + state.Revision + "_" + ledger.Count, OwnerGrowthSource.Slogan,
                    state.Definition.name + " Lv." + state.Level, values, runtime.ManagerMode.LiveSeason.SeasonNumber));
            }
        }
    }
}

using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    public sealed partial class ManagerHistoricalSaveAdapter
    {
        private static OwnerSupportSaveData CreateSupport(OwnerSupportState support)
        {
            var result = new OwnerSupportSaveData { nextSequence = support.NextSequence,
                inventoryIds = new string[support.Inventory.Count], inventoryCounts = new int[support.Inventory.Count],
                assignments = new OwnerSupportAssignmentSaveData[support.Assignments.Count] };
            int index = 0;
            foreach (var entry in support.Inventory)
            {
                result.inventoryIds[index] = entry.Key; result.inventoryCounts[index++] = entry.Value;
            }
            for (int i = 0; i < result.assignments.Length; i++)
            {
                var entry = support.Assignments[i];
                result.assignments[i] = new OwnerSupportAssignmentSaveData { sourceId = entry.SourceId,
                    definitionId = entry.DefinitionId, cardId = entry.CardId, remainingGames = entry.RemainingGames,
                    definition = new OwnerSupportDefinition { id = entry.Definition.id, displayName = entry.Definition.displayName,
                        description = entry.Definition.description, price = entry.Definition.price, scope = entry.Definition.scope,
                        target = entry.Definition.target, maximumAge = entry.Definition.maximumAge, conditionPoints = entry.Definition.conditionPoints,
                        bonuses = (int[])entry.Definition.bonuses.Clone() } };
            }
            return result;
        }
        private static OwnerSupportState RestoreSupport(OwnerSupportSaveData data)
        {
            if (data == null) return new OwnerSupportState();
            Require(data.inventoryIds, nameof(data.inventoryIds)); Require(data.inventoryCounts, nameof(data.inventoryCounts));
            if (data.inventoryIds.Length != data.inventoryCounts.Length) throw new System.ArgumentException("서포트 재고 길이가 다릅니다.");
            var result = new OwnerSupportState(data.nextSequence);
            var ids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < data.inventoryIds.Length; i++)
            {
                if (!ids.Add(data.inventoryIds[i]) || data.inventoryCounts[i] < 0) throw new System.ArgumentException("서포트 재고가 올바르지 않습니다.");
                if (data.inventoryCounts[i] > 0) result.Add(data.inventoryIds[i], data.inventoryCounts[i]);
            }
            foreach (var entry in Require(data.assignments, nameof(data.assignments)))
                result.RestoreAssignment(new OwnerSupportAssignment(entry.sourceId, entry.definitionId, entry.cardId, entry.remainingGames,
                    Require(entry.definition, nameof(entry.definition))));
            return result;
        }
        private static OwnerGrowthModifierSaveData[] CreateGrowthLedger(OwnerGrowthLedger ledger)
        {
            var result = new OwnerGrowthModifierSaveData[ledger.Count];
            var entries = ledger.Entries;
            for (int i = 0; i < result.Length; i++)
            {
                OwnerGrowthModifier entry = entries[i];
                result[i] = new OwnerGrowthModifierSaveData { sourceId = entry.SourceId,
                    source = (int)entry.Source, displayName = entry.DisplayName, values = entry.CopyValues(),
                    seasonNumber = entry.SeasonNumber, remainingGames = entry.RemainingGames, isActive = entry.IsActive };
            }
            return result;
        }

        private static OwnerGrowthLedger RestoreGrowthLedger(OwnerGrowthModifierSaveData[] entries)
        {
            if (entries == null) return null;
            var result = new OwnerGrowthLedger();
            foreach (OwnerGrowthModifierSaveData entry in entries)
            {
                Require(entry, nameof(entries));
                result.Add(new OwnerGrowthModifier(entry.sourceId, (OwnerGrowthSource)entry.source,
                    entry.displayName, entry.values, entry.seasonNumber, entry.remainingGames, entry.isActive));
            }
            return result;
        }
    }
}

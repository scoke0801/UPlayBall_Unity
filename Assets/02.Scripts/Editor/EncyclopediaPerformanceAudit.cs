using System;
using System.Diagnostics;
using Baseball.Core.Historical;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using UnityEditor;

namespace Baseball.Editor.Historical
{
    /// <summary>Production 역사 Archive에서 도감 Index·조회 시간과 할당량을 재현 가능하게 측정한다.</summary>
    public static class EncyclopediaPerformanceAudit
    {
        [MenuItem("Baseball/Historical/Measure Encyclopedia Performance")]
        public static void Run()
        {
            var total = Stopwatch.StartNew();
            HistoricalBakedContent baked = NewGameDefinition.LoadHistoricalContentProvider().Load();
            var configuration = NewGameDefinition.LoadConfiguration();
            OwnerModeNewGameConfiguration owner = NewGameDefinition.LoadOwnerModeConfiguration();
            HistoricalWorldRuntimeContent world = new HistoricalWorldRuntimeBuilder(
                    configuration.Balance,
                    bakedHistorySource: NewGameDefinition.LoadBakedWorldHistorySource())
                .GetOrBuild(baked, WorldRecordMode.SimulatedHistory, owner.WorldSeed);

            long memoryBefore = GC.GetTotalMemory(true);
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            var build = Stopwatch.StartNew();
            var service = new EncyclopediaCatalogService(
                baked,
                world.WorldCardCatalog,
                world.IdentityRegistry,
                world.WorldHistory,
                Array.Empty<OwnedPlayerCardState>(),
                new CardCollectionHistoryState(),
                new WishlistState());
            build.Stop();

            var fullQuery = Stopwatch.StartNew();
            int cardCount = service.QueryCards().Count;
            int seasonCount = service.QueryPlayerSeasons().Count;
            fullQuery.Stop();

            var filteredQuery = Stopwatch.StartNew();
            PlayerCardDefinition sample = world.WorldCardCatalog.Cards[cardCount / 2];
            PlayerSeasonDefinition sampleSeason = world.WorldCardCatalog.GetPlayerSeason(sample);
            int filteredCount = service.QueryCards(new EncyclopediaFilter
            {
                FranchiseId = sampleSeason.OriginFranchiseId,
                OriginYear = sampleSeason.OriginYear,
                Edition = sample.Edition
            }).Count;
            filteredQuery.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            long retainedHeapDeltaBytes = GC.GetTotalMemory(false) - memoryBefore;
            total.Stop();

            UnityEngine.Debug.Log(string.Concat(
                "ENCYCLOPEDIA_PERF ",
                "cards=", cardCount.ToString(),
                " seasons=", seasonCount.ToString(),
                " filtered=", filteredCount.ToString(),
                " indexMs=", build.Elapsed.TotalMilliseconds.ToString("F2"),
                " fullQueryMs=", fullQuery.Elapsed.TotalMilliseconds.ToString("F2"),
                " filteredQueryMs=", filteredQuery.Elapsed.TotalMilliseconds.ToString("F2"),
                " allocatedBytes=", allocatedBytes.ToString(),
                " retainedHeapDeltaBytes=", retainedHeapDeltaBytes.ToString(),
                " totalWithWorldMs=", total.Elapsed.TotalMilliseconds.ToString("F2")));
        }
    }
}

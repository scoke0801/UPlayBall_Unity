using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Baseball.Core.Balance;
using Baseball.Editor.Tools;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>
    /// 44시즌 World History를 미리 시뮬레이션해 Player Build 산출물로 굽는다.
    /// 새 게임 시작이 매번 약 1만 8천 경기를 실행하던 비용을 파일 하나 읽기로 바꾸는 것이 목적이다.
    /// 결과는 결정론적이므로 같은 Seed·콘텐츠·밸런스에서 구운 값은 실제로 돌린 값과 일치한다.
    /// </summary>
    public static class WorldHistoryBakeTool
    {
        private const string OutputDirectory = "Assets/10.Datas/HistoricalSimulation/BakedWorldHistory";
        private const string CatalogAssetPath = OutputDirectory + "/BakedWorldHistoryCatalog.asset";

        [BaseballEditorTool(
            "데이터",
            "World History Bake",
            "구단주·커리어 Seed의 44시즌 역사를 미리 시뮬레이션해 Runtime 산출물로 굽습니다. 몇 분 걸립니다.",
            order: 30,
            impact: ToolImpact.BulkWrite)]
        public static void BakeAll()
        {
            NewGameDefinition definition = LoadDefinition();
            var requests = new List<BakeRequest>();
            OwnerModeNewGameConfiguration ownerConfiguration = definition.ToOwnerModeConfiguration();
            requests.Add(new BakeRequest(
                "owner",
                ownerConfiguration.WorldSeed,
                definition.ToOwnerModeBalanceTable()));

            BalanceTable careerBalance = definition.ToConfiguration().Balance;
            IReadOnlyList<long> careerSeeds = definition.CareerWorldSeedPool;
            for (int index = 0; index < careerSeeds.Count; index++)
            {
                ulong seed = unchecked((ulong)careerSeeds[index]);
                requests.Add(new BakeRequest(
                    "career" + index.ToString(CultureInfo.InvariantCulture),
                    seed,
                    careerBalance));
            }

            if (careerSeeds.Count == 0)
            {
                Debug.LogWarning(
                    "[WorldHistoryBakeTool] NewGameDefinition의 Career World Seed Pool이 비어 있습니다. " +
                    "커리어 모드는 매 새 게임마다 임의 Seed를 뽑으므로 Bake가 적중하지 않습니다. " +
                    "Pool에 Seed를 넣고 다시 구우면 커리어 시작도 즉시 열립니다.");
            }

            Bake(definition, requests);
        }

        private static void Bake(NewGameDefinition definition, IReadOnlyList<BakeRequest> requests)
        {
            HistoricalBakedContent content = LoadVerifiedContent(definition);
            Directory.CreateDirectory(OutputDirectory);

            var entries = new List<BakedWorldHistoryEntry>(requests.Count);
            var writtenPaths = new List<string>(requests.Count);
            try
            {
                for (int index = 0; index < requests.Count; index++)
                {
                    BakeRequest request = requests[index];
                    EditorUtility.DisplayProgressBar(
                        "World History Bake",
                        $"{request.Label} (seed={request.WorldSeed}) 44시즌 시뮬레이션 중…",
                        requests.Count == 0 ? 0f : (float)index / requests.Count);

                    string assetPath = WriteBake(content, request);
                    writtenPaths.Add(assetPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            for (int index = 0; index < writtenPaths.Count; index++)
            {
                var payload = AssetDatabase.LoadAssetAtPath<TextAsset>(writtenPaths[index]);
                if (payload == null)
                    throw new InvalidOperationException($"구운 산출물을 다시 읽지 못했습니다: {writtenPaths[index]}");
                entries.Add(new BakedWorldHistoryEntry(payload, requests[index].Label));
            }

            BakedWorldHistoryCatalog catalog = LoadOrCreateCatalog();
            catalog.Configure(entries);
            EditorUtility.SetDirty(catalog);
            definition.ConfigureBakedWorldHistoryCatalog(catalog);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldHistoryBakeTool] {entries.Count}건을 구웠습니다. catalog={CatalogAssetPath}");
        }

        private static string WriteBake(HistoricalBakedContent content, BakeRequest request)
        {
            long startedAt = Stopwatch.GetTimestamp();
            WorldHistoryBakeResult result = WorldHistoryBakeService.Create(
                content,
                request.Balance,
                request.WorldSeed);
            double elapsedMs = (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;

            byte[] bytes = WorldHistoryBakeService.Encode(result.Payload);
            string assetPath = OutputDirectory + "/world_history_" + request.Label + ".bytes";
            File.WriteAllBytes(assetPath, bytes);
            Debug.Log(
                $"[WorldHistoryBakeTool] {request.Label} seed={request.WorldSeed} " +
                $"games={result.TotalGameCount} simulateMs={elapsedMs:F0} " +
                $"rows={result.StatisticsRowCount} size={bytes.Length / 1024}KB");
            return assetPath;
        }

        /// <summary>저작 시점에는 파일별 SHA-256까지 전부 확인한다. Runtime이 건너뛰는 검증을 여기서 대신한다.</summary>
        private static HistoricalBakedContent LoadVerifiedContent(NewGameDefinition definition)
        {
            HistoricalRuntimeContentCatalog catalog = definition.HistoricalContentCatalog;
            if (catalog == null)
                throw new InvalidOperationException("NewGameDefinition에 HistoricalRuntimeContentCatalog가 없습니다.");
            var provider = new UnityHistoricalContentProvider(
                catalog,
                HistoricalContentVerificationMode.Full);
            return provider.Load();
        }

        private static NewGameDefinition LoadDefinition()
        {
            var definition = Resources.Load<NewGameDefinition>("NewGame/NewGameDefinition");
            if (definition == null)
                throw new InvalidOperationException("Resources에서 NewGameDefinition을 찾지 못했습니다.");
            return definition;
        }

        private static BakedWorldHistoryCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BakedWorldHistoryCatalog>(CatalogAssetPath);
            if (catalog != null)
                return catalog;
            catalog = ScriptableObject.CreateInstance<BakedWorldHistoryCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            return catalog;
        }

        private readonly struct BakeRequest
        {
            public BakeRequest(string label, ulong worldSeed, BalanceTable balance)
            {
                Label = label;
                WorldSeed = worldSeed;
                Balance = balance;
            }

            public string Label { get; }
            public ulong WorldSeed { get; }
            public BalanceTable Balance { get; }
        }
    }
}

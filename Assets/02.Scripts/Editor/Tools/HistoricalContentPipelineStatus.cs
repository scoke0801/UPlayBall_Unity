using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Baseball.Core.Balance;
using Baseball.Editor.HistoricalDatabase;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using UnityEditor;

namespace Baseball.Editor.Tools
{
    /// <summary>단계 산출물이 지금 입력과 맞는지에 대한 판정이다.</summary>
    public enum HistoricalContentFreshness
    {
        /// <summary>판정에 필요한 입력이 없어 확인하지 못했다.</summary>
        Unknown = 0,

        /// <summary>산출물이 현재 입력과 일치한다.</summary>
        UpToDate = 1,

        /// <summary>입력이 바뀌었는데 산출물을 다시 만들지 않았다.</summary>
        Stale = 2,

        /// <summary>산출물 자체가 없다.</summary>
        Missing = 3
    }

    /// <summary>한 단계의 최신 여부와 그 근거다.</summary>
    public readonly struct HistoricalContentStepStatus
    {
        public HistoricalContentStepStatus(
            HistoricalContentPipelineStepId stepId,
            HistoricalContentFreshness freshness,
            string detail)
        {
            StepId = stepId;
            Freshness = freshness;
            Detail = detail ?? string.Empty;
        }

        public HistoricalContentPipelineStepId StepId { get; }
        public HistoricalContentFreshness Freshness { get; }
        public string Detail { get; }
    }

    public sealed class HistoricalContentPipelineStatusReport
    {
        public HistoricalContentPipelineStatusReport(
            IReadOnlyList<HistoricalContentStepStatus> steps,
            IReadOnlyList<string> bakeKeyLines,
            string contentHash)
        {
            Steps = steps;
            BakeKeyLines = bakeKeyLines;
            ContentHash = contentHash ?? string.Empty;
        }

        public IReadOnlyList<HistoricalContentStepStatus> Steps { get; }

        /// <summary>Bake Key를 기대값과 하나씩 대조한 결과다. 3단계가 왜 낡았는지의 근거가 된다.</summary>
        public IReadOnlyList<string> BakeKeyLines { get; }

        public string ContentHash { get; }

        public HistoricalContentFreshness GetFreshness(HistoricalContentPipelineStepId stepId)
        {
            for (int index = 0; index < Steps.Count; index++)
                if (Steps[index].StepId == stepId)
                    return Steps[index].Freshness;
            return HistoricalContentFreshness.Unknown;
        }

        public string GetDetail(HistoricalContentPipelineStepId stepId)
        {
            for (int index = 0; index < Steps.Count; index++)
                if (Steps[index].StepId == stepId)
                    return Steps[index].Detail;
            return string.Empty;
        }
    }

    /// <summary>
    /// 각 단계 산출물이 현재 입력과 맞는지 판정한다.
    /// 3단계는 실행 시각이 아니라 Bake Key를 실제로 대조한다 — Key가 어긋나면 Bake가 조용히
    /// 무시되고 44시즌을 다시 시뮬레이션하므로, 시각 비교로는 이 상태를 잡을 수 없다.
    /// </summary>
    public static class HistoricalContentPipelineStatus
    {
        public const string EditorArchiveRoot =
            "Assets/Editor Default Resources/HistoricalSimulation/1982-2025";
        public const string BakedWorldHistoryRoot =
            "Assets/10.Datas/HistoricalSimulation/BakedWorldHistory";

        public static HistoricalContentPipelineStatusReport Inspect()
        {
            var steps = new List<HistoricalContentStepStatus>(3);
            var bakeKeyLines = new List<string>();
            string contentHash = string.Empty;

            steps.Add(InspectCanonicalArchive());
            steps.Add(InspectRuntimeExport());
            steps.Add(InspectWorldHistoryBake(bakeKeyLines, out contentHash));
            return new HistoricalContentPipelineStatusReport(steps, bakeKeyLines, contentHash);
        }

        /// <summary>
        /// 정규화 캐시가 Archive보다 새로우면 다시 구워야 한다.
        /// 캐시 파일 수정 시각에 기대는 근사 판정이라, 확신이 필요하면 그냥 다시 굽는 편이 빠르다.
        /// </summary>
        private static HistoricalContentStepStatus InspectCanonicalArchive()
        {
            string archiveManifest = Path.GetFullPath(EditorArchiveRoot + "/manifest.json");
            if (!File.Exists(archiveManifest))
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.CanonicalArchiveBake,
                    HistoricalContentFreshness.Missing,
                    "Editor 원본 Archive가 없습니다.");
            }

            string cacheDirectory = Path.GetFullPath(
                HistoricalContentPipelineRunner.NormalizedCacheDirectory);
            if (!Directory.Exists(cacheDirectory))
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.CanonicalArchiveBake,
                    HistoricalContentFreshness.Unknown,
                    "KBO 정규화 캐시가 없어 판정할 수 없습니다. 이미 구운 Archive는 그대로 씁니다.");
            }

            DateTime archiveTime = File.GetLastWriteTimeUtc(archiveManifest);
            DateTime newestInput = DateTime.MinValue;
            string[] inputs = Directory.GetFiles(cacheDirectory, "*.json", SearchOption.AllDirectories);
            for (int index = 0; index < inputs.Length; index++)
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(inputs[index]);
                if (writeTime > newestInput)
                    newestInput = writeTime;
            }

            if (newestInput > archiveTime)
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.CanonicalArchiveBake,
                    HistoricalContentFreshness.Stale,
                    "정규화 캐시가 Archive보다 새롭습니다. 캐시 " + ToLocalText(newestInput) +
                    " > Archive " + ToLocalText(archiveTime));
            }
            return new HistoricalContentStepStatus(
                HistoricalContentPipelineStepId.CanonicalArchiveBake,
                HistoricalContentFreshness.UpToDate,
                "Archive " + ToLocalText(archiveTime));
        }

        /// <summary>
        /// manifest에 모든 파일의 해시가 들어 있으므로 manifest 두 개가 같으면 내보낸 콘텐츠도 같다.
        /// </summary>
        private static HistoricalContentStepStatus InspectRuntimeExport()
        {
            string source = Path.GetFullPath(HistoricalRuntimeContentExporter.SourceRoot + "/manifest.json");
            string runtime = Path.GetFullPath(HistoricalRuntimeContentExporter.RuntimeRoot + "/manifest.json");
            if (!File.Exists(source))
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.RuntimeContentExport,
                    HistoricalContentFreshness.Unknown,
                    "1단계 Runtime 정제본이 없습니다.");
            }
            if (!File.Exists(runtime))
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.RuntimeContentExport,
                    HistoricalContentFreshness.Missing,
                    "내보낸 Runtime 콘텐츠가 없습니다.");
            }

            if (!AreFilesEqual(source, runtime))
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.RuntimeContentExport,
                    HistoricalContentFreshness.Stale,
                    "정제본과 내보낸 manifest가 다릅니다. 정제본 " +
                    ToLocalText(File.GetLastWriteTimeUtc(source)) + ", 내보냄 " +
                    ToLocalText(File.GetLastWriteTimeUtc(runtime)));
            }
            return new HistoricalContentStepStatus(
                HistoricalContentPipelineStepId.RuntimeContentExport,
                HistoricalContentFreshness.UpToDate,
                "내보낸 manifest가 정제본과 일치합니다.");
        }

        /// <summary>
        /// 런타임과 똑같이 CreateBakeKey로 기대 Key를 만들어 Catalog의 Bake와 대조한다.
        /// Key 계산식을 여기서 다시 쓰지 않는 이유는, 식이 갈라지면 이 진단이 조용히 거짓말을 하기 때문이다.
        /// </summary>
        private static HistoricalContentStepStatus InspectWorldHistoryBake(
            List<string> bakeKeyLines,
            out string contentHash)
        {
            contentHash = string.Empty;
            NewGameDefinition definition =
                AssetDatabase.LoadAssetAtPath<NewGameDefinition>(
                    HistoricalRuntimeContentExporter.NewGameDefinitionAssetPath);
            if (definition == null)
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.WorldHistoryBake,
                    HistoricalContentFreshness.Unknown,
                    "NewGameDefinition을 찾지 못했습니다.");
            }
            if (definition.HistoricalContentCatalog == null)
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.WorldHistoryBake,
                    HistoricalContentFreshness.Unknown,
                    "Historical Content Catalog가 연결되지 않았습니다. 2단계를 먼저 실행하세요.");
            }

            HistoricalBakedContent content;
            try
            {
                content = new UnityHistoricalContentProvider(
                    definition.HistoricalContentCatalog,
                    HistoricalContentVerificationMode.Fast).Load();
            }
            catch (Exception exception) when (exception is InvalidOperationException ||
                                              exception is ArgumentException ||
                                              exception is InvalidDataException)
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.WorldHistoryBake,
                    HistoricalContentFreshness.Unknown,
                    "콘텐츠를 읽지 못했습니다: " + exception.Message);
            }

            contentHash = content.Manifest.ContentHash;
            BakedWorldHistoryCatalog catalog = definition.BakedWorldHistoryCatalog;
            if (catalog == null || catalog.Entries.Count == 0)
            {
                bakeKeyLines.Add("Bake Catalog가 비어 있습니다.");
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.WorldHistoryBake,
                    HistoricalContentFreshness.Missing,
                    "구운 World History가 없습니다. 새 게임마다 44시즌을 실제로 시뮬레이션합니다.");
            }

            List<BakedWorldHistoryKey> availableKeys = ReadCatalogKeys(catalog);
            int hitCount = 0;
            int expectedCount = 0;

            expectedCount++;
            if (AppendKeyComparison(
                    bakeKeyLines,
                    "owner",
                    HistoricalWorldRuntimeBuilder.CreateBakeKey(
                        content,
                        definition.ToOwnerModeConfiguration().WorldSeed,
                        definition.ToOwnerModeBalanceTable()),
                    availableKeys))
            {
                hitCount++;
            }

            BalanceTable careerBalance = definition.ToConfiguration().Balance;
            IReadOnlyList<long> careerSeeds = definition.CareerWorldSeedPool;
            for (int index = 0; index < careerSeeds.Count; index++)
            {
                expectedCount++;
                ulong seed = unchecked((ulong)careerSeeds[index]);
                if (AppendKeyComparison(
                        bakeKeyLines,
                        "career" + index.ToString(CultureInfo.InvariantCulture),
                        HistoricalWorldRuntimeBuilder.CreateBakeKey(content, seed, careerBalance),
                        availableKeys))
                {
                    hitCount++;
                }
            }

            if (careerSeeds.Count == 0)
            {
                bakeKeyLines.Add(
                    "커리어 Seed Pool이 비어 있습니다. 커리어는 매번 임의 Seed를 쓰므로 Bake가 적중하지 않습니다.");
            }

            string summary = hitCount + " / " + expectedCount + " 적중";
            if (hitCount == expectedCount)
            {
                return new HistoricalContentStepStatus(
                    HistoricalContentPipelineStepId.WorldHistoryBake,
                    HistoricalContentFreshness.UpToDate,
                    summary);
            }
            return new HistoricalContentStepStatus(
                HistoricalContentPipelineStepId.WorldHistoryBake,
                HistoricalContentFreshness.Stale,
                summary + " — 미스는 새 게임마다 44시즌을 실제로 시뮬레이션합니다.");
        }

        private static List<BakedWorldHistoryKey> ReadCatalogKeys(BakedWorldHistoryCatalog catalog)
        {
            var keys = new List<BakedWorldHistoryKey>(catalog.Entries.Count);
            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                BakedWorldHistoryEntry entry = catalog.Entries[index];
                if (entry == null || entry.Payload == null)
                    continue;
                if (WorldHistoryBakeCodec.TryPeekKey(entry.Payload.bytes, out BakedWorldHistoryKey key))
                    keys.Add(key);
            }
            return keys;
        }

        private static bool AppendKeyComparison(
            List<string> lines,
            string label,
            BakedWorldHistoryKey expected,
            List<BakedWorldHistoryKey> available)
        {
            for (int index = 0; index < available.Count; index++)
            {
                if (!available[index].Equals(expected))
                    continue;
                lines.Add("적중  " + label + "  seed=" + expected.WorldHistorySeed);
                return true;
            }

            string difference = available.Count == 0
                ? "대조할 Bake가 없습니다"
                : DescribeClosestDifference(expected, available);
            lines.Add("미스  " + label + "  seed=" + expected.WorldHistorySeed + "  → " + difference);
            return false;
        }

        /// <summary>Seed가 같은 Bake를 우선 비교한다. 대개 그것이 "다시 굽지 않은 같은 Bake"이기 때문이다.</summary>
        private static string DescribeClosestDifference(
            BakedWorldHistoryKey expected,
            List<BakedWorldHistoryKey> available)
        {
            BakedWorldHistoryKey closest = available[0];
            for (int index = 0; index < available.Count; index++)
            {
                if (available[index].WorldHistorySeed != expected.WorldHistorySeed)
                    continue;
                closest = available[index];
                break;
            }

            var fields = new List<string>(4);
            if (expected.RecordMode != closest.RecordMode) fields.Add("RecordMode");
            if (expected.WorldHistorySeed != closest.WorldHistorySeed) fields.Add("Seed");
            if (!string.Equals(expected.ContentHash, closest.ContentHash, StringComparison.Ordinal))
                fields.Add("ContentHash");
            if (expected.BalanceVersion != closest.BalanceVersion) fields.Add("BalanceVersion");
            if (!string.Equals(expected.BalanceContentHash, closest.BalanceContentHash, StringComparison.Ordinal))
                fields.Add("BalanceContentHash");
            return fields.Count == 0
                ? "동일한 Key를 찾지 못했습니다"
                : "불일치 " + string.Join(", ", fields);
        }

        private static bool AreFilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
                return false;
            byte[] leftBytes = File.ReadAllBytes(left);
            byte[] rightBytes = File.ReadAllBytes(right);
            for (int index = 0; index < leftBytes.Length; index++)
                if (leftBytes[index] != rightBytes[index])
                    return false;
            return true;
        }

        private static string ToLocalText(DateTime utc)
        {
            return utc.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    /// 새 게임에서 전체 역사 경기를 다시 계산하는 비용을 파일 읽기로 바꾼다.
    /// 결과는 결정론적이므로 같은 Seed·콘텐츠·밸런스에서 구운 값은 실제로 돌린 값과 일치한다.
    /// </summary>
    public static class WorldHistoryBakeTool
    {
        private const string OutputDirectory = "Assets/10.Datas/HistoricalSimulation/BakedWorldHistory";
        private const string CatalogAssetPath = OutputDirectory + "/BakedWorldHistoryCatalog.asset";
        internal const string WorkerPreferenceKey = "Baseball.WorldHistoryBake.Workers";
        private static bool _isBaking;

        [BaseballEditorTool(
            "데이터",
            "World History Bake",
            "구단주·커리어 Seed의 44시즌 역사를 미리 시뮬레이션해 Runtime 산출물로 굽습니다. 몇 분 걸립니다.",
            order: 30,
            impact: ToolImpact.BulkWrite)]
        public static async void BakeAll()
        {
            try { await BakeAllAsync(LoadDefinition(), true, false); }
            catch (OperationCanceledException) { Debug.Log("[WorldHistoryBakeTool] 취소했습니다. 완료한 Seed는 다음 실행에서 재사용합니다."); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [BaseballEditorTool("데이터", "World History Bake 설정", "동시 실행 연도 수를 설정합니다.", order: 31)]
        public static void OpenSettings() => EditorWindow.GetWindow<WorldHistoryBakeSettingsWindow>("역사 베이크 설정");

        [BaseballEditorTool("데이터", "World History 강제 재베이크", "기존 결과를 재사용하지 않고 전체를 다시 계산합니다.", order: 32, impact: ToolImpact.BulkWrite)]
        public static async void ForceBakeAll()
        {
            try { await BakeAllAsync(LoadDefinition(), true, true); }
            catch (OperationCanceledException) { Debug.Log("[WorldHistoryBakeTool] 강제 재베이크를 취소했습니다."); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>
        /// Editor를 띄우지 않고 굽는 배치모드 진입점이다.
        /// -careerWorldSeeds a,b,c,d 를 주면 Pool을 먼저 확정한 뒤 굽는다.
        /// </summary>
        public static void BakeFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                NewGameDefinition definition = LoadDefinition();
                long[] seeds = ParseCareerWorldSeedArgument();
                if (seeds.Length > 0)
                {
                    definition.ConfigureCareerWorldSeedPool(seeds);
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssets();
                    Debug.Log(
                        "[WorldHistoryBakeTool] Career World Seed Pool을 설정했습니다: " +
                        string.Join(", ", Array.ConvertAll(seeds, seed => seed.ToString(CultureInfo.InvariantCulture))));
                }
                BakeAllAsync(definition, false, Array.IndexOf(Environment.GetCommandLineArgs(),
                    "-forceWorldHistoryBake") >= 0).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[WorldHistoryBakeTool] Bake에 실패했습니다: {exception}");
                exitCode = 1;
            }
            finally
            {
                if (Application.isBatchMode)
                    EditorApplication.Exit(exitCode);
            }
        }

        private static long[] ParseCareerWorldSeedArgument()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-careerWorldSeeds", StringComparison.Ordinal))
                    continue;
                string[] parts = arguments[index + 1].Split(',');
                var seeds = new long[parts.Length];
                for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    seeds[partIndex] = long.Parse(
                        parts[partIndex].Trim(),
                        CultureInfo.InvariantCulture);
                }
                return seeds;
            }
            return Array.Empty<long>();
        }

        private static Task BakeAllAsync(NewGameDefinition definition, bool background, bool force)
        {
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

            return BakeAsync(definition, requests, background, force);
        }

        private static async Task BakeAsync(NewGameDefinition definition, IReadOnlyList<BakeRequest> requests,
            bool background, bool force)
        {
            if (_isBaking) throw new InvalidOperationException("World History Bake가 이미 실행 중입니다.");
            _isBaking = true;
            using var cancellation = new CancellationTokenSource();
            var status = new BakeStatus();
            double nextUpdate = 0;
            void UpdateProgress()
            {
                if (EditorApplication.timeSinceStartup < nextUpdate) return;
                nextUpdate = EditorApplication.timeSinceStartup + 0.1;
                HistoricalWorldBuildProgress progress = Volatile.Read(ref status.Progress);
                float fraction = progress == null ? 0 : (float)progress.CompletedYears / progress.TotalYears;
                string detail = progress == null ? "준비 중…" :
                    $"{progress.CompletedYears}/{progress.TotalYears}시즌 · {progress.CompletedGames:N0}경기 · 최근 완료 {progress.SeasonYear}년";
                if (EditorUtility.DisplayCancelableProgressBar("World History Bake",
                    $"{status.Label} · {detail}", (status.RequestIndex + fraction) / Math.Max(1, requests.Count)))
                    cancellation.Cancel();
            }
            void CancelOnQuit() => cancellation.Cancel();
            void CancelOnPlayMode(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.ExitingEditMode) cancellation.Cancel();
            }
            var entries = new List<BakedWorldHistoryEntry>(requests.Count);
            var writtenPaths = new List<string>(requests.Count);
            bool autoRefreshDisabled = false;
            EditorApplication.LockReloadAssemblies();
            EditorApplication.quitting += CancelOnQuit;
            EditorApplication.playModeStateChanged += CancelOnPlayMode;
            if (background) EditorApplication.update += UpdateProgress;
            try
            {
                // 비동기 베이크 중 Editor 업데이트가 산출물을 자동 임포트해 파일 교체와 경합하지 않게 한다.
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshDisabled = true;
                var loadTimer = Stopwatch.StartNew();
                HistoricalBakedContent content = LoadVerifiedContent(definition);
                // 지연 로딩된 JsonUtility 호출까지 모두 메인 스레드에서 끝낸다.
                int yearCount = content.Years.Count;
                Debug.Log($"[WorldHistoryBakeTool] loadMs={loadTimer.Elapsed.TotalMilliseconds:F0} years={yearCount}");
                Directory.CreateDirectory(OutputDirectory);
                int workers = ResolveWorkerCount();
                for (int index = 0; index < requests.Count; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    BakeRequest request = requests[index];
                    status.Label = $"{request.Label} (seed={request.WorldSeed}, workers={workers})";
                    status.RequestIndex = index;
                    Volatile.Write(ref status.Progress, null);
                    var options = new HistoricalWorldExecutionOptions(workers,
                        progress => Volatile.Write(ref status.Progress, progress));
                    BakeWriteResult write = background
                        ? await Task.Run(() => WriteBake(content, request, options, force, cancellation.Token))
                        : WriteBake(content, request, options, force, cancellation.Token);
                    Debug.Log(write.Log);
                    writtenPaths.Add(write.Path);
                }
                cancellation.Token.ThrowIfCancellationRequested();
                var saveTimer = Stopwatch.StartNew();
                AssetDatabase.AllowAutoRefresh();
                autoRefreshDisabled = false;
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
                Debug.Log($"[WorldHistoryBakeTool] {entries.Count}건 준비 완료. assetSaveMs={saveTimer.Elapsed.TotalMilliseconds:F0} catalog={CatalogAssetPath}");
            }
            finally
            {
                if (autoRefreshDisabled) AssetDatabase.AllowAutoRefresh();
                if (background) EditorApplication.update -= UpdateProgress;
                EditorApplication.quitting -= CancelOnQuit;
                EditorApplication.playModeStateChanged -= CancelOnPlayMode;
                EditorUtility.ClearProgressBar();
                EditorApplication.UnlockReloadAssemblies();
                _isBaking = false;
            }
        }

        private static BakeWriteResult WriteBake(HistoricalBakedContent content, BakeRequest request,
            HistoricalWorldExecutionOptions options, bool force, CancellationToken cancellationToken)
        {
            string assetPath = OutputDirectory + "/world_history_" + request.Label + ".bytes";
            if (!force && WorldHistoryBakeService.TryReadCompleted(assetPath,
                    HistoricalWorldRuntimeBuilder.CreateBakeKey(content, request.WorldSeed, request.Balance), out _))
                return new BakeWriteResult(assetPath, $"[WorldHistoryBakeTool] {request.Label} seed={request.WorldSeed} 기존 산출물 재사용");
            long startedAt = Stopwatch.GetTimestamp();
            WorldHistoryBakeResult result = WorldHistoryBakeService.Create(
                content,
                request.Balance,
                request.WorldSeed, options, cancellationToken);
            double elapsedMs = (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;

            var timer = Stopwatch.StartNew();
            byte[] bytes = WorldHistoryBakeService.Encode(result.Payload);
            double encodeMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            WorldHistoryBakeService.WriteAtomically(assetPath, bytes, cancellationToken);
            return new BakeWriteResult(assetPath,
                $"[WorldHistoryBakeTool] {request.Label} seed={request.WorldSeed} " +
                $"games={result.TotalGameCount} simulateMs={elapsedMs:F0} " +
                $"mappingMs={result.MappingMilliseconds:F0} encodeMs={encodeMs:F0} writeMs={timer.Elapsed.TotalMilliseconds:F0} " +
                $"workers={options.MaxDegreeOfParallelism} allocatedMB={result.Metrics.AllocatedBytes / 1048576d:F1} " +
                $"exactAllocation={result.Metrics.UsesExactAllocationCounter} rows={result.StatisticsRowCount} size={bytes.Length / 1024}KB");
        }

        private static int ResolveWorkerCount()
        {
            int workers = EditorPrefs.GetInt(WorkerPreferenceKey, Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1)));
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-worldHistoryBakeWorkers");
            if (index >= 0 && (index + 1 >= arguments.Length || !int.TryParse(arguments[index + 1], out workers) || workers < 1 || workers > 32))
                throw new ArgumentException("-worldHistoryBakeWorkers는 1~32여야 합니다.");
            return Math.Max(1, Math.Min(32, workers));
        }

        private sealed class BakeStatus
        {
            public string Label = "역사 자료 로딩";
            public int RequestIndex;
            public HistoricalWorldBuildProgress Progress;
        }

        private readonly struct BakeWriteResult
        {
            public BakeWriteResult(string path, string log) { Path = path; Log = log; }
            public string Path { get; }
            public string Log { get; }
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

    /// <summary>경기 밸런스와 분리된 로컬 베이크 작업 수를 설정한다.</summary>
    internal sealed class WorldHistoryBakeSettingsWindow : EditorWindow
    {
        private void OnGUI()
        {
            int fallback = Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1));
            int workers = EditorPrefs.GetInt(WorldHistoryBakeTool.WorkerPreferenceKey, fallback);
            int next = EditorGUILayout.IntSlider("동시 실행 연도 수", workers, 1, 32);
            if (next != workers) EditorPrefs.SetInt(WorldHistoryBakeTool.WorkerPreferenceKey, next);
            EditorGUILayout.HelpBox("기본 최대 4개입니다. 메모리가 부족하면 줄이세요. 완료된 동일 입력의 Seed는 자동 재사용합니다.", MessageType.Info);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Baseball.Editor.HistoricalDatabase;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Baseball.Editor.Tools
{
    /// <summary>역사 콘텐츠를 게임이 읽는 상태까지 만드는 세 단계다. 순서대로만 의미가 있다.</summary>
    /// <remarks>
    /// 뒤 단계의 산출물은 앞 단계 결과의 해시를 Key에 담는다. 앞을 다시 만들고 뒤를 다시 만들지 않으면
    /// 뒤 산출물이 조용히 무효가 되는데, 무효가 되어도 게임은 정상 동작하고 느려지기만 해서
    /// 눈치채기 어렵다. 세 단계를 한 창에 모아 순서와 최신 여부를 보이게 하는 것이 이 파이프라인의 목적이다.
    /// </remarks>
    public enum HistoricalContentPipelineStepId
    {
        /// <summary>KBO 정규화 캐시 → Editor 원본 Archive + Runtime 정제본(파이썬).</summary>
        CanonicalArchiveBake = 0,

        /// <summary>Runtime 정제본 → Player Build용 TextAsset·Catalog.</summary>
        RuntimeContentExport = 1,

        /// <summary>확정된 콘텐츠·밸런스로 44시즌 역사를 미리 시뮬레이션해 굽기.</summary>
        WorldHistoryBake = 2
    }

    public enum HistoricalContentPipelineStepState
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Canceled = 4
    }

    /// <summary>한 단계의 정의와 마지막 실행 결과다.</summary>
    public sealed class HistoricalContentPipelineStep
    {
        public HistoricalContentPipelineStep(
            HistoricalContentPipelineStepId id,
            string title,
            string summary,
            string inputPath,
            string outputPath)
        {
            Id = id;
            Title = title;
            Summary = summary;
            InputPath = inputPath;
            OutputPath = outputPath;
        }

        public HistoricalContentPipelineStepId Id { get; }
        public string Title { get; }
        public string Summary { get; }
        public string InputPath { get; }
        public string OutputPath { get; }

        public HistoricalContentPipelineStepState State { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
        public string Message { get; internal set; } = string.Empty;

        internal void Reset()
        {
            State = HistoricalContentPipelineStepState.Pending;
            ElapsedSeconds = 0d;
            Message = string.Empty;
        }
    }

    /// <summary>파이썬 Canonical Bake 실행 인자다. 창이 EditorPrefs로 보존한다.</summary>
    public sealed class HistoricalCanonicalBakeOptions
    {
        public string UvExecutablePath { get; set; } = "uv";
        public string Years { get; set; } = "1982-2025";
        public int GenerationSeed { get; set; } = 20260901;
    }

    /// <summary>
    /// 세 단계를 선택한 것만 순서대로 실행한다. 파이썬 단계는 외부 프로세스라 에디터를 멈추지 않도록
    /// EditorApplication.update에서 폴링하고, 나머지 두 단계는 기존 도구를 그대로 호출한다.
    /// </summary>
    public sealed class HistoricalContentPipelineRunner
    {
        public const string ImporterDirectory = "Tools/KBOImporter";
        public const string NormalizedCacheDirectory = ImporterDirectory + "/.cache/KBOImport/Normalized";

        private const string CanonicalBakeScript = "synthetic_bake.py";

        private readonly List<HistoricalContentPipelineStep> _steps = new List<HistoricalContentPipelineStep>
        {
            new HistoricalContentPipelineStep(
                HistoricalContentPipelineStepId.CanonicalArchiveBake,
                "1. 선수 아카이브 굽기 (파이썬)",
                "KBO 정규화 캐시로 선수·구단 1:1 정본과 Runtime 정제본을 만듭니다. 실명은 Editor 원본에만 남습니다.",
                NormalizedCacheDirectory,
                HistoricalContentPipelineStatus.EditorArchiveRoot),
            new HistoricalContentPipelineStep(
                HistoricalContentPipelineStepId.RuntimeContentExport,
                "2. Runtime 콘텐츠 내보내기",
                "정제본을 검증하고 Player Build용 TextAsset·Catalog로 묶어 NewGameDefinition에 연결합니다.",
                HistoricalRuntimeContentExporter.SourceRoot,
                HistoricalRuntimeContentExporter.RuntimeRoot),
            new HistoricalContentPipelineStep(
                HistoricalContentPipelineStepId.WorldHistoryBake,
                "3. World History 굽기",
                "확정된 콘텐츠·밸런스로 구단주·커리어 Seed의 44시즌을 미리 시뮬레이션합니다. 몇 분 걸립니다.",
                HistoricalRuntimeContentExporter.RuntimeRoot,
                HistoricalContentPipelineStatus.BakedWorldHistoryRoot)
        };

        private readonly ConcurrentQueue<string> _pendingOutput = new ConcurrentQueue<string>();
        private readonly StringBuilder _log = new StringBuilder();
        private readonly HashSet<HistoricalContentPipelineStepId> _requested =
            new HashSet<HistoricalContentPipelineStepId>();

        private HistoricalCanonicalBakeOptions _options = new HistoricalCanonicalBakeOptions();
        private Process _process;
        private Stopwatch _stepWatch;
        private int _cursor;
        private bool _isCanceling;

        public event Action Changed;

        public IReadOnlyList<HistoricalContentPipelineStep> Steps => _steps;
        public bool IsRunning { get; private set; }
        public string Log => _log.ToString();

        public void ClearLog()
        {
            _log.Clear();
            Changed?.Invoke();
        }

        /// <summary>선택한 단계만 정의된 순서대로 실행한다. 이미 실행 중이면 무시한다.</summary>
        public void Start(
            IEnumerable<HistoricalContentPipelineStepId> stepIds,
            HistoricalCanonicalBakeOptions options)
        {
            if (IsRunning || stepIds == null)
                return;

            _requested.Clear();
            foreach (HistoricalContentPipelineStepId id in stepIds)
                _requested.Add(id);
            if (_requested.Count == 0)
                return;

            _options = options ?? new HistoricalCanonicalBakeOptions();
            for (int index = 0; index < _steps.Count; index++)
                _steps[index].Reset();

            _cursor = 0;
            _isCanceling = false;
            IsRunning = true;
            AppendLog("===== 파이프라인 시작 " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " =====");
            EditorApplication.update += Tick;
            Changed?.Invoke();
        }

        /// <summary>
        /// 파이썬 단계는 프로세스를 종료해 즉시 멈춘다.
        /// 2·3단계는 동기 호출이라 중간에 끊을 수 없고, 그 단계가 끝난 뒤 다음으로 넘어가지 않는다.
        /// </summary>
        public void Cancel()
        {
            if (!IsRunning || _isCanceling)
                return;

            _isCanceling = true;
            AppendLog("취소를 요청했습니다.");
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (InvalidOperationException)
            {
                // 이미 끝난 프로세스다. 마무리는 Tick이 이어서 한다.
            }
            Changed?.Invoke();
        }

        private void Tick()
        {
            if (_process != null)
            {
                DrainProcessOutput();
                if (!_process.HasExited)
                    return;
                CompleteProcessStep();
                return;
            }

            if (_cursor >= _steps.Count)
            {
                Finish();
                return;
            }

            HistoricalContentPipelineStep step = _steps[_cursor];
            if (!_requested.Contains(step.Id))
            {
                _cursor++;
                return;
            }
            if (_isCanceling)
            {
                step.State = HistoricalContentPipelineStepState.Canceled;
                Finish();
                return;
            }

            BeginStep(step);
        }

        private void BeginStep(HistoricalContentPipelineStep step)
        {
            step.State = HistoricalContentPipelineStepState.Running;
            _stepWatch = Stopwatch.StartNew();
            AppendLog("--- " + step.Title + " 시작 ---");
            Changed?.Invoke();

            if (step.Id == HistoricalContentPipelineStepId.CanonicalArchiveBake)
            {
                StartCanonicalBakeProcess(step);
                return;
            }

            try
            {
                if (step.Id == HistoricalContentPipelineStepId.RuntimeContentExport)
                    HistoricalRuntimeContentExporter.ExportFromToolLauncher();
                else
                    WorldHistoryBakeTool.BakeAll();
                SucceedStep(step);
            }
            catch (Exception exception)
            {
                AppendLog(exception.ToString());
                FailStep(step, exception.Message);
            }
        }

        private void StartCanonicalBakeProcess(HistoricalContentPipelineStep step)
        {
            string workingDirectory = Path.GetFullPath(ImporterDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                FailStep(step, "임포터 디렉터리가 없습니다: " + workingDirectory);
                return;
            }
            if (!Directory.Exists(Path.GetFullPath(NormalizedCacheDirectory)))
            {
                FailStep(
                    step,
                    "KBO 정규화 캐시가 없습니다. fetch_kbo.py로 먼저 수집해야 합니다: " + NormalizedCacheDirectory);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _options.UvExecutablePath,
                Arguments = BuildCanonicalBakeArguments(),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            AppendLog("$ " + startInfo.FileName + " " + startInfo.Arguments);

            try
            {
                _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _process.OutputDataReceived += HandleProcessOutput;
                _process.ErrorDataReceived += HandleProcessOutput;
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception exception) when (exception is InvalidOperationException ||
                                              exception is System.ComponentModel.Win32Exception)
            {
                DisposeProcess();
                FailStep(
                    step,
                    _options.UvExecutablePath + " 실행에 실패했습니다. uv 경로를 확인하세요. (" + exception.Message + ")");
            }
        }

        /// <summary>Editor 원본 경로에 공백이 있으므로 따옴표로 감싼다.</summary>
        private string BuildCanonicalBakeArguments()
        {
            return "run python " + CanonicalBakeScript +
                   " --input-dir .cache/KBOImport/Normalized" +
                   " --years " + _options.Years +
                   " --seed " + _options.GenerationSeed.ToString(CultureInfo.InvariantCulture) +
                   " --editor-assets-dir \"../../" + HistoricalContentPipelineStatus.EditorArchiveRoot + "\"" +
                   " --verify-editor-assets";
        }

        private void HandleProcessOutput(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Data))
                _pendingOutput.Enqueue(args.Data);
        }

        private void DrainProcessOutput()
        {
            bool hasOutput = false;
            while (_pendingOutput.TryDequeue(out string line))
            {
                _log.AppendLine(line);
                hasOutput = true;
            }
            if (hasOutput)
                Changed?.Invoke();
        }

        private void CompleteProcessStep()
        {
            int exitCode = _process.ExitCode;
            DrainProcessOutput();
            DisposeProcess();

            HistoricalContentPipelineStep step = _steps[_cursor];
            if (_isCanceling)
            {
                step.State = HistoricalContentPipelineStepState.Canceled;
                step.ElapsedSeconds = _stepWatch.Elapsed.TotalSeconds;
                AppendLog("--- " + step.Title + " 취소됨 ---");
                Finish();
                return;
            }
            if (exitCode != 0)
            {
                FailStep(step, "프로세스가 코드 " + exitCode.ToString(CultureInfo.InvariantCulture) + "로 끝났습니다.");
                return;
            }

            // 외부 프로세스가 Assets 아래 파일을 바꿨으므로 다음 단계가 읽기 전에 임포트해야 한다.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SucceedStep(step);
        }

        private void DisposeProcess()
        {
            if (_process == null)
                return;
            _process.OutputDataReceived -= HandleProcessOutput;
            _process.ErrorDataReceived -= HandleProcessOutput;
            _process.Dispose();
            _process = null;
        }

        private void SucceedStep(HistoricalContentPipelineStep step)
        {
            step.State = HistoricalContentPipelineStepState.Succeeded;
            step.ElapsedSeconds = _stepWatch.Elapsed.TotalSeconds;
            AppendLog("--- " + step.Title + " 완료 (" +
                      step.ElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture) + "초) ---");
            _cursor++;
            Changed?.Invoke();
        }

        private void FailStep(HistoricalContentPipelineStep step, string message)
        {
            step.State = HistoricalContentPipelineStepState.Failed;
            step.ElapsedSeconds = _stepWatch != null ? _stepWatch.Elapsed.TotalSeconds : 0d;
            step.Message = message;
            AppendLog("--- " + step.Title + " 실패: " + message + " ---");
            Debug.LogError("[역사 콘텐츠 파이프라인] " + step.Title + " 실패: " + message);
            Finish();
        }

        private void Finish()
        {
            EditorApplication.update -= Tick;
            DisposeProcess();
            IsRunning = false;
            _isCanceling = false;
            AppendLog("===== 파이프라인 종료 " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " =====");
            Changed?.Invoke();
        }

        private void AppendLog(string line)
        {
            _log.AppendLine(line);
            Changed?.Invoke();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Baseball.Game.Diagnostics
{
    /// <summary>타이틀의 구단주 모드 클릭부터 홈 화면이 실제로 렌더된 시점까지의 소요 시간을 단계별로 기록한다.</summary>
    /// <remarks>
    /// 진입 딜레이는 특정 한 곳이 아니라 세이브 로드·스냅샷 생성·런타임 UI 합성 중 어디서든 발생할 수 있어
    /// 총 시간만으로는 원인을 지목할 수 없다. 그래서 구간별 누적 시간을 함께 남긴다.
    /// 마감은 LateUpdate가 아니라 WaitForEndOfFrame이다 — 이 화면은 런타임에 UI를 통째로 합성하므로
    /// Canvas 레이아웃 리빌드와 렌더 비용이 체감 딜레이의 일부이고, LateUpdate 기준으로 끊으면 그게 빠진다.
    /// 계측 자체가 릴리스 빌드 비용이 되지 않도록 에디터·개발 빌드에서만 동작한다.
    /// </remarks>
    public static class OwnerModeEntryProfiler
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string LogPrefix = "[OwnerEntry]";

        private static readonly Stopwatch Watch = new Stopwatch();
        private static readonly List<Step> Steps = new List<Step>();
        private static readonly StringBuilder Report = new StringBuilder();

        private static string _trigger;
        private static double _lastMilliseconds;
        private static int _beginFrame;
        private static bool _isAwaitingRender;
        private static FrameRunner _runner;

        private readonly struct Step
        {
            public Step(string name, double elapsedMilliseconds, double deltaMilliseconds, int frameOffset)
            {
                Name = name;
                ElapsedMilliseconds = elapsedMilliseconds;
                DeltaMilliseconds = deltaMilliseconds;
                FrameOffset = frameOffset;
            }

            public string Name { get; }
            public double ElapsedMilliseconds { get; }
            public double DeltaMilliseconds { get; }
            public int FrameOffset { get; }
        }

        /// <summary>렌더가 끝난 시점에 계측을 마감하기 위한 최소 실행기.</summary>
        private sealed class FrameRunner : MonoBehaviour
        {
            public IEnumerator CompleteAfterRender()
            {
                yield return new WaitForEndOfFrame();
                CompleteAfterRenderInternal();
            }
        }
#endif

        /// <summary>계측이 진행 중인지 여부. 진행 중이 아니면 모든 Mark는 무시된다.</summary>
        public static bool IsMeasuring
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return Watch.IsRunning;
#else
                return false;
#endif
            }
        }

        /// <summary>구단주 모드 진입 클릭 시점에 계측을 시작한다.</summary>
        public static void Begin(string trigger)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Watch.IsRunning)
            {
                Debug.LogWarning(
                    $"{LogPrefix} 이전 계측({_trigger})이 마감되지 않은 채 새 계측이 시작됐습니다. " +
                    $"이전 기록 {Watch.Elapsed.TotalMilliseconds:F1}ms는 폐기합니다.");
            }

            Steps.Clear();
            _trigger = trigger;
            _lastMilliseconds = 0d;
            _beginFrame = Time.frameCount;
            _isAwaitingRender = false;
            Watch.Restart();
            Debug.Log($"{LogPrefix} 시작 · {trigger}");
#endif
        }

        /// <summary>진입 경로의 한 단계가 끝난 시점을 기록한다.</summary>
        public static void Mark(string step)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Watch.IsRunning)
                return;

            double elapsed = Watch.Elapsed.TotalMilliseconds;
            Steps.Add(new Step(step, elapsed, elapsed - _lastMilliseconds, Time.frameCount - _beginFrame));
            _lastMilliseconds = elapsed;
#endif
        }

        /// <summary>홈 화면 구성이 끝났음을 알리고, 렌더가 끝나는 프레임 끝에서 계측을 마감하도록 예약한다.</summary>
        public static void MarkHomeComposed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Watch.IsRunning || _isAwaitingRender)
                return;

            Mark("홈 화면 구성 완료");
            _isAwaitingRender = true;
            EnsureRunner().StartCoroutine(_runner.CompleteAfterRender());
#endif
        }

        /// <summary>진입이 실패해 홈에 도달하지 못했을 때 계측을 폐기한다.</summary>
        public static void Abort(string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Watch.IsRunning)
                return;

            Watch.Stop();
            _isAwaitingRender = false;
            Debug.LogWarning($"{LogPrefix} 중단 · {reason} ({Watch.Elapsed.TotalMilliseconds:F1}ms)");
            Steps.Clear();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static FrameRunner EnsureRunner()
        {
            if (_runner != null)
                return _runner;

            var host = new GameObject("OwnerModeEntryProfiler")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<FrameRunner>();
            return _runner;
        }

        private static void CompleteAfterRenderInternal()
        {
            if (!Watch.IsRunning)
                return;

            Mark("Canvas 리빌드·렌더");
            Watch.Stop();
            _isAwaitingRender = false;
            double total = Watch.Elapsed.TotalMilliseconds;
            int frames = Time.frameCount - _beginFrame + 1;

            Report.Clear();
            Report.Append(LogPrefix).Append(" 진입 완료 · ").Append(_trigger)
                .Append(" · 총 ").AppendFormat("{0:F1}", total).Append("ms · ")
                .Append(frames).Append("프레임");
            for (int index = 0; index < Steps.Count; index++)
            {
                Step step = Steps[index];
                Report.AppendLine().Append("  ")
                    .AppendFormat("{0,8:F1}", step.DeltaMilliseconds).Append("ms  ")
                    .Append("[+").Append(step.FrameOffset).Append("f] ")
                    .Append(step.Name)
                    .Append("  (누적 ").AppendFormat("{0:F1}", step.ElapsedMilliseconds).Append("ms)");
            }

            Debug.Log(Report.ToString());
            Steps.Clear();
        }
#endif
    }
}

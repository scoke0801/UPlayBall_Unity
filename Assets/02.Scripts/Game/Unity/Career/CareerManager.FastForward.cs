using System;
using Baseball.Game.Diagnostics;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Baseball.Game.Career
{
    public sealed partial class CareerManager
    {
        private static readonly ProfilerSection FastForwardStepMarker =
            new("Career.FastForward.Step");
        private static readonly ProfilerSection FastForwardFinalizeMarker =
            new("Career.FastForward.Finalize");
        private static readonly long ProgressSnapshotIntervalTicks = Stopwatch.Frequency / 10;

        private SeasonFastForwardSession _fastForwardSession;
        private SeasonFastForwardProgressView _seasonFastForwardProgress;
        private SeasonFastForwardPerformanceReport? _lastFastForwardPerformance;
        private long _lastFastForwardSnapshotTimestamp;

        public bool IsSeasonFastForwardRunning => _fastForwardSession != null;
        public SeasonFastForwardExecutionMode FastForwardExecutionMode =>
            SeasonFastForwardExecutionMode.CooperativeMainThread;
        public SeasonFastForwardProgressView SeasonFastForwardProgress => _seasonFastForwardProgress;
        public SeasonFastForwardPerformanceReport? LastFastForwardPerformance => _lastFastForwardPerformance;

        /// <summary>직전 자동 진행의 Player 환경·버전·성능 수치를 한 번에 복사할 문자열로 반환한다.</summary>
        public string CreateLastFastForwardRuntimeReport()
        {
            if (!_lastFastForwardPerformance.HasValue || CurrentCareer == null)
                return string.Empty;
            return SeasonFastForwardRuntimeReport.Create(
                _lastFastForwardPerformance.Value,
                CurrentCareer.CurrentLeague.CurrentSeason.VersionStamp);
        }

        /// <summary>진행 팝업을 먼저 그릴 수 있도록 계산하지 않은 자동 진행 세션만 연다.</summary>
        public bool BeginSeasonFastForward()
        {
            if (CurrentCareer == null || _balance == null)
                return Fail("진행 중인 커리어가 없습니다.");
            if (_fastForwardSession != null)
                return Fail("이미 시즌 자동 진행 중입니다.");
            if (_activeMatch != null)
                return Fail("준비하거나 진행 중인 경기를 먼저 마쳐야 합니다.");
            if (CurrentCareer.Narrative.PendingReaction != null)
                return Fail("먼저 경기 후 질문에 답해 주세요.");

            try
            {
                _fastForwardSession = new SeasonFastForwardSession(CurrentCareer, _balance);
                _seasonFastForwardProgress = BuildFastForwardProgress(
                    _fastForwardSession.CreateProgressSnapshot());
                _lastFastForwardSnapshotTimestamp = Stopwatch.GetTimestamp();
                _lastFastForwardPerformance = null;
                LastError = string.Empty;
                return true;
            }
            catch (InvalidOperationException exception)
            {
                _fastForwardSession = null;
                return Fail(exception.Message);
            }
        }

        /// <summary>메인 스레드 한 프레임에서 안전 커밋 단위를 하나만 진행한다.</summary>
        public bool AdvanceSeasonFastForwardFrame()
        {
            if (_fastForwardSession == null)
                return Fail("진행 중인 시즌 자동 진행 세션이 없습니다.");

            try
            {
                SeasonFastForwardStepResult result;
                using (FastForwardStepMarker.Auto())
                    result = _fastForwardSession.AdvanceBatch(maximumSteps: 1);
                long now = Stopwatch.GetTimestamp();
                if (result.IsCompleted ||
                    now - _lastFastForwardSnapshotTimestamp >= ProgressSnapshotIntervalTicks)
                {
                    _seasonFastForwardProgress = BuildFastForwardProgress(result);
                    _lastFastForwardSnapshotTimestamp = now;
                }
                if (result.IsCompleted)
                    FinalizeFastForward();
                return true;
            }
            catch (Exception exception)
            {
                if (_fastForwardSession != null)
                    _lastFastForwardPerformance = _fastForwardSession.CreatePerformanceReport();
                _fastForwardSession = null;
                return Fail($"시즌 자동 진행 중 오류가 발생했습니다. {exception.Message}");
            }
        }

        /// <summary>완료한 안전 경계까지는 유지하고 다음 라운드 진입을 중단한다.</summary>
        public bool StopSeasonFastForward()
        {
            if (_fastForwardSession == null)
                return false;
            SeasonFastForwardStepResult result = _fastForwardSession.StopByUser();
            _seasonFastForwardProgress = BuildFastForwardProgress(result);
            _lastFastForwardPerformance = _fastForwardSession.CreatePerformanceReport();
            _fastForwardSession = null;
            RefreshSeasonServices();
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        /// <summary>화면 종료 시 다음 안전 단위를 시작하지 않고 세션 참조를 정리한다.</summary>
        public void AbortSeasonFastForwardForSceneUnload()
        {
            if (_fastForwardSession == null)
                return;
            _fastForwardSession.AbortBySceneUnload();
            _lastFastForwardPerformance = _fastForwardSession.CreatePerformanceReport();
            _fastForwardSession = null;
            RefreshSeasonServices();
        }

        private void FinalizeFastForward()
        {
            using (FastForwardFinalizeMarker.Auto())
            {
                _lastSeasonAutoCompletion = _fastForwardSession.CreateCompletedResult();
                _lastFastForwardPerformance = _fastForwardSession.CreatePerformanceReport();
                _fastForwardSession = null;
                RefreshSeasonServices();
                TryCompleteDeclaredRetirement();
                _lastGame = null;
                LastError = string.Empty;
                CareerChanged?.Invoke();
            }
        }

        private SeasonFastForwardProgressView BuildFastForwardProgress(
            SeasonFastForwardStepResult progress)
        {
            CareerDashboardView dashboard = BuildDashboard();
            var articles = CurrentCareer.News.CurrentSeasonArticles;
            string latestNewsHeadline = articles.Count == 0
                ? string.Empty
                : articles[articles.Count - 1].Headline;
            return new SeasonFastForwardProgressView(
                progress,
                CurrentCareer.World.Calendar.CurrentDate,
                dashboard.PlayerName,
                dashboard.TeamName,
                dashboard.TeamRank,
                dashboard.TeamWins,
                dashboard.TeamLosses,
                dashboard.TeamTies,
                dashboard.Statistics,
                latestNewsHeadline);
        }

        private void ResetFastForwardRuntime()
        {
            _fastForwardSession = null;
            _seasonFastForwardProgress = default;
            _lastFastForwardPerformance = null;
            _lastFastForwardSnapshotTimestamp = 0L;
        }
    }
}

using System;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private ManagerRegularSeasonSimulationSession _regularSeasonSimulationSession;
        private ManagerRegularSeasonSimulationWorker _regularSeasonSimulationWorker;
        private ManagerHistoricalRuntimeState _regularSeasonSimulationCopy;
        private ManagerRegularSeasonSimulationProgress _regularSeasonSimulationProgress;
        private ManagerRegularSeasonCompletionResult _lastRegularSeasonCompletion;
        private OwnerPostseasonSimulationSession _postseasonSimulationSession;
        private OwnerPostseasonSimulationProgress _postseasonSimulationProgress;

        public bool IsRegularSeasonSimulationRunning => _regularSeasonSimulationSession != null;
        public ManagerRegularSeasonSimulationProgress RegularSeasonSimulationProgress =>
            _regularSeasonSimulationProgress;
        public ManagerRegularSeasonCompletionResult LastRegularSeasonCompletion =>
            _lastRegularSeasonCompletion;

        /// <summary>독립된 Runtime 복사본을 단일 Worker에서 계산하고 기존 UI 상태는 유지한다.</summary>
        public bool BeginRegularSeasonSimulationInBackground(int weeks = 0)
        {
            if (!BeginRegularSeasonSimulation()) return false;
            try
            {
                // 저장 DTO 복원으로 가변 상태를 분리한다. Unity 공급자 호출과 복원은 메인 스레드에서 끝낸다.
                var copy = _saveAdapter.CreateSimulationCopy(Runtime);
                var service = new ManagerModeMatchService(_contentProvider.Load(), _balance,
                    teamColors: _teamColors, tacticCards: _tacticCards, dugoutCatalog: _dugoutCatalog);
                _regularSeasonSimulationCopy = copy;
                _regularSeasonSimulationSession = new ManagerRegularSeasonSimulationSession(copy, service, weeks);
                _regularSeasonSimulationProgress = _regularSeasonSimulationSession.CreateProgressSnapshot();
                _regularSeasonSimulationWorker = new ManagerRegularSeasonSimulationWorker(
                    _regularSeasonSimulationSession,
                    _ => OwnerMatchAchievementResolver.TryUnlockLosingStreakSignature(copy));
                return true;
            }
            catch (Exception exception)
            {
                _regularSeasonSimulationCopy = null;
                _regularSeasonSimulationSession = null;
                LastError = exception.Message;
                UnityEngine.Debug.LogException(exception);
                return false;
            }
        }
        public bool IsPostseasonSimulationRunning => _postseasonSimulationSession != null;
        public OwnerPostseasonSimulationProgress PostseasonSimulationProgress => _postseasonSimulationProgress;
        public bool IsNextPostseasonGamePlayerMatch => _postseasonSimulationSession?.IsNextGamePlayerMatch == true;

        /// <summary>진행 세션의 다음 우리 구단 경기만 확정하고 중계 사건을 반환한다.</summary>
        public ManagerModeMatchResult PlayNextPostseasonGame(
            Baseball.Simulation.Match.IMatchEventSink eventSink,
            Baseball.Simulation.Match.MatchExecutionProfile executionProfile)
        {
            if (!IsNextPostseasonGamePlayerMatch)
                throw new InvalidOperationException("앞선 대진을 진행한 뒤 우리 구단 경기를 관전할 수 있습니다.");
            try
            {
                LastMatch = _postseasonSimulationSession.AdvanceNextStep(eventSink, executionProfile).PlayerMatch;
                CurrentPregame = null;
                RefreshAvailableTacticCards();
                return LastMatch;
            }
            finally
            {
                // 관전 중에는 후속 경기를 계산하지 않는다. 재진입은 저장된 시리즈에서 이어진다.
                StopPostseasonSimulation();
            }
        }

        /// <summary>모든 정규시즌을 확정하고 프레임 단위 포스트시즌 진행 세션을 연다.</summary>
        public bool BeginPostseasonSimulation()
        {
            if (Runtime == null || _matchService == null)
            {
                LastError = "진행 중인 구단주 시즌이 없습니다.";
                return false;
            }
            if (_regularSeasonSimulationSession != null || _postseasonSimulationSession != null)
            {
                LastError = "이미 시즌 시뮬레이션이 진행 중입니다.";
                return false;
            }
            try
            {
                if (!Runtime.LeagueWorld.IsRegularSeasonCompleted)
                    _matchService.CompleteRegularSeason(Runtime);
                _postseasonSimulationSession = new OwnerPostseasonSimulationSession(Runtime, _matchService, _balance);
                _postseasonSimulationProgress = _postseasonSimulationSession.CreateProgressSnapshot();
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                _postseasonSimulationSession = null;
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>한 프레임에 포스트시즌 상세 경기 한 건만 확정한다.</summary>
        public bool AdvancePostseasonSimulationFrame()
        {
            if (_postseasonSimulationSession == null)
            {
                LastError = "진행 중인 포스트시즌이 없습니다.";
                return false;
            }
            try
            {
                OwnerPostseasonAdvanceResult result = _postseasonSimulationSession.AdvanceNextStep();
                if (result.PlayerMatch != null) LastMatch = result.PlayerMatch;
                _postseasonSimulationProgress = _postseasonSimulationSession.CreateProgressSnapshot();
                if (!_postseasonSimulationSession.IsCompleted) return true;
                _postseasonSimulationSession = null;
                CurrentPregame = null;
                LastError = string.Empty;
                NotifyRuntimeChanged();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                _postseasonSimulationProgress = _postseasonSimulationSession.CreateProgressSnapshot();
                _postseasonSimulationSession = null;
                LastError = $"포스트시즌 시뮬레이션 중 오류가 발생했습니다. {exception.Message}";
                NotifyRuntimeChanged();
                return false;
            }
        }

        public bool StopPostseasonSimulation()
        {
            if (_postseasonSimulationSession == null) return false;
            _postseasonSimulationProgress = _postseasonSimulationSession.CreateProgressSnapshot();
            _postseasonSimulationSession = null;
            LastError = string.Empty;
            NotifyRuntimeChanged();
            return true;
        }

        /// <summary>진행 팝업을 먼저 그릴 수 있도록 계산하지 않은 정규시즌 자동 진행 세션만 연다.</summary>
        public bool BeginRegularSeasonSimulation()
        {
            if (Runtime == null || _matchService == null)
            {
                LastError = "진행 중인 구단주 시즌이 없습니다.";
                return false;
            }
            if (_regularSeasonSimulationSession != null || _postseasonSimulationSession != null)
            {
                LastError = "이미 정규시즌 시뮬레이션이 진행 중입니다.";
                return false;
            }
            try
            {
                if (Runtime.ManagerMode.LiveSeason.NextPlayerGame != null)
                {
                    ManagerPregamePreparation preparation = CurrentPregame ?? PrepareNextGame();
                    if (!preparation.CanStartGame)
                    {
                        LastError = "현재 선수단과 라인업으로 시즌 시뮬레이션을 시작할 수 없습니다.";
                        return false;
                    }
                }

                _regularSeasonSimulationSession = new ManagerRegularSeasonSimulationSession(Runtime, _matchService);
                _regularSeasonSimulationProgress = _regularSeasonSimulationSession.CreateProgressSnapshot();
                _lastRegularSeasonCompletion = null;
                LastUnlockedSignatureCardId = string.Empty;
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                _regularSeasonSimulationSession = null;
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>백그라운드 결과를 수신하거나 동기 세션의 경기 한 건을 진행한다.</summary>
        public bool AdvanceRegularSeasonSimulationFrame()
        {
            if (_regularSeasonSimulationWorker != null)
                return PollRegularSeasonSimulationWorker();
            if (_regularSeasonSimulationSession == null)
            {
                LastError = "진행 중인 정규시즌 시뮬레이션이 없습니다.";
                return false;
            }

            try
            {
                ManagerRegularSeasonSimulationStepResult step =
                    _regularSeasonSimulationSession.AdvanceNextStep();
                _regularSeasonSimulationProgress = step.Progress;
                CurrentPregame = null;
                if (step.MatchResult != null)
                {
                    LastMatch = step.MatchResult;
                    if (TryUnlockLosingStreakSignature())
                        LastUnlockedSignatureCardId = LosingStreakSignatureCardId;
                }

                if (!_regularSeasonSimulationSession.IsCompleted)
                    return true;

                _lastRegularSeasonCompletion = _regularSeasonSimulationSession.CreateCompletionResult();
                _regularSeasonSimulationSession = null;
                RefreshAvailableTacticCards();
                LastError = string.Empty;
                NotifyRuntimeChanged();
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                if (_regularSeasonSimulationSession != null)
                    _regularSeasonSimulationProgress = _regularSeasonSimulationSession.CreateProgressSnapshot();
                _regularSeasonSimulationSession = null;
                CurrentPregame = null;
                RefreshAvailableTacticCards();
                LastError = $"정규시즌 시뮬레이션 중 오류가 발생했습니다. {exception.Message}";
                NotifyRuntimeChanged();
                return false;
            }
        }

        /// <summary>완료한 경기까지 유지하고 다음 경기 시뮬레이션을 시작하지 않는다.</summary>
        public bool StopRegularSeasonSimulation()
        {
            if (_regularSeasonSimulationSession == null)
                return false;
            if (_regularSeasonSimulationWorker != null)
            {
                _regularSeasonSimulationWorker.StopAndWait();
                return PollRegularSeasonSimulationWorker();
            }
            _regularSeasonSimulationProgress = _regularSeasonSimulationSession.StopByUser();
            _regularSeasonSimulationSession = null;
            CurrentPregame = null;
            RefreshAvailableTacticCards();
            LastError = string.Empty;
            NotifyRuntimeChanged();
            return true;
        }

        /// <summary>화면 종료 시 완료한 경기까지 유지하고 작업과 세션 참조를 정리한다.</summary>
        public void AbortRegularSeasonSimulationForSceneUnload()
        {
            if (_regularSeasonSimulationWorker != null)
            {
                _regularSeasonSimulationWorker.AbortAndWait();
                PollRegularSeasonSimulationWorker(notify: false);
            }
            if (_regularSeasonSimulationSession != null)
            {
                _regularSeasonSimulationProgress = _regularSeasonSimulationSession.AbortBySceneUnload();
                _regularSeasonSimulationSession = null;
                CurrentPregame = null;
                RefreshAvailableTacticCards();
            }
            _postseasonSimulationSession = null;
        }

        private bool PollRegularSeasonSimulationWorker(bool notify = true)
        {
            var worker = _regularSeasonSimulationWorker;
            _regularSeasonSimulationProgress = worker.ReadProgress();
            if (!worker.IsCompleted) return true;
            // 완료된 Task와 동기화한 후에만 복사본을 공개한다. 중단도 완료된 경기까지 반영한다.
            bool hadSignature = Runtime.TacticCollection.Contains(LosingStreakSignatureCardId);
            Runtime = _regularSeasonSimulationCopy;
            // 이전 Runtime을 참조하는 경기 준비 캐시를 소유권 전환 시 함께 해제한다.
            _matchService = new ManagerModeMatchService(_contentProvider.Load(), _balance,
                teamColors: _teamColors, tacticCards: _tacticCards, dugoutCatalog: _dugoutCatalog);
            _regularSeasonSimulationCopy = null;
            _regularSeasonSimulationProgress = worker.ReadProgress();
            if (worker.LastMatch != null) LastMatch = worker.LastMatch;
            if (!hadSignature && Runtime.TacticCollection.Contains(LosingStreakSignatureCardId))
                LastUnlockedSignatureCardId = LosingStreakSignatureCardId;
            _lastRegularSeasonCompletion = _regularSeasonSimulationSession.IsCompleted
                ? _regularSeasonSimulationSession.CreateCompletionResult() : null;
            Exception fault = worker.Fault;
            _regularSeasonSimulationWorker = null;
            _regularSeasonSimulationSession = null;
            CurrentPregame = null;
            RefreshAvailableTacticCards();
            LastError = fault == null ? string.Empty : $"정규시즌 시뮬레이션 중 오류가 발생했습니다. {fault.Message}";
            if (fault != null) UnityEngine.Debug.LogException(fault);
            if (notify) NotifyRuntimeChanged();
            return fault == null;
        }

        private void EnsureRegularSeasonSimulationIsNotRunning()
        {
            if (_regularSeasonSimulationSession != null)
                throw new InvalidOperationException("정규시즌 시뮬레이션이 진행 중입니다.");
            if (_postseasonSimulationSession != null)
                throw new InvalidOperationException("포스트시즌 시뮬레이션이 진행 중입니다.");
        }
    }
}

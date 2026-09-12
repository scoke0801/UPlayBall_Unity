using System;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private ManagerRegularSeasonSimulationSession _regularSeasonSimulationSession;
        private ManagerRegularSeasonSimulationProgress _regularSeasonSimulationProgress;
        private ManagerRegularSeasonCompletionResult _lastRegularSeasonCompletion;
        private OwnerPostseasonSimulationSession _postseasonSimulationSession;
        private OwnerPostseasonSimulationProgress _postseasonSimulationProgress;

        public bool IsRegularSeasonSimulationRunning => _regularSeasonSimulationSession != null;
        public ManagerRegularSeasonSimulationProgress RegularSeasonSimulationProgress =>
            _regularSeasonSimulationProgress;
        public ManagerRegularSeasonCompletionResult LastRegularSeasonCompletion =>
            _lastRegularSeasonCompletion;
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
            if (_regularSeasonSimulationSession != null)
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

        /// <summary>메인 스레드가 양보할 수 있도록 정규시즌 Detailed 경기를 최대 한 건 진행한다.</summary>
        public bool AdvanceRegularSeasonSimulationFrame()
        {
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

        /// <summary>완료한 라운드까지 유지하고 다음 경기 시뮬레이션을 시작하지 않는다.</summary>
        public bool StopRegularSeasonSimulation()
        {
            if (_regularSeasonSimulationSession == null)
                return false;
            _regularSeasonSimulationProgress = _regularSeasonSimulationSession.StopByUser();
            _regularSeasonSimulationSession = null;
            CurrentPregame = null;
            RefreshAvailableTacticCards();
            LastError = string.Empty;
            NotifyRuntimeChanged();
            return true;
        }

        /// <summary>화면 종료 시 완료한 라운드까지만 유지하고 세션 참조를 정리한다.</summary>
        public void AbortRegularSeasonSimulationForSceneUnload()
        {
            if (_regularSeasonSimulationSession != null)
            {
                _regularSeasonSimulationProgress = _regularSeasonSimulationSession.AbortBySceneUnload();
                _regularSeasonSimulationSession = null;
                CurrentPregame = null;
                RefreshAvailableTacticCards();
            }
            _postseasonSimulationSession = null;
        }

        private void EnsureRegularSeasonSimulationIsNotRunning()
        {
            if (_regularSeasonSimulationSession != null || _postseasonSimulationSession != null)
                throw new InvalidOperationException("정규시즌 시뮬레이션이 진행 중입니다.");
        }
    }
}

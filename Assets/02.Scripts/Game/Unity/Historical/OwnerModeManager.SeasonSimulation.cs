using System;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private ManagerRegularSeasonSimulationSession _regularSeasonSimulationSession;
        private ManagerRegularSeasonSimulationProgress _regularSeasonSimulationProgress;
        private ManagerRegularSeasonCompletionResult _lastRegularSeasonCompletion;

        public bool IsRegularSeasonSimulationRunning => _regularSeasonSimulationSession != null;
        public ManagerRegularSeasonSimulationProgress RegularSeasonSimulationProgress =>
            _regularSeasonSimulationProgress;
        public ManagerRegularSeasonCompletionResult LastRegularSeasonCompletion =>
            _lastRegularSeasonCompletion;

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
            if (Runtime.ManagerMode.LiveSeason.NextPlayerGame == null)
            {
                LastError = "플레이어 구단의 남은 정규시즌 경기가 없습니다.";
                return false;
            }

            try
            {
                ManagerPregamePreparation preparation = CurrentPregame ?? PrepareNextGame();
                if (!preparation.CanStartGame)
                {
                    LastError = "현재 선수단과 라인업으로 시즌 시뮬레이션을 시작할 수 없습니다.";
                    return false;
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

        /// <summary>메인 스레드 한 프레임에서 플레이어 경기와 같은 라운드의 AI 대진만 진행한다.</summary>
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
            if (_regularSeasonSimulationSession == null)
                return;
            _regularSeasonSimulationProgress = _regularSeasonSimulationSession.AbortBySceneUnload();
            _regularSeasonSimulationSession = null;
            CurrentPregame = null;
            RefreshAvailableTacticCards();
        }

        private void EnsureRegularSeasonSimulationIsNotRunning()
        {
            if (_regularSeasonSimulationSession != null)
                throw new InvalidOperationException("정규시즌 시뮬레이션이 진행 중입니다.");
        }
    }
}

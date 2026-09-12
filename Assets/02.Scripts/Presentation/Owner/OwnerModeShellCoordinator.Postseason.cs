using UnityEngine;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private readonly OwnerPostseasonCelebrationGate _celebrationGate = new OwnerPostseasonCelebrationGate();
        private OwnerPostseasonCelebration _pendingCelebration;
        private UI_Popup_OwnerPostseasonCelebration _celebrationPopup;
        private float _celebrationAt;
        private bool _isCelebrationResultCaptured;
        private bool _isCelebrationFromReview;
        private readonly System.Collections.Generic.HashSet<string> _shownSeasonCelebrations =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        private bool _isNextOwnerMatchPending;
        private bool _continueAfterCelebration;
        private int _nextOwnerMatchRequestedFrame;

        private bool HasNextOwnerMatch()
        {
            var runtime = _manager?.Runtime;
            if (runtime == null) return false;
            if (!_isPostseasonMatchVisible)
                return runtime.ManagerMode.LiveSeason.NextPlayerGame != null;
            var group = runtime.LeagueWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            return group.Postseason?.HasRemainingGames(group.Season.PlayerTeamId) == true;
        }

        private void HandleNextOwnerMatchRequested()
        {
            if (!_isOwnerMatchVisible || _isTransitioningToOwnerMatch ||
                _matchSpectatorView?.IsComplete != true || !HasNextOwnerMatch()) return;
            _continueAfterCelebration = true;
            if (ShowPendingCelebration()) return;
            _continueAfterCelebration = false;
            // 안내 저장은 다음 포스트시즌 세션이 상태 변경을 잠그기 전에 끝낸다.
            ExecuteOperation(() => _manager.PublishGuideMatchResult());
            _isNextOwnerMatchPending = true;
            _isTransitioningToOwnerMatch = true;
            _nextOwnerMatchRequestedFrame = Time.frameCount;
            _matchSpectatorView.SetNextGameAvailability(true, true);
        }

        // 앞선 다른 대진은 프레임별로 확정하고, 우리 구단 경기 직전에 기존 관전 경로로 넘긴다.
        private bool UpdateNextOwnerMatch()
        {
            if (!_isNextOwnerMatchPending) return false;
            if (Time.frameCount <= _nextOwnerMatchRequestedFrame) return true;
            if (_isPostseasonMatchVisible)
            {
                bool succeeded = _manager.IsPostseasonSimulationRunning || _manager.BeginPostseasonSimulation();
                if (succeeded && !_manager.IsNextPostseasonGamePlayerMatch)
                    succeeded = _manager.AdvancePostseasonSimulationFrame();
                if (!succeeded || !HasNextOwnerMatch())
                {
                    ReportSeasonProgressError("다음 경기를 준비하지 못했습니다. 대진으로 돌아가 진행 상태를 확인해 주세요.");
                    _manager.StopPostseasonSimulation();
                    _isNextOwnerMatchPending = _isTransitioningToOwnerMatch = false;
                    _matchSpectatorView.SetNextGameAvailability(HasNextOwnerMatch());
                    _matchSpectatorView.FocusCompletedResult();
                    return true;
                }
                if (!_manager.IsNextPostseasonGamePlayerMatch) return true;
            }
            _isNextOwnerMatchPending = false;
            PlayOwnerMatchSpectator();
            return true;
        }

        private void BeginPostseasonPresentation()
        {
            ResetPostseasonPresentation();
            if (_isPostseasonMatchVisible) _celebrationGate.Begin(_manager.CreateSeasonReview());
        }

        private void HandlePostseasonPresentationCompleted()
        {
            _matchSpectatorView.SetNextGameAvailability(HasNextOwnerMatch());
            _matchSpectatorView.FocusCompletedResult();
            CaptureCompletedCelebration();
        }

        // 완료 통지와 복귀 버튼이 같은 공개 처리를 거쳐 결과를 한 번만 소비한다.
        private void CaptureCompletedCelebration()
        {
            if (_isCelebrationResultCaptured || !_isOwnerMatchVisible || !_isPostseasonMatchVisible ||
                _matchSpectatorView?.IsComplete != true) return;
            _isCelebrationResultCaptured = true;
            var snapshot = _manager.CreateSeasonReview();
            _pendingCelebration = _celebrationGate.Reveal(snapshot, true)
                ?? OwnerPostseasonCelebration.CreateChampion(snapshot);
            _celebrationAt = Time.unscaledTime + OwnerPostseasonPresentationData.Load().resultHold;
        }

        private void UpdatePostseasonCelebration()
        {
            CaptureCompletedCelebration();
            if (_pendingCelebration != null && _isPostseasonMatchVisible && _isOwnerMatchVisible &&
                _matchSpectatorView.IsComplete && Time.unscaledTime >= _celebrationAt)
                ShowPendingCelebration();
        }

        private bool ShowPendingCelebration()
        {
            CaptureCompletedCelebration();
            if (_pendingCelebration == null) return false;
            OwnerPostseasonCelebration result = _pendingCelebration;
            _pendingCelebration = null;
            string key = $"{_manager.Runtime.WorldHistory.WorldHistorySeed}/{result.SeasonNumber}/{result.LeagueGrade}/{result.TeamKey}/{result.Kind}";
            if (_shownSeasonCelebrations.Contains(key)) return false;
            if (_celebrationPopup == null)
            {
                _celebrationPopup = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(_shell.PopupHost);
                _celebrationPopup.ContinueRequested += HandleCelebrationContinue;
                _celebrationPopup.RecordsRequested += HandleCelebrationRecords;
            }
            _celebrationPopup.Show(result, _manager.GetTeamDisplayName, _manager.GetTeamUniformFranchiseId(result.TeamKey),
                !_isCelebrationFromReview);
            _shownSeasonCelebrations.Add(key);
            return true;
        }

        private void ShowSeasonReviewCelebration(Baseball.Game.Historical.OwnerSeasonReviewSnapshot snapshot, int page)
        {
            // 월드 전체 종료를 기다리지 않는다. 우리 조 우승은 결승이 끝나는 즉시 확정된다.
            _pendingCelebration = page == 0 ? OwnerPostseasonCelebration.CreatePennantWinner(snapshot)
                : OwnerPostseasonCelebration.CreateChampion(snapshot);
            _isCelebrationFromReview = true;
            if (!ShowPendingCelebration()) _isCelebrationFromReview = false;
        }

        private void HandleCelebrationContinue()
        {
            _celebrationPopup.Hide();
            if (_isCelebrationFromReview)
            {
                _isCelebrationFromReview = false;
                _seasonReviewPopup.Show();
                return;
            }
            if (_continueAfterCelebration)
            {
                _continueAfterCelebration = false;
                HandleNextOwnerMatchRequested();
                return;
            }
            HandleOwnerMatchHomeRequested();
        }

        private void HandleCelebrationRecords()
        {
            _continueAfterCelebration = false;
            _celebrationPopup.Hide();
            _matchSpectatorView?.FocusCompletedResult();
        }

        private void ResetPostseasonPresentation()
        {
            _isCelebrationResultCaptured = false;
            _isCelebrationFromReview = false;
            _continueAfterCelebration = false;
            _celebrationGate.Clear();
            _pendingCelebration = null;
            _celebrationPopup?.Hide();
        }

        private void DestroyPostseasonPresentation()
        {
            ResetPostseasonPresentation();
            if (_celebrationPopup == null) return;
            if (Application.isPlaying) Destroy(_celebrationPopup.gameObject);
            else DestroyImmediate(_celebrationPopup.gameObject);
        }
    }
}

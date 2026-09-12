using UnityEngine;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private readonly OwnerPostseasonCelebrationGate _celebrationGate = new OwnerPostseasonCelebrationGate();
        private OwnerPostseasonCelebration _pendingCelebration;
        private UI_Popup_OwnerPostseasonCelebration _celebrationPopup;
        private float _celebrationAt;

        private void BeginPostseasonPresentation()
        {
            ResetPostseasonPresentation();
            if (_isPostseasonMatchVisible) _celebrationGate.Begin(_manager.CreateSeasonReview());
        }

        private void HandlePostseasonPresentationCompleted()
        {
            if (!_isPostseasonMatchVisible || !_matchSpectatorView.IsComplete) return;
            _pendingCelebration = _celebrationGate.Reveal(_manager.CreateSeasonReview(), true);
            _celebrationAt = Time.unscaledTime + OwnerPostseasonPresentationData.Load().resultHold;
        }

        private void UpdatePostseasonCelebration()
        {
            if (_pendingCelebration != null && _isPostseasonMatchVisible && _isOwnerMatchVisible &&
                _matchSpectatorView.IsComplete && Time.unscaledTime >= _celebrationAt)
                ShowPendingCelebration();
        }

        private bool ShowPendingCelebration()
        {
            if (_pendingCelebration == null) return false;
            if (_celebrationPopup == null)
            {
                _celebrationPopup = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(_shell.PopupHost);
                _celebrationPopup.ContinueRequested += HandleCelebrationContinue;
                _celebrationPopup.RecordsRequested += HandleCelebrationRecords;
            }
            OwnerPostseasonCelebration result = _pendingCelebration;
            _pendingCelebration = null;
            _celebrationPopup.Show(result, _manager.GetTeamDisplayName);
            return true;
        }

        private void HandleCelebrationContinue()
        {
            _celebrationPopup.Hide();
            HandleOwnerMatchHomeRequested();
        }

        private void HandleCelebrationRecords()
        {
            _celebrationPopup.Hide();
            _matchSpectatorView?.FocusCompletedResult();
        }

        private void ResetPostseasonPresentation()
        {
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

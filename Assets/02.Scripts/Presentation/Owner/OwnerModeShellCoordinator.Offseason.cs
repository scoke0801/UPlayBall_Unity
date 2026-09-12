using System;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private void ShowOffseasonExit()
        {
            if (_offseasonExitPopup == null)
            {
                _offseasonExitPopup = UI_Popup_OwnerOffseason.CreateRuntime(_shell.PopupHost);
                _offseasonExitPopup.TrainingRequested += HandleOffseasonTrainingRequested;
                _offseasonExitPopup.SeasonAdvanceRequested += HandleOffseasonExitConfirmed;
            }
            _offseasonExitSeasonId = _manager.Runtime.ManagerMode.LiveSeason.SeasonId;
            _offseasonExitPopup.ShowSeasonExit(OwnerOffseasonPresentationModel.Build(_manager));
        }

        private void HandleOffseasonTrainingRequested()
        {
            _offseasonExitPopup.Hide();
            HandleNavigationRequested(OwnerNavigationRoutes.PowerUpStudy);
        }

        private void HandleOffseasonExitConfirmed(int expectedCompletedWeeks)
        {
            var runtime = _manager.Runtime;
            if (!string.Equals(_offseasonExitSeasonId, runtime.ManagerMode.LiveSeason.SeasonId, StringComparison.Ordinal) ||
                expectedCompletedWeeks != runtime.PlayerGrowth.Offseason.CompletedWeeks || runtime.PlayerGrowth.StudyProjects.Count > 0)
            {
                ShowOffseasonExit();
                return;
            }
            _hasConfirmedOffseasonExit = true;
            try { HandleAdvanceSeasonRequested(); }
            finally { _hasConfirmedOffseasonExit = false; }
            if (_offseasonExitPopup.IsVisible) _offseasonExitPopup.ShowError();
        }
    }
}

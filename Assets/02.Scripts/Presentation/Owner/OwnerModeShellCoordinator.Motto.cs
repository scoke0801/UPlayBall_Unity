using System;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private UI_Popup_OwnerMotto _mottoPopup;

        private void HandleEditMottoRequested()
        {
            if (_mottoPopup == null)
            {
                _mottoPopup = UI_Popup_OwnerMotto.CreateRuntime(_shell.PopupHost);
                _mottoPopup.SaveRequested += HandleMottoSaveRequested;
                _mottoPopup.CloseRequested += CloseMottoPopup;
            }
            _mottoPopup.Show(_manager.Runtime.OwnerProfile.Motto);
        }

        private void HandleMottoSaveRequested(string motto)
        {
            try { _manager.ChangeOwnerMotto(motto); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _mottoPopup.ShowError();
                return;
            }
            CloseMottoPopup();
        }

        private bool TryCloseMottoPopup()
        {
            if (_mottoPopup == null || !_mottoPopup.gameObject.activeInHierarchy) return false;
            CloseMottoPopup();
            return true;
        }

        private void CloseMottoPopup()
        {
            _mottoPopup.Hide();
            _sharedInformationWorkspace.FocusMottoButton();
        }
    }
}

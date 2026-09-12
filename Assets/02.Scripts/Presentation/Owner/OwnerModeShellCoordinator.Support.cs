using System.Collections.Generic;
using Baseball.Game.Historical;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private bool _hasConfirmedSupportLoss;
        private GameObject _supportLossPopup;
        private System.Action _cancelSupportLoss;
        private bool ShowSupportLossConfirmation()
        {
            if (_hasConfirmedSupportLoss || _pendingActiveRosterChange == null) return false;
            if (_supportLossPopup != null) return true;
            var names = new List<string>();
            var runtime = _manager.Runtime;
            foreach (var assignment in runtime.PlayerGrowth.Support.Assignments)
                if (!assignment.IsTeam && !ContainsRosterCard(_pendingActiveRosterChange.Roster, assignment.CardId))
                {
                    runtime.WorldCardCatalog.TryGetCard(assignment.CardId, out var card);
                    names.Add(runtime.IdentityRegistry.GetPresentationPlayerName(runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerPersonId));
                }
            if (names.Count == 0) return false;
            var root = OwnerRuntimeUiFactory.CreateRect("SupportLossConfirmation", _shell.PopupHost);
            OwnerRuntimeUiFactory.Stretch(root); _supportLossPopup = root.gameObject;
            root.gameObject.AddComponent<Image>().color = new Color(0,0,0,.75f);
            var panel = OwnerDugoutDetailUiFactory.CreatePanel(root, "Confirmation", .22f,.30f,.78f,.70f);
            OwnerDugoutDetailUiFactory.CreateLabel(panel,"Message",string.Join(" · ", names) + "\n\n2군으로 이동하면 남아 있는 개인 서포트가 종료됩니다.\n소비한 카드는 돌려받지 않습니다. 배치를 확정할까요?",
                .05f,.34f,.95f,.92f,22,FontStyle.Normal,TextAnchor.UpperLeft);
            var previous = EventSystem.current?.currentSelectedGameObject;
            void Close()
            {
                if (_supportLossPopup != null) Destroy(_supportLossPopup);
                _supportLossPopup = null;
                _cancelSupportLoss = null;
                if (previous != null) EventSystem.current?.SetSelectedGameObject(previous);
            }
            var cancel = OwnerDugoutDetailUiFactory.CreateButton(panel,"Cancel","돌아가기",.05f,.07f,.46f,.25f,Close);
            _cancelSupportLoss = Close;
            var confirm = OwnerDugoutDetailUiFactory.CreateButton(panel,"Confirm","효과 종료 · 배치 확정",.52f,.07f,.95f,.25f,() =>
            {
                Close(); _hasConfirmedSupportLoss = true;
                try { HandleLineupChangeConfirmed(); } finally { _hasConfirmedSupportLoss = false; }
            });
            cancel.navigation = new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=confirm,selectOnRight=confirm,selectOnUp=confirm,selectOnDown=confirm};
            confirm.navigation = new Navigation {mode=Navigation.Mode.Explicit,selectOnLeft=cancel,selectOnRight=cancel,selectOnUp=cancel,selectOnDown=cancel};
            cancel.Select(); return true;
        }
    }
}

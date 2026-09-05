using System;
using Baseball.Presentation.SharedUI;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 확장 화면이 Profile 통합 시 사용할 안정적인 Route ID다.</summary>
    public static class OwnerManagementRoutes
    {
        public const string ClubFinance = "Owner.Club.Finance";
        public const string ClubFacility = "Owner.Club.Facility";
        public const string RosterCondition = "Owner.Roster.Condition";

        public static bool IsClubOperation(string routeId)
        {
            return string.Equals(routeId, ClubFinance, StringComparison.Ordinal) ||
                   string.Equals(routeId, ClubFacility, StringComparison.Ordinal);
        }
    }

    /// <summary>SharedGameShell의 MainWorkspaceHost 안에서 구단 경영과 Condition 탭 가시성만 전환한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OwnerManagementWorkspaceView : MonoBehaviour
    {
        private SharedGameShellView _shell;
        private UI_Scene_OwnerClubOperations _clubOperations;
        private UI_Scene_OwnerConditionChemistry _conditionChemistry;
        private string _activeRouteId = string.Empty;

        public UI_Scene_OwnerClubOperations ClubOperations => _clubOperations;
        public UI_Scene_OwnerConditionChemistry ConditionChemistry => _conditionChemistry;
        public string ActiveRouteId => _activeRouteId;

        /// <summary>기존 SharedGameShell의 Workspace 슬롯에 두 runtime View를 한 번만 합성한다.</summary>
        public static OwnerManagementWorkspaceView CreateRuntime(SharedGameShellView shell)
        {
            if (shell == null) throw new ArgumentNullException(nameof(shell));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(
                "OwnerManagementWorkspace", shell.MainWorkspaceHost);
            OwnerRuntimeUiFactory.Stretch(root);
            OwnerManagementWorkspaceView workspace = root.gameObject.AddComponent<OwnerManagementWorkspaceView>();
            workspace.Initialize(shell);
            return workspace;
        }

        public void Initialize(SharedGameShellView shell)
        {
            if (_shell != null) return;
            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _clubOperations = UI_Scene_OwnerClubOperations.CreateRuntime(transform);
            _conditionChemistry = UI_Scene_OwnerConditionChemistry.CreateRuntime(transform);
            _clubOperations.gameObject.SetActive(false);
            _conditionChemistry.gameObject.SetActive(false);
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
        }

        public void BindClubOperation(OwnerClubOperationPresentationModel model)
        {
            RequireInitialized();
            _clubOperations.Bind(model);
        }

        public void BindConditionChemistry(OwnerConditionChemistryPresentationModel model)
        {
            RequireInitialized();
            _conditionChemistry.Bind(model);
        }

        /// <summary>Profile/Coordinator가 승인한 Route만 표시하고 나머지는 건드리지 않는다.</summary>
        public bool ShowRoute(string routeId)
        {
            RequireInitialized();
            bool showClub = OwnerManagementRoutes.IsClubOperation(routeId);
            bool showCondition = string.Equals(
                routeId,
                OwnerManagementRoutes.RosterCondition,
                StringComparison.Ordinal);
            if (!showClub && !showCondition)
                return false;

            _activeRouteId = routeId;
            _clubOperations.gameObject.SetActive(showClub);
            _conditionChemistry.gameObject.SetActive(showCondition);
            return true;
        }

        private void RequireInitialized()
        {
            if (_shell == null)
                throw new InvalidOperationException("OwnerManagementWorkspaceView가 초기화되지 않았습니다.");
        }
    }
}

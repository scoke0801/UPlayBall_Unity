using System;
using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner 공용 Shell의 Workspace/Inspector/Action 슬롯에 경기 준비와 Staff Office를 합성한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OwnerExpansionWorkspaceCoordinator : MonoBehaviour
    {
        public const string PregameRouteId = "Owner.Match.Pregame";
        public const string StaffOfficeRouteId = "Owner.Club.Staff";
        public const string RosterLineupRouteId = "Owner.Roster.Lineup";
        public const string CollectionRouteId = "Owner.Roster.Collection";

        private SharedUI.SharedGameShellView _shell;
        private UI_Scene_OwnerPregame _pregameView;
        private UI_Scene_OwnerStaffOffice _staffView;
        private UI_Scene_OwnerClubOperations _clubView;
        private UI_Scene_OwnerConditionChemistry _conditionView;
        private UI_Scene_OwnerRosterLineup _rosterLineupView;
        private UI_Scene_OwnerCollection _collectionView;
        private RectTransform _lockedWorkspaceRoot;
        private OwnerPregamePresentationModel _pregameModel;
        private OwnerStaffOfficePresentationModel _staffModel;
        private OwnerRosterLineupPresentationModel _rosterLineupModel;
        private Func<string, Sprite> _staffPortraitResolver;
        private bool _hasConditionModel;

        public event Action<string> PregamePresetSelected;
        public event Action MatchStartRequested;
        public event Action<string> StaffOfferSelected;
        public event Action<string> SignStaffRequested;
        public event Action<TicketPriceTier> TicketPolicyRequested;
        public event Action<FacilityType> FacilityUpgradeRequested;
        public event Action StadiumUpgradeRequested;
        public event Action WeekAdvanceRequested;
        public event Action SaveRequested;
        public event Action LoadRequested;
        public event Action<string> ConditionPlayerSelected;
        public event Action<OwnerLineupSwapGroup, int, int> LineupSwapRequested;
        public event Action<string> LineupPresetSelected;
        public event Action<int> TeamColorSlotCycleRequested;
        public event Action<int> TacticSlotCycleRequested;

        public string ActiveRouteId { get; private set; } = string.Empty;

        public void Initialize(SharedUI.SharedGameShellView shell, Func<string, Sprite> staffPortraitResolver = null)
        {
            if (_shell != null) return;
            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _staffPortraitResolver = staffPortraitResolver;
        }

        public void BindPregame(OwnerPregameSnapshot snapshot)
        {
            RequireInitialized();
            _pregameModel = OwnerPregamePresentationBuilder.Build(snapshot);
            EnsurePregameView();
            _pregameView.Bind(_pregameModel);
        }

        public void BindStaffOffice(OwnerStaffOfficeSnapshot snapshot)
        {
            RequireInitialized();
            _staffModel = OwnerStaffOfficePresentationBuilder.Build(snapshot);
            EnsureStaffView();
            _staffView.Bind(_staffModel, _staffPortraitResolver);
        }

        public void BindClubOperation(OwnerClubOperationSnapshot snapshot)
        {
            RequireInitialized();
            EnsureClubView();
            _clubView.Bind(OwnerClubOperationPresentationBuilder.Build(snapshot));
        }

        public void BindConditionChemistry(
            System.Collections.Generic.IReadOnlyList<OwnerConditionPlayerSnapshot> players,
            ConditionPresentationTable presentation)
        {
            RequireInitialized();
            EnsureConditionView();
            _conditionView.Bind(OwnerConditionChemistryPresentationBuilder.Build(players, presentation));
            _hasConditionModel = true;
        }

        public void BindRosterLineup(OwnerRosterLineupSnapshot snapshot)
        {
            RequireInitialized();
            _rosterLineupModel = OwnerRosterLineupPresentationBuilder.Build(snapshot);
            EnsureRosterLineupView();
            _rosterLineupView.Bind(_rosterLineupModel);
        }

        public void BindCollection(OwnerCollectionSnapshot snapshot)
        {
            RequireInitialized();
            EnsureCollectionView();
            _collectionView.Bind(snapshot);
        }

        /// <summary>다음 경기 Snapshot이 없을 때 이전 경기 준비 화면을 다시 열지 않도록 폐기한다.</summary>
        public void ClearMatchPreparation()
        {
            _pregameModel = null;
            _hasConditionModel = false;
            if (_pregameView != null) _pregameView.SetVisible(false);
            if (_conditionView != null) _conditionView.SetVisible(false);
            if (string.Equals(ActiveRouteId, PregameRouteId, StringComparison.Ordinal) ||
                string.Equals(ActiveRouteId, OwnerManagementRoutes.RosterCondition, StringComparison.Ordinal) ||
                ActiveRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal))
                ActiveRouteId = string.Empty;
        }

        /// <summary>Home 등 다른 Workspace를 표시하기 전에 확장 화면의 모든 슬롯을 숨긴다.</summary>
        public void HideAll()
        {
            RequireInitialized();
            SetAllViewsVisible(false);
            ActiveRouteId = string.Empty;
        }

        /// <summary>Home 외 Route의 Command 결과를 현재 화면이 소유한 feedback 영역에 표시한다.</summary>
        public bool SetFeedback(string message, bool isError)
        {
            if ((string.Equals(ActiveRouteId, PregameRouteId, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.MatchCenterAnalysis, StringComparison.Ordinal)) &&
                _pregameView != null)
            {
                _pregameView.SetFeedback(message, isError);
                return true;
            }
            if (string.Equals(ActiveRouteId, StaffOfficeRouteId, StringComparison.Ordinal) && _staffView != null)
            {
                _staffView.SetFeedback(message, isError);
                return true;
            }
            if ((string.Equals(ActiveRouteId, RosterLineupRouteId, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.DugoutLineupNotes, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.MatchCenterLineup, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal)) &&
                _rosterLineupView != null)
            {
                _rosterLineupView.SetFeedback(message, isError);
                return true;
            }
            if (OwnerManagementRoutes.IsClubOperation(ActiveRouteId) && _clubView != null)
            {
                _clubView.SetFeedback(message, isError);
                return true;
            }
            return false;
        }

        /// <summary>Owner Route Registry가 승인한 Route만 현재 Shell 슬롯에 표시한다.</summary>
        public bool TryShowRoute(string routeId)
        {
            return TryShowRoute(routeId, routeId);
        }

        /// <summary>Navigation Route는 유지하면서 기존 Production Workspace를 adapter로 표시한다.</summary>
        public bool TryShowRoute(string workspaceRouteId, string navigationRouteId)
        {
            RequireInitialized();
            if (string.Equals(workspaceRouteId, CollectionRouteId, StringComparison.Ordinal) && _collectionView != null)
            {
                SetAllViewsVisible(false);
                _collectionView.SetVisible(true);
                _shell.SetInspectorVisible(true);
                _shell.SetActionBarVisible(true);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "보유 선수",
                    "현재 보유 카드를 검색·정렬하고 소유 상태를 확인합니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if ((string.Equals(workspaceRouteId, RosterLineupRouteId, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.DugoutLineupNotes, StringComparison.Ordinal)) &&
                _rosterLineupModel != null)
            {
                string title = string.Equals(navigationRouteId, OwnerNavigationRoutes.DugoutLineupNotes, StringComparison.Ordinal)
                    ? "라인업 노트"
                    : string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal)
                        ? "전술카드"
                        : string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterLineup, StringComparison.Ordinal)
                            ? "우리 라인업"
                            : "선수단 라인업";
                return ShowRosterLineup(
                    navigationRouteId,
                    title,
                    navigationRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal));
            }
            if (string.Equals(workspaceRouteId, PregameRouteId, StringComparison.Ordinal) &&
                _pregameModel != null)
            {
                EnsurePregameView();
                SetAllViewsVisible(false);
                _pregameView.SetVisible(true);
                _shell.SetInspectorVisible(true);
                _shell.SetActionBarVisible(true);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "상대 분석",
                    "상대 분석 근거와 현재 라인업 노트의 컨디션·궁합을 확인합니다.",
                    "경기 준비",
                    canGoBack: navigationRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal),
                    backLabel: "돌아가기"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (string.Equals(workspaceRouteId, StaffOfficeRouteId, StringComparison.Ordinal) && _staffModel != null)
            {
                EnsureStaffView();
                SetAllViewsVisible(false);
                _staffView.SetVisible(true);
                _shell.SetInspectorVisible(true);
                _shell.SetActionBarVisible(true);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "코칭스태프",
                    "다섯 역할의 운영 효율과 계약 비용을 비교합니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (OwnerManagementRoutes.IsClubOperation(workspaceRouteId) && _clubView != null)
            {
                SetAllViewsVisible(false);
                _clubView.SetVisible(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    string.Equals(workspaceRouteId, OwnerManagementRoutes.ClubFinance, StringComparison.Ordinal)
                        ? "구단 재정"
                        : "구장·시설",
                    "관중과 수익을 확인하고 다음 운영 투자의 기회비용을 비교합니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerManagementRoutes.RosterCondition, StringComparison.Ordinal) &&
                _conditionView != null && _hasConditionModel)
            {
                SetAllViewsVisible(false);
                _conditionView.SetVisible(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "컨디션·궁합",
                    "기본 컨디션과 배치·타선·배터리 보정을 다음 경기 기준으로 확인합니다.",
                    navigationRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal)
                        ? "경기 준비"
                        : "구단주 모드",
                    canGoBack: navigationRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal),
                    backLabel: "돌아가기"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUp, StringComparison.Ordinal))
            {
                EnsureLockedWorkspace();
                SetAllViewsVisible(false);
                _lockedWorkspaceRoot.gameObject.SetActive(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "전력보강",
                    "스카우트·카드훈련·강화·판매 기능을 준비하고 있습니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            return false;
        }

        private bool ShowRosterLineup(string routeId, string title, bool canGoBack)
        {
            EnsureRosterLineupView();
            SetAllViewsVisible(false);
            _rosterLineupView.SetVisible(true);
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new SharedUI.ShellContextModel(
                routeId,
                title,
                    "25인 1군과 역할 배치, 라인업 노트와 전력 설정의 검증 결과를 확인합니다.",
                canGoBack ? "경기 준비" : "구단주 모드",
                canGoBack,
                "돌아가기"));
            _shell.SetContextHeaderVisible(false);
            ActiveRouteId = routeId;
            return true;
        }

        private void OnDestroy()
        {
            if (_pregameView != null)
            {
                _pregameView.PresetSelected -= HandlePresetSelected;
                _pregameView.MatchStartRequested -= HandleMatchStartRequested;
                DestroyView(_pregameView);
            }
            if (_staffView != null)
            {
                _staffView.StaffOfferSelected -= HandleStaffOfferSelected;
                _staffView.SignStaffRequested -= HandleSignStaffRequested;
                DestroyView(_staffView);
            }
            if (_clubView != null)
            {
                _clubView.TicketPolicyRequested -= HandleTicketPolicyRequested;
                _clubView.FacilityUpgradeRequested -= HandleFacilityUpgradeRequested;
                _clubView.StadiumUpgradeRequested -= HandleStadiumUpgradeRequested;
                _clubView.WeekAdvanceRequested -= HandleWeekAdvanceRequested;
                _clubView.SaveRequested -= HandleSaveRequested;
                _clubView.LoadRequested -= HandleLoadRequested;
                DestroyView(_clubView);
            }
            if (_conditionView != null)
            {
                _conditionView.PlayerSelected -= HandleConditionPlayerSelected;
                DestroyView(_conditionView);
            }
            if (_rosterLineupView != null)
            {
                _rosterLineupView.SwapRequested -= HandleLineupSwapRequested;
                _rosterLineupView.PresetSelected -= HandleLineupPresetSelected;
                _rosterLineupView.TeamColorSlotCycleRequested -= HandleTeamColorSlotCycleRequested;
                _rosterLineupView.TacticSlotCycleRequested -= HandleTacticSlotCycleRequested;
                DestroyView(_rosterLineupView);
            }
            if (_collectionView != null)
                DestroyView(_collectionView);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_lockedWorkspaceRoot);
        }

        private void EnsurePregameView()
        {
            if (_pregameView != null) return;
            _pregameView = UI_Scene_OwnerPregame.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _pregameView.PresetSelected += HandlePresetSelected;
            _pregameView.MatchStartRequested += HandleMatchStartRequested;
            _pregameView.SetVisible(false);
        }

        private void EnsureStaffView()
        {
            if (_staffView != null) return;
            _staffView = UI_Scene_OwnerStaffOffice.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _staffView.StaffOfferSelected += HandleStaffOfferSelected;
            _staffView.SignStaffRequested += HandleSignStaffRequested;
            _staffView.SetVisible(false);
        }

        private void EnsureClubView()
        {
            if (_clubView != null) return;
            _clubView = UI_Scene_OwnerClubOperations.CreateRuntime(_shell.MainWorkspaceHost);
            _clubView.TicketPolicyRequested += HandleTicketPolicyRequested;
            _clubView.FacilityUpgradeRequested += HandleFacilityUpgradeRequested;
            _clubView.StadiumUpgradeRequested += HandleStadiumUpgradeRequested;
            _clubView.WeekAdvanceRequested += HandleWeekAdvanceRequested;
            _clubView.SaveRequested += HandleSaveRequested;
            _clubView.LoadRequested += HandleLoadRequested;
            _clubView.SetVisible(false);
        }

        private void EnsureConditionView()
        {
            if (_conditionView != null) return;
            _conditionView = UI_Scene_OwnerConditionChemistry.CreateRuntime(_shell.MainWorkspaceHost);
            _conditionView.PlayerSelected += HandleConditionPlayerSelected;
            _conditionView.SetVisible(false);
        }

        private void EnsureRosterLineupView()
        {
            if (_rosterLineupView != null) return;
            _rosterLineupView = UI_Scene_OwnerRosterLineup.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _rosterLineupView.SwapRequested += HandleLineupSwapRequested;
            _rosterLineupView.PresetSelected += HandleLineupPresetSelected;
            _rosterLineupView.TeamColorSlotCycleRequested += HandleTeamColorSlotCycleRequested;
            _rosterLineupView.TacticSlotCycleRequested += HandleTacticSlotCycleRequested;
            _rosterLineupView.SetVisible(false);
        }

        private void EnsureCollectionView()
        {
            if (_collectionView != null) return;
            _collectionView = UI_Scene_OwnerCollection.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _collectionView.SetVisible(false);
        }

        private void EnsureLockedWorkspace()
        {
            if (_lockedWorkspaceRoot != null) return;
            _lockedWorkspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(
                _shell.MainWorkspaceHost, "OwnerPowerUpWorkspace", true);
            OwnerWorkspaceUiFactory.Panel panel = OwnerWorkspaceUiFactory.CreatePanel(
                _lockedWorkspaceRoot, "PreparationPanel", "전력보강 준비 중", true);
            OwnerWorkspaceUiFactory.Stretch(panel.Root);
            Text message = OwnerWorkspaceUiFactory.CreateText(
                panel.Content,
                "Message",
                "스카우트 후보·실제 확률, 카드훈련 비용·결과, 강화·판매 검증 계약이 연결되면 이곳에서 제공합니다.\n" +
                "현재 사용할 수 없는 기능은 위 세부 탭에서 잠김 사유를 확인할 수 있습니다.",
                18,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            OwnerWorkspaceUiFactory.Stretch(message.rectTransform);
            _lockedWorkspaceRoot.gameObject.SetActive(false);
        }

        private void SetAllViewsVisible(bool visible)
        {
            if (_pregameView != null) _pregameView.SetVisible(visible);
            if (_staffView != null) _staffView.SetVisible(visible);
            if (_clubView != null) _clubView.SetVisible(visible);
            if (_conditionView != null) _conditionView.SetVisible(visible);
            if (_rosterLineupView != null) _rosterLineupView.SetVisible(visible);
            if (_collectionView != null) _collectionView.SetVisible(visible);
            if (_lockedWorkspaceRoot != null) _lockedWorkspaceRoot.gameObject.SetActive(visible);
        }

        private void HandlePresetSelected(string presetId) => PregamePresetSelected?.Invoke(presetId);
        private void HandleMatchStartRequested() => MatchStartRequested?.Invoke();
        private void HandleStaffOfferSelected(string offerId) => StaffOfferSelected?.Invoke(offerId);
        private void HandleSignStaffRequested(string offerId) => SignStaffRequested?.Invoke(offerId);
        private void HandleTicketPolicyRequested(TicketPriceTier tier) => TicketPolicyRequested?.Invoke(tier);
        private void HandleFacilityUpgradeRequested(FacilityType type) => FacilityUpgradeRequested?.Invoke(type);
        private void HandleStadiumUpgradeRequested() => StadiumUpgradeRequested?.Invoke();
        private void HandleWeekAdvanceRequested() => WeekAdvanceRequested?.Invoke();
        private void HandleSaveRequested() => SaveRequested?.Invoke();
        private void HandleLoadRequested() => LoadRequested?.Invoke();
        private void HandleConditionPlayerSelected(string playerId) => ConditionPlayerSelected?.Invoke(playerId);
        private void HandleLineupSwapRequested(OwnerLineupSwapGroup group, int firstIndex, int secondIndex) =>
            LineupSwapRequested?.Invoke(group, firstIndex, secondIndex);
        private void HandleLineupPresetSelected(string presetId) => LineupPresetSelected?.Invoke(presetId);
        private void HandleTeamColorSlotCycleRequested(int slotIndex) =>
            TeamColorSlotCycleRequested?.Invoke(slotIndex);
        private void HandleTacticSlotCycleRequested(int slotIndex) => TacticSlotCycleRequested?.Invoke(slotIndex);

        private void RequireInitialized()
        {
            if (_shell == null) throw new InvalidOperationException("SharedGameShellView 초기화가 필요합니다.");
        }

        private static void DestroyView(MonoBehaviour view)
        {
            if (view == null) return;
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
    }
}

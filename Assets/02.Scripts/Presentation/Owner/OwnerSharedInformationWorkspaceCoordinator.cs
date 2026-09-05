using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>공용 Schedule/Records Snapshot과 Owner 전용 읽기 Action 정책을 Shell Workspace에 합성한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OwnerSharedInformationWorkspaceCoordinator : MonoBehaviour
    {
        public const string ScheduleRouteId = "Shared.League.Schedule";
        public const string RecordsRouteId = "Shared.League.Records";

        private SharedGameShellView _shell;
        private UI_Scene_OwnerSharedInformation _scheduleView;
        private UI_Scene_OwnerSharedInformation _recordsView;
        private SharedScreenPresentationModel<ScheduleScreenSnapshot> _scheduleModel;
        private SharedScreenPresentationModel<RecordsScreenSnapshot> _recordsModel;

        public string ActiveRouteId { get; private set; } = string.Empty;

        /// <summary>Owner 공용 정보 화면이 사용할 Shell 슬롯을 한 번만 연결한다.</summary>
        public void Initialize(SharedGameShellView shell)
        {
            if (_shell != null)
                return;
            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
        }

        /// <summary>현재 Owner 일정 Snapshot을 읽기 전용 Action Provider와 합성한다.</summary>
        public void BindSchedule(ScheduleScreenSnapshot snapshot, UiCapabilitySet capabilities)
        {
            RequireInitialized();
            UiContentStateModel state = snapshot == null || snapshot.Games.Count == 0
                ? UiContentStateModel.CreateEmpty("일정 없음", "현재 저장 데이터에 표시할 시즌 일정이 없습니다.")
                : UiContentStateModel.Ready;
            _scheduleModel = new SharedScreenPresentationModel<ScheduleScreenSnapshot>(
                new SharedScreenProfile(
                    ScheduleRouteId,
                    "일정",
                    SharedScreenKind.Schedule,
                    UiCapability.CanViewLeagueInformation,
                    usesActionBar: false),
                new SharedScreenContext(ScheduleRouteId, snapshot?.FocusTeamId),
                snapshot,
                state,
                capabilities,
                OwnerReadOnlySharedScreenActionProvider.Instance);
            EnsureScheduleView();
            _scheduleView.BindSchedule(_scheduleModel);
        }

        /// <summary>확정 WorldHistory 기록 Snapshot을 읽기 전용 Action Provider와 합성한다.</summary>
        public void BindHistoricalRecords(RecordsScreenSnapshot snapshot, UiCapabilitySet capabilities)
        {
            RequireInitialized();
            UiContentStateModel state = snapshot == null || snapshot.Table.Rows.Count == 0
                ? UiContentStateModel.CreateEmpty(
                    "역사 기록 없음",
                    "현재 월드 히스토리에 확정된 정규 시즌 타격 기록이 없습니다.")
                : UiContentStateModel.Ready;
            _recordsModel = new SharedScreenPresentationModel<RecordsScreenSnapshot>(
                new SharedScreenProfile(
                    RecordsRouteId,
                    "역사 기록",
                    SharedScreenKind.SeasonRecords,
                    UiCapability.CanViewSeasonRecords,
                    usesActionBar: false),
                new SharedScreenContext(RecordsRouteId),
                snapshot,
                state,
                capabilities,
                OwnerReadOnlySharedScreenActionProvider.Instance);
            EnsureRecordsView();
            _recordsView.BindRecords(_recordsModel);
        }

        /// <summary>실제 Snapshot이 연결된 Owner 공용 정보 Route만 표시한다.</summary>
        public bool TryShowRoute(string routeId)
        {
            RequireInitialized();
            if (string.Equals(routeId, ScheduleRouteId, StringComparison.Ordinal) && _scheduleModel != null)
            {
                HideAll();
                _scheduleView.SetVisible(true);
                ShowContext(ScheduleRouteId, "구단 일정",
                    "현재 저장 데이터의 대진 라운드와 완료 점수를 읽기 전용으로 확인합니다.");
                ActiveRouteId = ScheduleRouteId;
                return true;
            }
            if (string.Equals(routeId, RecordsRouteId, StringComparison.Ordinal) && _recordsModel != null)
            {
                HideAll();
                _recordsView.SetVisible(true);
                ShowContext(RecordsRouteId, "역사 기록",
                    "현재 시즌 누적과 분리된 월드 히스토리 확정 기록을 확인합니다.");
                ActiveRouteId = RecordsRouteId;
                return true;
            }
            return false;
        }

        /// <summary>Home이나 Owner 전용 Workspace를 열기 전에 공용 정보 화면을 숨긴다.</summary>
        public void HideAll()
        {
            if (_shell == null)
                return;
            _scheduleView?.SetVisible(false);
            _recordsView?.SetVisible(false);
            ActiveRouteId = string.Empty;
        }

        private void EnsureScheduleView()
        {
            if (_scheduleView != null)
                return;
            _scheduleView = UI_Scene_OwnerSharedInformation.CreateRuntime(_shell.MainWorkspaceHost);
            _scheduleView.gameObject.name = "UI_Scene_OwnerSchedule";
            _scheduleView.SetVisible(false);
        }

        private void EnsureRecordsView()
        {
            if (_recordsView != null)
                return;
            _recordsView = UI_Scene_OwnerSharedInformation.CreateRuntime(_shell.MainWorkspaceHost);
            _recordsView.gameObject.name = "UI_Scene_OwnerHistoricalRecords";
            _recordsView.SetVisible(false);
        }

        private void ShowContext(string routeId, string title, string description)
        {
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new ShellContextModel(
                routeId,
                title,
                description,
                "구단주 모드"));
        }

        private void RequireInitialized()
        {
            if (_shell == null)
                throw new InvalidOperationException("SharedGameShellView 초기화가 필요합니다.");
        }
    }
}

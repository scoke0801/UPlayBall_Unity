using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner 리그·구단 정보 Snapshot과 읽기 전용 Action 정책을 Shell Workspace에 합성한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OwnerSharedInformationWorkspaceCoordinator : MonoBehaviour
    {
        private static readonly string[] LeagueRouteIds =
        {
            OwnerNavigationRoutes.LeagueStandings,
            OwnerNavigationRoutes.LeagueTeamResults,
            OwnerNavigationRoutes.LeagueMatchups,
            OwnerNavigationRoutes.LeagueRankHistory
        };
        private static readonly string[] LeagueTabLabels = { "순위표", "구단 성적", "대전 결과", "순위 변화" };
        public const string ScheduleRouteId = "Shared.League.Schedule";
        public const string RecordsRouteId = "Shared.League.Records";
        public const string SeasonRecordsRouteId = "Shared.League.SeasonRecords";

        private SharedGameShellView _shell;
        private UI_Scene_OwnerSharedInformation _scheduleView;
        private UI_Scene_OwnerSharedInformation _recordsView;
        private UI_Scene_OwnerSeasonRecords _seasonRecordsView;
        private UI_Scene_OwnerLeague _leagueView;
        private UI_Scene_OwnerClubInformation _clubInformationView;
        private SharedScreenPresentationModel<ScheduleScreenSnapshot> _scheduleModel;
        private SharedScreenPresentationModel<RecordsScreenSnapshot> _recordsModel;
        private OwnerClubInformationPresentationModel _clubInformationModel;

        public event Action NextMatchAnalysisRequested;

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
            if (_leagueView == null)
            {
                _leagueView = UI_Scene_OwnerLeague.CreateRuntime(_shell.MainWorkspaceHost);
                _leagueView.gameObject.SetActive(false);
            }
            if (snapshot != null)
                _leagueView.Bind(new OwnerLeaguePresentationModel(snapshot));
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

        /// <summary>현재 시즌 누적 개인 기록을 네 부문 탭이 있는 기록 화면에 연결한다.</summary>
        public void BindSeasonRecords(OwnerSeasonRecordsPresentationModel model, Action<int> selectSeason = null)
        {
            RequireInitialized();
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (_seasonRecordsView == null)
            {
                _seasonRecordsView = UI_Scene_OwnerSeasonRecords.CreateRuntime(_shell.MainWorkspaceHost);
                _seasonRecordsView.SetVisible(false);
            }
            _seasonRecordsView.Bind(model, selectSeason);
        }

        /// <summary>현재 구단의 구단주·구단 정보 화면을 실제 진행 Snapshot으로 갱신한다.</summary>
        public void BindClubInformation(OwnerClubInformationPresentationModel model)
        {
            RequireInitialized();
            _clubInformationModel = model ?? throw new ArgumentNullException(nameof(model));
            if (_clubInformationView == null)
            {
                _clubInformationView = UI_Scene_OwnerClubInformation.CreateRuntime(_shell.MainWorkspaceHost);
                _clubInformationView.gameObject.SetActive(false);
            }
            _clubInformationView.Bind(model);
        }

        /// <summary>실제 Snapshot이 연결된 Owner 공용 정보 Route만 표시한다.</summary>
        public bool TryShowRoute(string routeId)
        {
            RequireInitialized();
            if ((string.Equals(routeId, OwnerNavigationRoutes.ClubOwner, StringComparison.Ordinal) ||
                 string.Equals(routeId, OwnerNavigationRoutes.ClubInformation, StringComparison.Ordinal)) &&
                _clubInformationView != null && _clubInformationModel != null)
            {
                HideAll();
                bool showOwner = string.Equals(routeId, OwnerNavigationRoutes.ClubOwner, StringComparison.Ordinal);
                _clubInformationView.ShowTab(showOwner);
                ShowContext(routeId, showOwner ? "구단주 정보" : "구단 정보",
                    "현재 시즌 구단 구성과 성적을 확인합니다.");
                ActiveRouteId = routeId;
                return true;
            }
            int tab = Array.IndexOf(LeagueRouteIds, routeId);
            if (tab >= 0 && _leagueView != null && _scheduleModel?.Snapshot != null)
            {
                HideAll();
                _leagueView.ShowTab(tab);
                ShowContext(routeId, LeagueTabLabels[tab], string.Empty);
                ActiveRouteId = routeId;
                return true;
            }
            if (string.Equals(routeId, ScheduleRouteId, StringComparison.Ordinal) && _scheduleModel != null)
            {
                HideAll();
                _scheduleView.SetVisible(true);
                ShowContext(ScheduleRouteId, "구단 일정",
                    "현재 저장 데이터의 대진 라운드와 완료 점수를 읽기 전용으로 확인합니다.");
                ActiveRouteId = ScheduleRouteId;
                return true;
            }
            if (string.Equals(routeId, SeasonRecordsRouteId, StringComparison.Ordinal) &&
                _seasonRecordsView != null)
            {
                HideAll();
                _seasonRecordsView.SetVisible(true);
                ShowContext(SeasonRecordsRouteId, "선수 기록",
                    "이번 시즌 리그 전체 선수의 누적 기록을 부문별로 확인합니다.");
                ActiveRouteId = SeasonRecordsRouteId;
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
            _seasonRecordsView?.SetVisible(false);
            if (_leagueView != null) _leagueView.gameObject.SetActive(false);
            if (_clubInformationView != null) _clubInformationView.gameObject.SetActive(false);
            ActiveRouteId = string.Empty;
        }

        private void EnsureScheduleView()
        {
            if (_scheduleView != null)
                return;
            _scheduleView = UI_Scene_OwnerSharedInformation.CreateRuntime(_shell.MainWorkspaceHost);
            _scheduleView.gameObject.name = "UI_Scene_OwnerSchedule";
            _scheduleView.NextMatchAnalysisRequested += HandleNextMatchAnalysisRequested;
            _scheduleView.SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_scheduleView != null)
                _scheduleView.NextMatchAnalysisRequested -= HandleNextMatchAnalysisRequested;
        }

        private void HandleNextMatchAnalysisRequested()
        {
            NextMatchAnalysisRequested?.Invoke();
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

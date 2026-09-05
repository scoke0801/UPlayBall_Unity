using System;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Career
{
    /// <summary>기존 선수 Workspace 위에 하나의 SharedGameShell Chrome과 Route 연결을 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCareerShellCoordinator : MonoBehaviour
    {
        private SharedGameShellView _shell;
        private SharedGameShellPresenter _presenter;
        private PlayerShellStatusProvider _statusProvider;
        private CareerManager _manager;
        private readonly PlayerHomePresentationModelBuilder _homeBuilder = new PlayerHomePresentationModelBuilder();

        /// <summary>공용 셸과 실제 CareerManager를 연결하고 현재 Career 상태를 표시한다.</summary>
        public void Initialize(SharedGameShellView shell, CareerManager manager)
        {
            if (_presenter != null)
                return;

            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _shell.SetChromeOverlayMode(true);
            _shell.SettingsRequested += HandleSettingsRequested;

            _manager.CareerChanged += HandleCareerChanged;
            CareerTabNavigation.TabChanged += HandleTabChanged;
            UiGameModeSession.ModeChanged += HandleModeChanged;
            RefreshCareerState();
        }

        /// <summary>Management Scene 재진입 시 현재 Career 가시성과 Header 상태를 다시 동기화한다.</summary>
        public void Refresh()
        {
            RefreshCareerState();
        }

        private void LateUpdate()
        {
            if (_shell != null &&
                _shell.gameObject.activeSelf &&
                _shell.transform.GetSiblingIndex() != _shell.transform.parent.childCount - 1)
                _shell.transform.SetAsLastSibling();
        }

        private void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            CareerTabNavigation.TabChanged -= HandleTabChanged;
            UiGameModeSession.ModeChanged -= HandleModeChanged;
            if (_shell != null)
                _shell.SettingsRequested -= HandleSettingsRequested;
            if (_presenter != null)
            {
                _presenter.NavigationRequested -= HandleNavigationRequested;
                _presenter.Dispose();
                _presenter = null;
            }
        }

        private void HandleCareerChanged()
        {
            RefreshCareerState();
        }

        private void HandleModeChanged(UiGameMode? mode)
        {
            RefreshCareerState();
        }

        private void RefreshCareerState()
        {
            bool isVisible = _manager != null &&
                _manager.HasActiveCareer &&
                !_manager.HasActiveMatch &&
                UiGameModeSession.IsSelected(UiGameMode.PlayerCareer);
            if (_shell != null)
                _shell.gameObject.SetActive(isVisible);
            if (!isVisible)
                return;

            PlayerHomePresentationModel home = _homeBuilder.Build(_manager.Dashboard);
            if (_statusProvider == null)
            {
                _statusProvider = new PlayerShellStatusProvider(home);
                _presenter = new SharedGameShellPresenter(
                    _shell,
                    PlayerCareerUiProfileFactory.Create(),
                    _statusProvider);
                _presenter.NavigationRequested += HandleNavigationRequested;
            }
            else
            {
                _statusProvider.Update(home);
            }

            ShowContext(GetDefaultRoute(CareerTabNavigation.CurrentTab));
        }

        private void HandleTabChanged(CareerMainTab tab)
        {
            if (_presenter != null)
                ShowContext(GetDefaultRoute(tab));
        }

        private void HandleNavigationRequested(string routeId)
        {
            if (!TryGetCareerTab(routeId, out CareerMainTab tab))
                return;
            if (CareerTabNavigation.Show(tab))
                ShowContext(routeId);
        }

        private static void HandleSettingsRequested()
        {
            UI_Popup_CareerSettings.ShowRuntime();
        }

        private void ShowContext(string routeId)
        {
            if (_presenter == null)
                return;

            GetContextText(routeId, out string title, out string summary, out string eyebrow);
            _presenter.ShowContext(new ShellContextModel(routeId, title, summary, eyebrow));
        }

        private static bool TryGetCareerTab(string routeId, out CareerMainTab tab)
        {
            switch (routeId)
            {
                case PlayerCareerRoutes.Home:
                    tab = CareerMainTab.Home;
                    return true;
                case PlayerCareerRoutes.Match:
                case PlayerCareerRoutes.NextMatch:
                case PlayerCareerRoutes.MatchRole:
                    tab = CareerMainTab.Schedule;
                    return true;
                case PlayerCareerRoutes.Profile:
                case PlayerCareerRoutes.Abilities:
                case PlayerCareerRoutes.SeasonStatistics:
                    tab = CareerMainTab.Player;
                    return true;
                case PlayerCareerRoutes.Growth:
                    tab = CareerMainTab.Growth;
                    return true;
                case PlayerCareerRoutes.Schedule:
                    tab = CareerMainTab.Schedule;
                    return true;
                case PlayerCareerRoutes.League:
                    tab = CareerMainTab.League;
                    return true;
                case PlayerCareerRoutes.Team:
                case PlayerCareerRoutes.TeamRoster:
                case PlayerCareerRoutes.ManagerDecision:
                    tab = CareerMainTab.Team;
                    return true;
                case PlayerCareerRoutes.Records:
                    tab = CareerMainTab.Records;
                    return true;
                case PlayerCareerRoutes.Career:
                case PlayerCareerRoutes.Contract:
                    tab = CareerMainTab.Contract;
                    return true;
                default:
                    tab = CareerMainTab.Home;
                    return false;
            }
        }

        private static string GetDefaultRoute(CareerMainTab tab)
        {
            switch (tab)
            {
                case CareerMainTab.Player: return PlayerCareerRoutes.Profile;
                case CareerMainTab.Growth: return PlayerCareerRoutes.Growth;
                case CareerMainTab.Schedule: return PlayerCareerRoutes.Match;
                case CareerMainTab.League: return PlayerCareerRoutes.League;
                case CareerMainTab.Team: return PlayerCareerRoutes.Team;
                case CareerMainTab.Records: return PlayerCareerRoutes.Records;
                case CareerMainTab.Contract: return PlayerCareerRoutes.Career;
                default: return PlayerCareerRoutes.Home;
            }
        }

        private static void GetContextText(
            string routeId,
            out string title,
            out string summary,
            out string eyebrow)
        {
            eyebrow = "선수 커리어";
            switch (routeId)
            {
                case PlayerCareerRoutes.Match:
                case PlayerCareerRoutes.NextMatch:
                    title = "다음 경기";
                    summary = "출전 예상과 상대 정보를 확인합니다.";
                    return;
                case PlayerCareerRoutes.MatchRole:
                case PlayerCareerRoutes.ManagerDecision:
                    title = "감독 판단";
                    summary = "현재 역할과 기용 결정을 읽기 전용으로 확인합니다.";
                    return;
                case PlayerCareerRoutes.Profile:
                case PlayerCareerRoutes.Abilities:
                case PlayerCareerRoutes.SeasonStatistics:
                    title = "내 선수";
                    summary = "능력치와 시즌 기록을 비교합니다.";
                    return;
                case PlayerCareerRoutes.Growth:
                    title = "성장";
                    summary = "Career 성장과 훈련 상태를 관리합니다.";
                    return;
                case PlayerCareerRoutes.Schedule:
                    title = "일정";
                    summary = "구단 일정과 내 출전 결과를 확인합니다.";
                    return;
                case PlayerCareerRoutes.League:
                    title = "리그";
                    summary = "순위와 리그 경쟁 구도를 확인합니다.";
                    return;
                case PlayerCareerRoutes.Team:
                case PlayerCareerRoutes.TeamRoster:
                    title = "구단";
                    summary = "내 선수를 강조한 읽기 전용 선수단입니다.";
                    return;
                case PlayerCareerRoutes.Records:
                    title = "기록";
                    summary = "개인·리그 기록과 수상 이력을 확인합니다.";
                    return;
                case PlayerCareerRoutes.Career:
                case PlayerCareerRoutes.Contract:
                    title = "커리어";
                    summary = "계약과 커리어 진행 상태를 확인합니다.";
                    return;
                default:
                    title = "홈";
                    summary = "다음 경기, 현재 역할, 성장과 계약 상태를 한눈에 확인합니다.";
                    return;
            }
        }
    }

    /// <summary>Management Scene에 선수 커리어 SharedGameShell을 한 번만 설치한다.</summary>
    public static class PlayerCareerShellBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayerCareerShellCoordinator coordinator = UnityEngine.Object.FindFirstObjectByType<PlayerCareerShellCoordinator>(
                FindObjectsInactive.Include);
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                if (coordinator != null)
                    coordinator.gameObject.SetActive(false);
                return;
            }

            GameManager gameManager = GameManager.EnsureExists();
            CareerManager manager = gameManager.EnsureManager<CareerManager>("CareerManager");
            OwnerModeManager ownerManager = gameManager.EnsureManager<OwnerModeManager>("OwnerModeManager");
            UiGameModeSession.ResolveInitialMode(
                manager.HasActiveCareer,
                ownerManager.HasActiveRuntime);

            if (coordinator != null)
            {
                coordinator.Refresh();
                return;
            }

            UIManager uiManager = gameManager.EnsureManager<UIManager>("UIManager");
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(
                uiManager.Root.GetLayerRoot(UILayer.Scene),
                "PlayerCareerSharedShell");
            coordinator = shell.gameObject.AddComponent<PlayerCareerShellCoordinator>();
            coordinator.Initialize(shell, manager);
        }

        /// <summary>명시적 선택이 없고 Player Career만 활성화된 복원 경로인지 판정한다.</summary>
        public static bool ShouldSelectPlayerCareer(
            UiGameMode? currentMode,
            bool hasActiveCareer,
            bool hasActiveOwnerRuntime)
        {
            return UiGameModeSession.InferInitialMode(
                currentMode,
                hasActiveCareer,
                hasActiveOwnerRuntime) == UiGameMode.PlayerCareer &&
                !currentMode.HasValue;
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.Player;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>기존 선수 Workspace 위에 하나의 SharedGameShell Chrome과 Route 연결을 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCareerShellCoordinator : MonoBehaviour
    {
        private SharedGameShellView _shell;
        private SharedGameShellPresenter _presenter;
        private PlayerShellStatusProvider _statusProvider;
        private GameModeUiProfile _profile;
        private GameModeNavigationState _navigationState;
        private CareerManager _manager;
        private PlayerCareerWorkspaceAdapter _workspaceAdapter;
        private bool _isRoutingNavigation;
        private readonly PlayerHomePresentationModelBuilder _homeBuilder = new PlayerHomePresentationModelBuilder();

        /// <summary>공용 셸과 실제 CareerManager를 연결하고 현재 Career 상태를 표시한다.</summary>
        public void Initialize(SharedGameShellView shell, CareerManager manager)
        {
            if (_presenter != null)
                return;

            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _shell.SetChromeOverlayMode(false);
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _workspaceAdapter = new PlayerCareerWorkspaceAdapter(_shell.MainWorkspaceHost);
            _workspaceAdapter.Synchronize();
            _shell.SettingsRequested += HandleSettingsRequested;

            _manager.CareerChanged += HandleCareerChanged;
            CareerTabNavigation.TabChanged += HandleTabChanged;
            UiGameModeSession.ModeChanged += HandleModeChanged;
            RefreshCareerState();
        }

        private void Start()
        {
            // 모든 sceneLoaded bootstrap이 화면 생성을 마친 뒤 한 번 더 수집한다.
            _workspaceAdapter?.Synchronize();
        }

        /// <summary>Management Scene 재진입 시 현재 Career 가시성과 Header 상태를 다시 동기화한다.</summary>
        public void Refresh()
        {
            RefreshCareerState();
        }

        private void LateUpdate()
        {
            if (_workspaceAdapter != null && _workspaceAdapter.HasPendingLayout)
                _workspaceAdapter.Synchronize();

            if (_shell != null &&
                _shell.gameObject.activeSelf &&
                _shell.transform.GetSiblingIndex() != _shell.transform.parent.childCount - 1)
                _shell.transform.SetAsLastSibling();
        }

        private void OnRectTransformDimensionsChange()
        {
            _workspaceAdapter?.Synchronize();
        }

        private void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            CareerTabNavigation.TabChanged -= HandleTabChanged;
            UiGameModeSession.ModeChanged -= HandleModeChanged;
            if (_shell != null)
                _shell.SettingsRequested -= HandleSettingsRequested;
            _workspaceAdapter?.RestoreAll();
            _workspaceAdapter = null;
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

            _workspaceAdapter?.Synchronize();

            PlayerHomePresentationModel home = _homeBuilder.Build(_manager.Dashboard);
            if (_statusProvider == null)
            {
                _statusProvider = new PlayerShellStatusProvider(home);
                _profile = PlayerCareerUiProfileFactory.Create();
                _navigationState = new GameModeNavigationState(
                    _profile,
                    GetDefaultRoute(CareerTabNavigation.CurrentTab));
                _presenter = new SharedGameShellPresenter(
                    _shell,
                    _profile,
                    _statusProvider);
                _presenter.NavigationRequested += HandleNavigationRequested;
            }
            else
            {
                _statusProvider.Update(home);
            }

            ShowContext(_navigationState.ActiveRouteId);
        }

        private void HandleTabChanged(CareerMainTab tab)
        {
            if (_presenter == null || _navigationState == null || _isRoutingNavigation)
                return;

            string routeId = _navigationState.Navigate(GetDefaultRoute(tab));
            ApplyInternalScreenRoute(routeId);
            _workspaceAdapter?.Synchronize();
            ShowContext(routeId);
        }

        private void HandleNavigationRequested(string routeId)
        {
            if (_navigationState == null)
                return;

            string destination = _navigationState.Navigate(routeId);
            if (!TryGetCareerTab(destination, out CareerMainTab tab))
                return;

            _isRoutingNavigation = true;
            bool wasShown = CareerTabNavigation.Show(tab);
            _isRoutingNavigation = false;
            if (!wasShown)
                return;

            ApplyInternalScreenRoute(destination);
            _workspaceAdapter?.Synchronize();
            ShowContext(destination);
        }

        private static void ApplyInternalScreenRoute(string routeId)
        {
            if (routeId == PlayerCareerRoutes.Profile || routeId == PlayerCareerRoutes.Abilities ||
                routeId == PlayerCareerRoutes.Skills)
            {
                UI_Scene_Player player = UnityEngine.Object.FindFirstObjectByType<UI_Scene_Player>(
                    FindObjectsInactive.Include);
                if (player == null)
                    return;

                UI_Scene_Player.PlayerDetailTab tab = routeId switch
                {
                    PlayerCareerRoutes.Abilities => UI_Scene_Player.PlayerDetailTab.Attributes,
                    PlayerCareerRoutes.Skills => UI_Scene_Player.PlayerDetailTab.Skills,
                    _ => UI_Scene_Player.PlayerDetailTab.Profile
                };
                player.SelectDetailTab(tab);
                return;
            }

            UI_Scene_CareerRecords records = UnityEngine.Object.FindFirstObjectByType<UI_Scene_CareerRecords>(
                FindObjectsInactive.Include);
            if (records == null)
                return;

            switch (routeId)
            {
                case PlayerCareerRoutes.Records:
                    records.SelectNavigationPage(CareerRecordsPage.Personal);
                    break;
                case PlayerCareerRoutes.CareerRecords:
                    records.SelectNavigationPage(CareerRecordsPage.Career);
                    break;
                case PlayerCareerRoutes.AwardsHighlights:
                    records.SelectNavigationPage(CareerRecordsPage.Awards);
                    break;
            }
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
                case PlayerCareerRoutes.NextMatch:
                case PlayerCareerRoutes.Schedule:
                case PlayerCareerRoutes.GameResults:
                    tab = CareerMainTab.Schedule;
                    return true;
                case PlayerCareerRoutes.Profile:
                case PlayerCareerRoutes.Abilities:
                case PlayerCareerRoutes.Skills:
                    tab = CareerMainTab.Player;
                    return true;
                case PlayerCareerRoutes.Growth:
                    tab = CareerMainTab.Growth;
                    return true;
                case PlayerCareerRoutes.League:
                case PlayerCareerRoutes.LeagueBatting:
                case PlayerCareerRoutes.LeaguePitching:
                case PlayerCareerRoutes.LeagueRecords:
                    tab = CareerMainTab.League;
                    return true;
                case PlayerCareerRoutes.Team:
                case PlayerCareerRoutes.TeamRoster:
                case PlayerCareerRoutes.TeamLineup:
                case PlayerCareerRoutes.TeamPitching:
                    tab = CareerMainTab.Team;
                    return true;
                case PlayerCareerRoutes.Records:
                case PlayerCareerRoutes.CareerRecords:
                case PlayerCareerRoutes.AwardsHighlights:
                    tab = CareerMainTab.Records;
                    return true;
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
                case CareerMainTab.Schedule: return PlayerCareerRoutes.NextMatch;
                case CareerMainTab.League: return PlayerCareerRoutes.League;
                case CareerMainTab.Team: return PlayerCareerRoutes.TeamRoster;
                case CareerMainTab.Records: return PlayerCareerRoutes.Records;
                case CareerMainTab.Contract: return PlayerCareerRoutes.Contract;
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
                case PlayerCareerRoutes.NextMatch:
                    eyebrow = "경기";
                    title = "다음 경기";
                    summary = "출전 예상과 상대 정보를 확인합니다.";
                    return;
                case PlayerCareerRoutes.GameResults:
                    eyebrow = "경기";
                    title = "경기 결과";
                    summary = "완료된 경기와 내 출전 결과를 확인합니다.";
                    return;
                case PlayerCareerRoutes.Profile:
                    eyebrow = "선수";
                    title = "선수 정보";
                    summary = "현재 상태와 역할, 최근 흐름을 확인합니다.";
                    return;
                case PlayerCareerRoutes.Abilities:
                    eyebrow = "선수";
                    title = "능력치";
                    summary = "현재 능력과 성장 근거를 비교합니다.";
                    return;
                case PlayerCareerRoutes.Growth:
                    eyebrow = "선수";
                    title = "성장";
                    summary = "선수 성장과 훈련 상태를 관리합니다.";
                    return;
                case PlayerCareerRoutes.Skills:
                    eyebrow = "선수";
                    title = "스킬";
                    summary = "보유 스킬 블록과 적용 효과를 확인합니다.";
                    return;
                case PlayerCareerRoutes.Schedule:
                    eyebrow = "경기";
                    title = "일정";
                    summary = "구단 일정과 내 출전 결과를 확인합니다.";
                    return;
                case PlayerCareerRoutes.League:
                    eyebrow = "리그";
                    title = "순위";
                    summary = "현재 순위와 승강 경쟁 구도를 확인합니다.";
                    return;
                case PlayerCareerRoutes.LeagueBatting:
                    eyebrow = "리그";
                    title = "타자 순위";
                    summary = "리그 타격 부문 경쟁을 확인합니다.";
                    return;
                case PlayerCareerRoutes.LeaguePitching:
                    eyebrow = "리그";
                    title = "투수 순위";
                    summary = "리그 투구 부문 경쟁을 확인합니다.";
                    return;
                case PlayerCareerRoutes.LeagueRecords:
                    eyebrow = "리그";
                    title = "리그 기록";
                    summary = "리그 주요 지표와 시즌 흐름을 확인합니다.";
                    return;
                case PlayerCareerRoutes.Team:
                    eyebrow = "팀";
                    title = "팀 정보";
                    summary = "구단 전력과 운영 방침을 읽기 전용으로 확인합니다.";
                    return;
                case PlayerCareerRoutes.TeamRoster:
                    eyebrow = "팀";
                    title = "선수단";
                    summary = "내 선수를 강조한 읽기 전용 선수단을 확인합니다.";
                    return;
                case PlayerCareerRoutes.TeamLineup:
                    eyebrow = "팀";
                    title = "선발 라인업";
                    summary = "감독이 정한 타순과 내 예상 역할을 확인합니다.";
                    return;
                case PlayerCareerRoutes.TeamPitching:
                    eyebrow = "팀";
                    title = "투수진";
                    summary = "감독이 정한 선발 로테이션과 불펜을 확인합니다.";
                    return;
                case PlayerCareerRoutes.Records:
                    eyebrow = "커리어";
                    title = "시즌 기록";
                    summary = "현재 시즌 기록을 종목·경기 범위·표시 정보 필터로 확인합니다.";
                    return;
                case PlayerCareerRoutes.CareerRecords:
                    eyebrow = "커리어";
                    title = "통산 기록";
                    summary = "시즌별 기록과 커리어 누적을 확인합니다.";
                    return;
                case PlayerCareerRoutes.AwardsHighlights:
                    eyebrow = "커리어";
                    title = "수상·하이라이트";
                    summary = "수상 이력과 기억할 경기를 확인합니다.";
                    return;
                case PlayerCareerRoutes.Contract:
                    eyebrow = "커리어";
                    title = "계약";
                    summary = "현재 계약과 상여, 다음 협상 상태를 확인합니다.";
                    return;
                default:
                    title = "홈";
                    summary = "다음 경기, 현재 역할, 성장과 계약 상태를 한눈에 확인합니다.";
                    return;
            }
        }
    }

    /// <summary>기존 Player Career 화면을 Shared Shell Workspace에 안전하게 합성하고 원래 배치를 복원한다.</summary>
    public sealed class PlayerCareerWorkspaceAdapter
    {
        private readonly RectTransform _workspaceHost;
        private readonly List<EmbeddedScreenState> _embeddedScreens = new List<EmbeddedScreenState>();

        /// <summary>Workspace 크기 확정 뒤 한 번 더 배율 계산이 필요한지 나타낸다.</summary>
        public bool HasPendingLayout { get; private set; }

        public PlayerCareerWorkspaceAdapter(RectTransform workspaceHost)
        {
            _workspaceHost = workspaceHost != null
                ? workspaceHost
                : throw new ArgumentNullException(nameof(workspaceHost));
        }

        /// <summary>현재 로드된 Player Career 화면을 찾아 Workspace 아래로 옮긴다.</summary>
        public void Synchronize()
        {
            HasPendingLayout = false;
            UIBase[] screens = UnityEngine.Object.FindObjectsByType<UIBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] is ICareerTabScreen)
                    Embed(screens[index]);
            }
        }

        /// <summary>단일 Career 화면을 Shell Workspace에 맞게 배치한다.</summary>
        public void Embed(UIBase screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (screen is not ICareerTabScreen)
                throw new ArgumentException("Player Career 화면만 Workspace에 합성할 수 있습니다.", nameof(screen));

            EmbeddedScreenState state = FindState(screen);
            if (state == null)
            {
                state = new EmbeddedScreenState(screen);
                _embeddedScreens.Add(state);
            }

            RectTransform screenRect = state.ScreenRect;
            if (screenRect.parent != _workspaceHost)
                screenRect.SetParent(_workspaceHost, false);
            Stretch(screenRect);
            screenRect.localScale = Vector3.one;
            screenRect.localRotation = Quaternion.identity;

            if (state.Mask == null)
            {
                state.Mask = screen.GetComponent<RectMask2D>();
                if (state.Mask == null)
                {
                    state.Mask = screen.gameObject.AddComponent<RectMask2D>();
                    state.OwnsMask = true;
                }
            }

            RectTransform content = screenRect.Find("Content") as RectTransform;
            if (content == null)
                return;

            state.CaptureContent(content);
            content.anchoredPosition = Vector2.zero;
            if (!state.FitContentWithin(_workspaceHost.rect.size))
                HasPendingLayout = true;
        }

        /// <summary>Coordinator가 제거될 때 화면 부모와 RectTransform 상태를 복원한다.</summary>
        public void RestoreAll()
        {
            for (int index = _embeddedScreens.Count - 1; index >= 0; index--)
                _embeddedScreens[index].Restore();
            _embeddedScreens.Clear();
        }

        private EmbeddedScreenState FindState(UIBase screen)
        {
            for (int index = 0; index < _embeddedScreens.Count; index++)
            {
                if (ReferenceEquals(_embeddedScreens[index].Screen, screen))
                    return _embeddedScreens[index];
            }
            return null;
        }

        private sealed class EmbeddedScreenState
        {
            private readonly Transform _originalParent;
            private readonly int _originalSiblingIndex;
            private readonly RectTransformState _screenLayout;
            private RectTransform _content;
            private Vector2 _contentAnchoredPosition;
            private Vector3 _contentLocalScale;
            private bool _hasContentLayout;

            public EmbeddedScreenState(UIBase screen)
            {
                Screen = screen;
                ScreenRect = screen.GetComponent<RectTransform>();
                _originalParent = ScreenRect.parent;
                _originalSiblingIndex = ScreenRect.GetSiblingIndex();
                _screenLayout = new RectTransformState(ScreenRect);
            }

            public UIBase Screen { get; }
            public RectTransform ScreenRect { get; }
            public RectMask2D Mask { get; set; }
            public bool OwnsMask { get; set; }

            public void CaptureContent(RectTransform content)
            {
                if (_hasContentLayout)
                    return;

                _content = content;
                _contentAnchoredPosition = content.anchoredPosition;
                _contentLocalScale = content.localScale;
                _hasContentLayout = true;
            }

            public bool FitContentWithin(Vector2 availableSize)
            {
                if (!_hasContentLayout || _content == null)
                    return true;

                Vector2 contentSize = _content.rect.size;
                if (availableSize.x <= 0f || availableSize.y <= 0f ||
                    contentSize.x <= 0f || contentSize.y <= 0f)
                    return false;

                float width = contentSize.x * Mathf.Abs(_contentLocalScale.x);
                float height = contentSize.y * Mathf.Abs(_contentLocalScale.y);
                if (width <= 0f || height <= 0f)
                    return false;

                float scale = Mathf.Min(availableSize.x / width, availableSize.y / height);
                scale = Mathf.Min(scale, 1f);
                _content.localScale = new Vector3(
                    _contentLocalScale.x * scale,
                    _contentLocalScale.y * scale,
                    _contentLocalScale.z);
                return true;
            }

            public void Restore()
            {
                if (ScreenRect == null)
                    return;

                ScreenRect.SetParent(_originalParent, false);
                _screenLayout.Apply(ScreenRect);
                if (_originalParent != null)
                    ScreenRect.SetSiblingIndex(Mathf.Min(_originalSiblingIndex, _originalParent.childCount - 1));

                if (_hasContentLayout && _content != null)
                {
                    _content.anchoredPosition = _contentAnchoredPosition;
                    _content.localScale = _contentLocalScale;
                }
                if (OwnsMask && Mask != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(Mask);
                    else
                        UnityEngine.Object.DestroyImmediate(Mask);
                }
            }
        }

        private readonly struct RectTransformState
        {
            private readonly Vector2 _anchorMin;
            private readonly Vector2 _anchorMax;
            private readonly Vector2 _pivot;
            private readonly Vector2 _anchoredPosition;
            private readonly Vector2 _sizeDelta;
            private readonly Vector3 _localScale;
            private readonly Quaternion _localRotation;

            public RectTransformState(RectTransform rect)
            {
                _anchorMin = rect.anchorMin;
                _anchorMax = rect.anchorMax;
                _pivot = rect.pivot;
                _anchoredPosition = rect.anchoredPosition;
                _sizeDelta = rect.sizeDelta;
                _localScale = rect.localScale;
                _localRotation = rect.localRotation;
            }

            public void Apply(RectTransform rect)
            {
                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
                rect.pivot = _pivot;
                rect.anchoredPosition = _anchoredPosition;
                rect.sizeDelta = _sizeDelta;
                rect.localScale = _localScale;
                rect.localRotation = _localRotation;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
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

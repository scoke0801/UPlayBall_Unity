using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Diagnostics;
using Baseball.Game.Historical;
using Baseball.Game.Guide;
using Baseball.Core.Shop;
using Baseball.Game.Manager;
using Baseball.Game.Shop;
using Baseball.Game.SceneFlow;
using Baseball.Game.Sound;
using Baseball.Simulation.Historical;
using Baseball.Presentation.Career;
using Baseball.Presentation.Match;
using Baseball.Presentation.Guide;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.Shop;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baseball.Presentation.Owner
{
    /// <summary>Production OwnerModeManager를 공용 Shell과 실제 구단주 Home에 연결한다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class OwnerModeShellCoordinator : MonoBehaviour
    {
        public const string HomeRouteId = "Owner.Home";
        public const string MatchRouteId = "Owner.Match";

        private readonly OwnerModeRuntimeSnapshotFactory _snapshotFactory = new OwnerModeRuntimeSnapshotFactory();
        private readonly OwnerSharedInformationSnapshotFactory _sharedInformationSnapshotFactory =
            new OwnerSharedInformationSnapshotFactory();
        private SharedGameShellView _shell;
        private SharedGameShellPresenter _presenter;
        private GameModeUiProfile _profile;
        private OwnerShellStatusProvider _statusProvider;
        private OwnerModeManager _manager;
        private UI_Scene_OwnerHome _homeView;
        private UI_Scene_OwnerMatchSpectator _matchSpectatorView;
        private UI_Popup_OwnerSeasonSimulation _seasonSimulationPopup;
        private OwnerExpansionWorkspaceCoordinator _expansionWorkspace;
        private OwnerSharedInformationWorkspaceCoordinator _sharedInformationWorkspace;
        private GameModeNavigationState _navigationState;
        private ShopService _shopService;
        private LineupPresetState _pendingLineupPreset;
        private OwnerActiveRosterChangePreview _pendingActiveRosterChange;
        private string _selectedContractCardId = string.Empty;
        private int _selectedContractTerm = 1;
        private string _selectedTradePartnerId = string.Empty;
        private string _selectedTradeOutgoingCardId = string.Empty;
        private string _selectedTradeIncomingCardId = string.Empty;
        private string _pendingTrainingCardId = string.Empty;
        private int? _selectedRecordsSeason;
        private string _pendingTrainingProgramId = string.Empty;
        private bool _hasAppliedExclusivePresentation;
        private bool _isOwnerMatchVisible;
        private bool _isTransitioningToOwnerMatch;
        private bool _isSeasonSimulationVisible;
        private int _seasonSimulationStartedFrame = -1;

        public void Initialize(SharedGameShellView shell, OwnerModeManager manager)
        {
            if (_shell != null)
                return;

            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _shell.SetChromeOverlayMode(false);
            _shell.SettingsRequested += HandleSettingsRequested;
            UIManager.Instance.NavigationBackRequested += HandleCancelRequested;
            _manager.RuntimeChanged += HandleRuntimeChanged;
            DevelopmentRealIdentitySettings.Changed += Refresh;
            FrontManagerGuideCtaRouter.OwnerRouteRequested -= HandleGuideRouteRequested;
            FrontManagerGuideCtaRouter.OwnerRouteRequested += HandleGuideRouteRequested;
            UiGameModeSession.ModeChanged += HandleModeChanged;
            EnsureExpansionWorkspace();
            EnsureSharedInformationWorkspace();
            Refresh();
        }

        public void Refresh()
        {
            _boundSnapshotRoutes.Clear();
            bool isVisible = _manager != null &&
                _manager.HasActiveRuntime &&
                UiGameModeSession.IsSelected(UiGameMode.OwnerCareer);
            if (_shell != null)
                _shell.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                // 진입 도중 StartNewGame/Load가 RuntimeChanged를 먼저 쏘면 아직 모드 선택 전이라 여기로 온다.
                // 실패가 아니라 정상적인 중간 상태이므로 계측을 끊지 않고 구간만 남긴다.
                OwnerModeEntryProfiler.Mark("구단 런타임 생성·로드(RuntimeChanged 통지 시점)");
                _homeView?.SetVisible(false);
                _matchSpectatorView?.SetVisible(false);
                _expansionWorkspace?.HideAll();
                _sharedInformationWorkspace?.HideAll();
                _hasAppliedExclusivePresentation = false;
                return;
            }

            OwnerModeEntryProfiler.Mark("Shell 활성화");
            OwnerHomePresentationModel home = OwnerHomePresentationBuilder.Build(
                _snapshotFactory.CreateHome(_manager));
            OwnerModeEntryProfiler.Mark("홈 스냅샷 생성");
            if (_statusProvider == null)
            {
                _statusProvider = new OwnerShellStatusProvider(home);
                _profile = OwnerModeUiProfileFactory.Create();
                _navigationState = new GameModeNavigationState(_profile, HomeRouteId, HomeRouteId);
                _presenter = new SharedGameShellPresenter(
                    _shell,
                    _profile,
                    _statusProvider);
                _presenter.NavigationRequested += HandleNavigationRequested;
                _presenter.BackRequested += HandleBackRequested;
            }
            else
            {
                _statusProvider.Update(home);
            }

            _shell.SetModeBackgroundResourcePath(
                OwnerUiAssetIds.ResolveHomeBackgroundResourcePath(home.Snapshot.LeagueGrade));

            OwnerModeEntryProfiler.Mark("Shell 프레젠터 초기화");
            EnsureHomeView();
            OwnerModeEntryProfiler.Mark("홈 뷰 생성");
            if (_manager.Runtime.ManagerMode.LiveSeason.NextPlayerGame == null)
            {
                _expansionWorkspace.ClearMatchPreparation();
                if (_navigationState.IsContextOpen && !_isOwnerMatchVisible && !_isTransitioningToOwnerMatch)
                    _navigationState.Navigate(HomeRouteId);
            }
            if (!_isOwnerMatchVisible && !_isTransitioningToOwnerMatch)
                EnsureRouteSnapshots(ActiveRouteId);
            OwnerModeEntryProfiler.Mark("현재 화면 스냅샷 바인딩");
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch)
            {
                ShowOwnerMatchSpectator();
                OwnerModeEntryProfiler.MarkHomeComposed();
                _hasAppliedExclusivePresentation = false;
                return;
            }
            if (!string.Equals(ActiveRouteId, HomeRouteId, StringComparison.Ordinal) &&
                _sharedInformationWorkspace.TryShowRoute(ActiveRouteId))
            {
                _expansionWorkspace.HideAll();
                _homeView.SetVisible(false);
                OwnerModeEntryProfiler.MarkHomeComposed();
                _hasAppliedExclusivePresentation = false;
                return;
            }
            if (!string.Equals(ActiveRouteId, HomeRouteId, StringComparison.Ordinal) &&
                TryShowExpansionRoute(ActiveRouteId))
            {
                _sharedInformationWorkspace.HideAll();
                _homeView.SetVisible(false);
                OwnerModeEntryProfiler.MarkHomeComposed();
                _hasAppliedExclusivePresentation = false;
                return;
            }

            ShowHome(home);
            _hasAppliedExclusivePresentation = false;
        }

        private void ShowHome(OwnerHomePresentationModel home)
        {
            if (_navigationState != null && !string.Equals(ActiveRouteId, HomeRouteId, StringComparison.Ordinal))
                _navigationState.Navigate(HomeRouteId);
            _expansionWorkspace.HideAll();
            _sharedInformationWorkspace.HideAll();
            _matchSpectatorView?.SetVisible(false);
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _homeView.Bind(home, _manager.Runtime.ManagerMode.LiveSeason.NextPlayerGame != null);
            _homeView.SetVisible(true);
            _presenter.ShowContext(new ShellContextModel(
                HomeRouteId,
                "구단 현황",
                "다음 경기, 1군 구성과 구단 자원을 현재 저장 상태로 확인합니다.",
                "구단주 모드"));
            OwnerModeEntryProfiler.MarkHomeComposed();
        }

        private void LateUpdate()
        {
            if (_shell == null || !_shell.gameObject.activeSelf)
                return;

            Transform parent = _shell.transform.parent;
            if (parent != null && _shell.transform.GetSiblingIndex() != parent.childCount - 1)
                _shell.transform.SetAsLastSibling();
            if (!_hasAppliedExclusivePresentation)
            {
                HidePlayerPresentation();
                OwnerModeEntryProfiler.Mark("선수 모드 화면 정리");
            }
        }

        private void Update()
        {
            if (!_isSeasonSimulationVisible || _manager == null)
                return;
            if (!_manager.IsRegularSeasonSimulationRunning)
            {
                FinishSeasonSimulation();
                return;
            }

            _seasonSimulationPopup.Bind(
                _manager.RegularSeasonSimulationProgress,
                _manager.GetTeamDisplayName);
            // 확인 클릭과 같은 Frame에 계산을 시작하면 Popup이 한 번도 그려지지 않는다.
            if (Time.frameCount <= _seasonSimulationStartedFrame)
                return;

            bool succeeded = _manager.AdvanceRegularSeasonSimulationFrame();
            _seasonSimulationPopup.Bind(
                _manager.RegularSeasonSimulationProgress,
                _manager.GetTeamDisplayName);
            if (!succeeded || !_manager.IsRegularSeasonSimulationRunning)
                FinishSeasonSimulation();
        }

        private void OnDestroy()
        {
            SetOwnerMatchBgm(false);
            _manager?.AbortRegularSeasonSimulationForSceneUnload();
            if (_manager != null)
                _manager.RuntimeChanged -= HandleRuntimeChanged;
            DevelopmentRealIdentitySettings.Changed -= Refresh;
            UiGameModeSession.ModeChanged -= HandleModeChanged;
            FrontManagerGuideCtaRouter.OwnerRouteRequested -= HandleGuideRouteRequested;
            if (_shell != null)
                _shell.SettingsRequested -= HandleSettingsRequested;
            if (UIManager.Instance != null)
                UIManager.Instance.NavigationBackRequested -= HandleCancelRequested;
            if (_presenter != null)
            {
                _presenter.NavigationRequested -= HandleNavigationRequested;
                _presenter.BackRequested -= HandleBackRequested;
                _presenter.Dispose();
            }
            UnsubscribeExpansionWorkspace();
            if (_sharedInformationWorkspace != null)
                _sharedInformationWorkspace.NextMatchAnalysisRequested -= HandleOpponentAnalysisRequested;
            if (_matchSpectatorView != null)
            {
                _matchSpectatorView.HomeRequested -= HandleOwnerMatchHomeRequested;
                _matchSpectatorView.MatchAudioEnabledChanged -= HandleOwnerMatchAudioEnabledChanged;
                if (Application.isPlaying) Destroy(_matchSpectatorView.gameObject);
                else DestroyImmediate(_matchSpectatorView.gameObject);
            }
            if (_seasonSimulationPopup != null)
            {
                _seasonSimulationPopup.StopRequested -= HandleStopSeasonSimulationRequested;
                if (Application.isPlaying) Destroy(_seasonSimulationPopup.gameObject);
                else DestroyImmediate(_seasonSimulationPopup.gameObject);
            }
            if (_homeView != null)
            {
                _homeView.OpponentAnalysisRequested -= HandleOpponentAnalysisRequested;
                _homeView.MatchPreparationRequested -= HandleMatchPreparationRequested;
                _homeView.PlayNextGameRequested -= HandlePlayNextGameRequested;
                _homeView.CompleteSeasonRequested -= HandleCompleteSeasonRequested;
                _homeView.AdvanceSeasonRequested -= HandleAdvanceSeasonRequested;
                _homeView.NavigationRequested -= HandleNavigationRequested;
                _homeView.SaveRequested -= HandleSaveRequested;
                if (Application.isPlaying) Destroy(_homeView.gameObject);
                else DestroyImmediate(_homeView.gameObject);
            }
        }

        private void HandleRuntimeChanged()
        {
            _boundSnapshotRoutes.Clear();
            _shopService = null;
            _pendingLineupPreset = null;
            _pendingActiveRosterChange = null;
            Refresh();
        }

        private void HandleModeChanged(UiGameMode? mode)
        {
            if (mode != UiGameMode.OwnerCareer && _manager != null &&
                _manager.IsRegularSeasonSimulationRunning)
            {
                _manager.AbortRegularSeasonSimulationForSceneUnload();
                _isSeasonSimulationVisible = false;
                _seasonSimulationPopup?.Hide();
            }
            if (mode != UiGameMode.OwnerCareer && _isOwnerMatchVisible)
            {
                _matchSpectatorView?.EndPresentation();
                _isOwnerMatchVisible = false;
                SetOwnerMatchBgm(false);
            }
            Refresh();
        }

        private void HandleNavigationRequested(string routeId)
        {
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch || _isSeasonSimulationVisible)
                return;

            try
            {
                routeId = _profile.IsContextRoute(routeId)
                    ? _navigationState.NavigateContext(routeId)
                    : _navigationState.Navigate(
                        OwnerModeUiProfileFactory.ResolvePrimaryMenuRoute(_profile, routeId));
            }
            catch (InvalidOperationException)
            {
                ShowHomeWithFeedback("현재 시즌 상태에서는 해당 화면을 열 수 없습니다.");
                return;
            }

            ShowSelectedRoute(routeId);
        }

        /// <summary>프런트 매니저의 첫 선수 배정 안내 CTA를 실제 선수단 화면에 연결한다.</summary>
        private void HandleGuideRouteRequested(GuideCtaAction action, string eventId)
        {
            string routeId = action switch
            {
                GuideCtaAction.OpenRoster or GuideCtaAction.OpenLineup or GuideCtaAction.OpenTodayLineup =>
                    OwnerNavigationRoutes.RosterLineup,
                GuideCtaAction.OpenPitchingRole or GuideCtaAction.OpenPitchingStaff or GuideCtaAction.OpenBullpen =>
                    OwnerNavigationRoutes.RosterPitching,
                GuideCtaAction.OpenScout or GuideCtaAction.OpenFocusScout => OwnerNavigationRoutes.PowerUpScout,
                GuideCtaAction.OpenTactics => OwnerNavigationRoutes.RosterTacticCards,
                _ => HomeRouteId
            };
            HandleNavigationRequested(routeId);
            if (_manager.HasActiveRuntime &&
                !_manager.Runtime.Onboarding.IsCompleted &&
                !string.IsNullOrWhiteSpace(eventId) &&
                eventId.IndexOf("owner-first-entry", StringComparison.Ordinal) >= 0)
            {
                _manager.SkipOnboarding();
            }
        }

        private void HandleOpponentAnalysisRequested()
        {
            OpenMatchCenter(OwnerNavigationRoutes.MatchCenterAnalysis);
        }

        private void HandleMatchPreparationRequested()
        {
            OpenMatchCenter(OwnerNavigationRoutes.MatchCenterLineup);
        }

        private void OpenMatchCenter(string routeId)
        {
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;

            try
            {
                string destination = _navigationState.OpenContext(routeId);
                ShowSelectedRoute(destination);
            }
            catch (InvalidOperationException exception)
            {
                _homeView.SetFeedback(exception.Message, true);
            }
        }

        private void HandleBackRequested()
        {
            if (_shell == null || !_shell.gameObject.activeInHierarchy ||
                _isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;

            if (_isSeasonSimulationVisible)
            {
                HandleStopSeasonSimulationRequested();
                return;
            }

            if (_sharedInformationWorkspace != null && _sharedInformationWorkspace.TryCloseTeamLineup())
                return;

            if (_expansionWorkspace != null && _expansionWorkspace.TryCloseShopOverlay())
                return;

            if (_navigationState != null && _navigationState.TryBack(out string returnRouteId))
            {
                ShowSelectedRoute(returnRouteId);
                return;
            }

            if (_navigationState != null && _navigationState.IsAtRoot)
                HandleSettingsRequested();
        }

        private void HandleCancelRequested()
        {
            if (_shell == null || !_shell.gameObject.activeInHierarchy ||
                _isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;
            if (_isSeasonSimulationVisible)
            {
                HandleStopSeasonSimulationRequested();
                return;
            }
            if (_sharedInformationWorkspace != null && _sharedInformationWorkspace.TryCloseTeamLineup())
                return;
            if (_expansionWorkspace != null && _expansionWorkspace.TryHandleCancel())
                return;
            if (_navigationState == null)
                return;
            if (_navigationState.IsAtRoot)
            {
                HandleSettingsRequested();
                return;
            }

            string homeRouteId = _navigationState.Navigate(HomeRouteId);
            ShowSelectedRoute(homeRouteId);
        }

        private void ShowSelectedRoute(string routeId)
        {
            if (string.Equals(routeId, HomeRouteId, StringComparison.Ordinal))
            {
                ShowHome(OwnerHomePresentationBuilder.Build(_snapshotFactory.CreateHome(_manager)));
                return;
            }

            EnsureRouteSnapshots(routeId);
            if (_sharedInformationWorkspace.TryShowRoute(routeId))
            {
                _expansionWorkspace.HideAll();
                _homeView.SetVisible(false);
                return;
            }

            if (TryShowExpansionRoute(routeId))
            {
                _sharedInformationWorkspace.HideAll();
                _homeView.SetVisible(false);
                return;
            }

            ShowHomeWithFeedback("현재 시즌 상태에서는 해당 화면을 열 수 없습니다.");
        }

        private bool TryShowExpansionRoute(string navigationRouteId)
        {
            string workspaceRouteId = ResolveWorkspaceRoute(navigationRouteId);
            bool isShown = _expansionWorkspace.TryShowRoute(workspaceRouteId, navigationRouteId);
            if (isShown && string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal) &&
                GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
            {
                GuideManager.Instance.PublishOwnerFact(
                    "ScoutFirstEntry",
                    $"owner-scout-first:{_manager.Runtime.PlayerTeamSeasonKey}");
            }
            if (isShown && string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal) &&
                _manager.HasAvailableCardStudySlot() && GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
            {
                GuideManager.Instance.PublishOwnerFact(
                    "CardStudySlotAvailable",
                    $"owner-study-slot:{_manager.Runtime.ManagerMode.LiveSeason.SeasonNumber}:{_manager.Runtime.PlayerGrowth.StudyProjects.Count}");
            }
            return isShown;
        }

        private static string ResolveWorkspaceRoute(string navigationRouteId)
        {
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterAnalysis, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.PregameRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterLineup, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal))
                return OwnerNavigationRoutes.RosterTacticCards;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterCondition, StringComparison.Ordinal))
                return OwnerNavigationRoutes.MatchCenterOpponentLineup;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal))
                return OwnerNavigationRoutes.PowerUpScout;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal))
                return OwnerNavigationRoutes.PowerUpEnhancementSale;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal))
                return OwnerNavigationRoutes.PowerUpTraining;
            return navigationRouteId;
        }

        private void HandleWishlistToggleRequested(string cardId)
        {
            try
            {
                bool isAdded = _manager.ToggleWishlist(cardId);
                ShowFeedback(isAdded ? "위시리스트에 등록했습니다." : "위시리스트에서 해제했습니다.", false);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                ShowFeedback(exception.Message, true);
            }
        }

        private void HandleEncyclopediaScoutRequested(string cardId)
        {
            if (!TryFindMostFocusedScoutProduct(cardId, out string productId))
            {
                ShowFeedback("현재 이 카드가 포함되는 스카우트 상품이 없습니다.", true);
                return;
            }
            HandleNavigationRequested(OwnerNavigationRoutes.PowerUpScout);
            _expansionWorkspace.SelectScoutProduct(productId);
            ShowFeedback("선택한 카드가 포함되는 가장 좁은 현재 스카우트 풀을 열었습니다.", false);
        }

        private bool TryFindMostFocusedScoutProduct(string cardId, out string productId)
        {
            productId = string.Empty;
            if (_manager?.Runtime == null ||
                !_manager.Runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                return false;
            _shopService ??= _manager.CreateShopService();

            WorldCardCatalog catalog = _manager.Runtime.WorldCardCatalog;
            ScoutFeaturePolicy featurePolicy = OwnerShopComposer.ResolveScoutFeaturePolicy(catalog);
            IReadOnlyList<ScoutPoolDefinition> pools = OwnerShopComposer.CreateScoutPools(catalog, featurePolicy);
            IReadOnlyList<ShopProductDefinition> products = _shopService.Catalog.GetProducts(ShopTab.PlayerCard);
            int bestCandidateCount = int.MaxValue;
            for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
            {
                ScoutPoolDefinition pool = pools[poolIndex];
                if (!ScoutRoller.IsCandidate(pool, catalog, featurePolicy, card)) continue;
                ShopProductDefinition product = FindSingleDrawScoutProduct(products, pool.ScoutPoolId);
                if (product == null || !_shopService.TryGetDetails(product.ProductId, out ShopProductDetails details))
                    continue;
                int candidateCount = 0;
                for (int bucketIndex = 0; bucketIndex < details.Probabilities.Count; bucketIndex++)
                    candidateCount += details.Probabilities[bucketIndex].CandidateCount;
                if (candidateCount > bestCandidateCount ||
                    (candidateCount == bestCandidateCount && string.CompareOrdinal(product.ProductId, productId) >= 0))
                    continue;
                bestCandidateCount = candidateCount;
                productId = product.ProductId;
            }
            return productId.Length > 0;
        }

        private static ShopProductDefinition FindSingleDrawScoutProduct(
            IReadOnlyList<ShopProductDefinition> products,
            string scoutPoolId)
        {
            ShopProductDefinition fallback = null;
            for (int index = 0; index < products.Count; index++)
            {
                ShopProductDefinition product = products[index];
                if (!string.Equals(product.SourceId, scoutPoolId, StringComparison.Ordinal)) continue;
                if (product.DrawCount == 1) return product;
                if (fallback == null) fallback = product;
            }
            return fallback;
        }

        private void HandleEncyclopediaCardRequested(string cardId)
        {
            HandleNavigationRequested(OwnerNavigationRoutes.RosterEncyclopedia);
            if (!string.IsNullOrWhiteSpace(cardId))
                _expansionWorkspace.SelectEncyclopediaCard(cardId);
        }

        private void HandleEncyclopediaWishlistRequested()
        {
            HandleNavigationRequested(OwnerNavigationRoutes.RosterWishlist);
        }

        private void ShowHomeWithFeedback(string message)
        {
            _navigationState.Navigate(HomeRouteId);
            Refresh();
            _homeView.SetFeedback(message, true);
        }

        private static void HandleSettingsRequested()
        {
            UI_Popup_CareerSettings.ShowRuntime();
        }

        private void HandlePlayNextGameRequested()
        {
            StartOwnerMatchSpectator();
        }

        private void HandleCompleteSeasonRequested()
        {
            if (_isSeasonSimulationVisible)
                return;
            if (!_manager.BeginRegularSeasonSimulation())
            {
                ShowFeedback(_manager.LastError, true);
                return;
            }

            EnsureSeasonSimulationPopup();
            _isSeasonSimulationVisible = true;
            _seasonSimulationStartedFrame = Time.frameCount;
            _seasonSimulationPopup.Bind(
                _manager.RegularSeasonSimulationProgress,
                _manager.GetTeamDisplayName);
            _seasonSimulationPopup.Show();
        }

        private void HandleStopSeasonSimulationRequested()
        {
            if (!_isSeasonSimulationVisible)
                return;
            ManagerRegularSeasonSimulationProgress progress = _manager.RegularSeasonSimulationProgress;
            _isSeasonSimulationVisible = false;
            _seasonSimulationPopup?.Hide();
            _manager.StopRegularSeasonSimulation();
            Refresh();
            ShowFeedback(
                $"{progress.LastCompletedRound}라운드까지 완료하고 시즌 진행을 중단했습니다. " +
                $"내 구단 {progress.PlayerGamesSimulated}경기가 반영됐습니다.",
                false);
        }

        private void FinishSeasonSimulation()
        {
            _isSeasonSimulationVisible = false;
            _seasonSimulationPopup?.Hide();
            Refresh();
            ManagerRegularSeasonCompletionResult result = _manager.LastRegularSeasonCompletion;
            if (result != null && result.IsCompleted)
            {
                ShowFeedback(
                    $"남은 {result.PlayerGamesSimulated}경기를 진행해 시즌을 완료했습니다. " +
                    $"최종 {result.SeasonWins}승 {result.SeasonDraws}무 {result.SeasonLosses}패입니다.",
                    false);
                return;
            }

            ShowFeedback(
                string.IsNullOrWhiteSpace(_manager.LastError)
                    ? "정규시즌 시뮬레이션을 완료하지 못했습니다."
                    : _manager.LastError,
                true);
        }

        private void HandleAdvanceSeasonRequested()
        {
            ExecuteOperation(() =>
            {
                ManagerSeasonAdvanceResult result = _manager.AdvanceSeason();
                if (result.IsApplied)
                {
                    ShowFeedback(
                        $"{result.NextSeason.SeasonNumber}번째 시즌을 시작했습니다. " +
                        $"리그 등급: {OwnerLeagueDisplayNameFormatter.FormatFull(result.PreviousLeagueGrade.Value)} → " +
                        OwnerLeagueDisplayNameFormatter.FormatFull(result.NextLeagueGrade.Value),
                        false);
                    return;
                }

                string message = result.Status switch
                {
                    ManagerSeasonAdvanceStatus.ContractRenewalRequired =>
                        "만료 예정 선수의 계약을 먼저 갱신하거나 정리해야 합니다.",
                    ManagerSeasonAdvanceStatus.InsufficientMoney =>
                        "선수·스태프 급여를 지급할 자금이 부족합니다.",
                    ManagerSeasonAdvanceStatus.InvalidStaffState =>
                        "스태프 계약 상태를 확인해야 다음 시즌으로 진행할 수 있습니다.",
                    _ => "아직 완료되지 않은 리그 경기가 있습니다."
                };
                ShowFeedback(message, true);
            });
        }

        private void HandlePregameMatchStartRequested()
        {
            StartOwnerMatchSpectator();
        }

        private void HandlePresetSelected(string presetId)
        {
            _pendingLineupPreset = null;
            _pendingActiveRosterChange = null;
            ExecuteOperation(() => _manager.SelectLineupPreset(presetId));
        }

        private void HandleSignStaffRequested(string offerId)
        {
            ExecuteOperation(() => _manager.SignStaff(offerId));
        }

        private void HandleContractPreviewRequested(string cardId, int seasons)
        {
            _selectedContractCardId = cardId ?? string.Empty;
            _selectedContractTerm = seasons;
            _expansionWorkspace.BindPlayerContracts(_snapshotFactory.CreatePlayerContracts(
                _manager, _selectedContractCardId, _selectedContractTerm));
        }

        private void HandleContractRenewalRequested(string cardId, int seasons)
        {
            _selectedContractCardId = cardId ?? string.Empty;
            _selectedContractTerm = seasons;
            ExecuteOperation(() =>
            {
                OwnerContractRenewalPreview result = _manager.RenewPlayerContract(cardId, seasons);
                HandleContractPreviewRequested(cardId, seasons);
                ShowFeedback(result.CanCommit
                    ? $"{seasons}년 계약 연장을 확정했습니다."
                    : result.Reason,
                    !result.CanCommit);
            });
        }

        private void HandleContractBatchRenewalRequested(int seasons)
        {
            ExecuteOperation(() =>
            {
                OwnerContractBatchPreview result = _manager.RenewExpiringPlayerContracts(seasons);
                HandleContractPreviewRequested(_selectedContractCardId, seasons);
                ShowFeedback(result.CanCommit
                    ? $"{result.Renewals.Count}명의 계약을 {seasons}년 연장했습니다."
                    : result.Reason, !result.CanCommit);
            });
        }

        private void HandleTradePreviewRequested(string teamSeasonKey, string outgoingCardId, string incomingCardId)
        {
            _selectedTradePartnerId = teamSeasonKey ?? string.Empty;
            _selectedTradeOutgoingCardId = outgoingCardId ?? string.Empty;
            _selectedTradeIncomingCardId = incomingCardId ?? string.Empty;
            _expansionWorkspace.BindPlayerTrade(_snapshotFactory.CreatePlayerTrade(
                _manager,
                _selectedTradePartnerId,
                _selectedTradeOutgoingCardId,
                _selectedTradeIncomingCardId));
        }

        private void HandleTradeRequested(string teamSeasonKey, string outgoingCardId, string incomingCardId)
        {
            ExecuteOperation(() =>
            {
                OwnerTradePreview result = _manager.CommitPlayerTrade(teamSeasonKey, outgoingCardId, incomingCardId);
                if (result.CanCommit)
                {
                    _selectedTradeOutgoingCardId = result.IncomingCardId;
                    _selectedTradeIncomingCardId = string.Empty;
                }
                ShowFeedback(result.CanCommit ? "1:1 트레이드를 확정했습니다." : result.Reason, !result.CanCommit);
            });
        }

        private void HandleTicketPolicyRequested(Baseball.Core.Historical.TicketPriceTier tier)
        {
            ExecuteOperation(() => _manager.SetTicketPolicy(tier));
        }

        private void HandleFacilityUpgradeRequested(Baseball.Core.Historical.FacilityType facilityType)
        {
            ExecuteOperation(() => _manager.UpgradeFacility(facilityType));
        }

        private void HandleStadiumUpgradeRequested()
        {
            ExecuteOperation(() => _manager.UpgradeStadium());
        }

        private void HandleWeekAdvanceRequested()
        {
            ExecuteOperation(() => _manager.AdvanceWeek());
        }

        private void HandleRecordsSeasonSelected(int seasonNumber)
        {
            _selectedRecordsSeason = seasonNumber;
            _sharedInformationWorkspace.BindSeasonRecords(
                _sharedInformationSnapshotFactory.CreateSeasonRecords(_manager, seasonNumber),
                HandleRecordsSeasonSelected);
        }

        private void HandleLoadRequested()
        {
            _selectedRecordsSeason = null;
            ExecuteOperation(() => _manager.Load());
        }

        private void HandleSaveRequested()
        {
            try
            {
                _manager.Save();
                ShowFeedback("구단주 진행 데이터를 저장했습니다.", false);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is System.IO.IOException ||
                exception is UnauthorizedAccessException)
            {
                ShowFeedback(exception.Message, true);
            }
        }

        private void HandleTitleRequested()
        {
            _isOwnerMatchVisible = false;
            _matchSpectatorView?.EndPresentation();
            SetOwnerMatchBgm(false);
            UiGameModeSession.Clear();
            UI_Scene_NewGame title = FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            title?.Show();
        }

        private void EnsureHomeView()
        {
            if (_homeView != null)
                return;

            _homeView = UI_Scene_OwnerHome.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.ContextActionBarHost);
            _homeView.OpponentAnalysisRequested += HandleOpponentAnalysisRequested;
            _homeView.MatchPreparationRequested += HandleMatchPreparationRequested;
            _homeView.PlayNextGameRequested += HandlePlayNextGameRequested;
            _homeView.CompleteSeasonRequested += HandleCompleteSeasonRequested;
            _homeView.AdvanceSeasonRequested += HandleAdvanceSeasonRequested;
            _homeView.NavigationRequested += HandleNavigationRequested;
            _homeView.SaveRequested += HandleSaveRequested;
        }

        private void EnsureMatchSpectatorView()
        {
            if (_matchSpectatorView != null)
                return;

            _matchSpectatorView = UI_Scene_OwnerMatchSpectator.CreateRuntime(_shell.MainWorkspaceHost);
            _matchSpectatorView.HomeRequested += HandleOwnerMatchHomeRequested;
            _matchSpectatorView.MatchAudioEnabledChanged += HandleOwnerMatchAudioEnabledChanged;
        }

        private void EnsureSeasonSimulationPopup()
        {
            if (_seasonSimulationPopup != null)
                return;
            _seasonSimulationPopup = UI_Popup_OwnerSeasonSimulation.CreateRuntime(_shell.PopupHost);
            _seasonSimulationPopup.StopRequested += HandleStopSeasonSimulationRequested;
        }

        private void StartOwnerMatchSpectator()
        {
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;

            EnsureMatchSpectatorView();
            _isTransitioningToOwnerMatch = true;
            _isOwnerMatchVisible = true;
            _navigationState.OpenContext(OwnerNavigationRoutes.MatchSpectator);
            ShowOwnerMatchSpectator();
            OwnerMatchPresentationOptions settings = OwnerMatchPresentationSettings.Load();
            SetOwnerMatchBgm(true, settings.ShouldPlayMatchAudio);
            try
            {
                // PlayNextGame 내부 RuntimeChanged가 먼저 발생해도 위 전환 상태가 관전 View를 유지한다.
                _matchSpectatorView.PlayNextGame(_manager);
                ShowOwnerMatchSpectator();
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                _isOwnerMatchVisible = false;
                _isTransitioningToOwnerMatch = false;
                _matchSpectatorView.EndPresentation();
                SetOwnerMatchBgm(false);
                Refresh();
                _homeView.SetFeedback(exception.Message, true);
            }
            finally
            {
                _isTransitioningToOwnerMatch = false;
            }
        }

        private void ShowOwnerMatchSpectator()
        {
            bool isResultOnly = OwnerMatchPresentationSettings.Load().ViewingMode ==
                                OwnerMatchViewingMode.ResultOnly;
            _homeView?.SetVisible(false);
            _expansionWorkspace?.HideAll();
            _sharedInformationWorkspace?.HideAll();
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _matchSpectatorView?.SetVisible(true);
            _presenter?.ShowContext(new ShellContextModel(
                OwnerNavigationRoutes.MatchSpectator,
                isResultOnly ? "경기 결과" : "경기 관전",
                isResultOnly
                    ? "중계와 사운드 없이 최종 결과와 기록을 확인합니다."
                    : "감독의 경기 운영을 실시간 중계로 확인합니다.",
                "구단주 모드",
                canGoBack: false));
        }

        private void HandleOwnerMatchHomeRequested()
        {
            if (_matchSpectatorView == null || !_matchSpectatorView.IsComplete)
                return;

            _matchSpectatorView.EndPresentation();
            _isOwnerMatchVisible = false;
            SetOwnerMatchBgm(false);
            _navigationState.Navigate(HomeRouteId);
            Refresh();
        }

        private void HandleOwnerMatchAudioEnabledChanged(bool isEnabled)
        {
            if (_isOwnerMatchVisible)
                SetOwnerMatchBgm(true, isEnabled);
        }

        private static void SetOwnerMatchBgm(bool isBroadcasting, bool shouldPlayAudio = true)
        {
            BgmDirector.Instance?.SetOwnerMatchBroadcasting(isBroadcasting, shouldPlayAudio);
        }

        private void EnsureExpansionWorkspace()
        {
            if (_expansionWorkspace != null)
                return;

            _expansionWorkspace = gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            _expansionWorkspace.Initialize(_shell);
            _expansionWorkspace.SpecialRecruitRouteRequested += HandleNavigationRequested;
            _expansionWorkspace.SetRosterCardDetailResolver(
                cardIds => _snapshotFactory.CreateCollectionCardDetails(_manager, cardIds));
            _expansionWorkspace.MatchStartRequested += HandlePregameMatchStartRequested;
            _expansionWorkspace.SignStaffRequested += HandleSignStaffRequested;
            _expansionWorkspace.ContractPreviewRequested += HandleContractPreviewRequested;
            _expansionWorkspace.ContractRenewalRequested += HandleContractRenewalRequested;
            _expansionWorkspace.ContractBatchRenewalRequested += HandleContractBatchRenewalRequested;
            _expansionWorkspace.TradePreviewRequested += HandleTradePreviewRequested;
            _expansionWorkspace.TradeRequested += HandleTradeRequested;
            _expansionWorkspace.TicketPolicyRequested += HandleTicketPolicyRequested;
            _expansionWorkspace.FacilityUpgradeRequested += HandleFacilityUpgradeRequested;
            _expansionWorkspace.StadiumUpgradeRequested += HandleStadiumUpgradeRequested;
            _expansionWorkspace.WeekAdvanceRequested += HandleWeekAdvanceRequested;
            _expansionWorkspace.LineupSwapRequested += HandleLineupSwapRequested;
            _expansionWorkspace.LineupAssignmentRequested += HandleLineupAssignmentRequested;
            _expansionWorkspace.LineupPresetSelected += HandlePresetSelected;
            _expansionWorkspace.TeamColorSlotCycleRequested += HandleTeamColorSlotCycleRequested;
            _expansionWorkspace.TacticSlotCycleRequested += HandleTacticSlotCycleRequested;
            _expansionWorkspace.LineupChangeConfirmed += HandleLineupChangeConfirmed;
            _expansionWorkspace.LineupChangeCancelled += HandleLineupChangeCancelled;
            _expansionWorkspace.ConditionLineupRequested += HandleConditionLineupRequested;
            _expansionWorkspace.ShopPurchasePreviewRequested += HandleShopPurchasePreviewRequested;
            _expansionWorkspace.ShopPurchaseRequested += HandleShopPurchaseRequested;
            _expansionWorkspace.ShopDetailsRequested += HandleShopDetailsRequested;
            _expansionWorkspace.ShopRepurchaseRequested += HandleShopPurchaseRequested;
            _expansionWorkspace.ShopInventoryRequested += HandleShopInventoryRequested;
            _expansionWorkspace.ShopPlayerCardDetailsRequested += HandleShopPlayerCardDetailsRequested;
            _expansionWorkspace.CardEnhancementRequested += HandleCardEnhancementRequested;
            _expansionWorkspace.CardDuplicateSaleRequested += HandleCardDuplicateSaleRequested;
            _expansionWorkspace.DugoutConfigurationConfirmed += HandleDugoutConfigurationConfirmed;
            _expansionWorkspace.TeamColorSelectionConfirmed += HandleTeamColorSelectionConfirmed;
            _expansionWorkspace.TacticSelectionConfirmed += HandleTacticSelectionConfirmed;
            _expansionWorkspace.CardTrainingRequested += HandleCardTrainingRequested;
            _expansionWorkspace.CardStudyRequested += HandleCardStudyRequested;
            _expansionWorkspace.CardSkillPlacementRequested += HandleSkillPlacementRequested;
            _expansionWorkspace.CardSkillRemovalRequested += HandleSkillRemovalRequested;
            _expansionWorkspace.GrowthShopRequested += HandleGrowthShopRequested;
            _expansionWorkspace.CardSkillBlockAutoPlaceRequested += HandleCardSkillBlockAutoPlaceRequested;
            _expansionWorkspace.CardSkillBlockRemoveRequested += HandleCardSkillBlockRemoveRequested;
            _expansionWorkspace.WishlistToggleRequested += HandleWishlistToggleRequested;
            _expansionWorkspace.EncyclopediaScoutRequested += HandleEncyclopediaScoutRequested;
            _expansionWorkspace.EncyclopediaCardRequested += HandleEncyclopediaCardRequested;
            _expansionWorkspace.EncyclopediaWishlistRequested += HandleEncyclopediaWishlistRequested;
        }

        private void EnsureSharedInformationWorkspace()
        {
            if (_sharedInformationWorkspace != null)
                return;

            _sharedInformationWorkspace = gameObject.AddComponent<OwnerSharedInformationWorkspaceCoordinator>();
            _sharedInformationWorkspace.Initialize(_shell);
            _sharedInformationWorkspace.SetTeamLineupResolver(teamKey => _snapshotFactory.CreateTeamLineup(_manager, teamKey));
            _sharedInformationWorkspace.NextMatchAnalysisRequested += HandleOpponentAnalysisRequested;
        }

        private void UnsubscribeExpansionWorkspace()
        {
            if (_expansionWorkspace == null)
                return;

            _expansionWorkspace.MatchStartRequested -= HandlePregameMatchStartRequested;
            _expansionWorkspace.SignStaffRequested -= HandleSignStaffRequested;
            _expansionWorkspace.ContractPreviewRequested -= HandleContractPreviewRequested;
            _expansionWorkspace.ContractRenewalRequested -= HandleContractRenewalRequested;
            _expansionWorkspace.ContractBatchRenewalRequested -= HandleContractBatchRenewalRequested;
            _expansionWorkspace.TradePreviewRequested -= HandleTradePreviewRequested;
            _expansionWorkspace.TradeRequested -= HandleTradeRequested;
            _expansionWorkspace.TicketPolicyRequested -= HandleTicketPolicyRequested;
            _expansionWorkspace.FacilityUpgradeRequested -= HandleFacilityUpgradeRequested;
            _expansionWorkspace.StadiumUpgradeRequested -= HandleStadiumUpgradeRequested;
            _expansionWorkspace.WeekAdvanceRequested -= HandleWeekAdvanceRequested;
            _expansionWorkspace.LineupSwapRequested -= HandleLineupSwapRequested;
            _expansionWorkspace.LineupAssignmentRequested -= HandleLineupAssignmentRequested;
            _expansionWorkspace.LineupPresetSelected -= HandlePresetSelected;
            _expansionWorkspace.TeamColorSlotCycleRequested -= HandleTeamColorSlotCycleRequested;
            _expansionWorkspace.TacticSlotCycleRequested -= HandleTacticSlotCycleRequested;
            _expansionWorkspace.LineupChangeConfirmed -= HandleLineupChangeConfirmed;
            _expansionWorkspace.LineupChangeCancelled -= HandleLineupChangeCancelled;
            _expansionWorkspace.ConditionLineupRequested -= HandleConditionLineupRequested;
            _expansionWorkspace.ShopPurchasePreviewRequested -= HandleShopPurchasePreviewRequested;
            _expansionWorkspace.ShopPurchaseRequested -= HandleShopPurchaseRequested;
            _expansionWorkspace.ShopDetailsRequested -= HandleShopDetailsRequested;
            _expansionWorkspace.ShopRepurchaseRequested -= HandleShopPurchaseRequested;
            _expansionWorkspace.ShopInventoryRequested -= HandleShopInventoryRequested;
            _expansionWorkspace.ShopPlayerCardDetailsRequested -= HandleShopPlayerCardDetailsRequested;
            _expansionWorkspace.CardEnhancementRequested -= HandleCardEnhancementRequested;
            _expansionWorkspace.CardDuplicateSaleRequested -= HandleCardDuplicateSaleRequested;
            _expansionWorkspace.DugoutConfigurationConfirmed -= HandleDugoutConfigurationConfirmed;
            _expansionWorkspace.TeamColorSelectionConfirmed -= HandleTeamColorSelectionConfirmed;
            _expansionWorkspace.TacticSelectionConfirmed -= HandleTacticSelectionConfirmed;
            _expansionWorkspace.CardTrainingRequested -= HandleCardTrainingRequested;
            _expansionWorkspace.CardStudyRequested -= HandleCardStudyRequested;
            _expansionWorkspace.CardSkillPlacementRequested -= HandleSkillPlacementRequested;
            _expansionWorkspace.CardSkillRemovalRequested -= HandleSkillRemovalRequested;
            _expansionWorkspace.GrowthShopRequested -= HandleGrowthShopRequested;
            _expansionWorkspace.CardSkillBlockAutoPlaceRequested -= HandleCardSkillBlockAutoPlaceRequested;
            _expansionWorkspace.CardSkillBlockRemoveRequested -= HandleCardSkillBlockRemoveRequested;
            _expansionWorkspace.WishlistToggleRequested -= HandleWishlistToggleRequested;
            _expansionWorkspace.EncyclopediaScoutRequested -= HandleEncyclopediaScoutRequested;
            _expansionWorkspace.EncyclopediaCardRequested -= HandleEncyclopediaCardRequested;
            _expansionWorkspace.EncyclopediaWishlistRequested -= HandleEncyclopediaWishlistRequested;
            _expansionWorkspace.SpecialRecruitRouteRequested -= HandleNavigationRequested;
        }

        /// <summary>구매로 재화·보유 상태가 바뀔 때마다 상점 타일을 다시 판정해 표시한다.</summary>
        private void BindShopSnapshot()
        {
            _shopService = _manager.CreateShopService();
            _expansionWorkspace.BindShop(ShopPresentationModel.CreateSnapshot(_shopService));
        }

        private void HandleShopPurchaseRequested(string productId)
        {
            if (_shopService == null)
                return;
            if (!TryCreateShopDetails(productId, out ShopProductDetailsSnapshot details))
            {
                _expansionWorkspace.SetShopFeedback("상품 정보를 다시 불러온 뒤 시도해 주세요.", true);
                return;
            }

            _expansionWorkspace.SetShopProcessing(true);
            ShopPurchaseResult result;
            try
            {
                result = _manager.PurchaseShopProduct(productId);
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                _expansionWorkspace.DismissShopPurchaseConfirmation();
                _expansionWorkspace.SetShopFeedback(exception.Message, true);
                return;
            }
            finally
            {
                _expansionWorkspace.SetShopProcessing(false);
            }

            _expansionWorkspace.SetShopFeedback(
                result.IsSuccess ? DescribePurchase(result) : result.FailureMessage,
                !result.IsSuccess);
            if (result.IsSuccess)
                _expansionWorkspace.ShowShopReveal(
                    result,
                    details,
                    CreateShopRevealCards(result, details),
                    CreateShopRevealSkillBlocks(result, details));
            else
                _expansionWorkspace.DismissShopPurchaseConfirmation();
        }

        private PlayerMiniCardModel[] CreateShopRevealCards(ShopPurchaseResult result, ShopProductDetailsSnapshot details)
        {
            if (details.Kind != ShopProductKind.PlayerCardPack) return null;
            var models = new PlayerMiniCardModel[result.Items.Length];
            for (int index = 0; index < models.Length; index++)
            {
                ShopGrantedItem item = result.Items[index];
                if (!_manager.Runtime.WorldCardCatalog.TryGetCard(item.ItemId, out var card)) continue;
                var season = _manager.Runtime.WorldCardCatalog.GetPlayerSeason(card);
                models[index] = new PlayerMiniCardModel(item.ItemId,
                _manager.Runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                    OwnerCollectionPresentationBuilder.FormatPlayerRole(
                        season.Position,
                        season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null),
                    season.OriginYear.ToString(), "Cost " + season.Cost,
                    item.GradeLabel, item.IsNew ? "신규 영입" : "중복 획득",
                    portraitAssetKey: season.Position.ToString(), isInteractable: false, frameEdition: card.Edition, cost: season.Cost);
            }
            return models;
        }

        private ShopSkillBlockRevealModel[] CreateShopRevealSkillBlocks(
            ShopPurchaseResult result,
            ShopProductDetailsSnapshot details)
        {
            if (details.Kind != ShopProductKind.SkillBlockPack) return null;
            var models = new ShopSkillBlockRevealModel[result.Items.Length];
            var definitions = _manager.Balance.Growth.SkillBlocks;
            for (int itemIndex = 0; itemIndex < result.Items.Length; itemIndex++)
            {
                string definitionId = result.Items[itemIndex].ItemId;
                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                {
                    if (!string.Equals(
                            definitions[definitionIndex].BlockId,
                            definitionId,
                            StringComparison.Ordinal))
                        continue;
                    models[itemIndex] = new ShopSkillBlockRevealModel(definitions[definitionIndex]);
                    break;
                }
            }
            return models;
        }

        private void HandleShopPurchasePreviewRequested(string productId)
        {
            if (!TryCreateShopDetails(productId, out ShopProductDetailsSnapshot details))
            {
                _expansionWorkspace.SetShopFeedback("상품 상세 정보를 불러올 수 없습니다.", true);
                return;
            }
            _expansionWorkspace.ShowShopPurchaseConfirmation(details);
        }

        private void HandleShopInventoryRequested(string productId)
        {
            if (_shopService == null ||
                !_shopService.Catalog.TryGetProduct(productId, out ShopProductDefinition product))
            {
                _expansionWorkspace.SetShopFeedback("획득 항목의 보관 위치를 찾을 수 없습니다.", true);
                return;
            }

            string routeId = product.Kind switch
            {
                ShopProductKind.PlayerCardPack => OwnerNavigationRoutes.RosterCollection,
                ShopProductKind.SkillBlockPack => OwnerNavigationRoutes.PowerUpSkills,
                ShopProductKind.TacticCardPack => OwnerNavigationRoutes.DugoutTactics,
                _ => OwnerNavigationRoutes.Shop
            };
            HandleNavigationRequested(routeId);
        }

        private void HandleCardEnhancementRequested(string cardId)
        {
            ExecuteOperation(() =>
            {
                CardEnhancementResult result = _manager.EnhanceOwnedCard(cardId);
                ShowFeedback(result switch
                {
                    CardEnhancementResult.Enhanced => "중복 카드 1장을 사용해 강화했습니다.",
                    CardEnhancementResult.NoDuplicate => "강화에 사용할 중복 카드가 없습니다.",
                    CardEnhancementResult.MaximumLevel => "이미 최대 강화 단계입니다.",
                    _ => "강화 결과를 확인할 수 없습니다."
                }, result != CardEnhancementResult.Enhanced);
            });
        }

        private void HandleCardDuplicateSaleRequested(string cardId, int count)
        {
            ExecuteOperation(() =>
            {
                int earnedSp = _manager.SellOwnedCardDuplicates(cardId, count);
            ShowFeedback($"중복 카드 {count:N0}장을 판매해 스카우트 포인트 {earnedSp:N0}을 획득했습니다.", false);
            });
        }

        private void HandleDugoutConfigurationConfirmed(OwnerDugoutConfigurationCommand command)
        {
            ExecuteOperation(() =>
            {
                _manager.ConfigureDugout(command.ManagerId, command.HeadCoachId, command.Policy);
                ShowFeedback("감독·수석코치와 작전 방침을 다음 경기 계획에 반영했습니다.", false);
            });
        }

        private void HandleTeamColorSelectionConfirmed(IReadOnlyList<string> teamColorIds)
        {
            ExecuteOperation(() =>
            {
                _manager.ConfigureSelectedPresetTeamColors(teamColorIds);
                ShowFeedback("팀컬러 두 슬롯을 선택 프리셋과 다음 경기 계획에 반영했습니다.", false);
            });
        }

        private void HandleTacticSelectionConfirmed(int gameId, IReadOnlyList<string> tacticCardIds)
        {
            ExecuteOperation(() =>
            {
                _manager.ConfigureScheduledGameTactics(gameId, tacticCardIds);
                ShowFeedback("작전카드 구성을 선택한 경기 계획에 반영했습니다.", false);
            });
        }

        private void HandleCardTrainingRequested(string cardId, string programId)
        {
            ExecuteOperation(() =>
            {
                CardTrainingPreview preview = _manager.PreviewOwnedCardTraining(cardId, programId);
                if (!preview.CanTrain)
                {
                    ShowFeedback("훈련 상한에 도달했거나 사용할 DP가 부족합니다.", true);
                    return;
                }
                if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal))
                {
                    CardTrainingResult confirmedResult = _manager.TrainOwnedCard(cardId, programId);
            ShowFeedback(
                $"{CareerSharedSnapshotFormatters.FormatAbility(confirmedResult.Ability)} +{confirmedResult.GainedPoints} · " +
                $"육성 포인트 {confirmedResult.SpentDp} 사용",
                false);
                    return;
                }
                if (!string.Equals(_pendingTrainingCardId, cardId, StringComparison.Ordinal) ||
                    !string.Equals(_pendingTrainingProgramId, programId, StringComparison.Ordinal))
                {
                    _pendingTrainingCardId = cardId;
                    _pendingTrainingProgramId = programId;
                    ShowFeedback(
                $"{CareerSharedSnapshotFormatters.FormatAbility(preview.Ability)} " +
                $"{preview.Current}→{preview.Current + preview.GainedPoints} / 상한 {preview.Ceiling} · " +
                $"육성 포인트 {preview.DpCost}. 같은 실행 버튼을 다시 누르면 확정합니다.",
                        false);
                    return;
                }
                _pendingTrainingCardId = string.Empty;
                _pendingTrainingProgramId = string.Empty;
                CardTrainingResult result = _manager.TrainOwnedCard(cardId, programId);
            ShowFeedback(
                $"{CareerSharedSnapshotFormatters.FormatAbility(result.Ability)} +{result.GainedPoints} · " +
                $"육성 포인트 {result.SpentDp} 사용",
                false);
            });
        }

        private void HandleGrowthShopRequested() => HandleNavigationRequested(OwnerNavigationRoutes.Shop);

        private void HandleSkillPlacementRequested(string cardId, int instanceId, int x, int y, int rotation)
        {
            ExecuteOperation(() =>
            {
                _manager.PlaceOwnedCardSkillBlock(cardId, instanceId, x, y, rotation);
                ShowFeedback("선택한 칸에 스킬 블록을 장착했습니다.", false);
            });
        }

        private void HandleSkillRemovalRequested(string cardId, int instanceId)
        {
            ExecuteOperation(() =>
            {
                bool removed = _manager.RemoveOwnedCardSkillBlock(cardId, instanceId);
                ShowFeedback(removed ? "선택한 블록을 인벤토리로 돌려놓았습니다." : "해제할 블록이 없습니다.", !removed);
            });
        }

        private void HandleCardStudyRequested(string cardId, string programId)
        {
            ExecuteOperation(() =>
            {
                _manager.StartOwnedCardStudy(cardId, programId);
                ShowFeedback($"유학을 시작했습니다. {_manager.Balance.OwnerCardGrowth.GetStudyProgram(programId).DurationWeeks}주 뒤 성장 결과가 확정됩니다.", false);
            });
        }

        private void HandleCardSkillBlockAutoPlaceRequested(string cardId)
        {
            ExecuteOperation(() =>
            {
                bool placed = _manager.AutoPlaceFirstAvailableSkillBlock(cardId);
                ShowFeedback(placed ? "빈 공간에 스킬 블록을 장착했습니다." : "장착 가능한 미사용 블록 또는 빈 공간이 없습니다.", !placed);
            });
        }

        private void HandleCardSkillBlockRemoveRequested(string cardId)
        {
            ExecuteOperation(() =>
            {
                bool removed = _manager.RemoveLastOwnedCardSkillBlock(cardId);
                ShowFeedback(removed ? "마지막 스킬 블록을 인벤토리로 돌려놓았습니다." : "해제할 스킬 블록이 없습니다.", !removed);
            });
        }

        private void HandleShopDetailsRequested(string productId)
        {
            if (!TryCreateShopDetails(productId, out ShopProductDetailsSnapshot details))
            {
                _expansionWorkspace.SetShopFeedback("상품 상세 정보를 불러올 수 없습니다.", true);
                return;
            }
            _expansionWorkspace.ShowShopDetails(details);
        }

        private void HandleShopPlayerCardDetailsRequested(string cardId)
        {
            if (_manager == null || string.IsNullOrEmpty(cardId)) return;
            OwnerCollectionSnapshot collection = _snapshotFactory.CreateCollection(_manager);
            for (int index = 0; index < collection.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = collection.Cards[index];
                if (!string.Equals(card.CardId, cardId, StringComparison.Ordinal)) continue;
                _expansionWorkspace.ShowShopPlayerCardDetails(card);
                return;
            }
            _expansionWorkspace.SetShopFeedback("선수 카드 상세 정보를 불러올 수 없습니다.", true);
        }

        private bool TryCreateShopDetails(string productId, out ShopProductDetailsSnapshot details)
        {
            if (_shopService == null)
            {
                details = null;
                return false;
            }
            return ShopPresentationModel.TryCreateDetails(_shopService, productId, out details);
        }

        private static string DescribePurchase(ShopPurchaseResult result)
        {
            var builder = new System.Text.StringBuilder("획득: ");
            for (int index = 0; index < result.Items.Length; index++)
            {
                if (index > 0)
                    builder.Append(", ");
                ShopGrantedItem item = result.Items[index];
                builder.Append(item.DisplayName).Append('(').Append(item.GradeLabel).Append(')');
                if (!item.IsNew)
                    builder.Append(" 중복");
            }
            return builder.ToString();
        }

        private static string DescribeQuote(ShopPurchaseQuote quote)
        {
            ShopProductDefinition product = quote.Product;
            var builder = new System.Text.StringBuilder();
            builder.Append(product.DisplayName).Append(" · ").Append(product.DrawCount).Append("회 · ")
                .Append(ShopCurrencyNames.GetSymbol(product.Currency)).Append(' ')
                .Append(product.Price.ToString("N0"));
            if (quote.RemainingPurchases != ShopPurchaseQuote.UnlimitedPurchases)
                builder.Append(" · 남은 구매 ").Append(quote.RemainingPurchases).Append("회");
            return builder.ToString();
        }

        private void HandleLineupSwapRequested(
            OwnerLineupSwapGroup group,
            int firstIndex,
            int secondIndex)
        {
            ExecuteOperation(() =>
            {
                LineupPresetState current = _pendingLineupPreset ??
                    _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                StageLineupPreview(OwnerLineupPresetCommandBuilder.Swap(current, group, firstIndex, secondIndex));
            });
        }

        private void HandleLineupAssignmentRequested(
            OwnerLineupSwapGroup group,
            int slotIndex,
            string incomingCardId)
        {
            ExecuteOperation(() =>
            {
                LineupPresetState current = _pendingLineupPreset ??
                    _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                CurrentRosterState roster = _pendingActiveRosterChange?.Roster ??
                    _manager.Runtime.GetRoster(_manager.Runtime.PlayerTeamSeasonKey);
                if (ContainsRosterCard(roster, incomingCardId))
                {
                    StageLineupPreview(OwnerLineupPresetCommandBuilder.AssignCard(
                        current,
                        group,
                        slotIndex,
                        incomingCardId));
                    return;
                }
                string outgoingCardId = OwnerLineupPresetCommandBuilder.GetAssignedCardId(
                    current,
                    group,
                    slotIndex);
                LineupPresetState candidate = OwnerLineupPresetCommandBuilder.ReplaceCard(
                    current,
                    outgoingCardId,
                    incomingCardId);
                OwnerActiveRosterChangePreview rosterChange = _pendingActiveRosterChange == null
                    ? _manager.PreviewActiveRosterChange(
                        outgoingCardId,
                        incomingCardId,
                        candidate)
                    : _manager.AppendActiveRosterChange(
                        _pendingActiveRosterChange,
                        outgoingCardId,
                        incomingCardId,
                        candidate);
                StageActiveRosterPreview(rosterChange);
            });
        }

        private void HandleTeamColorSlotCycleRequested(int slotIndex)
        {
            ExecuteOperation(() =>
            {
                IReadOnlyList<TeamColorDefinition> candidates = _manager.GetAvailableTeamColors();
                var ids = new string[candidates.Count];
                for (int index = 0; index < ids.Length; index++) ids[index] = candidates[index].TeamColorId;
                LineupPresetState current = _pendingLineupPreset ??
                    _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                StageLineupPreview(OwnerLineupPresetCommandBuilder.CycleTeamColor(current, slotIndex, ids));
            });
        }

        private void HandleTacticSlotCycleRequested(int slotIndex)
        {
            ExecuteOperation(() =>
            {
                IReadOnlyList<TacticCardDefinition> candidates = _manager.GetAvailableTacticCards();
                var ids = new string[candidates.Count];
                for (int index = 0; index < ids.Length; index++) ids[index] = candidates[index].CardId;
                LineupPresetState current = _pendingLineupPreset ??
                    _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                StageLineupPreview(OwnerLineupPresetCommandBuilder.CycleTactic(current, slotIndex, ids));
            });
        }

        private void StageLineupPreview(LineupPresetState candidate)
        {
            if (_pendingActiveRosterChange != null)
            {
                StageActiveRosterPreview(_manager.UpdateActiveRosterChangePreset(
                    _pendingActiveRosterChange,
                    candidate));
                return;
            }
            LineupPresetValidationResult validation = _manager.ValidateLineupPreset(candidate);
            _pendingLineupPreset = candidate;
            OwnerRosterLineupSnapshot live = _expansionWorkspace.RosterLineupSnapshot ??
                _snapshotFactory.CreateRosterLineup(_manager);
            OwnerRosterLineupSnapshot preview = live.CreatePreview(candidate, validation);
            OwnerRosterLineupPresentationModel previewModel = OwnerRosterLineupPresentationBuilder.Build(preview);
            _expansionWorkspace.BindRosterLineupPreview(
                previewModel,
                previewModel.CreatePendingChangeMessage());
        }

        private void StageActiveRosterPreview(OwnerActiveRosterChangePreview rosterChange)
        {
            _pendingActiveRosterChange = rosterChange ?? throw new ArgumentNullException(nameof(rosterChange));
            _pendingLineupPreset = rosterChange.Preset;
            OwnerRosterLineupSnapshot preview = _snapshotFactory.CreateRosterLineup(
                _manager,
                rosterChange,
                _expansionWorkspace.RosterLineupSnapshot?.OwnedPlayers);
            OwnerRosterLineupPresentationModel previewModel = OwnerRosterLineupPresentationBuilder.Build(preview);
            _expansionWorkspace.BindRosterLineupPreview(
                previewModel,
                previewModel.CreatePendingChangeMessage(
                    rosterChange.ReplacementCount,
                    rosterChange.ClearedTeamColorCount));
        }

        private void HandleLineupChangeConfirmed()
        {
            if (_pendingLineupPreset == null) return;
            ExecuteOperation(() =>
            {
                if (_pendingActiveRosterChange != null)
                    _manager.ApplyActiveRosterChange(_pendingActiveRosterChange);
                else
                    _manager.UpsertLineupPreset(_pendingLineupPreset);
                _pendingLineupPreset = null;
                _pendingActiveRosterChange = null;
                ShowFeedback("검증된 배치를 저장했습니다.", false);
            });
        }

        private void HandleLineupChangeCancelled()
        {
            _pendingLineupPreset = null;
            _pendingActiveRosterChange = null;
            _expansionWorkspace.BindRosterLineup(_snapshotFactory.CreateRosterLineup(_manager));
            ShowFeedback("변경 Preview를 취소하고 저장된 배치로 돌아왔습니다.", false);
        }

        private static bool ContainsRosterCard(CurrentRosterState roster, string cardId)
        {
            if (roster == null || string.IsNullOrWhiteSpace(cardId)) return false;
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }

        private void HandleConditionLineupRequested()
        {
            HandleNavigationRequested(OwnerNavigationRoutes.RosterLineup);
        }

        private void ExecuteOperation(Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is System.IO.IOException ||
                exception is UnauthorizedAccessException)
            {
                ShowFeedback(exception.Message, true);
            }
        }

        private void ShowFeedback(string message, bool isError)
        {
            if (!string.Equals(ActiveRouteId, HomeRouteId, StringComparison.Ordinal) &&
                _expansionWorkspace != null &&
                _expansionWorkspace.SetFeedback(message, isError))
                return;
            _homeView?.SetFeedback(message, isError);
        }

        private string ActiveRouteId => _navigationState?.ActiveRouteId ?? HomeRouteId;

        private void HidePlayerPresentation()
        {
            UIBase[] screens = FindObjectsByType<UIBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] is ICareerTabScreen)
                    screens[index].Hide();
            }
            FindFirstObjectByType<UI_Scene_CareerMatch>(FindObjectsInactive.Include)?.Hide();
            FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include)?.Hide();
            _hasAppliedExclusivePresentation = true;
        }
    }

    /// <summary>Management Scene에 구단주 공용 Shell을 한 번만 설치한다.</summary>
    public static class OwnerModePresentationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            OwnerModeShellCoordinator coordinator = FindCoordinator();
            if (scene.name != SceneCatalog.ManagementSceneName)
            {
                if (coordinator != null)
                    coordinator.gameObject.SetActive(false);
                return;
            }

            GameManager gameManager = GameManager.EnsureExists();
            UIManager uiManager = gameManager.EnsureManager<UIManager>("UIManager");
            OwnerModeManager ownerManager = gameManager.EnsureManager<OwnerModeManager>("OwnerModeManager");
            if (coordinator == null)
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(
                    uiManager.Root.GetLayerRoot(UILayer.Scene),
                    "OwnerModeSharedShell");
                coordinator = shell.gameObject.AddComponent<OwnerModeShellCoordinator>();
                coordinator.Initialize(shell, ownerManager);
            }

            CareerManager careerManager = gameManager.EnsureManager<CareerManager>("CareerManager");
            UiGameModeSession.ResolveInitialMode(
                careerManager.HasActiveCareer,
                ownerManager.HasActiveRuntime);
            coordinator.Refresh();
        }

        private static OwnerModeShellCoordinator FindCoordinator()
        {
            return UnityEngine.Object.FindFirstObjectByType<OwnerModeShellCoordinator>(
                FindObjectsInactive.Include);
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Diagnostics;
using Baseball.Game.Historical;
using Baseball.Game.Guide;
using Baseball.Core.Shop;
using Baseball.Game.Manager;
using Baseball.Game.Shop;
using Baseball.Game.SceneFlow;
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
    public sealed class OwnerModeShellCoordinator : MonoBehaviour
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
        private OwnerExpansionWorkspaceCoordinator _expansionWorkspace;
        private OwnerSharedInformationWorkspaceCoordinator _sharedInformationWorkspace;
        private GameModeNavigationState _navigationState;
        private ShopService _shopService;
        private string _pendingTrainingCardId = string.Empty;
        private string _pendingTrainingProgramId = string.Empty;
        private bool _hasAppliedExclusivePresentation;
        private bool _isOwnerMatchVisible;
        private bool _isTransitioningToOwnerMatch;

        public void Initialize(SharedGameShellView shell, OwnerModeManager manager)
        {
            if (_shell != null)
                return;

            _shell = shell != null ? shell : throw new ArgumentNullException(nameof(shell));
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            _shell.SetChromeOverlayMode(false);
            _shell.SettingsRequested += HandleSettingsRequested;
            _manager.RuntimeChanged += HandleRuntimeChanged;
            FrontManagerGuideCtaRouter.OwnerRouteRequested -= HandleGuideRouteRequested;
            FrontManagerGuideCtaRouter.OwnerRouteRequested += HandleGuideRouteRequested;
            UiGameModeSession.ModeChanged += HandleModeChanged;
            EnsureExpansionWorkspace();
            EnsureSharedInformationWorkspace();
            Refresh();
        }

        public void Refresh()
        {
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
                _navigationState = new GameModeNavigationState(_profile, HomeRouteId);
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

            OwnerModeEntryProfiler.Mark("Shell 프레젠터 초기화");
            EnsureHomeView();
            OwnerModeEntryProfiler.Mark("홈 뷰 생성");
            BindExpansionSnapshots();
            OwnerModeEntryProfiler.Mark("확장 워크스페이스 스냅샷 바인딩");
            BindSharedInformationSnapshots();
            OwnerModeEntryProfiler.Mark("공용 정보 스냅샷 바인딩");
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

        private void OnDestroy()
        {
            if (_manager != null)
                _manager.RuntimeChanged -= HandleRuntimeChanged;
            UiGameModeSession.ModeChanged -= HandleModeChanged;
            FrontManagerGuideCtaRouter.OwnerRouteRequested -= HandleGuideRouteRequested;
            if (_shell != null)
                _shell.SettingsRequested -= HandleSettingsRequested;
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
                if (Application.isPlaying) Destroy(_matchSpectatorView.gameObject);
                else DestroyImmediate(_matchSpectatorView.gameObject);
            }
            if (_homeView != null)
            {
                _homeView.OpponentAnalysisRequested -= HandleOpponentAnalysisRequested;
                _homeView.MatchPreparationRequested -= HandleMatchPreparationRequested;
                _homeView.PlayNextGameRequested -= HandlePlayNextGameRequested;
                if (Application.isPlaying) Destroy(_homeView.gameObject);
                else DestroyImmediate(_homeView.gameObject);
            }
        }

        private void HandleRuntimeChanged()
        {
            Refresh();
        }

        private void HandleModeChanged(UiGameMode? mode)
        {
            Refresh();
        }

        private void HandleNavigationRequested(string routeId)
        {
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;

            try
            {
                routeId = _profile.IsContextRoute(routeId)
                    ? _navigationState.NavigateContext(routeId)
                    : _navigationState.Navigate(routeId);
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
                    OwnerNavigationRoutes.RosterLineup,
                GuideCtaAction.OpenScout or GuideCtaAction.OpenFocusScout => OwnerNavigationRoutes.PowerUpScout,
                GuideCtaAction.OpenTactics => OwnerNavigationRoutes.DugoutTactics,
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
            if (_isOwnerMatchVisible || _isTransitioningToOwnerMatch)
                return;

            if (_navigationState != null && _navigationState.TryBack(out string returnRouteId))
                ShowSelectedRoute(returnRouteId);
        }

        private void ShowSelectedRoute(string routeId)
        {
            if (string.Equals(routeId, HomeRouteId, StringComparison.Ordinal))
            {
                Refresh();
                return;
            }

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
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterLineup, StringComparison.Ordinal) ||
                string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterCondition, StringComparison.Ordinal))
                return OwnerManagementRoutes.RosterCondition;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.ShopRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.CollectionRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.CollectionRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.DugoutTactics, StringComparison.Ordinal))
                return OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId;
            if (string.Equals(navigationRouteId, OwnerNavigationRoutes.DugoutManagerPolicy, StringComparison.Ordinal))
                return OwnerNavigationRoutes.DugoutLineupNotes;
            return navigationRouteId;
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

        private void HandlePregameMatchStartRequested()
        {
            StartOwnerMatchSpectator();
        }

        private void HandlePresetSelected(string presetId)
        {
            ExecuteOperation(() => _manager.SelectLineupPreset(presetId));
        }

        private void HandleSignStaffRequested(string offerId)
        {
            ExecuteOperation(() => _manager.SignStaff(offerId));
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

        private void HandleLoadRequested()
        {
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
        }

        private void EnsureMatchSpectatorView()
        {
            if (_matchSpectatorView != null)
                return;

            _matchSpectatorView = UI_Scene_OwnerMatchSpectator.CreateRuntime(_shell.MainWorkspaceHost);
            _matchSpectatorView.HomeRequested += HandleOwnerMatchHomeRequested;
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
            _homeView?.SetVisible(false);
            _expansionWorkspace?.HideAll();
            _sharedInformationWorkspace?.HideAll();
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _matchSpectatorView?.SetVisible(true);
            _presenter?.ShowContext(new ShellContextModel(
                OwnerNavigationRoutes.MatchSpectator,
                "경기 관전",
                "감독의 경기 운영을 실시간 중계로 확인합니다.",
                "구단주 모드",
                canGoBack: false));
        }

        private void HandleOwnerMatchHomeRequested()
        {
            if (_matchSpectatorView == null || !_matchSpectatorView.IsComplete)
                return;

            _matchSpectatorView.EndPresentation();
            _isOwnerMatchVisible = false;
            _navigationState.Navigate(HomeRouteId);
            Refresh();
        }

        private void EnsureExpansionWorkspace()
        {
            if (_expansionWorkspace != null)
                return;

            _expansionWorkspace = gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            _expansionWorkspace.Initialize(_shell);
            _expansionWorkspace.PregamePresetSelected += HandlePresetSelected;
            _expansionWorkspace.MatchStartRequested += HandlePregameMatchStartRequested;
            _expansionWorkspace.SignStaffRequested += HandleSignStaffRequested;
            _expansionWorkspace.TicketPolicyRequested += HandleTicketPolicyRequested;
            _expansionWorkspace.FacilityUpgradeRequested += HandleFacilityUpgradeRequested;
            _expansionWorkspace.StadiumUpgradeRequested += HandleStadiumUpgradeRequested;
            _expansionWorkspace.WeekAdvanceRequested += HandleWeekAdvanceRequested;
            _expansionWorkspace.SaveRequested += HandleSaveRequested;
            _expansionWorkspace.LoadRequested += HandleLoadRequested;
            _expansionWorkspace.LineupSwapRequested += HandleLineupSwapRequested;
            _expansionWorkspace.LineupPresetSelected += HandlePresetSelected;
            _expansionWorkspace.TeamColorSlotCycleRequested += HandleTeamColorSlotCycleRequested;
            _expansionWorkspace.TacticSlotCycleRequested += HandleTacticSlotCycleRequested;
            _expansionWorkspace.ShopPurchaseRequested += HandleShopPurchaseRequested;
            _expansionWorkspace.ShopDetailsRequested += HandleShopDetailsRequested;
            _expansionWorkspace.CardEnhancementRequested += HandleCardEnhancementRequested;
            _expansionWorkspace.CardDuplicateSaleRequested += HandleCardDuplicateSaleRequested;
            _expansionWorkspace.DugoutConfigurationConfirmed += HandleDugoutConfigurationConfirmed;
            _expansionWorkspace.CardTrainingRequested += HandleCardTrainingRequested;
            _expansionWorkspace.CardStudyRequested += HandleCardStudyRequested;
            _expansionWorkspace.CardSkillBlockAutoPlaceRequested += HandleCardSkillBlockAutoPlaceRequested;
            _expansionWorkspace.CardSkillBlockRemoveRequested += HandleCardSkillBlockRemoveRequested;
        }

        private void EnsureSharedInformationWorkspace()
        {
            if (_sharedInformationWorkspace != null)
                return;

            _sharedInformationWorkspace = gameObject.AddComponent<OwnerSharedInformationWorkspaceCoordinator>();
            _sharedInformationWorkspace.Initialize(_shell);
            _sharedInformationWorkspace.NextMatchAnalysisRequested += HandleOpponentAnalysisRequested;
        }

        private void UnsubscribeExpansionWorkspace()
        {
            if (_expansionWorkspace == null)
                return;

            _expansionWorkspace.PregamePresetSelected -= HandlePresetSelected;
            _expansionWorkspace.MatchStartRequested -= HandlePregameMatchStartRequested;
            _expansionWorkspace.SignStaffRequested -= HandleSignStaffRequested;
            _expansionWorkspace.TicketPolicyRequested -= HandleTicketPolicyRequested;
            _expansionWorkspace.FacilityUpgradeRequested -= HandleFacilityUpgradeRequested;
            _expansionWorkspace.StadiumUpgradeRequested -= HandleStadiumUpgradeRequested;
            _expansionWorkspace.WeekAdvanceRequested -= HandleWeekAdvanceRequested;
            _expansionWorkspace.SaveRequested -= HandleSaveRequested;
            _expansionWorkspace.LoadRequested -= HandleLoadRequested;
            _expansionWorkspace.LineupSwapRequested -= HandleLineupSwapRequested;
            _expansionWorkspace.LineupPresetSelected -= HandlePresetSelected;
            _expansionWorkspace.TeamColorSlotCycleRequested -= HandleTeamColorSlotCycleRequested;
            _expansionWorkspace.TacticSlotCycleRequested -= HandleTacticSlotCycleRequested;
            _expansionWorkspace.ShopPurchaseRequested -= HandleShopPurchaseRequested;
            _expansionWorkspace.ShopDetailsRequested -= HandleShopDetailsRequested;
            _expansionWorkspace.CardEnhancementRequested -= HandleCardEnhancementRequested;
            _expansionWorkspace.CardDuplicateSaleRequested -= HandleCardDuplicateSaleRequested;
            _expansionWorkspace.DugoutConfigurationConfirmed -= HandleDugoutConfigurationConfirmed;
            _expansionWorkspace.CardTrainingRequested -= HandleCardTrainingRequested;
            _expansionWorkspace.CardStudyRequested -= HandleCardStudyRequested;
            _expansionWorkspace.CardSkillBlockAutoPlaceRequested -= HandleCardSkillBlockAutoPlaceRequested;
            _expansionWorkspace.CardSkillBlockRemoveRequested -= HandleCardSkillBlockRemoveRequested;
        }

        private void BindExpansionSnapshots()
        {
            _expansionWorkspace.BindDugout(_snapshotFactory.CreateDugout(_manager));
            _expansionWorkspace.BindRosterLineup(_snapshotFactory.CreateRosterLineup(_manager));
            _expansionWorkspace.BindCollection(_snapshotFactory.CreateCollection(_manager));
            BindShopSnapshot();
            _expansionWorkspace.BindClubOperation(_snapshotFactory.CreateClubOperation(_manager));
            _expansionWorkspace.BindStaffOffice(_snapshotFactory.CreateStaffOffice(_manager));
            if (_manager.Runtime.ManagerMode.LiveSeason.NextPlayerGame != null)
            {
                _expansionWorkspace.BindPregame(_snapshotFactory.CreatePregame(_manager));
                _expansionWorkspace.BindConditionChemistry(
                    _snapshotFactory.CreateConditionChemistry(_manager),
                    _manager.Balance.ConditionChemistry.Presentation);
                return;
            }

            _expansionWorkspace.ClearMatchPreparation();
            if (_navigationState != null && _navigationState.IsContextOpen)
                _navigationState.Navigate(HomeRouteId);
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

            ShopPurchaseResult result = _manager.PurchaseShopProduct(productId);
            BindShopSnapshot();
            _expansionWorkspace.SetShopFeedback(
                result.IsSuccess ? DescribePurchase(result) : result.FailureMessage,
                !result.IsSuccess);
            if (result.IsSuccess)
                _expansionWorkspace.ShowShopReveal(result);
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

        private void HandleCardDuplicateSaleRequested(string cardId)
        {
            ExecuteOperation(() =>
            {
                int earnedSp = _manager.SellOwnedCardDuplicates(cardId);
                ShowFeedback($"중복 카드 1장을 판매해 SP {earnedSp:N0}을 획득했습니다.", false);
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
                if (!string.Equals(_pendingTrainingCardId, cardId, StringComparison.Ordinal) ||
                    !string.Equals(_pendingTrainingProgramId, programId, StringComparison.Ordinal))
                {
                    _pendingTrainingCardId = cardId;
                    _pendingTrainingProgramId = programId;
                    ShowFeedback(
                        $"{preview.Ability} {preview.Current}→{preview.Current + preview.GainedPoints} / 상한 {preview.Ceiling} · DP {preview.DpCost}. 같은 실행 버튼을 다시 누르면 확정합니다.",
                        false);
                    return;
                }
                _pendingTrainingCardId = string.Empty;
                _pendingTrainingProgramId = string.Empty;
                CardTrainingResult result = _manager.TrainOwnedCard(cardId, programId);
                ShowFeedback($"{result.Ability} +{result.GainedPoints} · DP {result.SpentDp} 사용", false);
            });
        }

        private void HandleCardStudyRequested(string cardId, string programId)
        {
            ExecuteOperation(() =>
            {
                _manager.StartOwnedCardStudy(cardId, programId);
                ShowFeedback("유학을 시작했습니다. 4주 뒤 성장 결과가 확정됩니다.", false);
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
            if (_shopService == null || !_shopService.TryGetQuote(productId, out ShopPurchaseQuote quote))
                return;

            _expansionWorkspace.SetShopFeedback(DescribeQuote(quote), false);
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

        private void BindSharedInformationSnapshots()
        {
            ScheduleScreenSnapshot schedule = _sharedInformationSnapshotFactory.CreateSchedule(_manager);
            _sharedInformationWorkspace.BindSchedule(schedule, _profile.Capabilities);
            _sharedInformationWorkspace.BindHistoricalRecords(
                _sharedInformationSnapshotFactory.CreateHistoricalBattingRecords(_manager),
                _profile.Capabilities);
            _sharedInformationWorkspace.BindSeasonRecords(
                _sharedInformationSnapshotFactory.CreateSeasonRecords(_manager));
            _sharedInformationWorkspace.BindClubInformation(new OwnerClubInformationPresentationModel(
                _snapshotFactory.CreateHome(_manager),
                _snapshotFactory.CreateCollection(_manager),
                _snapshotFactory.CreateClubOperation(_manager),
                schedule,
                _manager.Runtime.OwnerProfile.Nickname,
                _manager.Runtime.OwnerProfile.FrontManagerId));
        }

        private void HandleLineupSwapRequested(
            OwnerLineupSwapGroup group,
            int firstIndex,
            int secondIndex)
        {
            ExecuteOperation(() =>
            {
                LineupPresetState current =
                    _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                _manager.UpsertLineupPreset(
                    OwnerLineupPresetCommandBuilder.Swap(current, group, firstIndex, secondIndex));
            });
        }

        private void HandleTeamColorSlotCycleRequested(int slotIndex)
        {
            ExecuteOperation(() =>
            {
                IReadOnlyList<TeamColorDefinition> candidates = _manager.GetAvailableTeamColors();
                var ids = new string[candidates.Count];
                for (int index = 0; index < ids.Length; index++) ids[index] = candidates[index].TeamColorId;
                LineupPresetState current = _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                _manager.UpsertLineupPreset(OwnerLineupPresetCommandBuilder.CycleTeamColor(current, slotIndex, ids));
            });
        }

        private void HandleTacticSlotCycleRequested(int slotIndex)
        {
            ExecuteOperation(() =>
            {
                IReadOnlyList<TacticCardDefinition> candidates = _manager.GetAvailableTacticCards();
                var ids = new string[candidates.Count];
                for (int index = 0; index < ids.Length; index++) ids[index] = candidates[index].CardId;
                LineupPresetState current = _manager.Runtime.ManagerMode.GetSelectedLineupPreset();
                _manager.UpsertLineupPreset(OwnerLineupPresetCommandBuilder.CycleTactic(current, slotIndex, ids));
            });
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

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Presentation.UI;
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
        public const string ShopRouteId = "Owner.Shop";

        private SharedUI.SharedGameShellView _shell;
        private UI_Scene_OwnerPregame _pregameView;
        private UI_Scene_OwnerStaffOffice _staffView;
        private UI_Scene_OwnerClubOperations _clubView;
        private UI_Scene_OwnerPlayerMarket _playerMarketView;
        private UI_Scene_OwnerConditionChemistry _conditionView;
        private UI_Scene_OwnerRosterLineup _rosterLineupView;
        private UI_Scene_OwnerRosterLineup _rosterPitchingView;
        private UI_Scene_OwnerCollection _collectionView;
        private UI_Scene_OwnerPowerUp _powerUpView;
        private UI_Scene_OwnerGrowth _growthView;
        public event Action<string, int, int, int, int> CardSkillPlacementRequested;
        public event Action<string, int> CardSkillRemovalRequested;
        public event Action GrowthShopRequested;
        private UI_Scene_OwnerDugout _dugoutView;
        private UI_Scene_OwnerTeamColor _teamColorView;
        private UI_Scene_OwnerTactics _tacticsView;
        private UI_Scene_OwnerSupportCards _supportCardsView;
        private UI_Scene_OwnerManagerPolicy _managerPolicyView;
        private Shop.UI_Scene_Shop _shopView;
        private RectTransform _lockedWorkspaceRoot;
        private OwnerPregamePresentationModel _pregameModel;
        private OwnerStaffOfficePresentationModel _staffModel;
        private OwnerContractSnapshot _contractSnapshot;
        private OwnerTradeSnapshot _tradeSnapshot;
        private OwnerRosterLineupPresentationModel _rosterLineupModel;
        private Func<string, Sprite> _staffPortraitResolver;
        private UI_Scene_OwnerTeamLineup _opponentLineupView;

        public event Action MatchStartRequested;
        public event Action<string> StaffOfferSelected;
        public event Action<string> SignStaffRequested;
        public event Action<string, int> ContractPreviewRequested;
        public event Action<string, int> ContractRenewalRequested;
        public event Action<string, string, string> TradePreviewRequested;
        public event Action<string, string, string> TradeRequested;
        public event Action<TicketPriceTier> TicketPolicyRequested;
        public event Action<FacilityType> FacilityUpgradeRequested;
        public event Action StadiumUpgradeRequested;
        public event Action WeekAdvanceRequested;
        public event Action SaveRequested;
        public event Action LoadRequested;
        public event Action<string> ConditionPlayerSelected;
        public event Action<OwnerLineupSwapGroup, int, int> LineupSwapRequested;
        public event Action<OwnerLineupSwapGroup, int, string> LineupAssignmentRequested;
        public event Action<string> LineupPresetSelected;
        public event Action<int> TeamColorSlotCycleRequested;
        public event Action<int> TacticSlotCycleRequested;
        public event Action LineupChangeConfirmed;
        public event Action LineupChangeCancelled;
        public event Action ConditionLineupRequested;
        public event Action<string> ShopPurchasePreviewRequested;
        public event Action<string> ShopPurchaseRequested;
        public event Action<string> ShopDetailsRequested;
        public event Action<string> ShopRepurchaseRequested;
        public event Action<string> ShopInventoryRequested;
        public event Action<string> CardEnhancementRequested;
        public event Action<string, int> CardDuplicateSaleRequested;
        public event Action<OwnerDugoutConfigurationCommand> DugoutConfigurationConfirmed;
        public event Action<IReadOnlyList<string>> TeamColorSelectionConfirmed;
        public event Action<int, IReadOnlyList<string>> TacticSelectionConfirmed;
        public event Action<string, string> CardTrainingRequested;
        public event Action<string, string> CardStudyRequested;
        public event Action<string> CardSkillBlockAutoPlaceRequested;
        public event Action<string> CardSkillBlockRemoveRequested;

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

        /// <summary>다음 상대의 공개 라인업을 공용 카드 보드에 연결한다.</summary>
        public void BindOpponentLineup(OwnerTeamLineupSnapshot snapshot)
        {
            RequireInitialized();
            if (_opponentLineupView == null)
            {
                _opponentLineupView = UI_Scene_OwnerTeamLineup.CreateRuntime(_shell.MainWorkspaceHost);
                _opponentLineupView.SetVisible(false);
            }
            _opponentLineupView.Bind(snapshot);
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

        public void BindPlayerContracts(OwnerContractSnapshot snapshot)
        {
            RequireInitialized();
            _contractSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            EnsurePlayerMarketView();
            if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.ClubContract, StringComparison.Ordinal))
                _playerMarketView.BindContract(_contractSnapshot);
        }

        public void BindPlayerTrade(OwnerTradeSnapshot snapshot)
        {
            RequireInitialized();
            _tradeSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            EnsurePlayerMarketView();
            if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.ClubTrade, StringComparison.Ordinal))
                _playerMarketView.BindTrade(_tradeSnapshot);
        }

        public void BindConditionChemistry(
            System.Collections.Generic.IReadOnlyList<OwnerConditionPlayerSnapshot> players,
            ConditionPresentationTable presentation)
        {
            RequireInitialized();
            EnsureConditionView();
            _conditionView.Bind(OwnerConditionChemistryPresentationBuilder.Build(players, presentation));
        }

        public void BindRosterLineup(OwnerRosterLineupSnapshot snapshot)
        {
            RequireInitialized();
            _rosterLineupModel = OwnerRosterLineupPresentationBuilder.Build(snapshot);
            EnsureRosterLineupView();
            // 두 Route 모두 같은 Snapshot을 쓰므로 입력 전에 만들어 첫 투수진 진입의 UI 생성 hitch를 없앤다.
            bool wasPitchingViewReady = _rosterPitchingView != null;
            EnsureRosterPitchingView();
            _rosterLineupView.Bind(_rosterLineupModel);
            if (wasPitchingViewReady)
                _rosterPitchingView.Bind(_rosterLineupModel);
        }

        /// <summary>현재 Runtime은 유지한 채 검증된 후보 프리셋을 두 선수단 Route에 표시한다.</summary>
        public void BindRosterLineupPreview(OwnerRosterLineupSnapshot preview, string message)
        {
            RequireInitialized();
            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(preview);
            EnsureRosterLineupView();
            EnsureRosterPitchingView();
            _rosterLineupView.BindPreview(model, message);
            _rosterPitchingView.BindPreview(model, message);
        }

        public void BindCollection(OwnerCollectionSnapshot snapshot)
        {
            RequireInitialized();
            EnsureCollectionView();
            _collectionView.Bind(snapshot);
        }

        public void BindShop(Shop.ShopScreenSnapshot snapshot)
        {
            RequireInitialized();
            EnsureShopView();
            _shopView.Bind(snapshot);
        }

        /// <summary>새 성장 메뉴에 현재 저장 데이터의 조회 결과를 연결한다.</summary>
        public void BindGrowth(OwnerGrowthSnapshot snapshot)
        {
            RequireInitialized();
            if (_growthView == null)
            {
                _growthView = UI_Scene_OwnerGrowth.CreateRuntime(_shell.MainWorkspaceHost);
                _growthView.StudyRequested += HandleCardStudyRequested;
                _growthView.SkillPlacementRequested += HandleSkillPlacementRequested;
                _growthView.SkillRemovalRequested += HandleSkillRemovalRequested;
                _growthView.ShopRequested += HandleGrowthShopRequested;
                _growthView.SetVisible(false);
            }
            _growthView.Bind(snapshot);
        }

        private void HandleSkillPlacementRequested(string cardId, int instanceId, int x, int y, int rotation) =>
            CardSkillPlacementRequested?.Invoke(cardId, instanceId, x, y, rotation);
        private void HandleSkillRemovalRequested(string cardId, int instanceId) => CardSkillRemovalRequested?.Invoke(cardId, instanceId);
        private void HandleGrowthShopRequested() => GrowthShopRequested?.Invoke();

        public void BindPowerUp(OwnerPowerUpSnapshot snapshot)
        {
            RequireInitialized();
            EnsurePowerUpView();
            _powerUpView.Bind(snapshot);
        }

        /// <summary>저장된 인선·방침과 합성된 경기 판단값을 덕아웃에 연결한다.</summary>
        public void BindDugout(OwnerDugoutSnapshot snapshot)
        {
            RequireInitialized();
            EnsureDugoutView();
            _dugoutView.Bind(snapshot);
            EnsureManagerPolicyView();
            _managerPolicyView.Bind(snapshot);
        }

        /// <summary>전체 발동 단계와 현재 두 슬롯을 TeamColor 전용 화면에 연결한다.</summary>
        public void BindTeamColor(OwnerTeamColorSnapshot snapshot)
        {
            RequireInitialized();
            EnsureTeamColorView();
            _teamColorView.Bind(snapshot);
        }

        /// <summary>보유 카드 조건·효과와 현재 두 슬롯을 작전 전용 화면에 연결한다.</summary>
        public void BindTactics(OwnerTacticsSnapshot snapshot)
        {
            RequireInitialized();
            EnsureTacticsView();
            _tacticsView.Bind(snapshot);
        }

        /// <summary>다음 경기 Snapshot이 없을 때 이전 경기 준비 화면을 다시 열지 않도록 폐기한다.</summary>
        public void ClearMatchPreparation()
        {
            _pregameModel = null;
            if (_opponentLineupView != null) _opponentLineupView.SetVisible(false);
            if (_pregameView != null) _pregameView.SetVisible(false);
            if (string.Equals(ActiveRouteId, PregameRouteId, StringComparison.Ordinal) ||
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
            if ((ActiveRouteId == OwnerNavigationRoutes.PowerUpSkills || ActiveRouteId == OwnerNavigationRoutes.PowerUpStudy) && _growthView != null)
            {
                _growthView.SetFeedback(message, isError);
                return true;
            }
            if (ActiveRouteId.StartsWith("Owner.PowerUp.", StringComparison.Ordinal) && _powerUpView != null)
            {
                _powerUpView.SetFeedback(message, isError);
                return true;
            }
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
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.RosterPitching, StringComparison.Ordinal) ||
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
            if ((string.Equals(ActiveRouteId, OwnerNavigationRoutes.ClubContract, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.ClubTrade, StringComparison.Ordinal)) &&
                _playerMarketView != null)
            {
                _playerMarketView.SetFeedback(message, isError);
                return true;
            }
            if ((string.Equals(ActiveRouteId, CollectionRouteId, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal)) &&
                _collectionView != null)
            {
                _collectionView.SetFeedback(message, isError);
                return true;
            }
            if ((string.Equals(ActiveRouteId, OwnerNavigationRoutes.DugoutLineupNotes, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.Dugout, StringComparison.Ordinal)) &&
                _dugoutView != null)
            {
                _dugoutView.SetFeedback(message, isError);
                return true;
            }
            if ((string.Equals(ActiveRouteId, OwnerNavigationRoutes.RosterTeamColor, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.DugoutTeamColor, StringComparison.Ordinal)) &&
                _teamColorView != null)
            {
                _teamColorView.SetFeedback(message, isError);
                return true;
            }
            if ((string.Equals(ActiveRouteId, OwnerNavigationRoutes.RosterTacticCards, StringComparison.Ordinal) ||
                 string.Equals(ActiveRouteId, OwnerNavigationRoutes.DugoutTactics, StringComparison.Ordinal)) &&
                _tacticsView != null)
            {
                _tacticsView.SetFeedback(message, isError);
                return true;
            }
            if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.DugoutManagerPolicy, StringComparison.Ordinal) &&
                _managerPolicyView != null)
            {
                _managerPolicyView.SetFeedback(message, isError);
                return true;
            }
            return false;
        }

        /// <summary>상점 구매 결과를 상점 화면 안내 줄에 표시한다.</summary>
        public void SetShopFeedback(string message, bool isError)
        {
            if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal) &&
                _powerUpView != null)
                _powerUpView.SetFeedback(message, isError);
            else if (_shopView != null)
                _shopView.SetFeedback(message, isError);
        }

        public void ShowShopReveal(ShopPurchaseResult result, Shop.ShopProductDetailsSnapshot details,
            SharedUI.PlayerMiniCardModel[] playerCards = null)
        {
            if (string.Equals(ActiveRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal) &&
                _powerUpView != null)
                _powerUpView.ShowScoutReveal(result);
            else if (_shopView != null)
            {
                _shopView.ShowReveal(result, details, playerCards);
            }
        }

        public void ShowShopDetails(Shop.ShopProductDetailsSnapshot details)
        {
            _shopView?.ShowDetails(details);
        }

        public void ShowShopPurchaseConfirmation(Shop.ShopProductDetailsSnapshot details)
        {
            _shopView?.ShowPurchaseConfirmation(details);
        }

        public void SetShopProcessing(bool isProcessing)
        {
            _shopView?.SetProcessing(isProcessing);
        }

        public void DismissShopPurchaseConfirmation()
        {
            _shopView?.DismissPurchaseConfirmation();
        }

        public bool TryCloseShopOverlay()
        {
            return _shopView != null &&
                string.Equals(ActiveRouteId, ShopRouteId, StringComparison.Ordinal) &&
                _shopView.TryCloseOverlay();
        }

        /// <summary>현재 보이는 Workspace의 Popup 또는 저장 전 편집을 Route 이동보다 먼저 취소한다.</summary>
        public bool TryHandleCancel()
        {
            if (TryCloseShopOverlay())
                return true;

            IUiCancelHandler[] handlers =
            {
                _rosterLineupView,
                _rosterPitchingView,
                _clubView,
                _playerMarketView,
                _collectionView,
                _powerUpView,
                _growthView,
                _dugoutView,
                _teamColorView,
                _tacticsView,
                _managerPolicyView
            };
            for (int index = 0; index < handlers.Length; index++)
            {
                if (handlers[index] is MonoBehaviour view &&
                    view.gameObject.activeInHierarchy &&
                    handlers[index].TryHandleCancel())
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
            if ((workspaceRouteId == OwnerNavigationRoutes.PowerUpSkills || workspaceRouteId == OwnerNavigationRoutes.PowerUpStudy) && _growthView != null)
            {
                SetAllViewsVisible(false);
                _growthView.SetVisible(true);
                _growthView.ShowRoute(workspaceRouteId);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(navigationRouteId,
                    workspaceRouteId == OwnerNavigationRoutes.PowerUpStudy ? "유학" : "스킬 블록 배치",
                    "선수별 성장 효과와 적용 조건을 확인합니다.", "전력보강"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if ((string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUpEnhancementSale, StringComparison.Ordinal)) &&
                _powerUpView != null)
            {
                SetAllViewsVisible(false);
                _powerUpView.SetVisible(true);
                _powerUpView.ShowRoute(workspaceRouteId);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                string title = string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUpScout, StringComparison.Ordinal)
                    ? "스카우트"
                    : string.Equals(workspaceRouteId, OwnerNavigationRoutes.PowerUpTraining, StringComparison.Ordinal)
                        ? "카드훈련"
                        : "카드 합성";
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    title,
                    "카드와 재료, 비용, 적용 결과를 한 화면에서 확인합니다.",
                    "전력보강"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.DugoutLineupNotes, StringComparison.Ordinal))
            {
                EnsureDugoutView();
                SetAllViewsVisible(false);
                _dugoutView.SetVisible(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId, "덕아웃", "작전 방침과 감독·코치 카드를 확인합니다.", "구단주 모드"));
                _shell.SetContextHeaderVisible(false);
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if ((string.Equals(workspaceRouteId, OwnerNavigationRoutes.RosterTeamColor, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.DugoutTeamColor, StringComparison.Ordinal)) &&
                _teamColorView != null)
            {
                SetAllViewsVisible(false);
                _teamColorView.SetVisible(true);
                BindRosterContext(navigationRouteId, "팀 컬러", "발동 인원과 실제 적용 선수를 확인하고 두 효과를 장착합니다.");
                return true;
            }
            if ((string.Equals(workspaceRouteId, OwnerNavigationRoutes.RosterTacticCards, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.DugoutTactics, StringComparison.Ordinal)) &&
                _tacticsView != null)
            {
                SetAllViewsVisible(false);
                _tacticsView.SetVisible(true);
                if (string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal))
                {
                    _shell.SetInspectorVisible(false);
                    _shell.SetActionBarVisible(false);
                    _shell.BindContext(new SharedUI.ShellContextModel(
                        navigationRouteId,
                        "작전 카드",
                        "발동 조건·대상·지속시간을 비교하고 앞으로 열릴 경기에 작전을 배치합니다.",
                        "경기 준비",
                        canGoBack: true,
                        backLabel: "돌아가기"));
                    ActiveRouteId = navigationRouteId;
                }
                else
                {
                    BindRosterContext(navigationRouteId, "작전 카드", "발동 조건·대상·지속시간을 비교하고 앞으로 열릴 경기에 작전을 배치합니다.");
                }
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.RosterSupportCards, StringComparison.Ordinal))
            {
                EnsureSupportCardsView();
                SetAllViewsVisible(false);
                _supportCardsView.SetVisible(true);
                BindRosterContext(
                    navigationRouteId,
                    "서포트 카드",
                    "레퍼런스 편성 구조를 확인합니다. 효과·저장 기능은 Runtime 도입 전까지 잠겨 있습니다.");
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.DugoutManagerPolicy, StringComparison.Ordinal) &&
                _managerPolicyView != null)
            {
                SetAllViewsVisible(false);
                _managerPolicyView.SetVisible(true);
                BindDugoutContext(navigationRouteId, "감독방침", "감독 AI의 여섯 판단 축과 실제 경기 적용값을 조정합니다.");
                return true;
            }
            if (string.Equals(workspaceRouteId, ShopRouteId, StringComparison.Ordinal) && _shopView != null)
            {
                SetAllViewsVisible(false);
                _shopView.SetVisible(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "상점",
                    "선수 카드·스킬 블록·작전 카드를 구매합니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
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
            if (string.Equals(workspaceRouteId, RosterLineupRouteId, StringComparison.Ordinal) &&
                _rosterLineupModel != null)
            {
                string title = string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterTactics, StringComparison.Ordinal)
                        ? "전술카드"
                        : string.Equals(navigationRouteId, OwnerNavigationRoutes.MatchCenterLineup, StringComparison.Ordinal)
                            ? "우리 라인업"
                            : "선수 오더";
                return ShowRosterLineup(
                    navigationRouteId,
                    title,
                    navigationRouteId.StartsWith("Owner.MatchCenter.", StringComparison.Ordinal));
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.RosterPitching, StringComparison.Ordinal) &&
                _rosterLineupModel != null)
            {
                EnsureRosterPitchingView();
                SetAllViewsVisible(false);
                _rosterPitchingView.SetVisible(true);
                _rosterPitchingView.SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "투수진",
                "선발진과 불펜 역할, 컨디션·최근 3일 투구 부하·구종을 함께 비교합니다.",
                    "구단주 모드"));
                ActiveRouteId = navigationRouteId;
                return true;
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
                    "상대 분석 근거와 양 구단의 선발 구성을 확인합니다.",
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
                    "구단"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if ((string.Equals(workspaceRouteId, OwnerNavigationRoutes.ClubContract, StringComparison.Ordinal) ||
                 string.Equals(workspaceRouteId, OwnerNavigationRoutes.ClubTrade, StringComparison.Ordinal)) &&
                _playerMarketView != null)
            {
                SetAllViewsVisible(false);
                _playerMarketView.SetVisible(true);
                _shell.SetInspectorVisible(true);
                _shell.SetActionBarVisible(true);
                bool isContract = string.Equals(workspaceRouteId, OwnerNavigationRoutes.ClubContract, StringComparison.Ordinal);
                if (isContract && _contractSnapshot != null) _playerMarketView.BindContract(_contractSnapshot);
                if (!isContract && _tradeSnapshot != null) _playerMarketView.BindTrade(_tradeSnapshot);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    isContract ? "선수 계약" : "트레이드",
                    isContract
                        ? "잔여 계약과 연봉 부담을 비교하고 갱신안을 확정합니다."
                        : "두 구단의 가치와 25인 규칙을 비교한 뒤 1:1 제안을 확정합니다.",
                    "구단"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (OwnerManagementRoutes.IsClubOperation(workspaceRouteId) && _clubView != null)
            {
                SetAllViewsVisible(false);
                _clubView.SetVisible(true);
                _clubView.ShowRoute(workspaceRouteId);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    string.Equals(workspaceRouteId, OwnerManagementRoutes.ClubFinance, StringComparison.Ordinal)
                        ? "구단 재정"
                        : "구장·시설",
                    "관중과 수익을 확인하고 다음 운영 투자의 기회비용을 비교합니다.",
                    "구단"));
                ActiveRouteId = navigationRouteId;
                return true;
            }
            if (string.Equals(workspaceRouteId, OwnerNavigationRoutes.MatchCenterOpponentLineup, StringComparison.Ordinal) &&
                _opponentLineupView != null && _pregameModel != null)
            {
                SetAllViewsVisible(false);
                _opponentLineupView.SetVisible(true);
                _shell.SetInspectorVisible(false);
                _shell.SetActionBarVisible(false);
                _shell.BindContext(new SharedUI.ShellContextModel(
                    navigationRouteId,
                    "상대 라인업",
                    "상대 구단의 야수·벤치와 투수진을 확인합니다.",
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
            _rosterLineupView.SetWorkspaceMode(OwnerRosterWorkspaceMode.Lineup);
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new SharedUI.ShellContextModel(
                routeId,
                title,
                    "25인 1군과 역할 배치, 라인업 노트와 전력 설정의 검증 결과를 확인합니다.",
                canGoBack ? "경기 준비" : "구단주 모드",
                canGoBack,
                "돌아가기"));
            ActiveRouteId = routeId;
            return true;
        }

        private void OnDestroy()
        {
            DestroyView(_opponentLineupView);
            if (_pregameView != null)
            {
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
            if (_playerMarketView != null)
            {
                _playerMarketView.ContractPreviewRequested -= HandleContractPreviewRequested;
                _playerMarketView.ContractRenewalRequested -= HandleContractRenewalRequested;
                _playerMarketView.TradePreviewRequested -= HandleTradePreviewRequested;
                _playerMarketView.TradeRequested -= HandleTradeRequested;
                DestroyView(_playerMarketView);
            }
            if (_conditionView != null)
            {
                _conditionView.PlayerSelected -= HandleConditionPlayerSelected;
                _conditionView.LineupRequested -= HandleConditionLineupRequested;
                DestroyView(_conditionView);
            }
            if (_rosterLineupView != null)
            {
                _rosterLineupView.SwapRequested -= HandleLineupSwapRequested;
                _rosterLineupView.AssignmentRequested -= HandleLineupAssignmentRequested;
                _rosterLineupView.PresetSelected -= HandleLineupPresetSelected;
                _rosterLineupView.LineupChangeConfirmed -= HandleLineupChangeConfirmed;
                _rosterLineupView.LineupChangeCancelled -= HandleLineupChangeCancelled;
                DestroyView(_rosterLineupView);
            }
            if (_rosterPitchingView != null)
            {
                _rosterPitchingView.SwapRequested -= HandleLineupSwapRequested;
                _rosterPitchingView.AssignmentRequested -= HandleLineupAssignmentRequested;
                _rosterPitchingView.PresetSelected -= HandleLineupPresetSelected;
                _rosterPitchingView.LineupChangeConfirmed -= HandleLineupChangeConfirmed;
                _rosterPitchingView.LineupChangeCancelled -= HandleLineupChangeCancelled;
                DestroyView(_rosterPitchingView);
            }
            if (_collectionView != null)
            {
                _collectionView.EnhancementRequested -= HandleCardEnhancementRequested;
                _collectionView.DuplicateSaleRequested -= HandleCardDuplicateSaleRequested;
                _collectionView.TrainingRequested -= HandleCardTrainingRequested;
                _collectionView.StudyRequested -= HandleCardStudyRequested;
                _collectionView.SkillBlockAutoPlaceRequested -= HandleCardSkillBlockAutoPlaceRequested;
                _collectionView.SkillBlockRemoveRequested -= HandleCardSkillBlockRemoveRequested;
                DestroyView(_collectionView);
            }
            if (_powerUpView != null)
            {
                _powerUpView.ScoutPurchaseRequested -= HandleShopPurchaseRequested;
                _powerUpView.TrainingRequested -= HandleCardTrainingRequested;
                _powerUpView.EnhancementRequested -= HandleCardEnhancementRequested;
                _powerUpView.DuplicateSaleRequested -= HandleCardDuplicateSaleRequested;
                DestroyView(_powerUpView);
            }
            if (_growthView != null)
            {
                _growthView.StudyRequested -= HandleCardStudyRequested;
                _growthView.SkillPlacementRequested -= HandleSkillPlacementRequested;
                _growthView.SkillRemovalRequested -= HandleSkillRemovalRequested;
                _growthView.ShopRequested -= HandleGrowthShopRequested;
                DestroyView(_growthView);
            }
            if (_dugoutView != null)
            {
                _dugoutView.ConfigurationConfirmed -= HandleDugoutConfigurationConfirmed;
                DestroyView(_dugoutView);
            }
            if (_teamColorView != null)
            {
                _teamColorView.SelectionConfirmed -= HandleTeamColorSelectionConfirmed;
                DestroyView(_teamColorView);
            }
            if (_tacticsView != null)
            {
                _tacticsView.SelectionConfirmed -= HandleTacticSelectionConfirmed;
                DestroyView(_tacticsView);
            }
            if (_supportCardsView != null)
                DestroyView(_supportCardsView);
            if (_managerPolicyView != null)
            {
                _managerPolicyView.PolicyConfirmed -= HandleDugoutConfigurationConfirmed;
                DestroyView(_managerPolicyView);
            }
            if (_shopView != null)
            {
                _shopView.PurchasePreviewRequested -= HandleShopPurchasePreviewRequested;
                _shopView.PurchaseRequested -= HandleShopPurchaseRequested;
                _shopView.DetailsRequested -= HandleShopDetailsRequested;
                _shopView.RepurchaseRequested -= HandleShopRepurchaseRequested;
                _shopView.InventoryRequested -= HandleShopInventoryRequested;
                DestroyView(_shopView);
            }
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_lockedWorkspaceRoot);
        }

        private void EnsurePregameView()
        {
            if (_pregameView != null) return;
            _pregameView = UI_Scene_OwnerPregame.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
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

        private void EnsurePlayerMarketView()
        {
            if (_playerMarketView != null) return;
            _playerMarketView = UI_Scene_OwnerPlayerMarket.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _playerMarketView.ContractPreviewRequested += HandleContractPreviewRequested;
            _playerMarketView.ContractRenewalRequested += HandleContractRenewalRequested;
            _playerMarketView.TradePreviewRequested += HandleTradePreviewRequested;
            _playerMarketView.TradeRequested += HandleTradeRequested;
            _playerMarketView.SetVisible(false);
        }

        private void EnsureConditionView()
        {
            if (_conditionView != null) return;
            _conditionView = UI_Scene_OwnerConditionChemistry.CreateRuntime(_shell.MainWorkspaceHost);
            _conditionView.PlayerSelected += HandleConditionPlayerSelected;
            _conditionView.LineupRequested += HandleConditionLineupRequested;
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
            _rosterLineupView.AssignmentRequested += HandleLineupAssignmentRequested;
            _rosterLineupView.PresetSelected += HandleLineupPresetSelected;
            _rosterLineupView.LineupChangeConfirmed += HandleLineupChangeConfirmed;
            _rosterLineupView.LineupChangeCancelled += HandleLineupChangeCancelled;
            _rosterLineupView.SetVisible(false);
        }

        private void EnsureRosterPitchingView()
        {
            if (_rosterPitchingView != null) return;
            _rosterPitchingView = UI_Scene_OwnerRosterLineup.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _rosterPitchingView.gameObject.name = "UI_Scene_OwnerRosterPitching";
            _rosterPitchingView.SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
            _rosterPitchingView.SwapRequested += HandleLineupSwapRequested;
            _rosterPitchingView.AssignmentRequested += HandleLineupAssignmentRequested;
            _rosterPitchingView.PresetSelected += HandleLineupPresetSelected;
            _rosterPitchingView.LineupChangeConfirmed += HandleLineupChangeConfirmed;
            _rosterPitchingView.LineupChangeCancelled += HandleLineupChangeCancelled;
            if (_rosterLineupModel != null) _rosterPitchingView.Bind(_rosterLineupModel);
            _rosterPitchingView.SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
            _rosterPitchingView.SetVisible(false);
        }

        private void EnsureShopView()
        {
            if (_shopView != null) return;
            _shopView = Shop.UI_Scene_Shop.CreateRuntime(_shell.MainWorkspaceHost);
            _shopView.PurchasePreviewRequested += HandleShopPurchasePreviewRequested;
            _shopView.PurchaseRequested += HandleShopPurchaseRequested;
            _shopView.DetailsRequested += HandleShopDetailsRequested;
            _shopView.RepurchaseRequested += HandleShopRepurchaseRequested;
            _shopView.InventoryRequested += HandleShopInventoryRequested;
            _shopView.SetVisible(false);
        }

        private void EnsureCollectionView()
        {
            if (_collectionView != null) return;
            _collectionView = UI_Scene_OwnerCollection.CreateRuntime(
                _shell.MainWorkspaceHost,
                _shell.RightInspectorHost,
                _shell.ContextActionBarHost);
            _collectionView.EnhancementRequested += HandleCardEnhancementRequested;
            _collectionView.DuplicateSaleRequested += HandleCardDuplicateSaleRequested;
            _collectionView.TrainingRequested += HandleCardTrainingRequested;
            _collectionView.StudyRequested += HandleCardStudyRequested;
            _collectionView.SkillBlockAutoPlaceRequested += HandleCardSkillBlockAutoPlaceRequested;
            _collectionView.SkillBlockRemoveRequested += HandleCardSkillBlockRemoveRequested;
            _collectionView.SetVisible(false);
        }

        private void EnsurePowerUpView()
        {
            if (_powerUpView != null) return;
            _powerUpView = UI_Scene_OwnerPowerUp.CreateRuntime(_shell.MainWorkspaceHost);
            _powerUpView.ScoutPurchaseRequested += HandleShopPurchaseRequested;
            _powerUpView.TrainingRequested += HandleCardTrainingRequested;
            _powerUpView.EnhancementRequested += HandleCardEnhancementRequested;
            _powerUpView.DuplicateSaleRequested += HandleCardDuplicateSaleRequested;
            _powerUpView.SetVisible(false);
        }

        private void EnsureDugoutView()
        {
            if (_dugoutView != null) return;
            _dugoutView = UI_Scene_OwnerDugout.CreateRuntime(_shell.MainWorkspaceHost);
            _dugoutView.ConfigurationConfirmed += HandleDugoutConfigurationConfirmed;
            _dugoutView.SetVisible(false);
        }

        private void EnsureTeamColorView()
        {
            if (_teamColorView != null) return;
            _teamColorView = UI_Scene_OwnerTeamColor.CreateRuntime(_shell.MainWorkspaceHost);
            _teamColorView.SelectionConfirmed += HandleTeamColorSelectionConfirmed;
            _teamColorView.SetVisible(false);
        }

        private void EnsureTacticsView()
        {
            if (_tacticsView != null) return;
            _tacticsView = UI_Scene_OwnerTactics.CreateRuntime(_shell.MainWorkspaceHost);
            _tacticsView.SelectionConfirmed += HandleTacticSelectionConfirmed;
            _tacticsView.SetVisible(false);
        }

        private void EnsureSupportCardsView()
        {
            if (_supportCardsView != null) return;
            _supportCardsView = UI_Scene_OwnerSupportCards.CreateRuntime(_shell.MainWorkspaceHost);
            _supportCardsView.SetVisible(false);
        }

        private void EnsureManagerPolicyView()
        {
            if (_managerPolicyView != null) return;
            _managerPolicyView = UI_Scene_OwnerManagerPolicy.CreateRuntime(_shell.MainWorkspaceHost);
            _managerPolicyView.PolicyConfirmed += HandleDugoutConfigurationConfirmed;
            _managerPolicyView.SetVisible(false);
        }

        private void BindRosterContext(string routeId, string title, string description)
        {
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new SharedUI.ShellContextModel(routeId, title, description, "선수단"));
            ActiveRouteId = routeId;
        }

        private void BindDugoutContext(string routeId, string title, string description)
        {
            _shell.SetInspectorVisible(false);
            _shell.SetActionBarVisible(false);
            _shell.BindContext(new SharedUI.ShellContextModel(routeId, title, description, "덕아웃"));
            ActiveRouteId = routeId;
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
            if (_opponentLineupView != null) _opponentLineupView.SetVisible(visible);
            if (_pregameView != null) _pregameView.SetVisible(visible);
            if (_staffView != null) _staffView.SetVisible(visible);
            if (_clubView != null) _clubView.SetVisible(visible);
            if (_playerMarketView != null) _playerMarketView.SetVisible(visible);
            if (_conditionView != null) _conditionView.SetVisible(visible);
            if (_rosterLineupView != null) _rosterLineupView.SetVisible(visible);
            if (_rosterPitchingView != null) _rosterPitchingView.SetVisible(visible);
            if (_collectionView != null) _collectionView.SetVisible(visible);
            if (_powerUpView != null) _powerUpView.SetVisible(visible);
            if (_growthView != null) _growthView.SetVisible(visible);
            if (_dugoutView != null) _dugoutView.SetVisible(visible);
            if (_teamColorView != null) _teamColorView.SetVisible(visible);
            if (_tacticsView != null) _tacticsView.SetVisible(visible);
            if (_supportCardsView != null) _supportCardsView.SetVisible(visible);
            if (_managerPolicyView != null) _managerPolicyView.SetVisible(visible);
            if (_shopView != null) _shopView.SetVisible(visible);
            if (_lockedWorkspaceRoot != null) _lockedWorkspaceRoot.gameObject.SetActive(visible);
        }

        private void HandleShopPurchasePreviewRequested(string productId) =>
            ShopPurchasePreviewRequested?.Invoke(productId);
        private void HandleShopPurchaseRequested(string productId) => ShopPurchaseRequested?.Invoke(productId);
        private void HandleShopDetailsRequested(string productId) => ShopDetailsRequested?.Invoke(productId);
        private void HandleShopRepurchaseRequested(string productId) => ShopRepurchaseRequested?.Invoke(productId);
        private void HandleShopInventoryRequested(string productId) => ShopInventoryRequested?.Invoke(productId);
        private void HandleCardEnhancementRequested(string cardId) => CardEnhancementRequested?.Invoke(cardId);
        private void HandleCardDuplicateSaleRequested(string cardId) => CardDuplicateSaleRequested?.Invoke(cardId, 1);
        private void HandleCardDuplicateSaleRequested(string cardId, int count) =>
            CardDuplicateSaleRequested?.Invoke(cardId, count);
        private void HandleDugoutConfigurationConfirmed(OwnerDugoutConfigurationCommand command) =>
            DugoutConfigurationConfirmed?.Invoke(command);
        private void HandleTeamColorSelectionConfirmed(string[] ids) => TeamColorSelectionConfirmed?.Invoke(ids);
        private void HandleTacticSelectionConfirmed(int gameId, string[] ids) =>
            TacticSelectionConfirmed?.Invoke(gameId, ids);
        private void HandleCardTrainingRequested(string cardId, string programId) => CardTrainingRequested?.Invoke(cardId, programId);
        private void HandleCardStudyRequested(string cardId, string programId) => CardStudyRequested?.Invoke(cardId, programId);
        private void HandleCardSkillBlockAutoPlaceRequested(string cardId) => CardSkillBlockAutoPlaceRequested?.Invoke(cardId);
        private void HandleCardSkillBlockRemoveRequested(string cardId) => CardSkillBlockRemoveRequested?.Invoke(cardId);
        private void HandleMatchStartRequested() => MatchStartRequested?.Invoke();
        private void HandleStaffOfferSelected(string offerId) => StaffOfferSelected?.Invoke(offerId);
        private void HandleSignStaffRequested(string offerId) => SignStaffRequested?.Invoke(offerId);
        private void HandleContractPreviewRequested(string cardId, int seasons) =>
            ContractPreviewRequested?.Invoke(cardId, seasons);
        private void HandleContractRenewalRequested(string cardId, int seasons) =>
            ContractRenewalRequested?.Invoke(cardId, seasons);
        private void HandleTradePreviewRequested(string teamSeasonKey, string outgoingCardId, string incomingCardId) =>
            TradePreviewRequested?.Invoke(teamSeasonKey, outgoingCardId, incomingCardId);
        private void HandleTradeRequested(string teamSeasonKey, string outgoingCardId, string incomingCardId) =>
            TradeRequested?.Invoke(teamSeasonKey, outgoingCardId, incomingCardId);
        private void HandleTicketPolicyRequested(TicketPriceTier tier) => TicketPolicyRequested?.Invoke(tier);
        private void HandleFacilityUpgradeRequested(FacilityType type) => FacilityUpgradeRequested?.Invoke(type);
        private void HandleStadiumUpgradeRequested() => StadiumUpgradeRequested?.Invoke();
        private void HandleWeekAdvanceRequested() => WeekAdvanceRequested?.Invoke();
        private void HandleSaveRequested() => SaveRequested?.Invoke();
        private void HandleLoadRequested() => LoadRequested?.Invoke();
        private void HandleConditionPlayerSelected(string playerId) => ConditionPlayerSelected?.Invoke(playerId);
        private void HandleConditionLineupRequested() => ConditionLineupRequested?.Invoke();
        private void HandleLineupSwapRequested(OwnerLineupSwapGroup group, int firstIndex, int secondIndex) =>
            LineupSwapRequested?.Invoke(group, firstIndex, secondIndex);
        private void HandleLineupAssignmentRequested(OwnerLineupSwapGroup group, int index, string cardId) =>
            LineupAssignmentRequested?.Invoke(group, index, cardId);
        private void HandleLineupPresetSelected(string presetId) => LineupPresetSelected?.Invoke(presetId);
        private void HandleTeamColorSlotCycleRequested(int slotIndex) =>
            TeamColorSlotCycleRequested?.Invoke(slotIndex);
        private void HandleTacticSlotCycleRequested(int slotIndex) => TacticSlotCycleRequested?.Invoke(slotIndex);
        private void HandleLineupChangeConfirmed() => LineupChangeConfirmed?.Invoke();
        private void HandleLineupChangeCancelled() => LineupChangeCancelled?.Invoke();

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

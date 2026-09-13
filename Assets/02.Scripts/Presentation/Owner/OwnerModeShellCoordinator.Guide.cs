using System;
using System.Collections;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Game.Historical;
using Baseball.Presentation.Guide;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private UI_System_OwnerGuide _ownerGuide;
        private ManagerHistoricalRuntimeState _guideRuntime;
        private int _guideRequest;
        private bool _guideWasSuppressed;
        private bool _hasUnsavedGuideChange;
        private RectTransform _guideTarget;
        private GameObject _guideHighlight;
        private string _observedGuideTrackedKey = "";
        private GuideGoalKind _observedGuideTrackedKind;
        public GuideArrivalStatus LastGuideArrival { get; private set; } = GuideArrivalStatus.Cancelled;

        private bool IsGuideHidden => ActiveRouteId != HomeRouteId || IsGuideSuppressed;

        private bool IsGuideSuppressed => _manager == null || !_manager.HasActiveRuntime ||
            !UiGameModeSession.IsSelected(UiGameMode.OwnerCareer) || _isOwnerMatchVisible ||
            _isTransitioningToOwnerMatch || _isSeasonSimulationVisible ||
            (UIManager.Instance != null && UIManager.Instance.HasBlockingOverlay) ||
            (_expansionWorkspace != null && _expansionWorkspace.IsGuideSuppressed);

        private void RefreshOwnerGuide()
        {
            if (_shell == null || _manager == null || !_manager.HasActiveRuntime || IsGuideHidden) return;
            if (!ReferenceEquals(_guideRuntime, _manager.Runtime))
            {
                _guideRuntime = _manager.Runtime; _guideRequest++; _hasUnsavedGuideChange = false;
                _observedGuideTrackedKey = "";
                _ownerGuide?.SetOpen(false, false);
            }
            if (_ownerGuide == null)
            {
                EnsureHomeView();
                _ownerGuide = UI_System_OwnerGuide.Create(_homeView.ManagerHost, OwnerGuidePresentationData.Load(), _homeView.SetDashboardState);
                _ownerGuide.FeedbackHeightChanged += _homeView.SetManagerFeedbackHeight;
                _homeView.FeedbackChanged += _ownerGuide.SetFeedback;
                _ownerGuide.SetFeedback(_homeView.FeedbackMessage, _homeView.IsFeedbackError);
                _ownerGuide.ActionRequested += NavigateGuideGoal;
                _homeView.OutsideSuggestionPressed += _ownerGuide.CollapseSuggestion;
                _ownerGuide.ReadRequested += id => SaveGuideChoice(state => state.MarkNewsCaseRead(id));
                _ownerGuide.AllReadRequested += () => SaveGuideChoice(state => state.MarkAllReportsRead());
                _ownerGuide.BookmarkRequested += (id, bookmarked) => SaveGuideChoice(state => state.SetReportBookmark(id, bookmarked));
                _ownerGuide.KeepRequested += (key, keep) => SaveGuideChoice(state =>
                {
                    if (keep) state.AcceptAsIs(key); else state.Reconsider(key);
                });
                _ownerGuide.DeferRequested += id => SaveGuideChoice(state => state.SnoozeReport(id));
            }
            if (_hasUnsavedGuideChange) return;
            try
            {
                var progress = _manager.RefreshGuideProgress();
                _ownerGuide.Bind(progress, _manager.Runtime.OwnerProfile.FrontManagerId, ResolveGuidePlayerName);
                if (_observedGuideTrackedKey.Length > 0 && progress.GetStatus(_observedGuideTrackedKey) == GuideGoalStatus.Resolved &&
                    (_observedGuideTrackedKind == GuideGoalKind.RosterIssue || _observedGuideTrackedKind == GuideGoalKind.PresetIssue))
                    _ownerGuide.ShowResolution();
                _observedGuideTrackedKey = progress.TrackedKey;
                foreach (var goal in progress.GetVisibleGoals(true))
                    if (goal.Key == _observedGuideTrackedKey) _observedGuideTrackedKind = goal.Kind;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { _ownerGuide.SetFeedback(_ownerGuide.Copy.error); Debug.LogException(exception); }
        }

        private void UpdateGuideSuppression()
        {
            bool suppressed = IsGuideHidden;
            // 로드·홈 재구성 뒤에는 숨김 조건이 같아도 뷰가 없거나 비활성일 수 있다.
            // 캐시뿐 아니라 실제 뷰와 바인딩한 런타임까지 일치할 때만 갱신을 생략한다.
            if (_guideWasSuppressed == suppressed &&
                (suppressed
                    ? _ownerGuide == null || !_ownerGuide.gameObject.activeSelf
                    : _ownerGuide != null && _ownerGuide.gameObject.activeSelf &&
                      ReferenceEquals(_guideRuntime, _manager.Runtime))) return;
            _guideWasSuppressed = suppressed;
            if (suppressed)
            {
                // 홈에서 안내를 누른 뒤 대상 화면에 도착하는 처리는 계속 진행한다.
                if (IsGuideSuppressed) { _guideRequest++; ClearGuideTarget(); }
                _ownerGuide?.SetOpen(false, false);
                _shell?.SetGuideHeight(0);
                if (_ownerGuide != null) _ownerGuide.gameObject.SetActive(false);
            }
            else
            {
                RefreshOwnerGuide();
                if (_ownerGuide != null) _ownerGuide.gameObject.SetActive(true);
            }
        }

        private bool SaveGuideChoice(Action<GuideProgressState> change)
        {
            try { _manager.ChangeGuideProgress(change); RefreshOwnerGuide(); return true; }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is InvalidOperationException)
            { _ownerGuide?.SetFeedback(_ownerGuide.Copy.saveError); return false; }
        }

        private static string ResolveGuideRoute(GuideCtaAction action) => action switch
        {
            GuideCtaAction.OpenRoster or GuideCtaAction.OpenLineup or GuideCtaAction.OpenTodayLineup or
                GuideCtaAction.OpenPitchingRole or GuideCtaAction.OpenPitchingStaff or GuideCtaAction.OpenBullpen => OwnerNavigationRoutes.RosterLineup,
            GuideCtaAction.OpenScout or GuideCtaAction.OpenFocusScout => OwnerNavigationRoutes.PowerUpScout,
            GuideCtaAction.OpenTactics => OwnerNavigationRoutes.RosterTacticCards,
            GuideCtaAction.OpenTeamColor or GuideCtaAction.OpenTeamColorCoverage => OwnerNavigationRoutes.RosterTeamColor,
            GuideCtaAction.OpenOpponentAnalysis => OwnerNavigationRoutes.MatchCenterAnalysis,
            GuideCtaAction.StartMatch => OwnerNavigationRoutes.MatchCenterLineup,
            _ => null
        };

        private bool CanRouteGuideAction(GuideCtaAction action) => CanNavigateGuideRoute(ResolveGuideRoute(action));

        private bool CanNavigateGuideRoute(string route)
        {
            if (string.IsNullOrEmpty(route) || IsGuideSuppressed || _profile == null) return false;
            var entry = _profile.FindEntry(route);
            return entry != null && entry.IsEnabled && entry.IsVisible(_profile.Capabilities) &&
                (!_profile.IsContextRoute(route) || _manager.Runtime.ManagerMode.LiveSeason.NextPlayerGame != null);
        }

        private bool NavigateGuideRoute(string route)
        {
            if (!CanNavigateGuideRoute(route)) { LastGuideArrival = GuideArrivalStatus.Unsupported; return false; }
            if (_pendingLineupPreset != null || _expansionWorkspace.HasGuideBlockingPreview ||
                _expansionWorkspace.HasGuideBlockingEditor)
            { _ownerGuide?.SetFeedback(_ownerGuide.Copy.blockedEdit); LastGuideArrival = GuideArrivalStatus.Cancelled; return false; }
            ClearGuideTarget();
            LastGuideArrival = GuideArrivalStatus.Requested;
            if (_profile.IsContextRoute(route)) OpenMatchCenter(route); else HandleNavigationRequested(route);
            if (ActiveRouteId != _profile.ResolveRouteId(route)) { LastGuideArrival = GuideArrivalStatus.TargetMissing; return false; }
            LastGuideArrival = GuideArrivalStatus.RouteReady;
            return true;
        }

        private void NavigateGuideGoal(GuideGoal goal)
        {
            if (goal == null) return;
            RefreshOwnerGuide();
            if (goal.Kind == GuideGoalKind.News)
            {
                foreach (var report in _manager.Runtime.GuideProgress.GetReports())
                {
                    if (report.news == null || report.isExpired || report.deduplicationKey != goal.Key) continue;
                    if (report.target == GuideTargetKind.PlayerCard) HandleShopPlayerCardDetailsRequested(report.cardId);
                    else NavigateGuideRoute(OwnerNavigationRoutes.LeagueTeamResults);
                    return;
                }
                _ownerGuide.SetFeedback(_ownerGuide.Copy.missing);
                return;
            }
            bool exists = _manager.Runtime.GuideProgress.IsCurrentTarget(goal);
            if (!exists) { _ownerGuide.SetFeedback(_ownerGuide.Copy.missing); return; }
            string route = goal.Target switch
            {
                GuideTargetKind.Analysis => OwnerNavigationRoutes.MatchCenterAnalysis,
                GuideTargetKind.PlanConfirmation => OwnerNavigationRoutes.MatchCenterAnalysis,
                GuideTargetKind.TeamColor => OwnerNavigationRoutes.RosterTeamColor,
                GuideTargetKind.Tactic => OwnerNavigationRoutes.RosterTacticCards,
                _ => OwnerNavigationRoutes.RosterLineup
            };
            if (!NavigateGuideRoute(route)) { _ownerGuide.SetFeedback(_ownerGuide.Copy.locked); return; }
            if (!SaveGuideChoice(state => state.Track(goal.Key))) return;
            int request = ++_guideRequest;
            _ownerGuide.SetOpen(false, false);
            StartCoroutine(CompleteGuideArrival(goal, request, ActiveRouteId, _manager.Runtime));
        }

        private IEnumerator CompleteGuideArrival(GuideGoal goal, int request, string route, ManagerHistoricalRuntimeState runtime)
        {
            // 화면 바인딩·레이아웃 뒤 실제 대상이 살아 있는지 확인한다. 좌표 fallback은 만들지 않는다.
            yield return null;
            if (request != _guideRequest || route != ActiveRouteId || !ReferenceEquals(runtime, _manager.Runtime) || IsGuideSuppressed)
            { LastGuideArrival = GuideArrivalStatus.Cancelled; yield break; }
            if (!_expansionWorkspace.TrySelectGuideTarget(goal, out RectTransform target) || target == null || !target.gameObject.activeInHierarchy)
            { LastGuideArrival = GuideArrivalStatus.TargetMissing; if (!IsGuideHidden) _ownerGuide.SetOpen(true); _ownerGuide.SetFeedback(_ownerGuide.Copy.missing); yield break; }
            Canvas.ForceUpdateCanvases();
            _guideTarget = target;
            ShowGuideTarget(target);
            var selectable = target.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable()) selectable.Select();
            else EventSystem.current?.SetSelectedGameObject(target.gameObject);
            LastGuideArrival = GuideArrivalStatus.TargetReady;
            SaveGuideChoice(state =>
            {
                state.RecordArrival(goal.Key, LastGuideArrival);
                state.MarkReportRead(state.FindReportId(goal.Key));
            });
        }

        private void ShowGuideTarget(RectTransform target)
        {
            ClearGuideTarget();
            _guideTarget = target;
            _guideHighlight = new GameObject("GuideTargetFocus", typeof(RectTransform));
            _guideHighlight.transform.SetParent(target, false);
            var root = (RectTransform)_guideHighlight.transform;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            // 대상 내부의 가는 테두리만 쓰고 클릭·드래그를 가로채지 않는다.
            for (int edge = 0; edge < 4; edge++)
            {
                var line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(root, false);
                var rect = (RectTransform)line.transform;
                bool horizontal = edge < 2;
                rect.anchorMin = horizontal ? new Vector2(0, edge) : new Vector2(edge - 2, 0);
                rect.anchorMax = horizontal ? new Vector2(1, edge) : new Vector2(edge - 2, 1);
                rect.pivot = horizontal ? new Vector2(.5f, edge) : new Vector2(edge - 2, .5f);
                rect.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
                rect.anchoredPosition = Vector2.zero;
                var image = line.GetComponent<Image>(); image.color = CareerUiTheme.ReferenceAccent; image.raycastTarget = false;
            }
        }

        private string ResolveGuidePlayerName(string cardId)
        {
            var runtime = _manager.Runtime;
            if (string.IsNullOrEmpty(cardId) || !runtime.WorldCardCatalog.TryGetCard(cardId, out var card)) return null;
            return runtime.IdentityRegistry.GetPresentationPlayerName(runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerPersonId);
        }

        private void ClearGuideTarget()
        {
            _guideTarget = null;
            if (_guideHighlight != null)
            {
                _guideHighlight.SetActive(false);
                if (Application.isPlaying) Destroy(_guideHighlight); else DestroyImmediate(_guideHighlight);
                _guideHighlight = null;
            }
        }

        private void DestroyOwnerGuide()
        {
            _guideRequest++;
            ClearGuideTarget();
            if (_ownerGuide == null) return;
            if (_homeView != null) _homeView.OutsideSuggestionPressed -= _ownerGuide.CollapseSuggestion;
            if (_shell != null) _shell.SetGuideHeight(0);
            if (Application.isPlaying) Destroy(_ownerGuide.gameObject); else DestroyImmediate(_ownerGuide.gameObject);
            _ownerGuide = null;
        }
    }
}

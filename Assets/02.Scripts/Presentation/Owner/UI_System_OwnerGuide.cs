using System;
using System.Collections.Generic;
using System.Collections;
using Baseball.Core.Historical;
using Baseball.Game.Guide;
using Baseball.Presentation.Guide;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>홈 내부에서 접힌 추천·펼친 추천·매니저 리포트를 전환한다.</summary>
    public sealed partial class UI_System_OwnerGuide : MonoBehaviour, IPointerClickHandler
    {
        private OwnerGuidePresentationData _copy;
        private RectTransform _content, _suggestion, _reportsRoot;
        private RectTransform _feedbackRoot;
        private Text _feedbackMessage;
        public event Action<float> FeedbackHeightChanged;
        private Button _dock, _action, _snooze, _next, _review, _close, _news;
        private Text _body, _counter;
        private Image _portrait;
        private RectTransform _card;
        private Image _unreadBadge;
        private RectTransform _portraitViewport;
        private GuideGoal _goal;
        private GuideProgressState _progress;
        private readonly FrontManagerGuideCtaRouter _router = new();
        private GuideMessage _legacyMessage;
        private IReadOnlyList<GuideGoal> _goals = Array.Empty<GuideGoal>();
        private GameObject _returnFocus;
        private int _state;
        private string _scope;
        private Action<int> _layoutChanged;
        private CanvasGroup _transitionGroup;
        private Coroutine _transition;
        public bool IsOpen => _state != 0;
        public bool ShowsSnoozed => _state == 2 && _filter == 2;
        public OwnerGuidePresentationData Copy => _copy;
        public event Action<GuideGoal> ActionRequested;
        public event Action<string> ReadRequested;
        public event Action<string, bool> BookmarkRequested;
        public event Action TipsRequested;

        /// <summary>호환 호출도 Workspace 내부에만 생성한다.</summary>
        public static UI_System_OwnerGuide Create(SharedGameShellView shell, OwnerGuidePresentationData copy)
        {
            RectTransform host = OwnerWorkspaceUiFactory.CreateRoot(shell.MainWorkspaceHost, "ManagerHost", false);
            SetRect(host, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-744, 0), new Vector2(0, 352));
            return Create(host, copy, null);
        }

        /// <summary>홈이 소유한 카드 슬롯과 상태별 재배치 계약을 사용한다.</summary>
        public static UI_System_OwnerGuide Create(RectTransform host, OwnerGuidePresentationData copy, Action<int> layoutChanged)
        {
            var view = host.gameObject.AddComponent<UI_System_OwnerGuide>();
            view._copy = copy; view._layoutChanged = layoutChanged;
            view.Build(); return view;
        }

        /// <summary>갱신해도 사용자가 펼친 추천과 리포트 선택을 안정 ID로 유지한다.</summary>
        public void Bind(GuideProgressState progress, string managerId)
        {
            _progress = progress; _legacyMessage = null;
            if (_scope != progress.Scope) { _scope = progress.Scope; _goal = null; }
            string selected = _goal?.Key;
            _goals = progress.GetSuggestionGoals();
            _goal = null;
            foreach (var goal in _goals) if (goal.Key == selected) _goal = goal;
            if (_state == 2 && selected != null)
                foreach (var goal in progress.GetVisibleGoals(true)) if (goal.Key == selected) _goal = goal;
            if (_goal == null && _goals.Count > 0) _goal = _goals[0];
            _portrait.sprite = FrontManagerPortraitSprites.LoadForManager(managerId, "FM_NEUTRAL");
            _portrait.gameObject.SetActive(_portrait.sprite != null);
            Render();
        }

        /// <summary>추천과 리포트를 보존하며 카드 안에서 처리 결과를 안내한다.</summary>
        public void SetFeedback(string message, bool isError = false)
        {
            bool visible = !string.IsNullOrWhiteSpace(message);
            _feedbackRoot.gameObject.SetActive(visible);
            _feedbackMessage.text = visible
                ? (isError ? "확인이 필요해요\n" : "처리 결과를 알려드려요\n") + message : string.Empty;
            // 첫 레이아웃 이전에도 카드의 고정 본문 폭으로 줄바꿈 높이를 계산한다.
            var settings = _feedbackMessage.GetGenerationSettings(new Vector2(548, 0));
            float height = visible ? Mathf.Max(112f,
                _feedbackMessage.cachedTextGeneratorForLayout.GetPreferredHeight(_feedbackMessage.text, settings)
                / _feedbackMessage.pixelsPerUnit + 24f) : 0f;
            SetRect(_feedbackRoot, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, height));
            _suggestion.offsetMin = new Vector2(0, height);
            _reportsRoot.offsetMin = new Vector2(0, height);
            FeedbackHeightChanged?.Invoke(height);
        }

        private void DismissFeedback()
        {
            SetFeedback(string.Empty);
            if (_state == 2) _reportBack.Select();
            else _review.Select();
        }

        public void ShowResolution() => _counter.text = _copy.resolved;

        /// <summary>구단 소식도 현재 카드 안에서 표시한다.</summary>
        public void BindMessage(GuideMessage message)
        {
            _legacyMessage = message; _goal = null;
            _body.text = message?.Text ?? _copy.empty;
            _action.gameObject.SetActive(message != null && _router.CanRoute(message));
            if (message?.Cta != null) _action.GetComponentInChildren<Text>().text = message.Cta.Value.Label;
            _snooze.gameObject.SetActive(false); _next.gameObject.SetActive(false);
        }

        public void SetOpen(bool open, bool restoreFocus = true) => SetState(open ? 1 : 0, restoreFocus);

        public void CollapseSuggestion() { if (_state == 1) SetState(0); }

        /// <summary>리포트 뒤로는 펼친 추천, 접기는 기본 추천으로 돌아간다.</summary>
        public bool TryGoBack()
        {
            if (_state == 0) return false;
            if (_state == 2 && _selectedReportId != null) { ReturnToReportList(); return true; }
            SetState(_state == 2 ? 1 : 0);
            return true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_state == 0) SetState(1);
        }

        private void SetState(int state, bool restoreFocus = true)
        {
            bool changed = _state != state;
            if (state != 0 && _state == 0) _returnFocus = EventSystem.current?.currentSelectedGameObject;
            _state = state;
            UIOwnerFrontOfficePanel.Apply(_card, state == 2 ? "ManagerReport" : "ManagerCard");
            _layoutChanged?.Invoke(state);
            _suggestion.gameObject.SetActive(state != 2);
            _reportsRoot.gameObject.SetActive(state == 2);
            _portrait.gameObject.SetActive(state != 2 && _portrait.sprite != null);
            _dock.gameObject.SetActive(state == 0);
            _close.gameObject.SetActive(state == 1);
            Render();
            if (changed && Application.isPlaying && isActiveAndEnabled)
            {
                Baseball.Game.Sound.SoundManager.Instance?.PlayInterfaceConfirm();
                if (_transition != null) StopCoroutine(_transition);
                _transition = StartCoroutine(FadeContent());
            }
            if (state == 2) _reportBack.Select();
            else if (state == 1 && _action.gameObject.activeInHierarchy) _action.Select();
            else if (state == 1) _review.Select();
            else if (restoreFocus)
            {
                if (_returnFocus != null && _returnFocus.activeInHierarchy)
                    EventSystem.current?.SetSelectedGameObject(_returnFocus);
                else _dock.Select();
            }
        }

        private void Build()
        {
            gameObject.AddComponent<CareerUiPreserveTextColor>();
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "ManagerSuggestionCard", _copy.title);
            _card = panel.Root;
            SetRect(panel.Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            OwnerDashboardStyle.ApplySurface(panel.Root);
            panel.Root.GetComponent<Image>().raycastTarget = true;
            panel.Root.Find("HeaderSurface").GetComponent<Image>().color = OwnerDashboardStyle.Surface;
            panel.Root.Find("HeaderAccent").GetComponent<Image>().color = OwnerDashboardStyle.Line;
            panel.Root.Find("HeaderAccent").gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            // 공용 스킨이 장식 테두리를 불투명한 면으로 해석해 본문을 덮지 않도록 한다.
            var border = panel.Root.Find("ThinBorder").GetComponent<Image>();
            (border.GetComponent<CareerUiVisualElement>() ?? border.gameObject.AddComponent<CareerUiVisualElement>())
                .Initialize(CareerUiVisualRole.FlatSurface);
            border.enabled = false;
            var title = panel.Root.Find("HeaderSlot").GetComponent<Text>();
            title.color = OwnerDashboardStyle.Ivory; title.fontSize = 24;
            title.rectTransform.offsetMin = new Vector2(24, -43);
            OwnerDashboardStyle.SetTypography(title, true);
            _content = panel.Content;
            _transitionGroup = _content.gameObject.AddComponent<CanvasGroup>();
            _counter = MakeText(panel.Root, "Count", 22);
            _counter.color = OwnerDashboardStyle.Gold;
            _counter.alignment = TextAnchor.MiddleRight;
            _unreadBadge = UIOwnerFrontOfficeSkin.CreateBadge(panel.Root, "UnreadBadge", "Unread", 10f);
            SetRect(_unreadBadge.rectTransform, new Vector2(1, 1), Vector2.one, new Vector2(-26, -29), new Vector2(-16, -19));
            SetRect(_counter.rectTransform, new Vector2(.38f, 1), Vector2.one, new Vector2(0, -43), new Vector2(-36, -4));
            _suggestion = OwnerWorkspaceUiFactory.CreateRoot(_content, "Suggestion", false);
            // 투명 초상 뒤에 단색 면을 그리지 않고 카드 안쪽 잘림 영역만 유지한다.
            _portraitViewport = new GameObject("PortraitViewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _portraitViewport.SetParent(_suggestion, false);
            _portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _portrait.transform.SetParent(_portraitViewport, false); _portrait.preserveAspect = true; _portrait.raycastTarget = false;
            _portrait.rectTransform.anchorMin = _portrait.rectTransform.anchorMax = new Vector2(.5f, 1);
            _portrait.rectTransform.pivot = new Vector2(.5f, 1);
            // 전신 원화의 얼굴이 왼쪽으로 치우치지 않도록 보정하고 머리 위 여백을 확보한다.
            _portrait.rectTransform.sizeDelta = new Vector2(160, 240);
            _portrait.rectTransform.anchoredPosition = new Vector2(8, -4);
            _body = MakeText(_suggestion, "Evidence", 22);
            _body.alignment = TextAnchor.UpperLeft;
            _action = MakeButton(_suggestion, "Navigate", _copy.action, NavigateCurrent);
            _snooze = MakeButton(_suggestion, "Snooze", _copy.snooze, () =>
            {
                string id = _progress?.FindReportId(_goal?.Key);
                if (id != null) BookmarkRequested?.Invoke(id, true);
            }, OwnerButtonRole.Quiet);
            _next = MakeButton(_suggestion, "Next", _copy.next, () =>
            {
                string id = _progress?.FindReportId(_goal?.Key);
                if (id != null) ReadRequested?.Invoke(id);
            }, OwnerButtonRole.Quiet);
            _review = MakeButton(_suggestion, "Review", _copy.review, () => SetState(2), OwnerButtonRole.Quiet);
            _dock = MakeButton(_suggestion, "ManagerDock", _copy.expand, () => SetState(1), OwnerButtonRole.Quiet);
            _close = MakeButton(_suggestion, "Close", _copy.collapse, () => SetState(0), OwnerButtonRole.Quiet);
            _news = MakeButton(_suggestion, "ClubNews", _copy.tips, () => TipsRequested?.Invoke(), OwnerButtonRole.Quiet);
            BuildReports();
            _feedbackRoot = OwnerWorkspaceUiFactory.CreateRoot(_content, "ManagerFeedback", false);
            var feedbackSurface = _feedbackRoot.gameObject.AddComponent<Image>();
            feedbackSurface.color = OwnerDashboardStyle.Surface;
            feedbackSurface.raycastTarget = false;
            _feedbackRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            _feedbackMessage = MakeText(_feedbackRoot, "ResultMessage", 22);
            _feedbackMessage.alignment = TextAnchor.MiddleLeft;
            _feedbackMessage.horizontalOverflow = HorizontalWrapMode.Wrap;
            _feedbackMessage.resizeTextForBestFit = false;
            SetRect(_feedbackMessage.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-152, -12));
            var dismiss = MakeButton(_feedbackRoot, "DismissResult", "확인", DismissFeedback, OwnerButtonRole.Quiet);
            SetRect((RectTransform)dismiss.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-144, -34), new Vector2(-8, 34));
            _feedbackRoot.gameObject.SetActive(false);
            SetState(0, false);
        }

        private void NavigateCurrent()
        {
            if (_goal != null) ActionRequested?.Invoke(_goal);
            else if (_legacyMessage != null && _router.TryRoute(_legacyMessage)) SetOpen(false, false);
        }

        private IEnumerator FadeContent()
        {
            float elapsed = 0;
            while (elapsed < .18f)
            {
                elapsed += Time.unscaledDeltaTime;
                _transitionGroup.alpha = Mathf.Lerp(.65f, 1, elapsed / .18f);
                yield return null;
            }
            _transitionGroup.alpha = 1; _transition = null;
        }

        private void OnDisable()
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            if (_transitionGroup != null) _transitionGroup.alpha = 1;
        }

        private void Render()
        {
            if (_body == null) return;
            int unread = 0;
            if (_progress != null) foreach (var report in _progress.GetReports())
                if (!report.isRead && !report.isExpired) unread++;
            _counter.text = string.Format(_copy.unread, unread);
            _unreadBadge.gameObject.SetActive(unread > 0);
            if (_state == 1 && _goal != null)
            {
                int index = 0;
                for (int i = 0; i < _goals.Count; i++) if (_goals[i].Key == _goal.Key) index = i;
                _counter.text = string.Format(_copy.count, index + 1, _goals.Count) + " · " + _counter.text;
            }
            _dock.gameObject.SetActive(_state == 0 && _goal != null);
            _body.text = FormatBody(_goal, _progress?.HomeScore ?? 0, _progress?.AwayScore ?? 0);
            _action.GetComponentInChildren<Text>().text = ActionLabel(_goal);
            _action.gameObject.SetActive(_goal != null);
            _snooze.gameObject.SetActive(_state == 1 && _goal != null);
            _next.gameObject.SetActive(_state == 1 && _goal != null);
            _news.gameObject.SetActive(_state == 1);
            // 접힌 상태에서도 초상·본문·주요 행동의 위치를 유지해 클릭 대상이 움직이지 않게 한다.
            SetRect(_portraitViewport, Vector2.zero, new Vector2(0, 1), new Vector2(8, 156), new Vector2(152, -8));
            SetRect(_body.rectTransform, Vector2.zero, Vector2.one, new Vector2(176, 156), new Vector2(-16, -8));
            SetRect((RectTransform)_action.transform, Vector2.zero, new Vector2(0, 0), new Vector2(8, 80), new Vector2(260, 148));
            SetRect((RectTransform)_snooze.transform, Vector2.zero, Vector2.zero, new Vector2(268, 80), new Vector2(476, 148));
            SetRect((RectTransform)_next.transform, Vector2.zero, Vector2.zero, new Vector2(484, 80), new Vector2(708, 148));
            SetRect((RectTransform)_review.transform, Vector2.zero, Vector2.zero, new Vector2(8, 4), new Vector2(260, 72));
            SetRect((RectTransform)_dock.transform, Vector2.zero, Vector2.zero, new Vector2(528, 4), new Vector2(708, 72));
            SetRect((RectTransform)_close.transform, Vector2.zero, Vector2.zero, new Vector2(528, 4), new Vector2(708, 72));
            SetRect((RectTransform)_news.transform, Vector2.zero, Vector2.zero, new Vector2(268, 4), new Vector2(520, 72));
            if (_state == 2) RenderReports();
        }

        private string ActionLabel(GuideGoal goal) => goal?.Target switch
        {
            GuideTargetKind.Analysis => _copy.analysisAction,
            GuideTargetKind.PlanConfirmation => _copy.preparationAction,
            GuideTargetKind.Condition => _copy.pitchingAction,
            GuideTargetKind.TeamColor => _copy.teamColorAction,
            GuideTargetKind.Tactic => _copy.tacticAction,
            _ => _copy.rosterAction
        };

        private string FormatBody(GuideGoal goal, int home, int away)
        {
            if (goal == null) return _copy.empty;
            if (goal.Kind == GuideGoalKind.Preparation) return _copy.preparation;
            if (goal.Kind == GuideGoalKind.PlanConfirmation) return _copy.confirmation;
            if (goal.Kind == GuideGoalKind.Debrief) return string.Format(_copy.debrief, home, away);
            var issueCopy = FindIssueCopy(goal);
            if (issueCopy != null)
            {
                string explanation = issueCopy.body;
                if (goal.Actual.HasValue && goal.Expected.HasValue)
                    explanation += "\n" + string.Format(_copy.reportCounts, goal.Actual.Value, goal.Expected.Value);
                return explanation;
            }
            string detail;
            if (goal.Kind == GuideGoalKind.RosterIssue && Enum.TryParse(goal.Evidence, out RosterValidationIssueCode roster))
                detail = OwnerRosterLineupPresentationBuilder.FormatRosterIssueCode(roster);
            else if (Enum.TryParse(goal.Evidence, out LineupPresetValidationIssueCode preset))
                detail = OwnerRosterLineupPresentationBuilder.FormatLineupIssueCode(preset);
            else detail = _copy.missing;
            if (goal.Actual.HasValue && goal.Expected.HasValue)
                detail = string.Format(_copy.rosterCounts, detail, goal.Actual.Value, goal.Expected.Value);
            return string.Format(goal.IsRequired ? _copy.required : _copy.optional, detail);
        }

        private Text MakeText(Transform parent, string name, int size)
        {
            var text = OwnerWorkspaceUiFactory.CreateText(parent, name, "", size);
            text.color = OwnerDashboardStyle.Ivory;
            OwnerDashboardStyle.SetTypography(text);
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string label, Action action, OwnerButtonRole role = OwnerButtonRole.Secondary)
        {
            var button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            OwnerUiButtonSkin.Apply(button, role);
            OwnerUiButtonSkin.SetDashboardStyle(button);
            var labelText = button.GetComponentInChildren<Text>();
            labelText.fontSize = 22;
            OwnerDashboardStyle.SetTypography(labelText);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper; }
    }
}

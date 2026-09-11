using System;
using System.Collections.Generic;
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
    /// <summary>셸 예약 공간에서 한 과제의 근거·선택·실제 이동을 표시한다.</summary>
    public sealed class UI_System_OwnerGuide : MonoBehaviour
    {
        private SharedGameShellView _shell;
        private OwnerGuidePresentationData _copy;
        private RectTransform _expanded;
        private Button _dock, _action, _keep, _snooze, _next, _review;
        private Text _body, _counter;
        private Image _portrait;
        private GuideGoal _goal;
        private GuideMessage _legacyMessage;
        private readonly FrontManagerGuideCtaRouter _router = new FrontManagerGuideCtaRouter();
        private IReadOnlyList<GuideGoal> _goals = Array.Empty<GuideGoal>();
        private GameObject _returnFocus;
        private int _index;
        private bool _isOpen, _showSnoozed;
        private string _scope;
        public bool IsOpen => _isOpen;
        public bool ShowsSnoozed => _showSnoozed;
        public OwnerGuidePresentationData Copy => _copy;
        public event Action<GuideGoal> ActionRequested;
        public event Action<GuideGoal> KeepRequested;
        public event Action<GuideGoal> SnoozeRequested;
        public event Action ReviewRequested;
        public event Action TipsRequested;

        public static UI_System_OwnerGuide Create(SharedGameShellView shell, OwnerGuidePresentationData copy)
        {
            RectTransform host = shell.SetGuideHeight(copy.collapsedHeight);
            var view = host.gameObject.AddComponent<UI_System_OwnerGuide>();
            view._shell = shell; view._copy = copy; view.Build(); return view;
        }

        /// <summary>현재 원본 상태만 표시하며 선택 중인 문제는 안정 ID로 유지한다.</summary>
        public void Bind(GuideProgressState progress, string managerId)
        {
            _legacyMessage = null;
            string selectedKey = _goal?.Key;
            if (_scope != progress.Scope) { _scope = progress.Scope; selectedKey = null; _index = 0; }
            _goals = progress.GetVisibleGoals(_showSnoozed);
            for (int index = 0; index < _goals.Count; index++) if (_goals[index].Key == selectedKey) _index = index;
            _index = _goals.Count == 0 ? 0 : Mathf.Clamp(_index, 0, _goals.Count - 1);
            _goal = _goals.Count == 0 ? null : _goals[_index];
            _portrait.sprite = FrontManagerPortraitSprites.LoadForManager(managerId, "FM_NEUTRAL");
            _portrait.gameObject.SetActive(_portrait.sprite != null);
            Render(progress);
        }

        public void SetFeedback(string message) => _body.text = message;

        /// <summary>추적하던 수정이 실제 반영되면 창을 강제로 열지 않고 완료를 알린다.</summary>
        public void ShowResolution()
        {
            _counter.text = _copy.resolved;
            _dock.GetComponentInChildren<Text>().text = _copy.resolved;
        }

        /// <summary>기존 Fact·대사 큐도 같은 수동 안내 공간에서 재사용한다.</summary>
        public void BindMessage(GuideMessage message)
        {
            _legacyMessage = message; _goal = null;
            _body.text = message?.Text ?? _copy.empty;
            _action.GetComponentInChildren<Text>().text = _copy.action;
            _action.gameObject.SetActive(message != null && _router.CanRoute(message));
            _keep.gameObject.SetActive(false); _snooze.gameObject.SetActive(false);
            _next.gameObject.SetActive(false);
        }

        public void SetOpen(bool open, bool restoreFocus = true)
        {
            if (open && !_isOpen) _returnFocus = EventSystem.current?.currentSelectedGameObject;
            _isOpen = open;
            _shell.SetGuideHeight(open ? _copy.expandedHeight : _copy.collapsedHeight);
            ResizeConversation();
            _expanded.gameObject.SetActive(open);
            _dock.gameObject.SetActive(!open);
            if (open && _action.gameObject.activeInHierarchy) _action.Select();
            else if (open) _review.Select();
            else if (restoreFocus && _returnFocus != null && _returnFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_returnFocus);
        }

        private void Build()
        {
            _dock = OwnerWorkspaceUiFactory.CreateButton(transform, "ManagerDock", _copy.open, () => { ReviewRequested?.Invoke(); SetOpen(true); });
            SetRect((RectTransform)_dock.transform, new Vector2(1, 0), Vector2.one, new Vector2(-240, 4), new Vector2(-4, -4));
            OwnerUiButtonSkin.Apply(_dock, OwnerButtonRole.Primary);
            _expanded = new GameObject("Conversation", typeof(RectTransform)).GetComponent<RectTransform>();
            _expanded.SetParent(transform, false);
            _expanded.anchorMin = _expanded.anchorMax = new Vector2(.5f, .5f);
            var panel = OwnerWorkspaceUiFactory.CreatePanel(_expanded, "GuideDetail", _copy.title);
            SetRect(panel.Root, Vector2.zero, Vector2.one, new Vector2(192, 8), new Vector2(0, -8));
            panel.Root.GetComponent<Image>().color = CareerUiTheme.PanelDark;
            panel.Root.Find("HeaderSurface").GetComponent<Image>().color = CareerUiTheme.PanelDark;
            panel.Root.Find("HeaderAccent").GetComponent<Image>().color = CareerUiTheme.AccentGold;
            // 장식 테두리가 이름 기반 스킨에서 불투명 표면으로 해석되지 않게 역할을 고정한다.
            panel.Root.Find("ThinBorder").gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            panel.Root.Find("HeaderAccent").gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            panel.Root.Find("ThinBorder").GetComponent<Image>().enabled = false;
            panel.Root.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var title = panel.Root.Find("HeaderSlot").GetComponent<Text>();
            title.color = CareerUiTheme.AccentGold;
            title.fontSize = 18;
            _portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _portrait.transform.SetParent(_expanded, false); _portrait.preserveAspect = true; _portrait.raycastTarget = false;
            SetRect(_portrait.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(188, 0));
            _body = OwnerWorkspaceUiFactory.CreateText(panel.Content, "Evidence", _copy.loading,
                Mathf.RoundToInt(_copy.fontSize * _copy.textScale));
            _body.color = CareerUiTheme.TextPrimary;
            SetRect(_body.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 94), new Vector2(-12, -4));
            _counter = OwnerWorkspaceUiFactory.CreateText(panel.Root, "Count", "", 14, alignment: TextAnchor.MiddleRight);
            _counter.color = CareerUiTheme.TextSecondary;
            SetRect(_counter.rectTransform, new Vector2(0.5f, 1), Vector2.one, new Vector2(0, -42), new Vector2(-180, -4));
            var close = OwnerWorkspaceUiFactory.CreateButton(panel.Root, "Close", _copy.close, () => SetOpen(false));
            OwnerUiButtonSkin.Apply(close, OwnerButtonRole.Quiet);
            SetRect((RectTransform)close.transform, new Vector2(1, 1), Vector2.one, new Vector2(-80, -43), new Vector2(-8, -3));
            RectTransform actions = new GameObject("Actions", typeof(RectTransform)).GetComponent<RectTransform>();
            actions.SetParent(panel.Content, false);
            SetRect(actions, Vector2.zero, new Vector2(1, 0), new Vector2(12, 44), new Vector2(-12, 88));
            var layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(actions, CareerUiTheme.Space2);
            layout.childForceExpandWidth = false;
            _action = OwnerWorkspaceUiFactory.CreateButton(actions, "Navigate", _copy.action, () =>
            {
                if (_goal != null) ActionRequested?.Invoke(_goal);
                else if (_legacyMessage != null && _router.TryRoute(_legacyMessage)) SetOpen(false, false);
            });
            _keep = OwnerWorkspaceUiFactory.CreateButton(actions, "Keep", _copy.keep, () => { if (_goal != null) KeepRequested?.Invoke(_goal); });
            _snooze = OwnerWorkspaceUiFactory.CreateButton(actions, "Snooze", _copy.snooze, () => { if (_goal != null) SnoozeRequested?.Invoke(_goal); });
            StyleAction(_action, OwnerButtonRole.Primary, 184);
            StyleAction(_keep, OwnerButtonRole.Quiet, 172);
            StyleAction(_snooze, OwnerButtonRole.Quiet, 158);
            _next = OwnerWorkspaceUiFactory.CreateButton(panel.Root, "Next", _copy.next, () => { _index = (_index + 1) % Math.Max(1, _goals.Count); _goal = null; ReviewRequested?.Invoke(); });
            StyleAction(_next, OwnerButtonRole.Quiet, 96);
            SetRect((RectTransform)_next.transform, new Vector2(1, 1), Vector2.one, new Vector2(-178, -43), new Vector2(-86, -3));
            _review = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Review", _copy.review, () => { _showSnoozed = !_showSnoozed; ReviewRequested?.Invoke(); });
            StyleAction(_review, OwnerButtonRole.Quiet, 160);
            SetRect((RectTransform)_review.transform, Vector2.zero, Vector2.zero, new Vector2(12, 0), new Vector2(172, 36));
            var tips = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Tips", _copy.tips, () => TipsRequested?.Invoke());
            StyleAction(tips, OwnerButtonRole.Quiet, 108);
            SetRect((RectTransform)tips.transform, Vector2.zero, Vector2.zero, new Vector2(188, 0), new Vector2(296, 36));
            ResizeConversation();
            SetOpen(false, false);
        }

        private static void StyleAction(Button button, OwnerButtonRole role, float width)
        {
            OwnerUiButtonSkin.Apply(button, role);
            button.GetComponent<LayoutElement>().preferredWidth = width;
            button.GetComponentInChildren<Text>().fontSize = 16;
        }

        private void OnRectTransformDimensionsChange() => ResizeConversation();

        private void ResizeConversation()
        {
            if (_expanded == null || _copy == null) return;
            float width = Mathf.Min(_copy.conversationWidth, ((RectTransform)transform).rect.width);
            _expanded.sizeDelta = new Vector2(Mathf.Max(0, width), _copy.expandedHeight);
        }

        private void Render(GuideProgressState progress)
        {
            _action.GetComponentInChildren<Text>().text = _goal?.Target switch
            {
                GuideTargetKind.Analysis => _copy.analysisAction,
                GuideTargetKind.PlanConfirmation => _copy.preparationAction,
                GuideTargetKind.Condition => _copy.pitchingAction,
                GuideTargetKind.TeamColor => _copy.teamColorAction,
                GuideTargetKind.Tactic => _copy.tacticAction,
                _ => _copy.rosterAction
            };
            _counter.text = string.Format(_copy.count, _goals.Count == 0 ? 0 : _index + 1, _goals.Count);
            _dock.GetComponentInChildren<Text>().text = _copy.open + (_goals.Count > 0 ? " · " + _goals.Count : "");
            _review.GetComponentInChildren<Text>().text = _showSnoozed ? _copy.current : _copy.review;
            _action.gameObject.SetActive(_goal != null);
            _keep.gameObject.SetActive(_goal?.Kind == GuideGoalKind.PresetIssue && !_goal.IsRequired);
            _snooze.gameObject.SetActive(_goal != null);
            _next.gameObject.SetActive(_goals.Count > 1);
            if (_goal == null)
            {
                _body.text = progress.GetVisibleGoals(true).Count > 0 ? _copy.snoozedEmpty : _copy.empty;
                return;
            }
            if (_goal.Kind == GuideGoalKind.Preparation) { _body.text = _copy.preparation; return; }
            if (_goal.Kind == GuideGoalKind.PlanConfirmation) { _body.text = _copy.confirmation; return; }
            if (_goal.Kind == GuideGoalKind.Debrief)
            { _body.text = string.Format(_copy.debrief, progress.HomeScore, progress.AwayScore); return; }
            string detail = _goal.Evidence;
            if (_goal.Kind == GuideGoalKind.RosterIssue && Enum.TryParse(detail, out RosterValidationIssueCode roster))
                detail = OwnerRosterLineupPresentationBuilder.FormatRosterIssueCode(roster);
            else if (Enum.TryParse(detail, out LineupPresetValidationIssueCode preset))
                detail = OwnerRosterLineupPresentationBuilder.FormatLineupIssueCode(preset);
            else detail = _copy.missing;
            if (_goal.Actual.HasValue && _goal.Expected.HasValue)
                detail = string.Format(_copy.rosterCounts, detail, _goal.Actual.Value, _goal.Expected.Value);
            _body.text = string.Format(_goal.IsRequired ? _copy.required : _copy.optional, detail);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper; }
    }
}

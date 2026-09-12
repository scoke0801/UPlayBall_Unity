using System;
using Baseball.Core.Historical;
using Baseball.Core.Teams;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>코칭스태프 카드와 작전 조정, 다음 경기 운영 미리보기를 제공한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerDugout : MonoBehaviour, IUiCancelHandler
    {
        private const string ManagerPortraitPath = "UI/OwnerDugout/manager-male";
        private const string HeadCoachPortraitPath = "UI/OwnerDugout/coach-male";
        private static readonly Color Ink = CareerUiTheme.ReferenceText;
        private static readonly Color MutedInk = CareerUiTheme.ReferenceTextSecondary;
        private static readonly Color Blue = CareerUiTheme.ReferenceAccent;
        private static readonly Color Red = CareerUiTheme.Loss;
        private readonly OwnerPolicyStepSelector[] _policySelectors = new OwnerPolicyStepSelector[6];
        private RectTransform _root;
        private RectTransform _selectionOverlay;
        private RectTransform _selectionInventory;
        private Text _selectionTitle;
        private Text _selectionEmpty;
        private Text _selectionDetail;
        private Text _status;
        private Text _managerName;
        private Text _managerEffect;
        private Text _headCoachName;
        private Text _headCoachEffect;
        private readonly Text[,] _summaryValues = new Text[3, 3];
        private RawImage _managerPortrait, _coachPortrait, _selectionPortrait;
        private GameObject _previousFocus;
        private CanvasGroup _workspaceInput;
        private Text _trustHint;
        private readonly System.Collections.Generic.List<Button> _candidateButtons = new System.Collections.Generic.List<Button>();
        private Button _confirmButton;
        private Button _selectionConfirmButton;
        private OwnerDugoutSnapshot _snapshot;
        private string _draftManagerId = string.Empty;
        private string _draftHeadCoachId = string.Empty;
        private string _candidateId = string.Empty;
        private bool _isSelectingManager;

        public event Action<OwnerDugoutConfigurationCommand> ConfigurationConfirmed;

        /// <summary>셸의 전체 작업 영역에 덕아웃을 생성한다.</summary>
        public static UI_Scene_OwnerDugout CreateRuntime(RectTransform host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var view = new GameObject(nameof(UI_Scene_OwnerDugout)).AddComponent<UI_Scene_OwnerDugout>();
            view.Build(host);
            return view;
        }

        /// <summary>화면을 닫을 때 선택 창도 함께 닫는다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible) CloseSelection();
            _root.gameObject.SetActive(visible);
        }

        /// <summary>저장 원본과 실제 경기 적용값으로 화면의 임시 편집 상태를 초기화한다.</summary>
        public void Bind(OwnerDugoutSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RestoreSnapshot();
            RefreshDraftStatus();
        }

        /// <summary>Game Command의 성공·실패를 덕아웃 상태 줄에 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            _status.text = message ?? string.Empty;
            _status.color = isError ? Red : Ink;
        }

        /// <summary>인선 Overlay를 닫거나 저장되지 않은 덕아웃 편집을 원본으로 되돌린다.</summary>
        public bool TryHandleCancel()
        {
            if (_selectionOverlay != null && _selectionOverlay.gameObject.activeSelf)
            {
                CloseSelection();
                return true;
            }
            if (!HasDraftChanges())
                return false;
            RestoreSnapshot();
            return true;
        }

        private bool HasDraftChanges()
        {
            if (_snapshot == null ||
                !string.Equals(_draftManagerId, _snapshot.SelectedManagerId, StringComparison.Ordinal) ||
                !string.Equals(_draftHeadCoachId, _snapshot.SelectedHeadCoachId, StringComparison.Ordinal))
                return _snapshot != null;
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                if (_policySelectors[index].Value != _snapshot.Policy.GetLevel((DugoutPolicyAxis)index))
                    return true;
            }
            return false;
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerDugoutWorkspace", false);
            _root.offsetMin = Vector2.one * CareerUiTheme.Space4;
            _root.offsetMax = -Vector2.one * CareerUiTheme.Space4;
            _root.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var body = Rect(_root, "DugoutBody", 0f, 0f, 1f, 1f);
            _workspaceInput = body.gameObject.AddComponent<CanvasGroup>();
            Label(body, "Title", "우리 팀의 승부를 설계하세요", .01f, .91f, .60f, 1f, 27, Ink, TextAnchor.MiddleLeft);
            Label(body, "Subtitle", "코칭스태프 선택  →  작전 조정  →  다음 경기 적용", .60f, .91f, .99f, 1f, 17, MutedInk, TextAnchor.MiddleRight);
            var board = Rect(body, "DugoutBoard", 0f, .12f, 1f, .90f);
            var staff = Rect(board, "StaffColumn", 0f, 0f, .35f, 1f);
            CreateStaff(staff, "Manager", "감독", ManagerPortraitPath, 0f, 1f);
            CreateStaff(staff, "HeadCoach", "수석코치", HeadCoachPortraitPath, 0f, 1f);
            var panel = CreateDugoutPanel(board, "PolicyPanel", "작전 방침 · 감독의 기본 운영에 더할 지시");
            Place(panel.Root, .367f, 0f, .752f, 1f);
            var policy = panel.Content;
            CreatePolicy(policy, 0, "타격", "컨택", "장타", "인플레이 타구를 우선합니다.", "장타를 노리는 스윙을 늘립니다.");
            CreatePolicy(policy, 1, "도루", "신중하게", "적극적으로", "아웃 위험을 줄이는 주루입니다.", "추가 진루를 위해 위험을 감수합니다.");
            CreatePolicy(policy, 2, "번트", "타격 우선", "진루 우선", "아웃을 주기보다 타격을 택합니다.", "아웃 하나와 주자 진루를 교환합니다.");
            CreatePolicy(policy, 3, "대타", "선발 신뢰", "벤치 활용", "선발 타자에게 기회를 줍니다.", "유리한 타격 기회에 벤치를 씁니다.");
            CreatePolicy(policy, 4, "선발 교체", "긴 이닝", "빠른 교체", "선발의 이닝 소화를 중시합니다.", "불펜 부담을 감수하고 일찍 바꿉니다.");
            CreatePolicy(policy, 5, "불펜 교체", "길게 맡김", "빠른 교체", "구원 투수에게 더 맡깁니다.", "짧은 이닝으로 불펜을 운용합니다.");
            ActionButton(policy, "ResetPolicy", "방침을 모두 중립으로", .02f, .015f, .98f, .095f, ResetPolicy);
            var summary = CreateDugoutPanel(board, "PreviewPanel", "다음 경기 운영");
            Place(summary.Root, .769f, 0f, 1f, 1f);
            CreateSummaryCard(summary.Content, 0, "공격", new[] { "타격 접근", "도루", "번트" }, "", Blue);
            CreateSummaryCard(summary.Content, 1, "교체", new[] { "대타", "선발 투수", "불펜" }, "", Blue);
            CreateSummaryCard(summary.Content, 2, "코칭스태프 조합", new[] { "불펜 역할", "상대 맞춤", "수비 교체" }, "", Blue);
            _trustHint = Label(summary.Content, "TrustHint", "", .04f, .015f, .96f, .14f, 15, MutedInk, TextAnchor.UpperLeft);
            _status = Label(body, "PreviewStatus", "덕아웃 데이터를 불러오는 중입니다.", .01f, .015f, .65f, .095f, 17, Ink, TextAnchor.MiddleLeft);
            _confirmButton = ActionButton(body, "Confirm", "다음 경기 적용", .79f, .02f, .99f, .095f, ConfirmConfiguration);
            OwnerUiButtonSkin.Apply(_confirmButton, OwnerButtonRole.Primary);
            ActionButton(body, "Cancel", "변경 취소", .66f, .02f, .78f, .095f, RestoreSnapshot);
            _confirmButton.interactable = false;
            BuildSelectionOverlay();
            CareerUiSkin.Apply(_root);
            foreach (var selector in _policySelectors) selector.RefreshVisuals();
        }

        private void CreatePolicy(RectTransform parent, int index, string title, string low, string high,
            string lowDescription, string highDescription)
        {
            float top = .99f - index * .145f;
            var row = Rect(parent, "PolicyRow" + index, .02f, top - .138f, .98f, top);
            Label(row, "Name", title, 0f, .66f, .29f, 1f, 19, Ink, TextAnchor.MiddleLeft);
            Label(row, "Low", low, .32f, .66f, .65f, 1f, 14, MutedInk, TextAnchor.MiddleLeft);
            Label(row, "High", high, .66f, .66f, 1f, 1f, 14, MutedInk, TextAnchor.MiddleRight);
            var description = Label(row, "Description", "감독과 코치의 기본 운영을 따릅니다.", 0f, 0f, 1f, .28f, 14, MutedInk, TextAnchor.MiddleLeft);
            var selector = new OwnerPolicyStepSelector(Rect(row, "PolicySteps", 0f, .29f, 1f, .64f), Blue, 18);
            selector.ValueChanged += value =>
            {
                description.text = value < 2 ? lowDescription : value > 2 ? highDescription : "감독과 코치의 기본 운영을 따릅니다.";
                if (_snapshot != null) { RefreshSummary(); RefreshDraftStatus(); }
            };
            _policySelectors[index] = selector;
        }

        private void CreateStaff(RectTransform parent, string name, string title, string portraitPath, float bottom, float top)
        {
            bool manager = name == "Manager";
            var button = ActionButton(parent, name, title, manager ? 0f : .515f, bottom, manager ? .485f : 1f, top,
                () => OpenSelection(manager));
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Primary);
            var safe = Rect(button.transform, "ContentSafeRect", .065f, .035f, .935f, .965f);
            var role = button.transform.Find("Label").GetComponent<Text>();
            role.transform.SetParent(safe, false);
            Place(role.rectTransform, 0f, .92f, 1f, 1f);
            role.fontSize = 21;
            var portrait = CreatePortrait(safe, portraitPath, .0f, .48f, 1f, .91f);
            var nameLabel = Label(safe, "CurrentName", "미배정", 0f, .35f, 1f, .47f, 22, Ink);
            Label(safe, "EffectTitle", "운영 특색", 0f, .285f, 1f, .345f, 15, Ink, TextAnchor.MiddleLeft);
            var effect = Label(safe, "CurrentEffect", "정보 없음", 0f, .10f, 1f, .28f, 17, Ink, TextAnchor.UpperLeft);
            Label(safe, "SelectHint", manager ? "감독 선택  ›" : "수석코치 선택  ›", 0f, 0f, 1f, .075f, 18, Ink);
            if (manager) { _managerName = nameLabel; _managerEffect = effect; _managerPortrait = portrait; }
            else { _headCoachName = nameLabel; _headCoachEffect = effect; _coachPortrait = portrait; }
        }

        private static RawImage CreatePortrait(Transform parent, string path, float left, float bottom, float right, float top)
        {
            var host = Rect(parent, "PortraitSlot", left, bottom, right, top);
            var rect = Rect(host, "Portrait", 0f, 0f, 1f, 1f);
            var art = rect.gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>(path);
            art.raycastTarget = false;
            var aspect = rect.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1f;
            return art;
        }

        private void CreateSummaryCard(RectTransform parent, int index, string title, string[] metricNames, string note, Color accent)
        {
            float top = .99f - index * .28f;
            var card = Rect(parent, "SummaryCard" + index, .04f, top - .26f, .96f, top);
            Label(card, "Title", title, 0f, .76f, 1f, 1f, 18, accent, TextAnchor.MiddleLeft);
            for (int i = 0; i < 3; i++)
            {
                float y = .75f - i * .24f;
                Label(card, "MetricName" + i, metricNames[i], 0f, y - .23f, .47f, y, 16, MutedInk, TextAnchor.MiddleLeft);
                _summaryValues[index, i] = Label(card, "MetricValue" + i, "—", .48f, y - .23f, 1f, y, 16, Ink, TextAnchor.MiddleRight);
            }
        }



        private void BuildSelectionOverlay()
        {
            _selectionOverlay = Rect(_root, "StaffSelectionOverlay", 0f, 0f, 1f, 1f);
            Surface(_selectionOverlay, CareerUiTheme.InputBlocker);
            SetSkinRole(_selectionOverlay, CareerUiVisualRole.InputBlocker);
            _selectionOverlay.GetComponent<Image>().raycastTarget = true;
            var panel = CreateDugoutPanel(_selectionOverlay, "StaffSelectionDialog", "코칭스태프 선택");
            Place(panel.Root, .10f, .06f, .90f, .94f);
            _selectionTitle = panel.Root.Find("HeaderSlot").GetComponent<Text>();
            var dialog = panel.Content;
            _selectionPortrait = CreatePortrait(dialog, ManagerPortraitPath, .08f, .50f, .34f, .98f);
            _selectionDetail = Label(dialog, "Detail", "후보를 선택하세요.", .025f, .17f, .385f, .49f, 18, Ink, TextAnchor.UpperLeft);
            _selectionInventory = Rect(dialog, "StaffInventory", .42f, .16f, .98f, .98f);
            _selectionEmpty = Label(_selectionInventory, "EmptyState", "", .05f, .1f, .95f, .9f, 20, Ink);
            _selectionConfirmButton = ActionButton(dialog, "Confirm", "이 인선으로 선택", .55f, .015f, .78f, .115f, ApplySelectedCandidate);
            OwnerUiButtonSkin.Apply(_selectionConfirmButton, OwnerButtonRole.Primary);
            _selectionConfirmButton.interactable = false;
            ActionButton(dialog, "Exit", "선택 취소", .80f, .015f, .98f, .115f, CloseSelection);
            _selectionOverlay.gameObject.SetActive(false);
        }

        private void OpenSelection(bool isManager)
        {
            if (_snapshot == null) return;
            _previousFocus = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            _workspaceInput.interactable = false;
            _workspaceInput.blocksRaycasts = false;
            _isSelectingManager = isManager;
            _candidateId = string.Empty;
            string title = isManager ? "감독" : "수석코치";
            _selectionTitle.text = title + " 선택";
            _selectionDetail.text = "후보를 선택하면 전술 성향과 효과를 확인할 수 있습니다.";
            _selectionConfirmButton.interactable = false;
            RebuildCandidateButtons();
            _selectionOverlay.gameObject.SetActive(true);
            _selectionOverlay.SetAsLastSibling();
            SelectCandidate(isManager ? _snapshot.GetManager(_draftManagerId) : _snapshot.GetHeadCoach(_draftHeadCoachId));
            _selectionConfirmButton.Select();
        }

        private void CloseSelection()
        {
            _selectionOverlay.gameObject.SetActive(false);
            _workspaceInput.interactable = true;
            _workspaceInput.blocksRaycasts = true;
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(_previousFocus);
        }

        private void ResetPolicy()
        {
            for (int index = 0; index < _policySelectors.Length; index++)
                _policySelectors[index].SetValue(DugoutPolicySettings.NeutralLevel);
            if (_snapshot != null) _status.text = "중립 방침으로 변경했습니다. 결정 전에는 저장되지 않습니다.";
        }

        private void RestoreSnapshot()
        {
            if (_snapshot == null)
            {
                ResetPolicy();
                return;
            }
            _draftManagerId = _snapshot.SelectedManagerId;
            _draftHeadCoachId = _snapshot.SelectedHeadCoachId;
            int minimum = DugoutPolicySettings.NeutralLevel - _snapshot.AllowedPolicyOffset;
            int maximum = DugoutPolicySettings.NeutralLevel + _snapshot.AllowedPolicyOffset;
            for (int index = 0; index < _policySelectors.Length; index++)
            {
                _policySelectors[index].SetRange(minimum, maximum);
                _policySelectors[index].SetValue(
                    _snapshot.Policy.GetLevel((DugoutPolicyAxis)index),
                    true);
            }
            RefreshStaffLabels();
            RefreshSummary();
            _status.color = Ink;
            RefreshDraftStatus();
        }

        private void ConfirmConfiguration()
        {
            if (_snapshot == null) return;
            var policy = new DugoutPolicySettings(
                _policySelectors[0].Value,
                _policySelectors[1].Value,
                _policySelectors[2].Value,
                _policySelectors[3].Value,
                _policySelectors[4].Value,
                _policySelectors[5].Value);
            ConfigurationConfirmed?.Invoke(new OwnerDugoutConfigurationCommand(
                _draftManagerId,
                _draftHeadCoachId,
                policy));
        }

        private void RebuildCandidateButtons()
        {
            var source = _isSelectingManager ? _snapshot.Managers : _snapshot.HeadCoaches;
            _selectionEmpty.gameObject.SetActive(source.Length == 0);
            _selectionEmpty.text = "선택할 수 있는 코칭스태프가 없습니다.";
            for (int i = 0; i < Math.Max(source.Length, _candidateButtons.Count); i++)
            {
                if (i >= source.Length) { _candidateButtons[i].gameObject.SetActive(false); continue; }
                var candidate = source[i];
                if (i == _candidateButtons.Count)
                    _candidateButtons.Add(ActionButton(_selectionInventory, "Candidate" + i, "후보", 0f, 0f, 1f, 1f, () => { }));
                var button = _candidateButtons[i];
                button.gameObject.SetActive(true);
                float top = .975f - i * (.95f / source.Length);
                Place((RectTransform)button.transform, .04f, top - .85f / source.Length, .96f, top);
                button.transform.Find("Label").GetComponent<Text>().text = candidate.DisplayName + "  ·  " + candidate.Specialty;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectCandidate(candidate));
            }
        }

        private void SelectCandidate(OwnerDugoutStaffCandidate candidate)
        {
            _candidateId = candidate.Id;
            string detail = candidate.EffectDescription.StartsWith(candidate.Description, StringComparison.Ordinal)
                ? candidate.EffectDescription : candidate.Description + "\n\n" + candidate.EffectDescription;
            _selectionDetail.text = candidate.DisplayName + "\n" + candidate.Specialty + "\n\n" + detail;
            _selectionConfirmButton.interactable = true;
            _selectionPortrait.texture = Resources.Load<Texture2D>(candidate.PortraitResourcePath ??
                (_isSelectingManager ? ManagerPortraitPath : HeadCoachPortraitPath));
            var candidates = _isSelectingManager ? _snapshot.Managers : _snapshot.HeadCoaches;
            for (int i = 0; i < candidates.Length; i++)
                OwnerUiButtonSkin.SetSelected(_candidateButtons[i], candidates[i].Id == candidate.Id);
        }

        private void ApplySelectedCandidate()
        {
            if (string.IsNullOrEmpty(_candidateId)) return;
            if (_isSelectingManager) _draftManagerId = _candidateId;
            else _draftHeadCoachId = _candidateId;
            RefreshStaffLabels();
            RefreshSummary();
            RefreshDraftStatus();
            CloseSelection();
        }

        private void RefreshStaffLabels()
        {
            if (_snapshot == null) return;
            OwnerDugoutStaffCandidate manager = _snapshot.GetManager(_draftManagerId);
            OwnerDugoutStaffCandidate coach = _snapshot.GetHeadCoach(_draftHeadCoachId);
            _managerName.text = manager.DisplayName + "\n" + manager.Specialty;
            _managerEffect.text = manager.EffectDescription;
            _headCoachName.text = coach.DisplayName + "\n" + coach.Specialty;
            _headCoachEffect.text = coach.EffectDescription;
            _managerPortrait.texture = Resources.Load<Texture2D>(manager.PortraitResourcePath ?? ManagerPortraitPath);
            _coachPortrait.texture = Resources.Load<Texture2D>(coach.PortraitResourcePath ?? HeadCoachPortraitPath);
        }

        private DugoutPolicySettings ReadDraftPolicy() => new DugoutPolicySettings(
            _policySelectors[0].Value, _policySelectors[1].Value, _policySelectors[2].Value,
            _policySelectors[3].Value, _policySelectors[4].Value, _policySelectors[5].Value);

        private void RefreshDraftStatus()
        {
            bool changed = HasDraftChanges();
            _confirmButton.interactable = _snapshot != null && changed;
            _status.color = Ink;
            _status.text = changed ? "변경 미적용 · 다음 경기 적용을 눌러 확정하세요." : "저장된 작전입니다. 인선이나 방침을 선택해 조정하세요.";
        }

        private void RefreshSummary()
        {
            if (_snapshot == null) return;
            var p = _snapshot.Preview(_draftManagerId, _draftHeadCoachId, ReadDraftPolicy());
            _summaryValues[0, 0].text = Grade(p.BattingApproach, "컨택 중심", "균형", "장타 중심");
            _summaryValues[0, 1].text = Grade(p.RunningAggression, "신중", "상황에 따라", "적극적");
            _summaryValues[0, 2].text = Grade(p.SmallBallPreference, "타격 우선", "상황에 따라", "진루 우선");
            _summaryValues[1, 0].text = Grade(p.PinchHitAggression, "선발 신뢰", "상황에 따라", "벤치 활용");
            _summaryValues[1, 1].text = Grade(p.HookSpeed, "긴 이닝", "균형", "빠른 교체");
            _summaryValues[1, 2].text = Grade(p.BullpenAggression, "길게 맡김", "균형", "빠른 교체");
            _summaryValues[2, 0].text = Grade(p.BullpenRoleRigidity, "유연한 역할", "균형", "보직 유지");
            _summaryValues[2, 1].text = Grade(p.MatchupPreference, "기본 운영", "상황에 따라", "맞춤 중시");
            _summaryValues[2, 2].text = Grade(p.DefensiveAggression, "기존 수비", "상황에 따라", "적극 교체");
            _trustHint.text = "선발 투수 · " + Grade(p.StarTrust, "상황 우선", "균형", "신뢰 중시") +
                "\n작전은 언제나 5단계 조정 가능";
        }

        private static string Grade(int value, string low, string middle, string high) =>
            value < 45 ? low : value > 55 ? high : middle;

        private static OwnerWorkspaceUiFactory.Panel CreateDugoutPanel(Transform parent, string name, string title)
        {
            var panel = OwnerWorkspaceUiFactory.CreatePanel(parent, name, title);
            // 알파를 무시하는 기존 보조 Outline이 본문 전체를 덮지 않도록 공용 패널 자체의 테두리를 사용한다.
            panel.Root.Find("ThinBorder").gameObject.SetActive(false);
            panel.Root.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DecorativeFrame);
            CareerUiSkin.ApplyPanel(panel.Root.GetComponent<Image>(), false);
            return panel;
        }

        private static RectTransform Rect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Surface(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetSkinRole(rect, CareerUiVisualRole.DataImage);
        }

        private static void SetSkinRole(RectTransform rect, CareerUiVisualRole role)
        {
            var visual = rect.GetComponent<CareerUiVisualElement>() ?? rect.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(role);
            // 프레임 텍스처와 기존 Outline이 중복되지 않게 한다.
            Outline outline = rect.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private static Text Label(Transform parent, string name, string text, float left, float bottom, float right, float top,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text label = OwnerWorkspaceUiFactory.CreateText(parent, name, text, size, size >= 19 ? FontStyle.Bold : FontStyle.Normal, alignment, color);
            label.color = UIOwnerFrontOfficePanel.HasDarkSurface(parent)
                ? UIOwnerFrontOfficePanel.ResolveTextColor(color) : color;
            Place(label.rectTransform, left, bottom, right, top);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = size;
            label.resizeTextMaxSize = size;
            return label;
        }

        private static Button ActionButton(Transform parent, string name, string title, float left, float bottom,
            float right, float top, Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, title, action);
            Place((RectTransform)button.transform, left, bottom, right, top);
            button.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            return button;
        }

        private void OnDestroy()
        {
            ConfigurationConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>두 장착 슬롯과 전체 발동 진행도를 분리해 보여주는 구단주 TeamColor 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_OwnerTeamColor : MonoBehaviour, IUiCancelHandler
    {
        private const float CandidateRowHeight = 104f;
        private const float CandidateRowGap = 8f;

        private RectTransform _root;
        private RectTransform _backdrop;
        private RectTransform _candidateContent;
        private RectTransform _playerContent;
        private RectTransform _playerViewport;
        private readonly OwnerTeamColorCardView[] _slotCards = new OwnerTeamColorCardView[2];
        private readonly List<OwnerTeamColorCardView> _candidateCards = new List<OwnerTeamColorCardView>();
        private readonly List<float> _candidateHeights = new List<float>();
        private readonly List<PlayerMiniCardView> _playerCards = new List<PlayerMiniCardView>();
        private readonly List<OwnerTeamColorCandidateSnapshot> _visibleCandidates = new List<OwnerTeamColorCandidateSnapshot>();
        private Text _detailTitle;
        private Text _description;
        private Text _progress;
        private Text _playersTitle;
        private Text _playersEmpty;
        private Text _empty;
        private Text _equipReason;
        private Image _progressFill;
        private Button _confirmButton;
        private Button _restoreButton;
        private Button _clearButton;
        private Text _detail;
        private Text _status;
        private Button _equipButton;
        private Button _equipSecondButton;
        private InputField _search;
        private Dropdown _targetFilter;
        private Dropdown _sort;
        private Text _resultCount;
        private Button _allFilterButton;
        private Button _activeFilterButton;
        private Text _preset;
        private ScrollRect _candidateScroll;
        private OwnerTeamColorSnapshot _snapshot;
        private OwnerTeamColorCandidateSnapshot _selectedCandidate;
        private readonly string[] _draftIds = new string[2];
        private int _selectedSlot;
        private bool _showOnlyActive;
        private Vector2 _lastDetailSize;
        private Vector2 _lastPlayerViewportSize;
        private bool _hasDetailLayoutChanges = true;
        private float _candidateRowHeight = CandidateRowHeight;
        private float _lastCandidateWidth;
        private bool? _hasCompactDetail;

        public event Action<string[]> SelectionConfirmed;
        public RectTransform GuideTarget => _root != null && _root.gameObject.activeInHierarchy && _snapshot != null ? _root : null;
        public bool HasUnappliedGuideChanges
        {
            get
            {
                if (GuideTarget == null) return false;
                for (int index = 0; index < _draftIds.Length; index++)
                    if (!string.Equals(_draftIds[index], _snapshot.EquippedIds[index], StringComparison.Ordinal)) return true;
                return false;
            }
        }

        public static UI_Scene_OwnerTeamColor CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerTeamColor), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerTeamColor>();
            view.Build();
            return view;
        }

        public void Bind(OwnerTeamColorSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            for (int index = 0; index < _draftIds.Length; index++) _draftIds[index] = snapshot.EquippedIds[index];
            _selectedSlot = 0;
            _selectedCandidate = null;
            _showOnlyActive = false;
            RefreshAll();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void SetFeedback(string message, bool isError)
        {
            if (_status == null) return;
            _status.text = message ?? string.Empty;
            _status.color = isError ? CareerUiTheme.AccentGold : CareerUiTheme.RosterTextSecondary;
        }

        /// <summary>임시 TeamColor 장착 변경이 있을 때만 저장 구성으로 되돌린다.</summary>
        public bool TryHandleCancel()
        {
            if (_snapshot == null)
                return false;
            for (int index = 0; index < _draftIds.Length; index++)
            {
                if (!string.Equals(_draftIds[index], _snapshot.EquippedIds[index], StringComparison.Ordinal))
                {
                    Restore();
                    return true;
                }
            }
            return false;
        }

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerTeamColorWorkspace", false);
            _root.anchorMin = new Vector2(.5f, 0f);
            _root.anchorMax = new Vector2(.5f, 1f);
            ResizeBoard();
            Image background = _root.gameObject.AddComponent<Image>();
            background.color = CareerUiTheme.RosterBoard;
            background.raycastTarget = false;
            _root.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            // 와이드 화면에서도 셸의 밝은 바탕이 보드 양옆에 남지 않게 장식 배경만 확장한다.
            _backdrop = OwnerDugoutDetailUiFactory.CreateRect(_root, "FullWidthBackground", .5f, 0f, .5f, 1f);
            Image backdrop = _backdrop.gameObject.AddComponent<Image>();
            backdrop.color = CareerUiTheme.RosterBoard;
            backdrop.raycastTarget = false;
            _backdrop.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            RectTransform slots = CreateBoardPanel(_root, "EquippedSlots", .015f, .75f, .985f, .985f);
            CreateBoardLabel(slots, "Title", "팀 컬러", .02f, .70f, .22f, .97f, 24, FontStyle.Bold);
            _preset = CreateBoardLabel(slots, "Preset", string.Empty, .24f, .70f, .77f, .97f, 14);
            _clearButton = CreateBoardButton(slots, "Clear", "슬롯 1 해제", .79f, .72f, .88f, .95f,
                () => { _selectedSlot = 0; ClearSelectedSlot(); });
            CreateBoardButton(slots, "ClearSecond", "슬롯 2 해제", .89f, .72f, .98f, .95f,
                () => { _selectedSlot = 1; ClearSelectedSlot(); });
            for (int index = 0; index < _slotCards.Length; index++)
            {
                int slotIndex = index;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(slots, "Slot" + index, "슬롯 " + (index + 1),
                    .025f + index * .49f, .06f, .485f + index * .49f, .69f, () => SelectSlot(slotIndex));
                _slotCards[index] = OwnerTeamColorCardView.Attach(button);
            }

            RectTransform candidates = CreateBoardPanel(_root, "CandidateList", .015f, .105f, .51f, .735f);
            CreateBoardLabel(candidates, "Title", "팀 컬러 컬렉션", .035f, .895f, .55f, .985f, 19, FontStyle.Bold);
            _allFilterButton = CreateBoardButton(candidates, "All", "전체", .59f, .905f, .76f, .98f, () => SetFilter(false));
            _activeFilterButton = CreateBoardButton(candidates, "Active", "장착 가능", .78f, .905f, .965f, .98f, () => SetFilter(true));
            BuildDiscoveryControls(candidates);
            _candidateContent = CreateBoardScroll(
                candidates, "Scroll", .025f, .025f, .975f, .66f, out _candidateScroll);
            _empty = CreateBoardLabel(candidates, "Empty", "현재 장착 가능한 팀 컬러가 없습니다.\n전체 목록에서 필요한 선수를 확인하세요.",
                .07f, .35f, .93f, .65f, 16);

            RectTransform detail = CreateBoardPanel(_root, "DetailPanel", .525f, .105f, .985f, .735f);
            _detailTitle = CreateBoardLabel(detail, "Title", "팀의 개성을 완성하세요", .04f, .85f, .96f, .97f, 21, FontStyle.Bold);
            _description = CreateBoardLabel(detail, "Story", "왼쪽 목록에서 팀 컬러를 선택하세요.", .04f, .745f, .96f, .845f, 14);
            _progress = CreateBoardLabel(detail, "Progress", string.Empty, .04f, .675f, .96f, .735f, 15, FontStyle.Bold);
            RectTransform track = CreateBoardPanel(detail, "ProgressTrack", .04f, .65f, .96f, .66f);
            track.GetComponent<Image>().color = CareerUiTheme.RosterDivider;
            RectTransform fill = OwnerDugoutDetailUiFactory.CreateRect(track, "Fill", 0f, 0f, 0f, 1f);
            _progressFill = fill.gameObject.AddComponent<Image>();
            _progressFill.color = CareerUiTheme.RosterAccent;
            _progressFill.raycastTarget = false;
            fill.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            RectTransform effectContent = CreateBoardScroll(
                detail, "EffectScroll", .04f, .505f, .96f, .625f, out ScrollRect effectScroll);
            effectScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _detail = CreateBoardLabel(effectContent, "Description", string.Empty, 0f, 0f, 1f, 1f, 18, FontStyle.Bold, TextAnchor.UpperLeft);
            OwnerDashboardStyle.SetTypography(_detail);
            _detail.rectTransform.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space3);
            _detail.rectTransform.offsetMax = -_detail.rectTransform.offsetMin;
            _playersTitle = CreateBoardLabel(
                detail, "PlayersTitle", "효과를 받는 선수", .04f, .445f, .96f, .495f, 14, FontStyle.Bold);
            _playerContent = CreateHorizontalBoardScroll(
                detail, "PlayerScroll", .04f, .16f, .96f, .435f, out _, out _playerViewport);
            _playersEmpty = CreateBoardLabel(
                _playerContent, "Empty", string.Empty, .02f, .08f, .98f, .92f, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            _equipReason = CreateBoardLabel(detail, "EquipReason", string.Empty, .04f, .14f, .96f, .23f, 13);
            _equipButton = CreateBoardButton(detail, "Equip", "슬롯 1에 장착", .04f, .025f, .49f, .13f,
                () => { _selectedSlot = 0; EquipSelected(); });
            _equipSecondButton = CreateBoardButton(detail, "EquipSecond", "슬롯 2에 장착", .51f, .025f, .96f, .13f,
                () => { _selectedSlot = 1; EquipSelected(); });
            OwnerDugoutDetailUiFactory.Place((RectTransform)_playerViewport.parent, .04f, .24f, .96f, .53f);
            OwnerDugoutDetailUiFactory.Place(_playersTitle.rectTransform, .04f, .535f, .96f, .58f);
            OwnerDugoutDetailUiFactory.Place((RectTransform)effectContent.parent.parent, .04f, .585f, .96f, .705f);
            OwnerDugoutDetailUiFactory.Place(track, .04f, .72f, .96f, .73f);
            OwnerDugoutDetailUiFactory.Place(_progress.rectTransform, .04f, .735f, .96f, .79f);
            OwnerDugoutDetailUiFactory.Place(_description.rectTransform, .04f, .795f, .96f, .855f);
            OwnerDugoutDetailUiFactory.Place(_detailTitle.rectTransform, .04f, .86f, .96f, .98f);

            RectTransform actions = CreateBoardPanel(_root, "Actions", .015f, .012f, .985f, .09f);
            _status = CreateBoardLabel(actions, "Status", string.Empty, .02f, .10f, .61f, .9f, 14);
            _restoreButton = CreateBoardButton(actions, "Restore", "되돌리기", .63f, .14f, .77f, .86f, Restore);
            _confirmButton = CreateBoardButton(actions, "Confirm", "팀 컬러 적용", .79f, .14f, .98f, .86f, Confirm);
            OwnerUiButtonSkin.Apply(_confirmButton, OwnerButtonRole.Primary);
            WrapPanelContent(slots);
            WrapPanelContent(candidates);
            WrapPanelContent(detail);
            WrapPanelContent(actions);
        }

        private static void WrapPanelContent(RectTransform panel)
        {
            int childCount = panel.childCount;
            RectTransform safe = OwnerDugoutDetailUiFactory.CreateRect(panel, "ContentSafeRect", 0f, 0f, 1f, 1f);
            // 기존 비율 여백은 유지하고 네이티브 패널 경계에 추가 안전 여백을 둔다.
            safe.offsetMin = new Vector2(CareerUiTheme.Space1, CareerUiTheme.Space1);
            safe.offsetMax = -safe.offsetMin;
            for (int index = 0; index < childCount; index++) panel.GetChild(0).SetParent(safe, false);
        }

        private static RectTransform CreateBoardPanel(Transform parent, string name, float left, float bottom, float right, float top)
        {
            RectTransform panel = OwnerDugoutDetailUiFactory.CreatePanel(parent, name, left, bottom, right, top);
            panel.GetComponent<Image>().color = CareerUiTheme.RosterSurface;
            panel.GetComponent<Outline>().effectColor = CareerUiTheme.RosterDivider;
            panel.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            return panel;
        }

        private static Text CreateBoardLabel(Transform parent, string name, string value, float left, float bottom,
            float right, float top, int size, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text label = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, value, left, bottom, right, top, size, style, anchor);
            label.color = style == FontStyle.Bold ? CareerUiTheme.RosterText : CareerUiTheme.RosterTextSecondary;
            // 기본 Medium 폰트는 크기와 색으로 위계를 주고 인위적인 굵기 합성은 피한다.
            label.fontStyle = FontStyle.Normal;
            label.gameObject.AddComponent<CareerUiPreserveTextColor>();
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button CreateBoardButton(Transform parent, string name, string label, float left, float bottom,
            float right, float top, Action action)
        {
            Button button = OwnerDugoutDetailUiFactory.CreateButton(parent, name, label, left, bottom, right, top, action);
            OwnerUiButtonSkin.SetBoardStyle(button);
            button.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Normal;
            return button;
        }

        private static RectTransform CreateBoardScroll(Transform parent, string name, float left, float bottom,
            float right, float top, out ScrollRect scroll)
        {
            UIXScrollView view = UIXScrollView.Create(parent, name, Vector2.zero, Vector2.zero, Vector2.zero,
                false, true, CareerUiTheme.RosterSurface, CareerUiTheme.RosterBoard, CareerUiTheme.RosterTextSecondary);
            OwnerDugoutDetailUiFactory.Place(view.Root, left, bottom, right, top);
            view.Content.anchorMax = Vector2.one;
            foreach (Image image in view.Root.GetComponentsInChildren<Image>())
                image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            scroll = view.ScrollRect;
            OwnerDashboardStyle.ApplyInset(view.Viewport.GetComponent<Image>(), true);
            return view.Content;
        }

        private static RectTransform CreateHorizontalBoardScroll(
            Transform parent,
            string name,
            float left,
            float bottom,
            float right,
            float top,
            out ScrollRect scroll,
            out RectTransform viewport)
        {
            UIXScrollView view = UIXScrollView.Create(parent, name, Vector2.zero, Vector2.zero, Vector2.zero,
                true, false, CareerUiTheme.RosterSurface, CareerUiTheme.RosterBoard, CareerUiTheme.RosterTextSecondary);
            OwnerDugoutDetailUiFactory.Place(view.Root, left, bottom, right, top);
            view.Content.anchorMin = new Vector2(0f, 0f);
            view.Content.anchorMax = new Vector2(0f, 1f);
            view.Content.pivot = new Vector2(0f, .5f);
            view.Content.anchoredPosition = Vector2.zero;
            foreach (Image image in view.Root.GetComponentsInChildren<Image>())
                image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            scroll = view.ScrollRect;
            viewport = view.Viewport;
            OwnerDashboardStyle.ApplyInset(view.Viewport.GetComponent<Image>(), true);
            return view.Content;
        }

        private void RefreshAll()
        {
            if (_snapshot == null) return;
            RefreshSlots();
            SetFilter(false);
            RefreshDetail();
            RebuildCandidates();
            SetFeedback("팀 컬러 선택 → 슬롯 1·2에 장착 → 적용", false);
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotCards.Length; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = Find(_draftIds[index]);
                bool changed = !string.Equals(_draftIds[index], _snapshot.EquippedIds[index], StringComparison.Ordinal);
                _slotCards[index].Bind(candidate, "슬롯 " + (index + 1) +
                    " · 효과 확인" +
                    (changed ? " · 적용 대기" : " · 저장됨"), index == _selectedSlot, true, !changed);
            }
            bool hasChanges = HasDraftChanges();
            _preset.text = _snapshot.PresetName + (hasChanges ? " · 변경한 구성을 적용해 주세요" : " · 현재 적용 중인 구성");
            _confirmButton.interactable = hasChanges;
            _restoreButton.interactable = hasChanges;
            _clearButton.interactable = !string.IsNullOrEmpty(_draftIds[0]);
            _clearButton.transform.parent.Find("ClearSecond").GetComponent<Button>().interactable = !string.IsNullOrEmpty(_draftIds[1]);
            RefreshEquipState();
        }

        private bool HasDraftChanges()
        {
            for (int index = 0; index < _draftIds.Length; index++)
                if (!string.Equals(_draftIds[index], _snapshot.EquippedIds[index], StringComparison.Ordinal)) return true;
            return false;
        }

        private void RebuildCandidates()
        {
            _visibleCandidates.Clear();
            for (int index = 0; index < _snapshot.Candidates.Count; index++)
                if ((!_showOnlyActive || _snapshot.Candidates[index].IsActive) && MatchesDiscovery(_snapshot.Candidates[index]))
                    _visibleCandidates.Add(_snapshot.Candidates[index]);
            _visibleCandidates.Sort(CompareCandidates);
            _resultCount.text = _visibleCandidates.Count + "개 / 전체 " + _snapshot.Candidates.Count + "개";
            float height = 0f;
            _candidateHeights.Clear();
            for (int index = 0; index < _visibleCandidates.Count; index++)
            {
                if (index >= _candidateCards.Count)
                {
                    int rowIndex = index;
                    Button button = OwnerDugoutDetailUiFactory.CreateButton(_candidateContent, "Candidate" + index, string.Empty,
                        0f, 0f, 1f, 1f, () => SelectCandidate(_visibleCandidates[rowIndex]));
                    _candidateCards.Add(OwnerTeamColorCardView.Attach(button));
                    _candidateCards[index].UseComparisonLayout();
                }
                OwnerTeamColorCandidateSnapshot candidate = _visibleCandidates[index];
                OwnerTeamColorCardView card = _candidateCards[index];
                card.gameObject.SetActive(true);
                OwnerDugoutDetailUiFactory.Place((RectTransform)card.transform, .01f, 0f, .99f, 1f);
                string equipped = string.Equals(_draftIds[0], candidate.Id, StringComparison.Ordinal) ? " · 슬롯 1" :
                    string.Equals(_draftIds[1], candidate.Id, StringComparison.Ordinal) ? " · 슬롯 2" : string.Empty;
                card.Bind(candidate, candidate.ProgressText + equipped, ReferenceEquals(candidate, _selectedCandidate), true);
                float rowHeight = Mathf.Max(_candidateRowHeight, card.GetComparisonHeight()) + CandidateRowGap;
                _candidateHeights.Add(rowHeight);
                height += rowHeight;
            }
            _candidateContent.sizeDelta = new Vector2(0f, Mathf.Max(1f, height));
            float offset = 0f;
            for (int index = 0; index < _visibleCandidates.Count; index++)
            {
                RectTransform row = (RectTransform)_candidateCards[index].transform;
                row.anchorMin = new Vector2(.01f, 1f);
                row.anchorMax = new Vector2(.99f, 1f);
                row.pivot = new Vector2(.5f, 1f);
                row.sizeDelta = new Vector2(0f, _candidateHeights[index] - CandidateRowGap);
                row.anchoredPosition = new Vector2(0f, -offset);
                offset += _candidateHeights[index];
            }
            for (int index = _visibleCandidates.Count; index < _candidateCards.Count; index++)
                _candidateCards[index].gameObject.SetActive(false);
            _empty.gameObject.SetActive(_visibleCandidates.Count == 0);
            _empty.text = _snapshot.Candidates.Count == 0 ? "등록된 팀 컬러가 없습니다." :
                "조건에 맞는 팀 컬러가 없습니다.\n검색·필터 초기화로 전체 목록을 확인하세요.";
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            _selectedCandidate = Find(_draftIds[slotIndex]);
            RefreshSlots();
            RefreshDetail();
            RebuildCandidates();
        }

        private void SelectCandidate(OwnerTeamColorCandidateSnapshot candidate)
        {
            _selectedCandidate = candidate;
            RefreshDetail();
            RebuildCandidates();
        }

        private void RefreshDetail()
        {
            OwnerTeamColorCandidateSnapshot candidate = _selectedCandidate;
            _detailTitle.text = candidate == null ? "우리 팀의 시너지" : candidate.Grade + "  " + candidate.Name;
            _description.text = candidate == null ? "현재 적용 중인 효과입니다. 목록에서 다른 팀 컬러를 비교해 보세요." : candidate.Description;
            _progress.text = candidate == null ? string.Empty : candidate.IsActive
                ? "장착 가능  ·  " + candidate.ProgressText
                : "완성까지 " + Math.Max(0, candidate.Definition.RequiredCount - candidate.EligibleCount) + "명  ·  " + candidate.ProgressText;
            _progressFill.rectTransform.anchorMax = new Vector2(candidate == null ? 0f :
                Mathf.Clamp01((float)candidate.EligibleCount / candidate.Definition.RequiredCount), 1f);
            _detail.text = candidate == null ? _snapshot.ActiveEffectSummary :
                OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(candidate.Definition);
            _playersTitle.text = candidate != null && !candidate.IsActive
                ? "장착 후 효과를 받는 선수"
                : "효과를 받는 선수";
            RefreshEligiblePlayers(candidate);
            RefreshEquipState();
            _hasDetailLayoutChanges = true;
            ResizeDetailContent();
        }

        private void LateUpdate()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return;
            ResizeBoard();
            RefreshResponsiveDetail();
            float rowHeight = CandidateRowHeight;
            if (_snapshot != null && (Mathf.Abs(_candidateRowHeight - rowHeight) > .5f ||
                Mathf.Abs(_lastCandidateWidth - _candidateContent.rect.width) > .5f))
            {
                _candidateRowHeight = rowHeight;
                _lastCandidateWidth = _candidateContent.rect.width;
                RebuildCandidates();
            }
            Vector2 size = ((RectTransform)_detail.transform.parent.parent).rect.size;
            Vector2 playerViewportSize = _playerViewport.rect.size;
            if (!_hasDetailLayoutChanges && size == _lastDetailSize && playerViewportSize == _lastPlayerViewportSize) return;
            _lastDetailSize = size;
            _lastPlayerViewportSize = playerViewportSize;
            _hasDetailLayoutChanges = false;
            ResizeDetailContent();
        }

        private void ResizeBoard()
        {
            float availableWidth = ((RectTransform)_root.parent).rect.width;
            float width = Mathf.Min(availableWidth, CareerUiTheme.RosterMaxWidth);
            if (Mathf.Abs(_root.sizeDelta.x - width) > .5f) _root.sizeDelta = new Vector2(width, 0f);
            if (_backdrop != null && Mathf.Abs(_backdrop.sizeDelta.x - availableWidth) > .5f)
                _backdrop.sizeDelta = new Vector2(availableWidth, 0f);
        }

        private void RefreshResponsiveDetail()
        {
            bool compact = ((RectTransform)_detailTitle.transform.parent).rect.height < 420f;
            if (_hasCompactDetail == compact && !_hasDetailLayoutChanges &&
                _lastDetailSize == ((RectTransform)_detail.transform.parent.parent).rect.size) return;
            _hasCompactDetail = compact;
            // 낮은 화면에서는 보조 설명을 접어 선수 카드와 교체 행동의 가독성을 먼저 보존한다.
            _description.gameObject.SetActive(!compact);
            OwnerDugoutDetailUiFactory.Place(_detailTitle.rectTransform, .04f, compact ? .87f : .86f, .96f, .98f);
            OwnerDugoutDetailUiFactory.Place(_progress.rectTransform, .04f, compact ? .805f : .735f, .96f, compact ? .87f : .79f);
            OwnerDugoutDetailUiFactory.Place((RectTransform)_progressFill.transform.parent, .04f, compact ? .79f : .72f, .96f, compact ? .795f : .73f);
            bool hasCandidate = _selectedCandidate != null;
            _progress.gameObject.SetActive(hasCandidate);
            _progressFill.transform.parent.gameObject.SetActive(hasCandidate);
            float panelHeight = Mathf.Max(1f, ((RectTransform)_detailTitle.transform.parent).rect.height);
            float effectTop = hasCandidate ? (compact ? .785f : .705f) : (compact ? .86f : .79f);
            // 기본 시너지는 빈 선수 영역을 활용한다. 후보 선택 후에는 선수 카드의 가독성을 보존한다.
            float effectHeight = hasCandidate ? (compact ? .07f : .12f) :
                Mathf.Clamp((_detail.preferredHeight + CareerUiTheme.Space3 * 2f) / panelHeight,
                    .24f, effectTop - (compact ? .43f : .40f));
            float effectBottom = effectTop - effectHeight;
            OwnerDugoutDetailUiFactory.Place((RectTransform)_detail.transform.parent.parent.parent,
                .04f, effectBottom, .96f, effectTop);
            OwnerDugoutDetailUiFactory.Place(_playersTitle.rectTransform, .04f, effectBottom - .065f, .96f, effectBottom - .005f);
            OwnerDugoutDetailUiFactory.Place((RectTransform)_playerViewport.parent, .04f, compact ? .21f : .24f, .96f, effectBottom - .07f);
            OwnerDugoutDetailUiFactory.Place(_equipReason.rectTransform, .04f, compact ? .105f : .14f, .96f, compact ? .20f : .23f);
            OwnerDugoutDetailUiFactory.Place((RectTransform)_equipButton.transform, .04f, compact ? .01f : .025f, .49f, compact ? .10f : .13f);
            OwnerDugoutDetailUiFactory.Place((RectTransform)_equipSecondButton.transform, .51f, compact ? .01f : .025f, .96f, compact ? .10f : .13f);
            _hasDetailLayoutChanges = true;
        }

        private void ResizeDetailContent()
        {
            ResizeScrollLabel(_detail);
            ResizeEligiblePlayerCards();
        }

        private void RefreshEligiblePlayers(OwnerTeamColorCandidateSnapshot candidate)
        {
            IReadOnlyList<OwnerTeamColorEligiblePlayerSnapshot> players = candidate?.EligiblePlayers;
            int count = players?.Count ?? 0;
            while (_playerCards.Count < count)
            {
                PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(
                    _playerContent,
                    "EligiblePlayer" + _playerCards.Count);
                card.UseLineupSlotLayout();
                card.UseRosterPresentation();
                card.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
                card.GetComponent<CanvasGroup>().blocksRaycasts = false;
                _playerCards.Add(card);
            }

            for (int index = 0; index < count; index++)
            {
                OwnerTeamColorEligiblePlayerSnapshot player = players[index];
                PlayerMiniCardView card = _playerCards[index];
                card.gameObject.SetActive(true);
                card.Bind(
                    player.MiniCard,
                    PlayerPortraitSprites.GetForPlayer(player.PlayerPersonId, player.Position));
                card.SetTeamIdentity(player.TeamDisplayName);
                card.GetComponent<Button>().enabled = false;
            }
            for (int index = count; index < _playerCards.Count; index++)
                _playerCards[index].gameObject.SetActive(false);

            _playersEmpty.gameObject.SetActive(count == 0);
            _playersEmpty.text = candidate == null
                ? "컬러를 선택하면 해당 효과를 받는 선수들을 확인할 수 있습니다."
                : "조건에 맞는 선수가 없습니다.";
            ResizeEligiblePlayerCards();
        }

        private void ResizeEligiblePlayerCards()
        {
            if (_playerViewport == null || _playerContent == null) return;
            int visibleCount = 0;
            for (int index = 0; index < _playerCards.Count; index++)
                if (_playerCards[index].gameObject.activeSelf) visibleCount++;

            float viewportWidth = Mathf.Max(1f, _playerViewport.rect.width);
            float cardHeight = Mathf.Min(
                PlayerMiniCardView.LineupSlotHeight,
                Mathf.Max(1f, _playerViewport.rect.height - CareerUiTheme.Space1 * 2f));
            float cardWidth = cardHeight * PlayerMiniCardView.LineupSlotWidth / PlayerMiniCardView.LineupSlotHeight;
            float gap = CareerUiTheme.Space1;
            float contentWidth = CareerUiTheme.Space1 * 2f +
                visibleCount * cardWidth + Mathf.Max(0, visibleCount - 1) * gap;
            _playerContent.sizeDelta = new Vector2(Mathf.Max(viewportWidth, contentWidth), 0f);
            _playersEmpty.rectTransform.sizeDelta = Vector2.zero;

            int visibleIndex = 0;
            for (int index = 0; index < _playerCards.Count; index++)
            {
                PlayerMiniCardView card = _playerCards[index];
                if (!card.gameObject.activeSelf) continue;
                RectTransform rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.pivot = new Vector2(0f, .5f);
                rect.sizeDelta = new Vector2(cardWidth, cardHeight);
                rect.anchoredPosition = new Vector2(CareerUiTheme.Space1 + visibleIndex * (cardWidth + gap), 0f);
                visibleIndex++;
            }
        }

        private static void ResizeScrollLabel(Text label)
        {
            if (label == null) return;
            RectTransform content = (RectTransform)label.transform.parent;
            // 콘텐츠는 가로만 Stretch하고 세로는 상단 고정이므로 실제 전체 높이를 지정한다.
            float padding = label.rectTransform.offsetMin.y - label.rectTransform.offsetMax.y;
            float height = Mathf.Max(((RectTransform)content.parent).rect.height, label.preferredHeight + padding);
            if (Mathf.Abs(content.sizeDelta.y - height) > .5f)
                content.sizeDelta = new Vector2(0f, height);
        }

        private string GetEquipReason(int slotIndex)
        {
            if (_selectedCandidate == null) return "목록에서 팀 컬러를 선택하세요.";
            if (!_selectedCandidate.IsActive) return "필요한 선수를 1군에 등록하면 장착할 수 있습니다.";
            if (string.Equals(_draftIds[slotIndex], _selectedCandidate.Id, StringComparison.Ordinal))
                return "이미 장착됨";
            OwnerTeamColorCandidateSnapshot other = Find(_draftIds[1 - slotIndex]);
            if (other == null) return string.Empty;
            if (string.Equals(other.Id, _selectedCandidate.Id, StringComparison.Ordinal))
                return "다른 슬롯에 장착된 팀 컬러입니다.";
            if (other.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                _selectedCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                string.Equals(other.StackGroup, _selectedCandidate.StackGroup, StringComparison.Ordinal))
                return "같은 계열은 한 단계만 장착할 수 있습니다.";
            return string.Empty;
        }

        private void RefreshEquipState()
        {
            string first = RefreshSlotAction(_equipButton, 0);
            string second = RefreshSlotAction(_equipSecondButton, 1);
            _equipReason.text = _selectedCandidate == null ? "목록에서 효과를 비교하고 팀 컬러를 선택하세요." :
                first == second ? first : "슬롯 1: " + first + "\n슬롯 2: " + second;
        }

        private string RefreshSlotAction(Button button, int slotIndex)
        {
            string reason = GetEquipReason(slotIndex);
            button.interactable = reason.Length == 0;
            bool occupied = !string.IsNullOrEmpty(_draftIds[slotIndex]);
            button.transform.Find("Label").GetComponent<Text>().text = "슬롯 " + (slotIndex + 1) +
                (reason == "이미 장착됨" ? " · 장착 중" : occupied ? " 교체" : "에 장착");
            return reason.Length > 0 ? reason : occupied ? "장착 중인 컬러를 교체합니다." : "빈 슬롯에 장착합니다.";
        }

        private void EquipSelected()
        {
            if (GetEquipReason(_selectedSlot).Length > 0) return;
            _draftIds[_selectedSlot] = _selectedCandidate.Id;
            RefreshSlots();
            RebuildCandidates();
            SetFeedback("장착 구성을 바꿨습니다. ‘팀 컬러 적용’으로 확정하세요.", false);
        }

        private void ClearSelectedSlot()
        {
            _draftIds[_selectedSlot] = null;
            RefreshSlots();
            RebuildCandidates();
            SetFeedback("슬롯을 비웠습니다. 적용하면 해당 효과가 해제됩니다.", false);
        }

        private void SetFilter(bool activeOnly)
        {
            _showOnlyActive = activeOnly;
            OwnerUiButtonSkin.SetSelected(_allFilterButton, !activeOnly);
            OwnerUiButtonSkin.SetSelected(_activeFilterButton, activeOnly);
            RebuildCandidates();
            _candidateScroll.StopMovement();
            _candidateContent.anchoredPosition = Vector2.zero;
        }

        private void Restore()
        {
            if (_snapshot == null) return;
            for (int index = 0; index < _draftIds.Length; index++) _draftIds[index] = _snapshot.EquippedIds[index];
            RefreshSlots();
            RebuildCandidates();
            SetFeedback("적용 중인 구성으로 되돌렸습니다.", false);
        }

        private void Confirm()
        {
            if (!HasDraftChanges()) return;
            var result = new string[_draftIds.Length];
            Array.Copy(_draftIds, result, result.Length);
            SelectionConfirmed?.Invoke(result);
        }

        private OwnerTeamColorCandidateSnapshot Find(string id)
        {
            if (string.IsNullOrEmpty(id) || _snapshot == null) return null;
            for (int index = 0; index < _snapshot.Candidates.Count; index++)
                if (string.Equals(_snapshot.Candidates[index].Id, id, StringComparison.Ordinal)) return _snapshot.Candidates[index];
            return null;
        }

        private void OnDestroy()
        {
            SelectionConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }

    /// <summary>TeamColor 효과 대상을 구분하는 카드 플레이트를 Resources에서 한 번만 읽는다.</summary>
    internal static class OwnerTeamColorCardArtwork
    {
        // 원본 2048px 플레이트에서 등급 홈의 중심을 측정한 정규화 좌표다.
        private const float CommonGradeCenterX = 0.1875f;
        private const float RoleGradeCenterX = 0.178f;
        private const string CommonPlateResourcePath =
            "UI/OwnerTeamColor/team_color_card_plate_common_v2";
        private const string HitterPlateResourcePath =
            "UI/OwnerTeamColor/team_color_card_plate_hitter_v2";
        private const string PitcherPlateResourcePath =
            "UI/OwnerTeamColor/team_color_card_plate_pitcher_v2";

        private static Sprite _commonPlate;
        private static Sprite _hitterPlate;
        private static Sprite _pitcherPlate;

        public static Texture2D Load(TeamColorDefinition definition)
        {
            bool affectsHitters = definition != null && definition.HitterBonus.Total > 0;
            bool affectsPitchers = definition != null && definition.PitcherBonus.Total > 0;
            Texture2D selected;
            if (affectsHitters && !affectsPitchers)
                selected = LoadTexture(HitterPlateResourcePath, ref _hitterPlate);
            else if (affectsPitchers && !affectsHitters)
                selected = LoadTexture(PitcherPlateResourcePath, ref _pitcherPlate);
            else
                selected = LoadTexture(CommonPlateResourcePath, ref _commonPlate);

            // 개별 플레이트 import가 실패해도 카드 전체가 흰 사각형으로 노출되지는 않아야 한다.
            return selected ?? LoadTexture(CommonPlateResourcePath, ref _commonPlate);
        }

        public static float GetGradeCenterX(TeamColorDefinition definition)
        {
            bool affectsHitters = definition != null && definition.HitterBonus.Total > 0;
            bool affectsPitchers = definition != null && definition.PitcherBonus.Total > 0;
            return affectsHitters != affectsPitchers ? RoleGradeCenterX : CommonGradeCenterX;
        }

        private static Texture2D LoadTexture(string resourcePath, ref Sprite cachedSprite)
        {
            if (cachedSprite == null) cachedSprite = Resources.Load<Sprite>(resourcePath);
            return cachedSprite != null ? cachedSprite.texture : null;
        }
    }

    /// <summary>효과 대상별 TeamColor 카드 플레이트에 안전한 표시명·등급·진행도를 그린다.</summary>
    internal sealed class OwnerTeamColorCardView : MonoBehaviour, ISelectHandler
    {
        // 세 생성 자산의 정규화된 상하 투명 여백을 제외한 공통 플레이트 영역이다.
        private static readonly Rect CardPlateUv = new Rect(0f, 0.25f, 1f, 0.50f);
        private const float TextSafeLeft = 0.34f;

        private RawImage _surface;
        private Image _fallbackSurface;
        private Text _name;
        private Text _grade;
        private Text _meta;
        private Text _state;
        private Outline _selection;
        private Text _effect;
        private bool _hasComparisonLayout;

        /// <summary>키보드·게임패드로 이동한 카드가 스크롤 마스크 뒤에 숨지 않게 한다.</summary>
        public void OnSelect(BaseEventData eventData)
        {
            ScrollRect scroll = GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, (RectTransform)transform);
            float shift = bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y :
                bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y : 0f;
            if (Mathf.Approximately(shift, 0f)) return;
            scroll.StopMovement();
            scroll.content.anchoredPosition += new Vector2(0f, shift);
        }

        public static OwnerTeamColorCardView Attach(Button button)
        {
            var view = button.gameObject.AddComponent<OwnerTeamColorCardView>();
            view.Initialize(button);
            return view;
        }

        public void Bind(
            OwnerTeamColorCandidateSnapshot candidate,
            string meta,
            bool isSelected,
            bool showActivationState,
            bool isEquipped = false)
        {
            bool hasCandidate = candidate != null;
            _name.text = hasCandidate ? candidate.Name : "빈 슬롯";
            _grade.text = hasCandidate ? candidate.Grade : "-";
            float gradeCenterX = OwnerTeamColorCardArtwork.GetGradeCenterX(candidate?.Definition);
            _grade.rectTransform.anchorMin = new Vector2(gradeCenterX - 0.05f, 0.20f);
            _grade.rectTransform.anchorMax = new Vector2(gradeCenterX + 0.05f, 0.80f);
            _meta.text = meta ?? string.Empty;
            _state.text = !hasCandidate || !showActivationState
                ? string.Empty
                : candidate.IsActive ? isEquipped ? "발동" : "장착 가능" : "인원 부족";
            _state.color = hasCandidate && candidate.IsActive
                ? new Color(0.78f, 0.95f, 0.80f, 1f)
                : new Color(0.90f, 0.76f, 0.72f, 1f);
            _surface.color = !hasCandidate
                ? new Color(0.65f, 0.69f, 0.73f, 1f)
                : candidate.IsActive || !showActivationState
                    ? Color.white
                    : new Color(0.72f, 0.72f, 0.72f, 1f);
            Texture2D texture = OwnerTeamColorCardArtwork.Load(candidate?.Definition);
            _surface.texture = texture;
            _surface.enabled = texture != null;
            _fallbackSurface.color = texture == null
                ? new Color(0.08f, 0.10f, 0.13f, 1f)
                : Color.clear;
            _selection.enabled = isSelected;
            if (_hasComparisonLayout)
            {
                _effect.text = hasCandidate ? DescribeComparisonEffect(candidate.Definition) : string.Empty;
                _state.text = meta.Contains("슬롯") ? "장착 중" : _state.text;
            }
        }

        /// <summary>비교 목록도 공용 플레이트를 사용하고 이름·효과·진행도를 프레임 안에 배치한다.</summary>
        public void UseComparisonLayout()
        {
            _hasComparisonLayout = true;
            OwnerDugoutDetailUiFactory.Place(_name.rectTransform, TextSafeLeft, .56f, .80f, .80f);
            OwnerDugoutDetailUiFactory.Place(_state.rectTransform, .81f, .30f, .94f, .70f);
            OwnerDugoutDetailUiFactory.Place(_meta.rectTransform, TextSafeLeft, .14f, .80f, .29f);
            _effect = OwnerDugoutDetailUiFactory.CreateLabel(transform, "Effect", string.Empty,
                TextSafeLeft, .30f, .80f, .55f, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            _effect.color = CareerUiTheme.RosterText;
            _effect.horizontalOverflow = HorizontalWrapMode.Wrap;
            _effect.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>긴 한국어 이름과 여러 능력치 효과가 줄바꿈되어도 본문을 자르지 않는다.</summary>
        public float GetComparisonHeight()
        {
            return Mathf.Max(_name.preferredHeight / .24f, _effect.preferredHeight / .25f,
                _meta.preferredHeight / .15f);
        }

        /// <summary>상세와 같은 효과 문구에서 중첩 설명만 분리해 후보끼리 비교한다.</summary>
        public static string DescribeComparisonEffect(TeamColorDefinition definition)
        {
            string description = OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(definition);
            int suffix = description.IndexOf("\n중첩:", StringComparison.Ordinal);
            return suffix < 0 ? description : description.Substring(0, suffix);
        }

        private void Initialize(Button button)
        {
            // 슬롯 생성 시 붙은 문자 버튼 스킨을 해제하고 카드가 팔레트와 입력 Graphic을 소유한다.
            OwnerUiButtonSkin.Restore(button);
            button.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            gameObject.AddComponent<CareerUiPreserveTextColor>();
            _fallbackSurface = button.GetComponent<Image>();
            _fallbackSurface.sprite = null;
            _fallbackSurface.color = Color.clear;
            Outline hitOutline = _fallbackSurface.GetComponent<Outline>();
            if (hitOutline != null) hitOutline.enabled = false;

            var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            artwork.transform.SetParent(transform, false);
            artwork.transform.SetAsFirstSibling();
            OwnerDugoutDetailUiFactory.Place(artwork.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
            _surface = artwork.GetComponent<RawImage>();
            _surface.texture = OwnerTeamColorCardArtwork.Load(null);
            _surface.uvRect = CardPlateUv;
            _surface.raycastTarget = false;
            button.targetGraphic = _surface;
            button.transition = Selectable.Transition.ColorTint;

            _name = button.transform.Find("Label").GetComponent<Text>();
            _name.text = string.Empty;
            _name.fontSize = 16;
            _name.fontStyle = FontStyle.Normal;
            _name.alignment = TextAnchor.MiddleLeft;
            _name.color = Color.white;
            _name.horizontalOverflow = HorizontalWrapMode.Wrap;
            _name.verticalOverflow = VerticalWrapMode.Truncate;
            OwnerDugoutDetailUiFactory.Place(_name.rectTransform, TextSafeLeft, 0.35f, 0.78f, 0.87f);

            _grade = OwnerDugoutDetailUiFactory.CreateLabel(
                transform,
                "Grade",
                "-",
                OwnerTeamColorCardArtwork.GetGradeCenterX(null) - 0.05f,
                0.20f,
                OwnerTeamColorCardArtwork.GetGradeCenterX(null) + 0.05f,
                0.80f,
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _grade.color = Color.white;
            _grade.alignByGeometry = true;
            _meta = OwnerDugoutDetailUiFactory.CreateLabel(
                transform, "Meta", string.Empty, TextSafeLeft, 0.12f, 0.78f, 0.34f, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            _meta.color = new Color(0.80f, 0.82f, 0.84f, 1f);
            _state = OwnerDugoutDetailUiFactory.CreateLabel(
                transform, "State", string.Empty, 0.80f, 0.20f, 0.95f, 0.80f, 11, FontStyle.Bold, TextAnchor.MiddleCenter);

            _selection = artwork.AddComponent<Outline>();
            _selection.effectColor = CareerUiTheme.AccentGold;
            _selection.effectDistance = new Vector2(2f, -2f);
            _selection.useGraphicAlpha = false;
            _selection.enabled = false;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.84f, 0.75f, 0.75f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;
        }
    }
}

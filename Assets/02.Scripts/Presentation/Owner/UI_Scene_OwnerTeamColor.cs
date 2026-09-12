using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>두 장착 슬롯과 전체 발동 진행도를 분리해 보여주는 구단주 TeamColor 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerTeamColor : MonoBehaviour, IUiCancelHandler
    {
        private const float CandidateRowHeight = 112f;
        private const float CandidateRowGap = 8f;
        private const float CardAspectRatio = 5.6f;

        private RectTransform _root;
        private RectTransform _backdrop;
        private RectTransform _candidateContent;
        private readonly OwnerTeamColorCardView[] _slotCards = new OwnerTeamColorCardView[2];
        private readonly List<OwnerTeamColorCardView> _candidateCards = new List<OwnerTeamColorCardView>();
        private readonly List<OwnerTeamColorCandidateSnapshot> _visibleCandidates = new List<OwnerTeamColorCandidateSnapshot>();
        private Text _detailTitle;
        private Text _description;
        private Text _progress;
        private Text _players;
        private Text _empty;
        private Text _equipReason;
        private Image _progressFill;
        private Button _confirmButton;
        private Button _restoreButton;
        private Button _clearButton;
        private Text _detail;
        private Text _status;
        private Button _equipButton;
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
        private bool _hasDetailLayoutChanges = true;
        private float _candidateRowHeight = CandidateRowHeight;

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
            _clearButton = CreateBoardButton(slots, "Clear", "선택 슬롯 비우기", .79f, .72f, .98f, .95f, ClearSelectedSlot);
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
            _candidateContent = CreateBoardScroll(
                candidates, "Scroll", .025f, .025f, .975f, .875f, out _candidateScroll);
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
                detail, "EffectScroll", .04f, .405f, .96f, .625f, out _);
            _detail = CreateBoardLabel(effectContent, "Description", string.Empty, 0f, 0f, 1f, 1f, 18, FontStyle.Bold, TextAnchor.UpperLeft);
            CreateBoardLabel(detail, "PlayersTitle", "효과를 받는 선수", .04f, .335f, .96f, .39f, 14, FontStyle.Bold);
            RectTransform playerContent = CreateBoardScroll(
                detail, "PlayerScroll", .04f, .18f, .96f, .325f, out _);
            _players = CreateBoardLabel(playerContent, "Players", string.Empty, 0f, 0f, 1f, 1f, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            _equipReason = CreateBoardLabel(detail, "EquipReason", string.Empty, .04f, .015f, .57f, .16f, 13);
            _equipButton = CreateBoardButton(detail, "Equip", "슬롯 1에 장착", .60f, .04f, .96f, .145f, EquipSelected);

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
            return view.Content;
        }

        private void RefreshAll()
        {
            if (_snapshot == null) return;
            RefreshSlots();
            SetFilter(false);
            RefreshDetail();
            RebuildCandidates();
            SetFeedback("장착할 슬롯과 팀 컬러를 선택하세요.", false);
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotCards.Length; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = Find(_draftIds[index]);
                bool changed = !string.Equals(_draftIds[index], _snapshot.EquippedIds[index], StringComparison.Ordinal);
                _slotCards[index].Bind(candidate, "슬롯 " + (index + 1) +
                    (index == _selectedSlot ? " · 선택 중" : " · 선택하여 변경") +
                    (changed ? " · 적용 대기" : " · 저장됨"), index == _selectedSlot, true, !changed);
            }
            bool hasChanges = HasDraftChanges();
            _preset.text = _snapshot.PresetName + (hasChanges ? " · 변경한 구성을 적용해 주세요" : " · 현재 적용 중인 구성");
            _confirmButton.interactable = hasChanges;
            _restoreButton.interactable = hasChanges;
            _clearButton.interactable = !string.IsNullOrEmpty(_draftIds[_selectedSlot]);
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
                if (!_showOnlyActive || _snapshot.Candidates[index].IsActive)
                    _visibleCandidates.Add(_snapshot.Candidates[index]);
            // 장착 가능한 컬러와 완성에 가까운 컬러를 먼저 보여주되 동률 순서는 고정한다.
            _visibleCandidates.Sort((left, right) =>
            {
                int active = right.IsActive.CompareTo(left.IsActive);
                if (active != 0) return active;
                float leftProgress = (float)left.EligibleCount / left.Definition.RequiredCount;
                float rightProgress = (float)right.EligibleCount / right.Definition.RequiredCount;
                int progress = rightProgress.CompareTo(leftProgress);
                return progress != 0 ? progress : string.CompareOrdinal(left.Id, right.Id);
            });
            float height = Mathf.Max(1f, _visibleCandidates.Count * _candidateRowHeight);
            _candidateContent.sizeDelta = new Vector2(0f, height);
            for (int index = 0; index < _visibleCandidates.Count; index++)
            {
                if (index >= _candidateCards.Count)
                {
                    int rowIndex = index;
                    Button button = OwnerDugoutDetailUiFactory.CreateButton(_candidateContent, "Candidate" + index, string.Empty,
                        0f, 0f, 1f, 1f, () => SelectCandidate(_visibleCandidates[rowIndex]));
                    _candidateCards.Add(OwnerTeamColorCardView.Attach(button));
                }
                OwnerTeamColorCandidateSnapshot candidate = _visibleCandidates[index];
                OwnerTeamColorCardView card = _candidateCards[index];
                card.gameObject.SetActive(true);
                OwnerDugoutDetailUiFactory.Place((RectTransform)card.transform, .01f,
                    1f - (index + 1) * _candidateRowHeight / height + CandidateRowGap / height,
                    .99f, 1f - index * _candidateRowHeight / height);
                string equipped = string.Equals(_draftIds[0], candidate.Id, StringComparison.Ordinal) ? " · 슬롯 1" :
                    string.Equals(_draftIds[1], candidate.Id, StringComparison.Ordinal) ? " · 슬롯 2" : string.Empty;
                card.Bind(candidate, candidate.ProgressText + equipped, ReferenceEquals(candidate, _selectedCandidate), true);
            }
            for (int index = _visibleCandidates.Count; index < _candidateCards.Count; index++)
                _candidateCards[index].gameObject.SetActive(false);
            _empty.gameObject.SetActive(_visibleCandidates.Count == 0);
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            RefreshSlots();
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
            _players.text = candidate == null ? "컬러를 선택하면 해당 효과를 받는 선수들을 확인할 수 있습니다." :
                candidate.EligiblePlayerNames.Count == 0 ? "조건에 맞는 선수가 없습니다." :
                (candidate.IsActive ? string.Empty : "장착 후 적용되는 선수\n") + string.Join("   ·   ", candidate.EligiblePlayerNames);
            RefreshEquipState();
            _hasDetailLayoutChanges = true;
            ResizeDetailContent();
        }

        private void LateUpdate()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return;
            ResizeBoard();
            float rowHeight = Mathf.Max(CandidateRowHeight, _candidateContent.rect.width / CardAspectRatio);
            if (_snapshot != null && Mathf.Abs(_candidateRowHeight - rowHeight) > .5f)
            {
                _candidateRowHeight = rowHeight;
                RebuildCandidates();
            }
            Vector2 size = ((RectTransform)_detail.transform.parent.parent).rect.size;
            if (!_hasDetailLayoutChanges && size == _lastDetailSize) return;
            _lastDetailSize = size;
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

        private void ResizeDetailContent()
        {
            ResizeScrollLabel(_detail);
            ResizeScrollLabel(_players);
        }

        private static void ResizeScrollLabel(Text label)
        {
            if (label == null) return;
            RectTransform content = (RectTransform)label.transform.parent;
            float height = Mathf.Max(((RectTransform)content.parent).rect.height, label.preferredHeight + CareerUiTheme.Space2);
            if (Mathf.Abs(content.sizeDelta.y - height) > .5f)
                content.sizeDelta = new Vector2(0f, height);
        }

        private string GetEquipReason()
        {
            if (_selectedCandidate == null) return "목록에서 팀 컬러를 선택하세요.";
            if (!_selectedCandidate.IsActive) return "필요한 선수를 1군에 등록하면 장착할 수 있습니다.";
            if (string.Equals(_draftIds[_selectedSlot], _selectedCandidate.Id, StringComparison.Ordinal))
                return "선택한 슬롯에 이미 장착되어 있습니다.";
            OwnerTeamColorCandidateSnapshot other = Find(_draftIds[1 - _selectedSlot]);
            if (other == null) return string.Empty;
            if (string.Equals(other.Id, _selectedCandidate.Id, StringComparison.Ordinal))
                return "다른 슬롯에 장착된 팀 컬러입니다.";
            if (other.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                _selectedCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                string.Equals(other.StackGroup, _selectedCandidate.StackGroup, StringComparison.Ordinal))
                return "같은 계열은 한 단계만 장착할 수 있습니다. 다른 슬롯을 선택하세요.";
            return string.Empty;
        }

        private void RefreshEquipState()
        {
            string reason = GetEquipReason();
            _equipButton.interactable = reason.Length == 0;
            _equipButton.transform.Find("Label").GetComponent<Text>().text = "슬롯 " + (_selectedSlot + 1) + "에 장착";
            OwnerTeamColorCandidateSnapshot previous = _snapshot == null ? null : Find(_snapshot.EquippedIds[_selectedSlot]);
            _equipReason.text = reason.Length > 0 ? reason :
                previous == null ? "빈 슬롯에 장착합니다." : "현재 적용: " + previous.Name;
        }

        private void EquipSelected()
        {
            if (GetEquipReason().Length > 0) return;
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
        private const float GradeCenterX = 0.192f;
        private const float TextSafeLeft = 0.34f;

        private RawImage _surface;
        private Image _fallbackSurface;
        private Text _name;
        private Text _grade;
        private Text _meta;
        private Text _state;
        private Outline _selection;

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
            _name.fontStyle = FontStyle.Bold;
            _name.alignment = TextAnchor.MiddleLeft;
            _name.color = Color.white;
            _name.horizontalOverflow = HorizontalWrapMode.Wrap;
            _name.verticalOverflow = VerticalWrapMode.Truncate;
            OwnerDugoutDetailUiFactory.Place(_name.rectTransform, TextSafeLeft, 0.35f, 0.78f, 0.87f);

            _grade = OwnerDugoutDetailUiFactory.CreateLabel(
                transform,
                "Grade",
                "-",
                GradeCenterX - 0.05f,
                0.20f,
                GradeCenterX + 0.05f,
                0.80f,
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _grade.color = Color.white;
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

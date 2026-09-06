using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>두 장착 슬롯과 전체 발동 진행도를 분리해 보여주는 구단주 TeamColor 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerTeamColor : MonoBehaviour, IUiCancelHandler
    {
        private const float CandidateRowHeight = 112f;
        private const float CandidateRowGap = 8f;

        private RectTransform _root;
        private RectTransform _candidateContent;
        private readonly OwnerTeamColorCardView[] _slotCards = new OwnerTeamColorCardView[2];
        private readonly OwnerTeamColorCardView[] _activeSlotCards = new OwnerTeamColorCardView[2];
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

        public event Action<string[]> SelectionConfirmed;

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
            _status.color = isError ? new Color(0.72f, 0.16f, 0.12f) : new Color(0.12f, 0.35f, 0.20f);
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
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerTeamColorWorkspace", true);
            RectTransform slots = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "EquippedSlots", 0.015f, 0.43f, 0.315f, 0.975f);
            RectTransform candidates = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "CandidateList", 0.33f, 0.43f, 0.68f, 0.975f);
            RectTransform active = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "ActiveSlots", 0.695f, 0.43f, 0.985f, 0.975f);
            RectTransform detail = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "DetailPanel", 0.015f, 0.10f, 0.985f, 0.405f);

            OwnerDugoutDetailUiFactory.CreateLabel(slots, "Title", "장착할 팀 컬러", 0.06f, 0.87f, 0.94f, 0.98f, 18, FontStyle.Bold);
            _preset = OwnerDugoutDetailUiFactory.CreateLabel(slots, "Preset", "선택 프리셋", 0.06f, 0.79f, 0.94f, 0.87f, 13);
            for (int index = 0; index < _slotCards.Length; index++)
            {
                int slotIndex = index;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(slots, "Slot" + index, "슬롯 " + (index + 1),
                    0.06f, 0.56f - index * 0.24f, 0.94f, 0.75f - index * 0.24f, () => SelectSlot(slotIndex));
                _slotCards[index] = OwnerTeamColorCardView.Attach(button);
            }
            OwnerDugoutDetailUiFactory.CreateButton(
                slots, "Clear", "선택 슬롯 해제", 0.06f, 0.08f, 0.94f, 0.18f, ClearSelectedSlot);

            OwnerDugoutDetailUiFactory.CreateLabel(candidates, "Title", "사용 가능한 팀 컬러", 0.04f, 0.87f, 0.56f, 0.98f, 18, FontStyle.Bold);
            _allFilterButton = OwnerDugoutDetailUiFactory.CreateButton(candidates, "All", "전체", 0.58f, 0.88f, 0.76f, 0.97f, () => SetFilter(false));
            _activeFilterButton = OwnerDugoutDetailUiFactory.CreateButton(candidates, "Active", "발동", 0.78f, 0.88f, 0.96f, 0.97f, () => SetFilter(true));
            _candidateContent = OwnerDugoutDetailUiFactory.CreateScrollContent(
                candidates, "Scroll", 0.035f, 0.04f, 0.965f, 0.85f, out _candidateScroll);

            OwnerDugoutDetailUiFactory.CreateLabel(active, "Title", "현재 적용 팀 컬러", 0.06f, 0.87f, 0.94f, 0.98f, 18, FontStyle.Bold);
            for (int index = 0; index < _activeSlotCards.Length; index++)
            {
                Button button = OwnerDugoutDetailUiFactory.CreateButton(
                    active,
                    "ActiveSlot" + index,
                    "EMPTY",
                    0.06f,
                    0.56f - index * 0.24f,
                    0.94f,
                    0.75f - index * 0.24f,
                    null);
                button.interactable = false;
                _activeSlotCards[index] = OwnerTeamColorCardView.Attach(button);
            }
            OwnerDugoutDetailUiFactory.CreateLabel(
                active,
                "Rule",
                "결정 전에는 현재 적용 구성이 바뀌지 않습니다.\n같은 팀 컬러와 동일 최고 단계 계열은 중복할 수 없습니다.",
                0.06f,
                0.08f,
                0.94f,
                0.27f,
                12,
                FontStyle.Normal,
                TextAnchor.UpperLeft);

            OwnerDugoutDetailUiFactory.CreateLabel(detail, "Title", "팀 컬러 정보", 0.025f, 0.78f, 0.74f, 0.96f, 18, FontStyle.Bold);
            _detail = OwnerDugoutDetailUiFactory.CreateLabel(detail, "Description", "목록에서 팀컬러를 선택하세요.",
                0.025f, 0.10f, 0.74f, 0.76f, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _equipButton = OwnerDugoutDetailUiFactory.CreateButton(detail, "Equip", "선택 슬롯에 장착",
                0.77f, 0.54f, 0.975f, 0.74f, EquipSelected);
            _equipButton.interactable = false;
            OwnerDugoutDetailUiFactory.CreateButton(
                detail, "Restore", "되돌리기", 0.77f, 0.29f, 0.975f, 0.49f, Restore);
            OwnerDugoutDetailUiFactory.CreateButton(
                detail, "Confirm", "결정", 0.77f, 0.04f, 0.975f, 0.24f, Confirm);

            _status = OwnerDugoutDetailUiFactory.CreateLabel(
                _root, "Status", string.Empty, 0.02f, 0.025f, 0.98f, 0.085f, 13);
        }

        private void RefreshAll()
        {
            if (_snapshot == null) return;
            _preset.text = _snapshot.PresetName + " · 장착할 슬롯을 선택하세요";
            RefreshSlots();
            RefreshActiveSlots();
            SetFilter(false);
            _detail.text = _snapshot.ActiveEffectSummary +
                           "\n\n목록에서 팀컬러를 선택하면 필요 인원, 현재 적용 대상과 개별 효과를 확인할 수 있습니다.";
            _equipButton.interactable = false;
            SetFeedback($"{_snapshot.PresetName} 프리셋 · 변경 전까지 저장되지 않습니다.", false);
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotCards.Length; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = Find(_draftIds[index]);
                _slotCards[index].Bind(candidate, "슬롯 " + (index + 1) + (index == _selectedSlot ? " · 선택 중" : " · 선택하여 변경"), index == _selectedSlot, false);
            }
        }

        private void RefreshActiveSlots()
        {
            for (int index = 0; index < _activeSlotCards.Length; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = Find(_snapshot.EquippedIds[index]);
                _activeSlotCards[index].Bind(candidate, "현재 슬롯 " + (index + 1), false, true);
            }
        }

        private void RebuildCandidates()
        {
            OwnerDugoutDetailUiFactory.ClearChildren(_candidateContent);
            var visible = new List<OwnerTeamColorCandidateSnapshot>();
            for (int index = 0; index < _snapshot.Candidates.Count; index++)
                if (!_showOnlyActive || _snapshot.Candidates[index].IsActive) visible.Add(_snapshot.Candidates[index]);
            float height = Mathf.Max(1f, visible.Count * CandidateRowHeight);
            _candidateContent.sizeDelta = new Vector2(0f, height);
            for (int index = 0; index < visible.Count; index++)
            {
                OwnerTeamColorCandidateSnapshot candidate = visible[index];
                float top = 1f - index * CandidateRowHeight / height;
                float bottom = 1f - (index + 1) * CandidateRowHeight / height + CandidateRowGap / height;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(_candidateContent, "Candidate" + index, string.Empty,
                    0.01f, bottom, 0.99f, top, () => SelectCandidate(candidate));
                OwnerTeamColorCardView card = OwnerTeamColorCardView.Attach(button);
                card.Bind(candidate, candidate.ProgressText, ReferenceEquals(candidate, _selectedCandidate), true);
            }
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            RefreshSlots();
        }

        private void SelectCandidate(OwnerTeamColorCandidateSnapshot candidate)
        {
            _selectedCandidate = candidate;
            string players = candidate.EligiblePlayerNames.Count == 0
                ? "없음"
                : string.Join(", ", candidate.EligiblePlayerNames);
            _detail.text = candidate.Name + "  ·  효과 등급 " + candidate.Grade + "\n" + candidate.Description + "\n\n" +
                           "발동 인원  " + candidate.ProgressText + "\n" +
                           OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(candidate.Definition) + "\n\n" +
                           "적용 대상\n" + players;
            _equipButton.interactable = candidate.IsActive;
            RebuildCandidates();
            if (!candidate.IsActive) SetFeedback("발동 인원을 채우지 못해 현재 장착할 수 없습니다.", true);
        }

        private void EquipSelected()
        {
            if (_selectedCandidate == null || !_selectedCandidate.IsActive) return;
            int other = 1 - _selectedSlot;
            if (string.Equals(_draftIds[other], _selectedCandidate.Id, StringComparison.Ordinal))
            {
                SetFeedback("같은 팀컬러는 두 슬롯에 중복 장착할 수 없습니다.", true);
                return;
            }
            OwnerTeamColorCandidateSnapshot otherCandidate = Find(_draftIds[other]);
            if (otherCandidate != null &&
                otherCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                _selectedCandidate.Definition.StackPolicy == TeamColorStackPolicy.HighestOnly &&
                string.Equals(otherCandidate.StackGroup, _selectedCandidate.StackGroup, StringComparison.Ordinal))
            {
                SetFeedback("같은 최고 단계 전용 계열은 한 단계만 장착할 수 있습니다.", true);
                return;
            }
            _draftIds[_selectedSlot] = _selectedCandidate.Id;
            RefreshSlots();
            SetFeedback("임시 장착했습니다. 결정하면 선택 프리셋에 적용됩니다.", false);
        }

        private void ClearSelectedSlot()
        {
            _draftIds[_selectedSlot] = null;
            RefreshSlots();
            SetFeedback("선택 슬롯을 비웠습니다. 결정 전에는 저장되지 않습니다.", false);
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
            SetFeedback("저장된 팀컬러 구성으로 되돌렸습니다.", false);
        }

        private void Confirm()
        {
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
    internal sealed class OwnerTeamColorCardView : MonoBehaviour
    {
        // 세 생성 자산의 정규화된 상하 투명 여백을 제외한 공통 플레이트 영역이다.
        private static readonly Rect CardPlateUv = new Rect(0f, 0.25f, 1f, 0.50f);
        private const float GradeCenterX = 0.182f;
        private const float TextSafeLeft = 0.34f;

        private RawImage _surface;
        private Image _fallbackSurface;
        private Text _name;
        private Text _grade;
        private Text _meta;
        private Text _state;
        private Outline _selection;

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
            bool showActivationState)
        {
            bool hasCandidate = candidate != null;
            _name.text = hasCandidate ? candidate.Name : "빈 슬롯";
            _grade.text = hasCandidate ? candidate.Grade : "-";
            _meta.text = meta ?? string.Empty;
            _state.text = !hasCandidate || !showActivationState
                ? string.Empty
                : candidate.IsActive ? "발동" : "인원 부족";
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
            _name.fontSize = 13;
            _name.fontStyle = FontStyle.Bold;
            _name.alignment = TextAnchor.MiddleLeft;
            _name.color = Color.white;
            _name.horizontalOverflow = HorizontalWrapMode.Wrap;
            _name.verticalOverflow = VerticalWrapMode.Truncate;
            OwnerDugoutDetailUiFactory.Place(_name.rectTransform, TextSafeLeft, 0.31f, 0.78f, 0.78f);

            _grade = OwnerDugoutDetailUiFactory.CreateLabel(
                transform,
                "Grade",
                "-",
                GradeCenterX - 0.05f,
                0.20f,
                GradeCenterX + 0.05f,
                0.80f,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _grade.color = Color.white;
            _meta = OwnerDugoutDetailUiFactory.CreateLabel(
                transform, "Meta", string.Empty, TextSafeLeft, 0.08f, 0.78f, 0.34f, 12, FontStyle.Normal, TextAnchor.MiddleLeft);
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

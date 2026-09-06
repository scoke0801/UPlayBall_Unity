using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>보유 작전카드의 조건·대상·지속시간과 두 장착 슬롯을 함께 편집하는 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerTactics : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _cardContent;
        private readonly Text[] _slotLabels = new Text[2];
        private Text _detail;
        private Text _status;
        private Button _equipButton;
        private OwnerTacticsSnapshot _snapshot;
        private OwnerTacticCardSnapshot _selectedCard;
        private readonly string[] _draftIds = new string[2];
        private int _draftCount;
        private int _selectedSlot;
        private TacticCardCategory? _category;

        public event Action<string[]> SelectionConfirmed;

        public static UI_Scene_OwnerTactics CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerTactics), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerTactics>();
            view.Build();
            return view;
        }

        public void Bind(OwnerTacticsSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Restore();
            _category = null;
            _selectedCard = null;
            RebuildCards();
            _detail.text = "작전카드를 선택하면 발동 조건, 적용 대상과 지속시간을 확인할 수 있습니다.";
            _equipButton.interactable = false;
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

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerTacticsWorkspace", true);
            RectTransform catalog = OwnerDugoutDetailUiFactory.CreatePanel(_root, "CardCatalog", 0.015f, 0.10f, 0.56f, 0.975f);
            RectTransform loadout = OwnerDugoutDetailUiFactory.CreatePanel(_root, "Loadout", 0.575f, 0.10f, 0.765f, 0.975f);
            RectTransform detail = OwnerDugoutDetailUiFactory.CreatePanel(_root, "Detail", 0.78f, 0.10f, 0.985f, 0.975f);

            OwnerDugoutDetailUiFactory.CreateLabel(catalog, "Title", "보유 작전카드", 0.03f, 0.91f, 0.33f, 0.98f, 20, FontStyle.Bold);
            CreateCategoryButton(catalog, "전체", null, 0.34f, 0.46f);
            CreateCategoryButton(catalog, "야수", TacticCardCategory.Batting, 0.47f, 0.59f);
            CreateCategoryButton(catalog, "투수", TacticCardCategory.Pitching, 0.60f, 0.72f);
            CreateCategoryButton(catalog, "공통", TacticCardCategory.Common, 0.73f, 0.85f);
            CreateCategoryButton(catalog, "분석", TacticCardCategory.Analysis, 0.86f, 0.98f);
            _cardContent = OwnerDugoutDetailUiFactory.CreateScrollContent(catalog, "Scroll", 0.025f, 0.035f, 0.975f, 0.89f, out _);

            OwnerDugoutDetailUiFactory.CreateLabel(loadout, "Title", "경기 기본 장착", 0.07f, 0.90f, 0.93f, 0.98f, 18, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateLabel(loadout, "Preset", "선택 프리셋", 0.07f, 0.84f, 0.93f, 0.90f, 12);
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                int slotIndex = index;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(loadout, "Slot" + index, "슬롯 " + (index + 1),
                    0.07f, 0.63f - index * 0.22f, 0.93f, 0.80f - index * 0.22f, () => SelectSlot(slotIndex));
                _slotLabels[index] = button.transform.Find("Label").GetComponent<Text>();
                _slotLabels[index].fontSize = 12;
            }
            OwnerDugoutDetailUiFactory.CreateButton(loadout, "Clear", "선택 슬롯 해제", 0.07f, 0.30f, 0.93f, 0.37f, ClearSelectedSlot);
            OwnerDugoutDetailUiFactory.CreateLabel(loadout, "Rule", "경기당 최대 2장\n같은 카드 중복 장착 불가\n방해카드는 최대 1장",
                0.07f, 0.10f, 0.93f, 0.27f, 12, FontStyle.Normal, TextAnchor.UpperLeft);

            OwnerDugoutDetailUiFactory.CreateLabel(detail, "Title", "작전 상세", 0.06f, 0.90f, 0.94f, 0.98f, 20, FontStyle.Bold);
            _detail = OwnerDugoutDetailUiFactory.CreateLabel(detail, "Description", "카드를 선택하세요.",
                0.06f, 0.25f, 0.94f, 0.89f, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            _detail.verticalOverflow = VerticalWrapMode.Overflow;
            _equipButton = OwnerDugoutDetailUiFactory.CreateButton(detail, "Equip", "선택 슬롯에 장착",
                0.06f, 0.14f, 0.94f, 0.22f, EquipSelected);
            _equipButton.interactable = false;

            _status = OwnerDugoutDetailUiFactory.CreateLabel(_root, "Status", string.Empty, 0.02f, 0.025f, 0.68f, 0.085f, 13);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Restore", "되돌리기", 0.70f, 0.02f, 0.82f, 0.085f, Restore);
            OwnerDugoutDetailUiFactory.CreateButton(_root, "Confirm", "결정", 0.84f, 0.02f, 0.98f, 0.085f, Confirm);
        }

        private void CreateCategoryButton(Transform parent, string label, TacticCardCategory? category, float left, float right)
        {
            OwnerDugoutDetailUiFactory.CreateButton(parent, "Category" + label, label, left, 0.91f, right, 0.975f,
                () => SetCategory(category));
        }

        private void SetCategory(TacticCardCategory? category)
        {
            _category = category;
            RebuildCards();
        }

        private void RebuildCards()
        {
            if (_snapshot == null) return;
            OwnerDugoutDetailUiFactory.ClearChildren(_cardContent);
            var visible = new List<OwnerTacticCardSnapshot>();
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (!_category.HasValue || _snapshot.Cards[index].Definition.Category == _category.Value) visible.Add(_snapshot.Cards[index]);
            float height = Mathf.Max(1f, visible.Count * 92f);
            _cardContent.sizeDelta = new Vector2(0f, height);
            for (int index = 0; index < visible.Count; index++)
            {
                OwnerTacticCardSnapshot card = visible[index];
                float top = 1f - index * 92f / height;
                float bottom = 1f - (index + 1) * 92f / height + 5f / height;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(_cardContent, "Card" + index,
                    card.Name + "  ×" + card.OwnedCount + "\n" + GetCategoryName(card.Definition.Category) + " · " + GetTierName(card.Definition.TacticTier),
                    0.01f, bottom, 0.99f, top, () => SelectCard(card));
                Text label = button.transform.Find("Label").GetComponent<Text>();
                OwnerDugoutDetailUiFactory.Place(label.rectTransform, 0.22f, 0.04f, 0.98f, 0.96f);
                label.alignment = TextAnchor.MiddleLeft;
                RawImage artwork = TacticCardArtwork.Create(button.transform, "Artwork", card.ArtworkKey, new Color(1f, 1f, 1f, 0.80f));
                OwnerDugoutDetailUiFactory.Place(artwork.rectTransform, 0.02f, 0.08f, 0.20f, 0.92f);
            }
        }

        private void SelectCard(OwnerTacticCardSnapshot card)
        {
            _selectedCard = card;
            string counters = card.Definition.CounterCardIds.Count == 0
                ? "없음"
                : string.Join(", ", card.Definition.CounterCardIds);
            _detail.text = card.Name + "\n" + GetTierName(card.Definition.TacticTier) + " · 보유 " + card.OwnedCount + "장\n\n" +
                           "발동  " + card.TriggerText + "\n\n" +
                           "효과  " + card.EffectText + "\n\n" +
                           card.Definition.ReferenceBehavior + "\n" + card.Definition.ProjectBalanceValue + "\n\n" +
                           "상쇄 대상  " + counters;
            _equipButton.interactable = card.OwnedCount > 0;
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            RefreshSlots();
        }

        private void EquipSelected()
        {
            if (_selectedCard == null || _selectedCard.OwnedCount <= 0) return;
            int other = 1 - _selectedSlot;
            if (other < _draftCount && string.Equals(_draftIds[other], _selectedCard.Id, StringComparison.Ordinal))
            {
                SetFeedback("같은 작전카드는 중복 장착할 수 없습니다.", true);
                return;
            }
            OwnerTacticCardSnapshot otherCard = other < _draftCount ? Find(_draftIds[other]) : null;
            if (_selectedCard.Definition.IsDisruption && otherCard?.Definition.IsDisruption == true)
            {
                SetFeedback("방해 작전카드는 경기당 한 장만 장착할 수 있습니다.", true);
                return;
            }
            _draftIds[_selectedSlot] = _selectedCard.Id;
            _draftCount = Mathf.Max(_draftCount, _selectedSlot + 1);
            CompactDraft();
            RefreshSlots();
            SetFeedback("임시 장착했습니다. 결정하면 선택 프리셋에 적용됩니다.", false);
        }

        private void ClearSelectedSlot()
        {
            if (_selectedSlot < _draftCount) _draftIds[_selectedSlot] = null;
            CompactDraft();
            RefreshSlots();
            SetFeedback("선택 슬롯을 비웠습니다. 결정 전에는 저장되지 않습니다.", false);
        }

        private void CompactDraft()
        {
            if (string.IsNullOrEmpty(_draftIds[0]) && !string.IsNullOrEmpty(_draftIds[1]))
            {
                _draftIds[0] = _draftIds[1];
                _draftIds[1] = null;
            }
            _draftCount = string.IsNullOrEmpty(_draftIds[0]) ? 0 : string.IsNullOrEmpty(_draftIds[1]) ? 1 : 2;
            if (_selectedSlot >= _draftCount && _draftCount < 2) _selectedSlot = _draftCount;
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                OwnerTacticCardSnapshot card = index < _draftCount ? Find(_draftIds[index]) : null;
                string marker = index == _selectedSlot ? "▶ " : string.Empty;
                _slotLabels[index].text = marker + "슬롯 " + (index + 1) + "\n" + (card?.Name ?? "비어 있음");
            }
        }

        private void Restore()
        {
            if (_snapshot == null) return;
            Array.Clear(_draftIds, 0, _draftIds.Length);
            _draftCount = Math.Min(_snapshot.EquippedIds.Count, _draftIds.Length);
            for (int index = 0; index < _draftCount; index++) _draftIds[index] = _snapshot.EquippedIds[index];
            _selectedSlot = 0;
            RefreshSlots();
            SetFeedback($"{_snapshot.PresetName} 프리셋 · 저장된 작전 구성입니다.", false);
        }

        private void Confirm()
        {
            var result = new string[_draftCount];
            for (int index = 0; index < result.Length; index++) result[index] = _draftIds[index];
            SelectionConfirmed?.Invoke(result);
        }

        private OwnerTacticCardSnapshot Find(string id)
        {
            if (string.IsNullOrEmpty(id) || _snapshot == null) return null;
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (string.Equals(_snapshot.Cards[index].Id, id, StringComparison.Ordinal)) return _snapshot.Cards[index];
            return null;
        }

        private static string GetCategoryName(TacticCardCategory value) => value switch
        {
            TacticCardCategory.Batting => "야수",
            TacticCardCategory.Pitching => "투수",
            TacticCardCategory.Analysis => "분석",
            TacticCardCategory.Common => "공통",
            _ => "분류 없음"
        };

        private static string GetTierName(TacticTier value) => value switch
        {
            TacticTier.Normal => "일반",
            TacticTier.Rare => "레어",
            TacticTier.Special => "스페셜",
            TacticTier.Signature => "시그니처",
            _ => "등급 없음"
        };

        private void OnDestroy()
        {
            SelectionConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}

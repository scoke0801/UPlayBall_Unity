using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerSupportCards
    {
        private UIXScrollView _catalogScroll;
        private Text _catalogSummary;
        private Text _catalogEmpty;
        private readonly List<int> _filtered = new List<int>();
        private readonly List<Button[]> _filters = new List<Button[]>();
        private readonly int[] _filterValues = new int[3];
        private GameObject _lastCatalogFocus;
        private const float CatalogRowHeight = 148f;
        private const float CatalogRowGap = 10f;

        private void BuildCatalog(RectTransform parent)
        {
            _catalogSummary = Label(parent, "CatalogSummary", "", 0, .88f, 1, .93f, 15);
            _catalogSummary.color = OwnerDashboardStyle.Muted;
            BuildFilter(parent, 0, new[] { "전체", "팀", "개인" }, .805f, .867f);
            BuildFilter(parent, 1, new[] { "전체", "타자", "투수" }, .73f, .792f);
            BuildFilter(parent, 2, new[] { "전체", "해금", "보유", "잠김" }, .655f, .717f);
            _catalogScroll = UIXScrollView.Create(parent, "CatalogScroll", Vector2.zero, Vector2.zero,
                Vector2.zero, false, true, OwnerDashboardStyle.InsetSurface, OwnerDashboardStyle.Line, OwnerDashboardStyle.Gold);
            OwnerDugoutDetailUiFactory.Place(_catalogScroll.Root, 0, .055f, 1, .637f);
            _catalog = _catalogScroll.Content;
            _catalog.anchorMax = Vector2.one;
            _catalogEmpty = Label(parent, "CatalogEmpty", "조건에 맞는 카드가 없습니다.\n위 필터를 전체로 바꿔 주세요.", .05f, .2f, .95f, .57f, 18);
            Label(parent, "InventoryHint", "리그 진출로 해금 · 강등 후에도 유지", 0, 0, 1, .043f, 14).color = OwnerDashboardStyle.Muted;
        }

        private void BuildFilter(RectTransform parent, int group, string[] names, float bottom, float top)
        {
            var buttons = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int value = i;
                buttons[i] = Button(parent, "Filter" + group + "_" + i, names[i],
                    i / (float)names.Length, bottom, (i + 1f) / names.Length - .015f, top, () =>
                    {
                        _filterValues[group] = value;
                        _confirmation = 0;
                        _targetPage = 0;
                        _catalogScroll.ScrollRect.StopMovement();
                        _catalog.anchoredPosition = Vector2.zero;
                        Refresh();
                    });
                buttons[i].GetComponentInChildren<Text>().fontSize = 15;
            }
            _filters.Add(buttons);
        }

        private bool RefreshCatalogFilter()
        {
            _filtered.Clear();
            int unlockedCount = 0;
            for (int i = 0; i < _definitions.Count; i++)
            {
                var card = _definitions[i];
                bool unlocked = OwnerSupportService.IsUnlocked(_manager.Runtime, card);
                if (unlocked) unlockedCount++;
                if (_filterValues[0] > 0 && (int)card.scope != _filterValues[0] - 1) continue;
                if (_filterValues[1] > 0 && card.target != OwnerSupportTarget.All && (int)card.target != _filterValues[1]) continue;
                if (_filterValues[2] == 1 && !unlocked || _filterValues[2] == 2 && _manager.Runtime.PlayerGrowth.Support.GetCount(card.id) == 0
                    || _filterValues[2] == 3 && unlocked) continue;
                _filtered.Add(i);
            }
            _filtered.Sort((a, b) =>
            {
                int grade = _definitions[a].unlockGrade.CompareTo(_definitions[b].unlockGrade);
                return grade != 0 ? grade : a.CompareTo(b);
            });
            for (int group = 0; group < _filters.Count; group++)
                for (int i = 0; i < _filters[group].Length; i++)
                    OwnerUiButtonSkin.SetSelected(_filters[group][i], _filterValues[group] == i);
            _catalogSummary.text = $"해금 {unlockedCount}/{_definitions.Count}종 · 검색 결과 {_filtered.Count}종";
            bool hasResults = _filtered.Count > 0;
            _catalogEmpty.gameObject.SetActive(!hasResults);
            _targets.gameObject.SetActive(hasResults);
            if (hasResults)
            {
                if (!_filtered.Contains(_selected))
                {
                    _selected = _filtered[0];
                    _cardId = "";
                    _confirmation = 0;
                    _targetPage = 0;
                }
                return true;
            }
            foreach (var button in _cardButtons) button.gameObject.SetActive(false);
            _catalog.sizeDelta = Vector2.zero;
            _selection.text = "선택 가능한 카드가 없습니다";
            _details.text = "목록 필터를 바꾸면 카드와 적용 대상을 확인할 수 있습니다.";
            _cost.text = _active.text = _targetHeading.text = _page.text = "";
            _message.text = "필터를 전체로 바꿔 주세요.";
            _buy.interactable = _equip.interactable = _previous.interactable = _next.interactable = false;
            _cancel.gameObject.SetActive(false);
            return false;
        }

        private void LayoutCatalogRows()
        {
            foreach (var button in _cardButtons) button.gameObject.SetActive(false);
            for (int row = 0; row < _filtered.Count; row++)
            {
                var button = _cardButtons[_filtered[row]];
                button.gameObject.SetActive(true);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(0, CatalogRowHeight);
                rect.anchoredPosition = new Vector2(0, -row * (CatalogRowHeight + CatalogRowGap));
                button.transform.SetAsLastSibling();
            }
            _catalog.sizeDelta = new Vector2(0, Mathf.Max(0, _filtered.Count * (CatalogRowHeight + CatalogRowGap) - CatalogRowGap));
        }

        private void LateUpdate()
        {
            if (_catalogScroll == null || !_root.gameObject.activeInHierarchy) return;
            var focus = EventSystem.current?.currentSelectedGameObject;
            if (focus == _lastCatalogFocus) return;
            _lastCatalogFocus = focus;
            if (focus == null || !focus.transform.IsChildOf(_catalog)) return;
            // 키보드·패드로 마스크 밖 항목에 이동해도 포커스 카드를 항상 화면 안에 둔다.
            var rect = focus.transform as RectTransform;
            if (rect == null) return;
            float top = -rect.anchoredPosition.y;
            float height = _catalogScroll.Viewport.rect.height;
            float offset = _catalog.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + rect.rect.height > offset + height) offset = top + rect.rect.height - height;
            _catalogScroll.ScrollRect.StopMovement();
            _catalog.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, _catalog.rect.height - height)));
        }
    }
}

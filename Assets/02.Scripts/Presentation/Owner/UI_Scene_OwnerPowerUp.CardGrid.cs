using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        // 보유 수와 무관하게 Viewport와 여유 두 행만 유지한다. 목록 선택은 풀을 재생성하지 않는다.
        private sealed class CardGrid
        {
            private readonly RectTransform _content;
            private readonly ScrollRect _scroll;
            private readonly GridLayoutGroup _layout;
            private readonly Action<string> _selected;
            private readonly Action<string> _detail;
            private readonly List<PlayerMiniCardView> _views = new List<PlayerMiniCardView>();
            private IReadOnlyList<OwnerCollectionCardSnapshot> _cards = Array.Empty<OwnerCollectionCardSnapshot>();
            private Func<OwnerCollectionCardSnapshot, OwnerCollectionCardSnapshot> _resolve;
            private string _selectedId;
            private int _first = -1;
            private Vector2 _viewportSize;
            private Vector2 _cellSize;

            public CardGrid(RectTransform content, Action<string> selected, Action<string> detail)
            {
                _content = content;
                _selected = selected;
                _detail = detail;
                _scroll = content.GetComponentInParent<ScrollRect>();
                _layout = content.GetComponent<GridLayoutGroup>();
                _layout.enabled = false;
                content.GetComponent<ContentSizeFitter>().enabled = false;
            }

            public void Bind(IReadOnlyList<OwnerCollectionCardSnapshot> cards, string selectedId,
                Func<OwnerCollectionCardSnapshot, OwnerCollectionCardSnapshot> resolve)
            {
                _cards = cards;
                _resolve = resolve;
                _selectedId = selectedId;
                _first = -1;
                // 비활성 Route는 레이아웃 확정 후 처음 표시할 때 생성한다.
                Refresh();
            }

            public void Select(string cardId)
            {
                string previous = _selectedId;
                _selectedId = cardId;
                for (int slot = 0; slot < _views.Count; slot++)
                {
                    int index = _first + slot;
                    if (!_views[slot].gameObject.activeSelf || index < 0 || index >= _cards.Count) continue;
                    if (_cards[index].CardId == previous || _cards[index].CardId == cardId)
                        BindSlot(slot, index);
                }
            }

            public void Refresh()
            {
                if (!_content.gameObject.activeInHierarchy) return;
                Vector2 viewport = _scroll.viewport.rect.size;
                if (viewport.x <= 0 || viewport.y <= 0) return;
                int columns = _layout.constraintCount;
                Vector2 stride = _layout.cellSize + _layout.spacing;
                int rows = (_cards.Count + columns - 1) / columns;
                float height = _layout.padding.vertical + rows * stride.y - (rows > 0 ? _layout.spacing.y : 0);
                _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                float top = Mathf.Clamp(_content.anchoredPosition.y, 0, Mathf.Max(0, height - viewport.y));
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, top);
                int first = Mathf.Max(0, Mathf.FloorToInt((top - _layout.padding.top) / stride.y) - 1) * columns;
                int capacity = Mathf.Min(_cards.Count, (Mathf.CeilToInt(viewport.y / stride.y) + 3) * columns);
                if (_first == first && _viewportSize == viewport && _cellSize == _layout.cellSize) return;
                int focusedIndex = FindFocusedIndex();
                _first = first;
                _viewportSize = viewport;
                _cellSize = _layout.cellSize;
                while (_views.Count < capacity)
                {
                    int slot = _views.Count;
                    PlayerMiniCardView view = PlayerMiniCardView.CreateRuntime(_content, "PooledCard");
                    view.UseLineupSlotLayout();
                    view.Selected += _ => InvokeSlot(slot, _selected);
                    view.DetailRequested += _ => InvokeSlot(slot, _detail);
                    view.gameObject.AddComponent<UICardGridFocusRelay>().Selected = () => RevealSlot(slot);
                    _views.Add(view);
                }
                float width = columns * stride.x - _layout.spacing.x;
                float left = _layout.padding.left + Mathf.Max(0,
                    (viewport.x - _layout.padding.horizontal - width) * .5f);
                for (int slot = 0; slot < _views.Count; slot++)
                {
                    int index = first + slot;
                    bool active = slot < capacity && index < _cards.Count;
                    _views[slot].gameObject.SetActive(active);
                    if (!active) continue;
                    RectTransform rect = _views[slot].GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = _layout.cellSize;
                    rect.anchoredPosition = new Vector2(left + index % columns * stride.x,
                        -_layout.padding.top - index / columns * stride.y);
                    BindSlot(slot, index);
                }
                if (focusedIndex >= first && focusedIndex < first + capacity && focusedIndex < _cards.Count)
                    EventSystem.current.SetSelectedGameObject(_views[focusedIndex - first].gameObject);
                else if (focusedIndex >= 0)
                    EventSystem.current.SetSelectedGameObject(null);
            }

            private int FindFocusedIndex()
            {
                if (EventSystem.current == null) return -1;
                foreach (PlayerMiniCardView view in _views)
                {
                    if (EventSystem.current.currentSelectedGameObject != view.gameObject) continue;
                    for (int index = 0; index < _cards.Count; index++)
                        if (_cards[index].CardId == view.Model?.PlayerId) return index;
                    break;
                }
                return -1;
            }

            private void RevealSlot(int slot)
            {
                int index = _first + slot;
                if (index < 0 || index >= _cards.Count) return;
                float top = _layout.padding.top + index / _layout.constraintCount *
                    (_layout.cellSize.y + _layout.spacing.y);
                float offset = _content.anchoredPosition.y;
                if (top < offset) offset = top;
                else if (top + _layout.cellSize.y > offset + _scroll.viewport.rect.height)
                    offset = top + _layout.cellSize.y - _scroll.viewport.rect.height;
                _scroll.StopMovement();
                _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, offset);
            }

            private void BindSlot(int slot, int index)
            {
                OwnerCollectionCardSnapshot card = _resolve(_cards[index]);
                _views[slot].name = "Card_" + card.CardId;
                _views[slot].Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card, card.CardId == _selectedId),
                    PlayerPortraitSprites.GetDefault(card.Position));
            }

            private void InvokeSlot(int slot, Action<string> action)
            {
                int index = _first + slot;
                if (index >= 0 && index < _cards.Count) action?.Invoke(_cards[index].CardId);
            }
        }
    }
}

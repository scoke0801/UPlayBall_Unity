using System;
using System.Collections.Generic;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Encyclopedia
{
    /// <summary>도감과 독립 위시 화면을 공용 구단주 스킨으로 표시하고 조회 행만 가상화한다.</summary>
    public sealed partial class UI_Scene_PlayerEncyclopedia : MonoBehaviour, IUiCancelHandler
    {
        private readonly List<EncyclopediaScreenEntry> _visible = new List<EncyclopediaScreenEntry>();
        private readonly List<PlayerMiniCardView> _cards = new List<PlayerMiniCardView>();
        private readonly List<Button> _tabs = new List<Button>();
        private EncyclopediaScreenFilter _filter = new EncyclopediaScreenFilter();
        private EncyclopediaScreenSnapshot _snapshot;
        private RectTransform _workspace, _inspector, _actions, _filterRoot, _viewport, _content, _matrix;
        private ScrollRect _scroll;
        private Text _count, _empty, _detail, _feedback;
        private PlayerMiniCardView _preview;
        private Button _wish, _scout, _openDetail, _otherYears, _otherEditions, _openEncyclopedia, _openWishlist;
        private RectTransform _detailTabs;
        private InputField _search;
        private bool _wishlistOnly;
        private bool _advanced;
        private int _tab, _detailTab, _firstIndex = -1, _columns = 1;
        private Vector2 _viewportSize;
        private EncyclopediaScreenEntry _selected;

        public event Action<string> WishToggleRequested;
        public event Action<string> ScoutRequested;
        public event Action<string> EncyclopediaRequested;
        public event Action WishlistRequested;
        public event Action<string> DetailRequested;

        public int VisibleEntryCount => _visible.Count;
        public int InstantiatedCardCount => _cards.Count;
        public EncyclopediaScreenFilter Filter => _filter;

        /// <summary>셸이 소유한 세 호스트에 도감 또는 위시 전용 인스턴스를 만든다.</summary>
        public static UI_Scene_PlayerEncyclopedia CreateRuntime(RectTransform workspaceHost,
            RectTransform inspectorHost, RectTransform actionHost, bool wishlistOnly = false)
        {
            if (workspaceHost == null || inspectorHost == null || actionHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            var view = new GameObject(nameof(UI_Scene_PlayerEncyclopedia)).AddComponent<UI_Scene_PlayerEncyclopedia>();
            view._wishlistOnly = wishlistOnly;
            view._tab = wishlistOnly ? 1 : 0;
            if (wishlistOnly) view._filter.Sort = 11;
            view.Build(workspaceHost, inspectorHost, actionHost);
            return view;
        }

        /// <summary>검색과 선택을 유지하며 현재 월드의 조회 결과를 바인딩한다.</summary>
        public void Bind(EncyclopediaScreenSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (_snapshot.IsReadOnly && _tab == 2) _tab = 0;
            _tabs[2].gameObject.SetActive(!_wishlistOnly && !snapshot.IsReadOnly);
            _openWishlist.gameObject.SetActive(!_wishlistOnly && !snapshot.IsReadOnly);
            _openEncyclopedia.gameObject.SetActive(_wishlistOnly && !snapshot.IsReadOnly);
            BuildFilters();
            Refresh();
        }

        /// <summary>셸의 전환 시 별도 호스트에 배치된 모든 루트를 함께 숨긴다.</summary>
        public void SetVisible(bool visible)
        {
            _workspace.gameObject.SetActive(visible);
            _inspector.gameObject.SetActive(visible);
            _actions.gameObject.SetActive(visible);
        }

        /// <summary>위시에서 넘어온 정확한 카드를 도감에서 찾고 선택한다.</summary>
        public void SelectCard(string cardId)
        {
            if (_snapshot == null) return;
            _tab = 1;
            _filter = new EncyclopediaScreenFilter();
            BuildFilters();
            Refresh();
            foreach (var entry in _visible)
            {
                if (entry.CardId != cardId) continue;
                _selected = entry;
                int row = _visible.IndexOf(entry) / Math.Max(1, _columns);
                _content.anchoredPosition = new Vector2(0, row * RowHeight);
                RenderCards(true);
                ShowInspector();
                break;
            }
        }

        /// <summary>필터로 제한한 선수 연도·Edition 범위를 ESC로 해제한다.</summary>
        public bool TryHandleCancel()
        {
            if (_filter.PlayerPersonId.Length == 0 && _filter.PlayerSeasonId.Length == 0) return false;
            _filter.PlayerPersonId = _filter.PlayerSeasonId = string.Empty;
            Refresh();
            return true;
        }

        /// <summary>명령 처리 결과만 표시하며 저장 상태는 바꾸지 않는다.</summary>
        public void SetFeedback(string message, bool isError = false)
        {
            _feedback.text = message ?? string.Empty;
            _feedback.color = isError ? CareerUiTheme.Loss : CareerUiTheme.Success;
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionHost)
        {
            _workspace = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "EncyclopediaWorkspace", true);
            var panel = OwnerWorkspaceUiFactory.CreatePanel(_workspace, "EncyclopediaPanel", _wishlistOnly ? "위시리스트 · 영입 목표" : "선수 도감 · 월드 아카이브");
            OwnerRuntimeUiFactory.Stretch(panel.Root, new Vector2(8, 8), new Vector2(-8, -8));
            RectTransform tabRoot = Row(panel.Content, "ViewTabs", 0, 34);
            string[] labels = { "선수", "카드", "수집 현황" };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                Button button = OwnerWorkspaceUiFactory.CreateButton(tabRoot, "ViewTab" + i, labels[i], () =>
                {
                    _tab = index;
                    if (_tab == 0 && _filter.EditionId.Length > 0)
                    {
                        _filter.EditionId = string.Empty;
                        BuildFilters();
                    }
                    Refresh();
                });
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
                button.gameObject.SetActive(!_wishlistOnly);
                _tabs.Add(button);
            }
            _openWishlist = OwnerWorkspaceUiFactory.CreateButton(tabRoot, "OpenWishlist", "★ 위시리스트", () => WishlistRequested?.Invoke());
            _openEncyclopedia = OwnerWorkspaceUiFactory.CreateButton(tabRoot, "OpenEncyclopedia", "선수 도감 열기", () => EncyclopediaRequested?.Invoke(_selected?.CardId ?? string.Empty));
            _filterRoot = OwnerRuntimeUiFactory.CreateRect("Filters", panel.Content);
            Top(_filterRoot, 40, 128);
            BuildScroll(panel.Content);
            _matrix = OwnerRuntimeUiFactory.CreateRect("CollectionMatrix", panel.Content);
            OwnerRuntimeUiFactory.SetAnchors(_matrix, Vector2.zero, Vector2.one, new Vector2(0, 32), new Vector2(0, -42));
            _count = OwnerWorkspaceUiFactory.CreateText(panel.Content, "ResultCount", string.Empty, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
            OwnerRuntimeUiFactory.SetAnchors(_count.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 28));
            BuildInspector(inspectorHost);
            _actions = OwnerWorkspaceUiFactory.CreateRoot(actionHost, "EncyclopediaActions", false);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(_actions, 8).padding = new RectOffset(12, 12, 4, 4);
            _feedback = OwnerWorkspaceUiFactory.CreateText(_actions, "Feedback", "카드를 선택하면 수집 상태와 영입 경로를 확인할 수 있습니다.", 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            OwnerWorkspaceUiFactory.SetFlexible(_feedback.rectTransform, 1, 0);
            _wish = OwnerWorkspaceUiFactory.CreateButton(_actions, "ToggleWish", "☆ 위시 등록", ToggleWish);
            _scout = OwnerWorkspaceUiFactory.CreateButton(_actions, "FindScout", "스카우트에서 찾기", () => { if (_selected != null && !_snapshot.IsReadOnly) ScoutRequested?.Invoke(_selected.CardId); });
            CompactButtons(_workspace);
            CompactButtons(_inspector);
            ShowInspector();
        }

        private void BuildScroll(RectTransform parent)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage("VirtualCardList", parent, CareerUiTheme.ReferencePanel);
            surface.raycastTarget = true;
            OwnerRuntimeUiFactory.SetAnchors(surface.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 32), new Vector2(0, -168));
            _scroll = surface.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 32;
            _viewport = OwnerRuntimeUiFactory.CreateRect("Viewport", surface.transform);
            OwnerRuntimeUiFactory.Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = OwnerRuntimeUiFactory.CreateRect("Content", _viewport);
            _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one; _content.pivot = new Vector2(.5f, 1);
            _content.offsetMin = _content.offsetMax = Vector2.zero;
            _scroll.viewport = _viewport; _scroll.content = _content;
            _scroll.onValueChanged.AddListener(_ => RenderCards(false));
            _empty = OwnerWorkspaceUiFactory.CreateText(_viewport, "EmptyState", string.Empty, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
            OwnerRuntimeUiFactory.Stretch(_empty.rectTransform, new Vector2(20, 20), new Vector2(-20, -20));
        }

        /// <summary>외부 테스트와 명시적 필터 변경 뒤 목록을 다시 조회한다.</summary>
        public void Refresh()
        {
            if (_snapshot == null) return;
            _visible.Clear();
            IReadOnlyList<EncyclopediaScreenEntry> source = _tab == 0 ? _snapshot.Seasons : _snapshot.Cards;
            foreach (var entry in source)
                if ((!_wishlistOnly || entry.IsWishlisted) && _filter.Matches(entry, _snapshot.IsReadOnly)) _visible.Add(entry);
            _visible.Sort(_filter.Compare);
            string selectedId = _selected?.StableId;
            _selected = null;
            foreach (var entry in _visible) if (entry.StableId == selectedId) { _selected = entry; break; }
            _scroll.gameObject.SetActive(_tab != 2);
            _filterRoot.gameObject.SetActive(_tab != 2);
            _matrix.gameObject.SetActive(_tab == 2);
            for (int i = 0; i < _tabs.Count; i++) OwnerUiButtonSkin.SetSelected(_tabs[i], i == _tab);
            _empty.text = _wishlistOnly && CountWishes() == 0
                ? "위시리스트가 비어 있습니다.\n선수 도감에서 아직 보유하지 않은 카드를 ★ 위시에 등록할 수 있습니다.\n상단의 ‘선수 도감 열기’를 눌러 탐색해 보세요."
                : "검색 결과가 없습니다.\n필터를 초기화하거나 검색어를 바꿔 주세요.";
            _empty.gameObject.SetActive(_visible.Count == 0);
            _count.text = BuildCount(source.Count);
            if (_filter.PlayerPersonId.Length > 0) _count.text += "  · 같은 선수의 다른 연도 (ESC 해제)";
            if (_filter.PlayerSeasonId.Length > 0) _count.text += "  · 같은 시즌 Edition (ESC 해제)";
            _openWishlist.transform.Find("Label").GetComponent<Text>().text = "★ 위시리스트 " + CountWishes();
            _content.anchoredPosition = Vector2.zero;
            UpdateGridSize();
            if (_tab == 2) BuildMatrix(); else RenderCards(true);
            ShowInspector();
        }

        private int CountWishes()
        {
            int count = 0;
            foreach (var entry in _snapshot.Cards) if (entry.IsWishlisted) count++;
            return count;
        }

        private string BuildCount(int total)
        {
            if (_snapshot.IsReadOnly) return $"전체 {total:N0} | 조건 {_visible.Count:N0}";
            int owned = 0, acquired = 0, wishes = 0;
            foreach (var entry in _visible) { if (entry.IsCurrentlyOwned) owned++; if (entry.WasEverAcquired) acquired++; if (entry.IsWishlisted) wishes++; }
            return $"전체 {total:N0} | 조건 {_visible.Count:N0} | 보유 {owned:N0} | 획득 {acquired:N0} | 위시 {wishes:N0}";
        }

        private const float RowHeight = PlayerMiniCardView.PreferredHeight + 12;

        private void LateUpdate()
        {
            if (_snapshot == null || !_workspace.gameObject.activeInHierarchy || _tab == 2) return;
            if (_viewport.rect.size == _viewportSize) return;
            UpdateGridSize();
            RenderCards(true);
        }

        private void UpdateGridSize()
        {
            _viewportSize = _viewport.rect.size;
            _columns = Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(1, _viewportSize.x - 16) / (PlayerMiniCardView.PreferredWidth + 12)));
            int rows = Mathf.CeilToInt(_visible.Count / (float)_columns);
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_viewportSize.y, 16 + rows * RowHeight));
            _firstIndex = -1;
        }

        private void RenderCards(bool force)
        {
            if (_snapshot == null || _tab == 2) return;
            int first = Math.Max(0, Mathf.FloorToInt((_content.anchoredPosition.y - 8) / RowHeight)) * _columns;
            if (!force && first == _firstIndex) return;
            _firstIndex = first;
            int capacity = Math.Min(_visible.Count, (Mathf.Max(1, Mathf.CeilToInt(_viewport.rect.height / RowHeight)) + 2) * _columns);
            while (_cards.Count < capacity)
            {
                var card = PlayerMiniCardView.CreateRuntime(_content);
                card.Selected += HandleSelected;
                card.DetailRequested += model => { HandleSelected(model); OpenDetail(); };
                _cards.Add(card);
            }
            float width = Mathf.Max(1, (_viewport.rect.width - 16 - 12 * (_columns - 1)) / _columns);
            for (int i = 0; i < _cards.Count; i++)
            {
                int index = first + i;
                bool active = i < capacity && index < _visible.Count;
                _cards[i].gameObject.SetActive(active);
                if (!active) continue;
                var entry = _visible[index];
                RectTransform rect = _cards[i].GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(8 + index % _columns * (width + 12), -8 - index / _columns * RowHeight);
                rect.sizeDelta = new Vector2(width, PlayerMiniCardView.PreferredHeight);
                _cards[i].Bind(CreateMiniCard(entry, _selected?.StableId == entry.StableId));
                _cards[i].SetTeamIdentity(entry.FranchiseDisplayName);
            }
        }

        private PlayerMiniCardModel CreateMiniCard(EncyclopediaScreenEntry entry, bool selected)
        {
            PlayerMiniCardModel source = entry.MiniCard;
            string status = _snapshot.IsReadOnly ? string.Empty : entry.IsCurrentlyOwned ? "✓ 보유" : entry.WasEverAcquired ? "✓ 획득 · 현재 미보유" : "미획득";
            if (!_snapshot.IsReadOnly && entry.IsWishlisted) status += " · ★ 위시";
            if (string.IsNullOrEmpty(entry.CardId)) status = _snapshot.IsReadOnly ? $"카드 {entry.CollectibleCardCount}종" : $"카드 {entry.CollectibleCardCount}종 · 보유 {entry.OwnedCardCount} · 획득 {entry.EverAcquiredCardCount}";
            return new PlayerMiniCardModel(entry.StableId, entry.DisplayName, source?.PositionLabel ?? entry.Position,
                entry.OriginYear.ToString(), "COST " + entry.Cost, source?.EditionLabel ?? entry.EditionDisplayName, status,
                source?.PortraitAssetKey, source?.TeamAccentHex, selected ? PlayerMiniCardVisualState.Selected : PlayerMiniCardVisualState.Normal,
                true, source?.Stats, source?.FrameEdition, entry.Cost);
        }

        private void HandleSelected(PlayerMiniCardModel model)
        {
            foreach (var entry in _visible) if (entry.StableId == model.PlayerId) { _selected = entry; break; }
            RenderCards(true);
            ShowInspector();
        }

        private void ToggleWish()
        {
            if (_selected == null || _snapshot.IsReadOnly || string.IsNullOrEmpty(_selected.CardId)) return;
            if (_selected.IsWishlisted || !_selected.IsCurrentlyOwned) WishToggleRequested?.Invoke(_selected.CardId);
        }

        private void OpenDetail()
        {
            if (_selected == null) return;
            if (_selected.DetailCard != null) UI_Popup_OwnerPlayerCard.Show(_workspace, _selected.DetailCard);
            else DetailRequested?.Invoke(_selected.CardId);
        }

        private static RectTransform Row(RectTransform parent, string name, float top, float height)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect(name, parent);
            Top(row, top, height);
            var layout = OwnerWorkspaceUiFactory.AddHorizontalLayout(row, 6);
            layout.childForceExpandHeight = true;
            return row;
        }

        private static void Top(RectTransform rect, float top, float height)
        {
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(0, 1), Vector2.one, new Vector2(0, -top - height), new Vector2(0, -top));
        }

        private static void CompactButtons(Transform root)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                LayoutElement layout = button.GetComponent<LayoutElement>();
                if (layout == null) continue;
                layout.minHeight = 26;
                layout.preferredHeight = 30;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>특수 영입의 검색·페이지·선택 복원을 공유하는 한글 전용 드롭다운이다.</summary>
    public sealed class UIRecruitTargetDropdown : MonoBehaviour
    {
        /// <summary>거래 데이터와 분리된 영입 후보의 표시 정보다.</summary>
        public readonly struct Option
        {
            public readonly string Title, Detail;
            public readonly bool IsOwned;
            public Option(string title, string detail, bool isOwned)
            { Title = title; Detail = detail; IsOwned = isOwned; }
        }

        private const float RowHeight = 70;
        private const int MaximumRows = 6;
        private readonly List<int> _filtered = new List<int>();
        private readonly List<Button> _rows = new List<Button>();
        private IReadOnlyList<Option> _options = Array.Empty<Option>();
        private RectTransform _host, _overlay, _sheet;
        private Button _trigger, _previous, _next, _close;
        private Text _caption, _detail, _heading, _count, _pageLabel, _empty;
        private InputField _search;
        private int _value = -1, _page, _pageSize;
        private Vector2 _hostSize;
        public event Action<int> SelectionChanged;
        public event Action<bool> OpenChanged;
        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;
        public int OptionCount => _options.Count;

        /// <summary>목록을 작업 영역 안에 펼치는 선택 버튼을 생성한다.</summary>
        public static UIRecruitTargetDropdown CreateRuntime(Transform parent, RectTransform overlayHost)
        {
            var rect = OwnerRuntimeUiFactory.CreateRect("Target", parent);
            var view = rect.gameObject.AddComponent<UIRecruitTargetDropdown>();
            view._host = overlayHost;
            view.Build();
            return view;
        }

        /// <summary>데이터 갱신은 영입 거래를 실행하지 않고 현재 선택만 복원한다.</summary>
        public void Bind(IReadOnlyList<Option> options, int selectedIndex, string heading)
        {
            Close();
            _options = options ?? Array.Empty<Option>();
            _value = selectedIndex >= 0 && selectedIndex < _options.Count ? selectedIndex : -1;
            _heading.text = heading;
            _trigger.interactable = _options.Count > 0;
            _trigger.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            RefreshCaption();
        }

        /// <summary>영입 완료 직후 현재 대상의 보유 표시를 거래 결과와 맞춘다.</summary>
        public void MarkSelectedOwned()
        {
            if (_value < 0) return;
            var options = new List<Option>(_options);
            Option selected = options[_value];
            options[_value] = new Option(selected.Title, selected.Detail, true);
            _options = options;
            RefreshCaption();
        }

        /// <summary>검색을 초기화하고 현재 선택이 있는 페이지를 연다.</summary>
        public void Open()
        {
            if (!_trigger.IsInteractable() || IsOpen) return;
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            OpenChanged?.Invoke(true);
            _search.SetTextWithoutNotify(string.Empty);
            PositionSheet();
            Filter(string.Empty);
            _page = Mathf.Max(0, _value) / _pageSize;
            RefreshRows();
            if (_value >= 0) _rows[_value % _pageSize].Select();
            else _search.Select();
        }

        /// <summary>취소·외부 클릭·화면 전환 시 목록을 닫고 선택 버튼에 포커스를 돌린다.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            _overlay.gameObject.SetActive(false);
            OpenChanged?.Invoke(false);
            if (_trigger != null && _trigger.gameObject.activeInHierarchy) _trigger.Select();
        }

        private void OnDisable() => Close();
        private void OnDestroy()
        {
            if (_overlay == null) return;
            if (Application.isPlaying) Destroy(_overlay.gameObject);
            else DestroyImmediate(_overlay.gameObject);
        }

        private void LateUpdate()
        {
            if (!IsOpen || _hostSize == _host.rect.size) return;
            PositionSheet();
            _page = Mathf.Min(_page, Mathf.Max(0, (_filtered.Count - 1) / _pageSize));
            RefreshRows();
        }

        private void Build()
        {
            _trigger = CreateButton("SelectTarget", transform, "영입 대상 선택", Open);
            OwnerRuntimeUiFactory.Stretch((RectTransform)_trigger.transform);
            _trigger.GetComponentInChildren<Text>().gameObject.SetActive(false);
            _caption = CreateText("SelectedName", _trigger.transform, "영입 대상 없음", 18, OwnerDashboardStyle.Ivory);
            Top(_caption.rectTransform, 12, 4, 42, 30);
            _detail = CreateText("SelectedDetail", _trigger.transform, "등록된 선수카드가 없습니다", 13, OwnerDashboardStyle.Ivory);
            Top(_detail.rectTransform, 12, 34, 42, 26);
            var arrow = CreateText("Expand", _trigger.transform, "▾", 20, OwnerDashboardStyle.Ivory);
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.rectTransform.anchorMin = new Vector2(1, 0);
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.offsetMin = new Vector2(-40, 0);
            arrow.rectTransform.offsetMax = Vector2.zero;

            _overlay = OwnerRuntimeUiFactory.CreateRect("TargetDropdown", _host);
            OwnerRuntimeUiFactory.Stretch(_overlay);
            _overlay.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            var blocker = CreateButton("Outside", _overlay, string.Empty, Close);
            OwnerRuntimeUiFactory.Stretch((RectTransform)blocker.transform);
            blocker.GetComponent<Image>().color = CareerUiTheme.InputBlocker;
            blocker.transition = Selectable.Transition.None;
            blocker.navigation = new Navigation { mode = Navigation.Mode.None };
            _sheet = OwnerRuntimeUiFactory.CreateImage("Sheet", _overlay, CareerUiTheme.ReferencePanel).rectTransform;
            _sheet.GetComponent<Image>().raycastTarget = true;
            UIOwnerFrontOfficePanel.Apply(_sheet, "ManagerReport");
            var art = OwnerRuntimeUiFactory.CreateRect("HallOfFame", _sheet).gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("UI/SpecialRecruit/recruit_selection_header_v1");
            art.color = art.texture != null ? Color.white : CareerUiTheme.TopBar;
            art.raycastTarget = false;
            Top(art.rectTransform, 1, 1, 1, 88);
            _heading = CreateText("Heading", _sheet, "영입 대상 선택", 23, CareerUiTheme.TextPrimary);
            Top(_heading.rectTransform, 20, 10, 100, 36);
            var hint = CreateText("Hint", _sheet, "선수카드를 선택해 필요한 재료를 확인하세요", 14, CareerUiTheme.TextPrimary);
            Top(hint.rectTransform, 20, 49, 80, 28);
            _close = CreateButton("CloseList", _sheet, "닫기", Close);
            Top((RectTransform)_close.transform, 0, 15, 16, 36);
            _close.GetComponent<RectTransform>().anchorMin = new Vector2(1, 1);
            _close.GetComponent<RectTransform>().offsetMin = new Vector2(-78, -51);
            BuildSearch();
            _count = CreateText("ResultCount", _sheet, string.Empty, 14, OwnerDashboardStyle.Ivory);
            Top(_count.rectTransform, 20, 148, 20, 28);
            for (int i = 0; i < MaximumRows; i++)
            {
                int slot = i;
                var row = CreateButton("Option" + i, _sheet, "영입 후보", () => Choose(slot));
                row.GetComponentInChildren<Text>().gameObject.SetActive(false);
                Top((RectTransform)row.transform, 12, 180 + i * RowHeight, 12, RowHeight - 4);
                var title = CreateText("Name", row.transform, string.Empty, 18, OwnerDashboardStyle.Ivory);
                Top(title.rectTransform, 14, 3, 118, 31);
                var detail = CreateText("Detail", row.transform, string.Empty, 14, OwnerDashboardStyle.Ivory);
                Top(detail.rectTransform, 14, 34, 118, 28);
                var state = CreateText("State", row.transform, string.Empty, 14, OwnerDashboardStyle.Gold);
                state.alignment = TextAnchor.MiddleRight;
                Top(state.rectTransform, 0, 6, 14, 54);
                state.rectTransform.anchorMin = new Vector2(1, 1);
                state.rectTransform.offsetMin = new Vector2(-112, -60);
                _rows.Add(row);
            }
            _empty = CreateText("Empty", _sheet, "검색 결과가 없습니다.\n이름·연도·구단을 다시 확인하세요.", 18, OwnerDashboardStyle.Ivory);
            Top(_empty.rectTransform, 20, 184, 20, 80);
            _empty.alignment = TextAnchor.MiddleCenter;
            _previous = CreateButton("PreviousPage", _sheet, "이전", () => ChangePage(-1));
            _next = CreateButton("NextPage", _sheet, "다음", () => ChangePage(1));
            _pageLabel = CreateText("Page", _sheet, string.Empty, 14, OwnerDashboardStyle.Ivory);
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            _search.onValueChanged.AddListener(Filter);
            _overlay.gameObject.SetActive(false);
        }

        private void BuildSearch()
        {
            var surface = OwnerRuntimeUiFactory.CreateImage("Search", _sheet, Color.white);
            surface.raycastTarget = true;
            Top(surface.rectTransform, 16, 98, 100, 44);
            _search = surface.gameObject.AddComponent<InputField>();
            _search.targetGraphic = surface;
            var text = CreateText("Text", surface.transform, string.Empty, 16, OwnerDashboardStyle.Ivory);
            Top(text.rectTransform, 12, 4, 12, 36);
            text.supportRichText = false;
            var placeholder = CreateText("Placeholder", surface.transform, "선수명 · 연도 · 구단 검색", 15, OwnerDashboardStyle.Gold);
            Top(placeholder.rectTransform, 12, 4, 12, 36);
            _search.textComponent = text;
            _search.placeholder = placeholder;
            _search.characterLimit = 64;
            OwnerDashboardStyle.SetDataInput(_search);
            var clear = CreateButton("ClearSearch", _sheet, "초기화", () => { _search.text = string.Empty; _search.Select(); });
            Top((RectTransform)clear.transform, 0, 98, 16, 44);
            var rect = (RectTransform)clear.transform;
            rect.anchorMin = new Vector2(1, 1);
            rect.offsetMin = new Vector2(-92, -142);
        }

        private void PositionSheet()
        {
            Canvas.ForceUpdateCanvases();
            _hostSize = _host.rect.size;
            float width = Mathf.Min(660, _hostSize.x - 24);
            _pageSize = Mathf.Clamp(Mathf.FloorToInt((_hostSize.y - 256) / RowHeight), 1, MaximumRows);
            _pageSize = Mathf.Min(_pageSize, Mathf.Max(1, _options.Count));
            float height = 232 + _pageSize * RowHeight;
            // 작은 타깃 패널의 마스크·축소 배율을 상속하지 않고 작업 영역 안에 배치한다.
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            Vector3 origin = _host.InverseTransformPoint(corners[0]);
            Rect bounds = _host.rect;
            float left = Mathf.Clamp(origin.x, bounds.xMin + 12, bounds.xMax - width - 12);
            float top = Mathf.Clamp(origin.y - 4, bounds.yMin + height + 12, bounds.yMax - 12);
            _sheet.anchorMin = _sheet.anchorMax = new Vector2(.5f, .5f);
            _sheet.pivot = new Vector2(0, 1);
            _sheet.sizeDelta = new Vector2(width, height);
            _sheet.anchoredPosition = new Vector2(left - bounds.center.x, top - bounds.center.y);
            var art = _sheet.Find("HallOfFame").GetComponent<RawImage>();
            if (art.texture != null)
            {
                float crop = Mathf.Clamp01((float)art.texture.width / art.texture.height * 88 / width);
                art.uvRect = new Rect(0, (1 - crop) * .5f, 1, crop);
            }
            float footer = 184 + _pageSize * RowHeight;
            Top((RectTransform)_previous.transform, 16, footer, width - 106, 36);
            Top((RectTransform)_next.transform, width - 106, footer, 16, 36);
            Top(_pageLabel.rectTransform, 115, footer, 115, 36);
        }

        private void Filter(string query)
        {
            query = (query ?? string.Empty).Trim();
            _filtered.Clear();
            for (int i = 0; i < _options.Count; i++)
                if (query.Length == 0 || _options[i].Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    _options[i].Detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    _filtered.Add(i);
            _page = 0;
            RefreshRows();
        }

        private void RefreshRows()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / _pageSize));
            _count.text = "영입 후보 " + _filtered.Count + "명 · 전체 " + _options.Count + "명";
            _pageLabel.text = (_page + 1) + " / " + pages;
            _previous.interactable = _page > 0;
            _next.interactable = _page + 1 < pages;
            _empty.gameObject.SetActive(_filtered.Count == 0);
            for (int i = 0; i < _rows.Count; i++)
            {
                int offset = _page * _pageSize + i;
                var row = _rows[i];
                bool visible = i < _pageSize && offset < _filtered.Count;
                row.gameObject.SetActive(visible);
                if (!visible) continue;
                int index = _filtered[offset];
                Option option = _options[index];
                row.transform.Find("Name").GetComponent<Text>().text = option.Title;
                row.transform.Find("Detail").GetComponent<Text>().text = option.Detail;
                row.transform.Find("State").GetComponent<Text>().text = (index == _value ? "선택됨\n" : "") +
                    (option.IsOwned ? "보유 중" : "미보유");
                OwnerDashboardStyle.SetDataRow(row, index == _value, i % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
                Color ink = OwnerDashboardStyle.Ivory;
                row.transform.Find("Name").GetComponent<Text>().color = ink;
                row.transform.Find("Detail").GetComponent<Text>().color = OwnerDashboardStyle.TableSecondary;
                row.transform.Find("State").GetComponent<Text>().color = index == _value
                    ? CareerUiTheme.Number : OwnerDashboardStyle.Gold;
            }
            LinkNavigation();
        }

        private void LinkNavigation()
        {
            var controls = new List<Selectable> { _close, _search, _sheet.Find("ClearSearch").GetComponent<Button>() };
            foreach (var row in _rows) if (row.gameObject.activeSelf) controls.Add(row);
            if (_previous.interactable) controls.Add(_previous);
            if (_next.interactable) controls.Add(_next);
            for (int i = 0; i < controls.Count; i++)
                controls[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = controls[(i + controls.Count - 1) % controls.Count],
                    selectOnDown = controls[(i + 1) % controls.Count],
                    selectOnLeft = controls[(i + controls.Count - 1) % controls.Count],
                    selectOnRight = controls[(i + 1) % controls.Count] };
        }

        private void ChangePage(int direction)
        {
            _page = Mathf.Clamp(_page + direction, 0, Mathf.Max(0, (_filtered.Count - 1) / _pageSize));
            RefreshRows();
            if (_filtered.Count > 0) _rows[0].Select();
        }

        private void Choose(int slot)
        {
            int index = _filtered[_page * _pageSize + slot];
            bool changed = _value != index;
            _value = index;
            RefreshCaption();
            Close();
            // 같은 대상의 재선택으로 등록해 둔 재료를 지우지 않는다.
            if (changed) SelectionChanged?.Invoke(index);
        }

        private void RefreshCaption()
        {
            _caption.text = _value < 0 ? "영입 대상 없음" : _options[_value].Title;
            _detail.text = _value < 0 ? "등록된 선수카드가 없습니다" : _options[_value].Detail +
                (_options[_value].IsOwned ? " · 보유 중" : " · 미보유");
        }

        private static Text CreateText(string name, Transform parent, string value, int size, Color color)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Normal, TextAnchor.MiddleLeft, color);
            OwnerDashboardStyle.SetDataText(text, size >= 18);
            text.color = color;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = size - 2;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, null);
            button.onClick.AddListener(action);
            return button;
        }

        private static void Top(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}

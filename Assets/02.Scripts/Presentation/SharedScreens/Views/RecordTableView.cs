using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 대량 기록에서 viewport에 필요한 Row만 재사용하며 정렬과 Stable ID 선택을 제공하는 uGUI 기록표다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RecordTableView : MonoBehaviour
    {
        public const float DefaultHeaderHeight = 42f;
        public const float DefaultRowHeight = 36f;
        public const int DefaultOverscanRows = 2;

        private const float ScrollbarThickness = 11f;
        private const float MinimumColumnWidthPerWeight = 92f;
        private const float FallbackViewportHeight = 320f;
        private const float FallbackViewportWidth = 960f;

        private static Font _defaultFont;

        private readonly List<PooledRow> _rowPool = new();
        private RectTransform _tableRoot;
        private RectTransform _headerViewport;
        private RectTransform _headerContent;
        private RectTransform _bodyViewport;
        private RectTransform _content;
        private ScrollRect _scrollRect;
        private Scrollbar _horizontalScrollbar;
        private Scrollbar _verticalScrollbar;
        private GameObject _stateRoot;
        private Text _stateTitle;
        private Text _stateMessage;
        private Button _stateActionButton;
        private Text _stateActionLabel;
        private RecordTableModel _model;
        private UiContentStateModel _contentState;
        private string _columnSignature = string.Empty;
        private string _selectedRowId = string.Empty;
        private float _contentWidth;
        private int _firstRenderedRowIndex;

        /// <summary>사용자가 Row를 선택하면 Stable Row ID를 전달한다.</summary>
        public event Action<string> RowSelected;

        /// <summary>Header 정렬 결과가 바뀌면 열 ID와 방향을 전달한다.</summary>
        public event Action<string, RecordSortDirection> SortChanged;

        /// <summary>Empty 또는 Error 상태의 선택 행동을 요청하면 Action ID를 전달한다.</summary>
        public event Action<string> StateActionRequested;

        /// <summary>현재 표시 중인 정렬 결과 모델이다.</summary>
        public RecordTableModel Model => _model;

        /// <summary>현재 콘텐츠 상태다.</summary>
        public UiContentStateModel ContentState => _contentState;

        /// <summary>정렬이나 재바인딩 이후에도 유지되는 선택 Row의 Stable ID다.</summary>
        public string SelectedRowId => _selectedRowId;

        /// <summary>현재 풀에 생성된 재사용 Row 수다.</summary>
        public int CreatedRowViewCount => _rowPool.Count;

        /// <summary>현재 viewport 부근에 연결된 첫 데이터 Row 인덱스다.</summary>
        public int FirstRenderedRowIndex => _firstRenderedRowIndex;

        /// <summary>테스트와 화면 구성에서 Scroll 위치를 제어할 수 있는 uGUI ScrollRect다.</summary>
        public ScrollRect ScrollRect => _scrollRect;

        /// <summary>부모 전체를 채우는 대량 기록표를 런타임 생성한다.</summary>
        public static RecordTableView CreateRuntime(
            Transform parent,
            string objectName = "RecordTable")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var tableObject = new GameObject(objectName, typeof(RectTransform));
            tableObject.transform.SetParent(parent, false);
            Stretch(tableObject.GetComponent<RectTransform>());
            return tableObject.AddComponent<RecordTableView>();
        }

        /// <summary>부모 안의 지정 크기와 위치에 대량 기록표를 런타임 생성한다.</summary>
        public static RecordTableView CreateRuntime(
            Transform parent,
            Vector2 size,
            Vector2 anchoredPosition,
            string objectName = "RecordTable")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (size.x <= 0f || size.y <= DefaultHeaderHeight + ScrollbarThickness)
                throw new ArgumentOutOfRangeException(nameof(size), "기록표 크기는 Header와 Scrollbar를 포함할 수 있어야 합니다.");

            var tableObject = new GameObject(objectName, typeof(RectTransform));
            tableObject.transform.SetParent(parent, false);
            RectTransform rect = tableObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return tableObject.AddComponent<RecordTableView>();
        }

        /// <summary>준비가 끝난 기록표를 표시하고 기존에 유효한 Stable ID 선택을 유지한다.</summary>
        public void Bind(RecordTableModel model)
        {
            Bind(model, UiContentStateModel.Ready);
        }

        /// <summary>기록표와 Loading, Empty, Error 상태를 함께 표시한다.</summary>
        public void Bind(
            RecordTableModel model,
            UiContentStateModel contentState,
            string preferredSelectedRowId = null)
        {
            _contentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            if (_contentState.Kind == UiContentStateKind.Ready && model == null)
                throw new ArgumentNullException(nameof(model), "Ready 상태에는 기록표 모델이 필요합니다.");

            EnsureHierarchy();
            _model = model;
            bool isReady = _contentState.Kind == UiContentStateKind.Ready;
            _tableRoot.gameObject.SetActive(isReady);
            _stateRoot.SetActive(!isReady);
            if (!isReady)
            {
                RenderState();
                return;
            }

            ResolveSelection(preferredSelectedRowId);
            string signature = BuildColumnSignature(_model.Columns);
            if (!string.Equals(_columnSignature, signature, StringComparison.Ordinal))
            {
                _columnSignature = signature;
                ClearRowPool();
            }

            ConfigureContentSize();
            RebuildHeaders();
            EnsureRowPool();
            _content.anchoredPosition = Vector2.zero;
            _scrollRect.horizontalNormalizedPosition = 0f;
            _scrollRect.verticalNormalizedPosition = 1f;
            RefreshVisibleRows();
        }

        /// <summary>Stable Row ID가 존재하면 선택하고 필요할 때 해당 Row가 보이도록 이동한다.</summary>
        public bool TrySelectRow(string rowId, bool bringIntoView = false)
        {
            if (_model == null || string.IsNullOrWhiteSpace(rowId))
                return false;

            int rowIndex = FindRowIndex(rowId);
            if (rowIndex < 0)
                return false;

            _selectedRowId = rowId;
            if (bringIntoView)
                BringRowIntoView(rowIndex);
            RefreshVisibleRows();
            return true;
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_tableRoot == null || _model == null || _contentState?.Kind != UiContentStateKind.Ready)
                return;

            ConfigureContentSize();
            RebuildHeaders();
            EnsureRowPool();
            RefreshVisibleRows();
        }

        private void EnsureHierarchy()
        {
            if (_tableRoot != null)
                return;

            RectTransform root = GetComponent<RectTransform>();
            Image background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = CareerUiTheme.PanelDark;
            background.raycastTarget = false;
            AddOutline(background);

            _tableRoot = CreateRect("Table", root);
            Stretch(_tableRoot);

            _headerViewport = CreateRect("HeaderViewport", _tableRoot);
            SetAnchors(
                _headerViewport,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0f, -DefaultHeaderHeight),
                new Vector2(-ScrollbarThickness, 0f));
            Image headerMaskImage = _headerViewport.gameObject.AddComponent<Image>();
            headerMaskImage.color = CareerUiTheme.Panel;
            headerMaskImage.raycastTarget = true;
            _headerViewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _headerContent = CreateTopLeftRect("Header", _headerViewport);

            _bodyViewport = CreateRect("Viewport", _tableRoot);
            SetAnchors(
                _bodyViewport,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, ScrollbarThickness),
                new Vector2(-ScrollbarThickness, -DefaultHeaderHeight));
            Image viewportImage = _bodyViewport.gameObject.AddComponent<Image>();
            viewportImage.color = CareerUiTheme.PanelDark;
            viewportImage.raycastTarget = true;
            _bodyViewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _content = CreateTopLeftRect("Content", _bodyViewport);
            _scrollRect = _tableRoot.gameObject.AddComponent<ScrollRect>();
            _scrollRect.viewport = _bodyViewport;
            _scrollRect.content = _content;
            _scrollRect.horizontal = true;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.12f;
            _scrollRect.scrollSensitivity = DefaultRowHeight;
            _scrollRect.onValueChanged.AddListener(HandleScrollChanged);

            _horizontalScrollbar = CreateScrollbar(
                _tableRoot,
                "HorizontalScrollbar",
                Scrollbar.Direction.LeftToRight);
            RectTransform horizontalRect = (RectTransform)_horizontalScrollbar.transform;
            SetAnchors(
                horizontalRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(-ScrollbarThickness, ScrollbarThickness));
            _scrollRect.horizontalScrollbar = _horizontalScrollbar;
            _scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            _verticalScrollbar = CreateScrollbar(
                _tableRoot,
                "VerticalScrollbar",
                Scrollbar.Direction.BottomToTop);
            RectTransform verticalRect = (RectTransform)_verticalScrollbar.transform;
            SetAnchors(
                verticalRect,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-ScrollbarThickness, ScrollbarThickness),
                new Vector2(0f, -DefaultHeaderHeight));
            _scrollRect.verticalScrollbar = _verticalScrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            BuildState(root);
        }

        private void BuildState(RectTransform root)
        {
            RectTransform state = CreateRect("ContentState", root);
            Stretch(state);
            Image surface = state.gameObject.AddComponent<Image>();
            surface.color = CareerUiTheme.PanelDark;
            surface.raycastTarget = false;
            _stateRoot = state.gameObject;

            _stateTitle = CreateText(
                "Title", state, 20, FontStyle.Bold, TextAnchor.LowerCenter, CareerUiTheme.TextPrimary);
            SetAnchors(
                _stateTitle.rectTransform,
                new Vector2(0.1f, 0.5f),
                new Vector2(0.9f, 0.66f),
                Vector2.zero,
                Vector2.zero);
            _stateMessage = CreateText(
                "Message", state, 14, FontStyle.Normal, TextAnchor.UpperCenter, CareerUiTheme.TextSecondary);
            SetAnchors(
                _stateMessage.rectTransform,
                new Vector2(0.1f, 0.35f),
                new Vector2(0.9f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            RectTransform actionRect = CreateRect("Action", state);
            actionRect.anchorMin = actionRect.anchorMax = new Vector2(0.5f, 0.23f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.sizeDelta = new Vector2(180f, 38f);
            Image actionImage = actionRect.gameObject.AddComponent<Image>();
            actionImage.color = CareerUiTheme.PrimaryAction;
            _stateActionButton = actionRect.gameObject.AddComponent<Button>();
            _stateActionButton.targetGraphic = actionImage;
            _stateActionButton.onClick.AddListener(HandleStateAction);
            _stateActionLabel = CreateText(
                "Label", actionRect, 14, FontStyle.Bold, TextAnchor.MiddleCenter, CareerUiTheme.TextPrimary);
            Stretch(_stateActionLabel.rectTransform);
            _stateRoot.SetActive(false);
        }

        private void ConfigureContentSize()
        {
            float viewportWidth = ResolveViewportWidth();
            float viewportHeight = ResolveViewportHeight();
            float totalWeight = 0f;
            for (int i = 0; i < _model.Columns.Count; i++)
                totalWeight += _model.Columns[i].WidthWeight;

            _contentWidth = Mathf.Max(viewportWidth, totalWeight * MinimumColumnWidthPerWeight);
            float contentHeight = Mathf.Max(viewportHeight, _model.Rows.Count * DefaultRowHeight);
            _content.sizeDelta = new Vector2(_contentWidth, contentHeight);
            _headerContent.sizeDelta = new Vector2(_contentWidth, DefaultHeaderHeight);

            bool canScrollHorizontally = _contentWidth > viewportWidth + 0.5f;
            bool canScrollVertically = contentHeight > viewportHeight + 0.5f;
            _scrollRect.horizontal = canScrollHorizontally;
            _scrollRect.vertical = canScrollVertically;
            _horizontalScrollbar.gameObject.SetActive(canScrollHorizontally);
            _verticalScrollbar.gameObject.SetActive(canScrollVertically);
        }

        private void RebuildHeaders()
        {
            ClearChildren(_headerContent);
            float left = 0f;
            float totalWeight = CalculateTotalColumnWeight();
            for (int i = 0; i < _model.Columns.Count; i++)
            {
                RecordTableColumnModel column = _model.Columns[i];
                float width = _contentWidth * column.WidthWeight / totalWeight;
                RectTransform rect = CreateTopLeftRect(column.ColumnId, _headerContent);
                SetTopLeftRect(rect, left, 0f, width, DefaultHeaderHeight);
                left += width;

                Image image = rect.gameObject.AddComponent<Image>();
                bool isSorted = string.Equals(_model.SortedColumnId, column.ColumnId, StringComparison.Ordinal);
                image.color = isSorted ? CareerUiTheme.SurfaceSelected : CareerUiTheme.Panel;
                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.interactable = column.IsSortable;

                string marker = isSorted
                    ? _model.SortDirection == RecordSortDirection.Ascending ? " ▲" : " ▼"
                    : string.Empty;
                Text label = CreateText(
                    "Label",
                    rect,
                    13,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    isSorted ? CareerUiTheme.PrimaryBright : CareerUiTheme.TextSecondary);
                label.text = column.DisplayName + marker;
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(6f, 0f);
                label.rectTransform.offsetMax = new Vector2(-6f, 0f);
                if (column.IsSortable)
                {
                    string columnId = column.ColumnId;
                    button.onClick.AddListener(() => HandleSort(columnId));
                }
            }

            SyncHeaderPosition();
        }

        private void EnsureRowPool()
        {
            int required = Mathf.Min(_model.Rows.Count, CalculateRequiredPoolSize());
            while (_rowPool.Count < required)
                _rowPool.Add(new PooledRow(this, _content, _model.Columns));

            for (int i = required; i < _rowPool.Count; i++)
                _rowPool[i].SetActive(false);
        }

        private int CalculateRequiredPoolSize()
        {
            int visibleRows = Mathf.Max(1, Mathf.CeilToInt(ResolveViewportHeight() / DefaultRowHeight));
            return visibleRows + DefaultOverscanRows * 2;
        }

        private void RefreshVisibleRows()
        {
            if (_model == null || _contentState?.Kind != UiContentStateKind.Ready)
                return;

            float scrollOffset = Mathf.Max(0f, _content.anchoredPosition.y);
            int first = Mathf.Max(0, Mathf.FloorToInt(scrollOffset / DefaultRowHeight) - DefaultOverscanRows);
            int maxFirst = Mathf.Max(0, _model.Rows.Count - _rowPool.Count);
            _firstRenderedRowIndex = Mathf.Min(first, maxFirst);

            for (int poolIndex = 0; poolIndex < _rowPool.Count; poolIndex++)
            {
                int rowIndex = _firstRenderedRowIndex + poolIndex;
                if (rowIndex >= _model.Rows.Count)
                {
                    _rowPool[poolIndex].SetActive(false);
                    continue;
                }

                _rowPool[poolIndex].Bind(
                    _model.Rows[rowIndex],
                    rowIndex,
                    _contentWidth,
                    string.Equals(_selectedRowId, _model.Rows[rowIndex].RowId, StringComparison.Ordinal));
            }
        }

        private void HandleScrollChanged(Vector2 _)
        {
            SyncHeaderPosition();
            RefreshVisibleRows();
        }

        private void SyncHeaderPosition()
        {
            if (_headerContent != null && _content != null)
                _headerContent.anchoredPosition = new Vector2(_content.anchoredPosition.x, 0f);
        }

        private void HandleSort(string columnId)
        {
            RecordTableColumnModel column = FindColumn(columnId);
            if (column == null)
                return;

            RecordSortDirection direction = column.DefaultDirection;
            if (string.Equals(_model.SortedColumnId, columnId, StringComparison.Ordinal))
            {
                direction = _model.SortDirection == RecordSortDirection.Descending
                    ? RecordSortDirection.Ascending
                    : RecordSortDirection.Descending;
            }

            _model = _model.SortBy(columnId, direction);
            RebuildHeaders();
            RefreshVisibleRows();
            SortChanged?.Invoke(columnId, direction);
        }

        private void HandleRowSelected(PooledRow row)
        {
            if (row == null || string.IsNullOrEmpty(row.RowId))
                return;

            _selectedRowId = row.RowId;
            RefreshVisibleRows();
            RowSelected?.Invoke(_selectedRowId);
        }

        private void ResolveSelection(string preferredSelectedRowId)
        {
            if (!string.IsNullOrWhiteSpace(preferredSelectedRowId) && FindRowIndex(preferredSelectedRowId) >= 0)
            {
                _selectedRowId = preferredSelectedRowId;
                return;
            }

            if (string.IsNullOrEmpty(_selectedRowId) || FindRowIndex(_selectedRowId) < 0)
                _selectedRowId = string.Empty;
        }

        private void BringRowIntoView(int rowIndex)
        {
            float viewportHeight = ResolveViewportHeight();
            float currentTop = Mathf.Max(0f, _content.anchoredPosition.y);
            float rowTop = rowIndex * DefaultRowHeight;
            float rowBottom = rowTop + DefaultRowHeight;
            float targetTop = currentTop;
            if (rowTop < currentTop)
                targetTop = rowTop;
            else if (rowBottom > currentTop + viewportHeight)
                targetTop = rowBottom - viewportHeight;

            float maximumTop = Mathf.Max(0f, _content.rect.height - viewportHeight);
            Vector2 position = _content.anchoredPosition;
            position.y = Mathf.Clamp(targetTop, 0f, maximumTop);
            _content.anchoredPosition = position;
        }

        private int FindRowIndex(string rowId)
        {
            if (_model == null)
                return -1;
            for (int i = 0; i < _model.Rows.Count; i++)
            {
                if (string.Equals(_model.Rows[i].RowId, rowId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private RecordTableColumnModel FindColumn(string columnId)
        {
            if (_model == null)
                return null;
            for (int i = 0; i < _model.Columns.Count; i++)
            {
                if (string.Equals(_model.Columns[i].ColumnId, columnId, StringComparison.Ordinal))
                    return _model.Columns[i];
            }
            return null;
        }

        private float CalculateTotalColumnWeight()
        {
            float total = 0f;
            for (int i = 0; i < _model.Columns.Count; i++)
                total += _model.Columns[i].WidthWeight;
            return Mathf.Max(total, 1f);
        }

        private float ResolveViewportWidth()
        {
            float width = _bodyViewport.rect.width;
            if (width <= 1f)
                width = ((RectTransform)transform).rect.width - ScrollbarThickness;
            return width > 1f ? width : FallbackViewportWidth;
        }

        private float ResolveViewportHeight()
        {
            float height = _bodyViewport.rect.height;
            if (height <= 1f)
                height = ((RectTransform)transform).rect.height - DefaultHeaderHeight - ScrollbarThickness;
            return height > 1f ? height : FallbackViewportHeight;
        }

        private void RenderState()
        {
            _stateTitle.text = _contentState.Title;
            _stateMessage.text = _contentState.Message;
            _stateTitle.color = _contentState.Kind == UiContentStateKind.Error
                ? CareerUiTheme.Error
                : CareerUiTheme.TextPrimary;
            bool hasAction = !string.IsNullOrEmpty(_contentState.ActionId);
            _stateActionButton.gameObject.SetActive(hasAction);
            _stateActionLabel.text = hasAction ? _contentState.ActionLabel : string.Empty;
        }

        private void HandleStateAction()
        {
            if (_contentState != null && !string.IsNullOrEmpty(_contentState.ActionId))
                StateActionRequested?.Invoke(_contentState.ActionId);
        }

        private void ClearRowPool()
        {
            for (int i = 0; i < _rowPool.Count; i++)
                DestroyObject(_rowPool[i].Root.gameObject);
            _rowPool.Clear();
        }

        private static string BuildColumnSignature(IReadOnlyList<RecordTableColumnModel> columns)
        {
            var signature = new System.Text.StringBuilder(columns.Count * 16);
            for (int i = 0; i < columns.Count; i++)
                signature.Append(columns[i].ColumnId).Append('|');
            return signature.ToString();
        }

        private static TextAnchor GetTextAnchor(RecordCellAlignment alignment)
        {
            return alignment switch
            {
                RecordCellAlignment.Left => TextAnchor.MiddleLeft,
                RecordCellAlignment.Right => TextAnchor.MiddleRight,
                _ => TextAnchor.MiddleCenter
            };
        }

        private static Scrollbar CreateScrollbar(
            Transform parent,
            string name,
            Scrollbar.Direction direction)
        {
            RectTransform track = CreateRect(name, parent);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = CareerUiTheme.Panel;

            RectTransform slidingArea = CreateRect("SlidingArea", track);
            Stretch(slidingArea, 1f);
            RectTransform handle = CreateRect("Handle", slidingArea);
            Stretch(handle, 1f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = CareerUiTheme.Primary;

            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = direction;
            return scrollbar;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static RectTransform CreateTopLeftRect(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static Font DefaultFont =>
            _defaultFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static void AddOutline(Image image)
        {
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetTopLeftRect(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyObject(parent.GetChild(i).gameObject);
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private sealed class PooledRow
        {
            private readonly RecordTableView _owner;
            private readonly Image _background;
            private readonly Text[] _values;

            public PooledRow(
                RecordTableView owner,
                Transform parent,
                IReadOnlyList<RecordTableColumnModel> columns)
            {
                _owner = owner;
                Root = CreateTopLeftRect("PooledRow", parent);
                _background = Root.gameObject.AddComponent<Image>();
                Button button = Root.gameObject.AddComponent<Button>();
                button.targetGraphic = _background;
                button.onClick.AddListener(() => _owner.HandleRowSelected(this));

                _values = new Text[columns.Count];
                float totalWeight = 0f;
                for (int i = 0; i < columns.Count; i++)
                    totalWeight += columns[i].WidthWeight;

                float leftWeight = 0f;
                for (int i = 0; i < columns.Count; i++)
                {
                    RecordTableColumnModel column = columns[i];
                    RectTransform cell = CreateTopLeftRect(column.ColumnId, Root);
                    float leftRatio = leftWeight / totalWeight;
                    float rightRatio = (leftWeight + column.WidthWeight) / totalWeight;
                    cell.anchorMin = new Vector2(leftRatio, 0f);
                    cell.anchorMax = new Vector2(rightRatio, 1f);
                    cell.pivot = new Vector2(0.5f, 0.5f);
                    cell.offsetMin = new Vector2(1f, 0f);
                    cell.offsetMax = new Vector2(-1f, 0f);
                    leftWeight += column.WidthWeight;

                    Text value = CreateText(
                        "Value",
                        cell,
                        13,
                        FontStyle.Normal,
                        GetTextAnchor(column.Alignment),
                        CareerUiTheme.TextSecondary);
                    Stretch(value.rectTransform);
                    value.rectTransform.offsetMin = new Vector2(7f, 0f);
                    value.rectTransform.offsetMax = new Vector2(-7f, 0f);
                    _values[i] = value;
                }
            }

            public RectTransform Root { get; }
            public string RowId { get; private set; } = string.Empty;

            public void Bind(
                RecordTableRowModel row,
                int rowIndex,
                float contentWidth,
                bool isSelected)
            {
                RowId = row.RowId;
                Root.gameObject.name = "Row_" + row.RowId;
                SetTopLeftRect(
                    Root,
                    0f,
                    rowIndex * DefaultRowHeight,
                    contentWidth,
                    DefaultRowHeight - 1f);
                Root.gameObject.SetActive(true);

                _background.color = isSelected
                    ? CareerUiTheme.SurfaceSelected
                    : row.IsHighlighted
                        ? CareerUiTheme.CurrentRow
                        : rowIndex % 2 == 0
                            ? CareerUiTheme.Surface
                            : CareerUiTheme.SurfaceSubtle;
                Color textColor = isSelected || row.IsHighlighted
                    ? CareerUiTheme.TextPrimary
                    : CareerUiTheme.TextSecondary;
                FontStyle fontStyle = isSelected || row.IsHighlighted
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                for (int i = 0; i < _values.Length; i++)
                {
                    RecordTableColumnModel column = _owner._model.Columns[i];
                    RecordTableCellModel cell = row.FindCell(column.ColumnId);
                    _values[i].text = cell.DisplayValue;
                    _values[i].color = textColor;
                    _values[i].fontStyle = fontStyle;
                }
            }

            public void SetActive(bool active)
            {
                Root.gameObject.SetActive(active);
                if (!active)
                    RowId = string.Empty;
            }
        }
    }
}

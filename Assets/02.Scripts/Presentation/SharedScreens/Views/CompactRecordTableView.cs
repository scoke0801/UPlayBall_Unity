using System;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 소규모 공용 기록표 모델만 소비해 정렬 가능한 Header와 읽기 전용 Row를 그리는 uGUI View다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CompactRecordTableView : MonoBehaviour
    {
        /// <summary>
        /// 전체 Row를 미리 생성해도 되는 소규모 표의 최대 행 수다.
        /// </summary>
        public const int MaxRows = 12;

        private static Font _defaultFont;

        private RectTransform _headerHost;
        private RectTransform _content;
        private GameObject _tableRoot;
        private GameObject _stateRoot;
        private Text _stateTitle;
        private Text _stateMessage;
        private Button _stateActionButton;
        private Text _stateActionLabel;
        private RecordTableModel _model;
        private UiContentStateModel _contentState;

        /// <summary>
        /// 사용자가 Row를 선택하면 Stable Row ID를 전달한다.
        /// </summary>
        public event Action<string> RowSelected;

        /// <summary>
        /// Header 정렬 결과가 바뀌면 열 ID와 방향을 전달한다.
        /// </summary>
        public event Action<string, RecordSortDirection> SortChanged;

        /// <summary>
        /// Empty 또는 Error 상태의 선택 행동을 요청하면 Action ID를 전달한다.
        /// </summary>
        public event Action<string> StateActionRequested;

        /// <summary>
        /// 현재 표시 중인 정렬 결과 모델이다.
        /// </summary>
        public RecordTableModel Model => _model;

        /// <summary>
        /// 부모 아래에 완전한 공용 기록표 계층을 런타임 생성한다.
        /// </summary>
        public static CompactRecordTableView CreateRuntime(
            Transform parent,
            string objectName = "CompactRecordTable")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var tableObject = new GameObject(objectName, typeof(RectTransform));
            tableObject.transform.SetParent(parent, false);
            Stretch(tableObject.GetComponent<RectTransform>());
            return tableObject.AddComponent<CompactRecordTableView>();
        }

        /// <summary>
        /// 준비가 끝난 기록표를 표시한다.
        /// </summary>
        public void Bind(RecordTableModel model)
        {
            Bind(model, UiContentStateModel.Ready);
        }

        /// <summary>
        /// 기록표와 Loading, Empty, Error 상태를 함께 표시한다.
        /// </summary>
        public void Bind(RecordTableModel model, UiContentStateModel contentState)
        {
            _contentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            if (_contentState.Kind == UiContentStateKind.Ready && model == null)
                throw new ArgumentNullException(nameof(model), "Ready 상태에는 기록표 모델이 필요합니다.");
            if (_contentState.Kind == UiContentStateKind.Ready && model.Rows.Count > MaxRows)
            {
                throw new ArgumentException(
                    $"{nameof(CompactRecordTableView)}는 최대 {MaxRows}행만 지원합니다. " +
                    "대량 기록은 Virtualization을 제공하는 별도 View를 사용해야 합니다.",
                    nameof(model));
            }

            EnsureHierarchy();
            _model = model;
            bool isReady = _contentState.Kind == UiContentStateKind.Ready;
            _tableRoot.SetActive(isReady);
            _stateRoot.SetActive(!isReady);
            if (isReady)
            {
                RenderTable();
                return;
            }

            RenderState();
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void EnsureHierarchy()
        {
            if (_tableRoot != null)
                return;

            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);
            Image background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = CareerUiTheme.ReferencePanel;
            background.raycastTarget = false;
            AddOutline(background);

            RectTransform tableRoot = CreateRect("Table", root);
            Stretch(tableRoot);
            _tableRoot = tableRoot.gameObject;

            _headerHost = CreateRect("Header", tableRoot);
            SetAnchors(_headerHost, new Vector2(0f, 1f), Vector2.one,
                new Vector2(2f, -42f), new Vector2(-2f, -2f));
            var headerLayout = _headerHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureHorizontalLayout(headerLayout);

            RectTransform viewport = CreateRect("Viewport", tableRoot);
            SetAnchors(viewport, Vector2.zero, Vector2.one,
                new Vector2(2f, 2f), new Vector2(-2f, -44f));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _content = CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var rowsLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 1f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = tableRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = _content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            BuildState(root);
        }

        private void BuildState(RectTransform root)
        {
            RectTransform state = CreateRect("ContentState", root);
            Stretch(state);
            Image surface = state.gameObject.AddComponent<Image>();
            surface.color = CareerUiTheme.ReferencePanel;
            surface.raycastTarget = false;
            _stateRoot = state.gameObject;

            _stateTitle = CreateText("Title", state, 20, FontStyle.Bold, TextAnchor.LowerCenter, CareerUiTheme.ReferenceText);
            SetAnchors(_stateTitle.rectTransform, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.66f),
                Vector2.zero, Vector2.zero);
            _stateMessage = CreateText("Message", state, 14, FontStyle.Normal, TextAnchor.UpperCenter, CareerUiTheme.ReferenceTextSecondary);
            SetAnchors(_stateMessage.rectTransform, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.5f),
                Vector2.zero, Vector2.zero);

            RectTransform actionRect = CreateRect("Action", state);
            actionRect.anchorMin = new Vector2(0.5f, 0.23f);
            actionRect.anchorMax = new Vector2(0.5f, 0.23f);
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            actionRect.sizeDelta = new Vector2(180f, 38f);
            Image actionImage = actionRect.gameObject.AddComponent<Image>();
            actionImage.color = CareerUiTheme.ReferenceAccent;
            _stateActionButton = actionRect.gameObject.AddComponent<Button>();
            _stateActionButton.targetGraphic = actionImage;
            _stateActionButton.onClick.AddListener(HandleStateAction);
            _stateActionLabel = CreateText("Label", actionRect, 14, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            Stretch(_stateActionLabel.rectTransform);
            _stateRoot.SetActive(false);
        }

        private void RenderTable()
        {
            ClearChildren(_headerHost);
            ClearChildren(_content);
            for (int columnIndex = 0; columnIndex < _model.Columns.Count; columnIndex++)
                CreateHeader(_model.Columns[columnIndex]);
            for (int rowIndex = 0; rowIndex < _model.Rows.Count; rowIndex++)
                CreateRow(_model.Rows[rowIndex], rowIndex);
        }

        private void CreateHeader(RecordTableColumnModel column)
        {
            RectTransform rect = CreateRect(column.ColumnId, _headerHost);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = column.WidthWeight;
            layout.minWidth = 24f;
            layout.flexibleHeight = 1f;
            Image image = rect.gameObject.AddComponent<Image>();
            bool isSorted = string.Equals(_model.SortedColumnId, column.ColumnId, StringComparison.Ordinal);
            image.color = isSorted ? CareerUiTheme.ReferenceAccentLight : CareerUiTheme.ReferencePanelHeader;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = column.IsSortable;
            string marker = isSorted
                ? _model.SortDirection == RecordSortDirection.Ascending ? " ▲" : " ▼"
                : string.Empty;
            Text label = CreateText(
                "Label", rect, 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                isSorted ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceTextSecondary);
            label.text = column.DisplayName + marker;
            Stretch(label.rectTransform);
            if (column.IsSortable)
            {
                string columnId = column.ColumnId;
                button.onClick.AddListener(() => HandleSort(columnId));
            }
        }

        private void CreateRow(RecordTableRowModel row, int rowIndex)
        {
            RectTransform rect = CreateRect(row.RowId, _content);
            LayoutElement rowLayout = rect.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 36f;
            rowLayout.minHeight = 32f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = row.IsHighlighted
                ? CareerUiTheme.ReferenceAccentLight
                : rowIndex % 2 == 0 ? CareerUiTheme.ReferencePanel : CareerUiTheme.ReferenceCanvas;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            string rowId = row.RowId;
            button.onClick.AddListener(() => RowSelected?.Invoke(rowId));

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureHorizontalLayout(layout);
            for (int columnIndex = 0; columnIndex < _model.Columns.Count; columnIndex++)
            {
                RecordTableColumnModel column = _model.Columns[columnIndex];
                RecordTableCellModel cell = row.FindCell(column.ColumnId);
                CreateCell(rect, column, cell.DisplayValue, row.IsHighlighted);
            }
        }

        private static void CreateCell(
            Transform parent,
            RecordTableColumnModel column,
            string displayValue,
            bool isHighlighted)
        {
            RectTransform rect = CreateRect(column.ColumnId, parent);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = column.WidthWeight;
            layout.minWidth = 24f;
            layout.flexibleHeight = 1f;
            TextAnchor alignment = GetTextAnchor(column.Alignment);
            Text value = CreateText(
                "Value", rect, 13, isHighlighted ? FontStyle.Bold : FontStyle.Normal,
                alignment, isHighlighted ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceTextSecondary);
            value.text = displayValue;
            Stretch(value.rectTransform);
            value.rectTransform.offsetMin = new Vector2(7f, 0f);
            value.rectTransform.offsetMax = new Vector2(-7f, 0f);
        }

        private void RenderState()
        {
            _stateTitle.text = _contentState.Title;
            _stateMessage.text = _contentState.Message;
            _stateTitle.color = _contentState.Kind == UiContentStateKind.Error
                ? CareerUiTheme.Error
                : CareerUiTheme.ReferenceText;
            bool hasAction = !string.IsNullOrEmpty(_contentState.ActionId);
            _stateActionButton.gameObject.SetActive(hasAction);
            _stateActionLabel.text = hasAction ? _contentState.ActionLabel : string.Empty;
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
            RenderTable();
            SortChanged?.Invoke(columnId, direction);
        }

        private RecordTableColumnModel FindColumn(string columnId)
        {
            for (int i = 0; i < _model.Columns.Count; i++)
            {
                if (string.Equals(_model.Columns[i].ColumnId, columnId, StringComparison.Ordinal))
                    return _model.Columns[i];
            }
            return null;
        }

        private void HandleStateAction()
        {
            if (_contentState != null && !string.IsNullOrEmpty(_contentState.ActionId))
                StateActionRequested?.Invoke(_contentState.ActionId);
        }

        private static TextAnchor GetTextAnchor(RecordCellAlignment alignment)
        {
            switch (alignment)
            {
                case RecordCellAlignment.Left:
                    return TextAnchor.MiddleLeft;
                case RecordCellAlignment.Right:
                    return TextAnchor.MiddleRight;
                default:
                    return TextAnchor.MiddleCenter;
            }
        }

        private static void ConfigureHorizontalLayout(HorizontalLayoutGroup layout)
        {
            layout.spacing = 1f;
            layout.padding = new RectOffset(1, 1, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
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
            outline.effectColor = CareerUiTheme.ReferenceBorder;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}

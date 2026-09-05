using System;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 편집·Drag 입력 없이 역할별 선수와 기용 근거를 표시하는 공용 uGUI Roster 목록이다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ReadOnlyRosterListView : MonoBehaviour
    {
        /// <summary>
        /// 선수단 한 개를 미리 생성해 표시할 수 있는 최대 선수 수다.
        /// </summary>
        public const int MaxPlayerRows = 40;

        private static Font _defaultFont;

        private Text _title;
        private Text _summary;
        private RectTransform _content;
        private GameObject _listRoot;
        private GameObject _stateRoot;
        private Text _stateTitle;
        private Text _stateMessage;
        private ReadOnlyRosterModel _model;

        /// <summary>
        /// 사용자가 선수 상세 대상으로 Row를 선택하면 Stable Player ID를 전달한다.
        /// </summary>
        public event Action<string> PlayerSelected;

        /// <summary>
        /// 현재 표시 중인 읽기 전용 Roster Snapshot이다.
        /// </summary>
        public ReadOnlyRosterModel Model => _model;

        /// <summary>
        /// 부모 아래에 읽기 전용 Roster 목록 계층을 런타임 생성한다.
        /// </summary>
        public static ReadOnlyRosterListView CreateRuntime(Transform parent, string objectName = "ReadOnlyRosterList")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var listObject = new GameObject(objectName, typeof(RectTransform));
            listObject.transform.SetParent(parent, false);
            Stretch(listObject.GetComponent<RectTransform>());
            return listObject.AddComponent<ReadOnlyRosterListView>();
        }

        /// <summary>
        /// 준비가 끝난 읽기 전용 Roster를 표시한다.
        /// </summary>
        public void Bind(ReadOnlyRosterModel model)
        {
            Bind(model, UiContentStateModel.Ready);
        }

        /// <summary>
        /// Roster와 Loading, Empty, Error 상태를 함께 표시한다.
        /// </summary>
        public void Bind(ReadOnlyRosterModel model, UiContentStateModel contentState)
        {
            if (contentState == null)
                throw new ArgumentNullException(nameof(contentState));
            if (contentState.Kind == UiContentStateKind.Ready && model == null)
                throw new ArgumentNullException(nameof(model), "Ready 상태에는 Roster 모델이 필요합니다.");
            if (contentState.Kind == UiContentStateKind.Ready && CountPlayers(model) > MaxPlayerRows)
            {
                throw new ArgumentException(
                    $"{nameof(ReadOnlyRosterListView)}는 최대 {MaxPlayerRows}명의 단일 선수단만 지원합니다.",
                    nameof(model));
            }

            EnsureHierarchy();
            _model = model;
            bool isReady = contentState.Kind == UiContentStateKind.Ready;
            _listRoot.SetActive(isReady);
            _stateRoot.SetActive(!isReady);
            if (!isReady)
            {
                _stateTitle.text = contentState.Title;
                _stateMessage.text = contentState.Message;
                return;
            }

            _title.text = $"{model.TeamName} · {model.SeasonLabel}";
            _summary.text = model.Summary;
            RenderRows();
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void EnsureHierarchy()
        {
            if (_listRoot != null)
                return;

            RectTransform root = GetComponent<RectTransform>();
            Stretch(root);
            Image background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = CareerUiTheme.PanelDark;
            background.raycastTarget = false;

            RectTransform listRoot = CreateRect("List", root);
            Stretch(listRoot);
            _listRoot = listRoot.gameObject;
            _title = CreateText("Title", listRoot, 18, FontStyle.Bold, TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            SetAnchors(_title.rectTransform, new Vector2(0f, 1f), Vector2.one,
                new Vector2(12f, -38f), new Vector2(-12f, -2f));
            _summary = CreateText("Summary", listRoot, 13, FontStyle.Normal, TextAnchor.MiddleRight, CareerUiTheme.TextSecondary);
            SetAnchors(_summary.rectTransform, new Vector2(0.48f, 1f), Vector2.one,
                new Vector2(0f, -38f), new Vector2(-12f, -2f));

            RectTransform viewport = CreateRect("Viewport", listRoot);
            SetAnchors(viewport, Vector2.zero, Vector2.one,
                new Vector2(2f, 2f), new Vector2(-2f, -42f));
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
            VerticalLayoutGroup layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 1f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = listRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform state = CreateRect("ContentState", root);
            Stretch(state);
            _stateRoot = state.gameObject;
            _stateTitle = CreateText("Title", state, 20, FontStyle.Bold, TextAnchor.LowerCenter, CareerUiTheme.TextPrimary);
            SetAnchors(_stateTitle.rectTransform, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.66f),
                Vector2.zero, Vector2.zero);
            _stateMessage = CreateText("Message", state, 14, FontStyle.Normal, TextAnchor.UpperCenter, CareerUiTheme.TextSecondary);
            SetAnchors(_stateMessage.rectTransform, new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.5f),
                Vector2.zero, Vector2.zero);
            _stateRoot.SetActive(false);
        }

        private void RenderRows()
        {
            ClearChildren(_content);
            for (int groupIndex = 0; groupIndex < _model.Groups.Count; groupIndex++)
            {
                ReadOnlyRosterGroupModel group = _model.Groups[groupIndex];
                CreateGroupHeader(group);
                for (int playerIndex = 0; playerIndex < group.Players.Count; playerIndex++)
                    CreatePlayerRow(group.Players[playerIndex]);
            }
        }

        private void CreateGroupHeader(ReadOnlyRosterGroupModel group)
        {
            RectTransform rect = CreateRect(group.GroupId, _content);
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 30f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = CareerUiTheme.Panel;
            image.raycastTarget = false;
            Text label = CreateText("Label", rect, 14, FontStyle.Bold, TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            label.text = $"{group.DisplayName}  {group.Players.Count}명";
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
        }

        private void CreatePlayerRow(ReadOnlyRosterPlayerModel player)
        {
            RectTransform rect = CreateRect(player.PlayerId, _content);
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = string.IsNullOrEmpty(player.HighlightReason) ? 38f : 52f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = GetRowColor(player.VisualState);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            string playerId = player.PlayerId;
            button.onClick.AddListener(() => PlayerSelected?.Invoke(playerId));

            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            CreateValue(rect, player.PositionLabel, 48f, TextAnchor.MiddleCenter, CareerUiTheme.PrimaryBright, FontStyle.Bold);
            CreateValue(rect, player.DisplayName, 124f, TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary, FontStyle.Bold);
            CreateValue(rect, player.RoleLabel, 82f, TextAnchor.MiddleCenter, CareerUiTheme.TextSecondary, FontStyle.Normal);
            CreateValue(rect, player.OverallText, 48f, TextAnchor.MiddleCenter, CareerUiTheme.TextPrimary, FontStyle.Bold);
            CreateValue(rect, player.ConditionText, 68f, TextAnchor.MiddleCenter, CareerUiTheme.TextSecondary, FontStyle.Normal);
            CreateValue(rect, player.PrimaryRecordText, 78f, TextAnchor.MiddleRight, CareerUiTheme.TextPrimary, FontStyle.Bold);
            CreateValue(rect, player.HighlightReason, 0f, TextAnchor.MiddleLeft, CareerUiTheme.PrimaryBright, FontStyle.Normal, true);
        }

        private static void CreateValue(
            Transform parent,
            string value,
            float width,
            TextAnchor alignment,
            Color color,
            FontStyle style,
            bool flexible = false)
        {
            RectTransform rect = CreateRect("Value", parent);
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = flexible ? 20f : width;
            element.flexibleWidth = flexible ? 1f : 0f;
            Text text = CreateText("Text", rect, 13, style, alignment, color);
            text.text = value;
            Stretch(text.rectTransform);
        }

        private static Color GetRowColor(RosterPlayerVisualState state)
        {
            switch (state)
            {
                case RosterPlayerVisualState.Highlighted:
                    return CareerUiTheme.CurrentRow;
                case RosterPlayerVisualState.Warning:
                    return CareerUiTheme.SpecialAction;
                case RosterPlayerVisualState.Unavailable:
                    return CareerUiTheme.PanelDark;
                default:
                    return CareerUiTheme.Surface;
            }
        }

        private static int CountPlayers(ReadOnlyRosterModel model)
        {
            int count = 0;
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
                count += model.Groups[groupIndex].Players.Count;
            return count;
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

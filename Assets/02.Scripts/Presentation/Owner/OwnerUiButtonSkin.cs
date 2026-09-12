using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 버튼의 정보 위계에 맞는 프레임 종류다.</summary>
    public enum OwnerButtonRole { Secondary, Primary, Navigation, Tab, Detail, Quiet }

    /// <summary>구단주 전용 ImageGen 프레임과 입력 상태를 기존 버튼의 의미 색상에서 분리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OwnerUiButtonSkin : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(28, 43, 62, 255);
        private static readonly Color Ivory = new Color32(250, 247, 237, 255);
        private static readonly Sprite[] Frames = new Sprite[3];
        private Button _button;
        private Image _source;
        private Image _frame;
        private Text _label;
        private OwnerButtonRole _role;
        private bool? _isSelected;
        private Color _lastSource;
        private Color _lastLabel;
        private bool _lastInteractable;
        private bool _hasRendered;
        private bool _usesBoardStyle;
        private bool _usesDashboardStyle;

        /// <summary>홈의 행동 위계를 골드 기본 행동·네이비 추천·투명 탐색으로 표현한다.</summary>
        public static void SetDashboardStyle(Button button)
        {
            var skin = button.GetComponent<OwnerUiButtonSkin>();
            if (skin == null) return;
            skin._usesDashboardStyle = true;
            skin.Refresh();
        }

        /// <summary>기존 프레임을 유지하며 어두운 카드 편성 보드의 보조 버튼 대비를 맞춘다.</summary>
        public static void SetBoardStyle(Button button)
        {
            var skin = button.GetComponent<OwnerUiButtonSkin>();
            if (skin == null || skin._usesBoardStyle) return;
            skin._usesBoardStyle = true;
            skin.Refresh();
        }

        /// <summary>문자 버튼에만 전용 프레임을 연결한다. 카드와 투명 클릭 영역은 유지한다.</summary>
        public static void Apply(Button button, OwnerButtonRole role = OwnerButtonRole.Secondary)
        {
            if (button == null) return;
            var visual = button.GetComponent<CareerUiVisualElement>();
            if (visual != null && visual.Role == CareerUiVisualRole.DataImage) return;
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            Image source = button.GetComponent<Image>();
            if (label == null || string.IsNullOrEmpty(label.text) || source == null) return;
            var skin = button.GetComponent<OwnerUiButtonSkin>() ?? button.gameObject.AddComponent<OwnerUiButtonSkin>();
            skin._role = role;
            skin.enabled = true;
            if (skin._frame == null) skin.Initialize(button, source, label);
            skin._frame.gameObject.SetActive(true);
            skin.Refresh();
        }

        /// <summary>공용 셸이 선수 모드로 전환되면 기존 버튼 Graphic으로 복귀한다.</summary>
        public static void Restore(Button button)
        {
            var skin = button != null ? button.GetComponent<OwnerUiButtonSkin>() : null;
            if (skin == null || skin._frame == null) return;
            skin.enabled = false;
            skin._frame.gameObject.SetActive(false);
            skin._source.enabled = true;
            button.targetGraphic = skin._source;
            button.colors = ColorBlock.defaultColorBlock;
        }

        /// <summary>현재 탭 선택은 키보드 포커스와 별개로 유지한다.</summary>
        public static void SetSelected(Button button, bool isSelected)
        {
            var skin = button != null ? button.GetComponent<OwnerUiButtonSkin>() : null;
            if (skin == null) return;
            skin._isSelected = isSelected;
            skin.Refresh();
        }

        private void Initialize(Button button, Image source, Text label)
        {
            _button = button;
            _source = source;
            _label = label;
            var rect = new GameObject("OwnerButtonFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _frame = rect.gameObject.AddComponent<Image>();
            // 원본 Image는 화면의 선택 색상 계약을 계속 소유하고, 별도 Graphic이 입력 효과를 받는다.
            _frame.raycastTarget = true;
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            if (label.GetComponent<CareerUiPreserveTextColor>() == null)
                label.gameObject.AddComponent<CareerUiPreserveTextColor>();
        }

        private void LateUpdate()
        {
            if (_button == null || _frame == null) return;
            // 기존 화면의 Bind/선택 갱신은 Image.color를 쓴다. 변경된 경우에만 렌더 상태를 동기화한다.
            if (!_hasRendered || _source.color != _lastSource || _label.color != _lastLabel
                || _button.IsInteractable() != _lastInteractable || _button.targetGraphic != _frame)
                Refresh();
        }

        /// <summary>공용 스킨 재적용 뒤에도 구단주 프레임과 라벨 대비를 보존한다.</summary>
        public void Refresh()
        {
            if (_frame == null || !enabled) return;
            if (_usesDashboardStyle)
            {
                RefreshDashboard();
                return;
            }
            if (_role == OwnerButtonRole.Quiet || _role == OwnerButtonRole.Navigation)
            {
                _source.enabled = false;
                _frame.sprite = null;
                _frame.type = Image.Type.Simple;
                _frame.color = Color.white;
                _button.targetGraphic = _frame;
                _button.transition = Selectable.Transition.ColorTint;
                var quietColors = ColorBlock.defaultColorBlock;
                quietColors.normalColor = Color.clear;
                quietColors.highlightedColor = CareerUiTheme.Surface;
                quietColors.selectedColor = CareerUiTheme.SurfaceSelected;
                quietColors.pressedColor = CareerUiTheme.PanelDark;
                quietColors.disabledColor = Color.clear;
                _button.colors = quietColors;
                _label.color = _button.IsInteractable() ? CareerUiTheme.TextSecondary : CareerUiTheme.TextMuted;
                if (_isSelected == true) _label.color = CareerUiTheme.AccentGold;
                if (_role == OwnerButtonRole.Navigation)
                {
                    var navigationOutline = _source.GetComponent<Outline>();
                    if (navigationOutline != null) navigationOutline.enabled = false;
                    var icon = transform.Find("Icon")?.GetComponent<RawImage>();
                    if (icon != null) icon.color = _isSelected == true ? CareerUiTheme.AccentGold : CareerUiTheme.TextSecondary;
                }
                quietColors.fadeDuration = .18f;
                _button.colors = quietColors;
                _lastSource = _source.color; _lastLabel = _label.color;
                _lastInteractable = _button.IsInteractable(); _hasRendered = true;
                return;
            }
            Color semantic = _source.color;
            bool isRed = semantic.r > semantic.b + .15f && semantic.r > semantic.g + .15f;
            bool isAccent = semantic.b > semantic.r + .12f || isRed;
            bool isSelected = _isSelected ?? isAccent;
            bool isDark = _role == OwnerButtonRole.Primary || _role == OwnerButtonRole.Navigation || isSelected;
            int frameIndex = _role == OwnerButtonRole.Navigation && !isSelected ? 0 : isDark && !isRed ? 2 : 1;
            Sprite sprite = LoadFrame(frameIndex);
            if (sprite == null) return;

            _source.enabled = false;
            Outline outline = _source.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            _frame.sprite = sprite;
            _frame.type = Image.Type.Sliced;
            _frame.pixelsPerUnitMultiplier = frameIndex == 0 ? 14f : 10f;
            _frame.color = isSelected && isRed ? new Color(.62f, .19f, .21f, 1f) : Color.white;
            _button.targetGraphic = _frame;
            _button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = isDark ? new Color(1.3f, 1.3f, 1.3f, 1f) : new Color(.86f, .93f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.70f, .77f, .85f, 1f);
            colors.disabledColor = new Color(.60f, .63f, .67f, .65f);
            colors.fadeDuration = .10f;
            _button.colors = colors;
            _lastInteractable = _button.IsInteractable();
            _label.color = _lastInteractable ? isDark ? Ivory : Ink
                : isDark ? new Color32(207, 213, 220, 255) : new Color32(78, 87, 99, 255);
            if (_usesBoardStyle && !isDark)
            {
                _frame.color = CareerUiTheme.RosterSurfaceRaised;
                _label.color = _lastInteractable ? CareerUiTheme.RosterText : CareerUiTheme.RosterTextSecondary;
            }
            // 아이콘 메뉴의 하단 라벨과 화면별 다중 행 배치는 유지한다.
            if (_label.rectTransform.anchorMin == Vector2.zero && _label.rectTransform.anchorMax == Vector2.one)
            {
                float padding = _role == OwnerButtonRole.Detail ? 6f : 9f;
                _label.rectTransform.offsetMin = new Vector2(padding, 2f);
                _label.rectTransform.offsetMax = new Vector2(-padding, -2f);
            }
            _lastSource = semantic;
            _lastLabel = _label.color;
            _hasRendered = true;
        }

        private void RefreshDashboard()
        {
            _source.enabled = false;
            var outline = _source.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            _frame.sprite = null;
            _frame.type = Image.Type.Simple;
            _frame.color = Color.white;
            bool primary = _role == OwnerButtonRole.Primary;
            bool quiet = _role == OwnerButtonRole.Quiet;
            Color surface = primary ? OwnerDashboardStyle.Gold : quiet ? Color.clear : OwnerDashboardStyle.Raised;
            if (_isSelected == true && !primary) surface = OwnerDashboardStyle.Raised;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = surface;
            colors.highlightedColor = primary ? new Color32(242, 212, 146, 255) : new Color32(55, 72, 85, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color32(186, 151, 87, 255) : new Color32(27, 41, 52, 255);
            colors.disabledColor = quiet ? Color.clear : new Color32(39, 46, 51, 255);
            colors.fadeDuration = .18f;
            _button.targetGraphic = _frame;
            _button.transition = Selectable.Transition.ColorTint;
            _button.colors = colors;
            _label.font = primary ? UIProjectFonts.Default : UIProjectFonts.Body;
            _label.fontStyle = FontStyle.Normal;
            _label.color = !_button.IsInteractable() ? OwnerDashboardStyle.Muted
                : primary ? OwnerDashboardStyle.Ink : _isSelected == true ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Ivory;
            _lastSource = _source.color; _lastLabel = _label.color;
            _lastInteractable = _button.IsInteractable(); _hasRendered = true;
        }

        private static Sprite LoadFrame(int index)
        {
            if (Frames[index] != null) return Frames[index];
            string name = index == 0 ? "navigation" : index == 1 ? "secondary" : "primary";
            Texture2D texture = Resources.Load<Texture2D>("UI/OwnerSkin/owner_button_" + name + "_v1");
            if (texture == null) return null;
            // 메뉴 원본만 외곽 투명 여백이 있다. 픽셀을 수정하지 않고 Sprite 영역으로 제거한다.
            Rect area = index == 0 ? new Rect(38f, 169f, 1596f, 652f) : new Rect(0f, 0f, texture.width, texture.height);
            Vector4 border = index == 0 ? new Vector4(82f, 72f, 82f, 72f) : new Vector4(52f, 52f, 52f, 52f);
            Frames[index] = Sprite.Create(texture, area, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            Frames[index].name = "OwnerButton_" + name;
            return Frames[index];
        }
    }
}

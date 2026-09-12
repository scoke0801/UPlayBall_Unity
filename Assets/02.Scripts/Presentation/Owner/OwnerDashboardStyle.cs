using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>홈과 매니저 리포트가 공유하는 재질·타이포그래피 규격이다.</summary>
    public static class OwnerDashboardStyle
    {
        public static readonly Color Ink = new Color32(18, 27, 34, 255);
        public static readonly Color Surface = new Color32(23, 33, 42, 248);
        public static readonly Color Raised = new Color32(42, 57, 69, 255);
        public static readonly Color Gold = new Color32(218, 187, 123, 255);
        public static readonly Color Ivory = new Color32(239, 235, 222, 255);
        public static readonly Color Muted = new Color32(155, 168, 175, 255);
        public static readonly Color Line = new Color32(111, 130, 144, 65);

        // 기록표는 장식 프레임 안에서도 불투명한 저대비 작업면을 유지한다.
        public static readonly Color TableSurface = new Color32(32, 42, 53, 255);
        public static readonly Color TableAlternate = new Color32(37, 49, 62, 255);
        public static readonly Color TableHeader = new Color32(43, 55, 69, 255);
        public static readonly Color TableSelected = new Color32(48, 65, 81, 255);
        public static readonly Color TableSecondary = new Color32(170, 183, 197, 255);
        public static readonly Color InsetSurface = new Color32(15, 31, 46, 255);

        /// <summary>업무 구획의 제목 띠와 본문 음영을 분리한다. 장식은 입력을 받지 않는다.</summary>
        public static void ApplySection(Image image, float headingHeight = 36f)
        {
            ApplyInset(image, image.raycastTarget);
            if (image.transform.Find("SectionHeading") != null) return;
            var heading = OwnerRuntimeUiFactory.CreateImage("SectionHeading", image.transform, TableHeader);
            heading.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SetDataSurface(heading, TableHeader);
            heading.rectTransform.anchorMin = Vector2.up;
            heading.rectTransform.anchorMax = Vector2.one;
            heading.rectTransform.offsetMin = new Vector2(0, -headingHeight);
            heading.rectTransform.offsetMax = Vector2.zero;
            Rule(heading.transform, "Baseline", Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0, 1), Line);
            Rule(heading.transform, "Accent", Vector2.zero, Vector2.up,
                Vector2.zero, new Vector2(3, 0), Gold);
        }

        /// <summary>목록·상세 본문의 안쪽 면에 네이비 음영과 얇은 반사선을 한 번만 구성한다.</summary>
        public static void ApplyInset(Image image, bool interactive = false)
        {
            SetDataSurface(image, InsetSurface, interactive);
            var gradient = image.GetComponent<UIOwnerSurfaceGradient>()
                ?? image.gameObject.AddComponent<UIOwnerSurfaceGradient>();
            gradient.enabled = true;
            if (image.transform.Find("InsetEdge") != null) return;
            var edge = OwnerRuntimeUiFactory.CreateRect("InsetEdge", image.transform);
            OwnerRuntimeUiFactory.Stretch(edge);
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Rule(edge, "Top", Vector2.up, Vector2.one, new Vector2(0, -1), Vector2.zero, Line);
            Rule(edge, "Bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 1), Ink);
            Rule(edge, "Left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(1, 0), Line);
            Rule(edge, "Right", Vector2.right, Vector2.one, new Vector2(-1, 0), Vector2.zero, Ink);
        }

        /// <summary>공용 하단 행동 영역에 V2 띠를 연결한다.</summary>
        public static void ApplyActionBar(RectTransform root)
        {
            if (root.GetComponent<Image>() == null) root.gameObject.AddComponent<Image>().raycastTarget = false;
            UIOwnerFrontOfficePanel.Apply(root, "CompactStrip");
        }

        /// <summary>네이티브 면과 가는 상단 반사선으로 패널 깊이를 만든다.</summary>
        public static void ApplySurface(RectTransform root, bool accent = false)
        {
            UIOwnerFrontOfficePanel.Apply(root, "CompactStrip");
        }

        /// <summary>제목에는 Medium, 설명에는 Light 실제 폰트를 사용한다.</summary>
        public static void SetTypography(Text text, bool title = false)
        {
            text.font = title ? UIProjectFonts.Default : UIProjectFonts.Body;
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = 1.12f;
        }

        /// <summary>장식 프레임 안의 데이터 면만 보호하며 입력 여부는 호출자가 지정한다.</summary>
        public static void SetDataSurface(Image image, Color color, bool interactive = false)
        {
            image.color = color;
            image.sprite = null;
            image.overrideSprite = null;
            image.raycastTarget = interactive;
            var visual = image.GetComponent<CareerUiVisualElement>() ?? image.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.DataImage);
            var outline = image.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        /// <summary>스킨 재적용에도 본문 대비와 실제 서체를 유지한다.</summary>
        public static void SetDataText(Text text, bool primary = false)
        {
            SetTypography(text, primary);
            text.color = primary ? Ivory : TableSecondary;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            if (text.GetComponent<CareerUiPreserveTextColor>() == null)
                text.gameObject.AddComponent<CareerUiPreserveTextColor>();
        }

        /// <summary>검색의 글자·캐럿·선택·비활성·포커스를 같은 작업면으로 구성한다.</summary>
        public static void SetDataInput(InputField input)
        {
            ApplyInset(input.GetComponent<Image>(), true);
            SetDataText(input.textComponent);
            input.textComponent.color = Ivory;
            if (input.placeholder is Text placeholder) SetDataText(placeholder);
            input.customCaretColor = true;
            input.caretColor = Ivory;
            input.selectionColor = new Color(Gold.r, Gold.g, Gold.b, .35f);
            ConfigureDataControl(input);
        }

        /// <summary>드롭다운의 접힌 값과 복제되는 목록 템플릿을 함께 맞춘다.</summary>
        public static void SetDataDropdown(Dropdown dropdown)
        {
            ApplyInset(dropdown.GetComponent<Image>(), true);
            if (dropdown.captionText != null) SetDataText(dropdown.captionText);
            ConfigureDataControl(dropdown);
            if (dropdown.template == null) return;
            var surface = dropdown.template.GetComponent<Image>();
            if (surface != null) SetDataSurface(surface, TableSurface, true);
            if (dropdown.itemText != null) SetDataText(dropdown.itemText);
            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.targetGraphic is Image background) SetDataSurface(background, TableHeader, true);
                ConfigureDataControl(toggle);
            }
        }

        /// <summary>실제 입력 면에만 밝기 Hover와 독립적인 포커스 외곽선을 연결한다.</summary>
        public static void ConfigureDataControl(Selectable control)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.17f, 1.17f, 1.17f, 1);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.85f, .85f, .85f, 1);
            colors.disabledColor = new Color(.65f, .65f, .65f, 1);
            control.colors = colors;
            control.transition = Selectable.Transition.ColorTint;
            var rect = (RectTransform)control.transform;
            if (rect.Find("DataFocus") != null) return;
            var focus = OwnerRuntimeUiFactory.CreateRect("DataFocus", rect);
            focus.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Rule(focus, "Top", Vector2.up, Vector2.one, new Vector2(0, -1), Vector2.zero, TableSecondary);
            Rule(focus, "Bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 1), TableSecondary);
            Rule(focus, "Left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(1, 0), TableSecondary);
            Rule(focus, "Right", Vector2.right, Vector2.one, new Vector2(-1, 0), Vector2.zero, TableSecondary);
            focus.gameObject.SetActive(false);
            control.gameObject.AddComponent<UIOwnerDataFocus>().Initialize(control, focus.gameObject);
        }

        /// <summary>데이터 행의 지속 선택을 입력 포커스와 별도로 표시한다.</summary>
        public static void SetDataRow(Button button, bool selected, Color normal)
        {
            OwnerUiButtonSkin.Restore(button);
            SetDataSurface(button.GetComponent<Image>(), selected ? TableSelected : normal, true);
            button.targetGraphic = button.GetComponent<Image>();
            ConfigureDataControl(button);
            var mark = button.transform.Find("DataSelection");
            if (mark == null)
            {
                Rule(button.transform, "DataSelection", Vector2.zero, Vector2.up,
                    Vector2.zero, new Vector2(2, 0), Gold);
                mark = button.transform.Find("DataSelection");
                mark.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            mark.gameObject.SetActive(selected);
        }

        /// <summary>화면 배율에 따라 선명하게 그려지는 장식선이다.</summary>
        public static void Rule(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 lower, Vector2 upper, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color; image.raycastTarget = false;
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = lower; rect.offsetMax = upper;
        }
    }

    /// <summary>스크롤 이벤트 전파를 가로막지 않고 입력 포커스만 표시한다.</summary>
    internal sealed class UIOwnerDataFocus : MonoBehaviour, UnityEngine.EventSystems.ISelectHandler,
        UnityEngine.EventSystems.IDeselectHandler
    {
        [SerializeField] private Selectable _control;
        [SerializeField] private GameObject _outline;
        internal void Initialize(Selectable control, GameObject outline)
        { _control = control; _outline = outline; }
        public void OnSelect(UnityEngine.EventSystems.BaseEventData data)
        {
            if (_outline == null) return;
            _outline.transform.SetAsLastSibling();
            _outline.SetActive(_control.IsInteractable());
        }
        public void OnDeselect(UnityEngine.EventSystems.BaseEventData data)
        { if (_outline != null) _outline.SetActive(false); }
        private void OnDisable() { if (_outline != null) _outline.SetActive(false); }
    }
}

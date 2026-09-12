using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>V2 PNG 상태를 기존 클릭·포커스·지속 선택 계약 위에 적용한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIOwnerFrontOfficeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Sprite normal, hover, pressed, disabled, selected;
        [SerializeField] private bool primary;
        [SerializeField, Range(0, 1)] private float normalOpacity = 1;
        private bool _hovered;
        private OwnerUiButtonSkin _semantic;
        private bool _lastSelected, _lastInteractable;
        private bool _pressed, _focused, _unread;
        private string _assetKey;
        private Sprite unread;
        private bool _underline;

        /// <summary>배경만 교체하고 텍스트·아이콘·콜백과 클릭 영역은 유지한다.</summary>
        public void Configure(Button target, string folder, string prefix, bool isPrimary, bool underline, float backgroundOpacity = 1)
        {
            button = target;
            _semantic = target.GetComponent<OwnerUiButtonSkin>();
            string key = folder + "/" + prefix;
            if (_assetKey == key && frame != null)
            {
                normalOpacity = backgroundOpacity;
                Refresh();
                return;
            }
            _assetKey = key;
            _underline = underline;
            frame = target.transform.Find("OwnerButtonFrame")?.GetComponent<Image>();
            if (frame == null && !underline) frame = target.GetComponent<Image>();
            if (frame == null)
            {
                var child = new GameObject("OwnerButtonFrame", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                child.transform.SetParent(target.transform, false);
                child.transform.SetAsFirstSibling();
                child.GetComponent<LayoutElement>().ignoreLayout = true;
                child.AddComponent<Baseball.Presentation.UI.CareerUiVisualElement>()
                    .Initialize(Baseball.Presentation.UI.CareerUiVisualRole.DataImage);
                frame = child.GetComponent<Image>();
            }
            if (frame == null) return;
            label = target.transform.Find("Label")?.GetComponent<Text>();
            primary = isPrimary;
            normalOpacity = backgroundOpacity;
            normal = UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Normal");
            hover = UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Hover");
            pressed = UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Pressed") ?? hover;
            disabled = UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Disabled");
            selected = UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Selected");
            unread = folder == "ListItems" ? UIOwnerFrontOfficeSkin.Load(folder + "/" + prefix + "_Unread") : null;
            frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 2;
            frame.color = Color.white; frame.canvasRenderer.SetColor(Color.white);
            frame.raycastTarget = frame.transform == target.transform;
            var backgroundRect = frame.rectTransform;
            if (frame.transform != target.transform)
            {
                backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            }
            if (underline)
            {
                var rect = frame.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right;
                rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(0, 4);
                var source = target.GetComponent<Image>();
                source.enabled = true; source.canvasRenderer.SetAlpha(0f); source.raycastTarget = true;
            }
            var oldRule = target.transform.Find("SelectedRule")?.GetComponent<Graphic>();
            if (underline && oldRule != null) oldRule.enabled = false;
            button.targetGraphic = frame; button.transition = Selectable.Transition.SpriteSwap;
            Refresh();
        }

        private void OnEnable() { _hovered = _pressed = false; if (button != null && frame != null) Refresh(); }
        private void OnDisable() { _hovered = _pressed = _focused = false; }
        private void LateUpdate()
        {
            if (button == null || frame == null) return;
            if (_lastSelected != (_semantic != null && _semantic.IsSelected) || _lastInteractable != button.IsInteractable()) Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; Refresh(); }
        public void OnPointerDown(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) { _pressed = true; Refresh(); } }
        public void OnPointerUp(PointerEventData eventData) { _pressed = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { _focused = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { _focused = false; Refresh(); }

        /// <summary>읽음 상태는 입력 포커스·지속 선택과 독립적으로 갱신한다.</summary>
        public void SetUnread(bool value) { _unread = value; Refresh(); }

        private void RefreshOpacity()
        {
            if (frame == null || button == null) return;
            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            float alpha = button.IsInteractable() && (_hovered || focused) ? 1 : normalOpacity;
            // 비선택 기본선을 숨겨 현재 탭을 분명히 한다.
            if (_underline && !_lastSelected && !_hovered && !focused) alpha = 0;
            if (!Mathf.Approximately(frame.color.a, alpha)) frame.color = new Color(1,1,1,alpha);
        }

        public void Refresh()
        {
            if (button == null || frame == null) return;
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.SpriteSwap;
            _focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            _lastSelected = _semantic != null && _semantic.IsSelected;
            _lastInteractable = button.IsInteractable();
            frame.sprite = _lastSelected && selected != null ? selected : _unread && unread != null ? unread : normal;
            var states = button.spriteState;
            states.highlightedSprite = hover; states.pressedSprite = pressed;
            states.selectedSprite = _lastSelected && selected != null ? selected : hover;
            states.disabledSprite = disabled; button.spriteState = states;
            frame.overrideSprite = !_lastInteractable ? disabled : _pressed && _hovered ? pressed
                : _underline && _lastSelected ? frame.sprite : _hovered || _focused ? hover : frame.sprite;
            RefreshOpacity();
            if (label != null) label.color = !_lastInteractable ? new Color32(174,185,196,255)
                : primary ? new Color32(9,24,39,255) : _lastSelected ? new Color32(221,180,92,255)
                : _underline && !UIOwnerFrontOfficePanel.HasDarkSurface(transform.parent)
                    ? Baseball.Presentation.UI.CareerUiTheme.ReferenceText : new Color32(233,237,240,255);
        }
    }
}

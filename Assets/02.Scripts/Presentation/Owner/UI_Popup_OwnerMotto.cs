using System;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Baseball.Presentation.Owner
{
    /// <summary>한마디 초안의 미리보기·입력 검증·저장 및 취소를 제공한다.</summary>
    public sealed class UI_Popup_OwnerMotto : MonoBehaviour
    {
        private RectTransform _modal;
        private InputField _input;
        private Text _preview, _feedback, _count;
        private Button _save, _cancel;
        private string _original;
        public event Action<string> SaveRequested;
        public event Action CloseRequested;

        /// <summary>공용 팝업 스킨과 입력 스타일로 편집창을 만든다.</summary>
        public static UI_Popup_OwnerMotto CreateRuntime(RectTransform host)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerMotto), host);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerMotto>();
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = CareerUiTheme.InputBlocker;
            blocker.raycastTarget = true;
            gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "MottoEditor", "구단주의 한마디 수정");
            _modal = panel.Root;
            _modal.anchorMin = _modal.anchorMax = new Vector2(.5f, .5f);
            _modal.sizeDelta = new Vector2(740, 460);
            Label(panel.Content, "Hint", "우리 구단의 다짐을 남겨보세요. 저장하면 구단주 정보에 표시됩니다.", .80f, .98f);
            var field = DefaultControls.CreateInputField(new DefaultControls.Resources());
            field.name = "MottoInput";
            field.transform.SetParent(panel.Content, false);
            _input = field.GetComponent<InputField>();
            _input.characterLimit = OwnerProfileState.MottoMaxLength;
            _input.lineType = InputField.LineType.SingleLine;
            foreach (var text in field.GetComponentsInChildren<Text>(true))
            {
                text.font = UIProjectFonts.Body;
                text.fontSize = 20;
                text.fontStyle = FontStyle.Normal;
                text.supportRichText = false;
            }
            ((Text)_input.placeholder).text = "구단을 위한 한마디를 입력하세요";
            OwnerDashboardStyle.SetDataInput(_input);
            Place((RectTransform)field.transform, .03f, .64f, .97f, .80f);
            _count = Label(panel.Content, "Count", "", .54f, .64f);
            _count.alignment = TextAnchor.MiddleRight;
            Label(panel.Content, "PreviewTitle", "미리보기", .44f, .54f).color = OwnerDashboardStyle.Gold;
            _preview = Label(panel.Content, "Preview", "", .28f, .44f);
            _preview.supportRichText = false;
            _feedback = Label(panel.Content, "Feedback", "", .14f, .28f);
            _cancel = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Cancel", "취소", () => CloseRequested?.Invoke());
            _save = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Save", "저장", Submit);
            OwnerUiButtonSkin.Apply(_save, OwnerButtonRole.Primary);
            Place((RectTransform)_cancel.transform, .51f, .01f, .72f, .13f);
            Place((RectTransform)_save.transform, .75f, .01f, .97f, .13f);
            _input.onValueChanged.AddListener(_ => RefreshDraft());
            _input.onEndEdit.AddListener(_ => RefreshDraft());
        }

        /// <summary>저장된 한마디를 초안으로 열고 한글 입력 포커스를 둔다.</summary>
        public void Show(string motto)
        {
            _original = motto;
            _input.SetTextWithoutNotify(motto);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FitModal();
            RefreshDraft();
            _input.Select();
            _input.ActivateInputField();
        }

        private void RefreshDraft()
        {
            string value = _input.text.Trim();
            _preview.text = value.Length == 0 ? "한마디를 입력하면 여기에 표시됩니다." : value;
            _count.text = $"{_input.text.Length} / {OwnerProfileState.MottoMaxLength}자";
            bool valid = value.Length > 0 && value.Length <= OwnerProfileState.MottoMaxLength;
            foreach (char character in value)
                if (char.IsControl(character) || character == '\u2028' || character == '\u2029') valid = false;
            bool changed = !string.Equals(value, _original, StringComparison.Ordinal);
            _save.interactable = valid && changed;
            _feedback.text = !valid ? "1~40자로, 줄바꿈 없이 입력해 주세요."
                : !changed ? "내용을 수정하면 저장할 수 있어요." : "저장을 누르면 새 한마디가 적용됩니다.";
            // 비활성 저장 버튼은 건너뛰고 팝업 내부에서만 방향 포커스를 순환한다.
            Link(_input, _cancel);
            Link(_cancel, _save.interactable ? (Selectable)_save : _input);
            if (_save.interactable) Link(_save, _input);
        }

        private void Submit()
        {
            if (!_save.interactable || !string.IsNullOrEmpty(UnityEngine.Input.compositionString)) return;
            SaveRequested?.Invoke(_input.text.Trim());
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tabKey.wasPressedThisFrame ||
                !string.IsNullOrEmpty(UnityEngine.Input.compositionString)) return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            bool reverse = keyboard.shiftKey.isPressed;
            Selectable next;
            if (selected == _input.gameObject) next = reverse && _save.interactable ? (Selectable)_save : _cancel;
            else if (selected == _cancel.gameObject) next = !reverse && _save.interactable ? (Selectable)_save : _input;
            else next = reverse ? (Selectable)_cancel : _input;
            if (next != _input) _input.DeactivateInputField();
            next.Select();
            if (next == _input) _input.ActivateInputField();
        }

        /// <summary>저장 실패 시 초안을 유지해 다시 시도할 수 있게 한다.</summary>
        public void ShowError() => _feedback.text = "저장하지 못했습니다. 입력 내용은 그대로예요. 다시 저장해 주세요.";

        /// <summary>초안을 적용하지 않고 편집창을 닫는다.</summary>
        public void Hide() { _input.DeactivateInputField(); gameObject.SetActive(false); }

        private void OnRectTransformDimensionsChange() { if (_modal != null) FitModal(); }
        private void FitModal()
        {
            Rect bounds = ((RectTransform)transform).rect;
            _modal.localScale = Vector3.one * Mathf.Clamp(Mathf.Min(bounds.width / 788f, bounds.height / 508f), .1f, 1f);
        }

        private static void Link(Selectable previous, Selectable next)
        {
            var before = previous.navigation;
            before.mode = Navigation.Mode.Explicit;
            before.selectOnRight = before.selectOnDown = next;
            previous.navigation = before;
            var after = next.navigation;
            after.mode = Navigation.Mode.Explicit;
            after.selectOnLeft = after.selectOnUp = previous;
            next.navigation = after;
        }

        private static Text Label(RectTransform parent, string name, string value, float bottom, float top)
        {
            var text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            Place(text.rectTransform, .03f, bottom, .97f, top);
            return text;
        }

        private static void Place(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}

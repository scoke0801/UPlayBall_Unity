using System;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>감독 방침의 0~4 정수 단계를 이미지 없는 다섯 칸 선택기로 표시한다.</summary>
    internal sealed class OwnerPolicyStepSelector
    {
        public const int MinimumLevel = 0;
        public const int MaximumLevel = 4;

        private const int StepCount = MaximumLevel - MinimumLevel + 1;
        private const float StepGap = 0.012f;

        private readonly Button[] _buttons = new Button[StepCount];
        private readonly Image[] _surfaces = new Image[StepCount];
        private readonly Text[] _labels = new Text[StepCount];
        private readonly Color _accent;
        private int _minimum = MinimumLevel;
        private int _maximum = MaximumLevel;
        private int _value = DugoutPolicySettings.NeutralLevel;

        public OwnerPolicyStepSelector(RectTransform root, Color accent)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            _accent = accent;
            float stepWidth = (1f - StepGap * (StepCount - 1)) / StepCount;
            for (int index = 0; index < StepCount; index++)
            {
                float left = index * (stepWidth + StepGap);
                RectTransform step = OwnerDugoutDetailUiFactory.CreateRect(
                    root,
                    "Step" + index,
                    left,
                    0f,
                    left + stepWidth,
                    1f);
                Image surface = step.gameObject.AddComponent<Image>();
                surface.sprite = null;
                surface.type = Image.Type.Simple;
                step.gameObject.AddComponent<CareerUiVisualElement>()
                    .Initialize(CareerUiVisualRole.FlatSurface);

                var outline = step.gameObject.AddComponent<Outline>();
                outline.effectColor = CareerUiTheme.ReferenceBorder;
                outline.effectDistance = new Vector2(1f, -1f);

                Button button = step.gameObject.AddComponent<Button>();
                button.targetGraphic = surface;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.84f, 0.87f, 0.90f, 1f);
                colors.disabledColor = Color.white;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.06f;
                button.colors = colors;

                Text label = OwnerDugoutDetailUiFactory.CreateLabel(
                    step,
                    "Label",
                    FormatLevel(index),
                    0f,
                    0f,
                    1f,
                    1f,
                    12,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter);
                label.raycastTarget = false;

                int level = index;
                button.onClick.AddListener(() => SetValue(level));
                _buttons[index] = button;
                _surfaces[index] = surface;
                _labels[index] = label;
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Detail);
            }

            RefreshVisuals();
        }

        public event Action<int> ValueChanged;

        public int Value => _value;

        public void SetRange(int minimum, int maximum)
        {
            int previous = _value;
            _minimum = Mathf.Clamp(minimum, MinimumLevel, MaximumLevel);
            _maximum = Mathf.Clamp(maximum, _minimum, MaximumLevel);
            _value = Mathf.Clamp(_value, _minimum, _maximum);
            RefreshVisuals();
            if (_value != previous) ValueChanged?.Invoke(_value);
        }

        public void SetValue(int value, bool notify = true)
        {
            int next = Mathf.Clamp(value, _minimum, _maximum);
            if (_value == next)
            {
                RefreshVisuals();
                return;
            }

            _value = next;
            RefreshVisuals();
            if (notify) ValueChanged?.Invoke(_value);
        }

        public void RefreshVisuals()
        {
            for (int index = 0; index < StepCount; index++)
            {
                bool isAllowed = index >= _minimum && index <= _maximum;
                bool isSelected = index == _value;
                _buttons[index].interactable = isAllowed;
                _surfaces[index].sprite = null;
                _surfaces[index].type = Image.Type.Simple;
                _surfaces[index].color = isSelected
                    ? _accent
                    : isAllowed
                        ? CareerUiTheme.ReferenceButton
                        : new Color(0.82f, 0.83f, 0.82f, 0.38f);
                _labels[index].color = isSelected
                    ? Color.white
                    : isAllowed
                        ? CareerUiTheme.ReferenceText
                        : new Color(0.42f, 0.45f, 0.47f, 0.62f);
            }
        }

        private static string FormatLevel(int level)
        {
            int offset = level - DugoutPolicySettings.NeutralLevel;
            if (offset == 0) return "중립";
            return offset > 0 ? "+" + offset : offset.ToString();
        }
    }

    /// <summary>덕아웃 상세 화면 세 장의 조밀한 패널·목록·액션 모양을 통일한다.</summary>
    internal static class OwnerDugoutDetailUiFactory
    {
        public static RectTransform CreatePanel(Transform parent, string name, float left, float bottom, float right, float top)
        {
            RectTransform rect = CreateRect(parent, name, left, bottom, right, top);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CareerUiTheme.ReferencePanel;
            image.raycastTarget = false;
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        public static Text CreateLabel(
            Transform parent,
            string name,
            string value,
            float left,
            float bottom,
            float right,
            float top,
            int size = 14,
            FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, anchor,
                CareerUiTheme.ReferenceText);
            Place(text.rectTransform, left, bottom, right, top);
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float left,
            float bottom,
            float right,
            float top,
            Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            Place((RectTransform)button.transform, left, bottom, right, top);
            return button;
        }

        public static RectTransform CreateScrollContent(
            Transform parent,
            string name,
            float left,
            float bottom,
            float right,
            float top,
            out ScrollRect scrollRect)
        {
            RectTransform root = CreateRect(parent, name, left, bottom, right, top);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(0.94f, 0.94f, 0.92f, 0.72f);
            var mask = root.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(root, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scrollRect.viewport = root;
            scrollRect.content = content;
            return content;
        }

        public static void ClearChildren(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }

        public static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static RectTransform CreateRect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }
    }
}

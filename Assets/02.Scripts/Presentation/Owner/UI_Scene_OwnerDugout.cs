using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>덕아웃 참조의 작전 방침, 감독·코치, 카드 네 칸을 표시하는 UI 전용 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerDugout : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.96f, 0.96f, 0.94f);
        private static readonly Color Border = new Color(0.65f, 0.67f, 0.67f);
        private static readonly Color Ink = new Color(0.18f, 0.22f, 0.25f);
        private static readonly Color Blue = new Color(0.04f, 0.36f, 0.70f);
        private static readonly Color Red = new Color(0.69f, 0.12f, 0.24f);
        private readonly Slider[] _sliders = new Slider[6];
        private RectTransform _root;
        private RectTransform _selectionOverlay;
        private Text _selectionTitle;
        private Text _selectionEmpty;
        private Text _status;

        /// <summary>셸의 전체 작업 영역에 덕아웃을 생성한다.</summary>
        public static UI_Scene_OwnerDugout CreateRuntime(RectTransform host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var view = new GameObject(nameof(UI_Scene_OwnerDugout)).AddComponent<UI_Scene_OwnerDugout>();
            view.Build(host);
            return view;
        }

        /// <summary>화면을 닫을 때 선택 창도 함께 닫는다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible) _selectionOverlay.gameObject.SetActive(false);
            _root.gameObject.SetActive(visible);
        }

        private void Build(RectTransform host)
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(host, "OwnerDugoutWorkspace", false);
            _root.offsetMin = new Vector2(16f, 16f);
            _root.offsetMax = new Vector2(-16f, -16f);
            Surface(_root, Paper);

            // 전체 참조의 40:15:45 열 비율을 유지하고 상세 참조의 여섯 방침 행을 넣는다.
            RectTransform board = Box(_root, "DugoutBoard", 0.015f, 0.10f, 0.985f, 0.975f, Paper);
            RectTransform policy = Box(board, "PolicyPanel", 0f, 0f, 0.40f, 1f, Paper);
            Label(policy, "PolicyTitle", "작전 방침", 0f, 0.93f, 1f, 1f, 24, Ink);
            CreatePolicy(policy, 0, "타격방침", "단타", "장타", "정확한 타격을 중시합니다.", "큰 것 한방을 적극적으로 노립니다.");
            CreatePolicy(policy, 1, "도루시도", "소극적", "적극적", "안정적인 주루를 중시합니다.", "기동력을 중시하는 야구를 펼칩니다.");
            CreatePolicy(policy, 2, "번트시도", "소극적", "적극적", "번트보다는 타격을 선호합니다.", "번트로 진루 기회를 만듭니다.");
            CreatePolicy(policy, 3, "대타기용", "소극적", "적극적", "선발 타자의 타격을 믿습니다.", "상황에 맞춰 대타를 적극 기용합니다.");
            CreatePolicy(policy, 4, "선발교체", "느리게", "빠르게", "선발을 믿고 긴 이닝을 책임지게 합니다.", "선발 투수를 일찍 교체합니다.");
            CreatePolicy(policy, 5, "중간교체", "느리게", "빠르게", "중계 투수에게 충분한 기회를 줍니다.", "중계 투수를 평소보다 일찍 교체합니다.");
            ActionButton(policy, "ResetPolicy", "작전 방침 없음", 0.02f, 0.075f, 0.98f, 0.16f, ResetPolicy);
            Label(policy, "PolicyHint", "방침을 움직여 설명을 확인하세요.", 0.03f, 0.005f, 0.97f, 0.07f, 16, Ink);

            RectTransform staff = Box(board, "StaffColumn", 0.41f, 0f, 0.55f, 1f, Paper);
            CreateStaff(staff, "Manager", "감독", 0.515f, 0.98f, new Color(0.98f, 0.92f, 0.73f));
            CreateStaff(staff, "HeadCoach", "수석코치", 0.02f, 0.485f, new Color(0.91f, 0.87f, 0.95f));
            RectTransform cards = Box(board, "CardSlots", 0.56f, 0f, 1f, 1f, Paper);
            for (int index = 0; index < 4; index++)
            {
                int column = index % 2;
                int row = index / 2;
                RectTransform card = Box(cards, "CardSlot" + index,
                    0.025f + column * 0.495f, 0.515f - row * 0.495f,
                    0.48f + column * 0.495f, 0.98f - row * 0.495f,
                    new Color(0.89f, 0.90f, 0.90f));
                CardBack(card);
            }

            _status = Label(_root, "PreviewStatus", "화면 미리보기 · 방침은 경기에 적용되지 않습니다.",
                0.02f, 0.01f, 0.47f, 0.08f, 16, Ink);
            Button sell = ActionButton(_root, "Sell", "판매", 0.49f, 0.015f, 0.64f, 0.075f, null);
            Button confirm = ActionButton(_root, "Confirm", "결정", 0.65f, 0.015f, 0.80f, 0.075f, null);
            ActionButton(_root, "Cancel", "취소", 0.81f, 0.015f, 0.96f, 0.075f, ResetPolicy);
            // 저장·판매 경로가 없으므로 성공한 것처럼 보이는 로컬 확정 동작을 만들지 않는다.
            sell.interactable = false;
            confirm.interactable = false;
            BuildSelectionOverlay();
        }

        private void CreatePolicy(RectTransform parent, int index, string title, string low, string high,
            string lowDescription, string highDescription)
        {
            float top = 0.925f - index * 0.125f;
            RectTransform row = Box(parent, "PolicyRow" + index, 0.02f, top - 0.12f, 0.98f, top, Color.white);
            Color accent = index < 4 ? Blue : Red;
            Label(row, "Name", title, 0f, 0f, 0.22f, 1f, 20, accent);
            Label(row, "Low", low, 0.25f, 0.69f, 0.49f, 0.98f, 13, Ink, TextAnchor.MiddleLeft);
            Label(row, "High", high, 0.70f, 0.69f, 0.97f, 0.98f, 13, Ink, TextAnchor.MiddleRight);
            Text description = Label(row, "Description", "균형 잡힌 방침을 사용합니다.",
                0.24f, 0.03f, 0.98f, 0.39f, 16, accent);
            RectTransform control = Rect(row, "PolicySlider", 0.27f, 0.42f, 0.94f, 0.72f);
            Surface(control, new Color(0.94f, 0.94f, 0.94f));
            var slider = control.gameObject.AddComponent<Slider>();
            RectTransform track = Box(control, "Track", 0f, 0.30f, 1f, 0.70f, Border);
            RectTransform fillArea = Rect(track, "FillArea", 0f, 0f, 1f, 1f);
            RectTransform fill = Rect(fillArea, "Fill", 0f, 0f, 1f, 1f);
            Surface(fill, accent);
            RectTransform handleArea = Rect(control, "HandleArea", 0f, 0f, 1f, 1f);
            RectTransform handle = Box(handleArea, "Handle", 0f, 0f, 0f, 1f, Paper);
            handle.sizeDelta = new Vector2(12f, 0f);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 4f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(2f);
            slider.onValueChanged.AddListener(value =>
            {
                description.text = value < 2f ? lowDescription : value > 2f ? highDescription : "균형 잡힌 방침을 사용합니다.";
                _status.text = "방침 미리보기 중 · 경기에 적용되지 않습니다.";
            });
            _sliders[index] = slider;
        }

        private void CreateStaff(RectTransform parent, string name, string title, float bottom, float top, Color tint)
        {
            RectTransform panel = Box(parent, name, 0.06f, bottom, 0.94f, top, tint);
            Label(panel, "Title", title, 0f, 0.85f, 1f, 1f, 21, name == "Manager" ? new Color(0.56f, 0.39f, 0.15f) : new Color(0.42f, 0.20f, 0.57f));
            RectTransform card = Box(panel, "StaffCard", 0.17f, 0.27f, 0.83f, 0.81f, new Color(0.81f, 0.82f, 0.82f));
            CardBack(card);
            ActionButton(panel, "Select", title == "감독" ? "감독 선택" : "코치 선택",
                0.10f, 0.04f, 0.90f, 0.20f, () => OpenSelection(title));
        }

        private void BuildSelectionOverlay()
        {
            _selectionOverlay = Rect(_root, "StaffSelectionOverlay", 0f, 0f, 1f, 1f);
            Surface(_selectionOverlay, new Color(0f, 0f, 0f, 0.55f));
            RectTransform dialog = Box(_selectionOverlay, "StaffSelectionDialog", 0.17f, 0.08f, 0.83f, 0.92f, Paper);
            _selectionTitle = Label(dialog, "Title", "감독 선택", 0.025f, 0.92f, 0.85f, 0.995f, 24, Blue, TextAnchor.MiddleLeft);
            ActionButton(dialog, "Close", "×", 0.92f, 0.935f, 0.98f, 0.99f, CloseSelection);
            RectTransform preview = Box(dialog, "SelectedCard", 0.025f, 0.17f, 0.40f, 0.91f, new Color(0.90f, 0.86f, 0.72f));
            CardBack(preview);
            RectTransform inventory = Box(dialog, "StaffInventory", 0.425f, 0.17f, 0.975f, 0.91f, Color.white);
            _selectionEmpty = Label(inventory, "EmptyState", string.Empty, 0.08f, 0.12f, 0.92f, 0.88f, 22, Ink);
            Button confirm = ActionButton(dialog, "Confirm", "결정", 0.28f, 0.035f, 0.49f, 0.12f, null);
            confirm.interactable = false;
            ActionButton(dialog, "Exit", "나가기", 0.51f, 0.035f, 0.72f, 0.12f, CloseSelection);
            _selectionOverlay.gameObject.SetActive(false);
        }

        private void OpenSelection(string title)
        {
            _selectionTitle.text = title + " 선택";
            _selectionEmpty.text = "선택 가능한 " + title + " 카드가 없습니다.\n\n카드 선택 기능 준비 중";
            _selectionOverlay.gameObject.SetActive(true);
            _selectionOverlay.SetAsLastSibling();
        }

        private void CloseSelection() => _selectionOverlay.gameObject.SetActive(false);

        private void ResetPolicy()
        {
            for (int index = 0; index < _sliders.Length; index++) _sliders[index].value = 2f;
            _status.text = "화면 미리보기 · 방침은 경기에 적용되지 않습니다.";
        }

        private static void CardBack(RectTransform parent)
        {
            RectTransform inset = Box(parent, "CardBackInset", 0.06f, 0.04f, 0.94f, 0.96f, new Color(0.92f, 0.93f, 0.92f));
            Label(inset, "CardBackLabel", "UPlayBall", 0.05f, 0.40f, 0.95f, 0.60f, 25, new Color(0.77f, 0.79f, 0.78f));
        }

        private static RectTransform Rect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Surface(RectTransform rect, Color color)
        {
            rect.gameObject.AddComponent<Image>().color = color;
        }

        private static RectTransform Box(Transform parent, string name, float left, float bottom, float right, float top, Color color)
        {
            RectTransform rect = Rect(parent, name, left, bottom, right, top);
            Surface(rect, color);
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static Text Label(Transform parent, string name, string text, float left, float bottom, float right, float top,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text label = OwnerWorkspaceUiFactory.CreateText(parent, name, text, size, FontStyle.Bold, alignment, color);
            Place(label.rectTransform, left, bottom, right, top);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = size;
            return label;
        }

        private static Button ActionButton(Transform parent, string name, string title, float left, float bottom,
            float right, float top, Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, title, action);
            Place((RectTransform)button.transform, left, bottom, right, top);
            return button;
        }

        private void OnDestroy()
        {
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>축약된 헤더 상태를 포인터와 키보드 포커스 양쪽에서 설명한다.</summary>
    public sealed class UIStatusHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private RectTransform _host, _hint;
        private string _message;
        public void Initialize(RectTransform host, string message) { _host = host; _message = message; }
        public void OnPointerEnter(PointerEventData eventData) => Show();
        public void OnPointerExit(PointerEventData eventData) => Hide();
        public void OnSelect(BaseEventData eventData) => Show();
        public void OnDeselect(BaseEventData eventData) => Hide();

        private void Show()
        {
            if (_host == null || _hint != null || string.IsNullOrWhiteSpace(_message)) return;
            _hint = new GameObject("StatusHint", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _hint.SetParent(_host, false);
            _hint.GetComponent<Image>().color = CareerUiTheme.PanelDark;
            _hint.GetComponent<Image>().raycastTarget = false;
            _hint.GetComponent<CanvasGroup>().blocksRaycasts = false;
            _hint.sizeDelta = new Vector2(340, 84);
            _hint.pivot = new Vector2(1, 1);
            var corners = new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
            Vector3 position = _host.InverseTransformPoint(corners[3]);
            position.x = Mathf.Clamp(position.x, _host.rect.xMin + 352, _host.rect.xMax - 12);
            position.y = Mathf.Clamp(position.y - ((RectTransform)transform).rect.height - 8, _host.rect.yMin + 96, _host.rect.yMax - 12);
            _hint.localPosition = position;
            var label = new GameObject("Label", typeof(RectTransform), typeof(UIProjectText)).GetComponent<Text>();
            label.transform.SetParent(_hint, false); label.font = UIProjectFonts.Default; label.fontSize = 22;
            label.text = _message; label.color = CareerUiTheme.TextPrimary; label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 8); label.rectTransform.offsetMax = new Vector2(-12, -8);
            _hint.gameObject.AddComponent<CareerUiPreserveTextColor>();
            if (Baseball.Presentation.Owner.UIOwnerFrontOfficeSkin.IsOwnerContext)
            {
                Baseball.Presentation.Owner.UIOwnerFrontOfficePanel.Apply(_hint, "CompactStrip");
                Baseball.Presentation.Owner.OwnerDashboardStyle.SetDataText(label);
                label.fontSize = 16;
            }
            // 긴 비활성 사유도 말줄임 없이 안전 영역 안에서 읽을 수 있게 높이를 계산한다.
            float width = Mathf.Min(340, Mathf.Max(120, _host.rect.width - 24));
            _hint.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            float height = Mathf.Min(Mathf.Max(64, label.preferredHeight + 16), Mathf.Max(64, _host.rect.height - 24));
            _hint.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            position.x = Mathf.Clamp(position.x, _host.rect.xMin + width + 12, _host.rect.xMax - 12);
            position.y = Mathf.Clamp(position.y, _host.rect.yMin + height + 12, _host.rect.yMax - 12);
            _hint.localPosition = position;
        }

        private void OnDisable() => Hide();
        private void OnDestroy() => Hide();
        private void Hide()
        {
            if (_hint == null) return;
            _hint.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(_hint.gameObject); else DestroyImmediate(_hint.gameObject);
            _hint = null;
        }
    }
}

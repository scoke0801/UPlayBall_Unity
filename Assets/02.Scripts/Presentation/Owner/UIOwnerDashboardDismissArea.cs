using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>홈 배경의 클릭만 받아 추천을 접으며 하위 버튼의 동작은 가로채지 않는다.</summary>
    public sealed class UIOwnerDashboardDismissArea : MonoBehaviour, IPointerDownHandler
    {
        private RectTransform _card;
        private Action _dismiss;
        public void Initialize(RectTransform card, Action dismiss) { _card = card; _dismiss = dismiss; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_card != null && !RectTransformUtility.RectangleContainsScreenPoint(_card, eventData.position, eventData.pressEventCamera))
                _dismiss?.Invoke();
        }
    }
}

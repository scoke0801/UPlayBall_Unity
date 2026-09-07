using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 카드 표면이 아닌 표 행에서도 PlayerMiniCardView와 같은 우클릭 상세 보기 규약을 쓰게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIRightClickDetailTrigger : MonoBehaviour, IPointerClickHandler
    {
        private Action _detailRequested;

        /// <summary>
        /// 대상이 우클릭을 받도록 Raycast 대상 Graphic을 보장한 뒤 상세 요청 핸들러를 붙인다.
        /// 배경 Image가 없는 행에도 붙일 수 있도록 투명 Image를 대신 만들어 준다.
        /// </summary>
        public static UIRightClickDetailTrigger Attach(RectTransform target, Action detailRequested)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (detailRequested == null) throw new ArgumentNullException(nameof(detailRequested));

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image surface = target.gameObject.AddComponent<Image>();
                surface.color = Color.clear;
                graphic = surface;
            }
            graphic.raycastTarget = true;

            UIRightClickDetailTrigger trigger = target.GetComponent<UIRightClickDetailTrigger>();
            if (trigger == null) trigger = target.gameObject.AddComponent<UIRightClickDetailTrigger>();
            trigger._detailRequested = detailRequested;
            return trigger;
        }

        /// <summary>왼쪽 클릭은 상위 ScrollRect·버튼에 그대로 넘기고 우클릭만 상세 보기로 쓴다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                _detailRequested?.Invoke();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>카드 포커스만 목록에 전달하고 휠·드래그 이벤트는 부모 ScrollRect에 남긴다.</summary>
    public sealed class UICardGridFocusRelay : MonoBehaviour, ISelectHandler
    {
        public Action Selected { get; set; }

        /// <summary>키보드·게임패드로 선택한 카드가 Viewport에 들어오도록 요청한다.</summary>
        public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
    }
}

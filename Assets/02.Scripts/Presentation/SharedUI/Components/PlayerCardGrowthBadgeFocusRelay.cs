using UnityEngine;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>상세 카드의 뒤집기 버튼 포커스를 실제 앞면 배지 설명으로 전달한다.</summary>
    public sealed class PlayerCardGrowthBadgeFocusRelay : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public PlayerCardGrowthBadgesView Target { get; set; }

        public void OnSelect(BaseEventData eventData)
        {
            if (Target != null && Target.isActiveAndEnabled) Target.OnSelect(eventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (Target != null && Target.isActiveAndEnabled) Target.OnDeselect(eventData);
        }
    }
}

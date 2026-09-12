using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>카드 뒷면에 피규어 화풍의 수비 필드 스프라이트를 표시한다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UICardDefenseField : Image
    {
        protected override void Awake()
        {
            base.Awake();
            sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_DefenseField");
            preserveAspect = true;
            raycastTarget = false;
        }

        /// <summary>위치 표시와 한국어 라벨을 원화의 내야·외야 좌표에 맞춘다.</summary>
        public static Vector2 FitPoint(Vector2 point) => new Vector2(.10f, .10f) + point * .80f;
    }
}

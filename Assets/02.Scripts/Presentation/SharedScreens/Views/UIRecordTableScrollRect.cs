using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>기록표 드래그를 한 축으로 고정하고 가로 입력의 이동량을 낮춘다.</summary>
    public sealed class UIRecordTableScrollRect : ScrollRect
    {
        [SerializeField, Range(0.1f, 1f)] private float _horizontalInputScale = 0.5f;

        private Vector2 _dragOrigin;
        private bool _hasDragAxis;
        private bool _isHorizontalDrag;

        /// <summary>새 드래그의 기준점을 저장한다.</summary>
        public override void OnBeginDrag(PointerEventData eventData)
        {
            _dragOrigin = eventData.position;
            Vector2 delta = eventData.position - eventData.pressPosition;
            _hasDragAxis = delta.sqrMagnitude > 0f;
            _isHorizontalDrag = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            base.OnBeginDrag(eventData);
        }

        /// <summary>처음 움직인 방향을 유지해 세로 탐색 중 가로 흔들림을 막는다.</summary>
        public override void OnDrag(PointerEventData eventData)
        {
            Vector2 position = eventData.position;
            Vector2 delta = position - _dragOrigin;
            if (!_hasDragAxis)
            {
                if (delta.sqrMagnitude == 0f) return;
                _isHorizontalDrag = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
                _hasDragAxis = true;
            }

            // 공유 입력을 잠시 투영하고 복원해 다른 이벤트 수신자에게 영향을 주지 않는다.
            eventData.position = _dragOrigin + (_isHorizontalDrag
                ? new Vector2(delta.x * _horizontalInputScale, 0f)
                : new Vector2(0f, delta.y));
            try { base.OnDrag(eventData); }
            finally { eventData.position = position; }
        }

        /// <summary>휠의 주 입력 축만 사용하며 가로 이동은 더 세밀하게 조절한다.</summary>
        public override void OnScroll(PointerEventData eventData)
        {
            Vector2 delta = eventData.scrollDelta;
            bool isHorizontalInput = Mathf.Abs(delta.x) > Mathf.Abs(delta.y);
            // 행이 화면에 모두 들어와도 세로 휠을 가로 이동으로 전환하지 않는다.
            if (isHorizontalInput ? !horizontal : !vertical) return;
            eventData.scrollDelta = isHorizontalInput
                ? new Vector2(delta.x * _horizontalInputScale, 0f)
                : new Vector2(0f, delta.y);
            try { base.OnScroll(eventData); }
            finally { eventData.scrollDelta = delta; }
        }
    }
}

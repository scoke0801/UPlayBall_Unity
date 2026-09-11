using UnityEngine;

namespace Baseball.Presentation.SharedUI
{
    public sealed partial class SharedGameShellView
    {
        private RectTransform _guideHost;
        private float _guideHeight;

        /// <summary>안내가 행동 바·선수단과 겹치지 않도록 셸 안에 전용 공간을 예약한다.</summary>
        public RectTransform SetGuideHeight(float height)
        {
            EnsureHierarchy();
            if (_guideHost == null) _guideHost = CreateRect("FrontManagerHost", transform);
            _guideHeight = Mathf.Max(0f, height);
            _guideHost.gameObject.SetActive(_guideHeight > 0f);
            UpdateWorkspaceOffsets();
            return _guideHost;
        }

        private float ApplyGuideReservation(float bottom)
        {
            if (_guideHost == null || _guideHeight <= 0f) return bottom;
            SetAnchors(_guideHost, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(16f, bottom), new Vector2(-16f, bottom + _guideHeight));
            return bottom + _guideHeight + WorkspaceGap;
        }
    }
}

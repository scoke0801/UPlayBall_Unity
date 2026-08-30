using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>화면 표시 뒤 동적으로 추가된 UI에도 다음 프레임에 공통 스킨을 적용한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerUiSkinObserver : MonoBehaviour
    {
        private bool _isDirty;

        private void OnTransformChildrenChanged()
        {
            _isDirty = true;
        }

        private void LateUpdate()
        {
            if (!_isDirty)
                return;

            _isDirty = false;
            CareerUiSkin.Apply(transform);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>커리어 UI Image가 담당하는 시각 역할을 명시한다.</summary>
    public enum CareerUiVisualRole
    {
        DecorativeFrame,
        FlatSurface,
        InteractiveControl,
        DataImage,
        Divider,
        InputBlocker
    }

    /// <summary>이름 추정 없이 Image의 시각 역할과 프레임 Variant를 공통 스킨에 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerUiVisualElement : MonoBehaviour
    {
        [SerializeField] private CareerUiVisualRole _role;
        [SerializeField] private bool _isHeroFrame;

        public CareerUiVisualRole Role => _role;
        public bool IsHeroFrame => _isHeroFrame;

        public void Initialize(CareerUiVisualRole role, bool isHeroFrame = false)
        {
            _role = role;
            _isHeroFrame = isHeroFrame;
        }
    }

    /// <summary>장식 프레임 한 장과 그 프레임의 안전한 콘텐츠·상호작용 영역을 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CareerUiFrame : MonoBehaviour
    {
        [SerializeField] private Image _decorativeFrame;
        [SerializeField] private RectTransform _headerRoot;
        [SerializeField] private RectTransform _contentSafeArea;
        [SerializeField] private RectTransform _interactionRoot;
        [SerializeField] private Vector4 _contentPadding;
        [SerializeField] private bool _isHero;

        public Image DecorativeFrame => _decorativeFrame;
        public RectTransform HeaderRoot => _headerRoot;
        public RectTransform ContentSafeArea => _contentSafeArea;
        public RectTransform InteractionRoot => _interactionRoot;
        public Vector4 ContentPadding => _contentPadding;
        public bool IsHero => _isHero;

        public void Initialize(
            Image decorativeFrame,
            RectTransform headerRoot,
            RectTransform contentSafeArea,
            RectTransform interactionRoot,
            Vector4 contentPadding,
            bool isHero)
        {
            _decorativeFrame = decorativeFrame;
            _headerRoot = headerRoot;
            _contentSafeArea = contentSafeArea;
            _interactionRoot = interactionRoot;
            _contentPadding = contentPadding;
            _isHero = isHero;
        }

        public bool ContainsContent(Transform target)
        {
            return target != null && _contentSafeArea != null && target.IsChildOf(_contentSafeArea);
        }
    }
}

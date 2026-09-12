using UnityEngine;
using UnityEngine.UI;
using Baseball.Presentation.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>패널의 V2 역할을 보존해 재진입과 공통 Theme 재적용에 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIOwnerFrontOfficePanel : MonoBehaviour
    {
        [SerializeField] private string frameName = "ManagerReport";
        private Image _image;

        /// <summary>가장 가까운 표면을 확인해 흰 기록표 내부의 글자색은 유지한다.</summary>
        public static bool HasDarkSurface(Transform parent)
        {
            for (var current = parent; current != null; current = current.parent)
            {
                if (current.GetComponent<UIOwnerFrontOfficePanel>() != null) return true;
                var mask = current.GetComponent<Mask>();
                if (mask != null && !mask.showMaskGraphic) continue;
                var image = current.GetComponent<Image>();
                if (image != null && image.enabled && image.color.a > .15f)
                    return image.color.grayscale < .45f;
            }
            return false;
        }

        /// <summary>표준 본문 색만 네이비 프레임에 맞추고 성과·경고 의미색은 보존한다.</summary>
        public static Color ResolveTextColor(Color color)
        {
            if (color == CareerUiTheme.ReferenceText || color == CareerUiTheme.TextPrimary) return OwnerDashboardStyle.Ivory;
            if (color == UIClubOfficeStyle.Ink || color == CareerUiTheme.ReferenceDataInk) return OwnerDashboardStyle.Ivory;
            if (color == UIClubOfficeStyle.Muted) return OwnerDashboardStyle.Muted;
            if (color == CareerUiTheme.ReferenceAccent || color == CareerUiTheme.ReferenceDataAccent
                || color == UIClubOfficeStyle.Blue) return OwnerDashboardStyle.Gold;
            if (color == CareerUiTheme.ReferenceTextSecondary || color == CareerUiTheme.TextSecondary
                || color == CareerUiTheme.TextMuted) return OwnerDashboardStyle.Muted;
            return color;
        }

        /// <summary>작업면을 소유한 컨테이너에 프레임을 연결해 형제 본문도 같은 표면을 참조하게 한다.</summary>
        public static void ApplyWorkspace(RectTransform root)
        {
            if (root.GetComponent<Image>() == null) root.gameObject.AddComponent<Image>().raycastTarget = false;
            Apply(root, "ManagerReport");
        }

        /// <summary>이미 생성된 콘텐츠와 입력 영역을 유지하고 표면만 지정한다.</summary>
        public static void Apply(RectTransform root, string frameName)
        {
            if (root == null || root.GetComponent<Image>() == null) return;
            var skin = root.GetComponent<UIOwnerFrontOfficePanel>() ?? root.gameObject.AddComponent<UIOwnerFrontOfficePanel>();
            skin.frameName = frameName;
            skin.Refresh();
        }

        private void OnEnable() => Refresh();

        /// <summary>자신의 장식만 교체한다. 하위 초상·마스크·표는 건드리지 않는다.</summary>
        public void Refresh()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;
            _image.sprite = UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_" + frameName);
            _image.overrideSprite = null;
            _image.type = Image.Type.Sliced;
            _image.pixelsPerUnitMultiplier = 2f;
            _image.color = Color.white;
            var outline = GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            var gradient = GetComponent<UIOwnerSurfaceGradient>();
            if (gradient != null) gradient.enabled = false;
            var frame = transform.Find("OwnerPanelFrame")?.GetComponent<UIOwnerPanelFrame>();
            if (frame != null) frame.enabled = false;
        }
    }
}

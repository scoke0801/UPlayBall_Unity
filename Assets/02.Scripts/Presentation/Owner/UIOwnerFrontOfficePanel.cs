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
        [SerializeField] private bool hasOpaqueCornerFrame;
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

        /// <summary>독립 작업면과 팝업에 불투명 본문 및 골드 코너 프레임을 적용한다.</summary>
        public static void ApplyFramedSurface(RectTransform root)
        {
            if (root == null || root.GetComponent<Image>() == null) return;
            var skin = root.GetComponent<UIOwnerFrontOfficePanel>() ?? root.gameObject.AddComponent<UIOwnerFrontOfficePanel>();
            skin.frameName = "ManagerReport";
            skin.hasOpaqueCornerFrame = true;
            skin.Refresh();
        }

        private void OnEnable() => Refresh();

        /// <summary>자신의 장식만 교체한다. 하위 초상·마스크·표는 건드리지 않는다.</summary>
        public void Refresh()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;
            // 일반 업무면은 하나의 얇은 프레임을 공유한다. 홈의 경기·매니저 Hero는 원래 자산을 유지한다.
            bool isWorkSurface = frameName == "ManagerReport" || frameName == "CompactStrip";
            _image.sprite = isWorkSurface
                ? UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_Surface")
                : UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_" + frameName);
            _image.overrideSprite = null;
            _image.type = Image.Type.Sliced;
            _image.pixelsPerUnitMultiplier = frameName == "Speech" ? 8f : 2f;
            _image.color = Color.white;
            // 중첩 패널은 표면 깊이를 더하지 않는다. 제목과 ContentSafeRect는 그대로 유지한다.
            if (isWorkSurface && transform.parent != null &&
                transform.parent.GetComponentInParent<UIOwnerFrontOfficePanel>() != null)
                _image.color = new Color(1, 1, 1, .35f);
            bool isMatchPanel = frameName == "MainDashboard";
            if (hasOpaqueCornerFrame) RefreshOpaqueCornerFrame();
            else
            {
                // 역할 전환 시 이전 프레임의 자식 면이 새 스킨 위에 남지 않게 한다.
                var decoration = transform.Find("CornerFrameDecoration");
                if (decoration != null) decoration.gameObject.SetActive(false);
                var backing = transform.Find("OpaqueFrameBacking");
                if (backing != null) backing.gameObject.SetActive(false);
            }
            if (isMatchPanel)
            {
                // PNG에 그려진 상단 띠와 본문 경계 대신 하나의 불투명 면을 사용한다.
                _image.sprite = null;
                _image.color = OwnerDashboardStyle.TableSurface;
                UIOwnerPanelFrame.Attach((RectTransform)transform, true);
            }
            var outline = GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            var gradient = GetComponent<UIOwnerSurfaceGradient>();
            if (gradient != null) gradient.enabled = false;
            var frame = transform.Find("OwnerPanelFrame")?.GetComponent<UIOwnerPanelFrame>();
            if (frame != null) frame.enabled = isMatchPanel;
        }

        private void RefreshOpaqueCornerFrame()
        {
            // PNG 본문의 알파도 남아 있으므로 틴트만 복원하지 않고 별도의 불투명 면을 받친다.
            _image.sprite = null;
            _image.color = Color.clear;
            Image decoration = GetFrameLayer("CornerFrameDecoration");
            decoration.gameObject.SetActive(true);
            decoration.sprite = UIOwnerFrontOfficeSkin.Load("Frames/UI_Frame_" + frameName);
            decoration.type = Image.Type.Sliced;
            decoration.pixelsPerUnitMultiplier = 2f;
            decoration.color = Color.white;
            decoration.transform.SetAsFirstSibling();

            Image backing = GetFrameLayer("OpaqueFrameBacking");
            backing.gameObject.SetActive(true);
            Color surface = OwnerDashboardStyle.Surface;
            surface.a = 1f;
            backing.color = surface;
            // 둥근 외곽 밖으로 사각 배경이 나오지 않게 금속 테두리 안쪽까지만 채운다.
            backing.rectTransform.offsetMin = Vector2.one * 8f;
            backing.rectTransform.offsetMax = Vector2.one * -8f;
            backing.transform.SetAsFirstSibling();
        }

        private Image GetFrameLayer(string name)
        {
            Transform existing = transform.Find(name);
            if (existing != null) return existing.GetComponent<Image>();
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            layer.transform.SetParent(transform, false);
            layer.raycastTarget = false;
            layer.rectTransform.anchorMin = Vector2.zero;
            layer.rectTransform.anchorMax = Vector2.one;
            layer.rectTransform.offsetMin = layer.rectTransform.offsetMax = Vector2.zero;
            layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            layer.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            return layer;
        }
    }
}

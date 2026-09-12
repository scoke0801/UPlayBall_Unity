using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    public sealed partial class SharedGameShellView
    {
        private const float OwnerTopBarHeight = 52f;
        private const float OwnerNavigationHeight = 68f;
        private const float HeaderSecondaryRowRatio = 0.38f;
        private RectTransform _ownerStatusPlate;

        private float ApplyReferenceChrome(bool isOwner)
        {
            float headerHeight = isOwner ? OwnerTopBarHeight : TopBarHeight;
            float navigationHeight = isOwner ? OwnerNavigationHeight : NavigationHeight;
            float chromeHeight = headerHeight + navigationHeight;
            SetAnchors(_globalTopBar, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -headerHeight), Vector2.zero);
            _globalTopBar.GetComponent<Image>().color = isOwner ? CareerUiTheme.ReferencePanelHeader : TopBar;
            _globalTopBar.Find("BottomBorder").GetComponent<Image>().color = isOwner ? CareerUiTheme.ReferenceBorder : GoldAccent;
            if (_ownerStatusPlate == null)
            {
                _ownerStatusPlate = CreateAnchoredImage("OwnerStatusPlate", _globalTopBar, TopBar,
                    new Vector2(.13f, 0f), new Vector2(.94f, 1f), Vector2.zero, Vector2.zero);
                _ownerStatusPlate.SetAsFirstSibling();
            }
            _ownerStatusPlate.gameObject.SetActive(isOwner);
            RectTransform brand = (RectTransform)_globalTopBar.Find("Brand");
            SetAnchors(brand, Vector2.zero, new Vector2(isOwner ? .13f : 0f, 1f),
                new Vector2(20f, 0f), new Vector2(isOwner ? -12f : 250f, 0f));
            Text logo = brand.Find("GameName").GetComponent<Text>();
            logo.fontSize = isOwner ? 20 : 25;
            logo.color = isOwner ? DarkText : TextPrimary;
            ConfigureHeaderTextRows(logo, _modeNameText, isOwner, -20f);
            if (brand.Find("GameLogo") != null)
            {
                // 낮은 헤더에서는 로고와 모드명을 좌우로 나눠 로고의 세로 공간을 확보한다.
                SetAnchors(_modeNameText.rectTransform, new Vector2(.60f, 0f), Vector2.one,
                    Vector2.zero, Vector2.zero);
                _modeNameText.alignment = TextAnchor.MiddleLeft;
            }
            _modeNameText.color = isOwner ? CareerUiTheme.ReferenceAccent : AccentLight;
            SetAnchors((RectTransform)_globalTopBar.Find("TeamStatus"), new Vector2(isOwner ? .14f : 0f, 0f),
                new Vector2(isOwner ? .40f : 0f, 1f),
                new Vector2(isOwner ? 0f : 270f, 0f),
                new Vector2(isOwner ? -12f : 790f, 0f));
            _teamNameText.fontSize = isOwner ? 18 : 20;
            ConfigureHeaderTextRows(_teamNameText, _commonStatusText, isOwner, -22f);
            _nextMatchText.gameObject.SetActive(!isOwner);
            _globalTopBar.Find("NextMatchAccent").gameObject.SetActive(!isOwner);
            _globalTopBar.Find("BrandDivider").gameObject.SetActive(!isOwner);
            _globalTopBar.Find("TeamDivider").gameObject.SetActive(!isOwner);
            SetAnchors(_statusSlotHost, new Vector2(isOwner ? .40f : .63f, 0f),
                new Vector2(isOwner ? .94f : 1f, 1f), new Vector2(0f, isOwner ? 1f : 7f),
                new Vector2(isOwner ? -8f : -78f, isOwner ? -1f : -7f));
            RectTransform settings = (RectTransform)_globalTopBar.Find("GlobalSettings");
            settings.GetComponent<Image>().color = isOwner ? CareerUiTheme.ReferenceButton : StatusSurface;
            settings.Find("Label").GetComponent<Text>().color = isOwner ? DarkText : TextSecondary;
            if (isOwner)
            {
                OwnerUiButtonSkin.Apply(settings.GetComponent<Button>(), OwnerButtonRole.Detail);
                OwnerUiButtonSkin.Apply(_backButton, OwnerButtonRole.Detail);
            }
            else
            {
                OwnerUiButtonSkin.Restore(settings.GetComponent<Button>());
                OwnerUiButtonSkin.Restore(_backButton);
                settings.Find("Label").GetComponent<Text>().color = TextSecondary;
                _backButtonLabel.color = TextPrimary;
            }
            SetAnchors(_primaryNavigation, new Vector2(isOwner ? .18f : 0f, 1f),
                new Vector2(isOwner ? .82f : 1f, 1f),
                new Vector2(0f, -chromeHeight), new Vector2(0f, -headerHeight));
            _navigationEntryHost.GetComponent<HorizontalLayoutGroup>().childAlignment =
                isOwner ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            SetAnchors(_contextHeader, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -(chromeHeight + ContextHeaderHeight)), new Vector2(0f, -chromeHeight));
            return chromeHeight;
        }

        private static void ConfigureHeaderTextRows(
            Text primary,
            Text secondary,
            bool isOwner,
            float playerPrimaryTopOffset)
        {
            if (!isOwner)
            {
                SetAnchors(primary.rectTransform, Vector2.zero, Vector2.one,
                    Vector2.zero, new Vector2(0f, playerPrimaryTopOffset));
                primary.alignment = TextAnchor.MiddleLeft;
                SetAnchors(secondary.rectTransform, Vector2.zero, Vector2.one,
                    Vector2.zero, new Vector2(0f, 6f));
                secondary.alignment = TextAnchor.LowerLeft;
                return;
            }

            // Owner Header는 공용 Header보다 낮으므로 정렬만으로 두 Text를 나누면 글리프가 겹친다.
            // 서로 만나지 않는 Rect를 부여해 폰트와 해상도가 달라도 두 행의 경계를 보존한다.
            SetAnchors(primary.rectTransform,
                new Vector2(0f, HeaderSecondaryRowRatio), Vector2.one,
                new Vector2(0f, 2f), new Vector2(0f, -2f));
            primary.alignment = TextAnchor.MiddleLeft;
            SetAnchors(secondary.rectTransform,
                Vector2.zero, new Vector2(1f, HeaderSecondaryRowRatio),
                new Vector2(0f, 2f), Vector2.zero);
            secondary.alignment = TextAnchor.MiddleLeft;
        }

        private static void AddOwnerNavigationIcon(RectTransform parent, string routeId, Text label)
        {
            if (string.Equals(routeId, OwnerNavigationRoutes.Shop, System.StringComparison.Ordinal))
            {
                Texture2D shopIcon = Resources.Load<Texture2D>("UI/Shop/shop_navigation_icon_v2");
                if (shopIcon == null) return;
                RectTransform shopRect = CreateRect("Icon", parent);
                // 원본의 4:3 Canvas 비율만 보존한다. FitInParent는 메뉴 셀 전체로 확대되어 하단 라벨을 침범한다.
                SetAnchors(shopRect, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                    new Vector2(-24f, -38f), new Vector2(24f, -2f));
                RawImage shopImage = shopRect.gameObject.AddComponent<RawImage>();
                shopImage.texture = shopIcon;
                shopImage.raycastTarget = false;
                SetAnchors(label.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                    new Vector2(8f, 2f), new Vector2(-8f, 20f));
                label.fontSize = 16;
                return;
            }

            int index = routeId switch
            {
                OwnerNavigationRoutes.Home => 0,
                OwnerNavigationRoutes.Roster => 1,
                OwnerNavigationRoutes.PowerUp => 2,
                OwnerNavigationRoutes.Dugout => 3,
                OwnerNavigationRoutes.Club => 4,
                OwnerNavigationRoutes.League => 5,
                _ => -1
            };
            if (index < 0) return;
            Texture2D atlas = Resources.Load<Texture2D>("UI/Generated/owner_navigation_atlas_v1");
            if (atlas == null) return;
            RectTransform rect = CreateRect("Icon", parent);
            SetAnchors(rect, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(-18f, -38f), new Vector2(18f, -2f));
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            // 셀을 UV로 참조해 별도 Texture 복사와 런타임 Sprite 할당을 피한다.
            image.uvRect = new Rect((index % 3) / 3f, index < 3 ? .5f : 0f, 1f / 3f, .5f);
            image.raycastTarget = false;
            SetAnchors(label.rectTransform, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(8f, 2f), new Vector2(-8f, 20f));
            label.fontSize = 16;
        }
    }
}

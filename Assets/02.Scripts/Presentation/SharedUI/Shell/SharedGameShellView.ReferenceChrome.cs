using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    public sealed partial class SharedGameShellView
    {
        private const float OwnerTopBarHeight = 52f;
        private const float OwnerNavigationHeight = 52f;
        private const float HeaderSecondaryRowRatio = 0.38f;
        private RectTransform _ownerStatusPlate;
        private Button _globalSave;
        private RectTransform _ownerLogoDivider;
        private Text _ownerIdentityLabels;
        private Text _ownerLeagueText;
        private Text _ownerScheduleText;

        private float ApplyReferenceChrome(bool isOwner)
        {
            float headerHeight = isOwner ? OwnerTopBarHeight : TopBarHeight;
            float navigationHeight = isOwner ? OwnerNavigationHeight : NavigationHeight;
            float chromeHeight = headerHeight + navigationHeight;
            SetAnchors(_globalTopBar, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -headerHeight), Vector2.zero);
            _globalTopBar.GetComponent<Image>().color = isOwner ? OwnerDashboardStyle.Ink : TopBar;
            _globalTopBar.Find("BottomBorder").GetComponent<Image>().color = isOwner ? OwnerDashboardStyle.Line : GoldAccent;
            if (_ownerStatusPlate == null)
            {
                _ownerStatusPlate = CreateAnchoredImage("OwnerStatusPlate", _globalTopBar, TopBar,
                    new Vector2(.13f, 0f), new Vector2(.94f, 1f), Vector2.zero, Vector2.zero);
                _ownerStatusPlate.SetAsFirstSibling();
            }
            _ownerStatusPlate.gameObject.SetActive(isOwner);
            if (isOwner) _ownerStatusPlate.GetComponent<Image>().color = OwnerDashboardStyle.Ink;
            RectTransform brand = (RectTransform)_globalTopBar.Find("Brand");
            SetAnchors(brand, Vector2.zero, new Vector2(isOwner ? .13f : 0f, 1f),
                new Vector2(20f, 0f), new Vector2(isOwner ? -12f : 250f, 0f));
            Text logo = brand.Find("GameName").GetComponent<Text>();
            logo.fontSize = isOwner ? 20 : 25;
            logo.color = TextPrimary;
            ConfigureHeaderTextRows(logo, _modeNameText, isOwner, -20f);
            if (brand.Find("GameLogo") != null)
            {
                // 낮은 헤더에서는 로고와 모드명을 좌우로 나눠 로고의 세로 공간을 확보한다.
                SetAnchors(_modeNameText.rectTransform, new Vector2(.60f, 0f), Vector2.one,
                    Vector2.zero, Vector2.zero);
                _modeNameText.alignment = TextAnchor.MiddleLeft;
            }
            _modeNameText.color = isOwner ? TextPrimary : AccentLight;
            SetAnchors((RectTransform)_globalTopBar.Find("TeamStatus"), new Vector2(isOwner ? .14f : 0f, 0f),
                new Vector2(isOwner ? .40f : 0f, 1f),
                new Vector2(isOwner ? 0f : 270f, 0f),
                new Vector2(isOwner ? -12f : 790f, 0f));
            _teamNameText.fontSize = isOwner ? 18 : 20;
            _commonStatusText.fontSize = 13;
            _commonStatusText.color = TextSecondary;
            ConfigureHeaderTextRows(_teamNameText, _commonStatusText, isOwner, -22f);
            _nextMatchText.gameObject.SetActive(!isOwner);
            _globalTopBar.Find("NextMatchAccent").gameObject.SetActive(!isOwner);
            ConfigureBrandDividers(brand, isOwner);
            ConfigureOwnerIdentity(brand, isOwner);
            _globalTopBar.Find("TeamDivider").gameObject.SetActive(!isOwner);
            SetAnchors(_statusSlotHost, new Vector2(isOwner ? .40f : .63f, 0f),
                new Vector2(isOwner ? .94f : 1f, 1f), new Vector2(0f, isOwner ? 1f : 7f),
                new Vector2(isOwner ? -8f : -78f, isOwner ? -1f : -7f));
            RectTransform settings = (RectTransform)_globalTopBar.Find("GlobalSettings");
            if (_globalSave == null)
            {
                _globalSave = OwnerWorkspaceUiFactory.CreateButton(_globalTopBar, "GlobalSave", "저장", () => SaveRequested?.Invoke());
                SetAnchors((RectTransform)_globalSave.transform, new Vector2(1, 0), Vector2.one,
                    new Vector2(-130, 4), new Vector2(-70, -4));
                OwnerUiButtonSkin.Apply(_globalSave, OwnerButtonRole.Utility);
            }
            _globalSave.gameObject.SetActive(isOwner);
            if (isOwner)
            {
                // 자원은 저장 버튼 앞에 우측 정렬하고 리그·일정은 왼쪽 구단 정보에 붙인다.
                SetAnchors(_statusSlotHost, new Vector2(.36f, 0), Vector2.one,
                    new Vector2(12, 1), new Vector2(-154, -1));
                SetAnchors(settings, new Vector2(1, 0), Vector2.one, new Vector2(-66, 4), new Vector2(-8, -4));
            }
            settings.GetComponent<Image>().color = isOwner ? CareerUiTheme.ReferenceButton : StatusSurface;
            settings.Find("Label").GetComponent<Text>().color = isOwner ? DarkText : TextSecondary;
            if (isOwner)
            {
                OwnerUiButtonSkin.Apply(settings.GetComponent<Button>(), OwnerButtonRole.Utility);
                OwnerUiButtonSkin.Apply(_backButton, OwnerButtonRole.Detail);
            }
            else
            {
                SetAnchors(settings, new Vector2(1, 0), Vector2.one, new Vector2(-66, 9), new Vector2(-14, -9));
                OwnerUiButtonSkin.Restore(settings.GetComponent<Button>());
                OwnerUiButtonSkin.Restore(_backButton);
                settings.Find("Label").GetComponent<Text>().color = TextSecondary;
                _backButtonLabel.color = TextPrimary;
            }
            SetAnchors(_primaryNavigation, new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -chromeHeight), new Vector2(0f, -headerHeight));
            _navigationEntryHost.GetComponent<HorizontalLayoutGroup>().childAlignment =
                isOwner ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            if (isOwner)
                SetAnchors(_navigationEntryHost, Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-24, 0));
            else
                SetAnchors(_navigationEntryHost, Vector2.zero, Vector2.one, new Vector2(18, 5), new Vector2(-18, -5));
            SetAnchors(_contextHeader, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -(chromeHeight + ContextHeaderHeight)), new Vector2(0f, -chromeHeight));
            _contextHeader.GetComponent<Image>().color = isOwner ? OwnerDashboardStyle.TableSurface : ContextSurface;
            _contextTitleText.color = isOwner ? OwnerDashboardStyle.Ivory : DarkText;
            _contextSummaryText.color = isOwner ? OwnerDashboardStyle.TableSecondary : Color.Lerp(DarkText, ContextSurface, .22f);
            return chromeHeight;
        }

        private void ConfigureBrandDividers(RectTransform brand, bool isOwner)
        {
            if (_ownerLogoDivider == null)
                _ownerLogoDivider = CreateAnchoredImage("LogoDivider", brand, Divider,
                    new Vector2(.59f, 0f), new Vector2(.59f, 1f),
                    new Vector2(0f, 10f), new Vector2(1f, -10f));
            _ownerLogoDivider.gameObject.SetActive(isOwner);
            _ownerLogoDivider.GetComponent<Image>().color = OwnerDashboardStyle.Line;
            if (isOwner)
            {
                SetAnchors(_modeNameText.rectTransform, new Vector2(.60f, 0f), Vector2.one,
                    new Vector2(8f, 0f), Vector2.zero);
                _modeNameText.alignment = TextAnchor.MiddleLeft;
                RectTransform logo = (RectTransform)brand.Find("GameLogo");
                if (logo == null)
                    SetAnchors(brand.Find("GameName").GetComponent<RectTransform>(),
                        Vector2.zero, new Vector2(.58f, 1f), Vector2.zero, Vector2.zero);
            }

            RectTransform divider = (RectTransform)_globalTopBar.Find("BrandDivider");
            divider.gameObject.SetActive(true);
            SetAnchors(divider, new Vector2(isOwner ? .13f : 0f, 0f),
                new Vector2(isOwner ? .13f : 0f, 1f),
                new Vector2(isOwner ? 0f : 260f, isOwner ? 10f : 13f),
                new Vector2(isOwner ? 1f : 261f, isOwner ? -10f : -13f));
            divider.GetComponent<Image>().color = isOwner ? OwnerDashboardStyle.Line : Divider;
        }

        private void ConfigureOwnerIdentity(RectTransform brand, bool isOwner)
        {
            RectTransform identity = (RectTransform)_globalTopBar.Find("TeamStatus");
            if (_ownerIdentityLabels == null)
            {
                _ownerIdentityLabels = CreateText("OwnerIdentityLabels", identity,
                    "구단주명\n구단명", 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                    OwnerDashboardStyle.Success);
                _ownerLeagueText = CreateText("OwnerLeague", _globalTopBar, string.Empty,
                    16, FontStyle.Normal, TextAnchor.MiddleRight, TextPrimary);
                _ownerScheduleText = CreateText("OwnerSchedule", _globalTopBar, string.Empty,
                    13, FontStyle.Normal, TextAnchor.MiddleRight, TextSecondary);
            }
            _ownerIdentityLabels.gameObject.SetActive(isOwner);
            _ownerLeagueText.gameObject.SetActive(isOwner);
            _ownerScheduleText.gameObject.SetActive(isOwner);
            _modeNameText.gameObject.SetActive(!isOwner);
            _teamNameText.horizontalOverflow = isOwner ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            _commonStatusText.horizontalOverflow = isOwner ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            RectTransform logo = (RectTransform)brand.Find("GameLogo");
            if (logo != null)
                SetAnchors(logo, Vector2.zero, new Vector2(isOwner ? 1f : .58f, 1f),
                    Vector2.zero, Vector2.zero);
            if (!isOwner) return;

            _ownerLogoDivider.gameObject.SetActive(false);
            SetAnchors(brand, Vector2.zero, new Vector2(.08f, 1f),
                new Vector2(20f, 0f), new Vector2(-12f, 0f));
            SetAnchors((RectTransform)_globalTopBar.Find("BrandDivider"),
                new Vector2(.08f, 0f), new Vector2(.08f, 1f),
                new Vector2(0f, 10f), new Vector2(1f, -10f));
            SetAnchors(identity, new Vector2(.09f, 0f), new Vector2(.22f, 1f),
                Vector2.zero, new Vector2(-12f, 0f));
            SetAnchors(_ownerIdentityLabels.rectTransform, Vector2.zero, new Vector2(0f, 1f),
                new Vector2(0f, 4f), new Vector2(80f, -4f));
            // 이름과 구단을 같은 높이의 두 행으로 분리하고 라벨 너비를 보장한다.
            SetAnchors(_teamNameText.rectTransform, new Vector2(0f, .5f), Vector2.one,
                new Vector2(84f, 0f), new Vector2(0f, -4f));
            SetAnchors(_commonStatusText.rectTransform, Vector2.zero, new Vector2(1f, .5f),
                new Vector2(84f, 4f), Vector2.zero);
            _teamNameText.fontSize = 15;
            _commonStatusText.fontSize = 15;
            _commonStatusText.color = TextPrimary;
            _ownerIdentityLabels.font = UIProjectFonts.Body;
            _ownerLeagueText.font = UIProjectFonts.Default;
            _ownerScheduleText.font = UIProjectFonts.Body;
            _ownerLeagueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ownerScheduleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ownerLeagueText.alignment = TextAnchor.MiddleLeft;
            _ownerScheduleText.alignment = TextAnchor.MiddleLeft;
            SetAnchors(_ownerLeagueText.rectTransform, new Vector2(.22f, .5f), new Vector2(.36f, 1f),
                Vector2.zero, new Vector2(-12f, -4f));
            SetAnchors(_ownerScheduleText.rectTransform, new Vector2(.22f, 0f), new Vector2(.36f, .5f),
                new Vector2(0f, 4f), new Vector2(-12f, 0f));
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
            primary.font = UIProjectFonts.Default;
            primary.fontStyle = FontStyle.Normal;
            secondary.font = UIProjectFonts.Body;
            secondary.fontStyle = FontStyle.Normal;
        }

        private static void AddOwnerNavigationIcon(RectTransform parent, string routeId, Text label)
        {
            label.fontSize = 22;
            label.font = UIProjectFonts.Default;
            label.fontStyle = FontStyle.Normal;
            if (string.Equals(routeId, OwnerNavigationRoutes.Shop, System.StringComparison.Ordinal))
            {
                Texture2D shopIcon = Resources.Load<Texture2D>("UI/Shop/shop_navigation_icon_v2");
                if (shopIcon == null) return;
                RectTransform shopRect = CreateRect("Icon", parent);
                // 원본의 4:3 Canvas 비율만 보존한다. FitInParent는 메뉴 셀 전체로 확대되어 하단 라벨을 침범한다.
                SetAnchors(shopRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(12f, -40f), new Vector2(44f, -12f));
                RawImage shopImage = shopRect.gameObject.AddComponent<RawImage>();
                shopImage.texture = shopIcon;
                shopImage.raycastTarget = false;
                SetAnchors(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(46f, 2f), new Vector2(-8f, -2f));
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
            SetAnchors(rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -40f), new Vector2(40f, -12f));
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            // 셀을 UV로 참조해 별도 Texture 복사와 런타임 Sprite 할당을 피한다.
            image.uvRect = new Rect((index % 3) / 3f, index < 3 ? .5f : 0f, 1f / 3f, .5f);
            image.raycastTarget = false;
            // Medium 폰트의 행 높이가 기존 18px 영역을 넘으면 Truncate가 라벨 전체를 숨긴다.
            // 24px 텍스트 영역과 28px 아이콘을 분리해 버튼 안에서 두 요소를 보존한다.
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(46f, 2f), new Vector2(-8f, -2f));
        }
    }
}

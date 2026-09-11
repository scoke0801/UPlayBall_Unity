#if UNITY_EDITOR
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_NewGame
    {
        private int _selectedCardDesign;
        private bool _showCardArtworkOnly;
        private bool _showFullCardDesignGrid;
        private bool _showCardGalleryFx = true;
        private static readonly string[] CardDesignVariants =
            { "Normal", "AllStar", "GoldenGlove", "MVP", "Rare", "Ex", "Legend", "CareerHigh" };
        private static readonly string[] CardDesignLabels =
            { "노말", "올스타", "골든글러브", "MVP", "레어", "EX", "레전드", "커리어하이" };

        /// <summary>현재 게임의 공통 카드 배치 위에서 전체 프레임과 Mini 프레임을 비교한다.</summary>
        private void RenderCardDesignGallery()
        {
            RectTransform shade = CreateImage("CardDesignGalleryShade", _content, new Color(0, 0, 0, .88f),
                new Vector2(1920, 1080), Vector2.zero);
            RectTransform gallery = CreateImage("CardDesignGalleryPopup", shade, new Color32(20, 24, 31, 255),
                new Vector2(1700, 980), Vector2.zero);
            CreateText("Title", gallery, "선수 카드 디자인", 30, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(900, 55), new Vector2(-330, 427), PrimaryTextColor);
            CreateText("Guide", gallery, "현재 프레임 8종 · 카드를 선택하면 크게 볼 수 있습니다", 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(1400, 38), new Vector2(-80, 375), SecondaryTextColor);

            CreateText("SelectedEdition", gallery, CardDesignLabels[_selectedCardDesign], 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(480, 44), new Vector2(-550, 308), PrimaryTextColor);
            RectTransform full = CreateRect("FullCard", gallery, new Vector2(300, 420), new Vector2(-620, 20));
            BuildGalleryFullCard(full, _selectedCardDesign);
            CreateText("FullCaption", gallery, "일반 카드", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(300, 32), new Vector2(-620, -220), SecondaryTextColor);
            CreateGalleryMiniCard(gallery, _selectedCardDesign, new Vector2(-350, 20), true);
            CreateText("MiniCaption", gallery, "미니 카드 · 실제 크기", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(190, 32), new Vector2(-350, -80), SecondaryTextColor);
            RectTransform starPreview = CreateImage("CostStarPreview", gallery, Color.white,
                new Vector2(60, 60), new Vector2(-350, -153));
            starPreview.GetComponent<Image>().sprite = OwnerPlayerCardFrames.GetCostStar(CardDesignVariants[_selectedCardDesign]);
            starPreview.GetComponent<Image>().preserveAspect = true;
            starPreview.GetComponent<Image>().raycastTarget = false;
            CreateText("CostStarCaption", gallery, "Cost 별 · 확대", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(190, 32), new Vector2(-350, -220), SecondaryTextColor);

            for (int index = 0; index < CardDesignVariants.Length; index++)
                CreateCurrentMiniPreview(gallery, index);
            CreateText("SampleNote", gallery,
                GetGallerySampleNote(),
                16, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(1450, 42), new Vector2(0, -360), SecondaryTextColor);
            Button sizeToggle = CreateButton("ToggleCardSize", gallery,
                _showFullCardDesignGrid ? "미니 카드 모아보기" : "일반 카드 모아보기", new Vector2(260, 52),
                new Vector2(-450, -435), CardColor, out _);
            sizeToggle.onClick.AddListener(() => { _showFullCardDesignGrid = !_showFullCardDesignGrid; Render(); });
            Button toggle = CreateButton("ToggleArtwork", gallery,
                _showCardArtworkOnly ? "카드 정보와 함께 보기" : "프레임 원화만 보기", new Vector2(260, 52),
                new Vector2(-165, -435), CardColor, out _);
            toggle.onClick.AddListener(() => { _showCardArtworkOnly = !_showCardArtworkOnly; Render(); });
            Button fxToggle = CreateButton("ToggleFx", gallery, _showCardGalleryFx ? "FX ON" : "FX OFF",
                new Vector2(200, 52), new Vector2(90, -435), _showCardGalleryFx ? AccentColor : CardColor, out _);
            fxToggle.onClick.AddListener(() => { _showCardGalleryFx = !_showCardGalleryFx; Render(); });
            Button close = CreateButton("Close", gallery, "닫기", new Vector2(190, 52),
                new Vector2(310, -435), CardColor, out _);
            close.onClick.AddListener(() => { _showCardGallery = false; Render(); });
        }

        private void BuildGalleryFullCard(RectTransform full, int index)
        {
            string variant = CardDesignVariants[index];
            if (_showCardArtworkOnly)
                CreateGalleryArtwork(full, variant, false);
            else
            {
                var snapshot = new OwnerCollectionCardSnapshot("gallery", "gallery-person", "김하늘", 2025,
                    PlayerPosition.Shortstop, PreviewCost(index), PreviewEdition(index),
                    0, 0, false, false, teamDisplayName: "서울 스타즈", isOwnedCard: false);
                UI_Popup_OwnerPlayerCard.BuildFrontCard(full, snapshot);
                full.Find("MainFrame").GetComponent<Image>().sprite = LoadGalleryFrame(variant, false);
                full.Find("EditionPlate/Edition").GetComponent<Text>().text = CardDesignLabels[index];
                OwnerPlayerCardFrames.SetCostStars((RectTransform)full.Find("CostStars"), variant, PreviewCost(index));
            }
            AttachGalleryFx(full, index, false);
        }

        private void CreateCurrentMiniPreview(RectTransform gallery, int index)
        {
            float x = -80 + index % 4 * 225;
            float y = index < 4 ? 153 : -147;
            RectTransform slot = CreateRect("Design_" + CardDesignVariants[index], gallery,
                new Vector2(200, 272), new Vector2(x, y));
            CreateText("Label", slot, CardDesignLabels[index], 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(200, 35), new Vector2(0, 120), index == _selectedCardDesign ? AccentColor : PrimaryTextColor);
            if (_showFullCardDesignGrid)
            {
                // 작은 Rect에서 글자 최소 크기만 유지하면 이름·능력치가 통째로 잘린다.
                // 실제 카드 기준 크기로 구성한 뒤 카드 전체를 같은 비율로 축소한다.
                RectTransform full = CreateRect("FullPreview", slot, new Vector2(456, 640), new Vector2(0, -8));
                full.localScale = Vector3.one * (151f / 456f);
                BuildGalleryFullCard(full, index);
            }
            else CreateGalleryMiniCard(slot, index, new Vector2(0, -8), false);
            Button select = CreateButton("Select", slot, index == _selectedCardDesign ? "선택됨" : "선택",
                new Vector2(170, 28), new Vector2(0, -128),
                index == _selectedCardDesign ? AccentColor : CardColor, out _);
            select.onClick.AddListener(() => { _selectedCardDesign = index; Render(); });
        }

        private void CreateGalleryMiniCard(RectTransform parent, int index, Vector2 position, bool actualSize)
        {
            var mini = PlayerMiniCardView.CreateRuntime(parent);
            RectTransform rect = (RectTransform)mini.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(151, 212);
            rect.anchoredPosition = position;
            mini.UseLineupSlotLayout();
            rect.sizeDelta = actualSize
                ? new Vector2(PlayerMiniCardView.LineupSlotWidth, PlayerMiniCardView.LineupSlotHeight)
                : new Vector2(151, 212);
            mini.Bind(new PlayerMiniCardModel("gallery-" + index, "김하늘", "유격수", "25", "", "",
                frameEdition: PreviewEdition(index), cost: PreviewCost(index)), PlayerPortraitSprites.GetDefault(PlayerPosition.Shortstop));
            mini.transform.Find("LineupSubFrame").GetComponent<Image>().sprite = LoadGalleryFrame(CardDesignVariants[index], true);
            OwnerPlayerCardFrames.SetCostStars((RectTransform)mini.transform.Find("CostStars"),
                CardDesignVariants[index], PreviewCost(index));
            if (_showCardArtworkOnly)
            {
                mini.gameObject.SetActive(false);
                RectTransform artwork = CreateRect("MiniArtwork", parent, rect.sizeDelta, position);
                CreateGalleryArtwork(artwork, CardDesignVariants[index], true);
                AttachGalleryFx(artwork, index, true);
            }
            else AttachGalleryFx(rect, index, true);
            mini.Selected += _ => { _selectedCardDesign = index; Render(); };
        }

        private string GetGallerySampleNote()
        {
            string cardNote = _showCardArtworkOnly
                ? "선택한 등급의 일반·미니 프레임을 함께 표시합니다."
                : "공통 카드 렌더러의 이름·연도·Cost·포지션 배치입니다.";
            return _showCardGalleryFx
                ? cardNote + " FX는 이 개발용 팝업 미리보기에만 적용됩니다."
                : cardNote + " FX 미리보기가 꺼져 있습니다.";
        }

        private void AttachGalleryFx(RectTransform target, int index, bool isCompact)
        {
            if (!_showCardGalleryFx || target == null)
                return;

            CardDesignGalleryFx fx = target.gameObject.AddComponent<CardDesignGalleryFx>();
            fx.Initialize(CardDesignVariants[index], isCompact, index == _selectedCardDesign);
        }

        private static PlayerCardEdition PreviewEdition(int index) =>
            (PlayerCardEdition)System.Enum.Parse(typeof(PlayerCardEdition), CardDesignVariants[index], true);
        private static int PreviewCost(int index) => index == 4 ? 5 : 10;
        private static Sprite LoadGalleryFrame(string variant, bool mini) => OwnerPlayerCardFrames.Get(variant, mini);

        private static void CreateGalleryArtwork(RectTransform parent, string variant, bool mini)
        {
            Image image = parent.gameObject.AddComponent<Image>();
            image.sprite = LoadGalleryFrame(variant, mini);
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (!mini) UI_Popup_OwnerPlayerCard.BuildTeamPlate(parent);
        }
    }
}
#endif

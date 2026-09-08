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

            string variant = CardDesignVariants[_selectedCardDesign];
            CreateText("SelectedEdition", gallery, CardDesignLabels[_selectedCardDesign], 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(400, 44), new Vector2(-530, 308), PrimaryTextColor);
            RectTransform full = CreateRect("FullCard", gallery, new Vector2(360, 504), new Vector2(-530, 20));
            if (_showCardArtworkOnly)
                CreateGalleryArtwork(full, variant, false);
            else
            {
                var snapshot = new OwnerCollectionCardSnapshot("gallery", "gallery-person", "김하늘", 2025,
                    PlayerPosition.Shortstop, PreviewCost(_selectedCardDesign), PreviewEdition(_selectedCardDesign),
                    0, 0, false, false, isOwnedCard: false, teamDisplayName: CardDesignLabels[_selectedCardDesign]);
                UI_Popup_OwnerPlayerCard.BuildFrontCard(full, snapshot);
                full.Find("MainFrame").GetComponent<Image>().sprite = LoadGalleryFrame(variant, false);
            }
            CreateText("FullCaption", gallery, "일반 Card · 360 × 504", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(430, 32), new Vector2(-530, -267), SecondaryTextColor);

            for (int index = 0; index < CardDesignVariants.Length; index++)
                CreateCurrentMiniPreview(gallery, index);
            CreateText("SampleNote", gallery,
                _showCardArtworkOnly ? "이름·Cost·포지션을 제외한 원화입니다." :
                    "배치 확인용 이름·연도·Cost를 공통 카드 렌더러로 표시합니다. 선수 데이터와 무관합니다.",
                16, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(1450, 42), new Vector2(0, -360), SecondaryTextColor);
            Button toggle = CreateButton("ToggleArtwork", gallery,
                _showCardArtworkOnly ? "카드 정보와 함께 보기" : "프레임 원화만 보기", new Vector2(290, 52),
                new Vector2(-180, -435), CardColor, out _);
            toggle.onClick.AddListener(() => { _showCardArtworkOnly = !_showCardArtworkOnly; Render(); });
            Button close = CreateButton("Close", gallery, "닫기", new Vector2(220, 52),
                new Vector2(150, -435), CardColor, out _);
            close.onClick.AddListener(() => { _showCardGallery = false; Render(); });
        }

        private void CreateCurrentMiniPreview(RectTransform gallery, int index)
        {
            float x = -80 + index % 4 * 225;
            float y = index < 4 ? 153 : -147;
            RectTransform slot = CreateRect("Design_" + CardDesignVariants[index], gallery,
                new Vector2(200, 272), new Vector2(x, y));
            CreateText("Label", slot, CardDesignLabels[index], 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(200, 35), new Vector2(0, 120), index == _selectedCardDesign ? AccentColor : PrimaryTextColor);
            var mini = PlayerMiniCardView.CreateRuntime(slot);
            RectTransform rect = (RectTransform)mini.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(151, 212);
            rect.anchoredPosition = new Vector2(0, -8);
            mini.UseLineupSlotLayout();
            rect.sizeDelta = new Vector2(151, 212);
            mini.Bind(new PlayerMiniCardModel("gallery-" + index, "김하늘", "유격수", "25", "", "",
                frameEdition: PreviewEdition(index), cost: PreviewCost(index)), PlayerPortraitSprites.GetDefault(PlayerPosition.Shortstop));
            mini.transform.Find("LineupSubFrame").GetComponent<Image>().sprite = LoadGalleryFrame(CardDesignVariants[index], true);
            if (_showCardArtworkOnly)
            {
                mini.gameObject.SetActive(false);
                RectTransform artwork = CreateRect("MiniArtwork", slot, new Vector2(151, 212), new Vector2(0, -8));
                CreateGalleryArtwork(artwork, CardDesignVariants[index], true);
            }
            Button select = CreateButton("Select", slot, "선택", new Vector2(170, 28),
                new Vector2(0, -128), CardColor, out _);
            select.onClick.AddListener(() => { _selectedCardDesign = index; Render(); });
            mini.Selected += _ => { _selectedCardDesign = index; Render(); };
        }

        private static PlayerCardEdition PreviewEdition(int index) => index < 4 ? (PlayerCardEdition)index : PlayerCardEdition.Normal;
        private static int PreviewCost(int index) => index == 4 ? 5 : 10;
        private static Sprite LoadGalleryFrame(string variant, bool mini) => Resources.Load<Sprite>(
            "UI/PlayerCards/PlayerCard_" + (mini ? "Mini_" : "Full_") + variant + "_v2");

        private static void CreateGalleryArtwork(RectTransform parent, string variant, bool mini)
        {
            Image image = parent.gameObject.AddComponent<Image>();
            image.sprite = LoadGalleryFrame(variant, mini);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
#endif

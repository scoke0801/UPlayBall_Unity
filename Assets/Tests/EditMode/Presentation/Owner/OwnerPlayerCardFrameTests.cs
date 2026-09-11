using System;
using System.IO;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>등급 프레임과 COST 별의 데이터 연결 및 실제 카드 렌더링을 검증한다.</summary>
    public sealed class OwnerPlayerCardFrameTests
    {
        [TestCase(PlayerCardEdition.Normal)]
        [TestCase(PlayerCardEdition.AllStar)]
        [TestCase(PlayerCardEdition.GoldenGlove)]
        [TestCase(PlayerCardEdition.Mvp)]
        [TestCase(PlayerCardEdition.Rare)]
        [TestCase(PlayerCardEdition.Ex)]
        [TestCase(PlayerCardEdition.CareerHigh)]
        [TestCase(PlayerCardEdition.Legend)]
        public void MiniCard_UsesTypedEditionAndRebindsCost(PlayerCardEdition edition)
        {
            var root = new GameObject("Fixture", typeof(RectTransform));
            try
            {
                PlayerMiniCardView view = PlayerMiniCardView.CreateRuntime(root.transform);
                view.UseLineupSlotLayout();
                view.Bind(new PlayerMiniCardModel("p", "김하늘", "유격수", "26", "파싱 금지", "잘못된 라벨",
                    frameEdition: edition, cost: 3));
                Assert.That(view.transform.Find("LineupSubFrame").GetComponent<Image>().sprite.name,
                    Is.EqualTo("PlayerCard_Mini_" + Variant(edition) + "_v" + ExpectedFrameVersion(edition)));
                Assert.That(view.transform.Find("Edition").gameObject.activeSelf, Is.False);
                AssertCost(view.transform, edition, 3);
                view.Bind(new PlayerMiniCardModel("p2", "이바다", "포수", "25", "", "",
                    frameEdition: edition, cost: 8));
                AssertCost(view.transform, edition, 8);
                Assert.That(view.transform.Find("Name").GetComponent<Text>().text, Is.EqualTo("이바다"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(PlayerCardEdition.Normal)]
        [TestCase(PlayerCardEdition.AllStar)]
        [TestCase(PlayerCardEdition.GoldenGlove)]
        [TestCase(PlayerCardEdition.Mvp)]
        [TestCase(PlayerCardEdition.Rare)]
        [TestCase(PlayerCardEdition.Ex)]
        [TestCase(PlayerCardEdition.CareerHigh)]
        [TestCase(PlayerCardEdition.Legend)]
        public void FullCard_UsesEditionFrameAndActualCost(PlayerCardEdition edition)
        {
            var root = new GameObject("Fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                UI_Popup_OwnerPlayerCard.Show(root.transform, Fixture(edition));
                Transform front = root.transform.Find("UI_Popup_OwnerPlayerCard/CardDetail/Front");
                Assert.That(front.Find("MainFrame").GetComponent<Image>().sprite.name,
                    Is.EqualTo("PlayerCard_Full_" + Variant(edition) + "_v" + ExpectedFrameVersion(edition, false)));
                AssertCost(front, edition, 7);
                RectTransform costLabel = (RectTransform)front.Find("CostLabel");
                Assert.That(costLabel.anchorMin.y, Is.EqualTo(.014f).Within(.0001f));
                Assert.That(costLabel.anchorMax.y, Is.EqualTo(.060f).Within(.0001f));
                Assert.That(front.Find("Ability5"), Is.Not.Null);
                var portrait = (RectTransform)front.Find("PortraitWindow");
                Assert.That(portrait.anchorMin, Is.EqualTo(new Vector2(.025f, .535f)));
                Assert.That(portrait.anchorMax, Is.EqualTo(new Vector2(.975f, .94f)));
                Assert.That(front.Find("TeamPlate/Team").GetComponent<Text>().text, Is.EqualTo("서울 스타즈"));
                AssertLayerOrder(front, "MainFrame", "PortraitWindow");
                Assert.That(front.parent.Find("Back/SkillBoardInformation/Grid/Cell_3_3"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(PitcherRole.Setup, "셋업")]
        [TestCase(PitcherRole.Closer, "마무리")]
        public void FullCard_투수NaturalRole을앞면에표시한다(PitcherRole role, string label)
        {
            var root = new GameObject("Fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                var card = new OwnerCollectionCardSnapshot(
                    "c", "p", "가상투수", 2025, PlayerPosition.ReliefPitcher, 7,
                    PlayerCardEdition.Normal, 0, 0, false, false, pitcherRole: role);

                UI_Popup_OwnerPlayerCard.Show(root.transform, card);

                Text position = root.transform.Find(
                    "UI_Popup_OwnerPlayerCard/CardDetail/Front/PositionPlate/Position").GetComponent<Text>();
                Assert.That(position.text, Is.EqualTo(label));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void GalleryVariants_UseLatestAvailableFrame()
        {
            string[] v4Variants = { "AllStar", "GoldenGlove", "MVP", "Rare", "Ex", "Legend", "CareerHigh" };
            foreach (string variant in v4Variants)
            {
                Assert.That(GetFrame(variant, false).name,
                    Is.EqualTo("PlayerCard_Full_" + variant + "_v" + ExpectedFrameVersion((PlayerCardEdition)Enum.Parse(typeof(PlayerCardEdition), variant, true), false)));
                Assert.That(GetFrame(variant, true).name,
                    Is.EqualTo("PlayerCard_Mini_" + variant + "_v" + ExpectedFrameVersion((PlayerCardEdition)Enum.Parse(typeof(PlayerCardEdition), variant, true))));
            }

            Assert.That(GetFrame("Normal", true).name, Is.EqualTo("PlayerCard_Mini_Normal_v2"));
        }

        [Test]
        public void TypedEditionFrames_UseEveryEditionResource()
        {
            foreach (PlayerCardEdition edition in Enum.GetValues(typeof(PlayerCardEdition)))
            {
                Assert.That(GetTypedFrame(edition, false).name,
                    Is.EqualTo("PlayerCard_Full_" + Variant(edition) + "_v" + ExpectedFrameVersion(edition, false)));
                Assert.That(GetTypedFrame(edition, true).name,
                    Is.EqualTo("PlayerCard_Mini_" + Variant(edition) + "_v" + ExpectedFrameVersion(edition)));
            }
        }

        [Test]
        public void CostStarVariants_UseV4SpriteSheetSlices()
        {
            string[] variants = { "Normal", "Rare", "AllStar", "GoldenGlove", "MVP", "Ex", "Legend", "CareerHigh" };
            foreach (string variant in variants)
                Assert.That(GetCostStar(variant).name, Is.EqualTo("PlayerCard_CostStar_" + variant + "_v4"));
        }

        [Test]
        public void MiniCard_AllEditionsShareFrameAndFooterRectangles()
        {
            var root = new GameObject("Alignment", typeof(RectTransform));
            try
            {
                string[] names = { "LineupSubFrame", "Portrait", "Cost", "CostStars", "Status" };
                var minimums = new Vector2[names.Length];
                var maximums = new Vector2[names.Length];
                foreach (PlayerCardEdition edition in Enum.GetValues(typeof(PlayerCardEdition)))
                {
                    var view = PlayerMiniCardView.CreateRuntime(root.transform);
                    view.UseLineupSlotLayout();
                    view.Bind(new PlayerMiniCardModel("p", "김하늘", "유격수", "26", "", "", frameEdition: edition, cost: 7));
                    AssertLayerOrder(view.transform, "LineupSubFrame", "Portrait");
                    Assert.That(((RectTransform)view.transform).sizeDelta, Is.EqualTo(new Vector2(80, 120)));
                    for (int index = 0; index < names.Length; index++)
                    {
                        var rect = (RectTransform)view.transform.Find(names[index]);
                        if (edition == PlayerCardEdition.Normal)
                        {
                            minimums[index] = rect.anchorMin;
                            maximums[index] = rect.anchorMax;
                        }
                        Assert.That(rect.anchorMin, Is.EqualTo(minimums[index]), edition + " " + names[index]);
                        Assert.That(rect.anchorMax, Is.EqualTo(maximums[index]), edition + " " + names[index]);
                        Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
                        Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MiniCard_RebindMovesNameAwayFromCrestAndRestoresContrast()
        {
            var root = new GameObject("Alignment", typeof(RectTransform));
            try
            {
                var view = PlayerMiniCardView.CreateRuntime(root.transform);
                view.UseLineupSlotLayout();
                foreach (PlayerCardEdition edition in new[] { PlayerCardEdition.Mvp, PlayerCardEdition.Legend,
                    PlayerCardEdition.Ex, PlayerCardEdition.CareerHigh, PlayerCardEdition.Normal })
                {
                    view.Bind(new PlayerMiniCardModel("p", "김하늘", "유격수", "26", "", "",
                        frameEdition: edition, cost: 7));
                    Text name = view.transform.Find("Name").GetComponent<Text>();
                    Text year = view.transform.Find("Year").GetComponent<Text>();
                    var portrait = (RectTransform)view.transform.Find("Portrait");
                    Assert.That(name.rectTransform.anchorMax.y, Is.LessThan(portrait.anchorMin.y));
                    Assert.That(year.rectTransform.anchorMin.y, Is.EqualTo(name.rectTransform.anchorMin.y));
                    Assert.That(year.rectTransform.anchorMax.y, Is.EqualTo(name.rectTransform.anchorMax.y));
                    Assert.That(name.color, Is.EqualTo(year.color));
                    Assert.That(name.color == Color.white, Is.EqualTo(edition == PlayerCardEdition.CareerHigh));
                    Assert.That((name.rectTransform.anchorMin.x + name.rectTransform.anchorMax.x) * .5f,
                        Is.EqualTo(.5f).Within(.0001f), edition + " 이름 중앙 정렬");
                    if (edition == PlayerCardEdition.Mvp)
                    {
                        Assert.That((name.rectTransform.anchorMin.x + name.rectTransform.anchorMax.x) * .5f,
                            Is.EqualTo(.5f).Within(.0001f), "이름은 카드 중앙");
                        Assert.That(portrait.anchorMin.y / .89f, Is.EqualTo(.35f).Within(.0001f), "공통 초상 크기");
                    }
                    if (edition == PlayerCardEdition.Ex)
                        Assert.That(name.rectTransform.anchorMin.y / .89f, Is.EqualTo(.24f).Within(.0001f), "공통 명찰 높이");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Render_AllEditionsAtActualCardSizes()
        {
            string output = Environment.GetEnvironmentVariable("BASEBALL_CARD_FRAME_CAPTURE");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("렌더 출력 경로를 지정한 시각 검증에서만 실행한다.");
            Directory.CreateDirectory(output);
            foreach (PlayerCardEdition edition in Enum.GetValues(typeof(PlayerCardEdition)))
            {
                Capture(output, edition, 80, 120, true);
                Capture(output, edition, 151, 212, true);
                Capture(output, edition, 456, 640, false);
            }
            Capture(output, PlayerCardEdition.Normal, 640, 240, true);
        }

        private static void Capture(string output, PlayerCardEdition edition, int width, int height, bool mini)
        {
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var target = new RenderTexture(width, height, 24);
            Texture2D capture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.08f, .09f, .1f);
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                if (mini)
                {
                    int count = width == 640 ? 4 : 1;
                    for (int index = 0; index < count; index++)
                    {
                        var view = PlayerMiniCardView.CreateRuntime(root.transform);
                        if (width == 80 || count == 4) view.UseLineupSlotLayout();
                        view.Bind(new PlayerMiniCardModel("p", "김하늘", "유격수", "26", "", "", "주전",
                            frameEdition: count == 4 ? (PlayerCardEdition)index : edition, cost: 7),
                            Resources.Load<Sprite>("UI/Portraits/img_hitter_default"));
                        RectTransform rect = (RectTransform)view.transform;
                        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                        rect.offsetMin = count == 4 ? new Vector2(7 + index * 158, 14) : Vector2.zero;
                        rect.offsetMax = count == 4 ? new Vector2(7 + index * 158 + 151 - width, -14) : Vector2.zero;
                    }
                }
                else
                {
                    typeof(UI_Popup_OwnerPlayerCard).GetMethod("BuildFrontCard",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(null, new object[] { (RectTransform)root.transform, Fixture(edition) });
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                string fileName = width == 640 ? "Mini_AllEditions_640.png"
                    : (mini ? "Mini_" : "Full_") + Variant(edition) + "_" + width + ".png";
                File.WriteAllBytes(Path.Combine(output, fileName), capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) Object.DestroyImmediate(capture);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static OwnerCollectionCardSnapshot Fixture(PlayerCardEdition edition) =>
            new OwnerCollectionCardSnapshot("c", "p", "김하늘", 2026, PlayerPosition.Shortstop, 7,
                edition, 0, 0, false, false, CreateAbilities(),
                teamDisplayName: "서울 스타즈", condition: 73, conditionLabel: "좋음");

        private static AbilityRatings CreateAbilities()
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++) values[index] = 75 + index % 12;
            return new AbilityRatings(values);
        }

        private static string Variant(PlayerCardEdition edition) => edition == PlayerCardEdition.Mvp ? "MVP" : edition.ToString();

        private static void AssertLayerOrder(Transform card, string backgroundName, string portraitName)
        {
            int background = card.Find(backgroundName).GetSiblingIndex();
            int portrait = card.Find(portraitName).GetSiblingIndex();
            int decoration = card.Find("CardDecoration").GetSiblingIndex();
            int name = card.Find("Name").GetSiblingIndex();
            Assert.That(background, Is.LessThan(portrait));
            Assert.That(portrait, Is.LessThan(decoration));
            Assert.That(decoration, Is.LessThan(name));
            Assert.That(card.Find("CardDecoration").GetComponent<Graphic>().raycastTarget, Is.False);
        }

        private static int ExpectedFrameVersion(PlayerCardEdition edition, bool isMini = true)
        {
            if (edition == PlayerCardEdition.Mvp || edition == PlayerCardEdition.Ex ||
                (!isMini && edition == PlayerCardEdition.AllStar)) return 5;
            return edition == PlayerCardEdition.Normal ? 2 : 4;
        }

        private static Sprite GetFrame(string variant, bool isMini)
        {
            Type type = typeof(PlayerMiniCardView).Assembly.GetType(
                "Baseball.Presentation.SharedUI.OwnerPlayerCardFrames", true);
            var method = type.GetMethod("Get", new[] { typeof(string), typeof(bool) });
            return (Sprite)method.Invoke(null, new object[] { variant, isMini });
        }

        private static Sprite GetCostStar(string variant)
        {
            Type type = typeof(PlayerMiniCardView).Assembly.GetType(
                "Baseball.Presentation.SharedUI.OwnerPlayerCardFrames", true);
            var method = type.GetMethod("GetCostStar", new[] { typeof(string) });
            return (Sprite)method.Invoke(null, new object[] { variant });
        }

        private static Sprite GetTypedFrame(PlayerCardEdition edition, bool isMini)
        {
            Type type = typeof(PlayerMiniCardView).Assembly.GetType(
                "Baseball.Presentation.SharedUI.OwnerPlayerCardFrames", true);
            var method = type.GetMethod("Get", new[] { typeof(PlayerCardEdition), typeof(bool) });
            return (Sprite)method.Invoke(null, new object[] { edition, isMini });
        }

        private static void AssertCost(Transform card, PlayerCardEdition edition, int cost)
        {
            Transform row = card.Find("CostStars");
            Assert.That(row.childCount, Is.EqualTo(10));
            int bright = 0;
            foreach (Transform child in row)
            {
                Image image = child.GetComponent<Image>();
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.name, Is.EqualTo("PlayerCard_CostStar_" + Variant(edition) + "_v4"));
                Assert.That(image.raycastTarget, Is.False);
                if (image.color == Color.white) bright++;
            }
            Assert.That(bright, Is.EqualTo(cost));
            Assert.That(card.Find("Cost").GetComponent<Text>().text, Is.EqualTo(cost.ToString()));
        }
    }
}

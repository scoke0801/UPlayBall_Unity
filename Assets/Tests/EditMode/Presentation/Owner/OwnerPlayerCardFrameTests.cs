using System;
using System.IO;
using Baseball.Core.Historical;
using Baseball.Core.Players;
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
                    Is.EqualTo("PlayerCard_Mini_" + Variant(edition) + "_v2"));
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
        public void FullCard_UsesEditionFrameAndActualCost(PlayerCardEdition edition)
        {
            var root = new GameObject("Fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                UI_Popup_OwnerPlayerCard.Show(root.transform, Fixture(edition));
                Transform front = root.transform.Find("UI_Popup_OwnerPlayerCard/CardDetail/Front");
                Assert.That(front.Find("MainFrame").GetComponent<Image>().sprite.name,
                    Is.EqualTo("PlayerCard_Full_" + Variant(edition) + "_v2"));
                AssertCost(front, edition, 7);
                Assert.That(front.Find("Ability5"), Is.Not.Null);
                Assert.That(front.parent.Find("Back/SkillBoardInformation/Grid/Cell_3_3"), Is.Not.Null);
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
                    var view = PlayerMiniCardView.CreateRuntime(root.transform);
                    if (width == 80) view.UseLineupSlotLayout();
                    view.Bind(new PlayerMiniCardModel("p", "김하늘", "유격수", "26", "", "", "주전",
                        frameEdition: edition, cost: 7), Resources.Load<Sprite>("UI/Portraits/img_hitter_default"));
                    RectTransform rect = (RectTransform)view.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                else
                {
                    var view = root.AddComponent<UI_Popup_OwnerPlayerCard>();
                    view.enabled = false;
                    typeof(UI_Popup_OwnerPlayerCard).GetMethod("BuildFront",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(view, new object[] { (RectTransform)root.transform, Fixture(edition), false });
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                File.WriteAllBytes(Path.Combine(output, (mini ? "Mini_" : "Full_") + Variant(edition) + "_" + width + ".png"), capture.EncodeToPNG());
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

        private static void AssertCost(Transform card, PlayerCardEdition edition, int cost)
        {
            Transform row = card.Find("CostStars");
            Assert.That(row.childCount, Is.EqualTo(10));
            int bright = 0;
            foreach (Transform child in row)
            {
                Image image = child.GetComponent<Image>();
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.sprite.name, Is.EqualTo("PlayerCard_CostStar_" + Variant(edition) + "_v2"));
                Assert.That(image.raycastTarget, Is.False);
                if (image.color == Color.white) bright++;
            }
            Assert.That(bright, Is.EqualTo(cost));
            Assert.That(card.Find("Cost").GetComponent<Text>().text, Is.EqualTo(cost.ToString()));
        }
    }
}

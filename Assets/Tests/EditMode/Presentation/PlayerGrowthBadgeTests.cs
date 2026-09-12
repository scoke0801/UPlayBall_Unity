using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>저장된 유학 상태, 카드 재사용, 생성 자산과 실제 화면 배치를 검증한다.</summary>
    public sealed class PlayerGrowthBadgeTests
    {
        [Test]
        public void Study_진행과완료를구분하고다른카드에는붙이지않는다()
        {
            var card = new OwnedPlayerCardState("card");
            var growth = new OwnerPlayerGrowthState();
            Assert.That(OwnerCardGrowthBadgeBuilder.Build(card, growth).HasStudy, Is.False);
            card.RecordStudySeason(0);
            growth.AddStudy(new CardStudyProjectState("card", "study_contact", 0, 4));
            var ongoing = OwnerCardGrowthBadgeBuilder.Build(card, growth);
            Assert.That(ongoing.StudyState, Is.EqualTo(PlayerStudyBadgeState.InProgress));
            StringAssert.Contains("4주", ongoing.StudyDescription);
            Assert.That(OwnerCardGrowthBadgeBuilder.Build(new OwnedPlayerCardState("other"), growth).HasStudy, Is.False);
            growth.RemoveStudyAt(0);
            Assert.That(OwnerCardGrowthBadgeBuilder.Build(card, growth).StudyState, Is.EqualTo(PlayerStudyBadgeState.Completed));
            Assert.That(OwnerCardGrowthBadgeBuilder.Build(card, growth).HasTrait, Is.False);
        }

        [Test]
        public void Study_일반훈련과블록은유학이나특성으로추정하지않는다()
        {
            var card = new OwnedPlayerCardState("card");
            card.Training.AddBonus(PlayerAbility.Contact, 3);
            var badges = OwnerCardGrowthBadgeBuilder.Build(card, new OwnerPlayerGrowthState());
            Assert.That(badges.HasStudy, Is.False);
            Assert.That(badges.HasTrait, Is.False);
        }

        [TestCase(PlayerTraitBadgeRank.C)]
        [TestCase(PlayerTraitBadgeRank.B)]
        [TestCase(PlayerTraitBadgeRank.A)]
        [TestCase(PlayerTraitBadgeRank.S)]
        public void Assets_등급별독립Sprite와무압축설정을검증한다(PlayerTraitBadgeRank rank)
        {
            Sprite sprite = PlayerCardGrowthBadgesView.GetTraitSprite(rank);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(256, 256)));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(PlayerCardGrowthBadgesView.GetStudySprite(), Is.Not.Null);
        }

        [Test]
        public void Rebind_목록과상세모두이전선수의표식을제거한다()
        {
            var root = new GameObject("BadgeRebind", typeof(RectTransform));
            try
            {
                var view = PlayerMiniCardView.CreateRuntime(root.transform);
                var trained = CreateSnapshot(PlayerTraitBadgeRank.S);
                view.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(trained, false));
                Assert.That(view.transform.Find("TraitRankBadge").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("StudyBadge").gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find("StudyBadge").GetComponent<Image>().raycastTarget, Is.False);
                view.Bind(new PlayerMiniCardModel("other", "미훈련 선수", "포수", "24", "5", ""));
                Assert.That(view.transform.Find("TraitRankBadge").gameObject.activeSelf, Is.False);
                Assert.That(view.transform.Find("StudyBadge").gameObject.activeSelf, Is.False);
                var unowned = new OwnerCollectionCardSnapshot("unowned", "person", "미보유", 2024,
                    PlayerPosition.Catcher, 5, PlayerCardEdition.Normal, 0, 0, false, false,
                    isOwnedCard: false, growthBadges: trained.GrowthBadges);
                Assert.That(unowned.GrowthBadges.HasStudy, Is.False);
                Assert.That(unowned.GrowthBadges.HasTrait, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void Render_모든해상도에서배지와기존카드정보를검수한다(int width, int height)
        {
            var host = new GameObject("GrowthBadgeCanvas", typeof(Canvas));
            var cameraObject = new GameObject("GrowthBadgeCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(20, 25, 33, 255);
                camera.targetTexture = target;
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var panel = new GameObject("Samples", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(host.transform, false);
                panel.sizeDelta = new Vector2(1120, 620);
                panel.localScale = Vector3.one * Mathf.Min(width / 1280f, height / 720f);
                for (int index = 0; index < 5; index++)
                {
                    var rank = (PlayerTraitBadgeRank)(index % 4 + 1);
                    var snapshot = CreateSnapshot(rank, (PlayerCardEdition)index);
                    var card = PlayerMiniCardView.CreateRuntime(panel);
                    card.UseLineupSlotLayout();
                    card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(snapshot, false),
                        Resources.Load<Sprite>("UI/Portraits/img_hitter_default"));
                    var rect = (RectTransform)card.transform;
                    rect.sizeDelta = new Vector2(140, 230);
                    rect.anchoredPosition = new Vector2(-465 + index * 172, 160);
                    Canvas.ForceUpdateCanvases();
                    AssertBadgeBounds(rect, camera);
                    // 실제 선수단의 가장 작은 슬롯도 별도로 검수한다.
                    var tiny = PlayerMiniCardView.CreateRuntime(panel);
                    tiny.UseLineupSlotLayout();
                    tiny.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(snapshot, false));
                    var tinyRect = (RectTransform)tiny.transform;
                    tinyRect.sizeDelta = new Vector2(56, 92);
                    tinyRect.anchoredPosition = new Vector2(-465 + index * 172, -20);
                    Canvas.ForceUpdateCanvases();
                    AssertBadgeBounds(tinyRect, camera);
                }
                var detail = new GameObject("Detail", typeof(RectTransform)).GetComponent<RectTransform>();
                detail.SetParent(panel, false);
                detail.sizeDelta = new Vector2(250, 390);
                detail.anchoredPosition = new Vector2(430, 40);
                typeof(UI_Popup_OwnerPlayerCard).GetMethod("BuildFrontCard", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { detail, CreateSnapshot(PlayerTraitBadgeRank.S, PlayerCardEdition.Legend) });
                Canvas.ForceUpdateCanvases();
                AssertBadgeBounds(detail, camera);
                camera.Render();
                string directory = Environment.GetEnvironmentVariable("BASEBALL_GROWTH_BADGE_CAPTURE");
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    RenderTexture.active = target;
                    var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(directory, $"badges-{width}x{height}.png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                // 카드 포커스에도 동일한 한국어 설명을 표시하며 입력을 가로채지 않는다.
                var badges = detail.GetComponent<PlayerCardGrowthBadgesView>();
                badges.OnSelect(null);
                Transform tooltip = host.transform.Find("PlayerGrowthTooltip");
                Assert.That(tooltip, Is.Not.Null);
                StringAssert.Contains("유학 완료", tooltip.GetComponentInChildren<Text>().text);
                Assert.That(tooltip.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                badges.OnDeselect(null);
                Assert.That(host.transform.Find("PlayerGrowthTooltip"), Is.Null);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DetailFocus_뒤집기버튼에서설명을열고숨긴앞면은표시하지않는다()
        {
            var canvas = new GameObject("FocusCanvas", typeof(Canvas));
            try
            {
                var button = new GameObject("Flip", typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(canvas.transform, false);
                var front = new GameObject("Front", typeof(RectTransform)).GetComponent<RectTransform>();
                front.SetParent(button.transform, false);
                var model = CreateSnapshot(PlayerTraitBadgeRank.A).GrowthBadges;
                PlayerCardGrowthBadgesView.Bind(front, model, isDetail: true);
                var relay = button.GetComponent<PlayerCardGrowthBadgeFocusRelay>();
                Assert.That(relay, Is.Not.Null);
                relay.OnSelect(null);
                Assert.That(canvas.transform.Find("PlayerGrowthTooltip"), Is.Not.Null);
                front.gameObject.SetActive(false);
                Assert.That(canvas.transform.Find("PlayerGrowthTooltip"), Is.Null);
                PlayerCardGrowthBadgesView.Bind(front, model, isDetail: true);
                Assert.That(canvas.transform.Find("PlayerGrowthTooltip"), Is.Null);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        private static OwnerCollectionCardSnapshot CreateSnapshot(PlayerTraitBadgeRank rank,
            PlayerCardEdition edition = PlayerCardEdition.Normal) =>
            new OwnerCollectionCardSnapshot("trained", "person", "크리스토퍼", 2025, PlayerPosition.CenterField,
                10, edition, 5, 0, true, false, condition: 85, conditionLevel: 8,
                growthBadges: new PlayerCardGrowthBadgeModel(PlayerStudyBadgeState.Completed,
                    traitRank: rank, traitDescription: "철벽 수비 · 수비 실책 억제"));

        private static void AssertBadgeBounds(RectTransform card, Camera camera)
        {
            Rect trait = ScreenRect((RectTransform)card.Find("TraitRankBadge"), camera);
            Rect study = ScreenRect((RectTransform)card.Find("StudyBadge"), camera);
            Rect outer = ScreenRect(card, camera);
            Assert.That(trait.width, Is.GreaterThanOrEqualTo(23.9f));
            Assert.That(study.width, Is.GreaterThanOrEqualTo(23.9f));
            Assert.That(trait.Overlaps(study), Is.False);
            foreach (Rect badge in new[] { trait, study })
            {
                Assert.That(badge.xMin, Is.GreaterThanOrEqualTo(outer.xMin));
                Assert.That(badge.xMax, Is.LessThanOrEqualTo(outer.xMax));
                Assert.That(badge.yMin, Is.GreaterThanOrEqualTo(outer.yMin));
                Assert.That(badge.yMax, Is.LessThanOrEqualTo(outer.yMax));
                foreach (string name in new[] { "Name", "Year", "EditionPlate", "Enhancement", "Locked", "PositionPlate" })
                {
                    var label = card.Find(name) as RectTransform;
                    if (label != null && label.gameObject.activeSelf)
                        Assert.That(badge.Overlaps(ScreenRect(label, camera)), Is.False, name);
                }
            }
        }

        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

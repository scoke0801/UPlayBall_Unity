using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>새 성장 메뉴의 입력·공유 인벤토리·유학 확인과 실제 렌더링을 검증한다.</summary>
    public sealed class OwnerGrowthPresentationTests
    {
        private GameObject _root;
        private UI_Scene_OwnerGrowth _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("GrowthTests", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1100, 560);
            _view = UI_Scene_OwnerGrowth.CreateRuntime((RectTransform)_root.transform);
            _view.Bind(CreateSnapshot());
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void Skills_선수모드원점역산으로가리킨칸에즉시배치한다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);
            Click("Block_1");
            Click("Rotate");
            int requests = 0;
            _view.SkillPlacementRequested += (card, instance, x, y, rotation) =>
            {
                Assert.That(card, Is.EqualTo("card0"));
                Assert.That(instance, Is.EqualTo(1));
                Assert.That(x, Is.EqualTo(0));
                Assert.That(y, Is.EqualTo(0));
                Assert.That(rotation, Is.EqualTo(1));
                requests++;
            };
            Assert.That(Find<Button>("BoardCell_0_3").interactable, Is.True,
                "세로 I 블록의 끝 칸을 가리켜도 유효한 원점으로 역산해야 합니다.");
            Click("BoardCell_0_3");
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void Skills_다른카드에장착된블록은중복배치할수없다()
        {
            OwnerGrowthSnapshot snapshot = CreateSnapshot();
            snapshot.Cards[1] = new OwnerGrowthCardSnapshot(snapshot.Cards[1].Card,
                new[] { new PlacedSkillBlock(snapshot.Inventory[0], 0, 0, 0) }, snapshot.Cards[1].Studies);
            _view.Bind(snapshot);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);
            Assert.That(Find<Button>("Block_1").interactable, Is.False);
            Assert.That(snapshot.GetEquippedCardId(1), Is.EqualTo("card1"));
        }

        [Test]
        public void Skills_선수유형에맞는블록만노출하고표시개수를맞춘다()
        {
            OwnerGrowthSnapshot source = CreateSnapshot();
            var batterBlock = new SkillBlockDefinition(
                "batter_arm", SkillBlockRarity.Normal, SkillBlockCategory.Arm,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O), true,
                new[] { new AbilityChange(PlayerAbility.Arm, 1) }, 100);
            var pitcherBlock = new SkillBlockDefinition(
                "pitcher_stamina", SkillBlockRarity.Normal, SkillBlockCategory.PitcherPhysical,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O), true,
                new[] { new AbilityChange(PlayerAbility.Stamina, 1) }, 100);
            var cards = new List<OwnerGrowthCardSnapshot>(source.Cards)
            {
                new OwnerGrowthCardSnapshot(
                    new OwnerCollectionCardSnapshot(
                        "pitcher", "pitcher-person", "김투수", 2024,
                        PlayerPosition.StartingPitcher, 5, PlayerCardEdition.Normal,
                        0, 0, false, false, new AbilityRatings(65)),
                    Array.Empty<PlacedSkillBlock>(),
                    Array.Empty<OwnerStudyOption>())
            };
            _view.Bind(new OwnerGrowthSnapshot(
                cards,
                new[]
                {
                    new SkillBlockInstance(101, batterBlock.BlockId),
                    new SkillBlockInstance(202, pitcherBlock.BlockId)
                },
                new[] { batterBlock, pitcherBlock },
                source.Board,
                source.DevelopmentPoints,
                source.StudyCount,
                source.StudyCapacity));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);

            Assert.That(FindOrNull<Button>("Block_101"), Is.Not.Null);
            Assert.That(FindOrNull<Button>("Block_202"), Is.Null);
            Assert.That(Find<Text>("InventoryHeading").text, Does.Contain("1개"));

            Click("PitcherTab");

            Assert.That(FindOrNull<Button>("Block_101"), Is.Null);
            Assert.That(FindOrNull<Button>("Block_202"), Is.Not.Null);
            Assert.That(Find<Text>("InventoryHeading").text, Does.Contain("1개"));
        }

        [Test]
        public void Study_비용확인후확정해야명령을전달하고취소하면확인을폐기한다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            int requests = 0;
            _view.StudyRequested += (card, program) => requests++;
            Click("StartStudy");
            Assert.That(requests, Is.Zero);
            Click("CancelStudy");
            Click("StartStudy");
            Assert.That(requests, Is.Zero);
            Click("StartStudy");
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void Study_차단사유를표시하고신청버튼을잠근다()
        {
            _view.Bind(CreateSnapshot("1군 등록 선수입니다."));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            Assert.That(Find<Button>("StartStudy").interactable, Is.False);
            Assert.That(Find<Text>("StudyBlockedReason").text, Does.Contain("1군 등록"));
        }

        [Test]
        public void Empty_카드와블록이없어도두화면에안내를표시한다()
        {
            var snapshot = CreateSnapshot();
            _view.Bind(new OwnerGrowthSnapshot(Array.Empty<OwnerGrowthCardSnapshot>(), Array.Empty<SkillBlockInstance>(),
                snapshot.Definitions, snapshot.Board, 0, 0, 0));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);
            Assert.That(Find<Button>("BoardCell_0_0").interactable, Is.False);
            Assert.That(Find<Text>("NoBlocks"), Is.Not.Null);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            Assert.That(Find<Button>("StartStudy").interactable, Is.False);
        }

        [TestCase(1100, 560)]
        [TestCase(1600, 900)]
        public void Visual_실제런타임UI를두해상도로출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 없는 실행에서는 PNG 검증을 생략합니다.");
            var cameraObject = new GameObject("GrowthVisualCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                foreach (string route in new[] { OwnerNavigationRoutes.PowerUpSkills, OwnerNavigationRoutes.PowerUpStudy })
                {
                    _view.ShowRoute(route);
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerGrowth).GetMethod("Resize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, null);
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_root.transform);
                    camera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    RenderTexture.active = previous;
                    string directory = Environment.GetEnvironmentVariable("BASEBALL_GROWTH_VISUAL_OUTPUT")
                        ?? Path.GetFullPath("docs/reports/owner-growth-visuals");
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, route.Substring(route.LastIndexOf('.') + 1) + "-" + width + ".png"), texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                cameraObject.GetComponent<Camera>().targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private void Click(string name) => Find<Button>(name).onClick.Invoke();

        private T Find<T>(string name) where T : Component
        {
            T component = FindOrNull<T>(name);
            if (component != null) return component;
            Assert.Fail("UI 요소를 찾을 수 없습니다: " + name);
            return null;
        }

        private T FindOrNull<T>(string name) where T : Component
        {
            foreach (T component in _root.GetComponentsInChildren<T>())
                if (component.name == name) return component;
            return null;
        }

        private static OwnerGrowthSnapshot CreateSnapshot(string reason = "")
        {
            var definitions = new List<SkillBlockDefinition>();
            var inventory = new List<SkillBlockInstance>();
            for (int index = 0; index < 12; index++)
            {
                definitions.Add(new SkillBlockDefinition("block" + index, (SkillBlockRarity)(index % 5), SkillBlockCategory.Contact,
                    TetrominoShapeCatalog.CreateCells((TetrominoShape)(index % 7)), true,
                    new[] { new AbilityChange((PlayerAbility)(index % 6), 1) }, 100));
                inventory.Add(new SkillBlockInstance(index + 1, "block" + index));
            }
            var cards = new List<OwnerGrowthCardSnapshot>();
            string[] names = { "김민준", "이도윤", "박서준", "최지훈", "정현우", "강민호", "윤성호", "임지완" };
            var programs = OwnerCardGrowthBalanceTable.CreateDefault().StudyPrograms;
            for (int index = 0; index < 25; index++)
            {
                var card = new OwnerCollectionCardSnapshot("card" + index, "person" + index, names[index % names.Length],
                    2024, PlayerPosition.Shortstop, 5, PlayerCardEdition.Normal, 0, 0, false, false, new AbilityRatings(65));
                var studies = new List<OwnerStudyOption>();
                foreach (CardStudyProgramDefinition program in programs)
                    if (program.PlayerType == PlayerType.Batter) studies.Add(new OwnerStudyOption(program, "교타력  +2\n타자 정신력  +1", reason));
                cards.Add(new OwnerGrowthCardSnapshot(card, Array.Empty<PlacedSkillBlock>(), studies));
            }
            return new OwnerGrowthSnapshot(cards, inventory, definitions.ToArray(), SkillBoardDefinition.CreateDefault(), 250, 0, 2);
        }
    }
}

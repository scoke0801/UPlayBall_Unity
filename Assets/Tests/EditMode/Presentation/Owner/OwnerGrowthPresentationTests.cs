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
        public void Study_천장목록도선택선수만조회하고선수선택은열여섯장씩표시한다()
        {
            int studyQueries = 0, detailQueries = 0;
            OwnerGrowthSnapshot source = CreateSnapshot();
            var cards = new List<OwnerGrowthCardSnapshot>();
            for (int index = 0; index < 1000; index++)
            {
                var card = new OwnerCollectionCardSnapshot("large" + index, "person" + index, "선수" + index,
                    2024, PlayerPosition.Shortstop, 5, PlayerCardEdition.Normal, 0, 0, false, false);
                cards.Add(new OwnerGrowthCardSnapshot(card, Array.Empty<PlacedSkillBlock>(),
                    () => { studyQueries++; return source.Cards[0].Studies; },
                    () => { detailQueries++; return card; }));
            }
            // 유학에는 성장판이 필요 없다. 스킬 화면을 먼저 만들면 이 진입은 실패해야 한다.
            var snapshot = new OwnerGrowthSnapshot(cards, Array.Empty<SkillBlockInstance>(),
                source.Definitions, null, 250, 0, 2);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _view.Bind(snapshot, OwnerNavigationRoutes.PowerUpStudy);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            watch.Stop();
            TestContext.WriteLine($"유학 보유 1,000장 진입 Bind+Show: {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.That(FindOrNull<ScrollRect>("PlayerInventory"), Is.Null);
            Assert.That(studyQueries, Is.EqualTo(1));
            Assert.That(detailQueries, Is.EqualTo(1));
            Click("ChooseStudyPlayer");
            Assert.That(Find<ScrollRect>("PlayerInventory").content.childCount, Is.EqualTo(16));
            Button firstCard = Find<Button>("Card_large0");
            Text firstName = firstCard.transform.Find("Name").GetComponent<Text>();
            Assert.That(firstName.text, Is.EqualTo("선수0"));
            Assert.That(firstName.gameObject.activeInHierarchy, Is.True);
            Assert.That(firstName.color.a, Is.GreaterThan(0.9f));
            Assert.That(firstName.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(.89f),
                "유학 선수 선택에서는 이름이 카드 상단에 표시되어야 합니다.");
            Canvas.ForceUpdateCanvases();
            ScrollRect picker = Find<ScrollRect>("PlayerInventory");
            LayoutRebuilder.ForceRebuildLayoutImmediate(picker.content);
            Assert.That(picker.content.rect.height, Is.LessThanOrEqualTo(picker.viewport.rect.height),
                "한 페이지의 두 행은 별도 스크롤 없이 선수 이름까지 보여야 합니다.");
            Click("NextRosterPage");
            Assert.That(Find<Text>("RosterPage").text, Does.StartWith("2/"));
            Assert.That(FindOrNull<Button>("Card_large0"), Is.Null);
            Click("Card_large16");
            Assert.That(studyQueries, Is.EqualTo(2));
            Assert.That(detailQueries, Is.EqualTo(2));
            Click("CloseStudyPlayerPicker");
            Assert.That(Find<Text>("StudyPlayer").text, Does.StartWith("선수16"));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            Assert.That(studyQueries, Is.EqualTo(2));
            Assert.That(detailQueries, Is.EqualTo(2));
        }

        [Test]
        public void Study_비행기아이콘으로목적지를열고같은버튼과지도빈곳으로닫는다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);

            Button destination = Find<Button>("StudyPin_study_contact");
            Assert.That(destination.GetComponent<RectTransform>().rect.width, Is.EqualTo(60).Within(0.1f));
            Assert.That(destination.GetComponent<RawImage>().texture, Is.Not.Null);
            Assert.That(FindOrNull<Image>("StudyRoute_study_contact"), Is.Not.Null);
            Assert.That(Find<Text>("StudyDestination").text, Is.Empty);
            Assert.That(Find<Text>("StudyName").text, Does.Contain("목적지를 선택"));
            Assert.That(FindOrNull<Button>("StartStudy"), Is.Null);

            Click("StudyPin_study_contact");
            Assert.That(Find<Text>("StudyDestination").text, Does.Contain("도쿄"));
            Assert.That(FindOrNull<Button>("StartStudy"), Is.Not.Null);
            Assert.That(Find<Button>("ChooseStudyPlayer").IsActive(), Is.True);
            Assert.That(Find<Text>("StudyPlayer").text, Is.Not.Empty);
            Assert.That(Find<CanvasGroup>("StudyPlayerCard").blocksRaycasts, Is.False);
            RawImage selectedPlane = Find<RawImage>("StudyPin_study_contact");
            Assert.That(selectedPlane.GetComponent<Outline>(), Is.Null,
                "비행기 원본을 복제하는 Outline 효과를 사용하면 안 됩니다.");
            Assert.That(selectedPlane.rectTransform.pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(FindOrNull<Image>("StudySelection_study_contact"), Is.Not.Null);

            Click("StudyPin_study_contact");
            Assert.That(Find<Text>("StudyDestination").text, Is.Empty);
            Click("StudyPin_study_contact");
            Click("StudyInformation");
            Assert.That(Find<Text>("StudyDestination").text, Is.Empty);
            Click("StudyPin_study_contact");
            Click("StudyWorldMap");
            Assert.That(Find<Text>("StudyDestination").text, Is.Empty);
            Assert.That(FindOrNull<Image>("StudyHomeNode"), Is.Not.Null);
        }

        [Test]
        public void Study_모든목적지를선택해도비행기가지도안에있고선수변경을계속할수있다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            foreach (OwnerStudyOption study in CreateSnapshot().Cards[0].Studies)
            {
                Click("StudyPin_" + study.Program.ProgramId);
                RectTransform map = Find<RawImage>("StudyWorldMap").rectTransform;
                RectTransform plane = Find<RawImage>("StudyPin_" + study.Program.ProgramId).rectTransform;
                var corners = new Vector3[4];
                plane.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = map.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(map.rect.xMin, map.rect.xMax));
                    Assert.That(local.y, Is.InRange(map.rect.yMin, map.rect.yMax));
                }
                Click("ChooseStudyPlayer");
                Click("Card_card1");
                Click("CloseStudyPlayerPicker");
                Assert.That(Find<Text>("StudyPlayer").text, Does.StartWith("이도윤"));
                Assert.That(Find<Button>("StartStudy").gameObject.activeInHierarchy, Is.True);
            }
        }

        [Test]
        public void Study_진행조건이잠긴목적지도선택해조건을확인할수있다()
        {
            _view.Bind(CreateSnapshot(
                "잠김 · 포스트시즌 우승 1회를 달성하면 이용할 수 있습니다.",
                false,
                "해금 조건  포스트시즌 우승 1회"));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);

            Assert.That(Find<Button>("StudyPin_study_contact").interactable, Is.True,
                "잠긴 목적지도 조건 확인을 위해 선택할 수 있어야 합니다.");
            Click("StudyPin_study_contact");
            Assert.That(Find<Button>("StartStudy").interactable, Is.False);
            Assert.That(Find<Text>("StudyUnlock").text, Does.Contain("포스트시즌 우승"));
            Assert.That(Find<Text>("StudyBlockedReason").text, Does.StartWith("잠김"));
        }

        [Test]
        public void Study_기본과정과진행형과정의해금계약을구분한다()
        {
            IReadOnlyList<CardStudyProgramDefinition> programs =
                OwnerCardGrowthBalanceTable.CreateDefault().StudyPrograms;
            CardStudyProgramDefinition contact = FindProgram(programs, "study_contact");
            CardStudyProgramDefinition power = FindProgram(programs, "study_power");
            CardStudyProgramDefinition allround = FindProgram(programs, "study_batter_allround");

            Assert.That(contact.UnlockRequirement.IsSatisfied(LeagueGrade.Rookie, 0), Is.True);
            Assert.That(power.UnlockRequirement.IsSatisfied(LeagueGrade.Rookie, 0), Is.False);
            Assert.That(power.UnlockRequirement.IsSatisfied(LeagueGrade.Minor, 0), Is.True);
            Assert.That(allround.UnlockRequirement.IsSatisfied(LeagueGrade.Galaxy, 0), Is.False);
            Assert.That(allround.UnlockRequirement.IsSatisfied(LeagueGrade.Rookie, 1), Is.True);
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
                "batter_bunt", SkillBlockRarity.Normal, SkillBlockCategory.Bunt,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O), true,
                new[] { new AbilityChange(PlayerAbility.Bunt, 1) }, 100);
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
        public void Skills_작은선수카드는이름을상단에표시한다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);

            Transform card = Find<Button>("Card_card0").transform;
            Text name = card.Find("Name").GetComponent<Text>();
            Text position = card.Find("Position").GetComponent<Text>();
            Text selected = card.Find("SelectionOverlay/Header/Label").GetComponent<Text>();

            Assert.That(name.text, Is.EqualTo("김민준"));
            Assert.That(name.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(.89f));
            Assert.That(position.text, Is.EqualTo("유격수"));
            Assert.That(position.rectTransform.anchorMax.y, Is.LessThan(.3f));
            Assert.That(selected.text, Is.EqualTo("선택 · 김민준"));
        }

        [Test]
        public void Skills_선수단장착카드를우선노출하고검색과필터를적용한다()
        {
            OwnerGrowthSnapshot source = CreateSnapshot();
            var activeCard = new OwnerCollectionCardSnapshot(
                "active-card", "active-person", "장착선수", 2025,
                PlayerPosition.CenterField, 6, PlayerCardEdition.Rare,
                0, 0, false, false, new AbilityRatings(70),
                isActiveRoster: true);
            var cards = new List<OwnerGrowthCardSnapshot>(source.Cards)
            {
                new OwnerGrowthCardSnapshot(
                    activeCard,
                    Array.Empty<PlacedSkillBlock>(),
                    Array.Empty<OwnerStudyOption>())
            };
            _view.Bind(new OwnerGrowthSnapshot(
                cards,
                source.Inventory,
                source.Definitions,
                source.Board,
                source.DevelopmentPoints,
                source.StudyCount,
                source.StudyCapacity));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpSkills);

            ScrollRect roster = Find<ScrollRect>("PlayerInventory");
            Assert.That(roster.content.GetChild(0).name, Is.EqualTo("Card_active-card"));
            Assert.That(Find<Text>("SelectedName").text, Is.EqualTo("장착선수"));

            Click("RosterEquipped");
            Assert.That(Find<Text>("RosterPage").text, Does.Contain("1명"));
            Assert.That(FindOrNull<Button>("Card_card0"), Is.Null);

            Click("ResetRosterSearch");
            InputField search = Find<InputField>("RosterSearch");
            search.text = "2025";
            Click("ApplyRosterSearch");
            Assert.That(FindOrNull<Button>("Card_active-card"), Is.Not.Null);
            Assert.That(Find<Text>("RosterPage").text, Does.Contain("1명"));
        }

        [Test]
        public void Study_비용확인후확정해야명령을전달하고취소하면확인을폐기한다()
        {
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpStudy);
            Click("StudyPin_study_contact");
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
            Click("StudyPin_study_contact");
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
            Assert.That(FindOrNull<Button>("StartStudy"), Is.Null);
            Assert.That(Find<Text>("StudyName").text, Does.Contain("목적지를 선택"));
        }

        [TestCase(1100, 560)]
        [TestCase(1600, 900)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
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
                foreach (string route in new[] { OwnerNavigationRoutes.PowerUpSkills, OwnerNavigationRoutes.PowerUpStudy, "StudySelected", "StudyPlayerPicker" })
                {
                    _view.ShowRoute(route.StartsWith("Study") ? OwnerNavigationRoutes.PowerUpStudy : route);
                    if (route == "StudySelected") Click("StudyPin_study_defense");
                    if (route == "StudyPlayerPicker") Click("ChooseStudyPlayer");
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

        private static OwnerGrowthSnapshot CreateSnapshot(
            string reason = "",
            bool isUnlocked = true,
            string unlockText = "해금 조건  기본 개방")
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
                    if (program.PlayerType == PlayerType.Batter) studies.Add(new OwnerStudyOption(
                        program,
                        "교타력  +2\n타자 정신력  +1",
                        reason,
                        isUnlocked,
                        unlockText));
                cards.Add(new OwnerGrowthCardSnapshot(card, Array.Empty<PlacedSkillBlock>(), studies));
            }
            return new OwnerGrowthSnapshot(cards, inventory, definitions.ToArray(), SkillBoardDefinition.CreateDefault(), 250, 0, 2);
        }

        private static CardStudyProgramDefinition FindProgram(
            IReadOnlyList<CardStudyProgramDefinition> programs,
            string programId)
        {
            for (int index = 0; index < programs.Count; index++)
                if (programs[index].ProgramId == programId) return programs[index];
            Assert.Fail("유학 프로그램을 찾을 수 없습니다: " + programId);
            return null;
        }
    }
}

using System;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>훈련 선택의 포커스 보존, 확인 뒤 단일 실행과 여섯 과정의 화면 배치를 검증한다.</summary>
    public sealed class OwnerTrainingUiTests
    {
        private GameObject _root;
        private UI_Scene_OwnerPowerUp _view;

        [TearDown]
        public void TearDown()
        {
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        private void CreateView()
        {
            _root = new GameObject("TrainingCanvas", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            _view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
        }

        private static OwnerCardTrainingProgramSnapshot[] Programs()
        {
            string[] names = { "교타력", "장타력", "주력", "번트력", "수비력", "타자 정신력" };
            var result = new OwnerCardTrainingProgramSnapshot[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = new OwnerCardTrainingProgramSnapshot("program" + i, names[i], PlayerAbility.Contact,
                    49 + i, i == 4 ? 49 + i : 52 + i, i == 4 ? 0 : 1, 12, i != 4, i == 4 ? "훈련 상한에 도달했습니다." : "");
            return result;
        }

        [Test]
        public void 검색과포지션교차필터는선택과훈련미리보기를유지하고초기화한다()
        {
            CreateView();
            int previews = 0;
            var snapshot = CreateSnapshot(1000, () => { }, () => previews++, () => { });
            _view.Bind(snapshot, OwnerNavigationRoutes.PowerUpTraining);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
            InputField search = _root.GetComponentsInChildren<InputField>().Single(f => f.name == "TrainingSearch");
            Dropdown position = _root.GetComponentsInChildren<Dropdown>().Single(f => f.name == "TrainingPositionFilter");
            Text count = _root.GetComponentsInChildren<Text>().Single(t => t.name == "TrainingCardCount");
            Transform front = _root.GetComponentsInChildren<RectTransform>().First(r => r.name == "CardFront").GetChild(0);
            search.text = "  박지훈  ";
            Assert.That(count.text, Does.Contain("표시 167 / 보유 1,000"));
            Assert.That(_root.GetComponentsInChildren<Text>().Single(t => t.name == "TrainingSelectionHint").text, Does.Contain("유지"));
            position.value = 2;
            Assert.That(count.text, Does.StartWith("표시 0"));
            Assert.That(_root.GetComponentsInChildren<Text>().Single(t => t.name == "TrainingEmpty").text, Does.Contain("초기화"));
            Assert.That(previews, Is.EqualTo(1));
            Assert.That(front.parent.GetChild(0), Is.SameAs(front));
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpScout);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
            Assert.That(search.text, Is.EqualTo("  박지훈  "));
            _root.GetComponentsInChildren<Button>().Single(b => b.name == "ResetTrainingFilters").onClick.Invoke();
            Assert.That(count.text, Does.StartWith("표시 1,000"));
            Assert.That(search.text, Is.Empty);
            Assert.That(position.value, Is.Zero);
            Assert.That(previews, Is.EqualTo(1));
        }

        [Test]
        public void 구단연도필터와정렬은요약만사용하고갱신후조건을보존한다()
        {
            CreateView();
            var cards = new[]
            {
                new OwnerCollectionCardSnapshot("a", "a", "김선수", 2024, PlayerPosition.Catcher, 3,
                    PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(), teamDisplayName: "서울"),
                new OwnerCollectionCardSnapshot("b", "b", "박선수", 2025, PlayerPosition.StartingPitcher, 9,
                    PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(), teamDisplayName: "부산"),
                new OwnerCollectionCardSnapshot("c", "c", "이선수", 2025, PlayerPosition.Shortstop, 6,
                    PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(), teamDisplayName: "서울")
            };
            Func<OwnerPowerUpSnapshot> snapshot = () => new OwnerPowerUpSnapshot(
                () => new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), ""),
                () => new OwnerCardTrainingScreenSnapshot(cards.Select(c => new OwnerCardTrainingTargetSnapshot(c, Programs())).ToArray(), 100),
                () => new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 100),
                id => cards.Single(c => c.CardId == id));
            _view.Bind(snapshot(), OwnerNavigationRoutes.PowerUpTraining);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Func<string[]> visible = () => ((System.Collections.Generic.List<OwnerCollectionCardSnapshot>)
                typeof(UI_Scene_OwnerPowerUp).GetField("_visibleTrainingCards", flags).GetValue(_view)).Select(c => c.CardId).ToArray();
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, visible());
            _root.GetComponentsInChildren<Dropdown>().Single(d => d.name == "TrainingSort").value = 1;
            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, visible());
            Dropdown year = _root.GetComponentsInChildren<Dropdown>().Single(d => d.name == "YearFilter");
            Dropdown team = _root.GetComponentsInChildren<Dropdown>().Single(d => d.name == "TeamFilter");
            year.value = year.options.FindIndex(o => o.text == "2025년");
            team.value = team.options.FindIndex(o => o.text == "서울");
            CollectionAssert.AreEqual(new[] { "c" }, visible());
            _view.Bind(snapshot(), OwnerNavigationRoutes.PowerUpTraining);
            CollectionAssert.AreEqual(new[] { "c" }, visible());
            _root.GetComponentsInChildren<Button>().Single(b => b.name == "ResetTrainingFilters").onClick.Invoke();
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, visible());
        }

        [Test]
        public void 훈련선택은행과카드를재생성하지않고확인뒤한번실행한다()
        {
            CreateView();
            _view.Bind(CreateSnapshot(10, () => { }, () => { }, () => { }), OwnerNavigationRoutes.PowerUpTraining);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
            Button[] rows = _root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Program_")).ToArray();
            Assert.That(rows.Length, Is.EqualTo(6));
            var front = _root.GetComponentsInChildren<RectTransform>().First(r => r.name == "CardFront");
            Transform child = front.GetChild(0);
            rows[1].onClick.Invoke();
            CollectionAssert.AreEqual(rows, _root.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Program_")).ToArray());
            Assert.That(front.GetChild(0), Is.SameAs(child));
            Button execute = _root.GetComponentsInChildren<Button>().Single(b => b.name == "TrainingExecute");
            Assert.That(_root.GetComponentsInChildren<Text>().Single(t => t.name == "TrainingBalance").text, Does.Contain("88"));
            int calls = 0;
            _view.TrainingRequested += (card, program) => { calls++; Assert.That(program, Is.EqualTo("program1")); };
            execute.onClick.Invoke();
            Assert.That(calls, Is.Zero);
            Assert.That(_view.TryHandleCancel(), Is.True);
            Assert.That(calls, Is.Zero);
            execute.onClick.Invoke();
            Button confirm = _root.GetComponentsInChildren<Button>().Single(b => b.name == "Confirm");
            confirm.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            rows[4].onClick.Invoke();
            Assert.That(execute.interactable, Is.False);
            Assert.That(_root.GetComponentsInChildren<Text>().Single(t => t.name == "TrainingBalance").text, Does.Contain("상한"));
        }
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 해상도별카드목록을기존스킨으로출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            CreateView();
            var cameraObject = new GameObject("PowerUpCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.gray;
                camera.targetTexture = target;
                Canvas canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                _view.Bind(CreateSnapshot(1000, () => { }, () => { }, () => { }));
                foreach (int screenState in new[] { 0, 1, 2 })
                {
                    bool enhancement = screenState > 0;
                    _view.ShowRoute(enhancement ? OwnerNavigationRoutes.PowerUpEnhancementSale : OwnerNavigationRoutes.PowerUpTraining);
                    if (screenState == 2) SelectEnhancementCard();
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_root.GetComponent<RectTransform>());
                    UpdateView();
                    Canvas.ForceUpdateCanvases();
                    RectTransform content = FindInventory(enhancement);
                    Assert.That(content.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.InRange(1, 100));
                    Assert.That(content.GetComponentInParent<ScrollRect>().viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                    UpdateView();
                    Canvas.ForceUpdateCanvases();

                    camera.Render();
                    foreach (Text label in _root.GetComponentsInChildren<Text>()) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        texture.Apply();
                        string output = System.IO.Path.GetFullPath("../screenshots");
                        System.IO.Directory.CreateDirectory(output);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,
                            $"{(enhancement ? "fusion-" + screenState : "training")}-redesign-{width}.png"), texture.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                    if (enhancement) AssertEnhancementBounds();
                    else AssertTrainingBounds();
                }
            }
            finally
            {
                RenderTexture.active = previous;
                cameraObject.GetComponent<Camera>().targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private void AssertTrainingBounds()
        {
            RectTransform filters = _root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "TrainingSearchRow");
            RectTransform safe = (RectTransform)filters.parent;
            foreach (Selectable control in safe.GetComponentsInChildren<Selectable>())
            {
                if (control.GetComponentInParent<ScrollRect>() != null) continue;
                var bounds = new Vector3[4];
                control.GetComponent<RectTransform>().GetWorldCorners(bounds);
                foreach (Vector3 corner in bounds)
                {
                    Vector3 point = safe.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(safe.rect.xMin - 1, safe.rect.xMax + 1), control.name);
                    Assert.That(point.y, Is.InRange(safe.rect.yMin - 1, safe.rect.yMax + 1), control.name);
                }
            }
            ScrollRect scroll = _root.GetComponentsInChildren<ScrollRect>().Single(s => s.name == "ProgramScroll");
            var corners = new Vector3[4];
            foreach (Button button in scroll.content.GetComponentsInChildren<Button>())
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = scroll.viewport.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(scroll.viewport.rect.xMin - 1, scroll.viewport.rect.xMax + 1), button.name);
                    Assert.That(point.y, Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1), button.name);
                }
                Assert.That(button.targetGraphic, Is.Not.Null);
                Assert.That(button.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null);
            }
            RectTransform[] panels = _root.GetComponentsInChildren<RectTransform>()
                .Where(r => r.name == "TrainingTargets" || r.name == "TrainingCard" || r.name == "TrainingPrograms").ToArray();
            Assert.That(panels.Length, Is.EqualTo(3));
            for (int i = 1; i < panels.Length; i++)
            {
                var previous = new Vector3[4];
                panels[i - 1].GetWorldCorners(previous);
                panels[i].GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(previous[2].x));
            }
        }

        private void AssertEnhancementBounds()
        {
            RectTransform inventory = _root.GetComponentsInChildren<RectTransform>()
                .Single(r => r.name == "InventoryBoard").Find("ContentSafeRect") as RectTransform;
            Assert.That(inventory, Is.Not.Null);
            foreach (Transform child in inventory)
            {
                RectTransform rect = (RectTransform)child;
                Assert.That(rect.anchoredPosition.x, Is.GreaterThanOrEqualTo(0), child.name);
                Assert.That(rect.anchoredPosition.x + rect.rect.width, Is.LessThanOrEqualTo(inventory.rect.width), child.name);
                Assert.That(-rect.anchoredPosition.y, Is.GreaterThanOrEqualTo(0), child.name);
                Assert.That(-rect.anchoredPosition.y + rect.rect.height, Is.LessThanOrEqualTo(inventory.rect.height), child.name);
            }
            RectTransform content = _root.GetComponentsInChildren<RectTransform>()
                .Single(r => r.name == "RegistrationFrame").Find("ContentSafeRect") as RectTransform;
            var corners = new Vector3[4];
            foreach (Transform child in content)
            {
                ((RectTransform)child).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = content.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(content.rect.xMin - 1, content.rect.xMax + 1), child.name);
                    Assert.That(point.y, Is.InRange(content.rect.yMin - 1, content.rect.yMax + 1), child.name);
                }
            }
            Assert.That(_root.GetComponentsInChildren<Text>().Any(t => t.text == "○"), Is.False);
        }

        private void SelectEnhancementCard() => typeof(UI_Scene_OwnerPowerUp).GetMethod("SelectAndRegisterEnhancementCard",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_view, new object[] { "card0" });

        [Test]
        public void 합성필터는교차검색과초기화를지원하고등록대상을유지한다()
        {
            CreateView();
            _view.Bind(CreateSnapshot(12, () => { }, () => { }, () => { }), OwnerNavigationRoutes.PowerUpEnhancementSale);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            SelectEnhancementCard();
            InputField search = _root.GetComponentsInChildren<InputField>().Single(input => input.name == "EnhancementSearch");
            Text summary = _root.GetComponentsInChildren<Text>().Single(text => text.name == "EnhancementCardCount");
            Button execute = _root.GetComponentsInChildren<Button>().Single(button => button.name == "Enhance");
            search.text = "김도윤";
            Assert.That(summary.text, Does.StartWith("표시 2 /"));
            _root.GetComponentsInChildren<Button>().Single(button => button.name == "DuplicateFilter").onClick.Invoke();
            Assert.That(summary.text, Does.StartWith("표시 2 /"));
            search.text = "없는선수";
            Assert.That(summary.text, Does.StartWith("표시 0 /"));
            Assert.That(_root.GetComponentsInChildren<Text>().Any(text => text.name == "EnhancementEmptyResults"), Is.True);
            Assert.That(execute.interactable, Is.True, "필터가 등록된 합성 대상을 임의로 교체하지 않는다.");
            _root.GetComponentsInChildren<Button>().Single(button => button.name == "HideLocked").onClick.Invoke();
            Assert.That(_root.GetComponentsInChildren<Button>().Single(button => button.name == "HideLocked")
                .transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("잠금 선수: 숨김"));
            _root.GetComponentsInChildren<Button>().Single(button => button.name == "ResetEnhancementFilters").onClick.Invoke();
            Assert.That(search.text, Is.Empty);
            Assert.That(summary.text, Does.StartWith("표시 12 /"));
            Assert.That(_root.GetComponentsInChildren<Text>().Any(text => text.name == "EnhancementEmptyResults"), Is.False);
        }

        [Test]
        public void 합성구단연도잠금중복필터는요약으로판정하고재조회후조건을유지한다()
        {
            CreateView();
            var cards = new[]
            {
                new OwnerCollectionCardSnapshot("a", "a", "김선수", 2024, PlayerPosition.Catcher, 3,
                    PlayerCardEdition.Normal, 0, 1, false, false, CreateAbilities(), teamDisplayName: "서울"),
                new OwnerCollectionCardSnapshot("b", "b", "박선수", 2025, PlayerPosition.StartingPitcher, 9,
                    PlayerCardEdition.Normal, 0, 0, false, false, CreateAbilities(), teamDisplayName: "부산"),
                new OwnerCollectionCardSnapshot("c", "c", "이선수", 2025, PlayerPosition.Shortstop, 6,
                    PlayerCardEdition.Normal, 0, 2, true, false, CreateAbilities(), teamDisplayName: "서울")
            };
            Func<OwnerPowerUpSnapshot> snapshot = () => new OwnerPowerUpSnapshot(
                () => new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), ""),
                () => new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 100),
                () => new OwnerEnhancementSaleScreenSnapshot(cards.Select(card => new OwnerEnhancementSaleTargetSnapshot(card,
                    new CardEnhancementPreview(0, 1, card.DuplicateCount, CardEnhancementResult.Enhanced),
                    Array.Empty<CardSalePreview>())).ToArray(), 100), id => cards.Single(card => card.CardId == id));
            _view.Bind(snapshot(), OwnerNavigationRoutes.PowerUpEnhancementSale);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Func<string[]> visible = () => ((System.Collections.Generic.List<OwnerCollectionCardSnapshot>)
                typeof(UI_Scene_OwnerPowerUp).GetField("_visibleEnhancementCards", flags).GetValue(_view)).Select(card => card.CardId).ToArray();
            Func<string, Button> button = name => _root.GetComponentsInChildren<Button>().Single(item => item.name == name);
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, visible());
            _root.GetComponentsInChildren<Dropdown>().Single(item => item.name == "YearFilter").value = 1;
            Dropdown team = _root.GetComponentsInChildren<Dropdown>().Single(item => item.name == "TeamFilter");
            team.value = team.options.FindIndex(option => option.text == "서울");
            CollectionAssert.AreEqual(new[] { "c" }, visible());
            _view.Bind(snapshot(), OwnerNavigationRoutes.PowerUpEnhancementSale);
            CollectionAssert.AreEqual(new[] { "c" }, visible());
            button("HideLocked").onClick.Invoke();
            Assert.That(visible(), Is.Empty);
            button("ResetEnhancementFilters").onClick.Invoke();
            button("DuplicateFilter").onClick.Invoke();
            CollectionAssert.AreEqual(new[] { "c", "a" }, visible());
            Dropdown role = _root.GetComponentsInChildren<Dropdown>().Single(item => item.name == "EnhancementRoleFilter");
            role.value = role.options.FindIndex(option => option.text == "포수");
            CollectionAssert.AreEqual(new[] { "a" }, visible());
        }

        [TestCase(CardEnhancementResult.Enhanced)]
        [TestCase(CardEnhancementResult.NoDuplicate)]
        [TestCase(CardEnhancementResult.MaximumLevel)]
        public void 합성상태와취소및확인뒤단일실행을검증한다(CardEnhancementResult result)
        {
            CreateView();
            OwnerCollectionCardSnapshot card = CreateCard(0);
            var target = new OwnerEnhancementSaleTargetSnapshot(card,
                new CardEnhancementPreview(0, 1, result == CardEnhancementResult.NoDuplicate ? 0 : 1, result),
                Array.Empty<CardSalePreview>());
            _view.Bind(new OwnerPowerUpSnapshot(
                () => new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), ""),
                () => new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                () => new OwnerEnhancementSaleScreenSnapshot(new[] { target }, 100), id => card),
                OwnerNavigationRoutes.PowerUpEnhancementSale);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            SelectEnhancementCard();
            Button execute = _root.GetComponentsInChildren<Button>().Single(b => b.name == "Enhance");
            Assert.That(execute.interactable, Is.EqualTo(result == CardEnhancementResult.Enhanced));
            if (!execute.interactable) return;
            int calls = 0;
            _view.EnhancementRequested += id => calls++;
            execute.onClick.Invoke();
            Assert.That(calls, Is.Zero);
            Assert.That(_view.TryHandleCancel(), Is.True);
            execute.onClick.Invoke();
            _root.GetComponentsInChildren<Button>().Single(b => b.name == "Confirm").onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            _root.GetComponentsInChildren<Button>().Single(b => b.name == "Register").onClick.Invoke();
            Assert.That(execute.interactable, Is.False);
            Assert.That(_root.GetComponentsInChildren<Text>().Single(t => t.name == "TargetEmpty"), Is.Not.Null);
        }
        private RectTransform FindInventory(bool enhancement)
        {
            return _root.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => enhancement ? scroll.name == "EnhancementInventory" : scroll.name == "CardScroll").content;
        }

        private void UpdateView() => typeof(UI_Scene_OwnerPowerUp).GetMethod("LateUpdate",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_view, null);

        private static AbilityRatings CreateAbilities()
        {
            var abilities = new AbilityRatings(49);
            PlayerAbility[] order = { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed,
                PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental };
            for (int i = 0; i < order.Length; i++) abilities.AddClamped(order[i], i);
            return abilities;
        }
        private static OwnerCollectionCardSnapshot CreateCard(int index) => new OwnerCollectionCardSnapshot(
            "card" + index, "person" + index, new[] { "김도윤", "박지훈", "이준서", "최민준", "정현우", "강시우" }[index % 6], 2025, PlayerPosition.Shortstop,
            7, PlayerCardEdition.Normal, 0, 10000, false, false, CreateAbilities(), teamDisplayName: "서울 트윈스");

        private static OwnerPowerUpSnapshot CreateSnapshot(int count, Action detail, Action program, Action sale)
        {
            var training = new OwnerCardTrainingTargetSnapshot[count];
            var enhancement = new OwnerEnhancementSaleTargetSnapshot[count];
            var cards = new System.Collections.Generic.Dictionary<string, OwnerCollectionCardSnapshot>();
            for (int index = 0; index < count; index++)
            {
                OwnerCollectionCardSnapshot card = CreateCard(index);
                cards.Add(card.CardId, card);
                training[index] = new OwnerCardTrainingTargetSnapshot(card, () =>
                {
                    program();
                    return Programs();
                });
                enhancement[index] = new OwnerEnhancementSaleTargetSnapshot(card,
                    () => new CardEnhancementPreview(0, 1, 1, CardEnhancementResult.Enhanced),
                    quantity => { sale(); return default; });
            }
            return new OwnerPowerUpSnapshot(
                () => new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), ""),
                () => new OwnerCardTrainingScreenSnapshot(training, 100),
                () => new OwnerEnhancementSaleScreenSnapshot(enhancement, 100),
                id => { detail(); return cards[id]; });
        }
    }
}

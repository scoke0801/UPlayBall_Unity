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

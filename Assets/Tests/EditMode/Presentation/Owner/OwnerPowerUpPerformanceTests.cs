using System;
using System.Collections;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>대량 카드 진입·재사용·스크롤과 지연 Preview의 조회 범위를 검증한다.</summary>
    public sealed class OwnerPowerUpPerformanceTests
    {
        private GameObject _root;
        private UI_Scene_OwnerPowerUp _view;

        [TearDown]
        public void TearDown()
        {
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [TestCase(100, false)]
        [TestCase(1000, false)]
        [TestCase(10000, false)]
        [TestCase(100, true)]
        [TestCase(1000, true)]
        [TestCase(10000, true)]
        public void 대량카드는보이는범위만생성하고선택시재사용한다(int count, bool enhancement)
        {
            CreateView();
            int details = 0, programs = 0, sales = 0;
            OwnerPowerUpSnapshot snapshot = CreateSnapshot(count, () => details++, () => programs++, () => sales++);
            string route = enhancement ? OwnerNavigationRoutes.PowerUpEnhancementSale : OwnerNavigationRoutes.PowerUpTraining;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _view.Bind(snapshot, route);
            _view.ShowRoute(route);
            watch.Stop();
            RectTransform content = FindInventory(enhancement);
            PlayerMiniCardView[] before = content.GetComponentsInChildren<PlayerMiniCardView>();
            Assert.That(before.Length, Is.InRange(2, 60));
            Assert.That(details, Is.InRange(1, 61));
            Assert.That(programs, Is.EqualTo(enhancement ? 0 : 1));
            Assert.That(sales, Is.EqualTo(enhancement ? 1 : 0));
            before[1].GetComponent<Button>().onClick.Invoke();
            CollectionAssert.AreEqual(before, content.GetComponentsInChildren<PlayerMiniCardView>());
            Assert.That(programs, Is.EqualTo(enhancement ? 0 : 2));
            Assert.That(sales, Is.EqualTo(enhancement ? 2 : 0));
            TestContext.WriteLine($"{route} {count:N0}장 진입 {watch.Elapsed.TotalMilliseconds:F2} ms / UI {before.Length} / 상세 {details}");
        }

        [UnityTest]
        public IEnumerator 마지막카드까지스크롤하고빈상태로갱신할수있다()
        {
            CreateView();
            _view.Bind(CreateSnapshot(1000, () => { }, () => { }, () => { }), OwnerNavigationRoutes.PowerUpEnhancementSale);
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            RectTransform content = FindInventory(true);
            ScrollRect scroll = content.GetComponentInParent<ScrollRect>();
            int poolSize = content.childCount;
            scroll.verticalNormalizedPosition = 0;
            // EditMode에서는 MonoBehaviour LateUpdate를 명시적으로 호출한다.
            UpdateView();
            yield return null;
            Assert.That(content.childCount, Is.EqualTo(poolSize));
            Assert.That(content.GetComponentsInChildren<PlayerMiniCardView>().Any(card => card.name == "Card_card999"), Is.True);
            _view.Bind(CreateSnapshot(0, () => { }, () => { }, () => { }));
            Assert.That(content.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.Zero);
        }

        [Test]
        public void 판매미리보기는수량별한번만조회하고새Snapshot은재조회한다()
        {
            int calls = 0;
            var card = CreateCard(0);
            OwnerEnhancementSaleTargetSnapshot CreateTarget() => new OwnerEnhancementSaleTargetSnapshot(card,
                () => default, count => { calls++; return default; });
            OwnerEnhancementSaleTargetSnapshot target = CreateTarget();
            Assert.That(calls, Is.Zero);
            target.GetSalePreview(1);
            target.GetSalePreview(1);
            target.GetSalePreview(2);
            Assert.That(calls, Is.EqualTo(2));
            target.GetSalePreview(0);
            Assert.That(calls, Is.EqualTo(2));
            CreateTarget().GetSalePreview(1);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void 카드위휠입력은목록으로전달하고포커스는스크롤후에도선수를유지한다()
        {
            CreateView();
            var events = new GameObject("PowerUpEvents", typeof(EventSystem));
            EventSystem eventSystem = events.GetComponent<EventSystem>();
            bool hasManualLifecycle = EventSystem.current == null;
            try
            {
                if (hasManualLifecycle)
                    typeof(EventSystem).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic).Invoke(eventSystem, null);
                _view.Bind(CreateSnapshot(1000, () => { }, () => { }, () => { }), OwnerNavigationRoutes.PowerUpEnhancementSale);
                _view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
                RectTransform content = FindInventory(true);
                PlayerMiniCardView[] cards = content.GetComponentsInChildren<PlayerMiniCardView>();
                ScrollRect scroll = content.GetComponentInParent<ScrollRect>();
                Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(cards[0].gameObject), Is.EqualTo(scroll.gameObject));
                Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(cards[0].gameObject), Is.EqualTo(scroll.gameObject));
                PlayerMiniCardView bottom = cards[cards.Length - 1];
                string expectedId = bottom.Model.PlayerId;
                EventSystem.current.SetSelectedGameObject(bottom.gameObject);
                UpdateView();
                Assert.That(content.anchoredPosition.y, Is.GreaterThan(0));
                Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<PlayerMiniCardView>().Model.PlayerId,
                    Is.EqualTo(expectedId));
            }
            finally
            {
                if (hasManualLifecycle)
                    typeof(EventSystem).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic).Invoke(eventSystem, null);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        private void CreateView()
        {
            _root = new GameObject("PowerUpPerformance", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1100, 650);
            _view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
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
                scaler.referenceResolution = new Vector2(1280, 720);
                _view.Bind(CreateSnapshot(1000, () => { }, () => { }, () => { }));
                foreach (bool enhancement in new[] { false, true })
                {
                    _view.ShowRoute(enhancement ? OwnerNavigationRoutes.PowerUpEnhancementSale : OwnerNavigationRoutes.PowerUpTraining);
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_root.GetComponent<RectTransform>());
                    UpdateView();
                    Canvas.ForceUpdateCanvases();
                    RectTransform content = FindInventory(enhancement);
                    Assert.That(content.GetComponentsInChildren<PlayerMiniCardView>().Length, Is.InRange(1, 100));
                    Assert.That(content.GetComponentInParent<ScrollRect>().viewport.GetComponent<RectMask2D>(), Is.Not.Null);
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
                            $"{(enhancement ? "enhancement" : "training")}-{width}.png"), texture.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
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

        private RectTransform FindInventory(bool enhancement)
        {
            return _root.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => enhancement ? scroll.name == "EnhancementInventory" : scroll.name == "CardScroll").content;
        }

        private void UpdateView() => typeof(UI_Scene_OwnerPowerUp).GetMethod("LateUpdate",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_view, null);

        private static OwnerCollectionCardSnapshot CreateCard(int index) => new OwnerCollectionCardSnapshot(
            "card" + index, "person" + index, "검증선수" + index, 2025, PlayerPosition.Shortstop,
            7, PlayerCardEdition.Normal, 0, 10000, false, false);

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
                    return new[] { new OwnerCardTrainingProgramSnapshot("contact", "정교함 훈련", PlayerAbility.Contact,
                        70, 90, 1, 10, true, string.Empty) };
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

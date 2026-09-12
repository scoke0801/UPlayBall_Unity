using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Baseball.Core.Shop;
using Baseball.Core.Players;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>파견 방침의 검색·취소·단일 실행과 스카우트 화면의 해상도별 표시를 검증한다.</summary>
    public sealed class OwnerScoutUiTests
    {
        private GameObject _root;
        private GameObject _events;
        private UI_Scene_OwnerPowerUp _view;

        [TearDown]
        public void TearDown()
        {
            if (_view != null) UnityEngine.Object.DestroyImmediate(_view.gameObject);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_events != null)
            {
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_events.GetComponent<EventSystem>(), null);
                UnityEngine.Object.DestroyImmediate(_events);
            }
        }

        private void CreateView()
        {
            _root = new GameObject("ScoutCanvas", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            if (EventSystem.current == null)
            {
                _events = new GameObject("ScoutEvents", typeof(EventSystem));
                // EventSystem은 ExecuteAlways가 아니므로 EditMode에서는 생명주기를 명시적으로 시작한다.
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_events.GetComponent<EventSystem>(), null);
            }
            _view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            _view.Bind(Snapshot());
            _view.ShowRoute(OwnerNavigationRoutes.PowerUpScout);
        }

        private static OwnerPowerUpSnapshot Snapshot(bool canPurchase = true, string error = null)
        {
            var products = new OwnerScoutProductSnapshot[24];
            for (int index = 0; index < products.Length; index++)
                products[index] = new OwnerScoutProductSnapshot("scout" + index,
                    "선수 카드 · " + (index < 12 ? "전 연도 균형" : "2024년 정밀 탐색"),
                    index < 2 ? "전국" : "검증 구단 " + index / 2 + " 연고 지역",
                    index % 2 == 0 ? "SP 160" : "SP 1,600", canPurchase,
                    canPurchase ? "" : "스카우트 포인트가 부족합니다.", index % 2 == 0 ? 1 : 10,
                    480, 2400, 1, 8, new[] { new OwnerScoutProbabilitySnapshot("일반", .95),
                        new OwnerScoutProbabilitySnapshot("특별", .05) },
                    candidateSummaryResolver: () => new OwnerScoutCandidateSummary(20337, 12));
            var cards = new OwnerCardTrainingTargetSnapshot[10];
            for (int index = 0; index < cards.Length; index++)
                cards[index] = new OwnerCardTrainingTargetSnapshot(new OwnerCollectionCardSnapshot(
                    "card" + index, "person" + index, "김검증" + index, 2024, PlayerPosition.Shortstop,
                    8, PlayerCardEdition.Normal, 0, 10000, false, false), Array.Empty<OwnerCardTrainingProgramSnapshot>());
            return new OwnerPowerUpSnapshot(
                () => new OwnerScoutScreenSnapshot(products, "스카우트 포인트 9,990   ·   보유 카드 311장", error),
                () => new OwnerCardTrainingScreenSnapshot(cards, 100),
                () => new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 9990));
        }

        private T Find<T>(string name) where T : Component =>
            _root.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        [Test]
        public void 방침검색과선택은취소가능하며확정시에만한번파견한다()
        {
            CreateView();
            string original = Find<Text>("ScoutCost").text;
            Find<Button>("ScoutPolicy").onClick.Invoke();
            Assert.That(Find<Button>("ScoutPurchase").IsInteractable(), Is.False);
            Find<InputField>("PolicySearch").text = "2024";
            Assert.That(Find<Text>("PolicyPage").text, Does.Contain("12개"));
            Find<Button>("PolicyChoice1").onClick.Invoke();
            Assert.That(Find<Button>("PolicyChoice1").interactable, Is.True);
            Assert.That(Find<Text>("ScoutCost").text, Is.EqualTo(original));
            Assert.That(_view.TryHandleCancel(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Find<Button>("ScoutPolicy").gameObject));
            Assert.That(Find<Text>("ScoutCost").text, Is.EqualTo(original));
            Find<Button>("ScoutPolicy").onClick.Invoke();
            Find<Button>("PolicyChoice1").onClick.Invoke();
            Find<Button>("ConfirmPolicy").onClick.Invoke();
            Assert.That(Find<Text>("ScoutCost").text, Does.Contain("10명"));
            int calls = 0;
            _view.ScoutPurchaseRequested += id => { calls++; Assert.That(id, Is.EqualTo("scout1")); };
            Find<Button>("ScoutPurchase").onClick.Invoke();
            Assert.That(calls, Is.Zero);
            Assert.That(_view.TryHandleCancel(), Is.True);
            Assert.That(calls, Is.Zero);
            Find<Button>("ScoutPurchase").onClick.Invoke();
            Find<Button>("Confirm").onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void 네외형은같은파견조건을유지하고확률안내는배경입력을차단한다()
        {
            CreateView();
            string cost = Find<Text>("ScoutCost").text;
            var portraits = new System.Collections.Generic.HashSet<Texture>();
            for (int index = 0; index < 4; index++)
            {
                Texture portrait = Find<RawImage>("ScoutPortrait").texture;
                Assert.That(portrait, Is.Not.Null);
                portraits.Add(portrait);
                Assert.That(Find<RawImage>("PolicyScoutPortrait").texture, Is.SameAs(portrait));
                Find<Button>("NextScout").onClick.Invoke();
                Assert.That(Find<Text>("ScoutCost").text, Is.EqualTo(cost));
            }
            Assert.That(portraits.Count, Is.EqualTo(4));
            Find<Button>("ScoutInformationTab").onClick.Invoke();
            Assert.That(Find<Button>("ScoutPurchase").IsInteractable(), Is.False);
            Assert.That(_view.TryHandleCancel(), Is.True);
            Assert.That(Find<Button>("ScoutPurchase").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("ScoutInformationTab"));
        }

        [Test]
        public void 검색실패와재화부족과조회오류를구분한다()
        {
            CreateView();
            Find<Button>("ScoutPolicy").onClick.Invoke();
            Find<InputField>("PolicySearch").text = "없는지역";
            Assert.That(Find<Text>("NoPolicyResults").text, Does.Contain("검색 결과가 없습니다"));
            _view.TryHandleCancel();
            _view.Bind(Snapshot(false));
            Assert.That(Find<Button>("ScoutPurchase").interactable, Is.False);
            Assert.That(Find<Text>("ScoutSummary").text, Does.Contain("부족"));
            _view.Bind(Snapshot(error: "스카우트 정보를 불러오지 못했습니다."));
            Assert.That(Find<Text>("ScoutSummary").text, Does.Contain("불러오지 못했습니다"));
            Assert.That(Find<Image>("GaugeFill").rectTransform.rect.width, Is.Zero);
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 네해상도에서파견과방침과결과를출력한다(int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("그래픽 장치가 필요합니다.");
            CreateView();
            var cameraObject = new GameObject("ScoutCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(30, 44, 61, 255);
                camera.targetTexture = target;
                Canvas canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                Capture(camera, target, "scout", width, height);
                Find<Button>("ScoutPolicy").onClick.Invoke();
                Capture(camera, target, "policy", width, height);
                _view.TryHandleCancel();
                var items = new ShopGrantedItem[10];
                for (int index = 0; index < items.Length; index++)
                    items[index] = new ShopGrantedItem("card" + index, "김검증" + index, "일반 · 코스트 8", index % 2 == 0);
                _view.ShowScoutReveal(ShopPurchaseResult.Success(items));
                Assert.That(_root.GetComponentsInChildren<Text>().Count(t => t.name == "GrantedStatus"), Is.EqualTo(10));
                Capture(camera, target, "results", width, height);
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

        private void Capture(Camera camera, RenderTexture target, string state, int width, int height)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root.GetComponent<RectTransform>());
            typeof(UI_Scene_OwnerPowerUp).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_view, null);
            Canvas.ForceUpdateCanvases();
            RectTransform board = Find<RectTransform>("ScoutReference");
            var corners = new Vector3[4];
            board.GetWorldCorners(corners);
            float boardBottom = corners[0].y;
            Find<Text>("PowerUpFeedback").rectTransform.GetWorldCorners(corners);
            Assert.That(boardBottom, Is.GreaterThanOrEqualTo(corners[2].y - 1), "하단 피드백 영역 침범");
            foreach (Button button in board.GetComponentsInChildren<Button>())
            {
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = board.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(board.rect.xMin - 1, board.rect.xMax + 1), button.name);
                    Assert.That(point.y, Is.InRange(board.rect.yMin - 1, board.rect.yMax + 1), button.name);
                }
            }
            foreach (Text label in board.GetComponentsInChildren<Text>())
            {
                // 자동 맞춤 카드 글자의 preferredHeight는 실제 축소된 글리프 높이가 아니다.
                if (label.name == "ScoutDetails" || label.text.Length == 0 || label.resizeTextForBestFit) continue;
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2),
                    label.name + ": " + label.text);
            }
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string output = Path.GetFullPath("../screenshots");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, $"{state}-{width}.png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}

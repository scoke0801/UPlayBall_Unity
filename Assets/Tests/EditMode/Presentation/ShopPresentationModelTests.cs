using Baseball.Core.Shop;
using Baseball.Game.Shop;
using Baseball.Presentation.Shop;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation
{
    public sealed class ShopPresentationModelTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 유학초기화권은대상선택후결제하며다시열면선택을지운다(int width, int height)
        {
            var host = new GameObject("Host", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                var view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());
                var details = new ShopProductDetailsSnapshot("shop.study.reset", ShopProductKind.StudyReset,
                    "유학 초기화권", "선수 지정 · 구매 즉시 사용", "DP 200", "지정 선수 즉시 적용", "유학 초기화",
                    new string[0], "", "구매 제한 없음", true, "", ShopArtwork.SkillPackKey);
                view.BindTargets(new[] { new ShopTargetSnapshot("first", "박용택 · 2024 · 10코스트 · 유학 능력치 -3"),
                    new ShopTargetSnapshot("second", "김광현 · 2023 · 10코스트 · 유학 능력치 -6") });
                view.ShowPurchaseConfirmation(details);
                Button confirm = FindButton(host, "Confirm");
                Assert.That(confirm.interactable, Is.False);
                string purchasedTarget = null;
                view.PurchaseRequested += _ => purchasedTarget = view.SelectedTargetCardId;
                var dropdown = FindDropdown(host, "PurchaseTarget");
                dropdown.value = 2;
                Assert.That(confirm.interactable, Is.True);
                confirm.onClick.Invoke();
                Assert.That(purchasedTarget, Is.EqualTo("second"));
                view.SetProcessing(true);
                Assert.That(confirm.interactable, Is.False);
                Assert.That(dropdown.interactable, Is.False);
                view.SetProcessing(false);
                Canvas.ForceUpdateCanvases();
                CaptureStudyResetConfirmation(host, width, height);
                var panel = (RectTransform)FindTransform(host.transform, "PurchaseConfirmationPanel");
                foreach (var control in new[] { (RectTransform)dropdown.transform, (RectTransform)confirm.transform })
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, control);
                    Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(panel.rect.xMin - 1));
                    Assert.That(bounds.max.x, Is.LessThanOrEqualTo(panel.rect.xMax + 1));
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(panel.rect.yMin - 1));
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(panel.rect.yMax + 1));
                }
                view.DismissPurchaseConfirmation();
                view.ShowPurchaseConfirmation(details);
                Assert.That(view.SelectedTargetCardId, Is.Null);
                Assert.That(confirm.interactable, Is.False);
                view.BindTargets(new ShopTargetSnapshot[0]);
                Assert.That(dropdown.options[0].text, Does.Contain("초기화할 선수 없음"));
                Assert.That(confirm.interactable, Is.False);
                var plan = ShopRevealPlanBuilder.Build(ShopPurchaseResult.Success(new[] {
                    new ShopGrantedItem("second", "김광현 유학 초기화", "다시 유학 가능", false) }), details,
                    ShopRevealPresentationMode.Full);
                Assert.That(plan.StageTitle, Does.Contain("유학 초기화"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void CaptureStudyResetConfirmation(GameObject host, int width, int height)
        {
            string directory = System.Environment.GetEnvironmentVariable("BASEBALL_GROWTH_VISUAL_OUTPUT");
            if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var cameraObject = new GameObject("StudyResetCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.orthographic = true;
                camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)host.transform);
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, $"study-reset-{width}x{height}.png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Object.DestroyImmediate(texture);
                cameraObject.GetComponent<Camera>().targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(ShopProductBadge.New, "신규")]
        [TestCase(ShopProductBadge.Sale, "할인")]
        [TestCase(ShopProductBadge.Best, "")]
        public void 상품배지는한글로표시한다(ShopProductBadge badge, string expected)
        {
            Assert.That(ShopPresentationModel.DescribeBadge(badge), Is.EqualTo(expected));
        }

        [Test]
        public void 상세_Snapshot은_Game_Query의_확률과_현재_Quote를_그대로_표시한다()
        {
            var product = new ShopProductDefinition(
                "shop.skill.Normal",
                ShopProductKind.SkillBlockPack,
                "Normal",
                "스킬 블록",
                "스킬블록 전체",
                "일반",
                ShopCurrency.Money,
                1000L,
                maxPurchasesPerPeriod: 3);
            var details = new ShopProductDetails(
                product.ProductId,
                "실제 확률 설명",
                new[] { new ShopProbabilityEntry("일반", 0.625d, 4) },
                "실제 후보만 표시합니다.");
            var service = new ShopService(
                new ShopCatalog(new[] { product }),
                ShopAvailabilityTable.AllUnlocked(),
                new WalletStub(2000L),
                new IShopProductFulfillment[0],
                new ShopPurchaseHistoryState(),
                new[] { details });

            bool created = ShopPresentationModel.TryCreateDetails(
                service, product.ProductId, out ShopProductDetailsSnapshot snapshot);

            Assert.IsTrue(created);
            Assert.AreEqual("일반  62.50% · 후보 4장", snapshot.ProbabilityLines[0]);
            Assert.AreEqual("이번 주기 남은 구매 3회", snapshot.PurchaseLimitText);
            Assert.AreEqual(ShopArtwork.SkillPackKey, snapshot.ArtworkKey);
            Assert.IsTrue(snapshot.CanPurchase);
        }

        [Test]
        public void 희귀만_연출은_10개_결과에서_희귀_이상만_전체_연출한다()
        {
            var items = new ShopGrantedItem[10];
            for (int index = 0; index < items.Length; index++)
            {
                ShopRevealIntensity intensity = index == 7
                    ? ShopRevealIntensity.Rare
                    : ShopRevealIntensity.Standard;
                items[index] = new ShopGrantedItem(
                    "item." + index,
                    "결과 " + index,
                    index == 7 ? "특수" : "일반",
                    true,
                    primaryIntensity: intensity);
            }
            var details = new ShopProductDetailsSnapshot(
                "shop.tactic.general.x10",
                ShopProductKind.TacticCardPack,
                "작전 카드 10회",
                "전체",
                "₩ 35,000",
                "10회 묶음",
                "설명",
                new string[0],
                string.Empty,
                "구매 제한 없음",
                true,
                string.Empty,
                ShopArtwork.TacticLabRevealKey);

            ShopRevealPlan plan = ShopRevealPlanBuilder.Build(
                ShopPurchaseResult.Success(items),
                details,
                ShopRevealPresentationMode.HighlightsOnly);

            Assert.AreEqual(10, plan.Items.Length);
            Assert.IsFalse(plan.Items[0].UsesFullSequence);
            Assert.IsTrue(plan.Items[7].UsesFullSequence);
            Assert.AreEqual(ShopRevealTheme.TacticalLab, plan.Theme);
        }

        [Test]
        public void 상품타일을_누르면_선택상품이_바뀐다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            UI_Scene_Shop view = null;
            try
            {
                var first = new ShopProductTileSnapshot(
                    "shop.player.single", "선수 카드", "선수단 전체-일반", "SP 100",
                    string.Empty, "무작위", true, string.Empty);
                var second = new ShopProductTileSnapshot(
                    "shop.player.bundle", "선수 카드 10회", "선수단 전체-일반", "SP 1,000",
                    string.Empty, "10회 묶음", true, string.Empty);
                var tab = new ShopTabSnapshot(
                    ShopTab.Featured, "추천 상품", true, string.Empty, new[] { first, second });

                view = UI_Scene_Shop.CreateRuntime(hostObject.GetComponent<RectTransform>());
                view.Bind(new ShopScreenSnapshot(new[] { tab }, string.Empty));

                Button secondTile = FindButton(hostObject, "Product_shop.player.bundle");
                Assert.That(secondTile, Is.Not.Null);
                Assert.That(secondTile.targetGraphic.raycastTarget, Is.True,
                    "상품 타일 표면이 Raycast를 받지 못하면 실제 포인터 클릭이 Button까지 도달하지 않는다.");

                secondTile.onClick.Invoke();

                Transform previewTitle = hostObject.transform.Find(
                    "ShopWorkspace/ShopPanel/ContentSafeRect/Storefront/SelectedProduct/Title");
                Assert.That(previewTitle, Is.Not.Null);
                Assert.That(previewTitle.GetComponent<Text>().text, Is.EqualTo("선수 카드 10회"));
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 선수카드_구단과연도_필터로_다른_구단의_과거연도_정밀상품을_고른다()
        {
            var hostObject = new GameObject("Host", typeof(RectTransform));
            UI_Scene_Shop view = null;
            try
            {
                var general = new ShopProductTileSnapshot(
                    "general", "선수 카드", "전국", "SP 100", string.Empty,
                    "무작위", true, string.Empty);
                var team = new ShopProductTileSnapshot(
                    "team-b", "선수 카드", "부산 웨일즈", "SP 160", string.Empty,
                    "무작위", true, string.Empty, targetFranchiseId: "b",
                    targetFranchiseName: "부산 웨일즈");
                var year = new ShopProductTileSnapshot(
                    "year-2023", "선수 카드", "2023년", "SP 160", string.Empty,
                    "무작위", true, string.Empty, targetYear: 2023);
                var precise = new ShopProductTileSnapshot(
                    "team-b-2023", "선수 카드", "부산 웨일즈 · 2023년", "SP 240", string.Empty,
                    "무작위", true, string.Empty, targetFranchiseId: "b",
                    targetFranchiseName: "부산 웨일즈", targetYear: 2023);
                var tab = new ShopTabSnapshot(
                    ShopTab.PlayerCard, "선수 카드", true, string.Empty,
                    new[] { general, team, year, precise });

                view = UI_Scene_Shop.CreateRuntime(hostObject.GetComponent<RectTransform>());
                view.Bind(new ShopScreenSnapshot(new[] { tab }, string.Empty));

                Assert.That(FindButton(hostObject, "Product_team-b-2023"), Is.Null,
                    "정밀 상품은 구단과 연도를 모두 고르기 전에는 목록을 불필요하게 늘리지 않는다.");
                Dropdown franchiseFilter = FindDropdown(hostObject, "FranchiseFilter");
                Dropdown yearFilter = FindDropdown(hostObject, "YearFilter");
                franchiseFilter.value = 1;
                Transform grid = FindTransform(hostObject.transform, "ProductGrid");
                Assert.That(grid.GetChild(0).name, Is.EqualTo("Product_team-b"));
                yearFilter.value = 1;

                Assert.That(FindButton(hostObject, "Product_team-b-2023"), Is.Not.Null);
                Assert.That(FindButton(hostObject, "Product_team-b"), Is.Not.Null);
                Assert.That(FindButton(hostObject, "Product_year-2023"), Is.Not.Null);
                Transform previewSubtitle = hostObject.transform.Find(
                    "ShopWorkspace/ShopPanel/ContentSafeRect/Storefront/SelectedProduct/Subtitle");
                Assert.That(previewSubtitle.GetComponent<Text>().text,
                    Is.EqualTo("부산 웨일즈 · 2023년"));
                Assert.That(grid.GetChild(0).name, Is.EqualTo("Product_team-b-2023"));
                Assert.That(grid.GetChild(1).name, Is.EqualTo("Product_team-b"));
                Assert.That(grid.GetChild(2).name, Is.EqualTo("Product_year-2023"));
                Assert.That(grid.GetChild(3).name, Is.EqualTo("Product_general"));

                view.Bind(new ShopScreenSnapshot(new[] { tab }, string.Empty));
                Assert.That(FindDropdown(hostObject, "FranchiseFilter").value, Is.EqualTo(1));
                Assert.That(FindDropdown(hostObject, "YearFilter").value, Is.EqualTo(1));
                Assert.That(grid.GetChild(0).name, Is.EqualTo("Product_team-b-2023"));
                FindDropdown(hostObject, "FranchiseFilter").value = 0;
                Assert.That(grid.GetChild(0).name, Is.EqualTo("Product_year-2023"));
                FindDropdown(hostObject, "YearFilter").value = 0;
                Assert.That(grid.GetChild(0).name, Is.EqualTo("Product_general"));
                Assert.That(FindButton(hostObject, "Product_team-b-2023"), Is.Null);
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void 구매확인과결과화면이닫힐때까지가이드를보류한다()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            UI_Scene_Shop view = null;
            try
            {
                view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());
                var details = new ShopProductDetailsSnapshot(
                    "shop.skill.single", ShopProductKind.SkillBlockPack, "스킬 블록", "전체",
                    "1,000", "1회", "설명", new string[0], string.Empty, "제한 없음",
                    true, string.Empty, string.Empty);
                Assert.IsFalse(view.IsGuideSuppressed);
                view.ShowPurchaseConfirmation(details);
                Assert.IsTrue(view.IsGuideSuppressed);
                view.DismissPurchaseConfirmation();
                Assert.IsFalse(view.IsGuideSuppressed);
                view.SetProcessing(true);
                Assert.IsTrue(view.IsGuideSuppressed);
                view.SetProcessing(false);

                // 최소 연출도 최종 결과 화면을 닫기 전에는 보류가 풀리면 안 된다.
                FindButton(host, "RevealMode").onClick.Invoke();
                var result = ShopPurchaseResult.Success(new[]
                {
                    new ShopGrantedItem("block", "스킬 블록", "일반", true)
                });
                view.ShowReveal(result, details);
                Assert.IsTrue(view.IsGuideSuppressed);
                Assert.IsTrue(view.TryCloseOverlay());
                Assert.IsFalse(view.IsGuideSuppressed);

                view.ShowReveal(result, details);
                view.SetVisible(false);
                Assert.IsFalse(view.IsGuideSuppressed);
                view.SetVisible(true);
                Assert.IsFalse(view.IsGuideSuppressed);
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public void 선수결과는목표위치의카드로남고다음구매에서교체된다(int count)
        {
            var host = new GameObject("Host", typeof(RectTransform));
            UI_Scene_Shop view = null;
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());
                FindButton(host, "RevealMode").onClick.Invoke();
                var details = new ShopProductDetailsSnapshot(
                    "shop.player.bundle", ShopProductKind.PlayerCardPack, "선수 카드", "전체",
                    "1,000", "묶음", "설명", new string[0], string.Empty, "제한 없음",
                    true, string.Empty, string.Empty);
                var items = new ShopGrantedItem[count];
                var models = new PlayerMiniCardModel[count];
                for (int index = 0; index < count; index++)
                {
                    items[index] = new ShopGrantedItem("card_" + index, "결과 요약", "올스타", index != 1);
                    models[index] = new PlayerMiniCardModel(items[index].ItemId, "선수 " + index,
                        "선발투수", "2024", "Cost 7", "올스타", portraitAssetKey: "StartingPitcher");
                }
                view.ShowReveal(ShopPurchaseResult.Success(items), details, models);
                var cards = host.GetComponentsInChildren<PlayerMiniCardView>(false);
                Assert.AreEqual(count, cards.Length);
                string requestedCardId = null;
                view.PlayerCardDetailsRequested += cardId => requestedCardId = cardId;
                for (int index = 0; index < count; index++)
                {
                    Assert.AreEqual(models[index].DisplayName, cards[index].Model.DisplayName);
                    Assert.AreEqual("Cost 7", cards[index].Model.CostLabel);
                    Assert.AreEqual(index == 1 ? "중복 획득" : "신규 영입", cards[index].Model.StatusLabel);
                    Assert.AreEqual(1f, cards[index].GetComponent<CanvasGroup>().alpha);
                    Assert.AreEqual(Vector3.one, cards[index].transform.localScale);
                    RectTransform slot = (RectTransform)cards[index].transform.parent;
                    Assert.AreEqual(count > 5 ? (index < 5 ? .75f : .25f) : .5f, slot.anchorMin.y);
                    if (count == 1) Assert.AreEqual(.5f, slot.anchorMin.x);
                }
                Assert.IsTrue(cards[0].Model.IsInteractable);
                Assert.IsTrue(cards[0].GetComponent<CanvasGroup>().blocksRaycasts);
                cards[0].GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(models[0].PlayerId, requestedCardId);
                Assert.IsTrue(FindButton(host, "CloseReveal").gameObject.activeInHierarchy);
                Assert.IsFalse(FindButton(host, "Skip").gameObject.activeInHierarchy);
                Assert.IsFalse(System.Array.Exists(host.GetComponentsInChildren<RawImage>(false),
                    image => image.name == "RevealArtwork"));

                // 모델을 주지 않는 다음 호출에 이전 선수의 연도·Cost·이름이 섞이면 안 된다.
                view.ShowReveal(ShopPurchaseResult.Success(new[]
                {
                    new ShopGrantedItem("next", "다음 선수", "일반", true)
                }), details);
                cards = host.GetComponentsInChildren<PlayerMiniCardView>(false);
                Assert.AreEqual(1, cards.Length);
                Assert.AreEqual("다음 선수", cards[0].Model.DisplayName);
                Assert.AreEqual(string.Empty, cards[0].Model.CostLabel);
                Assert.AreEqual(new Vector2(.5f, .5f), ((RectTransform)cards[0].transform.parent).anchorMin);
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(ShopProductKind.SkillBlockPack, "SkillBlockCard", "", "획득 완료", 1)]
        [TestCase(ShopProductKind.SkillBlockPack, "SkillBlockCard", "", "획득 완료", 5)]
        [TestCase(ShopProductKind.SkillBlockPack, "SkillBlockCard", "", "획득 완료", 10)]
        [TestCase(ShopProductKind.TacticCardPack, "TacticCard", TacticCardArtwork.BattingKey, "중복 획득", 1)]
        [TestCase(ShopProductKind.TacticCardPack, "TacticCard", TacticCardArtwork.BattingKey, "중복 획득", 5)]
        [TestCase(ShopProductKind.TacticCardPack, "TacticCard", TacticCardArtwork.BattingKey, "중복 획득", 10)]
        public void 스킬블록과작전카드도선수카드와같은배치와Flip결과를유지한다(
            ShopProductKind kind,
            string faceName,
            string artworkKey,
            string expectedStatus,
            int count)
        {
            var host = new GameObject("Host", typeof(RectTransform));
            UI_Scene_Shop view = null;
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());
                FindButton(host, "RevealMode").onClick.Invoke();
                var details = new ShopProductDetailsSnapshot(
                    "shop.reveal.bundle", kind, "묶음 상품", "전체",
                    "1,000", "10회", "설명", new string[0], string.Empty, "제한 없음",
                    true, string.Empty, string.Empty);
                var items = new ShopGrantedItem[count];
                for (int index = 0; index < items.Length; index++)
                {
                    items[index] = new ShopGrantedItem(
                        "item_" + index, "결과 " + index, "희귀", isNew: false,
                        artworkKey: artworkKey);
                }

                view.ShowReveal(ShopPurchaseResult.Success(items), details);

                Transform grid = FindTransform(host.transform, "RevealCardGrid");
                Assert.That(grid, Is.Not.Null);
                for (int index = 0; index < items.Length; index++)
                {
                    Transform slot = grid.Find("RevealSlot_" + index);
                    Assert.That(slot, Is.Not.Null);
                    Transform face = slot.Find(faceName);
                    Assert.That(face, Is.Not.Null);
                    Assert.AreEqual(1f, face.GetComponent<CanvasGroup>().alpha);
                    Assert.AreEqual(0f, slot.Find("CardBack").GetComponent<CanvasGroup>().alpha);
                    float expectedY = count > 5 ? (index < 5 ? .75f : .25f) : .5f;
                    Assert.AreEqual(expectedY, ((RectTransform)slot).anchorMin.y);
                    if (count == 1) Assert.AreEqual(.5f, ((RectTransform)slot).anchorMin.x);
                    Assert.AreEqual(expectedStatus, face.Find("Status").GetComponent<Text>().text);
                    Assert.That(face.Find("CategoryBand").GetComponent<Image>().color.a, Is.GreaterThan(.9f));
                    Assert.That(face.Find("InformationBand").GetComponent<Image>().color.a, Is.GreaterThan(.9f));
                    Transform artworkFrame = face.Find("ArtworkFrame");
                    Assert.That(artworkFrame, Is.Not.Null);
                    Assert.That(artworkFrame.GetComponent<RectMask2D>(), Is.Not.Null);
                    Assert.That(artworkFrame.Find("CardArtwork"), Is.Not.Null,
                        "AspectRatioFitter는 카드 전체가 아니라 전용 프레임 안에서만 크기를 계산해야 한다.");
                }
                Assert.IsFalse(FindTransform(host.transform, "RevealArtwork").gameObject.activeInHierarchy);
                Assert.IsTrue(FindButton(host, "CloseReveal").gameObject.activeInHierarchy);
                Assert.IsFalse(FindButton(host, "Skip").gameObject.activeInHierarchy);
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(host);
            }
        }

        private static Button FindButton(GameObject root, string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name) return buttons[index];
            return null;
        }

        private static Dropdown FindDropdown(GameObject root, string name)
        {
            Dropdown[] dropdowns = root.GetComponentsInChildren<Dropdown>(true);
            for (int index = 0; index < dropdowns.Length; index++)
                if (dropdowns[index].name == name) return dropdowns[index];
            return null;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name) return transforms[index];
            return null;
        }

        private sealed class WalletStub : IShopWallet
        {
            private readonly long _money;

            public WalletStub(long money)
            {
                _money = money;
            }

            public ShopWalletBalance GetBalance() => new ShopWalletBalance(_money, 0, 0);

            public bool TrySpend(ShopCurrency currency, long amount) => false;
        }
    }
}

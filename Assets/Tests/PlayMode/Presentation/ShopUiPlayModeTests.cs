using System.Collections;
using Baseball.Core.Growth;
using Baseball.Core.Shop;
using Baseball.Presentation.Shop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseball.Tests.PlayMode.Presentation
{
    /// <summary>필터 변경 프레임에 이전 타일이 레이아웃과 스크롤에 남지 않는지 검증한다.</summary>
    public sealed class ShopUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator 필터를연속변경해도_새상품만배치되고_첫행이잘리지않는다()
        {
            var host = new GameObject("ShopTestCanvas", typeof(RectTransform), typeof(Canvas));
            UI_Scene_Shop view = null;
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());
                var tiles = new[]
                {
                    new ShopProductTileSnapshot("general", "선수 카드", "전체", "SP 100", "", "1회", true, ""),
                    new ShopProductTileSnapshot("team", "선수 카드", "구단", "SP 160", "", "1회", true, "",
                        targetFranchiseId: "team", targetFranchiseName: "서울 스타즈"),
                    new ShopProductTileSnapshot("year", "선수 카드", "연도", "SP 160", "", "1회", true, "",
                        targetYear: 2025),
                    new ShopProductTileSnapshot("precise", "선수 카드", "정밀", "SP 240", "", "1회", true, "",
                        targetFranchiseId: "team", targetFranchiseName: "서울 스타즈", targetYear: 2025)
                };
                var snapshot = new ShopScreenSnapshot(new[]
                {
                    new ShopTabSnapshot(ShopTab.PlayerCard, "선수 카드", true, "", tiles)
                }, "");
                view.Bind(snapshot);
                yield return null;

                var scroll = System.Array.Find(host.GetComponentsInChildren<ScrollRect>(),
                    candidate => candidate.name == "CatalogScroll");
                scroll.velocity = new Vector2(0f, 500f);
                foreach (Dropdown dropdown in host.GetComponentsInChildren<Dropdown>())
                    dropdown.value = 1;
                view.Bind(snapshot);

                Assert.AreEqual(Vector2.zero, scroll.velocity);
                int activeCount = 0;
                foreach (Transform child in scroll.content)
                    if (child.gameObject.activeSelf) activeCount++;
                Assert.AreEqual(4, activeCount, "Destroy 대기 중인 이전 상품은 즉시 배치에서 제외한다.");
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(4, scroll.content.childCount);
                Assert.AreEqual("Product_precise", scroll.content.GetChild(0).name);
                var corners = new Vector3[4];
                ((RectTransform)scroll.content.GetChild(0)).GetWorldCorners(corners);
                float tileTop = scroll.viewport.InverseTransformPoint(corners[1]).y;
                Assert.LessOrEqual(tileTop, scroll.viewport.rect.yMax + 1f);
                Assert.GreaterOrEqual(tileTop, scroll.viewport.rect.yMax - 8f);
            }
            finally
            {
                if (view != null) Object.Destroy(view.gameObject);
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator 스킬블록결과카드를다시누르면_실제블록형상으로트윈Flip한다()
        {
            var host = new GameObject("ShopRevealTestCanvas", typeof(RectTransform), typeof(Canvas));
            UI_Scene_Shop view = null;
            try
            {
                host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                view = UI_Scene_Shop.CreateRuntime(host.GetComponent<RectTransform>());

                var definition = new SkillBlockDefinition(
                    "block_t",
                    SkillBlockRarity.Rare,
                    SkillBlockCategory.Contact,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.T),
                    true,
                    System.Array.Empty<AbilityChange>(),
                    0L);
                var details = new ShopProductDetailsSnapshot(
                    "shop.skill.rare", ShopProductKind.SkillBlockPack, "스킬 블록", "희귀",
                    "1,000", "1회", "설명", System.Array.Empty<string>(), string.Empty, "제한 없음",
                    true, string.Empty, string.Empty);
                var result = ShopPurchaseResult.Success(new[]
                {
                    new ShopGrantedItem("block_t", "컨택 +2", "희귀", true)
                });

                view.ShowReveal(
                    result,
                    details,
                    skillBlocks: new[] { new ShopSkillBlockRevealModel(definition) });

                Transform slot = FindTransform(host.transform, "RevealSlot_0");
                CanvasGroup face = slot.Find("SkillBlockCard").GetComponent<CanvasGroup>();
                CanvasGroup back = slot.Find("CardBack").GetComponent<CanvasGroup>();
                Transform shapeBack = slot.Find("CardBack/SkillBlockShapeBack");
                Button cardButton = slot.GetComponent<Button>();
                Assert.That(shapeBack, Is.Not.Null);
                Assert.That(shapeBack.gameObject.activeSelf, Is.False, "공개 전에는 블록 형상을 노출하지 않습니다.");
                Assert.That(cardButton.interactable, Is.False);

                FindButton(host, "Skip").onClick.Invoke();
                yield return null;
                Assert.That(shapeBack.gameObject.activeSelf, Is.True);
                Assert.That(FindTransform(shapeBack, "RevealShapeSprite") ??
                            FindTransform(shapeBack, "RevealShapeFallback"), Is.Not.Null);
                Assert.That(cardButton.interactable, Is.True);

                cardButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.45f);
                Assert.AreEqual(0f, face.alpha);
                Assert.AreEqual(1f, back.alpha);

                cardButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.45f);
                Assert.AreEqual(1f, face.alpha);
                Assert.AreEqual(0f, back.alpha);
            }
            finally
            {
                if (view != null) Object.Destroy(view.gameObject);
                Object.Destroy(host);
            }
        }

        private static Button FindButton(GameObject root, string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
                if (buttons[index].name == name) return buttons[index];
            return null;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name) return transforms[index];
            return null;
        }
    }
}

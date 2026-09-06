using System;
using Baseball.Core.Shop;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core.Shop
{
    public sealed class ShopCatalogTests
    {
        [Test]
        public void 상품은_종류에_따라_고정된_탭에_들어간다()
        {
            Assert.AreEqual(ShopTab.PlayerCard, ShopProductDefinition.GetTab(ShopProductKind.PlayerCardPack));
            Assert.AreEqual(ShopTab.SkillBlock, ShopProductDefinition.GetTab(ShopProductKind.SkillBlockPack));
            Assert.AreEqual(ShopTab.TacticCard, ShopProductDefinition.GetTab(ShopProductKind.TacticCardPack));
        }

        [Test]
        public void 추천_표시된_상품은_추천탭과_원래탭에_모두_노출된다()
        {
            var catalog = new ShopCatalog(new[]
            {
                CreateProduct("a", ShopProductKind.SkillBlockPack, isFeatured: true),
                CreateProduct("b", ShopProductKind.SkillBlockPack)
            });

            Assert.AreEqual(1, catalog.GetProducts(ShopTab.Featured).Count);
            Assert.AreEqual("a", catalog.GetProducts(ShopTab.Featured)[0].ProductId);
            Assert.AreEqual(2, catalog.GetProducts(ShopTab.SkillBlock).Count);
        }

        [Test]
        public void 같은_ProductId는_카탈로그에_두_번_들어갈_수_없다()
        {
            Assert.Throws<ArgumentException>(() => new ShopCatalog(new[]
            {
                CreateProduct("a", ShopProductKind.SkillBlockPack),
                CreateProduct("a", ShopProductKind.TacticCardPack)
            }));
        }

        [Test]
        public void 표시_순서는_SortOrder가_같아도_결정적이다()
        {
            var catalog = new ShopCatalog(new[]
            {
                CreateProduct("c", ShopProductKind.SkillBlockPack, sortOrder: 5),
                CreateProduct("a", ShopProductKind.SkillBlockPack, sortOrder: 5),
                CreateProduct("b", ShopProductKind.SkillBlockPack, sortOrder: 1)
            });

            Assert.AreEqual("b", catalog.Products[0].ProductId);
            Assert.AreEqual("a", catalog.Products[1].ProductId);
            Assert.AreEqual("c", catalog.Products[2].ProductId);
        }

        [Test]
        public void 잠긴_탭의_상품은_추천탭에_있어도_구매할_수_없다()
        {
            ShopProductDefinition product = CreateProduct(
                "a", ShopProductKind.PlayerCardPack, isFeatured: true);
            var availability = new ShopAvailabilityTable(new[]
            {
                ShopCategoryAvailability.Unlocked(ShopTab.Featured),
                ShopCategoryAvailability.Locked(
                    ShopTab.PlayerCard, ShopLockReason.LockedByGameMode, "감독의 권한입니다.")
            });

            Assert.IsFalse(availability.IsPurchasable(product));
        }

        internal static ShopProductDefinition CreateProduct(
            string productId,
            ShopProductKind kind,
            long price = 100L,
            ShopCurrency currency = ShopCurrency.Money,
            bool isFeatured = false,
            int maxPurchasesPerPeriod = 0,
            int sortOrder = 0)
        {
            return new ShopProductDefinition(
                productId,
                kind,
                "source",
                "상품",
                "전체",
                "일반",
                currency,
                price,
                isFeatured: isFeatured,
                maxPurchasesPerPeriod: maxPurchasesPerPeriod,
                sortOrder: sortOrder);
        }
    }
}

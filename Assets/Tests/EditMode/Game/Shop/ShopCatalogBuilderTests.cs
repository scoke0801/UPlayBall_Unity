using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Shop;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Shop
{
    public sealed class ShopCatalogBuilderTests
    {
        [Test]
        public void 세_계열_상품이_모두_진열된다()
        {
            ShopCatalog catalog = BuildDefaultCatalog();

            Assert.IsNotEmpty(catalog.GetProducts(ShopTab.PlayerCard));
            Assert.IsNotEmpty(catalog.GetProducts(ShopTab.SkillBlock));
            Assert.IsNotEmpty(catalog.GetProducts(ShopTab.TacticCard));
            Assert.IsNotEmpty(catalog.GetProducts(ShopTab.Featured));
        }

        [Test]
        public void 스킬_블록_가격은_상점이_아니라_뽑기_밸런스에서_온다()
        {
            SkillGachaBalanceTable gacha = GrowthBalanceTable.CreateDefault().SkillGacha;
            ShopCatalog catalog = BuildDefaultCatalog();

            Assert.IsTrue(catalog.TryGetProduct("shop.skill.Normal", out ShopProductDefinition single));
            Assert.AreEqual(gacha.GetPrice(SkillGachaPurchaseTier.Normal), single.Price);
            Assert.AreEqual(ShopCurrency.Money, single.Currency);
        }

        [Test]
        public void 다섯_장_묶음은_단품_다섯_배보다_싸고_SALE_배지를_단다()
        {
            SkillGachaBalanceTable gacha = GrowthBalanceTable.CreateDefault().SkillGacha;
            ShopCatalog catalog = BuildDefaultCatalog();

            Assert.IsTrue(catalog.TryGetProduct("shop.skill.Normal.x5", out ShopProductDefinition bundle));
            Assert.AreEqual(5, bundle.DrawCount);
            Assert.AreEqual(ShopProductBadge.Sale, bundle.Badge);
            Assert.Less(bundle.Price, gacha.GetPrice(SkillGachaPurchaseTier.Normal) * 5L);
        }

        [Test]
        public void 선수_카드_상품은_스카우트_포인트로만_판다()
        {
            ShopCatalog catalog = BuildDefaultCatalog();
            IReadOnlyList<ShopProductDefinition> products = catalog.GetProducts(ShopTab.PlayerCard);

            for (int index = 0; index < products.Count; index++)
                Assert.AreEqual(ShopCurrency.ScoutingPoint, products[index].Currency);
        }

        [Test]
        public void 특수_Edition이_닫힌_단계에서는_수상_스카우트를_진열하지_않는다()
        {
            IReadOnlyList<ScoutPoolDefinition> pools =
                ShopDefaultPools.CreateScoutPools(ScoutFeaturePolicy.Phase4NormalOnly);

            Assert.AreEqual(1, pools.Count);
            Assert.AreEqual(ScoutType.General, pools[0].ScoutType);
        }

        private static ShopCatalog BuildDefaultCatalog()
        {
            return ShopCatalogBuilder.Build(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                ShopDefaultPools.CreateScoutPools(ScoutFeaturePolicy.FullWorldAwards),
                ShopDefaultPools.CreateTacticResearchPools());
        }
    }
}

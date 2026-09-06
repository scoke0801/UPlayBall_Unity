using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Shop;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
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
        public void 세_상품_계열은_모두_10회_묶음을_제공한다()
        {
            SkillGachaBalanceTable gacha = GrowthBalanceTable.CreateDefault().SkillGacha;
            ShopCatalog catalog = BuildDefaultCatalog();

            Assert.IsTrue(catalog.TryGetProduct("shop.player.general.x10", out ShopProductDefinition player));
            Assert.IsTrue(catalog.TryGetProduct("shop.skill.Normal.x10", out ShopProductDefinition skill));
            Assert.IsTrue(catalog.TryGetProduct("shop.tactic.general.x10", out ShopProductDefinition tactic));
            Assert.AreEqual(10, player.DrawCount);
            Assert.AreEqual(10, skill.DrawCount);
            Assert.AreEqual(10, tactic.DrawCount);
            Assert.AreEqual(ShopDefaultPools.GeneralScoutPriceSp * 10L, player.Price);
            Assert.AreEqual(gacha.GetFivePullPrice(SkillGachaPurchaseTier.Normal) * 2L, skill.Price);
            Assert.AreEqual(ShopDefaultPools.GeneralTacticResearchPrice * 10L, tactic.Price);
        }

        [Test]
        public void 특수_Edition이_닫힌_단계에서는_수상_스카우트를_진열하지_않는다()
        {
            IReadOnlyList<ScoutPoolDefinition> pools =
                ShopDefaultPools.CreateScoutPools(ScoutFeaturePolicy.Phase4NormalOnly);

            Assert.AreEqual(1, pools.Count);
            Assert.AreEqual(ScoutType.General, pools[0].ScoutType);
        }

        [Test]
        public void 현재_구단과_연도가_있으면_네_파견범위와_문서가격을_진열한다()
        {
            IReadOnlyList<ScoutPoolDefinition> pools = ShopDefaultPools.CreateScoutPools(
                ScoutFeaturePolicy.Phase4NormalOnly,
                "franchise-a",
                2025);
            ShopCatalog catalog = ShopCatalogBuilder.Build(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                pools,
                ShopDefaultPools.CreateTacticResearchPools(),
                _ => "가상 서울 구단");

            Assert.AreEqual(4, pools.Count);
            Assert.IsTrue(catalog.TryGetProduct("shop.player.franchise_home", out ShopProductDefinition franchise));
            Assert.IsTrue(catalog.TryGetProduct("shop.player.year_current", out ShopProductDefinition year));
            Assert.IsTrue(catalog.TryGetProduct("shop.player.year_franchise_current", out ShopProductDefinition precise));
            Assert.AreEqual(160L, franchise.Price);
            Assert.AreEqual(160L, year.Price);
            Assert.AreEqual(240L, precise.Price);
            Assert.That(franchise.ScopeLabel, Does.Contain("가상 서울 구단"));
            Assert.That(year.ScopeLabel, Does.Contain("2025년"));
            Assert.That(precise.GradeLabel, Is.EqualTo("구단·연도 정밀"));
            Assert.That(franchise.ScopeLabel, Does.Not.Contain("franchise-a"));
        }

        [Test]
        public void 월드의_모든_구단과_연도를_스카우트_대상으로_진열한다()
        {
            IReadOnlyList<ScoutPoolDefinition> pools = ShopDefaultPools.CreateScoutPools(
                ScoutFeaturePolicy.Phase4NormalOnly,
                new[]
                {
                    new ScoutMarketTarget("franchise-a", 2024),
                    new ScoutMarketTarget("franchise-b", 2024),
                    new ScoutMarketTarget("franchise-b", 2023),
                    new ScoutMarketTarget("franchise-b", 2023)
                });
            ShopCatalog catalog = ShopCatalogBuilder.Build(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                pools,
                ShopDefaultPools.CreateTacticResearchPools(),
                id => id == "franchise-a" ? "서울 스타즈" : "부산 웨일즈");

            Assert.That(pools.Count, Is.EqualTo(8));
            Assert.IsTrue(catalog.TryGetProduct(
                "shop.player.franchise_franchise-b", out ShopProductDefinition otherTeam));
            Assert.IsTrue(catalog.TryGetProduct(
                "shop.player.year_2023", out ShopProductDefinition otherYear));
            Assert.IsTrue(catalog.TryGetProduct(
                "shop.player.year_franchise_2023_franchise-b", out ShopProductDefinition precise));
            Assert.That(otherTeam.TargetFranchiseName, Is.EqualTo("부산 웨일즈"));
            Assert.That(otherYear.TargetYear, Is.EqualTo(2023));
            Assert.That(precise.TargetFranchiseId, Is.EqualTo("franchise-b"));
            Assert.That(precise.TargetYear, Is.EqualTo(2023));
        }

        [Test]
        public void 스킬블록_구매결과는_내부ID가_아닌_한글_능력치와_보너스를_표시한다()
        {
            GrowthBalanceTable growth = GrowthBalanceTable.CreateDefault();
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(100_000L));
            var board = new SkillBoardState(growth.SkillBoard.BoardDefinitionId);
            var fulfillment = new SkillBlockPackFulfillment(
                new SkillGachaService(growth.SkillGacha, growth.SkillBlocks),
                growth.SkillBlocks,
                () => economy,
                () => board,
                () => new[] { SkillBlockCategory.Contact },
                () => 2026,
                () => new Pcg32Random(1234UL));
            var product = new ShopProductDefinition(
                "shop.skill.test",
                ShopProductKind.SkillBlockPack,
                SkillGachaPurchaseTier.Normal.ToString(),
                "스킬 블록",
                "교타 계통",
                "일반",
                ShopCurrency.Money,
                growth.SkillGacha.GetPrice(SkillGachaPurchaseTier.Normal));

            ShopFulfillmentResult result = fulfillment.Fulfill(product);

            Assert.That(result.IsSuccess, Is.True, result.FailureMessage);
            Assert.That(result.Items[0].DisplayName, Does.StartWith("교타력 +"));
            Assert.That(result.Items[0].DisplayName, Does.Not.Contain("contact"));
            Assert.That(result.Items[0].DisplayName, Does.Not.Contain("_"));
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

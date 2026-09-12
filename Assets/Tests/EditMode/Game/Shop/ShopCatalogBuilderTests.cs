using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
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
        public void 선택상품상세는_다른구단연도를제외한_실제후보확률을_반환한다()
        {
            var ratings = new AbilityRatings(50);
            var seasons = new PlayerSeasonDefinition[2];
            var cards = new PlayerCardDefinition[2];
            var targets = new ScoutMarketTarget[2];
            for (int index = 0; index < 2; index++)
            {
                string id = "season" + index;
                string franchise = "franchise" + index;
                int year = 2023 + index;
                seasons[index] = new PlayerSeasonDefinition(id, "person" + index, year,
                    franchise, franchise + year, PlayerPosition.Catcher, PitcherRole.MiddleRelief,
                    PlayerType.Batter, RegistrationType.Domestic, ratings, 3 + index * 5, ratings);
                cards[index] = new PlayerCardDefinition(
                    PlayerCardDefinition.CreateStableCardId(id, PlayerCardEdition.Normal),
                    id, PlayerCardEdition.Normal, new int[PlayerAbilityCatalog.AbilityCount]);
                targets[index] = new ScoutMarketTarget(franchise, year);
            }
            var world = new WorldCardCatalog(seasons, cards);
            var policy = ScoutFeaturePolicy.Phase4NormalOnly;
            var pools = ShopDefaultPools.CreateScoutPools(policy, targets);
            var gacha = GrowthBalanceTable.CreateDefault().SkillGacha;
            var tacticPools = new TacticResearchPoolDefinition[0];
            var catalog = ShopCatalogBuilder.Build(gacha, pools, tacticPools);
            var resolve = OwnerShopDetailsBuilder.CreateResolver(gacha, pools, policy, world,
                tacticPools, new TacticCardDefinition[0], ScoutPityBalanceTable.CreateInitial());
            int checkedProducts = 0;
            foreach (ShopProductDefinition product in catalog.Products)
            {
                if (product.TargetFranchiseId != "franchise1" || product.TargetYear != 2024) continue;
                ShopProductDetails details = resolve(product);
                Assert.AreEqual(product.ProductId, details.ProductId);
                Assert.AreEqual(1, details.Probabilities.Count);
                Assert.AreEqual("Cost 8 · 일반", details.Probabilities[0].Label);
                Assert.AreEqual(1d, details.Probabilities[0].Probability);
                Assert.AreEqual(1, details.Probabilities[0].CandidateCount);
                checkedProducts++;
            }
            Assert.AreEqual(2, checkedProducts, "단품과 10회 묶음 모두 같은 실제 후보를 사용한다.");
        }

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
        public void 연도구단상품은해당시즌Identity를구단명으로사용한다()
        {
            IReadOnlyList<ScoutPoolDefinition> pools = ShopDefaultPools.CreateScoutPools(
                ScoutFeaturePolicy.Phase4NormalOnly,
                new[] { new ScoutMarketTarget("franchise-a", 2003) });
            ShopCatalog catalog = ShopCatalogBuilder.Build(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                pools,
                ShopDefaultPools.CreateTacticResearchPools(),
                _ => "현재 구단",
                franchiseYearDisplayNameResolver: (_, year) => year.HasValue
                    ? year.Value + " 역사 구단"
                    : "현재 구단");

            Assert.That(catalog.TryGetProduct(
                "shop.player.year_franchise_2003_franchise-a",
                out ShopProductDefinition precise), Is.True);
            Assert.That(precise.TargetFranchiseName, Is.EqualTo("2003 역사 구단"));
            Assert.That(precise.ScopeLabel, Is.EqualTo("2003 역사 구단"));
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

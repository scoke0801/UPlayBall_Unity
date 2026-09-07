using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Career;
using Baseball.Game.Shop;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Shop
{
    public sealed class ShopServiceTests
    {
        [Test]
        public void 상점생성과구매는_상세확률을_미리계산하지않고_조회한상품만_재사용한다()
        {
            var product = CreateSkillProduct();
            var wallet = new ShopWalletStub(1000L);
            int calls = 0;
            var service = new ShopService(
                new ShopCatalog(new[] { product, CreatePlayerCardProduct() }),
                ShopAvailabilityTable.AllUnlocked(), wallet,
                new IShopProductFulfillment[] { new StubFulfillment(ShopProductKind.SkillBlockPack) },
                new ShopPurchaseHistoryState(),
                detailsResolver: requested =>
                {
                    calls++;
                    return new ShopProductDetails(requested.ProductId, "설명",
                        new[] { new ShopProbabilityEntry("일반", 1d, 1) }, string.Empty);
                });

            Assert.IsTrue(service.Purchase(product.ProductId).IsSuccess);
            Assert.AreEqual(0, calls);
            Assert.IsFalse(service.TryGetDetails("없는상품", out _));
            Assert.AreEqual(0, calls);
            Assert.IsTrue(service.TryGetDetails(product.ProductId, out var first));
            Assert.IsTrue(service.TryGetDetails(product.ProductId, out var second));
            Assert.AreSame(first, second);
            Assert.AreEqual(1, calls, "선택하지 않은 상품의 확률까지 계산하면 안 된다.");

            Assert.IsTrue(wallet.TrySpend(ShopCurrency.Money, 1000L));
            Assert.IsFalse(service.GetQuote(product).CanPurchase,
                "확률 캐시와 달리 구매 가능 여부는 최신 재화를 읽어야 한다.");
        }

        [Test]
        public void 다른상품의상세를반환하는조회함수는_거부한다()
        {
            var service = new ShopService(
                new ShopCatalog(new[] { CreateSkillProduct() }),
                ShopAvailabilityTable.AllUnlocked(), new ShopWalletStub(1000L),
                new IShopProductFulfillment[0], new ShopPurchaseHistoryState(),
                detailsResolver: product => new ShopProductDetails("wrong", "설명",
                    new ShopProbabilityEntry[0], string.Empty));
            Assert.Throws<InvalidOperationException>(() => service.TryGetDetails("skill", out _));
        }

        [Test]
        public void 구매에_성공하면_지급_결과와_구매_횟수가_남는다()
        {
            var fulfillment = new StubFulfillment(ShopProductKind.SkillBlockPack);
            ShopService service = CreateService(
                new ShopWalletStub(1000L), ShopAvailabilityTable.AllUnlocked(), fulfillment);

            ShopPurchaseResult result = service.Purchase("skill");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Items.Length);
            Assert.AreEqual(1, service.History.GetPurchaseCount("skill"));
            Assert.AreEqual(1, fulfillment.CallCount);
        }

        [Test]
        public void 잔액이_부족하면_지급을_시도조차_하지_않는다()
        {
            var fulfillment = new StubFulfillment(ShopProductKind.SkillBlockPack);
            ShopService service = CreateService(
                new ShopWalletStub(10L), ShopAvailabilityTable.AllUnlocked(), fulfillment);

            ShopPurchaseResult result = service.Purchase("skill");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(ShopPurchaseFailureReason.InsufficientFunds, result.FailureReason);
            Assert.AreEqual(0, fulfillment.CallCount);
            Assert.AreEqual(0, service.History.GetPurchaseCount("skill"));
        }

        [Test]
        public void 지급이_실패하면_구매_횟수가_늘지_않는다()
        {
            var fulfillment = new StubFulfillment(ShopProductKind.SkillBlockPack) { ShouldFail = true };
            ShopService service = CreateService(
                new ShopWalletStub(1000L), ShopAvailabilityTable.AllUnlocked(), fulfillment);

            ShopPurchaseResult result = service.Purchase("skill");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(0, service.History.GetPurchaseCount("skill"));
        }

        [Test]
        public void 없는_상품을_사려_하면_UnknownProduct로_거절한다()
        {
            ShopService service = CreateService(
                new ShopWalletStub(1000L),
                ShopAvailabilityTable.AllUnlocked(),
                new StubFulfillment(ShopProductKind.SkillBlockPack));

            ShopPurchaseResult result = service.Purchase("없는상품");

            Assert.AreEqual(ShopPurchaseFailureReason.UnknownProduct, result.FailureReason);
        }

        [Test]
        public void 선수_모드는_스킬_블록만_열리고_나머지는_사유와_함께_잠긴다()
        {
            ShopAvailabilityTable table = ShopAvailabilityFactory.CreateFor(GameMode.PlayerCareer);

            Assert.IsTrue(table.IsUnlocked(ShopTab.SkillBlock));
            Assert.IsTrue(table.IsUnlocked(ShopTab.Featured));
            Assert.IsFalse(table.IsUnlocked(ShopTab.PlayerCard));
            Assert.IsFalse(table.IsUnlocked(ShopTab.TacticCard));
            Assert.IsNotEmpty(table.Get(ShopTab.PlayerCard).LockDescription);
            Assert.AreEqual(ShopLockReason.LockedByGameMode, table.Get(ShopTab.TacticCard).LockReason);
        }

        [Test]
        public void 구단주_모드는_선수카드와_작전카드를_연다()
        {
            ShopAvailabilityTable table = ShopAvailabilityFactory.CreateFor(GameMode.OwnerCareer);

            Assert.IsTrue(table.IsUnlocked(ShopTab.PlayerCard));
            Assert.IsTrue(table.IsUnlocked(ShopTab.TacticCard));
        }

        [Test]
        public void 구단주_모드_스킬블록은_카드훈련의_스킬보드용으로_열린다()
        {
            ShopAvailabilityTable table = ShopAvailabilityFactory.CreateFor(GameMode.OwnerCareer);

            Assert.IsTrue(table.IsUnlocked(ShopTab.SkillBlock));
            Assert.IsEmpty(table.Get(ShopTab.SkillBlock).LockDescription);
        }

        [Test]
        public void 누적_구매_번호는_주기가_초기화돼도_이어진다()
        {
            var history = new ShopPurchaseHistoryState();
            history.RecordPurchase("a");
            history.RecordPurchase("b");
            history.ResetPeriod();
            history.RecordPurchase("a");

            // 주기마다 0으로 돌아가면 같은 Seed가 반복돼 오프시즌마다 같은 카드가 나온다.
            Assert.AreEqual(3, history.TotalPurchaseCount);
            Assert.AreEqual(1, history.GetPurchaseCount("a"));
        }

        [Test]
        public void 선수_모드에서는_선수카드_상품_구매가_잠금_사유로_막힌다()
        {
            var fulfillment = new StubFulfillment(ShopProductKind.PlayerCardPack);
            var catalog = new ShopCatalog(new[] { CreatePlayerCardProduct() });
            var service = new ShopService(
                catalog,
                ShopAvailabilityFactory.CreateFor(GameMode.PlayerCareer),
                new ShopWalletStub(1000L, 1000),
                new IShopProductFulfillment[] { fulfillment },
                new ShopPurchaseHistoryState());

            ShopPurchaseResult result = service.Purchase("player");

            Assert.AreEqual(ShopPurchaseFailureReason.CategoryLocked, result.FailureReason);
            Assert.AreEqual(0, fulfillment.CallCount);
        }

        [Test]
        public void 전술_연구_풀은_Signature를_담을_수_없는_구조다()
        {
            var pool = new TacticResearchPoolDefinition(
                "general", TacticResearchPoolDefinition.CreateInitialTierWeights(), 3_500L);

            Assert.AreEqual(0d, pool.GetTierWeight(TacticTier.Signature));
            Assert.Throws<ArgumentException>(() => new TacticResearchPoolDefinition(
                "broken", new[] { 1d, 1d, 1d, 1d }, 100L));
        }

        [Test]
        public void 상품_상세는_카탈로그_ProductId로만_조회된다()
        {
            ShopProductDefinition product = CreateSkillProduct();
            var details = new ShopProductDetails(
                product.ProductId,
                "실제 확률 설명",
                new[] { new ShopProbabilityEntry("일반", 1d, 3) },
                "테스트 공지");
            var service = new ShopService(
                new ShopCatalog(new[] { product }),
                ShopAvailabilityTable.AllUnlocked(),
                new ShopWalletStub(1000L),
                new IShopProductFulfillment[] { new StubFulfillment(ShopProductKind.SkillBlockPack) },
                new ShopPurchaseHistoryState(),
                new[] { details });

            Assert.IsTrue(service.TryGetDetails(product.ProductId, out ShopProductDetails found));
            Assert.AreSame(details, found);
            Assert.IsFalse(service.TryGetDetails("없는상품", out _));
        }

        private static ShopService CreateService(
            IShopWallet wallet,
            ShopAvailabilityTable availability,
            IShopProductFulfillment fulfillment)
        {
            var catalog = new ShopCatalog(new[] { CreateSkillProduct() });
            return new ShopService(
                catalog,
                availability,
                wallet,
                new[] { fulfillment },
                new ShopPurchaseHistoryState());
        }

        private static ShopProductDefinition CreateSkillProduct()
        {
            return new ShopProductDefinition(
                "skill", ShopProductKind.SkillBlockPack, "Normal",
                "스킬 블록", "스킬블록 전체", "일반", ShopCurrency.Money, 100L);
        }

        private static ShopProductDefinition CreatePlayerCardProduct()
        {
            return new ShopProductDefinition(
                "player", ShopProductKind.PlayerCardPack, "general",
                "선수 카드", "선수단 전체", "일반", ShopCurrency.ScoutingPoint, 100L);
        }

        private sealed class ShopWalletStub : IShopWallet
        {
            private long _money;
            private int _scoutingPoints;

            public ShopWalletStub(long money, int scoutingPoints = 0)
            {
                _money = money;
                _scoutingPoints = scoutingPoints;
            }

            public ShopWalletBalance GetBalance() => new ShopWalletBalance(_money, _scoutingPoints, 0);

            public bool TrySpend(ShopCurrency currency, long amount)
            {
                if (currency == ShopCurrency.Money && _money >= amount)
                {
                    _money -= amount;
                    return true;
                }
                if (currency == ShopCurrency.ScoutingPoint && _scoutingPoints >= amount)
                {
                    _scoutingPoints -= (int)amount;
                    return true;
                }
                return false;
            }
        }

        private sealed class StubFulfillment : IShopProductFulfillment
        {
            public StubFulfillment(ShopProductKind kind)
            {
                Kind = kind;
            }

            public ShopProductKind Kind { get; }
            public int CallCount { get; private set; }
            public bool ShouldFail { get; set; }

            public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
            {
                CallCount++;
                if (ShouldFail)
                    return ShopFulfillmentResult.Failure("지급에 실패했습니다.");
                return ShopFulfillmentResult.Success(new[]
                {
                    new ShopGrantedItem("item", "테스트 항목", "일반", true)
                });
            }
        }
    }
}

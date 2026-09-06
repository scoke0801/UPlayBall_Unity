using Baseball.Core.Shop;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core.Shop
{
    public sealed class ShopPurchaseRulesTests
    {
        [Test]
        public void 잔액이_충분하고_열린_탭이면_구매할_수_있다()
        {
            ShopProductDefinition product = ShopCatalogTests.CreateProduct(
                "a", ShopProductKind.SkillBlockPack, price: 1000L);

            ShopPurchaseQuote quote = ShopPurchaseRules.Evaluate(
                product, ShopAvailabilityTable.AllUnlocked(), new ShopWalletBalance(1000L, 0, 0), 0);

            Assert.IsTrue(quote.CanPurchase);
            Assert.AreEqual(0L, quote.Shortfall);
            Assert.AreEqual(ShopPurchaseQuote.UnlimitedPurchases, quote.RemainingPurchases);
        }

        [Test]
        public void 모자란_금액이_부족분으로_보고된다()
        {
            ShopProductDefinition product = ShopCatalogTests.CreateProduct(
                "a", ShopProductKind.SkillBlockPack, price: 1000L);

            ShopPurchaseQuote quote = ShopPurchaseRules.Evaluate(
                product, ShopAvailabilityTable.AllUnlocked(), new ShopWalletBalance(400L, 0, 0), 0);

            Assert.AreEqual(ShopPurchaseFailureReason.InsufficientFunds, quote.FailureReason);
            Assert.AreEqual(600L, quote.Shortfall);
        }

        [Test]
        public void 스카우트_포인트_상품은_자금이_많아도_SP로만_판정한다()
        {
            ShopProductDefinition product = ShopCatalogTests.CreateProduct(
                "a", ShopProductKind.PlayerCardPack, price: 50L, currency: ShopCurrency.ScoutingPoint);

            ShopPurchaseQuote rich = ShopPurchaseRules.Evaluate(
                product, ShopAvailabilityTable.AllUnlocked(), new ShopWalletBalance(999_999L, 10, 0), 0);
            ShopPurchaseQuote enough = ShopPurchaseRules.Evaluate(
                product, ShopAvailabilityTable.AllUnlocked(), new ShopWalletBalance(0L, 50, 0), 0);

            Assert.AreEqual(ShopPurchaseFailureReason.InsufficientFunds, rich.FailureReason);
            Assert.IsTrue(enough.CanPurchase);
        }

        [Test]
        public void 구매_한도에_도달하면_잔액이_충분해도_막힌다()
        {
            ShopProductDefinition product = ShopCatalogTests.CreateProduct(
                "a", ShopProductKind.SkillBlockPack, price: 10L, maxPurchasesPerPeriod: 2);

            ShopPurchaseQuote quote = ShopPurchaseRules.Evaluate(
                product, ShopAvailabilityTable.AllUnlocked(), new ShopWalletBalance(1000L, 0, 0), 2);

            Assert.AreEqual(ShopPurchaseFailureReason.PurchaseLimitReached, quote.FailureReason);
            Assert.AreEqual(0, quote.RemainingPurchases);
        }

        [Test]
        public void 주기가_바뀌면_구매_한도가_다시_열린다()
        {
            var history = new ShopPurchaseHistoryState();
            history.RecordPurchase("a");
            history.RecordPurchase("a");
            Assert.AreEqual(2, history.GetPurchaseCount("a"));

            history.ResetPeriod();

            Assert.AreEqual(0, history.GetPurchaseCount("a"));
        }

        [Test]
        public void 잠긴_탭은_잔액과_무관하게_잠금_사유로_막힌다()
        {
            ShopProductDefinition product = ShopCatalogTests.CreateProduct(
                "a", ShopProductKind.TacticCardPack, price: 10L);
            var availability = new ShopAvailabilityTable(new[]
            {
                ShopCategoryAvailability.Locked(
                    ShopTab.TacticCard, ShopLockReason.LockedByGameMode, "감독의 권한입니다.")
            });

            ShopPurchaseQuote quote = ShopPurchaseRules.Evaluate(
                product, availability, new ShopWalletBalance(1_000_000L, 0, 0), 0);

            Assert.AreEqual(ShopPurchaseFailureReason.CategoryLocked, quote.FailureReason);
        }
    }
}

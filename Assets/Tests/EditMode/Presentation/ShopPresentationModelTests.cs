using Baseball.Core.Shop;
using Baseball.Game.Shop;
using Baseball.Presentation.Shop;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    public sealed class ShopPresentationModelTests
    {
        [TestCase(ShopProductBadge.New, "신규")]
        [TestCase(ShopProductBadge.Sale, "할인")]
        [TestCase(ShopProductBadge.Best, "인기")]
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

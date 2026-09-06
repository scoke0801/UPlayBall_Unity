using System;
using System.Collections.Generic;

namespace Baseball.Core.Shop
{
    /// <summary>구매 판정 시점의 재화 잔액 스냅샷이다. 상태를 바꾸지 않는 순수 값이다.</summary>
    public readonly struct ShopWalletBalance
    {
        public ShopWalletBalance(long money, int scoutingPoints, int developmentPoints)
        {
            if (money < 0L)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (scoutingPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPoints));
            if (developmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(developmentPoints));
            Money = money;
            ScoutingPoints = scoutingPoints;
            DevelopmentPoints = developmentPoints;
        }

        public long Money { get; }
        public int ScoutingPoints { get; }
        public int DevelopmentPoints { get; }

        public long Get(ShopCurrency currency)
        {
            switch (currency)
            {
                case ShopCurrency.Money: return Money;
                case ShopCurrency.ScoutingPoint: return ScoutingPoints;
                case ShopCurrency.DevelopmentPoint: return DevelopmentPoints;
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }
    }

    public enum ShopPurchaseFailureReason
    {
        None,
        UnknownProduct,
        CategoryLocked,
        InsufficientFunds,
        PurchaseLimitReached
    }

    /// <summary>
    /// 구매 버튼을 누르기 전에 미리 계산된 구매 가능 여부다.
    /// UI는 이 값을 표시만 하고 가능 여부를 다시 계산하지 않는다.
    /// </summary>
    public readonly struct ShopPurchaseQuote
    {
        public const int UnlimitedPurchases = -1;

        private ShopPurchaseQuote(
            ShopProductDefinition product,
            ShopPurchaseFailureReason failureReason,
            long shortfall,
            int remainingPurchases)
        {
            Product = product;
            FailureReason = failureReason;
            Shortfall = shortfall;
            RemainingPurchases = remainingPurchases;
        }

        public ShopProductDefinition Product { get; }
        public ShopPurchaseFailureReason FailureReason { get; }

        /// <summary>재화가 모자란 양이다. 부족하지 않으면 0이다.</summary>
        public long Shortfall { get; }

        /// <summary>남은 구매 횟수다. 무제한이면 <see cref="UnlimitedPurchases"/>다.</summary>
        public int RemainingPurchases { get; }

        public bool CanPurchase => FailureReason == ShopPurchaseFailureReason.None;

        internal static ShopPurchaseQuote Allowed(ShopProductDefinition product, int remainingPurchases)
        {
            return new ShopPurchaseQuote(product, ShopPurchaseFailureReason.None, 0L, remainingPurchases);
        }

        internal static ShopPurchaseQuote Rejected(
            ShopProductDefinition product,
            ShopPurchaseFailureReason reason,
            long shortfall,
            int remainingPurchases)
        {
            return new ShopPurchaseQuote(product, reason, shortfall, remainingPurchases);
        }
    }

    /// <summary>주기(시즌·오프시즌)당 상품별 구매 횟수를 누적한다. 세이브 대상 런타임 상태다.</summary>
    public sealed class ShopPurchaseHistoryState
    {
        private readonly Dictionary<string, int> _purchaseCounts;

        public ShopPurchaseHistoryState()
        {
            _purchaseCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, int> PurchaseCounts => _purchaseCounts;

        /// <summary>
        /// 커리어 전체 누적 구매 횟수다. 구매마다 다른 결정론 Seed를 뽑기 위한 스트림 번호로 쓰이므로
        /// <see cref="ResetPeriod"/>가 비우지 않는다. 비우면 주기가 바뀔 때마다 같은 결과가 반복된다.
        /// </summary>
        public int TotalPurchaseCount { get; private set; }

        public int GetPurchaseCount(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return 0;
            return _purchaseCounts.TryGetValue(productId, out int count) ? count : 0;
        }

        public void RecordPurchase(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId는 비어 있을 수 없습니다.", nameof(productId));
            _purchaseCounts.TryGetValue(productId, out int count);
            _purchaseCounts[productId] = count + 1;
            TotalPurchaseCount++;
        }

        /// <summary>구매 한도가 주기 단위이므로 주기가 바뀌면 호출해 누적을 비운다.</summary>
        public void ResetPeriod()
        {
            _purchaseCounts.Clear();
        }
    }

    /// <summary>구매 가능 여부를 결정하는 유일한 판정 지점이다.</summary>
    public static class ShopPurchaseRules
    {
        public static ShopPurchaseQuote Evaluate(
            ShopProductDefinition product,
            ShopAvailabilityTable availability,
            ShopWalletBalance wallet,
            int purchaseCount)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (availability == null)
                throw new ArgumentNullException(nameof(availability));
            if (purchaseCount < 0)
                throw new ArgumentOutOfRangeException(nameof(purchaseCount));

            int remaining = product.MaxPurchasesPerPeriod == 0
                ? ShopPurchaseQuote.UnlimitedPurchases
                : Math.Max(0, product.MaxPurchasesPerPeriod - purchaseCount);

            if (!availability.IsPurchasable(product))
                return ShopPurchaseQuote.Rejected(product, ShopPurchaseFailureReason.CategoryLocked, 0L, remaining);

            if (remaining == 0)
                return ShopPurchaseQuote.Rejected(product, ShopPurchaseFailureReason.PurchaseLimitReached, 0L, 0);

            long balance = wallet.Get(product.Currency);
            if (balance < product.Price)
            {
                return ShopPurchaseQuote.Rejected(
                    product, ShopPurchaseFailureReason.InsufficientFunds, product.Price - balance, remaining);
            }

            return ShopPurchaseQuote.Allowed(product, remaining);
        }
    }
}

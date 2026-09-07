using System;
using System.Collections.Generic;
using Baseball.Core.Shop;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 상점 구매의 단일 진입점이다. 구매 가능 판정(<see cref="ShopPurchaseRules"/>)으로 막고,
    /// 실제 지급은 종류별 <see cref="IShopProductFulfillment"/>에 위임한 뒤 구매 횟수를 기록한다.
    /// </summary>
    public sealed class ShopService
    {
        private readonly ShopCatalog _catalog;
        private readonly ShopAvailabilityTable _availability;
        private readonly IShopWallet _wallet;
        private readonly Dictionary<ShopProductKind, IShopProductFulfillment> _fulfillments;
        private readonly ShopPurchaseHistoryState _history;
        private readonly Dictionary<string, ShopProductDetails> _detailsByProductId;
        private readonly Func<ShopProductDefinition, ShopProductDetails> _detailsResolver;
        private readonly ShopProgressDetails _progress;

        public ShopService(
            ShopCatalog catalog,
            ShopAvailabilityTable availability,
            IShopWallet wallet,
            IReadOnlyList<IShopProductFulfillment> fulfillments,
            ShopPurchaseHistoryState history,
            IReadOnlyList<ShopProductDetails> details = null,
            ShopProgressDetails? progress = null,
            Func<ShopProductDefinition, ShopProductDetails> detailsResolver = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _progress = progress ?? new ShopProgressDetails(0, 100);
            _detailsResolver = detailsResolver;
            if (fulfillments == null)
                throw new ArgumentNullException(nameof(fulfillments));

            _fulfillments = new Dictionary<ShopProductKind, IShopProductFulfillment>(fulfillments.Count);
            for (int index = 0; index < fulfillments.Count; index++)
            {
                IShopProductFulfillment fulfillment = fulfillments[index]
                    ?? throw new ArgumentException("null 지급 구현이 있습니다.", nameof(fulfillments));
                if (_fulfillments.ContainsKey(fulfillment.Kind))
                    throw new ArgumentException("같은 상품 종류의 지급 구현이 둘 이상입니다.", nameof(fulfillments));
                _fulfillments.Add(fulfillment.Kind, fulfillment);
            }

            _detailsByProductId = new Dictionary<string, ShopProductDetails>(StringComparer.Ordinal);
            if (details == null)
                return;
            for (int index = 0; index < details.Count; index++)
            {
                ShopProductDetails productDetails = details[index]
                    ?? throw new ArgumentException("null 상품 상세가 있습니다.", nameof(details));
                if (!_catalog.TryGetProduct(productDetails.ProductId, out _))
                    throw new ArgumentException("카탈로그에 없는 상품 상세가 있습니다.", nameof(details));
                if (_detailsByProductId.ContainsKey(productDetails.ProductId))
                    throw new ArgumentException("같은 상품 상세가 둘 이상입니다.", nameof(details));
                _detailsByProductId.Add(productDetails.ProductId, productDetails);
            }
        }

        public ShopCatalog Catalog => _catalog;
        public ShopAvailabilityTable Availability => _availability;
        public ShopPurchaseHistoryState History => _history;
        public ShopProgressDetails Progress => _progress;

        public ShopWalletBalance GetBalance() => _wallet.GetBalance();

        /// <summary>Simulation이 확정한 실제 결과군 확률을 돌려준다.</summary>
        public bool TryGetDetails(string productId, out ShopProductDetails details)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                details = null;
                return false;
            }
            if (_detailsByProductId.TryGetValue(productId, out details))
                return true;
            if (_detailsResolver == null || !_catalog.TryGetProduct(productId, out ShopProductDefinition product))
                return false;

            // 전체 구단·연도 조합의 확률표를 구매와 화면 갱신마다 계산하지 않는다.
            details = _detailsResolver(product);
            if (details == null) return false;
            if (!string.Equals(details.ProductId, productId, StringComparison.Ordinal))
                throw new InvalidOperationException("요청 상품과 상세 정보의 ID가 다릅니다.");
            _detailsByProductId.Add(productId, details);
            return true;
        }

        public ShopPurchaseQuote GetQuote(ShopProductDefinition product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return ShopPurchaseRules.Evaluate(
                product,
                _availability,
                _wallet.GetBalance(),
                _history.GetPurchaseCount(product.ProductId));
        }

        public bool TryGetQuote(string productId, out ShopPurchaseQuote quote)
        {
            if (!_catalog.TryGetProduct(productId, out ShopProductDefinition product))
            {
                quote = default;
                return false;
            }
            quote = GetQuote(product);
            return true;
        }

        public ShopPurchaseResult Purchase(string productId)
        {
            if (!_catalog.TryGetProduct(productId, out ShopProductDefinition product))
            {
                return ShopPurchaseResult.Failure(
                    ShopPurchaseFailureReason.UnknownProduct, "존재하지 않는 상품입니다.");
            }

            ShopPurchaseQuote quote = GetQuote(product);
            if (!quote.CanPurchase)
                return ShopPurchaseResult.Failure(quote.FailureReason, DescribeFailure(product, quote));

            if (!_fulfillments.TryGetValue(product.Kind, out IShopProductFulfillment fulfillment))
            {
                return ShopPurchaseResult.Failure(
                    ShopPurchaseFailureReason.UnknownProduct, "지급 방법이 등록되지 않은 상품입니다.");
            }

            ShopFulfillmentResult fulfillmentResult = fulfillment.Fulfill(product);
            if (!fulfillmentResult.IsSuccess)
            {
                // 지급이 실패하면 구매 횟수도 늘리지 않는다. 결제는 지급 구현이 소유하므로
                // 실패 경로에서 재화가 빠져나간 상태로 남지 않는 것은 지급 구현의 계약이다.
                return ShopPurchaseResult.Failure(
                    ShopPurchaseFailureReason.None, fulfillmentResult.FailureMessage);
            }

            _history.RecordPurchase(product.ProductId);
            return ShopPurchaseResult.Success(fulfillmentResult.Items);
        }

        private string DescribeFailure(ShopProductDefinition product, ShopPurchaseQuote quote)
        {
            switch (quote.FailureReason)
            {
                case ShopPurchaseFailureReason.CategoryLocked:
                    return _availability.Get(product.Tab).LockDescription;
                case ShopPurchaseFailureReason.InsufficientFunds:
                    return string.Concat(
                        ShopCurrencyNames.Get(product.Currency), "이(가) ", quote.Shortfall.ToString(), "만큼 부족합니다.");
                case ShopPurchaseFailureReason.PurchaseLimitReached:
                    return "이번 주기 구매 한도를 모두 사용했습니다.";
                default:
                    return "구매할 수 없는 상품입니다.";
            }
        }
    }

    /// <summary>재화 이름을 한 곳에서 문자열로 만든다.</summary>
    public static class ShopCurrencyNames
    {
        public static string Get(ShopCurrency currency)
        {
            switch (currency)
            {
                case ShopCurrency.Money: return "자금";
                case ShopCurrency.ScoutingPoint: return "스카우트 포인트";
                case ShopCurrency.DevelopmentPoint: return "육성 포인트";
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }

        /// <summary>타일 가격 앞에 붙는 재화 기호다.</summary>
        public static string GetSymbol(ShopCurrency currency)
        {
            switch (currency)
            {
                case ShopCurrency.Money: return "₩";
                case ShopCurrency.ScoutingPoint: return "SP";
                case ShopCurrency.DevelopmentPoint: return "DP";
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }
    }
}

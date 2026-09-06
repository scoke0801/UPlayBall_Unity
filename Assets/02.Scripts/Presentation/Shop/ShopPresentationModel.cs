using System;
using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Game.Shop;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Shop
{
    /// <summary>상품 타일 한 장이 그릴 문자열과 상태다. View는 이 값을 표시만 한다.</summary>
    public readonly struct ShopProductTileSnapshot
    {
        public ShopProductTileSnapshot(
            string productId,
            string title,
            string subtitle,
            string priceText,
            string badgeText,
            string countBadgeText,
            bool canPurchase,
            string blockedReason,
            string artworkKey = null)
        {
            ProductId = productId;
            Title = title;
            Subtitle = subtitle;
            PriceText = priceText;
            BadgeText = badgeText;
            CountBadgeText = countBadgeText;
            CanPurchase = canPurchase;
            BlockedReason = blockedReason;
            ArtworkKey = artworkKey ?? string.Empty;
        }

        public string ProductId { get; }
        public string Title { get; }

        /// <summary>부제 띠. 예: "선수단 전체-일반".</summary>
        public string Subtitle { get; }

        public string PriceText { get; }

        /// <summary>NEW/SALE/BEST. 없으면 빈 문자열이다.</summary>
        public string BadgeText { get; }

        /// <summary>묶음 상품의 획득 횟수 배지를 만든다. 단품은 무작위 획득으로 표시한다.</summary>
        public string CountBadgeText { get; }

        public bool CanPurchase { get; }

        /// <summary>구매할 수 없는 이유다. 구매 가능하면 빈 문자열이다.</summary>
        public string BlockedReason { get; }
        public string ArtworkKey { get; }
    }

    /// <summary>탭 하나의 표시 상태다. 잠긴 탭도 사유를 달고 그대로 노출한다.</summary>
    public sealed class ShopTabSnapshot
    {
        public ShopTabSnapshot(
            ShopTab tab,
            string title,
            bool isUnlocked,
            string lockDescription,
            IReadOnlyList<ShopProductTileSnapshot> tiles)
        {
            Tab = tab;
            Title = title;
            IsUnlocked = isUnlocked;
            LockDescription = lockDescription ?? string.Empty;
            Tiles = tiles ?? new ShopProductTileSnapshot[0];
        }

        public ShopTab Tab { get; }
        public string Title { get; }
        public bool IsUnlocked { get; }
        public string LockDescription { get; }
        public IReadOnlyList<ShopProductTileSnapshot> Tiles { get; }
    }

    /// <summary>상점 화면 전체 스냅샷이다.</summary>
    public sealed class ShopScreenSnapshot
    {
        public ShopScreenSnapshot(IReadOnlyList<ShopTabSnapshot> tabs, string walletSummary)
        {
            Tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
            WalletSummary = walletSummary ?? string.Empty;
        }

        public IReadOnlyList<ShopTabSnapshot> Tabs { get; }

        /// <summary>상단에 표시할 보유 재화 요약이다. 실제 존재하는 재화만 들어간다.</summary>
        public string WalletSummary { get; }
    }

    /// <summary>상품 상세·구매 확인·Reveal 재진입이 함께 사용하는 최신 구매 Preview다.</summary>
    public sealed class ShopProductDetailsSnapshot
    {
        public ShopProductDetailsSnapshot(
            string productId,
            ShopProductKind kind,
            string title,
            string subtitle,
            string priceText,
            string drawCountText,
            string summary,
            IReadOnlyList<string> probabilityLines,
            string notice,
            string purchaseLimitText,
            bool canPurchase,
            string blockedReason,
            string artworkKey)
        {
            ProductId = productId ?? string.Empty;
            Kind = kind;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            PriceText = priceText ?? string.Empty;
            DrawCountText = drawCountText ?? string.Empty;
            Summary = summary ?? string.Empty;
            ProbabilityLines = probabilityLines ?? new string[0];
            Notice = notice ?? string.Empty;
            PurchaseLimitText = purchaseLimitText ?? string.Empty;
            CanPurchase = canPurchase;
            BlockedReason = blockedReason ?? string.Empty;
            ArtworkKey = artworkKey ?? string.Empty;
        }

        public string ProductId { get; }
        public ShopProductKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string PriceText { get; }
        public string DrawCountText { get; }
        public string Summary { get; }
        public IReadOnlyList<string> ProbabilityLines { get; }
        public string Notice { get; }
        public string PurchaseLimitText { get; }
        public bool CanPurchase { get; }
        public string BlockedReason { get; }
        public string ArtworkKey { get; }
    }

    /// <summary>
    /// <see cref="ShopService"/>의 판정 결과를 화면 문자열로 바꾼다.
    /// 구매 가능 여부·확률·가격을 여기서 다시 계산하지 않는 것이 이 클래스의 계약이다.
    /// </summary>
    public static class ShopPresentationModel
    {
        private static readonly ShopTab[] TabOrder =
        {
            ShopTab.Featured, ShopTab.PlayerCard, ShopTab.SkillBlock, ShopTab.TacticCard
        };

        public static ShopScreenSnapshot CreateSnapshot(ShopService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var tabs = new List<ShopTabSnapshot>(TabOrder.Length);
            for (int index = 0; index < TabOrder.Length; index++)
                tabs.Add(CreateTab(service, TabOrder[index]));
            return new ShopScreenSnapshot(tabs, DescribeWallet(service.GetBalance()));
        }

        public static string DescribeTab(ShopTab tab)
        {
            switch (tab)
            {
                case ShopTab.Featured: return "추천 상품";
                case ShopTab.PlayerCard: return "선수 카드";
                case ShopTab.SkillBlock: return "스킬 블록";
                case ShopTab.TacticCard: return "작전 카드";
                default: throw new ArgumentOutOfRangeException(nameof(tab));
            }
        }

        public static string DescribeBadge(ShopProductBadge badge)
        {
            switch (badge)
            {
                case ShopProductBadge.None: return string.Empty;
                case ShopProductBadge.New: return "신규";
                case ShopProductBadge.Sale: return "할인";
                case ShopProductBadge.Best: return "인기";
                default: throw new ArgumentOutOfRangeException(nameof(badge));
            }
        }

        /// <summary>상품 클릭 시 Game 계층의 확률 Query와 현재 Quote를 하나의 표시 Snapshot으로 묶는다.</summary>
        public static bool TryCreateDetails(
            ShopService service,
            string productId,
            out ShopProductDetailsSnapshot snapshot)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (!service.Catalog.TryGetProduct(productId, out ShopProductDefinition product) ||
                !service.TryGetDetails(productId, out ShopProductDetails details))
            {
                snapshot = null;
                return false;
            }

            ShopPurchaseQuote quote = service.GetQuote(product);
            var lines = new string[details.Probabilities.Count];
            for (int index = 0; index < lines.Length; index++)
            {
                ShopProbabilityEntry entry = details.Probabilities[index];
                string candidateText = entry.CandidateCount > 0
                    ? string.Concat(" · 후보 ", entry.CandidateCount.ToString("N0"), "장")
                    : string.Empty;
                lines[index] = string.Concat(
                    entry.Label,
                    "  ",
                    entry.Probability.ToString("P2"),
                    candidateText);
            }

            snapshot = new ShopProductDetailsSnapshot(
                product.ProductId,
                product.Kind,
                product.DisplayName,
                DescribeSubtitle(product),
                DescribePrice(product),
                product.DrawCount == 1 ? "1회 획득" : product.DrawCount.ToString("N0") + "회 묶음",
                details.Summary,
                lines,
                details.Notice,
                DescribePurchaseLimit(quote),
                quote.CanPurchase,
                DescribeBlockedReason(service, product, quote),
                DescribeArtworkKey(product));
            return true;
        }

        private static ShopTabSnapshot CreateTab(ShopService service, ShopTab tab)
        {
            ShopCategoryAvailability availability = service.Availability.Get(tab);
            IReadOnlyList<ShopProductDefinition> products = service.Catalog.GetProducts(tab);
            var tiles = new List<ShopProductTileSnapshot>(products.Count);
            for (int index = 0; index < products.Count; index++)
                tiles.Add(CreateTile(service, products[index]));
            return new ShopTabSnapshot(
                tab, DescribeTab(tab), availability.IsUnlocked, availability.LockDescription, tiles);
        }

        private static ShopProductTileSnapshot CreateTile(ShopService service, ShopProductDefinition product)
        {
            ShopPurchaseQuote quote = service.GetQuote(product);
            return new ShopProductTileSnapshot(
                product.ProductId,
                product.DisplayName,
                DescribeSubtitle(product),
                DescribePrice(product),
                DescribeBadge(product.Badge),
                DescribeCountBadge(product),
                quote.CanPurchase,
                DescribeBlockedReason(service, product, quote),
                DescribeArtworkKey(product));
        }

        private static string DescribeArtworkKey(ShopProductDefinition product)
        {
            return ShopArtwork.GetProductKey(product.Kind, product.SourceId);
        }

        private static string DescribeSubtitle(ShopProductDefinition product)
        {
            if (product.GradeLabel.Length == 0)
                return product.ScopeLabel;
            return string.Concat(product.ScopeLabel, "-", product.GradeLabel);
        }

        private static string DescribePrice(ShopProductDefinition product)
        {
            return string.Concat(
                ShopCurrencyNames.GetSymbol(product.Currency), " ", product.Price.ToString("N0"));
        }

        private static string DescribeCountBadge(ShopProductDefinition product)
        {
            return product.DrawCount == 1 ? "무작위" : product.DrawCount + "회 묶음";
        }

        private static string DescribeBlockedReason(
            ShopService service,
            ShopProductDefinition product,
            ShopPurchaseQuote quote)
        {
            switch (quote.FailureReason)
            {
                case ShopPurchaseFailureReason.None:
                    return string.Empty;
                case ShopPurchaseFailureReason.CategoryLocked:
                    return service.Availability.Get(product.Tab).LockDescription;
                case ShopPurchaseFailureReason.InsufficientFunds:
                    return string.Concat(
                        ShopCurrencyNames.Get(product.Currency), " ", quote.Shortfall.ToString("N0"), " 부족");
                case ShopPurchaseFailureReason.PurchaseLimitReached:
                    return "구매 한도 도달";
                default:
                    return "구매 불가";
            }
        }

        private static string DescribePurchaseLimit(ShopPurchaseQuote quote)
        {
            return quote.RemainingPurchases == ShopPurchaseQuote.UnlimitedPurchases
                ? "구매 제한 없음"
                : string.Concat("이번 주기 남은 구매 ", quote.RemainingPurchases.ToString("N0"), "회");
        }

        private static string DescribeWallet(ShopWalletBalance balance)
        {
            return string.Concat(
                "₩ ", balance.Money.ToString("N0"),
                "   스카우트 포인트 ", balance.ScoutingPoints.ToString("N0"),
                "   육성 포인트 ", balance.DevelopmentPoints.ToString("N0"));
        }
    }
}

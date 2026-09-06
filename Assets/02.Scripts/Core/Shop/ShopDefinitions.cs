using System;
using System.Collections.Generic;

namespace Baseball.Core.Shop
{
    /// <summary>상점이 취급하는 재화다. 기존 경제 상태에 실재하는 것만 둔다.</summary>
    public enum ShopCurrency
    {
        Money,
        ScoutingPoint,
        DevelopmentPoint
    }

    /// <summary>상점 상단 탭. Featured는 다른 탭 상품 중 추천 표시된 것을 모아 보여주는 가상 탭이다.</summary>
    public enum ShopTab
    {
        Featured,
        PlayerCard,
        SkillBlock,
        TacticCard
    }

    /// <summary>상품이 무엇을 지급하는지를 정한다. 지급 절차는 Game 레이어 Fulfillment가 담당한다.</summary>
    public enum ShopProductKind
    {
        PlayerCardPack,
        SkillBlockPack,
        TacticCardPack
    }

    /// <summary>상품 타일 좌상단 강조 배지다. 표시 전용이며 가격·확률에 영향을 주지 않는다.</summary>
    public enum ShopProductBadge
    {
        None,
        New,
        Sale,
        Best
    }

    /// <summary>
    /// 상점 타일 하나를 정의한다. 가격·확률의 원본은 각 시스템 밸런스이며
    /// 이 정의는 그것을 상점 표시·구매 단위로 묶어 참조(<see cref="SourceId"/>)만 보관한다.
    /// </summary>
    public sealed class ShopProductDefinition
    {
        public ShopProductDefinition(
            string productId,
            ShopProductKind kind,
            string sourceId,
            string displayName,
            string scopeLabel,
            string gradeLabel,
            ShopCurrency currency,
            long price,
            int drawCount = 1,
            ShopProductBadge badge = ShopProductBadge.None,
            bool isFeatured = false,
            int maxPurchasesPerPeriod = 0,
            int sortOrder = 0,
            string targetFranchiseId = null,
            string targetFranchiseName = null,
            int? targetYear = null)
        {
            if (string.IsNullOrWhiteSpace(productId))
                throw new ArgumentException("ProductId는 비어 있을 수 없습니다.", nameof(productId));
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("SourceId는 비어 있을 수 없습니다.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("DisplayName은 비어 있을 수 없습니다.", nameof(displayName));
            if (price < 0L)
                throw new ArgumentOutOfRangeException(nameof(price));
            if (drawCount < 1)
                throw new ArgumentOutOfRangeException(nameof(drawCount));
            if (maxPurchasesPerPeriod < 0)
                throw new ArgumentOutOfRangeException(nameof(maxPurchasesPerPeriod));
            if (targetYear.HasValue && targetYear.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetYear));

            ProductId = productId.Trim();
            Kind = kind;
            SourceId = sourceId.Trim();
            DisplayName = displayName.Trim();
            ScopeLabel = scopeLabel == null ? string.Empty : scopeLabel.Trim();
            GradeLabel = gradeLabel == null ? string.Empty : gradeLabel.Trim();
            Currency = currency;
            Price = price;
            DrawCount = drawCount;
            Badge = badge;
            IsFeatured = isFeatured;
            MaxPurchasesPerPeriod = maxPurchasesPerPeriod;
            SortOrder = sortOrder;
            TargetFranchiseId = string.IsNullOrWhiteSpace(targetFranchiseId)
                ? string.Empty
                : targetFranchiseId.Trim();
            TargetFranchiseName = string.IsNullOrWhiteSpace(targetFranchiseName)
                ? string.Empty
                : targetFranchiseName.Trim();
            TargetYear = targetYear;
        }

        public string ProductId { get; }
        public ShopProductKind Kind { get; }

        /// <summary>지급 대상 원본 키다. ScoutPoolId / SkillGachaPurchaseTier / TacticResearchPoolId를 가리킨다.</summary>
        public string SourceId { get; }

        /// <summary>타일 제목. 예: "선수 카드".</summary>
        public string DisplayName { get; }

        /// <summary>부제 좌측. 예: "선수단 전체".</summary>
        public string ScopeLabel { get; }

        /// <summary>부제 우측. 예: "2단계".</summary>
        public string GradeLabel { get; }

        public ShopCurrency Currency { get; }
        public long Price { get; }

        /// <summary>한 번 구매로 뽑는 장수. 1보다 크면 묶음 상품이다.</summary>
        public int DrawCount { get; }

        public ShopProductBadge Badge { get; }
        public bool IsFeatured { get; }

        /// <summary>주기당 구매 한도. 0이면 무제한이다.</summary>
        public int MaxPurchasesPerPeriod { get; }

        public int SortOrder { get; }

        /// <summary>선수 카드 집중 상품의 원 구단 필터다. 다른 상품과 전국 상품은 빈 문자열이다.</summary>
        public string TargetFranchiseId { get; }

        /// <summary>원 구단 필터를 UI에 표시할 이름이다.</summary>
        public string TargetFranchiseName { get; }

        /// <summary>선수 카드 집중 상품의 원 연도 필터다.</summary>
        public int? TargetYear { get; }

        public ShopTab Tab => GetTab(Kind);

        public static ShopTab GetTab(ShopProductKind kind)
        {
            switch (kind)
            {
                case ShopProductKind.PlayerCardPack: return ShopTab.PlayerCard;
                case ShopProductKind.SkillBlockPack: return ShopTab.SkillBlock;
                case ShopProductKind.TacticCardPack: return ShopTab.TacticCard;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    /// <summary>한 게임에서 노출 가능한 상품 전체를 ProductId로 색인해 보관한다.</summary>
    public sealed class ShopCatalog
    {
        private static readonly ShopProductDefinition[] EmptyProducts = new ShopProductDefinition[0];

        private readonly ShopProductDefinition[] _products;
        private readonly Dictionary<string, ShopProductDefinition> _productsById;
        private readonly Dictionary<ShopTab, ShopProductDefinition[]> _productsByTab;

        public ShopCatalog(IReadOnlyList<ShopProductDefinition> products)
        {
            if (products == null)
                throw new ArgumentNullException(nameof(products));

            _productsById = new Dictionary<string, ShopProductDefinition>(products.Count, StringComparer.Ordinal);
            var ordered = new List<ShopProductDefinition>(products.Count);
            for (int index = 0; index < products.Count; index++)
            {
                ShopProductDefinition product = products[index]
                    ?? throw new ArgumentException("null 상품이 있습니다.", nameof(products));
                if (_productsById.ContainsKey(product.ProductId))
                    throw new ArgumentException("ProductId는 중복될 수 없습니다.", nameof(products));
                _productsById.Add(product.ProductId, product);
                ordered.Add(product);
            }

            // SortOrder가 같으면 ProductId로 확정해, 카탈로그 표시 순서가 항상 결정적으로 남게 한다.
            ordered.Sort(CompareByDisplayOrder);
            _products = ordered.ToArray();
            _productsByTab = BuildTabIndex(_products);
        }

        public IReadOnlyList<ShopProductDefinition> Products => _products;

        public bool TryGetProduct(string productId, out ShopProductDefinition product)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                product = null;
                return false;
            }
            return _productsById.TryGetValue(productId, out product);
        }

        public IReadOnlyList<ShopProductDefinition> GetProducts(ShopTab tab)
        {
            return _productsByTab.TryGetValue(tab, out ShopProductDefinition[] products)
                ? products
                : EmptyProducts;
        }

        private static Dictionary<ShopTab, ShopProductDefinition[]> BuildTabIndex(ShopProductDefinition[] products)
        {
            var buckets = new Dictionary<ShopTab, List<ShopProductDefinition>>();
            for (int index = 0; index < products.Length; index++)
            {
                ShopProductDefinition product = products[index];
                AddToBucket(buckets, product.Tab, product);
                if (product.IsFeatured)
                    AddToBucket(buckets, ShopTab.Featured, product);
            }

            var result = new Dictionary<ShopTab, ShopProductDefinition[]>(buckets.Count);
            foreach (KeyValuePair<ShopTab, List<ShopProductDefinition>> bucket in buckets)
                result[bucket.Key] = bucket.Value.ToArray();
            return result;
        }

        private static void AddToBucket(
            Dictionary<ShopTab, List<ShopProductDefinition>> buckets,
            ShopTab tab,
            ShopProductDefinition product)
        {
            if (!buckets.TryGetValue(tab, out List<ShopProductDefinition> bucket))
            {
                bucket = new List<ShopProductDefinition>();
                buckets.Add(tab, bucket);
            }
            bucket.Add(product);
        }

        private static int CompareByDisplayOrder(ShopProductDefinition left, ShopProductDefinition right)
        {
            int bySortOrder = left.SortOrder.CompareTo(right.SortOrder);
            return bySortOrder != 0
                ? bySortOrder
                : string.CompareOrdinal(left.ProductId, right.ProductId);
        }
    }
}

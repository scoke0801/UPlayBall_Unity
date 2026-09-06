using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Shop
{
    /// <summary>작전 카드 연구 상품을 지급한다. Signature 전술은 연구 풀 구조상 절대 나오지 않는다.</summary>
    public sealed class TacticCardPackFulfillment : IShopProductFulfillment
    {
        private readonly TacticResearchRoller _roller;
        private readonly IReadOnlyList<TacticResearchPoolDefinition> _pools;
        private readonly IReadOnlyList<TacticCardDefinition> _tacticCatalog;
        private readonly IShopWallet _wallet;
        private readonly Func<TacticCollectionState> _collectionProvider;
        private readonly Func<IRandomSource> _randomFactory;

        public TacticCardPackFulfillment(
            TacticResearchRoller roller,
            IReadOnlyList<TacticResearchPoolDefinition> pools,
            IReadOnlyList<TacticCardDefinition> tacticCatalog,
            IShopWallet wallet,
            Func<TacticCollectionState> collectionProvider,
            Func<IRandomSource> randomFactory)
        {
            _roller = roller ?? throw new ArgumentNullException(nameof(roller));
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _tacticCatalog = tacticCatalog ?? throw new ArgumentNullException(nameof(tacticCatalog));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        public ShopProductKind Kind => ShopProductKind.TacticCardPack;

        public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (!TryFindPool(product.SourceId, out TacticResearchPoolDefinition pool))
                return ShopFulfillmentResult.Failure("알 수 없는 작전 연구 풀입니다.");

            TacticCollectionState collection = _collectionProvider();
            if (collection == null)
                return ShopFulfillmentResult.Failure("작전 카드 보유 상태가 없습니다.");

            if (!_wallet.TrySpend(product.Currency, product.Price))
                return ShopFulfillmentResult.Failure("자금이 부족합니다.");

            IRandomSource random = _randomFactory();
            var items = new ShopGrantedItem[product.DrawCount];
            for (int index = 0; index < product.DrawCount; index++)
            {
                TacticCardDefinition card = _roller.Roll(pool, _tacticCatalog, random);
                bool isNew = collection.Acquire(card.CardId);
                items[index] = new ShopGrantedItem(
                    card.CardId,
                    card.Name,
                    DescribeTier(card.TacticTier),
                    isNew,
                    DescribeArtworkKey(card.Category));
            }
            return ShopFulfillmentResult.Success(items);
        }

        private bool TryFindPool(string researchPoolId, out TacticResearchPoolDefinition pool)
        {
            for (int index = 0; index < _pools.Count; index++)
            {
                if (string.Equals(_pools[index].ResearchPoolId, researchPoolId, StringComparison.Ordinal))
                {
                    pool = _pools[index];
                    return true;
                }
            }
            pool = null;
            return false;
        }

        private static string DescribeTier(TacticTier tier)
        {
            switch (tier)
            {
                case TacticTier.Normal: return "일반";
                case TacticTier.Rare: return "희귀";
                case TacticTier.Special: return "특수";
                case TacticTier.Signature: return "시그니처";
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        private static string DescribeArtworkKey(TacticCardCategory category)
        {
            switch (category)
            {
                case TacticCardCategory.Batting: return "tactic-batting";
                case TacticCardCategory.Pitching: return "tactic-pitching";
                case TacticCardCategory.Analysis:
                case TacticCardCategory.Common: return "tactic-common";
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }
}

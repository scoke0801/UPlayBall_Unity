using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 선수 카드 상품을 기존 <see cref="ScoutRoller"/>로 지급한다.
    /// SP 차감·카드 편입·Pity 누적을 한 트랜잭션으로 처리하고, 결과 카드는 즉시 확정된다.
    /// </summary>
    public sealed class PlayerCardPackFulfillment : IShopProductFulfillment
    {
        private readonly ScoutRoller _roller;
        private readonly IReadOnlyList<ScoutPoolDefinition> _pools;
        private readonly ScoutFeaturePolicy _featurePolicy;
        private readonly ScoutPityBalanceTable _pityBalance;
        private readonly IShopWallet _wallet;
        private readonly Func<ManagerHistoricalRuntimeState> _runtimeProvider;
        private readonly Func<IRandomSource> _randomFactory;
        private readonly Func<string, string> _playerNameResolver;

        public PlayerCardPackFulfillment(
            ScoutRoller roller,
            IReadOnlyList<ScoutPoolDefinition> pools,
            ScoutFeaturePolicy featurePolicy,
            ScoutPityBalanceTable pityBalance,
            IShopWallet wallet,
            Func<ManagerHistoricalRuntimeState> runtimeProvider,
            Func<IRandomSource> randomFactory,
            Func<string, string> playerNameResolver = null)
        {
            _roller = roller ?? throw new ArgumentNullException(nameof(roller));
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _featurePolicy = featurePolicy ?? throw new ArgumentNullException(nameof(featurePolicy));
            _pityBalance = pityBalance ?? throw new ArgumentNullException(nameof(pityBalance));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
            _playerNameResolver = playerNameResolver;
        }

        public ShopProductKind Kind => ShopProductKind.PlayerCardPack;

        public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (!TryFindPool(product.SourceId, out ScoutPoolDefinition pool))
                return ShopFulfillmentResult.Failure("알 수 없는 스카우트 풀입니다.");

            ManagerHistoricalRuntimeState runtime = _runtimeProvider();
            if (runtime == null)
                return ShopFulfillmentResult.Failure("구단주 모드 진행 상태가 없습니다.");

            if (!_wallet.TrySpend(product.Currency, product.Price))
                return ShopFulfillmentResult.Failure("스카우트 포인트가 부족합니다.");

            IRandomSource random = _randomFactory();
            var items = new ShopGrantedItem[product.DrawCount];
            for (int index = 0; index < product.DrawCount; index++)
            {
                PlayerCardDefinition card = _roller.Roll(pool, runtime.WorldCardCatalog, _featurePolicy, random);
                bool isNew = runtime.AcquireCard(card.CardId);
                runtime.Economy.AddPityGauge(_pityBalance.GaugeGainPerScout, _pityBalance.Threshold);
                items[index] = new ShopGrantedItem(
                    card.CardId,
                    DescribeCard(runtime, card),
                    DescribeEdition(card.Edition),
                    isNew);
            }
            return ShopFulfillmentResult.Success(items);
        }

        private bool TryFindPool(string scoutPoolId, out ScoutPoolDefinition pool)
        {
            for (int index = 0; index < _pools.Count; index++)
            {
                if (string.Equals(_pools[index].ScoutPoolId, scoutPoolId, StringComparison.Ordinal))
                {
                    pool = _pools[index];
                    return true;
                }
            }
            pool = null;
            return false;
        }

        private string DescribeCard(ManagerHistoricalRuntimeState runtime, PlayerCardDefinition card)
        {
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            string name = _playerNameResolver == null
                ? season.PlayerPersonId
                : _playerNameResolver(season.PlayerPersonId);
            return string.Concat(name, " ", season.OriginYear.ToString(), " (Cost ", season.Cost.ToString(), ")");
        }

        private static string DescribeEdition(PlayerCardEdition edition)
        {
            switch (edition)
            {
                case PlayerCardEdition.Normal: return "일반";
                case PlayerCardEdition.AllStar: return "올스타";
                case PlayerCardEdition.GoldenGlove: return "골든글러브";
                case PlayerCardEdition.Mvp: return "MVP";
                default: throw new ArgumentOutOfRangeException(nameof(edition));
            }
        }
    }
}

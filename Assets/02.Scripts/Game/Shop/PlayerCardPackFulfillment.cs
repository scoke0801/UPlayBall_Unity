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
    /// 재화가 <see cref="ShopCurrency.ScoutPity"/>인 상품은 게이지를 소비하는 보장 영입이다.
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

            bool isGuaranteed = product.Currency == ShopCurrency.ScoutPity;
            if (!_wallet.TrySpend(product.Currency, product.Price))
                return ShopFulfillmentResult.Failure(isGuaranteed
                    ? "보장 영입 게이지가 부족합니다."
                    : "스카우트 포인트가 부족합니다.");

            IRandomSource random = _randomFactory();
            var items = new ShopGrantedItem[product.DrawCount];
            for (int index = 0; index < product.DrawCount; index++)
            {
                PlayerCardDefinition card = isGuaranteed
                    ? RollGuaranteed(pool, runtime, random)
                    : _roller.Roll(pool, runtime.WorldCardCatalog, _featurePolicy, random);
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                CardAcquisitionCommitResult acquisition = runtime.AcquireCardWithResult(card.CardId);
                if (!isGuaranteed)
                    runtime.Economy.AddPityGauge(_pityBalance.GetGaugeGain(pool), _pityBalance.Threshold);
                items[index] = new ShopGrantedItem(
                    card.CardId,
                    DescribeCard(season),
                    DescribeEdition(card.Edition),
                    acquisition.IsNew,
                    primaryIntensity: DescribeCostIntensity(season.Cost),
                    secondaryIntensity: DescribeEditionIntensity(card.Edition),
                    wasWishlisted: acquisition.WasWishlisted);
            }
            return ShopFulfillmentResult.Success(items);
        }

        private PlayerCardDefinition RollGuaranteed(
            ScoutPoolDefinition pool,
            ManagerHistoricalRuntimeState runtime,
            IRandomSource random)
        {
            HashSet<string> ownedSeasonIds = CollectOwnedSeasonIds(runtime);
            return _roller.RollGuaranteed(
                pool, runtime.WorldCardCatalog, _featurePolicy, _pityBalance, ownedSeasonIds.Contains, random);
        }

        /// <summary>어느 Edition이든 한 장이라도 가진 선수 시즌은 보유한 것으로 본다.</summary>
        public static HashSet<string> CollectOwnedSeasonIds(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            var seasonIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<OwnedPlayerCardState> owned = runtime.OwnedCards;
            for (int index = 0; index < owned.Count; index++)
            {
                if (runtime.WorldCardCatalog.TryGetCard(owned[index].CardId, out PlayerCardDefinition card))
                    seasonIds.Add(card.PlayerSeasonId);
            }
            return seasonIds;
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

        private string DescribeCard(PlayerSeasonDefinition season)
        {
            string name = _playerNameResolver == null
                ? season.PlayerPersonId
                : _playerNameResolver(season.PlayerPersonId);
            return string.Concat(name, " ", season.OriginYear.ToString(), " (Cost ", season.Cost.ToString(), ")");
        }

        private static string DescribeEdition(PlayerCardEdition edition)
        {
            return Baseball.Game.Historical.PlayerCardEditionText.Get(edition);
        }

        private static ShopRevealIntensity DescribeCostIntensity(int cost)
        {
            if (cost >= 9) return ShopRevealIntensity.Exceptional;
            if (cost >= 7) return ShopRevealIntensity.Rare;
            if (cost >= 4) return ShopRevealIntensity.Notable;
            return ShopRevealIntensity.Standard;
        }

        private static ShopRevealIntensity DescribeEditionIntensity(PlayerCardEdition edition)
        {
            switch (edition)
            {
                case PlayerCardEdition.Normal: return ShopRevealIntensity.Standard;
                case PlayerCardEdition.AllStar: return ShopRevealIntensity.Notable;
                case PlayerCardEdition.GoldenGlove: return ShopRevealIntensity.Rare;
                case PlayerCardEdition.Mvp: return ShopRevealIntensity.Exceptional;
                case PlayerCardEdition.Rare: return ShopRevealIntensity.Rare;
                case PlayerCardEdition.Ex:
                case PlayerCardEdition.CareerHigh:
                case PlayerCardEdition.Legend: return ShopRevealIntensity.Exceptional;
                default: throw new ArgumentOutOfRangeException(nameof(edition));
            }
        }
    }
}

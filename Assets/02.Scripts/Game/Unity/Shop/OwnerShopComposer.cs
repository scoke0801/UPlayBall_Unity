using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 구단주 런타임에서 상점 한 벌을 조립한다. 상점이 어떤 밸런스·풀·지갑을 쓰는지 아는 곳을
    /// 여기 하나로 모아, UI와 <see cref="ShopService"/>는 조립 방법을 모르게 한다.
    /// </summary>
    public static class OwnerShopComposer
    {
        /// <summary>상점 구매가 월드 시드와 겹치지 않도록 쓰는 고정 스트림 태그다.</summary>
        private const ulong ShopStreamTag = 0x5348_4F50_0000_0000UL;

        public static ShopService Create(
            OwnerModeManager manager,
            ShopPurchaseHistoryState history,
            TacticCollectionState tacticCollection)
        {
            if (manager == null)
                throw new System.ArgumentNullException(nameof(manager));
            if (history == null)
                throw new System.ArgumentNullException(nameof(history));
            if (tacticCollection == null)
                throw new System.ArgumentNullException(nameof(tacticCollection));

            ManagerHistoricalRuntimeState runtime = manager.Runtime
                ?? throw new System.InvalidOperationException("구단주 런타임이 아직 준비되지 않았습니다.");

            ScoutFeaturePolicy featurePolicy = ResolveFeaturePolicy(runtime.WorldCardCatalog);
            IReadOnlyList<ScoutPoolDefinition> scoutPools = ShopDefaultPools.CreateScoutPools(featurePolicy);
            IReadOnlyList<TacticResearchPoolDefinition> tacticPools = ShopDefaultPools.CreateTacticResearchPools();

            ShopCatalog catalog = ShopCatalogBuilder.Build(
                manager.Balance.Growth.SkillGacha, scoutPools, tacticPools);
            var wallet = new ManagerEconomyShopWallet(runtime.Economy);

            var fulfillments = new List<IShopProductFulfillment>
            {
                new PlayerCardPackFulfillment(
                    new ScoutRoller(),
                    scoutPools,
                    featurePolicy,
                    ScoutPityBalanceTable.CreateInitial(),
                    wallet,
                    () => manager.Runtime,
                    () => CreateRandom(manager, history),
                    personId => manager.Runtime.IdentityRegistry.GetPlayerDisplayName(personId)),
                new TacticCardPackFulfillment(
                    new TacticResearchRoller(),
                    tacticPools,
                    manager.GetAvailableTacticCards(),
                    wallet,
                    () => tacticCollection,
                    () => CreateRandom(manager, history))
            };

            return new ShopService(
                catalog,
                ShopAvailabilityFactory.CreateFor(GameMode.OwnerCareer),
                wallet,
                fulfillments,
                history);
        }

        /// <summary>
        /// 구매마다 누적 구매 번호를 스트림으로 써서, 같은 World Seed와 같은 구매 순번이면
        /// 언제나 같은 결과가 나오게 한다.
        /// </summary>
        private static IRandomSource CreateRandom(OwnerModeManager manager, ShopPurchaseHistoryState history)
        {
            ulong stream = ShopStreamTag ^ (uint)(history.TotalPurchaseCount + 1);
            ulong seed = DeterministicSeed.Derive(manager.Runtime.WorldHistory.WorldHistorySeed, stream);
            return new Pcg32Random(seed);
        }

        /// <summary>
        /// World에 특수 Edition 카드가 실제로 존재할 때만 수상 스카우트를 연다.
        /// Edition 활성화는 World 수상 기록이 결정하므로 카탈로그 내용이 곧 현재 Phase다.
        /// </summary>
        private static ScoutFeaturePolicy ResolveFeaturePolicy(WorldCardCatalog catalog)
        {
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                if (catalog.Cards[index].Edition != PlayerCardEdition.Normal)
                    return ScoutFeaturePolicy.FullWorldAwards;
            }
            return ScoutFeaturePolicy.Phase4NormalOnly;
        }
    }
}

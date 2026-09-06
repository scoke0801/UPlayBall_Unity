using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 상점이 처음 열릴 때 쓰는 기본 풀 구성이다. 확률 가중치는 기존 밸런스 헬퍼를 그대로 쓰고,
    /// 여기서는 어떤 풀을 진열할지와 가격만 정한다.
    /// </summary>
    public static class ShopDefaultPools
    {
        /// <summary>일반 스카우트 1회 가격(SP). 상위 풀은 이 값을 기준으로 배수를 매긴다.</summary>
        public const int GeneralScoutPriceSp = 100;

        /// <summary>전 계열 작전 연구 1회 가격(자금).</summary>
        public const long GeneralTacticResearchPrice = 3_500L;

        /// <summary>
        /// 특수 Edition이 아직 열리지 않은 단계에서도 안전한 기본 Scout 풀이다.
        /// Award 풀은 <see cref="ScoutFeaturePolicy"/>가 허용할 때만 진열한다.
        /// </summary>
        public static IReadOnlyList<ScoutPoolDefinition> CreateScoutPools(ScoutFeaturePolicy featurePolicy)
        {
            var pools = new List<ScoutPoolDefinition>
            {
                new ScoutPoolDefinition(
                    "general",
                    ScoutType.General,
                    ScoutPoolDefinition.CreateInitialCostWeights(),
                    featurePolicy.AreSpecialEditionsEnabled
                        ? ScoutPoolDefinition.CreateStandardEditionWeights()
                        : ScoutPoolDefinition.CreateNormalOnlyEditionWeights(),
                    GeneralScoutPriceSp)
            };

            if (featurePolicy.IsAwardScoutEnabled)
            {
                // 수상 Edition만 나오는 풀이라 일반 스카우트보다 훨씬 비싸다.
                pools.Add(new ScoutPoolDefinition(
                    "award_allstar",
                    ScoutType.Award,
                    ScoutPoolDefinition.CreateInitialCostWeights(),
                    new[] { 0d, 100d, 0d, 0d },
                    GeneralScoutPriceSp * 6,
                    editionFilter: PlayerCardEdition.AllStar));
            }

            return pools;
        }

        /// <summary>전 계열 하나와 계열별 연구 풀을 진열한다. 계열 지정은 값이 큰 대신 더 비싸다.</summary>
        public static IReadOnlyList<TacticResearchPoolDefinition> CreateTacticResearchPools()
        {
            double[] tierWeights = TacticResearchPoolDefinition.CreateInitialTierWeights();
            return new List<TacticResearchPoolDefinition>
            {
                new TacticResearchPoolDefinition("general", tierWeights, GeneralTacticResearchPrice),
                new TacticResearchPoolDefinition(
                    "batting", tierWeights, GeneralTacticResearchPrice * 3 / 2, TacticCardCategory.Batting),
                new TacticResearchPoolDefinition(
                    "pitching", tierWeights, GeneralTacticResearchPrice * 3 / 2, TacticCardCategory.Pitching),
                new TacticResearchPoolDefinition(
                    "analysis", tierWeights, GeneralTacticResearchPrice * 3 / 2, TacticCardCategory.Analysis)
            };
        }
    }
}

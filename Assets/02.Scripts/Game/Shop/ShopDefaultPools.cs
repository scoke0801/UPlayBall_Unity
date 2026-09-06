using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>현재 월드 카드 카탈로그에 실제로 존재하는 원 구단·원 연도 조합이다.</summary>
    public readonly struct ScoutMarketTarget
    {
        public ScoutMarketTarget(string franchiseId, int year)
        {
            if (string.IsNullOrWhiteSpace(franchiseId))
                throw new ArgumentException("FranchiseId는 비어 있을 수 없습니다.", nameof(franchiseId));
            if (year <= 0)
                throw new ArgumentOutOfRangeException(nameof(year));
            FranchiseId = franchiseId.Trim();
            Year = year;
        }

        public string FranchiseId { get; }
        public int Year { get; }
    }

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
            return CreateScoutPools(featurePolicy, null, null);
        }

        /// <summary>현재 구단과 시즌이 있으면 문서에 정의된 집중 Scout 풀까지 함께 연다.</summary>
        public static IReadOnlyList<ScoutPoolDefinition> CreateScoutPools(
            ScoutFeaturePolicy featurePolicy,
            string franchiseFilter,
            int? yearFilter)
        {
            if (featurePolicy == null)
                throw new ArgumentNullException(nameof(featurePolicy));
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

            bool hasFranchise = !string.IsNullOrWhiteSpace(franchiseFilter);
            bool hasYear = yearFilter.HasValue;
            if (hasFranchise)
            {
                pools.Add(new ScoutPoolDefinition(
                    "franchise_home",
                    ScoutType.Franchise,
                    ScoutPoolDefinition.CreateInitialCostWeights(),
                    featurePolicy.AreSpecialEditionsEnabled
                        ? ScoutPoolDefinition.CreateStandardEditionWeights()
                        : ScoutPoolDefinition.CreateNormalOnlyEditionWeights(),
                    GeneralScoutPriceSp * 8 / 5,
                    franchiseFilter: franchiseFilter));
            }
            if (hasYear)
            {
                pools.Add(new ScoutPoolDefinition(
                    "year_current",
                    ScoutType.Year,
                    ScoutPoolDefinition.CreateInitialCostWeights(),
                    featurePolicy.AreSpecialEditionsEnabled
                        ? ScoutPoolDefinition.CreateStandardEditionWeights()
                        : ScoutPoolDefinition.CreateNormalOnlyEditionWeights(),
                    GeneralScoutPriceSp * 8 / 5,
                    yearFilter: yearFilter));
            }
            if (hasFranchise && hasYear)
            {
                pools.Add(new ScoutPoolDefinition(
                    "year_franchise_current",
                    ScoutType.YearFranchise,
                    ScoutPoolDefinition.CreateInitialCostWeights(),
                    featurePolicy.AreSpecialEditionsEnabled
                        ? ScoutPoolDefinition.CreateStandardEditionWeights()
                        : ScoutPoolDefinition.CreateNormalOnlyEditionWeights(),
                    GeneralScoutPriceSp * 12 / 5,
                    franchiseFilter: franchiseFilter,
                    yearFilter: yearFilter));
            }

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

        /// <summary>
        /// 월드에 실제 존재하는 모든 구단·연도를 대상으로 구단 집중, 연도 집중, 정밀 Scout 풀을 만든다.
        /// 후보가 없는 구단·연도 조합은 만들지 않아 빈 상품이 진열되지 않게 한다.
        /// </summary>
        public static IReadOnlyList<ScoutPoolDefinition> CreateScoutPools(
            ScoutFeaturePolicy featurePolicy,
            IReadOnlyList<ScoutMarketTarget> marketTargets)
        {
            if (featurePolicy == null)
                throw new ArgumentNullException(nameof(featurePolicy));
            if (marketTargets == null)
                throw new ArgumentNullException(nameof(marketTargets));

            var targets = CopyDistinctTargets(marketTargets);
            var franchises = new List<string>();
            var years = new List<int>();
            for (int index = 0; index < targets.Count; index++)
            {
                ScoutMarketTarget target = targets[index];
                if (!franchises.Contains(target.FranchiseId)) franchises.Add(target.FranchiseId);
                if (!years.Contains(target.Year)) years.Add(target.Year);
            }
            franchises.Sort(StringComparer.Ordinal);
            years.Sort((left, right) => right.CompareTo(left));

            double[] editionWeights = featurePolicy.AreSpecialEditionsEnabled
                ? ScoutPoolDefinition.CreateStandardEditionWeights()
                : ScoutPoolDefinition.CreateNormalOnlyEditionWeights();
            var pools = new List<ScoutPoolDefinition>(
                1 + franchises.Count + years.Count + targets.Count + (featurePolicy.IsAwardScoutEnabled ? 1 : 0));
            pools.Add(CreatePool("general", ScoutType.General, GeneralScoutPriceSp, editionWeights));

            for (int index = 0; index < franchises.Count; index++)
            {
                string franchiseId = franchises[index];
                pools.Add(CreatePool(
                    "franchise_" + franchiseId,
                    ScoutType.Franchise,
                    GeneralScoutPriceSp * 8 / 5,
                    editionWeights,
                    franchiseId));
            }
            for (int index = 0; index < years.Count; index++)
            {
                int year = years[index];
                pools.Add(CreatePool(
                    "year_" + year,
                    ScoutType.Year,
                    GeneralScoutPriceSp * 8 / 5,
                    editionWeights,
                    yearFilter: year));
            }
            for (int index = 0; index < targets.Count; index++)
            {
                ScoutMarketTarget target = targets[index];
                pools.Add(CreatePool(
                    string.Concat("year_franchise_", target.Year.ToString(), "_", target.FranchiseId),
                    ScoutType.YearFranchise,
                    GeneralScoutPriceSp * 12 / 5,
                    editionWeights,
                    target.FranchiseId,
                    target.Year));
            }

            if (featurePolicy.IsAwardScoutEnabled)
            {
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

        private static ScoutPoolDefinition CreatePool(
            string poolId,
            ScoutType scoutType,
            int priceSp,
            IReadOnlyList<double> editionWeights,
            string franchiseFilter = null,
            int? yearFilter = null)
        {
            return new ScoutPoolDefinition(
                poolId,
                scoutType,
                ScoutPoolDefinition.CreateInitialCostWeights(),
                editionWeights,
                priceSp,
                franchiseFilter,
                yearFilter);
        }

        private static List<ScoutMarketTarget> CopyDistinctTargets(
            IReadOnlyList<ScoutMarketTarget> marketTargets)
        {
            var targets = new List<ScoutMarketTarget>(marketTargets.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < marketTargets.Count; index++)
            {
                ScoutMarketTarget target = marketTargets[index];
                if (string.IsNullOrWhiteSpace(target.FranchiseId) || target.Year <= 0)
                    throw new ArgumentException("Scout 대상의 구단과 연도가 올바르지 않습니다.", nameof(marketTargets));
                string key = string.Concat(target.FranchiseId, "\u001f", target.Year.ToString());
                if (keys.Add(key)) targets.Add(target);
            }
            targets.Sort((left, right) =>
            {
                int byYear = right.Year.CompareTo(left.Year);
                return byYear != 0 ? byYear : string.CompareOrdinal(left.FranchiseId, right.FranchiseId);
            });
            return targets;
        }

        /// <summary>전 계열 하나와 계열별 연구 풀을 진열한다. 계열 지정은 값이 큰 대신 더 비싸다.</summary>
        public static IReadOnlyList<TacticResearchPoolDefinition> CreateTacticResearchPools(
            double tacticResearchEfficiencyModifier = 0d)
        {
            if (double.IsNaN(tacticResearchEfficiencyModifier) ||
                double.IsInfinity(tacticResearchEfficiencyModifier) ||
                tacticResearchEfficiencyModifier < 0d)
                throw new System.ArgumentOutOfRangeException(nameof(tacticResearchEfficiencyModifier));
            double[] tierWeights = TacticResearchPoolDefinition.CreateInitialTierWeights();
            double multiplier = 1d + tacticResearchEfficiencyModifier;
            tierWeights[(int)TacticTier.Normal] /= multiplier;
            tierWeights[(int)TacticTier.Rare] *= multiplier;
            tierWeights[(int)TacticTier.Special] *= 1d + tacticResearchEfficiencyModifier * 2d;
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

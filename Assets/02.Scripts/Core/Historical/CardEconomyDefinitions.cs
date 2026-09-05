using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>한 World에서 실제로 활성화된 공통 선수 카드와 원본 시즌을 제공한다.</summary>
    public sealed class WorldCardCatalog
    {
        private readonly PlayerCardDefinition[] _cards;
        private readonly Dictionary<string, PlayerCardDefinition> _cardsById;
        private readonly Dictionary<string, PlayerSeasonDefinition> _seasonsById;

        public WorldCardCatalog(
            IReadOnlyList<PlayerSeasonDefinition> playerSeasons,
            IReadOnlyList<PlayerCardDefinition> cards)
        {
            if (playerSeasons == null)
                throw new ArgumentNullException(nameof(playerSeasons));
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));

            _seasonsById = new Dictionary<string, PlayerSeasonDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < playerSeasons.Count; index++)
            {
                PlayerSeasonDefinition season = playerSeasons[index]
                    ?? throw new ArgumentException("null 선수 시즌이 있습니다.", nameof(playerSeasons));
                if (!_seasonsById.TryAdd(season.PlayerSeasonId, season))
                    throw new ArgumentException("PlayerSeasonId는 중복될 수 없습니다.", nameof(playerSeasons));
            }

            _cards = new PlayerCardDefinition[cards.Count];
            _cardsById = new Dictionary<string, PlayerCardDefinition>(StringComparer.Ordinal);
            var normalSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index]
                    ?? throw new ArgumentException("null 카드가 있습니다.", nameof(cards));
                if (!_seasonsById.ContainsKey(card.PlayerSeasonId))
                    throw new ArgumentException("카드가 존재하지 않는 PlayerSeason을 참조합니다.", nameof(cards));
                string stableCardId = PlayerCardDefinition.CreateStableCardId(card.PlayerSeasonId, card.Edition);
                if (!string.Equals(card.CardId, stableCardId, StringComparison.Ordinal))
                    throw new ArgumentException("CardId가 Stable CardId 규칙과 일치하지 않습니다.", nameof(cards));
                if (!_cardsById.TryAdd(card.CardId, card))
                    throw new ArgumentException("CardId는 중복될 수 없습니다.", nameof(cards));
                if (card.Edition == PlayerCardEdition.Normal && !normalSeasonIds.Add(card.PlayerSeasonId))
                    throw new ArgumentException("한 PlayerSeason에는 Normal 카드가 하나만 있어야 합니다.", nameof(cards));
                _cards[index] = card;
            }

            if (normalSeasonIds.Count != _seasonsById.Count)
                throw new ArgumentException("모든 PlayerSeason에는 Normal 카드가 있어야 합니다.", nameof(cards));
        }

        public IReadOnlyList<PlayerCardDefinition> Cards => _cards;

        public bool TryGetCard(string cardId, out PlayerCardDefinition card)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                card = null;
                return false;
            }
            return _cardsById.TryGetValue(cardId, out card);
        }

        public PlayerSeasonDefinition GetPlayerSeason(PlayerCardDefinition card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (!_seasonsById.TryGetValue(card.PlayerSeasonId, out PlayerSeasonDefinition season))
                throw new ArgumentException("카탈로그에 속하지 않은 카드입니다.", nameof(card));
            return season;
        }
    }

    /// <summary>Edition 능력치 수치를 Resolver에 주입하는 초기 밸런스다.</summary>
    public sealed class CardEditionBalanceTable
    {
        private readonly int[] _allStarBonusByCost;
        private readonly int[] _mvpAllBonusByCost;

        public CardEditionBalanceTable(
            IReadOnlyList<int> allStarBonusByCost,
            int goldenGloveBonus,
            IReadOnlyList<int> mvpAllBonusByCost)
        {
            _allStarBonusByCost = CopyCostValues(allStarBonusByCost, nameof(allStarBonusByCost));
            _mvpAllBonusByCost = CopyCostValues(mvpAllBonusByCost, nameof(mvpAllBonusByCost));
            if (goldenGloveBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(goldenGloveBonus));
            GoldenGloveBonus = goldenGloveBonus;
        }

        public int GoldenGloveBonus { get; }
        public int GetAllStarBonus(int cost) => GetCostValue(_allStarBonusByCost, cost);
        public int GetMvpAllBonus(int cost) => GetCostValue(_mvpAllBonusByCost, cost);

        public static CardEditionBalanceTable CreateInitial()
        {
            return new CardEditionBalanceTable(
                new[] { 0, 5, 5, 5, 5, 4, 4, 3, 3, 2, 2 },
                2,
                new[] { 0, 5, 5, 5, 5, 5, 4, 4, 4, 3, 3 });
        }

        private static int[] CopyCostValues(IReadOnlyList<int> values, string parameterName)
        {
            if (values == null || values.Count != 11)
                throw new ArgumentException("Cost 1~10과 미사용 0 인덱스 값이 필요합니다.", parameterName);
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] < 0)
                    throw new ArgumentOutOfRangeException(parameterName);
                copy[index] = values[index];
            }
            return copy;
        }

        private static int GetCostValue(int[] values, int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return values[cost];
        }
    }

    public enum ScoutType
    {
        General,
        Franchise,
        Year,
        YearFranchise,
        Award
    }

    /// <summary>Joint Bucket 스카우트의 필터, 가중치와 SP 가격을 보관한다.</summary>
    public sealed class ScoutPoolDefinition
    {
        private readonly double[] _costWeights;
        private readonly double[] _editionWeights;

        public ScoutPoolDefinition(
            string scoutPoolId,
            ScoutType scoutType,
            IReadOnlyList<double> costWeights,
            IReadOnlyList<double> editionWeights,
            int priceSp,
            string franchiseFilter = null,
            int? yearFilter = null,
            PlayerCardEdition? editionFilter = null)
        {
            if (string.IsNullOrWhiteSpace(scoutPoolId))
                throw new ArgumentException("ScoutPoolId는 비어 있을 수 없습니다.", nameof(scoutPoolId));
            if (priceSp < 0)
                throw new ArgumentOutOfRangeException(nameof(priceSp));
            if (yearFilter.HasValue && yearFilter.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(yearFilter));
            if (scoutType == ScoutType.Franchise && string.IsNullOrWhiteSpace(franchiseFilter))
                throw new ArgumentException("Franchise Scout에는 구단 필터가 필요합니다.", nameof(franchiseFilter));
            if (scoutType == ScoutType.Year && !yearFilter.HasValue)
                throw new ArgumentException("Year Scout에는 연도 필터가 필요합니다.", nameof(yearFilter));
            if (scoutType == ScoutType.YearFranchise &&
                (string.IsNullOrWhiteSpace(franchiseFilter) || !yearFilter.HasValue))
                throw new ArgumentException("YearFranchise Scout에는 구단과 연도 필터가 필요합니다.");
            if (scoutType == ScoutType.Award &&
                (!editionFilter.HasValue || editionFilter.Value == PlayerCardEdition.Normal))
                throw new ArgumentException("Award Scout에는 특수 Edition 필터가 필요합니다.", nameof(editionFilter));

            ScoutPoolId = scoutPoolId.Trim();
            ScoutType = scoutType;
            PriceSp = priceSp;
            FranchiseFilter = string.IsNullOrWhiteSpace(franchiseFilter) ? null : franchiseFilter.Trim();
            YearFilter = yearFilter;
            EditionFilter = editionFilter;
            _costWeights = CopyWeights(costWeights, 11, nameof(costWeights));
            _editionWeights = CopyWeights(editionWeights, 4, nameof(editionWeights));
        }

        public string ScoutPoolId { get; }
        public ScoutType ScoutType { get; }
        public int PriceSp { get; }
        public string FranchiseFilter { get; }
        public int? YearFilter { get; }
        public PlayerCardEdition? EditionFilter { get; }

        public double GetCostWeight(int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return _costWeights[cost];
        }

        public double GetEditionWeight(PlayerCardEdition edition)
        {
            return _editionWeights[(int)edition];
        }

        public static double[] CreateInitialCostWeights()
        {
            return new[] { 0d, 12d, 13d, 15d, 15d, 14d, 12d, 8d, 6d, 3.5d, 1.5d };
        }

        public static double[] CreateNormalOnlyEditionWeights()
        {
            return new[] { 100d, 0d, 0d, 0d };
        }

        public static double[] CreateStandardEditionWeights()
        {
            return new[] { 97d, 2d, 0.7d, 0.3d };
        }

        private static double[] CopyWeights(IReadOnlyList<double> weights, int count, string parameterName)
        {
            if (weights == null || weights.Count != count)
                throw new ArgumentException("가중치 개수가 올바르지 않습니다.", parameterName);
            var copy = new double[count];
            double sum = 0d;
            for (int index = 0; index < count; index++)
            {
                double value = weights[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                    throw new ArgumentOutOfRangeException(parameterName);
                copy[index] = value;
                sum += value;
            }
            if (sum <= 0d)
                throw new ArgumentException("하나 이상의 양수 가중치가 필요합니다.", parameterName);
            return copy;
        }
    }

    /// <summary>Phase별 Scout Edition 활성 경계를 명시한다.</summary>
    public sealed class ScoutFeaturePolicy
    {
        public ScoutFeaturePolicy(bool isAwardScoutEnabled, bool areSpecialEditionsEnabled)
        {
            if (isAwardScoutEnabled && !areSpecialEditionsEnabled)
                throw new ArgumentException("특수 Edition 없이 Award Scout를 활성화할 수 없습니다.");
            IsAwardScoutEnabled = isAwardScoutEnabled;
            AreSpecialEditionsEnabled = areSpecialEditionsEnabled;
        }

        public bool IsAwardScoutEnabled { get; }
        public bool AreSpecialEditionsEnabled { get; }

        public bool IsEditionEnabled(PlayerCardEdition edition)
        {
            return edition == PlayerCardEdition.Normal || AreSpecialEditionsEnabled;
        }

        public static ScoutFeaturePolicy Phase4NormalOnly => new ScoutFeaturePolicy(false, false);
        public static ScoutFeaturePolicy FullWorldAwards => new ScoutFeaturePolicy(true, true);
    }

    /// <summary>일반 Scout Pity 증가량과 집중 Scout 소비 조건을 정의한다.</summary>
    public sealed class ScoutPityBalanceTable
    {
        public ScoutPityBalanceTable(int gaugeGainPerScout, int threshold, int guaranteedMinimumCost)
        {
            if (gaugeGainPerScout <= 0 || threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(gaugeGainPerScout));
            if (guaranteedMinimumCost < 1 || guaranteedMinimumCost > 10)
                throw new ArgumentOutOfRangeException(nameof(guaranteedMinimumCost));
            GaugeGainPerScout = gaugeGainPerScout;
            Threshold = threshold;
            GuaranteedMinimumCost = guaranteedMinimumCost;
        }

        public int GaugeGainPerScout { get; }
        public int Threshold { get; }
        public int GuaranteedMinimumCost { get; }

        public static ScoutPityBalanceTable CreateInitial() => new ScoutPityBalanceTable(10, 100, 7);
    }

    /// <summary>구단주 모드 카드 한 장에만 귀속되는 DP 훈련 누적치다.</summary>
    public sealed class CardTrainingState
    {
        private readonly int[] _bonuses;

        public CardTrainingState()
        {
            _bonuses = new int[PlayerAbilityCatalog.AbilityCount];
        }

        public CardTrainingState(IReadOnlyList<int> bonuses)
        {
            if (bonuses == null || bonuses.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 훈련 누적치가 필요합니다.", nameof(bonuses));
            _bonuses = new int[bonuses.Count];
            for (int index = 0; index < bonuses.Count; index++)
            {
                if (bonuses[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(bonuses));
                _bonuses[index] = bonuses[index];
            }
        }

        public int GetBonus(PlayerAbility ability) => _bonuses[(int)ability];

        public void AddBonus(PlayerAbility ability, int amount)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { _bonuses[(int)ability] += amount; }
        }
    }

    /// <summary>구단주 모드 플레이어 구단에만 저장되는 카드 소유 상태다.</summary>
    public sealed class OwnedPlayerCardState
    {
        public const int MaximumEnhancementLevel = 5;

        public OwnedPlayerCardState(
            string cardId,
            int enhancementLevel = 0,
            int duplicateCount = 0,
            bool isLocked = false,
            bool isFavorite = false,
            CardTrainingState training = null)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (enhancementLevel < 0 || enhancementLevel > MaximumEnhancementLevel)
                throw new ArgumentOutOfRangeException(nameof(enhancementLevel));
            if (duplicateCount < 0)
                throw new ArgumentOutOfRangeException(nameof(duplicateCount));
            CardId = cardId.Trim();
            EnhancementLevel = enhancementLevel;
            DuplicateCount = duplicateCount;
            IsLocked = isLocked;
            IsFavorite = isFavorite;
            Training = training ?? new CardTrainingState();
        }

        public string CardId { get; }
        public int EnhancementLevel { get; private set; }
        public int DuplicateCount { get; private set; }
        public bool IsLocked { get; set; }
        public bool IsFavorite { get; set; }
        public CardTrainingState Training { get; }

        public void AddDuplicate(int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            checked { DuplicateCount += count; }
        }

        public bool TryConsumeDuplicate()
        {
            if (DuplicateCount <= 0)
                return false;
            DuplicateCount--;
            return true;
        }

        public void IncreaseEnhancement()
        {
            if (EnhancementLevel >= MaximumEnhancementLevel)
                throw new InvalidOperationException("강화는 +5를 넘을 수 없습니다.");
            EnhancementLevel++;
        }
    }

    /// <summary>구단주 모드 플레이어 구단 전용 Money/SP/DP와 Pity 진행 상태다.</summary>
    public sealed class ManagerEconomyState
    {
        public ManagerEconomyState(long money = 0, int scoutingPoints = 0, int developmentPoints = 0, int pityGauge = 0)
        {
            if (money < 0 || scoutingPoints < 0 || developmentPoints < 0 || pityGauge < 0)
                throw new ArgumentOutOfRangeException(nameof(money));
            Money = money;
            ScoutingPoints = scoutingPoints;
            DevelopmentPoints = developmentPoints;
            PityGauge = pityGauge;
        }

        public long Money { get; private set; }
        public int ScoutingPoints { get; private set; }
        public int DevelopmentPoints { get; private set; }
        public int PityGauge { get; private set; }

        public bool TrySpendMoney(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (Money < amount)
                return false;
            Money -= amount;
            return true;
        }

        public bool TrySpendScoutingPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (ScoutingPoints < amount)
                return false;
            ScoutingPoints -= amount;
            return true;
        }

        public bool TrySpendDevelopmentPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (DevelopmentPoints < amount)
                return false;
            DevelopmentPoints -= amount;
            return true;
        }

        public void AddScoutingPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { ScoutingPoints += amount; }
        }

        public void AddMoney(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { Money += amount; }
        }

        public void AddDevelopmentPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { DevelopmentPoints += amount; }
        }

        public void AddPityGauge(int amount, int threshold)
        {
            if (amount < 0 || threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            PityGauge = Math.Min(threshold, checked(PityGauge + amount));
        }

        public bool TryConsumePity(int threshold)
        {
            if (threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if (PityGauge < threshold)
                return false;
            PityGauge -= threshold;
            return true;
        }
    }

    public enum DuplicateAutoSalePolicy
    {
        Disabled,
        MaxDuplicateOnly
    }

    /// <summary>Cost와 Edition에 따른 중복 카드 SP 판매가를 데이터화한다.</summary>
    public sealed class CardSaleBalanceTable
    {
        private readonly int[] _baseSaleSpByCost;
        private readonly double[] _editionMultipliers;

        public CardSaleBalanceTable(IReadOnlyList<int> baseSaleSpByCost, IReadOnlyList<double> editionMultipliers)
        {
            if (baseSaleSpByCost == null || baseSaleSpByCost.Count != 11)
                throw new ArgumentException("Cost 1~10 판매가가 필요합니다.", nameof(baseSaleSpByCost));
            if (editionMultipliers == null || editionMultipliers.Count != 4)
                throw new ArgumentException("네 Edition의 판매 배율이 필요합니다.", nameof(editionMultipliers));
            _baseSaleSpByCost = new int[11];
            _editionMultipliers = new double[4];
            for (int index = 0; index < 11; index++)
            {
                if (baseSaleSpByCost[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(baseSaleSpByCost));
                _baseSaleSpByCost[index] = baseSaleSpByCost[index];
            }
            for (int index = 0; index < 4; index++)
            {
                if (editionMultipliers[index] < 0d || double.IsNaN(editionMultipliers[index]))
                    throw new ArgumentOutOfRangeException(nameof(editionMultipliers));
                _editionMultipliers[index] = editionMultipliers[index];
            }
        }

        public int GetBaseSaleSp(int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return _baseSaleSpByCost[cost];
        }

        public double GetEditionMultiplier(PlayerCardEdition edition) => _editionMultipliers[(int)edition];

        public static CardSaleBalanceTable CreateInitial()
        {
            return new CardSaleBalanceTable(
                new[] { 0, 3, 4, 6, 8, 10, 14, 20, 28, 40, 55 },
                new[] { 1d, 1.2d, 1.4d, 1.8d });
        }
    }

    /// <summary>한 능력치에 DP를 사용하는 카드 훈련 프로그램이다.</summary>
    public sealed class CardTrainingProgramDefinition
    {
        public CardTrainingProgramDefinition(string programId, PlayerAbility ability, int dpCostPerPoint, int maximumPointsPerSession)
        {
            if (string.IsNullOrWhiteSpace(programId))
                throw new ArgumentException("ProgramId는 비어 있을 수 없습니다.", nameof(programId));
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (dpCostPerPoint <= 0 || maximumPointsPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpCostPerPoint));
            ProgramId = programId.Trim();
            Ability = ability;
            DpCostPerPoint = dpCostPerPoint;
            MaximumPointsPerSession = maximumPointsPerSession;
        }

        public string ProgramId { get; }
        public PlayerAbility Ability { get; }
        public int DpCostPerPoint { get; }
        public int MaximumPointsPerSession { get; }
    }
}

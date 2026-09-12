using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>공통 PlayerSeason과 World Award로 Stable WorldCardCatalog를 생성한다.</summary>
    public static class WorldCardCatalogBuilder
    {
        public static WorldCardCatalog Build(
            IReadOnlyList<PlayerSeasonDefinition> playerSeasons,
            WorldAwardRecord awards,
            CardEditionBalanceTable balance,
            IReadOnlyList<PlayerPersonDefinition> playerPersons = null,
            IReadOnlyList<TeamSeasonDefinition> teamSeasons = null,
            BakedSpecialCardContent specialCards = null)
        {
            if (playerSeasons == null)
                throw new ArgumentNullException(nameof(playerSeasons));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var sortedSeasons = new List<PlayerSeasonDefinition>(playerSeasons.Count);
            for (int index = 0; index < playerSeasons.Count; index++)
                sortedSeasons.Add(playerSeasons[index] ?? throw new ArgumentException("null 선수 시즌이 있습니다.", nameof(playerSeasons)));
            sortedSeasons.Sort((left, right) => string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId));

            var cards = new List<PlayerCardDefinition>(sortedSeasons.Count * 2);
            var preferences = PreferredBattingOrderEvaluator.Evaluate(sortedSeasons, teamSeasons);
            for (int index = 0; index < sortedSeasons.Count; index++)
            {
                PlayerSeasonDefinition season = sortedSeasons[index];
                AddCard(cards, season, PlayerCardEdition.Normal, balance, preferences[season.PlayerSeasonId]);
                if (awards == null)
                    continue;
                if (awards.HasAward(season.PlayerSeasonId, WorldAwardType.AllStar))
                    AddCard(cards, season, PlayerCardEdition.AllStar, balance, preferences[season.PlayerSeasonId]);
                if (awards.HasAward(season.PlayerSeasonId, WorldAwardType.GoldenGlove))
                    AddCard(cards, season, PlayerCardEdition.GoldenGlove, balance, preferences[season.PlayerSeasonId]);
                if (HasMvpAward(awards, season.PlayerSeasonId))
                    AddCard(cards, season, PlayerCardEdition.Mvp, balance, preferences[season.PlayerSeasonId]);
            }
            if (specialCards != null)
                foreach (var card in specialCards.Cards)
                {
                    var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
                    for (int index = 0; index < modifiers.Length; index++)
                        modifiers[index] = card.GetModifier((PlayerAbility)index);
                    cards.Add(new PlayerCardDefinition(card.CardId, card.PlayerSeasonId, card.Edition, modifiers,
                        preferences[card.PlayerSeasonId], card.TeamColorLineageId));
                }
            return new WorldCardCatalog(sortedSeasons, cards, playerPersons, specialCards?.Lineages, specialCards?.Recipes,
                ResolveActiveRosterSeasonIds(cards, teamSeasons));
        }

        /// <summary>구단 연도 Core25 카드가 가리키는 선수 시즌을 1군 명단으로 모은다. 구단 정보가 없으면 null이다.</summary>
        private static List<string> ResolveActiveRosterSeasonIds(
            List<PlayerCardDefinition> cards,
            IReadOnlyList<TeamSeasonDefinition> teamSeasons)
        {
            if (teamSeasons == null)
                return null;
            var seasonIdByCardId = new Dictionary<string, string>(cards.Count, StringComparer.Ordinal);
            for (int index = 0; index < cards.Count; index++)
                seasonIdByCardId[cards[index].CardId] = cards[index].PlayerSeasonId;

            var seasonIds = new List<string>(teamSeasons.Count * 25);
            for (int teamIndex = 0; teamIndex < teamSeasons.Count; teamIndex++)
            {
                IReadOnlyList<string> core = teamSeasons[teamIndex].Core25CardIds;
                for (int index = 0; index < core.Count; index++)
                {
                    if (!seasonIdByCardId.TryGetValue(core[index], out string seasonId))
                        throw new ArgumentException("Core25가 카탈로그에 없는 카드를 참조합니다.", nameof(teamSeasons));
                    seasonIds.Add(seasonId);
                }
            }
            return seasonIds;
        }

        private static bool HasMvpAward(WorldAwardRecord awards, string playerSeasonId)
        {
            return awards.HasAward(playerSeasonId, WorldAwardType.RegularSeasonMvp) ||
                   awards.HasAward(playerSeasonId, WorldAwardType.AllStarGameMvp) ||
                   awards.HasAward(playerSeasonId, WorldAwardType.PostseasonMvp);
        }

        private static void AddCard(
            List<PlayerCardDefinition> cards,
            PlayerSeasonDefinition season,
            PlayerCardEdition edition,
            CardEditionBalanceTable balance,
            PreferredBattingOrder preference)
        {
            cards.Add(new PlayerCardDefinition(
                PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, edition),
                season.PlayerSeasonId,
                edition,
                CreateModifiers(season, edition, balance), preference));
        }

        private static int[] CreateModifiers(
            PlayerSeasonDefinition season,
            PlayerCardEdition edition,
            CardEditionBalanceTable balance)
        {
            var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
            bool isHitter = season.PlayerType == PlayerType.Batter;
            switch (edition)
            {
                case PlayerCardEdition.Normal:
                    break;
                case PlayerCardEdition.AllStar:
                    int allStarBonus = balance.GetAllStarBonus(season.Cost);
                    if (isHitter)
                    {
                        modifiers[(int)PlayerAbility.Contact] = allStarBonus;
                        modifiers[(int)PlayerAbility.Speed] = allStarBonus;
                    }
                    else
                    {
                        modifiers[(int)PlayerAbility.Velocity] = allStarBonus;
                        modifiers[(int)PlayerAbility.Control] = allStarBonus;
                    }
                    break;
                case PlayerCardEdition.GoldenGlove:
                    if (isHitter)
                    {
                        modifiers[(int)PlayerAbility.Power] = balance.GoldenGloveBonus;
                        modifiers[(int)PlayerAbility.Defense] = balance.GoldenGloveBonus;
                    }
                    else
                    {
                        modifiers[(int)PlayerAbility.Stuff] = balance.GoldenGloveBonus;
                        modifiers[(int)PlayerAbility.Breaking] = balance.GoldenGloveBonus;
                    }
                    break;
                case PlayerCardEdition.Mvp:
                    int mvpBonus = balance.GetMvpAllBonus(season.Cost);
                    for (int abilityIndex = 0; abilityIndex < PlayerAbilityCatalog.AbilityCount; abilityIndex++)
                    {
                        var ability = (PlayerAbility)abilityIndex;
                        if ((isHitter && PlayerAbilityCatalog.IsBatterAbility(ability)) ||
                            (!isHitter && PlayerAbilityCatalog.IsPitcherAbility(ability)))
                            modifiers[abilityIndex] = mvpBonus;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(edition));
            }
            return modifiers;
        }
    }

    /// <summary>실제 존재하는 Joint Bucket 하나의 재정규화 확률이다.</summary>
    public readonly struct ScoutBucketProbability
    {
        public ScoutBucketProbability(int cost, PlayerCardEdition edition, double probability, int candidateCount)
        {
            Cost = cost;
            Edition = edition;
            Probability = probability;
            CandidateCount = candidateCount;
        }

        public int Cost { get; }
        public PlayerCardEdition Edition { get; }
        public double Probability { get; }
        public int CandidateCount { get; }
    }

    /// <summary>WorldCardCatalog만 소비해 Joint Bucket Scout를 결정론적으로 수행한다.</summary>
    public sealed class ScoutRoller
    {
        private sealed class Bucket
        {
            public int Cost;
            public PlayerCardEdition Edition;
            public double RawWeight;
            public readonly List<PlayerCardDefinition> Cards = new List<PlayerCardDefinition>();
        }

        public IReadOnlyList<ScoutBucketProbability> GetProbabilities(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy)
        {
            List<Bucket> buckets = BuildBuckets(pool, catalog, featurePolicy);
            double totalWeight = 0d;
            for (int index = 0; index < buckets.Count; index++)
                totalWeight += buckets[index].RawWeight;
            var result = new ScoutBucketProbability[buckets.Count];
            for (int index = 0; index < buckets.Count; index++)
            {
                Bucket bucket = buckets[index];
                result[index] = new ScoutBucketProbability(
                    bucket.Cost,
                    bucket.Edition,
                    bucket.RawWeight / totalWeight,
                    bucket.Cards.Count);
            }
            return result;
        }

        /// <summary>카드 한 장이 실제 Scout Bucket에 포함되는지 추첨과 같은 규칙으로 판정한다.</summary>
        public static bool IsCandidate(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            PlayerCardDefinition card)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (featurePolicy == null) throw new ArgumentNullException(nameof(featurePolicy));
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (!card.CanAcquireFromScout)
                return false;
            if (pool.ScoutType == ScoutType.Award && !featurePolicy.IsAwardScoutEnabled)
                return false;
            if (!featurePolicy.IsEditionEnabled(card.Edition))
                return false;
            if (pool.EditionFilter.HasValue && pool.EditionFilter.Value != card.Edition)
                return false;

            PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
            if (pool.FranchiseFilter != null &&
                !string.Equals(pool.FranchiseFilter, season.OriginFranchiseId, StringComparison.Ordinal))
                return false;
            if (pool.YearFilter.HasValue && pool.YearFilter.Value != season.OriginYear)
                return false;
            if (pool.RosterScope == ScoutRosterScope.ActiveRoster && !catalog.IsActiveRosterSeason(season.PlayerSeasonId))
                return false;
            return pool.GetCostWeight(season.Cost) * pool.GetEditionWeight(card.Edition) > 0d;
        }

        public PlayerCardDefinition Roll(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            List<Bucket> buckets = BuildBuckets(pool, catalog, featurePolicy);
            double totalWeight = 0d;
            for (int index = 0; index < buckets.Count; index++)
                totalWeight += buckets[index].RawWeight;

            double bucketRoll = RequireUnitRandom(random.NextDouble()) * totalWeight;
            Bucket selected = buckets[buckets.Count - 1];
            double cumulative = 0d;
            for (int index = 0; index < buckets.Count; index++)
            {
                cumulative += buckets[index].RawWeight;
                if (bucketRoll < cumulative)
                {
                    selected = buckets[index];
                    break;
                }
            }
            int cardIndex = (int)(RequireUnitRandom(random.NextDouble()) * selected.Cards.Count);
            return selected.Cards[cardIndex];
        }

        public PlayerCardDefinition RollAndSpend(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            ScoutPityBalanceTable pityBalance,
            ManagerEconomyState economy,
            IRandomSource random)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (pityBalance == null)
                throw new ArgumentNullException(nameof(pityBalance));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (economy.ScoutingPoints < pool.PriceSp)
                throw new InvalidOperationException("스카우트에 필요한 SP가 부족합니다.");

            PlayerCardDefinition card = Roll(pool, catalog, featurePolicy, random);
            if (!economy.TrySpendScoutingPoints(pool.PriceSp))
                throw new InvalidOperationException("SP 소비에 실패했습니다.");
            economy.AddPityGauge(pityBalance.GetGaugeGain(pool), pityBalance.Threshold);
            return card;
        }

        /// <summary>
        /// Pity 게이지로 여는 보장 영입이다. 게이지 소비는 호출자(지갑)가 맡고, 여기서는 후보만 확정한다.
        /// 후보는 풀과 같은 범위의 Normal 카드이며 아래 순서로 처음 비지 않은 단계에서 균등 추첨한다.
        /// 1) 아직 보유하지 않은 Cost 보장선 이상 선수 2) 아직 보유하지 않은 선수 3) Cost 보장선 이상 전체.
        /// 수집의 마지막 몇 장이 전체 기간 대부분을 차지하는 꼬리를 끊으려고 "미보유 우선"을 둔다.
        /// </summary>
        public PlayerCardDefinition RollGuaranteed(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            ScoutPityBalanceTable pityBalance,
            Func<string, bool> isPlayerSeasonOwned,
            IRandomSource random)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (featurePolicy == null) throw new ArgumentNullException(nameof(featurePolicy));
            if (pityBalance == null) throw new ArgumentNullException(nameof(pityBalance));
            if (isPlayerSeasonOwned == null) throw new ArgumentNullException(nameof(isPlayerSeasonOwned));
            if (random == null) throw new ArgumentNullException(nameof(random));

            List<PlayerCardDefinition> candidates = CollectGuaranteedCandidates(
                pool, catalog, featurePolicy, pityBalance, isPlayerSeasonOwned);
            if (candidates.Count == 0)
                throw new InvalidOperationException("보장 영입 후보가 없습니다.");
            int selectedIndex = (int)(RequireUnitRandom(random.NextDouble()) * candidates.Count);
            return candidates[selectedIndex];
        }

        /// <summary>보장 영입이 실제로 고를 후보 단계와 후보 목록을 추첨과 같은 규칙으로 돌려준다.</summary>
        public static List<PlayerCardDefinition> CollectGuaranteedCandidates(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            ScoutPityBalanceTable pityBalance,
            Func<string, bool> isPlayerSeasonOwned)
        {
            var unownedHighCost = new List<PlayerCardDefinition>();
            var unowned = new List<PlayerCardDefinition>();
            var highCost = new List<PlayerCardDefinition>();
            var all = new List<PlayerCardDefinition>();
            IReadOnlyList<PlayerCardDefinition> cards = catalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index];
                if (card.Edition != PlayerCardEdition.Normal || !IsCandidate(pool, catalog, featurePolicy, card))
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                bool isHighCost = season.Cost >= pityBalance.GuaranteedMinimumCost;
                bool isOwned = isPlayerSeasonOwned(season.PlayerSeasonId);
                if (isHighCost && !isOwned) unownedHighCost.Add(card);
                if (!isOwned) unowned.Add(card);
                if (isHighCost) highCost.Add(card);
                all.Add(card);
            }
            List<PlayerCardDefinition> selected = unownedHighCost.Count > 0 ? unownedHighCost
                : unowned.Count > 0 ? unowned
                : highCost.Count > 0 ? highCost
                : all;
            selected.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            return selected;
        }

        private static List<Bucket> BuildBuckets(
            ScoutPoolDefinition pool,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (featurePolicy == null)
                throw new ArgumentNullException(nameof(featurePolicy));
            if (pool.ScoutType == ScoutType.Award && !featurePolicy.IsAwardScoutEnabled)
                throw new InvalidOperationException("현재 Phase에서는 Award Scout가 비활성화되어 있습니다.");

            var byKey = new Dictionary<int, Bucket>();
            IReadOnlyList<PlayerCardDefinition> cards = catalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index];
                if (!IsCandidate(pool, catalog, featurePolicy, card))
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);

                double weight = pool.GetCostWeight(season.Cost) * pool.GetEditionWeight(card.Edition);
                if (weight <= 0d)
                    continue;
                int key = season.Cost * 8 + (int)card.Edition;
                if (!byKey.TryGetValue(key, out Bucket bucket))
                {
                    bucket = new Bucket { Cost = season.Cost, Edition = card.Edition, RawWeight = weight };
                    byKey.Add(key, bucket);
                }
                bucket.Cards.Add(card);
            }

            var buckets = new List<Bucket>(byKey.Values);
            buckets.Sort((left, right) =>
            {
                int costComparison = left.Cost.CompareTo(right.Cost);
                return costComparison != 0 ? costComparison : left.Edition.CompareTo(right.Edition);
            });
            for (int index = 0; index < buckets.Count; index++)
                buckets[index].Cards.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            if (buckets.Count == 0)
                throw new InvalidOperationException("Scout 조건에 맞는 카드 후보가 없습니다.");
            return buckets;
        }

        private static double RequireUnitRandom(double value)
        {
            if (value < 0d || value >= 1d || double.IsNaN(value))
                throw new InvalidOperationException("IRandomSource는 0 이상 1 미만의 값을 반환해야 합니다.");
            return value;
        }
    }

    public enum CardEnhancementResult
    {
        Enhanced,
        NoDuplicate,
        MaximumLevel
    }

    /// <summary>강화 Command와 같은 조건으로 현재 단계와 재료 소비 가능 여부를 설명한다.</summary>
    public readonly struct CardEnhancementPreview
    {
        public CardEnhancementPreview(
            int currentLevel,
            int nextLevel,
            int duplicateCount,
            CardEnhancementResult result)
        {
            CurrentLevel = currentLevel;
            NextLevel = nextLevel;
            DuplicateCount = duplicateCount;
            Result = result;
        }

        public int CurrentLevel { get; }
        public int NextLevel { get; }
        public int DuplicateCount { get; }
        public CardEnhancementResult Result { get; }
        public bool CanEnhance => Result == CardEnhancementResult.Enhanced;
    }

    /// <summary>중복 한 장을 소비해 실패 없이 최대 +5까지 강화한다.</summary>
    public static class CardEnhancementResolver
    {
        public static CardEnhancementPreview Preview(OwnedPlayerCardState ownedCard)
        {
            if (ownedCard == null)
                throw new ArgumentNullException(nameof(ownedCard));
            CardEnhancementResult result = ownedCard.EnhancementLevel >= OwnedPlayerCardState.MaximumEnhancementLevel
                ? CardEnhancementResult.MaximumLevel
                : ownedCard.DuplicateCount <= 0
                    ? CardEnhancementResult.NoDuplicate
                    : CardEnhancementResult.Enhanced;
            return new CardEnhancementPreview(
                ownedCard.EnhancementLevel,
                result == CardEnhancementResult.Enhanced
                    ? ownedCard.EnhancementLevel + 1
                    : ownedCard.EnhancementLevel,
                ownedCard.DuplicateCount,
                result);
        }

        public static CardEnhancementResult Enhance(OwnedPlayerCardState ownedCard)
        {
            if (ownedCard == null)
                throw new ArgumentNullException(nameof(ownedCard));
            if (ownedCard.EnhancementLevel >= OwnedPlayerCardState.MaximumEnhancementLevel)
                return CardEnhancementResult.MaximumLevel;
            if (!ownedCard.TryConsumeDuplicate())
                return CardEnhancementResult.NoDuplicate;
            ownedCard.IncreaseEnhancement();
            return CardEnhancementResult.Enhanced;
        }
    }

    /// <summary>중복 판매 Command가 소비할 수량과 지급할 SP를 상태 변경 없이 계산한다.</summary>
    public readonly struct CardSalePreview
    {
        public CardSalePreview(int requestedCount, int availableCount, int unitPriceSp, int totalPriceSp)
        {
            RequestedCount = requestedCount;
            AvailableCount = availableCount;
            UnitPriceSp = unitPriceSp;
            TotalPriceSp = totalPriceSp;
        }

        public int RequestedCount { get; }
        public int AvailableCount { get; }
        public int UnitPriceSp { get; }
        public int TotalPriceSp { get; }
        public bool CanSell => RequestedCount > 0 && RequestedCount <= AvailableCount;
    }

    /// <summary>Cost와 Edition 판매 배율만 사용해 중복 카드 판매 SP를 정산한다.</summary>
    public static class CardSaleResolver
    {
        public static CardSalePreview Preview(
            OwnedPlayerCardState ownedCard,
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            CardSaleBalanceTable balance,
            int count)
        {
            if (ownedCard == null)
                throw new ArgumentNullException(nameof(ownedCard));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!string.Equals(ownedCard.CardId, card?.CardId, StringComparison.Ordinal))
                throw new ArgumentException("소유 상태와 카드가 일치하지 않습니다.", nameof(card));
            int unitPrice = CalculateSaleSp(card, season, balance);
            int totalPrice = count <= ownedCard.DuplicateCount ? checked(unitPrice * count) : 0;
            return new CardSalePreview(count, ownedCard.DuplicateCount, unitPrice, totalPrice);
        }

        public static int CalculateSaleSp(
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            CardSaleBalanceTable balance)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (season == null)
                throw new ArgumentNullException(nameof(season));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (!string.Equals(card.PlayerSeasonId, season.PlayerSeasonId, StringComparison.Ordinal))
                throw new ArgumentException("카드와 PlayerSeason이 일치하지 않습니다.", nameof(season));
            return (int)Math.Floor(balance.GetBaseSaleSp(season.Cost) * balance.GetEditionMultiplier(card.Edition));
        }

        public static int SellDuplicates(
            OwnedPlayerCardState ownedCard,
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            CardSaleBalanceTable balance,
            ManagerEconomyState economy,
            int count)
        {
            if (ownedCard == null)
                throw new ArgumentNullException(nameof(ownedCard));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!string.Equals(ownedCard.CardId, card?.CardId, StringComparison.Ordinal))
                throw new ArgumentException("소유 상태와 카드가 일치하지 않습니다.", nameof(card));
            if (ownedCard.DuplicateCount < count)
                throw new InvalidOperationException("판매할 중복 카드가 부족합니다.");

            int unitPrice = CalculateSaleSp(card, season, balance);
            for (int index = 0; index < count; index++)
                ownedCard.TryConsumeDuplicate();
            int total = checked(unitPrice * count);
            economy.AddScoutingPoints(total);
            return total;
        }

        public static int AutoSellIfApplicable(
            DuplicateAutoSalePolicy policy,
            OwnedPlayerCardState ownedCard,
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            CardSaleBalanceTable balance,
            ManagerEconomyState economy)
        {
            if (policy != DuplicateAutoSalePolicy.MaxDuplicateOnly ||
                ownedCard == null ||
                ownedCard.EnhancementLevel < OwnedPlayerCardState.MaximumEnhancementLevel ||
                ownedCard.DuplicateCount <= 0)
                return 0;
            return SellDuplicates(ownedCard, card, season, balance, economy, ownedCard.DuplicateCount);
        }
    }

    public readonly struct CardTrainingResult
    {
        public CardTrainingResult(PlayerAbility ability, int gainedPoints, int spentDp)
        {
            Ability = ability;
            GainedPoints = gainedPoints;
            SpentDp = spentDp;
        }

        public PlayerAbility Ability { get; }
        public int GainedPoints { get; }
        public int SpentDp { get; }
    }

    public readonly struct CardTrainingPreview
    {
        public CardTrainingPreview(PlayerAbility ability, int current, int ceiling, int gainedPoints, int dpCost)
        {
            Ability = ability;
            Current = current;
            Ceiling = ceiling;
            GainedPoints = gainedPoints;
            DpCost = dpCost;
        }
        public PlayerAbility Ability { get; }
        public int Current { get; }
        public int Ceiling { get; }
        public int GainedPoints { get; }
        public int DpCost { get; }
        public bool CanTrain => GainedPoints > 0;
    }

    /// <summary>나이·노화 입력 없이 PlayerSeason TrainingCeiling까지만 DP 훈련을 적용한다.</summary>
    public static class CardTrainingResolver
    {
        public static CardTrainingPreview Preview(
            OwnedPlayerCardState ownedCard,
            PlayerSeasonDefinition season,
            CardTrainingProgramDefinition program,
            ManagerEconomyState economy,
            StaffTrainingEfficiencyResult staffEfficiency)
        {
            if (ownedCard == null || season == null || program == null || economy == null)
                throw new ArgumentNullException(nameof(ownedCard));
            int baseRating = season.CreateBaseAttributes().Get(program.Ability);
            int ceiling = season.CreateTrainingCeiling().Get(program.Ability);
            int current = baseRating + ownedCard.Training.GetBonus(program.Ability);
            int effectiveDpCost = Math.Max(1,
                (int)Math.Ceiling(program.DpCostPerPoint / staffEfficiency.EfficiencyMultiplier));
            int gained = Math.Min(program.MaximumPointsPerSession,
                Math.Min(Math.Max(0, ceiling - current), economy.DevelopmentPoints / effectiveDpCost));
            return new CardTrainingPreview(program.Ability, current, ceiling, gained, gained * effectiveDpCost);
        }

        public static CardTrainingResult Train(
            OwnedPlayerCardState ownedCard,
            PlayerSeasonDefinition season,
            CardTrainingProgramDefinition program,
            ManagerEconomyState economy)
        {
            return Train(
                ownedCard,
                season,
                program,
                economy,
                new StaffTrainingEfficiencyResult(1d));
        }

        /// <summary>스태프의 훈련 효율을 DP 비용에만 반영하고 TrainingCeiling은 그대로 지킨다.</summary>
        public static CardTrainingResult Train(
            OwnedPlayerCardState ownedCard,
            PlayerSeasonDefinition season,
            CardTrainingProgramDefinition program,
            ManagerEconomyState economy,
            StaffTrainingEfficiencyResult staffEfficiency)
        {
            if (ownedCard == null)
                throw new ArgumentNullException(nameof(ownedCard));
            if (season == null)
                throw new ArgumentNullException(nameof(season));
            if (program == null)
                throw new ArgumentNullException(nameof(program));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (!IsCardForSeason(ownedCard.CardId, season.PlayerSeasonId))
                throw new ArgumentException("소유 카드와 PlayerSeason이 일치하지 않습니다.", nameof(season));

            CardTrainingPreview preview = Preview(ownedCard, season, program, economy, staffEfficiency);
            int gainedPoints = preview.GainedPoints;
            int spentDp = preview.DpCost;
            if (spentDp > 0)
            {
                if (!economy.TrySpendDevelopmentPoints(spentDp))
                    throw new InvalidOperationException("DP 소비에 실패했습니다.");
                ownedCard.Training.AddBonus(program.Ability, gainedPoints);
            }
            return new CardTrainingResult(program.Ability, gainedPoints, spentDp);
        }

        private static bool IsCardForSeason(string cardId, string playerSeasonId)
        {
            Array editions = Enum.GetValues(typeof(PlayerCardEdition));
            for (int index = 0; index < editions.Length; index++)
            {
                var edition = (PlayerCardEdition)editions.GetValue(index);
                if (string.Equals(cardId, PlayerCardDefinition.CreateStableCardId(playerSeasonId, edition), StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }

    /// <summary>AI에는 Owned 경제를 전달하지 않고 World에서 활성화된 공통 카드만 노출한다.</summary>
    public sealed class AiEditionUnlockPolicy
    {
        public IReadOnlyList<PlayerCardDefinition> GetAvailableCards(WorldCardCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            return catalog.Cards;
        }
    }

    /// <summary>공통 Validator를 통과한 CurrentRoster 후보 중 AI 평가가 가장 높은 하나를 고른다.</summary>
    public sealed class AiRosterOptimizer
    {
        private readonly ActiveRosterValidator _validator;

        public AiRosterOptimizer(ActiveRosterValidator validator = null)
        {
            _validator = validator ?? new ActiveRosterValidator();
        }

        public CurrentRosterState SelectBest(IReadOnlyList<AiRosterCandidate> candidates)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            CurrentRosterState best = null;
            double bestScore = double.NegativeInfinity;
            string bestTieBreak = null;
            for (int index = 0; index < candidates.Count; index++)
            {
                AiRosterCandidate candidate = candidates[index];
                if (candidate.Roster == null || !_validator.Validate(candidate.Roster).IsValid)
                    continue;
                string tieBreak = CreateTieBreak(candidate.Roster);
                if (best == null || candidate.Score > bestScore ||
                    (candidate.Score.Equals(bestScore) && string.CompareOrdinal(tieBreak, bestTieBreak) < 0))
                {
                    best = candidate.Roster;
                    bestScore = candidate.Score;
                    bestTieBreak = tieBreak;
                }
            }
            if (best == null)
                throw new InvalidOperationException("공통 ActiveRoster 규칙을 만족하는 AI 후보가 없습니다.");
            return best;
        }

        private static string CreateTieBreak(CurrentRosterState roster)
        {
            var ids = new string[roster.Entries.Count];
            for (int index = 0; index < roster.Entries.Count; index++)
                ids[index] = roster.Entries[index].CardId;
            Array.Sort(ids, StringComparer.Ordinal);
            return roster.TeamSeasonKey + ":" + string.Join("|", ids);
        }
    }

    /// <summary>TeamColor·ClubDNA·포지션 비용까지 외부에서 합산한 AI 로스터 후보 점수다.</summary>
    public readonly struct AiRosterCandidate
    {
        public AiRosterCandidate(CurrentRosterState roster, double score)
        {
            if (double.IsNaN(score) || double.IsInfinity(score))
                throw new ArgumentOutOfRangeException(nameof(score));
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            Score = score;
        }

        public CurrentRosterState Roster { get; }
        public double Score { get; }
    }
}

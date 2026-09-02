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
            CardEditionBalanceTable balance)
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
            for (int index = 0; index < sortedSeasons.Count; index++)
            {
                PlayerSeasonDefinition season = sortedSeasons[index];
                AddCard(cards, season, PlayerCardEdition.Normal, balance);
                if (awards == null)
                    continue;
                if (awards.HasAward(season.PlayerSeasonId, WorldAwardType.AllStar))
                    AddCard(cards, season, PlayerCardEdition.AllStar, balance);
                if (awards.HasAward(season.PlayerSeasonId, WorldAwardType.GoldenGlove))
                    AddCard(cards, season, PlayerCardEdition.GoldenGlove, balance);
                if (HasMvpAward(awards, season.PlayerSeasonId))
                    AddCard(cards, season, PlayerCardEdition.Mvp, balance);
            }
            return new WorldCardCatalog(sortedSeasons, cards);
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
            CardEditionBalanceTable balance)
        {
            cards.Add(new PlayerCardDefinition(
                PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, edition),
                season.PlayerSeasonId,
                edition,
                CreateModifiers(season, edition, balance)));
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
            economy.AddPityGauge(pityBalance.GaugeGainPerScout, pityBalance.Threshold);
            return card;
        }

        public PlayerCardDefinition RollFocused(
            string franchiseFilter,
            int? yearFilter,
            WorldCardCatalog catalog,
            ScoutFeaturePolicy featurePolicy,
            ScoutPityBalanceTable pityBalance,
            ManagerEconomyState economy,
            IRandomSource random)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (featurePolicy == null)
                throw new ArgumentNullException(nameof(featurePolicy));
            if (pityBalance == null)
                throw new ArgumentNullException(nameof(pityBalance));
            if (economy == null)
                throw new ArgumentNullException(nameof(economy));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (string.IsNullOrWhiteSpace(franchiseFilter) && !yearFilter.HasValue)
                throw new ArgumentException("집중 Scout에는 구단 또는 연도 필터가 필요합니다.");
            if (economy.PityGauge < pityBalance.Threshold)
                throw new InvalidOperationException("Pity Gauge가 부족합니다.");

            var candidates = new List<PlayerCardDefinition>();
            IReadOnlyList<PlayerCardDefinition> cards = catalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index];
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (season.Cost < pityBalance.GuaranteedMinimumCost || !featurePolicy.IsEditionEnabled(card.Edition))
                    continue;
                if (!string.IsNullOrWhiteSpace(franchiseFilter) &&
                    !string.Equals(franchiseFilter.Trim(), season.OriginFranchiseId, StringComparison.Ordinal))
                    continue;
                if (yearFilter.HasValue && yearFilter.Value != season.OriginYear)
                    continue;
                candidates.Add(card);
            }
            if (candidates.Count == 0)
                throw new InvalidOperationException("선택한 조건에 Cost 보장 후보가 없습니다.");
            candidates.Sort((left, right) => string.CompareOrdinal(left.CardId, right.CardId));
            int selectedIndex = (int)(RequireUnitRandom(random.NextDouble()) * candidates.Count);
            if (!economy.TryConsumePity(pityBalance.Threshold))
                throw new InvalidOperationException("Pity Gauge 소비에 실패했습니다.");
            return candidates[selectedIndex];
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
                if (!featurePolicy.IsEditionEnabled(card.Edition))
                    continue;
                if (pool.EditionFilter.HasValue && pool.EditionFilter.Value != card.Edition)
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (pool.FranchiseFilter != null &&
                    !string.Equals(pool.FranchiseFilter, season.OriginFranchiseId, StringComparison.Ordinal))
                    continue;
                if (pool.YearFilter.HasValue && pool.YearFilter.Value != season.OriginYear)
                    continue;

                double weight = pool.GetCostWeight(season.Cost) * pool.GetEditionWeight(card.Edition);
                if (weight <= 0d)
                    continue;
                int key = season.Cost * 4 + (int)card.Edition;
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

    /// <summary>중복 한 장을 소비해 실패 없이 최대 +5까지 강화한다.</summary>
    public static class CardEnhancementResolver
    {
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

    /// <summary>Cost와 Edition 판매 배율만 사용해 중복 카드 판매 SP를 정산한다.</summary>
    public static class CardSaleResolver
    {
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

    /// <summary>나이·노화 입력 없이 PlayerSeason TrainingCeiling까지만 DP 훈련을 적용한다.</summary>
    public static class CardTrainingResolver
    {
        public static CardTrainingResult Train(
            OwnedPlayerCardState ownedCard,
            PlayerSeasonDefinition season,
            CardTrainingProgramDefinition program,
            ManagerEconomyState economy)
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

            int baseRating = season.CreateBaseAttributes().Get(program.Ability);
            int ceiling = season.CreateTrainingCeiling().Get(program.Ability);
            int current = baseRating + ownedCard.Training.GetBonus(program.Ability);
            int remainingHeadroom = Math.Max(0, ceiling - current);
            int affordablePoints = economy.DevelopmentPoints / program.DpCostPerPoint;
            int gainedPoints = Math.Min(program.MaximumPointsPerSession, Math.Min(remainingHeadroom, affordablePoints));
            int spentDp = gainedPoints * program.DpCostPerPoint;
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

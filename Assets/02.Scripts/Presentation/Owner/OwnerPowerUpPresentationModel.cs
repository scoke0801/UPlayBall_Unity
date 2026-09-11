using System;
using System.Collections.Generic;
using System.Globalization;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Shop;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Presentation.Shop;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    internal static class OwnerPowerUpSnapshotCopy
    {
        public static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>전력보강 Content가 표시할 동기 조회 상태다.</summary>
    public enum OwnerPowerUpContentState
    {
        Ready,
        Empty,
        Error
    }

    /// <summary>Cost·Edition Bucket 하나의 실제 스카우트 확률 표시값이다.</summary>
    public readonly struct OwnerScoutProbabilitySnapshot
    {
        public OwnerScoutProbabilitySnapshot(string label, double probability)
        {
            Label = label ?? string.Empty;
            Probability = probability;
        }

        public string Label { get; }
        public double Probability { get; }
        public string ProbabilityText => Probability.ToString("P2", CultureInfo.InvariantCulture);
    }

    /// <summary>스카우트 상품의 Quote와 후보 범위, Pity를 표시하는 불변 Snapshot이다.</summary>
    public sealed class OwnerScoutProductSnapshot
    {
        private readonly Lazy<OwnerScoutProbabilitySnapshot[]> _probabilities;
        private readonly Lazy<OwnerScoutCandidateSummary> _candidateSummary;

        public OwnerScoutProductSnapshot(
            string productId,
            string title,
            string scope,
            string priceText,
            bool canPurchase,
            string blockedReason,
            int drawCount,
            int pityGauge,
            int pityThreshold,
            int pityGainPerDraw,
            int guaranteedMinimumCost,
            IReadOnlyList<OwnerScoutProbabilitySnapshot> probabilities,
            Func<IReadOnlyList<OwnerScoutProbabilitySnapshot>> probabilityResolver = null,
            Func<OwnerScoutCandidateSummary> candidateSummaryResolver = null)
        {
            ProductId = productId ?? string.Empty;
            Title = title ?? string.Empty;
            Scope = scope ?? string.Empty;
            PriceText = priceText ?? string.Empty;
            CanPurchase = canPurchase;
            BlockedReason = blockedReason ?? string.Empty;
            DrawCount = drawCount;
            PityGauge = pityGauge;
            PityThreshold = pityThreshold;
            PityGainPerDraw = pityGainPerDraw;
            GuaranteedMinimumCost = guaranteedMinimumCost;
            // 월드의 모든 연도·구단 상품을 나열할 때 후보군 전체를 상품마다 순회하지 않는다.
            // Resolver는 이 Snapshot과 수명이 같은 상점의 정적 카탈로그만 조회한다.
            OwnerScoutProbabilitySnapshot[] copy = OwnerPowerUpSnapshotCopy.Copy(probabilities);
            _probabilities = new Lazy<OwnerScoutProbabilitySnapshot[]>(() => probabilityResolver == null
                ? copy : OwnerPowerUpSnapshotCopy.Copy(probabilityResolver()));
            _candidateSummary = new Lazy<OwnerScoutCandidateSummary>(() =>
                candidateSummaryResolver == null ? default : candidateSummaryResolver());
        }

        public string ProductId { get; }
        public string Title { get; }
        public string Scope { get; }
        public string PriceText { get; }
        public bool CanPurchase { get; }
        public string BlockedReason { get; }
        public int DrawCount { get; }
        public int PityGauge { get; }
        public int PityThreshold { get; }
        public int PityGainPerDraw { get; }
        public int GuaranteedMinimumCost { get; }
        public IReadOnlyList<OwnerScoutProbabilitySnapshot> Probabilities => _probabilities.Value;
        public int CandidateCount => _candidateSummary.Value.CandidateCount;
        public int WishlistCandidateCount => _candidateSummary.Value.WishlistCandidateCount;
    }

    /// <summary>Scout 후보군과 그 안에 포함된 위시 카드 수를 확률에 영향 없이 표시한다.</summary>
    public readonly struct OwnerScoutCandidateSummary
    {
        public OwnerScoutCandidateSummary(int candidateCount, int wishlistCandidateCount)
        {
            CandidateCount = candidateCount;
            WishlistCandidateCount = wishlistCandidateCount;
        }

        public int CandidateCount { get; }
        public int WishlistCandidateCount { get; }
    }

    /// <summary>선수 카드 상품과 현재 Scout 지갑을 묶은 화면 Snapshot이다.</summary>
    public sealed class OwnerScoutScreenSnapshot
    {
        private readonly OwnerScoutProductSnapshot[] _products;

        public OwnerScoutScreenSnapshot(
            IReadOnlyList<OwnerScoutProductSnapshot> products,
            string walletText,
            string errorMessage = null)
        {
            _products = OwnerPowerUpSnapshotCopy.Copy(products);
            WalletText = walletText ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            State = ErrorMessage.Length > 0
                ? OwnerPowerUpContentState.Error
                : _products.Length == 0 ? OwnerPowerUpContentState.Empty : OwnerPowerUpContentState.Ready;
        }

        public IReadOnlyList<OwnerScoutProductSnapshot> Products => _products;
        public string WalletText { get; }
        public string ErrorMessage { get; }
        public OwnerPowerUpContentState State { get; }
    }

    /// <summary>한 카드에 적용할 수 있는 훈련 프로그램과 실행 전 결과다.</summary>
    /// <summary>훈련 Program 하나의 실행 전 예상 결과와 가능 여부다.</summary>
    public sealed class OwnerCardTrainingProgramSnapshot
    {
        public OwnerCardTrainingProgramSnapshot(
            string programId,
            string title,
            PlayerAbility ability,
            int current,
            int ceiling,
            int gainedPoints,
            int dpCost,
            bool canTrain,
            string blockedReason)
        {
            ProgramId = programId ?? string.Empty;
            Title = title ?? string.Empty;
            Ability = ability;
            Current = current;
            Ceiling = ceiling;
            GainedPoints = gainedPoints;
            DpCost = dpCost;
            CanTrain = canTrain;
            BlockedReason = blockedReason ?? string.Empty;
        }

        public string ProgramId { get; }
        public string Title { get; }
        public PlayerAbility Ability { get; }
        public int Current { get; }
        public int Ceiling { get; }
        public int GainedPoints { get; }
        public int DpCost { get; }
        public bool CanTrain { get; }
        public string BlockedReason { get; }
    }

    /// <summary>보유 카드 한 장과 적용 가능한 훈련 Program 목록이다.</summary>
    public sealed class OwnerCardTrainingTargetSnapshot
    {
        private readonly OwnerCardTrainingProgramSnapshot[] _programs;

        public OwnerCardTrainingTargetSnapshot(
            OwnerCollectionCardSnapshot card,
            IReadOnlyList<OwnerCardTrainingProgramSnapshot> programs)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            _programs = OwnerPowerUpSnapshotCopy.Copy(programs);
        }

        public OwnerCollectionCardSnapshot Card { get; }
        public IReadOnlyList<OwnerCardTrainingProgramSnapshot> Programs => _programs;
    }

    /// <summary>훈련 대상과 현재 DP를 묶은 화면 Snapshot이다.</summary>
    public sealed class OwnerCardTrainingScreenSnapshot
    {
        private readonly OwnerCardTrainingTargetSnapshot[] _cards;

        public OwnerCardTrainingScreenSnapshot(
            IReadOnlyList<OwnerCardTrainingTargetSnapshot> cards,
            int developmentPoints,
            string errorMessage = null)
        {
            _cards = OwnerPowerUpSnapshotCopy.Copy(cards);
            DevelopmentPoints = developmentPoints;
            ErrorMessage = errorMessage ?? string.Empty;
            State = ErrorMessage.Length > 0
                ? OwnerPowerUpContentState.Error
                : _cards.Length == 0 ? OwnerPowerUpContentState.Empty : OwnerPowerUpContentState.Ready;
        }

        public IReadOnlyList<OwnerCardTrainingTargetSnapshot> Cards => _cards;
        public int DevelopmentPoints { get; }
        public string ErrorMessage { get; }
        public OwnerPowerUpContentState State { get; }
    }

    /// <summary>강화와 중복 판매가 같은 카드 상태에서 읽은 두 Preview다.</summary>
    public sealed class OwnerEnhancementSaleTargetSnapshot
    {
        private readonly CardSalePreview[] _salePreviews;

        public OwnerEnhancementSaleTargetSnapshot(
            OwnerCollectionCardSnapshot card,
            CardEnhancementPreview enhancement,
            IReadOnlyList<CardSalePreview> salePreviews)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            Enhancement = enhancement;
            _salePreviews = OwnerPowerUpSnapshotCopy.Copy(salePreviews);
        }

        public OwnerCollectionCardSnapshot Card { get; }
        public CardEnhancementPreview Enhancement { get; }
        public IReadOnlyList<CardSalePreview> SalePreviews => _salePreviews;

        /// <summary>요청 수량에 대응하는 Simulation 판매 Preview를 반환한다.</summary>
        public CardSalePreview GetSalePreview(int count)
        {
            if (count <= 0 || count > _salePreviews.Length)
                return default;
            return _salePreviews[count - 1];
        }
    }

    /// <summary>강화·판매 대상과 현재 SP를 묶은 화면 Snapshot이다.</summary>
    public sealed class OwnerEnhancementSaleScreenSnapshot
    {
        private readonly OwnerEnhancementSaleTargetSnapshot[] _cards;

        public OwnerEnhancementSaleScreenSnapshot(
            IReadOnlyList<OwnerEnhancementSaleTargetSnapshot> cards,
            int scoutingPoints,
            string errorMessage = null)
        {
            _cards = OwnerPowerUpSnapshotCopy.Copy(cards);
            ScoutingPoints = scoutingPoints;
            ErrorMessage = errorMessage ?? string.Empty;
            State = ErrorMessage.Length > 0
                ? OwnerPowerUpContentState.Error
                : _cards.Length == 0 ? OwnerPowerUpContentState.Empty : OwnerPowerUpContentState.Ready;
        }

        public IReadOnlyList<OwnerEnhancementSaleTargetSnapshot> Cards => _cards;
        public int ScoutingPoints { get; }
        public string ErrorMessage { get; }
        public OwnerPowerUpContentState State { get; }
    }

    /// <summary>전력보강 세 Route가 한 Runtime Revision에서 함께 읽는 화면 묶음이다.</summary>
    public sealed class OwnerPowerUpSnapshot
    {
        public OwnerPowerUpSnapshot(
            OwnerScoutScreenSnapshot scout,
            OwnerCardTrainingScreenSnapshot training,
            OwnerEnhancementSaleScreenSnapshot enhancementSale)
        {
            Scout = scout ?? throw new ArgumentNullException(nameof(scout));
            Training = training ?? throw new ArgumentNullException(nameof(training));
            EnhancementSale = enhancementSale ?? throw new ArgumentNullException(nameof(enhancementSale));
        }

        public OwnerScoutScreenSnapshot Scout { get; }
        public OwnerCardTrainingScreenSnapshot Training { get; }
        public OwnerEnhancementSaleScreenSnapshot EnhancementSale { get; }
    }

    /// <summary>Game Query와 Simulation Preview를 전력보강 표시 Snapshot으로 변환한다.</summary>
    public static class OwnerPowerUpPresentationBuilder
    {
        /// <summary>동일 Runtime에서 세 전력보강 Route의 표시 Snapshot을 만든다.</summary>
        public static OwnerPowerUpSnapshot Build(
            OwnerModeManager manager,
            ShopService shop,
            OwnerCollectionSnapshot collection)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (shop == null) throw new ArgumentNullException(nameof(shop));
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            return new OwnerPowerUpSnapshot(
                BuildScout(manager, shop),
                BuildTraining(manager, collection),
                BuildEnhancementSale(manager, collection));
        }

        private static OwnerScoutScreenSnapshot BuildScout(OwnerModeManager manager, ShopService shop)
        {
            try
            {
                IReadOnlyList<ShopProductDefinition> definitions = shop.Catalog.GetProducts(ShopTab.PlayerCard);
                WorldCardCatalog cardCatalog = manager.Runtime.WorldCardCatalog;
                ScoutFeaturePolicy featurePolicy = OwnerShopComposer.ResolveScoutFeaturePolicy(cardCatalog);
                IReadOnlyList<ScoutPoolDefinition> scoutPools = OwnerShopComposer.CreateScoutPools(cardCatalog, featurePolicy);
                var products = new OwnerScoutProductSnapshot[definitions.Count];
                for (int index = 0; index < products.Length; index++)
                    products[index] = CreateScoutProduct(
                        manager,
                        shop,
                        definitions[index],
                        FindScoutPool(scoutPools, definitions[index].SourceId),
                        featurePolicy);
                ShopWalletBalance wallet = shop.GetBalance();
                return new OwnerScoutScreenSnapshot(
                    products,
                    $"스카우트 포인트 {wallet.ScoutingPoints:N0}   보유 카드 {manager.Runtime.OwnedCards.Count:N0}장");
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), string.Empty, exception.Message);
            }
        }

        private static OwnerScoutProductSnapshot CreateScoutProduct(
            OwnerModeManager manager,
            ShopService shop,
            ShopProductDefinition product,
            ScoutPoolDefinition scoutPool,
            ScoutFeaturePolicy featurePolicy)
        {
            ShopPurchaseQuote quote = shop.GetQuote(product);
            ScoutPityBalanceTable pity = ScoutPityBalanceTable.CreateInitial();
            return new OwnerScoutProductSnapshot(
                product.ProductId,
                product.DisplayName + " · " + product.GradeLabel,
                product.ScopeLabel,
                ShopCurrencyNames.GetSymbol(product.Currency) + " " + product.Price.ToString("N0"),
                quote.CanPurchase,
                DescribeQuoteFailure(quote),
                product.DrawCount,
                manager.Runtime.Economy.PityGauge,
                pity.Threshold,
                pity.GaugeGainPerScout,
                pity.GuaranteedMinimumCost,
                null,
                () => CreateScoutProbabilities(shop, product.ProductId),
                () => CreateScoutCandidateSummary(manager.Runtime, scoutPool, featurePolicy));
        }

        private static ScoutPoolDefinition FindScoutPool(
            IReadOnlyList<ScoutPoolDefinition> pools,
            string sourceId)
        {
            for (int index = 0; index < pools.Count; index++)
                if (string.Equals(pools[index].ScoutPoolId, sourceId, StringComparison.Ordinal))
                    return pools[index];
            throw new InvalidOperationException("스카우트 상품의 실제 후보 풀을 찾을 수 없습니다.");
        }

        private static OwnerScoutCandidateSummary CreateScoutCandidateSummary(
            ManagerHistoricalRuntimeState runtime,
            ScoutPoolDefinition pool,
            ScoutFeaturePolicy featurePolicy)
        {
            int candidateCount = 0;
            int wishlistCandidateCount = 0;
            WorldCardCatalog catalog = runtime.WorldCardCatalog;
            IReadOnlyList<PlayerCardDefinition> cards = catalog.Cards;
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index];
                if (!ScoutRoller.IsCandidate(pool, catalog, featurePolicy, card))
                    continue;
                candidateCount++;
                if (runtime.Wishlist.Contains(card.CardId))
                    wishlistCandidateCount++;
            }
            return new OwnerScoutCandidateSummary(candidateCount, wishlistCandidateCount);
        }

        private static IReadOnlyList<OwnerScoutProbabilitySnapshot> CreateScoutProbabilities(
            ShopService shop, string productId)
        {
            if (!shop.TryGetDetails(productId, out ShopProductDetails details))
                throw new InvalidOperationException("스카우트 상품의 실제 확률 상세를 찾을 수 없습니다.");
            var probabilities = new OwnerScoutProbabilitySnapshot[details.Probabilities.Count];
            for (int index = 0; index < probabilities.Length; index++)
            {
                ShopProbabilityEntry bucket = details.Probabilities[index];
                probabilities[index] = new OwnerScoutProbabilitySnapshot(
                    bucket.Label + " · 후보 " + bucket.CandidateCount.ToString("N0") + "장",
                    bucket.Probability);
            }
            return probabilities;
        }

        private static OwnerCardTrainingScreenSnapshot BuildTraining(
            OwnerModeManager manager,
            OwnerCollectionSnapshot collection)
        {
            try
            {
                IReadOnlyList<CardTrainingProgramDefinition> definitions = manager.GetCardTrainingPrograms();
                var targets = new OwnerCardTrainingTargetSnapshot[collection.Cards.Count];
                for (int cardIndex = 0; cardIndex < targets.Length; cardIndex++)
                {
                    OwnerCollectionCardSnapshot card = collection.Cards[cardIndex];
                    var programs = new List<OwnerCardTrainingProgramSnapshot>(6);
                    bool isPitcher = IsPitcher(card);
                    for (int programIndex = 0; programIndex < definitions.Count; programIndex++)
                    {
                        CardTrainingProgramDefinition definition = definitions[programIndex];
                        if (PlayerAbilityCatalog.IsBatterAbility(definition.Ability) == isPitcher) continue;
                        programs.Add(CreateTrainingProgram(manager, card, definition));
                    }
                    targets[cardIndex] = new OwnerCardTrainingTargetSnapshot(card, programs);
                }
                return new OwnerCardTrainingScreenSnapshot(
                    targets,
                    manager.Runtime.Economy.DevelopmentPoints);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return new OwnerCardTrainingScreenSnapshot(
                    Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0, exception.Message);
            }
        }

        private static OwnerCardTrainingProgramSnapshot CreateTrainingProgram(
            OwnerModeManager manager,
            OwnerCollectionCardSnapshot card,
            CardTrainingProgramDefinition definition)
        {
            if (!string.IsNullOrEmpty(card.StudyStatus))
            {
                int current = card.GetAbility(definition.Ability) ?? 0;
                return new OwnerCardTrainingProgramSnapshot(
                    definition.ProgramId, DescribeAbility(definition.Ability), definition.Ability,
                    current, current, 0, 0, false, card.StudyStatus + "에는 훈련할 수 없습니다.");
            }
            CardTrainingPreview preview = manager.PreviewOwnedCardTraining(card.CardId, definition.ProgramId);
            string blocked = preview.CanTrain
                ? string.Empty
                : preview.Current >= preview.Ceiling ? "훈련 상한에 도달했습니다." : "DP가 부족합니다.";
            return new OwnerCardTrainingProgramSnapshot(
                definition.ProgramId,
                DescribeAbility(definition.Ability),
                definition.Ability,
                preview.Current,
                preview.Ceiling,
                preview.GainedPoints,
                preview.DpCost,
                preview.CanTrain,
                blocked);
        }

        private static OwnerEnhancementSaleScreenSnapshot BuildEnhancementSale(
            OwnerModeManager manager,
            OwnerCollectionSnapshot collection)
        {
            try
            {
                var targets = new OwnerEnhancementSaleTargetSnapshot[collection.Cards.Count];
                for (int index = 0; index < targets.Length; index++)
                {
                    OwnerCollectionCardSnapshot card = collection.Cards[index];
                    var salePreviews = new CardSalePreview[Math.Max(1, card.DuplicateCount)];
                    for (int count = 1; count <= salePreviews.Length; count++)
                        salePreviews[count - 1] = manager.PreviewOwnedCardSale(card.CardId, count);
                    targets[index] = new OwnerEnhancementSaleTargetSnapshot(
                        card,
                        manager.PreviewOwnedCardEnhancement(card.CardId),
                        salePreviews);
                }
                return new OwnerEnhancementSaleScreenSnapshot(
                    targets,
                    manager.Runtime.Economy.ScoutingPoints);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return new OwnerEnhancementSaleScreenSnapshot(
                    Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0, exception.Message);
            }
        }

        private static string DescribeQuoteFailure(ShopPurchaseQuote quote)
        {
            return quote.FailureReason switch
            {
                ShopPurchaseFailureReason.None => string.Empty,
                ShopPurchaseFailureReason.InsufficientFunds => "SP가 " + quote.Shortfall.ToString("N0") + " 부족합니다.",
                ShopPurchaseFailureReason.PurchaseLimitReached => "이번 기간의 구매 한도에 도달했습니다.",
                ShopPurchaseFailureReason.CategoryLocked => "현재 스카우트 상품은 잠겨 있습니다.",
                _ => "현재 구매할 수 없습니다."
            };
        }

        /// <summary>PlayerAbility를 플레이어용 한국어 이름으로 변환한다.</summary>
        public static string DescribeAbility(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "교타력", PlayerAbility.Power => "장타력",
                PlayerAbility.Speed => "주력", PlayerAbility.Bunt => "번트력",
                PlayerAbility.Defense => "수비력", PlayerAbility.BatterMental => "타자 정신력",
                PlayerAbility.Stamina => "체력", PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위", PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구력", _ => "투수 정신력"
            };
        }

        private static bool IsPitcher(OwnerCollectionCardSnapshot card) =>
            card.Position == PlayerPosition.StartingPitcher ||
            card.Position == PlayerPosition.ReliefPitcher;
    }
}

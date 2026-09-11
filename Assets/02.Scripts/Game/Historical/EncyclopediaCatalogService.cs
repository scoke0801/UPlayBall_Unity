using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Historical
{
    /// <summary>전체 Canonical Archive와 현재 WorldCardCatalog를 Stable ID로 합성해 도감 조회를 제공한다.</summary>
    public sealed class EncyclopediaCatalogService
    {
        private readonly HistoricalBakedContent _content;
        private readonly ManagerHistoricalRuntimeState _runtime;
        private readonly WorldCardCatalog _catalog;
        private readonly WorldIdentityRegistry _identities;
        private readonly WorldHistorySnapshot _worldHistory;
        private readonly CardCollectionHistoryState _collectionHistory;
        private readonly WishlistState _wishlist;
        private readonly Dictionary<string, OwnedPlayerCardState> _standaloneOwnedCards;
        private readonly SeasonNode[] _seasons;
        private readonly CardNode[] _cards;
        private readonly Dictionary<string, int> _seasonIndexById;
        private readonly Dictionary<string, int> _cardIndexById;
        private readonly Dictionary<string, int[]> _seasonIndicesByPersonId;
        private readonly Dictionary<string, int[]> _cardIndicesBySeasonId;
        private readonly Dictionary<string, int[]> _seasonIndicesByFranchiseId;
        private readonly Dictionary<string, int[]> _cardIndicesByFranchiseId;
        private readonly Dictionary<string, int[]> _cardIndicesByPersonId;
        private readonly Dictionary<int, int[]> _seasonIndicesByYear;
        private readonly Dictionary<int, int[]> _cardIndicesByYear;
        private readonly Dictionary<int, int[]> _cardIndicesByEdition;
        private readonly Dictionary<string, SeasonStatistics> _statisticsBySeasonId;
        private readonly string[] _franchiseIds;
        private readonly int[] _originYears;
        private readonly HashSet<string> _teamSeasonCells;

        public EncyclopediaCatalogService(
            HistoricalBakedContent content,
            ManagerHistoricalRuntimeState runtime)
            : this(
                content,
                runtime?.WorldCardCatalog,
                runtime?.IdentityRegistry,
                runtime?.WorldHistory,
                runtime?.OwnedCards,
                runtime?.CollectionHistory,
                runtime?.Wishlist,
                runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            runtime.ContentReference.EnsureMatches(content.Manifest);
        }

        /// <summary>런타임 Aggregate 생성 없이 순수 C# Fixture에서 조회 계약을 검증한다.</summary>
        public EncyclopediaCatalogService(
            HistoricalBakedContent content,
            WorldCardCatalog catalog,
            WorldIdentityRegistry identities,
            WorldHistorySnapshot worldHistory,
            IReadOnlyList<OwnedPlayerCardState> ownedCards,
            CardCollectionHistoryState collectionHistory,
            WishlistState wishlist)
            : this(
                content,
                catalog,
                identities,
                worldHistory,
                ownedCards,
                collectionHistory,
                wishlist,
                null)
        {
        }

        private EncyclopediaCatalogService(
            HistoricalBakedContent content,
            WorldCardCatalog catalog,
            WorldIdentityRegistry identities,
            WorldHistorySnapshot worldHistory,
            IReadOnlyList<OwnedPlayerCardState> ownedCards,
            CardCollectionHistoryState collectionHistory,
            WishlistState wishlist,
            ManagerHistoricalRuntimeState runtime)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _identities = identities ?? throw new ArgumentNullException(nameof(identities));
            _worldHistory = worldHistory ?? throw new ArgumentNullException(nameof(worldHistory));
            _collectionHistory = collectionHistory ?? throw new ArgumentNullException(nameof(collectionHistory));
            _wishlist = wishlist ?? throw new ArgumentNullException(nameof(wishlist));
            _runtime = runtime;
            _standaloneOwnedCards = runtime == null
                ? IndexOwnedCards(ownedCards ?? throw new ArgumentNullException(nameof(ownedCards)))
                : null;

            _seasonIndexById = new Dictionary<string, int>(content.PlayerSeasons.Count, StringComparer.Ordinal);
            _seasons = CreateSeasonNodes(content.PlayerSeasons);
            _cardIndexById = new Dictionary<string, int>(catalog.Cards.Count, StringComparer.Ordinal);
            _cards = CreateCardNodes(catalog.Cards);
            _seasonIndicesByPersonId = BuildIndex(_seasons, node => node.Season.PlayerPersonId);
            _cardIndicesBySeasonId = BuildIndex(_cards, node => node.Card.PlayerSeasonId);
            _seasonIndicesByFranchiseId = BuildIndex(_seasons, node => node.Season.OriginFranchiseId);
            _cardIndicesByFranchiseId = BuildIndex(_cards, node => node.Season.OriginFranchiseId);
            _cardIndicesByPersonId = BuildIndex(_cards, node => node.Season.PlayerPersonId);
            _seasonIndicesByYear = BuildIntIndex(_seasons, node => node.Season.OriginYear);
            _cardIndicesByYear = BuildIntIndex(_cards, node => node.Season.OriginYear);
            _cardIndicesByEdition = BuildIntIndex(_cards, node => (int)node.Card.Edition);
            EnsureCatalogCoverage();
            _statisticsBySeasonId = IndexStatistics(worldHistory.Statistics);
            _franchiseIds = CreateFranchiseIds(identities.FranchiseIdentities);
            _originYears = CreateOriginYears(content.YearNumbers);
            _teamSeasonCells = CreateTeamSeasonCells(content.TeamSeasons);
        }

        public int PlayerSeasonCount => _seasons.Length;
        public int CardCount => _cards.Length;

        /// <summary>현재 World의 실제 CardId만 필터링하고 동점을 Stable ID로 고정한다.</summary>
        public IReadOnlyList<EncyclopediaCardEntry> QueryCards(
            EncyclopediaFilter filter = null,
            EncyclopediaSort sort = EncyclopediaSort.Default)
        {
            filter ??= new EncyclopediaFilter();
            ValidateFilter(filter);
            QueryState state = CreateQueryState();
            var result = new List<EncyclopediaCardEntry>();
            int[] candidates = SelectCardCandidates(filter);
            int candidateCount = candidates?.Length ?? _cards.Length;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                int index = candidates == null ? candidateIndex : candidates[candidateIndex];
                CardNode node = _cards[index];
                CardStatus status = state.GetStatus(node.Card.CardId);
                if (!MatchesCanonical(node.Season, node.Person, node.DisplayName, filter) ||
                    (filter.Edition.HasValue && node.Card.Edition != filter.Edition.Value) ||
                    !MatchesCollection(status, filter.Collection))
                {
                    continue;
                }
                result.Add(CreateCardEntry(node, status));
            }
            result.Sort((left, right) => CompareCards(left, right, sort));
            return result.ToArray();
        }

        /// <summary>PlayerSeason은 자신의 발급 카드 중 Edition·수집 조건을 만족하는 항목이 하나라도 있을 때 노출한다.</summary>
        public IReadOnlyList<EncyclopediaPlayerSeasonEntry> QueryPlayerSeasons(
            EncyclopediaFilter filter = null,
            EncyclopediaSort sort = EncyclopediaSort.Default)
        {
            filter ??= new EncyclopediaFilter();
            ValidateFilter(filter);
            QueryState state = CreateQueryState();
            var result = new List<EncyclopediaPlayerSeasonEntry>();
            int[] candidates = SelectSeasonCandidates(filter);
            int candidateCount = candidates?.Length ?? _seasons.Length;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                int index = candidates == null ? candidateIndex : candidates[candidateIndex];
                SeasonNode node = _seasons[index];
                if (!MatchesCanonical(node.Season, node.Person, node.DisplayName, filter))
                    continue;
                EncyclopediaPlayerSeasonEntry entry = CreatePlayerSeasonEntry(index, state, filter, true);
                if (entry != null)
                    result.Add(entry);
            }
            result.Sort((left, right) => ComparePlayerSeasons(left, right, sort));
            return result.ToArray();
        }

        public EncyclopediaCardDetail GetCardDetail(string cardId)
        {
            int index = GetRequiredIndex(_cardIndexById, cardId, "CardId");
            CardNode node = _cards[index];
            _statisticsBySeasonId.TryGetValue(node.Season.PlayerSeasonId, out SeasonStatistics statistics);
            return new EncyclopediaCardDetail(CreateCardEntry(node, CreateQueryState().GetStatus(node.Card.CardId)), statistics);
        }

        public EncyclopediaPlayerSeasonDetail GetPlayerSeasonDetail(string playerSeasonId)
        {
            int seasonIndex = GetRequiredIndex(_seasonIndexById, playerSeasonId, "PlayerSeasonId");
            QueryState state = CreateQueryState();
            EncyclopediaPlayerSeasonEntry season = CreatePlayerSeasonEntry(
                seasonIndex,
                state,
                new EncyclopediaFilter(),
                false);
            return new EncyclopediaPlayerSeasonDetail(
                season,
                CreateCardsForPlayerSeason(playerSeasonId, state),
                CreatePlayerTimeline(_seasons[seasonIndex].Season.PlayerPersonId, state));
        }

        public IReadOnlyList<EncyclopediaCardEntry> GetCardsForPlayerSeason(string playerSeasonId)
        {
            GetRequiredIndex(_seasonIndexById, playerSeasonId, "PlayerSeasonId");
            return CreateCardsForPlayerSeason(playerSeasonId.Trim(), CreateQueryState());
        }

        public IReadOnlyList<EncyclopediaPlayerTimelineEntry> GetPlayerTimeline(string playerPersonId)
        {
            return CreatePlayerTimeline(RequireId(playerPersonId, nameof(playerPersonId)), CreateQueryState());
        }

        /// <summary>판매와 무관한 EverAcquired 분자를 Catalog 분모와 같은 호출 시점에서 집계한다.</summary>
        public EncyclopediaCollectionProgress GetCollectionProgress()
        {
            QueryState state = CreateQueryState();
            var cellByKey = new Dictionary<string, CellAccumulator>(StringComparer.Ordinal);
            for (int seasonIndex = 0; seasonIndex < _seasons.Length; seasonIndex++)
            {
                SeasonNode season = _seasons[seasonIndex];
                string key = CreateCellKey(season.Season.OriginFranchiseId, season.Season.OriginYear);
                if (!cellByKey.TryGetValue(key, out CellAccumulator cell))
                {
                    cell = new CellAccumulator();
                    cellByKey.Add(key, cell);
                }
                cell.PlayerSeasonCount++;
            }

            var acquiredSeasons = new HashSet<string>(StringComparer.Ordinal);
            var editionByValue = new Dictionary<PlayerCardEdition, EditionAccumulator>();
            int everAcquiredCardCount = 0;
            int ownedCardCount = 0;
            int wishlistCardCount = 0;
            for (int cardIndex = 0; cardIndex < _cards.Length; cardIndex++)
            {
                CardNode card = _cards[cardIndex];
                CardStatus status = state.GetStatus(card.Card.CardId);
                string key = CreateCellKey(card.Season.OriginFranchiseId, card.Season.OriginYear);
                CellAccumulator cell = cellByKey[key];
                cell.CollectibleCardCount++;
                if (status.WasEverAcquired)
                {
                    everAcquiredCardCount++;
                    cell.EverAcquiredCardCount++;
                    cell.AcquiredSeasonIds.Add(card.Season.PlayerSeasonId);
                    acquiredSeasons.Add(card.Season.PlayerSeasonId);
                }
                if (status.IsCurrentlyOwned)
                {
                    ownedCardCount++;
                    cell.OwnedCardCount++;
                }
                if (status.IsWishlisted)
                {
                    wishlistCardCount++;
                    cell.WishlistCardCount++;
                }

                if (!editionByValue.TryGetValue(card.Card.Edition, out EditionAccumulator edition))
                {
                    edition = new EditionAccumulator();
                    editionByValue.Add(card.Card.Edition, edition);
                }
                edition.Collectible++;
                if (status.WasEverAcquired) edition.EverAcquired++;
                if (status.IsCurrentlyOwned) edition.Owned++;
                if (status.IsWishlisted) edition.Wishlisted++;
            }

            FranchiseYearCollectionProgress[] cells = CreateProgressCells(cellByKey);
            EditionCollectionProgress[] editions = CreateEditionProgress(editionByValue);
            return new EncyclopediaCollectionProgress(
                _seasons.Length,
                acquiredSeasons.Count,
                _cards.Length,
                everAcquiredCardCount,
                ownedCardCount,
                wishlistCardCount,
                cells,
                editions);
        }

        private SeasonNode[] CreateSeasonNodes(IReadOnlyList<PlayerSeasonDefinition> definitions)
        {
            var result = new SeasonNode[definitions.Count];
            for (int index = 0; index < definitions.Count; index++)
            {
                PlayerSeasonDefinition season = definitions[index]
                    ?? throw new InvalidOperationException("Canonical PlayerSeason이 null입니다.");
                if (!_seasonIndexById.TryAdd(season.PlayerSeasonId, index))
                    throw new InvalidOperationException($"Canonical PlayerSeasonId가 중복됩니다: {season.PlayerSeasonId}");
                if (!_content.TryGetPlayerPerson(season.PlayerPersonId, out PlayerPersonDefinition person))
                    throw new InvalidOperationException($"PlayerSeason의 Person을 찾을 수 없습니다: {season.PlayerSeasonId}");
                string displayName = _identities.GetPlayerDisplayName(season.PlayerPersonId);
                string franchiseDisplayName = _identities.GetFranchiseDisplayName(season.OriginFranchiseId);
                result[index] = new SeasonNode(season, person, displayName, franchiseDisplayName);
            }
            return result;
        }

        private CardNode[] CreateCardNodes(IReadOnlyList<PlayerCardDefinition> definitions)
        {
            var result = new CardNode[definitions.Count];
            for (int index = 0; index < definitions.Count; index++)
            {
                PlayerCardDefinition card = definitions[index]
                    ?? throw new InvalidOperationException("WorldCardCatalog Card가 null입니다.");
                if (!_cardIndexById.TryAdd(card.CardId, index))
                    throw new InvalidOperationException($"WorldCardCatalog CardId가 중복됩니다: {card.CardId}");
                if (!_seasonIndexById.TryGetValue(card.PlayerSeasonId, out int seasonIndex))
                    throw new InvalidOperationException($"Card의 Canonical PlayerSeason을 찾을 수 없습니다: {card.CardId}");
                SeasonNode season = _seasons[seasonIndex];
                PlayerSeasonDefinition catalogSeason = _catalog.GetPlayerSeason(card);
                if (!string.Equals(catalogSeason.PlayerSeasonId, season.Season.PlayerSeasonId, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Card의 Catalog/Archive PlayerSeason이 다릅니다: {card.CardId}");
                result[index] = new CardNode(card, season);
            }
            return result;
        }

        private void EnsureCatalogCoverage()
        {
            for (int index = 0; index < _seasons.Length; index++)
            {
                string playerSeasonId = _seasons[index].Season.PlayerSeasonId;
                if (!_cardIndicesBySeasonId.TryGetValue(playerSeasonId, out int[] cardIndices))
                    throw new InvalidOperationException($"PlayerSeason에 현재 World Card가 없습니다: {playerSeasonId}");
                bool hasNormal = false;
                for (int cardIndex = 0; cardIndex < cardIndices.Length; cardIndex++)
                    if (_cards[cardIndices[cardIndex]].Card.Edition == PlayerCardEdition.Normal)
                        hasNormal = true;
                if (!hasNormal)
                    throw new InvalidOperationException($"PlayerSeason의 Normal Card가 없습니다: {playerSeasonId}");
            }
        }

        private EncyclopediaCardEntry[] CreateCardsForPlayerSeason(string playerSeasonId, QueryState state)
        {
            if (!_cardIndicesBySeasonId.TryGetValue(playerSeasonId, out int[] indices))
                return Array.Empty<EncyclopediaCardEntry>();
            var result = new EncyclopediaCardEntry[indices.Length];
            for (int index = 0; index < indices.Length; index++)
            {
                CardNode node = _cards[indices[index]];
                result[index] = CreateCardEntry(node, state.GetStatus(node.Card.CardId));
            }
            Array.Sort(result, (left, right) =>
            {
                int comparison = left.Edition.CompareTo(right.Edition);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
            });
            return result;
        }

        private EncyclopediaPlayerTimelineEntry[] CreatePlayerTimeline(string playerPersonId, QueryState state)
        {
            if (!_seasonIndicesByPersonId.TryGetValue(playerPersonId, out int[] indices))
                return Array.Empty<EncyclopediaPlayerTimelineEntry>();
            var result = new EncyclopediaPlayerTimelineEntry[indices.Length];
            for (int index = 0; index < indices.Length; index++)
            {
                EncyclopediaPlayerSeasonEntry season = CreatePlayerSeasonEntry(
                    indices[index],
                    state,
                    new EncyclopediaFilter(),
                    false);
                result[index] = new EncyclopediaPlayerTimelineEntry(season);
            }
            Array.Sort(result, (left, right) =>
            {
                int comparison = left.OriginYear.CompareTo(right.OriginYear);
                return comparison != 0
                    ? comparison
                    : StringComparer.Ordinal.Compare(left.PlayerSeasonId, right.PlayerSeasonId);
            });
            return result;
        }

        private EncyclopediaPlayerSeasonEntry CreatePlayerSeasonEntry(
            int seasonIndex,
            QueryState state,
            EncyclopediaFilter filter,
            bool requireMatchingCard)
        {
            SeasonNode node = _seasons[seasonIndex];
            int[] cardIndices = _cardIndicesBySeasonId[node.Season.PlayerSeasonId];
            int owned = 0;
            int acquired = 0;
            int wishlisted = 0;
            bool hasMatchingCard = false;
            long? newestSequence = null;
            for (int index = 0; index < cardIndices.Length; index++)
            {
                CardNode card = _cards[cardIndices[index]];
                CardStatus status = state.GetStatus(card.Card.CardId);
                if (status.IsCurrentlyOwned) owned++;
                if (status.WasEverAcquired) acquired++;
                if (status.IsWishlisted)
                {
                    wishlisted++;
                    if (!newestSequence.HasValue || status.WishlistAddedSequence > newestSequence.Value)
                        newestSequence = status.WishlistAddedSequence;
                }
                if ((!filter.Edition.HasValue || card.Card.Edition == filter.Edition.Value) &&
                    MatchesCollection(status, filter.Collection))
                {
                    hasMatchingCard = true;
                }
            }
            if (requireMatchingCard && !hasMatchingCard)
                return null;
            return new EncyclopediaPlayerSeasonEntry(
                node.Season,
                node.Person,
                node.DisplayName,
                node.FranchiseDisplayName,
                cardIndices.Length,
                owned,
                acquired,
                wishlisted,
                newestSequence);
        }

        private EncyclopediaCardEntry CreateCardEntry(CardNode node, CardStatus status)
        {
            return new EncyclopediaCardEntry(
                node.Card,
                node.Season,
                node.Person,
                node.DisplayName,
                node.FranchiseDisplayName,
                status.IsCurrentlyOwned,
                status.OwnedCount,
                status.WasEverAcquired,
                status.IsWishlisted,
                status.IsWishlisted ? status.WishlistAddedSequence : (long?)null);
        }

        private QueryState CreateQueryState()
        {
            var wishlistSequences = new Dictionary<string, long>(_wishlist.Count, StringComparer.Ordinal);
            for (int index = 0; index < _wishlist.Entries.Count; index++)
            {
                WishlistEntry entry = _wishlist.Entries[index];
                if (_cardIndexById.ContainsKey(entry.CardId))
                    wishlistSequences[entry.CardId] = entry.AddedSequence;
            }
            return new QueryState(this, wishlistSequences);
        }

        private int[] SelectCardCandidates(EncyclopediaFilter filter)
        {
            int[] selected = null;
            if (!string.IsNullOrWhiteSpace(filter.PlayerSeasonId))
                SelectSmaller(ref selected, Find(_cardIndicesBySeasonId, filter.PlayerSeasonId.Trim()));
            if (!string.IsNullOrWhiteSpace(filter.PlayerPersonId))
                SelectSmaller(ref selected, Find(_cardIndicesByPersonId, filter.PlayerPersonId.Trim()));
            if (!string.IsNullOrWhiteSpace(filter.FranchiseId))
                SelectSmaller(ref selected, Find(_cardIndicesByFranchiseId, filter.FranchiseId.Trim()));
            if (filter.OriginYear > 0)
                SelectSmaller(ref selected, Find(_cardIndicesByYear, filter.OriginYear));
            if (filter.Edition.HasValue)
                SelectSmaller(ref selected, Find(_cardIndicesByEdition, (int)filter.Edition.Value));
            return selected;
        }

        private int[] SelectSeasonCandidates(EncyclopediaFilter filter)
        {
            int[] selected = null;
            if (!string.IsNullOrWhiteSpace(filter.PlayerSeasonId))
            {
                string id = filter.PlayerSeasonId.Trim();
                SelectSmaller(
                    ref selected,
                    _seasonIndexById.TryGetValue(id, out int index) ? new[] { index } : Array.Empty<int>());
            }
            if (!string.IsNullOrWhiteSpace(filter.PlayerPersonId))
                SelectSmaller(ref selected, Find(_seasonIndicesByPersonId, filter.PlayerPersonId.Trim()));
            if (!string.IsNullOrWhiteSpace(filter.FranchiseId))
                SelectSmaller(ref selected, Find(_seasonIndicesByFranchiseId, filter.FranchiseId.Trim()));
            if (filter.OriginYear > 0)
                SelectSmaller(ref selected, Find(_seasonIndicesByYear, filter.OriginYear));
            return selected;
        }

        private static int[] Find<TKey>(Dictionary<TKey, int[]> index, TKey key)
        {
            return index.TryGetValue(key, out int[] values) ? values : Array.Empty<int>();
        }

        private static void SelectSmaller(ref int[] selected, int[] candidate)
        {
            if (selected == null || candidate.Length < selected.Length)
                selected = candidate;
        }

        private bool TryGetOwnedCard(string cardId, out OwnedPlayerCardState ownedCard)
        {
            if (_runtime != null)
                return _runtime.TryGetOwnedCard(cardId, out ownedCard);
            return _standaloneOwnedCards.TryGetValue(cardId, out ownedCard);
        }

        private FranchiseYearCollectionProgress[] CreateProgressCells(
            Dictionary<string, CellAccumulator> cellsByKey)
        {
            var result = new FranchiseYearCollectionProgress[_franchiseIds.Length * _originYears.Length];
            int resultIndex = 0;
            for (int franchiseIndex = 0; franchiseIndex < _franchiseIds.Length; franchiseIndex++)
            {
                string franchiseId = _franchiseIds[franchiseIndex];
                string displayName = _identities.GetFranchiseDisplayName(franchiseId);
                for (int yearIndex = 0; yearIndex < _originYears.Length; yearIndex++)
                {
                    int year = _originYears[yearIndex];
                    string key = CreateCellKey(franchiseId, year);
                    cellsByKey.TryGetValue(key, out CellAccumulator cell);
                    result[resultIndex++] = new FranchiseYearCollectionProgress(
                        franchiseId,
                        displayName,
                        year,
                        _teamSeasonCells.Contains(key),
                        cell?.PlayerSeasonCount ?? 0,
                        cell?.AcquiredSeasonIds.Count ?? 0,
                        cell?.CollectibleCardCount ?? 0,
                        cell?.EverAcquiredCardCount ?? 0,
                        cell?.OwnedCardCount ?? 0,
                        cell?.WishlistCardCount ?? 0);
                }
            }
            return result;
        }

        private static EditionCollectionProgress[] CreateEditionProgress(
            Dictionary<PlayerCardEdition, EditionAccumulator> values)
        {
            var editions = new PlayerCardEdition[values.Count];
            values.Keys.CopyTo(editions, 0);
            Array.Sort(editions);
            var result = new EditionCollectionProgress[editions.Length];
            for (int index = 0; index < editions.Length; index++)
            {
                EditionAccumulator value = values[editions[index]];
                result[index] = new EditionCollectionProgress(
                    editions[index], value.Collectible, value.EverAcquired, value.Owned, value.Wishlisted);
            }
            return result;
        }

        private static bool MatchesCanonical(
            PlayerSeasonDefinition season,
            PlayerPersonDefinition person,
            string displayName,
            EncyclopediaFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchText) &&
                displayName.IndexOf(filter.SearchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (!string.IsNullOrWhiteSpace(filter.FranchiseId) &&
                !string.Equals(season.OriginFranchiseId, filter.FranchiseId.Trim(), StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(filter.PlayerPersonId) &&
                !string.Equals(season.PlayerPersonId, filter.PlayerPersonId.Trim(), StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(filter.PlayerSeasonId) &&
                !string.Equals(season.PlayerSeasonId, filter.PlayerSeasonId.Trim(), StringComparison.Ordinal))
                return false;
            if (filter.OriginYear > 0 && season.OriginYear != filter.OriginYear)
                return false;
            if (filter.DecadeStartYear > 0 && season.OriginYear / 10 * 10 != filter.DecadeStartYear)
                return false;
            if (filter.PlayerType.HasValue && season.PlayerType != filter.PlayerType.Value)
                return false;
            PlayerPosition displayedPosition = season.IsPositionEvidenceMissing
                ? PlayerPosition.Unknown : season.Position;
            if (filter.Position.HasValue && displayedPosition != filter.Position.Value)
                return false;
            if (filter.PitcherRole.HasValue &&
                (season.PlayerType != PlayerType.Pitcher || season.PitcherRole != filter.PitcherRole.Value))
                return false;
            if (filter.MinimumCost > 0 && season.Cost < filter.MinimumCost)
                return false;
            if (filter.MaximumCost > 0 && season.Cost > filter.MaximumCost)
                return false;
            if (filter.RegistrationType.HasValue && season.RegistrationType != filter.RegistrationType.Value)
                return false;
            if (filter.Bats.HasValue && person.Bats != filter.Bats.Value)
                return false;
            if (filter.Throws.HasValue && person.Throws != filter.Throws.Value)
                return false;
            return true;
        }

        private static bool MatchesCollection(CardStatus status, EncyclopediaCollectionFilter filter)
        {
            return filter switch
            {
                EncyclopediaCollectionFilter.All => true,
                EncyclopediaCollectionFilter.CurrentlyOwned => status.IsCurrentlyOwned,
                EncyclopediaCollectionFilter.AcquiredButNotOwned => status.WasEverAcquired && !status.IsCurrentlyOwned,
                EncyclopediaCollectionFilter.NeverAcquired => !status.WasEverAcquired,
                EncyclopediaCollectionFilter.Wishlisted => status.IsWishlisted,
                EncyclopediaCollectionFilter.NotWishlisted => !status.IsWishlisted,
                _ => throw new ArgumentOutOfRangeException(nameof(filter))
            };
        }

        private static int CompareCards(EncyclopediaCardEntry left, EncyclopediaCardEntry right, EncyclopediaSort sort)
        {
            int result = CompareCommon(left, right, sort);
            if (result == 0 && (sort == EncyclopediaSort.Default || sort == EncyclopediaSort.Edition))
                result = left.Edition.CompareTo(right.Edition);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
        }

        private static int ComparePlayerSeasons(
            EncyclopediaPlayerSeasonEntry left,
            EncyclopediaPlayerSeasonEntry right,
            EncyclopediaSort sort)
        {
            int result = sort switch
            {
                EncyclopediaSort.DisplayName => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName),
                EncyclopediaSort.OriginYearAscending => left.OriginYear.CompareTo(right.OriginYear),
                EncyclopediaSort.OriginYearDescending => right.OriginYear.CompareTo(left.OriginYear),
                EncyclopediaSort.CostAscending => left.Cost.CompareTo(right.Cost),
                EncyclopediaSort.CostDescending => right.Cost.CompareTo(left.Cost),
                EncyclopediaSort.Position => ComparePosition(left.PlayerType, left.Position, left.PitcherRole, right.PlayerType, right.Position, right.PitcherRole),
                EncyclopediaSort.CurrentlyOwnedFirst => right.IsCurrentlyOwned.CompareTo(left.IsCurrentlyOwned),
                EncyclopediaSort.NeverAcquiredFirst => left.WasEverAcquired.CompareTo(right.WasEverAcquired),
                EncyclopediaSort.WishlistedFirst => right.IsWishlisted.CompareTo(left.IsWishlisted),
                EncyclopediaSort.WishlistNewest => CompareNullableSequence(left.NewestWishlistSequence, right.NewestWishlistSequence, false),
                EncyclopediaSort.WishlistOldest => CompareNullableSequence(left.NewestWishlistSequence, right.NewestWishlistSequence, true),
                _ => CompareDefault(left, right)
            };
            return result != 0
                ? result
                : StringComparer.Ordinal.Compare(left.PlayerSeasonId, right.PlayerSeasonId);
        }

        private static int CompareCommon(EncyclopediaCardEntry left, EncyclopediaCardEntry right, EncyclopediaSort sort)
        {
            return sort switch
            {
                EncyclopediaSort.DisplayName => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName),
                EncyclopediaSort.OriginYearAscending => left.OriginYear.CompareTo(right.OriginYear),
                EncyclopediaSort.OriginYearDescending => right.OriginYear.CompareTo(left.OriginYear),
                EncyclopediaSort.CostAscending => left.Cost.CompareTo(right.Cost),
                EncyclopediaSort.CostDescending => right.Cost.CompareTo(left.Cost),
                EncyclopediaSort.Position => ComparePosition(left.PlayerType, left.Position, left.PitcherRole, right.PlayerType, right.Position, right.PitcherRole),
                EncyclopediaSort.Edition => left.Edition.CompareTo(right.Edition),
                EncyclopediaSort.CurrentlyOwnedFirst => right.IsCurrentlyOwned.CompareTo(left.IsCurrentlyOwned),
                EncyclopediaSort.NeverAcquiredFirst => left.WasEverAcquired.CompareTo(right.WasEverAcquired),
                EncyclopediaSort.WishlistedFirst => right.IsWishlisted.CompareTo(left.IsWishlisted),
                EncyclopediaSort.WishlistNewest => CompareNullableSequence(left.WishlistAddedSequence, right.WishlistAddedSequence, false),
                EncyclopediaSort.WishlistOldest => CompareNullableSequence(left.WishlistAddedSequence, right.WishlistAddedSequence, true),
                _ => CompareDefault(left, right)
            };
        }

        private static int CompareDefault(EncyclopediaCardEntry left, EncyclopediaCardEntry right)
        {
            int result = right.OriginYear.CompareTo(left.OriginYear);
            if (result == 0) result = StringComparer.Ordinal.Compare(left.FranchiseDisplayName, right.FranchiseDisplayName);
            if (result == 0) result = left.PlayerType.CompareTo(right.PlayerType);
            if (result == 0) result = ComparePosition(left.PlayerType, left.Position, left.PitcherRole, right.PlayerType, right.Position, right.PitcherRole);
            if (result == 0) result = right.Cost.CompareTo(left.Cost);
            if (result == 0) result = StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
            return result;
        }

        private static int CompareDefault(EncyclopediaPlayerSeasonEntry left, EncyclopediaPlayerSeasonEntry right)
        {
            int result = right.OriginYear.CompareTo(left.OriginYear);
            if (result == 0) result = StringComparer.Ordinal.Compare(left.FranchiseDisplayName, right.FranchiseDisplayName);
            if (result == 0) result = left.PlayerType.CompareTo(right.PlayerType);
            if (result == 0) result = ComparePosition(left.PlayerType, left.Position, left.PitcherRole, right.PlayerType, right.Position, right.PitcherRole);
            if (result == 0) result = right.Cost.CompareTo(left.Cost);
            if (result == 0) result = StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
            return result;
        }

        private static int ComparePosition(
            PlayerType leftType,
            PlayerPosition leftPosition,
            PitcherRole leftRole,
            PlayerType rightType,
            PlayerPosition rightPosition,
            PitcherRole rightRole)
        {
            int result = leftType.CompareTo(rightType);
            if (result != 0) return result;
            result = leftPosition.CompareTo(rightPosition);
            if (result != 0) return result;
            return leftType == PlayerType.Pitcher ? leftRole.CompareTo(rightRole) : 0;
        }

        private static int CompareNullableSequence(long? left, long? right, bool ascending)
        {
            if (!left.HasValue) return right.HasValue ? 1 : 0;
            if (!right.HasValue) return -1;
            return ascending
                ? left.Value.CompareTo(right.Value)
                : right.Value.CompareTo(left.Value);
        }

        private static void ValidateFilter(EncyclopediaFilter filter)
        {
            if (filter.MinimumCost < 0 || filter.MinimumCost > 10)
                throw new ArgumentOutOfRangeException(nameof(filter.MinimumCost));
            if (filter.MaximumCost < 0 || filter.MaximumCost > 10)
                throw new ArgumentOutOfRangeException(nameof(filter.MaximumCost));
            if (filter.MinimumCost > 0 && filter.MaximumCost > 0 && filter.MinimumCost > filter.MaximumCost)
                throw new ArgumentException("최소 Cost는 최대 Cost보다 클 수 없습니다.", nameof(filter));
            if (filter.DecadeStartYear > 0 && filter.DecadeStartYear % 10 != 0)
                throw new ArgumentException("연대 필터는 10년 단위 시작 연도여야 합니다.", nameof(filter));
        }

        private static Dictionary<string, OwnedPlayerCardState> IndexOwnedCards(
            IReadOnlyList<OwnedPlayerCardState> ownedCards)
        {
            var result = new Dictionary<string, OwnedPlayerCardState>(ownedCards.Count, StringComparer.Ordinal);
            for (int index = 0; index < ownedCards.Count; index++)
            {
                OwnedPlayerCardState card = ownedCards[index]
                    ?? throw new ArgumentException("보유 카드가 null입니다.", nameof(ownedCards));
                if (!result.TryAdd(card.CardId, card))
                    throw new ArgumentException("보유 CardId가 중복됩니다.", nameof(ownedCards));
            }
            return result;
        }

        private static Dictionary<string, SeasonStatistics> IndexStatistics(IReadOnlyList<SeasonStatistics> statistics)
        {
            var result = new Dictionary<string, SeasonStatistics>(statistics.Count, StringComparer.Ordinal);
            for (int index = 0; index < statistics.Count; index++)
            {
                SeasonStatistics value = statistics[index];
                // World History는 전반기·정규시즌 전체·포스트시즌·올스타를 같은 PlayerSeasonId로 보관한다.
                // 도감의 시즌 기록은 다른 화면과 동일하게 정규시즌 전체 행만 소비해야 한다.
                if (value.IsFirstHalf || value.IsPostseason || value.IsAllStarGame)
                    continue;
                if (!result.TryAdd(value.PlayerSeasonId, value))
                    throw new InvalidOperationException($"World History 정규시즌 전체 기록이 중복됩니다: {value.PlayerSeasonId}");
            }
            return result;
        }

        private static Dictionary<string, int[]> BuildIndex<T>(T[] source, Func<T, string> keySelector)
        {
            var lists = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int index = 0; index < source.Length; index++)
            {
                string key = keySelector(source[index]);
                if (!lists.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    lists.Add(key, values);
                }
                values.Add(index);
            }
            var result = new Dictionary<string, int[]>(lists.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<int>> pair in lists)
                result.Add(pair.Key, pair.Value.ToArray());
            return result;
        }

        private static Dictionary<int, int[]> BuildIntIndex<T>(T[] source, Func<T, int> keySelector)
        {
            var lists = new Dictionary<int, List<int>>();
            for (int index = 0; index < source.Length; index++)
            {
                int key = keySelector(source[index]);
                if (!lists.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    lists.Add(key, values);
                }
                values.Add(index);
            }
            var result = new Dictionary<int, int[]>(lists.Count);
            foreach (KeyValuePair<int, List<int>> pair in lists)
                result.Add(pair.Key, pair.Value.ToArray());
            return result;
        }

        private string[] CreateFranchiseIds(IReadOnlyList<WorldFranchiseIdentity> identities)
        {
            var result = new string[identities.Count];
            for (int index = 0; index < identities.Count; index++)
                result[index] = identities[index].FranchiseId;
            Array.Sort(result, (left, right) =>
            {
                int comparison = StringComparer.Ordinal.Compare(
                    _identities.GetFranchiseDisplayName(left),
                    _identities.GetFranchiseDisplayName(right));
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left, right);
            });
            return result;
        }

        private static int[] CreateOriginYears(IReadOnlyList<int> years)
        {
            var result = new int[years.Count];
            for (int index = 0; index < years.Count; index++) result[index] = years[index];
            Array.Sort(result);
            return result;
        }

        private HashSet<string> CreateTeamSeasonCells(IReadOnlyList<TeamSeasonDefinition> teams)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < teams.Count; index++)
            {
                TeamSeasonDefinition team = teams[index];
                _identities.GetFranchiseDisplayName(team.FranchiseId);
                if (!result.Add(CreateCellKey(team.FranchiseId, team.OriginYear)))
                    throw new InvalidOperationException($"Franchise×OriginYear TeamSeason이 중복됩니다: {team.TeamSeasonKey}");
            }
            return result;
        }

        private static int GetRequiredIndex(Dictionary<string, int> index, string id, string kind)
        {
            string required = RequireId(id, kind);
            if (!index.TryGetValue(required, out int value))
                throw new KeyNotFoundException($"{kind} {required}을(를) 도감에서 찾을 수 없습니다.");
            return value;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }

        private static string CreateCellKey(string franchiseId, int originYear) =>
            franchiseId + "\u001f" + originYear;

        private sealed class SeasonNode
        {
            public SeasonNode(
                PlayerSeasonDefinition season,
                PlayerPersonDefinition person,
                string displayName,
                string franchiseDisplayName)
            {
                Season = season;
                Person = person;
                DisplayName = displayName;
                FranchiseDisplayName = franchiseDisplayName;
            }

            public PlayerSeasonDefinition Season { get; }
            public PlayerPersonDefinition Person { get; }
            public string DisplayName { get; }
            public string FranchiseDisplayName { get; }
        }

        private sealed class CardNode
        {
            public CardNode(PlayerCardDefinition card, SeasonNode season)
            {
                Card = card;
                Season = season.Season;
                Person = season.Person;
                DisplayName = season.DisplayName;
                FranchiseDisplayName = season.FranchiseDisplayName;
            }

            public PlayerCardDefinition Card { get; }
            public PlayerSeasonDefinition Season { get; }
            public PlayerPersonDefinition Person { get; }
            public string DisplayName { get; }
            public string FranchiseDisplayName { get; }
        }

        private sealed class QueryState
        {
            private readonly EncyclopediaCatalogService _owner;
            private readonly Dictionary<string, long> _wishlistSequences;

            public QueryState(EncyclopediaCatalogService owner, Dictionary<string, long> wishlistSequences)
            {
                _owner = owner;
                _wishlistSequences = wishlistSequences;
            }

            public CardStatus GetStatus(string cardId)
            {
                bool isOwned = _owner.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned);
                bool isWishlisted = _wishlistSequences.TryGetValue(cardId, out long sequence);
                return new CardStatus(
                    isOwned,
                    isOwned ? checked(owned.DuplicateCount + 1) : 0,
                    _owner._collectionHistory.WasEverAcquired(cardId),
                    isWishlisted,
                    isWishlisted ? sequence : -1L);
            }
        }

        private readonly struct CardStatus
        {
            public CardStatus(
                bool isCurrentlyOwned,
                int ownedCount,
                bool wasEverAcquired,
                bool isWishlisted,
                long wishlistAddedSequence)
            {
                IsCurrentlyOwned = isCurrentlyOwned;
                OwnedCount = ownedCount;
                WasEverAcquired = wasEverAcquired;
                IsWishlisted = isWishlisted;
                WishlistAddedSequence = wishlistAddedSequence;
            }

            public bool IsCurrentlyOwned { get; }
            public int OwnedCount { get; }
            public bool WasEverAcquired { get; }
            public bool IsWishlisted { get; }
            public long WishlistAddedSequence { get; }
        }

        private sealed class CellAccumulator
        {
            public int PlayerSeasonCount;
            public int CollectibleCardCount;
            public int EverAcquiredCardCount;
            public int OwnedCardCount;
            public int WishlistCardCount;
            public readonly HashSet<string> AcquiredSeasonIds = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class EditionAccumulator
        {
            public int Collectible;
            public int EverAcquired;
            public int Owned;
            public int Wishlisted;
        }
    }
}

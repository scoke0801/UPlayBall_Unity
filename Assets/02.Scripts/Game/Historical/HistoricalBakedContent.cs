using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Game.Historical
{
    /// <summary>Player Build에 포함된 고정 역사 콘텐츠를 순수 Definition으로 제공한다.</summary>
    public interface IHistoricalContentProvider
    {
        HistoricalBakedContent Load();
    }

    /// <summary>Archive manifest의 기존 버전과 Hash를 Runtime 경계에 그대로 전달한다.</summary>
    public sealed class HistoricalContentManifest
    {
        public HistoricalContentManifest(
            int assetFormatVersion,
            int contentSchemaVersion,
            string assetArchiveHash,
            HistoricalSourceContentManifest sourceManifest,
            string namePolicyVersion = "",
            string nameDataPolicy = "",
            string rawDataVersion = "",
            int normalizedSchemaVersion = 0,
            string normalizedImporterVersion = "",
            string normalizedContentHash = "",
            string abilityFormulaVersion = "",
            string positionRoleClassifierVersion = "",
            string rosterBuilderVersion = "",
            string costFormulaVersion = "",
            string derivationBalanceVersion = "")
        {
            if (assetFormatVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(assetFormatVersion));
            if (contentSchemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentSchemaVersion));
            if (string.IsNullOrWhiteSpace(assetArchiveHash))
                throw new ArgumentException("Asset Archive Hash는 비어 있을 수 없습니다.", nameof(assetArchiveHash));

            AssetFormatVersion = assetFormatVersion;
            ContentSchemaVersion = contentSchemaVersion;
            AssetArchiveHash = assetArchiveHash.Trim();
            SourceManifest = sourceManifest ?? throw new ArgumentNullException(nameof(sourceManifest));
            NamePolicyVersion = namePolicyVersion?.Trim() ?? string.Empty;
            NameDataPolicy = nameDataPolicy?.Trim() ?? string.Empty;
            RawDataVersion = rawDataVersion?.Trim() ?? string.Empty;
            NormalizedSchemaVersion = normalizedSchemaVersion;
            NormalizedImporterVersion = normalizedImporterVersion?.Trim() ?? string.Empty;
            NormalizedContentHash = normalizedContentHash?.Trim() ?? string.Empty;
            AbilityFormulaVersion = abilityFormulaVersion?.Trim() ?? string.Empty;
            PositionRoleClassifierVersion = positionRoleClassifierVersion?.Trim() ?? string.Empty;
            RosterBuilderVersion = rosterBuilderVersion?.Trim() ?? string.Empty;
            CostFormulaVersion = costFormulaVersion?.Trim() ?? string.Empty;
            DerivationBalanceVersion = derivationBalanceVersion?.Trim() ?? string.Empty;
        }

        public int AssetFormatVersion { get; }
        public int ContentSchemaVersion { get; }
        public string AssetArchiveHash { get; }
        public HistoricalSourceContentManifest SourceManifest { get; }
        public string NamePolicyVersion { get; }
        public string NameDataPolicy { get; }
        public string RawDataVersion { get; }
        public int NormalizedSchemaVersion { get; }
        public string NormalizedImporterVersion { get; }
        public string NormalizedContentHash { get; }
        public string AbilityFormulaVersion { get; }
        public string PositionRoleClassifierVersion { get; }
        public string RosterBuilderVersion { get; }
        public string CostFormulaVersion { get; }
        public string DerivationBalanceVersion { get; }
        public string ReferenceDataVersion => SourceManifest.ReferenceDataVersion;
        public string GeneratorVersion => SourceManifest.GeneratorVersion;
        public string BalanceVersion => SourceManifest.BalanceVersion;
        public ulong GenerationSeed => SourceManifest.GenerationSeed;
        public string ContentHash => SourceManifest.ContentHash;
    }

    /// <summary>한 연도의 고정 선수·카드·구단·원기록·원수상 Definition 묶음이다.</summary>
    public sealed class HistoricalYearContentDefinition
    {
        private readonly PlayerSeasonDefinition[] _playerSeasons;
        private readonly PlayerCardDefinition[] _normalCards;
        private readonly TeamSeasonDefinition[] _teamSeasons;
        private readonly OriginalSeasonRecordDefinition[] _originalSeasonRecords;
        private readonly OriginalAwardRecordDefinition[] _originalAwardRecords;
        private readonly IReadOnlyList<PlayerSeasonDefinition> _playerSeasonsView;
        private readonly IReadOnlyList<PlayerCardDefinition> _normalCardsView;
        private readonly IReadOnlyList<TeamSeasonDefinition> _teamSeasonsView;
        private readonly IReadOnlyList<OriginalSeasonRecordDefinition> _originalSeasonRecordsView;
        private readonly IReadOnlyList<OriginalAwardRecordDefinition> _originalAwardRecordsView;

        public HistoricalYearContentDefinition(
            int year,
            IReadOnlyList<PlayerSeasonDefinition> playerSeasons,
            IReadOnlyList<PlayerCardDefinition> normalCards,
            IReadOnlyList<TeamSeasonDefinition> teamSeasons,
            IReadOnlyList<OriginalSeasonRecordDefinition> originalSeasonRecords,
            IReadOnlyList<OriginalAwardRecordDefinition> originalAwardRecords)
        {
            if (year <= 0)
                throw new ArgumentOutOfRangeException(nameof(year));

            Year = year;
            _playerSeasons = Copy(playerSeasons, nameof(playerSeasons));
            _normalCards = Copy(normalCards, nameof(normalCards));
            _teamSeasons = Copy(teamSeasons, nameof(teamSeasons));
            _originalSeasonRecords = Copy(originalSeasonRecords, nameof(originalSeasonRecords));
            _originalAwardRecords = Copy(originalAwardRecords, nameof(originalAwardRecords));
            _playerSeasonsView = Array.AsReadOnly(_playerSeasons);
            _normalCardsView = Array.AsReadOnly(_normalCards);
            _teamSeasonsView = Array.AsReadOnly(_teamSeasons);
            _originalSeasonRecordsView = Array.AsReadOnly(_originalSeasonRecords);
            _originalAwardRecordsView = Array.AsReadOnly(_originalAwardRecords);
            ValidateYears();
        }

        public int Year { get; }
        public IReadOnlyList<PlayerSeasonDefinition> PlayerSeasons => _playerSeasonsView;
        public IReadOnlyList<PlayerCardDefinition> NormalCards => _normalCardsView;
        public IReadOnlyList<TeamSeasonDefinition> TeamSeasons => _teamSeasonsView;
        public IReadOnlyList<OriginalSeasonRecordDefinition> OriginalSeasonRecords => _originalSeasonRecordsView;
        public IReadOnlyList<OriginalAwardRecordDefinition> OriginalAwardRecords => _originalAwardRecordsView;

        private void ValidateYears()
        {
            for (int index = 0; index < _playerSeasons.Length; index++)
            {
                if (_playerSeasons[index].OriginYear != Year)
                    throw new ArgumentException("PlayerSeason의 OriginYear가 연도 묶음과 다릅니다.", nameof(_playerSeasons));
            }

            for (int index = 0; index < _normalCards.Length; index++)
            {
                if (_normalCards[index].Edition != PlayerCardEdition.Normal)
                    throw new ArgumentException("Offline Bake의 기본 카드 묶음에는 Normal Edition만 허용됩니다.", nameof(_normalCards));
            }

            for (int index = 0; index < _teamSeasons.Length; index++)
            {
                if (_teamSeasons[index].OriginYear != Year)
                    throw new ArgumentException("TeamSeason의 OriginYear가 연도 묶음과 다릅니다.", nameof(_teamSeasons));
            }

            for (int index = 0; index < _originalSeasonRecords.Length; index++)
            {
                if (_originalSeasonRecords[index].Statistics.SeasonYear != Year)
                    throw new ArgumentException("Original Record의 SeasonYear가 연도 묶음과 다릅니다.", nameof(_originalSeasonRecords));
            }

            for (int index = 0; index < _originalAwardRecords.Length; index++)
            {
                if (_originalAwardRecords[index].Award.SeasonYear != Year)
                    throw new ArgumentException("Original Award의 SeasonYear가 연도 묶음과 다릅니다.", nameof(_originalAwardRecords));
            }
        }

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);

            var result = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null Definition이 있습니다.", parameterName);
            return result;
        }
    }

    /// <summary>모든 Runtime Consumer가 공유하며 최초 구성 뒤 변경되지 않는 역사 콘텐츠 캐시다.</summary>
    public sealed class HistoricalBakedContent
    {
        private readonly PlayerPersonDefinition[] _playerPersons;
        private readonly HistoricalYearContentDefinition[] _years;
        private readonly PlayerSeasonDefinition[] _playerSeasons;
        private readonly PlayerCardDefinition[] _normalCards;
        private readonly TeamSeasonDefinition[] _teamSeasons;
        private readonly OriginalSeasonRecordDefinition[] _originalSeasonRecords;
        private readonly OriginalAwardRecordDefinition[] _originalAwardRecords;
        private readonly IReadOnlyList<PlayerPersonDefinition> _playerPersonsView;
        private readonly IReadOnlyList<HistoricalYearContentDefinition> _yearsView;
        private readonly IReadOnlyList<PlayerSeasonDefinition> _playerSeasonsView;
        private readonly IReadOnlyList<PlayerCardDefinition> _normalCardsView;
        private readonly IReadOnlyList<TeamSeasonDefinition> _teamSeasonsView;
        private readonly IReadOnlyList<OriginalSeasonRecordDefinition> _originalSeasonRecordsView;
        private readonly IReadOnlyList<OriginalAwardRecordDefinition> _originalAwardRecordsView;
        private readonly Dictionary<string, PlayerPersonDefinition> _personsById;
        private readonly Dictionary<string, PlayerSeasonDefinition> _seasonsById;
        private readonly Dictionary<string, PlayerCardDefinition> _cardsById;
        private readonly Dictionary<string, TeamSeasonDefinition> _teamsByKey;
        private readonly Dictionary<int, HistoricalYearContentDefinition> _yearsByValue;

        public HistoricalBakedContent(
            HistoricalContentManifest manifest,
            IReadOnlyList<PlayerPersonDefinition> playerPersons,
            IReadOnlyList<HistoricalYearContentDefinition> years,
            WorldIdentityNameCatalog identityNameCatalog = null)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _playerPersons = Copy(playerPersons, nameof(playerPersons));
            _years = Copy(years, nameof(years));
            if (_playerPersons.Length == 0)
                throw new ArgumentException("하나 이상의 PlayerPerson이 필요합니다.", nameof(playerPersons));
            if (_years.Length == 0)
                throw new ArgumentException("하나 이상의 연도 콘텐츠가 필요합니다.", nameof(years));
            Array.Sort(_years, CompareYears);

            _personsById = IndexPersons(_playerPersons);
            _yearsByValue = IndexYears(_years);
            int seasonCount = Count(_years, year => year.PlayerSeasons.Count);
            int cardCount = Count(_years, year => year.NormalCards.Count);
            int teamCount = Count(_years, year => year.TeamSeasons.Count);
            int recordCount = Count(_years, year => year.OriginalSeasonRecords.Count);
            int awardCount = Count(_years, year => year.OriginalAwardRecords.Count);
            _playerSeasons = new PlayerSeasonDefinition[seasonCount];
            _normalCards = new PlayerCardDefinition[cardCount];
            _teamSeasons = new TeamSeasonDefinition[teamCount];
            _originalSeasonRecords = new OriginalSeasonRecordDefinition[recordCount];
            _originalAwardRecords = new OriginalAwardRecordDefinition[awardCount];
            Flatten(_years, _playerSeasons, year => year.PlayerSeasons);
            Flatten(_years, _normalCards, year => year.NormalCards);
            Flatten(_years, _teamSeasons, year => year.TeamSeasons);
            Flatten(_years, _originalSeasonRecords, year => year.OriginalSeasonRecords);
            Flatten(_years, _originalAwardRecords, year => year.OriginalAwardRecords);

            _playerPersonsView = Array.AsReadOnly(_playerPersons);
            _yearsView = Array.AsReadOnly(_years);
            _playerSeasonsView = Array.AsReadOnly(_playerSeasons);
            _normalCardsView = Array.AsReadOnly(_normalCards);
            _teamSeasonsView = Array.AsReadOnly(_teamSeasons);
            _originalSeasonRecordsView = Array.AsReadOnly(_originalSeasonRecords);
            _originalAwardRecordsView = Array.AsReadOnly(_originalAwardRecords);

            _seasonsById = IndexUnique(_playerSeasons, item => item.PlayerSeasonId, "PlayerSeasonId");
            _cardsById = IndexUnique(_normalCards, item => item.CardId, "CardId");
            _teamsByKey = IndexUnique(_teamSeasons, item => item.TeamSeasonKey, "TeamSeasonKey");
            ValidateReferences();
            IdentityNameCatalog = identityNameCatalog ?? CreateDevelopmentIdentityNameCatalog(_playerPersons);
        }

        public HistoricalContentManifest Manifest { get; }
        public WorldIdentityNameCatalog IdentityNameCatalog { get; }
        public IReadOnlyList<PlayerPersonDefinition> PlayerPersons => _playerPersonsView;
        public IReadOnlyList<HistoricalYearContentDefinition> Years => _yearsView;
        public IReadOnlyList<PlayerSeasonDefinition> PlayerSeasons => _playerSeasonsView;
        public IReadOnlyList<PlayerCardDefinition> NormalCards => _normalCardsView;
        public IReadOnlyList<TeamSeasonDefinition> TeamSeasons => _teamSeasonsView;
        public IReadOnlyList<OriginalSeasonRecordDefinition> OriginalSeasonRecords => _originalSeasonRecordsView;
        public IReadOnlyList<OriginalAwardRecordDefinition> OriginalAwardRecords => _originalAwardRecordsView;

        public HistoricalYearContentDefinition GetYear(int year)
        {
            if (!_yearsByValue.TryGetValue(year, out HistoricalYearContentDefinition content))
                throw new KeyNotFoundException($"Historical Year {year} 콘텐츠가 없습니다.");
            return content;
        }

        public bool TryGetPlayerPerson(string playerPersonId, out PlayerPersonDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
            {
                definition = null;
                return false;
            }
            return _personsById.TryGetValue(playerPersonId, out definition);
        }

        public bool TryGetPlayerSeason(string playerSeasonId, out PlayerSeasonDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
            {
                definition = null;
                return false;
            }
            return _seasonsById.TryGetValue(playerSeasonId, out definition);
        }

        public bool TryGetNormalCard(string cardId, out PlayerCardDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                definition = null;
                return false;
            }
            return _cardsById.TryGetValue(cardId, out definition);
        }

        public bool TryGetTeamSeason(string teamSeasonKey, out TeamSeasonDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
            {
                definition = null;
                return false;
            }
            return _teamsByKey.TryGetValue(teamSeasonKey, out definition);
        }

        private static WorldIdentityNameCatalog CreateDevelopmentIdentityNameCatalog(
            IReadOnlyList<PlayerPersonDefinition> persons)
        {
            const string surnames = "김이박최정강조윤장임한오서신권황안송홍전고문양손배백허유남심노하곽";
            const string firstSyllables = "민서지현준우성도하윤시재태수영진호건주혁찬승원정규경동환희예은재";
            const string secondSyllables = "준우호진혁민석현수빈영원성훈환재윤찬건규하도경태욱승율희아린";
            var domestic = new List<string>(persons.Count);
            var foreign = new List<string>();
            int candidate = 0;
            for (int index = 0; index < persons.Count; index++)
            {
                PlayerPersonDefinition person = persons[index];
                if (person.RegistrationType == RegistrationType.Foreign)
                {
                    foreign.Add(CreateForeignDevelopmentName(candidate++));
                    continue;
                }
                int surnameIndex = candidate % surnames.Length;
                int firstIndex = (candidate / surnames.Length) % firstSyllables.Length;
                int secondIndex = (candidate / (surnames.Length * firstSyllables.Length)) % secondSyllables.Length;
                domestic.Add(string.Concat(
                    surnames[surnameIndex],
                    firstSyllables[firstIndex],
                    secondSyllables[secondIndex]));
                candidate++;
            }
            return new WorldIdentityNameCatalog(domestic, foreign, CreateDefaultFranchiseNames());
        }

        private static string CreateForeignDevelopmentName(int index)
        {
            string[] given = { "Liam", "Noah", "Ethan", "Lucas", "Mateo", "Adrian", "Julian", "Marco" };
            string[] family = { "Carter", "Bennett", "Foster", "Hayes", "Morgan", "Reed", "Turner", "Walker" };
            return given[index % given.Length] + " " + family[(index / given.Length) % family.Length];
        }

        internal static string[] CreateDefaultFranchiseNames()
        {
            return new[]
            {
                "서울 코멧츠", "부산 타이즈", "인천 하버스", "대구 포지", "대전 파이오니어스",
                "광주 피닉스", "수원 가디언즈", "창원 세일러스", "전주 스타즈", "강릉 웨이브스",
                "울산 오로라", "제주 윈드스", "춘천 레이븐스", "성남 볼츠", "청주 크레인스",
                "포항 트라이던츠", "고양 스카이라인", "용인 스톰즈", "천안 브레이브스", "김해 팔콘스"
            };
        }

        private void ValidateReferences()
        {
            for (int index = 0; index < _playerSeasons.Length; index++)
            {
                PlayerSeasonDefinition season = _playerSeasons[index];
                if (!_personsById.ContainsKey(season.PlayerPersonId))
                    throw new ArgumentException($"PlayerSeason {season.PlayerSeasonId}의 PlayerPerson을 찾을 수 없습니다.");
                if (!_teamsByKey.TryGetValue(season.OriginTeamSeasonKey, out TeamSeasonDefinition team))
                    throw new ArgumentException($"PlayerSeason {season.PlayerSeasonId}의 원소속 TeamSeason을 찾을 수 없습니다.");
                if (team.OriginYear != season.OriginYear ||
                    !string.Equals(team.FranchiseId, season.OriginFranchiseId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"SEASON_RECORD_CROSS_YEAR_REFERENCE: PlayerSeason {season.PlayerSeasonId}의 원소속 TeamSeason 연도/Franchise가 다릅니다.");
                }
            }

            var normalSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _normalCards.Length; index++)
            {
                PlayerCardDefinition card = _normalCards[index];
                if (!_seasonsById.ContainsKey(card.PlayerSeasonId))
                    throw new ArgumentException($"Normal Card {card.CardId}의 PlayerSeason을 찾을 수 없습니다.");
                string expectedCardId = PlayerCardDefinition.CreateStableCardId(
                    card.PlayerSeasonId,
                    PlayerCardEdition.Normal);
                if (!string.Equals(card.CardId, expectedCardId, StringComparison.Ordinal))
                    throw new ArgumentException($"Normal Card {card.CardId}가 Stable CardId 규칙과 다릅니다.");
                if (!normalSeasonIds.Add(card.PlayerSeasonId))
                    throw new ArgumentException($"PlayerSeason {card.PlayerSeasonId}의 Normal Card가 중복되었습니다.");
            }

            if (normalSeasonIds.Count != _playerSeasons.Length)
                throw new ArgumentException("모든 PlayerSeason에는 정확히 하나의 Normal Card가 필요합니다.");

            for (int teamIndex = 0; teamIndex < _teamSeasons.Length; teamIndex++)
                ValidateTeamCardReferences(_teamSeasons[teamIndex]);

            for (int index = 0; index < _originalSeasonRecords.Length; index++)
            {
                SeasonStatistics statistics = _originalSeasonRecords[index].Statistics;
                if (!_seasonsById.TryGetValue(statistics.PlayerSeasonId, out PlayerSeasonDefinition season))
                    throw new ArgumentException($"Original Record {statistics.PlayerSeasonId}의 PlayerSeason을 찾을 수 없습니다.");
                if (!_teamsByKey.ContainsKey(statistics.TeamSeasonKey))
                    throw new ArgumentException($"Original Record {statistics.PlayerSeasonId}의 TeamSeason을 찾을 수 없습니다.");
                if (statistics.SeasonYear != season.OriginYear ||
                    !string.Equals(statistics.TeamSeasonKey, season.OriginTeamSeasonKey, StringComparison.Ordinal) ||
                    !HasMatchingRecordPosition(statistics.Position, season))
                {
                    throw new ArgumentException(
                        $"SEASON_RECORD_CROSS_YEAR_REFERENCE: Original Record {statistics.PlayerSeasonId}의 연도/Team/Position이 PlayerSeason과 다릅니다.");
                }
            }

            for (int index = 0; index < _originalAwardRecords.Length; index++)
            {
                WorldAwardEntry award = _originalAwardRecords[index].Award;
                if (!_seasonsById.TryGetValue(award.PlayerSeasonId, out PlayerSeasonDefinition season))
                    throw new ArgumentException($"Original Award {award.PlayerSeasonId}의 PlayerSeason을 찾을 수 없습니다.");
                if (award.SeasonYear != season.OriginYear)
                {
                    throw new ArgumentException(
                        $"SEASON_RECORD_CROSS_YEAR_REFERENCE: Original Award {award.PlayerSeasonId}의 연도가 PlayerSeason과 다릅니다.");
                }
            }
        }

        private static bool HasMatchingRecordPosition(
            PlayerPosition recordPosition,
            PlayerSeasonDefinition season)
        {
            if (season.PlayerType != PlayerType.Pitcher)
                return recordPosition == season.Position;

            // Original Record의 P 표기는 선발/구원 역할을 담지 않는다. Natural PitcherRole은
            // PlayerSeasonDefinition이 소유하므로 두 투수 포지션 값은 같은 기록 포지션으로 본다.
            return (recordPosition == PlayerPosition.StartingPitcher ||
                    recordPosition == PlayerPosition.ReliefPitcher) &&
                   (season.Position == PlayerPosition.StartingPitcher ||
                    season.Position == PlayerPosition.ReliefPitcher);
        }

        private void ValidateTeamCardReferences(TeamSeasonDefinition team)
        {
            var allCards = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < team.AllNormalCardIds.Count; index++)
            {
                string cardId = team.AllNormalCardIds[index];
                if (!_cardsById.TryGetValue(cardId, out PlayerCardDefinition card))
                    throw new ArgumentException($"TeamSeason {team.TeamSeasonKey}가 없는 Normal Card {cardId}를 참조합니다.");
                PlayerSeasonDefinition season = _seasonsById[card.PlayerSeasonId];
                if (!string.Equals(season.OriginTeamSeasonKey, team.TeamSeasonKey, StringComparison.Ordinal))
                    throw new ArgumentException($"TeamSeason {team.TeamSeasonKey}의 카드 {cardId}가 다른 원소속을 가리킵니다.");
                allCards.Add(cardId);
            }

            for (int index = 0; index < team.Core25CardIds.Count; index++)
            {
                string cardId = team.Core25CardIds[index];
                if (!allCards.Contains(cardId))
                    throw new ArgumentException($"TeamSeason {team.TeamSeasonKey}의 Core25 카드 {cardId}가 전체 Pool에 없습니다.");
            }
        }

        private static PlayerPersonDefinition[] Copy(
            IReadOnlyList<PlayerPersonDefinition> source,
            string parameterName)
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            var result = new PlayerPersonDefinition[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null PlayerPerson이 있습니다.", parameterName);
            return result;
        }

        private static HistoricalYearContentDefinition[] Copy(
            IReadOnlyList<HistoricalYearContentDefinition> source,
            string parameterName)
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            var result = new HistoricalYearContentDefinition[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 연도 콘텐츠가 있습니다.", parameterName);
            return result;
        }

        private static Dictionary<string, PlayerPersonDefinition> IndexPersons(
            IReadOnlyList<PlayerPersonDefinition> source)
        {
            return IndexUnique(source, item => item.PlayerPersonId, "PlayerPersonId");
        }

        private static Dictionary<int, HistoricalYearContentDefinition> IndexYears(
            IReadOnlyList<HistoricalYearContentDefinition> source)
        {
            var result = new Dictionary<int, HistoricalYearContentDefinition>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                if (!result.TryAdd(source[index].Year, source[index]))
                    throw new ArgumentException($"Historical Year {source[index].Year}가 중복되었습니다.");
            }
            return result;
        }

        private static Dictionary<string, T> IndexUnique<T>(
            IReadOnlyList<T> source,
            Func<T, string> keySelector,
            string keyName)
        {
            var result = new Dictionary<string, T>(source.Count, StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string key = keySelector(source[index]);
                if (!result.TryAdd(key, source[index]))
                    throw new ArgumentException($"{keyName} {key}가 중복되었습니다.");
            }
            return result;
        }

        private static int Count(
            IReadOnlyList<HistoricalYearContentDefinition> years,
            Func<HistoricalYearContentDefinition, int> selector)
        {
            int result = 0;
            for (int index = 0; index < years.Count; index++)
                result = checked(result + selector(years[index]));
            return result;
        }

        private static void Flatten<T>(
            IReadOnlyList<HistoricalYearContentDefinition> years,
            T[] destination,
            Func<HistoricalYearContentDefinition, IReadOnlyList<T>> selector)
        {
            int destinationIndex = 0;
            for (int yearIndex = 0; yearIndex < years.Count; yearIndex++)
            {
                IReadOnlyList<T> source = selector(years[yearIndex]);
                for (int sourceIndex = 0; sourceIndex < source.Count; sourceIndex++)
                    destination[destinationIndex++] = source[sourceIndex];
            }
        }

        private static int CompareYears(
            HistoricalYearContentDefinition left,
            HistoricalYearContentDefinition right)
        {
            return left.Year.CompareTo(right.Year);
        }
    }
}

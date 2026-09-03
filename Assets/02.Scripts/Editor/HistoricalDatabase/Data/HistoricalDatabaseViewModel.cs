using System;
using System.Collections.Generic;
using System.IO;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Repository Cache와 Player Query 및 Entity 간 이동을 Editor Window에 제공한다.</summary>
    public sealed class HistoricalDatabaseViewModel
    {
        private readonly HistoricalArchiveRepository _repository;
        private readonly Dictionary<object, string> _rawJsonCache = new Dictionary<object, string>();
        private readonly Dictionary<string, string> _rawSourceTextCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<HistoricalPlayerRow> _visiblePlayers = Array.Empty<HistoricalPlayerRow>();

        public HistoricalDatabaseViewModel(HistoricalArchiveRepository repository = null)
        {
            _repository = repository ?? new HistoricalArchiveRepository();
            Filter = new HistoricalPlayerFilter();
        }

        public HistoricalArchiveData Data { get; private set; }
        public HistoricalPlayerFilter Filter { get; }
        public HistoricalPlayerSortField SortField { get; set; } = HistoricalPlayerSortField.Name;
        public HistoricalSortDirection SortDirection { get; set; } = HistoricalSortDirection.Ascending;
        public IReadOnlyList<HistoricalPlayerRow> VisiblePlayers => _visiblePlayers;
        public bool IsLoaded => Data != null;

        /// <summary>새 Source Folder를 Load하고 기존 Query를 새 Archive에 다시 적용한다.</summary>
        public HistoricalArchiveData Load(string path, IProgress<HistoricalLoadProgress> progress = null)
        {
            SetData(_repository.Load(path, progress));
            return Data;
        }

        /// <summary>Repository 밖에서 준비한 Data를 연결해 ViewModel 단위 테스트를 지원한다.</summary>
        public void SetData(HistoricalArchiveData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _rawJsonCache.Clear();
            _rawSourceTextCache.Clear();
            ApplyQuery();
        }

        /// <summary>현재 Filter와 Sort를 적용해 Virtualized List의 Source를 갱신한다.</summary>
        public IReadOnlyList<HistoricalPlayerRow> ApplyQuery()
        {
            if (Data == null)
            {
                _visiblePlayers = Array.Empty<HistoricalPlayerRow>();
                return _visiblePlayers;
            }

            var filtered = new List<HistoricalPlayerRow>();
            for (int index = 0; index < Data.PlayerRows.Count; index++)
            {
                HistoricalPlayerRow row = Data.PlayerRows[index];
                if (Filter.Matches(row))
                    filtered.Add(row);
            }
            _visiblePlayers = HistoricalPlayerSorter.Sort(filtered, SortField, SortDirection);
            return _visiblePlayers;
        }

        /// <summary>Filter를 초기화하고 전체 PlayerSeason 목록을 다시 표시한다.</summary>
        public IReadOnlyList<HistoricalPlayerRow> ResetFilter()
        {
            Filter.Reset();
            return ApplyQuery();
        }

        /// <summary>안정 PlayerSeasonId로 Player Detail 행을 찾는다.</summary>
        public HistoricalPlayerRow FindPlayer(string playerSeasonId)
        {
            if (Data == null || string.IsNullOrWhiteSpace(playerSeasonId))
                return null;
            Data.PlayersBySeasonId.TryGetValue(playerSeasonId, out HistoricalPlayerRow row);
            return row;
        }

        /// <summary>PlayerPersonId로 인물 정의를 찾는다.</summary>
        public HistoricalPlayerPerson FindPerson(string playerPersonId)
        {
            if (Data == null || string.IsNullOrWhiteSpace(playerPersonId))
                return null;
            Data.PersonsById.TryGetValue(playerPersonId, out HistoricalPlayerPerson person);
            return person;
        }

        /// <summary>TeamSeasonKey로 Team Detail을 찾는다.</summary>
        public HistoricalTeamSeason FindTeam(string teamSeasonKey)
        {
            if (Data == null || string.IsNullOrWhiteSpace(teamSeasonKey))
                return null;
            Data.TeamsByKey.TryGetValue(teamSeasonKey, out HistoricalTeamSeason team);
            return team;
        }

        /// <summary>CardId가 참조하는 PlayerSeason 행을 찾는다.</summary>
        public HistoricalPlayerRow FindPlayerByCardId(string cardId)
        {
            if (Data == null || string.IsNullOrWhiteSpace(cardId)
                || !Data.CardsById.TryGetValue(cardId, out HistoricalCard card))
            {
                return null;
            }
            return FindPlayer(card.PlayerSeasonId);
        }

        /// <summary>같은 PlayerPerson의 시즌들을 연도·안정 ID 순서로 반환한다.</summary>
        public IReadOnlyList<HistoricalPlayerRow> FindPersonCareer(string playerPersonId)
        {
            if (Data == null || string.IsNullOrWhiteSpace(playerPersonId)
                || !Data.PlayersByPersonId.TryGetValue(playerPersonId, out HistoricalPlayerRow[] career))
            {
                return Array.Empty<HistoricalPlayerRow>();
            }
            return career;
        }

        /// <summary>PlayerSeason에 연결된 모든 Original Award를 반환한다.</summary>
        public IReadOnlyList<HistoricalAwardRecord> FindPlayerAwards(string playerSeasonId)
        {
            if (Data == null || string.IsNullOrWhiteSpace(playerSeasonId)
                || !Data.AwardsBySeasonId.TryGetValue(playerSeasonId, out HistoricalAwardRecord[] awards))
            {
                return Array.Empty<HistoricalAwardRecord>();
            }
            return awards;
        }

        /// <summary>Team의 Core25를 JSON 순서 그대로 Player 행으로 연결한다.</summary>
        public IReadOnlyList<HistoricalPlayerRow> FindCoreRoster(string teamSeasonKey)
        {
            HistoricalTeamSeason team = FindTeam(teamSeasonKey);
            return team == null ? Array.Empty<HistoricalPlayerRow>() : ResolveCards(team.Core25CardIds);
        }

        /// <summary>Team의 전체 Normal Card Pool을 JSON 순서 그대로 Player 행으로 연결한다.</summary>
        public IReadOnlyList<HistoricalPlayerRow> FindPlayerPool(string teamSeasonKey)
        {
            HistoricalTeamSeason team = FindTeam(teamSeasonKey);
            return team == null ? Array.Empty<HistoricalPlayerRow>() : ResolveCards(team.AllNormalCardIds);
        }

        /// <summary>선택한 Entity가 들어 있는 원본 파일에서 해당 JSON object만 정확히 추출한다.</summary>
        public bool TryGetRawJson(object entity, out string rawJson, out string error)
        {
            if (entity == null)
            {
                rawJson = string.Empty;
                error = "Raw JSON을 지원하지 않는 Entity입니다.";
                return false;
            }
            if (_rawJsonCache.TryGetValue(entity, out rawJson))
            {
                error = string.Empty;
                return true;
            }

            bool succeeded;
            switch (entity)
            {
                case HistoricalPlayerRow row:
                    succeeded = TryExtractRaw(
                        row.Season.SourcePath,
                        "playerSeasons",
                        "playerSeasonId",
                        row.PlayerSeasonId,
                        out rawJson,
                        out error);
                    break;
                case HistoricalPlayerPerson person:
                    succeeded = TryExtractRaw(
                        person.SourcePath,
                        null,
                        "playerPersonId",
                        person.PlayerPersonId,
                        out rawJson,
                        out error);
                    break;
                case HistoricalPlayerSeason season:
                    succeeded = TryExtractRaw(
                        season.SourcePath,
                        "playerSeasons",
                        "playerSeasonId",
                        season.PlayerSeasonId,
                        out rawJson,
                        out error);
                    break;
                case HistoricalTeamSeason team:
                    succeeded = TryExtractRaw(
                        team.SourcePath,
                        "teamSeasons",
                        "teamSeasonKey",
                        team.TeamSeasonKey,
                        out rawJson,
                        out error);
                    break;
                case HistoricalCard card:
                    succeeded = TryExtractRaw(
                        card.SourcePath,
                        "normalCards",
                        "cardId",
                        card.CardId,
                        out rawJson,
                        out error);
                    break;
                case HistoricalSeasonRecord record:
                    succeeded = TryExtractRaw(
                        record.SourcePath,
                        "originalSeasonRecords",
                        "playerSeasonId",
                        record.PlayerSeasonId,
                        out rawJson,
                        out error);
                    break;
                case HistoricalAwardRecord award:
                    succeeded = TryExtractRaw(
                        award.SourcePath,
                        "originalAwardRecords",
                        new Dictionary<string, string>
                        {
                            { "playerSeasonId", award.PlayerSeasonId },
                            { "awardType", award.AwardType },
                            { "position", award.Position }
                        },
                        out rawJson,
                        out error);
                    break;
                default:
                    rawJson = string.Empty;
                    error = "Raw JSON을 지원하지 않는 Entity입니다.";
                    return false;
            }

            if (succeeded)
                _rawJsonCache[entity] = rawJson;
            return succeeded;
        }

        private bool TryExtractRaw(
            string sourcePath,
            string collectionProperty,
            string idProperty,
            string idValue,
            out string rawJson,
            out string error)
        {
            return TryExtractRaw(
                sourcePath,
                collectionProperty,
                new Dictionary<string, string> { { idProperty, idValue } },
                out rawJson,
                out error);
        }

        private bool TryExtractRaw(
            string sourcePath,
            string collectionProperty,
            IReadOnlyDictionary<string, string> requiredProperties,
            out string rawJson,
            out string error)
        {
            rawJson = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                error = "원본 JSON 경로가 비어 있습니다.";
                return false;
            }

            if (!_rawSourceTextCache.TryGetValue(sourcePath, out string sourceText))
            {
                try
                {
                    sourceText = File.ReadAllText(sourcePath);
                    _rawSourceTextCache.Add(sourcePath, sourceText);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is NotSupportedException)
                {
                    error = $"원본 JSON을 읽지 못했습니다: {exception.Message}";
                    return false;
                }
            }

            return HistoricalRawJsonExtractor.TryExtractObjectFromJson(
                sourceText,
                collectionProperty,
                requiredProperties,
                out rawJson,
                out error);
        }

        private IReadOnlyList<HistoricalPlayerRow> ResolveCards(IReadOnlyList<string> cardIds)
        {
            var result = new List<HistoricalPlayerRow>(cardIds.Count);
            for (int index = 0; index < cardIds.Count; index++)
            {
                HistoricalPlayerRow row = FindPlayerByCardId(cardIds[index]);
                if (row != null)
                    result.Add(row);
            }
            return result;
        }
    }
}

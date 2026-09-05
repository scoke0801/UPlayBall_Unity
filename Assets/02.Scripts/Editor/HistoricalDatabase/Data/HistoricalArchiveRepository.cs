using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Archive 폴더 선택 직후 UI에 표시할 경로 유효성 결과다.</summary>
    public readonly struct HistoricalArchivePathValidation
    {
        public HistoricalArchivePathValidation(bool isValid, string normalizedPath, string message)
        {
            IsValid = isValid;
            NormalizedPath = normalizedPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public string NormalizedPath { get; }
        public string Message { get; }
    }

    /// <summary>Repository Load 진행률을 UI Toolkit ProgressBar에 전달하는 값이다.</summary>
    public readonly struct HistoricalLoadProgress
    {
        public HistoricalLoadProgress(string stage, int current, int total, string message)
        {
            Stage = stage ?? string.Empty;
            Current = current;
            Total = total;
            Message = message ?? string.Empty;
        }

        public string Stage { get; }
        public int Current { get; }
        public int Total { get; }
        public string Message { get; }
        public float Ratio => Total <= 0 ? 0f : Math.Min(1f, Math.Max(0f, Current / (float)Total));
    }

    /// <summary>사용자가 지정한 JSON Archive를 직접 읽고 Editor Memory용 조회 모델을 만든다.</summary>
    public sealed class HistoricalArchiveRepository
    {
        private const string ManifestFileName = "manifest.json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>폴더와 manifest 존재 여부 및 최소 manifest 구조를 확인한다.</summary>
        public HistoricalArchivePathValidation ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new HistoricalArchivePathValidation(false, string.Empty, "Historical Database 폴더를 선택해 주세요.");

            string normalizedPath;
            try
            {
                normalizedPath = NormalizeFolder(path);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            {
                return new HistoricalArchivePathValidation(false, string.Empty, $"올바르지 않은 경로입니다: {exception.Message}");
            }

            if (!Directory.Exists(normalizedPath))
                return new HistoricalArchivePathValidation(false, normalizedPath, "선택한 폴더가 존재하지 않습니다.");

            string manifestPath = System.IO.Path.Combine(normalizedPath, ManifestFileName);
            if (!File.Exists(manifestPath))
                return new HistoricalArchivePathValidation(false, normalizedPath, "manifest.json을 찾을 수 없습니다.");

            try
            {
                HistoricalArchiveManifest manifest = ParseJson<HistoricalArchiveManifest>(
                    File.ReadAllText(manifestPath),
                    manifestPath);
                if (manifest.PlayerPersons == null || string.IsNullOrWhiteSpace(manifest.PlayerPersons.Path))
                    return new HistoricalArchivePathValidation(false, normalizedPath, "manifest에 playerPersons 파일 정보가 없습니다.");
                if (manifest.Years.Count == 0)
                    return new HistoricalArchivePathValidation(false, normalizedPath, "manifest에 연도 파일 정보가 없습니다.");
            }
            catch (Exception exception)
            {
                return new HistoricalArchivePathValidation(false, normalizedPath, $"manifest.json을 읽지 못했습니다: {exception.Message}");
            }

            return new HistoricalArchivePathValidation(true, normalizedPath, "Historical Archive를 불러올 수 있습니다.");
        }

        /// <summary>manifest와 모든 분할 JSON을 읽고 Person·Season·Record·Award Join을 한 번 구축한다.</summary>
        public HistoricalArchiveData Load(string path, IProgress<HistoricalLoadProgress> progress = null)
        {
            HistoricalArchivePathValidation pathValidation = ValidatePath(path);
            if (!pathValidation.IsValid)
                throw new InvalidDataException(pathValidation.Message);

            var stopwatch = Stopwatch.StartNew();
            string sourceFolder = pathValidation.NormalizedPath;
            string manifestPath = System.IO.Path.Combine(sourceFolder, ManifestFileName);
            progress?.Report(new HistoricalLoadProgress("Manifest", 0, 1, "manifest.json을 읽는 중입니다."));
            HistoricalArchiveManifest manifest = ParseJson<HistoricalArchiveManifest>(
                File.ReadAllText(manifestPath),
                manifestPath);

            int personCapacity = Math.Max(0, manifest.Summary?.PlayerPersonCount ?? manifest.PlayerPersons.Count);
            int seasonCapacity = Math.Max(0, manifest.Summary?.PlayerSeasonCount ?? 0);
            int teamCapacity = Math.Max(0, manifest.Summary?.TeamSeasonCount ?? 0);
            int cardCapacity = Math.Max(0, manifest.Summary?.NormalCardCount ?? 0);
            int recordCapacity = Math.Max(0, manifest.Summary?.OriginalRecordCount ?? 0);
            int awardCapacity = Math.Max(0, manifest.Summary?.OriginalAwardCount ?? 0);
            var persons = new List<HistoricalPlayerPerson>(personCapacity);
            var seasons = new List<HistoricalPlayerSeason>(seasonCapacity);
            var teams = new List<HistoricalTeamSeason>(teamCapacity);
            var cards = new List<HistoricalCard>(cardCapacity);
            var records = new List<HistoricalSeasonRecord>(recordCapacity);
            var awards = new List<HistoricalAwardRecord>(awardCapacity);
            var sourceFiles = new List<HistoricalSourceFileInfo>(manifest.Years.Count + 1);
            var yearSourcePaths = new Dictionary<int, string>();
            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(manifestPath);

            progress?.Report(new HistoricalLoadProgress("Persons", 0, 1, "player_persons.json을 읽는 중입니다."));
            string personsPath = ResolveArchivePath(sourceFolder, manifest.PlayerPersons.Path);
            string personsJson = ReadSourceFile(personsPath, manifest.PlayerPersons, sourceFiles, ref lastWriteUtc);
            HistoricalPlayerPersonArray personArray = ParseTopLevelPersonArray(personsJson, personsPath);
            HistoricalPlayerPerson[] loadedPersons = personArray.Items;
            for (int index = 0; index < loadedPersons.Length; index++)
            {
                HistoricalPlayerPerson person = loadedPersons[index];
                if (person == null)
                    continue;
                person.SetSourcePath(personsPath);
                persons.Add(person);
            }

            var yearEntries = new List<HistoricalArchiveYearEntry>(manifest.Years);
            yearEntries.Sort((left, right) => left.Year.CompareTo(right.Year));
            for (int yearIndex = 0; yearIndex < yearEntries.Count; yearIndex++)
            {
                HistoricalArchiveYearEntry entry = yearEntries[yearIndex];
                progress?.Report(new HistoricalLoadProgress(
                    "Years",
                    yearIndex,
                    yearEntries.Count,
                    $"{entry.Year.ToString(CultureInfo.InvariantCulture)} 시즌을 읽는 중입니다."));
                string yearPath = ResolveArchivePath(sourceFolder, entry.Path);
                string json = ReadSourceFile(yearPath, entry, sourceFiles, ref lastWriteUtc);
                HistoricalYearContent content = ParseJson<HistoricalYearContent>(json, yearPath);
                if (!yearSourcePaths.ContainsKey(entry.Year))
                    yearSourcePaths.Add(entry.Year, yearPath);
                AddYearContent(content, yearPath, seasons, teams, cards, records, awards);
            }

            progress?.Report(new HistoricalLoadProgress("Joining", 0, 1, "PlayerSeason 관계를 연결하는 중입니다."));
            HistoricalArchiveData result = BuildData(
                manifest,
                sourceFolder,
                stopwatch,
                lastWriteUtc,
                persons,
                seasons,
                teams,
                awards,
                cards,
                records,
                sourceFiles,
                yearSourcePaths);
            progress?.Report(new HistoricalLoadProgress("Completed", 1, 1, "Historical Archive Load가 완료되었습니다."));
            return result;
        }

        private static HistoricalArchiveData BuildData(
            HistoricalArchiveManifest manifest,
            string sourceFolder,
            Stopwatch stopwatch,
            DateTime lastWriteUtc,
            List<HistoricalPlayerPerson> persons,
            List<HistoricalPlayerSeason> seasons,
            List<HistoricalTeamSeason> teams,
            List<HistoricalAwardRecord> awards,
            List<HistoricalCard> cards,
            List<HistoricalSeasonRecord> records,
            List<HistoricalSourceFileInfo> sourceFiles,
            Dictionary<int, string> yearSourcePaths)
        {
            var personsById = IndexFirst(persons, person => person.PlayerPersonId);
            var teamsByKey = IndexFirst(teams, team => team.TeamSeasonKey);
            var cardsById = IndexFirst(cards, card => card.CardId);
            var recordsBySeasonId = IndexFirst(records, record => record.PlayerSeasonId);
            var awardListsBySeasonId = GroupByKey(awards, award => award.PlayerSeasonId);
            var playerRows = new HistoricalPlayerRow[seasons.Count];
            var playersBySeasonId = new Dictionary<string, HistoricalPlayerRow>(StringComparer.Ordinal);
            var careerLists = new Dictionary<string, List<HistoricalPlayerRow>>(StringComparer.Ordinal);

            for (int index = 0; index < seasons.Count; index++)
            {
                HistoricalPlayerSeason season = seasons[index];
                personsById.TryGetValue(season.PlayerPersonId, out HistoricalPlayerPerson person);
                recordsBySeasonId.TryGetValue(season.PlayerSeasonId, out HistoricalSeasonRecord record);
                awardListsBySeasonId.TryGetValue(season.PlayerSeasonId, out List<HistoricalAwardRecord> playerAwards);
                var row = new HistoricalPlayerRow(
                    person,
                    season,
                    record,
                    playerAwards?.ToArray() ?? Array.Empty<HistoricalAwardRecord>());
                playerRows[index] = row;
                if (!playersBySeasonId.ContainsKey(row.PlayerSeasonId))
                    playersBySeasonId.Add(row.PlayerSeasonId, row);
                if (!careerLists.TryGetValue(row.PlayerPersonId, out List<HistoricalPlayerRow> career))
                {
                    career = new List<HistoricalPlayerRow>();
                    careerLists.Add(row.PlayerPersonId, career);
                }
                career.Add(row);
            }

            var playersByPersonId = new Dictionary<string, HistoricalPlayerRow[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<HistoricalPlayerRow>> pair in careerLists)
            {
                pair.Value.Sort(CompareCareerRows);
                playersByPersonId.Add(pair.Key, pair.Value.ToArray());
            }

            var awardsBySeasonId = new Dictionary<string, HistoricalAwardRecord[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<HistoricalAwardRecord>> pair in awardListsBySeasonId)
                awardsBySeasonId.Add(pair.Key, pair.Value.ToArray());

            stopwatch.Stop();
            return new HistoricalArchiveData(
                manifest,
                sourceFolder,
                stopwatch.Elapsed,
                lastWriteUtc,
                persons.ToArray(),
                playerRows,
                teams.ToArray(),
                awards.ToArray(),
                cards.ToArray(),
                records.ToArray(),
                sourceFiles.ToArray(),
                personsById,
                playersBySeasonId,
                teamsByKey,
                cardsById,
                recordsBySeasonId,
                playersByPersonId,
                awardsBySeasonId,
                yearSourcePaths);
        }

        private static void AddYearContent(
            HistoricalYearContent content,
            string sourcePath,
            List<HistoricalPlayerSeason> seasons,
            List<HistoricalTeamSeason> teams,
            List<HistoricalCard> cards,
            List<HistoricalSeasonRecord> records,
            List<HistoricalAwardRecord> awards)
        {
            for (int index = 0; index < content.PlayerSeasons.Length; index++)
            {
                HistoricalPlayerSeason season = content.PlayerSeasons[index];
                if (season == null) continue;
                season.SetSourcePath(sourcePath);
                seasons.Add(season);
            }
            for (int index = 0; index < content.TeamSeasons.Length; index++)
            {
                HistoricalTeamSeason team = content.TeamSeasons[index];
                if (team == null) continue;
                team.SetSourcePath(sourcePath);
                teams.Add(team);
            }
            for (int index = 0; index < content.NormalCards.Length; index++)
            {
                HistoricalCard card = content.NormalCards[index];
                if (card == null) continue;
                card.SetSourcePath(sourcePath);
                cards.Add(card);
            }
            for (int index = 0; index < content.OriginalSeasonRecords.Length; index++)
            {
                HistoricalSeasonRecord record = content.OriginalSeasonRecords[index];
                if (record == null) continue;
                record.SetSourcePath(sourcePath);
                records.Add(record);
            }
            for (int index = 0; index < content.OriginalAwardRecords.Length; index++)
            {
                HistoricalAwardRecord award = content.OriginalAwardRecords[index];
                if (award == null) continue;
                award.SetSourcePath(sourcePath);
                awards.Add(award);
            }
        }

        private static string ReadSourceFile(
            string fullPath,
            HistoricalArchiveFileEntry entry,
            List<HistoricalSourceFileInfo> sourceFiles,
            ref DateTime lastWriteUtc)
        {
            return ReadSourceFile(
                fullPath,
                entry.Path,
                entry.Sha256,
                entry.ByteLength,
                sourceFiles,
                ref lastWriteUtc);
        }

        private static string ReadSourceFile(
            string fullPath,
            HistoricalArchiveYearEntry entry,
            List<HistoricalSourceFileInfo> sourceFiles,
            ref DateTime lastWriteUtc)
        {
            return ReadSourceFile(
                fullPath,
                entry.Path,
                entry.Sha256,
                entry.ByteLength,
                sourceFiles,
                ref lastWriteUtc);
        }

        private static string ReadSourceFile(
            string fullPath,
            string relativePath,
            string expectedSha256,
            long expectedByteLength,
            List<HistoricalSourceFileInfo> sourceFiles,
            ref DateTime lastWriteUtc)
        {
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Archive 분할 파일을 찾을 수 없습니다: {relativePath}", fullPath);

            byte[] bytes = File.ReadAllBytes(fullPath);
            string text;
            try
            {
                int preambleLength = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf
                    ? 3
                    : 0;
                text = StrictUtf8.GetString(bytes, preambleLength, bytes.Length - preambleLength);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException($"UTF-8 JSON으로 읽을 수 없습니다: {relativePath}", exception);
            }
            DateTime fileWriteUtc = File.GetLastWriteTimeUtc(fullPath);
            if (fileWriteUtc > lastWriteUtc)
                lastWriteUtc = fileWriteUtc;
            sourceFiles.Add(new HistoricalSourceFileInfo(
                relativePath,
                fullPath,
                expectedSha256,
                ComputeSha256(bytes),
                expectedByteLength,
                bytes.LongLength,
                fileWriteUtc));
            return text;
        }

        private static HistoricalPlayerPersonArray ParseTopLevelPersonArray(string json, string sourcePath)
        {
            string trimmed = json?.TrimStart();
            if (string.IsNullOrEmpty(trimmed) || (trimmed[0] != '[' && trimmed[0] != '{'))
                throw new InvalidDataException($"PlayerPerson JSON Root 형식이 올바르지 않습니다: {sourcePath}");
            return ParseJson<HistoricalPlayerPersonArray>(
                trimmed[0] == '[' ? "{\"items\":" + json + "}" : json,
                sourcePath);
        }

        private static T ParseJson<T>(string json, string sourcePath) where T : class
        {
            try
            {
                T result = JsonUtility.FromJson<T>(json);
                if (result == null)
                    throw new InvalidDataException("JSON Root를 읽을 수 없습니다.");
                return result;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException($"JSON을 파싱하지 못했습니다: {sourcePath}", exception);
            }
        }

        private static string ResolveArchivePath(string sourceFolder, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("manifest의 파일 경로가 비어 있습니다.");

            string root = NormalizeFolder(sourceFolder);
            string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath));
            string rootPrefix = root.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? root
                : root + System.IO.Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Archive 밖을 가리키는 파일 경로입니다: {relativePath}");
            return candidate;
        }

        private static string NormalizeFolder(string path)
        {
            string fullPath = System.IO.Path.GetFullPath(path.Trim());
            string root = System.IO.Path.GetPathRoot(fullPath);
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static Dictionary<string, T> IndexFirst<T>(IReadOnlyList<T> source, Func<T, string> keySelector)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                T item = source[index];
                string key = keySelector(item) ?? string.Empty;
                if (!result.ContainsKey(key))
                    result.Add(key, item);
            }
            return result;
        }

        private static Dictionary<string, List<T>> GroupByKey<T>(IReadOnlyList<T> source, Func<T, string> keySelector)
        {
            var result = new Dictionary<string, List<T>>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                T item = source[index];
                string key = keySelector(item) ?? string.Empty;
                if (!result.TryGetValue(key, out List<T> items))
                {
                    items = new List<T>();
                    result.Add(key, items);
                }
                items.Add(item);
            }
            return result;
        }

        private static int CompareCareerRows(HistoricalPlayerRow left, HistoricalPlayerRow right)
        {
            int yearComparison = left.OriginYear.CompareTo(right.OriginYear);
            return yearComparison != 0
                ? yearComparison
                : string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>Bake 산출물을 읽을 수 없을 때 원인을 보존한다.</summary>
    public sealed class WorldHistoryBakeFormatException : InvalidOperationException
    {
        public WorldHistoryBakeFormatException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// World History Bake 산출물의 이진 포맷이다.
    /// 44시즌 통계는 수만 행이라 JSON으로 두면 파싱만으로 초 단위가 나오므로,
    /// 문자열은 테이블로 한 번만 저장하고 정수는 zigzag varint로 적는다.
    /// 사람이 손으로 고칠 데이터가 아니라 빌드 산출물이므로 가독성보다 크기와 파싱 속도를 택했다.
    /// </summary>
    public static class WorldHistoryBakeCodec
    {
        /// <summary>포맷이 바뀌면 올린다. 값이 다르면 Bake를 무시하고 실제 시뮬레이션으로 되돌아간다.</summary>
        public const int FormatVersion = 1;

        private const uint Magic = 0x48575055u; // "UPWH"

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(BakedWorldHistoryPayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            WorldHistorySaveData history = payload.History;
            if (history.recordMode != (int)payload.Key.RecordMode ||
                history.worldHistorySeed != payload.Key.WorldHistorySeed)
            {
                throw new WorldHistoryBakeFormatException(
                    "Bake Key와 World History의 RecordMode/Seed가 다릅니다. " +
                    $"key={payload.Key.RecordMode}/{payload.Key.WorldHistorySeed}, " +
                    $"history={history.recordMode}/{history.worldHistorySeed}");
            }

            var strings = new StringTableWriter();
            using var buffer = new MemoryStream(1 << 20);
            using (var writer = new BinaryWriter(buffer, StrictUtf8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                WriteHeader(writer, payload.Key);

                // 문자열 테이블을 먼저 채워야 본문에서 인덱스만 쓸 수 있다.
                var body = new MemoryStream(1 << 20);
                using (var bodyWriter = new BinaryWriter(body, StrictUtf8, leaveOpen: true))
                    WriteBody(bodyWriter, history, strings);

                strings.WriteTo(writer);
                writer.Write(checked((int)body.Length));
                writer.Write(body.GetBuffer(), 0, (int)body.Length);
                body.Dispose();
            }
            return buffer.ToArray();
        }

        public static BakedWorldHistoryPayload Decode(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            try
            {
                using var buffer = new MemoryStream(bytes, writable: false);
                using var reader = new BinaryReader(buffer, StrictUtf8, leaveOpen: true);
                if (reader.ReadUInt32() != Magic)
                    throw new WorldHistoryBakeFormatException("World History Bake Magic이 다릅니다.");
                int formatVersion = reader.ReadInt32();
                if (formatVersion != FormatVersion)
                {
                    throw new WorldHistoryBakeFormatException(
                        $"지원하지 않는 World History Bake Format입니다. expected={FormatVersion}, actual={formatVersion}");
                }

                BakedWorldHistoryKey key = ReadHeader(reader);
                string[] strings = ReadStringTable(reader);
                int bodyLength = reader.ReadInt32();
                if (bodyLength < 0 || bodyLength > buffer.Length - buffer.Position)
                    throw new WorldHistoryBakeFormatException("World History Bake 본문 길이가 올바르지 않습니다.");
                WorldHistorySaveData history = ReadBody(reader, strings);
                history.recordMode = (int)key.RecordMode;
                history.worldHistorySeed = key.WorldHistorySeed;
                return new BakedWorldHistoryPayload(key, history);
            }
            catch (Exception exception) when (!(exception is WorldHistoryBakeFormatException))
            {
                throw new WorldHistoryBakeFormatException("World History Bake를 읽지 못했습니다.", exception);
            }
        }

        /// <summary>Key만 확인하면 되는 호출자가 본문 수만 행을 읽지 않도록 헤더만 훑는다.</summary>
        public static bool TryPeekKey(byte[] bytes, out BakedWorldHistoryKey key)
        {
            key = default;
            if (bytes == null || bytes.Length < 8)
                return false;
            try
            {
                using var buffer = new MemoryStream(bytes, writable: false);
                using var reader = new BinaryReader(buffer, StrictUtf8, leaveOpen: true);
                if (reader.ReadUInt32() != Magic || reader.ReadInt32() != FormatVersion)
                    return false;
                key = ReadHeader(reader);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is FormatException ||
                                              exception is ArgumentException)
            {
                return false;
            }
        }

        private static void WriteHeader(BinaryWriter writer, BakedWorldHistoryKey key)
        {
            writer.Write((int)key.RecordMode);
            writer.Write(key.WorldHistorySeed);
            writer.Write(key.ContentHash ?? string.Empty);
            writer.Write(key.BalanceVersion);
            writer.Write(key.BalanceContentHash ?? string.Empty);
        }

        private static BakedWorldHistoryKey ReadHeader(BinaryReader reader)
        {
            int recordMode = reader.ReadInt32();
            if (!Enum.IsDefined(typeof(WorldRecordMode), recordMode))
                throw new WorldHistoryBakeFormatException($"알 수 없는 WorldRecordMode입니다. actual={recordMode}");
            ulong seed = reader.ReadUInt64();
            string contentHash = reader.ReadString();
            int balanceVersion = reader.ReadInt32();
            string balanceContentHash = reader.ReadString();
            return new BakedWorldHistoryKey(
                (WorldRecordMode)recordMode,
                seed,
                contentHash,
                balanceVersion,
                balanceContentHash);
        }

        private static void WriteBody(BinaryWriter writer, WorldHistorySaveData history, StringTableWriter strings)
        {
            SeasonStatisticsSaveData[] statistics = history.statistics ?? Array.Empty<SeasonStatisticsSaveData>();
            WriteVarInt(writer, statistics.Length);
            for (int index = 0; index < statistics.Length; index++)
            {
                SeasonStatisticsSaveData row = statistics[index]
                    ?? throw new WorldHistoryBakeFormatException("null 통계 행은 구울 수 없습니다.");
                WriteVarInt(writer, strings.GetIndex(row.playerSeasonId));
                WriteVarInt(writer, strings.GetIndex(row.teamSeasonKey));
                WriteVarInt(writer, row.seasonYear);
                WriteVarInt(writer, row.position);
                WriteVarInt(writer, row.plateAppearances);
                WriteVarInt(writer, row.hits);
                WriteVarInt(writer, row.homeRuns);
                WriteVarInt(writer, row.walks);
                WriteVarInt(writer, row.strikeouts);
                WriteVarInt(writer, row.stolenBases);
                WriteVarInt(writer, row.pitchingOuts);
                WriteVarInt(writer, row.earnedRuns);
                WriteVarInt(writer, row.pitchingStrikeouts);
                WriteVarInt(writer, row.defensiveChances);
                WriteVarInt(writer, row.defensiveOutsAboveAverage);
                WriteVarInt(writer, row.fieldingErrors);
                writer.Write(PackFlags(row.isFirstHalf, row.isPostseason, row.isAllStarGame));
            }

            TeamSeasonStatisticsSaveData[] teamStatistics =
                history.teamStatistics ?? Array.Empty<TeamSeasonStatisticsSaveData>();
            WriteVarInt(writer, teamStatistics.Length);
            for (int index = 0; index < teamStatistics.Length; index++)
            {
                TeamSeasonStatisticsSaveData row = teamStatistics[index]
                    ?? throw new WorldHistoryBakeFormatException("null 팀 통계 행은 구울 수 없습니다.");
                WriteVarInt(writer, strings.GetIndex(row.teamSeasonKey));
                WriteVarInt(writer, row.seasonYear);
                WriteVarInt(writer, row.games);
                WriteVarInt(writer, row.wins);
                WriteVarInt(writer, row.losses);
                WriteVarInt(writer, row.ties);
                WriteVarInt(writer, row.runsScored);
                WriteVarInt(writer, row.runsAllowed);
                WriteVarInt(writer, row.atBats);
                WriteVarInt(writer, row.hits);
                WriteVarInt(writer, row.pitchingOuts);
                WriteVarInt(writer, row.earnedRuns);
                WriteVarInt(writer, row.hitsAllowed);
                WriteVarInt(writer, row.walksAllowed);
            }

            HistoricalStandingEntrySaveData[] standings =
                history.standings ?? Array.Empty<HistoricalStandingEntrySaveData>();
            WriteVarInt(writer, standings.Length);
            for (int index = 0; index < standings.Length; index++)
            {
                HistoricalStandingEntrySaveData row = standings[index]
                    ?? throw new WorldHistoryBakeFormatException("null 순위 행은 구울 수 없습니다.");
                WriteVarInt(writer, row.seasonYear);
                WriteVarInt(writer, row.rank);
                WriteVarInt(writer, strings.GetIndex(row.teamSeasonKey));
            }

            HistoricalPostseasonResultSaveData[] postseason =
                history.postseasonResults ?? Array.Empty<HistoricalPostseasonResultSaveData>();
            WriteVarInt(writer, postseason.Length);
            for (int index = 0; index < postseason.Length; index++)
            {
                HistoricalPostseasonResultSaveData row = postseason[index]
                    ?? throw new WorldHistoryBakeFormatException("null Postseason 행은 구울 수 없습니다.");
                WriteVarInt(writer, row.seasonYear);
                string[] qualifiers = row.qualifiedTeamSeasonKeys ?? Array.Empty<string>();
                WriteVarInt(writer, qualifiers.Length);
                for (int qualifierIndex = 0; qualifierIndex < qualifiers.Length; qualifierIndex++)
                    WriteVarInt(writer, strings.GetIndex(qualifiers[qualifierIndex]));
                WriteVarInt(writer, strings.GetIndex(row.championTeamSeasonKey));
            }

            WorldAwardEntrySaveData[] awards = history.awards ?? Array.Empty<WorldAwardEntrySaveData>();
            WriteVarInt(writer, awards.Length);
            for (int index = 0; index < awards.Length; index++)
            {
                WorldAwardEntrySaveData row = awards[index]
                    ?? throw new WorldHistoryBakeFormatException("null Award 행은 구울 수 없습니다.");
                WriteVarInt(writer, row.seasonYear);
                WriteVarInt(writer, row.awardType);
                WriteVarInt(writer, strings.GetIndex(row.playerSeasonId));
                WriteVarInt(writer, row.position);
            }
        }

        private static WorldHistorySaveData ReadBody(BinaryReader reader, string[] strings)
        {
            int statisticsCount = ReadCount(reader);
            var statistics = new SeasonStatisticsSaveData[statisticsCount];
            for (int index = 0; index < statisticsCount; index++)
            {
                var row = new SeasonStatisticsSaveData
                {
                    playerSeasonId = Resolve(strings, ReadVarInt(reader)),
                    teamSeasonKey = Resolve(strings, ReadVarInt(reader)),
                    seasonYear = ReadVarInt(reader),
                    position = ReadVarInt(reader),
                    plateAppearances = ReadVarInt(reader),
                    hits = ReadVarInt(reader),
                    homeRuns = ReadVarInt(reader),
                    walks = ReadVarInt(reader),
                    strikeouts = ReadVarInt(reader),
                    stolenBases = ReadVarInt(reader),
                    pitchingOuts = ReadVarInt(reader),
                    earnedRuns = ReadVarInt(reader),
                    pitchingStrikeouts = ReadVarInt(reader),
                    defensiveChances = ReadVarInt(reader),
                    defensiveOutsAboveAverage = ReadVarInt(reader),
                    fieldingErrors = ReadVarInt(reader)
                };
                byte flags = reader.ReadByte();
                row.isFirstHalf = (flags & 1) != 0;
                row.isPostseason = (flags & 2) != 0;
                row.isAllStarGame = (flags & 4) != 0;
                statistics[index] = row;
            }

            int teamStatisticsCount = ReadCount(reader);
            var teamStatistics = new TeamSeasonStatisticsSaveData[teamStatisticsCount];
            for (int index = 0; index < teamStatisticsCount; index++)
            {
                teamStatistics[index] = new TeamSeasonStatisticsSaveData
                {
                    teamSeasonKey = Resolve(strings, ReadVarInt(reader)),
                    seasonYear = ReadVarInt(reader),
                    games = ReadVarInt(reader),
                    wins = ReadVarInt(reader),
                    losses = ReadVarInt(reader),
                    ties = ReadVarInt(reader),
                    runsScored = ReadVarInt(reader),
                    runsAllowed = ReadVarInt(reader),
                    atBats = ReadVarInt(reader),
                    hits = ReadVarInt(reader),
                    pitchingOuts = ReadVarInt(reader),
                    earnedRuns = ReadVarInt(reader),
                    hitsAllowed = ReadVarInt(reader),
                    walksAllowed = ReadVarInt(reader)
                };
            }

            int standingsCount = ReadCount(reader);
            var standings = new HistoricalStandingEntrySaveData[standingsCount];
            for (int index = 0; index < standingsCount; index++)
            {
                standings[index] = new HistoricalStandingEntrySaveData
                {
                    seasonYear = ReadVarInt(reader),
                    rank = ReadVarInt(reader),
                    teamSeasonKey = Resolve(strings, ReadVarInt(reader))
                };
            }

            int postseasonCount = ReadCount(reader);
            var postseason = new HistoricalPostseasonResultSaveData[postseasonCount];
            for (int index = 0; index < postseasonCount; index++)
            {
                int seasonYear = ReadVarInt(reader);
                int qualifierCount = ReadCount(reader);
                var qualifiers = new string[qualifierCount];
                for (int qualifierIndex = 0; qualifierIndex < qualifierCount; qualifierIndex++)
                    qualifiers[qualifierIndex] = Resolve(strings, ReadVarInt(reader));
                postseason[index] = new HistoricalPostseasonResultSaveData
                {
                    seasonYear = seasonYear,
                    qualifiedTeamSeasonKeys = qualifiers,
                    championTeamSeasonKey = Resolve(strings, ReadVarInt(reader))
                };
            }

            int awardCount = ReadCount(reader);
            var awards = new WorldAwardEntrySaveData[awardCount];
            for (int index = 0; index < awardCount; index++)
            {
                awards[index] = new WorldAwardEntrySaveData
                {
                    seasonYear = ReadVarInt(reader),
                    awardType = ReadVarInt(reader),
                    playerSeasonId = Resolve(strings, ReadVarInt(reader)),
                    position = ReadVarInt(reader)
                };
            }

            return new WorldHistorySaveData
            {
                statistics = statistics,
                teamStatistics = teamStatistics,
                standings = standings,
                postseasonResults = postseason,
                awards = awards
            };
        }

        private static string[] ReadStringTable(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var result = new string[count];
            for (int index = 0; index < count; index++)
                result[index] = reader.ReadString();
            return result;
        }

        private static string Resolve(string[] strings, int index)
        {
            if (index < 0 || index >= strings.Length)
                throw new WorldHistoryBakeFormatException($"문자열 테이블 범위를 벗어난 인덱스입니다. index={index}");
            return strings[index];
        }

        private static int ReadCount(BinaryReader reader)
        {
            int value = ReadVarInt(reader);
            if (value < 0)
                throw new WorldHistoryBakeFormatException($"음수 개수는 허용되지 않습니다. actual={value}");
            return value;
        }

        private static byte PackFlags(bool isFirstHalf, bool isPostseason, bool isAllStarGame)
        {
            int flags = isFirstHalf ? 1 : 0;
            if (isPostseason)
                flags |= 2;
            if (isAllStarGame)
                flags |= 4;
            return (byte)flags;
        }

        /// <summary>음수 통계(수비 OAA 등)도 짧게 담기도록 zigzag로 접은 뒤 7비트씩 쓴다.</summary>
        private static void WriteVarInt(BinaryWriter writer, int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            while (zigzag >= 0x80u)
            {
                writer.Write((byte)(zigzag | 0x80u));
                zigzag >>= 7;
            }
            writer.Write((byte)zigzag);
        }

        private static int ReadVarInt(BinaryReader reader)
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte current = reader.ReadByte();
                result |= (uint)(current & 0x7f) << shift;
                if ((current & 0x80) == 0)
                    return (int)(result >> 1) ^ -(int)(result & 1);
            }
            throw new WorldHistoryBakeFormatException("varint가 5바이트를 넘었습니다.");
        }

        /// <summary>같은 입력이면 같은 바이트가 나오도록 첫 등장 순서로만 인덱스를 준다.</summary>
        private sealed class StringTableWriter
        {
            private readonly Dictionary<string, int> _indexByValue = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<string> _values = new List<string>();

            public int GetIndex(string value)
            {
                string normalized = value ?? string.Empty;
                if (_indexByValue.TryGetValue(normalized, out int index))
                    return index;
                index = _values.Count;
                _values.Add(normalized);
                _indexByValue.Add(normalized, index);
                return index;
            }

            public void WriteTo(BinaryWriter writer)
            {
                WriteVarInt(writer, _values.Count);
                for (int index = 0; index < _values.Count; index++)
                    writer.Write(_values[index]);
            }
        }
    }
}

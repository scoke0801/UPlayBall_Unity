using System;
using System.Collections.Generic;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>원본 JSON 파일 하나의 manifest 기대값과 Load 시점 실측값을 함께 보관한다.</summary>
    public sealed class HistoricalSourceFileInfo
    {
        public HistoricalSourceFileInfo(
            string relativePath,
            string fullPath,
            string expectedSha256,
            string actualSha256,
            long expectedByteLength,
            long actualByteLength,
            DateTime lastWriteUtc)
        {
            RelativePath = relativePath ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            ExpectedSha256 = expectedSha256 ?? string.Empty;
            ActualSha256 = actualSha256 ?? string.Empty;
            ExpectedByteLength = expectedByteLength;
            ActualByteLength = actualByteLength;
            LastWriteUtc = lastWriteUtc;
        }

        public string RelativePath { get; }
        public string FullPath { get; }
        public string ExpectedSha256 { get; }
        public string ActualSha256 { get; }
        public long ExpectedByteLength { get; }
        public long ActualByteLength { get; }
        public DateTime LastWriteUtc { get; }
        public bool IsHashMatch => string.Equals(ExpectedSha256, ActualSha256, StringComparison.OrdinalIgnoreCase);
        public bool IsByteLengthMatch => ExpectedByteLength == ActualByteLength;
    }

    /// <summary>Person·Season·Original Record·Award를 PlayerSeason 기준으로 연결한 Browser 행이다.</summary>
    public sealed class HistoricalPlayerRow
    {
        /// <summary>Archive 배열 능력치의 고정 순서를 사람이 읽을 수 있는 이름으로 제공한다.</summary>
        private static readonly string[] AbilityNameValues =
        {
            "Contact",
            "Power",
            "Speed",
            "Arm",
            "Defense",
            "BatterMental",
            "Stamina",
            "Velocity",
            "Stuff",
            "Breaking",
            "Control",
            "PitcherMental"
        };

        public static IReadOnlyList<string> AbilityNames => AbilityNameValues;

        public HistoricalPlayerRow(
            HistoricalPlayerPerson person,
            HistoricalPlayerSeason season,
            HistoricalSeasonRecord record,
            IReadOnlyList<HistoricalAwardRecord> awards)
        {
            Person = person;
            Season = season ?? throw new ArgumentNullException(nameof(season));
            Record = record;
            Awards = awards ?? Array.Empty<HistoricalAwardRecord>();
        }

        public HistoricalPlayerPerson Person { get; }
        public HistoricalPlayerSeason Season { get; }
        public HistoricalSeasonRecord Record { get; }
        public IReadOnlyList<HistoricalAwardRecord> Awards { get; }
        public string Name => Person?.DisplayName ?? string.Empty;
        public string OriginalName => Person?.OriginalName ?? string.Empty;
        public string RuntimeName => Person?.FictionalName ?? string.Empty;
        public IReadOnlyList<string> SourceReferenceNames => Season.SourceReferenceNames;
        public string PlayerPersonId => Season.PlayerPersonId;
        public string PlayerSeasonId => Season.PlayerSeasonId;
        public int OriginYear => Season.OriginYear;
        public string OriginFranchiseId => Season.OriginFranchiseId;
        public string OriginTeamSeasonKey => Season.OriginTeamSeasonKey;
        public string Position => Season.Position;
        public string PitcherRole => Season.PitcherRole;
        public string PlayerType => Season.PlayerType;
        public string RegistrationType => Season.RegistrationType;
        public string RosterRole => Season.RosterRole;
        public int Cost => Season.Cost;
        public int[] BaseAttributes => Season.BaseAttributes;
        public int[] TrainingCeiling => Season.TrainingCeiling;
        public int[] PotentialTrait => Person?.PotentialTrait ?? Array.Empty<int>();
        public int? Age => Person == null || Person.BirthYear <= 0 ? (int?)null : OriginYear - Person.BirthYear;
        public int AwardCount => Awards.Count;

        public bool IsOriginalSource => Record?.IsOriginalSourceRecord == true;

        public double? BattingAverage =>
            IsHitter && Record != null && Record.AtBats > 0
                ? Record.HasStoredBattingAverage
                    ? Record.StoredBattingAverage
                    : (double)Record.Hits / Record.AtBats
                : null;

        public double? OnBasePercentage =>
            IsHitter && Record != null && Record.HasStoredOnBasePercentage
                ? Record.StoredOnBasePercentage
                : null;

        public double? SluggingPercentage =>
            IsHitter && Record != null && Record.AtBats > 0
                ? Record.HasStoredSluggingPercentage
                    ? Record.StoredSluggingPercentage
                    : (Record.Hits + Record.Doubles + 2d * Record.Triples + 3d * Record.HomeRuns) / Record.AtBats
                : null;

        public double? OnBasePlusSlugging =>
            IsHitter && Record != null && Record.HasStoredOnBasePlusSlugging
                ? Record.StoredOnBasePlusSlugging
                : OnBasePercentage.HasValue && SluggingPercentage.HasValue
                    ? OnBasePercentage.Value + SluggingPercentage.Value
                    : null;

        // H/PA는 원본 저장 지표가 아니므로 타석과 안타에서 단순 산술 파생한다.
        public double? HitsPerPlateAppearance =>
            IsHitter && Record != null && Record.PlateAppearances > 0
                ? (double)Record.Hits / Record.PlateAppearances
                : null;

        public double? EarnedRunAverage =>
            IsPitcher && Record != null && Record.PitchingOuts > 0
                ? Record.HasStoredEarnedRunAverage
                    ? Record.StoredEarnedRunAverage
                    : Record.EarnedRuns * 27d / Record.PitchingOuts
                : null;

        public double? WalksAndHitsPerInningPitched =>
            IsPitcher && Record != null && Record.PitchingOuts > 0
                ? Record.HasStoredWhip
                    ? Record.StoredWhip
                    : (Record.HitsAllowed + Record.PitchingWalks) * 3d / Record.PitchingOuts
                : null;

        public double? StrikeoutsPerNine =>
            IsPitcher && Record != null && Record.PitchingOuts > 0
                ? Record.PitchingStrikeouts * 27d / Record.PitchingOuts
                : null;

        public bool IsHitter => string.Equals(PlayerType, "Hitter", StringComparison.Ordinal);
        public bool IsPitcher => string.Equals(PlayerType, "Pitcher", StringComparison.Ordinal);

        /// <summary>원본 배열에서 지정한 능력치의 Base 값을 반환하며 누락 시 0을 반환한다.</summary>
        public int GetBaseAbility(int abilityIndex) => GetAbility(BaseAttributes, abilityIndex);

        /// <summary>원본 배열에서 지정한 능력치의 Training Ceiling을 반환하며 누락 시 0을 반환한다.</summary>
        public int GetTrainingCeiling(int abilityIndex) => GetAbility(TrainingCeiling, abilityIndex);

        /// <summary>원본 배열에서 지정한 능력치의 Person 성장 성향을 반환하며 누락 시 0을 반환한다.</summary>
        public int GetPotentialTrait(int abilityIndex) => GetAbility(PotentialTrait, abilityIndex);

        /// <summary>이 PlayerSeason이 지정한 원본 AwardType을 받았는지 확인한다.</summary>
        public bool HasAward(string awardType)
        {
            if (string.IsNullOrWhiteSpace(awardType))
                return Awards.Count > 0;

            for (int index = 0; index < Awards.Count; index++)
            {
                if (string.Equals(Awards[index].AwardType, awardType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int GetAbility(IReadOnlyList<int> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] : 0;
        }
    }

    /// <summary>한 번 Load한 Archive의 Entity와 검색용 Index를 Editor Memory에 보관한다.</summary>
    public sealed class HistoricalArchiveData
    {
        internal HistoricalArchiveData(
            HistoricalArchiveManifest manifest,
            string sourceFolder,
            TimeSpan loadElapsed,
            DateTime lastWriteUtc,
            HistoricalPlayerPerson[] persons,
            HistoricalPlayerRow[] playerRows,
            HistoricalTeamSeason[] teams,
            HistoricalAwardRecord[] awards,
            HistoricalCard[] cards,
            HistoricalSeasonRecord[] records,
            HistoricalSourceFileInfo[] sourceFiles,
            Dictionary<string, HistoricalPlayerPerson> personsById,
            Dictionary<string, HistoricalPlayerRow> playersBySeasonId,
            Dictionary<string, HistoricalTeamSeason> teamsByKey,
            Dictionary<string, HistoricalCard> cardsById,
            Dictionary<string, HistoricalSeasonRecord> recordsBySeasonId,
            Dictionary<string, HistoricalPlayerRow[]> playersByPersonId,
            Dictionary<string, HistoricalAwardRecord[]> awardsBySeasonId,
            Dictionary<int, string> yearSourcePaths)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            SourceFolder = sourceFolder ?? string.Empty;
            LoadElapsed = loadElapsed;
            LastWriteUtc = lastWriteUtc;
            Persons = persons ?? Array.Empty<HistoricalPlayerPerson>();
            PlayerRows = playerRows ?? Array.Empty<HistoricalPlayerRow>();
            Teams = teams ?? Array.Empty<HistoricalTeamSeason>();
            Awards = awards ?? Array.Empty<HistoricalAwardRecord>();
            Cards = cards ?? Array.Empty<HistoricalCard>();
            Records = records ?? Array.Empty<HistoricalSeasonRecord>();
            SourceFiles = sourceFiles ?? Array.Empty<HistoricalSourceFileInfo>();
            PersonsById = personsById;
            PlayersBySeasonId = playersBySeasonId;
            TeamsByKey = teamsByKey;
            CardsById = cardsById;
            RecordsBySeasonId = recordsBySeasonId;
            PlayersByPersonId = playersByPersonId;
            AwardsBySeasonId = awardsBySeasonId;
            YearSourcePaths = yearSourcePaths;
        }

        public HistoricalArchiveManifest Manifest { get; }
        public string SourceFolder { get; }
        public TimeSpan LoadElapsed { get; }
        public DateTime LastWriteUtc { get; }
        public IReadOnlyList<HistoricalPlayerPerson> Persons { get; }
        public IReadOnlyList<HistoricalPlayerRow> PlayerRows { get; }
        public IReadOnlyList<HistoricalTeamSeason> Teams { get; }
        public IReadOnlyList<HistoricalAwardRecord> Awards { get; }
        public IReadOnlyList<HistoricalCard> Cards { get; }
        public IReadOnlyList<HistoricalSeasonRecord> Records { get; }
        public IReadOnlyList<HistoricalSourceFileInfo> SourceFiles { get; }
        public IReadOnlyDictionary<string, HistoricalPlayerPerson> PersonsById { get; }
        public IReadOnlyDictionary<string, HistoricalPlayerRow> PlayersBySeasonId { get; }
        public IReadOnlyDictionary<string, HistoricalTeamSeason> TeamsByKey { get; }
        public IReadOnlyDictionary<string, HistoricalCard> CardsById { get; }
        public IReadOnlyDictionary<string, HistoricalSeasonRecord> RecordsBySeasonId { get; }
        public IReadOnlyDictionary<string, HistoricalPlayerRow[]> PlayersByPersonId { get; }
        public IReadOnlyDictionary<string, HistoricalAwardRecord[]> AwardsBySeasonId { get; }
        public IReadOnlyDictionary<int, string> YearSourcePaths { get; }
    }
}

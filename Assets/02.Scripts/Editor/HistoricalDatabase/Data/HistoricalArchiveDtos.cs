using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Historical Archive의 분할 파일과 버전 정보를 보관하는 manifest DTO다.</summary>
    [Serializable]
    public sealed class HistoricalArchiveManifest
    {
        [SerializeField] private int assetFormatVersion;
        [SerializeField] private int contentSchemaVersion;
        [SerializeField] private string assetArchiveHash;
        [SerializeField] private HistoricalArchiveFileEntry playerPersons;
        [SerializeField] private HistoricalSourceManifest sourceManifest;
        [SerializeField] private HistoricalArchiveSummary summary;
        [SerializeField] private HistoricalArchiveYearEntry[] years;

        public int AssetFormatVersion => assetFormatVersion;
        public int ContentSchemaVersion => contentSchemaVersion;
        public string AssetArchiveHash => assetArchiveHash ?? string.Empty;
        public HistoricalArchiveFileEntry PlayerPersons => playerPersons;
        public HistoricalSourceManifest SourceManifest => sourceManifest;
        public HistoricalArchiveSummary Summary => summary;
        public IReadOnlyList<HistoricalArchiveYearEntry> Years => years ?? Array.Empty<HistoricalArchiveYearEntry>();
    }

    /// <summary>manifest가 가리키는 공통 파일 하나의 기대 크기와 Hash를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalArchiveFileEntry
    {
        [SerializeField] private string path;
        [SerializeField] private string sha256;
        [SerializeField] private long byteLength;
        [SerializeField] private int count;

        public string Path => path ?? string.Empty;
        public string Sha256 => sha256 ?? string.Empty;
        public long ByteLength => byteLength;
        public int Count => count;
    }

    /// <summary>manifest가 가리키는 한 연도 파일의 기대 건수와 Hash를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalArchiveYearEntry
    {
        [SerializeField] private int year;
        [SerializeField] private string path;
        [SerializeField] private string sha256;
        [SerializeField] private long byteLength;
        [SerializeField] private int playerSeasonCount;
        [SerializeField] private int teamSeasonCount;
        [SerializeField] private int normalCardCount;
        [SerializeField] private int originalRecordCount;
        [SerializeField] private int allStarCount;
        [SerializeField] private int goldenGloveCount;
        [SerializeField] private int sourceHitterCount;
        [SerializeField] private int sourcePitcherCount;
        [SerializeField] private int replacementHitterCount;
        [SerializeField] private int replacementPitcherCount;
        [SerializeField] private double replacementRatio;

        public int Year => year;
        public string Path => path ?? string.Empty;
        public string Sha256 => sha256 ?? string.Empty;
        public long ByteLength => byteLength;
        public int PlayerSeasonCount => playerSeasonCount;
        public int TeamSeasonCount => teamSeasonCount;
        public int NormalCardCount => normalCardCount;
        public int OriginalRecordCount => originalRecordCount;
        public int AllStarCount => allStarCount;
        public int GoldenGloveCount => goldenGloveCount;
        public int SourceHitterCount => sourceHitterCount;
        public int SourcePitcherCount => sourcePitcherCount;
        public int ReplacementHitterCount => replacementHitterCount;
        public int ReplacementPitcherCount => replacementPitcherCount;
        public double ReplacementRatio => replacementRatio;
    }

    /// <summary>Archive 생성에 사용한 입력·Generator·Balance 버전을 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalSourceManifest
    {
        [SerializeField] private string referenceDataVersion;
        [SerializeField] private string rawDataVersion;
        [SerializeField] private int normalizedSchemaVersion;
        [SerializeField] private string normalizedImporterVersion;
        [SerializeField] private string normalizedContentHash;
        [SerializeField] private string abilityFormulaVersion;
        [SerializeField] private string positionRoleClassifierVersion;
        [SerializeField] private string rosterBuilderVersion;
        [SerializeField] private string costFormulaVersion;
        [SerializeField] private string derivationBalanceVersion;
        [SerializeField] private string generatorVersion;
        [SerializeField] private string balanceVersion;
        [SerializeField] private long generationSeed;
        [SerializeField] private bool generationSeedAffectsCanonicalBake;
        [SerializeField] private string namePolicyVersion;
        [SerializeField] private string nameDataPolicy;
        [SerializeField] private string sourceIdentityPolicyVersion;
        [SerializeField] private string sourceAllocationPolicyVersion;
        [SerializeField] private string sourceFranchiseIdentityPolicyVersion;
        [SerializeField] private string sourceTeamSeasonIdentityPolicyVersion;
        [SerializeField] private string replacementGeneratorVersion;
        [SerializeField] private string replacementPopulationPolicyVersion;
        [SerializeField] private int sourceBackedPlayerPersonCount;
        [SerializeField] private int sourceBackedPlayerSeasonCount;
        [SerializeField] private int replacementGeneratedPlayerPersonCount;
        [SerializeField] private int replacementGeneratedPlayerSeasonCount;
        [SerializeField] private string contentHash;

        public string ReferenceDataVersion => referenceDataVersion ?? string.Empty;
        public string RawDataVersion => rawDataVersion ?? string.Empty;
        public int NormalizedSchemaVersion => normalizedSchemaVersion;
        public string NormalizedImporterVersion => normalizedImporterVersion ?? string.Empty;
        public string NormalizedContentHash => normalizedContentHash ?? string.Empty;
        public string AbilityFormulaVersion => abilityFormulaVersion ?? string.Empty;
        public string PositionRoleClassifierVersion => positionRoleClassifierVersion ?? string.Empty;
        public string RosterBuilderVersion => rosterBuilderVersion ?? string.Empty;
        public string CostFormulaVersion => costFormulaVersion ?? string.Empty;
        public string DerivationBalanceVersion => derivationBalanceVersion ?? string.Empty;
        public string GeneratorVersion => generatorVersion ?? string.Empty;
        public string BalanceVersion => balanceVersion ?? string.Empty;
        public long GenerationSeed => generationSeed;
        public bool GenerationSeedAffectsCanonicalBake => generationSeedAffectsCanonicalBake;
        public string NamePolicyVersion => namePolicyVersion ?? string.Empty;
        public string NameDataPolicy => nameDataPolicy ?? string.Empty;
        public string SourceIdentityPolicyVersion => sourceIdentityPolicyVersion ?? string.Empty;
        public string SourceAllocationPolicyVersion => sourceAllocationPolicyVersion ?? string.Empty;
        public string SourceFranchiseIdentityPolicyVersion => sourceFranchiseIdentityPolicyVersion ?? string.Empty;
        public string SourceTeamSeasonIdentityPolicyVersion => sourceTeamSeasonIdentityPolicyVersion ?? string.Empty;
        public string ReplacementGeneratorVersion => replacementGeneratorVersion ?? string.Empty;
        public string ReplacementPopulationPolicyVersion => replacementPopulationPolicyVersion ?? string.Empty;
        public int SourceBackedPlayerPersonCount => sourceBackedPlayerPersonCount;
        public int SourceBackedPlayerSeasonCount => sourceBackedPlayerSeasonCount;
        public int ReplacementGeneratedPlayerPersonCount => replacementGeneratedPlayerPersonCount;
        public int ReplacementGeneratedPlayerSeasonCount => replacementGeneratedPlayerSeasonCount;
        public string ContentHash => contentHash ?? string.Empty;
    }

    /// <summary>manifest에 기록된 Archive 전체 Entity 건수다.</summary>
    [Serializable]
    public sealed class HistoricalArchiveSummary
    {
        [SerializeField] private int yearCount;
        [SerializeField] private int playerPersonCount;
        [SerializeField] private int playerSeasonCount;
        [SerializeField] private int teamSeasonCount;
        [SerializeField] private int normalCardCount;
        [SerializeField] private int originalRecordCount;
        [SerializeField] private int originalAwardCount;
        [SerializeField] private int sourceBackedPlayerPersonCount;
        [SerializeField] private int sourceBackedPlayerSeasonCount;
        [SerializeField] private int replacementGeneratedPlayerPersonCount;
        [SerializeField] private int replacementGeneratedPlayerSeasonCount;

        public int YearCount => yearCount;
        public int PlayerPersonCount => playerPersonCount;
        public int PlayerSeasonCount => playerSeasonCount;
        public int TeamSeasonCount => teamSeasonCount;
        public int NormalCardCount => normalCardCount;
        public int OriginalRecordCount => originalRecordCount;
        public int OriginalAwardCount => originalAwardCount;
        public int SourceBackedPlayerPersonCount => sourceBackedPlayerPersonCount;
        public int SourceBackedPlayerSeasonCount => sourceBackedPlayerSeasonCount;
        public int ReplacementGeneratedPlayerPersonCount => replacementGeneratedPlayerPersonCount;
        public int ReplacementGeneratedPlayerSeasonCount => replacementGeneratedPlayerSeasonCount;
    }

    /// <summary>다년도 커리어에서 동일 인물을 연결하는 PlayerPerson 원본 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalPlayerPerson
    {
        [SerializeField] private string playerPersonId;
        [SerializeField] private string originalName;
        [SerializeField] private int birthYear;
        [SerializeField] private string bats;
        [SerializeField] private string throws;
        [SerializeField] private string primaryPosition;
        [SerializeField] private string registrationType;
        [SerializeField] private int careerStartYear;
        [SerializeField] private int careerEndYear;
        [SerializeField] private int[] personPotentialTrait;
        [NonSerialized] private string _sourcePath;

        public string PlayerPersonId => playerPersonId ?? string.Empty;
        public string OriginalName => originalName ?? string.Empty;
        public string DisplayName => OriginalName;
        public int BirthYear => birthYear;
        public string Bats => bats ?? string.Empty;
        public string Throws => throws ?? string.Empty;
        public string PrimaryPosition => primaryPosition ?? string.Empty;
        public string RegistrationType => registrationType ?? string.Empty;
        public int CareerStartYear => careerStartYear;
        public int CareerEndYear => careerEndYear;
        public int[] PotentialTrait => personPotentialTrait ?? Array.Empty<int>();
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>한 선수의 한 시즌 Origin·역할·능력치를 보관하는 원본 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalPlayerSeason
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string playerPersonId;
        [SerializeField] private int originYear;
        [SerializeField] private string originFranchiseId;
        [SerializeField] private string originTeamSeasonKey;
        [SerializeField] private string position;
        [SerializeField] private string pitcherRole;
        [SerializeField] private string pitcherRoleConfidence;
        [SerializeField] private string playerType;
        [SerializeField] private string dataProvenance;
        [SerializeField] private string registrationType;
        [SerializeField] private int[] baseAttributes;
        [SerializeField] private int cost;
        [SerializeField] private int[] trainingCeiling;
        [SerializeField] private string rosterRole;
        [SerializeField] private double referenceSimilarityDistance;
        [SerializeField] private string[] sourceReferenceNames;
        [SerializeField] private HistoricalAbilityDerivationTrace[] abilityDerivationTrace;
        [SerializeField] private HistoricalCostDerivationTrace costDerivationTrace;
        [SerializeField] private HistoricalPositionRoleDerivationTrace positionRoleDerivationTrace;
        [NonSerialized] private string _sourcePath;

        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string PlayerPersonId => playerPersonId ?? string.Empty;
        public int OriginYear => originYear;
        public string OriginFranchiseId => originFranchiseId ?? string.Empty;
        public string OriginTeamSeasonKey => originTeamSeasonKey ?? string.Empty;
        public string Position => position ?? string.Empty;
        public string PitcherRole => pitcherRole ?? string.Empty;
        public string PitcherRoleConfidence => pitcherRoleConfidence ?? string.Empty;
        public string PlayerType => playerType ?? string.Empty;
        public string DataProvenance => dataProvenance ?? string.Empty;
        public string RegistrationType => registrationType ?? string.Empty;
        public int[] BaseAttributes => baseAttributes ?? Array.Empty<int>();
        public int Cost => cost;
        public int[] TrainingCeiling => trainingCeiling ?? Array.Empty<int>();
        public string RosterRole => rosterRole ?? string.Empty;
        public double ReferenceSimilarityDistance => referenceSimilarityDistance;
        public string[] SourceReferenceNames => sourceReferenceNames ?? Array.Empty<string>();
        public IReadOnlyList<HistoricalAbilityDerivationTrace> AbilityDerivationTrace =>
            abilityDerivationTrace ?? Array.Empty<HistoricalAbilityDerivationTrace>();
        public HistoricalCostDerivationTrace CostDerivationTrace => costDerivationTrace;
        public HistoricalPositionRoleDerivationTrace PositionRoleDerivationTrace => positionRoleDerivationTrace;
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>Normal Card의 안정 ID와 Edition 보정을 보관하는 원본 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalCard
    {
        [SerializeField] private string cardId;
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string edition;
        [SerializeField] private int[] editionStatModifiers;
        [NonSerialized] private string _sourcePath;

        public string CardId => cardId ?? string.Empty;
        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string Edition => edition ?? string.Empty;
        public int[] EditionStatModifiers => editionStatModifiers ?? Array.Empty<int>();
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>한 Franchise 연도의 전체 Card Pool과 고정 Core25를 보관하는 원본 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalTeamSeason
    {
        [SerializeField] private string teamSeasonKey;
        [SerializeField] private string franchiseId;
        [SerializeField] private int originYear;
        [SerializeField] private string[] allNormalCardIds;
        [SerializeField] private string[] core25CardIds;
        [SerializeField] private double referenceStrength;
        [SerializeField] private HistoricalRosterSelectionTrace rosterSelectionTrace;
        [SerializeField] private HistoricalDerivationWarningTrace[] validationWarnings;
        [NonSerialized] private string _sourcePath;

        public string TeamSeasonKey => teamSeasonKey ?? string.Empty;
        public string FranchiseId => franchiseId ?? string.Empty;
        public int OriginYear => originYear;
        public string[] AllNormalCardIds => allNormalCardIds ?? Array.Empty<string>();
        public string[] Core25CardIds => core25CardIds ?? Array.Empty<string>();
        public double ReferenceStrength => referenceStrength;
        public HistoricalRosterSelectionTrace RosterSelectionTrace => rosterSelectionTrace;
        public IReadOnlyList<HistoricalDerivationWarningTrace> ValidationWarnings =>
            validationWarnings ?? Array.Empty<HistoricalDerivationWarningTrace>();
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>Editor 검수와 Legacy OriginalHistory 회귀 비교용 원기록 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalSeasonRecord
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string teamSeasonKey;
        [SerializeField] private int seasonYear;
        [SerializeField] private string position;
        [SerializeField] private int games;
        [SerializeField] private int plateAppearances;
        [SerializeField] private int atBats;
        [SerializeField] private int hits;
        [SerializeField] private int doubles;
        [SerializeField] private int triples;
        [SerializeField] private int homeRuns;
        [SerializeField] private int runsBattedIn;
        [SerializeField] private int runs;
        [SerializeField] private int walks;
        [SerializeField] private int strikeouts;
        [SerializeField] private int stolenBases;
        [SerializeField] private int caughtStealing;
        [SerializeField] private bool hasStoredBattingAverage;
        [SerializeField] private double storedBattingAverage;
        [SerializeField] private bool hasStoredOnBasePercentage;
        [SerializeField] private double storedOnBasePercentage;
        [SerializeField] private bool hasStoredSluggingPercentage;
        [SerializeField] private double storedSluggingPercentage;
        [SerializeField] private bool hasStoredOnBasePlusSlugging;
        [SerializeField] private double storedOnBasePlusSlugging;
        [SerializeField] private int defensiveChances;
        [SerializeField] private int fieldingErrors;
        [SerializeField] private int gamesStarted;
        [SerializeField] private int pitchingOuts;
        [SerializeField] private int wins;
        [SerializeField] private int losses;
        [SerializeField] private int saves;
        [SerializeField] private int holds;
        [SerializeField] private int hitsAllowed;
        [SerializeField] private int homeRunsAllowed;
        [SerializeField] private int pitchingWalks;
        [SerializeField] private int earnedRuns;
        [SerializeField] private int pitchingStrikeouts;
        [SerializeField] private bool hasStoredEarnedRunAverage;
        [SerializeField] private double storedEarnedRunAverage;
        [SerializeField] private bool hasStoredWhip;
        [SerializeField] private double storedWhip;
        [SerializeField] private bool isOriginalSourceRecord;
        [NonSerialized] private string _sourcePath;

        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string TeamSeasonKey => teamSeasonKey ?? string.Empty;
        public int SeasonYear => seasonYear;
        public string Position => position ?? string.Empty;
        public int Games => games;
        public int PlateAppearances => plateAppearances;
        public int AtBats => atBats;
        public int Hits => hits;
        public int Doubles => doubles;
        public int Triples => triples;
        public int HomeRuns => homeRuns;
        public int RunsBattedIn => runsBattedIn;
        public int Runs => runs;
        public int Walks => walks;
        public int Strikeouts => strikeouts;
        public int StolenBases => stolenBases;
        public int CaughtStealing => caughtStealing;
        public bool HasStoredBattingAverage => hasStoredBattingAverage;
        public double StoredBattingAverage => storedBattingAverage;
        public bool HasStoredOnBasePercentage => hasStoredOnBasePercentage;
        public double StoredOnBasePercentage => storedOnBasePercentage;
        public bool HasStoredSluggingPercentage => hasStoredSluggingPercentage;
        public double StoredSluggingPercentage => storedSluggingPercentage;
        public bool HasStoredOnBasePlusSlugging => hasStoredOnBasePlusSlugging;
        public double StoredOnBasePlusSlugging => storedOnBasePlusSlugging;
        public int DefensiveChances => defensiveChances;
        public int FieldingErrors => fieldingErrors;
        public int GamesStarted => gamesStarted;
        public int PitchingOuts => pitchingOuts;
        public int Wins => wins;
        public int Losses => losses;
        public int Saves => saves;
        public int Holds => holds;
        public int HitsAllowed => hitsAllowed;
        public int HomeRunsAllowed => homeRunsAllowed;
        public int PitchingWalks => pitchingWalks;
        public int EarnedRuns => earnedRuns;
        public int PitchingStrikeouts => pitchingStrikeouts;
        public bool HasStoredEarnedRunAverage => hasStoredEarnedRunAverage;
        public double StoredEarnedRunAverage => storedEarnedRunAverage;
        public bool HasStoredWhip => hasStoredWhip;
        public double StoredWhip => storedWhip;
        public bool IsOriginalSourceRecord => isOriginalSourceRecord;
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>Editor 검수와 Legacy OriginalHistory 비교용 Source 수상 DTO다.</summary>
    [Serializable]
    public sealed class HistoricalAwardRecord
    {
        [SerializeField] private int seasonYear;
        [SerializeField] private string awardType;
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string position;
        [SerializeField] private string source;
        [NonSerialized] private string _sourcePath;

        public int SeasonYear => seasonYear;
        public string AwardType => awardType ?? string.Empty;
        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string Position => position ?? string.Empty;
        public string Source => source ?? string.Empty;
        public string SourcePath => _sourcePath ?? string.Empty;

        internal void SetSourcePath(string sourcePath) => _sourcePath = sourcePath;
    }

    /// <summary>Years/{year}.json 한 파일의 여섯 Root 값을 Deserialize하는 DTO다.</summary>
    [Serializable]
    internal sealed class HistoricalYearContent
    {
        [SerializeField] private int year;
        [SerializeField] private HistoricalPlayerSeason[] playerSeasons;
        [SerializeField] private HistoricalCard[] normalCards;
        [SerializeField] private HistoricalTeamSeason[] teamSeasons;
        [SerializeField] private HistoricalSeasonRecord[] originalSeasonRecords;
        [SerializeField] private HistoricalAwardRecord[] originalAwardRecords;

        public int Year => year;
        public HistoricalPlayerSeason[] PlayerSeasons => playerSeasons ?? Array.Empty<HistoricalPlayerSeason>();
        public HistoricalCard[] NormalCards => normalCards ?? Array.Empty<HistoricalCard>();
        public HistoricalTeamSeason[] TeamSeasons => teamSeasons ?? Array.Empty<HistoricalTeamSeason>();
        public HistoricalSeasonRecord[] OriginalSeasonRecords => originalSeasonRecords ?? Array.Empty<HistoricalSeasonRecord>();
        public HistoricalAwardRecord[] OriginalAwardRecords => originalAwardRecords ?? Array.Empty<HistoricalAwardRecord>();
    }

    /// <summary>JsonUtility가 top-level PlayerPerson 배열을 읽도록 감싸는 전용 DTO다.</summary>
    [Serializable]
    internal sealed class HistoricalPlayerPersonArray
    {
        [SerializeField] private HistoricalPlayerPerson[] items;

        public HistoricalPlayerPerson[] Items => items ?? Array.Empty<HistoricalPlayerPerson>();
    }
}

using System;
using UnityEngine;

// 아래 private 필드는 JsonUtility가 Reflection으로 채우므로 C# 할당 분석 대상이 아니다.
#pragma warning disable 0649

namespace Baseball.Game.Historical
{
    [Serializable]
    internal sealed class HistoricalRuntimeManifestDto
    {
        [SerializeField] private int assetFormatVersion;
        [SerializeField] private int contentSchemaVersion;
        [SerializeField] private string assetArchiveHash;
        [SerializeField] private HistoricalRuntimeFileEntryDto playerPersons;
        [SerializeField] private HistoricalRuntimeSourceManifestDto sourceManifest;
        [SerializeField] private HistoricalRuntimeSummaryDto summary;
        [SerializeField] private HistoricalRuntimeYearEntryDto[] years;

        public int AssetFormatVersion => assetFormatVersion;
        public int ContentSchemaVersion => contentSchemaVersion;
        public string AssetArchiveHash => assetArchiveHash ?? string.Empty;
        public HistoricalRuntimeFileEntryDto PlayerPersons => playerPersons;
        public HistoricalRuntimeSourceManifestDto SourceManifest => sourceManifest;
        public HistoricalRuntimeSummaryDto Summary => summary;
        public HistoricalRuntimeYearEntryDto[] Years => years ?? Array.Empty<HistoricalRuntimeYearEntryDto>();
    }

    [Serializable]
    internal sealed class HistoricalRuntimeFileEntryDto
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

    [Serializable]
    internal sealed class HistoricalRuntimeYearEntryDto
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

    [Serializable]
    internal sealed class HistoricalRuntimeSourceManifestDto
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
        [SerializeField] private string sourceFranchiseIdentityPolicyVersion;
        [SerializeField] private string sourceTeamSeasonIdentityPolicyVersion;
        [SerializeField] private string sourceAllocationPolicyVersion;
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
        public string SourceFranchiseIdentityPolicyVersion => sourceFranchiseIdentityPolicyVersion ?? string.Empty;
        public string SourceTeamSeasonIdentityPolicyVersion => sourceTeamSeasonIdentityPolicyVersion ?? string.Empty;
        public string SourceAllocationPolicyVersion => sourceAllocationPolicyVersion ?? string.Empty;
        public string ReplacementGeneratorVersion => replacementGeneratorVersion ?? string.Empty;
        public string ReplacementPopulationPolicyVersion => replacementPopulationPolicyVersion ?? string.Empty;
        public int SourceBackedPlayerPersonCount => sourceBackedPlayerPersonCount;
        public int SourceBackedPlayerSeasonCount => sourceBackedPlayerSeasonCount;
        public int ReplacementGeneratedPlayerPersonCount => replacementGeneratedPlayerPersonCount;
        public int ReplacementGeneratedPlayerSeasonCount => replacementGeneratedPlayerSeasonCount;
        public string ContentHash => contentHash ?? string.Empty;
    }

    [Serializable]
    internal sealed class HistoricalRuntimeSummaryDto
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

    [Serializable]
    internal sealed class HistoricalRuntimePlayerPersonDto
    {
        [SerializeField] private string playerPersonId;
        [SerializeField] private int birthYear;
        [SerializeField] private string bats;
        [SerializeField] private string throws;
        [SerializeField] private string primaryPosition;
        [SerializeField] private string registrationType;
        [SerializeField] private int careerStartYear;
        [SerializeField] private int careerEndYear;
        [SerializeField] private int[] personPotentialTrait;

        public string PlayerPersonId => playerPersonId ?? string.Empty;
        public int BirthYear => birthYear;
        public string Bats => bats ?? string.Empty;
        public string Throws => throws ?? string.Empty;
        public string PrimaryPosition => primaryPosition ?? string.Empty;
        public string RegistrationType => registrationType ?? string.Empty;
        public int CareerStartYear => careerStartYear;
        public int CareerEndYear => careerEndYear;
        public int[] PersonPotentialTrait => personPotentialTrait ?? Array.Empty<int>();
    }

    [Serializable]
    internal sealed class HistoricalRuntimePlayerPersonArrayDto
    {
        [SerializeField] private HistoricalRuntimePlayerPersonDto[] items;
        [SerializeField] private HistoricalRuntimeWorldIdentityNamePoolDto worldIdentityNamePool;

        public HistoricalRuntimePlayerPersonDto[] Items =>
            items ?? Array.Empty<HistoricalRuntimePlayerPersonDto>();
        public HistoricalRuntimeWorldIdentityNamePoolDto WorldIdentityNamePool => worldIdentityNamePool;
    }

    [Serializable]
    internal sealed class HistoricalRuntimeWorldIdentityNamePoolDto
    {
        [SerializeField] private string version;
        [SerializeField] private string[] domesticPlayerNames;
        [SerializeField] private string[] foreignPlayerNames;
        [SerializeField] private string[] franchiseNames;

        public string Version => version ?? string.Empty;
        public string[] DomesticPlayerNames => domesticPlayerNames ?? Array.Empty<string>();
        public string[] ForeignPlayerNames => foreignPlayerNames ?? Array.Empty<string>();
        public string[] FranchiseNames => franchiseNames ?? Array.Empty<string>();
    }

    [Serializable]
    internal sealed class HistoricalRuntimePlayerSeasonDto
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
    }

    [Serializable]
    internal sealed class HistoricalRuntimeCardDto
    {
        [SerializeField] private string cardId;
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string edition;
        [SerializeField] private int[] editionStatModifiers;

        public string CardId => cardId ?? string.Empty;
        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string Edition => edition ?? string.Empty;
        public int[] EditionStatModifiers => editionStatModifiers ?? Array.Empty<int>();
    }

    [Serializable]
    internal sealed class HistoricalRuntimeTeamSeasonDto
    {
        [SerializeField] private string teamSeasonKey;
        [SerializeField] private string franchiseId;
        [SerializeField] private int originYear;
        [SerializeField] private string[] allNormalCardIds;
        [SerializeField] private string[] core25CardIds;
        [SerializeField] private double referenceStrength;

        public string TeamSeasonKey => teamSeasonKey ?? string.Empty;
        public string FranchiseId => franchiseId ?? string.Empty;
        public int OriginYear => originYear;
        public string[] AllNormalCardIds => allNormalCardIds ?? Array.Empty<string>();
        public string[] Core25CardIds => core25CardIds ?? Array.Empty<string>();
        public double ReferenceStrength => referenceStrength;
    }

    [Serializable]
    internal sealed class HistoricalRuntimeSeasonRecordDto
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string teamSeasonKey;
        [SerializeField] private int seasonYear;
        [SerializeField] private string position;
        [SerializeField] private int plateAppearances;
        [SerializeField] private int hits;
        [SerializeField] private int homeRuns;
        [SerializeField] private int walks;
        [SerializeField] private int strikeouts;
        [SerializeField] private int defensiveChances;
        [SerializeField] private int fieldingErrors;
        [SerializeField] private int pitchingOuts;
        [SerializeField] private int earnedRuns;
        [SerializeField] private int pitchingStrikeouts;

        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string TeamSeasonKey => teamSeasonKey ?? string.Empty;
        public int SeasonYear => seasonYear;
        public string Position => position ?? string.Empty;
        public int PlateAppearances => plateAppearances;
        public int Hits => hits;
        public int HomeRuns => homeRuns;
        public int Walks => walks;
        public int Strikeouts => strikeouts;
        public int DefensiveChances => defensiveChances;
        public int FieldingErrors => fieldingErrors;
        public int PitchingOuts => pitchingOuts;
        public int EarnedRuns => earnedRuns;
        public int PitchingStrikeouts => pitchingStrikeouts;
    }

    [Serializable]
    internal sealed class HistoricalRuntimeAwardDto
    {
        [SerializeField] private int seasonYear;
        [SerializeField] private string awardType;
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string position;

        public int SeasonYear => seasonYear;
        public string AwardType => awardType ?? string.Empty;
        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string Position => position ?? string.Empty;
    }

    [Serializable]
    internal sealed class HistoricalRuntimeYearContentDto
    {
        [SerializeField] private int year;
        [SerializeField] private HistoricalRuntimePlayerSeasonDto[] playerSeasons;
        [SerializeField] private HistoricalRuntimeCardDto[] normalCards;
        [SerializeField] private HistoricalRuntimeTeamSeasonDto[] teamSeasons;
        [SerializeField] private HistoricalRuntimeSeasonRecordDto[] originalSeasonRecords;
        [SerializeField] private HistoricalRuntimeAwardDto[] originalAwardRecords;

        public int Year => year;
        public HistoricalRuntimePlayerSeasonDto[] PlayerSeasons =>
            playerSeasons ?? Array.Empty<HistoricalRuntimePlayerSeasonDto>();
        public HistoricalRuntimeCardDto[] NormalCards => normalCards ?? Array.Empty<HistoricalRuntimeCardDto>();
        public HistoricalRuntimeTeamSeasonDto[] TeamSeasons =>
            teamSeasons ?? Array.Empty<HistoricalRuntimeTeamSeasonDto>();
        public HistoricalRuntimeSeasonRecordDto[] OriginalSeasonRecords =>
            originalSeasonRecords ?? Array.Empty<HistoricalRuntimeSeasonRecordDto>();
        public HistoricalRuntimeAwardDto[] OriginalAwardRecords =>
            originalAwardRecords ?? Array.Empty<HistoricalRuntimeAwardDto>();
    }
}

#pragma warning restore 0649

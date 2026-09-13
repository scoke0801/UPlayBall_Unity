using System;
using UnityEngine;

// 아래 private 필드는 JsonUtility가 Reflection으로 채우므로 C# 할당 분석 대상이 아니다.
#pragma warning disable 0649

namespace Baseball.Game.Historical
{
    [Serializable]
    internal sealed class HistoricalSpecialCardContentDto
    {
        public int schemaVersion;
        public string baseContentHash;
        public HistoricalSpecialCardDto[] cards;
        public HistoricalSpecialLineageDto[] lineages;
        public HistoricalSpecialRecipeDto[] recipes;

        public Baseball.Core.Historical.BakedSpecialCardContent Build(string expectedHash)
        {
            if (schemaVersion != 1 || cards == null || lineages == null || recipes == null)
                throw new HistoricalContentLoadException("특수 카드 스키마가 올바르지 않습니다. schemaVersion=" + schemaVersion);
            if (baseContentHash != expectedHash)
                throw new HistoricalContentLoadException(
                    "특수 카드의 원본 콘텐츠가 일치하지 않습니다. 역사 콘텐츠 파이프라인 2단계에서 재발급하세요. " +
                    "expected=" + expectedHash + ", actual=" + baseContentHash);
            var definitions = new System.Collections.Generic.List<Baseball.Core.Historical.PlayerCardDefinition>();
            foreach (var card in cards)
            {
                if (!Enum.TryParse(card.edition, out Baseball.Core.Historical.PlayerCardEdition edition) ||
                    edition < Baseball.Core.Historical.PlayerCardEdition.Rare || edition > Baseball.Core.Historical.PlayerCardEdition.Legend)
                    throw new HistoricalContentLoadException("특수 카드 등급이 유효하지 않습니다.");
                definitions.Add(new Baseball.Core.Historical.PlayerCardDefinition(card.cardId, card.playerSeasonId,
                    edition, card.editionStatModifiers, teamColorLineageId: card.teamColorLineageId));
            }
            var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var lineage in lineages) map.Add(lineage.franchiseId, lineage.lineageId);
            var result = new System.Collections.Generic.List<Baseball.Core.Historical.SpecialRecruitRecipe>();
            foreach (var recipe in recipes)
            {
                var groups = new System.Collections.Generic.List<Baseball.Core.Historical.SpecialRecruitMaterialGroup>();
                foreach (var group in recipe.materialGroups)
                    groups.Add(new Baseball.Core.Historical.SpecialRecruitMaterialGroup(group.groupId, group.candidateCardIds));
                result.Add(new Baseball.Core.Historical.SpecialRecruitRecipe(recipe.targetCardId, groups));
            }
            return new Baseball.Core.Historical.BakedSpecialCardContent(definitions,
                new Baseball.Core.Historical.TeamColorLineageMap(map), result);
        }
    }

    [Serializable]
    internal sealed class HistoricalSpecialCardDto
    {
        public string cardId, playerSeasonId, edition, teamColorLineageId;
        public int[] editionStatModifiers;
    }

    [Serializable]
    internal sealed class HistoricalSpecialLineageDto { public string franchiseId, lineageId; }

    [Serializable]
    internal sealed class HistoricalSpecialRecipeDto
    {
        public string targetCardId;
        public HistoricalSpecialMaterialDto[] materialGroups;
    }

    [Serializable]
    internal sealed class HistoricalSpecialMaterialDto { public string groupId; public string[] candidateCardIds; }

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
        [SerializeField] private HistoricalRuntimePersonIdentityResearchDto personIdentityResearch;
        [SerializeField] private string abilityFormulaVersion;
        [SerializeField] private int annualReferenceOverrideCardCount;
        [SerializeField] private string annualReferenceOverrideHash;
        [SerializeField] private string annualReferenceOverrideVersion;
        [SerializeField] private int researchRosterSupplementCount;
        [SerializeField] private string researchRosterSupplementHash;
        [SerializeField] private string researchRosterSupplementVersion;
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
        [SerializeField] private string pitchBalanceVersion;
        [SerializeField] private long pitchGenerationSeed;
        [SerializeField] private string contentHash;

        public string ReferenceDataVersion => referenceDataVersion ?? string.Empty;
        public string RawDataVersion => rawDataVersion ?? string.Empty;
        public int NormalizedSchemaVersion => normalizedSchemaVersion;
        public string NormalizedImporterVersion => normalizedImporterVersion ?? string.Empty;
        public string NormalizedContentHash => normalizedContentHash ?? string.Empty;
        public HistoricalRuntimePersonIdentityResearchDto PersonIdentityResearch => personIdentityResearch;
        public string AbilityFormulaVersion => abilityFormulaVersion ?? string.Empty;
        public int AnnualReferenceOverrideCardCount => annualReferenceOverrideCardCount;
        public string AnnualReferenceOverrideHash => annualReferenceOverrideHash ?? string.Empty;
        public string AnnualReferenceOverrideVersion => annualReferenceOverrideVersion ?? string.Empty;
        public int ResearchRosterSupplementCount => researchRosterSupplementCount;
        public string ResearchRosterSupplementHash => researchRosterSupplementHash ?? string.Empty;
        public string ResearchRosterSupplementVersion => researchRosterSupplementVersion ?? string.Empty;
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
        public string PitchBalanceVersion => pitchBalanceVersion ?? string.Empty;
        public long PitchGenerationSeed => pitchGenerationSeed;
        public string ContentHash => contentHash ?? string.Empty;
    }

    [Serializable]
    internal sealed class HistoricalRuntimePersonIdentityResearchDto
    {
        [SerializeField] private int birthYearResearchedCount;
        [SerializeField] private int handednessResearchedCount;
        [SerializeField] private int personCount;
        [SerializeField] private string researchVersion;

        public int BirthYearResearchedCount => birthYearResearchedCount;
        public int HandednessResearchedCount => handednessResearchedCount;
        public int PersonCount => personCount;
        public string ResearchVersion => researchVersion ?? string.Empty;
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
        [SerializeField] private bool isPositionEvidenceMissing;
        public bool IsPositionEvidenceMissing => isPositionEvidenceMissing;
        [SerializeField] private HistoricalRuntimePositionProficiencyDto[] secondaryPositions;
        public HistoricalRuntimePositionProficiencyDto[] SecondaryPositions => secondaryPositions ?? Array.Empty<HistoricalRuntimePositionProficiencyDto>();
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
        [SerializeField] private HistoricalRuntimePitchEntryDto[] pitchRepertoire;
        [SerializeField] private string pitchDataSourceKind;
        [SerializeField] private string pitchBalanceVersion;
        [SerializeField] private int historicalPitchingAppearances;
        [SerializeField] private int historicalPitchingOuts;
        [SerializeField] private int historicalTeamGames;

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
        public HistoricalRuntimePitchEntryDto[] PitchRepertoire => pitchRepertoire ?? Array.Empty<HistoricalRuntimePitchEntryDto>();
        public string PitchDataSourceKind => pitchDataSourceKind ?? string.Empty;
        public string PitchBalanceVersion => pitchBalanceVersion ?? string.Empty;
        public int HistoricalPitchingAppearances => historicalPitchingAppearances;
        public int HistoricalPitchingOuts => historicalPitchingOuts;
        public int HistoricalTeamGames => historicalTeamGames;
    }

    [Serializable]
    internal sealed class HistoricalRuntimePositionProficiencyDto
    {
        [SerializeField] private string position;
        [SerializeField] private int proficiency;
        public string Position => position ?? string.Empty;
        public int Proficiency => proficiency;
    }

    [Serializable]
    internal sealed class HistoricalRuntimePitchEntryDto
    {
        [SerializeField] private string pitchType;
        [SerializeField] private int baseMastery;
        [SerializeField] private bool isPrimary;
        [SerializeField] private double developmentAffinity;
        [SerializeField] private double usagePreference;
        [SerializeField] private double velocityOffset;
        public Baseball.Core.Players.PitchRepertoireEntry Build()
        {
            if (!Enum.TryParse(pitchType, out Baseball.Core.Players.PitchType type))
                throw new InvalidOperationException("구종을 읽을 수 없습니다: " + pitchType);
            return new Baseball.Core.Players.PitchRepertoireEntry(type, baseMastery, isPrimary,
                developmentAffinity, usagePreference, velocityOffset);
        }
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
        [SerializeField] private int atBats;
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
        public int AtBats => atBats;
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

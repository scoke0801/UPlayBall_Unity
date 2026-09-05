using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>능력치 하나를 구성한 정규화 지표의 원본값·표본 보정·기여도를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalAbilityComponentTrace
    {
        [SerializeField] private string metric;
        [SerializeField] private double rawValue;
        [SerializeField] private double numerator;
        [SerializeField] private double denominator;
        [SerializeField] private double sampleSize;
        [SerializeField] private double reliabilityConstant;
        [SerializeField] private bool isAvailable;
        [SerializeField] private string roleTier;
        [SerializeField] private double referenceWeight;
        [SerializeField] private string referenceFamilyKey;
        [SerializeField] private double referenceEffectiveSampleCount;
        [SerializeField] private double referenceGroupShare;
        [SerializeField] private double referenceFamilyShare;
        [SerializeField] private double groupMean;
        [SerializeField] private double groupStdDev;
        [SerializeField] private double rawZ;
        [SerializeField] private double boundedZ;
        [SerializeField] private double priorZ;
        [SerializeField] private string referenceGroupKey;
        [SerializeField] private double reliability;
        [SerializeField] private double adjustedZ;
        [SerializeField] private double weight;
        [SerializeField] private double contribution;

        public string Metric => metric ?? string.Empty;
        public double RawValue => rawValue;
        public double Numerator => numerator;
        public double Denominator => denominator;
        public double SampleSize => sampleSize;
        public double ReliabilityConstant => reliabilityConstant;
        public bool IsAvailable => isAvailable;
        /// <summary>Qualified/Limited 표본 진단값이다. 비교 모집단 분리에는 쓰지 않는다.</summary>
        public string RoleTier => roleTier ?? string.Empty;
        /// <summary>이 값이 비교 모집단 평균·표준편차에 기여한 가중치다.</summary>
        public double ReferenceWeight => referenceWeight;
        public string ReferenceFamilyKey => referenceFamilyKey ?? string.Empty;
        public double ReferenceEffectiveSampleCount => referenceEffectiveSampleCount;
        /// <summary>좁은 집단 통계가 최종 기준에서 차지한 비율이다. 1보다 작으면 상위 집단과 혼합했다.</summary>
        public double ReferenceGroupShare => referenceGroupShare;
        public double ReferenceFamilyShare => referenceFamilyShare;
        public double GroupMean => groupMean;
        public double GroupStdDev => groupStdDev;
        public double RawZ => rawZ;
        public double BoundedZ => boundedZ;
        public double PriorZ => priorZ;
        public string ReferenceGroupKey => referenceGroupKey ?? string.Empty;
        public double Reliability => reliability;
        public double AdjustedZ => adjustedZ;
        public double Weight => weight;
        public double Contribution => contribution;
    }

    /// <summary>BaseAttribute 하나가 Rating으로 변환된 전 과정을 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalAbilityDerivationTrace
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private int seasonYear;
        [SerializeField] private string attribute;
        [SerializeField] private string groupKey;
        [SerializeField] private HistoricalAbilityComponentTrace[] components;
        [SerializeField] private double combinedZ;
        [SerializeField] private double ratingBeforeClamp;
        [SerializeField] private int ratingAfterClamp;

        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public int SeasonYear => seasonYear;
        public string Attribute => attribute ?? string.Empty;
        public string GroupKey => groupKey ?? string.Empty;
        public IReadOnlyList<HistoricalAbilityComponentTrace> Components =>
            components ?? Array.Empty<HistoricalAbilityComponentTrace>();
        public double CombinedZ => combinedZ;
        public double RatingBeforeClamp => ratingBeforeClamp;
        public int RatingAfterClamp => ratingAfterClamp;
    }

    /// <summary>역할 보정 Composite에 적용한 능력치별 Weight를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalCostRoleWeightTrace
    {
        [SerializeField] private string ability;
        [SerializeField] private double weight;

        public string Ability => ability ?? string.Empty;
        public double Weight => weight;
    }

    /// <summary>Cost Composite에 대한 능력치 하나의 정규화 Weight와 기여도를 보관한다.</summary>
    [Serializable]
    public sealed class HistoricalCostAbilityContributionTrace
    {
        [SerializeField] private string ability;
        [SerializeField] private int rating;
        [SerializeField] private double weight;
        [SerializeField] private double normalizedWeight;
        [SerializeField] private double contribution;

        public string Ability => ability ?? string.Empty;
        public int Rating => rating;
        public double Weight => weight;
        public double NormalizedWeight => normalizedWeight;
        public double Contribution => contribution;
    }

    /// <summary>SourceBacked 모집단에서 확정한 Cost percentile 경계 하나다.</summary>
    [Serializable]
    public sealed class HistoricalCostThresholdTrace
    {
        [SerializeField] private double upperExclusive;
        [SerializeField] private int cost;
        [SerializeField] private double sourceCompositeAtBoundary;

        public double UpperExclusive => upperExclusive;
        public int Cost => cost;
        public double SourceCompositeAtBoundary => sourceCompositeAtBoundary;
    }

    /// <summary>시즌 길이·역할 기준의 출전량 진단이다. 가격에는 다시 반영하지 않는다.</summary>
    [Serializable]
    public sealed class HistoricalCostEligibilityTrace
    {
        [SerializeField] private string tier;
        [SerializeField] private string scope;
        [SerializeField] private double sample;
        [SerializeField] private double workloadRatio;
        [SerializeField] private int maximumCost;
        [SerializeField] private bool affectsCost;
        [SerializeField] private double fullSeasonSample;
        [SerializeField] private string reason;

        /// <summary>Full / Regular / Limited / Tiny 중 하나다. 사람이 읽는 이름일 뿐 상한을 정하지 않는다.</summary>
        public string Tier => tier ?? string.Empty;
        /// <summary>출전량 기준을 고른 집단이다. Hitter 또는 투수 역할군이다.</summary>
        public string Scope => scope ?? string.Empty;
        /// <summary>타자는 타석 수, 투수는 상대한 타자 수다.</summary>
        public double Sample => sample;
        /// <summary>해당 연도와 역할군의 온전한 시즌 대비 출전 비율이다.</summary>
        public double WorkloadRatio => workloadRatio;
        public int MaximumCost => maximumCost;
        public bool AffectsCost => affectsCost;
        public double FullSeasonSample => fullSeasonSample;
        public string Reason => reason ?? string.Empty;
    }

    /// <summary>OriginYear의 같은 선수 유형 모집단에서 RoleAdjustedComposite가 Cost로 변환된 근거다.</summary>
    [Serializable]
    public sealed class HistoricalCostDerivationTrace
    {
        [SerializeField] private string dataProvenance;
        [SerializeField] private string costPopulationSource;
        [SerializeField] private int sourcePopulationSize;
        [SerializeField] private bool replacementExcludedFromThresholdCalculation;
        [SerializeField] private HistoricalCostThresholdTrace[] thresholds;
        [SerializeField] private HistoricalCostThresholdTrace[] compositeThresholds;
        [SerializeField] private string costMethod;
        [SerializeField] private int[] baseAttributes;
        [SerializeField] private string role;
        [SerializeField] private string roleProfile;
        [SerializeField] private HistoricalCostRoleWeightTrace[] roleWeights;
        [SerializeField] private HistoricalCostAbilityContributionTrace[] abilityContribution;
        [SerializeField] private double composite;
        [SerializeField] private int originYear;
        [SerializeField] private int populationCount;
        [SerializeField] private int rank;
        [SerializeField] private double percentile;
        [SerializeField] private int rawPercentileCost;
        [SerializeField] private HistoricalCostEligibilityTrace costEligibility;
        [SerializeField] private int cost;

        public string DataProvenance => dataProvenance ?? string.Empty;
        public string CostPopulationSource => costPopulationSource ?? string.Empty;
        public int SourcePopulationSize => sourcePopulationSize;
        public bool ReplacementExcludedFromThresholdCalculation =>
            replacementExcludedFromThresholdCalculation;
        public IReadOnlyList<HistoricalCostThresholdTrace> Thresholds =>
            thresholds ?? Array.Empty<HistoricalCostThresholdTrace>();
        public IReadOnlyList<HistoricalCostThresholdTrace> CompositeThresholds =>
            compositeThresholds ?? Array.Empty<HistoricalCostThresholdTrace>();
        public string CostMethod => costMethod ?? string.Empty;
        public IReadOnlyList<int> BaseAttributes => baseAttributes ?? Array.Empty<int>();
        public string Role => role ?? string.Empty;
        public string RoleProfile => roleProfile ?? string.Empty;
        public IReadOnlyList<HistoricalCostRoleWeightTrace> RoleWeights =>
            roleWeights ?? Array.Empty<HistoricalCostRoleWeightTrace>();
        public IReadOnlyList<HistoricalCostAbilityContributionTrace> AbilityContribution =>
            abilityContribution ?? Array.Empty<HistoricalCostAbilityContributionTrace>();
        public double Composite => composite;
        public int OriginYear => originYear;
        public int PopulationCount => populationCount;
        public int Rank => rank;
        public double Percentile => percentile;
        /// <summary>자격 상한을 적용하기 전 백분위만으로 나온 Cost다.</summary>
        public int RawPercentileCost => rawPercentileCost;
        public HistoricalCostEligibilityTrace CostEligibility => costEligibility;
        public int Cost => cost;
    }

    /// <summary>한 시즌의 Natural Position 후보와 수비 출전 근거다.</summary>
    [Serializable]
    public sealed class HistoricalPositionCandidateTrace
    {
        [SerializeField] private string position;
        [SerializeField] private string sourcePosition;
        [SerializeField] private double inningsOuts;
        [SerializeField] private double gamesStarted;
        [SerializeField] private double games;

        public string Position => position ?? string.Empty;
        public string SourcePosition => sourcePosition ?? string.Empty;
        public double InningsOuts => inningsOuts;
        public double GamesStarted => gamesStarted;
        public double Games => games;
    }

    /// <summary>한 시즌 투수의 선발·구원·마무리 기용 증거다.</summary>
    [Serializable]
    public sealed class HistoricalPitcherRoleEvidenceTrace
    {
        [SerializeField] private double games;
        [SerializeField] private double gamesStarted;
        [SerializeField] private bool gamesStartedAvailable;
        [SerializeField] private double completeGames;
        [SerializeField] private double reliefAppearances;
        [SerializeField] private double gamesFinished;
        [SerializeField] private bool gamesFinishedAvailable;
        [SerializeField] private double saves;
        [SerializeField] private double holds;
        [SerializeField] private bool holdsAvailable;
        [SerializeField] private double innings;
        [SerializeField] private double gamesStartedRate;
        [SerializeField] private double inferredStarterRate;
        [SerializeField] private double reliefRate;
        [SerializeField] private double inningsPerGame;
        [SerializeField] private string starterEvidenceMode;

        public double Games => games;
        public double GamesStarted => gamesStarted;
        public bool GamesStartedAvailable => gamesStartedAvailable;
        public double CompleteGames => completeGames;
        public double ReliefAppearances => reliefAppearances;
        public double GamesFinished => gamesFinished;
        public bool GamesFinishedAvailable => gamesFinishedAvailable;
        public double Saves => saves;
        public double Holds => holds;
        public bool HoldsAvailable => holdsAvailable;
        public double Innings => innings;
        public double GamesStartedRate => gamesStartedRate;
        public double InferredStarterRate => inferredStarterRate;
        public double ReliefRate => reliefRate;
        public double InningsPerGame => inningsPerGame;
        public string StarterEvidenceMode => starterEvidenceMode ?? string.Empty;
    }

    /// <summary>Natural PitcherRole 후보 하나의 classifier 점수다.</summary>
    [Serializable]
    public sealed class HistoricalPitcherRoleScoreTrace
    {
        [SerializeField] private string role;
        [SerializeField] private double score;

        public string Role => role ?? string.Empty;
        public double Score => score;
    }

    /// <summary>파생·로스터 검증 단계가 남긴 안정된 진단 코드와 배정 근거다.</summary>
    [Serializable]
    public sealed class HistoricalDerivationWarningTrace
    {
        [SerializeField] private string code;
        [SerializeField] private string position;
        [SerializeField] private string assignedRole;
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string naturalPosition;
        [SerializeField] private string naturalPitcherRole;
        [SerializeField] private string message;

        public string Code => code ?? string.Empty;
        public string Position => position ?? string.Empty;
        public string AssignedRole => assignedRole ?? string.Empty;
        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string NaturalPosition => naturalPosition ?? string.Empty;
        public string NaturalPitcherRole => naturalPitcherRole ?? string.Empty;
        public string Message => message ?? string.Empty;
    }

    /// <summary>PlayerSeason의 Natural Position/PitcherRole 파생 결과와 시즌 근거다.</summary>
    [Serializable]
    public sealed class HistoricalPositionRoleDerivationTrace
    {
        [SerializeField] private string classifierVersion;
        [SerializeField] private HistoricalPositionCandidateTrace[] positionCandidates;
        [SerializeField] private string selectedNaturalPosition;
        [SerializeField] private HistoricalPitcherRoleEvidenceTrace pitcherRoleEvidence;
        [SerializeField] private HistoricalPitcherRoleScoreTrace[] pitcherRoleScores;
        [SerializeField] private string selectedNaturalPitcherRole;
        [SerializeField] private string reason;
        [SerializeField] private HistoricalDerivationWarningTrace[] warnings;

        public string ClassifierVersion => classifierVersion ?? string.Empty;
        public IReadOnlyList<HistoricalPositionCandidateTrace> PositionCandidates =>
            positionCandidates ?? Array.Empty<HistoricalPositionCandidateTrace>();
        public string SelectedNaturalPosition => selectedNaturalPosition ?? string.Empty;
        public HistoricalPitcherRoleEvidenceTrace PitcherRoleEvidence => pitcherRoleEvidence;
        public IReadOnlyList<HistoricalPitcherRoleScoreTrace> PitcherRoleScores =>
            pitcherRoleScores ?? Array.Empty<HistoricalPitcherRoleScoreTrace>();
        public string SelectedNaturalPitcherRole => selectedNaturalPitcherRole ?? string.Empty;
        public string Reason => reason ?? string.Empty;
        public IReadOnlyList<HistoricalDerivationWarningTrace> Warnings =>
            warnings ?? Array.Empty<HistoricalDerivationWarningTrace>();
    }

    /// <summary>수비 Starter slot 후보 하나의 적격 여부와 선택 점수다.</summary>
    [Serializable]
    public sealed class HistoricalRosterCandidateTrace
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string naturalPosition;
        [SerializeField] private string naturalPitcherRole;
        [SerializeField] private bool isEligible;
        [SerializeField] private double score;

        public string PlayerSeasonId => playerSeasonId ?? string.Empty;
        public string NaturalPosition => naturalPosition ?? string.Empty;
        public string NaturalPitcherRole => naturalPitcherRole ?? string.Empty;
        public bool IsEligible => isEligible;
        public double Score => score;
    }

    /// <summary>수비 Starter slot 하나의 후보 순위와 최종 배정이다.</summary>
    [Serializable]
    public sealed class HistoricalStartingSlotTrace
    {
        [SerializeField] private string slot;
        [SerializeField] private HistoricalRosterCandidateTrace[] candidates;
        [SerializeField] private string selectedPlayerSeasonId;
        [SerializeField] private double selectionScore;
        [SerializeField] private bool isFallback;
        [SerializeField] private string reason;

        public string Slot => slot ?? string.Empty;
        public IReadOnlyList<HistoricalRosterCandidateTrace> Candidates =>
            candidates ?? Array.Empty<HistoricalRosterCandidateTrace>();
        public string SelectedPlayerSeasonId => selectedPlayerSeasonId ?? string.Empty;
        public double SelectionScore => selectionScore;
        public bool IsFallback => isFallback;
        public string Reason => reason ?? string.Empty;
    }

    /// <summary>DH 또는 Bench 한 자리의 선택 결과다.</summary>
    [Serializable]
    public sealed class HistoricalSimpleRosterSelectionTrace
    {
        [SerializeField] private string playerSeasonId;
        [SerializeField] private string selectedPlayerSeasonId;
        [SerializeField] private double selectionScore;
        [SerializeField] private string reason;
        [SerializeField] private double abilityScore;
        [SerializeField] private string[] newBackupPositions;

        public string PlayerSeasonId => string.IsNullOrEmpty(playerSeasonId)
            ? selectedPlayerSeasonId ?? string.Empty
            : playerSeasonId;
        public double SelectionScore => selectionScore;
        public string Reason => reason ?? string.Empty;
        public double AbilityScore => abilityScore;
        public IReadOnlyList<string> NewBackupPositions => newBackupPositions ?? Array.Empty<string>();
    }

    /// <summary>투수 Assigned Role 하나의 후보 순위와 선택 목록이다.</summary>
    [Serializable]
    public sealed class HistoricalPitchingStaffSelectionTrace
    {
        [SerializeField] private string assignedRole;
        [SerializeField] private HistoricalRosterCandidateTrace[] candidates;
        [SerializeField] private string[] selectedPlayerSeasonIds;
        [SerializeField] private int fallbackCount;
        [SerializeField] private string reason;

        public string AssignedRole => assignedRole ?? string.Empty;
        public IReadOnlyList<HistoricalRosterCandidateTrace> Candidates =>
            candidates ?? Array.Empty<HistoricalRosterCandidateTrace>();
        public IReadOnlyList<string> SelectedPlayerSeasonIds =>
            selectedPlayerSeasonIds ?? Array.Empty<string>();
        public int FallbackCount => fallbackCount;
        public string Reason => reason ?? string.Empty;
    }

    /// <summary>TeamSeason의 수비·DH 동시 배치, 백업 벤치와 투수진 선택 근거다.</summary>
    [Serializable]
    public sealed class HistoricalRosterSelectionTrace
    {
        [SerializeField] private string rosterBuilderVersion;
        [SerializeField] private string teamSeasonKey;
        [SerializeField] private HistoricalStartingSlotTrace[] startingSlots;
        [SerializeField] private HistoricalSimpleRosterSelectionTrace designatedHitter;
        [SerializeField] private HistoricalSimpleRosterSelectionTrace[] bench;
        [SerializeField] private HistoricalPitchingStaffSelectionTrace[] pitchingStaff;
        [SerializeField] private HistoricalDerivationWarningTrace[] validationWarnings;

        public string RosterBuilderVersion => rosterBuilderVersion ?? string.Empty;
        public string TeamSeasonKey => teamSeasonKey ?? string.Empty;
        public IReadOnlyList<HistoricalStartingSlotTrace> StartingSlots =>
            startingSlots ?? Array.Empty<HistoricalStartingSlotTrace>();
        public HistoricalSimpleRosterSelectionTrace DesignatedHitter => designatedHitter;
        public IReadOnlyList<HistoricalSimpleRosterSelectionTrace> Bench =>
            bench ?? Array.Empty<HistoricalSimpleRosterSelectionTrace>();
        public IReadOnlyList<HistoricalPitchingStaffSelectionTrace> PitchingStaff =>
            pitchingStaff ?? Array.Empty<HistoricalPitchingStaffSelectionTrace>();
        public IReadOnlyList<HistoricalDerivationWarningTrace> ValidationWarnings =>
            validationWarnings ?? Array.Empty<HistoricalDerivationWarningTrace>();
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>상대 분석 값이 관측 근거로 얼마나 확정되었는지 구분한다.</summary>
    public enum IntelState
    {
        Confirmed,
        HighConfidence,
        Estimated,
        LowConfidence,
        Unknown
    }

    /// <summary>정확한 내부 체력 대신 공개하는 불펜 가용성 등급이다.</summary>
    public enum BullpenReadiness
    {
        Fresh,
        Available,
        Tired,
        VeryTired,
        Unavailable
    }

    /// <summary>시설과 스태프의 Intel 보정을 한 번만 합성하는 경기 전 분석 Context다.</summary>
    public readonly struct ScoutingConfidenceContext
    {
        public ScoutingConfidenceContext(
            double baseEvidenceMultiplier,
            double facilityModifier,
            double staffModifier)
        {
            if (baseEvidenceMultiplier <= 0d || double.IsNaN(baseEvidenceMultiplier) ||
                double.IsInfinity(baseEvidenceMultiplier))
                throw new ArgumentOutOfRangeException(nameof(baseEvidenceMultiplier));
            ValidateModifier(facilityModifier, nameof(facilityModifier));
            ValidateModifier(staffModifier, nameof(staffModifier));
            BaseEvidenceMultiplier = baseEvidenceMultiplier;
            FacilityModifier = facilityModifier;
            StaffModifier = staffModifier;
        }

        public double BaseEvidenceMultiplier { get; }
        public double FacilityModifier { get; }
        public double StaffModifier { get; }
        public double CombinedMultiplier => BaseEvidenceMultiplier + FacilityModifier + StaffModifier;

        private static void ValidateModifier(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>관측 근거를 신뢰도 등급으로 바꾸는 데이터 기반 경계다.</summary>
    public sealed class ScoutingConfidenceDefinition
    {
        public ScoutingConfidenceDefinition(
            double lowConfidenceThreshold,
            double estimatedThreshold,
            double highConfidenceThreshold,
            double maximumInferredConfidence,
            double maximumCombinedModifier,
            double publicRosterEvidenceQuality = 0.72d,
            double publicRosterRecencyFactor = 0.90d,
            double publicRosterSampleFactor = 0.75d,
            int bullpenFreshMaximumRecentPitches = 15,
            int bullpenTiredMinimumRecentPitches = 36,
            int bullpenVeryTiredMinimumRecentPitches = 61,
            int bullpenFreshMinimumRestDays = 2)
        {
            ValidateUnit(lowConfidenceThreshold, nameof(lowConfidenceThreshold));
            ValidateUnit(estimatedThreshold, nameof(estimatedThreshold));
            ValidateUnit(highConfidenceThreshold, nameof(highConfidenceThreshold));
            ValidateUnit(maximumInferredConfidence, nameof(maximumInferredConfidence));
            if (lowConfidenceThreshold <= 0d ||
                estimatedThreshold < lowConfidenceThreshold ||
                highConfidenceThreshold < estimatedThreshold ||
                maximumInferredConfidence < highConfidenceThreshold)
            {
                throw new ArgumentException("IntelState 경계는 0보다 크고 오름차순이어야 합니다.");
            }
            if (maximumCombinedModifier <= 0d || double.IsNaN(maximumCombinedModifier) ||
                double.IsInfinity(maximumCombinedModifier))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCombinedModifier));
            }
            ValidateUnit(publicRosterEvidenceQuality, nameof(publicRosterEvidenceQuality));
            ValidateUnit(publicRosterRecencyFactor, nameof(publicRosterRecencyFactor));
            ValidateUnit(publicRosterSampleFactor, nameof(publicRosterSampleFactor));
            if (bullpenFreshMaximumRecentPitches < 0 ||
                bullpenTiredMinimumRecentPitches <= bullpenFreshMaximumRecentPitches ||
                bullpenVeryTiredMinimumRecentPitches <= bullpenTiredMinimumRecentPitches ||
                bullpenFreshMinimumRestDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bullpenFreshMaximumRecentPitches));
            }

            LowConfidenceThreshold = lowConfidenceThreshold;
            EstimatedThreshold = estimatedThreshold;
            HighConfidenceThreshold = highConfidenceThreshold;
            MaximumInferredConfidence = maximumInferredConfidence;
            MaximumCombinedModifier = maximumCombinedModifier;
            PublicRosterEvidenceQuality = publicRosterEvidenceQuality;
            PublicRosterRecencyFactor = publicRosterRecencyFactor;
            PublicRosterSampleFactor = publicRosterSampleFactor;
            BullpenFreshMaximumRecentPitches = bullpenFreshMaximumRecentPitches;
            BullpenTiredMinimumRecentPitches = bullpenTiredMinimumRecentPitches;
            BullpenVeryTiredMinimumRecentPitches = bullpenVeryTiredMinimumRecentPitches;
            BullpenFreshMinimumRestDays = bullpenFreshMinimumRestDays;
        }

        public double LowConfidenceThreshold { get; }
        public double EstimatedThreshold { get; }
        public double HighConfidenceThreshold { get; }
        public double MaximumInferredConfidence { get; }
        public double MaximumCombinedModifier { get; }
        public double PublicRosterEvidenceQuality { get; }
        public double PublicRosterRecencyFactor { get; }
        public double PublicRosterSampleFactor { get; }
        public int BullpenFreshMaximumRecentPitches { get; }
        public int BullpenTiredMinimumRecentPitches { get; }
        public int BullpenVeryTiredMinimumRecentPitches { get; }
        public int BullpenFreshMinimumRestDays { get; }

        /// <summary>초기 상대 분석 신뢰도 경계를 데이터 계약으로 제공한다.</summary>
        public static ScoutingConfidenceDefinition CreateInitial()
        {
            return new ScoutingConfidenceDefinition(
                lowConfidenceThreshold: 0.18d,
                estimatedThreshold: 0.42d,
                highConfidenceThreshold: 0.68d,
                maximumInferredConfidence: 0.92d,
                maximumCombinedModifier: 1.25d);
        }

        private static void ValidateUnit(double value, string parameterName)
        {
            if (value < 0d || value > 1d || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>값과 관측 신뢰도를 분리해 UI가 추정치를 사실로 표시하지 않게 한다.</summary>
    public sealed class ScoutedValue<T>
    {
        private readonly string[] _evidenceTags;

        public ScoutedValue(
            T value,
            IntelState state,
            double confidence01,
            IReadOnlyList<string> evidenceTags)
            : this(true, value, state, confidence01, evidenceTags)
        {
            if (state == IntelState.Unknown)
                throw new ArgumentException("값이 있는 ScoutedValue는 Unknown일 수 없습니다.", nameof(state));
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            if (state == IntelState.Confirmed && confidence01 != 1d)
                throw new ArgumentException("Confirmed 값의 신뢰도는 1이어야 합니다.", nameof(confidence01));
        }

        private ScoutedValue(
            bool hasValue,
            T value,
            IntelState state,
            double confidence01,
            IReadOnlyList<string> evidenceTags)
        {
            if (confidence01 < 0d || confidence01 > 1d || double.IsNaN(confidence01))
                throw new ArgumentOutOfRangeException(nameof(confidence01));
            if (!hasValue && (state != IntelState.Unknown || confidence01 != 0d))
                throw new ArgumentException("값이 없는 ScoutedValue는 신뢰도 0의 Unknown이어야 합니다.");

            HasValue = hasValue;
            Value = value;
            State = state;
            Confidence01 = confidence01;
            _evidenceTags = CopyTags(evidenceTags);
        }

        public bool HasValue { get; }
        public T Value { get; }
        public IntelState State { get; }
        public double Confidence01 { get; }
        public IReadOnlyList<string> EvidenceTags => _evidenceTags;

        /// <summary>근거가 없는 항목을 값이 없는 Unknown으로 만든다.</summary>
        public static ScoutedValue<T> Unknown(IReadOnlyList<string> evidenceTags = null)
        {
            return new ScoutedValue<T>(false, default, IntelState.Unknown, 0d, evidenceTags);
        }

        private static string[] CopyTags(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("EvidenceTag는 비어 있을 수 없습니다.", nameof(source));
                result[index] = source[index].Trim();
            }
            return result;
        }
    }

    /// <summary>상대 분석에서 실제 선수 객체 대신 사용하는 안정 식별자 묶음이다.</summary>
    public sealed class ScoutedPlayerReference
    {
        public ScoutedPlayerReference(string cardId, string playerPersonId)
        {
            CardId = RequireId(cardId, nameof(cardId));
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>관측 로테이션으로 예상한 선발 후보와 공개된 투구 손이다.</summary>
    public sealed class ProbableStarterProjection
    {
        public ProbableStarterProjection(string cardId, string playerPersonId, Handedness throwingHand)
        {
            if (throwingHand == Handedness.Switch)
                throw new ArgumentException("투수 손은 Switch일 수 없습니다.", nameof(throwingHand));
            Player = new ScoutedPlayerReference(cardId, playerPersonId);
            ThrowingHand = throwingHand;
        }

        public ScoutedPlayerReference Player { get; }
        public Handedness ThrowingHand { get; }
    }

    /// <summary>관측 기록으로 예상한 상대의 한 타순 슬롯이다.</summary>
    public sealed class ExpectedLineupEntry
    {
        public ExpectedLineupEntry(
            string cardId,
            string playerPersonId,
            int battingOrder,
            PlayerPosition position)
        {
            if (battingOrder < 1 || battingOrder > ActiveRosterCompositionRule.StartingHitterCount)
                throw new ArgumentOutOfRangeException(nameof(battingOrder));
            if (position < PlayerPosition.Catcher || position > PlayerPosition.DesignatedHitter)
                throw new ArgumentOutOfRangeException(nameof(position));

            Player = new ScoutedPlayerReference(cardId, playerPersonId);
            BattingOrder = battingOrder;
            Position = position;
        }

        public ScoutedPlayerReference Player { get; }
        public int BattingOrder { get; }
        public PlayerPosition Position { get; }
    }

    /// <summary>관측 가능한 최근 등판량을 등급화한 상대 불펜 항목이다.</summary>
    public sealed class BullpenReadinessEntry
    {
        public BullpenReadinessEntry(
            string cardId,
            string playerPersonId,
            ActiveRosterRole role,
            BullpenReadiness readiness)
        {
            bool isReliefRole = ActiveRosterCompositionRule.Standard.IsBullpenRole(role) ||
                                role == ActiveRosterRole.Setup ||
                                role == ActiveRosterRole.Closer;
            if (!isReliefRole)
                throw new ArgumentException("불펜 역할만 Readiness를 가질 수 있습니다.", nameof(role));
            if (!Enum.IsDefined(typeof(BullpenReadiness), readiness))
                throw new ArgumentOutOfRangeException(nameof(readiness));

            Player = new ScoutedPlayerReference(cardId, playerPersonId);
            Role = role;
            Readiness = readiness;
        }

        public ScoutedPlayerReference Player { get; }
        public ActiveRosterRole Role { get; }
        public BullpenReadiness Readiness { get; }
    }

    /// <summary>완료된 경기만 집계한 상대의 최근 승패다.</summary>
    public sealed class OpponentRecentForm
    {
        public OpponentRecentForm(int wins, int losses, int ties)
        {
            if (wins < 0) throw new ArgumentOutOfRangeException(nameof(wins));
            if (losses < 0) throw new ArgumentOutOfRangeException(nameof(losses));
            if (ties < 0) throw new ArgumentOutOfRangeException(nameof(ties));
            Wins = wins;
            Losses = losses;
            Ties = ties;
        }

        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public int Games => Wins + Losses + Ties;
    }

    /// <summary>공개 통계를 정규화한 공격·투수·수비 프로필 한 축이다.</summary>
    public sealed class OpponentPerformanceProfile
    {
        public OpponentPerformanceProfile(double index01, string descriptionKey)
        {
            if (index01 < 0d || index01 > 1d || double.IsNaN(index01))
                throw new ArgumentOutOfRangeException(nameof(index01));
            if (string.IsNullOrWhiteSpace(descriptionKey))
                throw new ArgumentException("설명 키는 비어 있을 수 없습니다.", nameof(descriptionKey));
            Index01 = index01;
            DescriptionKey = descriptionKey.Trim();
        }

        public double Index01 { get; }
        public string DescriptionKey { get; }
    }

    /// <summary>완료 경기 행동에서 추정한 감독 성향 설명 모음이다.</summary>
    public sealed class ManagerTendencyEstimate
    {
        private readonly string[] _tendencyKeys;

        public ManagerTendencyEstimate(IReadOnlyList<string> tendencyKeys)
        {
            _tendencyKeys = CopyRequiredStrings(tendencyKeys, nameof(tendencyKeys));
        }

        public IReadOnlyList<string> TendencyKeys => _tendencyKeys;

        private static string[] CopyRequiredStrings(IReadOnlyList<string> source, string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("성향 키는 비어 있을 수 없습니다.", parameterName);
                result[index] = source[index].Trim();
            }
            return result;
        }
    }

    /// <summary>이미 종료된 경기에서 관측된 전술 사용 패턴이다.</summary>
    public sealed class RecentTacticPatternSummary
    {
        public RecentTacticPatternSummary(string tacticCardId, int observedCount)
        {
            if (string.IsNullOrWhiteSpace(tacticCardId))
                throw new ArgumentException("TacticCardId는 비어 있을 수 없습니다.", nameof(tacticCardId));
            if (observedCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(observedCount));
            TacticCardId = tacticCardId.Trim();
            ObservedCount = observedCount;
        }

        public string TacticCardId { get; }
        public int ObservedCount { get; }
    }

    /// <summary>공개 기록에서 도출한 위협·상성·위험 안내 한 건이다.</summary>
    public sealed class ScoutingReportNote
    {
        public ScoutingReportNote(string noteKey, string subjectCardId = null)
        {
            if (string.IsNullOrWhiteSpace(noteKey))
                throw new ArgumentException("NoteKey는 비어 있을 수 없습니다.", nameof(noteKey));
            NoteKey = noteKey.Trim();
            SubjectCardId = NormalizeOptionalId(subjectCardId);
        }

        public string NoteKey { get; }
        public string SubjectCardId { get; }

        private static string NormalizeOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>상대 분석 전체가 가진 평균 신뢰도와 관측 항목 수다.</summary>
    public sealed class ReportConfidenceSummary
    {
        public ReportConfidenceSummary(IntelState state, double confidence01, int observedItemCount)
        {
            if (!Enum.IsDefined(typeof(IntelState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (confidence01 < 0d || confidence01 > 1d || double.IsNaN(confidence01))
                throw new ArgumentOutOfRangeException(nameof(confidence01));
            if (observedItemCount < 0)
                throw new ArgumentOutOfRangeException(nameof(observedItemCount));
            if (observedItemCount == 0 && (state != IntelState.Unknown || confidence01 != 0d))
                throw new ArgumentException("관측 항목이 없으면 Report Confidence는 Unknown이어야 합니다.");
            State = state;
            Confidence01 = confidence01;
            ObservedItemCount = observedItemCount;
        }

        public IntelState State { get; }
        public double Confidence01 { get; }
        public int ObservedItemCount { get; }
    }

    /// <summary>공개 일정과 완료 경기 증거만으로 생성된 경기 전 상대 분석 결과다.</summary>
    public sealed class OpponentScoutingReport
    {
        private readonly ScoutedValue<ExpectedLineupEntry>[] _expectedLineup;
        private readonly ScoutedValue<BullpenReadinessEntry>[] _bullpenReadiness;
        private readonly ScoutedValue<RecentTacticPatternSummary>[] _recentTacticPatterns;
        private readonly ScoutingReportNote[] _keyThreats;
        private readonly ScoutingReportNote[] _favorableMatchups;
        private readonly ScoutingReportNote[] _riskWarnings;

        public OpponentScoutingReport(
            int scheduledGameId,
            string opponentTeamSeasonKey,
            DateTime generatedAtGameDate,
            ReportConfidenceSummary reportConfidenceSummary,
            ScoutedValue<ProbableStarterProjection> probableStarter,
            IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> expectedLineup,
            IReadOnlyList<ScoutedValue<BullpenReadinessEntry>> bullpenReadiness,
            ScoutedValue<OpponentRecentForm> recentForm,
            ScoutedValue<OpponentPerformanceProfile> offenseProfile,
            ScoutedValue<OpponentPerformanceProfile> pitchingProfile,
            ScoutedValue<OpponentPerformanceProfile> defenseProfile,
            ScoutedValue<ManagerTendencyEstimate> managerTendencyEstimate,
            IReadOnlyList<ScoutedValue<RecentTacticPatternSummary>> recentTacticPatterns,
            IReadOnlyList<ScoutingReportNote> keyThreats,
            IReadOnlyList<ScoutingReportNote> favorableMatchups,
            IReadOnlyList<ScoutingReportNote> riskWarnings)
        {
            if (scheduledGameId <= 0) throw new ArgumentOutOfRangeException(nameof(scheduledGameId));
            if (string.IsNullOrWhiteSpace(opponentTeamSeasonKey))
                throw new ArgumentException("OpponentTeamSeasonKey는 비어 있을 수 없습니다.", nameof(opponentTeamSeasonKey));

            ScheduledGameId = scheduledGameId;
            OpponentTeamSeasonKey = opponentTeamSeasonKey.Trim();
            GeneratedAtGameDate = generatedAtGameDate.Date;
            ReportConfidenceSummary = reportConfidenceSummary ?? throw new ArgumentNullException(nameof(reportConfidenceSummary));
            ProbableStarter = probableStarter ?? throw new ArgumentNullException(nameof(probableStarter));
            _expectedLineup = CopyRequired(expectedLineup, nameof(expectedLineup));
            _bullpenReadiness = CopyRequired(bullpenReadiness, nameof(bullpenReadiness));
            RecentForm = recentForm ?? throw new ArgumentNullException(nameof(recentForm));
            OffenseProfile = offenseProfile ?? throw new ArgumentNullException(nameof(offenseProfile));
            PitchingProfile = pitchingProfile ?? throw new ArgumentNullException(nameof(pitchingProfile));
            DefenseProfile = defenseProfile ?? throw new ArgumentNullException(nameof(defenseProfile));
            ManagerTendencyEstimate = managerTendencyEstimate ?? throw new ArgumentNullException(nameof(managerTendencyEstimate));
            _recentTacticPatterns = CopyRequired(recentTacticPatterns, nameof(recentTacticPatterns));
            _keyThreats = CopyRequired(keyThreats, nameof(keyThreats));
            _favorableMatchups = CopyRequired(favorableMatchups, nameof(favorableMatchups));
            _riskWarnings = CopyRequired(riskWarnings, nameof(riskWarnings));
        }

        public int ScheduledGameId { get; }
        public string OpponentTeamSeasonKey { get; }
        public DateTime GeneratedAtGameDate { get; }
        public ReportConfidenceSummary ReportConfidenceSummary { get; }
        public ScoutedValue<ProbableStarterProjection> ProbableStarter { get; }
        public IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> ExpectedLineup => _expectedLineup;
        public IReadOnlyList<ScoutedValue<BullpenReadinessEntry>> BullpenReadiness => _bullpenReadiness;
        public ScoutedValue<OpponentRecentForm> RecentForm { get; }
        public ScoutedValue<OpponentPerformanceProfile> OffenseProfile { get; }
        public ScoutedValue<OpponentPerformanceProfile> PitchingProfile { get; }
        public ScoutedValue<OpponentPerformanceProfile> DefenseProfile { get; }
        public ScoutedValue<ManagerTendencyEstimate> ManagerTendencyEstimate { get; }
        public IReadOnlyList<ScoutedValue<RecentTacticPatternSummary>> RecentTacticPatternSummary => _recentTacticPatterns;
        public IReadOnlyList<ScoutingReportNote> KeyThreats => _keyThreats;
        public IReadOnlyList<ScoutingReportNote> FavorableMatchups => _favorableMatchups;
        public IReadOnlyList<ScoutingReportNote> RiskWarnings => _riskWarnings;

        private static TItem[] CopyRequired<TItem>(IReadOnlyList<TItem> source, string parameterName)
            where TItem : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new TItem[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 항목이 있습니다.", parameterName);
            return result;
        }
    }
}

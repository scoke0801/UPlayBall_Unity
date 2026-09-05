using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Simulation.Historical
{
    /// <summary>한 관측값의 품질·최근성·표본 크기를 서로 분리한 신뢰도 입력이다.</summary>
    public readonly struct ScoutingEvidenceStrength
    {
        public ScoutingEvidenceStrength(
            bool hasEvidence,
            bool isConfirmed,
            double baseEvidenceQuality01,
            double recencyFactor01,
            double sampleSizeFactor01)
        {
            ValidateUnit(baseEvidenceQuality01, nameof(baseEvidenceQuality01));
            ValidateUnit(recencyFactor01, nameof(recencyFactor01));
            ValidateUnit(sampleSizeFactor01, nameof(sampleSizeFactor01));
            if (!hasEvidence && (isConfirmed || baseEvidenceQuality01 != 0d ||
                                 recencyFactor01 != 0d || sampleSizeFactor01 != 0d))
            {
                throw new ArgumentException("근거가 없으면 확정 여부와 신뢰도 요소도 없어야 합니다.");
            }
            HasEvidence = hasEvidence;
            IsConfirmed = isConfirmed;
            BaseEvidenceQuality01 = baseEvidenceQuality01;
            RecencyFactor01 = recencyFactor01;
            SampleSizeFactor01 = sampleSizeFactor01;
        }

        public bool HasEvidence { get; }
        public bool IsConfirmed { get; }
        public double BaseEvidenceQuality01 { get; }
        public double RecencyFactor01 { get; }
        public double SampleSizeFactor01 { get; }

        public static ScoutingEvidenceStrength None =>
            new ScoutingEvidenceStrength(false, false, 0d, 0d, 0d);

        private static void ValidateUnit(double value, string parameterName)
        {
            if (value < 0d || value > 1d || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>완료 경기나 공개 발표에서 관측된 값과 근거만 전달한다.</summary>
    public sealed class ObservedScoutingValue<T>
    {
        private readonly string[] _evidenceTags;

        public ObservedScoutingValue(
            T value,
            ScoutingEvidenceStrength strength,
            IReadOnlyList<string> evidenceTags)
        {
            if (!strength.HasEvidence)
                throw new ArgumentException("값이 있는 관측에는 근거가 필요합니다.", nameof(strength));
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            Value = value;
            Strength = strength;
            _evidenceTags = CopyTags(evidenceTags);
        }

        private ObservedScoutingValue(IReadOnlyList<string> evidenceTags)
        {
            Value = default;
            Strength = ScoutingEvidenceStrength.None;
            _evidenceTags = CopyTags(evidenceTags);
        }

        public T Value { get; }
        public ScoutingEvidenceStrength Strength { get; }
        public IReadOnlyList<string> EvidenceTags => _evidenceTags;

        public static ObservedScoutingValue<T> Unknown(IReadOnlyList<string> evidenceTags = null)
        {
            return new ObservedScoutingValue<T>(evidenceTags);
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

    /// <summary>시설·스태프가 합성한 단일 배율로 관측 근거를 IntelState로 변환한다.</summary>
    public sealed class ScoutingConfidenceResolver
    {
        private readonly ScoutingConfidenceDefinition _definition;

        public ScoutingConfidenceResolver(ScoutingConfidenceDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public ScoutedValue<T> Resolve<T>(ObservedScoutingValue<T> observed, double combinedModifier)
        {
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            ValidateCombinedModifier(combinedModifier);
            if (!observed.Strength.HasEvidence)
                return ScoutedValue<T>.Unknown(observed.EvidenceTags);

            double confidence = CalculateConfidence(observed.Strength, combinedModifier);
            IntelState state = ResolveState(confidence, observed.Strength.IsConfirmed);
            if (state == IntelState.Unknown)
                return ScoutedValue<T>.Unknown(observed.EvidenceTags);
            return new ScoutedValue<T>(observed.Value, state, confidence, observed.EvidenceTags);
        }

        public double CalculateConfidence(ScoutingEvidenceStrength strength, double combinedModifier)
        {
            ValidateCombinedModifier(combinedModifier);
            if (!strength.HasEvidence)
                return 0d;
            if (strength.IsConfirmed)
                return 1d;

            double modifier = Math.Min(combinedModifier, _definition.MaximumCombinedModifier);
            double confidence = strength.BaseEvidenceQuality01 *
                                strength.RecencyFactor01 *
                                strength.SampleSizeFactor01 *
                                modifier;
            return Math.Min(confidence, _definition.MaximumInferredConfidence);
        }

        public IntelState ResolveState(double confidence01, bool isConfirmed = false)
        {
            if (confidence01 < 0d || confidence01 > 1d || double.IsNaN(confidence01))
                throw new ArgumentOutOfRangeException(nameof(confidence01));
            if (isConfirmed)
                return IntelState.Confirmed;
            if (confidence01 >= _definition.HighConfidenceThreshold)
                return IntelState.HighConfidence;
            if (confidence01 >= _definition.EstimatedThreshold)
                return IntelState.Estimated;
            if (confidence01 >= _definition.LowConfidenceThreshold)
                return IntelState.LowConfidence;
            return IntelState.Unknown;
        }

        private static void ValidateCombinedModifier(double combinedModifier)
        {
            if (combinedModifier <= 0d || double.IsNaN(combinedModifier) || double.IsInfinity(combinedModifier))
                throw new ArgumentOutOfRangeException(nameof(combinedModifier));
        }
    }

    /// <summary>공개 로테이션과 완료된 선발 기록에서 얻은 후보 근거다.</summary>
    public sealed class ProbableStarterCandidateEvidence
    {
        private readonly string[] _evidenceTags;

        public ProbableStarterCandidateEvidence(
            string cardId,
            string playerPersonId,
            Handedness throwingHand,
            int observedRotationTurnDistance,
            int daysSinceLastStart,
            int recentStartCount,
            bool isPubliclyAvailable,
            ScoutingEvidenceStrength strength,
            IReadOnlyList<string> evidenceTags)
        {
            CardId = RequireId(cardId, nameof(cardId));
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            if (throwingHand == Handedness.Switch)
                throw new ArgumentException("투수 손은 Switch일 수 없습니다.", nameof(throwingHand));
            if (observedRotationTurnDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(observedRotationTurnDistance));
            if (daysSinceLastStart < 0) throw new ArgumentOutOfRangeException(nameof(daysSinceLastStart));
            if (recentStartCount < 0) throw new ArgumentOutOfRangeException(nameof(recentStartCount));
            ThrowingHand = throwingHand;
            ObservedRotationTurnDistance = observedRotationTurnDistance;
            DaysSinceLastStart = daysSinceLastStart;
            RecentStartCount = recentStartCount;
            IsPubliclyAvailable = isPubliclyAvailable;
            Strength = strength;
            _evidenceTags = CopyTags(evidenceTags);
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }
        public Handedness ThrowingHand { get; }
        public int ObservedRotationTurnDistance { get; }
        public int DaysSinceLastStart { get; }
        public int RecentStartCount { get; }
        public bool IsPubliclyAvailable { get; }
        public ScoutingEvidenceStrength Strength { get; }
        public IReadOnlyList<string> EvidenceTags => _evidenceTags;

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }

        private static string[] CopyTags(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
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

    /// <summary>공개 로테이션 순번을 우선하고 stable ID로 동률을 해소하는 예상 선발 Resolver다.</summary>
    public sealed class ProbableStarterResolver
    {
        private readonly ScoutingConfidenceResolver _confidenceResolver;

        public ProbableStarterResolver(ScoutingConfidenceResolver confidenceResolver)
        {
            _confidenceResolver = confidenceResolver ?? throw new ArgumentNullException(nameof(confidenceResolver));
        }

        public ScoutedValue<ProbableStarterProjection> Resolve(
            IReadOnlyList<ProbableStarterCandidateEvidence> candidates,
            double combinedConfidenceModifier)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            ProbableStarterCandidateEvidence selected = null;
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                ProbableStarterCandidateEvidence candidate = candidates[index] ??
                    throw new ArgumentException("null 선발 후보가 있습니다.", nameof(candidates));
                if (!seenCardIds.Add(candidate.CardId))
                    throw new ArgumentException("같은 CardId의 선발 후보가 중복되었습니다.", nameof(candidates));
                if (!candidate.IsPubliclyAvailable || !candidate.Strength.HasEvidence)
                    continue;
                if (selected == null || Compare(candidate, selected) < 0)
                    selected = candidate;
            }
            if (selected == null)
                return ScoutedValue<ProbableStarterProjection>.Unknown();

            var observed = new ObservedScoutingValue<ProbableStarterProjection>(
                new ProbableStarterProjection(
                    selected.CardId,
                    selected.PlayerPersonId,
                    selected.ThrowingHand),
                selected.Strength,
                selected.EvidenceTags);
            return _confidenceResolver.Resolve(observed, combinedConfidenceModifier);
        }

        private static int Compare(
            ProbableStarterCandidateEvidence left,
            ProbableStarterCandidateEvidence right)
        {
            int comparison = left.ObservedRotationTurnDistance.CompareTo(right.ObservedRotationTurnDistance);
            if (comparison != 0) return comparison;
            comparison = right.DaysSinceLastStart.CompareTo(left.DaysSinceLastStart);
            if (comparison != 0) return comparison;
            comparison = right.RecentStartCount.CompareTo(left.RecentStartCount);
            if (comparison != 0) return comparison;
            return string.CompareOrdinal(left.CardId, right.CardId);
        }
    }

    /// <summary>최근 선발 출장과 상대 선발 손잡이로만 예상 타선을 만드는 후보 근거다.</summary>
    public sealed class ExpectedLineupCandidateEvidence
    {
        private readonly string[] _evidenceTags;

        public ExpectedLineupCandidateEvidence(
            string cardId,
            string playerPersonId,
            PlayerPosition mostObservedPosition,
            int recentStartCount,
            int recentStartsVsLeft,
            int recentStartsVsRight,
            int gamesSinceLastStart,
            double averageBattingOrder,
            bool isPubliclyAvailable,
            ScoutingEvidenceStrength strength,
            IReadOnlyList<string> evidenceTags)
        {
            CardId = RequireId(cardId, nameof(cardId));
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            if (mostObservedPosition < PlayerPosition.Catcher ||
                mostObservedPosition > PlayerPosition.DesignatedHitter)
                throw new ArgumentOutOfRangeException(nameof(mostObservedPosition));
            if (recentStartCount < 0) throw new ArgumentOutOfRangeException(nameof(recentStartCount));
            if (recentStartsVsLeft < 0 || recentStartsVsLeft > recentStartCount)
                throw new ArgumentOutOfRangeException(nameof(recentStartsVsLeft));
            if (recentStartsVsRight < 0 || recentStartsVsRight > recentStartCount)
                throw new ArgumentOutOfRangeException(nameof(recentStartsVsRight));
            if (gamesSinceLastStart < 0) throw new ArgumentOutOfRangeException(nameof(gamesSinceLastStart));
            if (averageBattingOrder < 1d ||
                averageBattingOrder > ActiveRosterCompositionRule.StartingHitterCount ||
                double.IsNaN(averageBattingOrder))
            {
                throw new ArgumentOutOfRangeException(nameof(averageBattingOrder));
            }

            MostObservedPosition = mostObservedPosition;
            RecentStartCount = recentStartCount;
            RecentStartsVsLeft = recentStartsVsLeft;
            RecentStartsVsRight = recentStartsVsRight;
            GamesSinceLastStart = gamesSinceLastStart;
            AverageBattingOrder = averageBattingOrder;
            IsPubliclyAvailable = isPubliclyAvailable;
            Strength = strength;
            _evidenceTags = CopyTags(evidenceTags);
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }
        public PlayerPosition MostObservedPosition { get; }
        public int RecentStartCount { get; }
        public int RecentStartsVsLeft { get; }
        public int RecentStartsVsRight { get; }
        public int GamesSinceLastStart { get; }
        public double AverageBattingOrder { get; }
        public bool IsPubliclyAvailable { get; }
        public ScoutingEvidenceStrength Strength { get; }
        public IReadOnlyList<string> EvidenceTags => _evidenceTags;

        public int GetHandedStartCount(Handedness? pitcherHand)
        {
            if (!pitcherHand.HasValue) return RecentStartCount;
            if (pitcherHand == Handedness.Left) return RecentStartsVsLeft;
            if (pitcherHand == Handedness.Right) return RecentStartsVsRight;
            throw new ArgumentException("투수 손은 Switch일 수 없습니다.", nameof(pitcherHand));
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }

        private static string[] CopyTags(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
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

    /// <summary>최근 출장 빈도와 타순만 사용하고 실제 AI 라인업 객체를 읽지 않는 예상 타선 Resolver다.</summary>
    public sealed class ExpectedLineupEstimator
    {
        private readonly ScoutingConfidenceResolver _confidenceResolver;

        public ExpectedLineupEstimator(ScoutingConfidenceResolver confidenceResolver)
        {
            _confidenceResolver = confidenceResolver ?? throw new ArgumentNullException(nameof(confidenceResolver));
        }

        public IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> Estimate(
            IReadOnlyList<ExpectedLineupCandidateEvidence> candidates,
            Handedness? probableStarterHand,
            double combinedConfidenceModifier)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (probableStarterHand == Handedness.Switch)
                throw new ArgumentException("투수 손은 Switch일 수 없습니다.", nameof(probableStarterHand));

            var selected = new List<ExpectedLineupCandidateEvidence>(
                ActiveRosterCompositionRule.StartingHitterCount);
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                ExpectedLineupCandidateEvidence candidate = candidates[index] ??
                    throw new ArgumentException("null 타선 후보가 있습니다.", nameof(candidates));
                if (!seenCardIds.Add(candidate.CardId))
                    throw new ArgumentException("같은 CardId의 타선 후보가 중복되었습니다.", nameof(candidates));
                if (!candidate.IsPubliclyAvailable || !candidate.Strength.HasEvidence)
                    continue;
                InsertCandidate(selected, candidate, probableStarterHand);
                if (selected.Count > ActiveRosterCompositionRule.StartingHitterCount)
                    selected.RemoveAt(selected.Count - 1);
            }

            selected.Sort(CompareBattingOrder);
            var result = new ScoutedValue<ExpectedLineupEntry>[selected.Count];
            for (int index = 0; index < selected.Count; index++)
            {
                ExpectedLineupCandidateEvidence candidate = selected[index];
                var observed = new ObservedScoutingValue<ExpectedLineupEntry>(
                    new ExpectedLineupEntry(
                        candidate.CardId,
                        candidate.PlayerPersonId,
                        index + 1,
                        candidate.MostObservedPosition),
                    candidate.Strength,
                    candidate.EvidenceTags);
                result[index] = _confidenceResolver.Resolve(observed, combinedConfidenceModifier);
            }
            return result;
        }

        private static void InsertCandidate(
            List<ExpectedLineupCandidateEvidence> selected,
            ExpectedLineupCandidateEvidence candidate,
            Handedness? pitcherHand)
        {
            int insertionIndex = selected.Count;
            for (int index = 0; index < selected.Count; index++)
            {
                if (CompareSelection(candidate, selected[index], pitcherHand) < 0)
                {
                    insertionIndex = index;
                    break;
                }
            }
            selected.Insert(insertionIndex, candidate);
        }

        private static int CompareSelection(
            ExpectedLineupCandidateEvidence left,
            ExpectedLineupCandidateEvidence right,
            Handedness? pitcherHand)
        {
            int comparison = right.GetHandedStartCount(pitcherHand)
                .CompareTo(left.GetHandedStartCount(pitcherHand));
            if (comparison != 0) return comparison;
            comparison = right.RecentStartCount.CompareTo(left.RecentStartCount);
            if (comparison != 0) return comparison;
            comparison = left.GamesSinceLastStart.CompareTo(right.GamesSinceLastStart);
            if (comparison != 0) return comparison;
            comparison = left.AverageBattingOrder.CompareTo(right.AverageBattingOrder);
            if (comparison != 0) return comparison;
            return string.CompareOrdinal(left.CardId, right.CardId);
        }

        private static int CompareBattingOrder(
            ExpectedLineupCandidateEvidence left,
            ExpectedLineupCandidateEvidence right)
        {
            int comparison = left.AverageBattingOrder.CompareTo(right.AverageBattingOrder);
            if (comparison != 0) return comparison;
            comparison = right.RecentStartCount.CompareTo(left.RecentStartCount);
            if (comparison != 0) return comparison;
            return string.CompareOrdinal(left.CardId, right.CardId);
        }
    }

    /// <summary>최근 공개 투구 수를 Readiness 등급으로 바꾸는 Balance 계약이다.</summary>
    public sealed class BullpenReadinessDefinition
    {
        public BullpenReadinessDefinition(
            int freshMaximumRecentPitchCount,
            int tiredMinimumRecentPitchCount,
            int veryTiredMinimumRecentPitchCount,
            int freshMinimumRestDays)
        {
            if (freshMaximumRecentPitchCount < 0)
                throw new ArgumentOutOfRangeException(nameof(freshMaximumRecentPitchCount));
            if (tiredMinimumRecentPitchCount <= freshMaximumRecentPitchCount)
                throw new ArgumentOutOfRangeException(nameof(tiredMinimumRecentPitchCount));
            if (veryTiredMinimumRecentPitchCount <= tiredMinimumRecentPitchCount)
                throw new ArgumentOutOfRangeException(nameof(veryTiredMinimumRecentPitchCount));
            if (freshMinimumRestDays < 0)
                throw new ArgumentOutOfRangeException(nameof(freshMinimumRestDays));
            FreshMaximumRecentPitchCount = freshMaximumRecentPitchCount;
            TiredMinimumRecentPitchCount = tiredMinimumRecentPitchCount;
            VeryTiredMinimumRecentPitchCount = veryTiredMinimumRecentPitchCount;
            FreshMinimumRestDays = freshMinimumRestDays;
        }

        public int FreshMaximumRecentPitchCount { get; }
        public int TiredMinimumRecentPitchCount { get; }
        public int VeryTiredMinimumRecentPitchCount { get; }
        public int FreshMinimumRestDays { get; }
    }

    /// <summary>완료 경기의 투구 수와 공개 결장 상태만 담는 불펜 근거다.</summary>
    public sealed class BullpenReadinessEvidence
    {
        private readonly string[] _evidenceTags;

        public BullpenReadinessEvidence(
            string cardId,
            string playerPersonId,
            ActiveRosterRole role,
            int recentPitchCount,
            int restDays,
            bool isPubliclyAvailable,
            ScoutingEvidenceStrength strength,
            IReadOnlyList<string> evidenceTags)
        {
            bool isReliefRole = ActiveRosterCompositionRule.Standard.IsBullpenRole(role) ||
                                role == ActiveRosterRole.Setup || role == ActiveRosterRole.Closer;
            if (!isReliefRole) throw new ArgumentException("불펜 역할이 필요합니다.", nameof(role));
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(playerPersonId))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", nameof(playerPersonId));
            if (recentPitchCount < 0) throw new ArgumentOutOfRangeException(nameof(recentPitchCount));
            if (restDays < 0) throw new ArgumentOutOfRangeException(nameof(restDays));
            CardId = cardId.Trim();
            PlayerPersonId = playerPersonId.Trim();
            Role = role;
            RecentPitchCount = recentPitchCount;
            RestDays = restDays;
            IsPubliclyAvailable = isPubliclyAvailable;
            Strength = strength;
            _evidenceTags = CopyTags(evidenceTags);
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }
        public ActiveRosterRole Role { get; }
        public int RecentPitchCount { get; }
        public int RestDays { get; }
        public bool IsPubliclyAvailable { get; }
        public ScoutingEvidenceStrength Strength { get; }
        public IReadOnlyList<string> EvidenceTags => _evidenceTags;

        private static string[] CopyTags(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
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

    /// <summary>정확한 내부 stamina를 노출하지 않고 관측 workload를 등급화한다.</summary>
    public sealed class BullpenReadinessResolver
    {
        private readonly BullpenReadinessDefinition _definition;
        private readonly ScoutingConfidenceResolver _confidenceResolver;

        public BullpenReadinessResolver(
            BullpenReadinessDefinition definition,
            ScoutingConfidenceResolver confidenceResolver)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _confidenceResolver = confidenceResolver ?? throw new ArgumentNullException(nameof(confidenceResolver));
        }

        public IReadOnlyList<ScoutedValue<BullpenReadinessEntry>> Resolve(
            IReadOnlyList<BullpenReadinessEvidence> evidence,
            double combinedConfidenceModifier)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var ordered = new List<BullpenReadinessEvidence>(evidence.Count);
            var seenCardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < evidence.Count; index++)
            {
                BullpenReadinessEvidence item = evidence[index] ??
                    throw new ArgumentException("null 불펜 근거가 있습니다.", nameof(evidence));
                if (!seenCardIds.Add(item.CardId))
                    throw new ArgumentException("같은 CardId의 불펜 근거가 중복되었습니다.", nameof(evidence));
                InsertByRoleAndId(ordered, item);
            }

            var result = new ScoutedValue<BullpenReadinessEntry>[ordered.Count];
            for (int index = 0; index < ordered.Count; index++)
            {
                BullpenReadinessEvidence item = ordered[index];
                if (!item.Strength.HasEvidence)
                {
                    result[index] = ScoutedValue<BullpenReadinessEntry>.Unknown(item.EvidenceTags);
                    continue;
                }
                BullpenReadiness readiness = ResolveBand(item);
                var observed = new ObservedScoutingValue<BullpenReadinessEntry>(
                    new BullpenReadinessEntry(item.CardId, item.PlayerPersonId, item.Role, readiness),
                    item.Strength,
                    item.EvidenceTags);
                result[index] = _confidenceResolver.Resolve(observed, combinedConfidenceModifier);
            }
            return result;
        }

        private BullpenReadiness ResolveBand(BullpenReadinessEvidence evidence)
        {
            if (!evidence.IsPubliclyAvailable) return BullpenReadiness.Unavailable;
            if (evidence.RecentPitchCount >= _definition.VeryTiredMinimumRecentPitchCount)
                return BullpenReadiness.VeryTired;
            if (evidence.RecentPitchCount >= _definition.TiredMinimumRecentPitchCount)
                return BullpenReadiness.Tired;
            if (evidence.RecentPitchCount <= _definition.FreshMaximumRecentPitchCount &&
                evidence.RestDays >= _definition.FreshMinimumRestDays)
                return BullpenReadiness.Fresh;
            return BullpenReadiness.Available;
        }

        private static void InsertByRoleAndId(
            List<BullpenReadinessEvidence> ordered,
            BullpenReadinessEvidence item)
        {
            int insertionIndex = ordered.Count;
            for (int index = 0; index < ordered.Count; index++)
            {
                int comparison = item.Role.CompareTo(ordered[index].Role);
                if (comparison < 0 || comparison == 0 &&
                    string.CompareOrdinal(item.CardId, ordered[index].CardId) < 0)
                {
                    insertionIndex = index;
                    break;
                }
            }
            ordered.Insert(insertionIndex, item);
        }
    }

    /// <summary>상대 분석 Builder가 소비하는 공개·완료 경기 증거의 묶음이다.</summary>
    public sealed class OpponentScoutingReportEvidence
    {
        private readonly ProbableStarterCandidateEvidence[] _starterCandidates;
        private readonly ExpectedLineupCandidateEvidence[] _lineupCandidates;
        private readonly BullpenReadinessEvidence[] _bullpenEvidence;
        private readonly ObservedScoutingValue<RecentTacticPatternSummary>[] _tacticPatterns;
        private readonly ScoutingReportNote[] _keyThreats;
        private readonly ScoutingReportNote[] _favorableMatchups;
        private readonly ScoutingReportNote[] _riskWarnings;

        public OpponentScoutingReportEvidence(
            int scheduledGameId,
            string opponentTeamSeasonKey,
            DateTime generatedAtGameDate,
            IReadOnlyList<ProbableStarterCandidateEvidence> starterCandidates,
            IReadOnlyList<ExpectedLineupCandidateEvidence> lineupCandidates,
            IReadOnlyList<BullpenReadinessEvidence> bullpenEvidence,
            ObservedScoutingValue<OpponentRecentForm> recentForm,
            ObservedScoutingValue<OpponentPerformanceProfile> offenseProfile,
            ObservedScoutingValue<OpponentPerformanceProfile> pitchingProfile,
            ObservedScoutingValue<OpponentPerformanceProfile> defenseProfile,
            ObservedScoutingValue<ManagerTendencyEstimate> managerTendency,
            IReadOnlyList<ObservedScoutingValue<RecentTacticPatternSummary>> tacticPatterns,
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
            _starterCandidates = CopyRequired(starterCandidates, nameof(starterCandidates));
            _lineupCandidates = CopyRequired(lineupCandidates, nameof(lineupCandidates));
            _bullpenEvidence = CopyRequired(bullpenEvidence, nameof(bullpenEvidence));
            RecentForm = recentForm ?? throw new ArgumentNullException(nameof(recentForm));
            OffenseProfile = offenseProfile ?? throw new ArgumentNullException(nameof(offenseProfile));
            PitchingProfile = pitchingProfile ?? throw new ArgumentNullException(nameof(pitchingProfile));
            DefenseProfile = defenseProfile ?? throw new ArgumentNullException(nameof(defenseProfile));
            ManagerTendency = managerTendency ?? throw new ArgumentNullException(nameof(managerTendency));
            _tacticPatterns = CopyRequired(tacticPatterns, nameof(tacticPatterns));
            _keyThreats = CopyRequired(keyThreats, nameof(keyThreats));
            _favorableMatchups = CopyRequired(favorableMatchups, nameof(favorableMatchups));
            _riskWarnings = CopyRequired(riskWarnings, nameof(riskWarnings));
        }

        public int ScheduledGameId { get; }
        public string OpponentTeamSeasonKey { get; }
        public DateTime GeneratedAtGameDate { get; }
        public IReadOnlyList<ProbableStarterCandidateEvidence> StarterCandidates => _starterCandidates;
        public IReadOnlyList<ExpectedLineupCandidateEvidence> LineupCandidates => _lineupCandidates;
        public IReadOnlyList<BullpenReadinessEvidence> BullpenEvidence => _bullpenEvidence;
        public ObservedScoutingValue<OpponentRecentForm> RecentForm { get; }
        public ObservedScoutingValue<OpponentPerformanceProfile> OffenseProfile { get; }
        public ObservedScoutingValue<OpponentPerformanceProfile> PitchingProfile { get; }
        public ObservedScoutingValue<OpponentPerformanceProfile> DefenseProfile { get; }
        public ObservedScoutingValue<ManagerTendencyEstimate> ManagerTendency { get; }
        public IReadOnlyList<ObservedScoutingValue<RecentTacticPatternSummary>> TacticPatterns => _tacticPatterns;
        public IReadOnlyList<ScoutingReportNote> KeyThreats => _keyThreats;
        public IReadOnlyList<ScoutingReportNote> FavorableMatchups => _favorableMatchups;
        public IReadOnlyList<ScoutingReportNote> RiskWarnings => _riskWarnings;

        private static T[] CopyRequired<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var result = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 항목이 있습니다.", parameterName);
            return result;
        }
    }

    /// <summary>개별 추정기를 합성해 저장하지 않는 상대 분석 Report를 만든다.</summary>
    public sealed class OpponentScoutingReportBuilder
    {
        private readonly ScoutingConfidenceResolver _confidenceResolver;
        private readonly ProbableStarterResolver _starterResolver;
        private readonly ExpectedLineupEstimator _lineupEstimator;
        private readonly BullpenReadinessResolver _bullpenResolver;

        public OpponentScoutingReportBuilder(
            ScoutingConfidenceResolver confidenceResolver,
            ProbableStarterResolver starterResolver,
            ExpectedLineupEstimator lineupEstimator,
            BullpenReadinessResolver bullpenResolver)
        {
            _confidenceResolver = confidenceResolver ?? throw new ArgumentNullException(nameof(confidenceResolver));
            _starterResolver = starterResolver ?? throw new ArgumentNullException(nameof(starterResolver));
            _lineupEstimator = lineupEstimator ?? throw new ArgumentNullException(nameof(lineupEstimator));
            _bullpenResolver = bullpenResolver ?? throw new ArgumentNullException(nameof(bullpenResolver));
        }

        public OpponentScoutingReport Build(
            OpponentScoutingReportEvidence evidence,
            double combinedConfidenceModifier)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            ScoutedValue<ProbableStarterProjection> probableStarter = _starterResolver.Resolve(
                evidence.StarterCandidates,
                combinedConfidenceModifier);
            IReadOnlyList<ScoutedValue<ExpectedLineupEntry>> expectedLineup = _lineupEstimator.Estimate(
                evidence.LineupCandidates,
                probableStarter.HasValue ? probableStarter.Value.ThrowingHand : (Handedness?)null,
                combinedConfidenceModifier);
            IReadOnlyList<ScoutedValue<BullpenReadinessEntry>> bullpen = _bullpenResolver.Resolve(
                evidence.BullpenEvidence,
                combinedConfidenceModifier);
            ScoutedValue<OpponentRecentForm> recentForm = _confidenceResolver.Resolve(
                evidence.RecentForm,
                combinedConfidenceModifier);
            ScoutedValue<OpponentPerformanceProfile> offense = _confidenceResolver.Resolve(
                evidence.OffenseProfile,
                combinedConfidenceModifier);
            ScoutedValue<OpponentPerformanceProfile> pitching = _confidenceResolver.Resolve(
                evidence.PitchingProfile,
                combinedConfidenceModifier);
            ScoutedValue<OpponentPerformanceProfile> defense = _confidenceResolver.Resolve(
                evidence.DefenseProfile,
                combinedConfidenceModifier);
            ScoutedValue<ManagerTendencyEstimate> tendency = _confidenceResolver.Resolve(
                evidence.ManagerTendency,
                combinedConfidenceModifier);

            var tacticPatterns = new ScoutedValue<RecentTacticPatternSummary>[evidence.TacticPatterns.Count];
            for (int index = 0; index < tacticPatterns.Length; index++)
            {
                tacticPatterns[index] = _confidenceResolver.Resolve(
                    evidence.TacticPatterns[index],
                    combinedConfidenceModifier);
            }

            var summary = new ConfidenceAccumulator(_confidenceResolver);
            summary.Add(probableStarter);
            summary.Add(expectedLineup);
            summary.Add(bullpen);
            summary.Add(recentForm);
            summary.Add(offense);
            summary.Add(pitching);
            summary.Add(defense);
            summary.Add(tendency);
            summary.Add(tacticPatterns);

            return new OpponentScoutingReport(
                evidence.ScheduledGameId,
                evidence.OpponentTeamSeasonKey,
                evidence.GeneratedAtGameDate,
                summary.Create(),
                probableStarter,
                expectedLineup,
                bullpen,
                recentForm,
                offense,
                pitching,
                defense,
                tendency,
                tacticPatterns,
                evidence.KeyThreats,
                evidence.FavorableMatchups,
                evidence.RiskWarnings);
        }

        private sealed class ConfidenceAccumulator
        {
            private readonly ScoutingConfidenceResolver _resolver;
            private double _sum;
            private int _count;
            private bool _allConfirmed = true;

            public ConfidenceAccumulator(ScoutingConfidenceResolver resolver)
            {
                _resolver = resolver;
            }

            public void Add<T>(ScoutedValue<T> value)
            {
                if (!value.HasValue) return;
                _sum += value.Confidence01;
                _count++;
                _allConfirmed &= value.State == IntelState.Confirmed;
            }

            public void Add<T>(IReadOnlyList<ScoutedValue<T>> values)
            {
                for (int index = 0; index < values.Count; index++) Add(values[index]);
            }

            public ReportConfidenceSummary Create()
            {
                if (_count == 0)
                    return new ReportConfidenceSummary(IntelState.Unknown, 0d, 0);
                double average = _sum / _count;
                return new ReportConfidenceSummary(
                    _resolver.ResolveState(average, _allConfirmed),
                    average,
                    _count);
            }
        }
    }
}

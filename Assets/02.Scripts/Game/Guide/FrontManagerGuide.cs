using System;
using System.Collections.Generic;
using System.Text;

namespace Baseball.Game.Guide
{
    /// <summary>현재 화면이 Guide를 즉시 표시할 수 있는지 판단하는 표현 계층 입력이다.</summary>
    public readonly struct GuideDisplayContext
    {
        private readonly IReadOnlyCollection<string> _suppressionContexts;

        public GuideDisplayContext(
            IReadOnlyCollection<string> suppressionContexts,
            bool isMatchInProgress,
            bool isSafePoint,
            string homeEntryId = "")
        {
            _suppressionContexts = suppressionContexts ?? Array.Empty<string>();
            IsMatchInProgress = isMatchInProgress;
            IsSafePoint = isSafePoint;
            HomeEntryId = homeEntryId ?? string.Empty;
        }

        public bool IsMatchInProgress { get; }
        public bool IsSafePoint { get; }
        public string HomeEntryId { get; }

        public bool ContainsSuppression(string context) =>
            _suppressionContexts.Contains(context);
    }

    /// <summary>Queue에서 Presentation으로 전달되는 완전히 확정된 한 문장이다.</summary>
    public sealed class GuideMessage
    {
        internal GuideMessage(
            string cueId,
            string variationId,
            string eventId,
            GuideModeScope mode,
            GuidePriority priority,
            GuidePresentationType presentationType,
            GuideExpression expression,
            string expressionAssetKey,
            GuideTone tone,
            string text,
            GuideCta? cta,
            bool requiresAcknowledgement,
            float autoDismissSeconds,
            string dedupeKey)
        {
            CueId = cueId;
            VariationId = variationId;
            EventId = eventId;
            Mode = mode;
            Priority = priority;
            PresentationType = presentationType;
            Expression = expression;
            ExpressionAssetKey = expressionAssetKey;
            Tone = tone;
            Text = text;
            Cta = cta;
            RequiresAcknowledgement = requiresAcknowledgement;
            AutoDismissSeconds = autoDismissSeconds;
            DedupeKey = dedupeKey;
        }

        public string CueId { get; }
        public string VariationId { get; }
        public string EventId { get; }
        public GuideModeScope Mode { get; }
        public GuidePriority Priority { get; }
        public GuidePresentationType PresentationType { get; }
        public GuideExpression Expression { get; }
        public string ExpressionAssetKey { get; }
        public GuideTone Tone { get; }
        public string Text { get; }
        public GuideCta? Cta { get; }
        public bool RequiresAcknowledgement { get; }
        public float AutoDismissSeconds { get; }
        public string DedupeKey { get; }
    }

    /// <summary>Fact가 Queue에 들어갔는지와 거부 이유를 호출자에게 돌려준다.</summary>
    public readonly struct GuideEnqueueResult
    {
        public GuideEnqueueResult(int enqueuedCount, int duplicateCount, string error)
        {
            EnqueuedCount = enqueuedCount;
            DuplicateCount = duplicateCount;
            Error = error ?? string.Empty;
        }

        public int EnqueuedCount { get; }
        public int DuplicateCount { get; }
        public string Error { get; }
        public bool IsAccepted => Error.Length == 0;
    }

    [Serializable]
    public sealed class GuideRepeatStateData
    {
        public GuideRepeatStateEntryData[] entries = Array.Empty<GuideRepeatStateEntryData>();
    }

    [Serializable]
    public sealed class GuideRepeatStateEntryData
    {
        public string dedupeKey;
        public int displays;
    }

    /// <summary>세이브 가능한 dedupeKey별 실제 표시 횟수를 관리한다.</summary>
    public sealed class GuideRepeatState
    {
        private readonly Dictionary<string, int> _displayCounts = new(StringComparer.Ordinal);

        public bool CanDisplay(string dedupeKey, int maximumDisplays) =>
            !_displayCounts.TryGetValue(dedupeKey, out int displays) || displays < maximumDisplays;

        public void RecordDisplay(string dedupeKey)
        {
            _displayCounts.TryGetValue(dedupeKey, out int displays);
            _displayCounts[dedupeKey] = displays + 1;
        }

        public GuideRepeatStateData Capture()
        {
            var keys = new List<string>(_displayCounts.Keys);
            keys.Sort(StringComparer.Ordinal);
            var data = new GuideRepeatStateData
            {
                entries = new GuideRepeatStateEntryData[keys.Count]
            };
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                data.entries[index] = new GuideRepeatStateEntryData
                {
                    dedupeKey = key,
                    displays = _displayCounts[key]
                };
            }
            return data;
        }

        public void Restore(GuideRepeatStateData data)
        {
            _displayCounts.Clear();
            GuideRepeatStateEntryData[] entries = data?.entries ?? Array.Empty<GuideRepeatStateEntryData>();
            for (int index = 0; index < entries.Length; index++)
            {
                GuideRepeatStateEntryData entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.dedupeKey) || entry.displays < 1)
                    throw new ArgumentException("Guide repeat state 항목이 잘못되었습니다.", nameof(data));
                if (!_displayCounts.TryAdd(entry.dedupeKey, entry.displays))
                    throw new ArgumentException("Guide repeat state에 중복 key가 있습니다.", nameof(data));
            }
        }
    }

    /// <summary>Fact 소비, WeightedHash, 억제, 우선순위와 반복 제한을 한 경로에서 실행한다.</summary>
    public sealed class FrontManagerGuide
    {
        private readonly GuideDatasetCatalog _catalog;
        private readonly GuideRepeatState _repeatState;
        private readonly List<QueuedGuideMessage> _queue = new();
        private readonly HashSet<string> _pendingDedupeKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _homeDialogueCounts = new(StringComparer.Ordinal);
        private long _nextQueueSequence;

        public FrontManagerGuide(GuideDatasetCatalog catalog, GuideRepeatState repeatState = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _repeatState = repeatState ?? new GuideRepeatState();
        }

        public int QueuedCount => _queue.Count;
        public GuideRepeatState RepeatState => _repeatState;

        public void ClearPending()
        {
            _queue.Clear();
            _pendingDedupeKeys.Clear();
            _homeDialogueCounts.Clear();
        }

        public GuideEnqueueResult Enqueue(GuideFact fact)
        {
            if (fact == null)
                throw new ArgumentNullException(nameof(fact));
            if (!_catalog.TryGetFact(fact.FactType, out GuideFactContract contract))
                return new GuideEnqueueResult(0, 0, $"등록되지 않은 factType입니다: {fact.FactType}");
            if (!contract.Supports(fact.Mode))
                return new GuideEnqueueResult(0, 0, $"{fact.FactType}은 {fact.Mode} 모드 Fact가 아닙니다.");
            for (int index = 0; index < contract.RequiredPayload.Count; index++)
            {
                string key = contract.RequiredPayload[index];
                if (!fact.Payload.ContainsKey(key))
                    return new GuideEnqueueResult(0, 0, $"{fact.FactType} payload '{key}'가 없습니다.");
            }

            int enqueued = 0;
            int duplicates = 0;
            IReadOnlyList<GuideCueDefinition> cues = _catalog.GetCues(fact.FactType);
            for (int index = 0; index < cues.Count; index++)
            {
                GuideCueDefinition cue = cues[index];
                if (cue.ModeScope != GuideModeScope.Common && cue.ModeScope != fact.Mode)
                    continue;
                if (!TryCreateMessage(fact, cue, out GuideMessage message, out string error))
                    return new GuideEnqueueResult(enqueued, duplicates, error);
                if (_pendingDedupeKeys.Contains(message.DedupeKey) ||
                    !_repeatState.CanDisplay(message.DedupeKey, cue.RepeatPolicy.MaximumDisplays))
                {
                    duplicates++;
                    continue;
                }

                _queue.Add(new QueuedGuideMessage(message, cue, _nextQueueSequence++));
                _pendingDedupeKeys.Add(message.DedupeKey);
                enqueued++;
            }
            return new GuideEnqueueResult(enqueued, duplicates, string.Empty);
        }

        public bool TryDequeue(GuideDisplayContext context, out GuideMessage message)
        {
            int selectedIndex = -1;
            for (int index = 0; index < _queue.Count; index++)
            {
                QueuedGuideMessage candidate = _queue[index];
                if (!CanDisplay(candidate.Cue, context))
                    continue;
                if (selectedIndex < 0 || ComesBefore(candidate, _queue[selectedIndex]))
                    selectedIndex = index;
            }
            if (selectedIndex < 0)
            {
                message = null;
                return false;
            }

            QueuedGuideMessage selected = _queue[selectedIndex];
            _queue.RemoveAt(selectedIndex);
            _pendingDedupeKeys.Remove(selected.Message.DedupeKey);
            _repeatState.RecordDisplay(selected.Message.DedupeKey);
            if (selected.Cue.PresentationType == GuidePresentationType.FullDialogue &&
                !string.IsNullOrWhiteSpace(context.HomeEntryId))
            {
                _homeDialogueCounts.TryGetValue(context.HomeEntryId, out int count);
                _homeDialogueCounts[context.HomeEntryId] = count + 1;
            }
            message = selected.Message;
            return true;
        }

        private bool TryCreateMessage(
            GuideFact fact,
            GuideCueDefinition cue,
            out GuideMessage message,
            out string error)
        {
            string ResolveFactToken(string token) => fact.TryResolveToken(token, out string value) ? value : null;
            if (!GuideTemplate.TryRender(
                    cue.RepeatPolicy.DedupeKeyTemplate,
                    ResolveFactToken,
                    out string dedupeKey,
                    out string missingDedupeToken))
            {
                message = null;
                error = $"{cue.CueId} dedupe context '{missingDedupeToken}'가 없습니다.";
                return false;
            }

            if (!TryBuildVariationSeed(fact, cue.CueId, out string seed, out error))
            {
                message = null;
                return false;
            }
            GuideVariationDefinition variation = GuideWeightedHash.Select(cue.Variations, seed);
            if (!GuideTemplate.TryRender(
                    variation.Text,
                    ResolveFactToken,
                    out string text,
                    out string missingPayload))
            {
                message = null;
                error = $"{cue.CueId} text payload '{missingPayload}'가 없습니다.";
                return false;
            }

            message = new GuideMessage(
                cue.CueId,
                variation.VariationId,
                fact.Identity.EventId,
                fact.Mode,
                cue.Priority,
                cue.PresentationType,
                cue.Expression,
                cue.ExpressionAssetKey,
                variation.Tone,
                text,
                cue.Cta,
                cue.RequiresAcknowledgement,
                cue.AutoDismissSeconds,
                dedupeKey);
            error = string.Empty;
            return true;
        }

        private bool TryBuildVariationSeed(
            GuideFact fact,
            string cueId,
            out string seed,
            out string error)
        {
            string template = fact.Identity.HasPrimarySeed
                ? _catalog.SeedTemplate
                : _catalog.FallbackSeedTemplate;
            string Resolve(string token)
            {
                if (token == "cueId")
                    return cueId;
                return fact.Identity.Resolve(token);
            }
            if (!GuideTemplate.TryRender(template, Resolve, out seed, out string missing))
            {
                error = $"Variation seed context '{missing}'가 없습니다.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool CanDisplay(GuideCueDefinition cue, GuideDisplayContext context)
        {
            for (int index = 0; index < _catalog.DefaultSuppressionContexts.Count; index++)
                if (context.ContainsSuppression(_catalog.DefaultSuppressionContexts[index]))
                    return false;
            for (int index = 0; index < cue.SuppressionContexts.Count; index++)
                if (context.ContainsSuppression(cue.SuppressionContexts[index]))
                    return false;
            if (context.IsMatchInProgress && !context.IsSafePoint && cue.Priority != GuidePriority.Critical)
                return false;
            if (cue.PresentationType == GuidePresentationType.FullDialogue &&
                !string.IsNullOrWhiteSpace(context.HomeEntryId) &&
                _homeDialogueCounts.TryGetValue(context.HomeEntryId, out int count) &&
                count >= _catalog.HomeFullDialogueMaximum)
            {
                return false;
            }
            return true;
        }

        private static bool ComesBefore(QueuedGuideMessage left, QueuedGuideMessage right)
        {
            int priority = left.Message.Priority.CompareTo(right.Message.Priority);
            return priority < 0 || priority == 0 && left.Sequence < right.Sequence;
        }

        private readonly struct QueuedGuideMessage
        {
            public QueuedGuideMessage(GuideMessage message, GuideCueDefinition cue, long sequence)
            {
                Message = message;
                Cue = cue;
                Sequence = sequence;
            }

            public GuideMessage Message { get; }
            public GuideCueDefinition Cue { get; }
            public long Sequence { get; }
        }
    }

    /// <summary>프로세스·플랫폼과 무관한 FNV-1a 64-bit로 가중 Variation을 선택한다.</summary>
    public static class GuideWeightedHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static GuideVariationDefinition Select(
            IReadOnlyList<GuideVariationDefinition> variations,
            string seed)
        {
            if (variations == null || variations.Count == 0)
                throw new ArgumentException("Variation이 필요합니다.", nameof(variations));
            int totalWeight = 0;
            for (int index = 0; index < variations.Count; index++)
                totalWeight = checked(totalWeight + variations[index].Weight);

            ulong bucket = ComputeHash(seed ?? string.Empty) % (ulong)totalWeight;
            int cumulative = 0;
            for (int index = 0; index < variations.Count; index++)
            {
                cumulative += variations[index].Weight;
                if (bucket < (ulong)cumulative)
                    return variations[index];
            }
            return variations[variations.Count - 1];
        }

        public static ulong ComputeHash(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            ulong hash = OffsetBasis;
            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= Prime;
            }
            return hash;
        }
    }

    internal static class GuideCollectionExtensions
    {
        public static bool Contains(this IReadOnlyCollection<string> values, string expected)
        {
            foreach (string value in values)
                if (string.Equals(value, expected, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}

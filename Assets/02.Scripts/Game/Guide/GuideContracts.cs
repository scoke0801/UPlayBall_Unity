using System;
using System.Collections.Generic;

namespace Baseball.Game.Guide
{
    public enum GuideModeScope
    {
        Common,
        Owner,
        Career
    }

    public enum GuidePriority
    {
        Critical,
        High,
        Normal,
        Low,
        Flavor
    }

    public enum GuidePresentationType
    {
        FullDialogue,
        Briefing,
        ContextBubble,
        Toast,
        NotificationCard,
        ModalCelebration
    }

    public enum GuideExpression
    {
        Neutral,
        Welcome,
        Analysis,
        Concerned,
        Warning,
        Celebrate,
        Surprised,
        Calm
    }

    public enum GuideTone
    {
        Calm,
        Friendly,
        Analysis,
        Concerned,
        Warning,
        Celebrate,
        Welcome,
        Surprised
    }

    public enum GuideCooldownScope
    {
        Event,
        Revision,
        Day,
        Week,
        Season,
        Save
    }

    /// <summary>데이터 CTA와 Presentation 라우터 사이에서 컴파일 시점에 공유하는 명령 계약이다.</summary>
    public enum GuideCtaAction
    {
        OpenAllStarGame,
        OpenAwardRecord,
        OpenBullpen,
        OpenCardDetail,
        OpenCardSale,
        OpenCardTraining,
        OpenCareerAwards,
        OpenCareerCondition,
        OpenCareerHome,
        OpenCareerUsage,
        OpenClubRecords,
        OpenCollection,
        OpenContractOffer,
        OpenContractResult,
        OpenDailyBriefing,
        OpenDuplicateAction,
        OpenEconomy,
        OpenEnhancement,
        OpenFocusScout,
        OpenGuideSettings,
        OpenLeagueResult,
        OpenLineup,
        OpenMatchLog,
        OpenMatchSummary,
        OpenNews,
        OpenNewsArticle,
        OpenNewTeam,
        OpenNotificationCenter,
        OpenOpponentAnalysis,
        OpenPitchingRole,
        OpenPitchingStaff,
        OpenPitchingStats,
        OpenPlayerGameLog,
        OpenPostseasonAwards,
        OpenPostseasonSchedule,
        OpenRecentMatches,
        OpenRoster,
        OpenSchedule,
        OpenScout,
        OpenScoutFilters,
        OpenScoutOdds,
        OpenSeasonHistory,
        OpenSeasonReview,
        OpenStartingPitcher,
        OpenTacticCounters,
        OpenTacticLog,
        OpenTactics,
        OpenTeamColor,
        OpenTeamColorCoverage,
        OpenTodayLineup,
        OpenTradeInquiry,
        OpenTradeOffer,
        OpenTradeRosterImpact,
        StartMatch
    }

    /// <summary>한 Variation의 결정론적 선택 가중치와 표시 문장을 보관한다.</summary>
    public sealed class GuideVariationDefinition
    {
        public GuideVariationDefinition(string variationId, int weight, GuideTone tone, string text)
        {
            VariationId = variationId;
            Weight = weight;
            Tone = tone;
            Text = text;
        }

        public string VariationId { get; }
        public int Weight { get; }
        public GuideTone Tone { get; }
        public string Text { get; }
    }

    /// <summary>표시 횟수와 중복 키를 데이터에서 읽은 그대로 실행 정책으로 고정한다.</summary>
    public sealed class GuideRepeatPolicy
    {
        public GuideRepeatPolicy(string dedupeKeyTemplate, GuideCooldownScope cooldownScope, int maximumDisplays)
        {
            DedupeKeyTemplate = dedupeKeyTemplate;
            CooldownScope = cooldownScope;
            MaximumDisplays = maximumDisplays;
        }

        public string DedupeKeyTemplate { get; }
        public GuideCooldownScope CooldownScope { get; }
        public int MaximumDisplays { get; }
    }

    /// <summary>Guide 문장과 기존 화면 이동 명령을 잇는 선택형 CTA다.</summary>
    public readonly struct GuideCta
    {
        public GuideCta(GuideCtaAction action, string label)
        {
            Action = action;
            Label = label;
        }

        public GuideCtaAction Action { get; }
        public string Label { get; }
    }

    /// <summary>한 Fact가 어떤 화면 형태와 문장 후보로 표현되는지 정의한다.</summary>
    public sealed class GuideCueDefinition
    {
        private readonly string[] _requiredPayload;
        private readonly string[] _suppressionContexts;
        private readonly GuideVariationDefinition[] _variations;

        public GuideCueDefinition(
            string cueId,
            string factType,
            GuideModeScope modeScope,
            GuidePriority priority,
            GuidePresentationType presentationType,
            GuideExpression expression,
            string expressionAssetKey,
            string[] requiredPayload,
            GuideRepeatPolicy repeatPolicy,
            GuideCta? cta,
            bool requiresAcknowledgement,
            float autoDismissSeconds,
            string[] suppressionContexts,
            GuideVariationDefinition[] variations)
        {
            CueId = cueId;
            FactType = factType;
            ModeScope = modeScope;
            Priority = priority;
            PresentationType = presentationType;
            Expression = expression;
            ExpressionAssetKey = expressionAssetKey;
            _requiredPayload = requiredPayload ?? Array.Empty<string>();
            RepeatPolicy = repeatPolicy;
            Cta = cta;
            RequiresAcknowledgement = requiresAcknowledgement;
            AutoDismissSeconds = autoDismissSeconds;
            _suppressionContexts = suppressionContexts ?? Array.Empty<string>();
            _variations = variations ?? Array.Empty<GuideVariationDefinition>();
        }

        public string CueId { get; }
        public string FactType { get; }
        public GuideModeScope ModeScope { get; }
        public GuidePriority Priority { get; }
        public GuidePresentationType PresentationType { get; }
        public GuideExpression Expression { get; }
        public string ExpressionAssetKey { get; }
        public IReadOnlyList<string> RequiredPayload => _requiredPayload;
        public GuideRepeatPolicy RepeatPolicy { get; }
        public GuideCta? Cta { get; }
        public bool RequiresAcknowledgement { get; }
        public float AutoDismissSeconds { get; }
        public IReadOnlyList<string> SuppressionContexts => _suppressionContexts;
        public IReadOnlyList<GuideVariationDefinition> Variations => _variations;
    }

    /// <summary>Fact 이름별 payload와 허용 모드를 검증하는 데이터 인덱스 계약이다.</summary>
    public sealed class GuideFactContract
    {
        private readonly string[] _requiredPayload;
        private readonly GuideModeScope[] _modeScopes;

        public GuideFactContract(string factType, string[] requiredPayload, GuideModeScope[] modeScopes)
        {
            FactType = factType;
            _requiredPayload = requiredPayload ?? Array.Empty<string>();
            _modeScopes = modeScopes ?? Array.Empty<GuideModeScope>();
        }

        public string FactType { get; }
        public IReadOnlyList<string> RequiredPayload => _requiredPayload;
        public IReadOnlyList<GuideModeScope> ModeScopes => _modeScopes;

        public bool Supports(GuideModeScope mode)
        {
            for (int index = 0; index < _modeScopes.Length; index++)
                if (_modeScopes[index] == mode || _modeScopes[index] == GuideModeScope.Common)
                    return true;
            return false;
        }
    }

    /// <summary>검증된 Cue, Fact 인덱스와 전역 표시 정책을 런타임 조회 형태로 보관한다.</summary>
    public sealed class GuideDatasetCatalog
    {
        private readonly Dictionary<string, GuideFactContract> _facts;
        private readonly Dictionary<string, GuideCueDefinition[]> _cuesByFact;
        private readonly string[] _defaultSuppressionContexts;

        internal GuideDatasetCatalog(
            string datasetId,
            string version,
            string characterId,
            string seedTemplate,
            string fallbackSeedTemplate,
            int homeFullDialogueMaximum,
            string[] defaultSuppressionContexts,
            GuideFactContract[] facts,
            GuideCueDefinition[] cues)
        {
            DatasetId = datasetId;
            Version = version;
            CharacterId = characterId;
            SeedTemplate = seedTemplate;
            FallbackSeedTemplate = fallbackSeedTemplate;
            HomeFullDialogueMaximum = homeFullDialogueMaximum;
            _defaultSuppressionContexts = defaultSuppressionContexts ?? Array.Empty<string>();
            _facts = new Dictionary<string, GuideFactContract>(facts.Length, StringComparer.Ordinal);
            _cuesByFact = new Dictionary<string, GuideCueDefinition[]>(facts.Length, StringComparer.Ordinal);

            for (int index = 0; index < facts.Length; index++)
                _facts.Add(facts[index].FactType, facts[index]);

            var grouped = new Dictionary<string, List<GuideCueDefinition>>(StringComparer.Ordinal);
            for (int index = 0; index < cues.Length; index++)
            {
                GuideCueDefinition cue = cues[index];
                if (!grouped.TryGetValue(cue.FactType, out List<GuideCueDefinition> list))
                {
                    list = new List<GuideCueDefinition>();
                    grouped.Add(cue.FactType, list);
                }
                list.Add(cue);
            }

            foreach (KeyValuePair<string, List<GuideCueDefinition>> pair in grouped)
                _cuesByFact.Add(pair.Key, pair.Value.ToArray());
        }

        public string DatasetId { get; }
        public string Version { get; }
        public string CharacterId { get; }
        public string SeedTemplate { get; }
        public string FallbackSeedTemplate { get; }
        public int HomeFullDialogueMaximum { get; }
        public IReadOnlyList<string> DefaultSuppressionContexts => _defaultSuppressionContexts;

        public bool TryGetFact(string factType, out GuideFactContract contract) =>
            _facts.TryGetValue(factType, out contract);

        public IReadOnlyList<GuideCueDefinition> GetCues(string factType)
        {
            return _cuesByFact.TryGetValue(factType, out GuideCueDefinition[] cues)
                ? cues
                : Array.Empty<GuideCueDefinition>();
        }
    }
}

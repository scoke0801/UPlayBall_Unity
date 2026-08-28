using System;

namespace Baseball.Game.Career.News
{
    /// <summary>템플릿이 선택되기 위해 필요한 Fact 비교 방식이다.</summary>
    public enum NewsFactComparison
    {
        Exists,
        Equals,
        GreaterOrEqual,
        LessOrEqual
    }

    /// <summary>Fact 유무와 값으로 템플릿 변형의 적용 범위를 데이터화한다.</summary>
    public readonly struct NewsTemplateCondition
    {
        public NewsTemplateCondition(NewsFactKey key, NewsFactComparison comparison, double value = 0d)
        {
            Key = key;
            Comparison = comparison;
            Value = value;
        }

        public NewsFactKey Key { get; }
        public NewsFactComparison Comparison { get; }
        public double Value { get; }

        public bool Matches(NewsFactSet facts)
        {
            if (!facts.TryGet(Key, out NewsFact fact))
                return false;
            if (Comparison == NewsFactComparison.Exists)
                return true;
            double actual = fact.ValueType == NewsFactValueType.Decimal
                ? fact.DecimalValue
                : fact.IntegerValue;
            return Comparison switch
            {
                NewsFactComparison.Equals => Math.Abs(actual - Value) < 0.000001d,
                NewsFactComparison.GreaterOrEqual => actual >= Value,
                NewsFactComparison.LessOrEqual => actual <= Value,
                _ => false
            };
        }
    }

    /// <summary>확정 사실을 한국어 기사 문장으로 바꾸는 데이터 기반 템플릿 정의다.</summary>
    public sealed class NewsTemplateDefinition
    {
        public NewsTemplateDefinition(
            string templateId,
            NewsEventType eventType,
            NewsCategory category,
            NewsArticleLength length,
            NewsSourceType defaultSource,
            NewsTemplateCondition[] conditions,
            string[] headlineVariants,
            string[] leadVariants,
            string[] bodyVariants,
            string cooldownGroup,
            int cooldownCycles)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                throw new ArgumentException("TemplateId가 비어 있습니다.", nameof(templateId));
            if (headlineVariants == null || headlineVariants.Length == 0)
                throw new ArgumentException("제목 변형이 필요합니다.", nameof(headlineVariants));
            TemplateId = templateId;
            EventType = eventType;
            Category = category;
            Length = length;
            DefaultSource = defaultSource;
            Conditions = conditions ?? Array.Empty<NewsTemplateCondition>();
            HeadlineVariants = headlineVariants;
            LeadVariants = leadVariants ?? Array.Empty<string>();
            BodyVariants = bodyVariants ?? Array.Empty<string>();
            CooldownGroup = cooldownGroup ?? string.Empty;
            CooldownCycles = cooldownCycles;
        }

        public string TemplateId { get; }
        public NewsEventType EventType { get; }
        public NewsCategory Category { get; }
        public NewsArticleLength Length { get; }
        public NewsSourceType DefaultSource { get; }
        public NewsTemplateCondition[] Conditions { get; }
        public string[] HeadlineVariants { get; }
        public string[] LeadVariants { get; }
        public string[] BodyVariants { get; }
        public string CooldownGroup { get; }
        public int CooldownCycles { get; }

        public bool Matches(NewsEventType eventType, NewsFactSet facts)
        {
            if (EventType != eventType)
                return false;
            for (int index = 0; index < Conditions.Length; index++)
            {
                if (!Conditions[index].Matches(facts))
                    return false;
            }
            return true;
        }
    }

    /// <summary>뉴스 우선순위·트리거·문장 템플릿을 한 버전으로 묶는다.</summary>
    public sealed class CareerNewsConfiguration
    {
        public CareerNewsConfiguration(
            int generationVersion,
            NewsPriorityDefinition priority,
            NewsTriggerDefinition triggers,
            NewsTemplateDefinition[] templates)
        {
            GenerationVersion = generationVersion;
            Priority = priority ?? throw new ArgumentNullException(nameof(priority));
            Triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
            Templates = templates ?? throw new ArgumentNullException(nameof(templates));
        }

        public int GenerationVersion { get; }
        public NewsPriorityDefinition Priority { get; }
        public NewsTriggerDefinition Triggers { get; }
        public NewsTemplateDefinition[] Templates { get; }

        public static CareerNewsConfiguration CreateDefault()
        {
            return new CareerNewsConfiguration(
                generationVersion: 1,
                NewsPriorityDefinition.CreateDefault(),
                NewsTriggerDefinition.CreateDefault(),
                DefaultNewsTemplateLibrary.Create());
        }
    }
}

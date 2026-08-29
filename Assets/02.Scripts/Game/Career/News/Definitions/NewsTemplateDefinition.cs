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

    /// <summary>제목·리드·본문·인용문을 하나의 관점으로 묶어 문체가 섞이지 않게 한다.</summary>
    public sealed class NewsArticleVariant
    {
        public NewsArticleVariant(
            string variantId,
            NewsSourceType voice,
            NewsTone tone,
            NewsTemplateCondition[] requiredConditions,
            NewsTemplateCondition[] forbiddenConditions,
            string headline,
            string lead,
            string[] bodyBlocks,
            string quote = "",
            int weight = 1,
            string cooldownGroup = "",
            int cooldownCycles = 5)
        {
            if (string.IsNullOrWhiteSpace(variantId))
                throw new ArgumentException("VariantId가 비어 있습니다.", nameof(variantId));
            if (string.IsNullOrWhiteSpace(headline))
                throw new ArgumentException("기사 제목이 비어 있습니다.", nameof(headline));
            if (weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight));
            VariantId = variantId;
            Voice = voice;
            Tone = tone;
            RequiredConditions = requiredConditions ?? Array.Empty<NewsTemplateCondition>();
            ForbiddenConditions = forbiddenConditions ?? Array.Empty<NewsTemplateCondition>();
            Headline = headline;
            Lead = lead ?? string.Empty;
            Body = bodyBlocks == null || bodyBlocks.Length == 0
                ? string.Empty
                : string.Join("\n\n", bodyBlocks);
            Quote = quote ?? string.Empty;
            Weight = weight;
            CooldownGroup = cooldownGroup ?? string.Empty;
            CooldownCycles = cooldownCycles;
        }

        public string VariantId { get; }
        public NewsSourceType Voice { get; }
        public NewsTone Tone { get; }
        public NewsTemplateCondition[] RequiredConditions { get; }
        public NewsTemplateCondition[] ForbiddenConditions { get; }
        public string Headline { get; }
        public string Lead { get; }
        public string Body { get; }
        public string Quote { get; }
        public int Weight { get; }
        public string CooldownGroup { get; }
        public int CooldownCycles { get; }

        public bool Matches(NewsFactSet facts)
        {
            for (int index = 0; index < RequiredConditions.Length; index++)
            {
                if (!RequiredConditions[index].Matches(facts))
                    return false;
            }
            for (int index = 0; index < ForbiddenConditions.Length; index++)
            {
                if (ForbiddenConditions[index].Matches(facts))
                    return false;
            }
            return true;
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
            : this(
                templateId,
                eventType,
                category,
                length,
                conditions,
                BuildVariants(defaultSource, headlineVariants, leadVariants, bodyVariants),
                cooldownGroup,
                cooldownCycles)
        {
        }

        public NewsTemplateDefinition(
            string templateId,
            NewsEventType eventType,
            NewsCategory category,
            NewsArticleLength length,
            NewsTemplateCondition[] conditions,
            NewsArticleVariant[] variants,
            string cooldownGroup,
            int cooldownCycles)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                throw new ArgumentException("TemplateId가 비어 있습니다.", nameof(templateId));
            if (variants == null || variants.Length == 0)
                throw new ArgumentException("기사 묶음이 필요합니다.", nameof(variants));
            TemplateId = templateId;
            EventType = eventType;
            Category = category;
            Length = length;
            Conditions = conditions ?? Array.Empty<NewsTemplateCondition>();
            Variants = variants;
            DefaultSource = variants[0].Voice;
            HeadlineVariants = Extract(variants, field: 0);
            LeadVariants = Extract(variants, field: 1);
            BodyVariants = Extract(variants, field: 2);
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
        public NewsArticleVariant[] Variants { get; }
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

        private static NewsArticleVariant[] BuildVariants(
            NewsSourceType source,
            string[] headlines,
            string[] leads,
            string[] bodies)
        {
            if (headlines == null || headlines.Length == 0)
                throw new ArgumentException("제목 변형이 필요합니다.", nameof(headlines));
            leads ??= Array.Empty<string>();
            bodies ??= Array.Empty<string>();
            var result = new NewsArticleVariant[headlines.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new NewsArticleVariant(
                    $"legacy_{index}",
                    source,
                    NewsTone.Neutral,
                    Array.Empty<NewsTemplateCondition>(),
                    Array.Empty<NewsTemplateCondition>(),
                    headlines[index],
                    leads.Length == 0 ? string.Empty : leads[index % leads.Length],
                    bodies.Length == 0 ? Array.Empty<string>() : new[] { bodies[index % bodies.Length] },
                    cooldownGroup: $"headline_{index}");
            }
            return result;
        }

        private static string[] Extract(NewsArticleVariant[] variants, int field)
        {
            var result = new string[variants.Length];
            for (int index = 0; index < variants.Length; index++)
            {
                result[index] = field switch
                {
                    0 => variants[index].Headline,
                    1 => variants[index].Lead,
                    _ => variants[index].Body
                };
            }
            return result;
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
                generationVersion: 2,
                NewsPriorityDefinition.CreateDefault(),
                NewsTriggerDefinition.CreateDefault(),
                DefaultNewsTemplateLibrary.Create());
        }
    }
}

using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>필요 Fact를 모두 가진 템플릿만 골라 고정 시드로 문장 변형을 선택한다.</summary>
    internal sealed class NewsTemplateService
    {
        private readonly CareerNewsConfiguration _configuration;

        public NewsTemplateService(CareerNewsConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public NewsTemplateDefinition SelectTemplate(NewsCandidate candidate)
        {
            NewsTemplateDefinition best = null;
            for (int index = 0; index < _configuration.Templates.Length; index++)
            {
                NewsTemplateDefinition template = _configuration.Templates[index];
                if (!template.Matches(candidate.DominantEventType, candidate.Facts))
                    continue;
                if (best == null || template.Conditions.Length > best.Conditions.Length ||
                    template.Conditions.Length == best.Conditions.Length &&
                    string.CompareOrdinal(template.TemplateId, best.TemplateId) < 0)
                {
                    best = template;
                }
            }
            return best;
        }

        public NewsArticleState CreateArticle(
            CareerNewsState state,
            NewsCandidate candidate,
            NewsTemplateDefinition template,
            NewsPublicationContext context)
        {
            int variant = SelectVariant(state, candidate, template, context);
            NewsArticleVariant articleVariant = template.Variants[variant];
            string headline = NewsTemplateTextFormatter.Format(
                articleVariant.Headline,
                candidate.Facts);
            string lead = NewsTemplateTextFormatter.Format(
                articleVariant.Lead,
                candidate.Facts);
            string body = NewsTemplateTextFormatter.Format(
                articleVariant.Body,
                candidate.Facts);
            if (!string.IsNullOrEmpty(articleVariant.Quote))
            {
                string quote = NewsTemplateTextFormatter.Format(articleVariant.Quote, candidate.Facts);
                body = string.IsNullOrEmpty(body) ? quote : $"{body}\n\n{quote}";
            }
            state.RecordTopicPublished(
                GetVariantCooldownGroup(template, articleVariant),
                context.Cycle.ToOrdinal());
            return new NewsArticleState(
                state.AllocateArticleId(context.Cycle.SeasonId),
                context.PublishedAt,
                template.Category,
                candidate.Importance,
                template.Length,
                articleVariant.Voice,
                articleVariant.Tone,
                template.TemplateId,
                variant,
                articleVariant.VariantId,
                _configuration.GenerationVersion,
                headline,
                lead,
                body,
                candidate.PrimarySubject,
                candidate.RelatedSubjects,
                candidate.Facts,
                candidate.SourceEventIds,
                candidate.StorylineId,
                candidate.IsCareerArchive);
        }

        private static int SelectVariant(
            CareerNewsState state,
            NewsCandidate candidate,
            NewsTemplateDefinition template,
            NewsPublicationContext context)
        {
            var eligible = new List<int>(template.Variants.Length);
            CollectEligibleVariants(state, candidate, template, context, enforceCooldown: true, eligible);
            if (eligible.Count == 0)
                CollectEligibleVariants(state, candidate, template, context, enforceCooldown: false, eligible);
            if (eligible.Count == 0)
                throw new InvalidOperationException($"템플릿 {template.TemplateId}에 적용 가능한 기사 묶음이 없습니다.");

            ulong hash = 14695981039346656037UL;
            AddToHash(ref hash, context.Cycle.SeasonId.ToString());
            AddToHash(ref hash, template.TemplateId);
            for (int index = 0; index < candidate.SourceEventIds.Count; index++)
                AddToHash(ref hash, candidate.SourceEventIds[index]);
            int totalWeight = 0;
            for (int index = 0; index < eligible.Count; index++)
                totalWeight += template.Variants[eligible[index]].Weight;
            int ticket = (int)(hash % (ulong)totalWeight);
            for (int index = 0; index < eligible.Count; index++)
            {
                int variantIndex = eligible[index];
                int weight = template.Variants[variantIndex].Weight;
                if (ticket < weight)
                    return variantIndex;
                ticket -= weight;
            }
            return eligible[eligible.Count - 1];
        }

        private static void CollectEligibleVariants(
            CareerNewsState state,
            NewsCandidate candidate,
            NewsTemplateDefinition template,
            NewsPublicationContext context,
            bool enforceCooldown,
            List<int> result)
        {
            for (int index = 0; index < template.Variants.Length; index++)
            {
                NewsArticleVariant variant = template.Variants[index];
                if (!variant.Matches(candidate.Facts))
                    continue;
                if (enforceCooldown && state.IsTopicOnCooldown(
                        GetVariantCooldownGroup(template, variant),
                        context.Cycle.ToOrdinal(),
                        variant.CooldownCycles))
                {
                    continue;
                }
                result.Add(index);
            }
        }

        private static string GetVariantCooldownGroup(
            NewsTemplateDefinition template,
            NewsArticleVariant variant)
        {
            return string.IsNullOrEmpty(variant.CooldownGroup)
                ? string.Empty
                : $"{template.TemplateId}:{variant.CooldownGroup}";
        }

        private static void AddToHash(ref ulong hash, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 1099511628211UL;
            }
            hash ^= 0xFF;
            hash *= 1099511628211UL;
        }
    }
}

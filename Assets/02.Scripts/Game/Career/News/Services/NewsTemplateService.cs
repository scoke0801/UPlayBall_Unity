using System;

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
            int variant = SelectVariant(candidate, template, context.Cycle.SeasonId);
            string headline = NewsTemplateTextFormatter.Format(
                SelectText(template.HeadlineVariants, variant),
                candidate.Facts);
            string lead = NewsTemplateTextFormatter.Format(
                SelectText(template.LeadVariants, variant),
                candidate.Facts);
            string body = NewsTemplateTextFormatter.Format(
                SelectText(template.BodyVariants, variant),
                candidate.Facts);
            return new NewsArticleState(
                state.AllocateArticleId(context.Cycle.SeasonId),
                context.PublishedAt,
                template.Category,
                candidate.Importance,
                template.Length,
                template.DefaultSource,
                template.TemplateId,
                variant,
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
            NewsCandidate candidate,
            NewsTemplateDefinition template,
            int seasonId)
        {
            ulong hash = 14695981039346656037UL;
            AddToHash(ref hash, seasonId.ToString());
            AddToHash(ref hash, template.TemplateId);
            for (int index = 0; index < candidate.SourceEventIds.Count; index++)
                AddToHash(ref hash, candidate.SourceEventIds[index]);
            return (int)(hash % (ulong)template.HeadlineVariants.Length);
        }

        private static string SelectText(string[] values, int variant)
        {
            return values.Length == 0 ? string.Empty : values[variant % values.Length];
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

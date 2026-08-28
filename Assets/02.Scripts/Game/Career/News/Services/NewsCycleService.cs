using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>주기 종료 시 사건 병합·가치 평가·템플릿 생성을 거쳐 최대 네 기사를 발행한다.</summary>
    public sealed class NewsCycleService
    {
        private readonly CareerNewsState _state;
        private readonly CareerNewsConfiguration _configuration;
        private readonly NewsMergeService _mergeService = new();
        private readonly NewsPriorityService _priorityService;
        private readonly NewsTemplateService _templateService;
        private readonly NewsStorylineService _storylineService = new();

        public NewsCycleService(CareerNewsState state, CareerNewsConfiguration configuration)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _priorityService = new NewsPriorityService(configuration.Priority);
            _templateService = new NewsTemplateService(configuration);
        }

        public IReadOnlyList<NewsArticleState> Publish(NewsPublicationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            List<NewsEvent> eligible = CollectEligibleEvents(context);
            if (eligible.Count == 0)
                return Array.Empty<NewsArticleState>();

            for (int index = 0; index < eligible.Count; index++)
                _storylineService.Apply(_state, eligible[index]);

            List<NewsCandidate> candidates = _mergeService.Merge(eligible);
            for (int index = 0; index < candidates.Count; index++)
                _priorityService.Evaluate(candidates[index], context, _state);
            candidates.Sort(CompareCandidates);

            List<NewsArticleState> published = SelectAndCreate(candidates, context);
            for (int index = 0; index < eligible.Count; index++)
                _state.MarkProcessed(eligible[index]);
            return published;
        }

        private List<NewsEvent> CollectEligibleEvents(NewsPublicationContext context)
        {
            var result = new List<NewsEvent>();
            for (int index = 0; index < _state.PendingEvents.Count; index++)
            {
                NewsEvent newsEvent = _state.PendingEvents[index];
                if (!newsEvent.OccurredAt.Cycle.Equals(context.Cycle) ||
                    !context.IsReleased(newsEvent.ReleaseGate))
                {
                    continue;
                }
                result.Add(newsEvent);
            }
            result.Sort((left, right) => string.CompareOrdinal(left.EventId, right.EventId));
            return result;
        }

        private List<NewsArticleState> SelectAndCreate(
            List<NewsCandidate> candidates,
            NewsPublicationContext context)
        {
            var result = new List<NewsArticleState>(_configuration.Priority.MaximumArticlesPerCycle);
            var selected = new HashSet<NewsCandidate>();
            int standardCount = 0;
            int briefingCount = 0;

            NewsCandidate top = FindTopCandidate(candidates, context);
            if (top != null && TryCreate(top, context, result))
            {
                selected.Add(top);
                if (top.IsLeagueBriefing) briefingCount++;
                else standardCount++;
            }

            for (int index = 0; index < candidates.Count &&
                                result.Count < _configuration.Priority.MaximumArticlesPerCycle; index++)
            {
                NewsCandidate candidate = candidates[index];
                if (selected.Contains(candidate) || candidate.IsLeagueBriefing ||
                    (int)candidate.Importance < (int)NewsImportance.B ||
                    standardCount >= _configuration.Priority.MaximumStandardArticles + 1)
                {
                    continue;
                }
                if (CountPlayerArticles(result, candidate, context.MyPlayerId) >=
                    _configuration.Priority.MaximumArticlesPerPlayer)
                {
                    continue;
                }
                if (!TryCreate(candidate, context, result))
                    continue;
                selected.Add(candidate);
                standardCount++;
            }

            for (int index = 0; index < candidates.Count &&
                                result.Count < _configuration.Priority.MaximumArticlesPerCycle &&
                                briefingCount < _configuration.Priority.MaximumBriefings; index++)
            {
                NewsCandidate candidate = candidates[index];
                if (selected.Contains(candidate) || !candidate.IsLeagueBriefing ||
                    (int)candidate.Importance < (int)NewsImportance.C)
                {
                    continue;
                }
                if (!TryCreate(candidate, context, result))
                    continue;
                selected.Add(candidate);
                briefingCount++;
            }
            return result;
        }

        private NewsCandidate FindTopCandidate(
            IReadOnlyList<NewsCandidate> candidates,
            NewsPublicationContext context)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                NewsCandidate candidate = candidates[index];
                if ((int)candidate.Importance < (int)NewsImportance.C)
                    continue;
                NewsTemplateDefinition template = _templateService.SelectTemplate(candidate);
                if (template == null || IsTemplateOnCooldown(template, context))
                    continue;
                return candidate;
            }
            return null;
        }

        private bool TryCreate(
            NewsCandidate candidate,
            NewsPublicationContext context,
            List<NewsArticleState> result)
        {
            NewsTemplateDefinition template = _templateService.SelectTemplate(candidate);
            if (template == null || IsTemplateOnCooldown(template, context))
                return false;
            NewsArticleState article = _templateService.CreateArticle(_state, candidate, template, context);
            _state.AddArticle(article);
            _state.RecordTopicPublished(template.CooldownGroup, context.Cycle.ToOrdinal());
            _state.RecordTopicPublished(candidate.CooldownGroup, context.Cycle.ToOrdinal());
            result.Add(article);
            return true;
        }

        private bool IsTemplateOnCooldown(
            NewsTemplateDefinition template,
            NewsPublicationContext context)
        {
            return _state.IsTopicOnCooldown(
                template.CooldownGroup,
                context.Cycle.ToOrdinal(),
                template.CooldownCycles);
        }

        private static int CountPlayerArticles(
            IReadOnlyList<NewsArticleState> articles,
            NewsCandidate candidate,
            int playerId)
        {
            if (!candidate.IncludesSubject(NewsSubjectType.Player, playerId.ToString()))
                return 0;
            int count = 0;
            string id = playerId.ToString();
            for (int index = 0; index < articles.Count; index++)
            {
                NewsArticleState article = articles[index];
                if (article.PrimarySubject.Type == NewsSubjectType.Player &&
                    article.PrimarySubject.SubjectId == id)
                {
                    count++;
                    continue;
                }
                for (int relatedIndex = 0; relatedIndex < article.RelatedSubjects.Count; relatedIndex++)
                {
                    NewsSubject subject = article.RelatedSubjects[relatedIndex];
                    if (subject.Type == NewsSubjectType.Player && subject.SubjectId == id)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private static int CompareCandidates(NewsCandidate left, NewsCandidate right)
        {
            int score = right.Score.CompareTo(left.Score);
            if (score != 0) return score;
            return string.CompareOrdinal(left.SourceEventIds[0], right.SourceEventIds[0]);
        }
    }
}

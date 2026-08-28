using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>기사 원본에서 최신·내 커리어·구단·리그·계약·수상·연표 필터를 만든다.</summary>
    public sealed class CareerNewsFeedService
    {
        public CareerNewsFeedView Build(
            CareerNewsState state,
            NewsFeedCategory category,
            int myPlayerId,
            int myTeamId,
            int maximumArticles)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (maximumArticles < 0) throw new ArgumentOutOfRangeException(nameof(maximumArticles));

            var views = new List<NewsArticleView>(maximumArticles);
            var matches = new List<NewsArticleState>();
            int unreadCount = 0;
            if (category == NewsFeedCategory.CareerTimeline)
            {
                for (int index = 0; index < state.CareerArchive.Count; index++)
                {
                    NewsArticleState article = state.CareerArchive[index].Article;
                    if (!article.IsRead) unreadCount++;
                    matches.Add(article);
                }
                matches.Sort(CompareLatest);
                AddViews(matches, views, maximumArticles);
                return new CareerNewsFeedView(category, views.ToArray(), unreadCount);
            }

            for (int index = 0; index < state.CurrentSeasonArticles.Count; index++)
            {
                NewsArticleState article = state.CurrentSeasonArticles[index];
                if (!Matches(article, category, myPlayerId, myTeamId))
                    continue;
                if (!article.IsRead) unreadCount++;
                matches.Add(article);
            }
            matches.Sort(CompareLatest);
            AddViews(matches, views, maximumArticles);
            return new CareerNewsFeedView(category, views.ToArray(), unreadCount);
        }

        private static void AddViews(
            IReadOnlyList<NewsArticleState> articles,
            List<NewsArticleView> views,
            int maximumArticles)
        {
            int count = articles.Count < maximumArticles ? articles.Count : maximumArticles;
            for (int index = 0; index < count; index++)
                views.Add(new NewsArticleView(articles[index]));
        }

        private static int CompareLatest(NewsArticleState left, NewsArticleState right)
        {
            int cycle = right.PublishedAt.Cycle.ToOrdinal().CompareTo(left.PublishedAt.Cycle.ToOrdinal());
            if (cycle != 0) return cycle;
            int date = right.PublishedAt.CompareTo(left.PublishedAt);
            if (date != 0) return date;
            int importance = ((int)right.Importance).CompareTo((int)left.Importance);
            if (importance != 0) return importance;
            return string.CompareOrdinal(left.ArticleId, right.ArticleId);
        }

        private static bool Matches(
            NewsArticleState article,
            NewsFeedCategory category,
            int myPlayerId,
            int myTeamId)
        {
            return category switch
            {
                NewsFeedCategory.Latest => true,
                NewsFeedCategory.MyCareer => IncludesSubject(
                    article,
                    NewsSubjectType.Player,
                    myPlayerId.ToString()),
                NewsFeedCategory.Club => IncludesSubject(
                    article,
                    NewsSubjectType.Team,
                    myTeamId.ToString()),
                NewsFeedCategory.League => article.Category is
                    NewsCategory.League or NewsCategory.Game or NewsCategory.Postseason,
                NewsFeedCategory.TransferContract =>
                    article.Category == NewsCategory.TransferContract,
                NewsFeedCategory.RecordsAwards =>
                    article.Category == NewsCategory.RecordsAwards,
                _ => false
            };
        }

        private static bool IncludesSubject(
            NewsArticleState article,
            NewsSubjectType type,
            string id)
        {
            if (article.PrimarySubject.Type == type && article.PrimarySubject.SubjectId == id)
                return true;
            for (int index = 0; index < article.RelatedSubjects.Count; index++)
            {
                if (article.RelatedSubjects[index].Type == type &&
                    article.RelatedSubjects[index].SubjectId == id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

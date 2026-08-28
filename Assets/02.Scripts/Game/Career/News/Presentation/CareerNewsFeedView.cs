using System;

namespace Baseball.Game.Career.News
{
    /// <summary>Presentation이 뉴스 상태를 수정하지 않고 소비하는 기사 카드 값이다.</summary>
    public readonly struct NewsArticleView
    {
        public NewsArticleView(NewsArticleState article)
        {
            ArticleId = article.ArticleId;
            PublishedAt = article.PublishedAt.CalendarDate;
            Category = article.Category;
            Importance = article.Importance;
            SourceType = article.SourceType;
            Headline = article.Headline;
            Lead = article.Lead;
            Body = article.Body;
            IsRead = article.IsRead;
            IsCareerArchive = article.IsCareerArchive;
        }

        public string ArticleId { get; }
        public DateTime PublishedAt { get; }
        public NewsCategory Category { get; }
        public NewsImportance Importance { get; }
        public NewsSourceType SourceType { get; }
        public string Headline { get; }
        public string Lead { get; }
        public string Body { get; }
        public bool IsRead { get; }
        public bool IsCareerArchive { get; }
    }

    /// <summary>필터와 읽지 않은 수를 포함한 뉴스 화면의 읽기 전용 모델이다.</summary>
    public sealed class CareerNewsFeedView
    {
        public CareerNewsFeedView(
            NewsFeedCategory category,
            NewsArticleView[] articles,
            int unreadCount)
        {
            Category = category;
            Articles = articles ?? Array.Empty<NewsArticleView>();
            UnreadCount = unreadCount;
        }

        public NewsFeedCategory Category { get; }
        public NewsArticleView[] Articles { get; }
        public int UnreadCount { get; }
        public NewsArticleView? TopNews => Articles.Length > 0 ? Articles[0] : null;
    }
}

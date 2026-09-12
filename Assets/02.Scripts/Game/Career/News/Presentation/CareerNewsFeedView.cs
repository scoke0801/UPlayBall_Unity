using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>Presentation이 뉴스 상태를 수정하지 않고 소비하는 기사 카드 값이다.</summary>
    public readonly struct NewsArticleView
    {
        public NewsArticleView(NewsArticleState article)
            : this(article, null)
        {
        }

        public NewsArticleView(
            NewsArticleState article,
            Func<NewsSubject, string> subjectNameResolver)
        {
            ArticleId = article.ArticleId;
            PublishedAt = article.PublishedAt.CalendarDate;
            Category = article.Category;
            Importance = article.Importance;
            SourceType = article.SourceType;
            Headline = ResolveIdentityNames(article.Headline, article, subjectNameResolver);
            Lead = ResolveIdentityNames(article.Lead, article, subjectNameResolver);
            Body = ResolveIdentityNames(article.Body, article, subjectNameResolver);
            IsRead = article.IsRead;
            IsCareerArchive = article.IsCareerArchive;
            Illustration = NewsIllustrationResolver.Resolve(article.SourceEventIds);
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
        public NewsIllustrationKind Illustration { get; }

        private static string ResolveIdentityNames(
            string text,
            NewsArticleState article,
            Func<NewsSubject, string> subjectNameResolver)
        {
            if (string.IsNullOrEmpty(text) || subjectNameResolver == null)
                return text ?? string.Empty;

            var replacements = new List<NameReplacement>(article.RelatedSubjects.Count + 1);
            AddReplacement(replacements, article.PrimarySubject, subjectNameResolver);
            for (int index = 0; index < article.RelatedSubjects.Count; index++)
                AddReplacement(replacements, article.RelatedSubjects[index], subjectNameResolver);
            replacements.Sort(NameReplacement.Compare);
            for (int index = 0; index < replacements.Count; index++)
                text = ReplaceNameAndPostposition(text, replacements[index]);
            return text;
        }

        private static void AddReplacement(
            List<NameReplacement> replacements,
            NewsSubject subject,
            Func<NewsSubject, string> subjectNameResolver)
        {
            string source = subject.DisplayName;
            string target = subjectNameResolver(subject);
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target) || source == target)
                return;
            for (int index = 0; index < replacements.Count; index++)
                if (replacements[index].Source == source) return;
            replacements.Add(new NameReplacement(source, target));
        }

        private static string ReplaceNameAndPostposition(string text, NameReplacement replacement)
        {
            return KoreanPostpositionFormatter.ReplaceNoun(
                text,
                replacement.Source,
                replacement.Target);
        }

        private readonly struct NameReplacement
        {
            public NameReplacement(string source, string target)
            {
                Source = source;
                Target = target;
            }

            public string Source { get; }
            public string Target { get; }

            public static int Compare(NameReplacement left, NameReplacement right)
            {
                int length = right.Source.Length.CompareTo(left.Source.Length);
                return length != 0 ? length : string.CompareOrdinal(left.Source, right.Source);
            }
        }
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

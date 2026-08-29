using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>생성 시점의 제목과 본문까지 고정해 템플릿 변경 뒤에도 세이브 결과를 보존한다.</summary>
    public sealed class NewsArticleState
    {
        private readonly List<NewsSubject> _relatedSubjects;
        private readonly List<string> _sourceEventIds;

        public NewsArticleState(
            string articleId,
            CareerDate publishedAt,
            NewsCategory category,
            NewsImportance importance,
            NewsArticleLength length,
            NewsSourceType sourceType,
            NewsTone tone,
            string templateId,
            int templateVariantIndex,
            string templateVariantId,
            int generationVersion,
            string headline,
            string lead,
            string body,
            NewsSubject primarySubject,
            IReadOnlyList<NewsSubject> relatedSubjects,
            NewsFactSet factSet,
            IReadOnlyList<string> sourceEventIds,
            string storylineId,
            bool isCareerArchive)
        {
            if (string.IsNullOrWhiteSpace(articleId))
                throw new ArgumentException("ArticleId가 비어 있습니다.", nameof(articleId));
            ArticleId = articleId;
            PublishedAt = publishedAt;
            Category = category;
            Importance = importance;
            Length = length;
            SourceType = sourceType;
            Tone = tone;
            TemplateId = templateId ?? string.Empty;
            TemplateVariantIndex = templateVariantIndex;
            TemplateVariantId = templateVariantId ?? string.Empty;
            GenerationVersion = generationVersion;
            Headline = headline ?? string.Empty;
            Lead = lead ?? string.Empty;
            Body = body ?? string.Empty;
            PrimarySubject = primarySubject;
            _relatedSubjects = Copy(relatedSubjects);
            FactSet = factSet?.Clone() ?? new NewsFactSet();
            _sourceEventIds = Copy(sourceEventIds);
            StorylineId = storylineId ?? string.Empty;
            IsCareerArchive = isCareerArchive;
        }

        public string ArticleId { get; }
        public CareerDate PublishedAt { get; }
        public NewsCategory Category { get; }
        public NewsImportance Importance { get; }
        public NewsArticleLength Length { get; }
        public NewsSourceType SourceType { get; }
        public NewsTone Tone { get; }
        public string TemplateId { get; }
        public int TemplateVariantIndex { get; }
        public string TemplateVariantId { get; }
        public int GenerationVersion { get; }
        public string Headline { get; }
        public string Lead { get; }
        public string Body { get; }
        public NewsSubject PrimarySubject { get; }
        public IReadOnlyList<NewsSubject> RelatedSubjects => _relatedSubjects;
        public NewsFactSet FactSet { get; }
        public IReadOnlyList<string> SourceEventIds => _sourceEventIds;
        public string StorylineId { get; }
        public bool IsRead { get; private set; }
        public bool IsCareerArchive { get; private set; }

        public void MarkRead() => IsRead = true;
        public void AddToCareerArchive() => IsCareerArchive = true;

        private static List<T> Copy<T>(IReadOnlyList<T> source)
        {
            var result = new List<T>(source?.Count ?? 0);
            if (source == null)
                return result;
            for (int index = 0; index < source.Count; index++)
                result.Add(source[index]);
            return result;
        }
    }

    /// <summary>커리어 연표에 영구 보존할 중요 기사 원문을 참조한다.</summary>
    public sealed class CareerNewsArchiveEntry
    {
        public CareerNewsArchiveEntry(NewsArticleState article)
        {
            Article = article ?? throw new ArgumentNullException(nameof(article));
        }

        public NewsArticleState Article { get; }
    }
}

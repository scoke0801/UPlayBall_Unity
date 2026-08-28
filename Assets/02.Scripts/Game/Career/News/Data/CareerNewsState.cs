using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>기사·대기 이벤트·중복 방지·스토리라인을 함께 저장하는 커리어 뉴스 루트다.</summary>
    public sealed class CareerNewsState
    {
        private const int CurrentSeasonArticleCapacity = 250;

        private readonly List<NewsArticleState> _currentSeasonArticles = new();
        private readonly List<CareerNewsArchiveEntry> _careerArchive = new();
        private readonly List<NewsStorylineState> _activeStorylines = new();
        private readonly List<NewsEvent> _pendingEvents = new();
        private readonly HashSet<string> _processedEventIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _queuedEventIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _topicCooldownOrdinals = new(StringComparer.Ordinal);

        public CareerNewsState(int saveVersion)
        {
            SaveVersion = saveVersion;
            NextArticleSequence = 1L;
        }

        public int SaveVersion { get; }
        public long NextArticleSequence { get; private set; }
        public IReadOnlyList<NewsArticleState> CurrentSeasonArticles => _currentSeasonArticles;
        public IReadOnlyList<CareerNewsArchiveEntry> CareerArchive => _careerArchive;
        public IReadOnlyList<NewsStorylineState> ActiveStorylines => _activeStorylines;
        public IReadOnlyList<NewsEvent> PendingEvents => _pendingEvents;
        public IReadOnlyCollection<string> ProcessedEventIds => _processedEventIds;

        public bool Enqueue(NewsEvent newsEvent)
        {
            if (newsEvent == null)
                throw new ArgumentNullException(nameof(newsEvent));
            if (_processedEventIds.Contains(newsEvent.EventId) || !_queuedEventIds.Add(newsEvent.EventId))
                return false;
            _pendingEvents.Add(newsEvent);
            return true;
        }

        internal string AllocateArticleId(int seasonId)
        {
            return $"news_{seasonId}_{NextArticleSequence++}";
        }

        internal void AddArticle(NewsArticleState article)
        {
            _currentSeasonArticles.Add(article ?? throw new ArgumentNullException(nameof(article)));
            if (article.IsCareerArchive)
                _careerArchive.Add(new CareerNewsArchiveEntry(article));
            TrimCurrentSeasonArticles();
        }

        internal void MarkProcessed(NewsEvent newsEvent)
        {
            _processedEventIds.Add(newsEvent.EventId);
            _queuedEventIds.Remove(newsEvent.EventId);
            _pendingEvents.Remove(newsEvent);
        }

        internal bool IsTopicOnCooldown(string group, int currentOrdinal, int cooldownCycles)
        {
            if (string.IsNullOrEmpty(group) || cooldownCycles <= 0)
                return false;
            return _topicCooldownOrdinals.TryGetValue(group, out int previous) &&
                   currentOrdinal - previous < cooldownCycles;
        }

        internal void RecordTopicPublished(string group, int ordinal)
        {
            if (!string.IsNullOrEmpty(group))
                _topicCooldownOrdinals[group] = ordinal;
        }

        internal void AddStoryline(NewsStorylineState storyline)
        {
            _activeStorylines.Add(storyline ?? throw new ArgumentNullException(nameof(storyline)));
        }

        /// <summary>필터 선택과 무관하게 기사 한 건을 읽음으로 처리한다.</summary>
        public bool MarkArticleRead(string articleId)
        {
            NewsArticleState article = FindArticle(articleId);
            if (article == null)
                return false;
            article.MarkRead();
            return true;
        }

        /// <summary>지난 시즌의 일반 기사를 버리고 연표 기사만 영구 보관한다.</summary>
        public void CompactCompletedSeason(int completedSeasonId)
        {
            for (int index = _currentSeasonArticles.Count - 1; index >= 0; index--)
            {
                NewsArticleState article = _currentSeasonArticles[index];
                if (article.PublishedAt.Cycle.SeasonId == completedSeasonId && !article.IsCareerArchive)
                    _currentSeasonArticles.RemoveAt(index);
            }
        }

        private NewsArticleState FindArticle(string articleId)
        {
            for (int index = 0; index < _currentSeasonArticles.Count; index++)
            {
                if (_currentSeasonArticles[index].ArticleId == articleId)
                    return _currentSeasonArticles[index];
            }
            for (int index = 0; index < _careerArchive.Count; index++)
            {
                if (_careerArchive[index].Article.ArticleId == articleId)
                    return _careerArchive[index].Article;
            }
            return null;
        }

        private void TrimCurrentSeasonArticles()
        {
            while (_currentSeasonArticles.Count > CurrentSeasonArticleCapacity)
            {
                int removableIndex = -1;
                for (int index = 0; index < _currentSeasonArticles.Count; index++)
                {
                    if (_currentSeasonArticles[index].IsCareerArchive)
                        continue;
                    removableIndex = index;
                    break;
                }
                if (removableIndex < 0)
                    break;
                _currentSeasonArticles.RemoveAt(removableIndex);
            }
        }
    }
}

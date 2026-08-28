using Baseball.Game.Career.News;

namespace Baseball.Game.Career
{
    public sealed partial class CareerManager
    {
        /// <summary>뉴스 화면에 사용할 필터링된 기사 피드를 반환한다.</summary>
        public CareerNewsFeedView BuildNewsFeed(
            NewsFeedCategory category = NewsFeedCategory.Latest,
            int maximumArticles = 50)
        {
            if (CurrentCareer == null)
                return new CareerNewsFeedView(category, System.Array.Empty<NewsArticleView>(), 0);
            return new CareerNewsFeedService().Build(
                CurrentCareer.News,
                category,
                CurrentCareer.MyPlayer.PlayerId,
                CurrentCareer.MyPlayer.CurrentTeamId,
                maximumArticles);
        }

        /// <summary>기사 열람 상태만 바꾸며 계약·성장·경기 결과에는 영향을 주지 않는다.</summary>
        public bool MarkNewsArticleRead(string articleId)
        {
            if (CurrentCareer == null || !CurrentCareer.News.MarkArticleRead(articleId))
                return false;
            CareerChanged?.Invoke();
            return true;
        }
    }
}

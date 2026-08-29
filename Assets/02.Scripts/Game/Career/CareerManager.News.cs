using Baseball.Game.Career.News;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

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

        private void PublishSignedExtension(TeamState team, ContractOffer offer)
        {
            CareerDate occurredAt = BuildCurrentNewsDate();
            var newsService = new CareerNewsService(CurrentCareer);
            newsService.Collect(new ContractNewsEvaluator().EvaluateSignedContract(
                $"season_{occurredAt.Cycle.SeasonId}_player_{CurrentCareer.MyPlayer.PlayerId}_extension_signed",
                occurredAt,
                CurrentCareer.MyPlayer,
                team,
                offer.ContractYears,
                offer.AnnualSalary));
            newsService.PublishCycle(occurredAt, NewsReleaseGate.AfterContractConfirmation);
        }

        private void PublishDeclinedExtension(TeamState team)
        {
            CareerDate occurredAt = BuildCurrentNewsDate();
            var newsService = new CareerNewsService(CurrentCareer);
            newsService.Collect(new ContractNarrativeNewsEvaluator().EvaluateDeclined(
                CurrentCareer,
                occurredAt,
                team));
            newsService.PublishCycle(occurredAt, NewsReleaseGate.AfterContractConfirmation);
        }

        private CareerDate BuildCurrentNewsDate()
        {
            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            TeamSeasonRecordState record = season.GetTeamRecord(CurrentCareer.MyPlayer.CurrentTeamId);
            int cycleIndex = record?.GamesPlayed ?? 0;
            return new CareerDate(
                new NewsCycleKey(season.SeasonId, season.Phase, cycleIndex),
                CurrentCareer.World.Calendar.CurrentDate);
        }
    }
}

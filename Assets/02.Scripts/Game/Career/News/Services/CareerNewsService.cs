using System;
using System.Collections.Generic;
using Baseball.Game.Data;
using Baseball.Game.Career.Narrative;

namespace Baseball.Game.Career.News
{
    /// <summary>확정 이벤트 수집과 정규시즌 발행 주기를 외부 시스템에 제공하는 뉴스 진입점이다.</summary>
    public sealed class CareerNewsService
    {
        private readonly CareerState _career;
        private readonly CareerNewsConfiguration _configuration;
        private readonly NewsEventCollector _collector;
        private readonly NewsCycleService _cycleService;

        public CareerNewsService(CareerState career, CareerNewsConfiguration configuration = null)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _configuration = configuration ?? CareerNewsDefinition.LoadConfiguration();
            _collector = new NewsEventCollector(career.News);
            _cycleService = new NewsCycleService(career.News, _configuration);
        }

        public bool Collect(NewsEvent newsEvent) => _collector.Collect(newsEvent);

        /// <summary>당일 리그 경기·기록·순위가 모두 확정된 뒤 정규시즌 기사를 발행한다.</summary>
        public IReadOnlyList<NewsArticleState> PublishRegularSeasonRound(
            CareerGameAdvanceResult result,
            DateTime calendarDate,
            MatchNarrativeSnapshot narrative = null)
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            var cycle = new NewsCycleKey(season.SeasonId, SeasonPhase.RegularSeason, result.Round);
            var occurredAt = new CareerDate(cycle, calendarDate);
            var evaluator = new GameNewsEvaluator(_configuration.Triggers);
            IReadOnlyList<NewsEvent> events = evaluator.EvaluateRegularSeasonRound(
                _career,
                result,
                occurredAt,
                narrative);
            for (int index = 0; index < events.Count; index++)
                _collector.Collect(events[index]);
            return _cycleService.Publish(new NewsPublicationContext(
                occurredAt,
                _career.MyPlayer.PlayerId,
                _career.MyPlayer.CurrentTeamId,
                NewsReleaseGate.EndOfScheduleDate));
        }

        /// <summary>계약·부상·수상 등 외부 시스템이 모은 사건을 지정 공개 관문에서 발행한다.</summary>
        public IReadOnlyList<NewsArticleState> PublishCycle(
            CareerDate publishedAt,
            params NewsReleaseGate[] releasedGates)
        {
            return _cycleService.Publish(new NewsPublicationContext(
                publishedAt,
                _career.MyPlayer.PlayerId,
                _career.MyPlayer.CurrentTeamId,
                releasedGates));
        }
    }
}

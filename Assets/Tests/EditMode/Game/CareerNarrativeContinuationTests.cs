using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.Narrative;
using Baseball.Game.Career.News;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>구간 리포트·장기 스레드·중요 사건 반응이 경기 사이에 이어지는지 검증한다.</summary>
    public sealed class CareerNarrativeContinuationTests
    {
        [Test]
        public void NewsStoryline_상승세가시작되고무안타경기에서안정화된다()
        {
            var state = new CareerNewsState(10);
            var service = new NewsStorylineService();
            NewsEvent started = CreatePlayerEvent(NewsEventType.PlayerFormChanged, 4);
            started.FactSet.SetBoolean(NewsFactKey.FormHot, true);

            service.Apply(state, started);

            Assert.That(state.ActiveStorylines.Count, Is.EqualTo(1));
            Assert.That(state.ActiveStorylines[0].Type, Is.EqualTo(NewsStorylineType.RisingForm));
            NewsEvent cooled = CreatePlayerEvent(NewsEventType.PlayerFormChanged, 5);
            cooled.FactSet.SetBoolean(NewsFactKey.FormCooled, true);
            service.Apply(state, cooled);

            Assert.That(cooled.StorylineId, Is.EqualTo(started.StorylineId));
            Assert.That(state.ActiveStorylines[0].IsResolved, Is.True);
            Assert.That(state.ActiveStorylines[0].Resolution, Is.EqualTo(NewsStorylineResolution.Stabilized));
        }

        [Test]
        public void NewsStoryline_주전경쟁은신뢰회복기사에서해소된다()
        {
            var state = new CareerNewsState(10);
            var service = new NewsStorylineService();
            NewsEvent started = CreatePlayerEvent(NewsEventType.RoleCompetitionChanged, 8);
            started.FactSet.SetBoolean(NewsFactKey.RoleCompetitionStarted, true);
            service.Apply(state, started);

            NewsEvent resolved = CreatePlayerEvent(NewsEventType.RoleCompetitionChanged, 15);
            resolved.FactSet.SetBoolean(NewsFactKey.RoleCompetitionResolved, true);
            service.Apply(state, resolved);

            Assert.That(state.ActiveStorylines.Count, Is.EqualTo(1));
            Assert.That(resolved.StorylineId, Is.EqualTo(started.StorylineId));
            Assert.That(state.ActiveStorylines[0].Resolution, Is.EqualTo(NewsStorylineResolution.Stabilized));
        }

        [Test]
        public void NewsStoryline_트레이드루머가확정이적으로끝난다()
        {
            var state = new CareerNewsState(10);
            var service = new NewsStorylineService();
            NewsEvent rumor = CreatePlayerEvent(NewsEventType.TradeRumorReported, 40);
            service.Apply(state, rumor);

            NewsEvent traded = CreatePlayerEvent(NewsEventType.PlayerTraded, 46);
            service.Apply(state, traded);

            Assert.That(state.ActiveStorylines.Count, Is.EqualTo(1));
            Assert.That(state.ActiveStorylines[0].Type, Is.EqualTo(NewsStorylineType.TradeRumor));
            Assert.That(traded.StorylineId, Is.EqualTo(rumor.StorylineId));
            Assert.That(state.ActiveStorylines[0].Resolution, Is.EqualTo(NewsStorylineResolution.Transferred));
        }

        [Test]
        public void NewsStoryline_기록도전은같은종류와목표기록만해소한다()
        {
            var state = new CareerNewsState(10);
            var service = new NewsStorylineService();
            NewsEvent approaching = CreatePlayerEvent(NewsEventType.CareerMilestoneApproaching, 30);
            approaching.FactSet.SetText(NewsFactKey.MilestoneName, "통산 안타");
            approaching.FactSet.SetInteger(NewsFactKey.MilestoneTarget, 100);
            service.Apply(state, approaching);

            NewsEvent unrelated = CreatePlayerEvent(NewsEventType.CareerMilestoneReached, 31);
            unrelated.FactSet.SetText(NewsFactKey.CareerMilestone, "프로 첫 홈런");
            service.Apply(state, unrelated);
            Assert.That(state.ActiveStorylines[0].IsResolved, Is.False);

            NewsEvent reached = CreatePlayerEvent(NewsEventType.CareerMilestoneReached, 32);
            reached.FactSet.SetText(NewsFactKey.MilestoneName, "통산 안타");
            reached.FactSet.SetInteger(NewsFactKey.MilestoneTarget, 100);
            service.Apply(state, reached);

            Assert.That(reached.StorylineId, Is.EqualTo(approaching.StorylineId));
            Assert.That(state.ActiveStorylines[0].Resolution, Is.EqualTo(NewsStorylineResolution.Succeeded));
        }

        [Test]
        public void CareerReaction_계약질문답변효과는한번만적용된다()
        {
            CareerState career = CreateStartedCareer(44001UL);
            var service = new CareerReactionService(career);
            int trustBefore = career.MyPlayer.ManagerEvaluation;

            Assert.That(service.TryCreateContractOffer(1, 12, 120, "울산 가디언즈"), Is.True);
            Assert.That(career.Narrative.PendingReaction.Trigger, Is.EqualTo(CareerReactionTrigger.ContractOffer));

            service.Resolve(2);

            Assert.That(career.MyPlayer.ManagerEvaluation, Is.EqualTo(trustBefore + 1));
            Assert.That(career.Narrative.TeamChemistry, Is.EqualTo(53));
            Assert.That(career.Narrative.PendingReaction, Is.Null);
            Assert.Throws<InvalidOperationException>(() => service.Resolve(2));
        }

        [Test]
        public void CareerSeason_일곱경기뒤주간리포트를발행한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(44002UL, configuration);
            var service = new CareerSeasonService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());

            for (int index = 0; index < 7; index++)
                service.AdvanceNextRound();

            NewsArticleState report = FindArticle(career.News, "report.weekly");
            Assert.That(report, Is.Not.Null);
            Assert.That(report.FactSet.GetInteger(NewsFactKey.ReportGames), Is.EqualTo(7));
            Assert.That(report.FactSet.GetText(NewsFactKey.ReportTrend), Is.Not.Empty);
            Assert.That(report.SourceType, Is.EqualTo(NewsSourceType.RegionalSports).Or.EqualTo(NewsSourceType.ClubNews));
        }

        [Test]
        public void CareerSeason_스무경기뒤월간리포트를발행한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(44003UL, configuration);
            var service = new CareerSeasonService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());

            for (int index = 0; index < 20; index++)
                service.AdvanceNextRound();

            NewsArticleState report = FindArticle(career.News, "report.monthly");
            Assert.That(report, Is.Not.Null);
            Assert.That(report.FactSet.GetInteger(NewsFactKey.ReportGames), Is.EqualTo(20));
            Assert.That(report.FactSet.GetInteger(NewsFactKey.ReportAtBats), Is.GreaterThanOrEqualTo(0));
            Assert.That(report.SourceType, Is.EqualTo(NewsSourceType.LeagueSportsMedia).Or.EqualTo(NewsSourceType.NationalSports));
        }

        private static NewsEvent CreatePlayerEvent(NewsEventType type, int round)
        {
            var date = new CareerDate(
                new NewsCycleKey(1, SeasonPhase.RegularSeason, round),
                new DateTime(2028, 4, 1).AddDays(round));
            var newsEvent = new NewsEvent(
                $"event_{type}_{round}",
                type,
                date,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(42, "임민석"),
                "player_42_story",
                30);
            newsEvent.AddRelatedSubject(NewsSubject.Team(5, "울산 가디언즈"));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, "임민석");
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, "울산 가디언즈");
            return newsEvent;
        }

        private static NewsArticleState FindArticle(CareerNewsState state, string templateId)
        {
            for (int index = 0; index < state.CurrentSeasonArticles.Count; index++)
            {
                if (state.CurrentSeasonArticles[index].TemplateId == templateId)
                    return state.CurrentSeasonArticles[index];
            }
            return null;
        }

        private static CareerState CreateStartedCareer(ulong seed, NewGameConfiguration configuration = null)
        {
            configuration ??= NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("서사 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}

using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.News
{
    /// <summary>뉴스 중복 방지·결정론·병합·공개 관문·경기 흐름 통합을 검증한다.</summary>
    public sealed class CareerNewsServiceTests
    {
        [Test]
        public void Collect_같은EventId는한번만대기열에들어간다()
        {
            var state = new CareerNewsState(1);
            var collector = new NewsEventCollector(state);
            CareerDate date = CreateDate();
            NewsEvent first = CreateGameEvent("duplicate", date, "game_1");
            NewsEvent second = CreateGameEvent("duplicate", date, "game_1");

            Assert.That(collector.Collect(first), Is.True);
            Assert.That(collector.Collect(second), Is.False);
            Assert.That(state.PendingEvents.Count, Is.EqualTo(1));

            IReadOnlyList<NewsArticleState> articles = Publish(state, date);

            Assert.That(articles.Count, Is.EqualTo(1));
            Assert.That(state.ProcessedEventIds, Does.Contain("duplicate"));
        }

        [Test]
        public void Publish_같은입력은같은템플릿변형과문장을만든다()
        {
            CareerDate date = CreateDate();
            CareerNewsState firstState = CreateMergedGameState(date);
            CareerNewsState secondState = CreateMergedGameState(date);

            NewsArticleState first = Publish(firstState, date)[0];
            NewsArticleState second = Publish(secondState, date)[0];

            Assert.That(second.TemplateId, Is.EqualTo(first.TemplateId));
            Assert.That(second.TemplateVariantIndex, Is.EqualTo(first.TemplateVariantIndex));
            Assert.That(second.Headline, Is.EqualTo(first.Headline));
            Assert.That(second.Lead, Is.EqualTo(first.Lead));
            Assert.That(second.Body, Is.EqualTo(first.Body));
        }

        [Test]
        public void Publish_같은경기승리활약연승을기사하나로병합한다()
        {
            CareerDate date = CreateDate();
            CareerNewsState state = CreateMergedGameState(date);

            IReadOnlyList<NewsArticleState> articles = Publish(state, date);

            Assert.That(articles.Count, Is.EqualTo(1));
            Assert.That(articles[0].SourceEventIds.Count, Is.EqualTo(3));
            Assert.That(articles[0].Headline, Does.Contain("김도윤"));
            Assert.That(articles[0].Headline, Does.Contain("5연승"));
            Assert.That(articles[0].FactSet.GetInteger(NewsFactKey.GameHits), Is.EqualTo(3));
        }

        [Test]
        public void Publish_공개관문이열리기전에는계약기사를누설하지않는다()
        {
            CareerDate date = CreateDate();
            var state = new CareerNewsState(1);
            var contract = new NewsEvent(
                "contract_42_2029",
                NewsEventType.ContractSigned,
                date,
                NewsReleaseGate.AfterContractConfirmation,
                NewsSubject.Player(42, "김도윤"),
                "contract_42_2029",
                45)
            {
                CareerImpact = 25,
                IsCareerArchive = true
            };
            contract.AddRelatedSubject(NewsSubject.Team(5, "블루웨일스"));
            contract.FactSet.SetText(NewsFactKey.PlayerName, "김도윤");
            contract.FactSet.SetText(NewsFactKey.TeamName, "블루웨일스");
            contract.FactSet.SetInteger(NewsFactKey.ContractYears, 2);
            contract.FactSet.SetInteger(NewsFactKey.ContractSalary, 80_000_000);
            state.Enqueue(contract);
            var service = new NewsCycleService(state, CareerNewsConfiguration.CreateDefault());

            IReadOnlyList<NewsArticleState> hidden = service.Publish(new NewsPublicationContext(
                date,
                42,
                5,
                NewsReleaseGate.EndOfScheduleDate));

            Assert.That(hidden, Is.Empty);
            Assert.That(state.PendingEvents.Count, Is.EqualTo(1));
            IReadOnlyList<NewsArticleState> revealed = service.Publish(new NewsPublicationContext(
                date,
                42,
                5,
                NewsReleaseGate.AfterContractConfirmation));
            Assert.That(revealed.Count, Is.EqualTo(1));
            Assert.That(revealed[0].Headline, Does.Contain("2년 계약"));
            Assert.That(revealed[0].IsCareerArchive, Is.True);
        }

        [Test]
        public void Publish_출전하지않은경기는선수를억지로주인공으로만들지않는다()
        {
            CareerDate date = CreateDate();
            var state = new CareerNewsState(1);
            NewsEvent game = CreateGameEvent("game_only", date, "game_2");
            game.FactSet.SetBoolean(NewsFactKey.DidAppear, false);
            state.Enqueue(game);

            NewsArticleState article = Publish(state, date)[0];

            Assert.That(article.Category, Is.EqualTo(NewsCategory.Game));
            Assert.That(article.Headline, Does.Not.Contain("김도윤"));
        }

        [Test]
        public void AdvanceNextRound_당일전체경기확정후실제뉴스를발행한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 14521UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);

            CareerGameAdvanceResult result = seasonService.AdvanceNextRound();

            Assert.That(career.News.CurrentSeasonArticles.Count, Is.InRange(1, 4));
            NewsArticleState gameArticle = FindArticle(career.News, result.GameId);
            Assert.That(gameArticle, Is.Not.Null);
            Assert.That(gameArticle.FactSet.GetInteger(NewsFactKey.TeamRuns), Is.EqualTo(result.TeamRuns));
            Assert.That(gameArticle.FactSet.GetInteger(NewsFactKey.OpponentRuns), Is.EqualTo(result.OpponentRuns));
            Assert.That(
                career.League.CurrentSeason.Schedule.GetNextGameForTeam(career.MyPlayer.CurrentTeamId)?.Round,
                Is.Not.EqualTo(result.Round),
                "뉴스는 같은 날짜의 라운드 처리가 끝난 뒤에만 발행되어야 합니다.");
        }

        [Test]
        public void CompleteToSeasonReview_우승공개뒤포스트시즌과수상기사를발행한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 24521UL);

            new CareerSeasonAutoCompletionService(career, configuration.Balance)
                .CompleteToSeasonReview();

            bool hasChampionshipArticle = false;
            bool hasAwardArticle = false;
            for (int index = 0; index < career.News.CurrentSeasonArticles.Count; index++)
            {
                NewsArticleState article = career.News.CurrentSeasonArticles[index];
                hasChampionshipArticle |= article.TemplateId == "postseason.champion";
                hasAwardArticle |= article.TemplateId == "award.granted";
            }
            Assert.That(hasChampionshipArticle, Is.True);
            Assert.That(hasAwardArticle, Is.True);
        }

        [TestCase("김도윤", "은", "는", "김도윤은")]
        [TestCase("민혁", "은", "는", "민혁은")]
        [TestCase("서울", "으로", "로", "서울로")]
        [TestCase("마이클", "이", "가", "마이클이")]
        public void KoreanPostpositionFormatter_이름끝에맞는조사를붙인다(
            string noun,
            string consonantForm,
            string vowelForm,
            string expected)
        {
            Assert.That(
                KoreanPostpositionFormatter.Apply(noun, consonantForm, vowelForm),
                Is.EqualTo(expected));
        }

        private static CareerNewsState CreateMergedGameState(CareerDate date)
        {
            var state = new CareerNewsState(1);
            NewsEvent game = CreateGameEvent("game_completed", date, "game_1");
            var performance = new NewsEvent(
                "player_performance",
                NewsEventType.PlayerGamePerformance,
                date,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(42, "김도윤"),
                "game_1",
                30)
            {
                GameImpact = 10,
                Rarity = 15
            };
            performance.AddRelatedSubject(NewsSubject.Team(5, "블루웨일스"));
            performance.AddRelatedSubject(NewsSubject.Game(1));
            performance.FactSet.MergeFrom(game.FactSet);
            performance.FactSet.SetBoolean(NewsFactKey.HasNotablePerformance, true);

            var streak = new NewsEvent(
                "team_win_streak_5",
                NewsEventType.TeamStreakReached,
                date,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Team(5, "블루웨일스"),
                "game_1",
                18)
            {
                GameImpact = 8,
                Rarity = 10
            };
            streak.FactSet.MergeFrom(game.FactSet);
            state.Enqueue(game);
            state.Enqueue(performance);
            state.Enqueue(streak);
            return state;
        }

        private static NewsEvent CreateGameEvent(string id, CareerDate date, string mergeKey)
        {
            var game = new NewsEvent(
                id,
                NewsEventType.GameCompleted,
                date,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Team(5, "블루웨일스"),
                mergeKey,
                35)
            {
                GameImpact = 5
            };
            game.AddRelatedSubject(NewsSubject.Team(6, "레드호크스"));
            game.AddRelatedSubject(NewsSubject.Game(1));
            game.FactSet.SetText(NewsFactKey.PlayerName, "김도윤");
            game.FactSet.SetText(NewsFactKey.TeamName, "블루웨일스");
            game.FactSet.SetText(NewsFactKey.OpponentName, "레드호크스");
            game.FactSet.SetInteger(NewsFactKey.TeamRuns, 5);
            game.FactSet.SetInteger(NewsFactKey.OpponentRuns, 3);
            game.FactSet.SetBoolean(NewsFactKey.DidWin, true);
            game.FactSet.SetBoolean(NewsFactKey.DidLose, false);
            game.FactSet.SetBoolean(NewsFactKey.DidTie, false);
            game.FactSet.SetInteger(NewsFactKey.GameHits, 3);
            game.FactSet.SetInteger(NewsFactKey.TeamWinningStreak, 5);
            game.FactSet.SetText(NewsFactKey.GamePerformanceSummary, "3안타 1홈런 4타점");
            game.FactSet.SetText(NewsFactKey.GameStatLine, "4타수 3안타 1홈런 4타점");
            game.FactSet.SetText(NewsFactKey.TeamRecordSummary, "8승 3패");
            return game;
        }

        private static IReadOnlyList<NewsArticleState> Publish(CareerNewsState state, CareerDate date)
        {
            return new NewsCycleService(state, CareerNewsConfiguration.CreateDefault())
                .Publish(new NewsPublicationContext(
                    date,
                    42,
                    5,
                    NewsReleaseGate.EndOfScheduleDate));
        }

        private static CareerDate CreateDate()
        {
            return new CareerDate(
                new NewsCycleKey(1, SeasonPhase.RegularSeason, 5),
                new System.DateTime(2028, 4, 6));
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("뉴스 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new Baseball.Core.Players.BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static NewsArticleState FindArticle(CareerNewsState state, int gameId)
        {
            for (int index = 0; index < state.CurrentSeasonArticles.Count; index++)
            {
                NewsArticleState article = state.CurrentSeasonArticles[index];
                if (article.FactSet.GetInteger(NewsFactKey.GameId, -1) == gameId)
                    return article;
            }
            return null;
        }
    }
}

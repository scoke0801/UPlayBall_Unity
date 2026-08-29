using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 정규 시즌 종료가 4강 토너먼트로 이어지고, 우승 확정 뒤 성장 결산까지
    /// 흐름이 끊기지 않는지 검증한다.
    /// </summary>
    public sealed class CareerPostseasonServiceTests
    {
        [Test]
        public void 정규시즌을끝내면포스트시즌단계로넘어간다()
        {
            CareerState career = CreateCareerAtPostseason(1234UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;

            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Postseason));
            Assert.That(season.Postseason, Is.Not.Null);
            Assert.That(season.Postseason.SeedTeamIds.Count, Is.EqualTo(4));
            Assert.That(season.PostseasonPlayerStatistics, Is.Not.Null);
            Assert.That(season.PostseasonPlayerStatistics.TeamGames, Is.EqualTo(0));
            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.RegularSeasonIntro));
            Assert.That(season.ReviewSnapshot, Is.Not.Null);
            Assert.That(season.ReviewSnapshot.Standings.Count, Is.EqualTo(season.TeamRecords.Count));
            Assert.That(season.ReviewSnapshot.PlayerTeamRank, Is.InRange(1, season.TeamRecords.Count));
        }

        [Test]
        public void 시드는정규시즌승률상위4팀과일치한다()
        {
            CareerState career = CreateCareerAtPostseason(2345UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;

            var standings = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < standings.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                standings[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }
            int[] expected = PostseasonBracket.SelectSeeds(standings, 4);

            Assert.That(season.Postseason.SeedTeamIds, Is.EqualTo(expected));
        }

        [Test]
        public void 진출한내구단경기는준비와관전후에한경기만반영된다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateQualifiedCareerAtPostseason();
            SeasonState season = career.CurrentLeague.CurrentSeason;
            var service = new CareerPostseasonService(career, configuration.Balance);

            CareerMatchSession firstSession = service.PrepareNextPlayerGame();
            CareerMatchSession reopenedSession = service.PrepareNextPlayerGame();

            Assert.That(firstSession.CompetitionScope, Is.EqualTo(CompetitionScope.Postseason));
            Assert.That(firstSession.Input.RequiresWinner, Is.True);
            Assert.That(firstSession.ScheduledGame.IncludesTeam(career.MyPlayer.CurrentTeamId), Is.True);
            Assert.That(reopenedSession.ScheduledGame.GameId, Is.EqualTo(firstSession.ScheduledGame.GameId));
            Assert.That(reopenedSession.Input.RandomSeed, Is.EqualTo(firstSession.Input.RandomSeed));

            firstSession.Start(CareerMatchMode.ResultsOnly);
            CareerGameAdvanceResult result = service.CompletePreparedGame(firstSession);

            Assert.That(result.GameId, Is.EqualTo(firstSession.ScheduledGame.GameId));
            Assert.That(firstSession.ScheduledGame.IsCompleted, Is.True);
            Assert.That(season.PostseasonPlayerStatistics.TeamGames, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => service.CompletePreparedGame(firstSession));
        }

        [Test]
        public void AdvanceToChampion_두준결승과결승을거쳐우승구단을확정한다()
        {
            CareerState career = CreateCareerAtPostseason(3456UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);

            CareerPostseasonGameResult final = service.AdvanceToChampion();

            Assert.That(final.IsPostseasonCompleted, Is.True);
            Assert.That(season.Postseason.Series.Count, Is.EqualTo(3));
            Assert.That(season.Postseason.Series[0].Round, Is.EqualTo(PostseasonRound.Semifinal));
            Assert.That(season.Postseason.Series[1].Round, Is.EqualTo(PostseasonRound.Semifinal));
            Assert.That(
                season.Postseason.Series[2].Round,
                Is.EqualTo(PostseasonRound.ChampionshipSeries));
            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
            Assert.That(season.Postseason.ChampionTeamId, Is.EqualTo(final.ChampionTeamId));
            Assert.That(season.Postseason.SeedTeamIds, Does.Contain(final.ChampionTeamId));
            Assert.That(season.Review.Step, Is.EqualTo(SeasonReviewStep.PostseasonRecap));
            Assert.That(season.ReviewSnapshot.IsPostseasonFinalized, Is.True);
            Assert.That(season.ReviewSnapshot.ChampionTeamId, Is.EqualTo(final.ChampionTeamId));
            Assert.That(season.ReviewSnapshot.ChampionTeamName, Is.Not.Empty);

            bool hasDeferredChampionNews = false;
            bool hasDeferredAwardNews = false;
            for (int index = 0; index < career.News.PendingEvents.Count; index++)
            {
                NewsReleaseGate gate = career.News.PendingEvents[index].ReleaseGate;
                hasDeferredChampionNews |= gate == NewsReleaseGate.AfterPostseasonReveal;
                hasDeferredAwardNews |= gate == NewsReleaseGate.AfterAwardReveal;
            }
            Assert.That(hasDeferredChampionNews, Is.True);
            Assert.That(hasDeferredAwardNews, Is.True);
        }

        [Test]
        public void 준결승은1대4와2대3이고결승은두승자대결이다()
        {
            CareerState career = CreateCareerAtPostseason(4567UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);

            service.AdvanceToChampion();

            PostseasonState postseason = season.Postseason;
            PostseasonSeriesState semifinalA = postseason.Series[0];
            PostseasonSeriesState semifinalB = postseason.Series[1];
            PostseasonSeriesState championship = postseason.Series[2];

            Assert.That(semifinalA.HigherSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(0)));
            Assert.That(semifinalA.LowerSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(3)));
            Assert.That(semifinalB.HigherSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(1)));
            Assert.That(semifinalB.LowerSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(2)));
            Assert.That(new[] { semifinalA.WinnerTeamId, semifinalB.WinnerTeamId },
                Does.Contain(championship.HigherSeedTeamId));
            Assert.That(new[] { semifinalA.WinnerTeamId, semifinalB.WinnerTeamId },
                Does.Contain(championship.LowerSeedTeamId));
        }

        [Test]
        public void 포스트시즌기록은정규시즌기록과합산되지않는다()
        {
            CareerState career = CreateCareerAtPostseason(5678UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;
            PlayerSeasonStatisticsState regular = season.PlayerStatistics;
            int regularTeamGamesBefore = regular.TeamGames;
            int regularAtBatsBefore = regular.AtBats;
            int regularOutsBefore = regular.OutsRecorded;

            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);
            service.AdvanceToChampion();

            Assert.That(regular.TeamGames, Is.EqualTo(regularTeamGamesBefore));
            Assert.That(regular.AtBats, Is.EqualTo(regularAtBatsBefore));
            Assert.That(regular.OutsRecorded, Is.EqualTo(regularOutsBefore));

            // 내 구단이 진출했으면 포스트시즌 누적기에만 경기가 쌓이고, 탈락했으면 0으로 남는다.
            int playerPostseasonGames = CountPlayerPostseasonGames(season, career.MyPlayer.CurrentTeamId);
            Assert.That(season.PostseasonPlayerStatistics.TeamGames, Is.EqualTo(playerPostseasonGames));
        }

        [Test]
        public void 같은Seed는같은우승구단과같은시리즈결과를만든다()
        {
            CareerState first = CreateCareerAtPostseason(6789UL);
            CareerState second = CreateCareerAtPostseason(6789UL);
            var firstService = new CareerPostseasonService(first, NewGameConfiguration.CreateDefault().Balance);
            var secondService = new CareerPostseasonService(second, NewGameConfiguration.CreateDefault().Balance);

            firstService.AdvanceToChampion();
            secondService.AdvanceToChampion();

            PostseasonState firstPostseason = first.CurrentLeague.CurrentSeason.Postseason;
            PostseasonState secondPostseason = second.CurrentLeague.CurrentSeason.Postseason;

            Assert.That(secondPostseason.SeedTeamIds, Is.EqualTo(firstPostseason.SeedTeamIds));
            Assert.That(secondPostseason.ChampionTeamId, Is.EqualTo(firstPostseason.ChampionTeamId));
            Assert.That(secondPostseason.Series.Count, Is.EqualTo(firstPostseason.Series.Count));
            for (int index = 0; index < firstPostseason.Series.Count; index++)
            {
                PostseasonSeriesState firstSeries = firstPostseason.Series[index];
                PostseasonSeriesState secondSeries = secondPostseason.Series[index];
                Assert.That(secondSeries.WinnerTeamId, Is.EqualTo(firstSeries.WinnerTeamId));
                Assert.That(secondSeries.HigherSeedWins, Is.EqualTo(firstSeries.HigherSeedWins));
                Assert.That(secondSeries.LowerSeedWins, Is.EqualTo(firstSeries.LowerSeedWins));
                Assert.That(secondSeries.Games.Count, Is.EqualTo(firstSeries.Games.Count));
            }
        }

        [Test]
        public void 라운드별홈배정규칙을따른다()
        {
            CareerState career = CreateCareerAtPostseason(7890UL);
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);
            service.AdvanceToChampion();

            PostseasonState postseason = career.CurrentLeague.CurrentSeason.Postseason;
            for (int seriesIndex = 0; seriesIndex < postseason.Series.Count; seriesIndex++)
            {
                PostseasonSeriesState series = postseason.Series[seriesIndex];
                for (int gameIndex = 0; gameIndex < series.Games.Count; gameIndex++)
                {
                    ScheduledGameState game = series.Games[gameIndex];
                    int expectedHome = PostseasonBracket.IsHigherSeedHome(series.Round, game.Round)
                        ? series.HigherSeedTeamId
                        : series.LowerSeedTeamId;
                    Assert.That(game.HomeTeamId, Is.EqualTo(expectedHome));
                }
            }
        }

        [Test]
        public void 우승확정후성장결산으로이어진다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareerAtPostseason(8901UL);
            new CareerPostseasonService(career, configuration.Balance).AdvanceToChampion();

            var growthService = new CareerGrowthService(career, configuration.Balance);
            SeasonGrowthSettlementResult settlement = growthService.SettleSeasonAndBeginOffseason(
                CreateBatterUsage());

            Assert.That(career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(career.CurrentOffseason, Is.SameAs(settlement.Offseason));
        }

        [Test]
        public void 포스트시즌이아니면서비스를만들수없다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9012UL);

            Assert.Throws<InvalidOperationException>(
                () => new CareerPostseasonService(career, configuration.Balance));
        }

        [Test]
        public void 우승확정시리그전체통계와수상근거가함께고정된다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareerAtPostseason(9123UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;

            new CareerPostseasonService(career, configuration.Balance).AdvanceToChampion();

            Assert.That(season.LeagueStatistics.RegularSeason.IsFrozen, Is.True);
            Assert.That(season.LeagueStatistics.Postseason.IsFrozen, Is.True);
            Assert.That(season.LeagueStatistics.RegularSeason.Players.Count, Is.GreaterThan(80));
            Assert.That(season.Awards.Find(AwardCategory.RegularSeasonMvp), Is.Not.Null);
            Assert.That(season.Awards.Find(AwardCategory.RookieOfYear), Is.Not.Null);
            SeasonAwardResultState postseasonMvp = season.Awards.Find(AwardCategory.PostseasonMvp);
            Assert.That(postseasonMvp, Is.Not.Null);
            Assert.That(
                season.LeagueStatistics.Postseason.GetPlayer(postseasonMvp.WinnerPlayerId).TeamId,
                Is.EqualTo(season.Postseason.ChampionTeamId));
            Assert.That(postseasonMvp.TopCandidates.Count, Is.InRange(1, 3));
        }

        [Test]
        public void 시즌정산은화면재진입에도Money를한번만지급한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareerAtPostseason(9234UL);
            new CareerPostseasonService(career, configuration.Balance).AdvanceToChampion();
            var service = new SeasonSettlementService(career, configuration.Balance.SeasonSettlement);
            long moneyBefore = career.AvailableMoney;

            SeasonSettlementState first = service.ApplyOnce(performanceBonus: 300L);
            long moneyAfterFirst = career.AvailableMoney;
            SeasonSettlementState second = service.ApplyOnce(performanceBonus: 300L);

            Assert.That(first.IsApplied, Is.True);
            Assert.That(second, Is.SameAs(first));
            Assert.That(moneyAfterFirst, Is.GreaterThan(moneyBefore));
            Assert.That(career.AvailableMoney, Is.EqualTo(moneyAfterFirst));
            Assert.That(first.ContractEvaluationBonus, Is.InRange(0, 30));
        }

        [Test]
        public void 시즌리뷰는정규시즌공개뒤포스트시즌진행에서멈춘다()
        {
            var review = new SeasonReviewState();

            review.Advance(snapshot: null);
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.RegularSeasonResult));
            review.Advance(snapshot: null);
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.PostseasonEntry));
            review.Advance(snapshot: null);

            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.PostseasonInProgress));
            Assert.Throws<InvalidOperationException>(() => review.Advance(snapshot: null));
        }

        [Test]
        public void 포스트시즌완료뒤수상과요약과정산순서를지킨다()
        {
            var review = new SeasonReviewState();
            review.PreparePostseasonRecap();

            review.Advance(snapshot: null);
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.PostseasonResult));
            review.Advance(snapshot: null);
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.Awards));
            review.Advance(snapshot: null);
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.SeasonSummary));
            review.MarkIncomeSettlementReady();
            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.IncomeSettlement));
            review.Complete();

            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.Finished));
        }

        [Test]
        public void 건너뛰어도최종요약을생략하지않는다()
        {
            var review = new SeasonReviewState();
            review.PreparePostseasonRecap();

            review.SkipToSeasonSummary();

            Assert.That(review.Step, Is.EqualTo(SeasonReviewStep.SeasonSummary));
        }

        private static int CountPlayerPostseasonGames(SeasonState season, int playerTeamId)
        {
            int count = 0;
            for (int seriesIndex = 0; seriesIndex < season.Postseason.Series.Count; seriesIndex++)
            {
                PostseasonSeriesState series = season.Postseason.Series[seriesIndex];
                if (!series.IncludesTeam(playerTeamId))
                    continue;
                count += series.Games.Count;
            }
            return count;
        }

        /// <summary>
        /// 정규 시즌 전체를 진행해 포스트시즌 진입 직전 상태의 커리어를 만든다.
        /// </summary>
        private static CareerState CreateCareerAtPostseason(ulong seed)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, seed);
            var seasonService = new CareerSeasonService(career, configuration.Balance);

            int guard = configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam + 5;
            while (career.CurrentLeague.CurrentSeason.Phase == SeasonPhase.RegularSeason)
            {
                seasonService.AdvanceNextRound();
                if (--guard < 0)
                    Assert.Fail("정규 시즌이 예상 라운드 안에 끝나지 않았습니다.");
            }
            return career;
        }

        private static CareerState CreateQualifiedCareerAtPostseason()
        {
            for (ulong seed = 10_000UL; seed < 10_012UL; seed++)
            {
                CareerState career = CreateCareerAtPostseason(seed);
                if (career.CurrentLeague.CurrentSeason.Postseason.CanTeamPlayNextGame(
                        career.MyPlayer.CurrentTeamId))
                {
                    return career;
                }
            }

            Assert.Fail("테스트 Seed 범위에서 포스트시즌 진출 커리어를 만들지 못했습니다.");
            return null;
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("포스트시즌 테스트", "대한민국");
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

        private static SeasonUsageSummary CreateBatterUsage()
        {
            return new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.3d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.2d)
                });
        }
    }
}

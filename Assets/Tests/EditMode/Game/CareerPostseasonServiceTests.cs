using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 정규 시즌 종료가 계단식 포스트시즌으로 이어지고, 우승 확정 뒤 성장 결산까지
    /// 흐름이 끊기지 않는지 검증한다.
    /// </summary>
    public sealed class CareerPostseasonServiceTests
    {
        [Test]
        public void 정규시즌을끝내면포스트시즌단계로넘어간다()
        {
            CareerState career = CreateCareerAtPostseason(1234UL);
            SeasonState season = career.League.CurrentSeason;

            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Postseason));
            Assert.That(season.Postseason, Is.Not.Null);
            Assert.That(season.Postseason.SeedTeamIds.Count, Is.EqualTo(4));
            Assert.That(season.PostseasonPlayerStatistics, Is.Not.Null);
            Assert.That(season.PostseasonPlayerStatistics.TeamGames, Is.EqualTo(0));
        }

        [Test]
        public void 시드는정규시즌승률상위4팀과일치한다()
        {
            CareerState career = CreateCareerAtPostseason(2345UL);
            SeasonState season = career.League.CurrentSeason;

            var standings = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < standings.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                standings[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed);
            }
            int[] expected = PostseasonBracket.SelectSeeds(standings, 4);

            Assert.That(season.Postseason.SeedTeamIds, Is.EqualTo(expected));
        }

        [Test]
        public void AdvanceToChampion_계단식3개시리즈를거쳐우승구단을확정한다()
        {
            CareerState career = CreateCareerAtPostseason(3456UL);
            SeasonState season = career.League.CurrentSeason;
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);

            CareerPostseasonGameResult final = service.AdvanceToChampion();

            Assert.That(final.IsPostseasonCompleted, Is.True);
            Assert.That(season.Postseason.Series.Count, Is.EqualTo(3));
            Assert.That(season.Postseason.Series[0].Round, Is.EqualTo(PostseasonRound.WildCard));
            Assert.That(season.Postseason.Series[1].Round, Is.EqualTo(PostseasonRound.Playoff));
            Assert.That(
                season.Postseason.Series[2].Round,
                Is.EqualTo(PostseasonRound.ChampionshipSeries));
            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Completed));
            Assert.That(season.Postseason.ChampionTeamId, Is.EqualTo(final.ChampionTeamId));
            Assert.That(season.Postseason.SeedTeamIds, Does.Contain(final.ChampionTeamId));
        }

        [Test]
        public void 계단식대진의상대는직전시리즈승자다()
        {
            CareerState career = CreateCareerAtPostseason(4567UL);
            SeasonState season = career.League.CurrentSeason;
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);

            service.AdvanceToChampion();

            PostseasonState postseason = season.Postseason;
            PostseasonSeriesState wildCard = postseason.Series[0];
            PostseasonSeriesState playoff = postseason.Series[1];
            PostseasonSeriesState championship = postseason.Series[2];

            Assert.That(wildCard.HigherSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(2)));
            Assert.That(wildCard.LowerSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(3)));
            Assert.That(playoff.HigherSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(1)));
            Assert.That(playoff.LowerSeedTeamId, Is.EqualTo(wildCard.WinnerTeamId));
            Assert.That(championship.HigherSeedTeamId, Is.EqualTo(postseason.GetSeedTeamId(0)));
            Assert.That(championship.LowerSeedTeamId, Is.EqualTo(playoff.WinnerTeamId));
        }

        [Test]
        public void 포스트시즌기록은정규시즌기록과합산되지않는다()
        {
            CareerState career = CreateCareerAtPostseason(5678UL);
            SeasonState season = career.League.CurrentSeason;
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

            PostseasonState firstPostseason = first.League.CurrentSeason.Postseason;
            PostseasonState secondPostseason = second.League.CurrentSeason.Postseason;

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
        public void 상위시드가홀수경기를홈에서치른다()
        {
            CareerState career = CreateCareerAtPostseason(7890UL);
            var service = new CareerPostseasonService(career, NewGameConfiguration.CreateDefault().Balance);
            service.AdvanceToChampion();

            PostseasonState postseason = career.League.CurrentSeason.Postseason;
            for (int seriesIndex = 0; seriesIndex < postseason.Series.Count; seriesIndex++)
            {
                PostseasonSeriesState series = postseason.Series[seriesIndex];
                for (int gameIndex = 0; gameIndex < series.Games.Count; gameIndex++)
                {
                    ScheduledGameState game = series.Games[gameIndex];
                    int expectedHome = game.Round % 2 == 1
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

            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
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
            while (career.League.CurrentSeason.Phase == SeasonPhase.RegularSeason)
            {
                seasonService.AdvanceNextRound();
                if (--guard < 0)
                    Assert.Fail("정규 시즌이 예상 라운드 안에 끝나지 않았습니다.");
            }
            return career;
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

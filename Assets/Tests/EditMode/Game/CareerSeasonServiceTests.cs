using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 새 계약 상태가 정규 시즌 일정·경기·순위·개인 기록으로 이어지는지 검증한다.
    /// </summary>
    public sealed class CareerSeasonServiceTests
    {
        [Test]
        public void AdvanceNextRound_리그4경기와내선수경기로그를함께기록한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 4242UL);
            var service = new CareerSeasonService(career, configuration.Balance);
            service.EnsureNextGamePlan();
            int round = service.NextPlayerGame.Round;

            CareerGameAdvanceResult result = service.AdvanceNextRound();

            int completedInRound = 0;
            var games = career.League.CurrentSeason.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                if (games[index].Round == round && games[index].IsCompleted)
                    completedInRound++;
            }
            Assert.That(completedInRound, Is.EqualTo(4));
            Assert.That(career.League.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
            Assert.That(career.League.CurrentSeason.PlayerStatistics.RecentGames.Count, Is.EqualTo(1));
            Assert.That(result.Round, Is.EqualTo(round));
            for (int index = 0; index < career.League.CurrentSeason.TeamRecords.Count; index++)
                Assert.That(career.League.CurrentSeason.TeamRecords[index].GamesPlayed, Is.EqualTo(1));
        }

        [Test]
        public void AdvanceNextRound_같은커리어Seed는같은경기결과를만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState firstCareer = CreateStartedCareer(configuration, 7777UL);
            CareerState secondCareer = CreateStartedCareer(configuration, 7777UL);
            var first = new CareerSeasonService(firstCareer, configuration.Balance);
            var second = new CareerSeasonService(secondCareer, configuration.Balance);

            CareerGameAdvanceResult firstResult = first.AdvanceNextRound();
            CareerGameAdvanceResult secondResult = second.AdvanceNextRound();

            Assert.That(secondResult.Role, Is.EqualTo(firstResult.Role));
            Assert.That(secondResult.TeamRuns, Is.EqualTo(firstResult.TeamRuns));
            Assert.That(secondResult.OpponentRuns, Is.EqualTo(firstResult.OpponentRuns));
            Assert.That(secondResult.Hits, Is.EqualTo(firstResult.Hits));
            Assert.That(secondResult.EarnedRuns, Is.EqualTo(firstResult.EarnedRuns));
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("시즌 테스트", "대한민국");
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

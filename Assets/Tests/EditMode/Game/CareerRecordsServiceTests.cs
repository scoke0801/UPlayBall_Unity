using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>기록 화면 조회가 실제 시즌 원본의 순위·추이·하이라이트를 일관되게 투영하는지 검증한다.</summary>
    public sealed class CareerRecordsServiceTests
    {
        [Test]
        public void Build_타격기록은규정자격순으로정렬되고원본을바꾸지않는다()
        {
            CareerState career = CreateCareer(PlayerPosition.Shortstop, 7_701UL);
            AdvanceRounds(career, 12);
            var service = new CareerRecordsService();
            int playerCountBefore = career.League.CurrentSeason.LeagueStatistics.RegularSeason.Players.Count;

            CareerRecordsView first = service.Build(career, CareerRecordCategory.Batting);
            CareerRecordsView second = service.Build(career, CareerRecordCategory.Batting);

            Assert.That(first.Leaderboard, Has.Length.InRange(1, 10));
            Assert.That(first.LeaderboardColumns[0], Is.EqualTo(CareerRecordMetric.BattingAverage));
            Assert.That(first.Seasons, Has.Length.EqualTo(1));
            Assert.That(first.Trend, Has.Length.EqualTo(1));
            Assert.That(first.Highlights.Length, Is.InRange(1, 5));
            Assert.That(career.League.CurrentSeason.LeagueStatistics.RegularSeason.Players.Count,
                Is.EqualTo(playerCountBefore));
            Assert.That(second.Leaderboard.Length, Is.EqualTo(first.Leaderboard.Length));

            for (int index = 1; index < first.Leaderboard.Length; index++)
            {
                Assert.That(
                    first.Leaderboard[index - 1].Metrics[0].Value,
                    Is.GreaterThanOrEqualTo(first.Leaderboard[index].Metrics[0].Value));
            }
        }

        [Test]
        public void Build_투수는규정이닝과ERA오름차순을사용한다()
        {
            CareerState career = CreateCareer(PlayerPosition.StartingPitcher, 8_802UL);
            AdvanceRounds(career, 16);
            var service = new CareerRecordsService();

            CareerRecordsView view = service.Build(career, CareerRecordCategory.Pitching);

            Assert.That(view.PrimaryMetric, Is.EqualTo(CareerRecordMetric.EarnedRunAverage));
            Assert.That(view.Leaderboard, Has.Length.InRange(1, 10));
            for (int index = 1; index < view.Leaderboard.Length; index++)
            {
                Assert.That(
                    view.Leaderboard[index - 1].Metrics[0].Value,
                    Is.LessThanOrEqualTo(view.Leaderboard[index].Metrics[0].Value));
            }
            Assert.That(view.MyRecordMetrics[0].Metric, Is.EqualTo(CareerRecordMetric.EarnedRunAverage));
        }

        private static CareerState CreateCareer(PlayerPosition position, ulong seed)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("기록 테스트", "대한민국");
            flow.SelectPlayerType(isPitcher ? PlayerType.Pitcher : PlayerType.Batter);
            flow.SelectPosition(position);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            if (isPitcher)
                flow.SubmitPitcherAttributes(new PitcherAttributes(55, 55, 55, 50, 52, 45));
            else
                flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static void AdvanceRounds(CareerState career, int count)
        {
            var service = new CareerSeasonService(career, NewGameConfiguration.CreateDefault().Balance);
            for (int index = 0; index < count && service.NextPlayerGame != null; index++)
                service.AdvanceNextRound();
        }
    }
}

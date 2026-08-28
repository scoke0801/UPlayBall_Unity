using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>리그 화면 순위·규정 기록·일정 스냅샷이 실제 시즌 상태와 일치하는지 검증한다.</summary>
    public sealed class LeagueHubServiceTests
    {
        [Test]
        public void Build_진행중인시즌의순위리더보드구단비교일정을함께구성한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 82_801UL);
            AdvanceRounds(career, configuration, 12);

            LeagueHubView view = new LeagueHubService(career, configuration.Balance).Build();

            Assert.That(view.Standings.Count, Is.EqualTo(8));
            Assert.That(view.GamesPlayedPerTeam, Is.EqualTo(12));
            Assert.That(view.RegularSeasonGamesPerTeam, Is.EqualTo(80));
            Assert.That(view.NextRoundGames.Count, Is.EqualTo(4));
            Assert.That(view.RecentResults.Count, Is.EqualTo(5));
            Assert.That(view.TeamMetrics.Count, Is.EqualTo(4));

            int myTeamRows = 0;
            for (int index = 0; index < view.Standings.Count; index++)
            {
                LeagueStandingView standing = view.Standings[index];
                Assert.That(standing.Rank, Is.EqualTo(index + 1));
                Assert.That(standing.GamesPlayed, Is.EqualTo(12));
                if (standing.IsMyTeam)
                {
                    myTeamRows++;
                    Assert.That(standing.TeamId, Is.EqualTo(career.MyPlayer.CurrentTeamId));
                }
                if (index > 0)
                {
                    Assert.That(
                        standing.WinningPercentage,
                        Is.LessThanOrEqualTo(view.Standings[index - 1].WinningPercentage));
                }
            }
            Assert.That(myTeamRows, Is.EqualTo(1));

            LeagueBattingLeaderboardView batting =
                view.GetBattingLeaderboard(LeagueBattingCategory.BattingAverage);
            Assert.That(batting.Leaders.Count, Is.GreaterThan(0));
            for (int index = 0; index < batting.Leaders.Count; index++)
            {
                Assert.That(batting.Leaders[index].PlateAppearances, Is.GreaterThanOrEqualTo(36));
                if (index > 0)
                {
                    Assert.That(
                        batting.Leaders[index].BattingAverage,
                        Is.LessThanOrEqualTo(batting.Leaders[index - 1].BattingAverage));
                }
            }

            LeaguePitchingLeaderboardView pitching =
                view.GetPitchingLeaderboard(LeaguePitchingCategory.EarnedRunAverage);
            Assert.That(pitching.Leaders.Count, Is.GreaterThan(0));
            for (int index = 0; index < pitching.Leaders.Count; index++)
            {
                Assert.That(pitching.Leaders[index].OutsRecorded, Is.GreaterThanOrEqualTo(36));
                if (index > 0)
                {
                    Assert.That(
                        pitching.Leaders[index].EarnedRunAverage,
                        Is.GreaterThanOrEqualTo(pitching.Leaders[index - 1].EarnedRunAverage));
                }
            }

            for (int index = 0; index < view.TeamMetrics.Count; index++)
            {
                Assert.That(view.TeamMetrics[index].HasData, Is.True);
                Assert.That(view.TeamMetrics[index].MyTeamRank, Is.InRange(1, 8));
            }
        }

        [Test]
        public void Build_같은Seed와진행수에서는리그화면순서가같다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState firstCareer = CreateStartedCareer(configuration, 82_802UL);
            CareerState secondCareer = CreateStartedCareer(configuration, 82_802UL);
            AdvanceRounds(firstCareer, configuration, 16);
            AdvanceRounds(secondCareer, configuration, 16);

            LeagueHubView first = new LeagueHubService(firstCareer, configuration.Balance).Build();
            LeagueHubView second = new LeagueHubService(secondCareer, configuration.Balance).Build();

            for (int index = 0; index < first.Standings.Count; index++)
                Assert.That(second.Standings[index].TeamId, Is.EqualTo(first.Standings[index].TeamId));

            LeagueBattingLeaderboardView firstBatting =
                first.GetBattingLeaderboard(LeagueBattingCategory.OnBasePlusSlugging);
            LeagueBattingLeaderboardView secondBatting =
                second.GetBattingLeaderboard(LeagueBattingCategory.OnBasePlusSlugging);
            Assert.That(secondBatting.Leaders.Count, Is.EqualTo(firstBatting.Leaders.Count));
            for (int index = 0; index < firstBatting.Leaders.Count; index++)
                Assert.That(secondBatting.Leaders[index].PlayerId, Is.EqualTo(firstBatting.Leaders[index].PlayerId));

            LeaguePitchingLeaderboardView firstPitching =
                first.GetPitchingLeaderboard(LeaguePitchingCategory.Strikeouts);
            LeaguePitchingLeaderboardView secondPitching =
                second.GetPitchingLeaderboard(LeaguePitchingCategory.Strikeouts);
            Assert.That(secondPitching.Leaders.Count, Is.EqualTo(firstPitching.Leaders.Count));
            for (int index = 0; index < firstPitching.Leaders.Count; index++)
                Assert.That(secondPitching.Leaders[index].PlayerId, Is.EqualTo(firstPitching.Leaders[index].PlayerId));
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("리그 테스트", "대한민국");
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

        private static void AdvanceRounds(
            CareerState career,
            NewGameConfiguration configuration,
            int roundCount)
        {
            var service = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < roundCount; index++)
                service.AdvanceNextRound();
        }
    }
}

using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>일정 읽기 모델이 실제 시즌 진행과 월간 집계를 그대로 반영하는지 검증한다.</summary>
    public sealed class CareerScheduleViewBuilderTests
    {
        [Test]
        public void Build_전체리그와내구단일정을날짜와결과까지구성한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 28_801UL);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int index = 0; index < 12; index++)
                seasonService.AdvanceNextRound();

            CareerScheduleView view = new CareerScheduleViewBuilder(career, configuration.Balance).Build();
            CareerScheduleMonthView myTeamMonth = view.BuildMonth(
                view.CurrentDate.Year,
                view.CurrentDate.Month,
                CareerScheduleScope.MyTeam);
            CareerScheduleMonthView leagueMonth = view.BuildMonth(
                view.CurrentDate.Year,
                view.CurrentDate.Month,
                CareerScheduleScope.EntireLeague);

            Assert.That(view.Games.Count, Is.EqualTo(320));
            Assert.That(view.RecentGames.Count, Is.EqualTo(4));
            Assert.That(view.UpcomingGames.Count, Is.EqualTo(5));
            Assert.That(view.NextGame.HasValue, Is.True);
            Assert.That(view.NextGame.Value.Round, Is.EqualTo(13));
            Assert.That(
                view.CurrentDate,
                Is.EqualTo(SeasonDateCalculator.GetGameDate(
                    view.SeasonYear,
                    13,
                    configuration.Balance.CareerSeason)));
            Assert.That(myTeamMonth.Days.Count, Is.EqualTo(42));
            CareerScheduleGameView recent = view.RecentGames[0];
            Assert.That(
                recent.PlayerTeamRuns,
                Is.EqualTo(recent.IsPlayerHome ? recent.HomeRuns : recent.AwayRuns));
            Assert.That(
                recent.OpponentRuns,
                Is.EqualTo(recent.IsPlayerHome ? recent.AwayRuns : recent.HomeRuns));
            Assert.That(myTeamMonth.Summary.CompletedGames, Is.EqualTo(12));
            Assert.That(
                myTeamMonth.Summary.Wins + myTeamMonth.Summary.Losses + myTeamMonth.Summary.Ties,
                Is.EqualTo(12));
            Assert.That(
                myTeamMonth.Summary.HomeGames + myTeamMonth.Summary.AwayGames,
                Is.EqualTo(12));
            Assert.That(leagueMonth.DisplayedGames.Count, Is.EqualTo(myTeamMonth.DisplayedGames.Count * 4));
        }

        [Test]
        public void BuildMonth_경기가없는시즌중날짜를휴식일로표시한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 28_802UL);
            CareerScheduleView view = new CareerScheduleViewBuilder(career, configuration.Balance).Build();

            CareerScheduleMonthView month = view.BuildMonth(
                view.SeasonYear,
                configuration.Balance.CareerSeason.SeasonOpeningMonth,
                CareerScheduleScope.MyTeam);

            int restDays = 0;
            for (int index = 0; index < month.Days.Count; index++)
            {
                CareerScheduleDayView day = month.Days[index];
                if (day.IsVisibleMonth && day.IsRestDay)
                    restDays++;
            }
            Assert.That(restDays, Is.GreaterThan(0));
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("일정 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}

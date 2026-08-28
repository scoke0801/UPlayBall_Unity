using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 완료된 오프시즌이 다음 시즌의 리그·일정·로스터·계약으로 결정론적으로 이어지는지 검증한다.
    /// </summary>
    public sealed class CareerSeasonTransitionServiceTests
    {
        [Test]
        public void AdvanceToNextSeason_다음시즌정규시즌상태로전환한다()
        {
            NewGameFlow flow = CreateOffseasonCareer(101UL);
            CareerState career = flow.Career;
            int previousYear = career.League.CurrentSeason.Year;
            int previousGamesPlayed = career.SeasonHistory.Count;
            var service = new CareerSeasonTransitionService(career, NewGameConfiguration.CreateDefault().Balance);

            CareerSeasonTransitionResult result = service.AdvanceToNextSeason();

            Assert.That(career.League.CurrentSeason.Year, Is.EqualTo(previousYear + 1));
            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
            Assert.That(career.CurrentOffseason, Is.Null);
            Assert.That(career.SeasonHistory.Count, Is.EqualTo(previousGamesPlayed + 1));
            Assert.That(career.SeasonHistory[0].Year, Is.EqualTo(previousYear));
            Assert.That(result.Year, Is.EqualTo(previousYear + 1));
            Assert.That(career.League.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(0));
            Assert.That(
                career.League.CurrentSeason.Schedule.Games.Count,
                Is.EqualTo(career.League.Teams.Count / 2 *
                    NewGameConfiguration.CreateDefault().Balance.CareerSeason.RegularSeasonGamesPerTeam));
        }

        [Test]
        public void AdvanceToNextSeason_선수나이를한살올린다()
        {
            NewGameFlow flow = CreateOffseasonCareer(202UL);
            CareerState career = flow.Career;
            int previousAge = career.MyPlayer.Age;
            var service = new CareerSeasonTransitionService(career, NewGameConfiguration.CreateDefault().Balance);

            service.AdvanceToNextSeason();

            Assert.That(career.MyPlayer.Age, Is.EqualTo(previousAge + 1));
            Assert.That(career.MyPlayer.GrowthState.Age, Is.EqualTo(previousAge + 1));
        }

        [Test]
        public void AdvanceToNextSeason_계약이남아있으면같은구단재계약없이유지한다()
        {
            NewGameFlow flow = CreateOffseasonCareer(303UL);
            CareerState career = flow.Career;
            int contractHistoryCountBefore = career.ContractHistory.Count;
            int teamIdBefore = career.MyPlayer.CurrentTeamId;
            var service = new CareerSeasonTransitionService(career, NewGameConfiguration.CreateDefault().Balance);

            CareerSeasonTransitionResult result = service.AdvanceToNextSeason();

            Assert.That(career.ContractHistory.Count, Is.EqualTo(contractHistoryCountBefore));
            Assert.That(career.MyPlayer.CurrentTeamId, Is.EqualTo(teamIdBefore));
            Assert.That(result.TeamId, Is.EqualTo(teamIdBefore));
            Assert.That(result.WasTraded, Is.False);
        }

        [Test]
        public void AdvanceToNextSeason_같은Seed는같은다음시즌일정과로스터를만든다()
        {
            NewGameFlow first = CreateOffseasonCareer(404UL);
            NewGameFlow second = CreateOffseasonCareer(404UL);
            var firstService = new CareerSeasonTransitionService(first.Career, NewGameConfiguration.CreateDefault().Balance);
            var secondService = new CareerSeasonTransitionService(second.Career, NewGameConfiguration.CreateDefault().Balance);

            firstService.AdvanceToNextSeason();
            secondService.AdvanceToNextSeason();

            var firstGames = first.Career.League.CurrentSeason.Schedule.Games;
            var secondGames = second.Career.League.CurrentSeason.Schedule.Games;
            Assert.That(secondGames.Count, Is.EqualTo(firstGames.Count));
            for (int index = 0; index < firstGames.Count; index++)
            {
                Assert.That(secondGames[index].AwayTeamId, Is.EqualTo(firstGames[index].AwayTeamId));
                Assert.That(secondGames[index].HomeTeamId, Is.EqualTo(firstGames[index].HomeTeamId));
                Assert.That(secondGames[index].RandomSeed, Is.EqualTo(firstGames[index].RandomSeed));
            }

            for (int index = 0; index < first.Career.League.Teams.Count; index++)
            {
                var firstTeam = first.Career.League.Teams[index];
                var secondTeam = second.Career.League.Teams[index];
                Assert.That(secondTeam.RosterCompetitors.Count, Is.EqualTo(firstTeam.RosterCompetitors.Count));
                for (int competitorIndex = 0; competitorIndex < firstTeam.RosterCompetitors.Count; competitorIndex++)
                {
                    Assert.That(
                        secondTeam.RosterCompetitors[competitorIndex].Overall,
                        Is.EqualTo(firstTeam.RosterCompetitors[competitorIndex].Overall));
                }
            }
        }

        [Test]
        public void AdvanceToNextSeason_정규시즌중에는예외를던진다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, 505UL);
            flow.SubmitIdentity("전환 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            var service = new CareerSeasonTransitionService(flow.Career, configuration.Balance);

            Assert.Throws<InvalidOperationException>(() => service.AdvanceToNextSeason());
        }

        private static NewGameFlow CreateOffseasonCareer(ulong seed)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("전환 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            flow.Career.League.CurrentSeason.CompleteRegularSeason();

            var growthService = new CareerGrowthService(flow.Career, configuration.Balance);
            growthService.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            return flow;
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

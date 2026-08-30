using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>선수 상세 읽기 모델이 세이브 원본과 성장·기록 근거를 보존하는지 검증한다.</summary>
    public sealed class PlayerProfileViewBuilderTests
    {
        [Test]
        public void Build_타자선수의현재상태와여섯능력치를투영한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, PlayerType.Batter, 73_001UL);

            PlayerProfileView view = new PlayerProfileViewBuilder().Build(
                career,
                overall: 67,
                plannedRole: PlayerGameRole.StartingBatter);

            Assert.That(view.PlayerId, Is.EqualTo(career.MyPlayer.PlayerId));
            Assert.That(view.PlayerName, Is.EqualTo(career.MyPlayer.Name));
            Assert.That(view.TeamName, Is.Not.Empty);
            Assert.That(view.Overall, Is.EqualTo(67));
            Assert.That(view.PlannedRole, Is.EqualTo(PlayerGameRole.StartingBatter));
            Assert.That(view.Abilities.Length, Is.EqualTo(6));
            Assert.That(view.Abilities[0].Ability, Is.EqualTo(PlayerAbility.Contact));
            Assert.That(view.Abilities[0].BaseValue,
                Is.EqualTo(career.MyPlayer.GrowthState.BaseAbilities.Get(PlayerAbility.Contact)));
            Assert.That(view.Abilities[0].Potential,
                Is.EqualTo(career.MyPlayer.GrowthState.PotentialByAbility.Get(PlayerAbility.Contact)));
            Assert.That(view.ProfessionalYears, Is.EqualTo(1));
            Assert.That(view.CareerTotals.Length, Is.EqualTo(7));
        }

        [Test]
        public void Build_투수선수는투수능력치와투수통산지표를선택한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, PlayerType.Pitcher, 73_002UL);

            PlayerProfileView view = new PlayerProfileViewBuilder().Build(
                career,
                overall: 71,
                plannedRole: PlayerGameRole.StartingPitcher);

            Assert.That(view.PlayerType, Is.EqualTo(PlayerType.Pitcher));
            Assert.That(view.Abilities.Length, Is.EqualTo(6));
            Assert.That(view.Abilities[0].Ability, Is.EqualTo(PlayerAbility.Stamina));
            Assert.That(view.Abilities[5].Ability, Is.EqualTo(PlayerAbility.PitcherMental));
            Assert.That(view.CareerTotals[0].Metric, Is.EqualTo(CareerRecordMetric.EarnedRunAverage));
        }

        [Test]
        public void Build_최근경기를최신순으로복사해원본순서와분리한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, PlayerType.Batter, 73_003UL);
            var season = new CareerSeasonService(career, configuration.Balance);
            season.AdvanceNextRound();
            season.AdvanceNextRound();
            var builder = new PlayerProfileViewBuilder();

            PlayerProfileView view = builder.Build(career, 64, PlayerGameRole.Bench);

            Assert.That(view.RecentGames.Length, Is.EqualTo(2));
            Assert.That(view.RecentGames[0].GameId,
                Is.EqualTo(career.CurrentLeague.CurrentSeason.PlayerStatistics.RecentGames[1].GameId));
            Assert.That(view.RecentGames[1].GameId,
                Is.EqualTo(career.CurrentLeague.CurrentSeason.PlayerStatistics.RecentGames[0].GameId));
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            PlayerType playerType,
            ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("선수 화면 테스트", "대한민국");
            flow.SelectPlayerType(playerType);
            if (playerType == PlayerType.Pitcher)
            {
                flow.SelectPosition(PlayerPosition.StartingPitcher);
                flow.SelectHandedness(Handedness.Right, Handedness.Right);
                flow.SubmitPitcherAttributes(new PitcherAttributes(63, 62, 62, 58, 60, 55));
            }
            else
            {
                flow.SelectPosition(PlayerPosition.Shortstop);
                flow.SelectHandedness(Handedness.Left, Handedness.Right);
                flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            }
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}

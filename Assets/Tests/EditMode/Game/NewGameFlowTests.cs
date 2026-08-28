using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 캐릭터 생성부터 계약과 Rookie 정규 시즌 시작까지의 Game 상태 전이를 검증한다.
    /// </summary>
    public sealed class NewGameFlowTests
    {
        [Test]
        public void CompleteFlow_계약과세이브상태를만들고Rookie정규시즌을시작한다()
        {
            NewGameFlow flow = CreatePlayerCard(seed: 424242UL);

            flow.GenerateOffers();
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.ContractOffers));
            Assert.That(flow.State.SetupResult.Offers.Length, Is.InRange(3, 5));

            int selectedTeamId = flow.State.SetupResult.Offers[0].Team.TeamId;
            flow.SelectOffer(selectedTeamId);
            flow.SignSelectedOffer();

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.ContractComplete));
            Assert.That(flow.Career.SaveVersion, Is.EqualTo(NewGameFlow.CurrentSaveVersion));
            Assert.That(flow.Career.MyPlayer.Name, Is.EqualTo("최민석"));
            Assert.That(flow.Career.MyPlayer.Nationality, Is.EqualTo("대한민국"));
            Assert.That(flow.Career.MyPlayer.CurrentTeamId, Is.EqualTo(selectedTeamId));
            Assert.That(flow.Career.League.RandomSeed, Is.EqualTo(424242UL));
            Assert.That(flow.Career.League.Teams.Count, Is.EqualTo(8));
            Assert.That(flow.Career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Preseason));
            Assert.That(flow.Career.AvailableMoney, Is.EqualTo(flow.Career.CurrentContract.SigningBonus));

            flow.StartRookieSeason();

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.Completed));
            Assert.That(flow.Career.League.CurrentSeason.LeagueLevel, Is.EqualTo(LeagueLevel.Rookie));
            Assert.That(flow.Career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
        }

        [Test]
        public void SelectPosition_타자유형에서투수포지션을선택하면거부한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 1UL);
            flow.SubmitIdentity("테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);

            Assert.Throws<System.ArgumentException>(() =>
                flow.SelectPosition(PlayerPosition.StartingPitcher));
            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.Position));
        }

        [Test]
        public void SubmitAttributes_포지션과어긋난빌드도경고만하고진행한다()
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), 9UL);
            flow.SubmitIdentity("장타 유격수", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);

            flow.SubmitBatterAttributes(new BatterAttributes(50, 65, 50, 55, 40, 40));

            Assert.That(flow.State.Step, Is.EqualTo(NewGameStep.PlayerCard));
            Assert.That(flow.BuildWarning, Is.Not.Empty);
            Assert.DoesNotThrow(flow.GenerateOffers);
        }

        [Test]
        public void GenerateOffers_같은Seed와선택이면계약목록이같다()
        {
            NewGameFlow first = CreatePlayerCard(777UL);
            NewGameFlow second = CreatePlayerCard(777UL);

            first.GenerateOffers();
            second.GenerateOffers();

            Assert.That(second.State.SetupResult.Offers.Length, Is.EqualTo(first.State.SetupResult.Offers.Length));
            for (int index = 0; index < first.State.SetupResult.Offers.Length; index++)
            {
                Assert.That(
                    second.State.SetupResult.Offers[index].Team.TeamId,
                    Is.EqualTo(first.State.SetupResult.Offers[index].Team.TeamId));
                Assert.That(
                    second.State.SetupResult.Offers[index].AnnualSalary,
                    Is.EqualTo(first.State.SetupResult.Offers[index].AnnualSalary));
            }
        }

        private static NewGameFlow CreatePlayerCard(ulong seed)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("최민석", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            return flow;
        }
    }
}

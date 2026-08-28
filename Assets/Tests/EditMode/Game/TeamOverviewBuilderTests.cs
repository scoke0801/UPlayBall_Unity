using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 구단 읽기 모델이 실제 경기 기용 계획과 같은 선발을 노출하는지 검증한다.
    /// </summary>
    public sealed class TeamOverviewBuilderTests
    {
        [Test]
        public void Build_전체로스터와내선수를중복없이포함한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 4242UL);
            new CareerSeasonService(career, configuration.Balance).EnsureNextGamePlan();
            TeamState team = GetPlayerTeam(career);

            TeamOverviewView view = new TeamOverviewBuilder(configuration.Balance.PlayerEvaluation).Build(career);

            Assert.That(view.Roster.Length, Is.EqualTo(team.RosterCompetitors.Count + 1));
            int myPlayerCount = 0;
            int conditionCount = 0;
            for (int index = 0; index < view.Roster.Length; index++)
            {
                if (view.Roster[index].PlayerId == career.MyPlayer.PlayerId)
                    myPlayerCount++;
                if (view.Roster[index].HasCondition)
                    conditionCount++;
            }
            Assert.That(myPlayerCount, Is.EqualTo(1));
            Assert.That(conditionCount, Is.EqualTo(1), "저장 상태에 없는 경쟁자 컨디션을 꾸며내면 안 됩니다.");
            Assert.That(view.StartingLineup.Length, Is.EqualTo(9));
        }

        [Test]
        public void Build_표시한선발과투수계획이실제Match입력선택규칙과일치한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 7777UL);
            var service = new CareerSeasonService(career, configuration.Balance);
            service.EnsureNextGamePlan();
            ScheduledGameState nextGame = service.NextPlayerGame;
            TeamState team = GetPlayerTeam(career);

            TeamOverviewView view = new TeamOverviewBuilder(configuration.Balance.PlayerEvaluation).Build(career);

            Assert.That(view.HasNextGamePlan, Is.True);
            Assert.That(view.PlannedPlayerRole, Is.EqualTo(nextGame.PlannedPlayerRole));
            int myLineupCount = 0;
            for (int index = 0; index < view.StartingLineup.Length; index++)
            {
                TeamLineupSlotView slot = view.StartingLineup[index];
                Assert.That(slot.Position, Is.EqualTo((PlayerPosition)(index + 1)));
                int expectedPlayerId = nextGame.PlannedPlayerRole == PlayerGameRole.StartingBatter &&
                                       career.MyPlayer.PrimaryPosition == slot.Position
                    ? career.MyPlayer.PlayerId
                    : team.GetStrongestCompetitor(slot.Position).PlayerId;
                Assert.That(slot.Player.PlayerId, Is.EqualTo(expectedPlayerId));
                if (slot.Player.PlayerId == career.MyPlayer.PlayerId)
                    myLineupCount++;
            }
            Assert.That(
                myLineupCount,
                Is.EqualTo(nextGame.PlannedPlayerRole == PlayerGameRole.StartingBatter ? 1 : 0));

            int expectedStartingPitcherId = nextGame.PlannedPlayerRole == PlayerGameRole.StartingPitcher
                ? career.MyPlayer.PlayerId
                : team.GetCompetitor(PlayerPosition.StartingPitcher, nextGame.Round % 2).PlayerId;
            int expectedReliefPitcherId = nextGame.PlannedPlayerRole == PlayerGameRole.ReliefPitcher
                ? career.MyPlayer.PlayerId
                : team.GetCompetitor(PlayerPosition.ReliefPitcher, (nextGame.Round + 1) % 2).PlayerId;
            Assert.That(GetPlannedPitcherId(view.StartingRotation), Is.EqualTo(expectedStartingPitcherId));
            Assert.That(GetPlannedPitcherId(view.Bullpen), Is.EqualTo(expectedReliefPitcherId));
        }

        [Test]
        public void Build_진행된경기의리그원본통계를로스터행에전달한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9911UL);
            TeamState team = GetPlayerTeam(career);
            var service = new CareerSeasonService(career, configuration.Balance);
            service.AdvanceNextRound();
            int catcherId = team.GetStrongestCompetitor(PlayerPosition.Catcher).PlayerId;

            TeamOverviewView view = new TeamOverviewBuilder(configuration.Balance.PlayerEvaluation).Build(career);
            TeamRosterPlayerView catcher = FindPlayer(view, catcherId);

            Assert.That(catcher.HasBattingRecord, Is.True);
            Assert.That(catcher.BattingAverage, Is.InRange(0d, 1d));
        }

        private static int GetPlannedPitcherId(TeamRosterPlayerView[] pitchers)
        {
            for (int index = 0; index < pitchers.Length; index++)
            {
                if (pitchers[index].IsInNextGamePlan)
                    return pitchers[index].PlayerId;
            }
            Assert.Fail("다음 경기 투수가 지정되지 않았습니다.");
            return 0;
        }

        private static TeamRosterPlayerView FindPlayer(TeamOverviewView view, int playerId)
        {
            for (int index = 0; index < view.Roster.Length; index++)
            {
                if (view.Roster[index].PlayerId == playerId)
                    return view.Roster[index];
            }
            Assert.Fail($"PlayerId {playerId}를 찾지 못했습니다.");
            return default;
        }

        private static TeamState GetPlayerTeam(CareerState career)
        {
            for (int index = 0; index < career.League.Teams.Count; index++)
            {
                TeamState team = career.League.Teams[index];
                if (team.TeamId == career.MyPlayer.CurrentTeamId)
                    return team;
            }
            Assert.Fail("내 선수의 소속 구단을 찾지 못했습니다.");
            return null;
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("구단 화면 테스트", "대한민국");
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

using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Career.Narrative;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>확정 기록만으로 결과 해석·최근 흐름·변화 이유를 재현하는지 검증한다.</summary>
    public sealed class MatchNarrativeServiceTests
    {
        [Test]
        public void CreateSnapshot_무안타볼넷무삼진한점차패배를사실범위에서해석한다()
        {
            CareerState career = CreateStartedCareer();
            MatchNarrativeBaseline baseline = CreateBaseline(career, System.Array.Empty<PlayerGameLogState>());
            CareerGameAdvanceResult result = CreateBatterResult(
                baseline,
                teamRuns: 4,
                opponentRuns: 5,
                hits: 0,
                walks: 1,
                strikeouts: 0,
                managerTrustBefore: 64,
                managerTrustAfter: 62);

            MatchNarrativeSnapshot snapshot = MatchNarrativeService.CreateSnapshot(career, baseline, result);

            Assert.That(snapshot.HasTag(NarrativeTag.Hitless), Is.True);
            Assert.That(snapshot.HasTag(NarrativeTag.WalkOnly), Is.True);
            Assert.That(snapshot.HasTag(NarrativeTag.NoStrikeout), Is.True);
            Assert.That(snapshot.HasTag(NarrativeTag.OneRunGame), Is.True);
            Assert.That(snapshot.HasTag(NarrativeTag.ManagerTrustDown), Is.True);
            Assert.That(snapshot.Headline, Does.Contain("한 점 차 패배"));
            Assert.That(snapshot.PerformanceEvaluation, Is.EqualTo("안타는 없었지만 볼넷으로 한 차례 출루했다."));
            Assert.That(snapshot.PerformanceDetail, Does.Contain("삼진 없이"));
            Assert.That(snapshot.GameImpact, Does.Contain("출루가 득점으로 이어지지 못한"));
            Assert.That(snapshot.ManagerTrustReason, Is.EqualTo("선발 출전에서 무안타"));
        }

        [Test]
        public void CreateSnapshot_값이같은신뢰와역할은유지로기록한다()
        {
            CareerState career = CreateStartedCareer();
            MatchNarrativeBaseline baseline = CreateBaseline(career, System.Array.Empty<PlayerGameLogState>());
            CareerGameAdvanceResult result = CreateBatterResult(
                baseline,
                teamRuns: 6,
                opponentRuns: 2,
                hits: 1,
                walks: 0,
                strikeouts: 1,
                managerTrustBefore: 64,
                managerTrustAfter: 64);

            MatchNarrativeSnapshot snapshot = MatchNarrativeService.CreateSnapshot(career, baseline, result);

            Assert.That(snapshot.HasTag(NarrativeTag.ManagerTrustStable), Is.True);
            Assert.That(snapshot.HasTag(NarrativeTag.RoleStable), Is.True);
            Assert.That(snapshot.ManagerTrustReason, Does.Contain("변화 없음"));
            Assert.That(snapshot.RoleReason, Is.EqualTo("선발 유지"));
        }

        [Test]
        public void CreateSnapshot_세경기무안타뒤안타를부진해소로기억한다()
        {
            CareerState career = CreateStartedCareer();
            var recent = new List<PlayerGameLogState>
            {
                CreateHitlessLog(101),
                CreateHitlessLog(102),
                CreateHitlessLog(103)
            };
            MatchNarrativeBaseline baseline = CreateBaseline(career, recent);
            CareerGameAdvanceResult result = CreateBatterResult(
                baseline,
                teamRuns: 5,
                opponentRuns: 3,
                hits: 1,
                walks: 0,
                strikeouts: 0,
                managerTrustBefore: 60,
                managerTrustAfter: 60);

            MatchNarrativeSnapshot first = MatchNarrativeService.CreateSnapshot(career, baseline, result);
            MatchNarrativeSnapshot second = MatchNarrativeService.CreateSnapshot(career, baseline, result);

            Assert.That(first.PreviousHitlessStreak, Is.EqualTo(3));
            Assert.That(first.HasTag(NarrativeTag.SlumpEnded), Is.True);
            Assert.That(first.RecentForm, Does.StartWith("4경기 만에"));
            Assert.That(second.Headline, Is.EqualTo(first.Headline));
            Assert.That(second.RecentForm, Is.EqualTo(first.RecentForm));
        }

        [Test]
        public void SeasonState_같은경기내러티브를두번저장하지않는다()
        {
            CareerState career = CreateStartedCareer();
            MatchNarrativeBaseline baseline = CreateBaseline(career, System.Array.Empty<PlayerGameLogState>());
            MatchNarrativeSnapshot snapshot = MatchNarrativeService.CreateSnapshot(
                career,
                baseline,
                CreateBatterResult(baseline, 3, 2, 1, 0, 0, 50, 50));
            SeasonState season = career.CurrentLeague.CurrentSeason;

            season.RecordMatchNarrative(snapshot);

            Assert.That(season.FindMatchNarrative(snapshot.GameId), Is.SameAs(snapshot));
            Assert.Throws<System.InvalidOperationException>(() => season.RecordMatchNarrative(snapshot));
        }

        private static MatchNarrativeBaseline CreateBaseline(
            CareerState career,
            IReadOnlyList<PlayerGameLogState> recentGames)
        {
            ScheduledGameState game = career.CurrentLeague.CurrentSeason.Schedule
                .GetNextGameForTeam(career.MyPlayer.CurrentTeamId);
            int opponentId = game.HomeTeamId == career.MyPlayer.CurrentTeamId
                ? game.AwayTeamId
                : game.HomeTeamId;
            return new MatchNarrativeBaseline(
                career.CurrentLeague.CurrentSeason.SeasonId,
                game.GameId,
                career.MyPlayer.CurrentTeamId,
                "울산 가디언즈",
                opponentId,
                "대전 호크스",
                "임민석",
                PlayerPosition.Shortstop,
                PlayerGameRole.StartingBatter,
                CompetitionScope.RegularSeason,
                1,
                0.272d,
                0d,
                58,
                64,
                recentGames);
        }

        private static CareerGameAdvanceResult CreateBatterResult(
            MatchNarrativeBaseline baseline,
            int teamRuns,
            int opponentRuns,
            int hits,
            int walks,
            int strikeouts,
            int managerTrustBefore,
            int managerTrustAfter)
        {
            return new CareerGameAdvanceResult(
                gameId: baseline.GameId,
                round: 1,
                opponentTeamId: baseline.OpponentTeamId,
                isHome: true,
                teamRuns,
                opponentRuns,
                role: PlayerGameRole.StartingBatter,
                plateAppearances: 4,
                atBats: 3,
                runs: 0,
                hits,
                doubles: 0,
                triples: 0,
                homeRuns: 0,
                runsBattedIn: 0,
                walks,
                hitByPitches: 0,
                sacrificeFlies: 0,
                groundedIntoDoublePlays: 0,
                outsRecorded: 0,
                earnedRuns: 0,
                strikeouts,
                walksAllowed: 0,
                hitBatters: 0,
                conditionBefore: 58,
                conditionAfter: 56,
                managerEvaluationBefore: managerTrustBefore,
                managerEvaluationAfter: managerTrustAfter);
        }

        private static PlayerGameLogState CreateHitlessLog(int gameId)
        {
            return new PlayerGameLogState(
                gameId,
                opponentTeamId: 2,
                isHome: true,
                didWin: false,
                teamRuns: 2,
                opponentRuns: 3,
                PlayerGameRole.StartingBatter,
                atBats: 4,
                hits: 0,
                homeRuns: 0,
                runsBattedIn: 0,
                walks: 0,
                hitByPitches: 0,
                outsRecorded: 0,
                earnedRuns: 0,
                strikeouts: 1,
                walksAllowed: 0,
                hitBatters: 0);
        }

        private static CareerState CreateStartedCareer()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, 290829UL);
            flow.SubmitIdentity("내러티브 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}

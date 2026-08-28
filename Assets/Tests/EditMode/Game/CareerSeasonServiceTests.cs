using Baseball.Core.Players;
using Baseball.Core.Teams;
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

        [Test]
        public void CompletePreparedGame_화면에서완료한경기를라운드에한번만반영한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 8181UL);
            var service = new CareerSeasonService(career, configuration.Balance);
            CareerMatchSession session = service.PrepareNextGame();
            int round = session.ScheduledGame.Round;

            session.Start(CareerMatchMode.ResultsOnly);
            CareerGameAdvanceResult result = service.CompletePreparedGame(session);

            Assert.That(session.IsComplete, Is.True);
            Assert.That(result.GameId, Is.EqualTo(session.ScheduledGame.GameId));
            Assert.That(career.League.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
            int completedInRound = 0;
            for (int index = 0; index < career.League.CurrentSeason.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = career.League.CurrentSeason.Schedule.Games[index];
                if (game.Round == round && game.IsCompleted)
                    completedInRound++;
            }
            Assert.That(completedInRound, Is.EqualTo(4));
            Assert.Throws<System.InvalidOperationException>(() => service.CompletePreparedGame(session));
        }

        [Test]
        public void CompleteToSeasonReview_남은일정과포스트시즌을완료하고결산에서멈춘다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9191UL);
            SeasonState season = career.League.CurrentSeason;
            int scheduledPlayerGames = CountScheduledPlayerGames(season, career.MyPlayer.CurrentTeamId);

            CareerSeasonAutoCompletionResult result = new CareerSeasonAutoCompletionService(
                    career,
                    configuration.Balance)
                .CompleteToSeasonReview();

            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
            Assert.That(season.Postseason.IsCompleted, Is.True);
            Assert.That(season.PlayerStatistics.TeamGames, Is.EqualTo(scheduledPlayerGames));
            Assert.That(result.RegularSeasonGames, Is.EqualTo(scheduledPlayerGames));
            Assert.That(result.PostseasonGames, Is.GreaterThan(0));
            Assert.That(result.ChampionTeamId, Is.Not.EqualTo(0));
            for (int index = 0; index < season.Schedule.Games.Count; index++)
                Assert.That(season.Schedule.Games[index].IsCompleted, Is.True);
        }

        [Test]
        public void CompleteToSeasonReview_경기단위진행과같은시즌결과를만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState automaticCareer = CreateStartedCareer(configuration, 9292UL);
            CareerState manualCareer = CreateStartedCareer(configuration, 9292UL);

            CareerSeasonAutoCompletionResult automatic = new CareerSeasonAutoCompletionService(
                    automaticCareer,
                    configuration.Balance)
                .CompleteToSeasonReview();

            var regularSeason = new CareerSeasonService(manualCareer, configuration.Balance);
            while (regularSeason.NextPlayerGame != null)
                regularSeason.AdvanceNextRound();
            CareerPostseasonGameResult manual = new CareerPostseasonService(manualCareer, configuration.Balance)
                .AdvanceToChampion();

            SeasonState automaticSeason = automaticCareer.League.CurrentSeason;
            SeasonState manualSeason = manualCareer.League.CurrentSeason;
            Assert.That(automatic.ChampionTeamId, Is.EqualTo(manual.ChampionTeamId));
            Assert.That(automaticSeason.PlayerStatistics.Hits, Is.EqualTo(manualSeason.PlayerStatistics.Hits));
            Assert.That(automaticSeason.PlayerStatistics.HomeRuns, Is.EqualTo(manualSeason.PlayerStatistics.HomeRuns));
            Assert.That(automaticSeason.PlayerStatistics.RunsBattedIn, Is.EqualTo(manualSeason.PlayerStatistics.RunsBattedIn));
            for (int index = 0; index < automaticSeason.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState expected = manualSeason.GetTeamRecord(automaticSeason.TeamRecords[index].TeamId);
                TeamSeasonRecordState actual = automaticSeason.TeamRecords[index];
                Assert.That(actual.Wins, Is.EqualTo(expected.Wins));
                Assert.That(actual.Losses, Is.EqualTo(expected.Losses));
                Assert.That(actual.RunsScored, Is.EqualTo(expected.RunsScored));
                Assert.That(actual.RunsAllowed, Is.EqualTo(expected.RunsAllowed));
            }
        }

        private static int CountScheduledPlayerGames(SeasonState season, int playerTeamId)
        {
            int count = 0;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                if (season.Schedule.Games[index].IncludesTeam(playerTeamId))
                    count++;
            }
            return count;
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

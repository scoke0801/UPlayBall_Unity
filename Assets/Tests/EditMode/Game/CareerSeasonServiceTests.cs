using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Career.Diagnostics;
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
            var games = career.CurrentLeague.CurrentSeason.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                if (games[index].Round == round && games[index].IsCompleted)
                    completedInRound++;
            }
            Assert.That(completedInRound, Is.EqualTo(4));
            Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
            Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.RecentGames.Count, Is.EqualTo(1));
            Assert.That(result.Round, Is.EqualTo(round));
            for (int index = 0; index < career.CurrentLeague.CurrentSeason.TeamRecords.Count; index++)
                Assert.That(career.CurrentLeague.CurrentSeason.TeamRecords[index].GamesPlayed, Is.EqualTo(1));
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
        public void CareerStateChecksum_같은공개상태와참조구조는같은값을만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState first = CreateStartedCareer(configuration, 7801UL);
            CareerState second = CreateStartedCareer(configuration, 7801UL);

            Assert.That(
                CareerStateChecksum.Calculate(second),
                Is.EqualTo(CareerStateChecksum.Calculate(first)));
        }

        [Test]
        public void CareerStateChecksum_라운드가확정되면값이바뀐다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 7802UL);
            string before = CareerStateChecksum.Calculate(career);

            new CareerSeasonService(career, configuration.Balance).AdvanceNextRound();

            Assert.That(CareerStateChecksum.Calculate(career), Is.Not.EqualTo(before));
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
            Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
            int completedInRound = 0;
            for (int index = 0; index < career.CurrentLeague.CurrentSeason.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = career.CurrentLeague.CurrentSeason.Schedule.Games[index];
                if (game.Round == round && game.IsCompleted)
                    completedInRound++;
            }
            Assert.That(completedInRound, Is.EqualTo(4));
            Assert.Throws<System.InvalidOperationException>(() => service.CompletePreparedGame(session));
        }

        [Test]
        public void CompleteCurrentPhase_정규시즌에서는포스트시즌진입에서멈춘다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9191UL);
            SeasonState season = career.CurrentLeague.CurrentSeason;
            int scheduledPlayerGames = CountScheduledPlayerGames(season, career.MyPlayer.CurrentTeamId);

            CareerSeasonAutoCompletionResult result = new CareerSeasonAutoCompletionService(
                    career,
                    configuration.Balance)
                .CompleteCurrentPhase();

            Assert.That(season.Phase, Is.EqualTo(SeasonPhase.Postseason));
            Assert.That(season.Postseason.IsCompleted, Is.False);
            Assert.That(season.PlayerStatistics.TeamGames, Is.EqualTo(scheduledPlayerGames));
            Assert.That(result.CompletedPhase, Is.EqualTo(SeasonPhase.RegularSeason));
            Assert.That(result.RegularSeasonGames, Is.EqualTo(scheduledPlayerGames));
            Assert.That(result.PostseasonGames, Is.Zero);
            Assert.That(result.ChampionTeamId, Is.Zero);
            for (int index = 0; index < season.Schedule.Games.Count; index++)
                Assert.That(season.Schedule.Games[index].IsCompleted, Is.True);
        }

        [Test]
        public void SeasonFastForwardSession_한Step은월드라운드하나를확정한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9201UL);
            var session = new SeasonFastForwardSession(career, configuration.Balance);

            SeasonFastForwardStepResult step = session.AdvanceNextStep();

            Assert.That(step.CompletedSteps, Is.EqualTo(1));
            Assert.That(step.LastCompletedRound, Is.EqualTo(1));
            Assert.That(step.ProcessedWorldGames, Is.EqualTo(40));
            Assert.That(step.TotalWorldGames, Is.EqualTo(3200));
            Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
        }

        [Test]
        public void SeasonFastForwardSession_중단은완료한라운드를유지하고재진입을막는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9203UL);
            var session = new SeasonFastForwardSession(career, configuration.Balance);
            session.AdvanceNextStep();

            SeasonFastForwardStepResult stopped = session.StopByUser();

            Assert.That(stopped.Status, Is.EqualTo(SeasonFastForwardStatus.StoppedByUser));
            Assert.That(stopped.LastCompletedRound, Is.EqualTo(1));
            Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.TeamGames, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => session.AdvanceNextStep());
        }

        [Test]
        public void SeasonFastForwardSession_기존라운드반복과같은상태Checksum을만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState sessionCareer = CreateStartedCareer(configuration, 9202UL);
            CareerState manualCareer = CreateStartedCareer(configuration, 9202UL);

            new SeasonFastForwardSession(sessionCareer, configuration.Balance).Complete();
            var manual = new CareerSeasonService(manualCareer, configuration.Balance);
            while (manual.NextPlayerGame != null)
                manual.AdvanceNextRound();

            Assert.That(
                CareerStateChecksum.Calculate(sessionCareer),
                Is.EqualTo(CareerStateChecksum.Calculate(manualCareer)));
        }

        [Test]
        public void CompleteCurrentPhase_포스트시즌에서는결산에서멈춘다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, 9192UL);
            var service = new CareerSeasonAutoCompletionService(career, configuration.Balance);
            service.CompleteCurrentPhase();

            CareerSeasonAutoCompletionResult result = service.CompleteCurrentPhase();

            Assert.That(career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.SeasonReview));
            Assert.That(career.CurrentLeague.CurrentSeason.Postseason.IsCompleted, Is.True);
            Assert.That(result.CompletedPhase, Is.EqualTo(SeasonPhase.Postseason));
            Assert.That(result.RegularSeasonGames, Is.Zero);
            Assert.That(result.PostseasonGames, Is.GreaterThan(0));
            Assert.That(result.ChampionTeamId, Is.Not.Zero);
        }

        [Test]
        public void CompleteCurrentPhase_단계별자동진행이경기단위진행과같은시즌결과를만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState automaticCareer = CreateStartedCareer(configuration, 9292UL);
            CareerState manualCareer = CreateStartedCareer(configuration, 9292UL);

            CareerSeasonAutoCompletionResult automatic = new CareerSeasonAutoCompletionService(
                    automaticCareer,
                    configuration.Balance)
                .CompleteCurrentPhase();
            automatic = new CareerSeasonAutoCompletionService(automaticCareer, configuration.Balance)
                .CompleteCurrentPhase();

            var regularSeason = new CareerSeasonService(manualCareer, configuration.Balance);
            while (regularSeason.NextPlayerGame != null)
                regularSeason.AdvanceNextRound();
            CareerPostseasonGameResult manual = new CareerPostseasonService(manualCareer, configuration.Balance)
                .AdvanceToChampion();

            SeasonState automaticSeason = automaticCareer.CurrentLeague.CurrentSeason;
            SeasonState manualSeason = manualCareer.CurrentLeague.CurrentSeason;
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
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }
    }
}

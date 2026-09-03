using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 여러 완주 시즌에서 타자·SP·RP 기용 빈도와 기본 야구 지표가 납득 가능한 범위인지 검증한다.
    /// </summary>
    public sealed class CareerSeasonStatisticsTests
    {
        private const int SeasonsPerRole = 15;

        [Test]
        public void Simulate_45개완주시즌에서기용과기본통계가유효하다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var batter = new RoleTotals();
            var starter = new RoleTotals();
            var reliever = new RoleTotals();
            long leagueRuns = 0;
            long leagueTeamGames = 0;

            for (int index = 0; index < SeasonsPerRole; index++)
            {
                SimulateSeason(configuration, PlayerPosition.Shortstop, (ulong)(10_000 + index), batter,
                    ref leagueRuns, ref leagueTeamGames);
                SimulateSeason(configuration, PlayerPosition.StartingPitcher, (ulong)(20_000 + index), starter,
                    ref leagueRuns, ref leagueTeamGames);
                SimulateSeason(configuration, PlayerPosition.ReliefPitcher, (ulong)(30_000 + index), reliever,
                    ref leagueRuns, ref leagueTeamGames);
            }

            double runsPerTeamGame = leagueRuns / (double)leagueTeamGames;
            System.Console.WriteLine(
                $"45 seasons / {leagueTeamGames / 2:N0} league games / R/G {runsPerTeamGame:F2}\n" +
                $"Batter Start% {batter.StartRate:P1} AVG {batter.BattingAverage:F3} OPS {batter.Ops:F3}\n" +
                $"SP App% {starter.AppearanceRate:P1} IP/App {starter.InningsPerAppearance:F1} ERA {starter.Era:F2}\n" +
                $"RP App% {reliever.AppearanceRate:P1} IP/App {reliever.InningsPerAppearance:F1} ERA {reliever.Era:F2}");

            var failures = new List<string>();
            AddOutOfRange(failures, "R/G", runsPerTeamGame, 3.2d, 5.8d);
            AddOutOfRange(failures, "Batter StartRate", batter.StartRate, 0.18d, 0.95d);
            AddOutOfRange(failures, "Batter AVG", batter.BattingAverage, 0.180d, 0.380d);
            AddOutOfRange(failures, "Batter OPS", batter.Ops, 0.500d, 1.100d);
            AddOutOfRange(failures, "SP AppearanceRate", starter.AppearanceRate, 0.02d, 0.20d);
            AddOutOfRange(failures, "SP IP/App", starter.InningsPerAppearance, 5.5d, 6.5d);
            AddOutOfRange(failures, "SP ERA", starter.Era, 1.5d, 7.0d);
            AddOutOfRange(failures, "RP AppearanceRate", reliever.AppearanceRate, 0.10d, 0.90d);
            // 역할 기반 다인 불펜에서는 한 명이 7~9회를 전담하던 옛 3이닝 기대치를 쓰지 않는다.
            AddOutOfRange(failures, "RP IP/App", reliever.InningsPerAppearance, 0.8d, 2.2d);
            AddOutOfRange(failures, "RP ERA", reliever.Era, 1.5d, 7.0d);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void Simulate_BenchCompetition20개시즌에서평가기회가유지된다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            int completedSeasons = 0;
            int totalGamesPlayed = 0;
            int totalGamesStarted = 0;
            int totalAtBats = 0;

            for (ulong seed = 1_000UL; seed < 20_000UL && completedSeasons < 20; seed++)
            {
                CareerState career = TryCreateStartedCareer(
                    configuration,
                    PlayerPosition.Shortstop,
                    seed,
                    ExpectedRole.BenchCompetition);
                if (career == null)
                    continue;

                var service = new CareerSeasonService(career, configuration.Balance);
                while (service.NextPlayerGame != null)
                    service.AdvanceNextRound();

                PlayerSeasonStatisticsState statistics = career.CurrentLeague.CurrentSeason.PlayerStatistics;
                Assert.That(statistics.GamesStarted, Is.GreaterThanOrEqualTo(5), $"Seed {seed}");
                Assert.That(statistics.AtBats, Is.GreaterThanOrEqualTo(12), $"Seed {seed}");
                totalGamesPlayed += statistics.GamesPlayed;
                totalGamesStarted += statistics.GamesStarted;
                totalAtBats += statistics.AtBats;
                completedSeasons++;
            }

            Assert.That(completedSeasons, Is.EqualTo(20));
            int substituteAppearances = totalGamesPlayed - totalGamesStarted;
            System.Console.WriteLine(
                $"BenchCompetition 20 seasons / GS {totalGamesStarted} / " +
                $"Sub appearances {substituteAppearances} / AB {totalAtBats}");
            Assert.That(substituteAppearances, Is.GreaterThan(0));
        }

        private static void SimulateSeason(
            NewGameConfiguration configuration,
            PlayerPosition position,
            ulong seed,
            RoleTotals totals,
            ref long leagueRuns,
            ref long leagueTeamGames)
        {
            CareerState career = CreateStartedCareer(configuration, position, seed);
            var service = new CareerSeasonService(career, configuration.Balance);
            while (service.NextPlayerGame != null)
                service.AdvanceNextRound();

            Assert.That(career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Postseason));
            for (int index = 0; index < career.CurrentLeague.CurrentSeason.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState record = career.CurrentLeague.CurrentSeason.TeamRecords[index];
                Assert.That(record.GamesPlayed, Is.EqualTo(80));
                leagueRuns += record.RunsScored;
                leagueTeamGames += record.GamesPlayed;
            }
            totals.Add(career.CurrentLeague.CurrentSeason.PlayerStatistics);
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            PlayerPosition position,
            ulong seed)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("통계 테스트", "대한민국");
            flow.SelectPlayerType(isPitcher ? PlayerType.Pitcher : PlayerType.Batter);
            flow.SelectPosition(position);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            if (isPitcher)
            {
                flow.SubmitPitcherAttributes(new PitcherAttributes(63, 62, 62, 58, 60, 55));
            }
            else
            {
                flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            }
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static void AddOutOfRange(
            ICollection<string> failures,
            string metric,
            double actual,
            double minimum,
            double maximum)
        {
            if (actual < minimum || actual > maximum)
                failures.Add($"{metric}: actual={actual:F3}, expected={minimum:F3}..{maximum:F3}");
        }

        private static CareerState TryCreateStartedCareer(
            NewGameConfiguration configuration,
            PlayerPosition position,
            ulong seed,
            ExpectedRole expectedRole)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("벤치 기회 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(position);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(63, 58, 60, 53, 66, 60));
            flow.GenerateOffers();

            for (int index = 0; index < flow.State.SetupResult.Offers.Length; index++)
            {
                var offer = flow.State.SetupResult.Offers[index];
                if (offer.ExpectedRole != expectedRole)
                    continue;

                flow.SelectOffer(offer.Team.TeamId);
                flow.SignSelectedOffer();
                flow.StartRookieSeason();
                return flow.Career;
            }

            return null;
        }

        private sealed class RoleTotals
        {
            private long _teamGames;
            private long _gamesStarted;
            private long _atBats;
            private long _hits;
            private long _walks;
            private long _totalBases;
            private long _pitchingAppearances;
            private long _outsRecorded;
            private long _earnedRuns;

            public double StartRate => _teamGames == 0 ? 0d : _gamesStarted / (double)_teamGames;
            public double AppearanceRate => _teamGames == 0 ? 0d : _pitchingAppearances / (double)_teamGames;
            public double BattingAverage => _atBats == 0 ? 0d : _hits / (double)_atBats;
            public double Ops => _atBats == 0
                ? 0d
                : (_hits + _walks) / (double)(_atBats + _walks) + _totalBases / (double)_atBats;
            public double InningsPerAppearance => _pitchingAppearances == 0
                ? 0d
                : _outsRecorded / 3d / _pitchingAppearances;
            public double Era => _outsRecorded == 0 ? 0d : _earnedRuns * 27d / _outsRecorded;

            public void Add(PlayerSeasonStatisticsState statistics)
            {
                _teamGames += statistics.TeamGames;
                _gamesStarted += statistics.GamesStarted;
                _atBats += statistics.AtBats;
                _hits += statistics.Hits;
                _walks += statistics.Walks;
                _totalBases += statistics.Hits + statistics.Doubles +
                               statistics.Triples * 2 + statistics.HomeRuns * 3;
                _pitchingAppearances += statistics.PitchingAppearances;
                _outsRecorded += statistics.OutsRecorded;
                _earnedRuns += statistics.EarnedRuns;
            }
        }
    }
}

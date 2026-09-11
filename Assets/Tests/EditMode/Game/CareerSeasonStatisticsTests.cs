using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
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
            int gamesPerTeam = configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam;
            int rotationSize = configuration.Balance.CareerSeason.StartingRotationSize;
            // 144경기의 5인 순환은 일부 순번에 29경기를 배분하므로 정확히 20%가 상한이 아니다.
            double maximumStarterRate = ((gamesPerTeam + rotationSize - 1) / rotationSize) / (double)gamesPerTeam;
            AddOutOfRange(failures, "SP AppearanceRate", starter.AppearanceRate, 0.02d, maximumStarterRate);
            AddSeasonMeanOutOfRange(failures, "SP IP/App", starter.SeasonInningsPerAppearance, 5.5d, 6.5d);
            AddOutOfRange(failures, "SP ERA", starter.Era, 1.5d, 7.0d);
            AddSeasonMeanOutOfRange(failures, "RP AppearanceRate", reliever.SeasonAppearanceRates, 0.10d, 0.90d);
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
                int evaluationStarts = 0;
                foreach (ScheduledGameState game in career.CurrentLeague.CurrentSeason.Schedule.Games)
                    if (game.HasPlayerRoleDecision &&
                        game.PlayerRoleDecision.Reason == ManagerUsageDecisionReason.EvaluationOpportunity &&
                        game.PlayerRoleDecision.Role == PlayerGameRole.StartingBatter)
                        evaluationStarts++;
                Assert.That(evaluationStarts, Is.GreaterThan(0), $"Seed {seed}: 계약상 최소 평가 기회");
                Assert.That(statistics.GamesPlayed, Is.GreaterThanOrEqualTo(statistics.GamesStarted));
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
            // 평가 기회는 선발로 보장된다. 상세 경기의 대타 기용은 상대 능력 우세가 있는 별도 사례에서 검증한다.
        }

        [Test]
        public void Simulate_타격이강한벤치선수의상세경기교체출전을시즌기록에누적한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateStartedCareer(configuration, PlayerPosition.Shortstop, 10_000UL,
                new BatterAttributes(90, 90, 40, 40, 60, 40));
            var service = new CareerSeasonService(career, configuration.Balance);
            while (service.NextPlayerGame != null)
            {
                // 경기 중 교체와 집계를 분리 검증하기 위해 경기 전 선발 경쟁만 고정한다.
                service.NextPlayerGame.PlanPlayerRole(PlayerGameRole.Bench);
                service.AdvanceNextRound();
            }
            PlayerSeasonStatisticsState statistics = career.CurrentLeague.CurrentSeason.PlayerStatistics;
            Assert.That(statistics.GamesStarted, Is.Zero);
            Assert.That(statistics.GamesPlayed, Is.GreaterThan(0));
            Assert.That(statistics.AtBats, Is.GreaterThan(0));
            System.Console.WriteLine($"Detailed bench / G {statistics.GamesPlayed} / AB {statistics.AtBats}");
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
                Assert.That(record.GamesPlayed, Is.EqualTo(configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam));
                leagueRuns += record.RunsScored;
                leagueTeamGames += record.GamesPlayed;
            }
            if (position == PlayerPosition.StartingPitcher)
            {
                int games = configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam;
                int rotation = configuration.Balance.CareerSeason.StartingRotationSize;
                Assert.That(career.CurrentLeague.CurrentSeason.PlayerStatistics.PitchingAppearances,
                    Is.LessThanOrEqualTo((games + rotation - 1) / rotation), $"Seed {seed}: 고정 로테이션 등판 상한");
            }
            totals.Add(career.CurrentLeague.CurrentSeason.PlayerStatistics);
        }

        private static CareerState CreateStartedCareer(
            NewGameConfiguration configuration,
            PlayerPosition position,
            ulong seed,
            BatterAttributes? batterAttributes = null)
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
                flow.SubmitBatterAttributes(batterAttributes ?? new BatterAttributes(63, 58, 60, 53, 66, 60));
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

        private static void AddSeasonMeanOutOfRange(
            ICollection<string> failures, string metric, IReadOnlyList<double> seasons, double minimum, double maximum)
        {
            // 경기들은 같은 선수의 피로·컨디션을 공유한다. 독립 표본인 15개 시즌의 평균과 95% t 구간(df=14)을 쓴다.
            Assert.That(seasons.Count, Is.EqualTo(SeasonsPerRole));
            double mean = 0d;
            foreach (double value in seasons) mean += value;
            mean /= seasons.Count;
            double squaredDeviations = 0d;
            foreach (double value in seasons) squaredDeviations += (value - mean) * (value - mean);
            double margin = 2.1447866879d * System.Math.Sqrt(squaredDeviations / (seasons.Count - 1) / seasons.Count);
            Assert.That(margin, Is.LessThan((maximum - minimum) / 2d), $"{metric}: 표본 불확실성이 너무 커 판정할 수 없습니다.");
            System.Console.WriteLine($"{metric} season mean {mean:F4}, 95% CI {mean - margin:F4}..{mean + margin:F4}, target {minimum:F3}..{maximum:F3}");
            if (mean + margin < minimum || mean - margin > maximum)
                failures.Add($"{metric}: 95% 구간 {mean - margin:F3}..{mean + margin:F3}, 목표 {minimum:F3}..{maximum:F3}");
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
            public List<double> SeasonAppearanceRates { get; } = new();
            public List<double> SeasonInningsPerAppearance { get; } = new();
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
                if (statistics.PitchingAppearances > 0)
                {
                    SeasonAppearanceRates.Add(statistics.PitchingAppearances / (double)statistics.TeamGames);
                    SeasonInningsPerAppearance.Add(statistics.OutsRecorded / 3d / statistics.PitchingAppearances);
                }
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

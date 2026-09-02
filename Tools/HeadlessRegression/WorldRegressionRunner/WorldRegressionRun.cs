using System;
using System.Collections.Generic;
using System.Diagnostics;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.Diagnostics;
using Baseball.Game.Career.News;
using Baseball.Simulation.Career;

namespace Baseball.Tools.WorldRegression
{
    /// <summary>한 번의 Headless 월드 회귀 실행이 남긴 측정값과 checksum이다.</summary>
    public sealed class WorldRegressionRun
    {
        public double WorldCreateMs;
        public double RegularSeasonMs;
        public double PostseasonMs;
        public double GrowthMs;
        public double TransitionMs;
        public double ChecksumMs;
        public double TotalSeconds;
        public int LeagueCount;
        public int TeamsPerLeague;
        public int SeasonCount;
        public long RegularGames;
        public long PostseasonGames;
        public long AllocatedBytes;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
        public string FinalWorldChecksum;
        public readonly List<string> SeasonChecksums = new();

        public double AutoCompletionMs => RegularSeasonMs + PostseasonMs;
        public long TotalGames => RegularGames + PostseasonGames;
    }

    /// <summary>
    /// 실제 Production Career/World 진행 경로만 사용해 다중 리그 장기 회귀를 실행한다.
    /// </summary>
    /// <remarks>
    /// 새 게임 생성·시즌 자동완료·성장 정산·오프시즌 전환 모두 게임이 실제로 쓰는 서비스를 그대로
    /// 호출한다. 벤치마크 전용 축약 시뮬레이션을 두면 측정값이 게임과 무관해지므로 만들지 않는다.
    /// </remarks>
    public static class WorldRegressionScenario
    {
        public static WorldRegressionRun Run(ulong worldSeed, int seasonCount, StageTimingSink sink)
        {
            if (seasonCount <= 0) throw new ArgumentOutOfRangeException(nameof(seasonCount));

            sink?.Reset();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var run = new WorldRegressionRun { SeasonCount = seasonCount };
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            var total = Stopwatch.StartNew();

            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var createStopwatch = Stopwatch.StartNew();
            CareerState career = CreateCareer(worldSeed);
            createStopwatch.Stop();
            run.WorldCreateMs = createStopwatch.Elapsed.TotalMilliseconds;
            run.LeagueCount = career.World.Leagues.Count;
            run.TeamsPerLeague = career.World.Leagues[0].Teams.Count;

            for (int season = 0; season < seasonCount; season++)
            {
                AdvanceOneSeason(career, configuration, run);

                // checksum은 회귀 검증용 계측이므로 게임 진행 시간에 포함시키지 않는다.
                total.Stop();
                var checksumStopwatch = Stopwatch.StartNew();
                run.SeasonChecksums.Add(CareerStateChecksum.Calculate(career));
                checksumStopwatch.Stop();
                run.ChecksumMs += checksumStopwatch.Elapsed.TotalMilliseconds;
                total.Start();
            }

            total.Stop();
            run.TotalSeconds = total.Elapsed.TotalSeconds;
            run.AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore;
            run.Gen0Collections = GC.CollectionCount(0) - gen0;
            run.Gen1Collections = GC.CollectionCount(1) - gen1;
            run.Gen2Collections = GC.CollectionCount(2) - gen2;
            run.FinalWorldChecksum = run.SeasonChecksums[run.SeasonChecksums.Count - 1];
            return run;
        }

        /// <summary>
        /// 정규시즌 자동완료 → 포스트시즌 자동완료 → 성장 정산 → 오프시즌 전환까지 한 시즌을 돌린다.
        /// </summary>
        private static void AdvanceOneSeason(
            CareerState career,
            NewGameConfiguration configuration,
            WorldRegressionRun run)
        {
            var autoCompletion = new CareerSeasonAutoCompletionService(
                career,
                configuration.Balance,
                CareerNewsConfiguration.CreateDefault());

            var stopwatch = Stopwatch.StartNew();
            autoCompletion.CompleteCurrentPhase();
            stopwatch.Stop();
            run.RegularSeasonMs += stopwatch.Elapsed.TotalMilliseconds;
            run.RegularGames += CountRegularSeasonGames(career);

            stopwatch.Restart();
            autoCompletion.CompleteCurrentPhase();
            stopwatch.Stop();
            run.PostseasonMs += stopwatch.Elapsed.TotalMilliseconds;
            run.PostseasonGames += CountPostseasonGames(career);

            stopwatch.Restart();
            new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());
            stopwatch.Stop();
            run.GrowthMs += stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            new CareerSeasonTransitionService(career, configuration.Balance).AdvanceToNextSeason();
            stopwatch.Stop();
            run.TransitionMs += stopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>월드 전체 리그의 정규시즌 경기 수를 센다. 한 경기가 두 구단 기록에 잡히므로 절반으로 나눈다.</summary>
        private static long CountRegularSeasonGames(CareerState career)
        {
            long games = 0;
            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                IReadOnlyList<TeamSeasonRecordState> records =
                    career.World.Leagues[leagueIndex].CurrentSeason.TeamRecords;
                for (int index = 0; index < records.Count; index++)
                    games += records[index].GamesPlayed;
            }
            return games / 2;
        }

        private static long CountPostseasonGames(CareerState career)
        {
            long games = 0;
            for (int leagueIndex = 0; leagueIndex < career.World.Leagues.Count; leagueIndex++)
            {
                PostseasonState postseason = career.World.Leagues[leagueIndex].CurrentSeason.Postseason;
                if (postseason == null)
                    continue;
                for (int index = 0; index < postseason.Series.Count; index++)
                {
                    PostseasonSeriesState series = postseason.Series[index];
                    games += series.HigherSeedWins + series.LowerSeedWins;
                }
            }
            return games;
        }

        private static CareerState CreateCareer(ulong seed)
        {
            var flow = new NewGameFlow(NewGameConfiguration.CreateDefault(), seed);
            flow.SubmitIdentity("회귀 러너", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 50, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
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

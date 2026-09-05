using System;
using Baseball.Core.Balance;
using Baseball.Game.Historical;
using NUnit.Framework;
using UnityEditor;

namespace Baseball.Tests.EditMode.Editor.Historical
{
    /// <summary>Player Build와 같은 Runtime payload로 1982~2025 전체 World를 실행하는 수동 장기 검증이다.</summary>
    public sealed class HistoricalWorldFullValidationTests
    {
        private const string CatalogAssetPath =
            "Assets/10.Datas/HistoricalSimulation/HistoricalRuntimeContentCatalog.asset";

        [Test]
        [Explicit("Runtime payload를 실제 Detailed 경기로 44시즌 4회 실행하는 수동 장기 검증입니다.")]
        [Category("LongRunning")]
        [Timeout(900000)]
        public void FullRuntimePayload_1982To2025_CompletesAndReportsMetrics()
        {
            HistoricalRuntimeContentCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(
                    CatalogAssetPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "Historical Runtime Content Export를 먼저 실행해 Player payload catalog를 생성해야 합니다.");

            HistoricalBakedContent content = new UnityHistoricalContentProvider(catalog).Load();
            Assert.That(
                content.Manifest.ContentSchemaVersion,
                Is.EqualTo(UnityHistoricalContentProvider.SupportedContentSchemaVersion));
            Assert.That(content.Years.Count, Is.EqualTo(44));
            Assert.That(content.Years[0].Year, Is.EqualTo(1982));
            Assert.That(content.Years[content.Years.Count - 1].Year, Is.EqualTo(2025));

            try
            {
                HistoricalWorldValidationReport report = HistoricalWorldLongValidationHarness.Run(
                    content,
                    BalanceTable.CreateDefault(),
                    new[]
                    {
                        new HistoricalWorldValidationSeed(20260902UL, 20260902UL),
                        new HistoricalWorldValidationSeed(20260902UL, 20260902UL),
                        new HistoricalWorldValidationSeed(20260903UL, 20260903UL),
                        new HistoricalWorldValidationSeed(20260902UL, 20260903UL)
                    });
                HistoricalWorldValidationRun run = report.Runs[0];

                for (int index = 0; index < run.Metrics.Seasons.Count; index++)
                {
                    HistoricalSeasonSimulationMetrics season = run.Metrics.Seasons[index];
                    TestContext.WriteLine(
                        $"HistoricalWorldYear Year={season.SeasonYear} " +
                        $"Games={season.TotalGameCount} " +
                        $"Regular={season.RegularSeasonGameCount} " +
                        $"AllStar={season.AllStarGameCount} " +
                        $"Postseason={season.PostseasonGameCount} " +
                        $"ElapsedMs={season.ElapsedMilliseconds:F1} " +
                        $"AllocatedBytes={season.AllocatedBytes}");
                }

                TestContext.WriteLine(
                    $"HistoricalWorldFull Seasons={run.Metrics.Seasons.Count} " +
                    $"Games={run.Metrics.TotalGameCount} " +
                    $"SimulationElapsedMs={run.Metrics.HistoricalSimulationMilliseconds:F1} " +
                    $"TotalElapsedMs={run.Metrics.TotalElapsedMilliseconds:F1} " +
                    $"MsPerGame={run.Metrics.MillisecondsPerGame:F3} " +
                    $"AllocatedBytes={run.Metrics.AllocatedBytes} " +
                    $"AwardCount={run.AwardCount} FailedSeasonCount=0 " +
                    $"ResultHash={run.ResultHash} HistoryHash={run.Fingerprints.HistoryHash} " +
                    $"SaveRoundTripHash={run.RestoredHistoryHash} " +
                    $"ContentHash={content.Manifest.ContentHash}");

                Assert.That(run.Metrics.Seasons.Count, Is.EqualTo(44));
                // 경기 수는 연도별 실제 구단 수에 비례한다. KBO는 1982년 6구단으로 출발해
                // 2015년에야 10구단이 되었으므로 "44시즌 x 10구단"을 전제한 고정 하한을 쓰면
                // 정본 Archive에서 영원히 실패한다. TeamSeason 하나당 최소 경기 수로 표현한다.
                Assert.That(
                    run.Metrics.TotalGameCount,
                    Is.GreaterThanOrEqualTo(content.TeamSeasons.Count * 40));
                Assert.That(run.AwardCount, Is.EqualTo(44 * 38));
                Assert.That(run.ReplacementAwards.AllStarCount, Is.EqualTo(44 * 25));
                Assert.That(run.ReplacementAwards.GoldenGloveCount, Is.EqualTo(44 * 10));
                Assert.That(run.ReplacementAwards.MvpCount, Is.EqualTo(44 * 3));
                Assert.That(run.TeamStatisticsCount, Is.EqualTo(content.TeamSeasons.Count));
                Assert.That(run.StandingsCount, Is.EqualTo(content.TeamSeasons.Count));
                Assert.That(run.PostseasonResultCount, Is.EqualTo(content.Years.Count));
                Assert.That(run.ResultHash, Has.Length.EqualTo(16));
                Assert.That(report.Runs[1].ResultHash, Is.EqualTo(run.ResultHash));
                Assert.That(report.Runs[1].Fingerprints.HistoryHash, Is.EqualTo(run.Fingerprints.HistoryHash));
                Assert.That(report.Runs[1].Fingerprints.PlayerStatisticsHash, Is.EqualTo(run.Fingerprints.PlayerStatisticsHash));
                Assert.That(report.Runs[1].Fingerprints.TeamStatisticsHash, Is.EqualTo(run.Fingerprints.TeamStatisticsHash));
                Assert.That(report.Runs[1].Fingerprints.StandingsHash, Is.EqualTo(run.Fingerprints.StandingsHash));
                Assert.That(report.Runs[1].Fingerprints.AwardsHash, Is.EqualTo(run.Fingerprints.AwardsHash));
                Assert.That(report.Runs[1].Fingerprints.IdentityHash, Is.EqualTo(run.Fingerprints.IdentityHash));
                Assert.That(report.Runs[2].Fingerprints.PlayerStatisticsHash, Is.Not.EqualTo(run.Fingerprints.PlayerStatisticsHash));
                Assert.That(report.Runs[2].Fingerprints.TeamStatisticsHash, Is.Not.EqualTo(run.Fingerprints.TeamStatisticsHash));
                Assert.That(report.Runs[2].Fingerprints.StandingsHash, Is.Not.EqualTo(run.Fingerprints.StandingsHash));
                Assert.That(report.Runs[2].Fingerprints.AwardsHash, Is.Not.EqualTo(run.Fingerprints.AwardsHash));
                Assert.That(report.Runs[2].Fingerprints.IdentityHash, Is.Not.EqualTo(run.Fingerprints.IdentityHash));
                Assert.That(report.Runs[3].Fingerprints.IdentityHash, Is.Not.EqualTo(run.Fingerprints.IdentityHash));
                Assert.That(report.Runs[3].Fingerprints.HistoryHash, Is.EqualTo(run.Fingerprints.HistoryHash));
                Assert.That(report.Runs[3].Fingerprints.PlayerStatisticsHash, Is.EqualTo(run.Fingerprints.PlayerStatisticsHash));
                Assert.That(report.Runs[3].Fingerprints.TeamStatisticsHash, Is.EqualTo(run.Fingerprints.TeamStatisticsHash));
                Assert.That(report.Runs[3].Fingerprints.StandingsHash, Is.EqualTo(run.Fingerprints.StandingsHash));
                Assert.That(report.Runs[3].Fingerprints.AwardsHash, Is.EqualTo(run.Fingerprints.AwardsHash));
                for (int runIndex = 0; runIndex < report.Runs.Count; runIndex++)
                    Assert.That(report.Runs[runIndex].IsSaveRoundTripStable, Is.True);
                TestContext.WriteLine(
                    $"HistoricalWorldDeterminism SameSeedHistory={run.Fingerprints.HistoryHash} " +
                    $"RepeatHistory={report.Runs[1].Fingerprints.HistoryHash} " +
                    $"DifferentSeedHistory={report.Runs[2].Fingerprints.HistoryHash} " +
                    $"Statistics={run.Fingerprints.PlayerStatisticsHash}/{report.Runs[2].Fingerprints.PlayerStatisticsHash} " +
                    $"Standings={run.Fingerprints.StandingsHash}/{report.Runs[2].Fingerprints.StandingsHash} " +
                    $"Awards={run.Fingerprints.AwardsHash}/{report.Runs[2].Fingerprints.AwardsHash} " +
                    $"RenamedIdentity={report.Runs[3].Fingerprints.IdentityHash} " +
                    $"RenamedHistory={report.Runs[3].Fingerprints.HistoryHash}");
                TestContext.WriteLine(
                    $"HistoricalWorldReplacementAwards " +
                    $"PlayerSeasons={run.ReplacementAwards.ReplacementPlayerSeasonCount}/{run.ReplacementAwards.PlayerSeasonCount} " +
                    $"AllStar={run.ReplacementAwards.ReplacementAllStarCount}/{run.ReplacementAwards.AllStarCount} " +
                    $"GoldenGlove={run.ReplacementAwards.ReplacementGoldenGloveCount}/{run.ReplacementAwards.GoldenGloveCount} " +
                    $"Mvp={run.ReplacementAwards.ReplacementMvpCount}/{run.ReplacementAwards.MvpCount}");
            }
            catch (Exception exception)
            {
                int? failedYear = (exception as HistoricalWorldSeasonSimulationException)?.SeasonYear;
                TestContext.WriteLine(
                    $"HistoricalWorldFull FailedSeasonCount=1 " +
                    $"FailedSeasons={(failedYear.HasValue ? failedYear.Value.ToString() : "unknown")} " +
                    $"ErrorType={exception.GetType().Name} " +
                    $"Error={exception.Message}");
                throw;
            }
        }
    }
}

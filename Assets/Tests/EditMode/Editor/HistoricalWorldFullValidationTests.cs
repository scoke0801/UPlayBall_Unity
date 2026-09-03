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
        [Explicit("Runtime payload를 실제 Detailed 경기로 44시즌 실행하는 수동 장기 검증입니다.")]
        [Category("LongRunning")]
        [Timeout(600000)]
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
            Assert.That(content.Years.Count, Is.EqualTo(44));
            Assert.That(content.Years[0].Year, Is.EqualTo(1982));
            Assert.That(content.Years[content.Years.Count - 1].Year, Is.EqualTo(2025));

            try
            {
                HistoricalWorldValidationReport report = HistoricalWorldLongValidationHarness.Run(
                    content,
                    BalanceTable.CreateDefault(),
                    new[] { 20260902UL, 20260902UL, 20260903UL });
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
                    $"ResultHash={run.ResultHash} " +
                    $"ContentHash={content.Manifest.ContentHash}");

                Assert.That(run.Metrics.Seasons.Count, Is.EqualTo(44));
                Assert.That(run.Metrics.TotalGameCount, Is.GreaterThanOrEqualTo(44 * 401));
                Assert.That(run.AwardCount, Is.EqualTo(44 * 38));
                Assert.That(run.ResultHash, Has.Length.EqualTo(16));
                Assert.That(report.Runs[1].ResultHash, Is.EqualTo(run.ResultHash));
                Assert.That(report.Runs[2].ResultHash, Is.Not.EqualTo(run.ResultHash));
                TestContext.WriteLine(
                    $"HistoricalWorldDeterminism SameSeedHash={run.ResultHash} " +
                    $"RepeatHash={report.Runs[1].ResultHash} " +
                    $"DifferentSeedHash={report.Runs[2].ResultHash}");
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

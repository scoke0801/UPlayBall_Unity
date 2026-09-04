using System;
using Baseball.Core.Balance;
using Baseball.Editor.Tools;
using Baseball.Game.Historical;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Unity Test Runner의 전역 prebuild와 분리해 실제 Runtime payload 장기 검증을 실행한다.</summary>
    public static class HistoricalWorldFullValidationRunner
    {
        private const double MinimumReplacementAwardShareLimit = 0.05d;
        private const double ReplacementAwardShareMultiplier = 2d;
        private const string CatalogAssetPath =
            "Assets/10.Datas/HistoricalSimulation/HistoricalRuntimeContentCatalog.asset";

        [BaseballEditorTool(
            "검증",
            "Full Historical World Validation",
            "Runtime payload로 1982~2025 전체를 동일 Seed 반복과 다른 Seed로 검증합니다.",
            order: 30,
            impact: ToolImpact.ReadOnly)]
        public static void RunFromToolLauncher()
        {
            HistoricalRuntimeContentCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(CatalogAssetPath);
            if (catalog == null)
                throw new InvalidOperationException($"Runtime Historical Content Catalog를 찾을 수 없습니다: {CatalogAssetPath}");

            HistoricalBakedContent content = new UnityHistoricalContentProvider(catalog).Load();
            HistoricalWorldValidationReport report = HistoricalWorldLongValidationHarness.Run(
                content,
                BalanceTable.CreateDefault(),
                new[] { 20260902UL, 20260902UL, 20260903UL });

            HistoricalWorldValidationRun first = report.Runs[0];
            HistoricalWorldValidationRun repeat = report.Runs[1];
            HistoricalWorldValidationRun variation = report.Runs[2];
            if (!string.Equals(first.ResultHash, repeat.ResultHash, StringComparison.Ordinal))
                throw new InvalidOperationException("동일 WorldHistorySeed의 Full Historical World 결과가 다릅니다.");
            if (string.Equals(first.ResultHash, variation.ResultHash, StringComparison.Ordinal))
                throw new InvalidOperationException("서로 다른 WorldHistorySeed의 Full Historical World 결과가 같습니다.");
            if (first.Metrics.Seasons.Count != 44 || first.AwardCount != 44 * 38)
                throw new InvalidOperationException(
                    $"Full Historical World 집계가 예상과 다릅니다. " +
                    $"seasons={first.Metrics.Seasons.Count}, awards={first.AwardCount}");
            HistoricalReplacementAwardMetrics replacementAwards = first.ReplacementAwards;
            double maximumReplacementAwardShare = Math.Max(
                MinimumReplacementAwardShareLimit,
                replacementAwards.ReplacementPlayerSeasonShare * ReplacementAwardShareMultiplier);
            if (replacementAwards.ReplacementAllStarShare > maximumReplacementAwardShare ||
                replacementAwards.ReplacementGoldenGloveShare > maximumReplacementAwardShare ||
                replacementAwards.ReplacementMvpShare > maximumReplacementAwardShare)
            {
                throw new InvalidOperationException(
                    $"ReplacementGenerated가 Simulation 수상을 과도하게 점유합니다. " +
                    $"limit={maximumReplacementAwardShare:P2}, " +
                    $"allStar={replacementAwards.ReplacementAllStarShare:P2}, " +
                    $"goldenGlove={replacementAwards.ReplacementGoldenGloveShare:P2}, " +
                    $"mvp={replacementAwards.ReplacementMvpShare:P2}");
            }

            for (int index = 0; index < first.Metrics.Seasons.Count; index++)
            {
                HistoricalSeasonSimulationMetrics season = first.Metrics.Seasons[index];
                Debug.Log(
                    $"HistoricalWorldYear Year={season.SeasonYear} Games={season.TotalGameCount} " +
                    $"Regular={season.RegularSeasonGameCount} AllStar={season.AllStarGameCount} " +
                    $"Postseason={season.PostseasonGameCount} ElapsedMs={season.ElapsedMilliseconds:F1} " +
                    $"AllocatedBytes={season.AllocatedBytes}");
            }

            Debug.Log(
                $"HistoricalWorldFull Seasons={first.Metrics.Seasons.Count} " +
                $"Games={first.Metrics.TotalGameCount} " +
                $"SimulationElapsedMs={first.Metrics.HistoricalSimulationMilliseconds:F1} " +
                $"TotalElapsedMs={first.Metrics.TotalElapsedMilliseconds:F1} " +
                $"MsPerGame={first.Metrics.MillisecondsPerGame:F3} " +
                $"AllocatedBytes={first.Metrics.AllocatedBytes} " +
                $"AwardCount={first.AwardCount} FailedSeasonCount=0 " +
                $"ResultHash={first.ResultHash} ContentHash={content.Manifest.ContentHash}");
            Debug.Log(
                $"HistoricalWorldDeterminism SameSeedHash={first.ResultHash} " +
                $"RepeatHash={repeat.ResultHash} DifferentSeedHash={variation.ResultHash} " +
                "UniqueSeeds=2 FullRuns=3");
            Debug.Log(
                $"HistoricalWorldReplacementAwards " +
                $"PlayerSeasons={replacementAwards.ReplacementPlayerSeasonCount}/{replacementAwards.PlayerSeasonCount} " +
                $"PlayerSeasonShare={replacementAwards.ReplacementPlayerSeasonShare:P4} " +
                $"AllStar={replacementAwards.ReplacementAllStarCount}/{replacementAwards.AllStarCount} " +
                $"AllStarShare={replacementAwards.ReplacementAllStarShare:P4} " +
                $"GoldenGlove={replacementAwards.ReplacementGoldenGloveCount}/{replacementAwards.GoldenGloveCount} " +
                $"GoldenGloveShare={replacementAwards.ReplacementGoldenGloveShare:P4} " +
                $"Mvp={replacementAwards.ReplacementMvpCount}/{replacementAwards.MvpCount} " +
                $"MvpShare={replacementAwards.ReplacementMvpShare:P4} " +
                $"MaximumAllowedShare={maximumReplacementAwardShare:P4}");
        }
    }
}

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
            if (content.Manifest.ContentSchemaVersion != UnityHistoricalContentProvider.SupportedContentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Full Historical World 검증은 최신 Runtime schema를 요구합니다. " +
                    $"loaded={content.Manifest.ContentSchemaVersion}, " +
                    $"supported={UnityHistoricalContentProvider.SupportedContentSchemaVersion}");
            }
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

            HistoricalWorldValidationRun first = report.Runs[0];
            HistoricalWorldValidationRun repeat = report.Runs[1];
            HistoricalWorldValidationRun variation = report.Runs[2];
            HistoricalWorldValidationRun renamed = report.Runs[3];
            ValidateSameSeed(first, repeat);
            ValidateDifferentSeed(first, variation);
            ValidateIdentityIndependence(first, renamed);
            ValidateSaveRoundTrip(report);
            if (first.Metrics.Seasons.Count != 44 || first.AwardCount != 44 * 38)
                throw new InvalidOperationException(
                    $"Full Historical World 집계가 예상과 다릅니다. " +
                    $"seasons={first.Metrics.Seasons.Count}, awards={first.AwardCount}");
            if (first.TeamStatisticsCount != content.TeamSeasons.Count ||
                first.StandingsCount != content.TeamSeasons.Count ||
                first.PostseasonResultCount != content.Years.Count)
            {
                throw new InvalidOperationException(
                    $"Full Historical World 팀 기록 집계가 실제 archive와 다릅니다. " +
                    $"teamStatistics={first.TeamStatisticsCount}/{content.TeamSeasons.Count}, " +
                    $"standings={first.StandingsCount}/{content.TeamSeasons.Count}, " +
                    $"postseason={first.PostseasonResultCount}/{content.Years.Count}");
            }
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
                $"ResultHash={first.ResultHash} HistoryHash={first.Fingerprints.HistoryHash} " +
                $"SaveRoundTripHash={first.RestoredHistoryHash} " +
                $"ContentSchema={content.Manifest.ContentSchemaVersion} " +
                $"ContentHash={content.Manifest.ContentHash}");
            Debug.Log(
                $"HistoricalWorldDeterminism SameSeedHistory={first.Fingerprints.HistoryHash} " +
                $"RepeatHistory={repeat.Fingerprints.HistoryHash} " +
                $"DifferentSeedHistory={variation.Fingerprints.HistoryHash} " +
                $"PlayerStatistics={first.Fingerprints.PlayerStatisticsHash}/{variation.Fingerprints.PlayerStatisticsHash} " +
                $"TeamStatistics={first.Fingerprints.TeamStatisticsHash}/{variation.Fingerprints.TeamStatisticsHash} " +
                $"Standings={first.Fingerprints.StandingsHash}/{variation.Fingerprints.StandingsHash} " +
                $"Awards={first.Fingerprints.AwardsHash}/{variation.Fingerprints.AwardsHash} " +
                $"Identity={first.Fingerprints.IdentityHash}/{variation.Fingerprints.IdentityHash} " +
                $"RenamedIdentity={renamed.Fingerprints.IdentityHash} " +
                $"RenamedHistory={renamed.Fingerprints.HistoryHash} " +
                "UniqueSimulationSeeds=2 UniqueIdentitySeeds=2 FullRuns=4");
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

        private static void ValidateSameSeed(
            HistoricalWorldValidationRun first,
            HistoricalWorldValidationRun repeat)
        {
            if (!string.Equals(first.ResultHash, repeat.ResultHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.HistoryHash, repeat.Fingerprints.HistoryHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.PlayerStatisticsHash, repeat.Fingerprints.PlayerStatisticsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.TeamStatisticsHash, repeat.Fingerprints.TeamStatisticsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.StandingsHash, repeat.Fingerprints.StandingsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.AwardsHash, repeat.Fingerprints.AwardsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.IdentityHash, repeat.Fingerprints.IdentityHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "동일 WorldHistorySeed의 통계·순위·수상·Identity 결과가 결정론적으로 일치하지 않습니다.");
            }
        }

        private static void ValidateDifferentSeed(
            HistoricalWorldValidationRun first,
            HistoricalWorldValidationRun variation)
        {
            if (string.Equals(first.Fingerprints.PlayerStatisticsHash, variation.Fingerprints.PlayerStatisticsHash, StringComparison.Ordinal) ||
                string.Equals(first.Fingerprints.TeamStatisticsHash, variation.Fingerprints.TeamStatisticsHash, StringComparison.Ordinal) ||
                string.Equals(first.Fingerprints.StandingsHash, variation.Fingerprints.StandingsHash, StringComparison.Ordinal) ||
                string.Equals(first.Fingerprints.AwardsHash, variation.Fingerprints.AwardsHash, StringComparison.Ordinal) ||
                string.Equals(first.Fingerprints.IdentityHash, variation.Fingerprints.IdentityHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "서로 다른 WorldHistorySeed가 통계·순위·수상·Identity 중 하나 이상을 바꾸지 못했습니다.");
            }
        }

        private static void ValidateSaveRoundTrip(HistoricalWorldValidationReport report)
        {
            for (int index = 0; index < report.Runs.Count; index++)
            {
                if (!report.Runs[index].IsSaveRoundTripStable)
                {
                    throw new InvalidOperationException(
                        $"World History Save/Load round-trip이 기록을 변경했습니다. seed={report.Runs[index].WorldHistorySeed}");
                }
            }
        }

        private static void ValidateIdentityIndependence(
            HistoricalWorldValidationRun first,
            HistoricalWorldValidationRun renamed)
        {
            if (string.Equals(first.Fingerprints.IdentityHash, renamed.Fingerprints.IdentityHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Identity Seed를 바꿨지만 Full Historical World 표시 Identity가 같습니다.");
            if (!string.Equals(first.Fingerprints.HistoryHash, renamed.Fingerprints.HistoryHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.PlayerStatisticsHash, renamed.Fingerprints.PlayerStatisticsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.TeamStatisticsHash, renamed.Fingerprints.TeamStatisticsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.StandingsHash, renamed.Fingerprints.StandingsHash, StringComparison.Ordinal) ||
                !string.Equals(first.Fingerprints.AwardsHash, renamed.Fingerprints.AwardsHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "DisplayName만 바꾼 Full Historical World에서 통계·순위·수상이 달라졌습니다.");
            }
        }
    }
}

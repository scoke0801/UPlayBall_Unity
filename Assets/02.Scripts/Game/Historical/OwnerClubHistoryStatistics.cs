using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    /// <summary>구단 소속 기간의 원본 합계로 통산 비율을 계산한다.</summary>
    public sealed class OwnerClubHistoryTotals
    {
        public long AtBats { get; private set; }
        public long Hits { get; private set; }
        public long HomeRuns { get; private set; }
        public long StolenBases { get; private set; }
        public long OutsRecorded { get; private set; }
        public long EarnedRuns { get; private set; }
        public long Strikeouts { get; private set; }
        public double BattingAverage => AtBats == 0 ? 0 : Hits / (double)AtBats;
        public double EarnedRunAverage => OutsRecorded == 0 ? 0 : EarnedRuns * 27d / OutsRecorded;

        /// <summary>시즌별 비율을 평균 내지 않고 분자와 분모를 합산한다.</summary>
        public void Add(OwnerClubHistoryTotals other)
        {
            AtBats += other.AtBats; Hits += other.Hits; HomeRuns += other.HomeRuns;
            StolenBases += other.StolenBases; OutsRecorded += other.OutsRecorded;
            EarnedRuns += other.EarnedRuns; Strikeouts += other.Strikeouts;
        }

        internal void Add(BattingStatisticsState batting, PitchingStatisticsState pitching)
        {
            AtBats += batting.AtBats; Hits += batting.Hits; HomeRuns += batting.HomeRuns;
            StolenBases += batting.StolenBases; OutsRecorded += pitching.OutsRecorded;
            EarnedRuns += pitching.EarnedRuns; Strikeouts += pitching.Strikeouts;
        }
    }

    /// <summary>최고 기록에서 달성자와 시즌을 잃지 않는 한 시즌의 후보 기록이다.</summary>
    public sealed class OwnerClubRecordCandidate
    {
        public OwnerClubRecordCandidate(int playerId, string playerName, CareerRecordMetric metric, double value)
        { PlayerId = playerId; PlayerName = playerName; Metric = metric; Value = value; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public CareerRecordMetric Metric { get; }
        public double Value { get; }
    }

    /// <summary>완료 시즌의 구단별 기록만 읽고 시뮬레이션이나 저장 상태를 변경하지 않는다.</summary>
    public static class OwnerClubHistoryStatistics
    {
        public static readonly IReadOnlyList<CareerRecordMetric> RecordMetrics = Array.AsReadOnly(new[]
        {
            CareerRecordMetric.BattingAverage, CareerRecordMetric.HomeRuns, CareerRecordMetric.RunsBattedIn,
            CareerRecordMetric.Hits, CareerRecordMetric.StolenBases, CareerRecordMetric.Runs,
            CareerRecordMetric.EarnedRunAverage, CareerRecordMetric.PitchingStrikeouts, CareerRecordMetric.Wins,
            CareerRecordMetric.Saves, CareerRecordMetric.Holds, CareerRecordMetric.OutsRecorded
        });

        /// <summary>이적 전후 기록이 섞이지 않도록 구단별 Split을 우선한다.</summary>
        public static OwnerClubHistoryTotals CalculateTotals(ManagerLiveSeasonState season)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            var totals = new OwnerClubHistoryTotals();
            foreach (var player in season.Statistics.RegularSeason.Players.Values)
                if (TryGetClubStatistics(player, season.PlayerTeamId, out var batting, out var pitching))
                    totals.Add(batting, pitching);
            return totals;
        }

        /// <summary>확정 시즌만 최고 기록 후보로 삼고 비율 기록은 공통 규정 타석·이닝을 적용한다.</summary>
        public static IReadOnlyList<OwnerClubRecordCandidate> CollectRecords(ManagerLiveSeasonState season)
        {
            var result = new List<OwnerClubRecordCandidate>();
            if (!season.IsCompleted) return result.AsReadOnly();
            int games = season.GetCompletedGameCount(season.PlayerTeamId);
            foreach (var player in season.Statistics.RegularSeason.Players.Values)
            {
                if (!TryGetClubStatistics(player, season.PlayerTeamId, out var batting, out var pitching)) continue;
                foreach (var metric in RecordMetrics)
                {
                    if (metric == CareerRecordMetric.BattingAverage && (batting.AtBats == 0 ||
                        batting.PlateAppearances < Math.Ceiling(games * LeagueLeaderboardService.PlateAppearancesPerTeamGame))) continue;
                    if (metric == CareerRecordMetric.EarnedRunAverage && (pitching.OutsRecorded == 0 ||
                        pitching.OutsRecorded < games * LeagueLeaderboardService.PitchingOutsPerTeamGame)) continue;
                    double value = GetValue(metric, batting, pitching);
                    if (value <= 0 && metric != CareerRecordMetric.BattingAverage && metric != CareerRecordMetric.EarnedRunAverage) continue;
                    result.Add(new OwnerClubRecordCandidate(player.PlayerId, player.PlayerName, metric, value));
                }
            }
            result.Sort((a, b) => { int order = a.Metric.CompareTo(b.Metric); return order != 0 ? order : a.PlayerId.CompareTo(b.PlayerId); });
            return result.AsReadOnly();
        }

        private static bool TryGetClubStatistics(PlayerCompetitionStatisticsState player, int teamId,
            out BattingStatisticsState batting, out PitchingStatisticsState pitching)
        {
            var split = player.GetTeamSplit(teamId);
            batting = split?.Batting ?? player.Batting;
            pitching = split?.Pitching ?? player.Pitching;
            return split != null || player.TeamId == teamId;
        }

        private static double GetValue(CareerRecordMetric metric, BattingStatisticsState b, PitchingStatisticsState p)
        {
            return metric switch
            {
                CareerRecordMetric.BattingAverage => b.BattingAverage,
                CareerRecordMetric.HomeRuns => b.HomeRuns,
                CareerRecordMetric.RunsBattedIn => b.RunsBattedIn,
                CareerRecordMetric.Hits => b.Hits,
                CareerRecordMetric.StolenBases => b.StolenBases,
                CareerRecordMetric.Runs => b.Runs,
                CareerRecordMetric.EarnedRunAverage => p.EarnedRunAverage,
                CareerRecordMetric.PitchingStrikeouts => p.Strikeouts,
                CareerRecordMetric.Wins => p.Wins,
                CareerRecordMetric.Saves => p.Saves,
                CareerRecordMetric.Holds => p.Holds,
                CareerRecordMetric.OutsRecorded => p.OutsRecorded,
                _ => throw new ArgumentOutOfRangeException(nameof(metric))
            };
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Game.Career;

namespace Baseball.Game.Historical
{
    /// <summary>현재 조의 정규시즌 기록으로 만든 구단 합계와 개인 기록 타이틀이다.</summary>
    public sealed class OwnerSeasonHonorsReview
    {
        private static readonly CareerRecordMetric[] Metrics =
        {
            CareerRecordMetric.BattingAverage, CareerRecordMetric.HomeRuns,
            CareerRecordMetric.RunsBattedIn, CareerRecordMetric.StolenBases,
            CareerRecordMetric.EarnedRunAverage, CareerRecordMetric.Wins,
            CareerRecordMetric.PitchingStrikeouts, CareerRecordMetric.Saves
        };

        private OwnerSeasonHonorsReview() { }
        public int HomeRuns { get; private set; }
        public int StolenBases { get; private set; }
        public int AtBats { get; private set; }
        public int Hits { get; private set; }
        public int PitchingOuts { get; private set; }
        public int EarnedRuns { get; private set; }
        public double BattingAverage => AtBats == 0 ? 0d : Hits / (double)AtBats;
        public double EarnedRunAverage => PitchingOuts == 0 ? 0d : EarnedRuns * 27d / PitchingOuts;
        public bool IsFinal { get; private set; }
        public IReadOnlyList<OwnerRecordTitleReview> Titles { get; private set; }

        /// <summary>저장된 경기 기록만 읽어 경기 상태나 난수를 변경하지 않고 결산을 만든다.</summary>
        public static OwnerSeasonHonorsReview Create(ManagerLiveSeasonState season)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            var result = new OwnerSeasonHonorsReview { IsFinal = season.IsCompleted };
            foreach (var player in season.Statistics.RegularSeason.Players.Values)
            {
                if (player.TeamId != season.PlayerTeamId) continue;
                result.HomeRuns += player.Batting.HomeRuns;
                result.StolenBases += player.Batting.StolenBases;
                result.AtBats += player.Batting.AtBats;
                result.Hits += player.Batting.Hits;
                result.PitchingOuts += player.Pitching.OutsRecorded;
                result.EarnedRuns += player.Pitching.EarnedRuns;
            }
            var titles = new List<OwnerRecordTitleReview>();
            if (result.IsFinal)
                foreach (CareerRecordMetric metric in Metrics) AddTitle(season, metric, titles);
            result.Titles = titles.AsReadOnly();
            return result;
        }

        private static void AddTitle(ManagerLiveSeasonState season, CareerRecordMetric metric,
            List<OwnerRecordTitleReview> titles)
        {
            bool pitching = metric == CareerRecordMetric.EarnedRunAverage || metric == CareerRecordMetric.Wins ||
                metric == CareerRecordMetric.PitchingStrikeouts || metric == CareerRecordMetric.Saves;
            var eligible = new List<PlayerCompetitionStatisticsState>();
            foreach (var player in season.Statistics.RegularSeason.Players.Values)
            {
                if (pitching ? player.Pitching.Appearances == 0 : player.Batting.PlateAppearances == 0) continue;
                // 비율 타이틀은 기록실과 같은 규정 타석·이닝을 사용하고 누적 타이틀은 출전자 전체를 비교한다.
                if ((metric == CareerRecordMetric.BattingAverage || metric == CareerRecordMetric.EarnedRunAverage) &&
                    !LeagueLeaderboardService.IsQualified(player,
                        pitching ? CareerRecordCategory.Pitching : CareerRecordCategory.Batting,
                        season.GetCompletedGameCount(player.TeamId), CompetitionScope.RegularSeason)) continue;
                eligible.Add(player);
            }
            LeagueLeaderboardService.SortByMetric(eligible, metric);
            if (eligible.Count == 0) return;
            double best = LeagueLeaderboardService.GetMetricValue(eligible[0], metric);
            if (!LeagueLeaderboardService.IsLowerBetter(metric) && best <= 0d) return;
            var winners = new List<PlayerCompetitionStatisticsState>();
            foreach (var player in eligible)
                if (LeagueLeaderboardService.CalculateRank(eligible, player, metric) == 1) winners.Add(player);
            foreach (var player in winners)
                titles.Add(new OwnerRecordTitleReview(metric, player.PlayerName,
                    season.GetTeamSeasonKey(player.TeamId),
                    LeagueLeaderboardService.GetMetricValue(player, metric),
                    player.TeamId == season.PlayerTeamId, winners.Count > 1));
        }
    }

    /// <summary>공동 수상도 선수마다 한 행으로 보존하는 읽기 전용 기록 타이틀이다.</summary>
    public sealed class OwnerRecordTitleReview
    {
        public OwnerRecordTitleReview(CareerRecordMetric metric, string playerName, string teamSeasonKey,
            double value, bool isOurPlayer, bool isShared)
        {
            Metric = metric; PlayerName = playerName; TeamSeasonKey = teamSeasonKey;
            Value = value; IsOurPlayer = isOurPlayer; IsShared = isShared;
        }
        public CareerRecordMetric Metric { get; }
        public string PlayerName { get; }
        public string TeamSeasonKey { get; }
        public double Value { get; }
        public bool IsOurPlayer { get; }
        public bool IsShared { get; }
    }
}

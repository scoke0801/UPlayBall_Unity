using System;
using System.Collections.Generic;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Historical 선수 분석에 공통 적용할 선택 필터다.</summary>
    public sealed class HistoricalAnalysisFilter
    {
        public int? Year { get; set; }
        public string FranchiseId { get; set; }
        public string Position { get; set; }
        public string PitcherRole { get; set; }
        public string PlayerType { get; set; }
        public int? MinimumCost { get; set; }
        public int? MaximumCost { get; set; }

        /// <summary>선수 시즌 Row가 현재 분석 범위에 포함되는지 반환한다.</summary>
        public bool Matches(HistoricalPlayerRow row)
        {
            if (row == null || row.Season == null)
                return false;

            HistoricalPlayerSeason season = row.Season;
            bool matchesPitcherRole = string.IsNullOrWhiteSpace(PitcherRole) ||
                                      (row.IsPitcher && MatchesText(PitcherRole, season.PitcherRole));
            return (!Year.HasValue || season.OriginYear == Year.Value) &&
                   MatchesText(FranchiseId, season.OriginFranchiseId) &&
                   MatchesText(Position, season.Position) &&
                   matchesPitcherRole &&
                   MatchesText(PlayerType, season.PlayerType) &&
                   (!MinimumCost.HasValue || season.Cost >= MinimumCost.Value) &&
                   (!MaximumCost.HasValue || season.Cost <= MaximumCost.Value);
        }

        private static bool MatchesText(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected) ||
                   string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>범주 하나의 건수와 현재 분석 범위 내 비율을 보관한다.</summary>
    public readonly struct HistoricalDistributionBucket
    {
        public HistoricalDistributionBucket(string key, int count, double percentage)
        {
            Key = key ?? string.Empty;
            Count = count;
            Percentage = percentage;
        }

        public string Key { get; }
        public int Count { get; }
        public double Percentage { get; }
    }

    /// <summary>능력치 한 축의 기술 통계와 주요 백분위를 보관한다.</summary>
    public sealed class HistoricalAbilitySummary
    {
        public HistoricalAbilitySummary(
            int abilityIndex,
            string abilityName,
            int count,
            double minimum,
            double maximum,
            double mean,
            double median,
            double standardDeviation,
            double percentile10,
            double percentile50,
            double percentile90)
        {
            AbilityIndex = abilityIndex;
            AbilityName = abilityName ?? string.Empty;
            Count = count;
            Minimum = minimum;
            Maximum = maximum;
            Mean = mean;
            Median = median;
            StandardDeviation = standardDeviation;
            Percentile10 = percentile10;
            Percentile50 = percentile50;
            Percentile90 = percentile90;
        }

        public int AbilityIndex { get; }
        public string AbilityName { get; }
        public int Count { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public double Mean { get; }
        public double Median { get; }
        public double StandardDeviation { get; }
        public double Percentile10 { get; }
        public double Percentile50 { get; }
        public double Percentile90 { get; }
    }

    /// <summary>저장 또는 단순 산술 파생 시즌 지표 한 축의 기술 통계다.</summary>
    public sealed class HistoricalStatisticSummary
    {
        public HistoricalStatisticSummary(
            string statisticName,
            int count,
            double minimum,
            double maximum,
            double mean,
            double median,
            double standardDeviation,
            double percentile10,
            double percentile90)
        {
            StatisticName = statisticName ?? string.Empty;
            Count = count;
            Minimum = minimum;
            Maximum = maximum;
            Mean = mean;
            Median = median;
            StandardDeviation = standardDeviation;
            Percentile10 = percentile10;
            Percentile90 = percentile90;
        }

        public string StatisticName { get; }
        public int Count { get; }
        public double Minimum { get; }
        public double Maximum { get; }
        public double Mean { get; }
        public double Median { get; }
        public double StandardDeviation { get; }
        public double Percentile10 { get; }
        public double Percentile90 { get; }
    }

    /// <summary>분포와 능력치 요약을 한 번의 필터 적용으로 계산한 결과다.</summary>
    public sealed class HistoricalDatabaseAnalysisResult
    {
        public HistoricalDatabaseAnalysisResult(
            int playerCount,
            IReadOnlyList<HistoricalDistributionBucket> costDistribution,
            IReadOnlyList<HistoricalDistributionBucket> positionDistribution,
            IReadOnlyList<HistoricalDistributionBucket> pitcherRoleDistribution,
            IReadOnlyList<HistoricalDistributionBucket> awardDistribution,
            IReadOnlyList<HistoricalDistributionBucket> awardsByYear,
            IReadOnlyList<HistoricalDistributionBucket> awardsByPosition,
            IReadOnlyList<HistoricalDistributionBucket> awardsByCost,
            IReadOnlyList<HistoricalDistributionBucket> awardsByFranchise,
            IReadOnlyList<HistoricalAbilitySummary> abilities,
            IReadOnlyList<HistoricalStatisticSummary> seasonStatistics)
        {
            PlayerCount = playerCount;
            CostDistribution = costDistribution ?? throw new ArgumentNullException(nameof(costDistribution));
            PositionDistribution = positionDistribution ?? throw new ArgumentNullException(nameof(positionDistribution));
            PitcherRoleDistribution = pitcherRoleDistribution ?? throw new ArgumentNullException(nameof(pitcherRoleDistribution));
            AwardDistribution = awardDistribution ?? throw new ArgumentNullException(nameof(awardDistribution));
            AwardsByYear = awardsByYear ?? throw new ArgumentNullException(nameof(awardsByYear));
            AwardsByPosition = awardsByPosition ?? throw new ArgumentNullException(nameof(awardsByPosition));
            AwardsByCost = awardsByCost ?? throw new ArgumentNullException(nameof(awardsByCost));
            AwardsByFranchise = awardsByFranchise ?? throw new ArgumentNullException(nameof(awardsByFranchise));
            Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            SeasonStatistics = seasonStatistics ?? throw new ArgumentNullException(nameof(seasonStatistics));
        }

        public int PlayerCount { get; }
        public IReadOnlyList<HistoricalDistributionBucket> CostDistribution { get; }
        public IReadOnlyList<HistoricalDistributionBucket> PositionDistribution { get; }
        public IReadOnlyList<HistoricalDistributionBucket> PitcherRoleDistribution { get; }
        public IReadOnlyList<HistoricalDistributionBucket> AwardDistribution { get; }
        public IReadOnlyList<HistoricalDistributionBucket> AwardsByYear { get; }
        public IReadOnlyList<HistoricalDistributionBucket> AwardsByPosition { get; }
        public IReadOnlyList<HistoricalDistributionBucket> AwardsByCost { get; }
        public IReadOnlyList<HistoricalDistributionBucket> AwardsByFranchise { get; }
        public IReadOnlyList<HistoricalAbilitySummary> Abilities { get; }
        public IReadOnlyList<HistoricalStatisticSummary> SeasonStatistics { get; }
    }

    /// <summary>Historical Archive의 선수·수상·능력치 분포를 읽기 전용으로 계산한다.</summary>
    public sealed class HistoricalDatabaseAnalyzer
    {
        public const int AbilityCount = 12;

        private static readonly string[] AbilityNames =
        {
            "컨택",
            "장타력",
            "주력",
            "송구",
            "수비",
            "타자 멘탈",
            "체력",
            "구속",
            "구위",
            "변화구",
            "제구",
            "투수 멘탈"
        };

        /// <summary>필터된 선수 집합의 Cost·Position·Award와 능력치 분포를 계산한다.</summary>
        public HistoricalDatabaseAnalysisResult Analyze(
            HistoricalArchiveData archive,
            HistoricalAnalysisFilter filter = null,
            int? abilityIndex = null)
        {
            if (archive == null)
                throw new ArgumentNullException(nameof(archive));
            if (abilityIndex.HasValue)
                ValidateAbilityIndex(abilityIndex.Value);

            filter ??= new HistoricalAnalysisFilter();
            var rows = new List<HistoricalPlayerRow>();
            for (int index = 0; index < archive.PlayerRows.Count; index++)
            {
                HistoricalPlayerRow row = archive.PlayerRows[index];
                if (filter.Matches(row))
                    rows.Add(row);
            }

            return new HistoricalDatabaseAnalysisResult(
                rows.Count,
                BuildCostDistribution(rows),
                BuildTextDistribution(rows, row => row.Season.Position),
                BuildTextDistribution(rows, row => row.IsPitcher ? row.Season.PitcherRole : string.Empty),
                BuildAwardDistribution(rows, (row, award) => award.AwardType),
                BuildAwardDistribution(rows, (row, award) => award.SeasonYear.ToString()),
                BuildAwardDistribution(rows, (row, award) => award.Position),
                BuildAwardDistribution(rows, (row, award) => row.Cost.ToString()),
                BuildAwardDistribution(rows, (row, award) => row.OriginFranchiseId),
                BuildAbilitySummaries(rows, abilityIndex),
                BuildSeasonStatisticSummaries(rows));
        }

        /// <summary>지정한 능력치 한 축만 현재 필터에 따라 계산한다.</summary>
        public HistoricalAbilitySummary AnalyzeAbility(
            HistoricalArchiveData archive,
            int abilityIndex,
            HistoricalAnalysisFilter filter = null)
        {
            HistoricalDatabaseAnalysisResult result = Analyze(archive, filter, abilityIndex);
            return result.Abilities[0];
        }

        private static IReadOnlyList<HistoricalDistributionBucket> BuildCostDistribution(
            IReadOnlyList<HistoricalPlayerRow> rows)
        {
            var counts = new int[11];
            for (int index = 0; index < rows.Count; index++)
            {
                int cost = rows[index].Season.Cost;
                if (cost >= 1 && cost <= 10)
                    counts[cost]++;
            }

            var result = new HistoricalDistributionBucket[10];
            for (int cost = 1; cost <= 10; cost++)
                result[cost - 1] = CreateBucket(cost.ToString(), counts[cost], rows.Count);
            return result;
        }

        private static IReadOnlyList<HistoricalDistributionBucket> BuildTextDistribution(
            IReadOnlyList<HistoricalPlayerRow> rows,
            Func<HistoricalPlayerRow, string> keySelector)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            for (int index = 0; index < rows.Count; index++)
            {
                string key = keySelector(rows[index]);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
                total++;
            }

            var keys = new List<string>(counts.Keys);
            keys.Sort(CompareDistributionKeys);
            var result = new HistoricalDistributionBucket[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                result[index] = CreateBucket(key, counts[key], total);
            }
            return result;
        }

        private static IReadOnlyList<HistoricalDistributionBucket> BuildAwardDistribution(
            IReadOnlyList<HistoricalPlayerRow> rows,
            Func<HistoricalPlayerRow, HistoricalAwardRecord, string> keySelector)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                IReadOnlyList<HistoricalAwardRecord> awards = rows[rowIndex].Awards;
                if (awards == null)
                    continue;
                for (int awardIndex = 0; awardIndex < awards.Count; awardIndex++)
                {
                    HistoricalAwardRecord award = awards[awardIndex];
                    string key = award == null ? string.Empty : keySelector(rows[rowIndex], award);
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    counts.TryGetValue(key, out int count);
                    counts[key] = count + 1;
                    total++;
                }
            }

            var keys = new List<string>(counts.Keys);
            keys.Sort(CompareDistributionKeys);
            var result = new HistoricalDistributionBucket[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                result[index] = CreateBucket(key, counts[key], total);
            }
            return result;
        }

        private static IReadOnlyList<HistoricalAbilitySummary> BuildAbilitySummaries(
            IReadOnlyList<HistoricalPlayerRow> rows,
            int? selectedAbilityIndex)
        {
            if (selectedAbilityIndex.HasValue)
                return new[] { BuildAbilitySummary(rows, selectedAbilityIndex.Value) };

            var result = new HistoricalAbilitySummary[AbilityCount];
            for (int abilityIndex = 0; abilityIndex < result.Length; abilityIndex++)
                result[abilityIndex] = BuildAbilitySummary(rows, abilityIndex);
            return result;
        }

        private static IReadOnlyList<HistoricalStatisticSummary> BuildSeasonStatisticSummaries(
            IReadOnlyList<HistoricalPlayerRow> rows)
        {
            var result = new List<HistoricalStatisticSummary>(4);
            AddStatisticSummary(result, "타석당 안타 · 파생", rows, row => row.HitsPerPlateAppearance);
            AddStatisticSummary(
                result,
                "홈런 · 저장값",
                rows,
                row => row.IsHitter && row.Record != null ? row.Record.HomeRuns : (double?)null);
            AddStatisticSummary(result, "평균자책점 · 저장값 우선", rows, row => row.EarnedRunAverage);
            AddStatisticSummary(result, "9이닝당 삼진 · 파생", rows, row => row.StrikeoutsPerNine);
            return result;
        }

        private static void AddStatisticSummary(
            ICollection<HistoricalStatisticSummary> destination,
            string statisticName,
            IReadOnlyList<HistoricalPlayerRow> rows,
            Func<HistoricalPlayerRow, double?> valueSelector)
        {
            var values = new List<double>(rows.Count);
            for (int index = 0; index < rows.Count; index++)
            {
                double? value = valueSelector(rows[index]);
                if (value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value))
                    values.Add(value.Value);
            }
            if (values.Count == 0)
                return;

            values.Sort();
            double sum = 0d;
            for (int index = 0; index < values.Count; index++)
                sum += values[index];
            double mean = sum / values.Count;
            double squaredDifferenceSum = 0d;
            for (int index = 0; index < values.Count; index++)
            {
                double difference = values[index] - mean;
                squaredDifferenceSum += difference * difference;
            }

            destination.Add(new HistoricalStatisticSummary(
                statisticName,
                values.Count,
                values[0],
                values[values.Count - 1],
                mean,
                Percentile(values, 0.5d),
                Math.Sqrt(squaredDifferenceSum / values.Count),
                Percentile(values, 0.1d),
                Percentile(values, 0.9d)));
        }

        private static HistoricalAbilitySummary BuildAbilitySummary(
            IReadOnlyList<HistoricalPlayerRow> rows,
            int abilityIndex)
        {
            ValidateAbilityIndex(abilityIndex);
            var values = new List<int>(rows.Count);
            for (int index = 0; index < rows.Count; index++)
            {
                HistoricalPlayerRow row = rows[index];
                if (!MatchesAbilityFamily(row.Season.PlayerType, abilityIndex))
                    continue;
                int[] ratings = row.BaseAttributes;
                if (ratings != null && ratings.Length > abilityIndex)
                    values.Add(ratings[abilityIndex]);
            }

            if (values.Count == 0)
            {
                return new HistoricalAbilitySummary(
                    abilityIndex,
                    AbilityNames[abilityIndex],
                    0,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d);
            }

            values.Sort();
            double sum = 0d;
            for (int index = 0; index < values.Count; index++)
                sum += values[index];
            double mean = sum / values.Count;
            double squaredDifferenceSum = 0d;
            for (int index = 0; index < values.Count; index++)
            {
                double difference = values[index] - mean;
                squaredDifferenceSum += difference * difference;
            }

            return new HistoricalAbilitySummary(
                abilityIndex,
                AbilityNames[abilityIndex],
                values.Count,
                values[0],
                values[values.Count - 1],
                mean,
                Percentile(values, 0.5d),
                Math.Sqrt(squaredDifferenceSum / values.Count),
                Percentile(values, 0.1d),
                Percentile(values, 0.5d),
                Percentile(values, 0.9d));
        }

        private static bool MatchesAbilityFamily(string playerType, int abilityIndex)
        {
            return abilityIndex < 6
                ? string.Equals(playerType, "Hitter", StringComparison.OrdinalIgnoreCase)
                : string.Equals(playerType, "Pitcher", StringComparison.OrdinalIgnoreCase);
        }

        private static double Percentile(IReadOnlyList<int> sortedValues, double percentile)
        {
            if (sortedValues.Count == 1)
                return sortedValues[0];
            double rank = (sortedValues.Count - 1) * percentile;
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);
            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];
            double fraction = rank - lowerIndex;
            return sortedValues[lowerIndex] +
                   (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 1)
                return sortedValues[0];
            double rank = (sortedValues.Count - 1) * percentile;
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);
            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];
            double fraction = rank - lowerIndex;
            return sortedValues[lowerIndex] +
                   (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
        }

        private static HistoricalDistributionBucket CreateBucket(string key, int count, int total)
        {
            double percentage = total == 0 ? 0d : count * 100d / total;
            return new HistoricalDistributionBucket(key, count, percentage);
        }

        private static int CompareDistributionKeys(string left, string right)
        {
            if (int.TryParse(left, out int leftNumber) && int.TryParse(right, out int rightNumber))
                return leftNumber.CompareTo(rightNumber);
            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private static void ValidateAbilityIndex(int abilityIndex)
        {
            if (abilityIndex < 0 || abilityIndex >= AbilityCount)
                throw new ArgumentOutOfRangeException(nameof(abilityIndex));
        }
    }
}

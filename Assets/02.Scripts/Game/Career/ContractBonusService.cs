using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 계약 상여가 추적하는 실제 시즌 지표를 구분한다.
    /// </summary>
    public enum ContractBonusMetric
    {
        GamesPlayed,
        HomeRuns,
        RunsBattedIn,
        OnBasePlusSlugging,
        PitchingAppearances,
        PitchingOuts,
        PitchingStrikeouts,
        EarnedRunAverage,
        IndividualAward,
        Championship
    }

    /// <summary>
    /// 한 계약 상여의 고정 조건과 달성 보상을 나타낸다.
    /// </summary>
    public readonly struct ContractBonusClause
    {
        public ContractBonusClause(
            string clauseId,
            ContractBonusMetric metric,
            double targetValue,
            long reward,
            bool isLowerBetter = false)
        {
            ClauseId = clauseId ?? throw new ArgumentNullException(nameof(clauseId));
            Metric = metric;
            TargetValue = targetValue;
            Reward = reward;
            IsLowerBetter = isLowerBetter;
        }

        public string ClauseId { get; }
        public ContractBonusMetric Metric { get; }
        public double TargetValue { get; }
        public long Reward { get; }
        public bool IsLowerBetter { get; }
    }

    /// <summary>
    /// 계약 화면과 시즌 정산이 함께 소비하는 상여 달성 상태다.
    /// </summary>
    public readonly struct ContractBonusProgress
    {
        public ContractBonusProgress(
            ContractBonusClause clause,
            double currentValue,
            double normalizedProgress,
            bool isCompleted,
            bool hasSample)
        {
            Clause = clause;
            CurrentValue = currentValue;
            NormalizedProgress = normalizedProgress;
            IsCompleted = isCompleted;
            HasSample = hasSample;
        }

        public ContractBonusClause Clause { get; }
        public double CurrentValue { get; }
        public double NormalizedProgress { get; }
        public bool IsCompleted { get; }
        public bool HasSample { get; }
    }

    /// <summary>
    /// 타자·투수별 계약 상여 조건을 만들고 현재 시즌 원본 기록으로 달성도를 계산한다.
    /// </summary>
    public sealed class ContractBonusService
    {
        private readonly ContractBonusBalance _balance;

        public ContractBonusService(ContractBonusBalance balance)
        {
            _balance = balance;
        }

        /// <summary>
        /// 현재 계약 연봉과 리그 경기 수에 맞는 여섯 개 상여 조건을 만든다.
        /// </summary>
        public ContractBonusClause[] BuildClauses(
            PlayerPosition position,
            long annualSalary,
            int regularSeasonGames)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            long appearanceReward = CalculateReward(annualSalary, _balance.AppearanceSalaryRate);
            long countingReward = CalculateReward(annualSalary, _balance.CountingStatSalaryRate);
            long rateReward = CalculateReward(annualSalary, _balance.RateStatSalaryRate);
            long awardReward = CalculateReward(annualSalary, _balance.IndividualAwardSalaryRate);
            long championshipReward = CalculateReward(annualSalary, _balance.ChampionshipSalaryRate);

            if (isPitcher)
            {
                return new[]
                {
                    new ContractBonusClause(
                        "pitching_appearances", ContractBonusMetric.PitchingAppearances,
                        _balance.PitcherAppearanceTarget, appearanceReward),
                    new ContractBonusClause(
                        "pitching_outs", ContractBonusMetric.PitchingOuts,
                        _balance.PitcherOutsTarget, countingReward),
                    new ContractBonusClause(
                        "pitching_strikeouts", ContractBonusMetric.PitchingStrikeouts,
                        _balance.PitcherStrikeoutTarget, countingReward),
                    new ContractBonusClause(
                        "earned_run_average", ContractBonusMetric.EarnedRunAverage,
                        _balance.PitcherEraTarget, rateReward, isLowerBetter: true),
                    new ContractBonusClause(
                        "individual_award", ContractBonusMetric.IndividualAward, 1d, awardReward),
                    new ContractBonusClause(
                        "championship", ContractBonusMetric.Championship, 1d, championshipReward)
                };
            }

            int appearanceTarget = Math.Max(
                1,
                (int)Math.Round(
                    regularSeasonGames * _balance.AppearanceTargetRate,
                    MidpointRounding.AwayFromZero));
            return new[]
            {
                new ContractBonusClause(
                    "games_played", ContractBonusMetric.GamesPlayed, appearanceTarget, appearanceReward),
                new ContractBonusClause(
                    "home_runs", ContractBonusMetric.HomeRuns,
                    _balance.BatterHomeRunTarget, countingReward),
                new ContractBonusClause(
                    "runs_batted_in", ContractBonusMetric.RunsBattedIn,
                    _balance.BatterRunsBattedInTarget, countingReward),
                new ContractBonusClause(
                    "on_base_plus_slugging", ContractBonusMetric.OnBasePlusSlugging,
                    _balance.BatterOpsTarget, rateReward),
                new ContractBonusClause(
                    "individual_award", ContractBonusMetric.IndividualAward, 1d, awardReward),
                new ContractBonusClause(
                    "championship", ContractBonusMetric.Championship, 1d, championshipReward)
            };
        }

        /// <summary>
        /// 현재 시즌 기록·수상·포스트시즌 결과를 계약 조건과 대조한다.
        /// </summary>
        public ContractBonusProgress[] Evaluate(CareerState career, int regularSeasonGames)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));

            ContractBonusClause[] clauses = BuildClauses(
                career.MyPlayer.PrimaryPosition,
                career.CurrentContract.AnnualSalary,
                regularSeasonGames);
            var result = new ContractBonusProgress[clauses.Length];
            for (int index = 0; index < clauses.Length; index++)
                result[index] = EvaluateClause(career, clauses[index]);
            return result;
        }

        private ContractBonusProgress EvaluateClause(CareerState career, ContractBonusClause clause)
        {
            PlayerSeasonStatisticsState statistics = career.CurrentLeague.CurrentSeason.PlayerStatistics;
            bool hasSample = true;
            double currentValue = clause.Metric switch
            {
                ContractBonusMetric.GamesPlayed => statistics.GamesPlayed,
                ContractBonusMetric.HomeRuns => statistics.HomeRuns,
                ContractBonusMetric.RunsBattedIn => statistics.RunsBattedIn,
                ContractBonusMetric.OnBasePlusSlugging => statistics.OnBasePlusSlugging,
                ContractBonusMetric.PitchingAppearances => statistics.PitchingAppearances,
                ContractBonusMetric.PitchingOuts => statistics.OutsRecorded,
                ContractBonusMetric.PitchingStrikeouts => statistics.PitchingStrikeouts,
                ContractBonusMetric.EarnedRunAverage => GetEarnedRunAverage(statistics, out hasSample),
                ContractBonusMetric.IndividualAward => HasIndividualAward(career) ? 1d : 0d,
                ContractBonusMetric.Championship => IsChampion(career) ? 1d : 0d,
                _ => 0d
            };

            bool hasRequiredPitchingSample = clause.Metric != ContractBonusMetric.EarnedRunAverage ||
                                              statistics.OutsRecorded >= _balance.PitcherOutsTarget;
            bool isCompleted = hasSample && hasRequiredPitchingSample &&
                               (clause.IsLowerBetter
                                   ? currentValue <= clause.TargetValue
                                   : currentValue >= clause.TargetValue);
            double progress = CalculateProgress(clause, currentValue, hasSample);
            if (clause.Metric == ContractBonusMetric.EarnedRunAverage)
            {
                double sampleProgress = statistics.OutsRecorded /
                                        (double)_balance.PitcherOutsTarget;
                progress = Math.Min(progress, Math.Min(1d, sampleProgress));
            }
            return new ContractBonusProgress(clause, currentValue, progress, isCompleted, hasSample);
        }

        private static double GetEarnedRunAverage(
            PlayerSeasonStatisticsState statistics,
            out bool hasSample)
        {
            hasSample = statistics.OutsRecorded > 0;
            return hasSample ? statistics.EarnedRunAverage : 0d;
        }

        private static bool HasIndividualAward(CareerState career)
        {
            SeasonAwardsState awards = career.CurrentLeague.CurrentSeason.Awards;
            if (awards == null)
                return false;

            int playerId = career.MyPlayer.PlayerId;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                if (awards.Results[index].IncludesWinner(playerId))
                    return true;
            }
            return false;
        }

        private static bool IsChampion(CareerState career)
        {
            return career.CurrentLeague.CurrentSeason.Postseason?.PlayerTeamResult ==
                   PlayerTeamPostseasonResult.Champion;
        }

        private static double CalculateProgress(
            ContractBonusClause clause,
            double currentValue,
            bool hasSample)
        {
            if (!hasSample)
                return 0d;

            double progress = clause.IsLowerBetter
                ? clause.TargetValue / Math.Max(currentValue, 0.001d)
                : currentValue / clause.TargetValue;
            if (progress < 0d) return 0d;
            return progress > 1d ? 1d : progress;
        }

        private static long CalculateReward(long annualSalary, double salaryRate)
        {
            long raw = (long)Math.Round(annualSalary * salaryRate, MidpointRounding.AwayFromZero);
            if (raw <= 0L)
                return 0L;
            const long Unit = 10_000L;
            return Math.Max(Unit, ((raw + Unit / 2L) / Unit) * Unit);
        }
    }
}

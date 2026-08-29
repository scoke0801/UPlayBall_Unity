using System.Collections.Generic;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 완료된 한 시즌의 소속 구단·팀 성적·개인 기록 스냅샷을 커리어 이력으로 보관한다.
    /// </summary>
    public readonly struct CareerSeasonHistoryRecord
    {
        public CareerSeasonHistoryRecord(
            int year,
            LeagueLevel leagueLevel,
            int teamId,
            string teamName,
            TeamSeasonRecordState teamRecord,
            PlayerSeasonStatisticsState statistics)
            : this(
                year,
                leagueLevel,
                teamId,
                teamName,
                teamRecord,
                statistics,
                postseasonStatistics: null,
                postseason: null,
                awards: null,
                settlement: null)
        {
        }

        public CareerSeasonHistoryRecord(
            int year,
            LeagueLevel leagueLevel,
            int teamId,
            string teamName,
            TeamSeasonRecordState teamRecord,
            PlayerSeasonStatisticsState statistics,
            PlayerSeasonStatisticsState postseasonStatistics,
            PostseasonState postseason,
            SeasonAwardsState awards,
            SeasonSettlementState settlement,
            LeagueId leagueId = default,
            double leagueStrengthIndex = 100d,
            PlayerAdjustedPerformanceState adjustedPerformance = default,
            int seasonId = 0,
            int playerId = 0,
            ExpectedRole role = ExpectedRole.BenchCompetition)
        {
            SeasonId = seasonId;
            Year = year;
            LeagueLevel = leagueLevel;
            TeamId = teamId;
            TeamName = teamName;
            TeamRecord = teamRecord;
            Statistics = statistics;
            PostseasonStatistics = postseasonStatistics;
            Postseason = postseason;
            Awards = awards;
            Settlement = settlement;
            LeagueId = leagueId.IsAssigned ? leagueId : LeagueId.FromLevel(leagueLevel);
            LeagueStrengthIndex = leagueStrengthIndex;
            AdjustedPerformance = adjustedPerformance;
            PlayerId = playerId;
            Role = role;
        }

        public int SeasonId { get; }
        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public TeamSeasonRecordState TeamRecord { get; }
        public PlayerSeasonStatisticsState Statistics { get; }
        public PlayerSeasonStatisticsState PostseasonStatistics { get; }
        public PostseasonState Postseason { get; }
        public SeasonAwardsState Awards { get; }
        public SeasonSettlementState Settlement { get; }
        public LeagueId LeagueId { get; }
        public double LeagueStrengthIndex { get; }
        public PlayerAdjustedPerformanceState AdjustedPerformance { get; }
        public int PlayerId { get; }
        public ExpectedRole Role { get; }
    }

    public enum SeasonEvaluationGrade
    {
        S,
        A,
        B,
        C,
        D
    }

    /// <summary>팀 기대·개인 백분위·포스트시즌·수상·출전 안정성을 묶은 한 시즌 평가다.</summary>
    public readonly struct CareerSeasonAchievementState
    {
        public CareerSeasonAchievementState(
            int year,
            LeagueId leagueId,
            LeagueLevel leagueTier,
            int expectedTeamRank,
            int actualTeamRank,
            double adjustedPerformance,
            bool reachedPostseason,
            bool wonChampionship,
            int awardCount,
            ExpectedRole expectedRole,
            double roleExpectationScore,
            double score,
            SeasonEvaluationGrade grade,
            double reputationChange)
        {
            Year = year;
            LeagueId = leagueId;
            LeagueTier = leagueTier;
            ExpectedTeamRank = expectedTeamRank;
            ActualTeamRank = actualTeamRank;
            AdjustedPerformance = adjustedPerformance;
            ReachedPostseason = reachedPostseason;
            WonChampionship = wonChampionship;
            AwardCount = awardCount;
            ExpectedRole = expectedRole;
            RoleExpectationScore = roleExpectationScore;
            Score = score;
            Grade = grade;
            ReputationChange = reputationChange;
        }

        public int Year { get; }
        public LeagueId LeagueId { get; }
        public LeagueLevel LeagueTier { get; }
        public int ExpectedTeamRank { get; }
        public int ActualTeamRank { get; }
        public double AdjustedPerformance { get; }
        public bool ReachedPostseason { get; }
        public bool WonChampionship { get; }
        public int AwardCount { get; }
        public ExpectedRole ExpectedRole { get; }
        public double RoleExpectationScore { get; }
        public double AppearanceStability => RoleExpectationScore;
        public double Score { get; }
        public SeasonEvaluationGrade Grade { get; }
        public double ReputationChange { get; }
    }

    /// <summary>평판을 승격 입장권으로 쓰지 않고 계약·보상용 누적값과 최초 진출만 보관한다.</summary>
    public sealed class CareerReputationState
    {
        private readonly List<CareerSeasonAchievementState> _seasons = new();
        private readonly HashSet<LeagueLevel> _reachedLeagues = new();

        public CareerReputationState(LeagueLevel startingTier)
        {
            Reputation = 50d;
            _reachedLeagues.Add(startingTier);
            HighestReachedTier = startingTier;
        }

        public double Reputation { get; private set; }
        public LeagueLevel HighestReachedTier { get; private set; }
        public IReadOnlyList<CareerSeasonAchievementState> Seasons => _seasons;

        public bool RecordLeagueReach(LeagueLevel tier)
        {
            if (_reachedLeagues.Contains(tier))
                return false;
            _reachedLeagues.Add(tier);
            if (tier > HighestReachedTier)
                HighestReachedTier = tier;
            return true;
        }

        public bool HasReached(LeagueLevel tier) => _reachedLeagues.Contains(tier);

        public void RecordSeason(CareerSeasonAchievementState achievement)
        {
            _seasons.Add(achievement);
            Reputation = Clamp(Reputation + achievement.ReputationChange, 0d, 100d);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}

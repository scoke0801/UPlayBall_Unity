namespace Baseball.Game.Career.News
{
    /// <summary>경기 활약·연속 기록을 기사 후보로 올리는 조정 가능한 기준이다.</summary>
    public sealed class NewsTriggerDefinition
    {
        public NewsTriggerDefinition(
            int notableHits,
            int notableHomeRuns,
            int notableRunsBattedIn,
            int scorelessPitchingOuts,
            int notableStrikeouts,
            int[] hittingStreakMilestones,
            int[] homeRunMilestones,
            int[] teamStreakMilestones,
            int hotStreakStart = 4,
            int roleCompetitionStartTrust = 60,
            int roleCompetitionResolveTrust = 70,
            int weeklyReportInterval = 7,
            int monthlyReportInterval = 20,
            int[] careerHitMilestones = null,
            int[] careerHomeRunMilestones = null,
            int milestoneApproachRange = 3)
        {
            NotableHits = notableHits;
            NotableHomeRuns = notableHomeRuns;
            NotableRunsBattedIn = notableRunsBattedIn;
            ScorelessPitchingOuts = scorelessPitchingOuts;
            NotableStrikeouts = notableStrikeouts;
            HittingStreakMilestones = (int[])hittingStreakMilestones.Clone();
            HomeRunMilestones = (int[])homeRunMilestones.Clone();
            TeamStreakMilestones = (int[])teamStreakMilestones.Clone();
            HotStreakStart = hotStreakStart;
            RoleCompetitionStartTrust = roleCompetitionStartTrust;
            RoleCompetitionResolveTrust = roleCompetitionResolveTrust;
            WeeklyReportInterval = weeklyReportInterval;
            MonthlyReportInterval = monthlyReportInterval;
            CareerHitMilestones = (int[])(careerHitMilestones ?? new[] { 50, 100, 200, 500, 1000 }).Clone();
            CareerHomeRunMilestones = (int[])(careerHomeRunMilestones ?? new[] { 10, 20, 50, 100, 200 }).Clone();
            MilestoneApproachRange = milestoneApproachRange;
        }

        public int NotableHits { get; }
        public int NotableHomeRuns { get; }
        public int NotableRunsBattedIn { get; }
        public int ScorelessPitchingOuts { get; }
        public int NotableStrikeouts { get; }
        public int[] HittingStreakMilestones { get; }
        public int[] HomeRunMilestones { get; }
        public int[] TeamStreakMilestones { get; }
        public int HotStreakStart { get; }
        public int RoleCompetitionStartTrust { get; }
        public int RoleCompetitionResolveTrust { get; }
        public int WeeklyReportInterval { get; }
        public int MonthlyReportInterval { get; }
        public int[] CareerHitMilestones { get; }
        public int[] CareerHomeRunMilestones { get; }
        public int MilestoneApproachRange { get; }

        public static NewsTriggerDefinition CreateDefault()
        {
            return new NewsTriggerDefinition(
                notableHits: 3,
                notableHomeRuns: 2,
                notableRunsBattedIn: 4,
                scorelessPitchingOuts: 18,
                notableStrikeouts: 10,
                hittingStreakMilestones: new[] { 5, 10, 15, 20 },
                homeRunMilestones: new[] { 10, 20, 30 },
                teamStreakMilestones: new[] { 3, 5, 8 },
                hotStreakStart: 4,
                roleCompetitionStartTrust: 60,
                roleCompetitionResolveTrust: 70,
                weeklyReportInterval: 7,
                monthlyReportInterval: 20,
                careerHitMilestones: new[] { 50, 100, 200, 500, 1000 },
                careerHomeRunMilestones: new[] { 10, 20, 50, 100, 200 },
                milestoneApproachRange: 3);
        }
    }
}

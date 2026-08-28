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
            int[] teamStreakMilestones)
        {
            NotableHits = notableHits;
            NotableHomeRuns = notableHomeRuns;
            NotableRunsBattedIn = notableRunsBattedIn;
            ScorelessPitchingOuts = scorelessPitchingOuts;
            NotableStrikeouts = notableStrikeouts;
            HittingStreakMilestones = (int[])hittingStreakMilestones.Clone();
            HomeRunMilestones = (int[])homeRunMilestones.Clone();
            TeamStreakMilestones = (int[])teamStreakMilestones.Clone();
        }

        public int NotableHits { get; }
        public int NotableHomeRuns { get; }
        public int NotableRunsBattedIn { get; }
        public int ScorelessPitchingOuts { get; }
        public int NotableStrikeouts { get; }
        public int[] HittingStreakMilestones { get; }
        public int[] HomeRunMilestones { get; }
        public int[] TeamStreakMilestones { get; }

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
                teamStreakMilestones: new[] { 3, 5, 8 });
        }
    }
}

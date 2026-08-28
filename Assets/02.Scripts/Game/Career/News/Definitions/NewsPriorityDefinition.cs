using System;

namespace Baseball.Game.Career.News
{
    /// <summary>뉴스 가치와 한 주기 노출량을 코드 밖 정의로 옮길 수 있는 순수 설정이다.</summary>
    public sealed class NewsPriorityDefinition
    {
        public NewsPriorityDefinition(
            int playerRelevance,
            int playerTeamRelevance,
            int leagueRelevance,
            int storylineContinuation,
            int repeatPenalty,
            int sThreshold,
            int aThreshold,
            int bThreshold,
            int cThreshold,
            int maximumArticlesPerCycle,
            int maximumStandardArticles,
            int maximumBriefings,
            int maximumArticlesPerPlayer,
            int defaultCooldownCycles)
        {
            if (sThreshold <= aThreshold || aThreshold <= bThreshold || bThreshold <= cThreshold)
                throw new ArgumentException("뉴스 등급 점수는 S > A > B > C 순서여야 합니다.");
            PlayerRelevance = playerRelevance;
            PlayerTeamRelevance = playerTeamRelevance;
            LeagueRelevance = leagueRelevance;
            StorylineContinuation = storylineContinuation;
            RepeatPenalty = repeatPenalty;
            SThreshold = sThreshold;
            AThreshold = aThreshold;
            BThreshold = bThreshold;
            CThreshold = cThreshold;
            MaximumArticlesPerCycle = maximumArticlesPerCycle;
            MaximumStandardArticles = maximumStandardArticles;
            MaximumBriefings = maximumBriefings;
            MaximumArticlesPerPlayer = maximumArticlesPerPlayer;
            DefaultCooldownCycles = defaultCooldownCycles;
        }

        public int PlayerRelevance { get; }
        public int PlayerTeamRelevance { get; }
        public int LeagueRelevance { get; }
        public int StorylineContinuation { get; }
        public int RepeatPenalty { get; }
        public int SThreshold { get; }
        public int AThreshold { get; }
        public int BThreshold { get; }
        public int CThreshold { get; }
        public int MaximumArticlesPerCycle { get; }
        public int MaximumStandardArticles { get; }
        public int MaximumBriefings { get; }
        public int MaximumArticlesPerPlayer { get; }
        public int DefaultCooldownCycles { get; }

        public static NewsPriorityDefinition CreateDefault()
        {
            return new NewsPriorityDefinition(
                playerRelevance: 40,
                playerTeamRelevance: 20,
                leagueRelevance: 5,
                storylineContinuation: 10,
                repeatPenalty: 25,
                sThreshold: 90,
                aThreshold: 70,
                bThreshold: 50,
                cThreshold: 30,
                maximumArticlesPerCycle: 4,
                maximumStandardArticles: 2,
                maximumBriefings: 1,
                maximumArticlesPerPlayer: 2,
                defaultCooldownCycles: 4);
        }
    }
}

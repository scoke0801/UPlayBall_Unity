namespace Baseball.Core.Teams
{
    /// <summary>
    /// 새 게임 구단 생성에 쓰이는 아키타입 기본값을 제공한다.
    /// `NewGameDefinition` Asset을 읽지 못하는 테스트와 개발 환경의 명시적인 대체값으로 사용한다.
    /// </summary>
    public static class TeamArchetypeLibrary
    {
        /// <summary>
        /// 다섯 성향의 최초 검증용 기본 프로필을 만든다.
        /// </summary>
        public static TeamArchetypeProfile[] CreateDefaultPool()
        {
            return new[]
            {
                new TeamArchetypeProfile(TeamArchetype.Development, budget: 45, development: 85, rosterDepth: 40, scouting: 70),
                new TeamArchetypeProfile(TeamArchetype.Contender, budget: 85, development: 55, rosterDepth: 80, scouting: 60),
                new TeamArchetypeProfile(TeamArchetype.OffenseFocused, budget: 60, development: 65, rosterDepth: 55, scouting: 55),
                new TeamArchetypeProfile(TeamArchetype.PitchingFocused, budget: 60, development: 65, rosterDepth: 55, scouting: 55),
                new TeamArchetypeProfile(TeamArchetype.SmallMarket, budget: 30, development: 45, rosterDepth: 35, scouting: 40)
            };
        }
    }
}

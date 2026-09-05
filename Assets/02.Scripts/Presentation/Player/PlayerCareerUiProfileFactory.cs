using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// 선수 모드 화면이 사용하는 안정적인 Route 식별자를 제공한다.
    /// </summary>
    public static class PlayerCareerRoutes
    {
        public const string Home = "Player.Home";
        public const string Match = "Player.Match";
        public const string NextMatch = "Player.Match.Next";
        public const string MatchRole = "Player.Match.Role";
        public const string Profile = "Player.Profile";
        public const string Abilities = "Player.Profile.Abilities";
        public const string SeasonStatistics = "Player.Profile.Season";
        public const string Growth = "Player.Growth";
        public const string Team = "Team.Overview";
        public const string TeamRoster = "Team.Roster";
        public const string ManagerDecision = "Player.Team.ManagerDecision";
        public const string League = "League.Standings";
        public const string Schedule = "League.Schedule";
        public const string Records = "Records.Season";
        public const string Career = "Player.Career";
        public const string Contract = "Player.Contract";
    }

    /// <summary>
    /// 선수 모드의 Navigation과 권한을 공용 셸 계약으로 만든다.
    /// </summary>
    public static class PlayerCareerUiProfileFactory
    {
        /// <summary>
        /// 팀 편집·카드 경제 권한이 없는 선수 모드 Profile을 만든다.
        /// </summary>
        public static GameModeUiProfile Create()
        {
            UiCapabilitySet capabilities = new UiCapabilitySet(
                UiCapability.CanViewCareerPlayerGrowth |
                UiCapability.CanPlayPlayerMiniGame |
                UiCapability.CanViewLeagueInformation |
                UiCapability.CanViewSeasonRecords);

            var navigation = new NavigationManifest(new[]
            {
                new NavigationEntry(PlayerCareerRoutes.Home, "홈"),
                new NavigationEntry(
                    PlayerCareerRoutes.Match,
                    "경기",
                    UiCapability.CanPlayPlayerMiniGame),
                new NavigationEntry(PlayerCareerRoutes.Profile, "선수"),
                new NavigationEntry(
                    PlayerCareerRoutes.Growth,
                    "성장",
                    UiCapability.CanViewCareerPlayerGrowth),
                new NavigationEntry(PlayerCareerRoutes.Team, "구단"),
                new NavigationEntry(
                    PlayerCareerRoutes.League,
                    "리그",
                    UiCapability.CanViewLeagueInformation),
                new NavigationEntry(
                    PlayerCareerRoutes.Records,
                    "기록",
                    UiCapability.CanViewSeasonRecords),
                new NavigationEntry(PlayerCareerRoutes.Career, "커리어")
            });

            return new GameModeUiProfile(UiGameMode.PlayerCareer, "선수 모드", navigation, capabilities);
        }
    }
}

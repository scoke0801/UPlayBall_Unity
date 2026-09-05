using System.Collections.Generic;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// 선수 모드 화면이 사용하는 안정적인 Route 식별자를 제공한다.
    /// </summary>
    public static class PlayerCareerRoutes
    {
        public const string Home = "Player.Home";

        public const string Game = "Player.Game";
        public const string Match = "Player.Match";
        public const string NextMatch = "Player.Match.Next";
        public const string MatchRole = "Player.Match.Role";
        public const string GameResults = "Player.Match.Results";

        public const string Player = "Player.Player";
        public const string Profile = "Player.Profile";
        public const string Abilities = "Player.Profile.Abilities";
        public const string SeasonStatistics = "Player.Profile.Season";
        public const string Growth = "Player.Growth";
        public const string Skills = "Player.Profile.Skills";

        public const string TeamHub = "Player.Team";
        public const string Team = "Team.Overview";
        public const string TeamRoster = "Team.Roster";
        public const string ManagerDecision = "Player.Team.ManagerDecision";
        public const string TeamLineup = "Player.Team.Lineup";
        public const string TeamPitching = "Player.Team.Pitching";

        public const string LeagueHub = "Player.League";
        public const string League = "League.Standings";
        public const string Schedule = "League.Schedule";
        public const string LeagueBatting = "Player.League.Batting";
        public const string LeaguePitching = "Player.League.Pitching";
        public const string LeagueRecords = "Player.League.Records";

        public const string CareerHub = "Player.CareerHub";
        public const string Records = "Records.Season";
        public const string CareerRecords = "Player.Career.Records";
        public const string AwardsHighlights = "Player.Career.AwardsHighlights";
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
                    PlayerCareerRoutes.Game,
                    "경기",
                    UiCapability.CanPlayPlayerMiniGame,
                    children: new[]
                    {
                        new NavigationEntry(PlayerCareerRoutes.NextMatch, "다음 경기"),
                        new NavigationEntry(PlayerCareerRoutes.Schedule, "일정"),
                        new NavigationEntry(PlayerCareerRoutes.GameResults, "경기 결과")
                    }),
                new NavigationEntry(
                    PlayerCareerRoutes.Player,
                    "선수",
                    children: new[]
                    {
                        new NavigationEntry(PlayerCareerRoutes.Profile, "선수 정보"),
                        new NavigationEntry(PlayerCareerRoutes.Abilities, "능력치"),
                        new NavigationEntry(
                            PlayerCareerRoutes.Growth,
                            "성장",
                            UiCapability.CanViewCareerPlayerGrowth),
                        new NavigationEntry(PlayerCareerRoutes.Skills, "스킬")
                    }),
                new NavigationEntry(
                    PlayerCareerRoutes.TeamHub,
                    "팀",
                    children: new[]
                    {
                        new NavigationEntry(PlayerCareerRoutes.TeamRoster, "선수단"),
                        new NavigationEntry(PlayerCareerRoutes.TeamLineup, "선발 라인업"),
                        new NavigationEntry(PlayerCareerRoutes.TeamPitching, "투수진"),
                        new NavigationEntry(PlayerCareerRoutes.Team, "팀 정보")
                    }),
                new NavigationEntry(
                    PlayerCareerRoutes.LeagueHub,
                    "리그",
                    UiCapability.CanViewLeagueInformation,
                    children: new[]
                    {
                        new NavigationEntry(PlayerCareerRoutes.League, "순위"),
                        new NavigationEntry(PlayerCareerRoutes.LeagueBatting, "타자 순위"),
                        new NavigationEntry(PlayerCareerRoutes.LeaguePitching, "투수 순위"),
                        new NavigationEntry(PlayerCareerRoutes.LeagueRecords, "리그 기록")
                    }),
                new NavigationEntry(
                    PlayerCareerRoutes.CareerHub,
                    "커리어",
                    children: new[]
                    {
                        new NavigationEntry(PlayerCareerRoutes.Contract, "계약"),
                        new NavigationEntry(
                            PlayerCareerRoutes.Records,
                            "시즌 기록",
                            UiCapability.CanViewSeasonRecords),
                        new NavigationEntry(
                            PlayerCareerRoutes.CareerRecords,
                            "통산 기록",
                            UiCapability.CanViewSeasonRecords),
                        new NavigationEntry(
                            PlayerCareerRoutes.AwardsHighlights,
                            "수상·하이라이트",
                            UiCapability.CanViewSeasonRecords)
                    })
            });

            var routeMigrations = new NavigationRouteMigrationMap(
                new Dictionary<string, string>
                {
                    [PlayerCareerRoutes.Match] = PlayerCareerRoutes.NextMatch,
                    [PlayerCareerRoutes.MatchRole] = PlayerCareerRoutes.NextMatch,
                    [PlayerCareerRoutes.SeasonStatistics] = PlayerCareerRoutes.Records,
                    [PlayerCareerRoutes.ManagerDecision] = PlayerCareerRoutes.TeamLineup,
                    [PlayerCareerRoutes.Career] = PlayerCareerRoutes.Contract
                });

            return new GameModeUiProfile(
                UiGameMode.PlayerCareer,
                "선수 모드",
                navigation,
                capabilities,
                routeMigrations: routeMigrations,
                backgroundResourcePath: PlayerUiAssetCatalog.HomeClubhouseBackgroundResourcePath);
        }
    }
}

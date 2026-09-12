using System.Collections.Generic;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 모드의 업무 영역과 Context Match Center Route를 한곳에 정의한다.</summary>
    public static class OwnerNavigationRoutes
    {
        public const string Home = "Owner.Home";
        public const string LegendaryPractice = "Owner.Practice.Legendary";
        public const string Roster = "Owner.Roster";
        public const string RosterLineup = "Owner.Roster.Lineup";
        public const string RosterTacticCards = "Owner.Roster.TacticCards";
        public const string RosterSupportCards = "Owner.Roster.SupportCards";
        public const string RosterTeamColor = "Owner.Roster.TeamColor";
        public const string RosterPitching = "Owner.Roster.Pitching";
        public const string RosterCollection = "Owner.Roster.Collection";
        public const string RosterEncyclopedia = "Owner.Roster.Encyclopedia";
        public const string RosterWishlist = "Owner.Roster.Wishlist";
        public const string RosterCondition = "Owner.Roster.Condition";
        public const string PowerUp = "Owner.PowerUp";
        public const string PowerUpScout = "Owner.PowerUp.Scout";
        public const string PowerUpTraining = "Owner.PowerUp.Training";
        public const string PowerUpSkills = "Owner.PowerUp.Skills";
        public const string PowerUpStudy = "Owner.PowerUp.Study";
        public const string PowerUpEnhancementSale = "Owner.PowerUp.EnhancementSale";
        public const string SpecialRecruitLegend = "Owner.SpecialRecruit.Legend";
        public const string SpecialRecruitCareerHigh = "Owner.SpecialRecruit.CareerHigh";
        public const string Dugout = "Owner.Dugout";
        public const string DugoutLineupNotes = "Owner.Dugout.LineupNotes";
        public const string DugoutTeamColor = "Owner.Dugout.TeamColor";
        public const string DugoutTactics = "Owner.Dugout.Tactics";
        public const string DugoutManagerPolicy = "Owner.Dugout.ManagerPolicy";
        public const string Club = "Owner.Club";
        public const string ClubOwner = "Owner.Club.Owner";
        public const string ClubInformation = "Owner.Club.Information";
        public const string ClubHistory = "Owner.Club.History";
        public const string League = "Shared.League";
        public const string LeagueStandings = "Shared.League.Standings";
        public const string LeagueTeamResults = "Shared.League.TeamResults";
        public const string LeagueMatchups = "Shared.League.Matchups";
        public const string LeagueRankHistory = "Shared.League.RankHistory";
        public const string Shop = "Owner.Shop";
        public const string MatchCenter = "Owner.MatchCenter";
        public const string MatchCenterAnalysis = "Owner.MatchCenter.Analysis";
        public const string MatchCenterLineup = "Owner.MatchCenter.Lineup";
        public const string MatchCenterCondition = "Owner.MatchCenter.Condition";
        public const string MatchCenterOpponentLineup = "Owner.MatchCenter.OpponentLineup";
        public const string MatchCenterTactics = "Owner.MatchCenter.Tactics";
        public const string MatchSpectator = "Owner.Match.Spectator";
    }

    /// <summary>구단주 모드에서 실제로 제공되는 Route와 권한을 공용 셸 계약으로 만든다.</summary>
    public static class OwnerModeUiProfileFactory
    {
        /// <summary>상위 메뉴 요청을 이전 선택과 관계없이 첫 번째 사용 가능한 Local Route로 변환한다.</summary>
        public static string ResolvePrimaryMenuRoute(GameModeUiProfile profile, string routeId)
        {
            if (profile == null)
                throw new System.ArgumentNullException(nameof(profile));

            string resolved = profile.ResolveRouteId(routeId);
            NavigationEntry entry = profile.Navigation.FindEntry(resolved);
            if (entry == null || entry.Children.Count == 0)
                return resolved;

            for (int index = 0; index < entry.Children.Count; index++)
            {
                NavigationEntry child = entry.Children[index];
                if (child.IsEnabled && child.IsVisible(profile.Capabilities))
                    return child.RouteId;
            }

            return resolved;
        }

        /// <summary>현재 백엔드 연결 범위를 숨기거나 과장하지 않는 구단주 UI Profile을 만든다.</summary>
        public static GameModeUiProfile Create()
        {
            var rosterTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.RosterLineup, "선수 오더"),
                new NavigationEntry(OwnerNavigationRoutes.RosterTacticCards, "작전 카드"),
                new NavigationEntry(OwnerNavigationRoutes.RosterSupportCards, "서포트 카드"),
                new NavigationEntry(OwnerNavigationRoutes.RosterTeamColor, "팀 컬러"),
                new NavigationEntry(OwnerNavigationRoutes.RosterEncyclopedia, "선수 도감"),
                new NavigationEntry(OwnerNavigationRoutes.RosterWishlist, "위시리스트")
            };
            var powerUpTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.PowerUpScout, "스카우트"),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpTraining, "카드훈련"),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpEnhancementSale, "카드 합성"),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpSkills, "스킬 블록 배치"),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpStudy, "유학"),
                new NavigationEntry(OwnerNavigationRoutes.SpecialRecruitLegend, "레전드 영입"),
                new NavigationEntry(OwnerNavigationRoutes.SpecialRecruitCareerHigh, "커리어하이 영입")
            };
            var dugoutTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.DugoutLineupNotes, "덕아웃"),
                new NavigationEntry(OwnerNavigationRoutes.DugoutManagerPolicy, "감독방침")
            };
            var leagueTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.LeagueStandings, "순위표"),
                new NavigationEntry(OwnerNavigationRoutes.LeagueTeamResults, "구단 성적"),
                new NavigationEntry(OwnerNavigationRoutes.LeagueMatchups, "대전 결과"),
                new NavigationEntry(OwnerNavigationRoutes.LeagueRankHistory, "순위 변화"),
                new NavigationEntry(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId, "일정"),
                new NavigationEntry(
                    OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId,
                    "선수 기록",
                    UiCapability.CanViewSeasonRecords),
                new NavigationEntry(
                    OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId,
                    "역사 기록",
                    UiCapability.CanViewSeasonRecords)
            };
            var clubTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.ClubOwner, "구단주"),
                new NavigationEntry(OwnerNavigationRoutes.ClubInformation, "구단"),
                new NavigationEntry(OwnerNavigationRoutes.ClubHistory, "기록실", UiCapability.CanViewSeasonRecords),
                new NavigationEntry(OwnerManagementRoutes.ClubFinance, "재정"),
                new NavigationEntry(OwnerManagementRoutes.ClubFacility, "시설"),
                new NavigationEntry(OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId, "코칭스태프"),
            };

            var manifest = new NavigationManifest(new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.Home, "홈"),
                new NavigationEntry(OwnerNavigationRoutes.Roster, "선수단", children: rosterTabs),
                new NavigationEntry(OwnerNavigationRoutes.PowerUp, "전력보강", children: powerUpTabs),
                new NavigationEntry(OwnerNavigationRoutes.Dugout, "덕아웃", children: dugoutTabs),
                new NavigationEntry(
                    OwnerNavigationRoutes.Club,
                    "구단",
                    UiCapability.CanManageFinance,
                    children: clubTabs),
                new NavigationEntry(
                    OwnerNavigationRoutes.League,
                    "리그",
                    UiCapability.CanViewLeagueInformation,
                    children: leagueTabs),
                new NavigationEntry(OwnerNavigationRoutes.Shop, "상점")
            });

            var contextNavigation = new NavigationManifest(new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.LegendaryPractice, "역대 강팀"),
                new NavigationEntry(OwnerNavigationRoutes.MatchCenter, "경기 준비", children: new[]
                {
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterAnalysis, "상대 분석"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterLineup, "우리 라인업"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterOpponentLineup, "상대 라인업"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterTactics, "전술카드")
                }),
                new NavigationEntry(OwnerNavigationRoutes.MatchSpectator, "경기 관전")
            });
            var migrations = new NavigationRouteMigrationMap(new Dictionary<string, string>
            {
                [OwnerNavigationRoutes.MatchCenterCondition] = OwnerNavigationRoutes.MatchCenterOpponentLineup,
                ["Owner.Roster.Active"] = OwnerNavigationRoutes.RosterLineup,
                [OwnerNavigationRoutes.RosterPitching] = OwnerNavigationRoutes.RosterLineup,
                [OwnerNavigationRoutes.RosterCollection] = OwnerNavigationRoutes.RosterLineup,
                [OwnerNavigationRoutes.RosterCondition] = OwnerNavigationRoutes.RosterLineup,
                ["Owner.Scout"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Scout.General"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Scout.Franchise"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Scout.Year"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Scout.YearFranchise"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Scout.Award"] = OwnerNavigationRoutes.PowerUpScout,
                ["Owner.Development"] = OwnerNavigationRoutes.PowerUpTraining,
                ["Owner.Development.Training"] = OwnerNavigationRoutes.PowerUpTraining,
                ["Owner.Development.Enhancement"] = OwnerNavigationRoutes.PowerUpEnhancementSale,
                ["Owner.Development.Sale"] = OwnerNavigationRoutes.PowerUpEnhancementSale,
                ["Owner.Tactic"] = OwnerNavigationRoutes.DugoutLineupNotes,
                [OwnerNavigationRoutes.DugoutTeamColor] = OwnerNavigationRoutes.RosterTeamColor,
                [OwnerNavigationRoutes.DugoutTactics] = OwnerNavigationRoutes.RosterTacticCards,
                ["Owner.Tactic.TeamColor"] = OwnerNavigationRoutes.RosterTeamColor,
                ["Owner.Tactic.Cards"] = OwnerNavigationRoutes.RosterTacticCards,
                ["Owner.Tactic.ManagerPolicy"] = OwnerNavigationRoutes.DugoutManagerPolicy,
                [OwnerModeShellCoordinator.MatchRouteId] = OwnerNavigationRoutes.MatchCenterAnalysis,
                [OwnerExpansionWorkspaceCoordinator.PregameRouteId] = OwnerNavigationRoutes.MatchCenterAnalysis
            });

            var capabilities = new UiCapabilitySet(
                UiCapability.CanEditLineup |
                UiCapability.CanEquipTeamColor |
                UiCapability.CanEquipTacticCards |
                UiCapability.CanUseScout |
                UiCapability.CanTrainOwnedCards |
                UiCapability.CanManageFinance |
                UiCapability.CanViewLeagueInformation |
                UiCapability.CanViewSeasonRecords);

            return new GameModeUiProfile(
                UiGameMode.OwnerCareer,
                "구단주 모드",
                manifest,
                capabilities,
                contextNavigation,
                migrations,
                OwnerUiAssetIds.HomeBackgroundResourcePath);
        }
    }
}

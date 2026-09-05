using System.Collections.Generic;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 모드의 업무 영역과 Context Match Center Route를 한곳에 정의한다.</summary>
    public static class OwnerNavigationRoutes
    {
        public const string Home = "Owner.Home";
        public const string Roster = "Owner.Roster";
        public const string RosterLineup = "Owner.Roster.Lineup";
        public const string RosterPitching = "Owner.Roster.Pitching";
        public const string RosterCollection = "Owner.Roster.Collection";
        public const string RosterCondition = "Owner.Roster.Condition";
        public const string PowerUp = "Owner.PowerUp";
        public const string PowerUpScout = "Owner.PowerUp.Scout";
        public const string PowerUpTraining = "Owner.PowerUp.Training";
        public const string PowerUpEnhancementSale = "Owner.PowerUp.EnhancementSale";
        public const string Dugout = "Owner.Dugout";
        public const string DugoutLineupNotes = "Owner.Dugout.LineupNotes";
        public const string DugoutTeamColor = "Owner.Dugout.TeamColor";
        public const string DugoutTactics = "Owner.Dugout.Tactics";
        public const string DugoutManagerPolicy = "Owner.Dugout.ManagerPolicy";
        public const string Club = "Owner.Club";
        public const string ClubContract = "Owner.Club.Contract";
        public const string ClubTrade = "Owner.Club.Trade";
        public const string League = "Shared.League";
        public const string LeagueStandings = "Shared.League.Standings";
        public const string MatchCenter = "Owner.MatchCenter";
        public const string MatchCenterAnalysis = "Owner.MatchCenter.Analysis";
        public const string MatchCenterLineup = "Owner.MatchCenter.Lineup";
        public const string MatchCenterCondition = "Owner.MatchCenter.Condition";
        public const string MatchCenterTactics = "Owner.MatchCenter.Tactics";
        public const string MatchSpectator = "Owner.Match.Spectator";
    }

    /// <summary>구단주 모드에서 실제로 제공되는 Route와 권한을 공용 셸 계약으로 만든다.</summary>
    public static class OwnerModeUiProfileFactory
    {
        private const string RuntimeAdapterPending =
            "실제 게임 기능과 이 화면의 연결이 아직 완료되지 않았습니다.";
        private const string ScoutBackendUnavailable =
            "스카우트 후보군·확률 조회·실행 기능이 아직 준비되지 않았습니다.";
        private const string LiveStandingsUnavailable =
            "현재 시즌의 구단별 누적 승패와 확정 순위 데이터가 아직 제공되지 않습니다.";
        private const string TeamColorBackendUnavailable =
            "라인업의 팀컬러 슬롯은 변경할 수 있지만 조건·적용 대상·중첩 결과를 보여주는 상세 화면은 아직 준비되지 않았습니다.";
        private const string TacticBackendUnavailable =
            "라인업의 전술카드 슬롯은 변경할 수 있지만 발동 조건·대상·지속 시간을 보여주는 상세 화면은 아직 준비되지 않았습니다.";
        private const string ManagerPolicyBackendUnavailable =
            "감독 방침 조회·변경 기능이 아직 준비되지 않았습니다.";
        private const string CardTrainingBackendUnavailable =
            "카드 훈련 기능은 있으나 훈련 목록과 비용·결과 미리보기가 아직 제공되지 않았습니다.";
        /// <summary>현재 백엔드 연결 범위를 숨기거나 과장하지 않는 구단주 UI Profile을 만든다.</summary>
        public static GameModeUiProfile Create()
        {
            var rosterTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.RosterLineup, "라인업"),
                new NavigationEntry(OwnerNavigationRoutes.RosterPitching, "투수진", isEnabled: false,
                    disabledReason: RuntimeAdapterPending),
                new NavigationEntry(OwnerNavigationRoutes.RosterCollection, "보유선수"),
                new NavigationEntry(OwnerNavigationRoutes.RosterCondition, "컨디션·궁합")
            };
            var powerUpTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.PowerUpScout, "스카우트", isEnabled: false,
                    disabledReason: ScoutBackendUnavailable),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpTraining, "카드훈련", isEnabled: false,
                    disabledReason: CardTrainingBackendUnavailable),
                new NavigationEntry(OwnerNavigationRoutes.PowerUpEnhancementSale, "강화·판매", isEnabled: false,
                    disabledReason: "강화·판매 계산은 있으나 비용·결과 미리보기와 실행 기능이 아직 제공되지 않았습니다.")
            };
            var dugoutTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.DugoutLineupNotes, "덕아웃"),
                new NavigationEntry(OwnerNavigationRoutes.DugoutTeamColor, "팀컬러", isEnabled: false,
                    disabledReason: TeamColorBackendUnavailable),
                new NavigationEntry(OwnerNavigationRoutes.DugoutTactics, "작전", isEnabled: false,
                    disabledReason: TacticBackendUnavailable),
                new NavigationEntry(OwnerNavigationRoutes.DugoutManagerPolicy, "감독방침", isEnabled: false,
                    disabledReason: ManagerPolicyBackendUnavailable)
            };
            var leagueTabs = new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.LeagueStandings, "순위", isEnabled: false,
                    disabledReason: LiveStandingsUnavailable),
                new NavigationEntry(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId, "일정"),
                new NavigationEntry(OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId, "리그 기록")
            };
            var clubTabs = new[]
            {
                new NavigationEntry(OwnerManagementRoutes.ClubFinance, "재정"),
                new NavigationEntry(OwnerManagementRoutes.ClubFacility, "시설"),
                new NavigationEntry(OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId, "코칭스태프"),
                new NavigationEntry(
                    OwnerNavigationRoutes.ClubContract,
                    "계약",
                    isEnabled: false,
                    disabledReason: "구단 계약 조회와 협상 기능이 아직 구현되지 않았습니다."),
                new NavigationEntry(
                    OwnerNavigationRoutes.ClubTrade,
                    "트레이드",
                    isEnabled: false,
                    disabledReason: "트레이드 조회와 제안 기능이 아직 구현되지 않았습니다.")
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
                    children: leagueTabs)
            });

            var contextNavigation = new NavigationManifest(new[]
            {
                new NavigationEntry(OwnerNavigationRoutes.MatchCenter, "경기 준비", children: new[]
                {
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterAnalysis, "상대 분석"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterLineup, "우리 라인업"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterCondition, "컨디션·궁합"),
                    new NavigationEntry(OwnerNavigationRoutes.MatchCenterTactics, "전술카드")
                }),
                new NavigationEntry(OwnerNavigationRoutes.MatchSpectator, "경기 관전")
            });
            var migrations = new NavigationRouteMigrationMap(new Dictionary<string, string>
            {
                ["Owner.Roster.Active"] = OwnerNavigationRoutes.RosterLineup,
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
                ["Owner.Tactic.TeamColor"] = OwnerNavigationRoutes.DugoutTeamColor,
                ["Owner.Tactic.Cards"] = OwnerNavigationRoutes.DugoutTactics,
                ["Owner.Tactic.ManagerPolicy"] = OwnerNavigationRoutes.DugoutManagerPolicy,
                [OwnerModeShellCoordinator.MatchRouteId] = OwnerNavigationRoutes.MatchCenterAnalysis,
                [OwnerExpansionWorkspaceCoordinator.PregameRouteId] = OwnerNavigationRoutes.MatchCenterAnalysis
            });

            var capabilities = new UiCapabilitySet(
                UiCapability.CanEditLineup |
                UiCapability.CanEquipTeamColor |
                UiCapability.CanEquipTacticCards |
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

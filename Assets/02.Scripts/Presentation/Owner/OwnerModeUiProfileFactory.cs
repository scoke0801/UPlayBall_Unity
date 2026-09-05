using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
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
        private const string EnhancementBackendUnavailable =
            "강화 결과 계산은 있으나 비용·결과 미리보기와 실행 기능이 아직 제공되지 않았습니다.";
        private const string CardSaleBackendUnavailable =
            "판매 가격 계산은 있으나 가격 미리보기와 실행 기능이 아직 제공되지 않았습니다.";

        /// <summary>현재 백엔드 연결 범위를 숨기거나 과장하지 않는 구단주 UI Profile을 만든다.</summary>
        public static GameModeUiProfile Create()
        {
            var rosterTabs = new[]
            {
                new NavigationEntry("Owner.Roster.Active", "1군 편성", isEnabled: false, disabledReason: RuntimeAdapterPending),
                new NavigationEntry(OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId, "선수단·라인업"),
                new NavigationEntry("Owner.Roster.Pitching", "투수진", isEnabled: false, disabledReason: RuntimeAdapterPending),
                new NavigationEntry(OwnerExpansionWorkspaceCoordinator.CollectionRouteId, "보유 선수"),
                new NavigationEntry(OwnerManagementRoutes.RosterCondition, "Condition·궁합")
            };
            var scoutingTabs = new[]
            {
                new NavigationEntry("Owner.Scout.General", "일반", isEnabled: false,
                    disabledReason: ScoutBackendUnavailable),
                new NavigationEntry("Owner.Scout.Franchise", "구단", isEnabled: false,
                    disabledReason: ScoutBackendUnavailable),
                new NavigationEntry("Owner.Scout.Year", "연도", isEnabled: false,
                    disabledReason: ScoutBackendUnavailable),
                new NavigationEntry("Owner.Scout.YearFranchise", "구단+연도", isEnabled: false,
                    disabledReason: ScoutBackendUnavailable),
                new NavigationEntry(
                    "Owner.Scout.Award",
                    "수상 카드",
                    isEnabled: false,
                    disabledReason: "수상 카드 스카우트 정책과 실행 기능이 아직 준비되지 않았습니다.")
            };
            var developmentTabs = new[]
            {
                new NavigationEntry("Owner.Development.Training", "카드 훈련", isEnabled: false,
                    disabledReason: CardTrainingBackendUnavailable),
                new NavigationEntry("Owner.Development.Enhancement", "중복 강화", isEnabled: false,
                    disabledReason: EnhancementBackendUnavailable),
                new NavigationEntry("Owner.Development.Sale", "판매", isEnabled: false,
                    disabledReason: CardSaleBackendUnavailable)
            };
            var tacticTabs = new[]
            {
                new NavigationEntry("Owner.Tactic.TeamColor", "팀컬러", isEnabled: false,
                    disabledReason: TeamColorBackendUnavailable),
                new NavigationEntry("Owner.Tactic.Cards", "전술카드", isEnabled: false,
                    disabledReason: TacticBackendUnavailable),
                new NavigationEntry("Owner.Tactic.ManagerPolicy", "감독 방침", isEnabled: false,
                    disabledReason: ManagerPolicyBackendUnavailable)
            };
            var leagueTabs = new[]
            {
                new NavigationEntry("Shared.League.Standings", "순위", isEnabled: false,
                    disabledReason: LiveStandingsUnavailable),
                new NavigationEntry(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId, "일정"),
                new NavigationEntry(OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId, "역사 기록")
            };
            var clubTabs = new[]
            {
                new NavigationEntry(OwnerManagementRoutes.ClubFinance, "재정"),
                new NavigationEntry(OwnerManagementRoutes.ClubFacility, "시설"),
                new NavigationEntry(OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId, "코칭스태프"),
                new NavigationEntry(
                    "Owner.Club.Contract",
                    "계약",
                    isEnabled: false,
                    disabledReason: "구단 계약 기능이 아직 구현되지 않았습니다."),
                new NavigationEntry(
                    "Owner.Club.Trade",
                    "트레이드",
                    isEnabled: false,
                    disabledReason: "트레이드 기능이 아직 구현되지 않았습니다.")
            };

            var manifest = new NavigationManifest(new[]
            {
                new NavigationEntry("Owner.Home", "홈"),
                new NavigationEntry(
                    "Owner.Roster",
                    "선수단",
                    UiCapability.CanEditLineup,
                    children: rosterTabs),
                new NavigationEntry(
                    "Owner.Scout",
                    "스카우트",
                    UiCapability.CanUseScout,
                    isEnabled: false,
                    disabledReason: ScoutBackendUnavailable,
                    children: scoutingTabs),
                new NavigationEntry(
                    "Owner.Development",
                    "육성",
                    UiCapability.CanTrainOwnedCards,
                    isEnabled: false,
                    disabledReason: "카드 훈련 목록·미리보기와 강화·판매 실행 기능이 아직 제공되지 않았습니다.",
                    children: developmentTabs),
                new NavigationEntry(
                    "Owner.Tactic",
                    "전술",
                    UiCapability.CanEquipTacticCards,
                    isEnabled: false,
                    disabledReason: "라인업 슬롯 변경은 가능하지만 팀컬러·전술카드 상세 분석 화면은 아직 준비되지 않았습니다.",
                    children: tacticTabs),
                new NavigationEntry(
                    "Shared.League",
                    "리그",
                    UiCapability.CanViewLeagueInformation,
                    children: leagueTabs),
                new NavigationEntry(
                    "Owner.Club",
                    "구단",
                    UiCapability.CanManageFinance,
                    children: clubTabs),
                new NavigationEntry(
                    "Owner.Match",
                    "경기 준비",
                    UiCapability.CanEditLineup,
                    children: new[]
                    {
                        new NavigationEntry(OwnerExpansionWorkspaceCoordinator.PregameRouteId, "상대 분석·프리셋")
                    })
            });

            var capabilities = new UiCapabilitySet(
                UiCapability.CanEditLineup |
                UiCapability.CanEquipTeamColor |
                UiCapability.CanEquipTacticCards |
                UiCapability.CanManageFinance |
                UiCapability.CanViewLeagueInformation |
                UiCapability.CanViewSeasonRecords);

            return new GameModeUiProfile(UiGameMode.OwnerCareer, "구단주 모드", manifest, capabilities);
        }
    }
}

using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>정적 정의 에셋이 없을 때 사용하는 한국어 기사 템플릿 기본 세트다.</summary>
    public static class DefaultNewsTemplateLibrary
    {
        public static NewsTemplateDefinition[] Create()
        {
            var templates = new List<NewsTemplateDefinition>
            {
                Template(
                    "game.player.win.streak",
                    NewsEventType.PlayerGamePerformance,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.LeagueSportsMedia,
                    new[]
                    {
                        Equal(NewsFactKey.DidWin, 1),
                        AtLeast(NewsFactKey.TeamWinningStreak, 3)
                    },
                    new[]
                    {
                        "{PlayerName|이|가} {GamePerformanceSummary}, {TeamName} {TeamWinningStreak}연승 완성",
                        "{PlayerName} 활약 앞세운 {TeamName}, {TeamWinningStreak}연승"
                    },
                    new[] { "{PlayerName|이|가} 승부처에서 존재감을 보이며 팀의 상승세를 이어갔다." },
                    new[]
                    {
                        "{GameStatLine}. {TeamName|은|는} {OpponentName|을|를} {TeamRuns}대 {OpponentRuns}로 꺾었다. " +
                        "이번 승리로 연승 기록은 {TeamWinningStreak}경기까지 늘어났다."
                    }),
                Template(
                    "game.player.win",
                    NewsEventType.PlayerGamePerformance,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.LeagueSportsMedia,
                    new[] { Equal(NewsFactKey.DidWin, 1) },
                    new[]
                    {
                        "{PlayerName}, {GamePerformanceSummary}…{TeamName} 승리 이끌어",
                        "{PlayerName|이|가} 만든 승부처, {TeamName} {TeamRuns}대 {OpponentRuns} 승리"
                    },
                    new[] { "내 선수의 활약과 팀의 경기 결과가 한 흐름으로 이어졌다." },
                    new[]
                    {
                        "{PlayerName|은|는} {GameStatLine}을 기록했다. {TeamName|은|는} {OpponentName|을|를} " +
                        "{TeamRuns}대 {OpponentRuns}로 이겼다."
                    }),
                Template(
                    "game.player.loss",
                    NewsEventType.PlayerGamePerformance,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.LeagueSportsMedia,
                    new[] { Equal(NewsFactKey.DidLose, 1) },
                    new[]
                    {
                        "{PlayerName} {GamePerformanceSummary} 분전에도 {TeamName} 패배",
                        "{PlayerName|은|는} 빛났지만…{TeamName}, {OpponentName}에 패배"
                    },
                    new[] { "개인 활약은 분명했지만 팀은 경기 결과를 뒤집지 못했다." },
                    new[]
                    {
                        "{PlayerName|은|는} {GameStatLine}으로 제 몫을 했다. 그러나 {TeamName|은|는} " +
                        "{OpponentName}에 {TeamRuns}대 {OpponentRuns}로 패했다."
                    }),
                Template(
                    "game.player.tie",
                    NewsEventType.PlayerGamePerformance,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Brief,
                    NewsSourceType.LeagueSportsMedia,
                    new[] { Equal(NewsFactKey.DidTie, 1) },
                    new[] { "{PlayerName} {GamePerformanceSummary}, 승부는 {TeamRuns}대 {OpponentRuns} 무승부" },
                    new[] { "{GameStatLine}." },
                    new[] { "{TeamName|과|와} {OpponentName}의 경기는 승부를 가리지 못했다." }),
                Template(
                    "game.team.win.streak",
                    NewsEventType.GameCompleted,
                    NewsCategory.Game,
                    NewsArticleLength.Standard,
                    NewsSourceType.ClubNews,
                    new[]
                    {
                        Equal(NewsFactKey.DidWin, 1),
                        AtLeast(NewsFactKey.TeamWinningStreak, 3)
                    },
                    new[]
                    {
                        "{TeamName}, {OpponentName|을|를} 꺾고 {TeamWinningStreak}연승",
                        "{TeamName} 상승세 계속…{TeamWinningStreak}경기 연속 승리"
                    },
                    new[] { "{TeamRecordSummary}." },
                    new[] { "{TeamName|은|는} {OpponentName}에 {TeamRuns}대 {OpponentRuns}로 승리했다." }),
                Template(
                    "game.team.win",
                    NewsEventType.GameCompleted,
                    NewsCategory.Game,
                    NewsArticleLength.Brief,
                    NewsSourceType.ClubNews,
                    new[] { Equal(NewsFactKey.DidWin, 1) },
                    new[]
                    {
                        "{TeamName}, {OpponentName}에 {TeamRuns}대 {OpponentRuns} 승리",
                        "{TeamName|이|가} 지킨 리드…{OpponentName} 제압"
                    },
                    new[] { "{TeamRecordSummary}." },
                    new[]
                    {
                        "{TeamName|은|는} {OpponentName|을|를} {TeamRuns}대 {OpponentRuns}로 꺾고 승리를 추가했다."
                    }),
                Template(
                    "game.team.loss",
                    NewsEventType.GameCompleted,
                    NewsCategory.Game,
                    NewsArticleLength.Brief,
                    NewsSourceType.ClubNews,
                    new[] { Equal(NewsFactKey.DidLose, 1) },
                    new[]
                    {
                        "{TeamName}, {OpponentName}에 {TeamRuns}대 {OpponentRuns} 패배",
                        "{TeamName|은|는} 추격했지만 {OpponentName} 넘지 못해"
                    },
                    new[] { "{TeamRecordSummary}." },
                    new[]
                    {
                        "{TeamName|은|는} {OpponentName}에 {TeamRuns}대 {OpponentRuns}로 패했다. " +
                        "다음 경기에서 분위기 반전을 준비한다."
                    }),
                Template(
                    "game.team.tie",
                    NewsEventType.GameCompleted,
                    NewsCategory.Game,
                    NewsArticleLength.Brief,
                    NewsSourceType.ClubNews,
                    new[] { Equal(NewsFactKey.DidTie, 1) },
                    new[] { "{TeamName|과|와} {OpponentName}, {TeamRuns}대 {OpponentRuns} 무승부" },
                    new[] { "두 팀이 승부를 가리지 못했다." },
                    new[] { "{TeamRecordSummary}." }),
                new NewsTemplateDefinition(
                    "league.daily.briefing",
                    NewsEventType.LeagueBriefing,
                    NewsCategory.League,
                    NewsArticleLength.Brief,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        new NewsArticleVariant(
                            "leader_changed",
                            NewsSourceType.LeagueOfficial,
                            NewsTone.Neutral,
                            new[] { Equal(NewsFactKey.LeaderChanged, 1) },
                            System.Array.Empty<NewsTemplateCondition>(),
                            "선두 바뀌었다…{TeamName}, 리그 1위 도약",
                            "오늘 열린 {RoundGames}경기가 모두 끝난 가운데 {TeamName|이|가} " +
                            "{PreviousTeamRank}위에서 1위로 올라섰다.",
                            new[]
                            {
                                "오늘의 경기\n{RoundScoreSummary}",
                                "순위 변동\n{StandingChangeSummary}",
                                "내 팀\n{PlayerGameSummary}"
                            },
                            cooldownGroup: "leader_changed"),
                        new NewsArticleVariant(
                            "leader_held_daily",
                            NewsSourceType.LeagueOfficial,
                            NewsTone.Neutral,
                            new[] { Equal(NewsFactKey.LeaderChanged, 0) },
                            System.Array.Empty<NewsTemplateCondition>(),
                            "오늘의 리그: {RoundGames}경기 종료…{TeamName}, 선두 유지",
                            "오늘 열린 {RoundGames}경기가 모두 끝난 가운데 {TeamName|은|는} " +
                            "{TeamRecordSummary}로 1위를 지켰다.",
                            new[]
                            {
                                "오늘의 경기\n{RoundScoreSummary}",
                                "순위 변동\n{StandingChangeSummary}",
                                "내 팀\n{PlayerGameSummary}"
                            },
                            cooldownGroup: "leader_held_daily"),
                        new NewsArticleVariant(
                            "leader_held_standings",
                            NewsSourceType.LeagueOfficial,
                            NewsTone.Analytical,
                            new[] { Equal(NewsFactKey.LeaderChanged, 0) },
                            System.Array.Empty<NewsTemplateCondition>(),
                            "{RoundGames}경기 모두 종료…{TeamName} 1위 유지",
                            "{TeamName|이|가} {TeamRecordSummary}로 선두를 지킨 가운데 순위 경쟁이 이어졌다.",
                            new[]
                            {
                                "스코어\n{RoundScoreSummary}",
                                "오늘의 흐름\n{StandingChangeSummary}",
                                "내 선수\n{PlayerGameSummary}"
                            },
                            cooldownGroup: "leader_held_standings")
                    },
                    cooldownGroup: "league_briefing",
                    cooldownCycles: 1),
                Template(
                    "form.slump",
                    NewsEventType.PlayerFormChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    new[] { Equal(NewsFactKey.FormSlump, 1) },
                    new[]
                    {
                        "길어지는 침묵…{PlayerName} {HitlessStreak}경기 연속 무안타",
                        "{PlayerName}, 최근 타석에서 돌파구 필요",
                        "{PlayerName} 무안타 흐름 {HitlessStreak}경기째"
                    },
                    new[]
                    {
                        "{PlayerName|은|는} {GameStatLine}에 그치며 최근 흐름을 바꾸지 못했다.",
                        "무안타 경기가 이어지며 다음 출전의 의미가 커졌다."
                    },
                    new[]
                    {
                        "최근 {RecentFiveGames}경기 {RecentFiveAtBats}타수 {RecentFiveHits}안타. " +
                        "감독 신뢰는 {ManagerTrustBefore}에서 {ManagerTrustAfter}로 변했다.",
                        "현재 역할은 유지됐지만 다음 경기에서 반전의 계기가 필요해졌다."
                    },
                    cooldownGroup: "player_form_slump",
                    cooldownCycles: 2),
                Template(
                    "form.rebound",
                    NewsEventType.PlayerFormChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    new[] { Equal(NewsFactKey.FormRebound, 1) },
                    new[]
                    {
                        "{PlayerName}, {GamesSinceLastHit}경기 만에 안타…침묵 깼다",
                        "길었던 무안타 끝낸 {PlayerName}",
                        "{PlayerName}, 기다렸던 안타와 함께 다시 출발"
                    },
                    new[]
                    {
                        "{PlayerName|이|가} 안타를 기록하며 길었던 무안타 흐름을 끝냈다.",
                        "최근 결과가 좋지 않았던 {PlayerName|이|가} 반등의 출발점을 만들었다."
                    },
                    new[]
                    {
                        "{GameStatLine}. 이번 결과가 다음 경기까지 이어질지 주목된다.",
                        "최근 {RecentFiveGames}경기 기록은 {RecentFiveAtBats}타수 {RecentFiveHits}안타가 됐다."
                    },
                    cooldownGroup: "player_form_rebound",
                    cooldownCycles: 2),
                Template(
                    "form.hot",
                    NewsEventType.PlayerFormChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    new[] { Equal(NewsFactKey.FormHot, 1) },
                    new[]
                    {
                        "{PlayerName}, {HitStreak}경기 연속 안타…상승세 뚜렷",
                        "꾸준한 출루 이어가는 {PlayerName}"
                    },
                    new[] { "최근 경기의 결과가 한 번의 활약을 넘어 흐름으로 이어지고 있다." },
                    new[]
                    {
                        "최근 {RecentFiveGames}경기 {RecentFiveAtBats}타수 {RecentFiveHits}안타. " +
                        "{ManagerStyle} 감독은 \"{ManagerComment}\"라고 평가했다."
                    },
                    cooldownGroup: "player_form_hot",
                    cooldownCycles: 3),
                Template(
                    "form.cooled",
                    NewsEventType.PlayerFormChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Brief,
                    NewsSourceType.RegionalSports,
                    new[] { Equal(NewsFactKey.FormCooled, 1) },
                    new[] { "{PlayerName} 연속 안타 마감…다음 흐름 준비" },
                    new[] { "상승세는 멈췄지만 한 경기만으로 타격 흐름 전체를 판단하기는 이르다." },
                    new[] { "{GameStatLine}. 최근 기록은 다음 경기부터 새 구간으로 이어진다." },
                    cooldownGroup: "player_form_cooled",
                    cooldownCycles: 2),
                new NewsTemplateDefinition(
                    "report.weekly",
                    NewsEventType.WeeklyReport,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Brief,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        new NewsArticleVariant(
                            "weekly_regional",
                            NewsSourceType.RegionalSports,
                            NewsTone.Analytical,
                            System.Array.Empty<NewsTemplateCondition>(),
                            System.Array.Empty<NewsTemplateCondition>(),
                            "{ReportLabel}: {PlayerName}, {ReportAtBats}타수 {ReportHits}안타",
                            "최근 {ReportGames}경기에서 {PlayerName|은|는} {ReportHomeRuns}홈런 {ReportRbi}타점을 기록했다.",
                            new[]
                            {
                                "개인 흐름: {ReportTrend}",
                                "팀 결과: {ReportTeamWins}승 {ReportTeamLosses}패"
                            },
                            cooldownGroup: "weekly_regional"),
                        new NewsArticleVariant(
                            "weekly_club",
                            NewsSourceType.ClubNews,
                            NewsTone.Positive,
                            System.Array.Empty<NewsTemplateCondition>(),
                            System.Array.Empty<NewsTemplateCondition>(),
                            "{TeamName} 주간 결산…{PlayerName} 최근 흐름",
                            "최근 {ReportGames}경기의 선수 기록과 팀 결과를 함께 정리했다.",
                            new[]
                            {
                                "{PlayerName}: {ReportAtBats}타수 {ReportHits}안타 · {ReportHomeRuns}홈런 · {ReportRbi}타점",
                                "{TeamName}: {ReportTeamWins}승 {ReportTeamLosses}패"
                            },
                            cooldownGroup: "weekly_club")
                    },
                    cooldownGroup: "weekly_report",
                    cooldownCycles: 3),
                new NewsTemplateDefinition(
                    "report.monthly",
                    NewsEventType.MonthlyReport,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        new NewsArticleVariant(
                            "monthly_league",
                            NewsSourceType.LeagueSportsMedia,
                            NewsTone.Analytical,
                            System.Array.Empty<NewsTemplateCondition>(),
                            System.Array.Empty<NewsTemplateCondition>(),
                            "{ReportLabel}: {PlayerName}의 최근 {ReportGames}경기",
                            "긴 구간의 기록으로 현재 시즌 흐름을 점검했다.",
                            new[]
                            {
                                "개인 기록\n{ReportAtBats}타수 {ReportHits}안타 · {ReportHomeRuns}홈런 · {ReportRbi}타점",
                                "팀 성적\n{ReportTeamWins}승 {ReportTeamLosses}패",
                                "평가\n{ReportTrend}"
                            },
                            cooldownGroup: "monthly_league"),
                        new NewsArticleVariant(
                            "monthly_national",
                            NewsSourceType.NationalSports,
                            NewsTone.Dramatic,
                            System.Array.Empty<NewsTemplateCondition>(),
                            System.Array.Empty<NewsTemplateCondition>(),
                            "한 달의 기록이 말하는 {PlayerName}의 현재",
                            "{PlayerName|이|가} 최근 {ReportGames}경기에서 남긴 결과가 시즌의 다음 장면을 예고한다.",
                            new[]
                            {
                                "{ReportAtBats}타수 {ReportHits}안타, {ReportHomeRuns}홈런 {ReportRbi}타점.",
                                "소속팀은 이 구간 {ReportTeamWins}승 {ReportTeamLosses}패를 기록했다."
                            },
                            cooldownGroup: "monthly_national")
                    },
                    cooldownGroup: "monthly_report",
                    cooldownCycles: 8),
                Template(
                    "role.competition.started",
                    NewsEventType.RoleCompetitionChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    new[] { Equal(NewsFactKey.RoleCompetitionStarted, 1) },
                    new[]
                    {
                        "{PlayerName}, 주전 경쟁의 갈림길",
                        "감독 신뢰 {ManagerTrustAfter}…{PlayerName} 다음 경기 중요"
                    },
                    new[] { "최근 결과가 기용 판단의 기준선에 닿으며 선발 경쟁이 다시 열렸다." },
                    new[]
                    {
                        "감독 신뢰는 {ManagerTrustBefore}에서 {ManagerTrustAfter}로 변했다. " +
                        "감독은 \"{ManagerComment}\"라고 말했다."
                    },
                    cooldownGroup: "role_competition",
                    cooldownCycles: 4),
                Template(
                    "role.competition.resolved",
                    NewsEventType.RoleCompetitionChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.ClubNews,
                    new[] { Equal(NewsFactKey.RoleCompetitionResolved, 1) },
                    new[] { "{PlayerName}, 결과로 주전 경쟁 안정시켜" },
                    new[] { "최근 경기의 누적 평가가 기용 기준선을 다시 넘어섰다." },
                    new[] { "감독 신뢰는 {ManagerTrustBefore}에서 {ManagerTrustAfter}로 상승했다." },
                    cooldownGroup: "role_competition_resolved",
                    cooldownCycles: 4),
                Template(
                    "career.milestone.approaching",
                    NewsEventType.CareerMilestoneApproaching,
                    NewsCategory.RecordsAwards,
                    NewsArticleLength.Standard,
                    NewsSourceType.NationalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        "{PlayerName}, {MilestoneName} {MilestoneTarget} 고지 눈앞",
                        "다가오는 기록…{PlayerName} {MilestoneName} 도전"
                    },
                    new[] { "통산 기록이 다음 이정표까지 세 개 이하로 다가왔다." },
                    new[] { "기록 달성 여부는 이후 공식 경기 결과에서 확정된다." },
                    cooldownGroup: "milestone_approach",
                    cooldownCycles: 8),
                Template(
                    "career.milestone",
                    NewsEventType.CareerMilestoneReached,
                    NewsCategory.RecordsAwards,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    new[] { Exists(NewsFactKey.CareerMilestone) },
                    new[]
                    {
                        "{PlayerName}, {CareerMilestone} 달성",
                        "커리어에 남을 순간…{PlayerName} {CareerMilestone}"
                    },
                    new[] { "확정된 커리어 기록이 새로운 이정표에 도달했다." },
                    new[] { "{GameStatLine}. 이 기록은 커리어 연표에 영구 보관된다." }),
                Template(
                    "role.changed",
                    NewsEventType.PlayerRoleChanged,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Standard,
                    NewsSourceType.ClubNews,
                    new[] { Exists(NewsFactKey.NewRole) },
                    new[] { "{PlayerName}, {NewRole} 역할 확정" },
                    new[] { "감독의 기용 판단이 임시 계획이 아닌 공식 역할 변화로 확정됐다." },
                    new[] { "{PreviousRole}에서 {NewRole}로 역할이 바뀌었다." },
                    cooldownGroup: "role_change",
                    cooldownCycles: 2),
                Template(
                    "injury.confirmed",
                    NewsEventType.PlayerInjuryConfirmed,
                    NewsCategory.Injury,
                    NewsArticleLength.Feature,
                    NewsSourceType.ClubNews,
                    new[] { Exists(NewsFactKey.ExpectedAbsenceGames) },
                    new[] { "{PlayerName}, 부상 진단…{ExpectedAbsenceGames}경기 결장 예상" },
                    new[] { "진단이 확정된 뒤 구단이 예상 결장 기간을 공개했다." },
                    new[] { "뉴스는 부상 기간을 추측하지 않고 부상 시스템의 확정 결과만 전달한다." },
                    cooldownGroup: "injury",
                    cooldownCycles: 4),
                Template(
                    "injury.recovery.stage",
                    NewsEventType.InjuryRecoveryStageReached,
                    NewsCategory.Injury,
                    NewsArticleLength.Brief,
                    NewsSourceType.ClubNews,
                    new[] { Exists(NewsFactKey.RecoveryStage) },
                    new[] { "{PlayerName}, {RecoveryStage} 단계 진입" },
                    new[] { "재활 과정에서 공개할 가치가 있는 단계 변화다." },
                    new[] { "매주 반복하지 않고 실제 단계가 바뀔 때만 발행한다." }),
                Template(
                    "injury.return",
                    NewsEventType.PlayerReturnedFromInjury,
                    NewsCategory.Injury,
                    NewsArticleLength.Standard,
                    NewsSourceType.ClubNews,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, 재활 마치고 1군 복귀" },
                    new[] { "구단이 선수의 복귀를 공식 확정했다." },
                    new[] { "복귀전 결과는 실제 경기가 끝난 뒤 별도 기사에 반영된다." }),
                Template(
                    "contract.negotiation",
                    NewsEventType.ContractNegotiationReported,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        "{TeamName}, {PlayerName|과|와} 연장 계약 논의",
                        "{PlayerName} 잔류 가능성…구단 제안 도착"
                    },
                    new[] { "현재 구단이 확정 제안을 전달하며 계약 시즌이 시작됐다." },
                    new[] { "제안 조건은 {ContractYears}년, 연봉 {ContractSalary}원이다." },
                    cooldownGroup: "contract_negotiation",
                    cooldownCycles: 10),
                Template(
                    "contract.declined",
                    NewsEventType.ContractNegotiationDeclined,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, {TeamName} 연장 제안 거절" },
                    new[] { "선수는 시즌 중 제안을 받아들이지 않고 다음 선택지를 열어두기로 했다." },
                    new[] { "현재 계약은 기존 조건대로 유지된다." },
                    cooldownGroup: "contract_declined",
                    cooldownCycles: 10),
                Template(
                    "contract.signed",
                    NewsEventType.ContractSigned,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    new[] { Exists(NewsFactKey.ContractYears) },
                    new[] { "{PlayerName}, {TeamName|과|와} {ContractYears}년 계약 체결" },
                    new[] { "계약 제안 단계가 아니라 서명이 끝난 확정 계약이다." },
                    new[] { "공개 연봉은 {ContractSalary}원이다." },
                    cooldownGroup: "contract",
                    cooldownCycles: 1),
                Template(
                    "trade.interest",
                    NewsEventType.TradeInterestReported,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Brief,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{InterestedTeamName}, {PlayerName} 영입 관심" },
                    new[] { "구단 전력 평가에서 실제 관심 단계가 확인됐다." },
                    new[] { "예상 역할은 {ProjectedRole}이다. 아직 협상이나 이동이 확정된 단계는 아니다." },
                    cooldownGroup: "trade_interest",
                    cooldownCycles: 6),
                Template(
                    "trade.rumor",
                    NewsEventType.TradeRumorReported,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Standard,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        "{PlayerName} 이적설…{InterestedTeamName} 관심 구체화",
                        "{InterestedTeamName}, {PlayerName} 영입 검토"
                    },
                    new[] { "관심 단계가 루머 단계로 진전되며 시즌 중 이동 가능성이 수면 위로 올라왔다." },
                    new[] { "현재 예상 역할은 {ProjectedRole}이며 실제 협상 결과는 아직 확정되지 않았다." },
                    cooldownGroup: "trade_rumor",
                    cooldownCycles: 5),
                Template(
                    "trade.negotiating",
                    NewsEventType.TradeNegotiationReported,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Standard,
                    NewsSourceType.NationalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName} 트레이드 협상 단계…{InterestedTeamName} 행선지 후보" },
                    new[] { "관심 구단과 현재 구단의 평가가 협상 단계에 도달했다." },
                    new[] { "예상 역할은 {ProjectedRole}이다. 확정 발표 전까지 소속은 바뀌지 않는다." },
                    cooldownGroup: "trade_negotiating",
                    cooldownCycles: 4),
                Template(
                    "trade.completed",
                    NewsEventType.PlayerTraded,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, {PreviousTeamName} 떠나 {NewTeamName} 이적" },
                    new[] { "리그 등록이 완료되며 시즌 중 트레이드가 공식 확정됐다." },
                    new[] { "새 구단에서 예상되는 역할은 {ProjectedRole}이다." },
                    cooldownGroup: "trade_completed",
                    cooldownCycles: 1),
                Template(
                    "postseason.game.win",
                    NewsEventType.PostseasonGameCompleted,
                    NewsCategory.Postseason,
                    NewsArticleLength.Standard,
                    NewsSourceType.LeagueSportsMedia,
                    new[] { Equal(NewsFactKey.DidWin, 1) },
                    new[]
                    {
                        "{TeamName}, 포스트시즌 {OpponentName}전 {TeamRuns}대 {OpponentRuns} 승리",
                        "{TeamName|이|가} 먼저 웃었다…시리즈 전적 {PostseasonSeriesScore}"
                    },
                    new[] { "한 경기의 결과와 현재 시리즈 전적이 확정됐다." },
                    new[] { "포스트시즌 결과는 정규시즌 기록과 분리해 집계한다." }),
                Template(
                    "postseason.game.loss",
                    NewsEventType.PostseasonGameCompleted,
                    NewsCategory.Postseason,
                    NewsArticleLength.Standard,
                    NewsSourceType.LeagueSportsMedia,
                    new[] { Equal(NewsFactKey.DidLose, 1) },
                    new[]
                    {
                        "{TeamName}, 포스트시즌 {OpponentName}전 {TeamRuns}대 {OpponentRuns} 패배",
                        "{TeamName|은|는} 추격했지만…시리즈 전적 {PostseasonSeriesScore}"
                    },
                    new[] { "한 경기의 결과와 현재 시리즈 전적이 확정됐다." },
                    new[] { "포스트시즌 결과는 정규시즌 기록과 분리해 집계한다." }),
                Template(
                    "postseason.series.completed",
                    NewsEventType.PostseasonSeriesCompleted,
                    NewsCategory.Postseason,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, 시리즈 {PostseasonSeriesScore}로 다음 라운드 진출" },
                    new[] { "시리즈 승자가 확정되며 다음 대진으로 향한다." },
                    new[] { "{OpponentName|은|는} 이번 포스트시즌 일정을 마쳤다." }),
                Template(
                    "postseason.berth",
                    NewsEventType.PostseasonBerthClinched,
                    NewsCategory.Postseason,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, 포스트시즌 진출 확정" },
                    new[] { "정규시즌 순위가 확정되며 가을 무대 진출권을 얻었다." },
                    new[] { "대진 공개 이후 뉴스 피드에 활성화된 기사다." }),
                Template(
                    "postseason.eliminated",
                    NewsEventType.PostseasonEliminated,
                    NewsCategory.Postseason,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, 포스트시즌에서 탈락" },
                    new[] { "시리즈 결과가 확정되며 이번 시즌 도전을 마쳤다." },
                    new[] { "시리즈 최종 전적은 {PostseasonSeriesScore}이다." }),
                Template(
                    "postseason.champion",
                    NewsEventType.ChampionshipWon,
                    NewsCategory.Postseason,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, 결승 {PostseasonSeriesScore}로 통합 우승" },
                    new[] { "전용 결과 화면에서 우승팀이 공개된 뒤 발행된 기사다." },
                    new[] { "리그의 마지막 시리즈를 승리로 마치며 시즌 정상에 올랐다." }),
                Template(
                    "award.granted",
                    NewsEventType.SeasonAwardGranted,
                    NewsCategory.RecordsAwards,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    new[] { Exists(NewsFactKey.AwardName) },
                    new[] { "{PlayerName}, {AwardName} 수상" },
                    new[] { "동결된 시즌 기록으로 산정된 수상 결과다." },
                    new[] { "수상 보상은 기사와 독립된 결산 시스템에서 이미 처리된다." }),
                Template(
                    "offseason.activity",
                    NewsEventType.OffseasonActivityCompleted,
                    NewsCategory.Offseason,
                    NewsArticleLength.Standard,
                    NewsSourceType.ClubNews,
                    new[] { Exists(NewsFactKey.OffseasonActivityName) },
                    new[] { "{PlayerName}, {OffseasonActivityName} 마쳐" },
                    new[] { "외부에 알려질 가치가 있는 오프시즌 활동이 완료됐다." },
                    new[] { "스킬 블록 같은 시스템 용어 대신 실제 훈련 내용으로 전달한다." }),
                Template(
                    "league.promotion_race",
                    NewsEventType.PromotionRaceEntered,
                    NewsCategory.League,
                    NewsArticleLength.Brief,
                    NewsSourceType.LeagueSportsMedia,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, {LeagueName} 승격 경쟁 돌입" },
                    new[] { "시즌 막판 순위가 승격 구역에 들어왔다." },
                    new[] { "현재 정규시즌 순위는 {TeamRank}위다. 승격은 포스트시즌 결과와 별도로 정규시즌 순위로 결정된다." }),
                Template(
                    "league.promotion_clinched",
                    NewsEventType.PromotionClinched,
                    NewsCategory.League,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, {LeagueName} 승격 확정" },
                    new[] { "정규시즌 최종 순위로 다음 시즌 승격이 확정됐다." },
                    new[] { "포스트시즌 우승 여부와 관계없이 구단은 다음 시즌 {LeagueName}에서 경쟁한다." }),
                Template(
                    "league.relegation_risk",
                    NewsEventType.RelegationRiskEntered,
                    NewsCategory.League,
                    NewsArticleLength.Brief,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, {LeagueName} 강등권 진입" },
                    new[] { "시즌 막판 순위가 강등 구역으로 내려갔다." },
                    new[] { "현재 정규시즌 순위는 {TeamRank}위다. 남은 경기에서 잔류선을 되찾아야 한다." }),
                Template(
                    "league.relegation_confirmed",
                    NewsEventType.RelegationConfirmed,
                    NewsCategory.League,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, {LeagueName} 강등 확정" },
                    new[] { "정규시즌 최종 순위로 다음 시즌 강등이 확정됐다." },
                    new[] { "구단과 남은 계약은 유지되지만 오프시즌 로스터 재편으로 선수 역할은 다시 평가된다." }),
                Template(
                    "league.team_changed",
                    NewsEventType.TeamLeagueChanged,
                    NewsCategory.League,
                    NewsArticleLength.Brief,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{TeamName}, 다음 시즌 {LeagueName} 참가" },
                    new[] { "리그 승강 이동 계획이 전체 구단에 일괄 적용됐다." },
                    new[] { "구단의 영구 ID와 과거 기록은 그대로 유지된다." }),
                Template(
                    "contract.upper_interest",
                    NewsEventType.UpperLeagueInterestConfirmed,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Brief,
                    NewsSourceType.RegionalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, {LeagueName} 구단 관심 확인" },
                    new[] { "실제 구단의 포지션 수요와 경쟁자 평가를 통과한 관심이다." },
                    new[] { "아직 계약 서명 전이므로 소속 이동은 확정되지 않았다." }),
                Template(
                    "contract.cross_league_signed",
                    NewsEventType.CrossLeagueContractSigned,
                    NewsCategory.TransferContract,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, {TeamName|과|와} 계약하며 리그 이동" },
                    new[] { "계약 서명과 선수 등록이 모두 완료됐다." },
                    new[] { "새 구단의 포지션 경쟁을 거쳐 다음 시즌 역할이 결정된다." }),
                Template(
                    "career.galaxy_debut",
                    NewsEventType.GalaxyLeagueDebut,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Feature,
                    NewsSourceType.NationalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, Galaxy League 데뷔" },
                    new[] { "커리어 최상위 리그의 첫 공식 출전이 기록됐다." },
                    new[] { "이제 승격보다 Galaxy 우승과 통산 기록 경쟁이 새로운 목표가 된다." }),
                Template(
                    "career.first_league_reached",
                    NewsEventType.FirstLeagueReached,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, {LeagueName} 최초 진출" },
                    new[] { "커리어 최초로 새 리그 단계에 등록됐다." },
                    new[] { "첫 진출 보상은 커리어당 한 번만 지급되며 재강등 뒤 재진입에는 반복 지급되지 않는다." }),
                Template(
                    "career.final_season",
                    NewsEventType.FinalSeasonAnnounced,
                    NewsCategory.MyPlayer,
                    NewsArticleLength.Feature,
                    NewsSourceType.NationalSports,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, 올 시즌 끝으로 은퇴 선언" },
                    new[] { "현역 마지막 시즌이 공식 발표됐다." },
                    new[] { "시즌 종료 뒤 커리어 기록과 주요 장면을 회고한다." }),
                Template(
                    "career.player_retired",
                    NewsEventType.PlayerRetired,
                    NewsCategory.Offseason,
                    NewsArticleLength.Feature,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[] { "{PlayerName}, 현역 생활 마감" },
                    new[] { "계약 시장과 선수 의사를 거쳐 은퇴가 확정됐다." },
                    new[] { "선수의 시즌별 리그 기록과 통산 기록은 월드 역사에 영구 보존된다." })
            };
            return templates.ToArray();
        }

        private static NewsTemplateDefinition Template(
            string id,
            NewsEventType type,
            NewsCategory category,
            NewsArticleLength length,
            NewsSourceType source,
            NewsTemplateCondition[] conditions,
            string[] headlines,
            string[] leads,
            string[] bodies,
            string cooldownGroup = "",
            int cooldownCycles = 0)
        {
            return new NewsTemplateDefinition(
                id,
                type,
                category,
                length,
                source,
                conditions,
                headlines,
                leads,
                bodies,
                cooldownGroup,
                cooldownCycles);
        }

        private static NewsTemplateCondition Exists(NewsFactKey key) =>
            new(key, NewsFactComparison.Exists);

        private static NewsTemplateCondition Equal(NewsFactKey key, double value) =>
            new(key, NewsFactComparison.Equals, value);

        private static NewsTemplateCondition AtLeast(NewsFactKey key, double value) =>
            new(key, NewsFactComparison.GreaterOrEqual, value);
    }
}

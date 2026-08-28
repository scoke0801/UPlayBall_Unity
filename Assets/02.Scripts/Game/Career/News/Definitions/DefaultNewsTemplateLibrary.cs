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
                    new[] { "내 선수의 출전 여부와 별개로 구단의 확정 경기 결과를 전한다." }),
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
                    new[] { "{TeamName|은|는} 이날 승리를 추가하지 못했다." }),
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
                Template(
                    "league.daily.briefing",
                    NewsEventType.LeagueBriefing,
                    NewsCategory.League,
                    NewsArticleLength.Brief,
                    NewsSourceType.LeagueOfficial,
                    System.Array.Empty<NewsTemplateCondition>(),
                    new[]
                    {
                        "리그 브리핑: {TeamName} 선두, 순위 경쟁 계속",
                        "오늘의 리그: {RoundGames}경기 종료…{TeamName} {TeamRank}위"
                    },
                    new[] { "{TeamName} {TeamRecordSummary}." },
                    new[] { "같은 일정일의 모든 경기가 끝난 뒤 확정된 순위를 반영했다." },
                    cooldownGroup: "league_briefing",
                    cooldownCycles: 1),
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
                    new[] { "스킬 블록 같은 시스템 용어 대신 실제 훈련 내용으로 전달한다." })
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

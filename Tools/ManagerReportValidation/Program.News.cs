using System.Text.Json;
using Baseball.Core.Growth;
using Baseball.Game.Career;
using Baseball.Game.Guide;
using Baseball.Presentation.Guide;

namespace Baseball.Tools.ManagerReportValidation;

internal static partial class Program
{
    private static void RunNewsChecks()
    {
        Run("성장 원장 재수신·복원 중복 차단과 같은 주 실제 성장 합산", () =>
        {
            var state = new GuideProgressState(); Growth(state, 1);
            state.MarkAllReportsRead(); state = RoundTrip(state);
            Require(!Growth(state, 1)); Require(Growth(state, 2));
            Equal(1, state.GetReports().Count); Equal(4, state.GetReports()[0].news.growth[0]);
            Require(state.GetReports()[0].isRead);
            Growth(state, 1, "second", ManagerNewsKind.Study);
            Equal(1, state.GetReportCases(ManagerReportView.News).Count);
            Equal(2, state.GetReportCases(ManagerReportView.News)[0].Members.Count);
            Require(state.GetHomeNews() == null);
        });
        Run("성장·기록·회고가 겹쳐도 주당 홈 알림 두 묶음과 초과분 기록 유지", () =>
        {
            var state = new GuideProgressState(); Growth(state, 1);
            Game(state, 1, new ManagerNewsPlayerLine { CardId = "slugger", HomeRuns = 1, SeasonHomeRuns = 20 });
            for (int i = 2; i <= 6; i++) Game(state, i);
            Equal(3, state.GetReportCases(ManagerReportView.News).Count);
            Equal(2, state.GetReportCases(ManagerReportView.News).Count(x => x.HasUnread));
            Require(News(state, ManagerNewsKind.WeeklyReview).Single().isRead);
            state.MarkNewsCaseRead(state.GetHomeNews().Primary.reportId);
            state.MarkNewsCaseRead(state.GetHomeNews().Primary.reportId);
            Require(state.GetHomeNews() == null);
            state = RoundTrip(state); Growth(state, 2, week: 1);
            Require(state.GetHomeNews() != null);
            Reconcile(state, Array.Empty<GuideGoal>(), 1, 3);
            Require(state.GetHomeNews() == null); Equal(0, state.GetReportCases(ManagerReportView.News).Count);
        });
        Run("기록 기준을 실제로 넘긴 정규시즌 경기만 발행하며 저장 후 재수신 무시", () =>
        {
            var state = new GuideProgressState();
            var line = new ManagerNewsPlayerLine { CardId = "pitcher", Strikeouts = 2, SeasonStrikeouts = 101 };
            Require(!state.RecordOfficialGame(1, 1, 6, false, CompetitionScope.Postseason, 1, 0, new[] { line }));
            Game(state, 1, line); state = RoundTrip(state);
            Require(!state.RecordOfficialGame(1, 1, 6, false, CompetitionScope.RegularSeason, 1, 0, new[] { line }));
            Game(state, 2, new ManagerNewsPlayerLine { CardId = "pitcher", Strikeouts = 1, SeasonStrikeouts = 102 });
            Equal(100, News(state, ManagerNewsKind.StrikeoutMilestone).Single().news.milestone);
        });
        Run("주간 회고의 승패·득실점·불펜 비교는 완전한 공식 경기 합계", () =>
        {
            var state = new GuideProgressState();
            for (int i = 1; i <= 12; i++)
            {
                Game(state, i, Pitcher(walks: i <= 6 ? 2 : 0));
                if (i == 8) state = RoundTrip(state);
            }
            var reports = News(state, ManagerNewsKind.WeeklyReview).OrderBy(x => x.createdWeek).ToArray();
            Equal(2, reports.Length); Require(!reports[0].news.hasComparison); Require(reports[1].news.hasComparison);
            Equal(6, reports[1].news.games); Equal(6, reports[1].news.wins);
            Equal(24, reports[1].news.runsFor); Equal(12, reports[1].news.runsAgainst);
            Equal(12, reports[1].news.previousWalks); Equal(18, reports[1].news.bullpenOuts);
        });
        Run("표본 부족·누락·시즌 변경은 불펜 변화 해석을 만들지 않음", () =>
        {
            var state = new GuideProgressState();
            for (int i = 1; i <= 12; i++) Game(state, i, Pitcher(outs: 1, walks: i <= 6 ? 3 : 0));
            Require(News(state, ManagerNewsKind.WeeklyReview).All(x => !x.news.hasComparison));
            var gap = new GuideProgressState();
            for (int i = 2; i <= 12; i++) if (i != 8) Game(gap, i);
            Equal(0, News(gap, ManagerNewsKind.WeeklyReview).Count());
            for (int i = 1; i <= 6; i++) Game(state, i, Pitcher(), season: 2);
            Require(!News(state, ManagerNewsKind.WeeklyReview).Single(x => x.createdSeason == 2).news.hasComparison);
        });
        Run("마지막 짧은 주도 실제 받은 경기 수만 회고", () =>
        {
            var state = new GuideProgressState();
            for (int i = 1; i <= 8; i++) Game(state, i, last: i == 8);
            Equal(2, News(state, ManagerNewsKind.WeeklyReview).Single(x => x.createdWeek == 1).news.games);
        });
        Run("기용 유지 이후 충분한 표본에서 한 번 발행하고 같은 동의 재호출로 재시작하지 않음", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            Game(state, 1, Pitcher()); Game(state, 2, Pitcher());
            Equal(0, News(state, ManagerNewsKind.DecisionFollowup).Count());
            state = RoundTrip(state); Reconcile(state, Goals(), 1, 0); Game(state, 3, Pitcher());
            var evidence = News(state, ManagerNewsKind.DecisionFollowup).Single().news;
            Equal(3, evidence.appearances); Equal(9, evidence.outs); Require(!evidence.roleChanged);
            state.AcceptAsIs("slot0");
            for (int i = 4; i <= 9; i++) Game(state, i, Pitcher());
            Equal(1, News(state, ManagerNewsKind.DecisionFollowup).Count());
        });
        Run("실제 선발·불펜 역할이 바뀌면 이전 표본을 섞지 않음", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            Game(state, 1, Pitcher()); Game(state, 2, Pitcher());
            Game(state, 3, Pitcher(starter: true)); Game(state, 4, Pitcher(starter: true));
            Equal(0, News(state, ManagerNewsKind.DecisionFollowup).Count());
            Game(state, 5, Pitcher(starter: true));
            var evidence = News(state, ManagerNewsKind.DecisionFollowup).Single().news;
            Require(evidence.roleChanged); Equal(3, evidence.appearances); Equal(9, evidence.outs);
        });
        Run("적은 이닝·다시 검토·관찰 기한 만료에서 억지 후속 없음", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            for (int i = 1; i <= 12; i++) Game(state, i, Pitcher(outs: 0));
            for (int i = 13; i <= 18; i++) Game(state, i, Pitcher());
            Equal(0, News(state, ManagerNewsKind.DecisionFollowup).Count());
            var cancelled = Create(); cancelled.AcceptAsIs("slot0"); cancelled.Reconsider("slot0");
            for (int i = 1; i <= 6; i++) Game(cancelled, i, Pitcher());
            Equal(0, News(cancelled, ManagerNewsKind.DecisionFollowup).Count());
        });
        Run("타자 기용 관찰은 최소 타석을 충족해야 발행", () =>
        {
            var state = new GuideProgressState();
            var goal = new GuideGoal("batter", GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, false,
                "PositionMismatch", cardId: "batter");
            Reconcile(state, new[] { goal }, 1, 0); state.AcceptAsIs(goal.Key);
            for (int i = 1; i <= 4; i++) Game(state, i, new ManagerNewsPlayerLine { CardId = "batter", PlateAppearances = 4, Hits = 1 });
            Equal(0, News(state, ManagerNewsKind.DecisionFollowup).Count());
            Game(state, 5, new ManagerNewsPlayerLine { CardId = "batter", PlateAppearances = 4, Hits = 2 });
            Equal(20, News(state, ManagerNewsKind.DecisionFollowup).Single().news.plateAppearances);
        });
        Run("20시즌 소식 정리 후에도 수신 원장은 보존되고 복제되지 않음", () =>
        {
            var state = new GuideProgressState();
            for (int season = 1; season <= 20; season++)
            {
                Reconcile(state, Array.Empty<GuideGoal>(), season, 0);
                for (int week = 0; week < 24; week++) Growth(state, (season - 1) * 24 + week + 1, season: season, week: week);
                state = RoundTrip(state);
            }
            Require(state.GetReports().Count <= 50);
            Require(!Growth(state, 1, season: 1));
            Equal(480, state.GetReports().Count + state.GetReportSummaries().Sum(x => x.closedTargets));
        });
        Run("저장 근거는 깊은 복사이며 손상된 성장 배열·음수 집계 거부", () =>
        {
            var state = new GuideProgressState(); Growth(state, 1);
            var data = state.Capture(); data.reports[0].news.growth[0] = 99;
            Equal(2, state.GetReports()[0].news.growth[0]);
            data.reports[0].news.growth = new int[1]; Reject(() => GuideProgressState.Restore(data));
            data = state.Capture(); data.newsProgress.weekly.games = -1;
            Reject(() => GuideProgressState.Restore(data));
        });
        Run("저작 문구는 실제 성장값·공식 범위·안전한 이닝과 관찰 한계를 표시", () =>
        {
            using var json = JsonDocument.Parse(File.ReadAllText("Assets/10.Datas/Resources/FrontManager/OwnerGuidePresentation.json"));
            var copy = JsonSerializer.Deserialize<OwnerManagerNewsCopy>(json.RootElement.GetProperty("newsText").GetRawText(), JsonOptions);
            var state = new GuideProgressState(); Growth(state, 1);
            string growth = OwnerManagerNewsFormatter.FormatBody(state.GetReports()[0].news, copy);
            Require(growth.Contains(PlayerAbilityCatalog.GetDisplayName((PlayerAbility)0) + " +2"));
            string observation = OwnerManagerNewsFormatter.FormatBody(new ManagerNewsEvidence {
                kind = ManagerNewsKind.DecisionFollowup, appearances = 3, outs = 10 }, copy);
            Require(observation.Contains("3.1") && observation.Contains(copy.observedOnly));
            string empty = OwnerManagerNewsFormatter.FormatBody(new ManagerNewsEvidence { kind = ManagerNewsKind.WeeklyReview }, copy);
            Require(!empty.Contains("NaN") && !empty.Contains("Infinity"));
        });
        RunIntegrationChecks();
    }

    private static bool Growth(GuideProgressState state, int sequence, string card = "card", ManagerNewsKind kind = ManagerNewsKind.Training,
        int season = 1, int week = 0)
    {
        var gains = new int[PlayerAbilityCatalog.AbilityCount]; gains[0] = 2;
        return state.PublishGrowth(kind + ":" + card, sequence, card, season, week, kind, "", gains);
    }
    private static void Game(GuideProgressState state, int ordinal, ManagerNewsPlayerLine player = null, int season = 1, bool last = false) =>
        state.RecordOfficialGame(season, ordinal, 6, last, CompetitionScope.RegularSeason, 4, 2,
            player == null ? Array.Empty<ManagerNewsPlayerLine>() : new[] { player });
    private static ManagerNewsPlayerLine Pitcher(int outs = 3, int walks = 0, bool starter = false) =>
        new() { CardId = "card0", HasPitchingLine = true, StartedPitching = starter, Outs = outs, Walks = walks };
    private static IEnumerable<ManagerReportData> News(GuideProgressState state, ManagerNewsKind kind) =>
        state.GetReports().Where(x => x.news?.kind == kind);
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new InvalidOperationException("손상 입력을 허용했습니다.");
    }
}

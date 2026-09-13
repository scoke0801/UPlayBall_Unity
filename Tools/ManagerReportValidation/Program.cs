using System.Text.Json;
using Baseball.Core.Historical;
using Baseball.Game.Guide;

namespace Baseball.Tools.ManagerReportValidation;

/// <summary>Unity 에디터 없이 실제 Game 소스의 안건·저장·장기 이력 계약을 검증한다.</summary>
internal static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private static int _passed;

    private static void Main()
    {
        Run("슬롯별 경고 세 개를 한 안건으로 묶고 개별 이동 정보 보존", () =>
        {
            var state = Create();
            var item = state.GetReportCases(ManagerReportView.Current).Single();
            Equal(3, item.Members.Count);
            Equal(3, item.Members.Select(x => x.ToGoal().CardId).Distinct().Count());
            Equal(1, state.GetSuggestionGoals().Count);
            Require(!item.HasUpdates);
        });
        Run("동일 입력 100회와 경기 전환에서 최초 발생·읽음·순서 보존", () =>
        {
            var state = Create(); state.MarkAllReportsRead();
            string before = Describe(state);
            for (int i = 0; i < 100; i++) Reconcile(state, Goals(), 1, i / 5, "game" + i);
            Equal(before, Describe(state));
        });
        Run("실제 JSON 저장 복원 뒤에도 새 경고와 복제 없음", () =>
        {
            var state = Create(); state.MarkAllReportsRead();
            state = RoundTrip(state); Reconcile(state, Goals(), 1, 2);
            Equal(3, state.GetReports().Count); Equal(0, state.GetSuggestionGoals().Count);
        });
        Run("일부 대상 해소는 인원 감소만 반영하고 새 배지 없음", () =>
        {
            var state = Create(); state.MarkAllReportsRead();
            Reconcile(state, Goals().Take(2).ToArray(), 1, 1);
            var item = state.GetReportCases(ManagerReportView.Current).Single();
            Equal(2, item.Members.Count); Require(!item.HasUnread);
            Equal(1, state.GetReportCases(ManagerReportView.History).Single().Members.Count);
        });
        Run("새 대상만 읽지 않음으로 추가되고 기존 기용 판단은 유지", () =>
        {
            var state = Create();
            foreach (var goal in Goals()) Require(state.AcceptAsIs(goal.Key));
            Reconcile(state, Goals().Append(Goal(3)).ToArray(), 1, 0);
            Equal(3, state.GetReports().Count(x => x.isAccepted));
            Equal("card3", state.GetSuggestionGoals().Single().CardId);
            Require(state.GetReportCases(ManagerReportView.Current).Single().HasUpdates);
        });
        Run("기용 유지는 다음 경기·시즌·저장 복원 이후에도 유효", () =>
        {
            var state = Create(); foreach (var goal in Goals()) state.AcceptAsIs(goal.Key);
            string id = state.FindReportId("slot0");
            state = RoundTrip(state); Reconcile(state, Goals(), 2, 0);
            Equal(id, state.FindReportId("slot0")); Equal(0, state.GetSuggestionGoals().Count);
            Require(state.GetReportCases(ManagerReportView.Current).Single().IsAccepted);
            Require(state.IsCurrentTarget(Goal(0)));
        });
        Run("실제 불이익 악화는 보관을 유지하며 다시 판단", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            string id = state.FindReportId("slot0"); state.SetReportBookmark(id, true);
            Reconcile(state, new[] { Goal(0, penalty: 8), Goal(1), Goal(2) }, 1, 4);
            var report = state.GetReports().Single(x => x.reportId == id);
            Require(!report.isAccepted && !report.isRead && report.isBookmarked);
            Equal(0, report.createdWeek); Equal(4, report.updatedWeek);
        });
        Run("불이익 감소는 기용 유지와 읽음을 깨지 않음", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            Reconcile(state, new[] { Goal(0, penalty: 1), Goal(1), Goal(2) }, 1, 1);
            Require(state.GetReports().Single(x => x.deduplicationKey == "slot0").isAccepted);
        });
        Run("같은 슬롯의 선수 교체는 이전 동의를 승계하지 않음", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            Reconcile(state, new[] { Goal(0, card: "replacement"), Goal(1), Goal(2) }, 1, 1);
            Require(!state.GetReports().Single(x => x.deduplicationKey == "slot0").isAccepted);
            Require(!state.IsCurrentTarget(Goal(0)));
        });
        Run("필수 오류는 유지 불가이며 선택 경고 묶음과 분리", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0");
            Reconcile(state, new[] { Goal(0, required: true), Goal(1), Goal(2) }, 1, 1);
            Require(!state.AcceptAsIs("slot0"));
            Equal(2, state.GetReportCases(ManagerReportView.Current).Count);
            Require(state.GetSuggestionGoals()[0].IsRequired);
            state.SnoozeReport(state.FindReportId("slot0"));
            Equal(ManagerReportPriority.Critical, state.GetReportCases(ManagerReportView.Current)[0].Primary.priority);
        });
        Run("보관은 열람과 독립이며 해제도 새 알림을 만들지 않음", () =>
        {
            var state = Create(); string id = state.FindReportId("slot0");
            state.SetReportBookmark(id, true);
            Require(!state.GetReports().Single(x => x.reportId == id).isRead);
            state.MarkAllReportsRead(); state.SetReportBookmark(id, false);
            Equal(0, state.GetSuggestionGoals().Count);
        });
        Run("보류는 같은 주 다음 경기·저장 후 유지, 다음 주 배지 없이 복귀", () =>
        {
            var state = Create(); state.SnoozeReport(state.FindReportId("slot0"));
            state = RoundTrip(state); Reconcile(state, Goals(), 1, 0, "next-game");
            Equal(GuideGoalStatus.Snoozed, state.GetStatus("slot0"));
            Reconcile(state, Goals(), 1, 1);
            Equal(GuideGoalStatus.Pending, state.GetStatus("slot0"));
            Require(state.GetReports().Single(x => x.deduplicationKey == "slot0").isRead);
        });
        Run("시즌 번호 생략 호출에서도 시즌 ID 변경 시 보류 종료", () =>
        {
            var state = new GuideProgressState();
            state.Reconcile("a", 3, Goals(), "season1"); state.Snooze("slot0", 4);
            state.Reconcile("b", 0, Goals(), "season2");
            Equal(GuideGoalStatus.Pending, state.GetStatus("slot0"));
        });
        Run("해소 후 재발은 같은 원본을 재사용하고 발생 회차 표시", () =>
        {
            var state = Create(); string id = state.FindReportId("slot0");
            Reconcile(state, Array.Empty<GuideGoal>(), 1, 1); Reconcile(state, Goals(), 1, 2);
            Equal(3, state.GetReports().Count); Equal(id, state.FindReportId("slot0"));
            Equal(2, state.GetOccurrence("slot0")); Require(state.GetReportCases(ManagerReportView.Current).Single().HasUnread);
        });
        Run("기용 유지 후 다시 검토는 편성 변경 없이 현재 대상으로 복귀", () =>
        {
            var state = Create(); state.AcceptAsIs("slot0"); Require(state.Reconsider("slot0"));
            Equal(GuideGoalStatus.Pending, state.GetStatus("slot0")); Require(state.IsCurrentTarget(Goal(0)));
        });
        Run("원본 Validator 역할과 실제 적용 불이익 전달", () =>
        {
            var result = new LineupPresetValidationResult("preset", new[] {
                new LineupPresetValidationIssue(LineupPresetValidationIssueCode.PitcherRoleMismatch,
                    LineupPresetIssueSeverity.Warning, LineupPresetAssignmentGroup.Bullpen, 0, "card", "Starter->MiddleRelief", 4) });
            var goal = OwnerGuideGoalProvider.Create(null, result, true, "").Single();
            Equal("Starter->MiddleRelief", goal.Context); Equal(4, goal.ConditionPenalty);
        });
        Run("열람형 안내의 완료 상태를 재평가로 되돌리지 않음", () =>
        {
            var goal = new GuideGoal("analysis", GuideGoalKind.Preparation, GuideTargetKind.Analysis, false, "");
            var state = new GuideProgressState(); Reconcile(state, new[] { goal }, 1, 0);
            Require(state.RecordArrival(goal.Key, GuideArrivalStatus.TargetReady));
            state = RoundTrip(state); Reconcile(state, new[] { goal }, 1, 0);
            Equal(0, state.GetVisibleGoals().Count);
        });
        Run("그룹별 순서는 열람·입력 순서와 무관", () =>
        {
            var goals = Goals().Append(Goal(3, required: true)).ToArray();
            var left = new GuideProgressState(); var right = new GuideProgressState();
            Reconcile(left, goals, 1, 0); Reconcile(right, goals.Reverse().ToArray(), 1, 0);
            Equal(CaseKeys(left), CaseKeys(right)); left.MarkAllReportsRead(); Equal(CaseKeys(left), CaseKeys(right));
        });
        Run("중복 입력 거부 시 기존 저장 상태가 손상되지 않음", () =>
        {
            var state = Create(); string before = JsonSerializer.Serialize(state.Capture(), JsonOptions);
            try { Reconcile(state, new[] { Goal(0), Goal(0) }, 2, 0); throw new Exception("중복 입력 허용"); }
            catch (ArgumentException) { }
            Equal(before, JsonSerializer.Serialize(state.Capture(), JsonOptions));
        });
        Run("20시즌·2880경기 진행에도 동일 안건은 복제되지 않음", () =>
        {
            var state = Create(); state.MarkAllReportsRead();
            for (int season = 1; season <= 20; season++)
            {
                state = RoundTrip(state);
                for (int game = 0; game < 144; game++) Reconcile(state, Goals(), season, game / 6, "game" + game);
            }
            Equal(3, state.GetReports().Count); Equal(0, state.GetSuggestionGoals().Count);
        });
        Run("장기 종료 상세는 정책 상한을 지키며 보관과 시즌별 합계 보존", () =>
        {
            var state = new GuideProgressState();
            var policy = new ManagerReportPolicy { retainedSeasons = 2, maximumHistoryTargets = 8, maximumBookmarks = 1 };
            state.ConfigureReportPolicy(policy);
            for (int season = 1; season <= 20; season++)
            {
                for (int game = 0; game < 10; game++)
                {
                    var goal = new GuideGoal(season + ":" + game, GuideGoalKind.PresetIssue,
                        GuideTargetKind.PresetSlot, false, "MissingAssignment");
                    Reconcile(state, new[] { goal }, season, game);
                    if (season == 1 && game == 0) state.SetReportBookmark(state.FindReportId(goal.Key), true);
                }
                state = RoundTrip(state); state.ConfigureReportPolicy(policy);
            }
            Reconcile(state, Array.Empty<GuideGoal>(), 20, 11);
            Equal(1, state.GetReports().Count(x => x.isBookmarked));
            Require(state.GetReports().Count <= 9);
            Equal(200, state.GetReports().Count + state.GetReportSummaries().Sum(x => x.closedTargets));
            Require(!state.CanBookmarkReport(state.GetReports().First(x => !x.isBookmarked).reportId));
        });
        Run("기존 48건 반복 fixture도 최신 대상 세 명과 안건 한 장만 현재에 표시", () =>
        {
            var data = Create().Capture();
            var reports = new List<ManagerReportData>();
            for (int repetition = 0; repetition < 16; repetition++)
                foreach (var source in data.reports)
                {
                    var report = JsonSerializer.Deserialize<ManagerReportData>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions);
                    report.sequence = reports.Count + 1; report.reportId = report.sequence.ToString();
                    report.isRead = true; reports.Add(report);
                }
            data.reports = reports.ToArray(); data.reportSequence = reports.Count;
            var state = GuideProgressState.Restore(data); Reconcile(state, Goals(), 1, 0);
            Equal(1, state.GetReportCases(ManagerReportView.Current).Count);
            Equal(3, state.GetReportCases(ManagerReportView.Current).Single().Members.Count);
            Equal(0, state.GetSuggestionGoals().Count);
            Equal(45, state.GetReports().Count(x => x.isExpired));
        });
        Run("등록 인원이 기준에 가까워진 것은 새 경고가 아님", () =>
        {
            var state = new GuideProgressState();
            GuideGoal Roster(int actual) => new("roster", GuideGoalKind.RosterIssue, GuideTargetKind.Roster, true,
                "TotalCount", actual: actual, expected: 25);
            Reconcile(state, new[] { Roster(23) }, 1, 0); state.MarkAllReportsRead();
            Reconcile(state, new[] { Roster(24) }, 1, 1); Equal(0, state.GetSuggestionGoals().Count);
            Reconcile(state, new[] { Roster(22) }, 1, 2); Equal(1, state.GetSuggestionGoals().Count);
        });
        RunNewsChecks();
        Console.WriteLine($"매니저 리포트 콘솔 검증 {_passed}건 통과. Unity 에디터 테스트는 실행하지 않았습니다.");
    }

    private static GuideGoal Goal(int slot, string card = null, int penalty = 4, bool required = false) =>
        new("slot" + slot, GuideGoalKind.PresetIssue, GuideTargetKind.PresetSlot, required,
            "PitcherRoleMismatch", LineupPresetAssignmentGroup.Bullpen, slot, card ?? "card" + slot,
            "preset", context: "Starter->MiddleRelief", conditionPenalty: penalty);
    private static GuideGoal[] Goals() => new[] { Goal(0), Goal(1), Goal(2) };
    private static GuideProgressState Create() { var state = new GuideProgressState(); Reconcile(state, Goals(), 1, 0); return state; }
    private static void Reconcile(GuideProgressState state, GuideGoal[] goals, int season, int week, string game = "game") =>
        state.Reconcile(season + ":" + game, week, goals, "season" + season, season);
    private static GuideProgressState RoundTrip(GuideProgressState state) => GuideProgressState.Restore(
        JsonSerializer.Deserialize<GuideProgressData>(JsonSerializer.Serialize(state.Capture(), JsonOptions), JsonOptions));
    private static string Describe(GuideProgressState state) => string.Join("|", state.GetReports().OrderBy(x => x.reportId)
        .Select(x => $"{x.reportId}:{x.isRead}:{x.createdSeason}:{x.createdWeek}:{x.updatedSeason}:{x.updatedWeek}"));
    private static string CaseKeys(GuideProgressState state) => string.Join("|", state.GetReportCases(ManagerReportView.Current)
        .Select(x => x.Key + ":" + string.Join(",", x.Members.Select(m => m.deduplicationKey))));
    private static void Run(string name, Action check) { check(); _passed++; Console.WriteLine("통과: " + name); }
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("검증 조건 불충족"); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"기대 {expected}, 실제 {actual}"); }
}

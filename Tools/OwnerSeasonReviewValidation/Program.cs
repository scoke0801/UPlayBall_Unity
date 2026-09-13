using System;
using System.Linq;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;

// 에디터를 실행하지 않고 수상 자격·동률·소속·집계 범위를 검증한다.
int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    checks++;
}
void Set(object target, string name, int value) => target.GetType().GetProperty(name).SetValue(target, value);
ManagerLiveSeasonState Season(bool complete)
{
    var games = new ScheduledGameState[10];
    for (int i = 0; i < games.Length; i++)
    {
        games[i] = new ScheduledGameState(i + 1, i + 1, (ulong)i, 1, 2);
        if (complete) games[i].Complete(3, 2);
    }
    return new ManagerLiveSeasonState("review", 1, 2026, 0, 1,
        new[] { new ManagerTeamReference(1, "ours"), new ManagerTeamReference(2, "opponent") },
        new SeasonScheduleState(games));
}
void Populate(ManagerLiveSeasonState season, bool reverse)
{
    for (int i = 0; i < 9; i++)
    {
        int id = reverse ? 9 - i : i + 1;
        var p = season.Statistics.RegularSeason.GetOrCreate(id, "선수" + id, id == 1 ? 1 : 2, PlayerPosition.FirstBase);
        Set(p.Batting, "PlateAppearances", id == 9 ? 30 : 40);
        Set(p.Batting, "AtBats", 30);
        Set(p.Batting, "Hits", id == 9 ? 30 : 10);
        Set(p.Batting, "HomeRuns", 5);
        Set(p.Batting, "StolenBases", id == 1 ? 3 : 0);
        Set(p.Pitching, "Appearances", 2);
        Set(p.Pitching, "OutsRecorded", id == 9 ? 29 : 30);
        Set(p.Pitching, "EarnedRuns", id == 9 ? 0 : 2);
    }
    var postseason = season.Statistics.Postseason.GetOrCreate(1, "선수1", 1, PlayerPosition.FirstBase);
    Set(postseason.Batting, "HomeRuns", 100);
}
var season = Season(true);
Populate(season, false);
var report = OwnerSeasonHonorsReview.Create(season);
Check(report.IsFinal, "종료 시즌 수상 확정");
Check(report.HomeRuns == 5 && report.StolenBases == 3 && report.Hits == 10, "우리 구단 정규시즌 합계만 사용");
Check(Math.Abs(report.EarnedRunAverage - 1.8) < 0.00001, "팀 평균자책점 계산");
Check(report.Titles.Count(x => x.Metric == CareerRecordMetric.HomeRuns) == 9, "페이지 크기를 넘는 공동 수상 보존");
Check(report.Titles.Where(x => x.Metric == CareerRecordMetric.HomeRuns).All(x => x.IsShared), "공동 수상 표시");
Check(report.Titles.Where(x => x.Metric == CareerRecordMetric.BattingAverage).All(x => x.PlayerName != "선수9"), "규정 31타석 미달 제외");
Check(report.Titles.Where(x => x.Metric == CareerRecordMetric.EarnedRunAverage).All(x => x.PlayerName != "선수9"), "규정 30아웃 미달 제외");
Check(report.Titles.All(x => x.Metric != CareerRecordMetric.Saves), "0개 기록에 수상 없음");
Check(report.Titles.Where(x => x.IsOurPlayer).All(x => x.TeamSeasonKey == "ours"), "우리 구단 필터 식별");
Check(!season.Statistics.RegularSeason.IsFrozen && !season.Statistics.Postseason.IsFrozen, "조회가 시즌 동결 상태를 변경하지 않음");
var reverse = Season(true);
Populate(reverse, true);
string Signature(OwnerSeasonHonorsReview source) => string.Join("|", source.Titles.Select(x => $"{x.Metric}/{x.PlayerName}/{x.Value}"));
Check(Signature(report) == Signature(OwnerSeasonHonorsReview.Create(reverse)), "입력 순서와 무관한 결정론");
var pending = Season(false);
Populate(pending, false);
Check(OwnerSeasonHonorsReview.Create(pending).Titles.Count == 0, "시즌 중 수상 확정 금지");
Check(OwnerSeasonHonorsReview.Create(Season(true)).Titles.Count == 0, "빈 기록 안전 처리");
Console.WriteLine($"시즌 결산 콘솔 검증 {checks}건 통과");

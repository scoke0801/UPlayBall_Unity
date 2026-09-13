using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;

var cards = new[] { Card("a", TacticTier.Normal), Card("b", TacticTier.Rare),
    Card("c", TacticTier.Special, true), Card("d", TacticTier.Signature, true) };
var games = Enumerable.Range(1, 12).Select(i => new ScheduledGameState(i, i, (ulong)i, i % 2 == 0 ? 1 : 2, i % 2 == 0 ? 2 : 1)).ToArray();
var stock = new Dictionary<string, int> { ["a"] = 12, ["b"] = 12, ["c"] = 12, ["d"] = 12 };
int checks = 0;
IReadOnlyList<TacticAutoGamePlan> Build(TacticAutoOptions options) => TacticAutoPlanner.Build(games, 1, games[0], new[] { "b" }, cards, id => stock[id], options, 10);
void Check(bool valid, string name) { if (!valid) throw new Exception(name); checks++; Console.WriteLine("통과: " + name); }
var options = new TacticAutoOptions { GameCount = 10 };
var plan = Build(options);
Check(plan.Count == 10 && plan[0].CardIds.Contains("b"), "10경기 제한과 다음 경기 기본 카드 보존");
Check(plan.All(p => p.CardIds.Count == 2 && p.CardIds.Distinct().Count() == 2), "경기당 두 장과 중복 금지");
Check(plan.All(p => !p.CardIds.Contains("c") && !p.CardIds.Contains("d")), "방해 카드 제외");
games[11].PlanTactics(new[] { "a" }); stock["a"] = 2;
plan = Build(options);
Check(plan.Sum(p => p.CardIds.Count(id => id == "a")) == 1, "범위 밖 경기 예약 수량 보호");
options.Policy = TacticAutoPolicy.Replace; options.Priority = TacticAutoPriority.HigherTier; options.CanUseDisruption = true;
plan = Build(options);
Check(plan.All(p => p.CardIds.Count(id => id == "c" || id == "d") <= 1), "방해 카드 경기당 한 장 제한");
Check(plan[0].CardIds[0] == "d", "높은 등급 우선");
options.Venue = TacticAutoVenue.Away; options.GameCount = 3;
Check(Build(options).Count == 1 && Build(options)[0].GameId == 2, "다음 세 경기 안에서 원정 필터");
options.MaximumTier = TacticTier.Normal;
Check(Build(options).All(p => p.CardIds.All(id => id == "a")), "등급 상한");
options.Category = TacticCardCategory.Pitching;
Check(Build(options).All(p => p.CardIds.Count == 0), "후보 없는 교체는 해제 미리보기");
options.Category = null; options.Venue = TacticAutoVenue.All; options.Priority = TacticAutoPriority.LargerStock;
var first = string.Join(";", Build(options).Select(p => string.Join(",", p.CardIds)));
Check(first == string.Join(";", Build(options).Select(p => string.Join(",", p.CardIds))), "동일 입력 결정론");
Check(!games[0].HasTacticPlan && games[11].PlannedTacticCardIds[0] == "a", "미리보기 원본 상태 불변");
var extendedGames = Enumerable.Range(1, TacticAutoPlanner.MaximumPlanningGames + 1)
    .Select(i => new ScheduledGameState(i, i, (ulong)i, 1, 2)).ToArray();
var extendedOptions = new TacticAutoOptions();
var extendedPlan = TacticAutoPlanner.Build(extendedGames, 1, extendedGames[0], Array.Empty<string>(),
    cards, _ => 100, extendedOptions, TacticAutoPlanner.MaximumPlanningGames);
Check(extendedPlan.Count == 18 && extendedPlan[^1].GameId == 18,
    "3주 기본 범위는 18경기이며 19번째 경기는 제외");
extendedGames[0].Complete(1, 0);
extendedPlan = TacticAutoPlanner.Build(extendedGames, 1, extendedGames[1], Array.Empty<string>(),
    cards, _ => 100, extendedOptions, TacticAutoPlanner.MaximumPlanningGames);
Check(extendedPlan.Count == 18 && extendedPlan[0].GameId == 2 && extendedPlan[^1].GameId == 19,
    "경기 종료 후 다음 경기부터 3주 범위 이동");
Console.WriteLine($"자동 작전 콘솔 검증 {checks}건 통과");

static TacticCardDefinition Card(string id, TacticTier tier, bool disruption = false) => new TacticCardDefinition(
    id, id, TacticCardCategory.Batting, tier, "", "", Array.Empty<TacticTriggerCondition>(), TacticTargetRule.BattingTeam,
    Array.Empty<TacticStatModifier>(), Array.Empty<TacticBehaviorModifier>(), TacticDurationRule.RestOfGame, Array.Empty<string>(), disruption);

using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

// 실제 계약 서비스와 화면 표시 모델을 Unity 없이 검증한다.
var balance = StaffBalanceTable.CreateInitial();
var catalog = new StaffCatalog(new[] { Staff("old", "김선임", 1), Staff("new", "이후보", 5) });
var contracts = new[] { new StaffContractState("current", "old", "team", 1, 2, 100_000_000L) };
var assignment = new TeamStaffAssignmentState("team", hittingCoachStaffId: "old");
var offer = new StaffMarketOffer("offer", "new", "team", "period", StaffMarketKind.Offseason, 3, 300_000_000L, 30_000_000L);
var service = new StaffContractService();
var effects = new TeamStaffEffectResolver();
var blocked = Preview(0L);
var proposed = Preview(long.MaxValue);
int passed = 0;
Check(!blocked.IsSuccess && blocked.Status == StaffServiceStatus.InsufficientMoney, "자금 부족 계약 차단");
Check(proposed.IsSuccess && proposed.MoneyCommand.Amount > offer.SigningCost, "교체 위약금을 포함한 서비스 비용");
Check(assignment.GetAssignedStaffId(StaffRole.HittingCoach) == "old" && contracts[0].RemainingSeasons == 2, "미리보기는 현재 계약과 배치를 변경하지 않음");
Check(effects.Resolve(catalog, proposed.Contracts, proposed.Assignment, balance).HittingTrainingEfficiency >
      effects.Resolve(catalog, contracts, assignment, balance).HittingTrainingEfficiency, "자금 부족 후보도 영입 효과 비교 가능");
var model = OwnerStaffOfficePresentationBuilder.Build(new OwnerStaffOfficeSnapshot(UiContentStateModel.Ready,
    catalog, contracts, assignment, effects.Resolve(catalog, contracts, assignment, balance),
    new[] { new OwnerStaffMarketOfferSnapshot(offer, false, "보유금이 부족합니다.", "타자 훈련 효율 +11%", immediateCost: proposed.MoneyCommand.Amount) }));
Check(model.GetCurrentSlot(model.Offers[0]).StaffId == "old", "같은 역할 담당자 비교");
Check(model.GetSummary().Contains("배치 1/5명") && model.GetSummary().Contains("공석 4자리") && model.GetSummary().Contains("1억원"), "공석과 활성 연봉 요약");
Check(model.Offers[0].SigningCostText.Contains("교체 위약금") && model.Offers[0].SigningCostText.Contains("즉시 지출 " + OwnerMoneyFormatter.Format(proposed.MoneyCommand.Amount)), "서비스 지출액 표시 일치");
Check(!model.Offers[0].CanSign && model.Offers[0].DisabledReason == "보유금이 부족합니다.", "비용 표시와 실제 구매 권한 분리");
var repeated = Preview(long.MaxValue);
Check(repeated.MoneyCommand.Amount == proposed.MoneyCommand.Amount && repeated.Assignment.GetAssignedStaffId(StaffRole.HittingCoach) == "new", "동일 입력 계약 미리보기 결정론");
var exact = Preview(proposed.MoneyCommand.Amount);
Check(exact.IsSuccess && !Preview(proposed.MoneyCommand.Amount - 1L).IsSuccess, "즉시 지출 경계 금액 계약 검증");
var vacant = new TeamStaffAssignmentState("team");
var fresh = service.TrySign(new StaffSigningCommand("fresh", "fresh-payment", "team", 1, long.MaxValue),
    offer, catalog, Array.Empty<StaffContractState>(), vacant, balance);
Check(fresh.IsSuccess && fresh.MoneyCommand.Amount == offer.SigningCost, "공석 영입에는 위약금 없음");
foreach (var state in new[] { UiContentStateModel.CreateLoading("불러오는 중"), UiContentStateModel.CreateEmpty("후보 없음"), UiContentStateModel.CreateError("불러오기 실패", "다시 진입하세요") })
{
    var empty = OwnerStaffOfficePresentationBuilder.Build(new OwnerStaffOfficeSnapshot(state, null, null, null, null, null));
    Check(empty.Offers.Count == 0 && empty.Slots.Count == 0, state.Kind + " 빈 데이터 안전 처리");
}
Check(model.FindOffers(-1, "이후보", 5, false, 0, 0).Count == 1, "이름·최소 등급 검색");
Check(model.FindOffers(-1, "컨택", 1, false, 0, 0).Count == 1, "전문 분야 검색");
Check(model.FindOffers(-1, "", 1, true, 0, 0).Count == 0, "자금 부족 후보 필터");
Check(model.FindOffers(-1, "", 1, false, 2, 0).Count == 0, "장기 계약 제외");
Check(model.FindOffers(1, "", 1, false, 0, 0).Count == 0, "다른 역할 제외");
var comparisonOffers = new[] {
    new OwnerStaffMarketOfferSnapshot(offer, true, "", "훈련 효율 상승"),
    new OwnerStaffMarketOfferSnapshot(new StaffMarketOffer("cheap", "old", "team", "period", StaffMarketKind.Offseason,
        1, 80_000_000L, 8_000_000L), true, "", "훈련 효율 상승") };
var comparisonModel = OwnerStaffOfficePresentationBuilder.Build(new OwnerStaffOfficeSnapshot(UiContentStateModel.Ready,
    catalog, Array.Empty<StaffContractState>(), vacant, effects.Resolve(catalog, Array.Empty<StaffContractState>(), vacant, balance), comparisonOffers));
Check(comparisonModel.FindOffers(-1, "", 1, false, 0, 1).SequenceEqual(new[] { 1, 0 }), "연봉 오름차순은 실제 금액 비교");
Check(comparisonModel.FindOffers(-1, "", 1, false, 0, 2).SequenceEqual(new[] { 0, 1 }), "연봉 내림차순");
Check(comparisonModel.FindOffers(-1, "", 1, false, 0, 0).SequenceEqual(new[] { 0, 1 }), "등급 내림차순");

var names = new StaffNameCatalog(Enumerable.Range(0, 100).Select(index => "김" + (char)('가' + index / 10) + (char)('가' + index % 10)).ToArray());
var generator = new StaffCatalogGenerator();
var market = new StaffMarketResolver();
double previousQuality = 0;
double previousSalary = 0;
foreach (LeagueGrade grade in Enum.GetValues<LeagueGrade>())
{
    long salaryTotal = 0;
    int qualityTotal = 0, highTier = 0, total = 0;
    for (ulong seed = 1; seed <= 1000; seed++)
    {
        var pool = generator.Generate(names, 20, seed, balance);
        for (int week = 0; week < 5; week++)
        {
            var offers = market.CreateOffers(pool, Array.Empty<StaffContractState>(), "team", "week" + week,
                StaffMarketKind.MidseasonReplacement, grade, seed, balance);
            if (offers.Count != 15 || offers.Select(value => value.StaffId).Distinct().Count() != 15)
                throw new InvalidOperationException("후보 수·중복 오류");
            foreach (StaffRole role in Enum.GetValues<StaffRole>())
                if (offers.Count(value => pool.Get(value.StaffId).Role == role) != 3)
                    throw new InvalidOperationException("역할별 선택 폭 불일치");
            if (week == 0)
            {
                var again = market.CreateOffers(pool, Array.Empty<StaffContractState>(), "team", "week" + week,
                    StaffMarketKind.MidseasonReplacement, grade, seed, balance);
                if (!offers.Select(value => (value.OfferId, value.AnnualSalary)).SequenceEqual(
                    again.Select(value => (value.OfferId, value.AnnualSalary))))
                    throw new InvalidOperationException("시장 결정론 실패");
            }
            foreach (var value in offers)
            {
                int quality = pool.Get(value.StaffId).QualityTier;
                qualityTotal += quality;
                salaryTotal += value.AnnualSalary;
                highTier += quality >= 4 ? 1 : 0;
                total++;
            }
        }
    }
    double averageQuality = (double)qualityTotal / total;
    double averageSalary = (double)salaryTotal / total;
    Check(averageQuality > previousQuality && averageSalary > previousSalary, grade + " 품질·연봉 상승");
    previousQuality = averageQuality;
    previousSalary = averageSalary;
    Console.WriteLine($"시장 통계 {grade}: 후보 {total:N0}, 평균 등급 {averageQuality:F3}, 4~5등급 {100d * highTier / total:F2}%, 평균 연봉 {averageSalary:N0}원");
}
Console.WriteLine($"스태프 운영실 콘솔 검증 {passed}/{passed} 통과 / 50,000시장·750,000제안");

StaffSigningResult Preview(long money) => service.TrySign(new StaffSigningCommand("proposal", "payment", "team", 1, money), offer, catalog, contracts, assignment, balance);
static StaffDefinition Staff(string id, string name, int quality) => new(id, name, StaffRole.HittingCoach,
    quality, StaffSalaryBand.Standard, StaffContractPreference.Balanced,
    new[] { StaffSpecialtyTag.ContactTraining }, new[] { StaffPhilosophyTag.Fundamentals });
void Check(bool condition, string title)
{
    if (!condition) throw new InvalidOperationException(title);
    passed++;
    Console.WriteLine("통과: " + title);
}

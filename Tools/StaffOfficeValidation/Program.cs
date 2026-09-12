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
Console.WriteLine($"스태프 운영실 콘솔 검증 {passed}/{passed} 통과");

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

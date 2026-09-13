using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Tools.SimulationDiagnostics
{
    internal static partial class Program
    {
        /// <summary>실제 홈 경기 재무 계산으로 반복 서포트 구매와 시설 투자 여력을 비교한다.</summary>
        private static int RunSupportEconomy(string[] args)
        {
            int samples = ParseCount(args, 1, 100);
            var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            var catalog = JsonSerializer.Deserialize<SupportDiagnosticCatalog>(File.ReadAllText(
                args.Length > 2 ? args[2] : "Assets/10.Datas/Resources/NewGame/OwnerSupportCards.json"), options);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in catalog.cards)
            {
                card.Validate();
                if (!ids.Add(card.id)) throw new InvalidOperationException("서포트 ID 중복");
            }
            var root = JsonNode.Parse(File.ReadAllText("Assets/10.Datas/Resources/NewGame/OwnerExpansionBalance.json"));
            var operationJson = root["clubOperation"];
            // Unity JSON의 Has 플래그를 순수 계약의 nullable 값으로 변환한다.
            foreach (var row in operationJson["facilityLevels"].AsArray())
            {
                RestoreOptionalNumber(row, "hasRequiredLeagueGrade", "requiredLeagueGrade");
                RestoreOptionalNumber(row, "hasScoutingPointStorageCapacity", "scoutingPointStorageCapacity");
                RestoreOptionalNumber(row, "hasDevelopmentPointStorageCapacity", "developmentPointStorageCapacity");
            }
            foreach (var row in operationJson["stadiumLevels"].AsArray())
                RestoreOptionalNumber(row, "hasRequiredLeagueGrade", "requiredLeagueGrade");
            var balance = JsonSerializer.Deserialize<ClubOperationBalanceTable>(operationJson.ToJsonString(), options);
            var resolver = new HomeGameFinanceResolver(balance);
            long facilityCost = balance.GetFacilityLevel(FacilityType.TrainingCenter, 1).UpgradeMoneyCost;
            Console.WriteLine("Grade,FanBase,WinRate,PackageCost,SelectiveCost,ContinuousCost,MedianNetHomeIncome,MedianSpent,MedianSkippedPackages,MedianFacilityDelayGames");
            for (int grade = 0; grade < 10; grade++)
            {
                long package = ResolveSupportPackageCost(catalog.cards, grade);
                foreach (int fanBase in new[] { 20, 50, 80 })
                foreach (double winRate in new[] { .4, .5, .6 })
                    PrintSupportEconomyScenario(balance, resolver, samples, grade, fanBase, winRate, package, facilityCost);
            }
            Console.WriteLine($"검증: 카드 {ids.Count}종, 재무 시즌 {90 * samples}회, 홈 재무 {90 * samples * 72}회. 시설 비교 비용 {facilityCost}원.");
            return 0;
        }

        private static void PrintSupportEconomyScenario(ClubOperationBalanceTable balance, HomeGameFinanceResolver resolver,
            int samples, int grade, int fanBase, double winRate, long package, long facilityCost)
        {
            var incomes = new List<long>();
            var spent = new List<long>();
            var skipped = new List<long>();
            var delay = new List<long>();
            for (int sample = 0; sample < samples; sample++)
            {
                var result = SimulateSupportEconomySeason(balance, resolver, sample, grade, fanBase, winRate, package, facilityCost);
                incomes.Add(result.Income); spent.Add(result.Spent); skipped.Add(result.Skipped); delay.Add(result.Delay);
            }
            Console.WriteLine(FormattableString.Invariant($"{grade},{fanBase},{winRate:F1},{package},{package * 8},{package * 72},{Median(incomes)},{Median(spent)},{Median(skipped)},{Median(delay)}"));
        }

        private static (long Income, long Spent, long Skipped, int Delay) SimulateSupportEconomySeason(
            ClubOperationBalanceTable balance, HomeGameFinanceResolver resolver, int sample, int grade,
            int fanBase, double winRate, long package, long facilityCost)
        {
            // 구장·팬·승률은 민감도 입력이다. 승격/성장·선수 연봉까지 포함한 월드 회귀가 아니다.
            var stadium = balance.StadiumLevels[0];
            var facilities = new FacilityState[6];
            for (int i = 0; i < facilities.Length; i++) facilities[i] = new FacilityState((FacilityType)i, 0);
            var state = new ClubOperationState("support-club", fanBase, 50, 50,
                new StadiumState(stadium.Level, stadium.Capacity), facilities,
                new TicketPolicy(TicketPriceTier.Standard), new WeeklyOperationLedger("support-season", 0),
                new SeasonFinanceSummary("support-season"));
            long net = 0, wallet = 0, purchased = 0, misses = 0;
            int baselineReady = 145, supportedReady = 145;
            for (int game = 0; game < 144; game++)
            {
                // 시작 자금은 이미 기존 구단 운영에 배정한 것으로 두고 새 시즌 현금 흐름만 비교한다.
                if (game % 2 == 0)
                {
                    if (wallet >= package) { wallet -= package; purchased += package; }
                    else misses++;
                }
                if (game % 2 == 1)
                {
                    ulong seed = DeterministicSeed.Derive((ulong)sample + 0xEC070UL, (ulong)game);
                    var rng = new Pcg32Random(seed);
                    var context = new HomeGameContext("support-" + game, "support-season", 0,
                        "support-club", "opponent", GameVenue.Home, (LeagueGrade)grade,
                        rng.NextDouble() < winRate ? HomeGameOutcome.Win : HomeGameOutcome.Loss,
                        winRate, .5, (double)game / 143, 0);
                    var finance = resolver.Resolve(context, state, rng);
                    if (!state.TryApplyHomeGame(finance)) throw new InvalidOperationException("재무 반영 실패");
                    net += finance.NetGameIncome;
                    wallet += finance.NetGameIncome;
                }
                if (baselineReady == 145 && net >= facilityCost) baselineReady = game + 1;
                if (supportedReady == 145 && wallet >= facilityCost) supportedReady = game + 1;
            }
            return (net, purchased, misses, supportedReady - baselineReady);
        }

        private static void RestoreOptionalNumber(JsonNode row, string flag, string value)
        {
            if (!row[flag].GetValue<bool>()) row[value] = null;
        }

        private static long ResolveSupportPackageCost(OwnerSupportDefinition[] cards, int grade)
        {
            long team = 0, player = 0;
            foreach (var card in cards)
            {
                if ((int)card.unlockGrade > grade) continue;
                if (card.scope == OwnerSupportScope.Team) team = Math.Max(team, card.price);
                else player = Math.Max(player, card.price);
            }
            if (team == 0 || player == 0) throw new InvalidOperationException("리그별 팀·개인 카드가 필요합니다.");
            return checked(team + player * 3);
        }

        private static long Median(List<long> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }
    }
}

using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 새 게임 전체 생성의 결정론과 계약 선택 폭을 검증한다.
    /// </summary>
    public sealed class NewGameSetupTests
    {
        private static readonly string[] TeamNames =
        {
            "서울 블루윙스", "부산 마리너스", "인천 웨이브", "광주 레드폭스",
            "수원 스타즈", "대전 호크스", "대구 크라운", "창원 블레이즈"
        };

        [Test]
        public void GenerateLeagueAndOffers_같은Seed는구단경쟁자오퍼가모두같다()
        {
            Player player = CreateShortstop();
            NewGameSetupResult first = CreateSetup(998877UL)
                .GenerateLeagueAndOffers(player, 8, TeamArchetypeLibrary.CreateDefaultPool(), TeamNames);
            NewGameSetupResult second = CreateSetup(998877UL)
                .GenerateLeagueAndOffers(player, 8, TeamArchetypeLibrary.CreateDefaultPool(), TeamNames);

            Assert.That(second.Teams, Has.Length.EqualTo(first.Teams.Length));
            Assert.That(second.Offers, Has.Length.EqualTo(first.Offers.Length));
            for (int index = 0; index < first.Teams.Length; index++)
            {
                Assert.That(second.Teams[index].Name, Is.EqualTo(first.Teams[index].Name));
                Assert.That(second.Teams[index].Archetype.Budget, Is.EqualTo(first.Teams[index].Archetype.Budget));
                Assert.That(
                    second.Teams[index].GetPositionNeed(PlayerPosition.Shortstop),
                    Is.EqualTo(first.Teams[index].GetPositionNeed(PlayerPosition.Shortstop)));
                Assert.That(
                    second.Teams[index].GetPositionCompetitors(PlayerPosition.Shortstop)[0].Overall,
                    Is.EqualTo(first.Teams[index].GetPositionCompetitors(PlayerPosition.Shortstop)[0].Overall));
            }

            for (int index = 0; index < first.Offers.Length; index++)
            {
                Assert.That(second.Offers[index].Team.TeamId, Is.EqualTo(first.Offers[index].Team.TeamId));
                Assert.That(second.Offers[index].SigningBonus, Is.EqualTo(first.Offers[index].SigningBonus));
                Assert.That(second.Offers[index].ExpectedRole, Is.EqualTo(first.Offers[index].ExpectedRole));
            }
        }

        [Test]
        public void GenerateLeagueAndOffers_8개구단중3개에서5개만오퍼한다()
        {
            NewGameSetupResult result = CreateSetup(123456UL)
                .GenerateLeagueAndOffers(
                    CreateShortstop(),
                    8,
                    TeamArchetypeLibrary.CreateDefaultPool(),
                    TeamNames);

            Assert.That(result.Offers.Length, Is.InRange(3, 5));
            Assert.That(result.Offers.Length, Is.LessThan(result.Teams.Length));
        }

        [Test]
        public void CalculatePositionValue_같은총합이면유격수핵심능력치가높은빌드가더높다()
        {
            var evaluator = new PlayerValueEvaluator(PlayerEvaluationBalance.CreateDefault());
            var suited = new Player(
                1,
                "수비형",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(54, 40, 54, 40, 64, 60),
                default);
            var mismatched = new Player(
                2,
                "장타형",
                PlayerPosition.Shortstop,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(54, 64, 54, 60, 40, 40),
                default);

            Assert.That(
                evaluator.CalculatePositionValue(suited),
                Is.GreaterThan(evaluator.CalculatePositionValue(mismatched)));
        }

        [Test]
        [Timeout(60000)]
        public void GenerateLeagueAndOffers_10000회생성에서오퍼분포를집계한다()
        {
            const int SimulationCount = 10_000;
            Player player = CreateShortstop();
            TeamArchetypeProfile[] archetypes = TeamArchetypeLibrary.CreateDefaultPool();
            var offerCounts = new int[6];
            var roleCounts = new int[3];
            long signingBonusTotal = 0L;
            int totalOffers = 0;

            for (ulong seed = 1UL; seed <= SimulationCount; seed++)
            {
                NewGameSetupResult result = CreateSetup(seed)
                    .GenerateLeagueAndOffers(player, 8, archetypes, TeamNames);
                offerCounts[result.Offers.Length]++;
                totalOffers += result.Offers.Length;
                for (int offerIndex = 0; offerIndex < result.Offers.Length; offerIndex++)
                {
                    ContractOffer offer = result.Offers[offerIndex];
                    roleCounts[(int)offer.ExpectedRole]++;
                    signingBonusTotal += offer.SigningBonus;
                }
            }

            double averageOfferCount = totalOffers / (double)SimulationCount;
            long averageSigningBonus = signingBonusTotal / totalOffers;
            double benchShare = roleCounts[(int)ExpectedRole.BenchCompetition] / (double)totalOffers;
            double rosterShare = roleCounts[(int)ExpectedRole.RosterCompetition] / (double)totalOffers;
            double startingShare = roleCounts[(int)ExpectedRole.StartingCompetition] / (double)totalOffers;
            TestContext.WriteLine(
                $"10,000회: 3오퍼={offerCounts[3]}, 4오퍼={offerCounts[4]}, 5오퍼={offerCounts[5]}, " +
                $"평균={averageOfferCount:F2}");
            TestContext.WriteLine(
                $"예상 역할: 백업={roleCounts[(int)ExpectedRole.BenchCompetition]}, " +
                $"로스터={roleCounts[(int)ExpectedRole.RosterCompetition]}, " +
                $"주전={roleCounts[(int)ExpectedRole.StartingCompetition]}, " +
                $"평균 계약금={averageSigningBonus:N0}원");

            Assert.That(offerCounts[3] + offerCounts[4] + offerCounts[5], Is.EqualTo(SimulationCount));
            Assert.That(averageOfferCount, Is.InRange(3d, 5d));
            Assert.That(benchShare, Is.InRange(0.15d, 0.40d));
            Assert.That(rosterShare, Is.InRange(0.40d, 0.65d));
            Assert.That(startingShare, Is.InRange(0.10d, 0.35d));
        }

        private static NewGameSetup CreateSetup(ulong seed)
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            return new NewGameSetup(
                balance.ContractOffer,
                balance.TeamGeneration,
                balance.PlayerEvaluation,
                new Pcg32Random(seed));
        }

        private static Player CreateShortstop()
        {
            return new Player(
                1,
                "테스트 선수",
                PlayerPosition.Shortstop,
                Handedness.Left,
                Handedness.Right,
                new BatterAttributes(55, 52, 54, 43, 62, 54),
                default,
                nationality: "대한민국");
        }
    }
}

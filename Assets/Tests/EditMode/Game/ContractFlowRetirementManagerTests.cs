using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using Baseball.Game.Manager;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 계약 오퍼 화면에서 고른 은퇴가 시즌을 넘기지 않고 회고까지 이어지는지 검증한다.
    /// CareerManager를 거치므로 Unity EditMode 러너에서만 실행된다.
    /// </summary>
    public sealed class ContractFlowRetirementManagerTests
    {
        [TearDown]
        public void TearDown()
        {
            if (GameManager.HasInstance)
                Object.DestroyImmediate(GameManager.Instance.gameObject);
        }

        [Test]
        public void 오퍼단계은퇴는시즌을넘기지않고회고를만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(configuration, 52_001UL);
            CareerManager manager = CreateManager(career, balance);

            Assert.That(manager.CanRetireFromContractOffers, Is.False, "오퍼 단계 전에는 이 경로가 열리면 안 된다.");
            Assert.That(manager.BeginContractNegotiation(), Is.True, manager.LastError);
            Assert.That(manager.CanRetireFromContractOffers, Is.True);

            int retirementYear = career.CurrentLeague.CurrentSeason.Year;
            int declinedOfferCount = manager.Contract.RenewalOffers.Length;
            int previousDeclineCount = CountMemories(career, CareerMemoryType.ContractDeclined);

            Assert.That(manager.RetireFromContractOffers(), Is.True, manager.LastError);

            Assert.That(career.Retirement.IsRetired, Is.True);
            Assert.That(manager.HasRetirementRecap, Is.True);
            Assert.That(manager.RetirementRecap.RetirementReason, Is.EqualTo(RetirementReason.Voluntary));
            Assert.That(manager.RetirementRecap.RetirementSeason, Is.EqualTo(retirementYear));
            Assert.That(career.MyPlayer.CareerStatus, Is.EqualTo(PlayerCareerStatus.Retired));
            Assert.That(
                career.CurrentLeague.CurrentSeason.Year,
                Is.EqualTo(retirementYear),
                "은퇴는 시즌 전환을 취소해야 하며 다음 시즌을 시작하면 안 된다.");
            Assert.That(
                CountMemories(career, CareerMemoryType.ContractDeclined) - previousDeclineCount,
                Is.EqualTo(declinedOfferCount),
                "마다한 제안은 마지막 선택의 무게이므로 모두 기억에 남아야 한다.");
            Assert.DoesNotThrow(career.World.ValidateInvariants);
        }

        [Test]
        public void 은퇴가능나이미만이면오퍼단계에서도은퇴할수없다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(
                configuration, 52_002UL, ageToRetirementEligibility: false);
            CareerManager manager = CreateManager(career, balance);
            Assert.That(manager.BeginContractNegotiation(), Is.True, manager.LastError);

            Assert.That(manager.CanRetireFromContractOffers, Is.False);
            Assert.That(manager.RetireFromContractOffers(), Is.False);
            Assert.That(career.Retirement.IsRetired, Is.False);
        }

        private static CareerManager CreateManager(CareerState career, BalanceTable balance)
        {
            CareerManager manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            manager.BeginCareer(career, balance);
            return manager;
        }

        private static int CountMemories(CareerState career, CareerMemoryType type)
        {
            int count = 0;
            for (int index = 0; index < career.Retirement.MemoryLog.Records.Count; index++)
            {
                if (career.Retirement.MemoryLog.Records[index].Type == type)
                    count++;
            }
            return count;
        }

        /// <summary>계약이 만료된 채 오프시즌에 서 있는 커리어를 만든다.</summary>
        private static CareerState CreateCareerAtExpiredContractOffseason(
            NewGameConfiguration configuration,
            ulong seed,
            bool ageToRetirementEligibility = true)
        {
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateStartedCareer(configuration, seed);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                AdvanceToOffseason(career, balance);
                if (ageToRetirementEligibility)
                {
                    while (career.MyPlayer.Age < balance.PlayerLifecycle.RetirementMinimumAge)
                        career.MyPlayer.AdvanceAge();
                }
                if (career.CurrentContract.EndYear <= career.CurrentLeague.CurrentSeason.Year)
                    return career;
                new CareerSeasonTransitionService(career, balance).AdvanceToNextSeason();
            }

            Assert.Fail("계약 만료 오프시즌에 도달하지 못했습니다.");
            return career;
        }

        private static void AdvanceToOffseason(CareerState career, BalanceTable balance)
        {
            var autoCompletion = new CareerSeasonAutoCompletionService(
                career, balance, CareerNewsConfiguration.CreateDefault());
            for (int step = 0; step < 4; step++)
            {
                if (career.CurrentLeague.CurrentSeason.Phase == SeasonPhase.SeasonReview)
                    break;
                autoCompletion.CompleteCurrentPhase();
            }
            new CareerGrowthService(career, balance).SettleSeasonAndBeginOffseason(CreateBatterUsage());
        }

        private static CareerState CreateStartedCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("은퇴 계약 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(60, 55, 58, 50, 62, 54));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static SeasonUsageSummary CreateBatterUsage()
        {
            return new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.3d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.2d)
                });
        }
    }
}

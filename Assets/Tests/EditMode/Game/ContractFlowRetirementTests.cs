using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Career.News;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 계약 만료 후 오퍼를 비교하는 자리에 은퇴 선택지가 나이 기준대로 열리는지,
    /// 그 시점의 시즌 전환이 아직 커리어를 건드리지 않아 은퇴로 되돌릴 수 있는지 검증한다.
    /// </summary>
    public sealed class ContractFlowRetirementTests
    {
        [Test]
        public void 오퍼단계는은퇴가능나이에서은퇴선택지를연다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(configuration, 51_001UL);

            CareerContractView view = OpenOfferStep(career, balance, out _);

            Assert.That(view.CanRetireInsteadOfSigning, Is.True);
            Assert.That(view.RetirementEligibleAge, Is.EqualTo(balance.PlayerLifecycle.RetirementMinimumAge));
            Assert.That(view.GuaranteedRetirementAge, Is.EqualTo(balance.PlayerLifecycle.GuaranteedRetirementAge));
            Assert.That(view.RenewalOffers, Is.Not.Empty);
        }

        [Test]
        public void 오퍼단계라도은퇴가능나이미만이면선택지를열지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(
                configuration, 51_002UL, ageToRetirementEligibility: false);

            CareerContractView view = OpenOfferStep(career, balance, out _);

            Assert.That(career.MyPlayer.Age, Is.LessThan(balance.PlayerLifecycle.RetirementMinimumAge));
            Assert.That(view.CanRetireInsteadOfSigning, Is.False);
        }

        [Test]
        public void 오퍼단계는아직시즌도나이도넘기지않아은퇴로되돌릴수있다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(configuration, 51_003UL);
            int year = career.CurrentLeague.CurrentSeason.Year;
            int age = career.MyPlayer.Age;
            int teamId = career.MyPlayer.CurrentTeamId;
            int contractCount = career.ContractHistory.Count;

            OpenOfferStep(career, balance, out _);

            Assert.That(career.CurrentLeague.CurrentSeason.Year, Is.EqualTo(year));
            Assert.That(career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(career.MyPlayer.Age, Is.EqualTo(age));
            Assert.That(career.MyPlayer.CurrentTeamId, Is.EqualTo(teamId));
            Assert.That(career.ContractHistory.Count, Is.EqualTo(contractCount));
            Assert.That(career.Retirement.IsRetired, Is.False);
        }

        [Test]
        public void 보장은퇴나이직전계약은마지막시즌임을알린다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            BalanceTable balance = configuration.Balance;
            CareerState career = CreateCareerAtExpiredContractOffseason(configuration, 51_004UL);
            while (career.MyPlayer.Age < balance.PlayerLifecycle.GuaranteedRetirementAge - 1)
                career.MyPlayer.AdvanceAge();

            CareerContractView view = OpenOfferStep(career, balance, out _);

            Assert.That(view.IsNextSeasonForcedFinal, Is.True);
        }

        /// <summary>오프시즌 커리어를 계약 오퍼 비교 단계까지 밀어 넣고 그 화면 모델을 만든다.</summary>
        private static CareerContractView OpenOfferStep(
            CareerState career,
            BalanceTable balance,
            out CareerSeasonTransitionService transition)
        {
            transition = new CareerSeasonTransitionService(career, balance);
            SeasonTransitionStep step = transition.BeginTransition();
            Assert.That(
                step,
                Is.EqualTo(SeasonTransitionStep.CurrentTeamNegotiation)
                    .Or.EqualTo(SeasonTransitionStep.ContractOffers));
            return new CareerContractViewBuilder(career, balance).Build(transition, string.Empty);
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
            Assert.That(career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
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

using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 계약이 만료된 시즌 전환에서 재계약 오퍼 제시·선택·확정이
    /// 새 게임 입단과 같은 규칙으로 결정론적으로 동작하는지 검증한다.
    /// </summary>
    public sealed class CareerContractRenewalFlowTests
    {
        [Test]
        public void BeginTransition_계약이남아있으면멈추지않고다음시즌을시작한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 1111UL);
            var service = new CareerSeasonTransitionService(career, configuration.Balance);

            SeasonTransitionStep step = service.BeginTransition();

            Assert.That(step, Is.EqualTo(SeasonTransitionStep.Completed));
            Assert.That(service.RenewalOffers.Count, Is.Zero);
            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
        }

        [Test]
        public void BeginTransition_계약만료시즌에오퍼를제시하고커리어를바꾸지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 2222UL);
            CareerSeasonTransitionService service = AdvanceToRenewalSeason(career, configuration.Balance);

            int ageBefore = career.MyPlayer.Age;
            int yearBefore = career.League.CurrentSeason.Year;

            Assert.That(service.Step, Is.EqualTo(SeasonTransitionStep.ContractOffers));
            Assert.That(service.RenewalOffers.Count,
                Is.GreaterThanOrEqualTo(configuration.Balance.ContractOffer.MinimumOfferCount));
            Assert.That(service.RenewalOffers.Count,
                Is.LessThanOrEqualTo(configuration.Balance.ContractOffer.MaximumOfferCount));

            // 오퍼 화면에서 멈춘 동안에는 커리어가 반쯤 전환된 상태로 남지 않아야 한다.
            Assert.That(career.MyPlayer.Age, Is.EqualTo(ageBefore));
            Assert.That(career.League.CurrentSeason.Year, Is.EqualTo(yearBefore));
            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(career.CurrentOffseason, Is.Not.Null);
        }

        [Test]
        public void SignSelectedOffer_선택한구단과재계약하고다음시즌을시작한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 3333UL);
            CareerSeasonTransitionService service = AdvanceToRenewalSeason(career, configuration.Balance);
            int contractCountBefore = career.ContractHistory.Count;
            int expectedYear = career.League.CurrentSeason.Year + 1;
            ContractOffer chosen = service.RenewalOffers[service.RenewalOffers.Count - 1];

            service.SelectRenewalOffer(chosen.Team.TeamId);
            CareerSeasonTransitionResult result = service.SignSelectedOffer();

            Assert.That(service.Step, Is.EqualTo(SeasonTransitionStep.Completed));
            Assert.That(career.CurrentContract.TeamId, Is.EqualTo(chosen.Team.TeamId));
            Assert.That(career.CurrentContract.AnnualSalary, Is.EqualTo(chosen.AnnualSalary));
            Assert.That(career.CurrentContract.SignedYear, Is.EqualTo(expectedYear));
            Assert.That(career.ContractHistory.Count, Is.EqualTo(contractCountBefore + 1));
            Assert.That(career.MyPlayer.CurrentTeamId, Is.EqualTo(chosen.Team.TeamId));
            Assert.That(result.TeamId, Is.EqualTo(chosen.Team.TeamId));
            Assert.That(career.League.CurrentSeason.Year, Is.EqualTo(expectedYear));
            Assert.That(career.League.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
            Assert.That(career.CurrentOffseason, Is.Null);
        }

        [Test]
        public void SelectRenewalOffer_제시되지않은구단은예외를던진다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 4444UL);
            CareerSeasonTransitionService service = AdvanceToRenewalSeason(career, configuration.Balance);

            Assert.Throws<ArgumentException>(() => service.SelectRenewalOffer(-1));
        }

        [Test]
        public void SignSelectedOffer_구단을고르기전에는예외를던진다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 5555UL);
            CareerSeasonTransitionService service = AdvanceToRenewalSeason(career, configuration.Balance);

            Assert.Throws<InvalidOperationException>(() => service.SignSelectedOffer());
        }

        [Test]
        public void BeginTransition_같은Seed는같은재계약오퍼목록을만든다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState firstCareer = CreateOffseasonCareer(configuration, 6666UL);
            CareerState secondCareer = CreateOffseasonCareer(configuration, 6666UL);
            CareerSeasonTransitionService first = AdvanceToRenewalSeason(firstCareer, configuration.Balance);
            CareerSeasonTransitionService second = AdvanceToRenewalSeason(secondCareer, configuration.Balance);

            Assert.That(second.RenewalOffers.Count, Is.EqualTo(first.RenewalOffers.Count));
            for (int index = 0; index < first.RenewalOffers.Count; index++)
            {
                Assert.That(second.RenewalOffers[index].Team.TeamId,
                    Is.EqualTo(first.RenewalOffers[index].Team.TeamId));
                Assert.That(second.RenewalOffers[index].AnnualSalary,
                    Is.EqualTo(first.RenewalOffers[index].AnnualSalary));
                Assert.That(second.RenewalOffers[index].SigningBonus,
                    Is.EqualTo(first.RenewalOffers[index].SigningBonus));
            }
        }

        [Test]
        public void RenewalOffers_점수내림차순으로제시된다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateOffseasonCareer(configuration, 7777UL);
            CareerSeasonTransitionService service = AdvanceToRenewalSeason(career, configuration.Balance);

            for (int index = 1; index < service.RenewalOffers.Count; index++)
            {
                Assert.That(service.RenewalOffers[index].OfferScore,
                    Is.LessThanOrEqualTo(service.RenewalOffers[index - 1].OfferScore));
            }
        }

        [Test]
        public void AdvanceToNextSeason_자동진행은최고점오퍼를수락한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState manualCareer = CreateOffseasonCareer(configuration, 8888UL);
            CareerState autoCareer = CreateOffseasonCareer(configuration, 8888UL);

            CareerSeasonTransitionService manual = AdvanceToRenewalSeason(manualCareer, configuration.Balance);
            int topTeamId = manual.RenewalOffers[0].Team.TeamId;

            // 자동 경로도 같은 지점까지 진행한 뒤 한 번에 확정한다.
            CareerSeasonTransitionService inspector = AdvanceToRenewalSeason(autoCareer, configuration.Balance);
            Assert.That(inspector.RenewalOffers[0].Team.TeamId, Is.EqualTo(topTeamId));

            manual.SelectRenewalOffer(topTeamId);
            CareerSeasonTransitionResult manualResult = manual.SignSelectedOffer();

            Assert.That(manualResult.TeamId, Is.EqualTo(topTeamId));
            Assert.That(manualCareer.CurrentContract.TeamId, Is.EqualTo(topTeamId));
        }

        /// <summary>
        /// 계약 기간이 남은 시즌은 그대로 통과시키고, 계약이 만료되어 오퍼가 제시된 시점의 서비스를 돌려준다.
        /// </summary>
        private static CareerSeasonTransitionService AdvanceToRenewalSeason(
            CareerState career,
            BalanceTable balance)
        {
            for (int guard = 0; guard < 10; guard++)
            {
                var service = new CareerSeasonTransitionService(career, balance);
                if (service.BeginTransition() == SeasonTransitionStep.ContractOffers)
                    return service;

                career.League.CurrentSeason.CompleteRegularSeason();
                new CareerGrowthService(career, balance)
                    .SettleSeasonAndBeginOffseason(CreateBatterUsage());
            }

            throw new InvalidOperationException("계약 만료 시즌에 도달하지 못했습니다.");
        }

        private static CareerState CreateOffseasonCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("재계약 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            flow.Career.League.CurrentSeason.CompleteRegularSeason();
            new CareerGrowthService(flow.Career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());
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

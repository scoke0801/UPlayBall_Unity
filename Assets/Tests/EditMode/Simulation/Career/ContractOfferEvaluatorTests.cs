using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Tests.EditMode.Simulation;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 계약 오퍼 평가 공식이 포지션 필요도와 예산에 따라 합리적으로 움직이는지 확인한다.
    /// </summary>
    public sealed class ContractOfferEvaluatorTests
    {
        [Test]
        public void Evaluate_포지션필요도가높을수록오퍼점수가높다()
        {
            ContractOfferBalance balance = ContractOfferBalance.CreateDefault();
            GeneratedTeam lowNeedTeam = CreateTeam(positionNeed: 20, budget: 60);
            GeneratedTeam highNeedTeam = CreateTeam(positionNeed: 90, budget: 60);

            var lowNeedEvaluator = new ContractOfferEvaluator(balance, new SequenceRandom(0.5d));
            var highNeedEvaluator = new ContractOfferEvaluator(balance, new SequenceRandom(0.5d));

            ContractOffer? lowOffer = lowNeedEvaluator.Evaluate(lowNeedTeam, 60, PlayerPosition.Shortstop);
            ContractOffer? highOffer = highNeedEvaluator.Evaluate(highNeedTeam, 60, PlayerPosition.Shortstop);

            Assert.That(highOffer, Is.Not.Null);
            Assert.That(lowOffer.HasValue ? lowOffer.Value.OfferScore : 0d, Is.LessThan(highOffer.Value.OfferScore));
        }

        [Test]
        public void Evaluate_예산이낮은구단은오퍼금액이더낮다()
        {
            ContractOfferBalance balance = ContractOfferBalance.CreateDefault();
            GeneratedTeam richTeam = CreateTeam(positionNeed: 80, budget: 90);
            GeneratedTeam poorTeam = CreateTeam(positionNeed: 80, budget: 30);

            ContractOffer? richOffer = new ContractOfferEvaluator(balance, new SequenceRandom(0.5d))
                .Evaluate(richTeam, 60, PlayerPosition.Shortstop);
            ContractOffer? poorOffer = new ContractOfferEvaluator(balance, new SequenceRandom(0.5d))
                .Evaluate(poorTeam, 60, PlayerPosition.Shortstop);

            Assert.That(richOffer, Is.Not.Null);
            long poorSigningBonus = poorOffer.HasValue ? poorOffer.Value.SigningBonus : 0L;
            Assert.That(poorSigningBonus, Is.LessThan(richOffer.Value.SigningBonus));
        }

        private static GeneratedTeam CreateTeam(int positionNeed, int budget)
        {
            var archetype = new TeamArchetypeProfile(
                TeamArchetype.Contender,
                budget: budget,
                development: 60,
                rosterDepth: 60,
                scouting: 60);

            var needs = new int[(int)PlayerPosition.ReliefPitcher + 1];
            needs[(int)PlayerPosition.Shortstop] = positionNeed;

            return new GeneratedTeam(1, "테스트 구단", archetype, needs);
        }
    }
}

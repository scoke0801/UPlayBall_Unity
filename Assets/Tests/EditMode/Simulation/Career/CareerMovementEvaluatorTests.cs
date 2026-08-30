using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    public sealed class CareerMovementEvaluatorTests
    {
        [Test]
        public void ContractRenewal_구단의필요와선수가치가낮으면오퍼하지않는다()
        {
            GeneratedTeam team = CreateTeam(positionNeed: 20, budget: 35);
            var input = new ContractRenewalEvaluationInput(
                team,
                playerMarketValue: 25d,
                currentRoleValue: 40d,
                recentPerformance: 25d,
                ageAndPotential: 40d,
                costEfficiency: 30d,
                managerRelationship: 30d,
                strongestCompetitorOverall: 70);
            var evaluator = new ContractRenewalEvaluator(
                ContractRenewalBalance.CreateDefault(),
                ContractOfferBalance.CreateDefault());

            ContractOffer? offer = evaluator.Evaluate(
                input,
                PlayerPosition.Shortstop,
                ContractOfferChannel.CurrentTeamRenewal);

            Assert.That(offer.HasValue, Is.False);
        }

        [Test]
        public void ContractRenewal_핵심선수평가는장기주전경쟁오퍼를만든다()
        {
            GeneratedTeam team = CreateTeam(positionNeed: 90, budget: 85);
            var input = new ContractRenewalEvaluationInput(
                team,
                playerMarketValue: 92d,
                currentRoleValue: 90d,
                recentPerformance: 90d,
                ageAndPotential: 90d,
                costEfficiency: 85d,
                managerRelationship: 85d,
                strongestCompetitorOverall: 55);
            var evaluator = new ContractRenewalEvaluator(
                ContractRenewalBalance.CreateDefault(),
                ContractOfferBalance.CreateDefault());

            ContractOffer offer = evaluator.Evaluate(
                input,
                PlayerPosition.Shortstop,
                ContractOfferChannel.CurrentTeamRenewal).Value;

            Assert.That(offer.ExpectedRole, Is.EqualTo(ExpectedRole.StartingCompetition));
            Assert.That(offer.ContractYears, Is.EqualTo(3));
            Assert.That(offer.EstimatedPlayingTime, Is.GreaterThan(0.6d));
        }

        [Test]
        public void TradeValuation_이적요청은잔류선호보다매각의향과성사확률을높인다()
        {
            var ai = new TradeValuationAi(TradeMarketBalance.CreateDefault());
            TradeValuationResult stay = ai.Evaluate(CreateTradeInput(TradePreference.PreferToStay));
            TradeValuationResult request = ai.Evaluate(CreateTradeInput(TradePreference.RequestTrade));

            Assert.That(request.SellerInterest, Is.GreaterThan(stay.SellerInterest));
            Assert.That(request.CompletionProbability, Is.GreaterThan(stay.CompletionProbability));
            Assert.That(request.ProjectedRole, Is.EqualTo(ExpectedRole.StartingCompetition));
        }

        [Test]
        [Timeout(30000)]
        public void TradeValuation_10000개커리어시즌에서평균3에서4시즌에한번성사된다()
        {
            const int seasonCount = 10_000;
            const int targetTeamCount = 7;
            const int negotiationAttempts = 3;
            TradeMarketBalance balance = TradeMarketBalance.CreateDefault();
            var ai = new TradeValuationAi(balance);
            var random = new Pcg32Random(0x28A82026UL);
            int completedTrades = 0;

            for (int season = 0; season < seasonCount; season++)
            {
                TradePreference preference = SelectPreference(random.NextDouble());
                double playerValue = Range(random, 45d, 85d);
                double currentCompetitor = Range(random, 45d, 85d);
                double managerTrust = Range(random, 35d, 85d);
                double roleImportance = Clamp(
                    50d + (playerValue - currentCompetitor) * 2d +
                    (managerTrust - 50d) * 0.5d,
                    15d,
                    90d);
                double expiryRoll = random.NextDouble();
                double expiryRisk = expiryRoll < 0.40d ? 20d : expiryRoll < 0.70d ? 60d : 100d;
                double rebuildingPressure = Range(random, 0d, 100d);
                double salaryBurden = Range(random, 0d, 100d);
                double currentContention = random.NextDouble() < 0.50d
                    ? Range(random, 20d, 45d)
                    : Range(random, 55d, 100d);
                bool isCompleted = false;

                for (int team = 0; team < targetTeamCount; team++)
                {
                    double targetCompetitor = Range(random, 45d, 85d);
                    var input = new TradeValuationInput(
                        playerValue,
                        Range(random, 30d, 95d),
                        Clamp(50d + (playerValue - targetCompetitor) * 2.5d, 0d, 100d),
                        random.NextDouble() < 0.50d
                            ? Range(random, 35d, 65d)
                            : Range(random, 70d, 95d),
                        Range(random, 40d, 90d),
                        Clamp(50d + (currentCompetitor - playerValue) * 2.5d, 0d, 100d),
                        expiryRisk,
                        rebuildingPressure,
                        salaryBurden,
                        roleImportance,
                        currentContention,
                        preference);
                    TradeValuationResult result = ai.Evaluate(input);
                    if (result.BuyerInterest < balance.TeamInterestThreshold ||
                        result.SellerInterest < balance.SellerInterestThreshold)
                    {
                        continue;
                    }

                    for (int attempt = 0; attempt < negotiationAttempts; attempt++)
                    {
                        if (random.NextDouble() < result.CompletionProbability)
                            isCompleted = true;
                    }
                }

                if (isCompleted)
                    completedTrades++;
            }

            double tradeRate = completedTrades / (double)seasonCount;
            System.Console.WriteLine($"트레이드 성사: {completedTrades}/{seasonCount} ({tradeRate:P2})");
            Assert.That(tradeRate, Is.InRange(0.25d, 0.33d));
        }

        private static TradeValuationInput CreateTradeInput(TradePreference preference)
        {
            return new TradeValuationInput(
                playerValue: 70d,
                targetPositionNeed: 85d,
                targetUpgrade: 70d,
                targetContentionUrgency: 80d,
                contractValue: 75d,
                positionDuplication: 80d,
                expiryRisk: 100d,
                rebuildingPressure: 80d,
                salaryBurden: 55d,
                currentRoleImportance: 25d,
                currentTeamContention: 20d,
                preference);
        }

        private static TradePreference SelectPreference(double roll)
        {
            if (roll < 0.15d) return TradePreference.PreferToStay;
            if (roll < 0.75d) return TradePreference.Neutral;
            if (roll < 0.95d) return TradePreference.OpenToTrade;
            return TradePreference.RequestTrade;
        }

        private static double Range(Pcg32Random random, double minimum, double maximum)
        {
            return minimum + random.NextDouble() * (maximum - minimum);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }

        private static GeneratedTeam CreateTeam(int positionNeed, int budget)
        {
            var needs = new int[(int)PlayerPosition.ReliefPitcher + 1];
            for (int index = 0; index < needs.Length; index++)
                needs[index] = 50;
            needs[(int)PlayerPosition.Shortstop] = positionNeed;
            var archetype = new TeamArchetypeProfile(
                TeamArchetype.Development,
                budget,
                development: 50,
                rosterDepth: 50,
                scouting: 50);
            return new GeneratedTeam(1, "테스트 구단", archetype, needs);
        }
    }
}

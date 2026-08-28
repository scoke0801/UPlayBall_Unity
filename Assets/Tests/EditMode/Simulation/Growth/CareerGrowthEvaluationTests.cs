using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// AI 오프시즌 계획·감독 역할·계약 가치가 성장 상태와 연결되는지 검증한다.
    /// </summary>
    public sealed class CareerGrowthEvaluationTests
    {
        [Test]
        public void ManagerRoleEvaluator_경쟁자보다충분히앞서면주전을준다()
        {
            var evaluator = new ManagerRoleEvaluator(ManagerEvaluationWeightTable.CreateDefault());
            var player = new ManagerRoleEvaluationInput(80, 78, 90, 75, 82, 75, false);
            var competitor = new ManagerRoleEvaluationInput(65, 65, 80, 60, 68, 60, false);

            ManagerRoleEvaluationResult result = evaluator.Evaluate(
                player,
                new[] { competitor },
                ManagerDevelopmentStyle.Balanced);

            Assert.That(result.Role, Is.EqualTo(OpportunityRole.Starter));
            Assert.That(result.Margin, Is.GreaterThanOrEqualTo(5d));
        }

        [Test]
        public void AiOffseasonPlanner_12주와유학파트너1회제약안에서계획한다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            var player = new PlayerGrowthState(
                1, 20, PlayerType.Batter,
                new AbilityRatings(50), new AbilityRatings(70),
                WorkEthicGrade.Normal, 90, 0, 70);

            AiOffseasonPlanItem[] plan = new AiOffseasonPlanner(balance)
                .Plan(player, 6000L, requiresRehabilitation: false);

            int studyCount = 0;
            int partnerCount = 0;
            for (int index = 0; index < plan.Length; index++)
            {
                TrainingProgramDefinition program = balance.FindProgram(plan[index].ProgramId);
                if (program.IsStudy) studyCount++;
                if (program.ActivityType == OffseasonActivityType.TrainingPartner) partnerCount++;
                Assert.That(plan[index].EndWeek, Is.LessThanOrEqualTo(12));
            }
            Assert.That(studyCount, Is.LessThanOrEqualTo(1));
            Assert.That(partnerCount, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void ContractMarketEvaluator_최근성과만이아닌안정전력과내구도를함께반영한다()
        {
            var evaluator = new ContractMarketEvaluator(ContractMarketBalanceTable.CreateDefault());
            double value = evaluator.Evaluate(
                new ContractMarketInput(80, 70, 65, 90, 60, 75));

            Assert.That(value, Is.InRange(60d, 85d));
        }
    }
}

using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>
    /// 타석의 Count 규칙과 결정론적 결과를 검증한다.
    /// </summary>
    public sealed class PlateAppearanceSimulatorTests
    {
        [Test]
        public void Simulate_Ball네개에서Walk가된다()
        {
            PlateAppearanceMatchup matchup = CreateMatchup();
            var simulator = new PlateAppearanceSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0.99d));

            PlateAppearanceOutcome outcome = simulator.Simulate(matchup);

            Assert.That(outcome.Result, Is.EqualTo(PlateAppearanceResult.Walk));
            Assert.That(outcome.FinalBalls, Is.EqualTo(4));
            Assert.That(outcome.PitchCount, Is.EqualTo(4));
        }

        [Test]
        public void Simulate_Strike세개에서Strikeout이된다()
        {
            PlateAppearanceMatchup matchup = CreateMatchup();
            var simulator = new PlateAppearanceSimulator(
                BalanceTable.CreateDefault(),
                new SequenceRandom(0d, 0.99d));

            PlateAppearanceOutcome outcome = simulator.Simulate(matchup);

            Assert.That(outcome.Result, Is.EqualTo(PlateAppearanceResult.Strikeout));
            Assert.That(outcome.FinalStrikes, Is.EqualTo(3));
            Assert.That(outcome.PitchCount, Is.EqualTo(3));
        }

        private static PlateAppearanceMatchup CreateMatchup()
        {
            var batter = new Player(
                1,
                "테스트 타자",
                PlayerPosition.CenterField,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(50, 50, 50, 50, 50, 50),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
            var pitcher = new Player(
                2,
                "테스트 투수",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 20, 20),
                new PitcherAttributes(50, 50, 50, 50, 50, 50));
            return new PlateAppearanceMatchup(batter, pitcher, 50d, false);
        }
    }
}

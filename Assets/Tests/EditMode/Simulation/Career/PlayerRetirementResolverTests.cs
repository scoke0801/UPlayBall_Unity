using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>AI 은퇴 판정의 나이 경계와 확률 입력 계약을 검증한다.</summary>
    public sealed class PlayerRetirementResolverTests
    {
        [Test]
        public void ShouldRetire_최소나이미만이면은퇴하지않는다()
        {
            var resolver = new PlayerRetirementResolver(
                PlayerLifecycleBalance.CreateDefault(),
                new FixedRandom(0d));

            Assert.That(resolver.ShouldRetire(33, overall: 20), Is.False);
        }

        [Test]
        public void ShouldRetire_보장은퇴나이부터난수와무관하게은퇴한다()
        {
            var resolver = new PlayerRetirementResolver(
                PlayerLifecycleBalance.CreateDefault(),
                new FixedRandom(0.999999d));

            Assert.That(resolver.ShouldRetire(43, overall: 100), Is.True);
        }

        [Test]
        public void ShouldRetire_같은입력난수는같은판정을낸다()
        {
            PlayerLifecycleBalance balance = PlayerLifecycleBalance.CreateDefault();

            bool first = new PlayerRetirementResolver(balance, new FixedRandom(0.10d))
                .ShouldRetire(36, overall: 50);
            bool second = new PlayerRetirementResolver(balance, new FixedRandom(0.10d))
                .ShouldRetire(36, overall: 50);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ShouldRetire_현역집착형은같은상황의야망형보다현역을연장한다()
        {
            PlayerLifecycleBalance balance = PlayerLifecycleBalance.CreateDefault();
            var ambitious = new RetirementEvaluationInput(
                nextSeasonAge: 36,
                overall: 60,
                RetirementPersonality.Ambitious);
            var playingObsessed = new RetirementEvaluationInput(
                nextSeasonAge: 36,
                overall: 60,
                RetirementPersonality.PlayingObsessed);

            Assert.That(
                new PlayerRetirementResolver(balance, new FixedRandom(0.15d)).ShouldRetire(ambitious),
                Is.True);
            Assert.That(
                new PlayerRetirementResolver(balance, new FixedRandom(0.15d)).ShouldRetire(playingObsessed),
                Is.False);
        }

        [Test]
        public void ShouldRetire_남은계약과마일스톤과프랜차이즈관계는은퇴압박을낮춘다()
        {
            var input = new RetirementEvaluationInput(
                nextSeasonAge: 36,
                overall: 60,
                RetirementPersonality.FranchiseLoyal,
                hasContractRemaining: true,
                isMilestonePursuit: true,
                isChampionshipContender: true,
                isFranchiseTeam: true,
                hasVeteranDemand: true);

            bool shouldRetire = new PlayerRetirementResolver(
                    PlayerLifecycleBalance.CreateDefault(),
                    new FixedRandom(0d))
                .ShouldRetire(input);

            Assert.That(shouldRetire, Is.False);
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly double _value;

            public FixedRandom(double value)
            {
                _value = value;
            }

            public double NextDouble() => _value;
        }
    }
}

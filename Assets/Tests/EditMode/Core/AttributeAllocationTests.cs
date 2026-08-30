using System;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>실제 새 게임 경로가 사용하는 단일 생성 규칙을 검증한다.</summary>
    public sealed class CareerCreationRulesTests
    {
        [Test]
        public void CareerCreationRules_균형배분은Rookie평균60을만든다()
        {
            CareerCreationRules rules = CareerCreationRules.CreateDefault();

            Assert.That(
                rules.Batter.CreateWeightedValues(1, 1, 1, 1, 1, 1),
                Is.All.EqualTo(60));
            Assert.That(
                rules.Pitcher.CreateWeightedValues(1, 1, 1, 1),
                Is.All.EqualTo(60));
        }

        [Test]
        public void CreateDefault_타자와투수의출발선을각각정의한다()
        {
            CareerCreationRules rules = CareerCreationRules.CreateDefault();

            Assert.That(rules.Version, Is.EqualTo(CareerCreationRules.CurrentVersion));
            Assert.That(rules.Batter.AttributeCount, Is.EqualTo(6));
            Assert.That(rules.Batter.BaseValue, Is.EqualTo(50));
            Assert.That(rules.Batter.BonusPoints, Is.EqualTo(60));
            Assert.That(rules.Pitcher.AttributeCount, Is.EqualTo(4));
            Assert.That(rules.Pitcher.BonusPoints, Is.EqualTo(40));
            Assert.That(rules.Pitcher.MaxValue, Is.EqualTo(75));
        }

        [Test]
        public void ValidateComplete_포인트를덜쓰거나상한을넘으면거부한다()
        {
            CareerAttributeAllocationRule rule = CareerCreationRules.CreateDefault().Batter;

            Assert.Throws<ArgumentException>(() =>
                rule.ValidateComplete(new[] { 59, 59, 59, 59, 59, 59 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                rule.ValidateComplete(new[] { 76, 64, 60, 60, 50, 50 }));
            Assert.DoesNotThrow(() =>
                rule.ValidateComplete(new[] { 60, 60, 60, 60, 60, 60 }));
        }

        [Test]
        public void CreateWeightedValues_포인트를상한안에서모두배분한다()
        {
            CareerAttributeAllocationRule rule = CareerCreationRules.CreateDefault().Batter;

            int[] values = rule.CreateWeightedValues(3, 5, 1, 1, 1, 2);

            int spentPoints = 0;
            for (int index = 0; index < values.Length; index++)
            {
                Assert.That(values[index], Is.InRange(rule.BaseValue, rule.MaxValue));
                spentPoints += values[index] - rule.BaseValue;
            }

            Assert.That(spentPoints, Is.EqualTo(rule.BonusPoints));
            Assert.That(values[1], Is.GreaterThan(values[0]));
        }
    }
}

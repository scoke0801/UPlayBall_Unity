using System;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>
    /// 새 게임 캐릭터 생성의 능력치 배분 검증 규칙을 확인한다.
    /// </summary>
    public sealed class AttributeAllocationTests
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
        public void Validate_기준값과추가포인트를지키면예외가없다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            Assert.DoesNotThrow(() =>
                AttributeAllocation.Validate(balance, 65, 72, 58, 50, 60, 55));
        }

        [Test]
        public void Validate_기준값보다낮은능력치는거부한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AttributeAllocation.Validate(balance, 49, 50, 50, 50, 50, 50));
        }

        [Test]
        public void Validate_상한을넘는능력치는거부한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AttributeAllocation.Validate(balance, 76, 50, 50, 50, 50, 50));
        }

        [Test]
        public void Validate_배분포인트총합을넘으면거부한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            // 각 항목에서 기준값(50) 대비 25씩, 6개 합계 150은 BonusPoints(60)를 초과한다.
            Assert.Throws<ArgumentException>(() =>
                AttributeAllocation.Validate(balance, 75, 75, 75, 75, 75, 75));
        }

        [Test]
        public void Validate_능력치가6개가아니면거부한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            Assert.Throws<ArgumentException>(() =>
                AttributeAllocation.Validate(balance, 50, 50, 50, 50, 50));
        }

        [Test]
        public void CreateWeightedValues_추가포인트를가중치와상한에맞춰모두배분한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            int[] values = AttributeAllocation.CreateWeightedValues(balance, 3, 5, 1, 1, 1, 2);

            int spentPoints = 0;
            for (int index = 0; index < values.Length; index++)
            {
                Assert.That(values[index], Is.InRange(balance.BaseValue, balance.MaxValue));
                spentPoints += values[index] - balance.BaseValue;
            }

            Assert.That(spentPoints, Is.EqualTo(balance.BonusPoints));
            Assert.That(values[1], Is.GreaterThan(values[0]));
        }

        [Test]
        public void CreateWeightedValues_추가포인트가전체상한보다커도상한까지만배분한다()
        {
            var balance = new CharacterCreationBalance(baseValue: 40, bonusPoints: 200, maxValue: 65);

            int[] values = AttributeAllocation.CreateWeightedValues(balance, 1, 1, 1, 1, 1, 1);

            Assert.That(values, Is.All.EqualTo(balance.MaxValue));
        }

        [Test]
        public void CreateWeightedValues_모든가중치가0이면거부한다()
        {
            CharacterCreationBalance balance = CharacterCreationBalance.CreateDefault();

            Assert.Throws<ArgumentException>(() =>
                AttributeAllocation.CreateWeightedValues(balance, 0, 0, 0, 0, 0, 0));
        }
    }
}

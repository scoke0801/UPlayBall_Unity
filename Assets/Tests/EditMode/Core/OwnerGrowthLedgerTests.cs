using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    public sealed class OwnerGrowthLedgerTests
    {
        private static OwnerGrowthModifier Modifier(string id, OwnerGrowthSource source, int value, int games = 0)
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            values[(int)PlayerAbility.Contact] = value;
            return new OwnerGrowthModifier(id, source, "훈련 결과", values, remainingGames: games);
        }

        [Test]
        public void 만료후에도_동일출처를_중복반영하지않는다()
        {
            var ledger = new OwnerGrowthLedger();
            ledger.Add(Modifier("support_1", OwnerGrowthSource.Support, 2, 2));
            ledger.AdvanceMatch();
            Assert.That(ledger.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.EqualTo(2));
            ledger.AdvanceMatch();
            Assert.That(ledger.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.Zero);
            Assert.That(ledger.Entries.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => ledger.Add(Modifier("support_1", OwnerGrowthSource.Support, 2, 2)));
        }

        [Test]
        public void 유학초기화는_일반훈련과_이력을보존한다()
        {
            var training = new CardTrainingState();
            training.AddBonus(PlayerAbility.Contact, 3);
            training.AddStudyBonus(PlayerAbility.Contact, 2, "정교 타격 아카데미");
            training.ResetStudyBonuses();
            Assert.That(training.GetBonus(PlayerAbility.Contact), Is.EqualTo(3));
            Assert.That(training.Ledger.Entries[1].IsActive, Is.False);
            training.AddStudyBonus(PlayerAbility.Contact, 1);
            Assert.That(training.GetBonus(PlayerAbility.Contact), Is.EqualTo(4));
        }

        [Test]
        public void 원장합계위조와_오버플로는_부분반영없이거부한다()
        {
            var ledger = new OwnerGrowthLedger();
            ledger.Add(Modifier("first", OwnerGrowthSource.Training, int.MaxValue));
            Assert.Throws<OverflowException>(() => ledger.Add(Modifier("second", OwnerGrowthSource.Training, 1)));
            Assert.That(ledger.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => new CardTrainingState(new int[PlayerAbilityCatalog.AbilityCount], ledger: ledger));
        }

        [Test]
        public void 두카드원장은_공유입력객체의만료로_함께변하지않는다()
        {
            var first = new OwnerGrowthLedger(); var second = new OwnerGrowthLedger();
            var entry = Modifier("support", OwnerGrowthSource.Support, 1, 1);
            first.Add(entry); second.Add(entry); first.AdvanceMatch();
            Assert.That(second.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.EqualTo(1));
            Assert.That(entry.IsActive, Is.True);
        }
    }
}

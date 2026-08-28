using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 스킬 뽑기 보장 규칙과 4×4 보드 배치 계약을 검증한다.
    /// </summary>
    public sealed class SkillBoardAndGachaTests
    {
        [Test]
        public void PullSingle_Rare10회보장카운트다음뽑기에서Rare를지급한다()
        {
            SkillBlockDefinition[] definitions = CreateGachaDefinitions();
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, definitions);
            var economy = new CareerEconomyState(10000L);
            var board = new SkillBoardState("standard_4x4");
            var random = new FixedRandom(0d);

            for (int index = 0; index < 10; index++)
                service.PullSingle(economy, board, SkillBlockCategory.Contact, 2028, random);
            SkillBlockInstance guaranteed = service.PullSingle(
                economy, board, SkillBlockCategory.Contact, 2028, random);

            Assert.That(guaranteed.DefinitionId, Is.EqualTo("contact_rare"));
            Assert.That(board.PityRareCount, Is.EqualTo(0));
        }

        [Test]
        public void PullBundle_Common만나오면마지막에Uncommon이상을보장한다()
        {
            var service = new SkillGachaService(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                CreateGachaDefinitions());
            var economy = new CareerEconomyState(5000L);
            var board = new SkillBoardState("standard_4x4");

            SkillBlockInstance[] result = service.PullBundle(
                economy, board, SkillBlockCategory.Contact, 2028, new FixedRandom(0d));

            Assert.That(result[4].DefinitionId, Is.EqualTo("contact_uncommon"));
            Assert.That(economy.Money, Is.EqualTo(2300L));
        }

        [Test]
        public void PlaceBlock_회전과겹침을검증하고Socket위Trait만활성화한다()
        {
            var trait = new SkillBlockDefinition(
                "trait_l",
                SkillBlockRarity.Rare,
                SkillBlockCategory.Contact,
                new[] { new BoardCell(0, 0), new BoardCell(1, 0), new BoardCell(0, 1) },
                true,
                new[] { new AbilityChange(PlayerAbility.Contact, 2) },
                120L,
                "clutch_contact",
                TraitSocketRule.CoversSocket);
            var filler = new SkillBlockDefinition(
                "filler",
                SkillBlockRarity.Common,
                SkillBlockCategory.Contact,
                new[] { new BoardCell(0, 0) },
                false,
                new[] { new AbilityChange(PlayerAbility.Contact, 1) },
                60L);
            var state = new SkillBoardState("standard_4x4");
            SkillBlockInstance traitInstance = state.AddOwnedBlock(trait.BlockId);
            SkillBlockInstance fillerInstance = state.AddOwnedBlock(filler.BlockId);
            var service = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { trait, filler });

            service.PlaceBlock(state, traitInstance.InstanceId, 0, 0, 1);

            Assert.Throws<InvalidOperationException>(() =>
                service.PlaceBlock(state, fillerInstance.InstanceId, 0, 0, 0));
            Assert.That(service.GetAbilityBonus(state, PlayerAbility.Contact), Is.EqualTo(2));
            Assert.That(service.GetActiveTraitIds(state), Does.Contain("clutch_contact"));
        }

        private static SkillBlockDefinition[] CreateGachaDefinitions()
        {
            return new[]
            {
                CreateDefinition("contact_common", SkillBlockRarity.Common, 1, 60L),
                CreateDefinition("contact_uncommon", SkillBlockRarity.Uncommon, 2, 90L),
                CreateDefinition("contact_rare", SkillBlockRarity.Rare, 4, 120L),
                CreateDefinition("contact_epic", SkillBlockRarity.Epic, 6, 150L)
            };
        }

        private static SkillBlockDefinition CreateDefinition(
            string id,
            SkillBlockRarity rarity,
            int bonus,
            long sellValue)
        {
            return new SkillBlockDefinition(
                id,
                rarity,
                SkillBlockCategory.Contact,
                new[] { new BoardCell(0, 0) },
                false,
                new[] { new AbilityChange(PlayerAbility.Contact, bonus) },
                sellValue);
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly double _value;

            public FixedRandom(double value)
            {
                _value = value;
            }

            public double NextDouble()
            {
                return _value;
            }
        }
    }
}

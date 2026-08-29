using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Growth
{
    /// <summary>
    /// 등급 최소 보장·고등급 보호·임시 보드 일괄 적용 계약을 검증한다.
    /// </summary>
    public sealed class GrowthBoardWorkspaceRulesTests
    {
        [TestCase(SkillGachaPurchaseTier.Normal, "contact_normal")]
        [TestCase(SkillGachaPurchaseTier.Rare, "contact_rare")]
        [TestCase(SkillGachaPurchaseTier.Elite, "contact_elite")]
        [TestCase(SkillGachaPurchaseTier.Unique, "contact_unique")]
        [TestCase(SkillGachaPurchaseTier.Legendary, "contact_legendary")]
        public void PullSingle_선택상품이최소보장등급을결정한다(
            SkillGachaPurchaseTier tier,
            string expectedDefinitionId)
        {
            var service = new SkillGachaService(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                CreateDefinitions());
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(100_000L));
            var board = new SkillBoardState("standard_4x4");

            SkillBlockInstance result = service.PullSingle(
                economy,
                board,
                SkillBlockCategory.Contact,
                tier,
                2028,
                new FixedRandom(0d));

            Assert.That(result.DefinitionId, Is.EqualTo(expectedDefinitionId));
        }

        [Test]
        public void Unique_오프시즌두회제한과자동잠금을함께적용한다()
        {
            var service = new SkillGachaService(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                CreateDefinitions());
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(100_000L));
            var board = new SkillBoardState("standard_4x4");

            SkillBlockInstance first = service.PullSingle(
                economy, board, SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Unique, 2028, new FixedRandom(0d));
            service.PullSingle(
                economy, board, SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Unique, 2028, new FixedRandom(0d));

            Assert.That(board.IsBlockLocked(first.InstanceId), Is.True);
            Assert.That(board.GetLimitedPurchaseCount(SkillGachaPurchaseTier.Unique, 2028), Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => service.PullSingle(
                economy, board, SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Unique, 2028, new FixedRandom(0d)));
            Assert.Throws<InvalidOperationException>(() =>
                service.SellOwnedBlock(economy, board, first.InstanceId, 2028));

            board.SetBlockLocked(first.InstanceId, false);
            Assert.That(service.SellOwnedBlock(economy, board, first.InstanceId, 2028), Is.GreaterThan(0L));
        }

        [Test]
        public void ApplyLayout_신규배치는무료이고기존배치변경은안전회수를한번사용한다()
        {
            SkillBlockDefinition normal = CreateDefinition(
                "contact_normal", SkillBlockRarity.Normal,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O), 1);
            SkillBlockDefinition rare = CreateDefinition(
                "contact_rare", SkillBlockRarity.Rare,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.I), 2);
            var service = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { normal, rare });
            var board = new SkillBoardState("standard_4x4");
            SkillBlockInstance first = board.AddOwnedBlock(normal.BlockId);
            SkillBlockInstance second = board.AddOwnedBlock(rare.BlockId);
            service.PlaceBlock(board, first.InstanceId, 0, 0, 0);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));
            var offseason = new OffseasonState(2028, 12, 70);
            long recoveryCost = MoneyAmount.FromTenThousandWon(1_500L);

            bool firstApplyUsedRecovery = service.ApplyLayout(
                board,
                new[]
                {
                    new PlacedSkillBlock(first, 0, 0, 0),
                    new PlacedSkillBlock(second, 0, 2, 0)
                },
                economy,
                offseason,
                2028,
                recoveryCost);

            Assert.That(firstApplyUsedRecovery, Is.False);
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(5_000L)));

            bool secondApplyUsedRecovery = service.ApplyLayout(
                board,
                new[]
                {
                    new PlacedSkillBlock(first, 2, 0, 0),
                    new PlacedSkillBlock(second, 0, 2, 0)
                },
                economy,
                offseason,
                2028,
                recoveryCost);

            Assert.That(secondApplyUsedRecovery, Is.True);
            Assert.That(offseason.BoardRedesignUsed, Is.True);
            Assert.That(board.PlacedBlocks, Has.Count.EqualTo(2));
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(3_500L)));
        }

        [Test]
        public void ApplyLayout_겹치는임시보드는결제와원본변경전에거부한다()
        {
            SkillBlockDefinition definition = CreateDefinition(
                "contact_normal", SkillBlockRarity.Normal,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O), 1);
            var service = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { definition });
            var board = new SkillBoardState("standard_4x4");
            SkillBlockInstance first = board.AddOwnedBlock(definition.BlockId);
            SkillBlockInstance second = board.AddOwnedBlock(definition.BlockId);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));
            var offseason = new OffseasonState(2028, 12, 70);

            Assert.Throws<InvalidOperationException>(() => service.ApplyLayout(
                board,
                new[]
                {
                    new PlacedSkillBlock(first, 0, 0, 0),
                    new PlacedSkillBlock(second, 0, 0, 0)
                },
                economy,
                offseason,
                2028,
                MoneyAmount.FromTenThousandWon(1_500L)));

            Assert.That(board.PlacedBlocks, Is.Empty);
            Assert.That(board.OwnedBlocks, Has.Count.EqualTo(2));
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(5_000L)));
        }

        [Test]
        public void RecoverInvalidPlacements_모양변경으로겹친기존장착을무료로보관함에돌린다()
        {
            SkillBlockDefinition definition = CreateDefinition(
                "contact_normal",
                SkillBlockRarity.Normal,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O),
                1);
            var service = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { definition });
            var board = new SkillBoardState("standard_4x4");
            SkillBlockInstance first = board.AddOwnedBlock(definition.BlockId);
            SkillBlockInstance second = board.AddOwnedBlock(definition.BlockId);
            SkillBlockInstance third = board.AddOwnedBlock(definition.BlockId);
            board.PlaceOwnedBlock(new PlacedSkillBlock(first, 0, 0, 0));
            board.PlaceOwnedBlock(new PlacedSkillBlock(second, 1, 0, 0));
            board.PlaceOwnedBlock(new PlacedSkillBlock(third, 3, 3, 0));

            int recoveredCount = service.RecoverInvalidPlacements(board);

            Assert.That(recoveredCount, Is.EqualTo(2));
            Assert.That(board.PlacedBlocks, Has.Count.EqualTo(1));
            Assert.That(board.PlacedBlocks[0].Instance.InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(board.OwnedBlocks, Has.Count.EqualTo(2));
        }

        private static SkillBlockDefinition[] CreateDefinitions()
        {
            return new[]
            {
                CreateDefinition("contact_normal", SkillBlockRarity.Normal,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.O), 1),
                CreateDefinition("contact_rare", SkillBlockRarity.Rare,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.I), 2),
                CreateDefinition("contact_elite", SkillBlockRarity.Elite,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.T), 4),
                CreateDefinition("contact_unique", SkillBlockRarity.Unique,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.S), 5),
                CreateDefinition("contact_legendary", SkillBlockRarity.Legendary,
                    TetrominoShapeCatalog.CreateCells(TetrominoShape.L), 7)
            };
        }

        private static SkillBlockDefinition CreateDefinition(
            string id,
            SkillBlockRarity rarity,
            BoardCell[] cells,
            int bonus)
        {
            return new SkillBlockDefinition(
                id,
                rarity,
                SkillBlockCategory.Contact,
                cells,
                cells.Length > 1,
                new[] { new AbilityChange(PlayerAbility.Contact, bonus) },
                MoneyAmount.FromTenThousandWon(100L));
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

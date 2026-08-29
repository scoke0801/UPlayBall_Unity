using System;
using System.Collections.Generic;
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
        public void DefaultMoneyValues_성장치료수상은계약과같은원단위를사용한다()
        {
            GrowthBalanceTable growth = GrowthBalanceTable.CreateDefault();

            Assert.That(growth.SkillGacha.SinglePrice,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(600L)));
            Assert.That(growth.SkillGacha.RarePrice,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(1_500L)));
            Assert.That(growth.SkillGacha.ElitePrice,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(4_000L)));
            Assert.That(growth.SkillGacha.UniquePrice,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(10_000L)));
            Assert.That(growth.SkillGacha.LegendaryPrice,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(25_000L)));
            Assert.That(growth.SkillGacha.GetFivePullPrice(SkillGachaPurchaseTier.Normal),
                Is.EqualTo(MoneyAmount.FromTenThousandWon(2_850L)));
            Assert.That(growth.FindProgram("personal_batting").MoneyCost,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(300L)));
            Assert.That(growth.SkillBoardRedesignCost,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(1_500L)));
            Assert.That(InjuryBalanceTable.CreateDefault().SpecialistTreatmentCost,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(500L)));
            Assert.That(SeasonSettlementBalance.CreateDefault().MinimumAwardMoney,
                Is.EqualTo(MoneyAmount.FromTenThousandWon(100L)));
        }

        [Test]
        public void PullSingle_Rare10회보장카운트다음뽑기에서Rare를지급한다()
        {
            SkillBlockDefinition[] definitions = CreateGachaDefinitions();
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, definitions);
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(10_000L));
            var board = new SkillBoardState("standard_4x4");
            var random = new FixedRandom(0d);

            for (int index = 0; index < 10; index++)
                service.PullSingle(economy, board, SkillBlockCategory.Contact, 2028, random);
            SkillBlockInstance guaranteed = service.PullSingle(
                economy, board, SkillBlockCategory.Contact, 2028, random);

            Assert.That(guaranteed.DefinitionId, Is.EqualTo("contact_elite"));
            Assert.That(board.PityEliteCount, Is.EqualTo(0));
        }

        [Test]
        public void PullSingle_Legendary60회보장카운트다음뽑기에서Legendary를지급한다()
        {
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, CreateGachaDefinitions());
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(40_000L));
            var board = new SkillBoardState("standard_4x4");
            var random = new FixedRandom(0d);

            for (int index = 0; index < balance.LegendaryPity; index++)
            {
                service.PullSingle(
                    economy,
                    board,
                    SkillBlockCategory.Contact,
                    SkillGachaPurchaseTier.Normal,
                    2028,
                    random);
            }
            SkillBlockInstance guaranteed = service.PullSingle(
                economy,
                board,
                SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Normal,
                2028,
                random);

            Assert.That(guaranteed.DefinitionId, Is.EqualTo("contact_legendary"));
            Assert.That(board.PityLegendaryCount, Is.EqualTo(0));
        }

        [Test]
        public void PullBundle_Common만나오면마지막에Uncommon이상을보장한다()
        {
            var service = new SkillGachaService(
                GrowthBalanceTable.CreateDefault().SkillGacha,
                CreateGachaDefinitions());
            var economy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));
            var board = new SkillBoardState("standard_4x4");

            SkillBlockInstance[] result = service.PullBundle(
                economy, board, SkillBlockCategory.Contact, 2028, new FixedRandom(0d));

            Assert.That(result, Has.Length.EqualTo(5));
            Assert.That(economy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(2_150L)));
        }

        [Test]
        public void PullSingle_고급구매는같은난수에서일반보다높은등급을지급한다()
        {
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, CreateGachaDefinitions());
            var standardEconomy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));
            var premiumEconomy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));
            var eliteEconomy = new CareerEconomyState(MoneyAmount.FromTenThousandWon(5_000L));

            SkillBlockInstance standard = service.PullSingle(
                standardEconomy,
                new SkillBoardState("standard_4x4"),
                SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Normal,
                2028,
                new FixedRandom(0.20d));
            SkillBlockInstance premium = service.PullSingle(
                premiumEconomy,
                new SkillBoardState("standard_4x4"),
                SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Rare,
                2028,
                new FixedRandom(0.20d));
            SkillBlockInstance elite = service.PullSingle(
                eliteEconomy,
                new SkillBoardState("standard_4x4"),
                SkillBlockCategory.Contact,
                SkillGachaPurchaseTier.Elite,
                2028,
                new FixedRandom(0.20d));

            Assert.That(standard.DefinitionId, Is.EqualTo("contact_normal"));
            Assert.That(premium.DefinitionId, Is.EqualTo("contact_rare"));
            Assert.That(elite.DefinitionId, Is.EqualTo("contact_elite"));
            Assert.That(standardEconomy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(4_400L)));
            Assert.That(premiumEconomy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(3_500L)));
            Assert.That(eliteEconomy.Money, Is.EqualTo(MoneyAmount.FromTenThousandWon(1_000L)));
        }

        [Test]
        [Timeout(30000)]
        public void PullSingle_십만회에서고급구매의Rare이상비율이유의미하게높다()
        {
            const int PullCount = 100_000;
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, CreateGachaDefinitions());
            var standardEconomy = new CareerEconomyState(2_000_000_000_000L);
            var premiumEconomy = new CareerEconomyState(2_000_000_000_000L);
            var standardBoard = new SkillBoardState("standard_4x4");
            var premiumBoard = new SkillBoardState("standard_4x4");
            var standardRandom = new Pcg32Random(20280828UL);
            var premiumRandom = new Pcg32Random(20280828UL);
            int standardRareOrBetter = 0;
            int premiumRareOrBetter = 0;
            int standardEpic = 0;
            int premiumEpic = 0;

            for (int index = 0; index < PullCount; index++)
            {
                SkillBlockInstance standard = service.PullSingle(
                    standardEconomy,
                    standardBoard,
                    SkillBlockCategory.Contact,
                    SkillGachaPurchaseTier.Normal,
                    2028,
                    standardRandom);
                SkillBlockInstance premium = service.PullSingle(
                    premiumEconomy,
                    premiumBoard,
                    SkillBlockCategory.Contact,
                    SkillGachaPurchaseTier.Rare,
                    2028,
                    premiumRandom);
                CountHighRarity(standard.DefinitionId, ref standardRareOrBetter, ref standardEpic);
                CountHighRarity(premium.DefinitionId, ref premiumRareOrBetter, ref premiumEpic);
            }

            double standardRareRate = standardRareOrBetter / (double)PullCount;
            double premiumRareRate = premiumRareOrBetter / (double)PullCount;
            double standardEpicRate = standardEpic / (double)PullCount;
            double premiumEpicRate = premiumEpic / (double)PullCount;
            System.Console.WriteLine(
                $"일반 R+ {standardRareRate:P2}, E {standardEpicRate:P2} · " +
                $"고급 R+ {premiumRareRate:P2}, E {premiumEpicRate:P2}");

            Assert.That(premiumRareRate, Is.GreaterThan(standardRareRate + 0.40d));
            Assert.That(premiumEpicRate, Is.GreaterThan(standardEpicRate + 0.10d));
        }

        [Test]
        [Timeout(30000)]
        public void PullSingle_십만회에서특급구매는Rare이상과Legendary비율을유지한다()
        {
            const int PullCount = 100_000;
            SkillGachaBalanceTable balance = GrowthBalanceTable.CreateDefault().SkillGacha;
            var service = new SkillGachaService(balance, CreateGachaDefinitions());
            var economy = new CareerEconomyState(4_000_000_000_000L);
            var board = new SkillBoardState("standard_4x4");
            var random = new Pcg32Random(20280829UL);
            int rareOrBetter = 0;
            int legendary = 0;

            for (int index = 0; index < PullCount; index++)
            {
                SkillBlockInstance result = service.PullSingle(
                    economy,
                    board,
                    SkillBlockCategory.Contact,
                    SkillGachaPurchaseTier.Elite,
                    2028,
                    random);
                if (result.DefinitionId.EndsWith("_elite", StringComparison.Ordinal) ||
                    result.DefinitionId.EndsWith("_unique", StringComparison.Ordinal) ||
                    result.DefinitionId.EndsWith("_legendary", StringComparison.Ordinal))
                {
                    rareOrBetter++;
                }
                if (result.DefinitionId.EndsWith("_legendary", StringComparison.Ordinal))
                    legendary++;
            }

            double rareOrBetterRate = rareOrBetter / (double)PullCount;
            double legendaryRate = legendary / (double)PullCount;
            System.Console.WriteLine(
                $"특급 R+ {rareOrBetterRate:P2}, L {legendaryRate:P2}");

            Assert.That(rareOrBetterRate, Is.EqualTo(1d));
            Assert.That(legendaryRate, Is.InRange(0.045d, 0.065d));
        }

        [Test]
        public void PlaceBlock_회전과겹침을검증하고Socket위Trait만활성화한다()
        {
            var trait = new SkillBlockDefinition(
                "trait_l",
                SkillBlockRarity.Rare,
                SkillBlockCategory.Contact,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.L),
                true,
                new[] { new AbilityChange(PlayerAbility.Contact, 2) },
                120L,
                "clutch_contact",
                TraitSocketRule.CoversSocket);
            var filler = new SkillBlockDefinition(
                "filler",
                SkillBlockRarity.Normal,
                SkillBlockCategory.Contact,
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O),
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

        [Test]
        public void GetPlacementPreview_상태변경없이모양과배치가능여부를반환한다()
        {
            SkillBlockDefinition block = CreateDefinition(
                "contact_normal",
                SkillBlockRarity.Normal,
                1,
                60L);
            var state = new SkillBoardState("standard_4x4");
            SkillBlockInstance instance = state.AddOwnedBlock(block.BlockId);
            var service = new SkillBoardService(
                SkillBoardDefinition.CreateDefault(),
                new[] { block });

            SkillBlockPlacementPreview valid = service.GetPlacementPreview(
                state,
                instance.InstanceId,
                1,
                1,
                0);
            SkillBlockPlacementPreview outOfBounds = service.GetPlacementPreview(
                state,
                instance.InstanceId,
                3,
                3,
                0);

            Assert.That(valid.Cells, Has.Length.EqualTo(4));
            Assert.That(valid.CanPlace, Is.True);
            Assert.That(outOfBounds.CanPlace, Is.False);
            Assert.That(state.PlacedBlocks, Is.Empty);
            Assert.That(state.OwnedBlocks, Has.Count.EqualTo(1));

            SkillBlockInstance second = state.AddOwnedBlock(block.BlockId);
            service.PlaceBlock(state, instance.InstanceId, 0, 0, 0);
            SkillBlockPlacementPreview overlap = service.GetPlacementPreview(
                state,
                second.InstanceId,
                0,
                0,
                0);

            Assert.That(overlap.CanPlace, Is.False);
            Assert.That(state.PlacedBlocks, Has.Count.EqualTo(1));
            Assert.That(state.OwnedBlocks, Has.Count.EqualTo(1));
        }

        [Test]
        public void DefaultBlocks_모두4칸표준테트로미노이며7종모양을포함한다()
        {
            SkillBlockDefinition[] definitions = GrowthSkillContent.CreateDefaultBlocks();
            TetrominoShape[] shapes = (TetrominoShape[])Enum.GetValues(typeof(TetrominoShape));
            var foundShapes = new bool[shapes.Length];

            for (int index = 0; index < definitions.Length; index++)
            {
                Assert.That(definitions[index].ShapeCells,
                    Has.Length.EqualTo(TetrominoShapeCatalog.CellCount));
                for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
                {
                    if (HasSameCells(
                        definitions[index].ShapeCells,
                        TetrominoShapeCatalog.CreateCells(shapes[shapeIndex])))
                    {
                        foundShapes[shapeIndex] = true;
                    }
                }
            }

            Assert.That(foundShapes, Has.All.True);
        }

        [Test]
        public void DefaultBlocks_모든계통과등급에서7종모양이모두나온다()
        {
            SkillBlockDefinition[] definitions = GrowthSkillContent.CreateDefaultBlocks();
            var shapes = (TetrominoShape[])Enum.GetValues(typeof(TetrominoShape));
            var categories = (SkillBlockCategory[])Enum.GetValues(typeof(SkillBlockCategory));
            var rarities = (SkillBlockRarity[])Enum.GetValues(typeof(SkillBlockRarity));

            for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
            {
                for (int rarityIndex = 0; rarityIndex < rarities.Length; rarityIndex++)
                {
                    var foundShapes = new bool[shapes.Length];
                    for (int index = 0; index < definitions.Length; index++)
                    {
                        SkillBlockDefinition definition = definitions[index];
                        if (definition.Category != categories[categoryIndex] ||
                            definition.Rarity != rarities[rarityIndex])
                        {
                            continue;
                        }
                        for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
                        {
                            if (HasSameCells(
                                definition.ShapeCells,
                                TetrominoShapeCatalog.CreateCells(shapes[shapeIndex])))
                            {
                                foundShapes[shapeIndex] = true;
                            }
                        }
                    }

                    Assert.That(
                        foundShapes,
                        Has.All.True,
                        $"{categories[categoryIndex]} {rarities[rarityIndex]} 풀에 빠진 모양이 있습니다.");
                }
            }
        }

        [Test]
        public void DefaultBlocks_블록ID는중복되지않는다()
        {
            SkillBlockDefinition[] definitions = GrowthSkillContent.CreateDefaultBlocks();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < definitions.Length; index++)
                Assert.That(seenIds.Add(definitions[index].BlockId), Is.True, definitions[index].BlockId);
        }

        [Test]
        public void SkillBlockDefinition_4칸연결조건을위반하면거부한다()
        {
            Assert.Throws<ArgumentException>(() => new SkillBlockDefinition(
                "triomino",
                SkillBlockRarity.Elite,
                SkillBlockCategory.Contact,
                new[] { new BoardCell(0, 0), new BoardCell(1, 0), new BoardCell(0, 1) },
                true,
                Array.Empty<AbilityChange>(),
                0L));
            Assert.Throws<ArgumentException>(() => new SkillBlockDefinition(
                "disconnected",
                SkillBlockRarity.Normal,
                SkillBlockCategory.Contact,
                new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0),
                    new BoardCell(3, 0), new BoardCell(4, 0)
                },
                true,
                Array.Empty<AbilityChange>(),
                0L));
            Assert.Throws<ArgumentException>(() => new SkillBlockDefinition(
                "six_cells",
                SkillBlockRarity.Legendary,
                SkillBlockCategory.Contact,
                new[]
                {
                    new BoardCell(0, 0), new BoardCell(1, 0), new BoardCell(2, 0),
                    new BoardCell(0, 1), new BoardCell(1, 1), new BoardCell(2, 1)
                },
                true,
                Array.Empty<AbilityChange>(),
                0L));
        }

        private static SkillBlockDefinition[] CreateGachaDefinitions()
        {
            return new[]
            {
                CreateDefinition("contact_normal", SkillBlockRarity.Normal, 1, 60L),
                CreateDefinition("contact_rare", SkillBlockRarity.Rare, 2, 90L),
                CreateDefinition("contact_elite", SkillBlockRarity.Elite, 4, 120L),
                CreateDefinition("contact_unique", SkillBlockRarity.Unique, 6, 150L),
                CreateDefinition("contact_legendary", SkillBlockRarity.Legendary, 8, 220L)
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
                TetrominoShapeCatalog.CreateCells(TetrominoShape.O),
                false,
                new[] { new AbilityChange(PlayerAbility.Contact, bonus) },
                sellValue);
        }

        private static void CountHighRarity(
            string definitionId,
            ref int rareOrBetter,
            ref int epic)
        {
            if (definitionId.EndsWith("_normal", StringComparison.Ordinal))
                return;
            rareOrBetter++;
            if (definitionId.EndsWith("_elite", StringComparison.Ordinal) ||
                definitionId.EndsWith("_unique", StringComparison.Ordinal) ||
                definitionId.EndsWith("_legendary", StringComparison.Ordinal))
            {
                epic++;
            }
        }

        private static bool HasSameCells(BoardCell[] left, BoardCell[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int leftIndex = 0; leftIndex < left.Length; leftIndex++)
            {
                bool found = false;
                for (int rightIndex = 0; rightIndex < right.Length; rightIndex++)
                {
                    if (left[leftIndex].X == right[rightIndex].X &&
                        left[leftIndex].Y == right[rightIndex].Y)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
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

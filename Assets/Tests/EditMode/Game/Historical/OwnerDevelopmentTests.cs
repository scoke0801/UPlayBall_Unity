using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed partial class OwnerDevelopmentTests
    {
        private static ManagerHistoricalRuntimeState Runtime(out ManagerHistoricalSaveAdapter adapter, bool offseason = true)
        {
            var fixture = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture",BindingFlags.NonPublic)
                .GetMethod("Create").Invoke(null,new object[]{WorldRecordMode.SimulatedHistory,false});
            adapter=(ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture,null);
            var runtime=adapter.Restore(adapter.CreateSaveData((ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture)));
            if (!offseason) return runtime;
            foreach(var group in runtime.LeagueWorld.Groups) foreach(var game in group.Season.Schedule.Games) if(!game.IsCompleted) game.Complete(1,0);
            typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
            return runtime;
        }
        private static string Reserve(ManagerHistoricalRuntimeState runtime)
        {
            var definition=runtime.WorldCardCatalog.Cards.First(card=>card.CanAcquireFromScout && !runtime.TryGetOwnedCard(card.CardId,out _)
                && runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerType==PlayerType.Batter);
            runtime.AcquireCard(definition.CardId); return definition.CardId;
        }
        private static OwnerDevelopmentBalance Balance() => new OwnerDevelopmentBalance
        {
            correctionCost=100,partnerCost=100,researchCost=100,researchWeights=new[]{50,35,15},
            slogans=new[]{new OwnerSloganDefinition{id="contact",name="정교한 야구",target=OwnerSupportTarget.Batter,requiredAbility=PlayerAbility.Contact,
                minimumAbility=1,cardsRequired=new[]{1,2,3,4,5,6},bonusAbility=PlayerAbility.Contact,bonusByLevel=new[]{1,1,2,2,3,4},
                penaltyAbility=PlayerAbility.Power,penaltyByLevel=new[]{-1,-1,-1,-1,-1,0}}},
            studyTiers=new[]{new OwnerStudyTierDefinition{weeks=1,cost=120000,growth=3,greatProbability=.2,greatBonus=1},
                new OwnerStudyTierDefinition{weeks=2,cost=350000,growth=7,greatProbability=.15,greatBonus=2},
                new OwnerStudyTierDefinition{weeks=3,cost=750000,growth=12,greatProbability=.1,greatBonus=3}}
        };
        [Test]
        public void 새성장판은_16칸에배치하고_경계와겹침을거부한다()
        {
            var growth = GrowthBalanceTable.CreateDefault();
            var inventory = new OwnerSkillBlockInventoryState();
            var board = new OwnedCardSkillBoardState();
            var service = new OwnerSkillBoardService(growth);
            string definitionId = growth.SkillBlocks.First(block => block.ShapeCells.All(cell => cell.Y == 0)).BlockId;
            for (int y = 0; y < 4; y++)
                service.Place(inventory, board, inventory.Add(definitionId).InstanceId, 0, y, 0);
            Assert.That(board.Placements.Count, Is.EqualTo(4));
            int extra = inventory.Add(definitionId).InstanceId;
            Assert.Throws<InvalidOperationException>(() => service.Place(inventory, board, extra, 0, 3, 0));
            Assert.Throws<InvalidOperationException>(() => service.Place(inventory, board, extra, 1, 0, 0));
            Assert.Throws<InvalidOperationException>(() => service.Place(inventory, board, extra, -1, 0, 0));
            Assert.That(board.Placements.Count, Is.EqualTo(4));
        }

        [Test]
        public void 마지막칸배치는_저장복원후에도유지되고_재배치할수있다()
        {
            var runtime = Runtime(out var adapter);
            string id = Reserve(runtime);
            runtime.TryGetOwnedCard(id, out var owned);
            var growth = GrowthBalanceTable.CreateDefault();
            var service = new OwnerSkillBoardService(growth);
            string definitionId = growth.SkillBlocks.First(block => block.ShapeCells.All(cell => cell.Y == 0)).BlockId;
            int instanceId = runtime.PlayerGrowth.Inventory.Add(definitionId).InstanceId;
            service.Place(runtime.PlayerGrowth.Inventory, owned.SkillBoard, instanceId, 0, 3, 0);

            var save = adapter.CreateSaveData(runtime);
            Assert.That(save.saveVersion, Is.EqualTo(ManagerHistoricalSaveAdapter.CurrentSaveVersion));
            runtime = adapter.Restore(save);
            runtime.TryGetOwnedCard(id, out owned);
            Assert.That(owned.SkillBoard.Placements.Single().OriginX, Is.Zero);
            Assert.That(owned.SkillBoard.Placements.Single().OriginY, Is.EqualTo(3));
            Assert.That(service.Remove(owned.SkillBoard, instanceId), Is.True);
            service.Place(runtime.PlayerGrowth.Inventory, owned.SkillBoard, instanceId, 0, 2, 0);
            Assert.That(owned.SkillBoard.Placements.Single().OriginY, Is.EqualTo(2));
        }

        [Test]
        public void 교정은_총합유지와_세번제한_저장후중복요청을검증한다()
        {
            var runtime=Runtime(out var adapter); string id=runtime.OwnedCards.First(card=>runtime.WorldCardCatalog.GetPlayerSeason(
                runtime.WorldCardCatalog.Cards.First(d=>d.CardId==card.CardId)).PlayerType==PlayerType.Batter).CardId;
            runtime.TryGetOwnedCard(id,out var owned); int expected=owned.Training.Ledger.Count;
            var preview=OwnerPermanentGrowthService.PreviewCorrection(runtime,id,PlayerAbility.Power,PlayerAbility.Contact,1);
            Assert.That(preview.Sum(),Is.Zero);
            OwnerPermanentGrowthService.Correct(runtime,id,PlayerAbility.Power,PlayerAbility.Contact,1,100,expected);
            long money=runtime.Economy.Money;
            Assert.Throws<InvalidOperationException>(()=>OwnerPermanentGrowthService.Correct(runtime,id,PlayerAbility.Power,PlayerAbility.Contact,1,100,expected));
            Assert.That(runtime.Economy.Money,Is.EqualTo(money));
            runtime=adapter.Restore(adapter.CreateSaveData(runtime)); runtime.TryGetOwnedCard(id,out owned);
            Assert.That(OwnerPermanentGrowthService.Count(owned,OwnerGrowthSource.Correction),Is.EqualTo(1));
            Assert.That(owned.Training.Ledger.Get(OwnerGrowthSource.Correction,PlayerAbility.Power),Is.EqualTo(-1));
        }
        [Test]
        public void 연구열번은_블록스무개와_S선택상자하나를보장한다()
        {
            var runtime=Runtime(out var adapter); var growth=GrowthBalanceTable.CreateDefault(); var balance=Balance();
            int before=runtime.PlayerGrowth.Inventory.Blocks.Count;
            for(int i=0;i<10;i++) OwnerSkillResearchService.Research(runtime,growth.SkillBlocks,balance,new Pcg32Random((ulong)i));
            Assert.That(runtime.PlayerGrowth.Inventory.Blocks.Count,Is.EqualTo(before+20));
            Assert.That(runtime.PlayerGrowth.Inventory.SelectionBoxes,Is.EqualTo(1));
            runtime=adapter.Restore(adapter.CreateSaveData(runtime)); typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
            var selected=growth.SkillBlocks.First(b=>b.Rarity==SkillBlockRarity.Unique);
            OwnerSkillResearchService.OpenSelectionBox(runtime,selected);
            Assert.That(runtime.PlayerGrowth.Inventory.SelectionBoxes,Is.Zero);
            Assert.Throws<InvalidOperationException>(()=>OwnerSkillResearchService.OpenSelectionBox(runtime,selected));
        }
        [Test]
        public void 유학은_등록시결과를저장하며_취소환급은진행여부를따른다()
        {
            var runtime=Runtime(out var adapter); string id=Reserve(runtime); runtime.TryGetOwnedCard(id,out var owned);
            runtime.WorldCardCatalog.TryGetCard(id,out var definition); var season=runtime.WorldCardCatalog.GetPlayerSeason(definition);
            var program=new CardStudyProgramDefinition("study_contact","정교 타격",PlayerType.Batter,0,2,
                new[]{new AbilityChange(PlayerAbility.Contact,2)},moneyCost:100,greatSuccessProbability:1,greatSuccessBonus:1);
            long before=runtime.Economy.Money;
            OwnerCardStudyResolver.Start(runtime.PlayerGrowth,owned,season,program,runtime.Economy,1,3,99,new Pcg32Random(99));
            var saved=adapter.CreateSaveData(runtime); runtime=adapter.Restore(saved); typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
            Assert.That(runtime.PlayerGrowth.StudyProjects[0].ResultBonus,Is.EqualTo(1));
            Assert.That(runtime.PlayerGrowth.StudyProjects[0].ResultSeed,Is.EqualTo(99UL));
            runtime.PlayerGrowth.StudyProjects[0].AdvanceWeek(); OwnerStudyCancellationService.Cancel(runtime,id);
            Assert.That(runtime.Economy.Money,Is.EqualTo(before-50)); Assert.That(runtime.PlayerGrowth.StudyProjects,Is.Empty);
            runtime.TryGetOwnedCard(id,out owned); Assert.That(owned.Training.GetStudyBonus(PlayerAbility.Contact),Is.Zero);
        }
        [Test]
        public void 슬로건은_해금후상승하락을함께반영하고_시즌중변경을거부한다()
        {
            var runtime=Runtime(out var adapter); var definition=Balance().slogans[0];
            OwnerSloganService.Select(runtime,definition);
            Assert.That(runtime.PlayerGrowth.Slogan.Level,Is.EqualTo(6));
            var restored=adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(restored.PlayerGrowth.Slogan.Level,Is.EqualTo(6));
            Assert.That(runtime.OwnedCards.Any(card=>card.Training.Ledger.Get(OwnerGrowthSource.Slogan,PlayerAbility.Contact)==4),Is.True);
            var regular = Runtime(out _, false);
            Assert.Throws<InvalidOperationException>(()=>OwnerSloganService.Select(regular,definition));
        }
        [Test]
        public void 성장등급은_기존목적지를보존하고_기간비용과보상을전환한다()
        {
            var source=OwnerCardGrowthBalanceTable.CreateDefault(); var mapped=Balance().ApplyStudyTiers(source);
            Assert.That(mapped.StudyPrograms.Count,Is.EqualTo(source.StudyPrograms.Count));
            foreach(var program in mapped.StudyPrograms)
            {
                int tier=(int)program.UnlockRequirement.Kind;
                Assert.That(program.DurationWeeks,Is.EqualTo(tier+1));
                Assert.That(program.Rewards.Sum(r=>r.Amount),Is.EqualTo(new[]{3,7,12}[tier]));
                Assert.That(program.DevelopmentPointCost,Is.Zero);
                Assert.That(program.DestinationName,Is.EqualTo(source.GetStudyProgram(program.ProgramId).DestinationName));
            }
        }
    }
}

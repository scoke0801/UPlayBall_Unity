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
            correctionCost=100,partnerCost=100,researchCost=100,slotExperienceRequired=100,researchWeights=new[]{50,35,15},
            camps=new[]{new OwnerCampDefinition{id="local",name="지역",capacity=6,weeklyExperience=40,costPerCardCost=1}},
            slogans=new[]{new OwnerSloganDefinition{id="contact",name="정교한 야구",target=OwnerSupportTarget.Batter,requiredAbility=PlayerAbility.Contact,
                minimumAbility=1,cardsRequired=new[]{1,2,3,4,5,6},bonusAbility=PlayerAbility.Contact,bonusByLevel=new[]{1,1,2,2,3,4},
                penaltyAbility=PlayerAbility.Power,penaltyByLevel=new[]{-1,-1,-1,-1,-1,0}}},
            studyTiers=new[]{new OwnerStudyTierDefinition{weeks=1,cost=120000,growth=3,greatProbability=.2,greatBonus=1},
                new OwnerStudyTierDefinition{weeks=2,cost=350000,growth=7,greatProbability=.15,greatBonus=2},
                new OwnerStudyTierDefinition{weeks=3,cost=750000,growth=12,greatProbability=.1,greatBonus=3}}
        };
        [Test]
        public void 초기여섯칸은_잠금배치를거부하고_인접칸만개방한다()
        {
            var board=new OwnedCardSkillBoardState(); int unlocked=0;
            for(int y=0;y<4;y++) for(int x=0;x<4;x++) if(board.IsCellUnlocked(x,y)) unlocked++;
            Assert.That(unlocked,Is.EqualTo(6));
            board.AddSlotExperience(100);
            Assert.Throws<InvalidOperationException>(()=>board.UnlockCell(3,3,100));
            Assert.That(board.SlotExperience,Is.EqualTo(100));
            board.UnlockCell(3,0,100); Assert.That(board.IsCellUnlocked(3,0),Is.True); Assert.That(board.SlotExperience,Is.Zero);
            var growth=GrowthBalanceTable.CreateDefault(); var inventory=new OwnerSkillBlockInventoryState();
            var block=inventory.Add(growth.SkillBlocks[0].BlockId);
            Assert.Throws<InvalidOperationException>(()=>new OwnerSkillBoardService(growth).Place(inventory,board,block.InstanceId,3,3,0));
            Assert.That(board.Placements,Is.Empty);
        }
        [Test]
        public void 캠프는_경험치를보존하며_세주뒤자동귀환한다()
        {
            var runtime=Runtime(out var adapter); string id=Reserve(runtime); var balance=Balance();
            OwnerCampService.Start(runtime,id,balance.camps[0],100);
            var coordinator=new ManagerModeCoordinator(BalanceTable.CreateDefault());
            coordinator.AdvanceOffseasonWeek(runtime,0); coordinator.AdvanceOffseasonWeek(runtime,1);
            runtime=adapter.Restore(adapter.CreateSaveData(runtime));
            // 복원으로 월드가 다시 구성돼도 테스트의 완료된 오프시즌 상태는 동일하게 유지한다.
            typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
            coordinator.AdvanceOffseasonWeek(runtime,2);
            Assert.That(runtime.PlayerGrowth.Camps,Is.Empty); runtime.TryGetOwnedCard(id,out var card);
            Assert.That(card.SkillBoard.SlotExperience,Is.EqualTo(120));
            OwnerCampService.Unlock(runtime,id,3,0,100); Assert.That(card.SkillBoard.SlotExperience,Is.EqualTo(20));
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

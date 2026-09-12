using System;
using System.Linq;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed partial class OwnerDevelopmentTests
    {
        [Test]
        public void 미선택슬로건이_JSON기본값객체가되어도_저장을복원한다()
        {
            var runtime = Runtime(out var adapter, false);
            var save = adapter.CreateSaveData(runtime);
            save.playerGrowth.slogan = new OwnerSloganDefinition {
                id = "", name = "", minimumAbilityByLevel = Array.Empty<int>(),
                cardsRequired = Array.Empty<int>(), bonusByLevel = Array.Empty<int>(), penaltyByLevel = Array.Empty<int>() };

            var restored = adapter.Restore(save);

            Assert.That(restored.PlayerGrowth.Slogan, Is.Null);
            Assert.That(restored.OwnedCards.Count, Is.EqualTo(runtime.OwnedCards.Count));
            Assert.That(restored.Economy.Money, Is.EqualTo(runtime.Economy.Money));
        }

        [Test]
        public void 선택된슬로건은_빈선택적배열을복원하고_손상된정의는거부한다()
        {
            var runtime = Runtime(out var adapter);
            OwnerSloganService.Select(runtime, Balance().slogans[0]);
            var save = adapter.CreateSaveData(runtime);
            save.playerGrowth.slogan.minimumAbilityByLevel = Array.Empty<int>();

            var restored = adapter.Restore(save);
            Assert.That(restored.PlayerGrowth.Slogan.Level, Is.EqualTo(runtime.PlayerGrowth.Slogan.Level));
            Assert.That(restored.PlayerGrowth.Slogan.Definition.GetMinimumAbility(restored.PlayerGrowth.Slogan.Level), Is.EqualTo(1));
            Assert.That(restored.OwnedCards.Sum(c => c.Training.Ledger.Count), Is.EqualTo(runtime.OwnedCards.Sum(c => c.Training.Ledger.Count)));

            save.playerGrowth.slogan.cardsRequired = Array.Empty<int>();
            Assert.Throws<ArgumentException>(() => adapter.Restore(save));
            save.playerGrowth.slogan = null;
            Assert.Throws<ArgumentException>(() => adapter.Restore(save));
        }

        [Test]
        public void 테트로미노도_기존개별전체보너스상한을넘지않는다()
        {
            var growth=OwnerSkillContent.Compose(GrowthBalanceTable.CreateDefault(),20);
            var inventory=new OwnerSkillBlockInventoryState();
            var board=new OwnedCardSkillBoardState();
            var placement=new OwnerSkillBoardService(growth);
            var categories=new[]{SkillBlockCategory.Contact,SkillBlockCategory.Power,SkillBlockCategory.Baserunning,SkillBlockCategory.Defense};
            for(int y=0;y<4;y++)
            {
                var definition=growth.SkillBlocks.First(b=>b.Category==categories[y] && b.Rarity==SkillBlockRarity.Legendary && b.ShapeCells.All(c=>c.Y==0));
                placement.Place(inventory,board,inventory.Add(definition.BlockId).InstanceId,0,y,0);
            }
            var service=new SkillBoardService(growth.SkillBoard,growth.SkillBlocks); int total=0;
            for(int i=0;i<12;i++)
            {
                int bonus=service.GetAbilityBonus(board.Placements,(PlayerAbility)i); total+=bonus;
                Assert.That(bonus,Is.LessThanOrEqualTo(SkillBoardService.MaximumBonusPerAbility));
            }
            Assert.That(total,Is.EqualTo(SkillBoardService.MaximumTotalAbilityBonus));
        }
        [Test]
        public void 실제경기서비스는_저장사이의두경기에서만_서포트를소모한다()
        {
            var fixture = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture",System.Reflection.BindingFlags.NonPublic)
                .GetMethod("Create").Invoke(null,new object[]{WorldRecordMode.SimulatedHistory,false});
            var provider = fixture.GetType().GetProperty("Provider").GetValue(fixture);
            var content = (HistoricalBakedContent)provider.GetType().GetMethod("Load").Invoke(provider,null);
            var adapter=(ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture,null);
            var runtime=(ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture);
            runtime=adapter.Restore(adapter.CreateSaveData(runtime));
            var definition=new OwnerSupportDefinition{id="match_support",displayName="실전 지원",scope=OwnerSupportScope.Team,
                price=100,conditionPoints=20,bonuses=new int[12]};
            OwnerSupportService.Purchase(runtime,definition); OwnerSupportService.Equip(runtime,definition,"");
            new ManagerModeMatchService(content,BalanceTable.CreateDefault()).PlayNextGame(runtime);
            Assert.That(runtime.PlayerGrowth.Support.Assignments.Single().RemainingGames,Is.EqualTo(1));
            runtime=adapter.Restore(adapter.CreateSaveData(runtime));
            new ManagerModeMatchService(content,BalanceTable.CreateDefault()).PlayNextGame(runtime);
            Assert.That(runtime.PlayerGrowth.Support.Assignments,Is.Empty);
        }
        [Test]
        public void 테트로미노는_세개인접시에만_세트효과를받는다()
        {
            var growth = OwnerSkillContent.Compose(GrowthBalanceTable.CreateDefault(), 1);
            Assert.That(growth.SkillBlocks.All(b => b.ShapeCells.Length == 4), Is.True);
            var definition = growth.SkillBlocks.First(b => b.ShapeCells.All(c => c.Y == 0) && b.Category == SkillBlockCategory.Contact);
            var inventory = new OwnerSkillBlockInventoryState(); var board = new OwnedCardSkillBoardState();
            var service = new OwnerSkillBoardService(growth); var geometry = new SkillBoardService(growth.SkillBoard,growth.SkillBlocks);
            for(int i=0;i<2;i++) service.Place(inventory,board,inventory.Add(definition.BlockId).InstanceId,0,i,0);
            Assert.That(OwnerSkillSetResolver.GetBonus(board.Placements,geometry,growth.SkillBlocks,PlayerAbility.Contact),Is.Zero);
            var third=inventory.Add(definition.BlockId); service.Place(inventory,board,third.InstanceId,0,2,0);
            Assert.That(OwnerSkillSetResolver.GetBonus(board.Placements,geometry,growth.SkillBlocks,PlayerAbility.Contact),Is.EqualTo(1));
            service.Remove(board,third.InstanceId);
            Assert.That(OwnerSkillSetResolver.GetBonus(board.Placements,geometry,growth.SkillBlocks,PlayerAbility.Contact),Is.Zero);
        }
        [Test]
        public void 합성으로마지막블록을소비해도_복원후번호를재사용하지않는다()
        {
            var runtime=Runtime(out var adapter); var inventory=runtime.PlayerGrowth.Inventory;
            var id=GrowthBalanceTable.CreateDefault().SkillBlocks[0].BlockId;
            var last=inventory.Add(id); inventory.Remove(last.InstanceId);
            runtime=adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(runtime.PlayerGrowth.Inventory.Add(id).InstanceId,Is.GreaterThan(last.InstanceId));
        }
        [Test]
        public void 슬로건은_같은편성갱신으로중복되지않고_저장원본과분리된다()
        {
            var runtime=Runtime(out var adapter); OwnerSloganService.Select(runtime,Balance().slogans[0]);
            int count=runtime.OwnedCards.Sum(c=>c.Training.Ledger.Count);
            OwnerSloganService.SynchronizeRoster(runtime); OwnerSloganService.SynchronizeRoster(runtime);
            Assert.That(runtime.OwnedCards.Sum(c=>c.Training.Ledger.Count),Is.EqualTo(count));
            var save=adapter.CreateSaveData(runtime); var clone=adapter.Restore(save);
            clone.PlayerGrowth.Slogan.Definition.bonusByLevel[5]=99;
            Assert.That(runtime.PlayerGrowth.Slogan.Definition.bonusByLevel[5],Is.EqualTo(4));
            Assert.That(save.playerGrowth.slogan.bonusByLevel[5],Is.EqualTo(4));
        }
        [Test]
        public void 파트너추천과확정은_같은성장을반영하고_양쪽참여를잠근다()
        {
            var runtime=Runtime(out var adapter);
            foreach(var card in runtime.WorldCardCatalog.Cards.Where(c=>c.CanAcquireFromScout).ToArray())
                if(!runtime.TryGetOwnedCard(card.CardId,out _)) runtime.AcquireCard(card.CardId);
            OwnedPlayerCardState target=null; OwnerPartnerPreview preview=null;
            foreach(var card in runtime.OwnedCards)
            {
                var candidates=OwnerPermanentGrowthService.Recommend(runtime,card.CardId);
                if(candidates.Count==0) continue;
                target=card; preview=candidates[0]; break;
            }
            Assert.That(preview,Is.Not.Null,"역할에 유효한 성장 후보가 필요합니다.");
            var repeated=OwnerPermanentGrowthService.PreviewPartner(runtime,target.CardId,preview.PartnerCardId);
            Assert.That(repeated.Values,Is.EqualTo(preview.Values));
            long before=runtime.Economy.Money;
            OwnerPermanentGrowthService.Partner(runtime,target.CardId,preview.PartnerCardId,100);
            Assert.That(runtime.Economy.Money,Is.EqualTo(before-100));
            for(int i=0;i<12;i++) Assert.That(target.Training.Ledger.Get(OwnerGrowthSource.Mentoring,(PlayerAbility)i),Is.EqualTo(preview.Values[i]));
            Assert.Throws<InvalidOperationException>(()=>OwnerPermanentGrowthService.Partner(runtime,target.CardId,preview.PartnerCardId,100));
            Assert.That(adapter.Restore(adapter.CreateSaveData(runtime)).Economy.Money,Is.EqualTo(before-100));
        }
        [Test]
        public void 컨디션서포트는_원본상태를바꾸지않고_저장후만료한다()
        {
            var runtime=Runtime(out var adapter,false);
            var definition=new OwnerSupportDefinition{id="condition",displayName="회복 지원",scope=OwnerSupportScope.Team,
                price=100,conditionPoints=20,bonuses=new int[12]};
            OwnerSupportService.Purchase(runtime,definition); OwnerSupportService.Equip(runtime,definition,"");
            string id=runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries[0].CardId;
            Assert.That(OwnerSupportService.GetConditionBonus(runtime,id),Is.EqualTo(20));
            definition.conditionPoints=99;
            Assert.That(OwnerSupportService.GetConditionBonus(runtime,id),Is.EqualTo(20),"외부 정의 변경과 분리");
            runtime=adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(OwnerSupportService.GetConditionBonus(runtime,id),Is.EqualTo(20));
            OwnerSupportService.CompleteMatch(runtime); OwnerSupportService.CompleteMatch(runtime);
            Assert.That(OwnerSupportService.GetConditionBonus(runtime,id),Is.Zero);
        }
    }
}

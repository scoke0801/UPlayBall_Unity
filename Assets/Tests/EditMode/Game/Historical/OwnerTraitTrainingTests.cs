using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>실제 저장 어댑터·경제·후보 명령으로 특성훈련 트랜잭션을 검증한다.</summary>
    public sealed class OwnerTraitTrainingTests
    {
        private static ManagerHistoricalRuntimeState Create(out ManagerHistoricalSaveAdapter adapter, bool offseason = true)
        {
            var fixture = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", BindingFlags.NonPublic)
                .GetMethod("Create").Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            adapter = (ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture,null);
            var runtime = adapter.CreateSimulationCopy((ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture));
            if (offseason)
            {
                foreach (var group in runtime.LeagueWorld.Groups)
                    foreach (var game in group.Season.Schedule.Games) if (!game.IsCompleted) game.Complete(1,0);
                typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(runtime,null);
            }
            return runtime;
        }
        private static string Target(ManagerHistoricalRuntimeState state) => state.OwnedCards.First(c =>
            OwnerTraitTrainingService.Season(state,c.CardId).PlayerType == PlayerType.Batter).CardId;
        private static string Partner(ManagerHistoricalRuntimeState state, string id, OwnerTraitTrainingBalance balance) =>
            state.OwnedCards.First(c => {
                try { OwnerTraitTrainingService.PartnerExperience(state,id,c.CardId,balance); return true; }
                catch (InvalidOperationException) { return false; }
            }).CardId;

        [Test]
        public void 훈련은_미리보기대로차감하고_카드를보존한다()
        {
            var state = Create(out _); var balance = new OwnerTraitTrainingBalance();
            OwnerTraitTrainingService.GrantOffseasonReward(state,balance);
            string id = Target(state), partner = Partner(state,id,balance);
            var preview = OwnerTraitTrainingService.Preview(state,id,new[]{partner},balance);
            long money = state.Economy.Money; int points = state.PlayerGrowth.Traits.points, count = state.OwnedCards.Count;
            OwnerTraitTrainingService.Train(state,id,new[]{partner},balance,state.ManagerMode.LiveSeason.SeasonNumber,0,new Pcg32Random(7));
            Assert.That(state.Economy.Money,Is.EqualTo(money-preview.Money));
            Assert.That(state.PlayerGrowth.Traits.points,Is.EqualTo(points-preview.Points));
            Assert.That(state.OwnedCards.Count,Is.EqualTo(count));
            state.TryGetOwnedCard(id,out var card); Assert.That(card.Trait.experience,Is.EqualTo(preview.TotalExperience));
            state.TryGetOwnedCard(partner,out var mentor); Assert.That(mentor.Trait.partnerUses,Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => OwnerTraitTrainingService.Train(state,id,new[]{partner},balance,state.ManagerMode.LiveSeason.SeasonNumber,0,new Pcg32Random(7)));
        }

        [Test]
        public void 후보는_저장복원후동일하고_복사본을바꿔도원본은보존된다()
        {
            var state = Create(out var adapter); var balance = new OwnerTraitTrainingBalance();
            string id=Target(state); state.TryGetOwnedCard(id,out var card); card.Trait.experience=99;
            state.PlayerGrowth.Traits.points=1000; string partner=Partner(state,id,balance);
            int season=state.ManagerMode.LiveSeason.SeasonNumber;
            OwnerTraitTrainingService.Train(state,id,new[]{partner},balance,season,0,new Pcg32Random(7));
            Assert.That(card.Trait.rank,Is.EqualTo(CardTraitRank.None)); Assert.That(card.Trait.candidates.Distinct().Count(),Is.EqualTo(3));
            var restored=adapter.CreateSimulationCopy(state); restored.TryGetOwnedCard(id,out var copy);
            // 합성 Fixture가 재생성하는 월드 대진 대신 단일 리그의 비시즌 조건을 고정한다.
            typeof(ManagerHistoricalRuntimeState).GetProperty("LeagueWorld").SetValue(restored,null);
            CollectionAssert.AreEqual(card.Trait.candidates,copy.Trait.candidates); Assert.That(copy.Trait.candidateSeed,Is.EqualTo(card.Trait.candidateSeed));
            int points=restored.PlayerGrowth.Traits.points;
            OwnerTraitTrainingService.Reroll(restored,id,balance,season,copy.Trait.revision,new Pcg32Random(8));
            Assert.That(restored.PlayerGrowth.Traits.points,Is.EqualTo(points));
            restored.TryGetOwnedCard(id,out copy);
            OwnerTraitTrainingService.Reroll(restored,id,balance,season,copy.Trait.revision,new Pcg32Random(9));
            Assert.That(restored.PlayerGrowth.Traits.points,Is.EqualTo(points-balance.rerollCost));
            restored.TryGetOwnedCard(id,out copy);
            var kind=copy.Trait.candidates[0];
            OwnerTraitTrainingService.Choose(restored,id,kind,balance,season,copy.Trait.revision);
            Assert.That(copy.Trait.rank,Is.EqualTo(CardTraitRank.C)); Assert.That(copy.Trait.trait,Is.EqualTo(kind));
            Assert.That(card.Trait.rank,Is.EqualTo(CardTraitRank.None));
            int exp=copy.Trait.experience;
            OwnerTraitTrainingService.Change(restored,id,balance,season,copy.Trait.revision,new Pcg32Random(10));
            restored.TryGetOwnedCard(id,out copy); Assert.That(copy.Trait.experience,Is.EqualTo(exp)); Assert.That(copy.Trait.rank,Is.EqualTo(CardTraitRank.C));
        }

        [Test]
        public void 시즌잠금_중복파트너_재화부족_최고등급은_무차감차단한다()
        {
            var regular=Create(out _,false); var balance=new OwnerTraitTrainingBalance();
            Assert.Throws<InvalidOperationException>(() => OwnerTraitTrainingService.Preview(regular,Target(regular),new[]{Target(regular)},balance));
            var state=Create(out _); string id=Target(state); string partner=Partner(state,id,balance); state.TryGetOwnedCard(id,out var card);
            long money=state.Economy.Money;
            Assert.Throws<InvalidOperationException>(() => OwnerTraitTrainingService.Train(state,id,new[]{partner},balance,state.ManagerMode.LiveSeason.SeasonNumber,0,new Pcg32Random(3)));
            Assert.That(state.Economy.Money,Is.EqualTo(money)); Assert.That(card.Trait.experience,Is.Zero);
            card.Trait.experience=500; card.Trait.rank=CardTraitRank.A; card.Trait.trait=CardTraitKind.Contact;
            Assert.Throws<InvalidOperationException>(()=>OwnerTraitTrainingService.Preview(state,id,new[]{partner,partner},balance));
            card.Trait.experience=900; card.Trait.rank=CardTraitRank.S;
            Assert.Throws<InvalidOperationException>(()=>OwnerTraitTrainingService.Preview(state,id,new[]{partner},balance));
            Assert.That(state.Economy.Money,Is.EqualTo(money));
        }

        [Test]
        public void 오프시즌보상은_저장복원과재진입에_중복되지않는다()
        {
            var state=Create(out var adapter); var balance=new OwnerTraitTrainingBalance();
            Assert.That(OwnerTraitTrainingService.GrantOffseasonReward(state,balance),Is.True);
            state=adapter.CreateSimulationCopy(state);
            Assert.That(OwnerTraitTrainingService.GrantOffseasonReward(state,balance),Is.False);
            Assert.That(state.PlayerGrowth.Traits.points,Is.EqualTo(balance.offseasonReward));
        }

        [TestCase(99,CardTraitRank.None)] [TestCase(100,CardTraitRank.C)] [TestCase(250,CardTraitRank.B)]
        [TestCase(500,CardTraitRank.A)] [TestCase(900,CardTraitRank.S)]
        public void 승급경계는_기획누적경험치를따른다(int experience,CardTraitRank expected) =>
            Assert.That(new OwnerTraitTrainingBalance().GetRank(experience),Is.EqualTo(expected));

        [Test]
        public void 경기특성은_실제상황에서만_발동한다()
        {
            var clutch=new CardTraitEffect(CardTraitKind.Clutch,5);
            Assert.That(CardTraitEffectResolver.Contact(clutch,false,true),Is.Zero);
            Assert.That(CardTraitEffectResolver.Contact(clutch,true,false),Is.EqualTo(5));
            var strikeout=new CardTraitEffect(CardTraitKind.Strikeout,4);
            Assert.That(CardTraitEffectResolver.Stuff(strikeout,1),Is.Zero);
            Assert.That(CardTraitEffectResolver.Stuff(strikeout,2),Is.EqualTo(4));
        }
    }
}

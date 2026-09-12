using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    public sealed class HistoricalCardEconomyTests
    {
        [Test]
        public void Study_야수투수각열두과정이동일예산으로서로다른성장방향을제공한다()
        {
            var balance = OwnerCardGrowthBalanceTable.CreateDefault();
            Assert.That(balance.StudyPrograms.Count, Is.EqualTo(24));
            foreach (PlayerType type in new[] { PlayerType.Batter, PlayerType.Pitcher })
            {
                var programs = balance.StudyPrograms.Where(program => program.PlayerType == type).ToArray();
                Assert.That(programs.Length, Is.EqualTo(12));
                Assert.That(programs.Select(program => program.DestinationName).Distinct().Count(), Is.EqualTo(12));
                foreach (var program in programs)
                {
                    Assert.That(program.DevelopmentPointCost, Is.EqualTo(100));
                    Assert.That(program.DurationWeeks, Is.EqualTo(4));
                    Assert.That(program.Rewards.Sum(reward => reward.Amount), Is.EqualTo(3));
                    Assert.That(balance.GetStudyProgram(program.ProgramId), Is.SameAs(program));
                }
            }
            Assert.That(balance.StudyPrograms.Select(program => program.ProgramId).Distinct().Count(), Is.EqualTo(24));
            Assert.Throws<ArgumentException>(() => new OwnerCardGrowthBalanceTable(balance.TrainingPrograms,
                new[] { balance.StudyPrograms[0], balance.StudyPrograms[0] }));
        }

        [Test]
        public void ContractArrears_PartialPaymentAndIncome_PreserveMoneyAndPreventBorrowedPurchases()
        {
            var economy = new ManagerEconomyState(40L);
            economy.SettleContractPayment(100L);
            Assert.That(economy.Money, Is.Zero);
            Assert.That(economy.ContractArrears, Is.EqualTo(60L));
            Assert.That(economy.TrySpendMoney(1L), Is.False);
            economy.AddMoney(25L);
            Assert.That(economy.ContractArrears, Is.EqualTo(35L));
            Assert.That(economy.Money, Is.Zero);
            economy.AddMoney(50L);
            Assert.That(economy.ContractArrears, Is.Zero);
            Assert.That(economy.Money, Is.EqualTo(15L));
            Assert.That(economy.TrySpendMoney(15L), Is.True);
        }

        [Test]
        public void ContractArrears_InvalidStateAndOverflow_DoNotPartiallyMutateEconomy()
        {
            Assert.Throws<ArgumentException>(() => new ManagerEconomyState(1L, contractArrears: 1L));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ManagerEconomyState(contractArrears: -1L));
            var economy = new ManagerEconomyState(contractArrears: long.MaxValue);
            Assert.Throws<OverflowException>(() => economy.SettleContractPayment(1L));
            Assert.That(economy.ContractArrears, Is.EqualTo(long.MaxValue));
            Assert.That(economy.Money, Is.Zero);
            economy.AddMoney(long.MaxValue);
            Assert.That(economy.ContractArrears, Is.Zero);
        }

        [Test]
        public void PlayerCardEdition_기본과특수카드여덟종을지원한다()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    PlayerCardEdition.Normal,
                    PlayerCardEdition.AllStar,
                    PlayerCardEdition.GoldenGlove,
                    PlayerCardEdition.Mvp,
                    PlayerCardEdition.Rare,
                    PlayerCardEdition.Ex,
                    PlayerCardEdition.CareerHigh,
                    PlayerCardEdition.Legend
                },
                Enum.GetValues(typeof(PlayerCardEdition)));
        }

        [Test]
        public void OwnedPlayerCardState_강화와_훈련은_공통_CardDefinition과_분리된다()
        {
            var definition = new PlayerCardDefinition(
                PlayerCardDefinition.CreateStableCardId("season-1", PlayerCardEdition.Normal),
                "season-1",
                PlayerCardEdition.Normal,
                new int[PlayerAbilityCatalog.AbilityCount]);
            var owned = new OwnedPlayerCardState(definition.CardId, duplicateCount: 2);

            owned.IncreaseEnhancement();
            owned.Training.AddBonus(PlayerAbility.Contact, 3);

            Assert.That(owned.EnhancementLevel, Is.EqualTo(1));
            Assert.That(owned.Training.GetBonus(PlayerAbility.Contact), Is.EqualTo(3));
            Assert.That(definition.GetModifier(PlayerAbility.Contact), Is.Zero);
        }

        [Test]
        public void CardCollectionHistoryState_같은CardId획득은한번만기록한다()
        {
            var history = new CardCollectionHistoryState();

            Assert.That(history.MarkAcquired(" CARD-A "), Is.True);
            Assert.That(history.MarkAcquired("CARD-A"), Is.False);

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history.WasEverAcquired("CARD-A"), Is.True);
            Assert.That(history.WasEverAcquired("CARD-B"), Is.False);
        }

        [Test]
        public void WishlistState_중복등록은순번을소비하지않고최근과오래된순서를결정론적으로반환한다()
        {
            var wishlist = new WishlistState();

            Assert.That(wishlist.Add("CARD-A"), Is.True);
            Assert.That(wishlist.Add("CARD-A"), Is.False);
            Assert.That(wishlist.Add("CARD-B"), Is.True);
            Assert.That(wishlist.Remove("CARD-A"), Is.True);
            Assert.That(wishlist.Add("CARD-C"), Is.True);

            Assert.That(wishlist.NextAddedSequence, Is.EqualTo(3));
            Assert.That(
                wishlist.GetOldestFirst().Select(entry => entry.CardId),
                Is.EqualTo(new[] { "CARD-B", "CARD-C" }));
            Assert.That(
                wishlist.GetMostRecentFirst().Select(entry => entry.CardId),
                Is.EqualTo(new[] { "CARD-C", "CARD-B" }));
            Assert.That(wishlist.GetMostRecentFirst()[0].AddedSequence, Is.EqualTo(2));
        }

        [Test]
        public void ScoutFeaturePolicy_Phase4는_Normal만_허용하고_AwardScout를_막는다()
        {
            ScoutFeaturePolicy policy = ScoutFeaturePolicy.Phase4NormalOnly;

            Assert.That(policy.IsEditionEnabled(PlayerCardEdition.Normal), Is.True);
            Assert.That(policy.IsEditionEnabled(PlayerCardEdition.AllStar), Is.False);
            Assert.That(policy.IsEditionEnabled(PlayerCardEdition.GoldenGlove), Is.False);
            Assert.That(policy.IsEditionEnabled(PlayerCardEdition.Mvp), Is.False);
            Assert.That(policy.IsAwardScoutEnabled, Is.False);
        }

        [Test]
        public void InitialTeamColorDefinition_GoldenGlove_기본은_동일연도_8명이다()
        {
            var definitions = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011);

            TeamColorDefinition reference = definitions.Single(value => value.TeamColorId == "GoldenGlove:2011:8");
            Assert.That(reference.RequiredCount, Is.EqualTo(8));
            Assert.That(reference.UpgradeGroupId, Is.EqualTo(InitialTeamColorDefinitionFactory.GoldenGloveUpgradeGroupId));
            Assert.That(reference.StackPolicy, Is.EqualTo(TeamColorStackPolicy.HighestOnly));
        }

        [Test]
        public void InitialTeamColorDefinition_YearFranchise와_Mvp는_Stackable이다()
        {
            var yearFranchise = InitialTeamColorDefinitionFactory.CreateYearFranchise(2011, "COMETS");
            var mvp = InitialTeamColorDefinitionFactory.CreateMvp();

            Assert.That(yearFranchise.All(value => value.StackPolicy == TeamColorStackPolicy.Stackable), Is.True);
            Assert.That(mvp.All(value => value.StackPolicy == TeamColorStackPolicy.Stackable), Is.True);
        }

        [Test]
        public void InitialTeamColorDefinition_CreateAll은_여섯_Family를_모두_포함한다()
        {
            IReadOnlyList<TeamColorDefinition> definitions =
                InitialTeamColorDefinitionFactory.CreateAll(2011, "COMETS");

            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(TeamColorFamily)),
                definitions.Select(value => value.Family).Distinct().ToArray());
        }

        [Test]
        public void InitialTeamColorDefinition_CreateAll의_TeamColorId는_중복되지_않는다()
        {
            IReadOnlyList<TeamColorDefinition> definitions =
                InitialTeamColorDefinitionFactory.CreateAll(2011, "COMETS");

            Assert.That(
                definitions.Select(value => value.TeamColorId).Distinct().Count(),
                Is.EqualTo(definitions.Count));
        }

        [Test]
        public void EffectiveRatingCap_초기값은_Soft150_Hard250이다()
        {
            EffectiveRatingCapTable table = EffectiveRatingCapTable.CreateInitial();

            Assert.That(table.SoftCap, Is.EqualTo(150));
            Assert.That(table.HardCap, Is.EqualTo(250));
            Assert.That(table.PostSoftCapSlope, Is.LessThan(1d));
        }
    }
}

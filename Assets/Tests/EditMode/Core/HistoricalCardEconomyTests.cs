using System;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    public sealed class HistoricalCardEconomyTests
    {
        [Test]
        public void PlayerCardEdition_정확히_네_종류만_존재한다()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    PlayerCardEdition.Normal,
                    PlayerCardEdition.AllStar,
                    PlayerCardEdition.GoldenGlove,
                    PlayerCardEdition.Mvp
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
        public void EffectiveRatingCap_초기값은_Soft120_Hard140이다()
        {
            EffectiveRatingCapTable table = EffectiveRatingCapTable.CreateInitial();

            Assert.That(table.SoftCap, Is.EqualTo(120));
            Assert.That(table.HardCap, Is.EqualTo(140));
            Assert.That(table.PostSoftCapSlope, Is.LessThan(1d));
        }
    }
}

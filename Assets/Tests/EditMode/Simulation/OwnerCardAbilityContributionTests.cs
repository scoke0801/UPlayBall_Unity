using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>카드 능력치 결과와 표시용 성장 출처 합계가 일치하는지 검증한다.</summary>
    public sealed class OwnerCardAbilityContributionTests
    {
        [Test]
        public void ResolveContribution_훈련과유학을분리하고영구능력치합계를보존한다()
        {
            var training = new CardTrainingState();
            training.AddBonus(PlayerAbility.Contact, 3);
            training.AddStudyBonus(PlayerAbility.Contact, 2);
            var owned = new OwnedPlayerCardState("PS-1:Normal", enhancementLevel: 2, training: training);
            var season = new PlayerSeasonDefinition(
                "PS-1",
                "PERSON-1",
                2025,
                "FRANCHISE-1",
                "TEAM-2025",
                PlayerPosition.Shortstop,
                PitcherRole.Starter,
                PlayerType.Batter,
                RegistrationType.Domestic,
                new AbilityRatings(50),
                5,
                new AbilityRatings(80));
            var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
            modifiers[(int)PlayerAbility.Contact] = 1;
            var card = new PlayerCardDefinition(
                "PS-1:Normal", "PS-1", PlayerCardEdition.Normal, modifiers);

            OwnerCardAbilityContribution result = new OwnerCardAbilityResolver(BalanceTable.CreateDefault().Growth)
                .ResolveContribution(season, card, owned, PlayerAbility.Contact);

            Assert.That(result.BaseCard, Is.EqualTo(51));
            Assert.That(result.Training, Is.EqualTo(3));
            Assert.That(result.Study, Is.EqualTo(2));
            Assert.That(result.SkillBlock, Is.Zero);
            Assert.That(result.Enhancement, Is.EqualTo(2));
            Assert.That(result.Total, Is.EqualTo(58));
        }
    }
}

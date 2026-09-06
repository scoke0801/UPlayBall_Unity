using System;
using System.Collections.Generic;
using System.Linq;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class TeamColorReferenceInspiredTests
    {
        [Test]
        public void 전체_팀컬러는_고유한_이름과_설명을_가진다()
        {
            IReadOnlyList<TeamColorDefinition> definitions =
                InitialTeamColorDefinitionFactory.CreateAll(2011, "COMETS");

            Assert.That(definitions.Select(value => value.DisplayName).Distinct().Count(), Is.EqualTo(definitions.Count));
            Assert.That(definitions.Select(value => value.Description).Distinct().Count(), Is.EqualTo(definitions.Count));
            Assert.That(definitions.Any(value => value.DisplayName == "스피드 스타즈"), Is.False);
            Assert.That(definitions.Any(value => value.DisplayName == "강속구 군단"), Is.False);
            Assert.That(definitions.Any(value => value.DisplayName == "베테랑의 힘"), Is.False);
        }

        [Test]
        public void 낮은_BaseStat_구간은_BaseStat만으로_발동하고_대상자에게만_보너스를_준다()
        {
            List<TeamColorRosterCard> roster = CreateRoster(index =>
                CreateCard(index, PlayerRole.Hitter, contact: index < 4 ? 45 : 55));
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory
                .CreateReferenceInspiredProfiles()
                .Single(value => value.TeamColorId == "HitterProfile:BatPath:Prospect");
            var resolver = new TeamColorResolver();

            IReadOnlyList<TeamColorCandidate> candidates = resolver.Resolve(roster, new[] { definition });
            PerCardBonusMap bonuses = resolver.ApplyEquipped(roster, new[] { definition }, definition, null);

            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].EligibleCardIds.Count, Is.EqualTo(4));
            Assert.That(bonuses.Get("card-0", PlayerAbility.Contact), Is.EqualTo(10));
            Assert.That(bonuses.Get("card-4", PlayerAbility.Contact), Is.Zero);
        }

        [Test]
        public void 세대_팀컬러는_원시즌_나이를_사용한다()
        {
            List<TeamColorRosterCard> roster = CreateRoster(index =>
                CreateCard(index, index < 14 ? PlayerRole.Hitter : PlayerRole.Pitcher,
                    age: index < 6 ? 25 : 30));
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory
                .CreateReferenceInspiredProfiles()
                .Single(value => value.TeamColorId == "Generation:YoungCore");

            PerCardBonusMap bonuses = new TeamColorResolver().ApplyEquipped(
                roster, new[] { definition }, definition, null);

            Assert.That(bonuses.Get("card-0", PlayerAbility.BatterMental), Is.EqualTo(4));
            Assert.That(bonuses.Get("card-6", PlayerAbility.BatterMental), Is.Zero);
        }

        [Test]
        public void 좌타_선발9명과_NaturalStarter5명은_각_구성_팀컬러를_발동한다()
        {
            List<TeamColorRosterCard> roster = CreateRoster(index =>
            {
                if (index < 9)
                    return CreateCard(index, PlayerRole.Hitter, bats: Handedness.Left,
                        activeRosterRole: (ActiveRosterRole)index);
                if (index < 14)
                    return CreateCard(index, PlayerRole.Hitter, activeRosterRole: ActiveRosterRole.BenchHitter);
                if (index < 19)
                    return CreateCard(index, PlayerRole.Pitcher, cost: 7,
                        naturalPitcherRole: PitcherRole.Starter,
                        activeRosterRole: (ActiveRosterRole)((int)ActiveRosterRole.StartingPitcher1 + index - 14));
                return CreateCard(index, PlayerRole.Pitcher,
                    naturalPitcherRole: PitcherRole.MiddleRelief,
                    activeRosterRole: (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + Math.Min(index - 19, 5)));
            });
            IReadOnlyList<TeamColorDefinition> definitions = InitialTeamColorDefinitionFactory.CreateReferenceInspiredProfiles();
            var resolver = new TeamColorResolver();

            IReadOnlyList<TeamColorCandidate> candidates = resolver.Resolve(roster, definitions);

            Assert.That(candidates.Any(value => value.Definition.TeamColorId == "RosterComposition:LeftStartingHitters"), Is.True);
            Assert.That(candidates.Any(value => value.Definition.TeamColorId == "PitcherProfile:StartingRotationRoleFit"), Is.True);
        }

        [Test]
        public void 로스터_기반_팩토리는_모든_Origin_조합을_안정된_순서로_생성한다()
        {
            List<TeamColorRosterCard> roster = CreateRoster(index =>
            {
                int year = index < 12 ? 2010 : 2011;
                string franchise = index < 12 ? "BEARS" : "COMETS";
                return CreateCard(index, index < 14 ? PlayerRole.Hitter : PlayerRole.Pitcher,
                    originYear: year, franchiseId: franchise);
            });

            IReadOnlyList<TeamColorDefinition> first = InitialTeamColorDefinitionFactory.CreateForRoster(roster);
            IReadOnlyList<TeamColorDefinition> second = InitialTeamColorDefinitionFactory.CreateForRoster(roster);

            Assert.That(first.Any(value => value.TeamColorId == "YearFranchise:2010:BEARS:10"), Is.True);
            Assert.That(first.Any(value => value.TeamColorId == "YearFranchise:2011:COMETS:10"), Is.True);
            Assert.That(first.Select(value => value.TeamColorId), Is.EqualTo(second.Select(value => value.TeamColorId)));
            Assert.That(first.Select(value => value.DisplayName).Distinct().Count(), Is.EqualTo(first.Count));
            Assert.That(first.Select(value => value.Description).Distinct().Count(), Is.EqualTo(first.Count));
        }

        private static List<TeamColorRosterCard> CreateRoster(Func<int, TeamColorRosterCard> factory)
        {
            var result = new List<TeamColorRosterCard>(25);
            for (int index = 0; index < 25; index++)
                result.Add(factory(index));
            return result;
        }

        private static TeamColorRosterCard CreateCard(
            int index,
            PlayerRole role,
            int contact = 55,
            int age = 30,
            int cost = 5,
            Handedness bats = Handedness.Right,
            PitcherRole? naturalPitcherRole = null,
            ActiveRosterRole? activeRosterRole = null,
            int originYear = 2011,
            string franchiseId = "COMETS")
        {
            int[] values = new AbilityRatings(55).ToArray();
            values[(int)PlayerAbility.Contact] = contact;
            return new TeamColorRosterCard(
                "card-" + index,
                new TeamColorEligibilityKey(originYear, franchiseId, originYear + ":" + franchiseId, PlayerCardEdition.Normal),
                role,
                cost,
                age,
                RegistrationType.Domestic,
                bats,
                Handedness.Right,
                naturalPitcherRole,
                activeRosterRole,
                new AbilityRatings(values));
        }
    }
}

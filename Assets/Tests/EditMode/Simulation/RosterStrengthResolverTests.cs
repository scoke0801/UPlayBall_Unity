using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>가격·비관련 능력과 전력을 분리하고 현재 로스터 구성에만 반응하는지 검증한다.</summary>
    public sealed class RosterStrengthResolverTests
    {
        [Test]
        public void Resolve_유형별능력만평균하며벤치는전력에포함하고비용에서제외한다()
        {
            CreateRoster(5, 40, out CurrentRosterState roster, out WorldCardCatalog catalog);
            RosterStrengthBreakdown strength = new RosterStrengthResolver().Resolve(roster, catalog);
            RosterCostBreakdown cost = new RosterCostResolver().Resolve(roster, catalog);

            Assert.That(strength.Overall, Is.EqualTo(200d / 3d).Within(1e-10));
            Assert.That(strength.HitterStrength, Is.EqualTo(55d));
            Assert.That(strength.PitcherStrength, Is.EqualTo(90d));
            Assert.That(strength.PlayerCount, Is.EqualTo(3));
            Assert.That(cost.TotalCost, Is.EqualTo(10));
        }

        [Test]
        public void Resolve_Cost변경과능력변경은서로독립적이다()
        {
            CreateRoster(1, 40, out CurrentRosterState roster, out WorldCardCatalog cheap);
            CreateRoster(10, 40, out _, out WorldCardCatalog expensive);
            CreateRoster(1, 70, out _, out WorldCardCatalog improvedBench);
            var strengthResolver = new RosterStrengthResolver();
            var costResolver = new RosterCostResolver();

            double? baseline = strengthResolver.Resolve(roster, cheap).Overall;
            Assert.That(strengthResolver.Resolve(roster, expensive).Overall, Is.EqualTo(baseline));
            Assert.That(costResolver.Resolve(roster, expensive).TotalCost, Is.EqualTo(20));
            Assert.That(costResolver.Resolve(roster, cheap).TotalCost, Is.EqualTo(2));
            Assert.That(strengthResolver.Resolve(roster, improvedBench).Overall, Is.EqualTo(baseline + 10d));
            Assert.That(costResolver.Resolve(roster, improvedBench).TotalCost, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_순서가달라도결정론적이며원본을변경하지않는다()
        {
            CreateRoster(5, 40, out CurrentRosterState roster, out WorldCardCatalog catalog);
            var reversed = new CurrentRosterState("TEAM_2025", new[] { roster.Entries[2], roster.Entries[1], roster.Entries[0] });
            var resolver = new RosterStrengthResolver();
            double? original = resolver.Resolve(roster, catalog).Overall;

            Assert.That(resolver.Resolve(reversed, catalog).Overall, Is.EqualTo(original));
            Assert.That(resolver.Resolve(roster, catalog).Overall, Is.EqualTo(original));
            Assert.That(roster.Entries[0].CardId, Is.EqualTo("SEASON_0:Normal"));
            Assert.That(catalog.GetPlayerSeason(catalog.Cards[0]).CreateBaseAttributes().Get(PlayerAbility.Contact), Is.EqualTo(70));
        }

        [Test]
        public void Resolve_빈로스터는미평가이며없는카드는조용히제외하지않는다()
        {
            CreateRoster(5, 40, out CurrentRosterState roster, out WorldCardCatalog catalog);
            var resolver = new RosterStrengthResolver();
            var empty = new CurrentRosterState("TEAM_2025", Array.Empty<ActiveRosterEntry>());
            Assert.That(resolver.Resolve(empty, catalog).Overall, Is.Null);
            Assert.That(resolver.Resolve(empty, catalog).HitterStrength, Is.Null);
            var emptyCatalog = new WorldCardCatalog(Array.Empty<PlayerSeasonDefinition>(), Array.Empty<PlayerCardDefinition>());
            Assert.Throws<ArgumentException>(() => resolver.Resolve(roster, emptyCatalog));
        }

        private static void CreateRoster(int cost, int benchRating, out CurrentRosterState roster, out WorldCardCatalog catalog)
        {
            var entries = new ActiveRosterEntry[3];
            var seasons = new PlayerSeasonDefinition[3];
            var cards = new PlayerCardDefinition[3];
            int[] primaryRatings = { 70, benchRating, 90 };
            ActiveRosterRole[] roles = { ActiveRosterRole.StartingCatcher, ActiveRosterRole.BenchHitter, ActiveRosterRole.StartingPitcher1 };
            for (int index = 0; index < 3; index++)
            {
                bool isHitter = index < 2;
                var attributes = new int[PlayerAbilityCatalog.AbilityCount];
                for (int abilityIndex = 0; abilityIndex < attributes.Length; abilityIndex++)
                {
                    bool isRelevant = isHitter
                        ? PlayerAbilityCatalog.IsBatterAbility((PlayerAbility)abilityIndex)
                        : PlayerAbilityCatalog.IsPitcherAbility((PlayerAbility)abilityIndex);
                    attributes[abilityIndex] = isRelevant ? primaryRatings[index] : 1;
                }
                var ratings = new AbilityRatings(attributes);
                string seasonId = "SEASON_" + index, personId = "PERSON_" + index;
                string cardId = PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal);
                seasons[index] = new PlayerSeasonDefinition(seasonId, personId, 2025, "TEAM", "TEAM_2025",
                    isHitter ? PlayerPosition.Catcher : PlayerPosition.StartingPitcher,
                    isHitter ? PitcherRole.MiddleRelief : PitcherRole.Starter,
                    isHitter ? PlayerType.Batter : PlayerType.Pitcher, RegistrationType.Domestic, ratings, cost, ratings);
                cards[index] = new PlayerCardDefinition(cardId, seasonId, PlayerCardEdition.Normal, new int[PlayerAbilityCatalog.AbilityCount]);
                entries[index] = new ActiveRosterEntry(cardId, seasonId, personId, RegistrationType.Domestic, roles[index]);
            }
            roster = new CurrentRosterState("TEAM_2025", entries);
            catalog = new WorldCardCatalog(seasons, cards);
        }
    }
}

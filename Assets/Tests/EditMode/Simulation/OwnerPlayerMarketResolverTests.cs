using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class OwnerPlayerMarketResolverTests
    {
        [Test]
        public void CreateInitialContracts_SameInput_ProducesSameTerms()
        {
            CreateWorld(out CurrentRosterState player, out _, out WorldCardCatalog catalog);
            var resolver = new OwnerPlayerMarketResolver(OwnerPlayerMarketBalanceTable.CreateInitial());

            OwnerPlayerContractState[] first = resolver.CreateInitialContracts(player, catalog, 1);
            OwnerPlayerContractState[] second = resolver.CreateInitialContracts(player, catalog, 1);

            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(second[index].CardId, Is.EqualTo(first[index].CardId));
                Assert.That(second[index].RemainingSeasons, Is.EqualTo(first[index].RemainingSeasons));
                Assert.That(first[index].RemainingSeasons, Is.InRange(2, 3));
                Assert.That(second[index].AnnualSalary, Is.EqualTo(first[index].AnnualSalary));
            }
        }



        [Test]
        public void CreateActiveRosterContracts_동일인물의다른카드로교체해도연장계약을보존한다()
        {
            CreateWorld(out CurrentRosterState roster, out _, out WorldCardCatalog catalog);
            var resolver = new OwnerPlayerMarketResolver(OwnerPlayerMarketBalanceTable.CreateInitial());
            var contracts = resolver.CreateInitialContracts(roster, catalog, 1);
            var seasons = new List<PlayerSeasonDefinition>();
            var cards = new List<PlayerCardDefinition>();
            foreach (var entry in roster.Entries)
            {
                var card = GetCard(catalog, entry.CardId);
                cards.Add(card);
                seasons.Add(catalog.GetPlayerSeason(card));
            }
            ActiveRosterEntry previous = roster.Entries[0];
            string replacementId = PlayerCardDefinition.CreateStableCardId(previous.PlayerSeasonId, PlayerCardEdition.Mvp);
            cards.Add(new PlayerCardDefinition(replacementId, previous.PlayerSeasonId,
                PlayerCardEdition.Mvp, new int[PlayerAbilityCatalog.AbilityCount]));
            var entries = new List<ActiveRosterEntry>(roster.Entries);
            entries[0] = new ActiveRosterEntry(replacementId, previous.PlayerSeasonId,
                previous.PlayerPersonId, RegistrationType.Domestic, previous.Role);
            OwnerPlayerContractState original = Array.Find(contracts, c => c.CardId == previous.CardId);
            original.Renew(1, 4, 123456L);
            var result = resolver.CreateActiveRosterContracts(new CurrentRosterState(roster.TeamSeasonKey, entries),
                new WorldCardCatalog(seasons, cards), 1, contracts);
            var inherited = Array.Find(result, c => c.CardId == replacementId);
            Assert.That(inherited.RemainingSeasons, Is.EqualTo(4));
            Assert.That(inherited.AnnualSalary, Is.EqualTo(123456L));
            Assert.That(original.CardId, Is.EqualTo(previous.CardId));
            var reverted = resolver.CreateActiveRosterContracts(roster, new WorldCardCatalog(seasons, cards), 1, result);
            Assert.That(Array.Find(reverted, c => c.CardId == previous.CardId).RemainingSeasons, Is.EqualTo(4));
        }

        private static void CreateWorld(
            out CurrentRosterState player,
            out CurrentRosterState partner,
            out WorldCardCatalog catalog)
        {
            ActiveRosterRole[] roles = CreateRoles();
            var playerEntries = new ActiveRosterEntry[roles.Length];
            var partnerEntries = new ActiveRosterEntry[roles.Length];
            var seasons = new List<PlayerSeasonDefinition>(roles.Length * 2);
            var cards = new List<PlayerCardDefinition>(roles.Length * 2);
            for (int index = 0; index < roles.Length; index++)
            {
                playerEntries[index] = CreateEntry("HOME", index, roles[index]);
                partnerEntries[index] = CreateEntry("AWAY", index, roles[index]);
                AddDefinition(playerEntries[index], "HOME_2011", index == 0 ? 6 : 5, seasons, cards);
                AddDefinition(partnerEntries[index], "AWAY_2011", index == 0 ? 6 : 5, seasons, cards);
            }
            player = new CurrentRosterState("HOME_2011", playerEntries);
            partner = new CurrentRosterState("AWAY_2011", partnerEntries);
            catalog = new WorldCardCatalog(seasons, cards);
        }

        private static ActiveRosterEntry CreateEntry(string prefix, int index, ActiveRosterRole role)
        {
            string seasonId = $"{prefix}_SEASON_{index:D2}";
            return new ActiveRosterEntry(
                seasonId + ":Normal",
                seasonId,
                $"{prefix}_PERSON_{index:D2}",
                RegistrationType.Domestic,
                role);
        }

        private static void AddDefinition(
            ActiveRosterEntry entry,
            string teamSeasonKey,
            int cost,
            ICollection<PlayerSeasonDefinition> seasons,
            ICollection<PlayerCardDefinition> cards)
        {
            bool pitcher = ActiveRosterCompositionRule.Standard.IsPitcherRole(entry.Role);
            var ratings = new AbilityRatings(50);
            seasons.Add(new PlayerSeasonDefinition(
                entry.PlayerSeasonId,
                entry.PlayerPersonId,
                2011,
                teamSeasonKey.Substring(0, 4),
                teamSeasonKey,
                pitcher ? PlayerPosition.StartingPitcher : PlayerPosition.Catcher,
                pitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                pitcher ? PlayerType.Pitcher : PlayerType.Batter,
                RegistrationType.Domestic,
                ratings,
                cost,
                ratings));
            cards.Add(new PlayerCardDefinition(
                entry.CardId,
                entry.PlayerSeasonId,
                PlayerCardEdition.Normal,
                new int[PlayerAbilityCatalog.AbilityCount]));
        }

        private static PlayerCardDefinition GetCard(WorldCardCatalog catalog, string cardId)
        {
            Assert.That(catalog.TryGetCard(cardId, out PlayerCardDefinition card), Is.True);
            return card;
        }

        private static ActiveRosterRole[] CreateRoles() => new[]
        {
            ActiveRosterRole.StartingCatcher,
            ActiveRosterRole.StartingFirstBase,
            ActiveRosterRole.StartingSecondBase,
            ActiveRosterRole.StartingThirdBase,
            ActiveRosterRole.StartingShortstop,
            ActiveRosterRole.StartingLeftField,
            ActiveRosterRole.StartingCenterField,
            ActiveRosterRole.StartingRightField,
            ActiveRosterRole.StartingDesignatedHitter,
            ActiveRosterRole.BenchHitter,
            ActiveRosterRole.BenchHitter,
            ActiveRosterRole.BenchHitter,
            ActiveRosterRole.BenchHitter,
            ActiveRosterRole.BenchHitter,
            ActiveRosterRole.StartingPitcher1,
            ActiveRosterRole.StartingPitcher2,
            ActiveRosterRole.StartingPitcher3,
            ActiveRosterRole.StartingPitcher4,
            ActiveRosterRole.StartingPitcher5,
            ActiveRosterRole.Bullpen1,
            ActiveRosterRole.Bullpen2,
            ActiveRosterRole.Bullpen3,
            ActiveRosterRole.Bullpen4,
            ActiveRosterRole.Setup,
            ActiveRosterRole.Closer
        };
    }
}

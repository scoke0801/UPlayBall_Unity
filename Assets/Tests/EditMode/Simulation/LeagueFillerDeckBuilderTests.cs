using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>CPU 임시 구단 덱이 등급 카드를 우선 쓰고 빈 포지션만 대체하는지 검증한다.</summary>
    public sealed class LeagueFillerDeckBuilderTests
    {
        [Test]
        public void Build_덱등급카드가없는포지션만다른카드로채우고선발야수는원래포지션을지킨다()
        {
            // A 구단 선수에게만 MVP 카드를 주되 포수와 유격수는 뺀다(벤치 야수도 포수라 함께 뺀다).
            // 두 포지션만 Normal로 대체되어야 한다.
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new[]
            {
                ActiveRosterRole.StartingCatcher, ActiveRosterRole.StartingShortstop, ActiveRosterRole.BenchHitter
            });
            var builder = new LeagueFillerDeckBuilder(catalog);
            string key = LeagueFillerTeamKey.Create(2, LeagueGrade.Champion, 0, 0, LeagueFillerDeckType.Mvp);

            CurrentRosterState roster = builder.Build(key, new Pcg32Random(11UL));

            Assert.That(roster.TeamSeasonKey, Is.EqualTo(key));
            Assert.That(roster.Entries.Count, Is.EqualTo(ActiveRosterCompositionRule.ActiveRosterSize));
            foreach (ActiveRosterEntry entry in roster.Entries)
            {
                PlayerCardDefinition card = catalog.GetRequiredCard(entry.CardId);
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (ActiveRosterCompositionRule.Standard.IsStartingHitterRole(entry.Role) &&
                    entry.Role != ActiveRosterRole.StartingDesignatedHitter)
                    Assert.That(season.Position,
                        Is.EqualTo(ActiveRosterCompositionRule.Standard.GetAssignedPosition(entry.Role)), entry.Role.ToString());
                bool isMissingMvp = entry.Role == ActiveRosterRole.StartingCatcher || entry.Role == ActiveRosterRole.StartingShortstop;
                if (entry.Role != ActiveRosterRole.BenchHitter)
                    Assert.That(card.Edition, Is.EqualTo(isMissingMvp ? PlayerCardEdition.Normal : PlayerCardEdition.Mvp),
                        entry.Role.ToString());
            }
        }

        [Test]
        public void Build_연도구단덱은원본구단의Normal카드로만구성한다()
        {
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new ActiveRosterRole[0]);
            var builder = new LeagueFillerDeckBuilder(catalog);
            string key = LeagueFillerTeamKey.Create(2, LeagueGrade.Rookie, 0, 0, LeagueFillerDeckType.YearTeam, "T-B");

            CurrentRosterState roster = builder.Build(key, new Pcg32Random(3UL));

            Assert.That(builder.YearTeamSeasonKeys, Is.EqualTo(new[] { "T-A", "T-B" }));
            foreach (ActiveRosterEntry entry in roster.Entries)
            {
                PlayerCardDefinition card = catalog.GetRequiredCard(entry.CardId);
                Assert.That(card.Edition, Is.EqualTo(PlayerCardEdition.Normal));
                Assert.That(catalog.GetPlayerSeason(card).OriginTeamSeasonKey, Is.EqualTo("T-B"));
            }
        }

        [Test]
        public void Build_같은Key와Seed면같은로스터를만든다()
        {
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new ActiveRosterRole[0]);
            string key = LeagueFillerTeamKey.Create(5, LeagueGrade.Champion, 1, 2, LeagueFillerDeckType.Mvp);

            CurrentRosterState first = new LeagueFillerDeckBuilder(catalog).Build(key, new Pcg32Random(99UL));
            CurrentRosterState second = new LeagueFillerDeckBuilder(catalog).Build(key, new Pcg32Random(99UL));

            Assert.That(CardIds(second), Is.EqualTo(CardIds(first)));
        }

        private static List<string> CardIds(CurrentRosterState roster)
        {
            var result = new List<string>();
            foreach (ActiveRosterEntry entry in roster.Entries) result.Add(entry.CardId);
            return result;
        }

        private static WorldCardCatalog CreateCatalog(string mvpTeam, ActiveRosterRole[] excludedMvpRoles)
        {
            var persons = new List<PlayerPersonDefinition>();
            var seasons = new List<PlayerSeasonDefinition>();
            var cards = new List<PlayerCardDefinition>();
            var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
            foreach (string team in new[] { "T-A", "T-B" })
            {
                for (int rosterIndex = 0; rosterIndex < ActiveRosterCompositionRule.ActiveRosterSize; rosterIndex++)
                {
                    ActiveRosterRole role = GetRole(rosterIndex);
                    PlayerPosition position = GetPosition(role);
                    bool isPitcher = ActiveRosterCompositionRule.Standard.IsPitcherRole(role);
                    string personId = $"{team}-P{rosterIndex:00}";
                    string seasonId = $"{team}-S{rosterIndex:00}";
                    persons.Add(new PlayerPersonDefinition(personId, 1990, Handedness.Right, Handedness.Right, position,
                        RegistrationType.Domestic, 2010, 2025, new PersonPotentialTrait(new int[PlayerAbilityCatalog.AbilityCount])));
                    seasons.Add(new PlayerSeasonDefinition(seasonId, personId, 2012, "F-" + team, team, position,
                        isPitcher ? ActiveRosterCompositionRule.Standard.GetAssignedPitcherRole(role) : PitcherRole.Starter,
                        isPitcher ? PlayerType.Pitcher : PlayerType.Batter, RegistrationType.Domestic,
                        new AbilityRatings(50), 5 + rosterIndex % 3, new AbilityRatings(60)));
                    cards.Add(new PlayerCardDefinition(
                        PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal), seasonId,
                        PlayerCardEdition.Normal, modifiers));
                    if (team == mvpTeam && System.Array.IndexOf(excludedMvpRoles, role) < 0)
                        cards.Add(new PlayerCardDefinition(
                            PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Mvp), seasonId,
                            PlayerCardEdition.Mvp, modifiers));
                }
            }
            return new WorldCardCatalog(seasons, cards, persons);
        }

        private static ActiveRosterRole GetRole(int rosterIndex)
        {
            if (rosterIndex < 9) return (ActiveRosterRole)rosterIndex;
            if (rosterIndex < 14) return ActiveRosterRole.BenchHitter;
            return (ActiveRosterRole)(rosterIndex - 4);
        }

        private static PlayerPosition GetPosition(ActiveRosterRole role)
        {
            if (ActiveRosterCompositionRule.Standard.IsStartingHitterRole(role))
                return ActiveRosterCompositionRule.Standard.GetAssignedPosition(role);
            if (role == ActiveRosterRole.BenchHitter) return PlayerPosition.Catcher;
            return ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(role)
                ? PlayerPosition.StartingPitcher
                : PlayerPosition.ReliefPitcher;
        }
    }
}

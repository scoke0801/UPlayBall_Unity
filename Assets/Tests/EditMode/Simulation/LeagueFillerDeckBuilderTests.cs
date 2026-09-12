using System;
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

            CurrentRosterState roster = builder.Build(key, 0d, 0d, new Pcg32Random(11UL));

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
                // 지명타자는 포지션·Edition이 아니라 남은 타자 중 가장 좋은 타자로 정해지므로 Edition을 단정하지 않는다.
                if (entry.Role != ActiveRosterRole.BenchHitter && entry.Role != ActiveRosterRole.StartingDesignatedHitter)
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

            CurrentRosterState roster = builder.Build(key, 0d, 0d, new Pcg32Random(3UL));

            Assert.That(builder.YearTeamSeasonKeys, Is.EqualTo(new[] { "T-A", "T-B" }));
            foreach (ActiveRosterEntry entry in roster.Entries)
            {
                PlayerCardDefinition card = catalog.GetRequiredCard(entry.CardId);
                Assert.That(card.Edition, Is.EqualTo(PlayerCardEdition.Normal));
                Assert.That(catalog.GetPlayerSeason(card).OriginTeamSeasonKey, Is.EqualTo("T-B"));
            }
        }

        [Test]
        public void Build_목표Cost가있으면바탕구단을스타로교체하되평균Cost상한을넘지않는다()
        {
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new ActiveRosterRole[0]);
            var builder = new LeagueFillerDeckBuilder(catalog);
            const double Target = 6.2d;

            for (ulong seed = 1; seed <= 20; seed++)
            {
                string key = LeagueFillerTeamKey.Create((int)seed, LeagueGrade.Champion, 0, 0, LeagueFillerDeckType.Mvp);
                CurrentRosterState roster = builder.Build(key, Target, 0d, new Pcg32Random(seed));
                int totalCost = 0;
                int mvpCount = 0;
                foreach (ActiveRosterEntry entry in roster.Entries)
                {
                    PlayerCardDefinition card = catalog.GetRequiredCard(entry.CardId);
                    totalCost += catalog.GetPlayerSeason(card).Cost;
                    if (card.Edition == PlayerCardEdition.Mvp) mvpCount++;
                }
                Assert.That(totalCost / (double)roster.Entries.Count, Is.LessThanOrEqualTo(Target), $"seed {seed}");
                Assert.That(mvpCount, Is.GreaterThan(0), $"seed {seed}: 여유 예산이 있으면 스타가 들어가야 한다.");
                Assert.That(mvpCount, Is.LessThan(roster.Entries.Count), $"seed {seed}: 상한 때문에 전원 스타가 될 수 없다.");
            }
        }

        [Test]
        public void Build_스타를벤치나불펜에두지않고중요한자리부터배치한다()
        {
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new ActiveRosterRole[0]);
            var builder = new LeagueFillerDeckBuilder(catalog);

            for (ulong seed = 1; seed <= 10; seed++)
            {
                string key = LeagueFillerTeamKey.Create((int)seed, LeagueGrade.Champion, 0, 0, LeagueFillerDeckType.Mvp);
                CurrentRosterState roster = builder.Build(key, 6.4d, 0d, new Pcg32Random(seed));

                int designatedHitterCost = 0;
                int benchMaximumCost = 0;
                int rotationMinimumCost = int.MaxValue;
                int relievedStarterMaximumCost = 0;
                int rotationRelieverCount = 0;
                int starterOutsideRotationCount = 0;
                foreach (ActiveRosterEntry entry in roster.Entries)
                {
                    PlayerSeasonDefinition season = catalog.GetPlayerSeason(catalog.GetRequiredCard(entry.CardId));
                    bool isStarterPitcher = season.PlayerType == PlayerType.Pitcher && season.PitcherRole == PitcherRole.Starter;
                    if (entry.Role == ActiveRosterRole.StartingDesignatedHitter) designatedHitterCost = season.Cost;
                    else if (entry.Role == ActiveRosterRole.BenchHitter) benchMaximumCost = Math.Max(benchMaximumCost, season.Cost);
                    else if (ActiveRosterCompositionRule.Standard.IsStartingPitcherRole(entry.Role))
                    {
                        rotationMinimumCost = Math.Min(rotationMinimumCost, season.Cost);
                        if (!isStarterPitcher) rotationRelieverCount++;
                    }
                    else if (isStarterPitcher)
                    {
                        starterOutsideRotationCount++;
                        relievedStarterMaximumCost = Math.Max(relievedStarterMaximumCost, season.Cost);
                    }
                }
                Assert.That(Math.Min(rotationRelieverCount, starterOutsideRotationCount), Is.Zero,
                    $"seed {seed}: 선발 자원을 불펜에 두고 불펜 자원을 로테이션에 세운 배치가 남아 있다.");

                Assert.That(designatedHitterCost, Is.GreaterThanOrEqualTo(benchMaximumCost),
                    $"seed {seed}: 벤치에 지명타자보다 좋은 타자가 남으면 안 된다.");
                Assert.That(rotationMinimumCost, Is.GreaterThanOrEqualTo(relievedStarterMaximumCost),
                    $"seed {seed}: 선발 투수가 불펜에 밀려 있으면 안 된다.");
            }
        }

        [Test]
        public void Build_같은Key와Seed면같은로스터를만든다()
        {
            WorldCardCatalog catalog = CreateCatalog(mvpTeam: "T-A", excludedMvpRoles: new ActiveRosterRole[0]);
            string key = LeagueFillerTeamKey.Create(5, LeagueGrade.Champion, 1, 2, LeagueFillerDeckType.Mvp);

            CurrentRosterState first = new LeagueFillerDeckBuilder(catalog).Build(key, 0d, 0d, new Pcg32Random(99UL));
            CurrentRosterState second = new LeagueFillerDeckBuilder(catalog).Build(key, 0d, 0d, new Pcg32Random(99UL));

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
                        // MVP 구단을 더 비싸게 두어 목표 Cost 덱의 바탕 구단이 항상 다른 구단이 되게 한다.
                        new AbilityRatings(50), 5 + rosterIndex % 3 + (team == mvpTeam ? 2 : 0), new AbilityRatings(60)));
                    cards.Add(new PlayerCardDefinition(
                        PlayerCardDefinition.CreateStableCardId(seasonId, PlayerCardEdition.Normal), seasonId,
                        PlayerCardEdition.Normal, modifiers));
                    if (team == mvpTeam && Array.IndexOf(excludedMvpRoles, role) < 0)
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

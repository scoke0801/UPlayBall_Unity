using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>World별 표시 Identity의 결정론·유일성·Canonical ID 경계를 검증한다.</summary>
    public sealed class WorldIdentityGeneratorTests
    {
        [Test]
        public void Generate_같은Seed는같고다른Seed는표시Identity가달라질수있다()
        {
            PlayerPersonDefinition[] persons = CreatePersons();
            TeamSeasonDefinition[] teams = CreateTeams();
            WorldIdentityNameCatalog names = CreateNames();
            var generator = new WorldIdentityGenerator();

            WorldIdentityRegistry first = generator.Generate(persons, teams, names, 71UL);
            WorldIdentityRegistry replay = generator.Generate(persons, teams, names, 71UL);
            WorldIdentityRegistry other = generator.Generate(persons, teams, names, 72UL);

            for (int index = 0; index < persons.Length; index++)
            {
                string playerPersonId = persons[index].PlayerPersonId;
                Assert.That(
                    replay.GetPlayerDisplayName(playerPersonId),
                    Is.EqualTo(first.GetPlayerDisplayName(playerPersonId)));
            }
            Assert.That(
                HasDifferentPlayerName(first, other, persons),
                Is.True,
                "다른 World Seed가 언제나 같은 이름 순열을 만들면 안 됩니다.");
        }

        [Test]
        public void Generate_Person과Franchise별로하나의고유이름만부여한다()
        {
            PlayerPersonDefinition[] persons = CreatePersons();
            TeamSeasonDefinition[] teams = CreateTeams();
            WorldIdentityRegistry registry = new WorldIdentityGenerator().Generate(
                persons,
                teams,
                CreateNames(),
                991UL);

            var playerNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < persons.Length; index++)
                Assert.That(playerNames.Add(registry.GetPlayerDisplayName(persons[index].PlayerPersonId)), Is.True);

            Assert.That(registry.FranchiseIdentities.Count, Is.EqualTo(2));
            Assert.That(registry.GetFranchiseDisplayName("FRANCHISE-A"), Is.Not.Empty);
            Assert.That(
                registry.GetFranchiseDisplayName("FRANCHISE-A"),
                Is.EqualTo(registry.FranchiseIdentities[0].FranchiseId == "FRANCHISE-A"
                    ? registry.FranchiseIdentities[0].DisplayName
                    : registry.FranchiseIdentities[1].DisplayName));
            Assert.That(
                registry.GetFranchiseDisplayName("FRANCHISE-A"),
                Is.Not.EqualTo(registry.GetFranchiseDisplayName("FRANCHISE-B")));
        }

        [Test]
        public void Generate_RegistrationType에맞는이름Pool을사용한다()
        {
            PlayerPersonDefinition[] persons = CreatePersons();
            WorldIdentityRegistry registry = new WorldIdentityGenerator().Generate(
                persons,
                CreateTeams(),
                CreateNames(),
                17UL);

            Assert.That(registry.GetPlayerDisplayName("PERSON-FOREIGN"), Does.Contain(" "));
            Assert.That(registry.GetPlayerDisplayName("PERSON-DOMESTIC-A"), Does.Not.Contain(" "));
        }

        [Test]
        public void Generate_Seed가달라도실제연고지는유지하고별칭만달라진다()
        {
            PlayerPersonDefinition[] persons = CreatePersons();
            TeamSeasonDefinition[] teams = CreateTeams();
            var names = new WorldIdentityNameCatalog(
                new[] { "김도윤", "박현우", "이준혁", "최성민", "정재호" },
                new[] { "Liam Carter", "Noah Bennett", "Ethan Foster" },
                new[] { "서울 코멧츠", "부산 타이즈", "인천 하버스", "대구 포지", "광주 피닉스" },
                CreateFranchiseRegions());
            var generator = new WorldIdentityGenerator();

            WorldIdentityRegistry first = generator.Generate(persons, teams, names, 1UL);
            bool foundDifferentSelection = false;
            for (ulong seed = 2UL; seed <= 100UL; seed++)
            {
                WorldIdentityRegistry other = generator.Generate(persons, teams, names, seed);
                Assert.That(other.GetFranchiseDisplayName("FRANCHISE-A"), Does.StartWith("서울 "));
                Assert.That(other.GetFranchiseDisplayName("FRANCHISE-B"), Does.StartWith("서울 "));
                if (!string.Equals(
                        first.GetFranchiseDisplayName("FRANCHISE-A"),
                        other.GetFranchiseDisplayName("FRANCHISE-A"),
                        StringComparison.Ordinal))
                {
                    foundDifferentSelection = true;
                    break;
                }
            }

            Assert.That(foundDifferentSelection, Is.True);
        }

        [Test]
        public void Generate_연고지치환으로중복되는후보를건너뛰고입력순서와무관하게재현한다()
        {
            var names = new WorldIdentityNameCatalog(
                CreateNames().DomesticPlayerNames, CreateNames().ForeignPlayerNames,
                new[] { "부산 코멧츠", "대전 코멧츠", "인천 하버스" }, CreateFranchiseRegions());
            PlayerPersonDefinition[] persons = CreatePersons();
            TeamSeasonDefinition[] teams = CreateTeams();
            TeamSeasonDefinition[] reversedTeams = CreateTeams();
            Array.Reverse(reversedTeams);
            var generator = new WorldIdentityGenerator();
            for (ulong seed = 0; seed < 1000; seed++)
            {
                WorldIdentityRegistry first = generator.Generate(persons, teams, names, seed);
                WorldIdentityRegistry replay = generator.Generate(persons, reversedTeams, names, seed);
                var actual = new HashSet<string>(StringComparer.Ordinal);
                foreach (WorldFranchiseIdentity identity in first.FranchiseIdentities)
                {
                    Assert.That(actual.Add(identity.DisplayName), Is.True, $"seed={seed}");
                    Assert.That(replay.GetFranchiseDisplayName(identity.FranchiseId), Is.EqualTo(identity.DisplayName));
                }
                Assert.That(actual, Is.EquivalentTo(new[] { "서울 코멧츠", "서울 하버스" }));
            }
        }

        [Test]
        public void Generate_연고지별고유별칭이부족하면구단과연고지를보고한다()
        {
            var names = new WorldIdentityNameCatalog(
                CreateNames().DomesticPlayerNames, CreateNames().ForeignPlayerNames,
                new[] { "부산 코멧츠", "대전 코멧츠" }, CreateFranchiseRegions());
            var error = Assert.Throws<InvalidOperationException>(() =>
                new WorldIdentityGenerator().Generate(CreatePersons(), CreateTeams(), names, 71UL));
            Assert.That(error.Message, Does.Contain("FRANCHISE-B"));
            Assert.That(error.Message, Does.Contain("서울"));
        }

        [Test]
        public void Generate_다른연고지에서는같은별칭을사용할수있다()
        {
            var names = new WorldIdentityNameCatalog(
                CreateNames().DomesticPlayerNames, CreateNames().ForeignPlayerNames,
                new[] { "부산 코멧츠", "대전 코멧츠" },
                new[]
                {
                    new WorldFranchiseRegionDefinition("FRANCHISE-A", "서울"),
                    new WorldFranchiseRegionDefinition("FRANCHISE-B", "인천")
                });
            WorldIdentityRegistry registry = new WorldIdentityGenerator().Generate(
                CreatePersons(), CreateTeams(), names, 71UL);
            Assert.That(registry.GetFranchiseDisplayName("FRANCHISE-A"), Is.EqualTo("서울 코멧츠"));
            Assert.That(registry.GetFranchiseDisplayName("FRANCHISE-B"), Is.EqualTo("인천 코멧츠"));
        }

        [TestCase("")]
        [TestCase("선수123")]
        [TestCase("선수\u0001")]
        [TestCase("가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마바사")]
        public void NameCatalog_비정상적인이름을거부한다(string invalidName)
        {
            Assert.Throws<ArgumentException>(() => new WorldIdentityNameCatalog(
                new[] { invalidName },
                Array.Empty<string>(),
                new[] { "서울 코멧츠" }));
        }

        private static bool HasDifferentPlayerName(
            WorldIdentityRegistry first,
            WorldIdentityRegistry second,
            IReadOnlyList<PlayerPersonDefinition> persons)
        {
            for (int index = 0; index < persons.Count; index++)
            {
                string id = persons[index].PlayerPersonId;
                if (!string.Equals(
                        first.GetPlayerDisplayName(id),
                        second.GetPlayerDisplayName(id),
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static PlayerPersonDefinition[] CreatePersons()
        {
            return new[]
            {
                CreatePerson("PERSON-DOMESTIC-A", RegistrationType.Domestic),
                CreatePerson("PERSON-DOMESTIC-B", RegistrationType.Domestic),
                CreatePerson("PERSON-FOREIGN", RegistrationType.Foreign)
            };
        }

        private static PlayerPersonDefinition CreatePerson(string id, RegistrationType registrationType)
        {
            return new PlayerPersonDefinition(
                id,
                1995,
                Handedness.Right,
                Handedness.Right,
                PlayerPosition.Shortstop,
                registrationType,
                2018,
                2035,
                new PersonPotentialTrait(new int[12]));
        }

        private static TeamSeasonDefinition[] CreateTeams()
        {
            return new[]
            {
                CreateTeam("TEAM-A-2023", "FRANCHISE-A", 2023),
                CreateTeam("TEAM-A-2024", "FRANCHISE-A", 2024),
                CreateTeam("TEAM-B-2024", "FRANCHISE-B", 2024)
            };
        }

        private static TeamSeasonDefinition CreateTeam(string key, string franchiseId, int year)
        {
            var cards = new string[25];
            for (int index = 0; index < cards.Length; index++)
                cards[index] = key + "-CARD-" + index;
            return new TeamSeasonDefinition(key, franchiseId, year, cards, cards, 50d);
        }

        private static WorldIdentityNameCatalog CreateNames()
        {
            return new WorldIdentityNameCatalog(
                new[] { "김도윤", "박현우", "이준혁", "최성민", "정재호" },
                new[] { "Liam Carter", "Noah Bennett", "Ethan Foster" },
                new[] { "서울 코멧츠", "부산 타이즈", "인천 하버스" },
                CreateFranchiseRegions());
        }

        private static WorldFranchiseRegionDefinition[] CreateFranchiseRegions()
        {
            return new[]
            {
                new WorldFranchiseRegionDefinition("FRANCHISE-A", "서울"),
                new WorldFranchiseRegionDefinition("FRANCHISE-B", "서울")
            };
        }
    }
}

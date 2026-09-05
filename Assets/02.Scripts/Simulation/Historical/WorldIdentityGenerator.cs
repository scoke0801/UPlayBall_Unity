using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>Canonical ID에 World Seed별 고유 표시 이름을 결정론적으로 배정한다.</summary>
    public sealed class WorldIdentityGenerator
    {
        public const string CurrentVersion = "world-identity-v1";

        private const ulong DomesticPlayerStream = 0x504C415945524B52UL;
        private const ulong ForeignPlayerStream = 0x504C41594552464FUL;
        private const ulong FranchiseStream = 0x4652414E43484953UL;

        public WorldIdentityRegistry Generate(
            IReadOnlyList<PlayerPersonDefinition> persons,
            IReadOnlyList<TeamSeasonDefinition> teamSeasons,
            WorldIdentityNameCatalog names,
            ulong worldSeed)
        {
            if (persons == null || persons.Count == 0)
                throw new ArgumentException("World Identity를 만들 Canonical Person이 필요합니다.", nameof(persons));
            if (teamSeasons == null || teamSeasons.Count == 0)
                throw new ArgumentException("World Identity를 만들 Canonical TeamSeason이 필요합니다.", nameof(teamSeasons));
            if (names == null)
                throw new ArgumentNullException(nameof(names));

            PlayerPersonDefinition[] orderedPersons = CopyAndSortPersons(persons);
            int domesticCount = 0;
            for (int index = 0; index < orderedPersons.Length; index++)
                if (orderedPersons[index].RegistrationType == RegistrationType.Domestic) domesticCount++;
            int foreignCount = orderedPersons.Length - domesticCount;
            if (names.DomesticPlayerNames.Count < domesticCount)
                throw new InvalidOperationException("검증된 국내 선수 이름 후보가 Canonical Person 수보다 적습니다.");
            if (names.ForeignPlayerNames.Count < foreignCount)
                throw new InvalidOperationException("검증된 외국인 선수 이름 후보가 Canonical Person 수보다 적습니다.");

            string[] domesticNames = Shuffle(names.DomesticPlayerNames, worldSeed, DomesticPlayerStream);
            string[] foreignNames = Shuffle(names.ForeignPlayerNames, worldSeed, ForeignPlayerStream);
            var playerIdentities = new WorldPlayerIdentity[orderedPersons.Length];
            int domesticIndex = 0;
            int foreignIndex = 0;
            for (int index = 0; index < orderedPersons.Length; index++)
            {
                PlayerPersonDefinition person = orderedPersons[index];
                string displayName = person.RegistrationType == RegistrationType.Foreign
                    ? foreignNames[foreignIndex++]
                    : domesticNames[domesticIndex++];
                playerIdentities[index] = new WorldPlayerIdentity(person.PlayerPersonId, displayName);
            }

            string[] franchiseIds = GetSortedFranchiseIds(teamSeasons);
            if (names.FranchiseNames.Count < franchiseIds.Length)
                throw new InvalidOperationException("검증된 구단 이름 후보가 Canonical Franchise 수보다 적습니다.");
            string[] franchiseNames = Shuffle(names.FranchiseNames, worldSeed, FranchiseStream);
            var franchiseIdentities = new WorldFranchiseIdentity[franchiseIds.Length];
            for (int index = 0; index < franchiseIds.Length; index++)
                franchiseIdentities[index] = new WorldFranchiseIdentity(franchiseIds[index], franchiseNames[index]);

            return new WorldIdentityRegistry(
                CurrentVersion,
                worldSeed,
                playerIdentities,
                franchiseIdentities);
        }

        private static PlayerPersonDefinition[] CopyAndSortPersons(
            IReadOnlyList<PlayerPersonDefinition> source)
        {
            var result = new PlayerPersonDefinition[source.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < result.Length; index++)
            {
                PlayerPersonDefinition person = source[index]
                    ?? throw new ArgumentException("null Canonical Person이 있습니다.", nameof(source));
                if (!ids.Add(person.PlayerPersonId))
                    throw new ArgumentException("PlayerPersonId는 중복될 수 없습니다.", nameof(source));
                result[index] = person;
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.PlayerPersonId, right.PlayerPersonId));
            return result;
        }

        private static string[] GetSortedFranchiseIds(IReadOnlyList<TeamSeasonDefinition> teamSeasons)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < teamSeasons.Count; index++)
            {
                TeamSeasonDefinition team = teamSeasons[index]
                    ?? throw new ArgumentException("null Canonical TeamSeason이 있습니다.", nameof(teamSeasons));
                unique.Add(team.FranchiseId);
            }
            var result = new string[unique.Count];
            unique.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static string[] Shuffle(
            IReadOnlyList<string> source,
            ulong worldSeed,
            ulong stream)
        {
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            var random = new Pcg32Random(DeterministicSeed.Derive(worldSeed, stream));
            for (int index = result.Length - 1; index > 0; index--)
            {
                int selected = (int)(random.NextDouble() * (index + 1));
                string value = result[index];
                result[index] = result[selected];
                result[selected] = value;
            }
            return result;
        }
    }
}

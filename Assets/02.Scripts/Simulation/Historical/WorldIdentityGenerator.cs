using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>Canonical ID에 실제 연고지와 World Seed별 고유 선수명·구단 별칭을 결정론적으로 배정한다.</summary>
    public sealed class WorldIdentityGenerator
    {
        public const string CurrentVersion = "world-identity-v3";

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
            var usedFranchiseNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < franchiseIds.Length; index++)
            {
                string franchiseId = franchiseIds[index];
                bool hasRegion = names.TryGetFranchiseRegion(franchiseId, out string region);
                if (names.HasFranchiseRegions && !hasRegion)
                    throw new InvalidOperationException($"Canonical Franchise의 실제 연고지 매핑이 없습니다: {franchiseId}");
                string displayName = SelectFranchiseName(
                    franchiseNames, index, hasRegion ? region : null, franchiseId, usedFranchiseNames);
                franchiseIdentities[index] = new WorldFranchiseIdentity(franchiseId, displayName);
            }

            return new WorldIdentityRegistry(
                CurrentVersion,
                worldSeed,
                playerIdentities,
                franchiseIdentities);
        }

        private static string SelectFranchiseName(
            string[] candidates, int startIndex, string region, string franchiseId,
            HashSet<string> usedNames)
        {
            // 연고지 치환으로 서로 다른 후보도 같은 이름이 된다. 원래 순열 위치부터
            // 한 바퀴 탐색하여 충돌 없는 배정을 유지하고 후보 고갈 시 무한 재시도를 막는다.
            // 다른 연고지에서는 같은 별칭을 사용할 수 있으므로 후보 자체는 소모하지 않는다.
            for (int offset = 0; offset < candidates.Length; offset++)
            {
                string candidate = candidates[(startIndex + offset) % candidates.Length];
                string displayName = region == null ? candidate : ComposeFranchiseName(region, candidate);
                if (usedNames.Add(displayName))
                    return displayName;
            }
            throw new InvalidOperationException(
                $"연고지 적용 후 중복되지 않는 구단 이름 후보가 부족합니다: FranchiseId={franchiseId}, 연고지={region}");
        }

        private static string ComposeFranchiseName(string region, string nameCandidate)
        {
            int separatorIndex = nameCandidate.IndexOf(' ');
            if (separatorIndex < 0 || separatorIndex == nameCandidate.Length - 1)
                throw new InvalidOperationException($"구단 이름 후보에서 별칭을 찾을 수 없습니다: {nameCandidate}");
            return region + nameCandidate.Substring(separatorIndex);
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

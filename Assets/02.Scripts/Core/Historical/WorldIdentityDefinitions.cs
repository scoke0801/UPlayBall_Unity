using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>한 World에서 PlayerPersonId에 확정된 표시 이름을 연결한다.</summary>
    public readonly struct WorldPlayerIdentity
    {
        public WorldPlayerIdentity(string playerPersonId, string displayName)
        {
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            DisplayName = WorldIdentityNameValidator.Validate(displayName, nameof(displayName));
        }

        public string PlayerPersonId { get; }
        public string DisplayName { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>한 World에서 FranchiseId에 확정된 표시 이름을 연결한다.</summary>
    public readonly struct WorldFranchiseIdentity
    {
        public WorldFranchiseIdentity(string franchiseId, string displayName)
        {
            FranchiseId = RequireId(franchiseId, nameof(franchiseId));
            DisplayName = WorldIdentityNameValidator.Validate(displayName, nameof(displayName));
        }

        public string FranchiseId { get; }
        public string DisplayName { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>Offline에서 Source 실명과 충돌하지 않음을 검증한 World 표시 이름 후보를 보관한다.</summary>
    public sealed class WorldIdentityNameCatalog
    {
        private readonly string[] _domesticPlayerNames;
        private readonly string[] _foreignPlayerNames;
        private readonly string[] _franchiseNames;

        public WorldIdentityNameCatalog(
            IReadOnlyList<string> domesticPlayerNames,
            IReadOnlyList<string> foreignPlayerNames,
            IReadOnlyList<string> franchiseNames)
        {
            _domesticPlayerNames = CopyUnique(domesticPlayerNames, nameof(domesticPlayerNames));
            _foreignPlayerNames = CopyUnique(foreignPlayerNames, nameof(foreignPlayerNames), allowEmpty: true);
            _franchiseNames = CopyUnique(franchiseNames, nameof(franchiseNames));
        }

        public IReadOnlyList<string> DomesticPlayerNames => _domesticPlayerNames;
        public IReadOnlyList<string> ForeignPlayerNames => _foreignPlayerNames;
        public IReadOnlyList<string> FranchiseNames => _franchiseNames;

        private static string[] CopyUnique(
            IReadOnlyList<string> source,
            string parameterName,
            bool allowEmpty = false)
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            if (!allowEmpty && source.Count == 0)
                throw new ArgumentException("하나 이상의 World Identity 이름 후보가 필요합니다.", parameterName);

            var result = new string[source.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string value = WorldIdentityNameValidator.Validate(source[index], parameterName);
                if (!unique.Add(value))
                    throw new ArgumentException($"World Identity 이름 후보는 중복될 수 없습니다: {value}", parameterName);
                result[index] = value;
            }
            return result;
        }

    }

    internal static class WorldIdentityNameValidator
    {
        public static string Validate(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("World Identity 이름은 비어 있을 수 없습니다.", parameterName);
            string trimmed = value.Trim();
            if (trimmed.Length > 30)
                throw new ArgumentException($"World Identity 이름이 너무 깁니다: {trimmed}", parameterName);
            for (int index = 0; index < trimmed.Length; index++)
            {
                if (char.IsControl(trimmed[index]) || char.IsDigit(trimmed[index]))
                    throw new ArgumentException($"World Identity 이름 형식이 올바르지 않습니다: {trimmed}", parameterName);
            }
            return trimmed;
        }
    }

    /// <summary>생성 뒤 Save에 그대로 보관하는 World별 선수·Franchise 표시 Identity 레지스트리다.</summary>
    public sealed class WorldIdentityRegistry
    {
        private readonly WorldPlayerIdentity[] _players;
        private readonly WorldFranchiseIdentity[] _franchises;
        private readonly Dictionary<string, string> _playerNamesById;
        private readonly Dictionary<string, string> _franchiseNamesById;

        public WorldIdentityRegistry(
            string identityGeneratorVersion,
            ulong identitySeed,
            IReadOnlyList<WorldPlayerIdentity> players,
            IReadOnlyList<WorldFranchiseIdentity> franchises)
        {
            if (string.IsNullOrWhiteSpace(identityGeneratorVersion))
                throw new ArgumentException("IdentityGeneratorVersion이 필요합니다.", nameof(identityGeneratorVersion));
            IdentityGeneratorVersion = identityGeneratorVersion.Trim();
            IdentitySeed = identitySeed;
            _players = CopyPlayers(players, out _playerNamesById);
            _franchises = CopyFranchises(franchises, out _franchiseNamesById);
        }

        public string IdentityGeneratorVersion { get; }
        public ulong IdentitySeed { get; }
        public IReadOnlyList<WorldPlayerIdentity> PlayerIdentities => _players;
        public IReadOnlyList<WorldFranchiseIdentity> FranchiseIdentities => _franchises;

        public string GetPlayerDisplayName(string playerPersonId)
        {
            string id = RequireId(playerPersonId, nameof(playerPersonId));
            if (!_playerNamesById.TryGetValue(id, out string displayName))
                throw new KeyNotFoundException($"PlayerPersonId {id}의 World Identity가 없습니다.");
            return displayName;
        }

        public string GetFranchiseDisplayName(string franchiseId)
        {
            string id = RequireId(franchiseId, nameof(franchiseId));
            if (!_franchiseNamesById.TryGetValue(id, out string displayName))
                throw new KeyNotFoundException($"FranchiseId {id}의 World Identity가 없습니다.");
            return displayName;
        }

        private static WorldPlayerIdentity[] CopyPlayers(
            IReadOnlyList<WorldPlayerIdentity> source,
            out Dictionary<string, string> byId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = new WorldPlayerIdentity[source.Count];
            byId = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                WorldPlayerIdentity identity = source[index];
                if (!byId.TryAdd(identity.PlayerPersonId, identity.DisplayName))
                    throw new ArgumentException("PlayerPersonId별 World Identity는 하나만 존재해야 합니다.", nameof(source));
                if (!names.Add(identity.DisplayName))
                    throw new ArgumentException("World 안에서 선수 DisplayName은 중복될 수 없습니다.", nameof(source));
                result[index] = identity;
            }
            return result;
        }

        private static WorldFranchiseIdentity[] CopyFranchises(
            IReadOnlyList<WorldFranchiseIdentity> source,
            out Dictionary<string, string> byId)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = new WorldFranchiseIdentity[source.Count];
            byId = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                WorldFranchiseIdentity identity = source[index];
                if (!byId.TryAdd(identity.FranchiseId, identity.DisplayName))
                    throw new ArgumentException("FranchiseId별 World Identity는 하나만 존재해야 합니다.", nameof(source));
                if (!names.Add(identity.DisplayName))
                    throw new ArgumentException("World 안에서 Franchise DisplayName은 중복될 수 없습니다.", nameof(source));
                result[index] = identity;
            }
            return result;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }
}

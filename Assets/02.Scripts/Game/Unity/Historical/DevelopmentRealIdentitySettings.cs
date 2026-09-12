using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>Editor와 Development Build에서만 실제 선수·구단 표시 Identity를 제공한다.</summary>
    public static class DevelopmentRealIdentitySettings
    {
        private const string CatalogResourcePath =
            "DevelopmentKboIdentities/DevelopmentRealIdentityCatalog";
        private const string PlayerPrefsKey = "baseball.development.real-identities.enabled";

        private static Dictionary<string, string> _emblemResourcesByTeamName;
        private static Dictionary<string, string> _playerNamesById;
        private static Dictionary<string, string> _franchiseNamesById;
        private static Dictionary<string, string> _franchiseHistoryNamesById;
        private static Dictionary<string, string> _teamSeasonNamesByKey;
        private static bool _isInitialized;
        private static bool _isEnabled;

        /// <summary>설정과 실제 Identity 리소스를 노출할 수 있는 빌드인지 반환한다.</summary>
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>현재 실제 선수·구단명과 엠블렘을 표시하는지 반환한다.</summary>
        public static bool IsEnabled
        {
            get
            {
                Initialize();
                return IsAvailable && _isEnabled;
            }
        }

        /// <summary>표시 Identity 변경 시 현재 화면이 Snapshot을 갱신할 수 있도록 알린다.</summary>
        public static event Action Changed;

        /// <summary>빌드 정책과 저장된 개발자 설정에 맞춰 표시 카탈로그를 초기화한다.</summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            if (!IsAvailable)
            {
                _emblemResourcesByTeamName = null;
                _playerNamesById = null;
                _franchiseNamesById = null;
                _franchiseHistoryNamesById = null;
                _teamSeasonNamesByKey = null;
                _isEnabled = false;
                _isInitialized = true;
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(CatalogResourcePath);
            if (asset == null)
                throw new InvalidOperationException("개발용 실제 Identity 카탈로그가 없습니다.");
            Catalog catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog == null || catalog.players == null || catalog.teams == null ||
                catalog.teamSeasons == null)
                throw new InvalidOperationException("개발용 실제 Identity 카탈로그 형식이 올바르지 않습니다.");

            var playerNames = new Dictionary<string, string>(catalog.players.Length, StringComparer.Ordinal);
            for (int index = 0; index < catalog.players.Length; index++)
                AddUnique(playerNames, catalog.players[index]?.id, catalog.players[index]?.name, "선수");

            var franchiseNames = new Dictionary<string, string>(catalog.teams.Length, StringComparer.Ordinal);
            var emblemResources = new Dictionary<string, string>(StringComparer.Ordinal);
            var emblemAliases = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.teams.Length; index++)
            {
                TeamEntry team = catalog.teams[index];
                if (team == null || string.IsNullOrWhiteSpace(team.emblemResource))
                    throw new InvalidOperationException("개발용 실제 구단 엠블렘 경로가 비어 있습니다.");
                AddUnique(franchiseNames, team.id, team.name, "구단");
                AddUnique(emblemResources, team.name, team.emblemResource, "구단 엠블렘");
                if (team.aliases == null)
                    continue;
                for (int aliasIndex = 0; aliasIndex < team.aliases.Length; aliasIndex++)
                    AddUnique(emblemAliases, team.aliases[aliasIndex], team.emblemResource, "구단 엠블렘 별칭");
            }

            var teamSeasonNames = new Dictionary<string, string>(
                catalog.teamSeasons.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < catalog.teamSeasons.Length; index++)
            {
                TeamSeasonEntry teamSeason = catalog.teamSeasons[index];
                if (teamSeason == null || string.IsNullOrWhiteSpace(teamSeason.emblemResource))
                    throw new InvalidOperationException("개발용 실제 TeamSeason 엠블렘 경로가 비어 있습니다.");
                AddUnique(
                    teamSeasonNames,
                    teamSeason.teamSeasonKey,
                    teamSeason.name,
                    "TeamSeason");
                AddOrMatch(
                    emblemResources,
                    teamSeason.name,
                    teamSeason.emblemResource,
                    "TeamSeason 엠블렘");
            }

            var franchiseHistoryNames = new Dictionary<string, string>(
                catalog.teams.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < catalog.teams.Length; index++)
            {
                TeamEntry team = catalog.teams[index];
                franchiseHistoryNames.Add(
                    team.id,
                    CreateFranchiseHistoryName(team, catalog.teamSeasons));
            }

            // 현재 구단 별칭이 과거 정식 구단명과 같으면 당시의 정식 엠블렘을 우선한다.
            foreach (KeyValuePair<string, string> alias in emblemAliases)
                emblemResources.TryAdd(alias.Key, alias.Value);

            _emblemResourcesByTeamName = emblemResources;
            _playerNamesById = playerNames;
            _franchiseNamesById = franchiseNames;
            _franchiseHistoryNamesById = franchiseHistoryNames;
            _teamSeasonNamesByKey = teamSeasonNames;
            _isEnabled = PlayerPrefs.GetInt(PlayerPrefsKey, 1) != 0;
            _isInitialized = true;
        }

        /// <summary>개발용 실제 Identity 표시 여부를 저장하고 즉시 적용한다.</summary>
        public static void SetEnabled(bool isEnabled)
        {
            Initialize();
            if (!IsAvailable)
                return;
            PlayerPrefs.SetInt(PlayerPrefsKey, isEnabled ? 1 : 0);
            PlayerPrefs.Save();
            _isEnabled = isEnabled;
            Changed?.Invoke();
        }

        /// <summary>Presentation Snapshot에서만 실제 선수 이름을 적용하고 원본 Identity는 유지한다.</summary>
        public static string ResolvePlayerName(WorldIdentityRegistry registry, string playerPersonId)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            Initialize();
            if (IsEnabled && _playerNamesById.TryGetValue(playerPersonId, out string realName))
                return realName;
            return registry.GetPlayerDisplayName(playerPersonId);
        }

        /// <summary>커리어 State의 가상 이름을 유지하면서 Source가 있는 선수만 실제 이름으로 덮어쓴다.</summary>
        public static string ResolvePlayerName(string virtualName, string playerPersonId)
        {
            Initialize();
            if (IsEnabled && !string.IsNullOrWhiteSpace(playerPersonId) &&
                _playerNamesById.TryGetValue(playerPersonId.Trim(), out string realName))
            {
                return realName;
            }
            return virtualName?.Trim() ?? string.Empty;
        }

        /// <summary>Presentation 코드가 실제 선수 이름 오버레이를 명시적으로 요청한다.</summary>
        public static string GetPresentationPlayerName(
            this WorldIdentityRegistry registry,
            string playerPersonId)
        {
            return ResolvePlayerName(registry, playerPersonId);
        }

        /// <summary>Presentation Snapshot에서만 실제 구단 이름을 적용하고 원본 Identity는 유지한다.</summary>
        public static string ResolveFranchiseName(WorldIdentityRegistry registry, string franchiseId)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            Initialize();
            if (IsEnabled && _franchiseNamesById.TryGetValue(franchiseId, out string realName))
                return realName;
            return registry.GetFranchiseDisplayName(franchiseId);
        }

        /// <summary>Presentation 코드가 실제 구단 이름 오버레이를 명시적으로 요청한다.</summary>
        public static string GetPresentationFranchiseName(
            this WorldIdentityRegistry registry,
            string franchiseId)
        {
            return ResolveFranchiseName(registry, franchiseId);
        }

        /// <summary>보유 선수 필터에서 연도별 브랜드를 하나의 실제 Franchise 계보명으로 표시한다.</summary>
        public static string ResolveFranchiseHistoryName(
            WorldIdentityRegistry registry,
            string franchiseId)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            Initialize();
            if (IsEnabled && _franchiseHistoryNamesById.TryGetValue(franchiseId, out string realName))
                return realName;
            return registry.GetFranchiseDisplayName(franchiseId);
        }

        /// <summary>Presentation Snapshot이 연도 접두사 없는 Franchise 계보 표시명을 요청한다.</summary>
        public static string GetPresentationFranchiseHistoryName(
            this WorldIdentityRegistry registry,
            string franchiseId)
        {
            return ResolveFranchiseHistoryName(registry, franchiseId);
        }

        /// <summary>실제 표시에서는 최신 Franchise명이 아니라 원본 연도의 TeamSeason명을 사용한다.</summary>
        public static string ResolveTeamSeasonName(
            WorldIdentityRegistry registry,
            string teamSeasonKey,
            string franchiseId)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            Initialize();
            if (IsEnabled && !string.IsNullOrWhiteSpace(teamSeasonKey) &&
                _teamSeasonNamesByKey.TryGetValue(teamSeasonKey.Trim(), out string realName))
                return realName;
            return ResolveFranchiseName(registry, franchiseId);
        }

        /// <summary>커리어 State의 가상 구단명을 유지하면서 Source가 있는 구단만 연도별 실제 브랜드로 덮어쓴다.</summary>
        public static string ResolveTeamSeasonName(
            string virtualName,
            string teamSeasonKey,
            string franchiseId)
        {
            Initialize();
            if (IsEnabled && !string.IsNullOrWhiteSpace(teamSeasonKey) &&
                _teamSeasonNamesByKey.TryGetValue(teamSeasonKey.Trim(), out string realSeasonName))
            {
                return realSeasonName;
            }
            if (IsEnabled && !string.IsNullOrWhiteSpace(franchiseId) &&
                _franchiseNamesById.TryGetValue(franchiseId.Trim(), out string realFranchiseName))
            {
                return realFranchiseName;
            }
            return virtualName?.Trim() ?? string.Empty;
        }

        /// <summary>Presentation Snapshot이 연도별 실제 구단 이름 오버레이를 요청한다.</summary>
        public static string GetPresentationTeamSeasonName(
            this WorldIdentityRegistry registry,
            string teamSeasonKey,
            string franchiseId)
        {
            return ResolveTeamSeasonName(registry, teamSeasonKey, franchiseId);
        }

        /// <summary>활성화된 실제 구단명에 대응하는 Resources 엠블렘 경로를 조회한다.</summary>
        public static bool TryGetEmblemResource(string teamName, out string resourcePath)
        {
            Initialize();
            if (IsEnabled && !string.IsNullOrWhiteSpace(teamName) && _emblemResourcesByTeamName != null)
                return _emblemResourcesByTeamName.TryGetValue(
                    RemoveSeasonYearPrefix(teamName),
                    out resourcePath);
            resourcePath = null;
            return false;
        }

        private static string RemoveSeasonYearPrefix(string teamName)
        {
            string normalized = teamName.Trim();
            int separator = normalized.IndexOf(' ');
            if (separator <= 0)
                return normalized;
            string prefix = normalized.Substring(0, separator);
            if (prefix.EndsWith("년", StringComparison.Ordinal))
                prefix = prefix.Substring(0, prefix.Length - 1);
            return prefix.Length == 4 && int.TryParse(prefix, out _)
                ? normalized.Substring(separator + 1).TrimStart()
                : normalized;
        }

        private static void AddUnique(
            Dictionary<string, string> destination,
            string key,
            string value,
            string entryType)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"개발용 실제 {entryType} 항목이 비어 있습니다.");
            if (!destination.TryAdd(key.Trim(), value.Trim()))
                throw new InvalidOperationException($"개발용 실제 {entryType} 키가 중복됩니다: {key}");
        }

        private static string CreateFranchiseHistoryName(
            TeamEntry franchise,
            IReadOnlyList<TeamSeasonEntry> teamSeasons)
        {
            var names = new List<string>();
            var uniqueNames = new HashSet<string>(StringComparer.Ordinal);
            string keyPrefix = franchise.id.Trim() + "_";
            for (int index = 0; index < teamSeasons.Count; index++)
            {
                TeamSeasonEntry season = teamSeasons[index];
                if (season == null ||
                    string.IsNullOrWhiteSpace(season.teamSeasonKey) ||
                    !season.teamSeasonKey.StartsWith(keyPrefix, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(season.name))
                {
                    continue;
                }
                string name = season.name.Trim();
                if (uniqueNames.Add(name)) names.Add(name);
            }
            if (names.Count == 0) names.Add(franchise.name.Trim());
            if (names.Count > 2)
            {
                for (int index = 0; index < names.Count; index++)
                {
                    int separator = names[index].IndexOf(' ');
                    if (separator > 0) names[index] = names[index].Substring(0, separator);
                }
            }
            return string.Join(" & ", names);
        }

        private static void AddOrMatch(
            Dictionary<string, string> destination,
            string key,
            string value,
            string entryType)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"개발용 실제 {entryType} 항목이 비어 있습니다.");
            string normalizedKey = key.Trim();
            string normalizedValue = value.Trim();
            if (destination.TryGetValue(normalizedKey, out string existing))
            {
                if (!string.Equals(existing, normalizedValue, StringComparison.Ordinal))
                    throw new InvalidOperationException($"개발용 실제 {entryType} 키가 충돌합니다: {key}");
                return;
            }
            destination.Add(normalizedKey, normalizedValue);
        }

        [Serializable]
        private sealed class Catalog
        {
            public int version;
            public PlayerEntry[] players;
            public TeamEntry[] teams;
            public TeamSeasonEntry[] teamSeasons;
        }

        [Serializable]
        private sealed class PlayerEntry
        {
            public string id;
            public string name;
        }

        [Serializable]
        private sealed class TeamEntry
        {
            public string id;
            public string name;
            public string[] aliases;
            public string emblemResource;
        }

        [Serializable]
        private sealed class TeamSeasonEntry
        {
            public string teamSeasonKey;
            public string name;
            public string emblemResource;
        }
    }
}

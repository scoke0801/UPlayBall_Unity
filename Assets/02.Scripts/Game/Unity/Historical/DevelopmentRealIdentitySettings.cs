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
            _isInitialized = true;

            if (!IsAvailable)
            {
                _emblemResourcesByTeamName = null;
                _playerNamesById = null;
                _franchiseNamesById = null;
                _isEnabled = false;
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(CatalogResourcePath);
            if (asset == null)
                throw new InvalidOperationException("개발용 실제 Identity 카탈로그가 없습니다.");
            Catalog catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog == null || catalog.players == null || catalog.teams == null)
                throw new InvalidOperationException("개발용 실제 Identity 카탈로그 형식이 올바르지 않습니다.");

            var playerNames = new Dictionary<string, string>(catalog.players.Length, StringComparer.Ordinal);
            for (int index = 0; index < catalog.players.Length; index++)
                AddUnique(playerNames, catalog.players[index]?.id, catalog.players[index]?.name, "선수");

            var franchiseNames = new Dictionary<string, string>(catalog.teams.Length, StringComparer.Ordinal);
            var emblemResources = new Dictionary<string, string>(StringComparer.Ordinal);
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
                    AddUnique(emblemResources, team.aliases[aliasIndex], team.emblemResource, "구단 엠블렘");
            }

            _emblemResourcesByTeamName = emblemResources;
            _playerNamesById = playerNames;
            _franchiseNamesById = franchiseNames;
            _isEnabled = PlayerPrefs.GetInt(PlayerPrefsKey, 1) != 0;
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

        /// <summary>활성화된 실제 구단명에 대응하는 Resources 엠블렘 경로를 조회한다.</summary>
        public static bool TryGetEmblemResource(string teamName, out string resourcePath)
        {
            Initialize();
            if (IsEnabled && !string.IsNullOrWhiteSpace(teamName) && _emblemResourcesByTeamName != null)
                return _emblemResourcesByTeamName.TryGetValue(teamName.Trim(), out resourcePath);
            resourcePath = null;
            return false;
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

        [Serializable]
        private sealed class Catalog
        {
            public int version;
            public PlayerEntry[] players;
            public TeamEntry[] teams;
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
    }
}

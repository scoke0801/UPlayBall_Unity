using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// Core의 직렬화 GameMode와 분리된 Presentation 전용 모드 구분이다.
    /// </summary>
    public enum UiGameMode
    {
        PlayerCareer = 0,
        OwnerCareer = 1
    }

    /// <summary>
    /// 공용 셸에 모드별 이름, Navigation, 조작 권한을 주입한다.
    /// </summary>
    public sealed class GameModeUiProfile
    {
        /// <summary>
        /// 모드 표시 계약을 만든다.
        /// </summary>
        public GameModeUiProfile(
            UiGameMode mode,
            string displayName,
            NavigationManifest navigation,
            UiCapabilitySet capabilities,
            NavigationManifest contextNavigation = null,
            NavigationRouteMigrationMap routeMigrations = null,
            string backgroundResourcePath = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("모드 표시 이름은 비어 있을 수 없습니다.", nameof(displayName));

            Mode = mode;
            DisplayName = displayName;
            Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            Capabilities = capabilities;
            ContextNavigation = contextNavigation ?? new NavigationManifest(Array.Empty<NavigationEntry>());
            RouteMigrations = routeMigrations ?? new NavigationRouteMigrationMap();
            BackgroundResourcePath = backgroundResourcePath ?? string.Empty;
            ValidateMigrationTargets();
        }

        /// <summary>
        /// Profile이 표현하는 UI 모드다.
        /// </summary>
        public UiGameMode Mode { get; }

        /// <summary>
        /// 셸에 표시할 모드 이름이다.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 모드가 노출할 Route 구조다.
        /// </summary>
        public NavigationManifest Navigation { get; }

        /// <summary>
        /// 모드가 허용하는 조작 기능 집합이다.
        /// </summary>
        public UiCapabilitySet Capabilities { get; }

        /// <summary>Global Navigation에 노출하지 않는 Match Center 같은 Context Route다.</summary>
        public NavigationManifest ContextNavigation { get; }

        /// <summary>기존 Deep Link를 새 Route로 연결하는 호환 계약이다.</summary>
        public NavigationRouteMigrationMap RouteMigrations { get; }

        /// <summary>공용 Shell 뒤에 낮은 대비로 표시할 모드별 배경 Resources 경로다.</summary>
        public string BackgroundResourcePath { get; }

        /// <summary>Old Route를 변환한 뒤 Global 또는 Context Route를 찾는다.</summary>
        public NavigationEntry FindEntry(string routeId)
        {
            string resolved = ResolveRouteId(routeId);
            return Navigation.FindEntry(resolved) ?? ContextNavigation.FindEntry(resolved);
        }

        /// <summary>Route가 속한 Global 또는 Context Navigation 그룹을 찾는다.</summary>
        public NavigationEntry FindNavigationGroup(string routeId)
        {
            string resolved = ResolveRouteId(routeId);
            return Navigation.FindPrimaryEntry(resolved) ?? ContextNavigation.FindPrimaryEntry(resolved);
        }

        /// <summary>Route가 Global에 노출되지 않는 Context Route인지 확인한다.</summary>
        public bool IsContextRoute(string routeId)
        {
            string resolved = ResolveRouteId(routeId);
            return ContextNavigation.FindEntry(resolved) != null;
        }

        /// <summary>기존 Route ID를 현재 Profile의 안정적인 Target으로 변환한다.</summary>
        public string ResolveRouteId(string routeId)
        {
            return RouteMigrations.Resolve(routeId);
        }

        private void ValidateMigrationTargets()
        {
            foreach (var pair in RouteMigrations.Targets)
            {
                string target = RouteMigrations.Resolve(pair.Key);
                if (Navigation.FindEntry(target) == null && ContextNavigation.FindEntry(target) == null)
                    throw new ArgumentException($"Route Migration Target이 Profile에 없습니다: {pair.Key} -> {target}");
            }
        }
    }
}

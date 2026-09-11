using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>업무 영역별 마지막 Local Route와 Home을 바닥으로 둔 화면 방문 스택을 보존한다.</summary>
    public sealed class GameModeNavigationState
    {
        private readonly GameModeUiProfile _profile;
        private readonly Dictionary<string, string> _lastLocalRoutes =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _routeHistory = new List<string>();
        private readonly string _rootRouteId;

        /// <summary>Profile의 기본 Route에서 시작하고 첫 Primary Route를 Home으로 사용하는 Navigation State를 만든다.</summary>
        public GameModeNavigationState(
            GameModeUiProfile profile,
            string initialRouteId,
            string rootRouteId = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rootRouteId = ResolveRootRoute(rootRouteId);
            ActiveRouteId = ResolveDestination(initialRouteId, allowContext: false);
            if (!IsAtRoot)
                _routeHistory.Add(_rootRouteId);
        }

        /// <summary>현재 표시 중인 canonical Route다.</summary>
        public string ActiveRouteId { get; private set; }

        /// <summary>현재 Match Center 같은 Context Screen이 열려 있는지 나타낸다.</summary>
        public bool IsContextOpen => _profile.IsContextRoute(ActiveRouteId);

        /// <summary>현재 화면이 더 이상 뒤로 갈 수 없는 Home Route인지 나타낸다.</summary>
        public bool IsAtRoot => string.Equals(ActiveRouteId, _rootRouteId, StringComparison.Ordinal);

        /// <summary>현재 화면에서 이전 방문 화면으로 돌아갈 수 있는지 나타낸다.</summary>
        public bool CanGoBack => !IsAtRoot && _routeHistory.Count > 0;

        /// <summary>Global 또는 Local Route로 이동하고 업무 영역별 마지막 선택을 기억한다.</summary>
        public string Navigate(string routeId)
        {
            string destination = ResolveDestination(routeId, allowContext: false);
            NavigationEntry group = _profile.Navigation.FindPrimaryEntry(destination);
            if (group == null)
                throw new InvalidOperationException($"Global Navigation Route가 아닙니다: {routeId}");

            if (string.Equals(destination, _rootRouteId, StringComparison.Ordinal))
            {
                ActiveRouteId = destination;
                _routeHistory.Clear();
                _lastLocalRoutes[group.RouteId] = destination;
                return destination;
            }

            if (IsContextOpen)
                RestoreContextOrigin();
            PushActiveRoute(destination);
            ActiveRouteId = destination;
            _lastLocalRoutes[group.RouteId] = destination;
            return destination;
        }

        /// <summary>현재 Route를 원점으로 기억하고 Context Screen을 연다.</summary>
        public string OpenContext(string routeId)
        {
            string destination = ResolveDestination(routeId, allowContext: true);
            if (!_profile.IsContextRoute(destination))
                throw new InvalidOperationException($"Context Navigation Route가 아닙니다: {routeId}");

            if (!IsContextOpen)
                PushActiveRoute(destination);
            ActiveRouteId = destination;
            return destination;
        }

        /// <summary>Context 내부 Local Route를 바꾸되 최초 진입 원점은 유지한다.</summary>
        public string NavigateContext(string routeId)
        {
            string destination = ResolveDestination(routeId, allowContext: true);
            if (!_profile.IsContextRoute(destination))
                throw new InvalidOperationException($"Context Navigation Route가 아닙니다: {routeId}");
            ActiveRouteId = destination;
            return destination;
        }

        /// <summary>현재 화면을 스택에서 꺼내 직전에 방문한 Route로 돌아간다.</summary>
        public bool TryBack(out string routeId)
        {
            if (!CanGoBack)
            {
                routeId = ActiveRouteId;
                return false;
            }

            int lastIndex = _routeHistory.Count - 1;
            routeId = _routeHistory[lastIndex];
            _routeHistory.RemoveAt(lastIndex);
            ActiveRouteId = routeId;
            return true;
        }

        private string ResolveRootRoute(string rootRouteId)
        {
            if (string.IsNullOrWhiteSpace(rootRouteId))
            {
                if (_profile.Navigation.Entries.Count == 0)
                    throw new InvalidOperationException("Navigation Profile에는 Home으로 사용할 Primary Route가 필요합니다.");
                rootRouteId = _profile.Navigation.Entries[0].RouteId;
            }

            string resolved = ResolveDestination(rootRouteId, allowContext: false);
            NavigationEntry entry = _profile.Navigation.FindEntry(resolved);
            if (entry == null)
                throw new InvalidOperationException($"Home Route는 Global Navigation에 있어야 합니다: {rootRouteId}");
            return resolved;
        }

        private void PushActiveRoute(string destination)
        {
            if (string.Equals(ActiveRouteId, destination, StringComparison.Ordinal))
                return;
            if (_routeHistory.Count > 0 &&
                string.Equals(_routeHistory[_routeHistory.Count - 1], ActiveRouteId, StringComparison.Ordinal))
                return;
            _routeHistory.Add(ActiveRouteId);
        }

        private void RestoreContextOrigin()
        {
            if (_routeHistory.Count == 0)
            {
                ActiveRouteId = _rootRouteId;
                return;
            }

            int lastIndex = _routeHistory.Count - 1;
            ActiveRouteId = _routeHistory[lastIndex];
            _routeHistory.RemoveAt(lastIndex);
        }

        private string ResolveDestination(string routeId, bool allowContext)
        {
            string resolved = _profile.ResolveRouteId(routeId);
            NavigationEntry entry = _profile.FindEntry(resolved);
            if (entry == null || !entry.IsVisible(_profile.Capabilities) || !entry.IsEnabled)
                throw new InvalidOperationException($"현재 Profile에서 열 수 없는 Route입니다: {routeId}");
            if (!allowContext && _profile.IsContextRoute(resolved))
                throw new InvalidOperationException($"Context Route는 OpenContext로 열어야 합니다: {routeId}");

            NavigationEntry group = _profile.FindNavigationGroup(resolved);
            if (entry.Children.Count > 0)
            {
                if (_lastLocalRoutes.TryGetValue(entry.RouteId, out string remembered))
                    return remembered;
                for (int i = 0; i < entry.Children.Count; i++)
                {
                    NavigationEntry child = entry.Children[i];
                    if (child.IsEnabled && child.IsVisible(_profile.Capabilities))
                        return child.RouteId;
                }
            }

            return group == null ? resolved : entry.RouteId;
        }
    }
}

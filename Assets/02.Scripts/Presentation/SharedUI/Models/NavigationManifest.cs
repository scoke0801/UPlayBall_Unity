using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 공용 셸에 표시할 하나의 Route와 선택 가능한 하위 Route를 정의한다.
    /// </summary>
    public sealed class NavigationEntry
    {
        private readonly NavigationEntry[] _children;

        /// <summary>
        /// Route 식별자와 표시 정보로 탐색 항목을 만든다.
        /// </summary>
        public NavigationEntry(
            string routeId,
            string displayName,
            UiCapability requiredCapability = UiCapability.None,
            bool isEnabled = true,
            string disabledReason = null,
            IReadOnlyList<NavigationEntry> children = null)
        {
            if (string.IsNullOrWhiteSpace(routeId))
                throw new ArgumentException("Route 식별자는 비어 있을 수 없습니다.", nameof(routeId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("표시 이름은 비어 있을 수 없습니다.", nameof(displayName));
            if (!isEnabled && string.IsNullOrWhiteSpace(disabledReason))
                throw new ArgumentException("비활성 Route에는 이유가 필요합니다.", nameof(disabledReason));

            RouteId = routeId;
            DisplayName = displayName;
            RequiredCapability = requiredCapability;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? string.Empty;
            _children = CopyChildren(children);
        }

        /// <summary>
        /// 화면 전환에 사용하는 안정적인 Route 식별자다.
        /// </summary>
        public string RouteId { get; }

        /// <summary>
        /// Navigation에 표시할 사용자용 이름이다.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 이 항목을 노출하기 위해 필요한 기능이다.
        /// </summary>
        public UiCapability RequiredCapability { get; }

        /// <summary>
        /// 현재 백엔드 연결 상태에서 선택 가능한지 나타낸다.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// 비활성 항목을 선택할 수 없는 이유다.
        /// </summary>
        public string DisabledReason { get; }

        /// <summary>
        /// Context Sub Tab으로 표시할 하위 Route 목록이다.
        /// </summary>
        public IReadOnlyList<NavigationEntry> Children => _children;

        /// <summary>
        /// 현재 기능 집합에서 이 항목을 노출할 수 있는지 확인한다.
        /// </summary>
        public bool IsVisible(UiCapabilitySet capabilities)
        {
            return capabilities.Has(RequiredCapability);
        }

        /// <summary>
        /// 자신이나 하위 항목이 지정 Route를 포함하는지 확인한다.
        /// </summary>
        public bool ContainsRoute(string routeId)
        {
            if (string.Equals(RouteId, routeId, StringComparison.Ordinal))
                return true;

            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].ContainsRoute(routeId))
                    return true;
            }

            return false;
        }

        private static NavigationEntry[] CopyChildren(IReadOnlyList<NavigationEntry> children)
        {
            if (children == null || children.Count == 0)
                return Array.Empty<NavigationEntry>();

            var copy = new NavigationEntry[children.Count];
            for (int i = 0; i < children.Count; i++)
                copy[i] = children[i] ?? throw new ArgumentException("하위 Route는 null일 수 없습니다.", nameof(children));
            return copy;
        }
    }

    /// <summary>
    /// 한 모드의 1차 Navigation과 Context Sub Tab 구조를 불변 목록으로 제공한다.
    /// </summary>
    public sealed class NavigationManifest
    {
        private readonly NavigationEntry[] _entries;

        /// <summary>
        /// 중복되지 않는 Route 항목으로 Manifest를 만든다.
        /// </summary>
        public NavigationManifest(IReadOnlyList<NavigationEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            _entries = new NavigationEntry[entries.Count];
            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                NavigationEntry entry = entries[i] ??
                    throw new ArgumentException("Navigation 항목은 null일 수 없습니다.", nameof(entries));
                ValidateDepth(entry, 1);
                ValidateUniqueRoutes(entry, routeIds);
                _entries[i] = entry;
            }
        }

        /// <summary>
        /// 등록 순서가 보존된 1차 Navigation 목록이다.
        /// </summary>
        public IReadOnlyList<NavigationEntry> Entries => _entries;

        /// <summary>
        /// 현재 기능 집합에 노출 가능한 1차 항목만 반환한다.
        /// </summary>
        public IReadOnlyList<NavigationEntry> GetVisibleEntries(UiCapabilitySet capabilities)
        {
            var visible = new List<NavigationEntry>(_entries.Length);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].IsVisible(capabilities))
                    visible.Add(_entries[i]);
            }

            return visible;
        }

        /// <summary>
        /// 지정 Route를 자신 또는 하위 항목으로 가진 1차 Navigation을 찾는다.
        /// </summary>
        public NavigationEntry FindPrimaryEntry(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
                return null;

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].ContainsRoute(routeId))
                    return _entries[i];
            }

            return null;
        }

        /// <summary>
        /// 1차 항목과 모든 하위 항목에서 지정 Route를 찾는다.
        /// </summary>
        public NavigationEntry FindEntry(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
                return null;

            for (int i = 0; i < _entries.Length; i++)
            {
                NavigationEntry match = FindEntryRecursive(_entries[i], routeId);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static void ValidateUniqueRoutes(NavigationEntry entry, HashSet<string> routeIds)
        {
            if (!routeIds.Add(entry.RouteId))
                throw new ArgumentException($"중복 Route가 있습니다: {entry.RouteId}");

            for (int i = 0; i < entry.Children.Count; i++)
                ValidateUniqueRoutes(entry.Children[i], routeIds);
        }

        private static void ValidateDepth(NavigationEntry entry, int depth)
        {
            if (depth > 2)
                throw new ArgumentException(
                    $"Navigation Route는 Primary와 Local 두 단계까지만 허용됩니다: {entry.RouteId}");

            for (int i = 0; i < entry.Children.Count; i++)
                ValidateDepth(entry.Children[i], depth + 1);
        }

        private static NavigationEntry FindEntryRecursive(NavigationEntry entry, string routeId)
        {
            if (string.Equals(entry.RouteId, routeId, StringComparison.Ordinal))
                return entry;

            for (int i = 0; i < entry.Children.Count; i++)
            {
                NavigationEntry match = FindEntryRecursive(entry.Children[i], routeId);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}

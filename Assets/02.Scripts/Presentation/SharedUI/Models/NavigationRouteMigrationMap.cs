using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>기존 Deep Link를 새 업무 영역의 안정적인 Route로 변환한다.</summary>
    public sealed class NavigationRouteMigrationMap
    {
        private readonly Dictionary<string, string> _targets;

        /// <summary>Profile 생성 시 Target 유효성을 검사할 수 있는 Migration 목록이다.</summary>
        public IReadOnlyDictionary<string, string> Targets => _targets;

        /// <summary>Old Route와 새 Target 쌍으로 불변 Migration Map을 만든다.</summary>
        public NavigationRouteMigrationMap(IReadOnlyDictionary<string, string> targets = null)
        {
            _targets = new Dictionary<string, string>(StringComparer.Ordinal);
            if (targets == null)
                return;

            foreach (KeyValuePair<string, string> pair in targets)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Route Migration의 Old Route와 Target은 비어 있을 수 없습니다.", nameof(targets));
                if (!_targets.TryAdd(pair.Key, pair.Value))
                    throw new ArgumentException($"중복 Route Migration이 있습니다: {pair.Key}", nameof(targets));
            }
        }

        /// <summary>등록된 Old Route면 새 Target을, 아니면 입력 Route를 반환한다.</summary>
        public string Resolve(string routeId)
        {
            if (string.IsNullOrWhiteSpace(routeId))
                return routeId;

            string current = routeId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (_targets.TryGetValue(current, out string target))
            {
                if (!visited.Add(current))
                    throw new InvalidOperationException($"Route Migration 순환 참조가 있습니다: {routeId}");
                current = target;
            }
            return current;
        }
    }
}

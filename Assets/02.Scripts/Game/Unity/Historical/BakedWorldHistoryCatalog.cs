using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Historical
{
    /// <summary>구운 World History 한 건의 Player Build 참조다.</summary>
    [Serializable]
    public sealed class BakedWorldHistoryEntry
    {
        [SerializeField] private TextAsset _payload;
        [SerializeField, Tooltip("어떤 Seed를 구웠는지 사람이 알아볼 수 있게 남기는 표시용 값이다.")]
        private string _label = string.Empty;

        public BakedWorldHistoryEntry(TextAsset payload, string label)
        {
            _payload = payload != null ? payload : throw new ArgumentNullException(nameof(payload));
            _label = label ?? string.Empty;
        }

        public TextAsset Payload => _payload;
        public string Label => _label ?? string.Empty;
    }

    /// <summary>
    /// 빌드 타임에 구워 둔 World History들을 Player Build 의존성으로 묶는 Catalog다.
    /// 비어 있어도 게임은 동작한다. 그 경우 44시즌을 실제로 시뮬레이션하므로 시작이 느려질 뿐이다.
    /// </summary>
    public sealed class BakedWorldHistoryCatalog : ScriptableObject
    {
        [SerializeField] private BakedWorldHistoryEntry[] _entries = Array.Empty<BakedWorldHistoryEntry>();

        public IReadOnlyList<BakedWorldHistoryEntry> Entries =>
            _entries ?? Array.Empty<BakedWorldHistoryEntry>();

        /// <summary>Editor Baker가 산출물 참조를 원자적으로 교체한다.</summary>
        public void Configure(IReadOnlyList<BakedWorldHistoryEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            _entries = new BakedWorldHistoryEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
                _entries[index] = entries[index] ?? throw new ArgumentException("null Bake 항목이 있습니다.", nameof(entries));
        }
    }

    /// <summary>
    /// Catalog의 Bake 중 Key가 정확히 맞는 것 하나를 복원한다.
    /// 맞는 것이 없으면 false를 돌려주고, 호출자는 실제 시뮬레이션으로 되돌아간다.
    /// </summary>
    public sealed class UnityBakedWorldHistorySource : IBakedWorldHistorySource
    {
        private readonly BakedWorldHistoryCatalog _catalog;
        private readonly ILoadDiagnostics _diagnostics;
        private readonly object _cacheLock = new object();
        private BakedWorldHistoryKey _cachedKey;
        private WorldHistorySnapshot _cachedSnapshot;

        public UnityBakedWorldHistorySource(BakedWorldHistoryCatalog catalog, ILoadDiagnostics diagnostics = null)
        {
            _catalog = catalog;
            _diagnostics = diagnostics;
        }

        /// <summary>Game 레이어가 UnityEngine.Debug를 모르도록 로그를 주입받는다.</summary>
        public interface ILoadDiagnostics
        {
            void ReportBakeIgnored(string message);
        }

        public bool TryLoad(BakedWorldHistoryKey key, out WorldHistorySnapshot snapshot)
        {
            snapshot = null;
            if (_catalog == null)
                return false;

            lock (_cacheLock)
            {
                if (_cachedSnapshot != null && _cachedKey.Equals(key))
                {
                    snapshot = _cachedSnapshot;
                    return true;
                }
            }

            IReadOnlyList<BakedWorldHistoryEntry> entries = _catalog.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                BakedWorldHistoryEntry entry = entries[index];
                if (entry == null || entry.Payload == null)
                    continue;
                byte[] bytes = entry.Payload.bytes;
                if (!WorldHistoryBakeCodec.TryPeekKey(bytes, out BakedWorldHistoryKey candidate) ||
                    !candidate.Equals(key))
                {
                    continue;
                }

                try
                {
                    BakedWorldHistoryPayload payload = WorldHistoryBakeCodec.Decode(bytes);
                    WorldHistorySnapshot restored = new WorldHistorySaveMapper().Restore(payload.History);
                    lock (_cacheLock)
                    {
                        _cachedKey = key;
                        _cachedSnapshot = restored;
                    }
                    snapshot = restored;
                    return true;
                }
                catch (Exception exception) when (exception is WorldHistoryBakeFormatException ||
                                                  exception is ArgumentException)
                {
                    // 손상된 Bake 때문에 게임이 시작되지 않으면 안 된다. 무시하고 실제 시뮬레이션으로 넘긴다.
                    _diagnostics?.ReportBakeIgnored(
                        $"World History Bake를 무시했습니다. label={entry.Label}, reason={exception.Message}");
                }
            }
            return false;
        }
    }
}

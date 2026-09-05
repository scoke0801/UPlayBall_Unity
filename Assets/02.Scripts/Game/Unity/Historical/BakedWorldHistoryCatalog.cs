using System;
using System.Collections.Generic;
using System.Text;
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
        private volatile CachedEntry[] _cachedEntries;

        /// <summary>메인 스레드에서 미리 떠 둔 Bake 한 건이다. 워커가 TextAsset을 만지지 않게 한다.</summary>
        private readonly struct CachedEntry
        {
            public CachedEntry(string label, byte[] bytes)
            {
                Label = label;
                Bytes = bytes;
            }

            public string Label { get; }
            public byte[] Bytes { get; }
        }

        public UnityBakedWorldHistorySource(BakedWorldHistoryCatalog catalog, ILoadDiagnostics diagnostics = null)
        {
            _catalog = catalog;
            _diagnostics = diagnostics;
        }

        /// <summary>Game 레이어가 UnityEngine.Debug를 모르도록 로그를 주입받는다.</summary>
        public interface ILoadDiagnostics
        {
            void ReportBakeIgnored(string message);

            /// <summary>Bake를 채택했음을 알린다. 미스와 구분되어야 원인 추적이 가능하다.</summary>
            void ReportBakeHit(string message);

            /// <summary>
            /// 맞는 Bake가 없어 실제 시뮬레이션으로 되돌아갔음을 알린다.
            /// 미스는 결과를 틀리게 하지 않지만 새 게임 시작 비용을 수십 배로 키우므로 조용히 넘기면 안 된다.
            /// </summary>
            void ReportBakeMissed(string message);
        }

        /// <summary>
        /// TextAsset 접근은 메인 스레드에서만 가능하다. 워밍업이 워커 스레드에서 World를 준비하려면
        /// 원본 바이트를 먼저 이 메서드로 확보해야 한다. 반드시 메인 스레드에서 호출한다.
        /// </summary>
        public void CacheAssetBytesOnMainThread()
        {
            if (_catalog == null)
            {
                _cachedEntries = Array.Empty<CachedEntry>();
                return;
            }
            _cachedEntries = ReadEntriesFromCatalog();
        }

        /// <summary>복원이 끝난 뒤 원본 바이트 사본을 놓아준다. 복원해 둔 Snapshot 캐시는 유지한다.</summary>
        public void ReleaseAssetByteCache()
        {
            _cachedEntries = null;
        }

        public bool TryLoad(BakedWorldHistoryKey key, out WorldHistorySnapshot snapshot)
        {
            snapshot = null;
            if (_catalog == null)
            {
                _diagnostics?.ReportBakeMissed($"Bake Catalog가 연결되어 있지 않습니다. 요청 Key={key}");
                return false;
            }

            lock (_cacheLock)
            {
                if (_cachedSnapshot != null && _cachedKey.Equals(key))
                {
                    snapshot = _cachedSnapshot;
                    return true;
                }
            }

            CachedEntry[] entries = ReadEntries();
            for (int index = 0; index < entries.Length; index++)
            {
                CachedEntry entry = entries[index];
                byte[] bytes = entry.Bytes;
                if (bytes == null)
                    continue;
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
                    _diagnostics?.ReportBakeHit($"World History Bake를 채택했습니다. label={entry.Label}, key={key}");
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

            _diagnostics?.ReportBakeMissed(DescribeMiss(key, entries));
            return false;
        }

        /// <summary>미리 확보한 바이트가 있으면 그것만 쓴다. 없으면 메인 스레드로 보고 TextAsset에서 직접 읽는다.</summary>
        private CachedEntry[] ReadEntries()
        {
            return _cachedEntries ?? ReadEntriesFromCatalog();
        }

        private CachedEntry[] ReadEntriesFromCatalog()
        {
            IReadOnlyList<BakedWorldHistoryEntry> entries = _catalog.Entries;
            var result = new List<CachedEntry>(entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                BakedWorldHistoryEntry entry = entries[index];
                if (entry == null || entry.Payload == null)
                    continue;
                result.Add(new CachedEntry(entry.Label, entry.Payload.bytes));
            }
            return result.ToArray();
        }

        /// <summary>
        /// 어느 항목이 왜 어긋났는지까지 남긴다. Key는 Seed·콘텐츠 해시·밸런스 해시의 합성이라
        /// "맞는 것이 없다"만으로는 다시 구워야 하는지, Seed 설정이 틀린 것인지 구분할 수 없다.
        /// </summary>
        private static string DescribeMiss(BakedWorldHistoryKey key, CachedEntry[] entries)
        {
            var builder = new StringBuilder();
            builder.Append("맞는 World History Bake가 없어 실제 시뮬레이션으로 진행합니다(느려집니다).")
                .Append("\n  요청 Key: ").Append(key)
                .Append("\n  Catalog 항목 ").Append(entries.Length).Append("건:");
            if (entries.Length == 0)
                builder.Append(" (비어 있음)");
            for (int index = 0; index < entries.Length; index++)
            {
                CachedEntry entry = entries[index];
                builder.Append("\n    - ").Append(entry.Label).Append(": ");
                if (entry.Bytes == null)
                {
                    builder.Append("payload 없음");
                    continue;
                }
                if (!WorldHistoryBakeCodec.TryPeekKey(entry.Bytes, out BakedWorldHistoryKey candidate))
                {
                    builder.Append("Key를 읽지 못함(형식 불일치)");
                    continue;
                }
                builder.Append(candidate).Append(DescribeDifference(key, candidate));
            }
            return builder.ToString();
        }

        private static string DescribeDifference(BakedWorldHistoryKey key, BakedWorldHistoryKey candidate)
        {
            var builder = new StringBuilder("  → 불일치:");
            if (key.RecordMode != candidate.RecordMode) builder.Append(" RecordMode");
            if (key.WorldHistorySeed != candidate.WorldHistorySeed) builder.Append(" Seed");
            if (!string.Equals(key.ContentHash, candidate.ContentHash, StringComparison.Ordinal))
                builder.Append(" ContentHash");
            if (key.BalanceVersion != candidate.BalanceVersion) builder.Append(" BalanceVersion");
            if (!string.Equals(key.BalanceContentHash, candidate.BalanceContentHash, StringComparison.Ordinal))
                builder.Append(" BalanceContentHash");
            return builder.ToString();
        }
    }
}

using System;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>
    /// 구운 World History가 지금 실행 중인 콘텐츠·밸런스와 같은 조건에서 나온 것인지 판별하는 식별자다.
    /// 시드가 같아도 원본 데이터나 밸런스 계수가 바뀌면 경기 결과가 달라지므로 둘 다 Key에 포함한다.
    /// </summary>
    public readonly struct BakedWorldHistoryKey : IEquatable<BakedWorldHistoryKey>
    {
        public BakedWorldHistoryKey(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            string contentHash,
            int balanceVersion,
            string balanceContentHash)
        {
            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
            ContentHash = Normalize(contentHash);
            BalanceVersion = balanceVersion;
            BalanceContentHash = Normalize(balanceContentHash);
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }

        /// <summary>역사 원본 Bake의 SourceManifest ContentHash다.</summary>
        public string ContentHash { get; }

        public int BalanceVersion { get; }

        /// <summary>경기 결과를 좌우하는 BalanceTable의 내용 식별자다.</summary>
        public string BalanceContentHash { get; }

        public bool Equals(BakedWorldHistoryKey other)
        {
            return RecordMode == other.RecordMode &&
                   WorldHistorySeed == other.WorldHistorySeed &&
                   BalanceVersion == other.BalanceVersion &&
                   string.Equals(ContentHash, other.ContentHash, StringComparison.Ordinal) &&
                   string.Equals(BalanceContentHash, other.BalanceContentHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is BakedWorldHistoryKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)RecordMode;
                hash = hash * 397 ^ WorldHistorySeed.GetHashCode();
                hash = hash * 397 ^ BalanceVersion;
                hash = hash * 397 ^ ContentHash.GetHashCode();
                return hash * 397 ^ BalanceContentHash.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{RecordMode}/seed={WorldHistorySeed}/content={ContentHash}/balance={BalanceVersion}:{BalanceContentHash}";
        }

        private static string Normalize(string value) => value == null ? string.Empty : value.Trim();
    }

    /// <summary>
    /// 빌드 타임에 구워 둔 World History를 런타임에 공급한다.
    /// Key가 맞지 않으면 false를 반환해 호출자가 실제 시뮬레이션으로 되돌아가게 한다.
    /// </summary>
    public interface IBakedWorldHistorySource
    {
        bool TryLoad(BakedWorldHistoryKey key, out WorldHistorySnapshot snapshot);
    }

    /// <summary>Key와 본문을 함께 담아 Bake 산출물 한 건을 나타낸다.</summary>
    public sealed class BakedWorldHistoryPayload
    {
        public BakedWorldHistoryPayload(BakedWorldHistoryKey key, WorldHistorySaveData history)
        {
            Key = key;
            History = history ?? throw new ArgumentNullException(nameof(history));
        }

        public BakedWorldHistoryKey Key { get; }
        public WorldHistorySaveData History { get; }
    }
}

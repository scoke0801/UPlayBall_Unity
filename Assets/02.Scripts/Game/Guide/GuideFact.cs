using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baseball.Game.Guide
{
    /// <summary>Save/Load 뒤에도 같은 Variation을 고르기 위한 사건 식별 정보를 보관한다.</summary>
    public readonly struct GuideFactIdentity
    {
        public GuideFactIdentity(
            ulong? worldSeed,
            string eventId,
            string saveId = "",
            long sequenceNumber = 0)
        {
            WorldSeed = worldSeed;
            EventId = eventId ?? string.Empty;
            SaveId = saveId ?? string.Empty;
            SequenceNumber = sequenceNumber;
        }

        public ulong? WorldSeed { get; }
        public string EventId { get; }
        public string SaveId { get; }
        public long SequenceNumber { get; }
        public bool HasPrimarySeed => WorldSeed.HasValue && !string.IsNullOrWhiteSpace(EventId);
        public bool HasFallbackSeed => !string.IsNullOrWhiteSpace(SaveId) && SequenceNumber >= 0;

        public string Resolve(string token)
        {
            return token switch
            {
                "worldSeed" when WorldSeed.HasValue =>
                    WorldSeed.Value.ToString(CultureInfo.InvariantCulture),
                "eventId" when !string.IsNullOrWhiteSpace(EventId) => EventId,
                "saveId" when !string.IsNullOrWhiteSpace(SaveId) => SaveId,
                "sequenceNo" when SequenceNumber >= 0 => SequenceNumber.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
        }
    }

    /// <summary>Application 계층이 계산을 끝낸 사실과 표시용 값만 Guide에 전달한다.</summary>
    public sealed class GuideFact
    {
        private readonly Dictionary<string, string> _payload;
        private readonly Dictionary<string, string> _runtimeContext;

        public GuideFact(
            GuideModeScope mode,
            string factType,
            GuideFactIdentity identity,
            IReadOnlyDictionary<string, string> payload = null,
            IReadOnlyDictionary<string, string> runtimeContext = null)
        {
            if (mode == GuideModeScope.Common)
                throw new ArgumentException("Fact 생산자는 Owner 또는 Career 모드를 명시해야 합니다.", nameof(mode));
            if (string.IsNullOrWhiteSpace(factType))
                throw new ArgumentException("factType이 필요합니다.", nameof(factType));
            if (!identity.HasPrimarySeed && !identity.HasFallbackSeed)
                throw new ArgumentException("결정론적 Variation을 위한 primary 또는 fallback 식별자가 필요합니다.", nameof(identity));

            Mode = mode;
            FactType = factType.Trim();
            Identity = identity;
            _payload = Copy(payload);
            _runtimeContext = Copy(runtimeContext);
        }

        public GuideModeScope Mode { get; }
        public string FactType { get; }
        public GuideFactIdentity Identity { get; }
        public IReadOnlyDictionary<string, string> Payload => _payload;
        public IReadOnlyDictionary<string, string> RuntimeContext => _runtimeContext;

        public bool TryResolveToken(string token, out string value)
        {
            if (_payload.TryGetValue(token, out value) || _runtimeContext.TryGetValue(token, out value))
                return true;
            value = Identity.Resolve(token);
            return value != null;
        }

        private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null)
                return result;
            foreach (KeyValuePair<string, string> pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                    throw new ArgumentException("Fact 값의 key는 비어 있을 수 없습니다.", nameof(source));
                result.Add(pair.Key, pair.Value ?? string.Empty);
            }
            return result;
        }
    }

    /// <summary>Fact 생산 어댑터가 숫자 형식을 고정해 payload와 runtime context를 조립한다.</summary>
    public sealed class GuideFactBuilder
    {
        private readonly GuideModeScope _mode;
        private readonly string _factType;
        private readonly GuideFactIdentity _identity;
        private readonly Dictionary<string, string> _payload = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _runtimeContext = new(StringComparer.Ordinal);

        public GuideFactBuilder(GuideModeScope mode, string factType, GuideFactIdentity identity)
        {
            _mode = mode;
            _factType = factType;
            _identity = identity;
        }

        public GuideFactBuilder AddPayload(string key, string value)
        {
            _payload.Add(key, value ?? string.Empty);
            return this;
        }

        public GuideFactBuilder AddPayload(string key, int value) =>
            AddPayload(key, value.ToString(CultureInfo.InvariantCulture));

        public GuideFactBuilder AddPayload(string key, double value, string format = "0.##") =>
            AddPayload(key, value.ToString(format, CultureInfo.InvariantCulture));

        public GuideFactBuilder AddContext(string key, string value)
        {
            _runtimeContext.Add(key, value ?? string.Empty);
            return this;
        }

        public GuideFactBuilder AddContext(string key, int value) =>
            AddContext(key, value.ToString(CultureInfo.InvariantCulture));

        public GuideFactBuilder AddContext(string key, long value) =>
            AddContext(key, value.ToString(CultureInfo.InvariantCulture));

        public GuideFact Build() => new(_mode, _factType, _identity, _payload, _runtimeContext);
    }
}

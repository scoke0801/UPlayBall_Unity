using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baseball.Game.Career.News
{
    /// <summary>기사 생성에 사용하는 하나의 확정 사실을 형식과 함께 저장한다.</summary>
    public readonly struct NewsFact
    {
        private NewsFact(
            NewsFactKey key,
            NewsFactValueType valueType,
            long integerValue,
            double decimalValue,
            string textValue)
        {
            Key = key;
            ValueType = valueType;
            IntegerValue = integerValue;
            DecimalValue = decimalValue;
            TextValue = textValue ?? string.Empty;
        }

        public NewsFactKey Key { get; }
        public NewsFactValueType ValueType { get; }
        public long IntegerValue { get; }
        public double DecimalValue { get; }
        public string TextValue { get; }

        public static NewsFact Integer(NewsFactKey key, long value) =>
            new(key, NewsFactValueType.Integer, value, value, string.Empty);

        public static NewsFact Decimal(NewsFactKey key, double value) =>
            new(key, NewsFactValueType.Decimal, (long)value, value, string.Empty);

        public static NewsFact Text(NewsFactKey key, string value) =>
            new(key, NewsFactValueType.Text, 0L, 0d, value);

        public static NewsFact Boolean(NewsFactKey key, bool value) =>
            new(key, NewsFactValueType.Boolean, value ? 1L : 0L, value ? 1d : 0d, string.Empty);

        public string ToDisplayString()
        {
            return ValueType switch
            {
                NewsFactValueType.Integer => IntegerValue.ToString(CultureInfo.InvariantCulture),
                NewsFactValueType.Decimal => DecimalValue.ToString("0.###", CultureInfo.InvariantCulture),
                NewsFactValueType.Boolean => IntegerValue != 0 ? "true" : "false",
                _ => TextValue
            };
        }
    }

    /// <summary>동일 키를 하나만 유지하며 기사에 필요한 사실을 구조화해 보관한다.</summary>
    public sealed class NewsFactSet
    {
        private readonly List<NewsFact> _facts = new();

        public IReadOnlyList<NewsFact> Facts => _facts;

        public void Set(NewsFact fact)
        {
            for (int index = 0; index < _facts.Count; index++)
            {
                if (_facts[index].Key != fact.Key)
                    continue;
                _facts[index] = fact;
                return;
            }
            _facts.Add(fact);
        }

        public void SetInteger(NewsFactKey key, long value) => Set(NewsFact.Integer(key, value));
        public void SetDecimal(NewsFactKey key, double value) => Set(NewsFact.Decimal(key, value));
        public void SetText(NewsFactKey key, string value) => Set(NewsFact.Text(key, value));
        public void SetBoolean(NewsFactKey key, bool value) => Set(NewsFact.Boolean(key, value));

        public bool Contains(NewsFactKey key) => TryGet(key, out _);

        public bool TryGet(NewsFactKey key, out NewsFact fact)
        {
            for (int index = 0; index < _facts.Count; index++)
            {
                if (_facts[index].Key != key)
                    continue;
                fact = _facts[index];
                return true;
            }
            fact = default;
            return false;
        }

        public int GetInteger(NewsFactKey key, int fallback = 0)
        {
            return TryGet(key, out NewsFact fact) ? checked((int)fact.IntegerValue) : fallback;
        }

        public double GetDecimal(NewsFactKey key, double fallback = 0d)
        {
            return TryGet(key, out NewsFact fact) ? fact.DecimalValue : fallback;
        }

        public bool GetBoolean(NewsFactKey key, bool fallback = false)
        {
            return TryGet(key, out NewsFact fact) ? fact.IntegerValue != 0 : fallback;
        }

        public string GetText(NewsFactKey key, string fallback = "")
        {
            return TryGet(key, out NewsFact fact) ? fact.ToDisplayString() : fallback;
        }

        /// <summary>다른 FactSet의 값으로 동일 키를 덮어쓰며 사건 병합 결과를 만든다.</summary>
        public void MergeFrom(NewsFactSet source)
        {
            if (source == null)
                return;
            for (int index = 0; index < source._facts.Count; index++)
                Set(source._facts[index]);
        }

        public NewsFactSet Clone()
        {
            var clone = new NewsFactSet();
            clone.MergeFrom(this);
            return clone;
        }
    }
}

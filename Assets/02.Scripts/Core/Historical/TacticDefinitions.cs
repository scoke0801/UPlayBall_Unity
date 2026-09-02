using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    public enum TacticCardCategory
    {
        Batting,
        Pitching,
        Analysis,
        Common
    }

    public enum TacticTier
    {
        Normal,
        Rare,
        Special,
        Signature
    }

    public enum TacticTriggerField
    {
        Inning,
        ScoreDifference,
        AbsoluteRunDifference,
        BatterOrder,
        RunnerOnSecondOrThird,
        OpponentPitcherHand,
        PitcherRole
    }

    public enum TacticComparison
    {
        Equal,
        NotEqual,
        LessOrEqual,
        GreaterOrEqual,
        BetweenInclusive
    }

    /// <summary>카드별 전용 코드 없이 경기 상태의 한 필드를 비교하는 발동 조건이다.</summary>
    public readonly struct TacticTriggerCondition
    {
        public TacticTriggerCondition(
            TacticTriggerField field,
            TacticComparison comparison,
            int value,
            int maximumValue = 0)
        {
            if (comparison == TacticComparison.BetweenInclusive && maximumValue < value)
                throw new ArgumentOutOfRangeException(nameof(maximumValue));
            Field = field;
            Comparison = comparison;
            Value = value;
            MaximumValue = maximumValue;
        }

        public TacticTriggerField Field { get; }
        public TacticComparison Comparison { get; }
        public int Value { get; }
        public int MaximumValue { get; }
    }

    public enum TacticTargetRule
    {
        CurrentBatter,
        CurrentPitcher,
        BattingTeam,
        PitchingTeam,
        Bullpen,
        Opponent
    }

    public readonly struct TacticStatModifier
    {
        public TacticStatModifier(PlayerAbility ability, int amount)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            Ability = ability;
            Amount = amount;
        }

        public PlayerAbility Ability { get; }
        public int Amount { get; }
    }

    public readonly struct TacticBehaviorModifier
    {
        public TacticBehaviorModifier(string behaviorId, double amount)
        {
            if (string.IsNullOrWhiteSpace(behaviorId))
                throw new ArgumentException("BehaviorId는 비어 있을 수 없습니다.", nameof(behaviorId));
            BehaviorId = behaviorId.Trim();
            Amount = amount;
        }

        public string BehaviorId { get; }
        public double Amount { get; }
    }

    public enum TacticDurationRule
    {
        CurrentPlateAppearance,
        UntilInningEnd,
        UntilPitcherRemoved,
        RestOfGame
    }

    /// <summary>공통 Trigger/Target/Modifier로 해석되는 데이터 기반 전술카드 정의다.</summary>
    public sealed class TacticCardDefinition
    {
        private readonly TacticTriggerCondition[] _triggerConditions;
        private readonly TacticStatModifier[] _statModifiers;
        private readonly TacticBehaviorModifier[] _behaviorModifiers;
        private readonly string[] _counterCardIds;

        public TacticCardDefinition(
            string cardId,
            string name,
            TacticCardCategory category,
            TacticTier tacticTier,
            string referenceBehavior,
            string projectBalanceValue,
            IReadOnlyList<TacticTriggerCondition> triggerConditions,
            TacticTargetRule targetRule,
            IReadOnlyList<TacticStatModifier> statModifiers,
            IReadOnlyList<TacticBehaviorModifier> behaviorModifiers,
            TacticDurationRule durationRule,
            IReadOnlyList<string> counterCardIds,
            bool isDisruption)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("전술카드 이름은 비어 있을 수 없습니다.", nameof(name));
            CardId = cardId.Trim();
            Name = name.Trim();
            Category = category;
            TacticTier = tacticTier;
            ReferenceBehavior = referenceBehavior?.Trim() ?? string.Empty;
            ProjectBalanceValue = projectBalanceValue?.Trim() ?? string.Empty;
            _triggerConditions = Copy(triggerConditions);
            TargetRule = targetRule;
            _statModifiers = Copy(statModifiers);
            _behaviorModifiers = Copy(behaviorModifiers);
            _counterCardIds = CopyIds(counterCardIds, cardId);
            DurationRule = durationRule;
            IsDisruption = isDisruption;
        }

        public string CardId { get; }
        public string Name { get; }
        public TacticCardCategory Category { get; }
        public TacticTier TacticTier { get; }
        public string ReferenceBehavior { get; }
        public string ProjectBalanceValue { get; }
        public IReadOnlyList<TacticTriggerCondition> TriggerConditions => _triggerConditions;
        public TacticTargetRule TargetRule { get; }
        public IReadOnlyList<TacticStatModifier> StatModifiers => _statModifiers;
        public IReadOnlyList<TacticBehaviorModifier> BehaviorModifiers => _behaviorModifiers;
        public TacticDurationRule DurationRule { get; }
        public IReadOnlyList<string> CounterCardIds => _counterCardIds;
        public bool IsDisruption { get; }

        public bool Counters(string otherCardId)
        {
            for (int index = 0; index < _counterCardIds.Length; index++)
                if (string.Equals(_counterCardIds[index], otherCardId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < source.Count; index++) result[index] = source[index];
            return result;
        }

        private static string[] CopyIds(IReadOnlyList<string> source, string ownCardId)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                string id = source[index]?.Trim();
                if (string.IsNullOrEmpty(id) || string.Equals(id, ownCardId, StringComparison.Ordinal))
                    throw new ArgumentException("CounterCardId가 유효하지 않습니다.", nameof(source));
                for (int previous = 0; previous < index; previous++)
                    if (string.Equals(result[previous], id, StringComparison.Ordinal))
                        throw new ArgumentException("CounterCardId는 중복될 수 없습니다.", nameof(source));
                result[index] = id;
            }
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    /// <summary>경기당 최대 두 장의 장착·소모 상태를 보관한다.</summary>
    public sealed class TacticLoadoutState
    {
        private readonly TacticCardDefinition[] _cards;

        public TacticLoadoutState(IReadOnlyList<TacticCardDefinition> cards)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            if (cards.Count > 2)
                throw new ArgumentException("전술카드는 경기당 최대 두 장입니다.", nameof(cards));
            _cards = new TacticCardDefinition[cards.Count];
            int disruptionCount = 0;
            for (int index = 0; index < cards.Count; index++)
            {
                TacticCardDefinition card = cards[index] ?? throw new ArgumentException("null 카드가 있습니다.", nameof(cards));
                for (int previous = 0; previous < index; previous++)
                    if (string.Equals(_cards[previous].CardId, card.CardId, StringComparison.Ordinal))
                        throw new ArgumentException("같은 전술카드를 중복 장착할 수 없습니다.", nameof(cards));
                if (card.IsDisruption) disruptionCount++;
                _cards[index] = card;
            }
            if (disruptionCount > 1)
                throw new ArgumentException("방해 전술카드는 경기당 한 장만 장착할 수 있습니다.", nameof(cards));
            Array.Sort(_cards, CompareCardId);
        }

        public IReadOnlyList<TacticCardDefinition> Cards => _cards;
        public bool IsGameConfirmed { get; private set; }

        /// <summary>조건 발동 여부와 무관하게 경기 확정 시 장착 카드를 소비 상태로 만든다.</summary>
        public void ConfirmGame()
        {
            if (IsGameConfirmed)
                throw new InvalidOperationException("같은 전술 Loadout을 두 번 소비할 수 없습니다.");
            IsGameConfirmed = true;
        }

        private static int CompareCardId(TacticCardDefinition left, TacticCardDefinition right)
        {
            return string.CompareOrdinal(left.CardId, right.CardId);
        }
    }
}

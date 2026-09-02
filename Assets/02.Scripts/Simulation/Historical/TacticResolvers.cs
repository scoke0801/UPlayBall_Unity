using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Historical
{
    /// <summary>공통 Trigger Evaluator가 읽는 한 판정 시점의 불변 경기 상태다.</summary>
    public readonly struct TacticGameState
    {
        public TacticGameState(
            int inning,
            int scoreDifference,
            int batterOrder,
            bool hasRunnerOnSecondOrThird,
            Handedness opponentPitcherHand,
            PitcherRole pitcherRole)
        {
            if (inning <= 0) throw new ArgumentOutOfRangeException(nameof(inning));
            if (batterOrder < 1 || batterOrder > 9) throw new ArgumentOutOfRangeException(nameof(batterOrder));
            Inning = inning;
            ScoreDifference = scoreDifference;
            BatterOrder = batterOrder;
            HasRunnerOnSecondOrThird = hasRunnerOnSecondOrThird;
            OpponentPitcherHand = opponentPitcherHand;
            PitcherRole = pitcherRole;
        }

        public int Inning { get; }
        public int ScoreDifference { get; }
        public int BatterOrder { get; }
        public bool HasRunnerOnSecondOrThird { get; }
        public Handedness OpponentPitcherHand { get; }
        public PitcherRole PitcherRole { get; }
    }

    /// <summary>데이터로 정의한 모든 TriggerCondition을 고정 순서로 평가한다.</summary>
    public sealed class TacticTriggerEvaluator
    {
        public bool IsTriggered(TacticCardDefinition card, TacticGameState state)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            for (int index = 0; index < card.TriggerConditions.Count; index++)
            {
                TacticTriggerCondition condition = card.TriggerConditions[index];
                int actual = GetValue(condition.Field, state);
                if (!Compare(actual, condition)) return false;
            }
            return true;
        }

        private static int GetValue(TacticTriggerField field, TacticGameState state)
        {
            return field switch
            {
                TacticTriggerField.Inning => state.Inning,
                TacticTriggerField.ScoreDifference => state.ScoreDifference,
                TacticTriggerField.AbsoluteRunDifference => Math.Abs(state.ScoreDifference),
                TacticTriggerField.BatterOrder => state.BatterOrder,
                TacticTriggerField.RunnerOnSecondOrThird => state.HasRunnerOnSecondOrThird ? 1 : 0,
                TacticTriggerField.OpponentPitcherHand => (int)state.OpponentPitcherHand,
                TacticTriggerField.PitcherRole => (int)state.PitcherRole,
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
        }

        private static bool Compare(int actual, TacticTriggerCondition condition)
        {
            return condition.Comparison switch
            {
                TacticComparison.Equal => actual == condition.Value,
                TacticComparison.NotEqual => actual != condition.Value,
                TacticComparison.LessOrEqual => actual <= condition.Value,
                TacticComparison.GreaterOrEqual => actual >= condition.Value,
                TacticComparison.BetweenInclusive =>
                    actual >= condition.Value && actual <= condition.MaximumValue,
                _ => throw new ArgumentOutOfRangeException(nameof(condition))
            };
        }
    }

    public enum TacticResolutionStage
    {
        OpponentDebuff = 4,
        AllyBuff = 5
    }

    /// <summary>봉쇄·Counter 이후 실제 효과 레이어로 전달할 카드 한 장의 판정 결과다.</summary>
    public readonly struct TacticResolvedCard
    {
        public TacticResolvedCard(TacticCardDefinition card, bool belongsToHomeTeam, TacticResolutionStage stage)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            BelongsToHomeTeam = belongsToHomeTeam;
            Stage = stage;
        }

        public TacticCardDefinition Card { get; }
        public bool BelongsToHomeTeam { get; }
        public TacticResolutionStage Stage { get; }
    }

    /// <summary>조건→봉쇄→Counter→Debuff→Buff 순서가 끝난 결정론적 카드 결과다.</summary>
    public sealed class TacticResolution
    {
        public TacticResolution(IReadOnlyList<TacticResolvedCard> cards)
        {
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
        }

        public IReadOnlyList<TacticResolvedCard> Cards { get; }
    }

    /// <summary>양 팀 전술카드를 공통 순서로 해석하며 카드별 Resolver 분기를 만들지 않는다.</summary>
    public sealed class TacticCardResolver
    {
        private readonly TacticTriggerEvaluator _triggerEvaluator;

        public TacticCardResolver(TacticTriggerEvaluator triggerEvaluator = null)
        {
            _triggerEvaluator = triggerEvaluator ?? new TacticTriggerEvaluator();
        }

        public TacticResolution Resolve(
            TacticLoadoutState home,
            TacticLoadoutState away,
            TacticGameState state,
            IReadOnlyList<string> blockedCardIds = null)
        {
            int maximumCount = (home?.Cards.Count ?? 0) + (away?.Cards.Count ?? 0);
            var buffer = new TacticResolvedCard[maximumCount];
            int count = ResolveInto(home, away, state, buffer, blockedCardIds);
            if (count == buffer.Length)
                return new TacticResolution(buffer);

            var result = new TacticResolvedCard[count];
            Array.Copy(buffer, result, count);
            return new TacticResolution(result);
        }

        /// <summary>타석 Hot Path에서 호출자가 재사용하는 버퍼에 무할당으로 전술 결과를 쓴다.</summary>
        public int ResolveInto(
            TacticLoadoutState home,
            TacticLoadoutState away,
            TacticGameState state,
            TacticResolvedCard[] output,
            IReadOnlyList<string> blockedCardIds = null)
        {
            ValidateConfirmed(home, nameof(home));
            ValidateConfirmed(away, nameof(away));
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (output.Length < home.Cards.Count + away.Cards.Count)
                throw new ArgumentException("장착 카드 수 이상의 재사용 버퍼가 필요합니다.", nameof(output));

            int count = 0;
            count = AddStageInto(output, count, home, away, state, blockedCardIds, true,
                TacticResolutionStage.OpponentDebuff);
            count = AddStageInto(output, count, away, home, state, blockedCardIds, false,
                TacticResolutionStage.OpponentDebuff);
            count = AddStageInto(output, count, home, away, state, blockedCardIds, true,
                TacticResolutionStage.AllyBuff);
            return AddStageInto(output, count, away, home, state, blockedCardIds, false,
                TacticResolutionStage.AllyBuff);
        }

        private int AddStageInto(
            TacticResolvedCard[] output,
            int count,
            TacticLoadoutState owner,
            TacticLoadoutState opponent,
            TacticGameState state,
            IReadOnlyList<string> blockedCardIds,
            bool belongsToHomeTeam,
            TacticResolutionStage stage)
        {
            for (int index = 0; index < owner.Cards.Count; index++)
            {
                TacticCardDefinition card = owner.Cards[index];
                if (!IsActive(card, state, blockedCardIds) ||
                    IsCountered(card, opponent, state, blockedCardIds))
                    continue;
                bool isDebuff = card.IsDisruption || card.TargetRule == TacticTargetRule.Opponent;
                if (isDebuff != (stage == TacticResolutionStage.OpponentDebuff))
                    continue;
                output[count++] = new TacticResolvedCard(card, belongsToHomeTeam, stage);
            }
            return count;
        }

        private bool IsCountered(
            TacticCardDefinition card,
            TacticLoadoutState opponent,
            TacticGameState state,
            IReadOnlyList<string> blockedCardIds)
        {
            for (int index = 0; index < opponent.Cards.Count; index++)
            {
                TacticCardDefinition counter = opponent.Cards[index];
                if (IsActive(counter, state, blockedCardIds) && counter.Counters(card.CardId))
                    return true;
            }
            return false;
        }

        private bool IsActive(
            TacticCardDefinition card,
            TacticGameState state,
            IReadOnlyList<string> blockedCardIds)
        {
            if (blockedCardIds != null)
            {
                for (int index = 0; index < blockedCardIds.Count; index++)
                    if (string.Equals(blockedCardIds[index], card.CardId, StringComparison.Ordinal))
                        return false;
            }
            return _triggerEvaluator.IsTriggered(card, state);
        }

        private static void ValidateConfirmed(TacticLoadoutState loadout, string parameterName)
        {
            if (loadout == null) throw new ArgumentNullException(parameterName);
            if (!loadout.IsGameConfirmed)
                throw new InvalidOperationException("경기 확정으로 카드가 소비된 뒤에만 전술을 판정할 수 있습니다.");
        }
    }
}

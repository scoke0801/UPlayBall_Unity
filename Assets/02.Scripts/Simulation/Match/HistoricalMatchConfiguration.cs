using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;

namespace Baseball.Simulation.Match
{
    /// <summary>공통 역사 시뮬레이션 규칙을 기존 DetailedMatchEngine 입력에 연결한다.</summary>
    public sealed class HistoricalMatchConfiguration
    {
        private readonly string[] _blockedTacticCardIds;
        private readonly double[] _leverageIndexByTier;

        public HistoricalMatchConfiguration(
            PositionAssignmentRule positionAssignmentRule = null,
            BullpenUsagePolicy bullpenUsagePolicy = null,
            IReadOnlyList<double> leverageIndexByTier = null,
            TacticLoadoutState awayTacticLoadout = null,
            TacticLoadoutState homeTacticLoadout = null,
            IReadOnlyList<string> blockedTacticCardIds = null)
        {
            if (bullpenUsagePolicy != null)
            {
                if (leverageIndexByTier == null || leverageIndexByTier.Count != 4)
                {
                    throw new ArgumentException(
                        "BullpenUsagePolicy를 연결하려면 Low~Critical 네 단계 LeverageIndex가 필요합니다.",
                        nameof(leverageIndexByTier));
                }

                _leverageIndexByTier = new double[leverageIndexByTier.Count];
                for (int index = 0; index < leverageIndexByTier.Count; index++)
                {
                    double value = leverageIndexByTier[index];
                    if (value < 0d || double.IsNaN(value))
                        throw new ArgumentOutOfRangeException(nameof(leverageIndexByTier));
                    _leverageIndexByTier[index] = value;
                }
            }
            else
            {
                _leverageIndexByTier = Array.Empty<double>();
            }

            if (awayTacticLoadout != null || homeTacticLoadout != null)
            {
                AwayTacticLoadout = awayTacticLoadout ?? CreateConfirmedEmptyLoadout();
                HomeTacticLoadout = homeTacticLoadout ?? CreateConfirmedEmptyLoadout();
                ValidateConfirmed(AwayTacticLoadout, nameof(awayTacticLoadout));
                ValidateConfirmed(HomeTacticLoadout, nameof(homeTacticLoadout));
            }

            PositionAssignmentRule = positionAssignmentRule;
            BullpenUsagePolicy = bullpenUsagePolicy;
            _blockedTacticCardIds = CopyStableIds(blockedTacticCardIds);
        }

        public PositionAssignmentRule PositionAssignmentRule { get; }
        public BullpenUsagePolicy BullpenUsagePolicy { get; }
        public TacticLoadoutState AwayTacticLoadout { get; }
        public TacticLoadoutState HomeTacticLoadout { get; }
        public IReadOnlyList<string> BlockedTacticCardIds => _blockedTacticCardIds;
        public bool HasTactics => AwayTacticLoadout != null;

        /// <summary>Balance가 정의한 LeverageTier별 실제 Index를 반환한다.</summary>
        public double GetLeverageIndex(LeverageTier tier)
        {
            if (_leverageIndexByTier.Length == 0)
                throw new InvalidOperationException("LeverageIndex Balance가 연결되지 않았습니다.");
            return _leverageIndexByTier[(int)tier];
        }

        private static TacticLoadoutState CreateConfirmedEmptyLoadout()
        {
            var loadout = new TacticLoadoutState(Array.Empty<TacticCardDefinition>());
            loadout.ConfirmGame();
            return loadout;
        }

        private static void ValidateConfirmed(TacticLoadoutState loadout, string parameterName)
        {
            if (!loadout.IsGameConfirmed)
                throw new ArgumentException("경기 확정으로 소비된 TacticLoadout만 연결할 수 있습니다.", parameterName);
        }

        private static string[] CopyStableIds(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                string id = source[index]?.Trim();
                if (string.IsNullOrEmpty(id))
                    throw new ArgumentException("봉쇄 CardId는 비어 있을 수 없습니다.", nameof(source));
                for (int previous = 0; previous < index; previous++)
                    if (string.Equals(result[previous], id, StringComparison.Ordinal))
                        throw new ArgumentException("봉쇄 CardId는 중복될 수 없습니다.", nameof(source));
                result[index] = id;
            }
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    /// <summary>한 타석에 적용할 전술과 배치 비용을 원본 능력치와 분리해 보관한다.</summary>
    internal readonly struct MatchPlateAppearanceModifiers
    {
        private readonly int[] _batter;
        private readonly int[] _pitcher;
        private readonly int[] _defense;

        public MatchPlateAppearanceModifiers(int[] batter, int[] pitcher, int[] defense)
        {
            _batter = batter;
            _pitcher = pitcher;
            _defense = defense;
        }

        public int GetBatter(PlayerAbility ability) => _batter == null ? 0 : _batter[(int)ability];
        public int GetPitcher(PlayerAbility ability) => _pitcher == null ? 0 : _pitcher[(int)ability];
        public int GetDefense(PlayerAbility ability) => _defense == null ? 0 : _defense[(int)ability];
    }

    /// <summary>TacticCardResolver 출력을 DetailedMatchEngine의 실효 능력치 입력으로 변환한다.</summary>
    internal sealed class DetailedMatchTacticRuntime
    {
        private readonly HistoricalMatchConfiguration _configuration;
        private readonly TacticCardResolver _resolver = new TacticCardResolver();
        private readonly ActiveTactic[] _active = new ActiveTactic[4];
        private readonly TacticResolvedCard[] _currentCards = new TacticResolvedCard[4];
        private readonly bool[] _currentCountered = new bool[4];
        private readonly int[] _batterModifiers = new int[(int)PlayerAbility.Count];
        private readonly int[] _pitcherModifiers = new int[(int)PlayerAbility.Count];
        private readonly int[] _defenseModifiers = new int[(int)PlayerAbility.Count];
        private int _activeCount;

        public DetailedMatchTacticRuntime(HistoricalMatchConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MatchPlateAppearanceModifiers Resolve(
            int inning,
            InningHalf half,
            int scoreDifference,
            int batterOrder,
            bool hasRunnerOnSecondOrThird,
            Player activePitcher,
            PitcherRole activePitcherRole)
        {
            if (_configuration?.HasTactics != true)
                return default;

            int pitcherId = activePitcher?.PlayerId ?? 0;
            RemoveExpired(inning, pitcherId);
            var gameState = new TacticGameState(
                inning,
                scoreDifference,
                batterOrder,
                hasRunnerOnSecondOrThird,
                activePitcher.ThrowingHand,
                activePitcherRole);
            int currentCount = _resolver.ResolveInto(
                _configuration.HomeTacticLoadout,
                _configuration.AwayTacticLoadout,
                gameState,
                _currentCards,
                _configuration.BlockedTacticCardIds);

            Array.Clear(_currentCountered, 0, _currentCountered.Length);
            ResolvePersistentCounters(currentCount);
            Array.Clear(_batterModifiers, 0, _batterModifiers.Length);
            Array.Clear(_pitcherModifiers, 0, _pitcherModifiers.Length);
            Array.Clear(_defenseModifiers, 0, _defenseModifiers.Length);
            bool offenseIsHome = half == InningHalf.Bottom;

            for (int index = 0; index < _activeCount; index++)
                Apply(
                    _active[index].ResolvedCard,
                    offenseIsHome,
                    _batterModifiers,
                    _pitcherModifiers,
                    _defenseModifiers);

            for (int index = 0; index < currentCount; index++)
            {
                if (_currentCountered[index])
                    continue;
                TacticResolvedCard resolved = _currentCards[index];
                if (IsActive(resolved))
                    continue;
                Apply(
                    resolved,
                    offenseIsHome,
                    _batterModifiers,
                    _pitcherModifiers,
                    _defenseModifiers);
                AddPersistent(resolved, inning, pitcherId);
            }

            return new MatchPlateAppearanceModifiers(
                _batterModifiers,
                _pitcherModifiers,
                _defenseModifiers);
        }

        private void ResolvePersistentCounters(int currentCount)
        {
            for (int currentIndex = 0; currentIndex < currentCount; currentIndex++)
            {
                TacticResolvedCard currentCard = _currentCards[currentIndex];
                for (int activeIndex = _activeCount - 1; activeIndex >= 0; activeIndex--)
                {
                    TacticResolvedCard activeCard = _active[activeIndex].ResolvedCard;
                    if (activeCard.BelongsToHomeTeam == currentCard.BelongsToHomeTeam)
                        continue;
                    if (activeCard.Card.Counters(currentCard.Card.CardId))
                        _currentCountered[currentIndex] = true;
                    if (currentCard.Card.Counters(activeCard.Card.CardId))
                        RemoveAt(activeIndex);
                }
            }
        }

        private void AddPersistent(TacticResolvedCard resolved, int inning, int pitcherId)
        {
            if (resolved.Card.DurationRule == TacticDurationRule.CurrentPlateAppearance)
                return;
            for (int index = 0; index < _activeCount; index++)
                if (string.Equals(_active[index].ResolvedCard.Card.CardId, resolved.Card.CardId, StringComparison.Ordinal) &&
                    _active[index].ResolvedCard.BelongsToHomeTeam == resolved.BelongsToHomeTeam)
                    return;
            if (_activeCount >= _active.Length)
                throw new InvalidOperationException("경기 중 활성 전술 수가 Loadout 계약을 초과했습니다.");
            _active[_activeCount++] = new ActiveTactic(resolved, inning, pitcherId);
        }

        private bool IsActive(TacticResolvedCard resolved)
        {
            for (int index = 0; index < _activeCount; index++)
                if (string.Equals(
                        _active[index].ResolvedCard.Card.CardId,
                        resolved.Card.CardId,
                        StringComparison.Ordinal) &&
                    _active[index].ResolvedCard.BelongsToHomeTeam == resolved.BelongsToHomeTeam)
                    return true;
            return false;
        }

        private void RemoveExpired(int inning, int pitcherId)
        {
            for (int index = _activeCount - 1; index >= 0; index--)
            {
                ActiveTactic active = _active[index];
                bool expired = active.ResolvedCard.Card.DurationRule switch
                {
                    TacticDurationRule.UntilInningEnd => active.ActivationInning != inning,
                    TacticDurationRule.UntilPitcherRemoved => active.ActivationPitcherId != pitcherId,
                    _ => false
                };
                if (expired)
                    RemoveAt(index);
            }
        }

        private void RemoveAt(int index)
        {
            _activeCount--;
            for (int move = index; move < _activeCount; move++)
                _active[move] = _active[move + 1];
            _active[_activeCount] = default;
        }

        private static void Apply(
            TacticResolvedCard resolved,
            bool offenseIsHome,
            int[] batter,
            int[] pitcher,
            int[] defense)
        {
            bool affectedIsHome = resolved.Stage == TacticResolutionStage.AllyBuff
                ? resolved.BelongsToHomeTeam
                : !resolved.BelongsToHomeTeam;
            bool affectsOffense = affectedIsHome == offenseIsHome;
            for (int index = 0; index < resolved.Card.StatModifiers.Count; index++)
            {
                TacticStatModifier modifier = resolved.Card.StatModifiers[index];
                if (AffectsBatter(resolved.Card.TargetRule, affectsOffense, modifier.Ability))
                    batter[(int)modifier.Ability] += modifier.Amount;
                if (AffectsPitcher(resolved.Card.TargetRule, affectsOffense, modifier.Ability))
                    pitcher[(int)modifier.Ability] += modifier.Amount;
                if (AffectsDefense(resolved.Card.TargetRule, affectsOffense, modifier.Ability))
                    defense[(int)modifier.Ability] += modifier.Amount;
            }
        }

        private static bool AffectsBatter(TacticTargetRule target, bool affectsOffense, PlayerAbility ability)
        {
            if (!affectsOffense || !PlayerAbilityCatalog.IsBatterAbility(ability))
                return false;
            return target is TacticTargetRule.CurrentBatter or TacticTargetRule.BattingTeam or
                TacticTargetRule.Opponent;
        }

        private static bool AffectsPitcher(TacticTargetRule target, bool affectsOffense, PlayerAbility ability)
        {
            if (affectsOffense || !PlayerAbilityCatalog.IsPitcherAbility(ability))
                return false;
            return target is TacticTargetRule.CurrentPitcher or TacticTargetRule.PitchingTeam or
                TacticTargetRule.Bullpen or TacticTargetRule.Opponent;
        }

        private static bool AffectsDefense(TacticTargetRule target, bool affectsOffense, PlayerAbility ability)
        {
            if (affectsOffense || ability is not (PlayerAbility.Defense or PlayerAbility.Arm))
                return false;
            return target is TacticTargetRule.PitchingTeam or TacticTargetRule.Opponent;
        }

        private readonly struct ActiveTactic
        {
            public ActiveTactic(TacticResolvedCard resolvedCard, int activationInning, int activationPitcherId)
            {
                ResolvedCard = resolvedCard;
                ActivationInning = activationInning;
                ActivationPitcherId = activationPitcherId;
            }

            public TacticResolvedCard ResolvedCard { get; }
            public int ActivationInning { get; }
            public int ActivationPitcherId { get; }
        }
    }
}

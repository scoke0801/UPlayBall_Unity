using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Simulation.Historical
{
    /// <summary>타선 궁합 계산에 필요한 선수 인물과 기존 타격 능력치만 묶는다.</summary>
    public readonly struct LineupChemistryPlayer
    {
        public LineupChemistryPlayer(string playerPersonId, BatterAttributes attributes)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", nameof(playerPersonId));
            PlayerPersonId = playerPersonId.Trim();
            Attributes = attributes;
        }

        public string PlayerPersonId { get; }
        public BatterAttributes Attributes { get; }
    }

    /// <summary>인접한 두 타순의 Familiarity와 스타일 근거를 설명한다.</summary>
    public readonly struct LineupChemistryEdge
    {
        public LineupChemistryEdge(
            PlayerPersonPairKey pair,
            int familiarity,
            HitterChemistryStyle firstStyle,
            HitterChemistryStyle secondStyle,
            double score)
        {
            Pair = pair;
            Familiarity = familiarity;
            FirstStyle = firstStyle;
            SecondStyle = secondStyle;
            Score = score;
        }

        public PlayerPersonPairKey Pair { get; }
        public int Familiarity { get; }
        public HitterChemistryStyle FirstStyle { get; }
        public HitterChemistryStyle SecondStyle { get; }
        public double Score { get; }
    }

    /// <summary>한 타자의 인접 Edge 평균과 Condition 변경량을 제공한다.</summary>
    public readonly struct LineupChemistryPlayerResult
    {
        public LineupChemistryPlayerResult(string playerPersonId, double score, int conditionModifier)
        {
            PlayerPersonId = playerPersonId ?? throw new ArgumentNullException(nameof(playerPersonId));
            Score = score;
            ConditionModifier = conditionModifier;
        }

        public string PlayerPersonId { get; }
        public double Score { get; }
        public int ConditionModifier { get; }
    }

    /// <summary>경기 전 한 번 계산해 재사용할 타선 궁합 snapshot이다.</summary>
    public sealed class LineupChemistryResult
    {
        private readonly LineupChemistryEdge[] _edges;
        private readonly LineupChemistryPlayerResult[] _players;

        public LineupChemistryResult(
            IReadOnlyList<LineupChemistryEdge> edges,
            IReadOnlyList<LineupChemistryPlayerResult> players)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            if (players == null) throw new ArgumentNullException(nameof(players));
            _edges = new LineupChemistryEdge[edges.Count];
            _players = new LineupChemistryPlayerResult[players.Count];
            for (int index = 0; index < edges.Count; index++) _edges[index] = edges[index];
            for (int index = 0; index < players.Count; index++) _players[index] = players[index];
        }

        public IReadOnlyList<LineupChemistryEdge> Edges => _edges;
        public IReadOnlyList<LineupChemistryPlayerResult> Players => _players;

        public int GetConditionModifier(string playerPersonId)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
                return 0;
            for (int index = 0; index < _players.Length; index++)
            {
                if (string.Equals(_players[index].PlayerPersonId, playerPersonId, StringComparison.Ordinal))
                    return _players[index].ConditionModifier;
            }
            return 0;
        }
    }

    /// <summary>9인 타순의 인접 8개 Pair만 평가해 작은 Condition 변경량으로 변환한다.</summary>
    public sealed class LineupChemistryResolver
    {
        private const int BattingOrderSize = 9;
        private readonly ConditionChemistryBalanceTable _balance;

        public LineupChemistryResolver(ConditionChemistryBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public LineupChemistryResult Resolve(
            string teamSeasonKey,
            IReadOnlyList<LineupChemistryPlayer> battingOrder,
            TeamChemistryFamiliarityState familiarity)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (battingOrder == null || battingOrder.Count != BattingOrderSize)
                throw new ArgumentException("타선 궁합에는 정확히 9명의 타순이 필요합니다.", nameof(battingOrder));
            if (familiarity == null) throw new ArgumentNullException(nameof(familiarity));
            if (!string.Equals(teamSeasonKey.Trim(), familiarity.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("다른 TeamSeason의 Familiarity는 사용할 수 없습니다.", nameof(familiarity));

            var edges = new LineupChemistryEdge[BattingOrderSize - 1];
            var edgeScores = new double[BattingOrderSize - 1];
            for (int index = 0; index < battingOrder.Count; index++)
            {
                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(
                        battingOrder[previous].PlayerPersonId,
                        battingOrder[index].PlayerPersonId,
                        StringComparison.Ordinal))
                    {
                        throw new ArgumentException("한 선수는 타순에 한 번만 들어갈 수 있습니다.", nameof(battingOrder));
                    }
                }
            }

            for (int index = 0; index < edges.Length; index++)
            {
                LineupChemistryPlayer first = battingOrder[index];
                LineupChemistryPlayer second = battingOrder[index + 1];
                var pair = new PlayerPersonPairKey(first.PlayerPersonId, second.PlayerPersonId);
                int familiarityValue = familiarity.GetLineupFamiliarity(pair);
                HitterChemistryStyle firstStyle = DeriveStyle(first.Attributes);
                HitterChemistryStyle secondStyle = DeriveStyle(second.Attributes);
                double score = ResolveFamiliarityScore(familiarityValue) + ResolveStyleScore(firstStyle, secondStyle);
                edgeScores[index] = score;
                edges[index] = new LineupChemistryEdge(pair, familiarityValue, firstStyle, secondStyle, score);
            }

            var players = new LineupChemistryPlayerResult[BattingOrderSize];
            for (int index = 0; index < battingOrder.Count; index++)
            {
                double score;
                if (index == 0)
                    score = edgeScores[0];
                else if (index == battingOrder.Count - 1)
                    score = edgeScores[edgeScores.Length - 1];
                else
                    score = (edgeScores[index - 1] + edgeScores[index]) * 0.5d;

                players[index] = new LineupChemistryPlayerResult(
                    battingOrder[index].PlayerPersonId,
                    score,
                    ConvertScoreToConditionModifier(score));
            }

            return new LineupChemistryResult(edges, players);
        }

        /// <summary>별도 영구 Stat 없이 Contact·Speed·Power의 상대 우세만 스타일로 읽는다.</summary>
        public HitterChemistryStyle DeriveStyle(BatterAttributes attributes)
        {
            int tableSetterValue = (attributes.Contact + attributes.Speed) / 2;
            if (tableSetterValue - attributes.Power >= _balance.TableSetterLeadThreshold)
                return HitterChemistryStyle.TableSetterLike;
            if (attributes.Power - tableSetterValue >= _balance.PowerLeadThreshold)
                return HitterChemistryStyle.PowerLike;
            return HitterChemistryStyle.Balanced;
        }

        private double ResolveFamiliarityScore(int familiarity)
        {
            int bounded = familiarity > _balance.FamiliarityCap ? _balance.FamiliarityCap : familiarity;
            return bounded * 100d / _balance.FamiliarityCap * _balance.FamiliarityScoreWeight;
        }

        private double ResolveStyleScore(HitterChemistryStyle first, HitterChemistryStyle second)
        {
            bool complements = first == HitterChemistryStyle.TableSetterLike && second == HitterChemistryStyle.PowerLike ||
                               first == HitterChemistryStyle.PowerLike && second == HitterChemistryStyle.TableSetterLike;
            if (complements)
                return _balance.StyleComplementScore;
            if (first != HitterChemistryStyle.Balanced && first == second)
                return -_balance.StyleConflictScore;
            return 0d;
        }

        private int ConvertScoreToConditionModifier(double score)
        {
            int levelDelta = score >= _balance.GoodScoreThreshold
                ? _balance.MaximumChemistryLevelDelta
                : score <= _balance.BadScoreThreshold
                    ? -_balance.MaximumChemistryLevelDelta
                    : 0;
            return levelDelta * _balance.ConditionLevelStep;
        }
    }

    /// <summary>현재 투수와 포수의 Familiarity·Handling·Stability 근거를 제공한다.</summary>
    public readonly struct BatteryChemistryResult
    {
        public BatteryChemistryResult(
            PlayerPersonPairKey pair,
            int familiarity,
            double familiarityScore,
            double catcherHandlingScore,
            double stabilityScore,
            int pitcherConditionModifier)
        {
            Pair = pair;
            Familiarity = familiarity;
            FamiliarityScore = familiarityScore;
            CatcherHandlingScore = catcherHandlingScore;
            StabilityScore = stabilityScore;
            PitcherConditionModifier = pitcherConditionModifier;
        }

        public PlayerPersonPairKey Pair { get; }
        public int Familiarity { get; }
        public double FamiliarityScore { get; }
        public double CatcherHandlingScore { get; }
        public double StabilityScore { get; }
        public double TotalScore => FamiliarityScore + CatcherHandlingScore + StabilityScore;
        public int PitcherConditionModifier { get; }
    }

    /// <summary>투수나 포수가 바뀔 때 현재 Pair만 다시 계산하는 배터리 궁합 Resolver다.</summary>
    public sealed class BatteryChemistryResolver
    {
        private readonly ConditionChemistryBalanceTable _balance;

        public BatteryChemistryResolver(ConditionChemistryBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public BatteryChemistryResult Resolve(
            string teamSeasonKey,
            string pitcherPersonId,
            PitcherAttributes pitcherAttributes,
            string catcherPersonId,
            BatterAttributes catcherAttributes,
            TeamChemistryFamiliarityState familiarity)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (familiarity == null) throw new ArgumentNullException(nameof(familiarity));
            if (!string.Equals(teamSeasonKey.Trim(), familiarity.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("다른 TeamSeason의 Familiarity는 사용할 수 없습니다.", nameof(familiarity));

            var pair = new PlayerPersonPairKey(pitcherPersonId, catcherPersonId);
            int familiarityValue = familiarity.GetBatteryFamiliarity(pair);
            int bounded = familiarityValue > _balance.FamiliarityCap ? _balance.FamiliarityCap : familiarityValue;
            double familiarityScore = bounded * 100d / _balance.FamiliarityCap * _balance.FamiliarityScoreWeight;
            double handlingScore = (catcherAttributes.Defense - 50) * _balance.CatcherDefenseWeight +
                                   (catcherAttributes.Mental - 50) * _balance.CatcherMentalWeight;
            double stabilityScore = (pitcherAttributes.Mental - 50) * _balance.PitcherMentalStabilityWeight;
            double total = familiarityScore + handlingScore + stabilityScore;
            int levelDelta = total >= _balance.GoodScoreThreshold
                ? _balance.MaximumChemistryLevelDelta
                : total <= _balance.BadScoreThreshold
                    ? -_balance.MaximumChemistryLevelDelta
                    : 0;

            return new BatteryChemistryResult(
                pair,
                familiarityValue,
                familiarityScore,
                handlingScore,
                stabilityScore,
                levelDelta * _balance.ConditionLevelStep);
        }
    }

    /// <summary>Stored Condition과 경기 한정 변경량을 원본 변경 없이 합성한다.</summary>
    public sealed class EffectiveMatchConditionResolver
    {
        public EffectiveMatchCondition Resolve(
            int storedBaseCondition,
            int assignmentModifier,
            int lineupChemistryModifier,
            int batteryChemistryModifier,
            int temporaryModifier)
        {
            return new EffectiveMatchCondition(
                storedBaseCondition,
                assignmentModifier,
                lineupChemistryModifier,
                batteryChemistryModifier,
                temporaryModifier);
        }
    }

    /// <summary>연속 Condition을 모든 능력치에 적용할 작은 공통 경기 보정으로 변환한다.</summary>
    public sealed class MatchConditionRatingResolver
    {
        private readonly ConditionChemistryBalanceTable _balance;

        public MatchConditionRatingResolver(ConditionChemistryBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public int ResolveRatingModifier(int effectiveMatchCondition)
        {
            if (effectiveMatchCondition < 0 || effectiveMatchCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(effectiveMatchCondition));
            double raw = (effectiveMatchCondition - _balance.NeutralMatchCondition) /
                         (double)_balance.ConditionPointsPerRating;
            int modifier = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            int maximum = _balance.MaximumConditionRatingModifier;
            if (modifier < -maximum) return -maximum;
            if (modifier > maximum) return maximum;
            return modifier;
        }
    }

    /// <summary>시설·스태프 modifier를 한 번 합성해 실제 회복량을 계산한다.</summary>
    public sealed class ConditionRecoveryResolver
    {
        public int ResolveRecovery(ConditionRecoveryContext context)
        {
            double recovery = context.BaseRecovery *
                              context.FacilityEfficiencyMultiplier *
                              context.StaffEfficiencyMultiplier;
            if (recovery <= 0d)
                return 0;
            if (recovery >= int.MaxValue)
                return int.MaxValue;
            return (int)Math.Round(recovery, MidpointRounding.AwayFromZero);
        }

        /// <summary>계산된 회복량을 각 선수 원본에 정확히 한 번 적용한다.</summary>
        public int ApplyRecovery(TeamSeasonPlayerStatusState state, ConditionRecoveryContext context)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int recovery = ResolveRecovery(context);
            for (int index = 0; index < state.Players.Count; index++)
                state.Players[index].ChangeCondition(recovery);
            return recovery;
        }
    }

    /// <summary>경기 종료 후 실제로 함께 뛴 Pair만 Familiarity 원본에 기록한다.</summary>
    public sealed class ChemistryFamiliarityRecorder
    {
        private readonly ConditionChemistryBalanceTable _balance;

        public ChemistryFamiliarityRecorder(ConditionChemistryBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public void RecordStartingLineup(
            TeamChemistryFamiliarityState state,
            IReadOnlyList<string> battingOrderPlayerPersonIds)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (battingOrderPlayerPersonIds == null || battingOrderPlayerPersonIds.Count != 9)
                throw new ArgumentException("실제 선발 타순 9명이 필요합니다.", nameof(battingOrderPlayerPersonIds));
            for (int index = 0; index < battingOrderPlayerPersonIds.Count - 1; index++)
            {
                state.RecordLineupPair(
                    new PlayerPersonPairKey(
                        battingOrderPlayerPersonIds[index],
                        battingOrderPlayerPersonIds[index + 1]),
                    _balance.LineupSharedStartGain,
                    _balance.FamiliarityCap);
            }
        }

        public void RecordBatteryInnings(
            TeamChemistryFamiliarityState state,
            string pitcherPersonId,
            string catcherPersonId,
            int sharedInnings)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (sharedInnings < 0) throw new ArgumentOutOfRangeException(nameof(sharedInnings));
            if (sharedInnings == 0)
                return;
            long amount = (long)sharedInnings * _balance.BatterySharedInningGain;
            int boundedAmount = amount >= int.MaxValue ? int.MaxValue : (int)amount;
            state.RecordBatteryPair(
                new PlayerPersonPairKey(pitcherPersonId, catcherPersonId),
                boundedAmount,
                _balance.FamiliarityCap);
        }

        /// <summary>교체 시점을 포함한 실제 공동 수비 아웃을 이닝 단위 Familiarity로 환산한다.</summary>
        public void RecordBatteryOuts(
            TeamChemistryFamiliarityState state,
            string pitcherPersonId,
            string catcherPersonId,
            int sharedDefensiveOuts)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (sharedDefensiveOuts < 0) throw new ArgumentOutOfRangeException(nameof(sharedDefensiveOuts));
            if (sharedDefensiveOuts == 0)
                return;
            double raw = sharedDefensiveOuts * _balance.BatterySharedInningGain /
                         (double)Baseball.Core.Rules.BaseballRules.OutsPerHalfInning;
            int amount = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            if (amount <= 0)
                amount = 1;
            state.RecordBatteryPair(
                new PlayerPersonPairKey(pitcherPersonId, catcherPersonId),
                amount,
                _balance.FamiliarityCap);
        }
    }
}

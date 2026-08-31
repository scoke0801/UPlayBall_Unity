using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    internal sealed class DetailedMatchState
    {
        private PitchOption[] _pitchOptionBuffer = Array.Empty<PitchOption>();

        public DetailedMatchState(
            MatchInput input,
            IMatchEventSink eventSink,
            PitcherFatigueResolver fatigueResolver,
            MatchExecutionProfile executionProfile)
        {
            Input = input;
            Away = new DetailedTeamGameState(input.AwayRoster, fatigueResolver);
            Home = new DetailedTeamGameState(input.HomeRoster, fatigueResolver);
            EventSink = eventSink;
            RecordsEvents = executionProfile.EventMode == MatchEventMode.Full;
            Trace = executionProfile.DecisionTraceMode == MatchDecisionTraceMode.Full
                ? new DecisionTrace()
                : null;
            RecentPitchBuffer = new PitchType[8];
        }

        public MatchInput Input { get; }
        public DetailedTeamGameState Away { get; }
        public DetailedTeamGameState Home { get; }
        public IMatchEventSink EventSink { get; }
        public DecisionTrace Trace { get; }
        public bool RecordsEvents { get; }
        public PitchType[] RecentPitchBuffer { get; }
        public int NextEventSequence { get; set; }
        public int NextDecisionIndex { get; set; }
        public int NextPitchingDecisionIndex { get; set; }
        public int NextPlateAppearanceIndex { get; set; }
        public int NextPitchSelectionIndex { get; set; }
        public int NextSwingExecutionIndex { get; set; }
        public bool IsHighLeverageActive { get; set; }

        /// <summary>한 경기 안에서 모든 타석이 순차 소비하는 투구 옵션 버퍼를 제공한다.</summary>
        public PitchOption[] GetPitchOptionBuffer(Player pitcher)
        {
            int required = PitchExecutionResolver.GetRequiredPitchOptionCapacity(pitcher);
            if (_pitchOptionBuffer.Length < required)
                _pitchOptionBuffer = new PitchOption[required];
            return _pitchOptionBuffer;
        }
    }

    internal sealed class DetailedTeamGameState
    {
        private readonly Player[] _activeBatters;
        private readonly PlayerPosition[] _activePositions;
        private readonly int[] _battingLineIndices;
        private readonly bool[] _benchAvailable;
        private readonly PitcherGameState[] _pitchers;
        private readonly int[] _highLeverageBatters;
        private readonly int[] _overloadPitches;
        private readonly bool[] _enteredInSaveSituation;

        public DetailedTeamGameState(
            MatchRosterSnapshot roster,
            PitcherFatigueResolver fatigueResolver)
        {
            Roster = roster;
            BoxScore = new TeamBoxScoreBuilder(roster, 16);
            Ledger = new SubstitutionLedger();
            _activeBatters = new Player[roster.StartingLineup.Count];
            _activePositions = new PlayerPosition[roster.StartingLineup.Count];
            _battingLineIndices = new int[roster.StartingLineup.Count];
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                LineupSlot slot = roster.StartingLineup[index];
                _activeBatters[index] = slot.Player;
                _activePositions[index] = slot.FieldingPosition;
                _battingLineIndices[index] = index;
                Ledger.RegisterStarter(slot.Player.PlayerId);
            }

            _benchAvailable = new bool[roster.Bench.Count];
            for (int index = 0; index < _benchAvailable.Length; index++)
                _benchAvailable[index] = true;

            _pitchers = new PitcherGameState[1 + roster.Bullpen.Count];
            _pitchers[0] = fatigueResolver.CreateState(roster.StartingPitcher);
            _pitchers[0].HasEntered = true;
            for (int index = 0; index < roster.Bullpen.Count; index++)
                _pitchers[index + 1] = fatigueResolver.CreateState(roster.Bullpen[index]);
            _highLeverageBatters = new int[_pitchers.Length];
            _overloadPitches = new int[_pitchers.Length];
            _enteredInSaveSituation = new bool[_pitchers.Length];
            ActivePitcherIndex = 0;
            Ledger.RegisterStarter(_pitchers[0].Player.PlayerId);
        }

        public MatchRosterSnapshot Roster { get; }
        public TeamBoxScoreBuilder BoxScore { get; }
        public SubstitutionLedger Ledger { get; }
        public int NextBattingOrderIndex { get; set; }
        public int ActivePitcherIndex { get; private set; }
        public PitcherGameState ActivePitcherState => _pitchers[ActivePitcherIndex];
        public Player ActivePitcher => ActivePitcherState.Player;
        public PlayerPitchingLine ActivePitchingLine => BoxScore.GetPitchingLine(ActivePitcher.PlayerId);
        public DefensiveAlignment Alignment { get; set; }
        public int PitcherCount => _pitchers.Length;

        public DetailedLineupReference GetBatter(int battingOrderIndex)
        {
            return new DetailedLineupReference(
                _activeBatters[battingOrderIndex],
                _activePositions[battingOrderIndex],
                battingOrderIndex,
                _battingLineIndices[battingOrderIndex]);
        }

        public Player GetOnDeckBatter(int battingOrderIndex)
        {
            return _activeBatters[(battingOrderIndex + 1) % _activeBatters.Length];
        }

        public Player GetActiveFielder(PlayerPosition position)
        {
            for (int index = 0; index < _activePositions.Length; index++)
            {
                if (_activePositions[index] == position)
                    return _activeBatters[index];
            }
            return null;
        }

        public Player GetFielderForZone(FieldZone zone, out PlayerPosition position)
        {
            position = zone switch
            {
                FieldZone.Catcher => PlayerPosition.Catcher,
                FieldZone.FirstBase => PlayerPosition.FirstBase,
                FieldZone.SecondBase => PlayerPosition.SecondBase,
                FieldZone.ThirdBase => PlayerPosition.ThirdBase,
                FieldZone.Shortstop => PlayerPosition.Shortstop,
                FieldZone.LeftField or FieldZone.LeftFieldLine => PlayerPosition.LeftField,
                FieldZone.CenterField => PlayerPosition.CenterField,
                FieldZone.RightField or FieldZone.RightFieldLine => PlayerPosition.RightField,
                _ => ActivePitcher.PrimaryPosition
            };
            if (zone == FieldZone.Pitcher)
                return ActivePitcher;
            Player fielder = GetActiveFielder(position);
            if (fielder != null)
                return fielder;
            position = PlayerPosition.CenterField;
            return GetActiveFielder(position) ?? _activeBatters[0];
        }

        public Player GetCatcher()
        {
            return GetActiveFielder(PlayerPosition.Catcher) ?? _activeBatters[0];
        }

        public double CalculateDefenseRating()
        {
            double total = 0d;
            int count = 0;
            for (int index = 0; index < _activeBatters.Length; index++)
            {
                if (_activePositions[index] == PlayerPosition.DesignatedHitter)
                    continue;
                total += _activeBatters[index].BatterAttributes.Defense *
                         _activeBatters[index].GetPositionProficiency(_activePositions[index]) / 100d;
                count++;
            }
            return count == 0 ? 50d : total / count;
        }

        public int CountAvailableRelievers(BullpenManagementBalance balance, bool allowEmergency)
        {
            int count = 0;
            for (int index = 1; index < _pitchers.Length; index++)
            {
                PitcherGameState pitcher = _pitchers[index];
                if (pitcher.HasEntered || pitcher.HasBeenRemoved)
                    continue;
                double recentLoad = pitcher.RosterEntry.RecentWorkload.PreviousDayPitches +
                                    pitcher.RosterEntry.RecentWorkload.TwoDaysAgoPitches *
                                    balance.RecentLoadDayTwoWeight +
                                    pitcher.RosterEntry.RecentWorkload.ThreeDaysAgoPitches *
                                    balance.RecentLoadDayThreeWeight;
                if (allowEmergency || recentLoad < balance.UnavailableRecentLoad)
                    count++;
            }
            return count;
        }

        public double CalculateBullpenFreshness(BullpenManagementBalance balance)
        {
            double total = 0d;
            int count = 0;
            for (int index = 1; index < _pitchers.Length; index++)
            {
                PitcherGameState pitcher = _pitchers[index];
                if (pitcher.HasEntered || pitcher.HasBeenRemoved)
                    continue;
                double load = pitcher.RosterEntry.RecentWorkload.PreviousDayPitches +
                              pitcher.RosterEntry.RecentWorkload.TwoDaysAgoPitches *
                              balance.RecentLoadDayTwoWeight +
                              pitcher.RosterEntry.RecentWorkload.ThreeDaysAgoPitches *
                              balance.RecentLoadDayThreeWeight;
                total += Math.Max(0d, 1d - load / balance.UnavailableRecentLoad);
                count++;
            }
            return count == 0 ? 0d : total / count;
        }

        public int SelectReliever(
            PitcherManagementAi ai,
            BullpenManagementBalance balance,
            LeverageTier leverage,
            int remainingInnings)
        {
            int bestIndex = -1;
            double bestScore = double.MinValue;
            bool hasNormallyAvailable = CountAvailableRelievers(balance, allowEmergency: false) > 0;
            for (int index = 1; index < _pitchers.Length; index++)
            {
                PitcherGameState candidate = _pitchers[index];
                if (candidate.HasEntered || candidate.HasBeenRemoved)
                    continue;
                double recentLoad = candidate.RosterEntry.RecentWorkload.PreviousDayPitches +
                                    candidate.RosterEntry.RecentWorkload.TwoDaysAgoPitches *
                                    balance.RecentLoadDayTwoWeight +
                                    candidate.RosterEntry.RecentWorkload.ThreeDaysAgoPitches *
                                    balance.RecentLoadDayThreeWeight;
                if (hasNormallyAvailable && recentLoad >= balance.UnavailableRecentLoad)
                    continue;
                double score = ai.ScoreReliever(candidate, leverage, remainingInnings, Roster.ManagerProfile);
                if (score > bestScore || Math.Abs(score - bestScore) < 0.0001d &&
                    candidate.Player.PlayerId < _pitchers[bestIndex].Player.PlayerId)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }
            return bestIndex;
        }

        public PitcherGameState ChangePitcher(
            int pitcherIndex,
            int inning,
            InningHalf half,
            PitcherChangeReason reason,
            int inheritedRunners,
            int defensiveLead)
        {
            if (pitcherIndex <= 0 || pitcherIndex >= _pitchers.Length)
                throw new ArgumentOutOfRangeException(nameof(pitcherIndex));
            PitcherGameState removed = ActivePitcherState;
            PitcherGameState entering = _pitchers[pitcherIndex];
            Ledger.Record(new SubstitutionRecord(
                inning,
                half,
                entering.Player.PlayerId,
                removed.Player.PlayerId,
                SubstitutionType.PitchingChange,
                MapReason(reason)));
            removed.HasBeenRemoved = true;
            entering.HasEntered = true;
            entering.InheritedRunners += inheritedRunners;
            _enteredInSaveSituation[pitcherIndex] = defensiveLead > 0 && defensiveLead <= 3;
            ActivePitcherIndex = pitcherIndex;
            PlayerPitchingLine enteringLine = BoxScore.GetPitchingLine(entering.Player.PlayerId);
            enteringLine.IsReliefAppearance = true;
            enteringLine.InheritedRunners += inheritedRunners;
            return removed;
        }

        /// <summary>구원투수가 지켜야 했던 리드를 잃었는지 타석 종료마다 기록한다.</summary>
        public void UpdateReliefDecisionState(int teamRuns, int opponentRuns)
        {
            if (ActivePitcherIndex == 0 || !_enteredInSaveSituation[ActivePitcherIndex])
                return;
            if (teamRuns <= opponentRuns)
                ActivePitchingLine.HasBlownSave = true;
        }

        /// <summary>최종 리드와 등판 순서로 Save와 Hold를 확정한다.</summary>
        public void FinalizeReliefDecisions(bool won, int runMargin)
        {
            if (!won)
                return;
            PlayerPitchingLine finalLine = ActivePitchingLine;
            if (ActivePitcherIndex > 0 &&
                _enteredInSaveSituation[ActivePitcherIndex] &&
                !finalLine.HasBlownSave &&
                finalLine.OutsRecorded >= 3 &&
                runMargin <= 3)
            {
                finalLine.HasSave = true;
            }

            for (int index = 1; index < _pitchers.Length; index++)
            {
                if (index == ActivePitcherIndex || !_enteredInSaveSituation[index])
                    continue;
                PlayerPitchingLine line = BoxScore.GetPitchingLine(_pitchers[index].Player.PlayerId);
                if (line.BattersFaced > 0 && !line.HasBlownSave)
                    line.HasHold = true;
            }
        }

        public bool TryFindPinchHitter(
            int battingOrderIndex,
            int inning,
            LeverageTier leverage,
            out int benchIndex)
        {
            benchIndex = -1;
            if (inning < 6 || leverage < LeverageTier.Medium)
                return false;
            Player current = _activeBatters[battingOrderIndex];
            double currentOffense = GetOffenseValue(current);
            double bestGain = 8d;
            for (int index = 0; index < _benchAvailable.Length; index++)
            {
                if (!_benchAvailable[index]) continue;
                Player candidate = Roster.Bench[index];
                if (candidate.PrimaryPosition != _activePositions[battingOrderIndex]) continue;
                double gain = GetOffenseValue(candidate) - currentOffense;
                if (gain > bestGain || Math.Abs(gain - bestGain) < 0.001d &&
                    (benchIndex < 0 || candidate.PlayerId < Roster.Bench[benchIndex].PlayerId))
                {
                    bestGain = gain;
                    benchIndex = index;
                }
            }
            return benchIndex >= 0;
        }

        public bool TryFindDefensiveReplacement(int battingOrderIndex, out int benchIndex)
        {
            benchIndex = -1;
            Player current = _activeBatters[battingOrderIndex];
            int bestGain = 14;
            for (int index = 0; index < _benchAvailable.Length; index++)
            {
                if (!_benchAvailable[index]) continue;
                Player candidate = Roster.Bench[index];
                if (candidate.PrimaryPosition != _activePositions[battingOrderIndex]) continue;
                int gain = candidate.BatterAttributes.Defense - current.BatterAttributes.Defense;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    benchIndex = index;
                }
            }
            return benchIndex >= 0;
        }

        public bool TryFindPinchRunner(
            int battingOrderIndex,
            int inning,
            int scoreDifference,
            LeverageTier leverage,
            out int benchIndex)
        {
            benchIndex = -1;
            if (inning < 7 || Math.Abs(scoreDifference) > 1 || leverage < LeverageTier.Medium)
                return false;
            Player current = _activeBatters[battingOrderIndex];
            int bestGain = 14;
            for (int index = 0; index < _benchAvailable.Length; index++)
            {
                if (!_benchAvailable[index]) continue;
                Player candidate = Roster.Bench[index];
                // 현재 구현은 다음 수비 이닝의 포지션 유효성을 보장할 수 있는 교체만 허용한다.
                if (candidate.PrimaryPosition != _activePositions[battingOrderIndex]) continue;
                int gain = candidate.BatterAttributes.Speed - current.BatterAttributes.Speed;
                if (gain > bestGain || gain == bestGain &&
                    (benchIndex < 0 || candidate.PlayerId < Roster.Bench[benchIndex].PlayerId))
                {
                    bestGain = gain;
                    benchIndex = index;
                }
            }
            return benchIndex >= 0;
        }

        public Player SubstitutePositionPlayer(
            int battingOrderIndex,
            int benchIndex,
            int inning,
            InningHalf half,
            SubstitutionType type,
            DecisionReasonCode reason)
        {
            if (benchIndex < 0 || benchIndex >= _benchAvailable.Length || !_benchAvailable[benchIndex])
                throw new InvalidOperationException("사용 가능한 벤치 선수가 아닙니다.");
            Player entering = Roster.Bench[benchIndex];
            Player leaving = _activeBatters[battingOrderIndex];
            if (entering.PrimaryPosition != _activePositions[battingOrderIndex])
                throw new InvalidOperationException("교체 뒤 수비 포지션을 합법적으로 채울 수 없습니다.");
            Ledger.Record(new SubstitutionRecord(
                inning,
                half,
                entering.PlayerId,
                leaving.PlayerId,
                type,
                reason));
            _activeBatters[battingOrderIndex] = entering;
            _battingLineIndices[battingOrderIndex] = Roster.StartingLineup.Count + benchIndex;
            _benchAvailable[benchIndex] = false;
            return leaving;
        }

        public void RecordDefensiveOut()
        {
            for (int index = 0; index < _activeBatters.Length; index++)
            {
                if (_activePositions[index] == PlayerPosition.DesignatedHitter)
                    continue;
                BoxScore.GetFieldingLineByPlayer(_activeBatters[index].PlayerId).DefensiveOuts++;
            }
            BoxScore.GetFieldingLineByPlayer(ActivePitcher.PlayerId).DefensiveOuts++;
        }

        public void RecordHighLeverageBatter()
        {
            _highLeverageBatters[ActivePitcherIndex]++;
        }

        public void RecordOverloadPitch()
        {
            _overloadPitches[ActivePitcherIndex]++;
        }

        public void RecordInheritedRunnerScored(int responsiblePitcherId)
        {
            if (responsiblePitcherId == ActivePitcher.PlayerId)
                return;
            ActivePitcherState.InheritedRunnersScored++;
            ActivePitchingLine.InheritedRunnersScored++;
        }

        public PitcherUsageReport[] BuildUsageReports()
        {
            int count = 0;
            for (int index = 0; index < _pitchers.Length; index++)
            {
                if (_pitchers[index].HasEntered)
                    count++;
            }
            var result = new PitcherUsageReport[count];
            int resultIndex = 0;
            for (int index = 0; index < _pitchers.Length; index++)
            {
                PitcherGameState pitcher = _pitchers[index];
                if (!pitcher.HasEntered) continue;
                result[resultIndex++] = new PitcherUsageReport(
                    pitcher.Player.PlayerId,
                    pitcher.PitchCount,
                    _highLeverageBatters[index],
                    _overloadPitches[index],
                    pitcher.InningsStarted,
                    pitcher.Role,
                    pitcher.InheritedRunners,
                    pitcher.InheritedRunnersScored);
            }
            return result;
        }

        private static double GetOffenseValue(Player player)
        {
            return player.BatterAttributes.Contact * 0.50d +
                   player.BatterAttributes.Power * 0.38d +
                   player.BatterAttributes.Mental * 0.12d;
        }

        private static DecisionReasonCode MapReason(PitcherChangeReason reason)
        {
            return reason switch
            {
                PitcherChangeReason.Fatigue => DecisionReasonCode.Fatigue,
                PitcherChangeReason.PitchLimit => DecisionReasonCode.PitchLimit,
                PitcherChangeReason.TimesThroughOrder => DecisionReasonCode.TimesThroughOrder,
                PitcherChangeReason.Performance => DecisionReasonCode.Performance,
                PitcherChangeReason.HighLeverage => DecisionReasonCode.HighLeverage,
                PitcherChangeReason.Matchup => DecisionReasonCode.Matchup,
                PitcherChangeReason.Injury => DecisionReasonCode.Injury,
                PitcherChangeReason.ScheduledUsage => DecisionReasonCode.ScheduledUsage,
                PitcherChangeReason.DefensiveStrategy => DecisionReasonCode.DefensiveStrategy,
                _ => DecisionReasonCode.Emergency
            };
        }
    }

    internal readonly struct DetailedLineupReference
    {
        public DetailedLineupReference(
            Player player,
            PlayerPosition position,
            int battingOrderIndex,
            int battingLineIndex)
        {
            Player = player;
            Position = position;
            BattingOrderIndex = battingOrderIndex;
            BattingLineIndex = battingLineIndex;
        }

        public Player Player { get; }
        public PlayerPosition Position { get; }
        public int BattingOrderIndex { get; }
        public int BattingLineIndex { get; }
    }

    internal readonly struct DetailedBaseRunner
    {
        public DetailedBaseRunner(
            Player player,
            int battingLineIndex,
            int responsiblePitcherId,
            bool isUnearned)
        {
            Player = player;
            BattingLineIndex = battingLineIndex;
            ResponsiblePitcherId = responsiblePitcherId;
            IsUnearned = isUnearned;
        }

        public Player Player { get; }
        public int BattingLineIndex { get; }
        public int ResponsiblePitcherId { get; }
        public bool IsUnearned { get; }
        public bool IsOccupied => Player != null;
    }

    internal sealed class DetailedBaseState
    {
        public DetailedBaseRunner First { get; set; }
        public DetailedBaseRunner Second { get; set; }
        public DetailedBaseRunner Third { get; set; }
        public BaseStateSnapshot Snapshot => new BaseStateSnapshot(
            First.IsOccupied,
            Second.IsOccupied,
            Third.IsOccupied);

        public int CountOccupied()
        {
            return (First.IsOccupied ? 1 : 0) + (Second.IsOccupied ? 1 : 0) + (Third.IsOccupied ? 1 : 0);
        }

        public void Clear()
        {
            First = default;
            Second = default;
            Third = default;
        }

        public bool ContainsRunner(int playerId)
        {
            return First.IsOccupied && First.Player.PlayerId == playerId ||
                   Second.IsOccupied && Second.Player.PlayerId == playerId ||
                   Third.IsOccupied && Third.Player.PlayerId == playerId;
        }

        public bool ReplaceRunner(int leavingPlayerId, Player entering, int battingLineIndex)
        {
            if (entering == null) throw new ArgumentNullException(nameof(entering));
            if (First.IsOccupied && First.Player.PlayerId == leavingPlayerId)
            {
                First = Replace(First, entering, battingLineIndex);
                return true;
            }
            if (Second.IsOccupied && Second.Player.PlayerId == leavingPlayerId)
            {
                Second = Replace(Second, entering, battingLineIndex);
                return true;
            }
            if (Third.IsOccupied && Third.Player.PlayerId == leavingPlayerId)
            {
                Third = Replace(Third, entering, battingLineIndex);
                return true;
            }
            return false;
        }

        private static DetailedBaseRunner Replace(
            in DetailedBaseRunner current,
            Player entering,
            int battingLineIndex)
        {
            return new DetailedBaseRunner(
                entering,
                battingLineIndex,
                current.ResponsiblePitcherId,
                current.IsUnearned);
        }
    }

    internal sealed class EarnedRunTracker
    {
        public int PotentialOutsWithoutErrors { get; private set; }

        public void RecordActualOut()
        {
            PotentialOutsWithoutErrors++;
        }

        public void RecordRoutineError()
        {
            PotentialOutsWithoutErrors++;
        }

        public bool IsEarned(in DetailedBaseRunner runner)
        {
            return !runner.IsUnearned && PotentialOutsWithoutErrors < 3;
        }
    }
}

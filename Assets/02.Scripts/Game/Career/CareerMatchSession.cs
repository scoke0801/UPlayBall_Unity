using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career.Narrative;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 경기 준비와 진행 방식의 현재 단계를 정의한다.
    /// </summary>
    public enum CareerMatchPhase
    {
        Preparation = 0,
        Playing = 1,
        Completed = 2
    }

    /// <summary>
    /// 같은 경기 이벤트를 어떤 관전 흐름으로 소비할지 정의한다.
    /// </summary>
    public enum CareerMatchMode
    {
        InterveneOnPlayer = 0,
        PlayerFocus = InterveneOnPlayer,
        ResultsOnly = 1,
        FullGameWatch = 2,
        PlayerFocusAutomatic = 3
    }

    /// <summary>
    /// 선택 기록을 같은 Seed 경기 위에 재생해 타석별 중단과 복구를 제공한다.
    /// </summary>
    public sealed class CareerMatchSession
    {
        private const int MaximumAutomaticDecisions = 256;

        private readonly BalanceTable _balance;
        private readonly List<BattingApproach> _decisions = new List<BattingApproach>(32);
        private readonly List<PitchingApproach> _pitchingDecisions = new List<PitchingApproach>(32);
        private readonly CareerGameSettings _gameSettings;
        private MatchSimulationProgress _progress;

        public CareerMatchSession(
            ScheduledGameState scheduledGame,
            MatchInput input,
            DateTime gameDate,
            int controlledPlayerId,
            PlayerGameRole playerRole,
            CompetitionScope competitionScope,
            BalanceTable balance,
            int conditionBefore,
            int managerEvaluationBefore,
            MatchNarrativeBaseline narrativeBaseline,
            CareerGameSettings gameSettings = null)
        {
            ScheduledGame = scheduledGame ?? throw new ArgumentNullException(nameof(scheduledGame));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            GameDate = gameDate;
            if (controlledPlayerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(controlledPlayerId));
            ControlledPlayerId = controlledPlayerId;
            PlayerRole = playerRole;
            CompetitionScope = competitionScope;
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            ConditionBefore = conditionBefore;
            ManagerEvaluationBefore = managerEvaluationBefore;
            NarrativeBaseline = narrativeBaseline ??
                                throw new ArgumentNullException(nameof(narrativeBaseline));
            _gameSettings = gameSettings?.Clone() ?? CareerGameSettings.CreateDefault();
            Phase = CareerMatchPhase.Preparation;
        }

        public ScheduledGameState ScheduledGame { get; }
        public MatchInput Input { get; }
        public DateTime GameDate { get; }
        public int ControlledPlayerId { get; }
        public PlayerGameRole PlayerRole { get; }
        public CompetitionScope CompetitionScope { get; }
        public CareerMatchPhase Phase { get; private set; }
        public CareerMatchMode Mode { get; private set; }
        public MatchDecisionRequest? PendingDecision => _progress?.PendingDecision;
        public MatchPitchingDecisionRequest? PendingPitchingDecision => _progress?.PendingPitchingDecision;
        public IReadOnlyList<MatchEvent> Events => _progress?.Events ?? Array.Empty<MatchEvent>();
        public MatchResult MatchResult => _progress?.Result;
        public bool IsComplete => Phase == CareerMatchPhase.Completed;
        public bool IsCommitted { get; private set; }
        public bool CanReceiveBattingDecisions =>
            PlayerRole == PlayerGameRole.StartingBatter || HasControlledBenchSubstitution();
        public bool CanReceivePitchingDecisions =>
            PlayerRole is PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher;
        public int ConditionBefore { get; }
        public int ConditionAfter { get; private set; }
        public int ManagerEvaluationBefore { get; }
        public int ManagerEvaluationAfter { get; private set; }
        public CareerGameAdvanceResult? CareerResult { get; private set; }
        public MatchNarrativeBaseline NarrativeBaseline { get; }
        public MatchNarrativeSnapshot NarrativeSnapshot { get; private set; }

        /// <summary>이 경기에서 플레이어가 실제 제출한 타격 방침 횟수를 반환한다.</summary>
        public int GetBattingApproachCount(BattingApproach approach)
        {
            int count = 0;
            for (int index = 0; index < _decisions.Count; index++)
            {
                if (_decisions[index] == approach)
                    count++;
            }
            return count;
        }

        /// <summary>이 경기에서 플레이어가 실제 제출한 투구 방침 횟수를 반환한다.</summary>
        public int GetPitchingApproachCount(PitchingApproach approach)
        {
            int count = 0;
            for (int index = 0; index < _pitchingDecisions.Count; index++)
            {
                if (_pitchingDecisions[index] == approach)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 준비 화면에서 고른 방식으로 경기를 시작한다.
        /// </summary>
        public void Start(CareerMatchMode mode)
        {
            if (Phase != CareerMatchPhase.Preparation)
                throw new InvalidOperationException("이미 시작한 경기입니다.");

            Mode = mode;
            Phase = CareerMatchPhase.Playing;
            if (mode != CareerMatchMode.InterveneOnPlayer ||
                (!CanReceiveBattingDecisions && !CanReceivePitchingDecisions))
            {
                _progress = CreateSimulator(decisionSource: null)
                    .SimulateUntilDecision(Input);
                Phase = CareerMatchPhase.Completed;
                return;
            }

            ReplayToNextDecision();
        }

        /// <summary>
        /// 현재 입력 대기 타석에 타격 방식을 적용하고 다음 입력까지 진행한다.
        /// </summary>
        public void SubmitBattingApproach(BattingApproach approach)
        {
            if (Phase != CareerMatchPhase.Playing || !PendingDecision.HasValue)
                throw new InvalidOperationException("타격 입력을 기다리는 상태가 아닙니다.");

            _decisions.Add(approach);
            ReplayToNextDecision();
        }

        /// <summary>현재 입력 대기 타석에 투구 방침을 적용하고 다음 결정 지점까지 진행한다.</summary>
        public void SubmitPitchingApproach(PitchingApproach approach)
        {
            if (Phase != CareerMatchPhase.Playing || !PendingPitchingDecision.HasValue)
                throw new InvalidOperationException("투구 방침 입력을 기다리는 상태가 아닙니다.");

            _pitchingDecisions.Add(approach);
            ReplayToNextDecision();
        }

        /// <summary>아직 계산하지 않은 다음 선수 결정부터 사용할 기본 방침을 갱신한다.</summary>
        public void UpdateApproaches(BattingApproach battingApproach, PitchingApproach pitchingApproach)
        {
            _gameSettings.SetBattingApproach(battingApproach);
            _gameSettings.SetPitchingApproach(pitchingApproach);
        }

        /// <summary>현재 이닝의 남은 상대 타자를 같은 투구 방침으로 진행하고 다음 이닝에서 멈춘다.</summary>
        public void AutoCompleteCurrentPitchingInning(PitchingApproach approach)
        {
            if (Phase != CareerMatchPhase.Playing || !PendingPitchingDecision.HasValue)
                throw new InvalidOperationException("자동 진행할 투수 이닝이 없습니다.");

            MatchPitchingDecisionRequest startingRequest = PendingPitchingDecision.Value;
            int safety = MaximumAutomaticDecisions;
            while (Phase == CareerMatchPhase.Playing && PendingPitchingDecision.HasValue && safety-- > 0)
            {
                MatchPitchingDecisionRequest current = PendingPitchingDecision.Value;
                if (current.DecisionIndex > startingRequest.DecisionIndex &&
                    (current.Inning != startingRequest.Inning || current.Half != startingRequest.Half))
                {
                    return;
                }
                SubmitPitchingApproach(approach);
            }

            if (safety <= 0)
                throw new InvalidOperationException("투수 이닝 자동 진행 안전 한도를 초과했습니다.");
        }

        /// <summary>
        /// 현재 타석에 경기 전 기본 타격 방침을 적용한다.
        /// </summary>
        public void AutoCompleteCurrentPlateAppearance()
        {
            if (Phase != CareerMatchPhase.Playing || !PendingDecision.HasValue)
                throw new InvalidOperationException("자동 진행할 타석이 없습니다.");

            MatchDecisionRequest startingRequest = PendingDecision.Value;
            int safety = MaximumAutomaticDecisions;
            while (Phase == CareerMatchPhase.Playing && PendingDecision.HasValue && safety-- > 0)
            {
                MatchDecisionRequest current = PendingDecision.Value;
                if (current.DecisionIndex > startingRequest.DecisionIndex && current.PitchNumber == 1)
                    return;
                SubmitBattingApproach(_gameSettings.BattingApproach);
            }

            if (safety <= 0)
                throw new InvalidOperationException("타석 자동 진행 안전 한도를 초과했습니다.");
        }

        /// <summary>
        /// 이미 내린 선택은 유지하고 남은 타석을 균형 타격으로 끝까지 진행한다.
        /// </summary>
        public void AutoCompleteMatch()
        {
            if (Phase != CareerMatchPhase.Playing)
                throw new InvalidOperationException("자동 진행할 경기가 없습니다.");

            int safety = MaximumAutomaticDecisions;
            while (Phase == CareerMatchPhase.Playing && safety-- > 0)
            {
                if (PendingDecision.HasValue)
                    SubmitBattingApproach(_gameSettings.BattingApproach);
                else if (PendingPitchingDecision.HasValue)
                    SubmitPitchingApproach(_gameSettings.PitchingApproach);
                else
                    throw new InvalidOperationException("자동 진행할 선수 결정 지점이 없습니다.");
            }

            if (safety <= 0)
                throw new InvalidOperationException("경기 자동 진행 안전 한도를 초과했습니다.");
        }

        /// <summary>현재 경계를 보존한 채 남은 이벤트를 계산하고 즉시 결과 모드로 전환한다.</summary>
        public void CompleteInstantly()
        {
            if (Phase == CareerMatchPhase.Preparation)
            {
                Start(CareerMatchMode.ResultsOnly);
                return;
            }

            if (Phase == CareerMatchPhase.Playing)
                AutoCompleteMatch();
            Mode = CareerMatchMode.ResultsOnly;
        }

        /// <summary>
        /// 커리어 기록 반영 결과와 경기 전후 상태를 한 번만 연결한다.
        /// </summary>
        public void MarkCommitted(
            CareerGameAdvanceResult result,
            int conditionAfter,
            int managerEvaluationAfter,
            MatchNarrativeSnapshot narrativeSnapshot)
        {
            if (!IsComplete || MatchResult == null)
                throw new InvalidOperationException("완료되지 않은 경기는 기록할 수 없습니다.");
            if (IsCommitted)
                throw new InvalidOperationException("이미 기록한 경기입니다.");

            CareerResult = result;
            ConditionAfter = conditionAfter;
            ManagerEvaluationAfter = managerEvaluationAfter;
            NarrativeSnapshot = narrativeSnapshot ??
                                throw new ArgumentNullException(nameof(narrativeSnapshot));
            IsCommitted = true;
        }

        private void ReplayToNextDecision()
        {
            IMatchDecisionSource battingSource = CanReceiveBattingDecisions
                ? new RecordedMatchDecisionSource(ControlledPlayerId, _decisions)
                : null;
            IMatchPitchingDecisionSource pitchingSource = CanReceivePitchingDecisions
                ? new RecordedMatchPitchingDecisionSource(ControlledPlayerId, _pitchingDecisions)
                : null;
            _progress = CreateSimulator(battingSource, pitchingSource)
                .SimulateUntilDecision(Input);
            if (_progress.IsComplete)
                Phase = CareerMatchPhase.Completed;
        }

        private MatchSimulator CreateSimulator(
            IMatchDecisionSource decisionSource,
            IMatchPitchingDecisionSource pitchingDecisionSource = null)
        {
            var decisionCoordinator = new MatchDecisionCoordinator(
                new CareerBattingDecisionProvider(
                    ControlledPlayerId,
                    _gameSettings.BattingApproach),
                new CareerPitchingDecisionProvider(
                    ControlledPlayerId,
                    _gameSettings.PitchingApproach));
            return new MatchSimulator(
                _balance,
                MatchRandomStreams.Create(Input.RandomSeed),
                decisionSource,
                pitchingDecisionSource,
                decisionCoordinator);
        }

        private bool HasControlledBenchSubstitution()
        {
            return HasControlledBenchSubstitution(Input.AwayRoster) ||
                   HasControlledBenchSubstitution(Input.HomeRoster);
        }

        private bool HasControlledBenchSubstitution(MatchRosterSnapshot roster)
        {
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                if (roster.Bench[index].PlayerId == ControlledPlayerId)
                    return true;
            }
            return false;
        }

        private sealed class CareerBattingDecisionProvider : IBattingDecisionProvider
        {
            private readonly int _controlledPlayerId;
            private readonly BattingApproach _controlledApproach;
            private readonly SituationalBattingDecisionProvider _automatic = new();

            public CareerBattingDecisionProvider(int controlledPlayerId, BattingApproach controlledApproach)
            {
                _controlledPlayerId = controlledPlayerId;
                _controlledApproach = controlledApproach;
            }

            public BattingApproach GetApproach(DecisionContext context)
            {
                return context.Batter.PlayerId == _controlledPlayerId
                    ? _controlledApproach
                    : _automatic.GetApproach(context);
            }
        }

        private sealed class CareerPitchingDecisionProvider : IPitchingDecisionProvider
        {
            private readonly int _controlledPlayerId;
            private readonly PitchingApproach _controlledApproach;
            private readonly SituationalPitchingDecisionProvider _automatic = new();

            public CareerPitchingDecisionProvider(int controlledPlayerId, PitchingApproach controlledApproach)
            {
                _controlledPlayerId = controlledPlayerId;
                _controlledApproach = controlledApproach;
            }

            public PitchingApproach GetApproach(DecisionContext context)
            {
                return context.Pitcher.PlayerId == _controlledPlayerId
                    ? _controlledApproach
                    : _automatic.GetApproach(context);
            }
        }
    }
}

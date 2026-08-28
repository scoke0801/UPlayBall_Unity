using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
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
    /// 선수 중심 진행 또는 즉시 결과 확인 방식을 정의한다.
    /// </summary>
    public enum CareerMatchMode
    {
        PlayerFocus = 0,
        ResultsOnly = 1
    }

    /// <summary>
    /// 선택 기록을 같은 Seed 경기 위에 재생해 투구별 중단과 복구를 제공한다.
    /// </summary>
    public sealed class CareerMatchSession
    {
        private const int MaximumAutomaticDecisions = 256;

        private readonly BalanceTable _balance;
        private readonly List<BattingApproach> _decisions = new List<BattingApproach>(32);
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
            int managerEvaluationBefore)
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
        public IReadOnlyList<MatchEvent> Events => _progress?.Events ?? Array.Empty<MatchEvent>();
        public MatchResult MatchResult => _progress?.Result;
        public bool IsComplete => Phase == CareerMatchPhase.Completed;
        public bool IsCommitted { get; private set; }
        public bool CanReceiveBattingDecisions =>
            PlayerRole == PlayerGameRole.StartingBatter || HasControlledBenchSubstitution();
        public int ConditionBefore { get; }
        public int ConditionAfter { get; private set; }
        public int ManagerEvaluationBefore { get; }
        public int ManagerEvaluationAfter { get; private set; }
        public CareerGameAdvanceResult? CareerResult { get; private set; }

        /// <summary>
        /// 준비 화면에서 고른 방식으로 경기를 시작한다.
        /// </summary>
        public void Start(CareerMatchMode mode)
        {
            if (Phase != CareerMatchPhase.Preparation)
                throw new InvalidOperationException("이미 시작한 경기입니다.");

            Mode = mode;
            Phase = CareerMatchPhase.Playing;
            if (mode == CareerMatchMode.ResultsOnly || !CanReceiveBattingDecisions)
            {
                _progress = new MatchSimulator(
                        _balance,
                        new Pcg32Random(Input.RandomSeed))
                    .SimulateUntilDecision(Input);
                Phase = CareerMatchPhase.Completed;
                return;
            }

            ReplayToNextDecision();
        }

        /// <summary>
        /// 현재 입력 대기 투구에 타격 방식을 적용하고 다음 입력까지 진행한다.
        /// </summary>
        public void SubmitBattingApproach(BattingApproach approach)
        {
            if (Phase != CareerMatchPhase.Playing || !PendingDecision.HasValue)
                throw new InvalidOperationException("타격 입력을 기다리는 상태가 아닙니다.");

            _decisions.Add(approach);
            ReplayToNextDecision();
        }

        /// <summary>
        /// 현재 타석의 남은 투구를 균형 타격으로 진행한다.
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
                SubmitBattingApproach(BattingApproach.Balanced);
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
                SubmitBattingApproach(BattingApproach.Balanced);

            if (safety <= 0)
                throw new InvalidOperationException("경기 자동 진행 안전 한도를 초과했습니다.");
        }

        /// <summary>
        /// 커리어 기록 반영 결과와 경기 전후 상태를 한 번만 연결한다.
        /// </summary>
        public void MarkCommitted(
            CareerGameAdvanceResult result,
            int conditionAfter,
            int managerEvaluationAfter)
        {
            if (!IsComplete || MatchResult == null)
                throw new InvalidOperationException("완료되지 않은 경기는 기록할 수 없습니다.");
            if (IsCommitted)
                throw new InvalidOperationException("이미 기록한 경기입니다.");

            CareerResult = result;
            ConditionAfter = conditionAfter;
            ManagerEvaluationAfter = managerEvaluationAfter;
            IsCommitted = true;
        }

        private void ReplayToNextDecision()
        {
            var decisionSource = new RecordedMatchDecisionSource(ControlledPlayerId, _decisions);
            _progress = new MatchSimulator(
                    _balance,
                    new Pcg32Random(Input.RandomSeed),
                    decisionSource)
                .SimulateUntilDecision(Input);
            if (_progress.IsComplete)
                Phase = CareerMatchPhase.Completed;
        }

        private bool HasControlledBenchSubstitution()
        {
            return HasControlledBenchSubstitution(Input.AwayTeam) ||
                   HasControlledBenchSubstitution(Input.HomeTeam);
        }

        private bool HasControlledBenchSubstitution(Team team)
        {
            return team.PositionPlayerSubstitution?.Player.PlayerId == ControlledPlayerId;
        }
    }
}

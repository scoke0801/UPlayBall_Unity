using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 경기 중 한 번 적용할 야수 교체 후보와 감독의 투입 조건을 보관한다.
    /// </summary>
    public sealed class PositionPlayerSubstitutionPlan
    {
        /// <summary>
        /// 교체 선수, 승계할 타순, 투입 가능한 경기 상황을 고정한다.
        /// </summary>
        public PositionPlayerSubstitutionPlan(
            Player player,
            int battingOrderIndex,
            int earliestInning,
            int maximumScoreDifference)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (battingOrderIndex < 0 || battingOrderIndex >= BaseballRules.BattingOrderSize)
                throw new ArgumentOutOfRangeException(nameof(battingOrderIndex));
            if (earliestInning <= 0 || earliestInning > BaseballRules.RegulationInnings)
                throw new ArgumentOutOfRangeException(nameof(earliestInning));
            if (maximumScoreDifference < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumScoreDifference));

            BattingOrderIndex = battingOrderIndex;
            EarliestInning = earliestInning;
            MaximumScoreDifference = maximumScoreDifference;
        }

        public Player Player { get; }
        public int BattingOrderIndex { get; }
        public int EarliestInning { get; }
        public int MaximumScoreDifference { get; }

        /// <summary>
        /// 감독이 정한 최소 이닝과 점수 차 조건을 모두 만족하는지 반환한다.
        /// </summary>
        public bool CanEnter(int inning, int teamRuns, int opponentRuns)
        {
            return inning >= EarliestInning &&
                   Math.Abs(teamRuns - opponentRuns) <= MaximumScoreDifference;
        }
    }

    /// <summary>
    /// 한 경기에 참가하는 구단의 라인업과 선발 투수를 보관한다.
    /// </summary>
    public sealed class Team
    {
        /// <summary>
        /// 경기 가능한 구단 입력을 생성한다.
        /// </summary>
        public Team(int teamId, string name, Lineup lineup, Player startingPitcher)
            : this(teamId, name, lineup, startingPitcher, null, 0)
        {
        }

        /// <summary>
        /// 선발과 지정 이닝부터 등판할 구원투수를 포함한 경기 입력을 생성한다.
        /// </summary>
        public Team(
            int teamId,
            string name,
            Lineup lineup,
            Player startingPitcher,
            Player reliefPitcher,
            int reliefStartInning)
            : this(
                teamId,
                name,
                lineup,
                startingPitcher,
                reliefPitcher,
                reliefStartInning,
                null)
        {
        }

        /// <summary>
        /// 투수 운용과 한 명의 야수 교체 계획을 포함한 경기 입력을 생성한다.
        /// </summary>
        public Team(
            int teamId,
            string name,
            Lineup lineup,
            Player startingPitcher,
            Player reliefPitcher,
            int reliefStartInning,
            PositionPlayerSubstitutionPlan positionPlayerSubstitution)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId), "TeamId는 양수여야 합니다.");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(name));

            TeamId = teamId;
            Name = name;
            Lineup = lineup ?? throw new ArgumentNullException(nameof(lineup));
            StartingPitcher = startingPitcher ?? throw new ArgumentNullException(nameof(startingPitcher));
            ReliefPitcher = reliefPitcher;
            ReliefStartInning = reliefStartInning;
            PositionPlayerSubstitution = positionPlayerSubstitution;

            if (reliefPitcher != null)
            {
                if (reliefStartInning < 2)
                    throw new ArgumentOutOfRangeException(nameof(reliefStartInning));
                if (reliefPitcher.PlayerId == startingPitcher.PlayerId)
                    throw new ArgumentException("선발과 구원투수는 서로 달라야 합니다.", nameof(reliefPitcher));
            }
            else if (reliefStartInning != 0)
            {
                throw new ArgumentException("구원투수가 없으면 교체 이닝도 지정할 수 없습니다.", nameof(reliefStartInning));
            }

            for (int index = 0; index < lineup.Count; index++)
            {
                int playerId = lineup[index].Player.PlayerId;
                if (playerId == startingPitcher.PlayerId || playerId == reliefPitcher?.PlayerId)
                    throw new ArgumentException("투수를 타순에 중복 배치할 수 없습니다.", nameof(startingPitcher));
            }

            ValidatePositionPlayerSubstitution(lineup, startingPitcher, reliefPitcher, positionPlayerSubstitution);
        }

        public int TeamId { get; }
        public string Name { get; }
        public Lineup Lineup { get; }
        public Player StartingPitcher { get; }
        public Player ReliefPitcher { get; }
        public int ReliefStartInning { get; }
        public PositionPlayerSubstitutionPlan PositionPlayerSubstitution { get; }

        /// <summary>
        /// 지정 이닝 전에는 선발, 이후에는 등록된 구원투수를 반환한다.
        /// </summary>
        public Player GetPitcherForInning(int inning)
        {
            return ReliefPitcher != null && inning >= ReliefStartInning
                ? ReliefPitcher
                : StartingPitcher;
        }

        private static void ValidatePositionPlayerSubstitution(
            Lineup lineup,
            Player startingPitcher,
            Player reliefPitcher,
            PositionPlayerSubstitutionPlan substitution)
        {
            if (substitution == null)
                return;

            Player substitute = substitution.Player;
            if (substitute.PlayerId == startingPitcher.PlayerId ||
                substitute.PlayerId == reliefPitcher?.PlayerId)
            {
                throw new ArgumentException("교체 야수를 투수와 중복 등록할 수 없습니다.", nameof(substitution));
            }

            for (int index = 0; index < lineup.Count; index++)
            {
                if (lineup[index].Player.PlayerId == substitute.PlayerId)
                    throw new ArgumentException("교체 선수는 선발 Lineup에 중복 등록할 수 없습니다.", nameof(substitution));
            }

            // 비주포지션 교체는 유효하며, 경기 판정에 들어가기 전에 공통 PositionAssignmentRule의 비용을 평가해야 한다.
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>계약 선택 전에는 월드를 바꾸지 않고 다음 시즌 세 리그 로스터 결정을 보관한다.</summary>
    public sealed class WorldOffseasonMarketPlan
    {
        private readonly LeagueRosterPlan[] _leagueRosters;
        private readonly AiMarketDecision[] _decisions;
        private readonly PlayerState[] _newPlayers;

        public WorldOffseasonMarketPlan(
            LeagueRosterPlan[] leagueRosters,
            AiMarketDecision[] decisions,
            PlayerState[] newPlayers)
        {
            _leagueRosters = leagueRosters ?? throw new ArgumentNullException(nameof(leagueRosters));
            _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
            _newPlayers = newPlayers ?? throw new ArgumentNullException(nameof(newPlayers));
            Array.Sort(_leagueRosters, (left, right) => left.LeagueId.CompareTo(right.LeagueId));
            Array.Sort(_decisions, (left, right) => left.PlayerId.CompareTo(right.PlayerId));
            Array.Sort(_newPlayers, (left, right) => left.PlayerId.CompareTo(right.PlayerId));
        }

        public IReadOnlyList<LeagueRosterPlan> LeagueRosters => _leagueRosters;
        public IReadOnlyList<AiMarketDecision> Decisions => _decisions;
        public IReadOnlyList<PlayerState> NewPlayers => _newPlayers;

        public TeamState[] GetTeams(LeagueId leagueId)
        {
            for (int index = 0; index < _leagueRosters.Length; index++)
            {
                if (_leagueRosters[index].LeagueId == leagueId)
                    return _leagueRosters[index].CopyTeams();
            }
            throw new InvalidOperationException($"{leagueId}의 시장 로스터 계획이 없습니다.");
        }

        public WorldOffseasonMarketPlan WithTeams(LeagueId leagueId, TeamState[] teams)
        {
            var rosters = new LeagueRosterPlan[_leagueRosters.Length];
            bool replaced = false;
            for (int index = 0; index < rosters.Length; index++)
            {
                LeagueRosterPlan source = _leagueRosters[index];
                if (source.LeagueId == leagueId)
                {
                    rosters[index] = new LeagueRosterPlan(leagueId, teams);
                    replaced = true;
                }
                else
                {
                    rosters[index] = new LeagueRosterPlan(source.LeagueId, source.CopyTeams());
                }
            }
            if (!replaced)
                throw new InvalidOperationException($"{leagueId}의 시장 로스터 계획이 없습니다.");
            return new WorldOffseasonMarketPlan(rosters, (AiMarketDecision[])_decisions.Clone(), (PlayerState[])_newPlayers.Clone());
        }
    }

    /// <summary>한 리그의 다음 시즌 구단 로스터 스냅샷을 보관한다.</summary>
    public sealed class LeagueRosterPlan
    {
        private readonly TeamState[] _teams;

        public LeagueRosterPlan(LeagueId leagueId, TeamState[] teams)
        {
            if (!leagueId.IsAssigned) throw new ArgumentException("유효한 LeagueId가 필요합니다.", nameof(leagueId));
            LeagueId = leagueId;
            _teams = teams == null ? throw new ArgumentNullException(nameof(teams)) : (TeamState[])teams.Clone();
            Array.Sort(_teams, (left, right) => left.TeamId.CompareTo(right.TeamId));
        }

        public LeagueId LeagueId { get; }
        public IReadOnlyList<TeamState> Teams => _teams;
        public TeamState[] CopyTeams() => (TeamState[])_teams.Clone();
    }

    /// <summary>AI 선수 한 명의 오프시즌 계약 또는 은퇴 결정을 보관한다.</summary>
    public readonly struct AiMarketDecision
    {
        public AiMarketDecision(
            int playerId,
            PlayerMovementType movementType,
            LeagueId previousLeagueId,
            int previousTeamId,
            LeagueId targetLeagueId,
            int targetTeamId,
            ExpectedRole expectedRole,
            int contractYears,
            long annualSalary,
            string reason)
        {
            PlayerId = playerId;
            MovementType = movementType;
            PreviousLeagueId = previousLeagueId;
            PreviousTeamId = previousTeamId;
            TargetLeagueId = targetLeagueId;
            TargetTeamId = targetTeamId;
            ExpectedRole = expectedRole;
            ContractYears = contractYears;
            AnnualSalary = annualSalary;
            Reason = reason ?? string.Empty;
        }

        public int PlayerId { get; }
        public PlayerMovementType MovementType { get; }
        public LeagueId PreviousLeagueId { get; }
        public int PreviousTeamId { get; }
        public LeagueId TargetLeagueId { get; }
        public int TargetTeamId { get; }
        public ExpectedRole ExpectedRole { get; }
        public int ContractYears { get; }
        public long AnnualSalary { get; }
        public string Reason { get; }
        public bool IsRetirement => MovementType == PlayerMovementType.Retirement;
    }
}

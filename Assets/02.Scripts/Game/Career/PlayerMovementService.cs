using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    public readonly struct TradeExecutionResult
    {
        public TradeExecutionResult(
            int previousTeamId,
            int newTeamId,
            int exchangedPlayerId,
            ExpectedRole projectedRole)
        {
            PreviousTeamId = previousTeamId;
            NewTeamId = newTeamId;
            ExchangedPlayerId = exchangedPlayerId;
            ProjectedRole = projectedRole;
        }

        public int PreviousTeamId { get; }
        public int NewTeamId { get; }
        public int ExchangedPlayerId { get; }
        public ExpectedRole ProjectedRole { get; }
    }

    /// <summary>
    /// 트레이드 확정 뒤 계약 승계·로스터 보상·소속·감독 평가·기록 소속을 한 번에 바꾼다.
    /// </summary>
    public sealed class PlayerMovementService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public PlayerMovementService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance;
        }

        public TradeExecutionResult ExecuteTrade(
            int targetTeamId,
            ExpectedRole projectedRole,
            int gameIndex)
        {
            SeasonState season = _career.League.CurrentSeason;
            if (season?.Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규 시즌 중에만 트레이드를 처리할 수 있습니다.");

            int previousTeamId = _career.MyPlayer.CurrentTeamId;
            if (targetTeamId == previousTeamId)
                throw new ArgumentException("현재 구단으로 트레이드할 수 없습니다.", nameof(targetTeamId));

            TeamState previousTeam = GetTeam(previousTeamId);
            TeamState targetTeam = GetTeam(targetTeamId);
            int playerOverall = new PlayerValueEvaluator(_balance.PlayerEvaluation)
                .CalculatePositionValue(_career.MyPlayer.ToPlayer());
            RosterCompetitorState exchangedPlayer = SelectExchangedPlayer(targetTeam, playerOverall);
            TeamState updatedPreviousTeam = previousTeam.WithRoster(
                Append(previousTeam.RosterCompetitors, exchangedPlayer));
            TeamState updatedTargetTeam = targetTeam.WithRoster(
                Remove(targetTeam.RosterCompetitors, exchangedPlayer.PlayerId));

            ExpectedRole previousRole = _career.CurrentExpectedRole;
            _career.League.ReplaceTeams(updatedPreviousTeam, updatedTargetTeam);
            _career.MyPlayer.TransferTo(targetTeamId);
            _career.MyPlayer.ResetManagerEvaluation(_balance.TradeMarket.ArrivalManagerEvaluation);
            _career.CurrentContract.TransferTo(targetTeamId);
            season.LeagueStatistics.RegularSeason.GetOrCreate(
                _career.MyPlayer.PlayerId,
                _career.MyPlayer.Name,
                targetTeamId,
                _career.MyPlayer.PrimaryPosition);
            season.LeagueStatistics.RegularSeason.GetOrCreate(
                exchangedPlayer.PlayerId,
                exchangedPlayer.Name,
                previousTeamId,
                exchangedPlayer.Position);

            var history = new TradeHistoryRecord(
                season.SeasonId,
                season.Year,
                gameIndex,
                previousTeamId,
                targetTeamId,
                previousRole,
                projectedRole,
                exchangedPlayer.PlayerId);
            _career.TradeState.RecordTrade(history);
            return new TradeExecutionResult(
                previousTeamId,
                targetTeamId,
                exchangedPlayer.PlayerId,
                projectedRole);
        }

        private RosterCompetitorState SelectExchangedPlayer(TeamState targetTeam, int playerOverall)
        {
            bool found = false;
            RosterCompetitorState selected = default;
            int selectedDistance = int.MaxValue;
            IReadOnlyList<RosterCompetitorState> roster = targetTeam.RosterCompetitors;
            for (int index = 0; index < roster.Count; index++)
            {
                RosterCompetitorState candidate = roster[index];
                if (candidate.Position != _career.MyPlayer.PrimaryPosition)
                    continue;
                int distance = Math.Abs(candidate.Overall - playerOverall);
                if (found && (distance > selectedDistance ||
                    distance == selectedDistance && candidate.PlayerId > selected.PlayerId))
                {
                    continue;
                }
                found = true;
                selected = candidate;
                selectedDistance = distance;
            }

            if (!found)
                throw new InvalidOperationException("트레이드 보상으로 이동할 동일 포지션 선수가 없습니다.");
            return selected;
        }

        private static RosterCompetitorState[] Append(
            IReadOnlyList<RosterCompetitorState> source,
            RosterCompetitorState player)
        {
            var result = new RosterCompetitorState[source.Count + 1];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            result[^1] = player;
            return result;
        }

        private static RosterCompetitorState[] Remove(
            IReadOnlyList<RosterCompetitorState> source,
            int playerId)
        {
            var result = new RosterCompetitorState[source.Count - 1];
            int resultIndex = 0;
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].PlayerId == playerId)
                    continue;
                result[resultIndex++] = source[index];
            }
            if (resultIndex != result.Length)
                throw new InvalidOperationException("이동할 선수를 대상 구단 로스터에서 찾지 못했습니다.");
            return result;
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < _career.League.Teams.Count; index++)
            {
                if (_career.League.Teams[index].TeamId == teamId)
                    return _career.League.Teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }
    }
}

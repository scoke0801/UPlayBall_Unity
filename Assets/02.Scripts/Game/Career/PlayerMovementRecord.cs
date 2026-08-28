using System;
using System.Collections.Generic;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    public enum PlayerMovementType
    {
        InitialSigning,
        CurrentTeamExtension,
        CurrentTeamRenewal,
        SameLeagueTransfer,
        Trade,
        Promotion,
        Rehabilitation,
        Release,
        Retirement
    }

    /// <summary>
    /// 선수 이동 당시 리그·구단·역할·계약 근거를 변경 불가능한 기록으로 남긴다.
    /// </summary>
    public readonly struct PlayerMovementRecord
    {
        public PlayerMovementRecord(
            DateTime worldDate,
            int seasonId,
            int playerId,
            PlayerMovementType movementType,
            LeagueId previousLeagueId,
            int previousTeamId,
            LeagueId targetLeagueId,
            int targetTeamId,
            ExpectedRole previousRole,
            ExpectedRole promisedRole,
            ExpectedRole projectedRole,
            int contractId,
            string reason)
        {
            WorldDate = worldDate.Date;
            SeasonId = seasonId;
            PlayerId = playerId;
            MovementType = movementType;
            PreviousLeagueId = previousLeagueId;
            PreviousTeamId = previousTeamId;
            TargetLeagueId = targetLeagueId;
            TargetTeamId = targetTeamId;
            PreviousRole = previousRole;
            PromisedRole = promisedRole;
            ProjectedRole = projectedRole;
            ContractId = contractId;
            Reason = reason ?? string.Empty;
        }

        public DateTime WorldDate { get; }
        public int SeasonId { get; }
        public int PlayerId { get; }
        public PlayerMovementType MovementType { get; }
        public LeagueId PreviousLeagueId { get; }
        public int PreviousTeamId { get; }
        public LeagueId TargetLeagueId { get; }
        public int TargetTeamId { get; }
        public ExpectedRole PreviousRole { get; }
        public ExpectedRole PromisedRole { get; }
        public ExpectedRole ProjectedRole { get; }
        public int ContractId { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// 월드 전체 선수 이동을 확정 순서대로 보관한다.
    /// </summary>
    public sealed class PlayerMovementLedger
    {
        private readonly List<PlayerMovementRecord> _records = new List<PlayerMovementRecord>();

        public IReadOnlyList<PlayerMovementRecord> Records => _records;

        public void Record(PlayerMovementRecord record)
        {
            if (_records.Count > 0 && record.WorldDate < _records[^1].WorldDate)
                throw new InvalidOperationException("선수 이동 기록 날짜는 역행할 수 없습니다.");
            _records.Add(record);
        }
    }
}

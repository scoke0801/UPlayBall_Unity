using System;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 대상이 되는 현재 선수 계약을 보관한다.
    /// </summary>
    public sealed class PlayerContractState
    {
        public PlayerContractState(
            int saveVersion,
            int teamId,
            int signedYear,
            int contractYears,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole)
            : this(
                saveVersion,
                contractId: 0,
                playerId: 0,
                teamId,
                LeagueId.Unassigned,
                signedYear,
                contractYears,
                signingBonus,
                annualSalary,
                expectedRole)
        {
        }

        /// <summary>
        /// 월드 전역 식별자와 체결 리그를 포함한 선수 계약을 생성한다.
        /// </summary>
        public PlayerContractState(
            int saveVersion,
            int contractId,
            int playerId,
            int teamId,
            LeagueId leagueId,
            int signedYear,
            int contractYears,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (contractYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(contractYears));

            SaveVersion = saveVersion;
            ContractId = contractId;
            PlayerId = playerId;
            TeamId = teamId;
            SigningTeamId = teamId;
            SigningLeagueId = leagueId;
            CurrentLeagueId = leagueId;
            SignedYear = signedYear;
            ContractYears = contractYears;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
            IsActive = true;
        }

        public int SaveVersion { get; }
        public int ContractId { get; private set; }
        public int PlayerId { get; private set; }
        public int TeamId { get; private set; }
        public int CurrentTeamId => TeamId;
        public int SigningTeamId { get; }
        public LeagueId SigningLeagueId { get; private set; }
        public LeagueId CurrentLeagueId { get; private set; }
        public int SignedYear { get; }
        public int ContractYears { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
        public ExpectedRole PromisedRole => ExpectedRole;
        public bool IsActive { get; private set; }
        public int EndYear => SignedYear + ContractYears - 1;
        public long GuaranteedValue => SigningBonus + AnnualSalary * ContractYears;

        /// <summary>
        /// 현재 시즌을 마친 뒤 보장된 시즌 수를 반환한다.
        /// </summary>
        public int GetRemainingSeasonsAfter(int currentYear)
        {
            return Math.Max(0, EndYear - currentYear);
        }

        /// <summary>
        /// 트레이드에서 계약 기간·연봉·상여 조건은 유지하고 계약을 승계한 구단만 바꾼다.
        /// </summary>
        public void TransferTo(int teamId)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            TeamId = teamId;
        }

        public void TransferTo(int teamId, LeagueId leagueId)
        {
            if (!leagueId.IsAssigned)
                throw new ArgumentException("계약 이동에는 유효한 LeagueId가 필요합니다.", nameof(leagueId));
            TransferTo(teamId);
            CurrentLeagueId = leagueId;
        }

        /// <summary>
        /// v7 계약에 v8 전역 계약·선수·리그 식별자를 한 번만 부여한다.
        /// </summary>
        public void AttachIdentity(int contractId, int playerId, LeagueId leagueId)
        {
            if (contractId <= 0) throw new ArgumentOutOfRangeException(nameof(contractId));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!leagueId.IsAssigned) throw new ArgumentException("유효한 LeagueId가 필요합니다.", nameof(leagueId));
            if (ContractId > 0 || PlayerId > 0)
                throw new InvalidOperationException("계약 식별자는 한 번만 부여할 수 있습니다.");
            ContractId = contractId;
            PlayerId = playerId;
            SigningLeagueId = leagueId;
            CurrentLeagueId = leagueId;
        }

        /// <summary>
        /// v7 계약 이력에 누락된 전역 식별자와 활성 상태를 결정론적으로 보충한다.
        /// </summary>
        public void MigrateLegacyIdentity(
            int contractId,
            int playerId,
            LeagueId leagueId,
            bool isActive)
        {
            if (contractId <= 0) throw new ArgumentOutOfRangeException(nameof(contractId));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!leagueId.IsAssigned) throw new ArgumentException("유효한 LeagueId가 필요합니다.", nameof(leagueId));
            if (ContractId > 0 && ContractId != contractId)
                throw new InvalidOperationException("기존 ContractId는 마이그레이션 중 변경할 수 없습니다.");
            if (PlayerId > 0 && PlayerId != playerId)
                throw new InvalidOperationException("기존 PlayerId는 마이그레이션 중 변경할 수 없습니다.");

            ContractId = contractId;
            PlayerId = playerId;
            if (!SigningLeagueId.IsAssigned)
                SigningLeagueId = leagueId;
            if (!CurrentLeagueId.IsAssigned)
                CurrentLeagueId = leagueId;
            IsActive = isActive;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

    }
}

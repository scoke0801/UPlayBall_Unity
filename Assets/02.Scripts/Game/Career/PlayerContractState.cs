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
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (contractYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(contractYears));

            SaveVersion = saveVersion;
            TeamId = teamId;
            SigningTeamId = teamId;
            SignedYear = signedYear;
            ContractYears = contractYears;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
        }

        public int SaveVersion { get; }
        public int TeamId { get; private set; }
        public int SigningTeamId { get; }
        public int SignedYear { get; }
        public int ContractYears { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
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

    }
}

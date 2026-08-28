using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 한 구단이 새 게임 선수에게 제시하는 계약 오퍼를 보관한다.
    /// </summary>
    public readonly struct ContractOffer
    {
        /// <summary>
        /// 오퍼 결과를 생성한다.
        /// </summary>
        public ContractOffer(
            GeneratedTeam team,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole,
            double offerScore)
            : this(team, signingBonus, annualSalary, expectedRole, offerScore, contractYears: 3)
        {
        }

        /// <summary>
        /// 계약 기간까지 포함한 오퍼 결과를 생성한다.
        /// </summary>
        public ContractOffer(
            GeneratedTeam team,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole,
            double offerScore,
            int contractYears)
        {
            Team = team;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
            OfferScore = offerScore;
            ContractYears = contractYears;
        }

        public GeneratedTeam Team { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
        public double OfferScore { get; }
        public int ContractYears { get; }
    }
}

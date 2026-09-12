using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>구단주 모드 선수 계약의 조정 가능 비용 계약이다.</summary>
    public sealed class OwnerPlayerMarketBalanceTable
    {
        private readonly long[] _annualSalaryByCost;
        private readonly double[] _editionSalaryMultipliers;

        public OwnerPlayerMarketBalanceTable(
            IReadOnlyList<long> annualSalaryByCost,
            IReadOnlyList<double> editionSalaryMultipliers,
            double renewalSigningCostRate,
            double multiYearSalaryDiscount,
            int maximumContractSeasons)
        {
            if (annualSalaryByCost == null || annualSalaryByCost.Count != 10)
                throw new ArgumentException("Cost 1~10의 연봉표가 필요합니다.", nameof(annualSalaryByCost));
            if (editionSalaryMultipliers == null || (editionSalaryMultipliers.Count != 4 && editionSalaryMultipliers.Count != 8))
                throw new ArgumentException("기존 4종 또는 전체 8종의 연봉 배율이 필요합니다.", nameof(editionSalaryMultipliers));
            if (renewalSigningCostRate < 0d || renewalSigningCostRate > 1d)
                throw new ArgumentOutOfRangeException(nameof(renewalSigningCostRate));
            if (multiYearSalaryDiscount < 0d || multiYearSalaryDiscount > 0.2d)
                throw new ArgumentOutOfRangeException(nameof(multiYearSalaryDiscount));
            if (maximumContractSeasons < 1 || maximumContractSeasons > 10)
                throw new ArgumentOutOfRangeException(nameof(maximumContractSeasons));

            _annualSalaryByCost = new long[annualSalaryByCost.Count];
            for (int index = 0; index < _annualSalaryByCost.Length; index++)
            {
                if (annualSalaryByCost[index] <= 0L)
                    throw new ArgumentOutOfRangeException(nameof(annualSalaryByCost));
                _annualSalaryByCost[index] = annualSalaryByCost[index];
            }
            _editionSalaryMultipliers = new double[8];
            for (int index = 0; index < editionSalaryMultipliers.Count; index++)
            {
                if (editionSalaryMultipliers[index] <= 0d || double.IsNaN(editionSalaryMultipliers[index]))
                    throw new ArgumentOutOfRangeException(nameof(editionSalaryMultipliers));
                _editionSalaryMultipliers[index] = editionSalaryMultipliers[index];
            }
            // 특별 등급의 추가 연봉 규약이 없는 기존 데이터에는 같은 Cost의 일반 배율을 유지한다.
            for (int index = editionSalaryMultipliers.Count; index < _editionSalaryMultipliers.Length; index++)
                _editionSalaryMultipliers[index] = editionSalaryMultipliers[0];

            RenewalSigningCostRate = renewalSigningCostRate;
            MultiYearSalaryDiscount = multiYearSalaryDiscount;
            MaximumContractSeasons = maximumContractSeasons;
        }

        public double RenewalSigningCostRate { get; }
        public double MultiYearSalaryDiscount { get; }
        public int MaximumContractSeasons { get; }

        public long GetAnnualSalary(int cost, PlayerCardEdition edition, int seasons)
        {
            if (cost < 1 || cost > 10) throw new ArgumentOutOfRangeException(nameof(cost));
            if (!Enum.IsDefined(typeof(PlayerCardEdition), edition))
                throw new ArgumentOutOfRangeException(nameof(edition));
            if (seasons < 1 || seasons > MaximumContractSeasons)
                throw new ArgumentOutOfRangeException(nameof(seasons));
            double discount = 1d - MultiYearSalaryDiscount * (seasons - 1);
            return (long)Math.Round(
                _annualSalaryByCost[cost - 1] * _editionSalaryMultipliers[(int)edition] * discount,
                MidpointRounding.AwayFromZero);
        }

        /// <summary>초기 수치는 Cost가 높은 핵심 선수일수록 재정 기회비용이 분명해지도록 완만한 곡선을 쓴다.</summary>
        public static OwnerPlayerMarketBalanceTable CreateInitial() => new OwnerPlayerMarketBalanceTable(
            new long[] { 2_000_000L, 3_000_000L, 5_000_000L, 8_000_000L, 12_000_000L,
                17_000_000L, 23_000_000L, 30_000_000L, 38_000_000L, 47_000_000L },
            new double[] { 1d, 1.08d, 1.12d, 1.2d, 1d, 1d, 1d, 1d },
            renewalSigningCostRate: 0.2d,
            multiYearSalaryDiscount: 0.025d,
            maximumContractSeasons: 3);
    }

    /// <summary>구단주 모드 한 선수 카드의 시즌 단위 계약 원본이다.</summary>
    public sealed class OwnerPlayerContractState
    {
        public OwnerPlayerContractState(
            string contractId,
            string cardId,
            int startSeason,
            int remainingSeasons,
            long annualSalary,
            int? lastSalaryPaidSeason = null)
        {
            ContractId = RequireId(contractId, nameof(contractId));
            CardId = RequireId(cardId, nameof(cardId));
            if (startSeason <= 0 || remainingSeasons < 0 || annualSalary <= 0L)
                throw new ArgumentOutOfRangeException(nameof(startSeason));
            if (lastSalaryPaidSeason.HasValue && lastSalaryPaidSeason.Value < startSeason)
                throw new ArgumentOutOfRangeException(nameof(lastSalaryPaidSeason));
            StartSeason = startSeason;
            RemainingSeasons = remainingSeasons;
            AnnualSalary = annualSalary;
            LastSalaryPaidSeason = lastSalaryPaidSeason;
        }

        public string ContractId { get; }
        public string CardId { get; }
        public int StartSeason { get; private set; }
        public int RemainingSeasons { get; private set; }
        public long AnnualSalary { get; private set; }
        public int? LastSalaryPaidSeason { get; private set; }
        public bool IsExpiring => RemainingSeasons <= 1;

        public void Renew(int season, int seasons, long annualSalary)
        {
            if (season <= 0 || seasons <= 0 || annualSalary <= 0L)
                throw new ArgumentOutOfRangeException(nameof(season));
            StartSeason = season;
            RemainingSeasons = seasons;
            AnnualSalary = annualSalary;
        }

        public bool TrySettleAndAdvance(int completedSeason)
        {
            if (LastSalaryPaidSeason == completedSeason) return false;
            if (completedSeason < StartSeason)
                throw new ArgumentOutOfRangeException(nameof(completedSeason));
            LastSalaryPaidSeason = completedSeason;
            // 재계약 절차 없이 소속과 기존 연봉을 유지한다. 잔여 연수는 과거 저장 정보다.
            return true;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

}

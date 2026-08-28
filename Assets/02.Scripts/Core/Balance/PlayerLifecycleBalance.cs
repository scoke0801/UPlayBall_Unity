using System;

namespace Baseball.Core.Balance
{
    /// <summary>AI 선수의 은퇴·신인 유입과 리그별 기본 계약 규모를 조정한다.</summary>
    public readonly struct PlayerLifecycleBalance
    {
        public PlayerLifecycleBalance(
            int retirementMinimumAge,
            int guaranteedRetirementAge,
            double retirementBaseProbability,
            double retirementAgeWeight,
            int lowAbilityThreshold,
            double lowAbilityWeight,
            int rookieEntryMinimumAge,
            int rookieEntryMaximumAge,
            int rookieEntryMinimumOverall,
            int rookieEntryMaximumOverall,
            long rookieBaseSalary,
            long minorBaseSalary,
            long majorBaseSalary,
            int rookieContractYears,
            int minorContractYears,
            int majorContractYears)
        {
            if (retirementMinimumAge < 18 || guaranteedRetirementAge <= retirementMinimumAge)
                throw new ArgumentOutOfRangeException(nameof(retirementMinimumAge));
            if (retirementBaseProbability < 0d || retirementBaseProbability > 1d)
                throw new ArgumentOutOfRangeException(nameof(retirementBaseProbability));
            if (retirementAgeWeight < 0d || lowAbilityWeight < 0d)
                throw new ArgumentOutOfRangeException(nameof(retirementAgeWeight));
            if (rookieEntryMinimumAge < 16 || rookieEntryMaximumAge < rookieEntryMinimumAge)
                throw new ArgumentOutOfRangeException(nameof(rookieEntryMinimumAge));
            if (rookieEntryMinimumOverall < 0 || rookieEntryMaximumOverall > 100 ||
                rookieEntryMaximumOverall < rookieEntryMinimumOverall)
            {
                throw new ArgumentOutOfRangeException(nameof(rookieEntryMinimumOverall));
            }
            if (rookieBaseSalary <= 0L || minorBaseSalary <= rookieBaseSalary || majorBaseSalary <= minorBaseSalary)
                throw new ArgumentOutOfRangeException(nameof(rookieBaseSalary));
            if (rookieContractYears <= 0 || minorContractYears <= 0 || majorContractYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(rookieContractYears));

            RetirementMinimumAge = retirementMinimumAge;
            GuaranteedRetirementAge = guaranteedRetirementAge;
            RetirementBaseProbability = retirementBaseProbability;
            RetirementAgeWeight = retirementAgeWeight;
            LowAbilityThreshold = lowAbilityThreshold;
            LowAbilityWeight = lowAbilityWeight;
            RookieEntryMinimumAge = rookieEntryMinimumAge;
            RookieEntryMaximumAge = rookieEntryMaximumAge;
            RookieEntryMinimumOverall = rookieEntryMinimumOverall;
            RookieEntryMaximumOverall = rookieEntryMaximumOverall;
            RookieBaseSalary = rookieBaseSalary;
            MinorBaseSalary = minorBaseSalary;
            MajorBaseSalary = majorBaseSalary;
            RookieContractYears = rookieContractYears;
            MinorContractYears = minorContractYears;
            MajorContractYears = majorContractYears;
        }

        public int RetirementMinimumAge { get; }
        public int GuaranteedRetirementAge { get; }
        public double RetirementBaseProbability { get; }
        public double RetirementAgeWeight { get; }
        public int LowAbilityThreshold { get; }
        public double LowAbilityWeight { get; }
        public int RookieEntryMinimumAge { get; }
        public int RookieEntryMaximumAge { get; }
        public int RookieEntryMinimumOverall { get; }
        public int RookieEntryMaximumOverall { get; }
        public long RookieBaseSalary { get; }
        public long MinorBaseSalary { get; }
        public long MajorBaseSalary { get; }
        public int RookieContractYears { get; }
        public int MinorContractYears { get; }
        public int MajorContractYears { get; }

        /// <summary>장기 월드에서 평균 은퇴 나이 36~39세를 목표로 한 초기값을 만든다.</summary>
        public static PlayerLifecycleBalance CreateDefault()
        {
            return new PlayerLifecycleBalance(
                retirementMinimumAge: 34,
                guaranteedRetirementAge: 43,
                retirementBaseProbability: 0.04d,
                retirementAgeWeight: 0.08d,
                lowAbilityThreshold: 55,
                lowAbilityWeight: 0.01d,
                rookieEntryMinimumAge: 18,
                rookieEntryMaximumAge: 22,
                rookieEntryMinimumOverall: 38,
                rookieEntryMaximumOverall: 58,
                rookieBaseSalary: 30_000_000L,
                minorBaseSalary: 90_000_000L,
                majorBaseSalary: 300_000_000L,
                rookieContractYears: 1,
                minorContractYears: 1,
                majorContractYears: 1);
        }
    }
}

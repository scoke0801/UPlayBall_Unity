using System;
using System.Collections.Generic;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 계약·상여·성장 비용이 공유하는 원화 단위를 명시적으로 변환한다.
    /// </summary>
    public static class MoneyAmount
    {
        public const long WonPerTenThousand = 10_000L;

        public static long FromTenThousandWon(long amount)
        {
            if (amount < 0L)
                throw new ArgumentOutOfRangeException(nameof(amount));
            return checked(amount * WonPerTenThousand);
        }
    }

    public enum MoneyTransactionType
    {
        ContractIncome,
        SalaryIncome,
        BonusIncome,
        TrainingExpense,
        TreatmentExpense,
        SkillBlockPurchase,
        SkillBlockSale
    }

    public readonly struct MoneyTransactionRecord
    {
        public MoneyTransactionRecord(int seasonYear, MoneyTransactionType type, string sourceId, long amount)
        {
            SeasonYear = seasonYear;
            Type = type;
            SourceId = sourceId ?? string.Empty;
            Amount = amount;
        }

        public int SeasonYear { get; }
        public MoneyTransactionType Type { get; }
        public string SourceId { get; }
        public long Amount { get; }
    }

    /// <summary>
    /// 원 단위 정수 Money와 수입·지출 이력을 소유한다.
    /// </summary>
    public sealed class CareerEconomyState
    {
        private readonly List<MoneyTransactionRecord> _transactions;

        public CareerEconomyState(long money)
        {
            if (money < 0L)
                throw new ArgumentOutOfRangeException(nameof(money));
            Money = money;
            _transactions = new List<MoneyTransactionRecord>();
        }

        public long Money { get; private set; }
        public IReadOnlyList<MoneyTransactionRecord> Transactions => _transactions;

        public void Earn(int seasonYear, MoneyTransactionType type, string sourceId, long amount)
        {
            if (amount <= 0L)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Money = checked(Money + amount);
            _transactions.Add(new MoneyTransactionRecord(seasonYear, type, sourceId, amount));
        }

        public void Spend(int seasonYear, MoneyTransactionType type, string sourceId, long amount)
        {
            if (amount < 0L)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (Money < amount)
                throw new InvalidOperationException("Money가 부족합니다.");
            Money -= amount;
            if (amount > 0L)
                _transactions.Add(new MoneyTransactionRecord(seasonYear, type, sourceId, -amount));
        }
    }
}

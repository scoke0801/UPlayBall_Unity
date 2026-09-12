using System;

namespace Baseball.Core.Historical
{
    /// <summary>경기 결산과 분리된 오프시즌 4주의 완료 횟수를 저장한다.</summary>
    public sealed class OwnerOffseasonState
    {
        public const int DurationWeeks = 4;

        public OwnerOffseasonState(int completedWeeks = 0)
        {
            if (completedWeeks < 0 || completedWeeks > DurationWeeks)
                throw new ArgumentOutOfRangeException(nameof(completedWeeks));
            CompletedWeeks = completedWeeks;
        }

        public int CompletedWeeks { get; private set; }
        public int RemainingWeeks => DurationWeeks - CompletedWeeks;

        /// <summary>명령이 미리 확인한 주차와 같을 때만 한 번 정산한다.</summary>
        public bool TryAdvance(int expectedCompletedWeeks)
        {
            if (expectedCompletedWeeks != CompletedWeeks || RemainingWeeks == 0) return false;
            CompletedWeeks++;
            return true;
        }

        public void BeginNextSeason() => CompletedWeeks = 0;
    }
}

using System;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 모든 리그가 공유하는 현재 월드 날짜를 단조 증가 상태로 보관한다.
    /// </summary>
    public sealed class GlobalCalendarState
    {
        public GlobalCalendarState(DateTime currentDate)
        {
            CurrentDate = currentDate.Date;
        }

        public DateTime CurrentDate { get; private set; }

        public void AdvanceTo(DateTime targetDate)
        {
            targetDate = targetDate.Date;
            if (targetDate < CurrentDate)
                throw new InvalidOperationException("월드 날짜는 역행할 수 없습니다.");
            CurrentDate = targetDate;
        }
    }
}

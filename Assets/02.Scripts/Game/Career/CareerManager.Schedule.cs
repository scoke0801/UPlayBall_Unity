namespace Baseball.Game.Career
{
    public sealed partial class CareerManager
    {
        /// <summary>현재 커리어의 전체 시즌 일정을 화면용 읽기 모델로 반환한다.</summary>
        public CareerScheduleView Schedule => CurrentCareer == null || _balance == null
            ? null
            : new CareerScheduleViewBuilder(CurrentCareer, _balance).Build();
    }
}

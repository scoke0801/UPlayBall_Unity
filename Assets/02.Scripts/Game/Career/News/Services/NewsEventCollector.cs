using System;

namespace Baseball.Game.Career.News
{
    /// <summary>여러 도메인 시스템의 확정 사건을 중복 없이 발행 대기열에 모은다.</summary>
    public sealed class NewsEventCollector
    {
        private readonly CareerNewsState _state;

        public NewsEventCollector(CareerNewsState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool Collect(NewsEvent newsEvent) => _state.Enqueue(newsEvent);
    }
}

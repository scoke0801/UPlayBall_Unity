using System;

namespace Baseball.Game.Diagnostics
{
    /// <summary>
    /// 이름 붙인 계측 구간이다. 싱크가 붙어 있지 않으면 같은 코드가 계측만 꺼진 채로 그대로 돈다.
    /// </summary>
    /// <remarks>
    /// 계측 구현(Unity ProfilerMarker, Headless Stopwatch 집계 등)은 Game 레이어 밖에서 주입한다.
    /// Game 레이어는 Unity에 의존하지 않아야 하므로 여기서 <c>Unity.Profiling</c>을 직접 쓰지 않는다.
    /// </remarks>
    public readonly struct ProfilerSection
    {
        private readonly string _name;

        public ProfilerSection(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구간 이름은 비어 있을 수 없습니다.", nameof(name));
            _name = name;
        }

        public string Name => _name;

        /// <summary>using 블록이 끝날 때 구간을 닫는다. 계측이 꺼져 있으면 아무 일도 하지 않는다.</summary>
        public Scope Auto()
        {
            return new Scope(_name);
        }

        /// <summary>대량 시뮬레이션 루프에서 호출되므로 할당 없는 struct로 둔다.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly string _name;
            private readonly IProfilerSectionSink _sink;

            internal Scope(string name)
            {
                _sink = name == null ? null : ProfilerSectionSink.Current;
                _name = name;
                _sink?.Begin(name);
            }

            public void Dispose()
            {
                _sink?.End(_name);
            }
        }
    }

    /// <summary>구간 시작·종료를 받아 실제 계측을 수행하는 어댑터 계약이다.</summary>
    public interface IProfilerSectionSink
    {
        void Begin(string sectionName);
        void End(string sectionName);
    }

    /// <summary>
    /// 현재 프로세스에서 사용할 계측 어댑터를 보관한다. 기본값 null이면 계측이 전부 no-op이다.
    /// </summary>
    /// <remarks>
    /// 시뮬레이션 결과에 영향을 주지 않는 관측 전용 상태이므로 결정론 계약과 무관하다.
    /// </remarks>
    public static class ProfilerSectionSink
    {
        public static IProfilerSectionSink Current { get; set; }
    }
}

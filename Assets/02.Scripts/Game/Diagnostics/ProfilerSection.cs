using System;
using Unity.Profiling;

namespace Baseball.Game.Diagnostics
{
    /// <summary>
    /// 이름 붙인 프로파일러 구간이다. Unity 런타임 밖에서는 계측만 끄고 같은 코드가 그대로 돈다.
    /// </summary>
    /// <remarks>
    /// <see cref="ProfilerMarker"/> 생성은 Unity 네이티브 ECall이라 콘솔 밸런스 러너와 순수 C# 테스트
    /// 프로세스에서는 <see cref="System.Security.SecurityException"/>으로 실패한다. 정적 필드에서
    /// 직접 만들면 타입 초기화가 통째로 죽어 대량 시뮬레이션 검증 경로가 막히므로,
    /// 생성 실패를 여기서 한 번만 흡수하고 이후 호출을 no-op으로 만든다.
    /// </remarks>
    public readonly struct ProfilerSection
    {
        private readonly ProfilerMarker _marker;
        private readonly bool _isEnabled;

        public ProfilerSection(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구간 이름은 비어 있을 수 없습니다.", nameof(name));
            try
            {
                _marker = new ProfilerMarker(name);
                _isEnabled = true;
            }
            catch (Exception)
            {
                _marker = default;
                _isEnabled = false;
            }
        }

        /// <summary>using 블록이 끝날 때 구간을 닫는다. 계측이 꺼져 있으면 아무 일도 하지 않는다.</summary>
        public Scope Auto()
        {
            return new Scope(_marker, _isEnabled);
        }

        /// <summary>대량 시뮬레이션 루프에서 호출되므로 할당 없는 struct로 둔다.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly ProfilerMarker _marker;
            private readonly bool _isEnabled;

            internal Scope(ProfilerMarker marker, bool isEnabled)
            {
                _marker = marker;
                _isEnabled = isEnabled;
                if (isEnabled)
                    marker.Begin();
            }

            public void Dispose()
            {
                if (_isEnabled)
                    _marker.End();
            }
        }
    }
}

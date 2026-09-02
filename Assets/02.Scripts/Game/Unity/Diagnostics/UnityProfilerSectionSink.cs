using System.Collections.Generic;
using Unity.Profiling;

namespace Baseball.Game.Diagnostics
{
    /// <summary>
    /// Game 레이어 계측 구간을 Unity ProfilerMarker로 전달하는 어댑터다.
    /// </summary>
    /// <remarks>
    /// <see cref="ProfilerMarker"/> 생성은 Unity 네이티브 ECall이라 콘솔 러너와 순수 C# 테스트
    /// 프로세스에서는 실패한다. 그래서 마커 생성은 Unity 런타임 전용인 이 어댑터에만 두고,
    /// 실패 시에는 해당 구간을 no-op으로 남겨 대량 시뮬레이션 경로가 막히지 않게 한다.
    /// </remarks>
    public sealed class UnityProfilerSectionSink : IProfilerSectionSink
    {
        private readonly Dictionary<string, ProfilerMarker> _markers = new();
        private readonly HashSet<string> _unavailable = new();

        public void Begin(string sectionName)
        {
            if (TryGetMarker(sectionName, out ProfilerMarker marker))
                marker.Begin();
        }

        public void End(string sectionName)
        {
            if (TryGetMarker(sectionName, out ProfilerMarker marker))
                marker.End();
        }

        private bool TryGetMarker(string sectionName, out ProfilerMarker marker)
        {
            if (_markers.TryGetValue(sectionName, out marker))
                return true;
            if (_unavailable.Contains(sectionName))
                return false;

            try
            {
                marker = new ProfilerMarker(sectionName);
                _markers.Add(sectionName, marker);
                return true;
            }
            catch (System.Exception)
            {
                _unavailable.Add(sectionName);
                marker = default;
                return false;
            }
        }
    }
}

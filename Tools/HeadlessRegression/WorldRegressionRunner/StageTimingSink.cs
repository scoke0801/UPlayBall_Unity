using System;
using System.Collections.Generic;
using System.Diagnostics;
using Baseball.Game.Diagnostics;

namespace Baseball.Tools.WorldRegression
{
    /// <summary>
    /// Game 레이어 계측 구간을 Headless에서 구간별 누적 시간으로 모은다.
    /// </summary>
    /// <remarks>
    /// 시뮬레이션 상태를 읽거나 바꾸지 않고 시작·종료 시각만 본다. 중첩 구간은 각자 자기 이름으로
    /// 누적하므로 상위 구간 시간에 하위 구간이 포함된다는 점을 읽는 쪽에서 감안한다.
    /// </remarks>
    public sealed class StageTimingSink : IProfilerSectionSink
    {
        private readonly Dictionary<string, long> _elapsedTicks = new();
        private readonly Dictionary<string, int> _callCounts = new();
        private readonly Dictionary<string, Stack<long>> _openStamps = new();

        public void Begin(string sectionName)
        {
            if (!_openStamps.TryGetValue(sectionName, out Stack<long> stamps))
            {
                stamps = new Stack<long>();
                _openStamps.Add(sectionName, stamps);
            }
            stamps.Push(Stopwatch.GetTimestamp());
        }

        public void End(string sectionName)
        {
            if (!_openStamps.TryGetValue(sectionName, out Stack<long> stamps) || stamps.Count == 0)
                return;
            long elapsed = Stopwatch.GetTimestamp() - stamps.Pop();
            _elapsedTicks.TryGetValue(sectionName, out long total);
            _elapsedTicks[sectionName] = total + elapsed;
            _callCounts.TryGetValue(sectionName, out int count);
            _callCounts[sectionName] = count + 1;
        }

        public void Reset()
        {
            _elapsedTicks.Clear();
            _callCounts.Clear();
            _openStamps.Clear();
        }

        /// <summary>보고 순서를 고정하기 위해 구간 이름으로 정렬해 돌려준다.</summary>
        public IReadOnlyList<(string Name, double Milliseconds, int Calls)> Snapshot()
        {
            var names = new List<string>(_elapsedTicks.Keys);
            names.Sort(StringComparer.Ordinal);
            var result = new List<(string, double, int)>(names.Count);
            for (int index = 0; index < names.Count; index++)
            {
                string name = names[index];
                double milliseconds = _elapsedTicks[name] * 1000d / Stopwatch.Frequency;
                result.Add((name, milliseconds, _callCounts[name]));
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// MatchSimulator가 표현 또는 분석 계층에 이벤트를 전달하는 계약이다.
    /// </summary>
    public interface IMatchEventSink
    {
        /// <summary>
        /// 순서가 확정된 이벤트 하나를 소비한다.
        /// </summary>
        void Record(in MatchEvent matchEvent);
    }

    /// <summary>
    /// 한 경기의 이벤트를 순서대로 보관한다.
    /// </summary>
    public sealed class MatchEventBuffer : IMatchEventSink
    {
        private readonly List<MatchEvent> _events;

        /// <summary>
        /// 예상 이벤트 수를 반영한 재사용 가능 버퍼를 만든다.
        /// </summary>
        public MatchEventBuffer(int capacity = 512)
        {
            _events = new List<MatchEvent>(capacity);
        }

        public int Count => _events.Count;

        public MatchEvent this[int index] => _events[index];

        /// <summary>
        /// 이벤트를 발생 순서대로 기록한다.
        /// </summary>
        public void Record(in MatchEvent matchEvent)
        {
            _events.Add(matchEvent);
        }

        /// <summary>
        /// 다음 경기에 재사용할 수 있도록 기록을 비운다.
        /// </summary>
        public void Clear()
        {
            _events.Clear();
        }

        /// <summary>
        /// 현재 이벤트를 불변 결과 배열로 복사한다.
        /// </summary>
        public MatchEvent[] ToArray()
        {
            return _events.ToArray();
        }
    }

    /// <summary>
    /// 대량 시뮬레이션에서 이벤트 할당 없이 스트림을 소비한다.
    /// </summary>
    public sealed class NullMatchEventSink : IMatchEventSink
    {
        public static readonly NullMatchEventSink Instance = new NullMatchEventSink();

        private NullMatchEventSink()
        {
        }

        /// <summary>
        /// 전달된 이벤트를 의도적으로 보관하지 않는다.
        /// </summary>
        public void Record(in MatchEvent matchEvent)
        {
        }
    }
}

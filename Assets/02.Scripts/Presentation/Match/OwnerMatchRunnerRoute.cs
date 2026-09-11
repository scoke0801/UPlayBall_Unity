using System;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Match
{
    /// <summary>같은 타구에 속한 공식 진루 사건의 시작·도착 베이스다. 판정 공개 상태는 포함하지 않는다.</summary>
    public readonly struct OwnerMatchRunnerRoute
    {
        public int PlayerId { get; }
        public int FromBase { get; }
        public int ToBase { get; }

        public OwnerMatchRunnerRoute(int playerId, int fromBase, int toBase)
        {
            PlayerId = playerId;
            FromBase = fromBase;
            ToBase = toBase;
        }

        /// <summary>다음 투구·타석 종료를 넘지 않고 주자별 최초 이동만 고정 배열에 복사한다.</summary>
        public static int Collect(MatchEvent[] events, int start, int batterId, int firstId, int secondId, int thirdId,
            OwnerMatchRunnerRoute[] destination)
        {
            Array.Clear(destination, 0, destination.Length);
            int count = 0;
            for (int index = start; index < events.Length; index++)
            {
                MatchEvent value = events[index];
                if (value.EventType == MatchEventType.PlateAppearanceEnded ||
                    (index > start && value.EventType == MatchEventType.Pitch)) break;
                if (value.PlayerId <= 0 || value.PlayerId == batterId) continue;
                int from = value.FromBase, to = value.ToBase;
                if (value.EventType == MatchEventType.Out)
                {
                    to = OwnerMatchPlaybackGroup.ResolveOutBase(value, batterId, firstId, secondId, thirdId);
                    from = value.PlayerId == firstId ? 1 : value.PlayerId == secondId ? 2 : value.PlayerId == thirdId ? 3 : 0;
                }
                else if (value.EventType is not (MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut)) continue;
                if (from < 1 || to <= from || to > 4) continue;
                bool exists = false;
                for (int previous = 0; previous < count; previous++)
                    if (destination[previous].PlayerId == value.PlayerId) exists = true;
                if (!exists && count < destination.Length)
                    destination[count++] = new OwnerMatchRunnerRoute(value.PlayerId, from, to);
            }
            return count;
        }
    }
}

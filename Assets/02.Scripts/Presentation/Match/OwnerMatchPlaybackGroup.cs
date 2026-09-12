using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Presentation.Match
{
    /// <summary>한 타석에서 이미 크게 보여 준 핵심 판정을 추적해 종료 사건의 중복 표시를 막는다.</summary>
    public struct OwnerMatchResultPresentationState
    {
        private MatchEventType _primaryResultEventType;

        public bool HasPrimaryResult { get; private set; }
        public bool WasDoublePlay => HasPrimaryResult && _primaryResultEventType == MatchEventType.DoublePlay;

        /// <summary>안타 또는 병살 판정 뒤의 타석 종료가 같은 결과를 다시 표시하는지 판정한다.</summary>
        public bool IsRepeatedPlateAppearanceResult(in MatchEvent value)
        {
            return HasPrimaryResult && value.EventType == MatchEventType.PlateAppearanceEnded;
        }

        /// <summary>공개된 사건을 반영하고 타석 경계에서 다음 타석을 위해 상태를 비운다.</summary>
        public void Observe(in MatchEvent value)
        {
            if (value.EventType is MatchEventType.Hit or MatchEventType.DoublePlay)
            {
                _primaryResultEventType = value.EventType;
                HasPrimaryResult = true;
                return;
            }

            if (value.EventType is MatchEventType.PlateAppearanceEnded or MatchEventType.HalfInningEnded or
                MatchEventType.MatchEnded or MatchEventType.MatchEndedAsDraw)
                Reset();
        }

        public void Reset()
        {
            _primaryResultEventType = default;
            HasPrimaryResult = false;
        }
    }

    /// <summary>결과보다 원인이 뒤에 기록된 아웃을 같은 연출 공개 단위로 묶는다.</summary>
    public readonly struct OwnerMatchPlaybackGroup
    {
        public MatchEvent VisualEvent { get; }
        public int EventCount { get; }

        private OwnerMatchPlaybackGroup(in MatchEvent visualEvent, int eventCount)
        {
            VisualEvent = visualEvent;
            EventCount = eventCount;
        }

        /// <summary>공식 Out 바로 뒤의 동일 주자 송구 기록만 묶고 판정 순서는 보존한다.</summary>
        public static OwnerMatchPlaybackGroup Resolve(in MatchEvent current, in MatchEvent next)
        {
            bool paired = current.EventType == MatchEventType.Out && next.EventType == MatchEventType.RunnerThrownOut &&
                current.PlayerId > 0 && current.PlayerId == next.PlayerId && current.Inning == next.Inning && current.Half == next.Half;
            return new OwnerMatchPlaybackGroup(paired ? next : current, paired ? 2 : 1);
        }

        /// <summary>타자 아웃 또는 공개 베이스의 강제 진루 관계로 확정할 수 있는 목적지만 반환한다.</summary>
        public static int ResolveOutBase(in MatchEvent value, int batterId, int firstId, int secondId, int thirdId)
        {
            if (value.EventType == MatchEventType.RunnerThrownOut)
                return value.ToBase >= 1 && value.ToBase <= 4 ? value.ToBase : 0;
            if (value.EventType != MatchEventType.Out || value.PlayerId <= 0) return 0;
            if (value.PlateAppearanceResult is not (PlateAppearanceResult.GroundOut or PlateAppearanceResult.SacrificeBunt or PlateAppearanceResult.FieldersChoice)) return 0;
            if (value.PlayerId == batterId)
                return value.PlateAppearanceResult == PlateAppearanceResult.FieldersChoice ? 0 : 1;
            if (firstId <= 0) return 0;
            if (value.PlayerId == firstId) return 2;
            if (secondId <= 0) return 0;
            if (value.PlayerId == secondId) return 3;
            return value.PlayerId == thirdId ? 4 : 0;
        }
    }
}

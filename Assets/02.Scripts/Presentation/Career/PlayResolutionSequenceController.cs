using System;

namespace Baseball.Presentation.Career
{
    /// <summary>재생 시계에서 받은 시간으로 공통 플레이 Cue 타임라인을 진행한다.</summary>
    public sealed class PlayResolutionSequenceController
    {
        public PlayResolutionSequence Sequence { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double PreviousElapsedSeconds { get; private set; }
        public bool IsActive => Sequence != null;

        public void Begin(PlayResolutionSequence sequence)
        {
            Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            ElapsedSeconds = 0d;
            PreviousElapsedSeconds = 0d;
        }

        public bool Tick(double deltaSeconds)
        {
            if (!IsActive)
                return false;
            if (deltaSeconds < 0d || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            PreviousElapsedSeconds = ElapsedSeconds;
            ElapsedSeconds += deltaSeconds;
            if (ElapsedSeconds > Sequence.DurationSeconds)
                ElapsedSeconds = Sequence.DurationSeconds;
            return ElapsedSeconds >= Sequence.DurationSeconds;
        }

        public int GetRevealThroughEventIndex()
        {
            if (!IsActive)
                return -1;

            int result = -1;
            PlayResolutionCue[] cues = Sequence.Cues;
            for (int index = 0; index < cues.Length; index++)
            {
                PlayResolutionCue cue = cues[index];
                if (cue.StartSeconds > ElapsedSeconds || cue.RevealThroughEventIndex < 0)
                    continue;
                if (cue.RevealThroughEventIndex > result)
                    result = cue.RevealThroughEventIndex;
            }
            return result;
        }

        public void Complete()
        {
            Sequence = null;
            ElapsedSeconds = 0d;
            PreviousElapsedSeconds = 0d;
        }
    }
}

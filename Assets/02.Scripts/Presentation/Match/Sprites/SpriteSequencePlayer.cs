using System;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>주입된 시간만 사용하고 건너뛴 프레임 사건도 순서대로 전달하는 모션 시계다.</summary>
    public sealed class SpriteSequencePlayer
    {
        private SpriteClipDefinition _clip;
        private double _elapsed;
        private int _frame;
        private double _frameEnd;
        public event Action<SpriteAnimationEvent> EventEntered;
        public SpriteFrameDefinition CurrentFrame => _clip == null ? null : _clip.frames[_frame];
        public bool IsComplete { get; private set; }

        /// <summary>검수가 끝난 모션의 첫 프레임부터 재생한다.</summary>
        public void Play(SpriteClipDefinition clip)
        {
            if (clip == null || !clip.IsProductionReady) throw new ArgumentException("승인된 모션이 필요합니다.", nameof(clip));
            _clip = clip;
            _elapsed = 0;
            _frame = 0;
            _frameEnd = clip.frames[0].durationMs * 0.001d;
            IsComplete = false;
            Dispatch();
        }

        /// <summary>배속을 적용한 경과 시간만큼 모든 프레임 경계를 처리한다.</summary>
        public void Advance(double deltaSeconds, double speed = 1d)
        {
            if (deltaSeconds < 0 || speed < 0 || double.IsNaN(deltaSeconds) || double.IsNaN(speed) ||
                double.IsInfinity(deltaSeconds) || double.IsInfinity(speed)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (_clip == null || IsComplete) return;
            double step = deltaSeconds * speed;
            if (double.IsInfinity(step)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            _elapsed += step;
            while (_elapsed >= _frameEnd)
            {
                if (_frame == _clip.frames.Length - 1)
                {
                    if (!_clip.loop) { IsComplete = true; return; }
                    _frame = 0;
                }
                else _frame++;
                _frameEnd += _clip.frames[_frame].durationMs * 0.001d;
                Dispatch();
            }
        }

        /// <summary>시계 상태를 바꾸지 않고 절대 시각의 프레임을 구한다.</summary>
        public static SpriteFrameDefinition Sample(SpriteClipDefinition clip, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return null;
            seconds = Math.Max(0, seconds);
            if (clip.loop && clip.DurationSeconds > 0) seconds %= clip.DurationSeconds;
            float end = 0;
            for (int i = 0; i < clip.frames.Length; i++)
            {
                end += clip.frames[i].durationMs * 0.001f;
                if (seconds < end) return clip.frames[i];
            }
            return clip.frames[clip.frames.Length - 1];
        }

        private void Dispatch()
        {
            SpriteAnimationEvent[] markers = CurrentFrame.events;
            if (markers == null) return;
            for (int i = 0; i < markers.Length; i++) EventEntered?.Invoke(markers[i]);
        }
    }
}

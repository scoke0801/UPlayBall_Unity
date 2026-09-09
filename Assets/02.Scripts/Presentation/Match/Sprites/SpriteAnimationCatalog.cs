using System;
using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    public enum SpriteHandedness { NeedsReview, Shared, Left, Right }
    public enum SpriteAnimationEvent
    {
        BallRelease, SwingWindowOpen, BatContact, GloveContact, Transfer,
        ThrowRelease, ThrowReady, BatDrop, RunStart, Miss, Reaction
    }

    /// <summary>검수된 한 프레임의 그림과 체류 시간, 진입 사건이다.</summary>
    [Serializable]
    public sealed class SpriteFrameDefinition
    {
        public Sprite sprite;
        public float durationMs = 100f;
        public SpriteAnimationEvent[] events = Array.Empty<SpriteAnimationEvent>();
    }

    /// <summary>사건 프레임에서 검수한 접점을 trim 이전 셀 좌표로 보존한다.</summary>
    [Serializable]
    public sealed class SpriteEventAnchorDefinition
    {
        public SpriteAnimationEvent marker;
        public int frameIndex;
        public Vector2 sourcePositionNormalized;
        public Vector2 sourceCellSize;
        public Vector2 sourceRootNormalized = new Vector2(0.5f, 0.92f);

        /// <summary>좌상단 원점의 원본 셀 위치와 양수 셀 크기를 검증한다.</summary>
        public bool HasValidCoordinates => IsFinite(sourcePositionNormalized) && IsFinite(sourceRootNormalized) &&
            IsFinite(sourceCellSize) && sourceCellSize.x > 0 && sourceCellSize.y > 0;

        private static bool IsFinite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    /// <summary>순서가 확정된 이미지 모션의 읽기 전용 런타임 정의다.</summary>
    [Serializable]
    public sealed class SpriteClipDefinition
    {
        public string clipId;
        public string sourceHash;
        public SpriteHandedness handedness;
        public bool approved;
        public bool loop;
        public float referenceHeightPixels = 512f;
        public SpriteFrameDefinition[] frames = Array.Empty<SpriteFrameDefinition>();
        public SpriteEventAnchorDefinition[] eventAnchors = Array.Empty<SpriteEventAnchorDefinition>();

        public bool IsProductionReady
        {
            get
            {
                if (!approved || (int)handedness < (int)SpriteHandedness.Shared || (int)handedness > (int)SpriteHandedness.Right || string.IsNullOrEmpty(clipId) ||
                    frames == null || frames.Length == 0 || referenceHeightPixels <= 0 ||
                    float.IsNaN(referenceHeightPixels) || float.IsInfinity(referenceHeightPixels)) return false;
                for (int i = 0; i < frames.Length; i++)
                    if (frames[i] == null || frames[i].sprite == null || frames[i].durationMs <= 0f ||
                        float.IsNaN(frames[i].durationMs) || float.IsInfinity(frames[i].durationMs)) return false;
                if (!HasValidEventOrder() || float.IsInfinity(DurationSeconds)) return false;
                if (eventAnchors != null)
                    for (int i = 0; i < eventAnchors.Length; i++)
                    {
                        if (!IsValidAnchor(eventAnchors[i])) return false;
                        for (int j = 0; j < i; j++)
                            if (eventAnchors[j].marker == eventAnchors[i].marker) return false;
                    }
                return true;
            }
        }

        private bool HasValidEventOrder()
        {
            // Inspector에서 직접 바꾼 카탈로그도 가져오기 도구와 같은 인과 계약을 지켜야 한다.
            for (int i = 0; i < frames.Length; i++)
            {
                SpriteAnimationEvent[] markers = frames[i].events;
                if (markers == null) continue;
                for (int j = 0; j < markers.Length; j++)
                {
                    if (markers[j] < SpriteAnimationEvent.BallRelease || markers[j] > SpriteAnimationEvent.Reaction) return false;
                    for (int previous = 0; previous <= i; previous++)
                    {
                        SpriteAnimationEvent[] earlier = frames[previous].events;
                        if (earlier == null) continue;
                        int limit = previous == i ? j : earlier.Length;
                        for (int k = 0; k < limit; k++) if (earlier[k] == markers[j]) return false;
                    }
                }
            }
            return IsOrdered(SpriteAnimationEvent.SwingWindowOpen, SpriteAnimationEvent.BatContact) &&
                IsOrdered(SpriteAnimationEvent.GloveContact, SpriteAnimationEvent.Transfer) &&
                IsOrdered(SpriteAnimationEvent.GloveContact, SpriteAnimationEvent.ThrowRelease) &&
                IsOrdered(SpriteAnimationEvent.Transfer, SpriteAnimationEvent.ThrowRelease) &&
                IsOrdered(SpriteAnimationEvent.GloveContact, SpriteAnimationEvent.ThrowReady) &&
                IsOrdered(SpriteAnimationEvent.BatDrop, SpriteAnimationEvent.RunStart);
        }

        private bool IsOrdered(SpriteAnimationEvent first, SpriteAnimationEvent second) =>
            !TryGetEventTime(first, out float firstTime) || !TryGetEventTime(second, out float secondTime) || firstTime <= secondTime;

        public float DurationSeconds
        {
            get
            {
                float total = 0;
                if (frames != null)
                    for (int i = 0; i < frames.Length; i++) total += frames[i].durationMs * 0.001f;
                return total;
            }
        }

        /// <summary>해당 사건이 시작되는 모션 내부 시각을 반환한다.</summary>
        public bool TryGetEventTime(SpriteAnimationEvent marker, out float seconds)
        {
            seconds = 0;
            if (frames == null) return false;
            for (int i = 0; i < frames.Length; i++)
            {
                SpriteAnimationEvent[] markers = frames[i].events;
                if (markers != null)
                    for (int j = 0; j < markers.Length; j++)
                        if (markers[j] == marker) return true;
                seconds += frames[i].durationMs * 0.001f;
            }
            seconds = 0;
            return false;
        }

        /// <summary>그 사건이 실제 존재하는 프레임에 연결된 원본 셀 접점만 반환한다.</summary>
        public bool TryGetEventAnchor(SpriteAnimationEvent marker, out SpriteEventAnchorDefinition anchor)
        {
            anchor = null;
            if (eventAnchors == null) return false;
            for (int i = 0; i < eventAnchors.Length; i++)
            {
                SpriteEventAnchorDefinition candidate = eventAnchors[i];
                if (candidate == null || candidate.marker != marker) continue;
                if (anchor != null || !IsValidAnchor(candidate)) { anchor = null; return false; }
                anchor = candidate;
            }
            return anchor != null;
        }

        private bool IsValidAnchor(SpriteEventAnchorDefinition anchor)
        {
            if (anchor == null || !anchor.HasValidCoordinates || frames == null || anchor.frameIndex < 0 ||
                anchor.frameIndex >= frames.Length || frames[anchor.frameIndex] == null) return false;
            SpriteAnimationEvent[] markers = frames[anchor.frameIndex].events;
            if (markers == null) return false;
            for (int i = 0; i < markers.Length; i++) if (markers[i] == anchor.marker) return true;
            return false;
        }
    }

    /// <summary>승인되지 않은 모션이 실제 경기로 유입되지 않게 차단하는 표현 카탈로그다.</summary>
    public sealed class SpriteAnimationCatalog : ScriptableObject
    {
        public SpriteClipDefinition[] clips = Array.Empty<SpriteClipDefinition>();
        public FieldLayoutDefinition fieldLayout;

        /// <summary>중복 ID와 미검수 모션은 실패로 처리한다.</summary>
        public bool TryGetClip(string id, out SpriteClipDefinition clip)
        {
            clip = null;
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null || clips[i].clipId != id) continue;
                if (clip != null || !clips[i].IsProductionReady) { clip = null; return false; }
                clip = clips[i];
            }
            return clip != null;
        }
    }
}

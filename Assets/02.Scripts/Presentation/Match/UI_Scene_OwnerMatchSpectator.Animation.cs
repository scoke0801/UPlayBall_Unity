using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private const float VisualFrameDurationSeconds = 0.13f;
        private readonly Texture2D[] _visualFrames = new Texture2D[8];
        private readonly bool[] _visualFrameMirrors = new bool[8];
        private int _visualFrameCount;
        private float _visualElapsedSeconds;
        private bool _isVisualSequencePlaying;
        private int _lastAnimatedPitchSequence = -1;

        private void StartStadiumAnimation(MatchEvent matchEvent)
        {
            if (!TryFindLatestPitch(matchEvent, out MatchEvent pitchEvent) ||
                pitchEvent.Sequence == _lastAnimatedPitchSequence)
                return;

            _lastAnimatedPitchSequence = pitchEvent.Sequence;
            OwnerMatchHandedness handedness = _session.GetHandedness(pitchEvent.PitcherId, pitchEvent.BatterId);
            Texture2D[] overlaySet = handedness.UsesRightPitcherLeftBatterSet
                ? _leftBatterOverlays
                : _rightBatterOverlays;
            bool mirror = handedness.IsPitcherLeftHanded;
            _visualFrameCount = 0;
            AddVisualFrame(overlaySet[0], mirror);
            AddVisualFrame(overlaySet[1], mirror);
            AddVisualFrame(overlaySet[2], mirror);
            AddVisualFrame(overlaySet[3], mirror);
            AddVisualFrame(overlaySet[4], mirror);
            AddVisualFrame(overlaySet[ResolveOutcomeFrame(pitchEvent.PitchResult)], mirror);

            _visualElapsedSeconds = 0f;
            _isVisualSequencePlaying = _visualFrameCount > 1;
            SetLayerTexture(_actors, _visualFrames[0], _visualFrameMirrors[0]);
            HideBlendLayer();
        }

        private bool TryFindLatestPitch(in MatchEvent boundary, out MatchEvent pitchEvent)
        {
            if (boundary.EventType == MatchEventType.Pitch)
            {
                pitchEvent = boundary;
                return true;
            }

            if (boundary.EventType is not (MatchEventType.PlateAppearanceEnded or MatchEventType.FieldingError or
                MatchEventType.ThrowingError))
            {
                pitchEvent = default;
                return false;
            }

            for (int index = _session.State.VisibleEventCount - 1; index >= 0; index--)
            {
                MatchEvent candidate = _session.GetVisibleEvent(index);
                if (candidate.EventType == MatchEventType.Pitch)
                {
                    pitchEvent = candidate;
                    return true;
                }
            }

            pitchEvent = default;
            return false;
        }

        private static int ResolveOutcomeFrame(PitchResult result)
        {
            return result switch
            {
                PitchResult.SwingingStrike => 6,
                PitchResult.Foul or PitchResult.InPlay => 5,
                _ => 7
            };
        }

        private void UpdateStadiumAnimation()
        {
            if (!_isVisualSequencePlaying || _session == null || _session.State.IsPaused)
                return;

            _visualElapsedSeconds += Time.unscaledDeltaTime * (int)_session.State.Speed;
            float framePosition = _visualElapsedSeconds / VisualFrameDurationSeconds;
            int frameIndex = Mathf.FloorToInt(framePosition);
            if (frameIndex >= _visualFrameCount - 1)
            {
                SetLayerTexture(_actors, _visualFrames[_visualFrameCount - 1],
                    _visualFrameMirrors[_visualFrameCount - 1]);
                HideBlendLayer();
                _isVisualSequencePlaying = false;
                return;
            }

            SetLayerTexture(_actors, _visualFrames[frameIndex], _visualFrameMirrors[frameIndex]);
            SetLayerTexture(_actorsBlend, _visualFrames[frameIndex + 1], _visualFrameMirrors[frameIndex + 1]);
            float transition = Mathf.Clamp01((framePosition - frameIndex - 0.5f) * 2f);
            transition = transition * transition * (3f - 2f * transition);
            _actorsBlend.color = new Color(1f, 1f, 1f, transition);
        }

        private void AddVisualFrame(Texture2D texture, bool mirror)
        {
            if (texture != null && _visualFrameCount < _visualFrames.Length)
            {
                _visualFrames[_visualFrameCount] = texture;
                _visualFrameMirrors[_visualFrameCount++] = mirror;
            }
        }

        private void SetActorTexture(Texture2D texture, bool mirror)
        {
            _isVisualSequencePlaying = false;
            SetLayerTexture(_actors, texture, mirror);
            HideBlendLayer();
        }

        private void HideBlendLayer()
        {
            if (_actorsBlend != null)
                _actorsBlend.color = new Color(1f, 1f, 1f, 0f);
        }

        private static void SetLayerTexture(RawImage layer, Texture2D texture, bool mirror)
        {
            if (layer == null)
                return;

            layer.texture = texture;
            if (texture == null)
                return;

            float sourceAspect = (float)texture.width / texture.height;
            // Footer 아래까지 배경을 이어 전체 수비 전경의 포수가 세로 크롭에 잘리지 않게 한다.
            float targetAspect = 1440f / 748f;
            float visibleHeight = sourceAspect / targetAspect;
            Rect uv = visibleHeight < 1f
                ? new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight)
                : new Rect((1f - 1f / visibleHeight) * 0.5f, 0f, 1f / visibleHeight, 1f);
            if (mirror)
                uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);
            layer.uvRect = uv;
        }
    }
}

using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private const float VisualFrameDurationSeconds = 0.2f;
        private readonly Texture2D[] _visualFrames = new Texture2D[4];
        private int _visualFrameCount;
        private float _visualElapsedSeconds;
        private bool _isVisualSequencePlaying;

        private void StartStadiumAnimation(MatchEvent matchEvent)
        {
            if (matchEvent.EventType != MatchEventType.PlateAppearanceEnded)
                return;

            OwnerMatchVisualSequenceKind kind =
                OwnerMatchVisualSequenceResolver.Resolve(matchEvent.PlateAppearanceResult);
            _visualFrameCount = 0;
            AddVisualFrame(_pitchRelease ?? _pitchView);
            switch (kind)
            {
                case OwnerMatchVisualSequenceKind.SwingMiss:
                    AddVisualFrame(_swingMiss ?? _pitchView);
                    break;
                case OwnerMatchVisualSequenceKind.ContactOnly:
                    AddVisualFrame(_batContact ?? _pitchView);
                    break;
                case OwnerMatchVisualSequenceKind.BallCaught:
                    AddVisualFrame(_batContact ?? _pitchView);
                    AddVisualFrame(_ballFlight ?? _overview);
                    AddVisualFrame(_ballCaught ?? _overview);
                    break;
                case OwnerMatchVisualSequenceKind.SafeHit:
                    AddVisualFrame(_batContact ?? _pitchView);
                    AddVisualFrame(_ballFlight ?? _overview);
                    AddVisualFrame(_safeHit ?? _overview);
                    break;
            }

            _visualElapsedSeconds = 0f;
            _isVisualSequencePlaying = _visualFrameCount > 1;
            SetLayerTexture(_stadium, _visualFrames[0]);
            HideBlendLayer();
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
                SetLayerTexture(_stadium, _visualFrames[_visualFrameCount - 1]);
                HideBlendLayer();
                _isVisualSequencePlaying = false;
                return;
            }

            SetLayerTexture(_stadium, _visualFrames[frameIndex]);
            SetLayerTexture(_stadiumBlend, _visualFrames[frameIndex + 1]);
            float transition = Mathf.Clamp01((framePosition - frameIndex - 0.5f) * 2f);
            transition = transition * transition * (3f - 2f * transition);
            _stadiumBlend.color = new Color(1f, 1f, 1f, transition);
        }

        private void AddVisualFrame(Texture2D texture)
        {
            if (texture != null && _visualFrameCount < _visualFrames.Length)
                _visualFrames[_visualFrameCount++] = texture;
        }

        private void HideBlendLayer()
        {
            if (_stadiumBlend != null)
                _stadiumBlend.color = new Color(1f, 1f, 1f, 0f);
        }

        private static void SetLayerTexture(RawImage layer, Texture2D texture)
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
            layer.uvRect = visibleHeight < 1f
                ? new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight)
                : new Rect((1f - 1f / visibleHeight) * 0.5f, 0f, 1f / visibleHeight, 1f);
        }
    }
}

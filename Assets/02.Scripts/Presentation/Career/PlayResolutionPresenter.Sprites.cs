using Baseball.Core.Players;
using Baseball.Presentation.Match.Sprites;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public sealed partial class PlayResolutionPresenter
    {
        private SpriteMatchStage _spriteStage;
        private Handedness _throwingHand = Handedness.Right;

        /// <summary>실제 투수의 투구손을 모션 선택에 전달한다.</summary>
        public void SetThrowingHand(Handedness throwingHand) { _throwingHand = throwingHand; }

        private void InitializeSpriteStage()
        {
            SpriteAnimationCatalog catalog = Resources.Load<SpriteAnimationCatalog>("UI/SpriteMatch/AnimationCatalog");
            if (catalog == null || catalog.fieldLayout == null || catalog.fieldLayout.background == null) return;
            var host = new GameObject("SpriteFieldView", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(_root, false);
            host.sizeDelta = _plateView.sizeDelta;
            host.anchoredPosition = _plateView.anchoredPosition;
            host.SetAsFirstSibling();
            _spriteStage = new SpriteMatchStage(host, catalog, Resources.Load<Sprite>("UI/MiniGame/img_baseball_ball"));
        }

        private void PrepareSpriteStage()
        {
            if (_spriteStage == null) return;
            if (_spriteStage.SetHands(_throwingHand, _isLeftHandedBatter ? Handedness.Left : Handedness.Right))
                _spriteStage.Reset();
        }

        private void RenderSpriteSequence(PlayResolutionSequence sequence, double elapsed)
        {
            _spriteStage.Reset();
            double pitchEnd = 0;
            for (int i = 0; i < sequence.Cues.Length; i++)
            {
                PlayResolutionCue cue = sequence.Cues[i];
                if (cue.Type is PlayResolutionCueType.BatterSwing or PlayResolutionCueType.BatterTake) pitchEnd = cue.EndSeconds;
                if (cue.Type == PlayResolutionCueType.Contact) { pitchEnd = cue.StartSeconds; break; }
            }
            _spriteStage.RenderPitch(pitchEnd > 0 ? Mathf.Clamp01((float)(elapsed / pitchEnd)) : 1f,
                sequence.PitchPlay.Swing.DidSwing,
                sequence.PitchPlay.Contact.PitchResult is Baseball.Simulation.PlateAppearance.PitchResult.InPlay or
                    Baseball.Simulation.PlateAppearance.PitchResult.Foul);
            for (int i = 0; i < _runnerIds.Length; i++)
                if (_runnerIds[i] > 0 && _runnerInitialBases[i] > 0)
                    _spriteStage.RenderRunner(i, _runnerInitialBases[i], _runnerInitialBases[i], 1f);
            for (int i = 0; i < sequence.Cues.Length; i++)
            {
                PlayResolutionCue cue = sequence.Cues[i];
                if (elapsed < cue.StartSeconds) continue;
                float progress = EvaluateProgress(cue, elapsed);
                switch (cue.Type)
                {
                    case PlayResolutionCueType.BattedBallFlight:
                        _spriteStage.RenderContact(sequence.BallInPlay, progress);
                        break;
                    case PlayResolutionCueType.Throw:
                        Vector2 throwStart = _spriteStage.Projection.Project(cue.StartPoint);
                        Vector2 target = _spriteStage.Projection.Project(PlayResolutionFieldLayout.GetBattedBallTarget(sequence.BallInPlay.BattedBall));
                        if ((throwStart - target).sqrMagnitude < 0.0001f)
                            _spriteStage.RenderFieldThrow(sequence.BallInPlay, _spriteStage.Projection.Project(cue.EndPoint), progress);
                        else
                            _spriteStage.RenderBallFlight(throwStart, _spriteStage.Projection.Project(cue.EndPoint), progress, 0.025f);
                        break;
                    case PlayResolutionCueType.FoulBall:
                        _spriteStage.RenderBallFlight(_spriteStage.Projection.Project(cue.StartPoint),
                            _spriteStage.Projection.Project(cue.EndPoint), progress, 0.08f);
                        break;
                    case PlayResolutionCueType.RunnerMove:
                        for (int slot = 0; slot < _runnerIds.Length; slot++)
                            if (_runnerIds[slot] == cue.PlayerId)
                                _spriteStage.RenderRunner(slot, cue.FromBase, cue.ToBase, progress);
                        break;
                    case PlayResolutionCueType.PlateCall:
                        _spriteStage.HideBall();
                        break;
                }
            }
        }
    }
}

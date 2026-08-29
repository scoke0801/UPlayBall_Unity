using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_CareerPresentation
    {
        private Sequence _mainSequence;
        private Sequence _idleSequence;
        private bool _isMotionComplete;

        private bool IsMotionComplete => _isMotionComplete;

        private void PlayMotion(CareerPresentationRequest request, CareerPresentationData data)
        {
            KillMotion();
            CareerMotionPreset preset = ResolveMotionPreset(request.Type, data);
            ResetMotionState(request, preset);
            CareerPresentationMode mode = CareerPresentationSettings.Mode;
            if (mode == CareerPresentationMode.ResultOnly)
            {
                ShowFinalState();
                _isMotionComplete = true;
                return;
            }

            if (request.Grade == CareerPresentationGrade.Major)
                BuildMajorSequence(request, data, preset, mode).Play();
            else
                BuildActivitySequence(request, data, preset, mode).Play();
        }

        private Sequence BuildMajorSequence(
            CareerPresentationRequest request,
            CareerPresentationData data,
            CareerMotionPreset preset,
            CareerPresentationMode mode)
        {
            float speed = mode == CareerPresentationMode.Simplified ? 0.58f : 1f;
            Vector2 heroBase = new(500f, 0f);
            Vector2 titleBase = new(-500f, 0f);

            _mainSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).Pause();
            _mainSequence
                .Append(Fade(_blackFade, 0f, 0.25f * speed))
                .Insert(0.10f * speed, Scale(_backgroundRoot, new Vector3(1.04f, 1.04f, 1f), 1.20f * speed, Ease.OutCubic))
                .Insert(0.15f * speed, Scale(_heroRoot, Vector3.one, 1.05f * speed, Ease.OutCubic))
                .Insert(0.15f * speed, Move(_heroRoot, heroBase, 1.05f * speed, Ease.OutCubic))
                .Insert(0.45f * speed, Fade(_categoryCanvasGroup, 1f, 0.35f * speed))
                .Insert(0.60f * speed, Move(_titleRoot, titleBase, 0.45f * speed, Ease.OutQuart))
                .Insert(0.72f * speed, Fade(_titleCanvasGroup, 1f, 0.40f * speed))
                .Insert(0.92f * speed, Fade(_descriptionCanvasGroup, 1f, 0.38f * speed))
                .Insert(1.10f * speed, Fade(_statCanvasGroup, 1f, 0.30f * speed))
                .Insert(1.12f * speed, BuildStatRevealSequence(speed));

            if (preset == CareerMotionPreset.Award)
                _mainSequence.Insert(1.50f * speed, BuildShineSequence(speed));
            if (_confettiRoot.gameObject.activeSelf)
                _mainSequence.InsertCallback(1.40f * speed, StartConfetti);
            if (preset == CareerMotionPreset.Championship)
                _mainSequence.Insert(1.25f * speed, BuildChampionShake(heroBase, speed));

            float continueTime = mode == CareerPresentationMode.Simplified ? 1.25f : 3.20f;
            _mainSequence.Insert(continueTime, Fade(_continueCanvasGroup, 1f, 0.25f));
            float targetDuration = mode == CareerPresentationMode.Simplified
                ? 2.5f
                : data?.DefaultDuration ?? 4.5f;
            float occupied = _mainSequence.Duration();
            if (targetDuration > occupied)
                _mainSequence.AppendInterval(targetDuration - occupied);
            _mainSequence.OnComplete(() =>
            {
                _isMotionComplete = true;
                StartIdleMotion(preset);
            });
            return _mainSequence;
        }

        private Sequence BuildActivitySequence(
            CareerPresentationRequest request,
            CareerPresentationData data,
            CareerMotionPreset preset,
            CareerPresentationMode mode)
        {
            bool compact = request.Grade == CareerPresentationGrade.Compact;
            float duration = compact
                ? mode == CareerPresentationMode.Simplified ? 1f : 1.4f
                : mode == CareerPresentationMode.Simplified ? 1f : data?.DefaultDuration ?? 3f;
            float timeScale = compact ? 0.60f : mode == CareerPresentationMode.Simplified ? 0.55f : 1f;
            Vector2 heroBase = new(500f, 0f);
            Vector2 titleBase = new(-500f, 0f);

            _mainSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).Pause();
            _mainSequence
                .Append(Fade(_blackFade, 0f, 0.20f * timeScale))
                .Insert(0.10f * timeScale, Scale(_heroRoot, Vector3.one, 0.70f * timeScale, Ease.OutCubic))
                .Insert(0.10f * timeScale, Move(_heroRoot, heroBase, 0.70f * timeScale, Ease.OutCubic))
                .Insert(0.30f * timeScale, Fade(_categoryCanvasGroup, 1f, 0.25f * timeScale))
                .Insert(0.40f * timeScale, Move(_titleRoot, titleBase, 0.36f * timeScale, Ease.OutQuart))
                .Insert(0.40f * timeScale, Fade(_titleCanvasGroup, 1f, 0.28f * timeScale))
                .Insert(0.58f * timeScale, Fade(_descriptionCanvasGroup, 1f, 0.26f * timeScale))
                .Insert(0.78f * timeScale, Fade(_statCanvasGroup, 1f, 0.26f * timeScale))
                .Insert(0.80f * timeScale, BuildStatRevealSequence(timeScale));

            if (preset == CareerMotionPreset.Travel)
            {
                _mainSequence.Insert(
                    0.10f * timeScale,
                    Move(_backgroundRoot, new Vector2(-40f, 0f), Math.Max(1f, duration - 0.2f), Ease.InOutSine));
            }
            else if (preset == CareerMotionPreset.Rest)
            {
                _mainSequence.Insert(
                    0.10f * timeScale,
                    Scale(_backgroundRoot, Vector3.one, Math.Max(0.8f, duration - 0.2f), Ease.InOutSine));
            }

            float continueTime = Math.Min(duration - 0.25f, compact ? 0.95f : 2.30f);
            _mainSequence.Insert(Math.Max(0.45f, continueTime), Fade(_continueCanvasGroup, 1f, 0.18f));
            float occupied = Math.Max(0.8f, _mainSequence.Duration());
            if (duration > occupied)
                _mainSequence.AppendInterval(duration - occupied);
            _mainSequence.OnComplete(() =>
            {
                _isMotionComplete = true;
                BeginExit();
            });
            return _mainSequence;
        }

        private Sequence BuildStatRevealSequence(float speed)
        {
            Sequence reveal = DOTween.Sequence();
            for (int index = 0; index < _statContainer.childCount; index++)
            {
                RectTransform row = (RectTransform)_statContainer.GetChild(index);
                CanvasGroup canvas = row.GetComponent<CanvasGroup>();
                row.localScale = new Vector3(0.92f, 0.92f, 1f);
                canvas.alpha = 0f;
                float at = index * 0.12f * speed;
                reveal.Insert(at, Fade(canvas, 1f, 0.22f * speed));
                reveal.Insert(at, Scale(row, Vector3.one, 0.30f * speed, Ease.OutBack));
            }
            return reveal;
        }

        private Sequence BuildShineSequence(float speed)
        {
            _shineRoot.anchoredPosition = new Vector2(-560f, 0f);
            _shineImage.color = new Color(1f, 0.88f, 0.48f, 0f);
            return DOTween.Sequence()
                .Append(Fade(_shineImage, 0.62f, 0.15f * speed))
                .Join(Move(_shineRoot, new Vector2(560f, 0f), 0.70f * speed, Ease.InOutSine))
                .Append(Fade(_shineImage, 0f, 0.12f * speed));
        }

        private Sequence BuildChampionShake(Vector2 basePosition, float speed)
        {
            return DOTween.Sequence()
                .Append(Move(_heroRoot, basePosition + new Vector2(9f, 2f), 0.045f * speed, Ease.Linear))
                .Append(Move(_heroRoot, basePosition + new Vector2(-7f, -2f), 0.045f * speed, Ease.Linear))
                .Append(Move(_heroRoot, basePosition + new Vector2(5f, 1f), 0.045f * speed, Ease.Linear))
                .Append(Move(_heroRoot, basePosition, 0.045f * speed, Ease.Linear));
        }

        private void StartConfetti()
        {
            CanvasGroup group = _confettiRoot.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            for (int index = 0; index < _confettiRoot.childCount; index++)
            {
                RectTransform piece = (RectTransform)_confettiRoot.GetChild(index);
                Vector2 start = piece.anchoredPosition;
                float drift = index % 2 == 0 ? 85f : -65f;
                DOTween.To(
                        () => piece.anchoredPosition,
                        value => piece.anchoredPosition = value,
                        new Vector2(start.x + drift, -620f),
                        2.8f + index % 5 * 0.23f)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart)
                    .SetUpdate(true)
                    .SetTarget(this);
            }
        }

        private void StartIdleMotion(CareerMotionPreset preset)
        {
            _idleSequence?.Kill();
            float scale = preset == CareerMotionPreset.Rest ? 1.012f : 1.008f;
            _idleSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            _idleSequence
                .Append(Scale(_heroRoot, new Vector3(scale, scale, 1f), 1.75f, Ease.InOutSine))
                .Append(Scale(_heroRoot, Vector3.one, 1.75f, Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart);
        }

        private void ResetMotionState(
            CareerPresentationRequest request,
            CareerMotionPreset preset)
        {
            _isMotionComplete = false;
            _rootCanvasGroup.alpha = 1f;
            SetAlpha(_blackFade, 1f);
            _backgroundRoot.anchoredPosition = preset == CareerMotionPreset.Travel
                ? new Vector2(40f, 0f)
                : Vector2.zero;
            float backgroundScale = preset == CareerMotionPreset.Rest ? 1.03f : 1.10f;
            _backgroundRoot.localScale = new Vector3(backgroundScale, backgroundScale, 1f);
            _heroRoot.anchoredPosition = new Vector2(
                540f,
                request.Type is CareerPresentationType.RegularSeasonFirst or CareerPresentationType.RegularSeasonMvp
                    ? -25f
                    : 0f);
            _heroRoot.localScale = new Vector3(1.05f, 1.05f, 1f);
            _titleRoot.anchoredPosition = new Vector2(-540f, 0f);
            _titleRoot.localScale = Vector3.one;
            _categoryCanvasGroup.alpha = 0f;
            _titleCanvasGroup.alpha = 0f;
            _descriptionCanvasGroup.alpha = 0f;
            _statCanvasGroup.alpha = 0f;
            _continueCanvasGroup.alpha = 0f;
            _continueCanvasGroup.interactable = false;
            _continueCanvasGroup.blocksRaycasts = false;
            _confettiRoot.GetComponent<CanvasGroup>().alpha = 0f;
            SetAlpha(_shineImage, 0f);
        }

        private void ShowFinalState()
        {
            SetAlpha(_blackFade, 0f);
            _backgroundRoot.localScale = Vector3.one;
            _backgroundRoot.anchoredPosition = Vector2.zero;
            _heroRoot.localScale = Vector3.one;
            _heroRoot.anchoredPosition = new Vector2(500f, 0f);
            _titleRoot.anchoredPosition = new Vector2(-500f, 0f);
            _categoryCanvasGroup.alpha = 1f;
            _titleCanvasGroup.alpha = 1f;
            _descriptionCanvasGroup.alpha = 1f;
            _statCanvasGroup.alpha = 1f;
            _continueCanvasGroup.alpha = 1f;
            _continueCanvasGroup.interactable = true;
            _continueCanvasGroup.blocksRaycasts = true;
            for (int index = 0; index < _statContainer.childCount; index++)
            {
                RectTransform row = (RectTransform)_statContainer.GetChild(index);
                row.localScale = Vector3.one;
                row.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        private void PlayExitMotion(Action completed)
        {
            KillMotion();
            _mainSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            _mainSequence
                .Append(Fade(_blackFade, 1f, 0.25f).SetEase(Ease.InCubic))
                .OnComplete(() => completed?.Invoke());
        }

        private void KillMotion()
        {
            _mainSequence?.Kill();
            _idleSequence?.Kill();
            _mainSequence = null;
            _idleSequence = null;
            DOTween.Kill(this);
        }

        private static Tween Fade(CanvasGroup group, float endValue, float duration)
        {
            return DOTween.To(() => group.alpha, value => group.alpha = value, endValue, duration)
                .OnUpdate(() =>
                {
                    bool visible = group.alpha > 0.01f;
                    group.interactable = visible;
                    group.blocksRaycasts = visible;
                });
        }

        private static Tween Fade(Image image, float endValue, float duration)
        {
            return DOTween.To(
                () => image.color.a,
                value => SetAlpha(image, value),
                endValue,
                duration);
        }

        private static Tween Move(RectTransform rect, Vector2 endValue, float duration, Ease ease)
        {
            return DOTween.To(
                    () => rect.anchoredPosition,
                    value => rect.anchoredPosition = value,
                    endValue,
                    duration)
                .SetEase(ease);
        }

        private static Tween Scale(RectTransform rect, Vector3 endValue, float duration, Ease ease)
        {
            return DOTween.To(
                    () => rect.localScale,
                    value => rect.localScale = value,
                    endValue,
                    duration)
                .SetEase(ease);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}

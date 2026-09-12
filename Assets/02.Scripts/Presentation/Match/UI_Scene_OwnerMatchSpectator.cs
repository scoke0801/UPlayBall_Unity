using System;
using Baseball.Game.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    /// <summary>공식 사건의 재생과 공개 시점을 맞추는 구단주 2D 경기 관전 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Scene_OwnerMatchSpectator : MonoBehaviour, IMatchHudView
    {
        private RectTransform _root;
        private RectTransform _canvas;
        private OwnerMatchSpectatorSession _session;
        private float _nextAutomaticAdvanceAt;
        private bool _showResults;
        private bool _showPitching;
        private bool _showHomeRecords;
        private bool _wasComplete;
        private int _lastVisibleCount = -1;

        public event Action HomeRequested;
        public event Action PresentationCompleted;
        /// <summary>경기 기록으로 복귀할 때 완료 화면의 기본 행동에 포커스를 복원한다.</summary>
        public void FocusCompletedResult()
        {
            if (IsComplete) _homeButton.Select();
        }
        public event Action<bool> MatchAudioEnabledChanged;
        public bool IsPresenting { get; private set; }
        public bool IsComplete => _session?.State.IsComplete == true;
        public MatchHudPresentationModel CurrentModel { get; private set; }

        /// <summary>공용 Shell의 Workspace에 비율을 유지하는 관전 화면을 생성한다.</summary>
        public static UI_Scene_OwnerMatchSpectator CreateRuntime(RectTransform workspaceHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            var go = new GameObject(nameof(UI_Scene_OwnerMatchSpectator), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(workspaceHost, false);
            var root = go.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = Ink;
            var view = go.AddComponent<UI_Scene_OwnerMatchSpectator>();
            view._root = root;
            view.Build();
            view.SetVisible(false);
            return view;
        }

        /// <summary>경기 한 건을 확정하고 동일 이벤트의 처음부터 관전한다.</summary>
        public void PlayNextGame(OwnerModeManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            _showResults = _showPitching = _showHomeRecords = _wasComplete = false;
            _lastVisibleCount = -1;
            ResetGameCast();
            bool isPostseason = manager.IsNextPostseasonGamePlayerMatch;
            _homeButton.GetComponentInChildren<Text>().text = isPostseason ? "대진으로 돌아가기" : "구단 홈으로";
            _session = OwnerMatchSpectatorSession.PlayNextGame(manager, this);
            OwnerMatchPresentationOptions settings = OwnerMatchPresentationSettings.Load();
            _session.TrySetPlaybackSpeed(settings.PlaybackSpeed);
            _session.TrySetViewingMode(isPostseason && settings.ViewingMode == OwnerMatchViewingMode.ResultOnly
                ? OwnerMatchViewingMode.KeyMoments : settings.ViewingMode);
            IsPresenting = true;
            SetVisible(true);
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        /// <summary>공용 HUD 계약의 현재 공개 점수와 참가자를 표시한다.</summary>
        public void Present(MatchHudPresentationModel model)
        {
            CurrentModel = model ?? throw new ArgumentNullException(nameof(model));
            RenderHud(model);
        }

        /// <summary>관전 화면의 표시 여부를 변경한다.</summary>
        public void SetVisible(bool isVisible)
        {
            if (!isVisible) ClearHighlightInset();
            if (_root != null) _root.gameObject.SetActive(isVisible);
        }

        /// <summary>홈 복귀 시 관전 표시를 종료한다.</summary>
        public void EndPresentation()
        {
            IsPresenting = false;
            SetVisible(false);
        }

        private void Update()
        {
            FitWorkspace();
            if (!IsPresenting || _session == null) return;
            var state = _session.State;
            if (state.IsPaused || state.IsComplete || Time.unscaledTime < _nextAutomaticAdvanceAt) return;
            UpdateGameCast(Time.unscaledDeltaTime);
        }

        private void FitWorkspace()
        {
            if (_canvas == null || _root == null) return;
            float scale = Mathf.Min(_root.rect.width / 1440f, _root.rect.height / 810f);
            _canvas.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        private void HandlePauseRequested()
        {
            if (_session?.TryTogglePause() != true) return;
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleSpeedRequested(OwnerMatchPlaybackSpeed speed)
        {
            if (_session?.TrySetPlaybackSpeed(speed) != true) return;
            OwnerMatchPresentationSettings.SetPlaybackSpeed(speed);
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleAdvanceRequested()
        {
            if (_session?.TryAdvance() != true) return;
            ClearHighlightInset();
            _hasPendingEvent = false;
            _playbackBoundary = -1;
            _zoneBall.gameObject.SetActive(false);
            _playVisualizer.Reset();
            _playVisualizer.PresentBases(CurrentModel);
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleRevealAllRequested()
        {
            if (_session?.TrySetViewingMode(OwnerMatchViewingMode.ResultOnly) != true) return;
            ClearHighlightInset();
            _hasPendingEvent = false;
            _zoneBall.gameObject.SetActive(false);
            RefreshControls();
        }

        private void HandleViewingModeRequested(OwnerMatchViewingMode mode)
        {
            if (_session?.TrySetViewingMode(mode) != true) return;
            ClearHighlightInset();
            OwnerMatchPresentationSettings.SetViewingMode(mode);
            MatchAudioEnabledChanged?.Invoke(mode != OwnerMatchViewingMode.ResultOnly);
            _hasPendingEvent = false;
            _playbackBoundary = -1;
            _zoneBall.gameObject.SetActive(false);
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void ScheduleNextAutomaticAdvance()
        {
            if (_session != null)
                _nextAutomaticAdvanceAt = Time.unscaledTime + OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(_session.State.Speed);
        }
    }
}

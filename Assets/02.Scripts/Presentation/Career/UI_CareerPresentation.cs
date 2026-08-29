using System;
using Baseball.Core.Growth;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baseball.Presentation.Career
{
    /// <summary>시즌의 주요 결과와 오프시즌 활동을 동일한 이미지·UI·Tween 화면에서 순서대로 재생한다.</summary>
    public sealed partial class UI_CareerPresentation : UIPopupBase
    {
        private readonly CareerPresentationQueue _queue = new();

        private CareerManager _manager;
        private CareerState _observedCareer;
        private int _observedGrowthCount;
        private int _observedOffseasonWeek = 1;
        private CareerPresentationRequest _currentRequest;
        private CareerPresentationData _currentData;
        private float _dismissAllowedAt;
        private bool _isTransitioning;
        private int _replaySequence;

        public static UI_CareerPresentation Instance { get; private set; }
        public static bool IsPlaying => Instance != null && Instance.IsVisible;
        public override bool CanCloseWithCancel => false;

        /// <summary>프리팹을 찾지 못한 개발 환경에서도 같은 컴포넌트를 런타임 생성한다.</summary>
        public static UI_CareerPresentation CreateRuntime(Transform parent)
        {
            var root = new GameObject(
                nameof(UI_CareerPresentation),
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            Stretch(rect);
            return root.AddComponent<UI_CareerPresentation>();
        }

        protected override void OnInitialize()
        {
            Instance = this;
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            BuildHierarchy();
            ResumeObservation();
        }

        protected override void OnHide()
        {
            KillMotion();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            KillMotion();
            if (ReferenceEquals(Instance, this))
                Instance = null;
            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsVisible || _currentRequest == null || _isTransitioning)
                return;

            bool confirmPressed = Keyboard.current != null &&
                                  (Keyboard.current.enterKey.wasPressedThisFrame ||
                                   Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                                   Keyboard.current.spaceKey.wasPressedThisFrame ||
                                   Keyboard.current.escapeKey.wasPressedThisFrame);
            confirmPressed |= Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            confirmPressed |= Gamepad.current != null &&
                              (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                               Gamepad.current.startButton.wasPressedThisFrame);
            if (confirmPressed)
                TryDismiss();
        }

        /// <summary>관리 Scene 재진입 시 현재 저장 상태를 기준으로 관찰을 다시 시작한다.</summary>
        public void ResumeObservation()
        {
            if (_manager == null || !_manager.HasActiveCareer)
                return;
            ObserveCareer(_manager.CurrentCareer);
            TryQueueSeasonReview();
            TryPlayNext();
        }

        /// <summary>다른 Scene으로 이동할 때 콜백을 실행하지 않고 런타임 연출 상태만 비운다.</summary>
        public void Suspend()
        {
            KillMotion();
            _queue.Clear();
            _currentRequest = null;
            _currentData = null;
            _observedCareer = null;
            _isTransitioning = false;
            Hide();
        }

        /// <summary>커리어 기록 화면 등에서 이미 적용된 결과를 보상 없이 다시 감상한다.</summary>
        public bool Replay(CareerPresentationRequest request)
        {
            if (request == null)
                return false;
            _replaySequence++;
            var replayRequest = new CareerPresentationRequest(
                $"replay:{_replaySequence}:{request.RequestId}",
                request.Type,
                request.Grade,
                request.SeasonYear,
                request.Category,
                request.Title,
                request.PlayerName,
                request.Description,
                request.Stats,
                request.StartWeek,
                request.EndWeek);
            bool enqueued = _queue.Enqueue(replayRequest);
            if (enqueued)
                TryPlayNext();
            return enqueued;
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveCareer)
            {
                Suspend();
                return;
            }

            CareerState career = _manager.CurrentCareer;
            if (!ReferenceEquals(_observedCareer, career))
            {
                ObserveCareer(career);
            }
            else
            {
                QueueNewGrowthResults(career);
            }
            TryQueueSeasonReview();
            TryPlayNext();
        }

        private void ObserveCareer(CareerState career)
        {
            _observedCareer = career;
            PlayerGrowthState growth = career?.MyPlayer?.GrowthState;
            _observedGrowthCount = growth?.GrowthHistory.Count ?? 0;
            _observedOffseasonWeek = Math.Max(1, career?.CurrentOffseason?.CurrentWeek ?? 1);
        }

        private void QueueNewGrowthResults(CareerState career)
        {
            PlayerGrowthState growth = career?.MyPlayer?.GrowthState;
            if (growth == null)
                return;
            int currentCount = growth.GrowthHistory.Count;
            if (currentCount < _observedGrowthCount)
            {
                _observedGrowthCount = currentCount;
                _observedOffseasonWeek = Math.Max(1, career.CurrentOffseason?.CurrentWeek ?? 1);
                return;
            }

            int nextWeek = _observedOffseasonWeek;
            for (int index = _observedGrowthCount; index < currentCount; index++)
            {
                GrowthResultRecord result = growth.GrowthHistory[index];
                bool isRepeat = CountPreviousSelections(growth, index, result) > 0;
                if (CareerPresentationRequestFactory.TryCreateGrowthActivity(
                        result,
                        career.MyPlayer.Name,
                        nextWeek,
                        isRepeat,
                        out CareerPresentationRequest request))
                {
                    _queue.Enqueue(request);
                }
                nextWeek += Math.Max(0, result.WeeksSpent);
            }
            _observedGrowthCount = currentCount;
            _observedOffseasonWeek = Math.Max(1, career.CurrentOffseason?.CurrentWeek ?? nextWeek);
        }

        private static int CountPreviousSelections(
            PlayerGrowthState growth,
            int exclusiveEnd,
            GrowthResultRecord result)
        {
            int count = 0;
            for (int index = 0; index < exclusiveEnd; index++)
            {
                GrowthResultRecord previous = growth.GrowthHistory[index];
                if (previous.SeasonYear == result.SeasonYear &&
                    string.Equals(previous.SourceId, result.SourceId, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private void TryQueueSeasonReview()
        {
            if (_manager == null || !_manager.HasActiveCareer)
                return;
            if (CareerPresentationRequestFactory.TryCreateSeasonReview(
                    _manager.Dashboard,
                    AdvanceSeasonReviewFromPresentation,
                    out CareerPresentationRequest request))
            {
                _queue.Enqueue(request);
            }
        }

        private void AdvanceSeasonReviewFromPresentation()
        {
            if (_manager != null && _manager.HasActiveCareer)
                _manager.AdvanceSeasonReview();
        }

        private void TryPlayNext()
        {
            if (_isTransitioning || _currentRequest != null || !_queue.TryDequeue(out _currentRequest))
                return;

            _currentData = CareerPresentationAssetLibrary.Get(_currentRequest.Type);
            float minimumViewTime = _currentData != null ? _currentData.MinimumViewTime : 1f;
            if (CareerPresentationSettings.Mode == CareerPresentationMode.ResultOnly)
                minimumViewTime = 0.1f;
            _dismissAllowedAt = Time.unscaledTime + minimumViewTime;
            Show();
            RenderRequest(_currentRequest, _currentData);
            PlayMotion(_currentRequest, _currentData);
        }

        private void TryDismiss()
        {
            if (_currentRequest == null || Time.unscaledTime < _dismissAllowedAt)
                return;
            if (_currentData != null && !_currentData.AllowSkip && !IsMotionComplete)
                return;
            BeginExit();
        }

        private void BeginExit()
        {
            if (_currentRequest == null || _isTransitioning)
                return;
            _isTransitioning = true;
            PlayExitMotion(FinishExit);
        }

        private void FinishExit()
        {
            CareerPresentationRequest finished = _currentRequest;
            _currentRequest = null;
            _currentData = null;
            Hide();
            _isTransitioning = false;
            finished?.Completed?.Invoke();
            if (_currentRequest == null)
                TryPlayNext();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

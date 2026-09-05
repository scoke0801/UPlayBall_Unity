using System;
using System.Collections;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SceneFlow
{
    /// <summary>
    /// Loading Scene에서 진행률을 표시하고 준비된 대상 Scene의 활성화를 승인한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingSceneController : MonoBehaviour
    {
        private const float ProgressAnimationSpeed = 2.5f;

        [SerializeField] private Slider _progressBar;
        [SerializeField] private Text _statusLabel;

        private SceneLoadManager _sceneLoadManager;
        private HistoricalWarmupManager _warmup;
        private bool _activationRequested;
        private bool _isPersistent;

        private void Awake()
        {
            if (_progressBar == null)
                _progressBar = CreateRuntimePresentation();
            if (_statusLabel == null)
                _statusLabel = CreateStatusLabel(_progressBar.transform.parent);

            _progressBar.minValue = 0f;
            _progressBar.maxValue = 1f;
            _progressBar.SetValueWithoutNotify(0f);
            CareerUiSkin.ApplySlider(_progressBar);
        }

        private void Start()
        {
            _sceneLoadManager = GameManager.EnsureExists()
                .EnsureManager<SceneLoadManager>("SceneLoadManager");
            _warmup = HistoricalWarmupManager.Instance;

            if (_sceneLoadManager.StartPendingLoad())
                return;

            Debug.LogWarning("[LoadingSceneController] 보류 중인 요청이 없어 Management Scene으로 복구합니다.");
            _sceneLoadManager.LoadScene(
                SceneId.Management,
                SceneTransitionMode.Direct,
                minimumLoadingTime: 0f);
        }

        private void Update()
        {
            if (_sceneLoadManager == null || _activationRequested)
                return;

            // 둘 중 느린 쪽이 곧 "들어갈 준비"이므로 최소값을 표시한다.
            UpdateStatusLabel();
            float readiness = Mathf.Min(_sceneLoadManager.LoadProgress, GetWarmupProgress());
            float nextValue = Mathf.MoveTowards(
                _progressBar.value,
                readiness,
                Time.unscaledDeltaTime * ProgressAnimationSpeed);
            _progressBar.SetValueWithoutNotify(nextValue);

            if (!_sceneLoadManager.IsReadyToActivate || !IsWarmupSettled() || nextValue < 0.999f)
                return;

            PreserveUntilTargetReady();
            _activationRequested = _sceneLoadManager.ActivatePendingScene();
        }

        /// <summary>
        /// 워밍업이 World 준비를 끝냈는지 본다. 성공뿐 아니라 실패·취소도 기다림의 끝이다.
        /// 실패한 경우 각 진입점이 스스로 만드는 경로를 그대로 갖고 있으므로 진입을 막지 않는다.
        /// 워밍업 매니저가 없거나 시작하지 않았다면 애초에 기다릴 대상이 없다.
        /// </summary>
        private bool IsWarmupSettled()
        {
            return _warmup == null || _warmup.IsSettled;
        }

        private float GetWarmupProgress()
        {
            return _warmup == null || _warmup.IsSettled ? 1f : _warmup.Progress;
        }

        /// <summary>
        /// Bake가 맞지 않아 44시즌을 돌리는 동안은 진행률이 거의 멈춘 것처럼 보인다.
        /// 그때 화면이 멈춘 게 아니라는 것을 알려주지 않으면 사용자는 강제 종료한다.
        /// </summary>
        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            string message = _warmup == null || _warmup.IsSettled
                ? "화면을 준비하는 중…"
                : _warmup.StatusMessage;
            if (!string.Equals(_statusLabel.text, message, StringComparison.Ordinal))
                _statusLabel.text = message;
        }

        private void PreserveUntilTargetReady()
        {
            if (_isPersistent)
                return;

            _isPersistent = true;
            DontDestroyOnLoad(gameObject);
            _sceneLoadManager.LoadCompleted += HandleLoadCompleted;
            _sceneLoadManager.LoadFailed += HandleLoadFailed;
        }

        private void HandleLoadCompleted(SceneId sceneId)
        {
            StartCoroutine(ReleaseAfterRenderedFrame());
        }

        private void HandleLoadFailed(SceneId sceneId, string reason)
        {
            Debug.LogError($"[LoadingSceneController] {sceneId} Scene Load 실패: {reason}");
            ReleasePresentation();
        }

        private IEnumerator ReleaseAfterRenderedFrame()
        {
            yield return new WaitForEndOfFrame();
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            Unsubscribe();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_sceneLoadManager == null)
                return;

            _sceneLoadManager.LoadCompleted -= HandleLoadCompleted;
            _sceneLoadManager.LoadFailed -= HandleLoadFailed;
            _isPersistent = false;
        }

        private Slider CreateRuntimePresentation()
        {
            var canvasObject = new GameObject(
                "UI_System_Loading",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform background = CreateImage(
                "Background",
                canvasObject.transform,
                new Color(0.025f, 0.04f, 0.065f, 1f));
            Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform track = CreateImage(
                "ProgressTrack",
                canvasObject.transform,
                new Color(0.15f, 0.19f, 0.25f, 1f));
            Stretch(
                track,
                new Vector2(0.2f, 0.475f),
                new Vector2(0.8f, 0.525f),
                Vector2.zero,
                Vector2.zero);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;

            RectTransform fillArea = new GameObject("FillArea", typeof(RectTransform))
                .GetComponent<RectTransform>();
            fillArea.SetParent(track, false);
            Stretch(fillArea, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            RectTransform fill = CreateImage(
                "Fill",
                fillArea,
                new Color(0.21f, 0.68f, 0.95f, 1f));
            Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slider.fillRect = fill;

            return slider;
        }

        /// <summary>진행률 막대 아래에 단계 문구 한 줄을 둔다.</summary>
        private static Text CreateStatusLabel(Transform parent)
        {
            var labelObject = new GameObject("StatusLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            Stretch(rect, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.46f), Vector2.zero, Vector2.zero);

            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.62f, 0.71f, 0.82f, 1f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = string.Empty;
            return label;
        }

        private static RectTransform CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject.GetComponent<RectTransform>();
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

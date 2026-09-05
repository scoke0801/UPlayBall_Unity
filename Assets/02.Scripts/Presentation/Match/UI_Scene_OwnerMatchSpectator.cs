using System;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    /// <summary>공통 경기 HUD와 구단주 관전 전용 재생 제어를 구성하는 런타임 uGUI 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerMatchSpectator : MonoBehaviour
    {
        private static Font _font;

        private RectTransform _root;
        private MatchHudView _hudView;
        private Text _permissionText;
        private Text _progressText;
        private Text _resultText;
        private Button _pauseButton;
        private Text _pauseLabel;
        private Button[] _speedButtons;
        private Button _advanceButton;
        private Button _revealAllButton;
        private Button _homeButton;
        private OwnerMatchSpectatorSession _session;
        private float _nextAutomaticAdvanceAt;

        public event Action HomeRequested;

        public bool IsPresenting { get; private set; }
        public bool IsComplete => _session?.State.IsComplete == true;

        /// <summary>Shared Shell의 MainWorkspace 안에 관전 화면을 생성한다.</summary>
        public static UI_Scene_OwnerMatchSpectator CreateRuntime(RectTransform workspaceHost)
        {
            if (workspaceHost == null)
                throw new ArgumentNullException(nameof(workspaceHost));

            var rootObject = new GameObject(
                nameof(UI_Scene_OwnerMatchSpectator),
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(workspaceHost, false);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            Stretch(root);
            UI_Scene_OwnerMatchSpectator view = rootObject.AddComponent<UI_Scene_OwnerMatchSpectator>();
            view.Build(root);
            view.SetVisible(false);
            return view;
        }

        /// <summary>실제 OwnerModeManager 경기 한 건을 확정하고 관전 재생을 시작한다.</summary>
        public void PlayNextGame(OwnerModeManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            _session = OwnerMatchSpectatorSession.PlayNextGame(manager, _hudView);
            IsPresenting = true;
            SetVisible(true);
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        public void SetVisible(bool isVisible)
        {
            if (_root != null)
                _root.gameObject.SetActive(isVisible);
        }

        public void EndPresentation()
        {
            IsPresenting = false;
            SetVisible(false);
        }

        private void Update()
        {
            if (!IsPresenting || _session == null)
                return;

            OwnerMatchOverlayState state = _session.State;
            if (state.IsPaused || state.IsComplete || Time.unscaledTime < _nextAutomaticAdvanceAt)
                return;

            _session.TryAdvance();
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void Build(RectTransform root)
        {
            _root = root;
            Image background = root.GetComponent<Image>();
            background.color = CareerUiTheme.Background;
            background.raycastTarget = true;

            _hudView = MatchHudView.CreateRuntime(root);
            RectTransform information = CreateImage(
                "SpectatorInformation",
                root,
                CareerUiTheme.Panel,
                new Vector2(1420f, 176f),
                new Vector2(0f, 238f));
            AddOutline(information, CareerUiTheme.Border);
            _permissionText = CreateText(
                "Permission",
                information,
                string.Empty,
                20,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(1300f, 42f),
                new Vector2(0f, 44f),
                CareerUiTheme.TextPrimary);
            _progressText = CreateText(
                "Progress",
                information,
                string.Empty,
                17,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(900f, 34f),
                new Vector2(0f, -6f),
                CareerUiTheme.TextSecondary);
            _resultText = CreateText(
                "Result",
                information,
                string.Empty,
                26,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Vector2(900f, 42f),
                new Vector2(0f, -52f),
                CareerUiTheme.Number);

            RectTransform controls = CreateImage(
                "SpectatorControls",
                root,
                CareerUiTheme.PanelDark,
                new Vector2(1420f, 250f),
                new Vector2(0f, -12f));
            AddOutline(controls, CareerUiTheme.Border);
            _pauseButton = CreateButton(
                "Pause",
                controls,
                "일시정지",
                new Vector2(190f, 54f),
                new Vector2(-500f, 46f),
                CareerUiTheme.SecondaryAction,
                out _pauseLabel);
            _pauseButton.onClick.AddListener(HandlePauseRequested);

            OwnerMatchPlaybackSpeed[] speeds =
            {
                OwnerMatchPlaybackSpeed.Normal,
                OwnerMatchPlaybackSpeed.Fast,
                OwnerMatchPlaybackSpeed.VeryFast
            };
            _speedButtons = new Button[speeds.Length];
            for (int index = 0; index < speeds.Length; index++)
            {
                OwnerMatchPlaybackSpeed speed = speeds[index];
                Button speedButton = CreateButton(
                    $"Speed{(int)speed}",
                    controls,
                    $"{(int)speed}배속",
                    new Vector2(150f, 54f),
                    new Vector2(-240f + index * 168f, 46f),
                    CareerUiTheme.SecondaryAction,
                    out _);
                speedButton.onClick.AddListener(() => HandleSpeedRequested(speed));
                _speedButtons[index] = speedButton;
            }

            _advanceButton = CreateButton(
                "Advance",
                controls,
                "다음 타석",
                new Vector2(190f, 54f),
                new Vector2(320f, 46f),
                CareerUiTheme.PrimaryAction,
                out _);
            _advanceButton.onClick.AddListener(HandleAdvanceRequested);
            _revealAllButton = CreateButton(
                "RevealAll",
                controls,
                "즉시 결과",
                new Vector2(190f, 54f),
                new Vector2(530f, 46f),
                CareerUiTheme.SpecialAction,
                out _);
            _revealAllButton.onClick.AddListener(HandleRevealAllRequested);

            _homeButton = CreateButton(
                "ReturnHome",
                controls,
                "구단 홈으로",
                new Vector2(260f, 62f),
                new Vector2(0f, -58f),
                CareerUiTheme.SuccessAction,
                out _);
            _homeButton.onClick.AddListener(() => HomeRequested?.Invoke());
            _homeButton.gameObject.SetActive(false);
        }

        private void HandlePauseRequested()
        {
            if (_session?.TryTogglePause() != true)
                return;
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleSpeedRequested(OwnerMatchPlaybackSpeed speed)
        {
            if (_session?.TrySetPlaybackSpeed(speed) != true)
                return;
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleAdvanceRequested()
        {
            if (_session?.TryAdvance() != true)
                return;
            ScheduleNextAutomaticAdvance();
            RefreshControls();
        }

        private void HandleRevealAllRequested()
        {
            if (_session?.TryRevealAll() != true)
                return;
            RefreshControls();
        }

        private void ScheduleNextAutomaticAdvance()
        {
            if (_session == null)
                return;
            _nextAutomaticAdvanceAt = Time.unscaledTime +
                                      OwnerMatchPlaybackTiming.GetAdvanceIntervalSeconds(_session.State.Speed);
        }

        private void RefreshControls()
        {
            if (_session == null)
                return;

            OwnerMatchOverlayState state = _session.State;
            _permissionText.text = state.PermissionMessage;
            _progressText.text = $"중계 진행  {state.VisibleEventCount:N0} / {state.TotalEventCount:N0}";
            _pauseButton.interactable = state.CanTogglePause;
            _pauseLabel.text = state.IsPaused ? "계속 보기" : "일시정지";
            _advanceButton.interactable = state.CanAdvance;
            _revealAllButton.interactable = state.CanAdvance;
            for (int index = 0; index < _speedButtons.Length; index++)
            {
                OwnerMatchPlaybackSpeed speed = index == 0
                    ? OwnerMatchPlaybackSpeed.Normal
                    : index == 1
                        ? OwnerMatchPlaybackSpeed.Fast
                        : OwnerMatchPlaybackSpeed.VeryFast;
                Button button = _speedButtons[index];
                button.interactable = state.CanChangeSpeed;
                button.targetGraphic.color = state.Speed == speed
                    ? CareerUiTheme.SurfaceSelected
                    : CareerUiTheme.SecondaryAction;
            }

            _homeButton.gameObject.SetActive(state.IsComplete);
            if (!state.IsComplete)
            {
                _resultText.text = state.IsPaused
                    ? $"중계 일시정지 · {(int)state.Speed}배속"
                    : $"감독 AI 경기 진행 · {(int)state.Speed}배속";
                return;
            }

            _resultText.text =
                $"경기 종료 · {_session.Result.Match.AwayBoxScore.Runs} : " +
                $"{_session.Result.Match.HomeBoxScore.Runs}";
        }

        private static RectTransform CreateImage(
            string objectName,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = child.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Text text = child.GetComponent<Text>();
            text.font = _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color background,
            out Text labelText)
        {
            RectTransform rect = CreateImage(objectName, parent, background, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = CareerUiTheme.PrimaryBright;
            colors.pressedColor = CareerUiTheme.Primary;
            colors.disabledColor = CareerUiTheme.SurfaceSubtle;
            button.colors = colors;
            labelText = CreateText(
                "Label",
                rect,
                label,
                17,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero,
                CareerUiTheme.TextPrimary);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static void AddOutline(RectTransform rect, Color color)
        {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
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

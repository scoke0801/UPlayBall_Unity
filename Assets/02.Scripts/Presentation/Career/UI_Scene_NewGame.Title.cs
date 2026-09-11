using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Diagnostics;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>타이틀에서 선수 생성·계약·루키 리그 진입까지 한 흐름으로 표시한다.</summary>
    public sealed partial class UI_Scene_NewGame : UISceneBase
    {
        private static readonly Color BackgroundColor = CareerUiTheme.Background;
        private static readonly Color PanelColor = CareerUiTheme.Panel;
        private static readonly Color CardColor = CareerUiTheme.Surface;
        private static readonly Color SelectedColor = CareerUiTheme.SurfaceSelected;
        private static readonly Color AccentColor = CareerUiTheme.PrimaryBright;
        private static readonly Color GoldColor = CareerUiTheme.AccentGold;
        private static readonly Color PrimaryTextColor = CareerUiTheme.TextPrimary;
        private static readonly Color SecondaryTextColor = CareerUiTheme.TextSecondary;
        private static readonly Color MutedTextColor = CareerUiTheme.TextMuted;
        private static readonly Color ErrorColor = CareerUiTheme.Error;
        private static readonly Color LockedCardColor = CareerUiTheme.PanelDark;

        private const float LockedCardAlpha = 0.72f;
#if UNITY_EDITOR
        private bool _showCardGallery;
#endif

        private readonly HashSet<PitchType> _selectedPitches = new();
        private NewGameManager _manager;
        private CareerCreationPresentationData _presentationData;
        private RectTransform _content;
        private RectTransform _panel;
        private RectTransform _body;
        private Text _screenTitle;
        private Text _error;
        private Button _backButton;
        private Button _nextButton;
        private Text _nextLabel;
        private InputField _nameInput;
        private RectTransform _hitterPreview;
        private RectTransform _pitcherPreview;

        private string _nameDraft = string.Empty;
        private PlayerType? _selectedPlayerType;
        private PlayerType? _lastAnimatedPlayerType;
        private Handedness _selectedBattingHand = Handedness.Right;
        private Handedness _selectedThrowingHand = Handedness.Right;
        private PlayerPosition _selectedPosition = PlayerPosition.Unknown;
        private PitcherRole _selectedPitcherRole = PitcherRole.Starter;
        private int[] _attributeDraft = Array.Empty<int>();
        private PlayerType? _attributeDraftType;
        private BatterStyle _selectedBatterStyle = BatterStyle.Balanced;
        private PitchType _primaryPitch = PitchType.Slider;
        private BattingApproach _selectedBattingApproach = BattingApproach.Balanced;
        private PitchingApproach _selectedPitchingApproach = PitchingApproach.Balanced;
        private MatchProgressMode _selectedProgressMode = MatchProgressMode.InterveneOnPlayer;
        private MatchProgressMode _selectedAutomaticProgressMode = MatchProgressMode.InterveneOnPlayer;
        private int _selectedGameSpeed = 2;
        private bool _autoSlowOnPlayerEvent = true;
        private bool _showStartConfirmation;
        private string _titleNotice = string.Empty;
        private bool _showQuitConfirmation;

        public override bool BlocksLowerInput => true;

        /// <summary>프리팹이 없는 환경에서도 동일한 타이틀·생성 화면을 만든다.</summary>
        public static UI_Scene_NewGame CreateRuntime(Transform parent)
        {
            var screenObject = new GameObject(
                nameof(UI_Scene_NewGame), typeof(RectTransform), typeof(CanvasGroup));
            screenObject.transform.SetParent(parent, false);
            UI_Scene_NewGame screen = screenObject.AddComponent<UI_Scene_NewGame>();
            Stretch(screenObject.GetComponent<RectTransform>());
            return screen;
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<NewGameManager>("NewGameManager");
            _presentationData = CareerCreationPresentationData.Load();
            _manager.FlowChanged += Render;
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
            Render();
        }

        protected override void OnShow() => Render();

        /// <summary>타이틀에 노출된 저장 상태와 이어하기 문구를 디스크 상태에 맞게 갱신한다.</summary>
        public void RefreshTitleSaveState()
        {
            if (IsVisible && _manager != null && _manager.IsAtTitle)
                Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.FlowChanged -= Render;
            DOTween.Kill(this);
            base.OnDestroy();
        }

        private void Render()
        {
            if (_content == null || _manager == null)
                return;

            DOTween.Kill(this);
            ClearChildren(_content);
            _panel = null;
            _body = null;
            _error = null;
            _backButton = null;
            _nextButton = null;
            _hitterPreview = null;
            _pitcherPreview = null;

            if (OwnerModeManager.Instance != null && OwnerModeManager.Instance.NewGameFlow != null)
            {
                RenderOwnerNewGame();
                return;
            }

            if (_manager.IsAtTitle)
            {
                RenderTitle();
                return;
            }

            BuildWizardShell();
            switch (_manager.CurrentStep)
            {
                case NewGameStep.Identity: RenderBasicInformation(); break;
                case NewGameStep.Position: RenderPositionAndRole(); break;
                case NewGameStep.AttributeAllocation: RenderAttributes(); break;
                case NewGameStep.PlayerDetails: RenderPlayerDetails(); break;
                case NewGameStep.MatchSettings: RenderMatchSettings(); break;
                case NewGameStep.FinalConfirmation: RenderFinalConfirmation(); break;
                case NewGameStep.PlayerType: RenderLegacyPlayerType(); break;
                case NewGameStep.Handedness: RenderLegacyHandedness(); break;
                case NewGameStep.PlayerCard: RenderLegacyPlayerCard(); break;
                case NewGameStep.ContractOffers: RenderOffers(); break;
                case NewGameStep.ContractComplete: RenderContractComplete(); break;
                case NewGameStep.Completed: OpenCareerHome(); break;
            }
        }

        private void RenderTitle()
        {
            Sprite titleSprite = _presentationData != null ? _presentationData.TitleImage : null;
            RectTransform background = CreateImage(
                "TitleBackground", _content, Color.white, new Vector2(1920f, 1080f), Vector2.zero);
            Image image = background.GetComponent<Image>();
            image.sprite = titleSprite;
            image.preserveAspect = false;
            if (titleSprite == null)
                image.color = BackgroundColor;

            CreateImage("TitleShade", _content, new Color(0.005f, 0.012f, 0.025f, 0.52f),
                new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform right = CreateImage(
                "ModePanel", _content, CareerUiTheme.PanelDark,
                new Vector2(720f, 1080f), new Vector2(600f, 0f));
            TitleUiButtonSkin.ApplyPanel(right.GetComponent<Image>());
            CreateText("Eyebrow", right, "싱글 플레이 야구 커리어", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 28f), new Vector2(0f, 430f), AccentColor);
            CreateText("Heading", right, "커리어를 선택하세요", 34, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 52f), new Vector2(0f, 382f), PrimaryTextColor);

            Button playerCareer = CreateButton(
                "PlayerCareer", right, string.Empty, new Vector2(580f, 220f), new Vector2(0f, 220f),
                CareerUiTheme.PrimaryAction, out _);
            TitleUiButtonSkin.Apply(playerCareer, TitleButtonRole.Mode);
            CreateText("Mode", playerCareer.transform, "선수 모드", 30, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(420f, 45f), new Vector2(55f, 58f), PrimaryTextColor);
            CreateText("Description", playerCareer.transform,
                "한 명의 선수를 만들고 경기·성장·계약을 통해\n여러 시즌의 커리어를 이어갑니다.",
                17, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(420f, 72f), new Vector2(55f, 0f), SecondaryTextColor);
            CreateText("Action", playerCareer.transform, "새 선수 만들기  →", 16, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(420f, 32f), new Vector2(55f, -73f), AccentColor);
            playerCareer.onClick.AddListener(() =>
            {
                ResetLocalDraft();
                UiGameModeSession.Select(UiGameMode.PlayerCareer);
                _manager.StartPlayerCareerCreation();
            });

            OwnerModeManager ownerManager = GameManager.EnsureExists()
                .EnsureManager<OwnerModeManager>("OwnerModeManager");
            string ownerAction = ownerManager.HasActiveRuntime
                ? "계속하기  →"
                : ownerManager.HasAnySave ? "저장 슬롯 선택  →" : "새 구단 시작  →";
            Button ownerCareer = CreateButton(
                "OwnerCareer", right, string.Empty, new Vector2(580f, 190f), new Vector2(0f, -15f),
                CareerUiTheme.SecondaryAction, out _);
            TitleUiButtonSkin.Apply(ownerCareer, TitleButtonRole.Mode);
            CreateText("Badge", ownerCareer.transform, "구단 운영", 12, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(420f, 24f), new Vector2(55f, 58f), GoldColor);
            CreateText("Mode", ownerCareer.transform, "구단주 모드", 27, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(420f, 40f), new Vector2(55f, 18f), PrimaryTextColor);
            CreateText("Description", ownerCareer.transform,
                "실제 구단 Save로 로스터·자원·일정을 운영합니다.",
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(420f, 40f), new Vector2(55f, -28f), SecondaryTextColor);
            CreateText("Action", ownerCareer.transform, ownerAction, 15, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(420f, 28f), new Vector2(55f, -68f), AccentColor);
            ownerCareer.onClick.AddListener(() =>
            {
                if (!ownerManager.HasActiveRuntime && ownerManager.HasAnySave)
                {
                    UI_Popup_CareerSettings.ShowSaveLoadRuntime(UiGameMode.OwnerCareer);
                    return;
                }
                if (!ownerManager.HasActiveRuntime && !ownerManager.HasAnySave)
                {
                    try
                    {
                        _ownerClubNameDraft = string.Empty;
                        _ownerNicknameDraft = "구단주";
                        ownerManager.BeginNewGameFlow();
                        Render();
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException || exception is InvalidOperationException)
                    {
                        _titleNotice = string.IsNullOrWhiteSpace(exception.Message)
                            ? "구단주 새 게임 준비에 실패했습니다."
                            : exception.Message;
                        Render();
                    }
                    return;
                }

                OwnerModeEntryProfiler.Begin($"구단주 모드 · {ownerAction.Replace("  →", string.Empty)}");
                try
                {
                    if (!ownerManager.HasActiveRuntime)
                    {
                        if (ownerManager.HasSave)
                        {
                            ownerManager.Load();
                            OwnerModeEntryProfiler.Mark("세이브 로드");
                        }
                    }
                    else
                    {
                        OwnerModeEntryProfiler.Mark("기존 런타임 재사용");
                    }

                    // Select는 ModeChanged를 동기 통지해 OwnerModeShellCoordinator.Refresh를 그 자리에서 돌린다.
                    // Shell 구성 구간의 세부 Mark는 그 안에서 찍힌다.
                    UiGameModeSession.Select(UiGameMode.OwnerCareer);
                    Hide();
                    OwnerModeEntryProfiler.Mark("타이틀 화면 숨김");
                }
                catch (Exception exception) when (
                    exception is ArgumentException || exception is InvalidOperationException)
                {
                    OwnerModeEntryProfiler.Abort(exception.Message);
                    _titleNotice = string.IsNullOrWhiteSpace(exception.Message)
                        ? "구단주 모드를 시작할 수 없습니다."
                        : exception.Message;
                    Render();
                }
            });
#if UNITY_EDITOR
            Button cardGallery = CreateButton(
                "CardDesignGallery", right, "카드 디자인 보기", new Vector2(280f, 52f),
                new Vector2(0f, -250f), CareerUiTheme.SecondaryAction, out _);
            TitleUiButtonSkin.Apply(cardGallery, TitleButtonRole.Secondary);
            cardGallery.onClick.AddListener(() =>
            {
                _showCardGallery = true;
                Render();
            });
#endif
            Button settings = CreateButton("TitleSettings", right, "설정", new Vector2(150f, 50f),
                new Vector2(-180f, -445f), CareerUiTheme.SecondaryAction, out _);
            TitleUiButtonSkin.Apply(settings, TitleButtonRole.Secondary);
            settings.onClick.AddListener(() =>
            {
                UI_Popup_CareerSettings.ShowSaveLoadRuntime();
            });
            Button credits = CreateButton("Credits", right, "크레딧", new Vector2(150f, 42f),
                new Vector2(0f, -445f), CareerUiTheme.SecondaryAction, out _);
            TitleUiButtonSkin.Apply(credits, TitleButtonRole.Secondary);
            credits.onClick.AddListener(() =>
            {
            _titleNotice = "UPlayBall · 프로야구 선수 커리어";
                Render();
            });
            Button quit = CreateButton("Quit", right, "게임 종료", new Vector2(150f, 42f),
                new Vector2(180f, -445f), CareerUiTheme.Loss, out _);
            TitleUiButtonSkin.Apply(quit, TitleButtonRole.Danger);
            quit.onClick.AddListener(() =>
            {
                _showQuitConfirmation = true;
                Render();
            });

#if UNITY_EDITOR
            if (_showCardGallery)
                RenderCardDesignGallery();
            else
#endif
            if (!string.IsNullOrEmpty(_titleNotice))
                RenderTitleNotice();
            else if (_showQuitConfirmation)
                RenderQuitConfirmation();
        }

        private void RenderTitleNotice()
        {
            RectTransform shade = CreateImage("NoticeShade", _content, new Color(0f, 0f, 0f, 0.70f),
                new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform modal = CreateImage("Notice", shade, PanelColor, new Vector2(680f, 300f), Vector2.zero);
            TitleUiButtonSkin.ApplyPanel(modal.GetComponent<Image>());
            CreateText("Message", modal, _titleNotice, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(580f, 90f), new Vector2(0f, 35f), PrimaryTextColor);
            Button close = CreateButton("Close", modal, "확인", new Vector2(220f, 54f),
                new Vector2(0f, -85f), AccentColor, out _);
            TitleUiButtonSkin.Apply(close, TitleButtonRole.Primary);
            close.onClick.AddListener(() =>
            {
                _titleNotice = string.Empty;
                Render();
            });
        }


        private void RenderQuitConfirmation()
        {
            RectTransform shade = CreateImage("QuitShade", _content, new Color(0f, 0f, 0f, 0.70f),
                new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform modal = CreateImage("QuitConfirmation", shade, PanelColor,
                new Vector2(680f, 330f), Vector2.zero);
            TitleUiButtonSkin.ApplyPanel(modal.GetComponent<Image>());
            CreateText("Message", modal, "게임을 종료하시겠습니까?", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(580f, 70f), new Vector2(0f, 55f), PrimaryTextColor);
            Button cancel = CreateButton("Cancel", modal, "취소", new Vector2(230f, 56f),
                new Vector2(-130f, -82f), CardColor, out _);
            TitleUiButtonSkin.Apply(cancel, TitleButtonRole.Secondary);
            cancel.onClick.AddListener(() =>
            {
                _showQuitConfirmation = false;
                Render();
            });
            Button confirm = CreateButton("Confirm", modal, "게임 종료", new Vector2(230f, 56f),
                new Vector2(130f, -82f), new Color(0.62f, 0.10f, 0.12f, 1f), out _);
            TitleUiButtonSkin.Apply(confirm, TitleButtonRole.Danger);
            confirm.onClick.AddListener(Application.Quit);
        }

        private void BuildWizardShell()
        {
            CreateImage("Background", _content, BackgroundColor, new Vector2(1920f, 1080f), Vector2.zero);
            _panel = CreateImage("NewGamePanel", _content, PanelColor, new Vector2(1740f, 990f), Vector2.zero);
            _screenTitle = CreateText("Title", _panel, "새 선수 커리어", 30, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(520f, 52f), new Vector2(-560f, 435f), PrimaryTextColor);
            RenderStepHeader(_panel);
            _body = CreateRect("Body", _panel, new Vector2(1600f, 760f), new Vector2(0f, 5f));
            _error = CreateText("Error", _panel, _manager.LastError, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(1000f, 34f), new Vector2(0f, -325f), ErrorColor);
            _backButton = CreateButton("Back", _panel, "이전", new Vector2(160f, 52f),
                new Vector2(-700f, -380f), CardColor, out _);
            _backButton.onClick.AddListener(() => _manager.GoBack());
            _nextButton = CreateButton("Next", _panel, "다음", new Vector2(250f, 54f),
                new Vector2(650f, -380f), AccentColor, out _nextLabel);
        }

        private void RenderStepHeader(Transform parent)
        {
            int current = GetGuidedStepIndex(_manager.CurrentStep);
            string[] labels = { "01 기본 정보", "02 포지션", "03 능력치", "04 세부 설정", "05 경기 설정" };
            for (int index = 0; index < labels.Length; index++)
            {
                bool active = current == index + 1;
                bool complete = current > index + 1 || _manager.CurrentStep is NewGameStep.ContractOffers or NewGameStep.ContractComplete;
                Color color = active ? AccentColor : complete ? PrimaryTextColor : MutedTextColor;
                CreateText("Step_" + (index + 1), parent, labels[index], 14,
                    active ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(190f, 32f), new Vector2(-350f + index * 190f, 433f), color);
                CreateImage("StepLine_" + (index + 1), parent,
                    active || complete ? AccentColor : new Color(0.12f, 0.18f, 0.23f, 1f),
                    new Vector2(155f, active ? 3f : 1f), new Vector2(-350f + index * 190f, 409f));
            }
        }

        private void SetTitle(string title, string subtitle)
        {
            _screenTitle.text = title;
            CreateText("Subtitle", _body, subtitle, 16, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(1240f, 40f), new Vector2(0f, 342f), SecondaryTextColor);
        }

        private void SetNext(string label, Action action, bool interactable = true)
        {
            _nextButton.gameObject.SetActive(true);
            _nextButton.interactable = interactable;
            _nextButton.onClick.RemoveAllListeners();
            _nextLabel.text = label;
            _nextButton.onClick.AddListener(() => action());
        }

        private void OpenCareerHome()
        {
            Hide();
            CareerTabNavigation.Show(CareerMainTab.Home);
        }
    }

    /// <summary>타이틀 화면 버튼의 정보 위계와 위험 동작을 구분한다.</summary>
    public enum TitleButtonRole { Mode, Secondary, Primary, Danger }

    /// <summary>타이틀 전용 ImageGen 프레임과 입력 상태를 기존 버튼 의미 색상에서 분리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleUiButtonSkin : MonoBehaviour
    {
        private const string ModeFramePath = "UI/TitleSkin/title_mode_frame_v1";
        private const string SecondaryFramePath = "UI/TitleSkin/title_button_secondary_v1";
        private const string PrimaryFramePath = "UI/TitleSkin/title_button_primary_v1";

        private static readonly Color Ink = new Color32(28, 43, 62, 255);
        private static readonly Color SecondaryInk = new Color32(69, 80, 92, 255);
        private static readonly Color AccentInk = new Color32(38, 108, 75, 255);
        private static readonly Color GoldInk = new Color32(128, 88, 28, 255);
        private static readonly Color Ivory = new Color32(250, 247, 237, 255);
        private static readonly Sprite[] Frames = new Sprite[3];
        private Button _button;
        private Image _source;
        private Image _frame;
        private Text _label;
        private TitleButtonRole _role;
        private bool _lastInteractable;
        private bool _hasRendered;

        /// <summary>빈 기본 라벨을 쓰는 모드 카드까지 타이틀 전용 프레임과 입력 상태를 연결한다.</summary>
        public static void Apply(Button button, TitleButtonRole role)
        {
            if (button == null)
                return;

            Image source = button.GetComponent<Image>();
            if (source == null)
                return;

            var skin = button.GetComponent<TitleUiButtonSkin>()
                ?? button.gameObject.AddComponent<TitleUiButtonSkin>();
            skin._role = role;
            skin.enabled = true;
            if (skin._frame == null)
                skin.Initialize(button, source);
            skin._frame.gameObject.SetActive(true);
            skin.Refresh();
        }

        /// <summary>타이틀의 비대화형 패널에 모드 카드와 같은 시각 언어를 적용한다.</summary>
        public static void ApplyPanel(Image image)
        {
            Sprite frame = LoadFrame(0);
            if (image == null || frame == null)
                return;

            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 10f;
            image.color = Color.white;
            image.raycastTarget = false;
            var visual = image.GetComponent<CareerUiVisualElement>()
                ?? image.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.DataImage);
        }

        private void Initialize(Button button, Image source)
        {
            _button = button;
            _source = source;
            _label = button.transform.Find("Label")?.GetComponent<Text>();

            var frameRect = new GameObject("TitleButtonFrame", typeof(RectTransform))
                .GetComponent<RectTransform>();
            frameRect.SetParent(transform, false);
            frameRect.SetAsFirstSibling();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frameRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _frame = frameRect.gameObject.AddComponent<Image>();
            _frame.raycastTarget = true;
            frameRect.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.DataImage);

            Text[] texts = button.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                if (texts[index].GetComponent<CareerUiPreserveTextColor>() == null)
                    texts[index].gameObject.AddComponent<CareerUiPreserveTextColor>();
            }
        }

        private void LateUpdate()
        {
            if (_button == null || _frame == null)
                return;
            if (!_hasRendered || _button.IsInteractable() != _lastInteractable
                || _button.targetGraphic != _frame)
                Refresh();
        }

        /// <summary>공용 스킨 재적용 뒤에도 역할별 프레임과 라벨 대비를 보존한다.</summary>
        public void Refresh()
        {
            if (_frame == null || !enabled)
                return;

            int frameIndex = _role == TitleButtonRole.Mode ? 0
                : _role == TitleButtonRole.Primary ? 2 : 1;
            Sprite frame = LoadFrame(frameIndex);
            if (frame == null)
                return;

            _source.enabled = false;
            Outline outline = _source.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            _frame.sprite = frame;
            _frame.type = Image.Type.Sliced;
            _frame.pixelsPerUnitMultiplier = 10f;
            _frame.color = _role == TitleButtonRole.Danger
                ? new Color(0.66f, 0.25f, 0.27f, 1f)
                : Color.white;
            _button.targetGraphic = _frame;
            _button.transition = Selectable.Transition.ColorTint;

            bool isDark = _role == TitleButtonRole.Primary || _role == TitleButtonRole.Danger;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = isDark
                ? new Color(1.22f, 1.22f, 1.22f, 1f)
                : new Color(0.86f, 0.93f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.70f, 0.77f, 0.85f, 1f);
            colors.disabledColor = new Color(0.60f, 0.63f, 0.67f, 0.65f);
            colors.fadeDuration = 0.10f;
            _button.colors = colors;

            RefreshDescendantTextColors(isDark);
            if (_label != null && !string.IsNullOrEmpty(_label.text))
                _label.color = _button.IsInteractable() ? isDark ? Ivory : Ink
                    : isDark ? new Color32(207, 213, 220, 255) : new Color32(78, 87, 99, 255);
            _lastInteractable = _button.IsInteractable();
            _hasRendered = true;
        }

        private void RefreshDescendantTextColors(bool isDark)
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                Text text = texts[index];
                Color source = text.color;
                if (isDark)
                {
                    if (IsNear(source, CareerUiTheme.TextPrimary) || IsBrightNeutral(source))
                        text.color = WithAlpha(Ivory, source.a);
                }
                else if (IsNear(source, CareerUiTheme.TextSecondary)
                    || IsNear(source, CareerUiTheme.TextMuted))
                {
                    text.color = WithAlpha(SecondaryInk, source.a);
                }
                else if (IsNear(source, CareerUiTheme.PrimaryBright))
                {
                    text.color = WithAlpha(AccentInk, source.a);
                }
                else if (IsNear(source, CareerUiTheme.AccentGold)
                    || IsNear(source, CareerUiTheme.Number))
                {
                    text.color = WithAlpha(GoldInk, source.a);
                }
                else if (IsNear(source, CareerUiTheme.TextPrimary) || IsBrightNeutral(source))
                {
                    text.color = WithAlpha(Ink, source.a);
                }

                if (text.GetComponent<CareerUiPreserveTextColor>() == null)
                    text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            }
        }

        private static bool IsNear(Color first, Color second)
        {
            const float tolerance = 0.035f;
            return Mathf.Abs(first.r - second.r) <= tolerance
                && Mathf.Abs(first.g - second.g) <= tolerance
                && Mathf.Abs(first.b - second.b) <= tolerance;
        }

        private static bool IsBrightNeutral(Color color)
        {
            float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return maximum >= 0.78f && maximum - minimum <= 0.14f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Sprite LoadFrame(int index)
        {
            if (Frames[index] != null)
                return Frames[index];

            string path = index == 0 ? ModeFramePath
                : index == 1 ? SecondaryFramePath : PrimaryFramePath;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
                return null;

            Frames[index] = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(58f, 58f, 58f, 58f));
            Frames[index].name = index == 0 ? "TitleFrame_mode"
                : index == 1 ? "TitleButton_secondary" : "TitleButton_primary";
            return Frames[index];
        }
    }
}

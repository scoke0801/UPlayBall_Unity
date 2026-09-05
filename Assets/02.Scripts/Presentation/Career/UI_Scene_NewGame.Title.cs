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
    /// <summary>타이틀에서 선수 생성·계약·Rookie League 진입까지 한 흐름으로 표시한다.</summary>
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
        private static readonly Color CardPreviewTeamColor = new(0.07f, 0.23f, 0.35f, 1f);
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
            CreateText("Eyebrow", right, "싱글 플레이 야구 커리어", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 28f), new Vector2(0f, 430f), AccentColor);
            CreateText("Heading", right, "커리어를 선택하세요", 34, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 52f), new Vector2(0f, 382f), PrimaryTextColor);

            Button playerCareer = CreateButton(
                "PlayerCareer", right, string.Empty, new Vector2(580f, 220f), new Vector2(0f, 220f),
                CareerUiTheme.PrimaryAction, out _);
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
                : ownerManager.HasSave ? "저장 불러오기  →" : "새 구단 시작  →";
            Button ownerCareer = CreateButton(
                "OwnerCareer", right, string.Empty, new Vector2(580f, 190f), new Vector2(0f, -15f),
                CareerUiTheme.SecondaryAction, out _);
            // ApplyFramedCardSkin(FramedCard 역할)은 장식용 배경판을 가정해 raycastTarget을 끈다.
            // Show()/Initialize()마다 CareerUiSkin.Apply가 재적용되며 다시 꺼버리므로,
            // 실제 클릭을 받아야 하는 이 Button에는 적용하지 않고 CareerUiSkin.ApplyButton의
            // 기본 버튼 스타일링(playerCareer 버튼과 동일한 경로)만 쓴다.
            CreateText("Badge", ownerCareer.transform, "CLUB MANAGEMENT", 12, FontStyle.Bold,
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
                        else if (!ownerManager.StartNewGame())
                        {
                            throw new InvalidOperationException(ownerManager.LastError);
                        }
                        else
                        {
                            OwnerModeEntryProfiler.Mark("신규 구단 생성");
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
            CareerUiSkin.ApplyButton(cardGallery);
            cardGallery.onClick.AddListener(() =>
            {
                _showCardGallery = true;
                Render();
            });
#endif
            Button settings = CreateButton("TitleSettings", right, "설정", new Vector2(150f, 50f),
                new Vector2(-180f, -445f), CareerUiTheme.SecondaryAction, out _);
            CareerUiSkin.ApplyButton(settings);
            settings.onClick.AddListener(() =>
            {
                _titleNotice = "화면·사운드·조작 설정은 후속 구현에서 제공됩니다.";
                Render();
            });
            Button credits = CreateButton("Credits", right, "크레딧", new Vector2(150f, 42f),
                new Vector2(0f, -445f), CareerUiTheme.SecondaryAction, out _);
            credits.onClick.AddListener(() =>
            {
            _titleNotice = "UPlayBall · 프로야구 선수 커리어";
                Render();
            });
            Button quit = CreateButton("Quit", right, "게임 종료", new Vector2(150f, 42f),
                new Vector2(180f, -445f), CareerUiTheme.Loss, out _);
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
            CreateText("Message", modal, _titleNotice, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(580f, 90f), new Vector2(0f, 35f), PrimaryTextColor);
            Button close = CreateButton("Close", modal, "확인", new Vector2(220f, 54f),
                new Vector2(0f, -85f), AccentColor, out _);
            close.onClick.AddListener(() =>
            {
                _titleNotice = string.Empty;
                Render();
            });
        }

#if UNITY_EDITOR
        /// <summary>타이틀에서 일반·특수 선수 카드의 실제 런타임 합성을 비교한다.</summary>
        private void RenderCardDesignGallery()
        {
            RectTransform shade = CreateImage(
                "CardDesignGalleryShade", _content, new Color(0f, 0f, 0f, 0.84f),
                new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform gallery = CreateImage(
                "CardDesignGalleryPopup", shade, new Color(0.012f, 0.032f, 0.052f, 0.99f),
                new Vector2(1700f, 980f), Vector2.zero);
            ApplyFramedCardSkin(gallery);

            CreateText("Title", gallery, "선수 카드 디자인 테스트", 32, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(760f, 52f), new Vector2(-390f, 425f),
                PrimaryTextColor);
            CreateText("Guide", gallery,
                "동일한 네이비 Team Color 기준 · 카드를 클릭하면 앞/뒤가 전환됩니다.",
                16, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(760f, 40f), new Vector2(390f, 425f), SecondaryTextColor);

            CreateCardPreview(gallery, "Normal", "일반", PlayerCardSpecialType.None, -600f);
            CreateCardPreview(gallery, "AllStar", "올스타", PlayerCardSpecialType.AllStar, -200f);
            CreateCardPreview(gallery, "MVP", "MVP", PlayerCardSpecialType.Mvp, 200f);
            CreateCardPreview(
                gallery, "GoldenGlove", "골든글러브", PlayerCardSpecialType.GoldenGlove, 600f);

            Button close = CreateButton(
                "Close", gallery, "닫기", new Vector2(220f, 52f), new Vector2(0f, -435f),
                new Color(0.025f, 0.16f, 0.25f, 1f), out _);
            CareerUiSkin.ApplyButton(close);
            close.onClick.AddListener(() =>
            {
                _showCardGallery = false;
                Render();
            });
        }

        private void CreateCardPreview(
            Transform parent,
            string objectName,
            string label,
            PlayerCardSpecialType specialType,
            float positionX)
        {
            RectTransform slot = CreateRect(
                "CardPreview_" + objectName, parent, new Vector2(360f, 780f),
                new Vector2(positionX, -8f));
            CreateText("Label", slot, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 40f), new Vector2(0f, 350f), PrimaryTextColor);

            UIPlayerCard card = UIPlayerCard.CreateRuntime(
                slot, new Vector2(320f, 480f), new Vector2(0f, 45f));
            card.BindArtPreview(CardPreviewTeamColor, PlayerPosition.Shortstop);
            card.SetSpecialType(specialType);

            string description = specialType switch
            {
                PlayerCardSpecialType.AllStar => "Silver · Starburst",
                PlayerCardSpecialType.Mvp => "Champagne · Spotlight",
                PlayerCardSpecialType.GoldenGlove => "Leather · Defense",
                _ => "Neutral · Team Color"
            };
            CreateText("Description", slot, description, 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(330f, 34f), new Vector2(0f, -225f),
                SecondaryTextColor);
        }
#endif

        private void RenderQuitConfirmation()
        {
            RectTransform shade = CreateImage("QuitShade", _content, new Color(0f, 0f, 0f, 0.70f),
                new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform modal = CreateImage("QuitConfirmation", shade, PanelColor,
                new Vector2(680f, 330f), Vector2.zero);
            CreateText("Message", modal, "게임을 종료하시겠습니까?", 25, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(580f, 70f), new Vector2(0f, 55f), PrimaryTextColor);
            Button cancel = CreateButton("Cancel", modal, "취소", new Vector2(230f, 56f),
                new Vector2(-130f, -82f), CardColor, out _);
            cancel.onClick.AddListener(() =>
            {
                _showQuitConfirmation = false;
                Render();
            });
            Button confirm = CreateButton("Confirm", modal, "게임 종료", new Vector2(230f, 56f),
                new Vector2(130f, -82f), new Color(0.62f, 0.10f, 0.12f, 1f), out _);
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
}

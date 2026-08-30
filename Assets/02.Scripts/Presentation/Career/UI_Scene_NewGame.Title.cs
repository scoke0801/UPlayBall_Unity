using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>타이틀에서 선수 생성·계약·Rookie League 진입까지 한 흐름으로 표시한다.</summary>
    public sealed partial class UI_Scene_NewGame : UISceneBase
    {
        private static readonly Color BackgroundColor = new(0.008f, 0.018f, 0.035f, 1f);
        private static readonly Color PanelColor = new(0.02f, 0.045f, 0.075f, 0.985f);
        private static readonly Color CardColor = new(0.035f, 0.075f, 0.115f, 1f);
        private static readonly Color SelectedColor = new(0.035f, 0.30f, 0.48f, 1f);
        private static readonly Color AccentColor = new(0.08f, 0.66f, 1f, 1f);
        private static readonly Color GoldColor = new(0.96f, 0.71f, 0.24f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.60f, 0.70f, 0.80f, 1f);
        private static readonly Color MutedTextColor = new(0.34f, 0.42f, 0.50f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.40f, 0.40f, 1f);

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
                "ModePanel", _content, new Color(0.008f, 0.022f, 0.04f, 0.91f),
                new Vector2(720f, 1080f), new Vector2(600f, 0f));
            CreateText("Eyebrow", right, "SINGLE PLAYER BASEBALL CAREER", 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 28f), new Vector2(0f, 430f), AccentColor);
            CreateText("Heading", right, "커리어를 선택하세요", 34, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(580f, 52f), new Vector2(0f, 382f), PrimaryTextColor);

            Button playerCareer = CreateButton(
                "PlayerCareer", right, string.Empty, new Vector2(580f, 220f), new Vector2(0f, 220f),
                new Color(0.025f, 0.20f, 0.34f, 0.98f), out _);
            CreateText("Mode", playerCareer.transform, "선수 커리어", 30, FontStyle.Bold,
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
                _manager.StartPlayerCareerCreation();
            });

            RectTransform managerCard = CreateImage(
                "ManagerCareer", right, new Color(0.04f, 0.05f, 0.065f, 0.88f),
                new Vector2(580f, 190f), new Vector2(0f, -15f));
            CreateText("Lock", managerCard, "LOCK", 11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(90f, 24f), new Vector2(-200f, 60f), MutedTextColor);
            CreateText("Badge", managerCard, "준비 중", 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(92f, 30f), new Vector2(215f, 61f), GoldColor);
            CreateText("Mode", managerCard, "감독 커리어", 27, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(470f, 42f), new Vector2(0f, 30f), SecondaryTextColor);
            CreateText("Description", managerCard,
                "구단 운영과 라인업·영입을 담당하는 모드입니다.\n추후 업데이트에서 제공됩니다.",
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(490f, 65f), new Vector2(0f, -25f), MutedTextColor);
            Button settings = CreateButton("TitleSettings", right, "설정", new Vector2(150f, 42f),
                new Vector2(-180f, -445f), new Color(0.018f, 0.045f, 0.07f, 0.94f), out _);
            settings.onClick.AddListener(() =>
            {
                _titleNotice = "화면·사운드·조작 설정은 후속 구현에서 제공됩니다.";
                Render();
            });
            Button credits = CreateButton("Credits", right, "크레딧", new Vector2(150f, 42f),
                new Vector2(0f, -445f), new Color(0.018f, 0.045f, 0.07f, 0.94f), out _);
            credits.onClick.AddListener(() =>
            {
                _titleNotice = "UPlayBall · Baseball Career Simulation";
                Render();
            });
            Button quit = CreateButton("Quit", right, "게임 종료", new Vector2(150f, 42f),
                new Vector2(180f, -445f), new Color(0.10f, 0.04f, 0.05f, 0.94f), out _);
            quit.onClick.AddListener(() =>
            {
                _showQuitConfirmation = true;
                Render();
            });

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

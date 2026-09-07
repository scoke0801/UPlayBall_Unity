using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Game.SceneFlow;
using Baseball.Presentation.Match;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>커리어 경기·저장·불러오기 설정과 세션 종료를 한곳에서 처리한다.</summary>
    public sealed partial class UI_Popup_CareerSettings : UIPopupBase
    {
        private static readonly int[] GameSpeeds = { 1, 2, 3, 5 };
        private static readonly Color BackdropColor = CareerUiTheme.InputBlocker;
        private static readonly Color PanelColor = CareerUiTheme.ReferencePanel;
        private static readonly Color CardColor = CareerUiTheme.ReferenceButton;
        private static readonly Color SelectedColor = CareerUiTheme.ReferenceAccent;
        private static readonly Color AccentColor = CareerUiTheme.ReferenceAccentLight;
        private static readonly Color PrimaryTextColor = CareerUiTheme.ReferenceText;
        private static readonly Color SecondaryTextColor = CareerUiTheme.ReferenceTextSecondary;
        private static readonly Color MutedTextColor = CareerUiTheme.ReferenceTextSecondary;
        private static readonly Color DangerColor = CareerUiTheme.Loss;

        private CareerManager _careerManager;
        private NewGameManager _newGameManager;
        private RectTransform _content;
        private int _selectedTab;
        private bool _showTitleConfirmation;
        private bool _showInstantResultConfirmation;
        private MatchProgressMode _pendingProgressMode;

        /// <summary>설정 Popup이 경기 입력과 자동 중계를 차단하고 있는지 나타낸다.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>현재 UI Root에 설정 Popup을 생성하거나 기존 인스턴스를 표시한다.</summary>
        public static UI_Popup_CareerSettings ShowRuntime()
        {
            UI_Popup_CareerSettings popup = Object.FindFirstObjectByType<UI_Popup_CareerSettings>(
                FindObjectsInactive.Include);
            if (popup == null)
            {
                UIManager uiManager = GameManager.EnsureExists().EnsureManager<UIManager>("UIManager");
                var popupObject = new GameObject(
                    nameof(UI_Popup_CareerSettings), typeof(RectTransform), typeof(CanvasGroup));
                popupObject.transform.SetParent(uiManager.Root.GetLayerRoot(UILayer.Popup), false);
                popup = popupObject.AddComponent<UI_Popup_CareerSettings>();
                Stretch(popupObject.GetComponent<RectTransform>());
            }

            popup.Show();
            return popup;
        }

        /// <summary>타이틀에서도 같은 설정 Popup의 저장·불러오기 탭을 바로 연다.</summary>
        public static UI_Popup_CareerSettings ShowSaveLoadRuntime()
        {
            UI_Popup_CareerSettings popup = ShowRuntime();
            popup._selectedTab = 1;
            popup.Render();
            return popup;
        }

        protected override void OnInitialize()
        {
            _careerManager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _newGameManager = GameManager.EnsureExists().EnsureManager<NewGameManager>("NewGameManager");
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            _content = CreateRect("Content", root, new Vector2(1920f, 1080f), Vector2.zero);
            Stretch(_content);
        }

        protected override void OnShow()
        {
            IsOpen = true;
            _selectedTab = 0;
            _showTitleConfirmation = false;
            _showInstantResultConfirmation = false;
            _persistenceConfirmation = PersistenceConfirmationAction.None;
            _persistenceMessage = string.Empty;
            _showOwnerPersistenceAtTitle = false;
            _hasChosenTitlePersistenceMode = false;
            Render();
        }

        /// <summary>설정 내부 확인 단계가 열려 있으면 Popup 전체를 닫기 전에 해당 작업만 취소한다.</summary>
        public override bool TryHandleCancel()
        {
            if (_showTitleConfirmation)
            {
                _showTitleConfirmation = false;
                Render();
                return true;
            }
            if (_showInstantResultConfirmation)
            {
                _showInstantResultConfirmation = false;
                Render();
                return true;
            }
            if (_persistenceConfirmation != PersistenceConfirmationAction.None)
            {
                _persistenceConfirmation = PersistenceConfirmationAction.None;
                Render();
                return true;
            }
            return false;
        }

        protected override void OnHide()
        {
            IsOpen = false;
        }

        protected override void OnDestroy()
        {
            IsOpen = false;
            base.OnDestroy();
        }

        private void Render()
        {
            ClearChildren(_content);
            RectTransform backdrop = CreateImage(
                "Backdrop", _content, BackdropColor, new Vector2(1920f, 1080f), Vector2.zero);
            Stretch(backdrop);
            backdrop.GetComponent<Image>().raycastTarget = true;
            RectTransform panel = CreateImage(
                "SettingsPanel", _content, PanelColor, new Vector2(1260f, 900f), Vector2.zero);
            TitleUiButtonSkin.ApplyPanel(panel.GetComponent<Image>());
            CreateText("Title", panel, "설정", 36, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(380f, 56f), new Vector2(-310f, 352f), PrimaryTextColor);
            Button close = CreateButton("Close", panel, "닫기  ESC", new Vector2(180f, 52f),
                new Vector2(500f, 370f), CardColor, out _);
            close.onClick.AddListener(Close);

            string[] tabs = { "경기", "저장·불러오기", "화면", "사운드", "조작", "게임 종료" };
            for (int index = 0; index < tabs.Length; index++)
            {
                int selected = index;
                Button tab = CreateButton(
                    "Tab_" + index,
                    panel,
                    tabs[index],
                    new Vector2(210f, 58f),
                    new Vector2(-500f, 300f - index * 72f),
                    _selectedTab == index ? SelectedColor : CardColor,
                    out _);
                tab.onClick.AddListener(() =>
                {
                    _selectedTab = selected;
                    Render();
                });
            }

            RectTransform body = CreateImage(
                "Body", panel, CareerUiTheme.ReferenceCanvas,
                new Vector2(930f, 720f), new Vector2(120f, -30f));
            if (_selectedTab == 0)
                RenderGameSettings(body);
            else if (_selectedTab == 1)
                RenderPersistenceSettings(body);
            else if (_selectedTab == 2)
                RenderDisplaySettings(body);
            else if (_selectedTab == 5)
                RenderExitSettings(body);
            else
                RenderPlaceholder(body, tabs[_selectedTab]);

            if (_persistenceConfirmation != PersistenceConfirmationAction.None)
                RenderPersistenceConfirmation(panel);
            else if (_showTitleConfirmation)
                RenderTitleConfirmation(panel);
            else if (_showInstantResultConfirmation)
                RenderInstantResultConfirmation(panel);
        }

        private void RenderGameSettings(RectTransform body)
        {
            if (UiGameModeSession.IsSelected(UiGameMode.OwnerCareer))
            {
                RenderOwnerGameSettings(body);
                return;
            }

            if (_careerManager.CurrentCareer == null)
            {
                CreateText("Unavailable", body,
                    "진행 중인 선수 커리어가 없어 경기 설정을 바꿀 수 없습니다.", 21,
                    FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(700f, 60f), Vector2.zero, MutedTextColor);
                return;
            }
            CareerGameSettings settings = _careerManager.CurrentCareer.GameSettings;
            bool isPitcher = _careerManager.CurrentCareer.MyPlayer.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            CreateHeading(body, "경기 진행 방식", 300f);
            MatchProgressMode[] modes =
            {
                MatchProgressMode.FullGameWatch,
                MatchProgressMode.InterveneOnPlayer,
                MatchProgressMode.PlayerFocusAutomatic,
                MatchProgressMode.InstantResult,
                MatchProgressMode.MiniGame
            };
            for (int index = 0; index < modes.Length; index++)
            {
                MatchProgressMode mode = modes[index];
                Button button = CreateButton(
                    "Mode_" + mode,
                    body,
                    GetProgressModeLabel(mode),
                    new Vector2(160f, 50f),
                    new Vector2(-330f + index * 165f, 245f),
                    settings.MatchProgressMode == mode ? SelectedColor : CardColor,
                    out _);
                button.onClick.AddListener(() => SelectProgressMode(mode));
            }

            CreateHeading(body, "경기 속도", 170f);
            for (int index = 0; index < GameSpeeds.Length; index++)
            {
                int speed = GameSpeeds[index];
                Button button = CreateButton(
                    "Speed_" + speed,
                    body,
                    speed + "×",
                    new Vector2(96f, 48f),
                    new Vector2(-330f + index * 108f, 120f),
                    settings.GameSpeed == speed ? SelectedColor : CardColor,
                    out _);
                button.interactable = settings.MatchProgressMode != MatchProgressMode.InstantResult;
                button.onClick.AddListener(() => ApplySettings(gameSpeed: speed));
            }
            Button autoSlow = CreateButton(
                "AutoSlow", body,
                settings.AutoSlowOnPlayerEvent
                    ? "ON  내 선수 장면에서는 1×"
                    : "OFF  내 선수 장면 자동 감속",
                new Vector2(330f, 48f), new Vector2(230f, 120f),
                settings.AutoSlowOnPlayerEvent ? SelectedColor : CardColor, out _);
            autoSlow.interactable = settings.MatchProgressMode != MatchProgressMode.InstantResult;
            autoSlow.onClick.AddListener(() => ApplySettings(autoSlow: !settings.AutoSlowOnPlayerEvent));

            CreateHeading(body, isPitcher ? "투구 방침" : "타격 방침", 40f);
            if (isPitcher)
                RenderPitchingApproaches(body, settings);
            else
                RenderBattingApproaches(body, settings);

            if (settings.MatchProgressMode == MatchProgressMode.MiniGame)
                RenderMiniGameOptions(body, settings);

            CreateText("ApplyGuide", body,
                _careerManager.HasActiveMatch
                    ? "배속은 즉시, 방침은 다음 플레이 경계부터 적용됩니다. 진행 방식은 다음 경기부터 적용됩니다."
                    : "선택한 설정은 다음 경기부터 사용됩니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 32f), new Vector2(0f, -300f), SecondaryTextColor);
        }

        private void RenderOwnerGameSettings(RectTransform body)
        {
            OwnerMatchPresentationOptions settings = OwnerMatchPresentationSettings.Load();
            CreateHeading(body, "경기 진행 방식", 300f);
            OwnerMatchViewingMode[] modes =
            {
                OwnerMatchViewingMode.EveryMoment,
                OwnerMatchViewingMode.KeyMoments,
                OwnerMatchViewingMode.ResultOnly
            };
            string[] labels = { "전체 자동 진행", "핵심 장면 자동 진행", "결과만 보기" };
            for (int index = 0; index < modes.Length; index++)
            {
                OwnerMatchViewingMode mode = modes[index];
                Button button = CreateButton(
                    "OwnerMode_" + mode,
                    body,
                    labels[index],
                    new Vector2(220f, 54f),
                    new Vector2(-245f + index * 245f, 235f),
                    settings.ViewingMode == mode ? SelectedColor : CardColor,
                    out _);
                button.onClick.AddListener(() =>
                {
                    OwnerMatchPresentationSettings.SetViewingMode(mode);
                    Render();
                });
            }

            CreateHeading(body, "경기 속도", 120f);
            OwnerMatchPlaybackSpeed[] speeds =
            {
                OwnerMatchPlaybackSpeed.Normal,
                OwnerMatchPlaybackSpeed.Fast,
                OwnerMatchPlaybackSpeed.VeryFast
            };
            for (int index = 0; index < speeds.Length; index++)
            {
                OwnerMatchPlaybackSpeed speed = speeds[index];
                Button button = CreateButton(
                    "OwnerSpeed_" + (int)speed,
                    body,
                    (int)speed + "×",
                    new Vector2(130f, 52f),
                    new Vector2(-170f + index * 150f, 60f),
                    settings.PlaybackSpeed == speed ? SelectedColor : CardColor,
                    out _);
                button.interactable = settings.ViewingMode != OwnerMatchViewingMode.ResultOnly;
                button.onClick.AddListener(() =>
                {
                    OwnerMatchPresentationSettings.SetPlaybackSpeed(speed);
                    Render();
                });
            }

            string guide = settings.ViewingMode == OwnerMatchViewingMode.ResultOnly
                ? "결과만 보기는 경기 중계와 사운드를 재생하지 않고 최종 결과와 기록으로 바로 이동합니다."
                : "선택한 관전 방식과 배속은 다음 구단주 경기부터 적용됩니다. 경기 중에도 변경할 수 있습니다.";
            CreateText("OwnerMatchGuide", body, guide, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 72f), new Vector2(0f, -85f), SecondaryTextColor);
        }

        private void RenderBattingApproaches(RectTransform body, CareerGameSettings settings)
        {
            BattingApproach[] approaches =
            {
                BattingApproach.Balanced, BattingApproach.Power, BattingApproach.Contact,
                BattingApproach.Patient, BattingApproach.Aggressive
            };
            string[] labels = { "균형 타격", "강하게 타격", "정확하게 타격", "신중한 타격", "적극적인 타격" };
            for (int index = 0; index < approaches.Length; index++)
            {
                BattingApproach approach = approaches[index];
                Button button = CreateButton(
                    "Batting_" + approach, body, labels[index], new Vector2(150f, 52f),
                    new Vector2(-320f + index * 160f, -18f),
                    settings.BattingApproach == approach ? SelectedColor : CardColor, out _);
                button.onClick.AddListener(() => ApplySettings(battingApproach: approach));
            }
        }

        private void RenderPitchingApproaches(RectTransform body, CareerGameSettings settings)
        {
            PitchingApproach[] approaches =
            {
                PitchingApproach.Balanced, PitchingApproach.FullPower, PitchingApproach.ControlFirst,
                PitchingApproach.InduceChase, PitchingApproach.QuickAttack
            };
            string[] labels = { "균형 투구", "전력 투구", "제구 우선", "유인구 승부", "빠른 승부" };
            for (int index = 0; index < approaches.Length; index++)
            {
                PitchingApproach approach = approaches[index];
                Button button = CreateButton(
                    "Pitching_" + approach, body, labels[index], new Vector2(150f, 52f),
                    new Vector2(-320f + index * 160f, -18f),
                    settings.PitchingApproach == approach ? SelectedColor : CardColor, out _);
                button.onClick.AddListener(() => ApplySettings(pitchingApproach: approach));
            }
        }

        private void RenderMiniGameOptions(RectTransform body, CareerGameSettings settings)
        {
            CreateText("MiniGameScopeLabel", body, "직접 플레이 범위", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(180f, 28f), new Vector2(-310f, -104f), AccentColor);
            MiniGameScope[] scopes =
            {
                MiniGameScope.RecommendedByRole,
                MiniGameScope.AllInvolvement,
                MiniGameScope.KeyMoments,
                MiniGameScope.ManualIntervention
            };
            string[] labels = { "역할별 권장", "모든 관여", "중요 상황", "타석 선택" };
            for (int index = 0; index < scopes.Length; index++)
            {
                MiniGameScope scope = scopes[index];
                Button button = CreateButton(
                    "MiniGameScope_" + scope,
                    body,
                    labels[index],
                    new Vector2(126f, 44f),
                    new Vector2(-195f + index * 132f, -105f),
                    settings.MiniGameScope == scope ? SelectedColor : CardColor,
                    out _);
                button.onClick.AddListener(() => ApplyMiniGameSettings(scope, settings.MiniGameDifficulty));
            }

            CreateText("MiniGameDifficultyLabel", body, "조작 보조", 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(180f, 28f), new Vector2(-310f, -164f), AccentColor);
            MiniGameDifficulty[] difficulties =
            {
                MiniGameDifficulty.Beginner,
                MiniGameDifficulty.Standard,
                MiniGameDifficulty.Professional
            };
            string[] difficultyLabels = { "입문", "표준", "프로" };
            for (int index = 0; index < difficulties.Length; index++)
            {
                MiniGameDifficulty difficulty = difficulties[index];
                Button button = CreateButton(
                    "MiniGameDifficulty_" + difficulty,
                    body,
                    difficultyLabels[index],
                    new Vector2(110f, 42f),
                    new Vector2(-120f + index * 120f, -166f),
                    settings.MiniGameDifficulty == difficulty ? SelectedColor : CardColor,
                    out _);
                button.onClick.AddListener(() => ApplyMiniGameSettings(settings.MiniGameScope, difficulty));
            }
        }

        private void ApplyMiniGameSettings(MiniGameScope scope, MiniGameDifficulty difficulty)
        {
            _careerManager.UpdateMiniGameSettings(scope, difficulty);
            Render();
        }

        private void RenderExitSettings(RectTransform body)
        {
            if (!UiGameModeSession.CurrentMode.HasValue)
            {
                Button quit = CreateButton("QuitGame", body, "게임 종료",
                    new Vector2(360f, 62f), Vector2.zero, DangerColor, out _);
                quit.onClick.AddListener(Application.Quit);
                return;
            }

            if (UiGameModeSession.IsSelected(UiGameMode.OwnerCareer))
            {
                CreateText("Title", body, "구단주 모드 종료", 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(600f, 50f), new Vector2(0f, 190f), PrimaryTextColor);
                CreateText("Guide", body,
                    "타이틀로 이동해도 현재 구단주 진행은 유지됩니다.\n앱을 종료하기 전에는 저장·불러오기 탭에서 현재 진행을 저장해 주세요.",
                    18, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(720f, 70f), new Vector2(0f, 85f), SecondaryTextColor);
                Button ownerTitle = CreateButton("ReturnToTitle", body, "타이틀 화면으로",
                    new Vector2(360f, 62f), new Vector2(0f, -20f), DangerColor, out _);
                ownerTitle.onClick.AddListener(OpenTitleConfirmation);
                return;
            }

            CreateText("Title", body, "선수 커리어 마무리", 28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(600f, 50f), new Vector2(0f, 235f), PrimaryTextColor);
            CreateText("RetirementGuide", body,
                "계약이 만료되면 계약 화면의 오퍼 비교에서 바로 은퇴를 고를 수 있습니다.\n여기서는 시즌 도중 마지막 시즌을 미리 선언할 수 있습니다.",
                18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(720f, 80f), new Vector2(0f, 155f), SecondaryTextColor);
            Button retirement = CreateButton("Retirement", body, "은퇴 계획 열기",
                new Vector2(360f, 62f), new Vector2(0f, 70f), SelectedColor, out _);
            retirement.onClick.AddListener(() => UI_Popup_RetirementDecision.ShowRuntime());
            CreateText("Guide", body,
                "저장하지 않은 진행은 타이틀 화면으로 돌아갈 때 사라집니다.\n중요한 경기나 시즌 전환 뒤에는 저장·불러오기 탭에서 저장해 주세요.",
                18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(720f, 70f), new Vector2(0f, -35f), SecondaryTextColor);
            Button title = CreateButton("ReturnToTitle", body, "타이틀 화면으로",
                new Vector2(360f, 62f), new Vector2(0f, -125f), DangerColor, out _);
            title.onClick.AddListener(OpenTitleConfirmation);
        }

        private void OpenTitleConfirmation()
        {
            _showTitleConfirmation = true;
            Render();
        }

        private void RenderPlaceholder(RectTransform body, string tab)
        {
            CreateText("Placeholder", body, tab + " 설정은 후속 구현에서 제공됩니다.", 21,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(700f, 60f), Vector2.zero, MutedTextColor);
        }

        private void RenderTitleConfirmation(RectTransform panel)
        {
            RectTransform modal = CreateModal(panel, "TitleConfirmation");
            bool isOwnerMode = UiGameModeSession.IsSelected(UiGameMode.OwnerCareer);
            bool isInMatch = !isOwnerMode && _careerManager.HasActiveMatch;
            string title = isOwnerMode
                ? "구단주 모드에서 나갈까요?"
                : isInMatch ? "경기와 커리어를 종료할까요?" : "커리어를 종료할까요?";
            CreateText("Title", modal, title,
                27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 48f), new Vector2(0f, 102f), PrimaryTextColor);
            CreateText("Message", modal,
                isOwnerMode
                    ? "현재 구단주 모드 진행을 유지한 채 타이틀로 이동합니다.\n앱을 종료하기 전에는 저장·불러오기 탭에서 저장해 주세요."
                    : isInMatch
                    ? "진행 중인 경기와 현재 커리어가 모두 종료됩니다.\n저장되지 않은 모든 진행 내용이 사라집니다."
                    : "저장하지 않은 진행은 사라집니다.\n필요하면 저장·불러오기 탭에서 먼저 저장해 주세요.",
                17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 74f), new Vector2(0f, 30f), SecondaryTextColor);
            Button cancel = CreateButton("Cancel", modal,
                isInMatch ? "경기로 돌아가기" : "계속 플레이",
                new Vector2(270f, 58f), new Vector2(-155f, -100f), CardColor, out _);
            cancel.onClick.AddListener(() =>
            {
                _showTitleConfirmation = false;
                Render();
            });
            Button confirm = CreateButton("Confirm", modal,
                isInMatch ? "경기 종료 후 타이틀로 이동" : "타이틀로 이동",
                new Vector2(320f, 58f), new Vector2(165f, -100f), DangerColor, out _);
            confirm.onClick.AddListener(ReturnToTitle);
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        private void RenderInstantResultConfirmation(RectTransform panel)
        {
            RectTransform modal = CreateModal(panel, "InstantResultConfirmation");
            CreateText("Title", modal, "남은 경기를 즉시 진행할까요?", 27, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(620f, 48f), new Vector2(0f, 95f), PrimaryTextColor);
            CreateText("Message", modal, "남은 경기를 계산하고 결과 화면으로 이동합니다.", 17,
                FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 52f), new Vector2(0f, 28f), SecondaryTextColor);
            Button cancel = CreateButton("Cancel", modal, "취소", new Vector2(250f, 58f),
                new Vector2(-145f, -92f), CardColor, out _);
            cancel.onClick.AddListener(() =>
            {
                _showInstantResultConfirmation = false;
                Render();
            });
            Button confirm = CreateButton("Confirm", modal, "즉시 결과 보기",
                new Vector2(280f, 58f), new Vector2(150f, -92f), DangerColor, out _);
            confirm.onClick.AddListener(ConfirmInstantResult);
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        private void SelectProgressMode(MatchProgressMode mode)
        {
            if (mode == MatchProgressMode.InstantResult && _careerManager.HasActiveMatch)
            {
                _pendingProgressMode = mode;
                _showInstantResultConfirmation = true;
                Render();
                return;
            }
            ApplySettings(progressMode: mode);
        }

        private void ConfirmInstantResult()
        {
            ApplySettings(progressMode: _pendingProgressMode, render: false);
            _careerManager.CompleteActiveMatchInstantly();
            _showInstantResultConfirmation = false;
            Close();
        }

        private void ApplySettings(
            BattingApproach? battingApproach = null,
            PitchingApproach? pitchingApproach = null,
            MatchProgressMode? progressMode = null,
            int? gameSpeed = null,
            bool? autoSlow = null,
            bool render = true)
        {
            CareerGameSettings settings = _careerManager.CurrentCareer.GameSettings;
            _careerManager.UpdateGameSettings(
                battingApproach ?? settings.BattingApproach,
                pitchingApproach ?? settings.PitchingApproach,
                progressMode ?? settings.MatchProgressMode,
                gameSpeed ?? settings.GameSpeed,
                autoSlow ?? settings.AutoSlowOnPlayerEvent);
            if (render)
                Render();
        }

        private void ReturnToTitle()
        {
            bool isOwnerMode = UiGameModeSession.IsSelected(UiGameMode.OwnerCareer);
            Time.timeScale = 1f;
            UI_Scene_CareerMatch match = Object.FindFirstObjectByType<UI_Scene_CareerMatch>(FindObjectsInactive.Include);
            UI_Scene_NewGame newGame = Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include);
            match?.StopAllCoroutines();
            newGame?.StopAllCoroutines();
            StopAllCoroutines();
            DOTween.KillAll();

            if (!isOwnerMode)
                _careerManager.EndCareer();
            UiGameModeSession.Clear();
            _newGameManager.DiscardDraftAndShowTitle();
            Close();

            if (SceneManager.GetActiveScene().name != SceneCatalog.ManagementSceneName)
            {
                GameManager.EnsureExists().EnsureManager<SceneLoadManager>("SceneLoadManager")
                    .LoadScene(SceneId.Management, SceneTransitionMode.Direct, 0f);
                return;
            }

            Object.FindFirstObjectByType<UI_Scene_CareerDashboard>(FindObjectsInactive.Include)?.Hide();
            match?.Hide();
            newGame?.Show();
        }

        private static string GetProgressModeLabel(MatchProgressMode mode)
        {
            return mode switch
            {
                MatchProgressMode.FullGameWatch => "전체 경기 관전",
                MatchProgressMode.InterveneOnPlayer => "내 선수 때만 개입",
                MatchProgressMode.PlayerFocusAutomatic => "내 선수 중심 자동",
                MatchProgressMode.InstantResult => "즉시 결과",
                MatchProgressMode.MiniGame => "직접 플레이",
                _ => string.Empty
            };
        }

        private static void CreateHeading(Transform parent, string text, float y)
        {
            CreateText("Heading_" + text, parent, text, 17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(800f, 30f), new Vector2(0f, y), AccentColor);
        }

        private static RectTransform CreateModal(Transform parent, string name)
        {
            RectTransform shade = CreateImage(
                name + "Shade", parent, new Color(0f, 0f, 0f, 0.74f),
                new Vector2(1260f, 900f), Vector2.zero);
            shade.GetComponent<Image>().raycastTarget = true;
            RectTransform modal = CreateImage(name, parent, PanelColor,
                new Vector2(760f, 390f), Vector2.zero);
            TitleUiButtonSkin.ApplyPanel(modal.GetComponent<Image>());
            return modal;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name, Transform parent, Color color, Vector2 size, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            // 동적 재렌더링 후에도 공통 스킨의 이름 추론이 명시한 표면 색을 바꾸지 않는다.
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int fontSize, FontStyle style,
            TextAnchor alignment, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name, Transform parent, string label, Vector2 size, Vector2 position,
            Color color, out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            text = CreateText("Label", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                size - new Vector2(10f, 8f), Vector2.zero, PrimaryTextColor);
            rect.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FramedControl);
            OwnerUiButtonSkin.Apply(button);
            OwnerUiButtonSkin.SetSelected(button, color == SelectedColor || color == DangerColor);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
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

using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>대기실의 구단 정보창과 경기 상태를 표시하고 기존 화면으로의 이동을 요청한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerHome : MonoBehaviour
    {
        private const float DockWidth = 744f;
        private const float DockHeight = 176f;
        private const float SuggestionHeight = 320f;
        private RectTransform _guideHost, _matchPanel, _seasonPanel;
        private int _dashboardState;
        public RectTransform ManagerHost => _guideHost;
        private RectTransform _workspaceRoot;
        private RectTransform _dashboardBackplate;
        private Text _teamNameText;
        private Text _leagueText;
        private Text _seasonText;
        private Text _recordText;
        private Text _rosterText;
        private Text _evaluationText;
        private Text _nextMatchText;
        private Text _opponentText;
        private float _managerFeedbackHeight;
        public string FeedbackMessage { get; private set; } = string.Empty;
        public bool IsFeedbackError { get; private set; }
        public event Action<string, bool> FeedbackChanged;
        private Text _matchStateText;
        private Button _opponentAnalysisButton;
        private Button _matchPreparationButton;
        private Button _playNextGameButton;
        private Button _completeSeasonButton;
        private Text _completeSeasonButtonText;
        private bool _hasRemainingGames;
        private bool _isRegularSeasonCompleted;
        private bool _isPostseasonCompleted;
        private bool _isPlayerPostseasonCompleted;
        private bool _isSeasonReviewAcknowledged;
        private bool _isSeasonActionArmed;

        /// <summary>안내창을 경기 상태창 위에 도킹하는 실제 화면 경계다.</summary>
        public RectTransform GuideDockTarget => _dashboardBackplate;

        public event Action OpponentAnalysisRequested;
        public event Action MatchPreparationRequested;
        public event Action PlayNextGameRequested;
        public event Action CompleteSeasonRequested;
        public event Action AdvanceSeasonRequested;
        public event Action OutsideSuggestionPressed;

        /// <summary>공용 셸의 Workspace 안에 홈을 생성한다.</summary>
        public static UI_Scene_OwnerHome CreateRuntime(RectTransform workspaceHost, RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));
            var owner = new GameObject(nameof(UI_Scene_OwnerHome)).AddComponent<UI_Scene_OwnerHome>();
            owner.Build(workspaceHost);
            return owner;
        }

        /// <summary>순위·일정·선수단은 공급된 Snapshot 값으로만 표시한다.</summary>
        public void Bind(OwnerHomePresentationModel model, bool canPlayNextGame)
        {
            Bind(model, canPlayNextGame, !canPlayNextGame, !canPlayNextGame, !canPlayNextGame, !canPlayNextGame);
        }

        /// <summary>현재 시즌 단계에 따라 홈의 대표 행동과 설명을 바꾼다.</summary>
        public void Bind(OwnerHomePresentationModel model, bool canPlayNextGame,
            bool isRegularSeasonCompleted, bool isPostseasonCompleted, bool isSeasonReviewAcknowledged)
        {
            Bind(model, canPlayNextGame, isRegularSeasonCompleted, isPostseasonCompleted,
                isSeasonReviewAcknowledged, isPostseasonCompleted);
        }

        /// <summary>우리 조와 전체 월드의 포스트시즌 완료를 구분해 다음 행동을 명확히 표시한다.</summary>
        public void Bind(OwnerHomePresentationModel model, bool canPlayNextGame,
            bool isRegularSeasonCompleted, bool isPostseasonCompleted, bool isSeasonReviewAcknowledged,
            bool isPlayerPostseasonCompleted)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            OwnerHomeSnapshot snapshot = model.Snapshot;
            _teamNameText.text = snapshot.TeamName;
            _leagueText.text = snapshot.LeagueText;
            _seasonText.text = snapshot.SeasonText + "  ·  " + snapshot.DateText;
            _recordText.text = string.IsNullOrWhiteSpace(snapshot.RankText) ? "시즌 성적 집계 전" : snapshot.RankText;
            _rosterText.text = model.RosterCountText + "  ·  " + model.RosterCompositionText;
            _evaluationText.text = model.StrengthText + "  /  " + model.CostText;
            _nextMatchText.text = canPlayNextGame && !string.IsNullOrWhiteSpace(snapshot.NextMatchText)
                ? snapshot.NextMatchText : "남은 일정 없음";
            _opponentText.text = canPlayNextGame ? snapshot.OpponentStrengthText : "일정에서 이번 시즌 결과를 확인하세요.";
            if (!canPlayNextGame && isRegularSeasonCompleted && !isPostseasonCompleted)
            {
                _nextMatchText.text = isPlayerPostseasonCompleted ? "우리 조 포스트시즌 종료" : "가을 야구 대진 확인";
                _opponentText.text = isPlayerPostseasonCompleted ? "남은 리그 결과를 확정하세요." : "포스트시즌에서 대진 확인과 경기 관전을 진행하세요.";
            }
            _opponentAnalysisButton.interactable = canPlayNextGame;
            // 잘못된 로스터도 경기 준비 화면에서 수정할 수 있어야 한다.
            _matchPreparationButton.interactable = canPlayNextGame;
            _playNextGameButton.interactable = canPlayNextGame && snapshot.IsRosterValid;
            _hasRemainingGames = canPlayNextGame;
            _isRegularSeasonCompleted = isRegularSeasonCompleted;
            _isPostseasonCompleted = isPostseasonCompleted;
            _isPlayerPostseasonCompleted = isPlayerPostseasonCompleted;
            _isSeasonReviewAcknowledged = isSeasonReviewAcknowledged;
            _isSeasonActionArmed = false;
            _completeSeasonButton.interactable = !canPlayNextGame || snapshot.IsRosterValid;
            _completeSeasonButtonText.text = canPlayNextGame ? "시즌 완료"
                : !isRegularSeasonCompleted ? "정규시즌 정리"
                : !isPostseasonCompleted ? isPlayerPostseasonCompleted ? "남은 리그 마감" : "포스트시즌"
                : !isSeasonReviewAcknowledged ? "시즌 결산"
                : "다음 시즌";
            _matchStateText.text = canPlayNextGame ? snapshot.IsRosterValid ? "경기 준비 완료" : "선수단 확인 필요"
                : !isRegularSeasonCompleted ? "리그 경기 정리 필요"
                : !isPostseasonCompleted ? isPlayerPostseasonCompleted ? "타 리그 진행 중" : "포스트시즌 대기"
                : "시즌 종료";
            _matchStateText.color = canPlayNextGame && snapshot.IsRosterValid ? CareerUiTheme.Number : CareerUiTheme.TextPrimary;
            string feedback = !snapshot.IsRosterValid ? snapshot.RosterValidationMessage
                : canPlayNextGame ? string.Empty
                : !isRegularSeasonCompleted ? "다른 조의 정규시즌 결과를 확정해야 합니다."
                : !isPostseasonCompleted ? isPlayerPostseasonCompleted
                    ? "우리 조 결과는 확정됐습니다. 남은 리그 결과를 마감하면 시즌 결산이 열립니다."
                    : "최종 순위와 대진을 확인한 뒤 포스트시즌을 진행하세요."
                : !isSeasonReviewAcknowledged ? "시즌 성과와 다음 등급을 결산에서 확인하세요."
                : "시즌 기록을 확인하고 다음 시즌을 준비하세요.";
            if (snapshot.IsRosterValid && snapshot.ContractArrears > 0L)
                feedback = $"미지급 급여·계약금 {OwnerMoneyFormatter.Format(snapshot.ContractArrears)} · 수입에서 우선 상환합니다.";
            SetFeedback(feedback, !snapshot.IsRosterValid);
            LayoutSeasonAction();
            ResizeDashboard();
        }

        /// <summary>저장과 경기 준비 결과를 정보창에 표시한다.</summary>
        public void SetFeedback(string message, bool isError = false)
        {
            FeedbackMessage = message ?? string.Empty;
            IsFeedbackError = isError;
            FeedbackChanged?.Invoke(FeedbackMessage, isError);
        }

        /// <summary>세부 화면으로 이동하면 홈에 속한 전체 UI를 숨긴다.</summary>
        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
        }

        private void Build(RectTransform workspaceHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerHomeWorkspace", false);
            _workspaceRoot.gameObject.AddComponent<CareerUiPreserveTextColor>();
            _dashboardBackplate = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "MainDashboard", false);
            _dashboardBackplate.anchorMin = _dashboardBackplate.anchorMax = new Vector2(1f, 0f);
            _dashboardBackplate.pivot = new Vector2(1f, 0f);
            _matchPanel = Surface(_dashboardBackplate, "NextMatchPanel", OwnerDashboardStyle.Surface, 0, 0, DockWidth, 320);
            BuildMatchDiamond(_matchPanel);
            UIOwnerFrontOfficePanel.Apply(_matchPanel, "MainDashboard");
            OwnerDashboardStyle.Rule(_matchPanel, "MatchAccent", Vector2.zero, Vector2.zero,
                new Vector2(24, 279), new Vector2(58, 282), OwnerDashboardStyle.Gold);
            Label(_matchPanel, "MatchHeading", "오늘의 경기", 22, FontStyle.Normal, OwnerDashboardStyle.Gold,
                new Vector2(74, 258), new Vector2(350, 304));
            _matchStateText = Label(_matchPanel, "MatchState", "오늘의 경기", 22, FontStyle.Bold, CareerUiTheme.AccentGold,
                new Vector2(24, 208), new Vector2(480, 248));
            _nextMatchText = Label(_matchPanel, "NextMatchValue", "", 32, FontStyle.Bold, OwnerDashboardStyle.Ivory,
                new Vector2(24, 156), new Vector2(720, 208));
            _opponentText = Label(_matchPanel, "OpponentStrength", "", 22, FontStyle.Normal, CareerUiTheme.TextSecondary,
                new Vector2(24, 104), new Vector2(720, 148));
            _playNextGameButton = CreateAction(_matchPanel, "PlayNextGameButton", "경기 시작",
                () => PlayNextGameRequested?.Invoke(), new Vector2(24, 24), new Vector2(384, 92), true);
            _matchPreparationButton = CreateAction(_matchPanel, "MatchPreparationButton", "경기 준비",
                () => MatchPreparationRequested?.Invoke(), new Vector2(392, 24), new Vector2(552, 92));
            _opponentAnalysisButton = CreateAction(_matchPanel, "OpponentAnalysisButton", "상대 분석",
                () => OpponentAnalysisRequested?.Invoke(), new Vector2(560, 24), new Vector2(720, 92));
            _completeSeasonButton = CreateAction(_matchPanel, "CompleteSeasonButton", "시즌 완료",
                HandleSeasonActionRequested, new Vector2(572, 24), new Vector2(720, 92));
            _completeSeasonButtonText = _completeSeasonButton.transform.Find("Label").GetComponent<Text>();
            _guideHost = OwnerWorkspaceUiFactory.CreateRoot(_dashboardBackplate, "ManagerHost", false);
            var hitArea = _workspaceRoot.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear; hitArea.raycastTarget = true;
            _workspaceRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            _workspaceRoot.gameObject.AddComponent<UIOwnerDashboardDismissArea>().Initialize(_guideHost,
                () => OutsideSuggestionPressed?.Invoke());
            _seasonPanel = Surface(_dashboardBackplate, "ClubInformationPanel", CareerUiTheme.ShellHeader, 0, 0, DockWidth, DockHeight);
            OwnerDashboardStyle.Rule(_seasonPanel, "SeasonDivider", Vector2.zero, Vector2.zero,
                new Vector2(24, 128), new Vector2(720, 129), OwnerDashboardStyle.Line);
            _teamNameText = Label(_seasonPanel, "TeamName", "", 22, FontStyle.Bold, CareerUiTheme.TextPrimary,
                new Vector2(24, 130), new Vector2(390, 166));
            _leagueText = Label(_seasonPanel, "League", "", 22, FontStyle.Bold, CareerUiTheme.AccentGold,
                new Vector2(400, 130), new Vector2(720, 166));
            _seasonText = Label(_seasonPanel, "Season", "", 22, FontStyle.Normal, CareerUiTheme.TextSecondary,
                new Vector2(24, 90), new Vector2(430, 126));
            _recordText = Label(_seasonPanel, "Record", "", 22, FontStyle.Normal, CareerUiTheme.TextPrimary,
                new Vector2(440, 90), new Vector2(720, 126));
            _rosterText = Label(_seasonPanel, "Roster", "", 22, FontStyle.Normal, CareerUiTheme.TextSecondary,
                new Vector2(24, 50), new Vector2(720, 86));
            _evaluationText = Label(_seasonPanel, "Evaluation", "", 22, FontStyle.Normal, CareerUiTheme.TextSecondary,
                new Vector2(24, 10), new Vector2(720, 46));
            SetDashboardState(0);
        }

        /// <summary>추천·리포트를 홈의 단일 대시보드 내부에 재배치한다.</summary>
        public void SetDashboardState(int state)
        {
            _dashboardState = state;
            bool reports = state == 2;
            UIOwnerFrontOfficePanel.Apply(_matchPanel, reports ? "CompactStrip" : "MainDashboard");
            // 접기·펼치기는 카드 안의 보조 행동만 바꾼다. 슬롯 높이를 바꾸면 전체 홈 배율까지 흔들린다.
            float guideHeight = (reports ? 620 : SuggestionHeight) + _managerFeedbackHeight;
            float seasonHeight = reports ? 0 : DockHeight + 12;
            float matchHeight = reports ? 140 : 320;
            _dashboardBackplate.sizeDelta = new Vector2(DockWidth, seasonHeight + guideHeight + 12 + matchHeight);
            SetRect(_seasonPanel, Vector2.zero, new Vector2(DockWidth, DockHeight));
            _seasonPanel.gameObject.SetActive(!reports);
            SetRect(_guideHost, new Vector2(0, seasonHeight), new Vector2(DockWidth, seasonHeight + guideHeight));
            SetRect(_matchPanel, new Vector2(0, 12 + seasonHeight + guideHeight),
                new Vector2(DockWidth, 12 + seasonHeight + guideHeight + matchHeight));
            _matchStateText.gameObject.SetActive(!reports);
            _matchPanel.Find("MatchHeading").gameObject.SetActive(!reports);
            _matchPanel.Find("MatchAccent").gameObject.SetActive(!reports);
            _matchPanel.Find("BaseballDiamond").gameObject.SetActive(!reports);
            _opponentText.gameObject.SetActive(!reports);
            _matchPreparationButton.gameObject.SetActive(!reports);
            _opponentAnalysisButton.gameObject.SetActive(!reports);
            SetRect(_nextMatchText.rectTransform, new Vector2(24, reports ? 88 : 156), new Vector2(720, reports ? 136 : 208));
            _nextMatchText.fontSize = reports ? 26 : 32;
            ResizeDashboard();
            LayoutSeasonAction();
        }

        /// <summary>매니저 안내 높이만큼 카드 내부를 확장한다.</summary>
        public void SetManagerFeedbackHeight(float height)
        {
            _managerFeedbackHeight = height;
            SetDashboardState(_dashboardState);
        }

        private void LayoutSeasonAction()
        {
            if (_playNextGameButton == null) return;
            _playNextGameButton.gameObject.SetActive(_hasRemainingGames);
            _matchPreparationButton.gameObject.SetActive(_hasRemainingGames && _dashboardState != 2);
            _opponentAnalysisButton.gameObject.SetActive(_hasRemainingGames && _dashboardState != 2);
            SetRect((RectTransform)_completeSeasonButton.transform,
                new Vector2(_hasRemainingGames ? 540 : 24, _hasRemainingGames && _dashboardState != 2 ? 248 : 24),
                new Vector2(_hasRemainingGames ? 720 : 384, _hasRemainingGames && _dashboardState != 2 ? 316 : 92));
            OwnerUiButtonSkin.Apply(_completeSeasonButton, _hasRemainingGames ? OwnerButtonRole.Quiet : OwnerButtonRole.Primary);
        }

        private void LateUpdate() => ResizeDashboard();

        private static void BuildMatchDiamond(RectTransform panel)
        {
            var diamond = OwnerWorkspaceUiFactory.CreateRoot(panel, "BaseballDiamond", false);
            SetRect(diamond, new Vector2(530, 140), new Vector2(674, 284));
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            Color line = new Color32(218, 187, 123, 16);
            OwnerDashboardStyle.Rule(diamond, "FirstBaseLine", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2), line);
            OwnerDashboardStyle.Rule(diamond, "ThirdBaseLine", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(2, 0), line);
            OwnerDashboardStyle.Rule(diamond, "RightFieldLine", new Vector2(1, 0), Vector2.one, new Vector2(-2, 0), Vector2.zero, line);
            OwnerDashboardStyle.Rule(diamond, "LeftFieldLine", new Vector2(0, 1), Vector2.one, new Vector2(0, -2), Vector2.zero, line);
        }

        private void ResizeDashboard()
        {
            if (_dashboardBackplate == null || _workspaceRoot == null) return;
            // 화면의 40% 이내에서 동일 카드 비율을 유지한다. 720p에서도 본문 14px·버튼 44px을 확보한다.
            float referenceHeight = DockHeight + 12 + SuggestionHeight + _managerFeedbackHeight + 12 + 320;
            float scale = Mathf.Min(1f, _workspaceRoot.rect.width * .40f / DockWidth,
                Mathf.Max(0, _workspaceRoot.rect.height - 24) / referenceHeight);
            _dashboardBackplate.localScale = Vector3.one * scale;
            _dashboardBackplate.anchoredPosition = new Vector2(-24, 12);
        }
        private static RectTransform Surface(Transform parent, string name, Color color, float left, float bottom, float right, float top)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            SetRect(image.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            OwnerDashboardStyle.ApplySurface(image.rectTransform);
            return image.rectTransform;
        }

        private static Text Row(RectTransform parent, string name, string title, float bottom, bool alternate = false)
        {
            RectTransform row = Surface(parent, name + "Row",
                alternate ? CareerUiTheme.ReferencePanelHeader : CareerUiTheme.ReferencePanel,
                2f, bottom, DockWidth - 2f, bottom + 32f);
            Label(row, "Title", title, 16, FontStyle.Bold, CareerUiTheme.ReferenceAccent,
                new Vector2(12f, 0f), new Vector2(126f, 32f));
            return Label(row, "Value", "", 17, FontStyle.Normal, CareerUiTheme.ReferenceText,
                new Vector2(134f, 0f), new Vector2(684f, 32f));
        }

        private static Text Label(Transform parent, string name, string value, int size, FontStyle style, Color color, Vector2 min, Vector2 max)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, TextAnchor.MiddleLeft, color);
            text.color = color;
            OwnerDashboardStyle.SetTypography(text, style == FontStyle.Bold);
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static Button CreateAction(Transform parent, string name, string label, Action action, Vector2 min, Vector2 max, bool primary = false)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            SetRect(button.GetComponent<RectTransform>(), min, max);
            button.GetComponent<Image>().color = primary ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceButton;
            Text text = button.transform.Find("Label").GetComponent<Text>();
            text.color = primary ? CareerUiTheme.TextPrimary : CareerUiTheme.ReferenceText;
            text.fontSize = 22;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            OwnerUiButtonSkin.Apply(button, primary ? OwnerButtonRole.Primary : OwnerButtonRole.Quiet);
            OwnerUiButtonSkin.SetDashboardStyle(button);
            return button;
        }

        private void HandleSeasonActionRequested()
        {
            if (!_isSeasonActionArmed)
            {
                _isSeasonActionArmed = true;
                _completeSeasonButtonText.text = "진행 확인";
                string message = _hasRemainingGames
                    ? "남은 모든 경기를 진행합니다. 미리 배치한 작전카드는 해당 경기마다 사용됩니다. 한 번 더 누르면 시작합니다."
                    : !_isRegularSeasonCompleted
                    ? "다른 조의 남은 정규시즌을 진행합니다. 한 번 더 누르면 시작합니다."
                        : !_isPostseasonCompleted
                            ? _isPlayerPostseasonCompleted
                                ? "우리 조는 끝났습니다. 다른 리그의 포스트시즌을 마감합니다. 한 번 더 누르면 엽니다."
                                : "최종 순위와 포스트시즌 대진을 확인합니다. 한 번 더 누르면 엽니다."
                            : !_isSeasonReviewAcknowledged
                                ? "정규시즌·포스트시즌·다음 등급을 확인합니다. 한 번 더 누르면 엽니다."
                                : "계약과 급여를 마감하고 다음 시즌을 엽니다. 한 번 더 누르면 시작합니다.";
                SetFeedback(message);
                return;
            }

            _isSeasonActionArmed = false;
            if (_hasRemainingGames) CompleteSeasonRequested?.Invoke();
            else AdvanceSeasonRequested?.Invoke();
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}

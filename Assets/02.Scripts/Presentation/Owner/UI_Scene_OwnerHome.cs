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
        private const float DockWidth = 704f;
        private const float DockHeight = 292f;
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
        private Text _feedbackText;
        private Text _matchStateText;
        private Button _opponentAnalysisButton;
        private Button _matchPreparationButton;
        private Button _playNextGameButton;

        /// <summary>안내창을 경기 상태창 위에 도킹하는 실제 화면 경계다.</summary>
        public RectTransform GuideDockTarget => _dashboardBackplate;

        public event Action OpponentAnalysisRequested;
        public event Action MatchPreparationRequested;
        public event Action PlayNextGameRequested;
        public event Action<string> NavigationRequested;
        public event Action SaveRequested;

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
            _opponentAnalysisButton.interactable = canPlayNextGame;
            // 잘못된 로스터도 경기 준비 화면에서 수정할 수 있어야 한다.
            _matchPreparationButton.interactable = canPlayNextGame;
            _playNextGameButton.interactable = canPlayNextGame && snapshot.IsRosterValid;
            _matchStateText.text = !canPlayNextGame ? "일정 종료" : snapshot.IsRosterValid ? "경기 준비 완료" : "선수단 확인 필요";
            _matchStateText.color = canPlayNextGame && snapshot.IsRosterValid ? CareerUiTheme.Number : CareerUiTheme.TextPrimary;
            _feedbackText.text = !snapshot.IsRosterValid ? snapshot.RosterValidationMessage
                : canPlayNextGame ? "상대 확인 → 경기 준비 → 다음 경기 진행" : "남은 일정이 없습니다. 구단에서 시즌 진행을 확인하세요.";
            _feedbackText.color = snapshot.IsRosterValid ? CareerUiTheme.ReferenceTextSecondary : CareerUiTheme.Loss;
        }

        /// <summary>저장과 경기 준비 결과를 정보창에 표시한다.</summary>
        public void SetFeedback(string message, bool isError = false)
        {
            _feedbackText.text = message ?? string.Empty;
            _feedbackText.color = isError ? CareerUiTheme.Loss : CareerUiTheme.ReferenceAccent;
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
            RectTransform dock = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "DashboardColumns", false);
            dock.anchorMin = dock.anchorMax = new Vector2(1f, 0f);
            dock.pivot = new Vector2(1f, 0f);
            dock.anchoredPosition = Vector2.zero;
            dock.sizeDelta = new Vector2(DockWidth, DockHeight + 116f);
            _dashboardBackplate = dock;

            RectTransform match = Surface(dock, "NextMatchPanel", CareerUiTheme.ShellHeader, 0f, 304f, DockWidth, 408f);
            _matchStateText = Label(match, "MatchState", "", 20, FontStyle.Bold, CareerUiTheme.Number,
                new Vector2(16f, 66f), new Vector2(300f, 96f));
            _nextMatchText = Label(match, "NextMatchValue", "", 20, FontStyle.Bold, CareerUiTheme.TextPrimary,
                new Vector2(16f, 34f), new Vector2(490f, 66f));
            _opponentText = Label(match, "OpponentStrength", "", 16, FontStyle.Normal, CareerUiTheme.TextSecondary,
                new Vector2(16f, 8f), new Vector2(490f, 34f));
            _playNextGameButton = CreateAction(match, "PlayNextGameButton", "다음 경기 진행",
                () => PlayNextGameRequested?.Invoke(), new Vector2(516f, 22f), new Vector2(688f, 82f), true);

            RectTransform info = Surface(dock, "ClubInformationPanel", CareerUiTheme.ReferencePanel, 0f, 0f, DockWidth, DockHeight);
            RectTransform teamHeader = Surface(info, "TeamHeader", CareerUiTheme.ShellHeader, 2f, 234f, DockWidth - 2f, 290f);
            _teamNameText = Label(teamHeader, "TeamName", "", 23, FontStyle.Bold, CareerUiTheme.TextPrimary,
                new Vector2(16f, 6f), new Vector2(450f, 50f));
            _leagueText = Label(teamHeader, "League", "", 18, FontStyle.Bold, CareerUiTheme.TextPrimary,
                new Vector2(460f, 6f), new Vector2(682f, 50f));
            _leagueText.alignment = TextAnchor.MiddleRight;
            _seasonText = Row(info, "Season", "페넌트레이스", 200f, true);
            _recordText = Row(info, "Record", "시즌 성적", 168f);
            _rosterText = Row(info, "Roster", "선수단", 136f, true);
            _evaluationText = Row(info, "Evaluation", "전력 / 비용", 104f);
            _feedbackText = Label(info, "Feedback", "", 16, FontStyle.Normal, CareerUiTheme.ReferenceTextSecondary,
                new Vector2(14f, 52f), new Vector2(690f, 102f));

            RectTransform actions = Surface(info, "QuickActions", CareerUiTheme.ReferencePanelHeader, 2f, 2f, DockWidth - 2f, 50f);
            _opponentAnalysisButton = CreateAction(actions, "OpponentAnalysisButton", "상대 분석",
                () => OpponentAnalysisRequested?.Invoke(), new Vector2(8f, 6f), new Vector2(138f, 42f));
            _matchPreparationButton = CreateAction(actions, "MatchPreparationButton", "경기 준비",
                () => MatchPreparationRequested?.Invoke(), new Vector2(146f, 6f), new Vector2(276f, 42f));
            CreateAction(actions, "ScheduleButton", "일정·결과",
                () => NavigationRequested?.Invoke(OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId),
                new Vector2(284f, 6f), new Vector2(414f, 42f));
            CreateAction(actions, "ClubButton", "구단 정보",
                () => NavigationRequested?.Invoke(OwnerNavigationRoutes.ClubInformation),
                new Vector2(422f, 6f), new Vector2(552f, 42f));
            CreateAction(actions, "SaveButton", "저장", () => SaveRequested?.Invoke(),
                new Vector2(560f, 6f), new Vector2(690f, 42f));
        }

        private static RectTransform Surface(Transform parent, string name, Color color, float left, float bottom, float right, float top)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            SetRect(image.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(1f, -1f);
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
            text.fontSize = primary ? 18 : 17;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}

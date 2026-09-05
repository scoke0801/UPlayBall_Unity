using System;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 구단주 Runtime의 다음 경기·로스터·자원을 공용 Shell 안에 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerHome : MonoBehaviour
    {
        private RectTransform _workspaceRoot;
        private RectTransform _actionRoot;
        private Text _nextMatchText;
        private Text _rosterText;
        private Text _resourceText;
        private Text _feedbackText;
        private Text _clubText;
        private Button _opponentAnalysisButton;
        private Button _matchPreparationButton;
        private Button _playNextGameButton;
        private Button _saveButton;
        private Button _titleButton;

        public event Action OpponentAnalysisRequested;
        public event Action MatchPreparationRequested;
        public event Action PlayNextGameRequested;
        public event Action SaveRequested;
        public event Action TitleRequested;

        public static UI_Scene_OwnerHome CreateRuntime(
            RectTransform workspaceHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));

            var owner = new GameObject(nameof(UI_Scene_OwnerHome)).AddComponent<UI_Scene_OwnerHome>();
            owner.Build(workspaceHost, actionBarHost);
            return owner;
        }

        public void Bind(OwnerHomePresentationModel model, bool canPlayNextGame)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            EnsureBuilt();

            OwnerHomeSnapshot snapshot = model.Snapshot;
            _clubText.text = $"{snapshot.TeamName}   |   {snapshot.LeagueText}   {snapshot.RankText}\n{snapshot.SeasonText}   {snapshot.DateText}";
            _nextMatchText.text = string.IsNullOrWhiteSpace(snapshot.NextMatchText)
                ? "남은 일정 없음"
                : snapshot.NextMatchText;
            if (!string.IsNullOrEmpty(snapshot.OpponentStrengthText))
                _nextMatchText.text += "\n" + snapshot.OpponentStrengthText;
            _rosterText.text =
                $"{model.StrengthText}\n{model.CostText}\n{model.RosterCountText}\n" +
                $"{model.RosterCompositionText}\n{model.ForeignPlayerText}\n{model.OwnedCardText}";
            _rosterText.color = model.RosterEmphasis == ShellStatusEmphasis.Critical
                ? CareerUiTheme.Error
                : CareerUiTheme.TextPrimary;
            _resourceText.text =
                $"보유 자금  {OwnerMoneyFormatter.Format(snapshot.Money)}\n스카우트 포인트  {snapshot.ScoutingPoints:N0}\n" +
                $"육성 포인트  {snapshot.DevelopmentPoints:N0}\n확정 영입 누적  {snapshot.PityGauge:N0}";
            _opponentAnalysisButton.interactable = canPlayNextGame;
            _matchPreparationButton.interactable = canPlayNextGame;
            _playNextGameButton.interactable = canPlayNextGame && snapshot.IsRosterValid;
            _feedbackText.text = snapshot.IsRosterValid
                ? canPlayNextGame ? "출전 준비 완료" : "남은 일정 없음"
                : snapshot.RosterValidationMessage;
            _feedbackText.color = snapshot.IsRosterValid
                ? CareerUiTheme.TextSecondary
                : CareerUiTheme.Error;
        }

        public void SetFeedback(string message, bool isError = false)
        {
            EnsureBuilt();
            _feedbackText.text = message ?? string.Empty;
            _feedbackText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (_opponentAnalysisButton != null) _opponentAnalysisButton.onClick.RemoveAllListeners();
            if (_matchPreparationButton != null) _matchPreparationButton.onClick.RemoveAllListeners();
            if (_playNextGameButton != null) _playNextGameButton.onClick.RemoveAllListeners();
            if (_saveButton != null) _saveButton.onClick.RemoveAllListeners();
            if (_titleButton != null) _titleButton.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerHomeWorkspace", false);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "DashboardColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);

            OwnerWorkspaceUiFactory.Panel nextMatch = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "NextMatchPanel", "다음 경기", true);
            OwnerRuntimeUiFactory.SetAnchors(
                nextMatch.Root,
                new Vector2(0.56f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 248f),
                new Vector2(0f, 476f));
            OwnerWorkspaceUiFactory.AddVerticalLayout(nextMatch.Content, CareerUiTheme.Space3);
            _nextMatchText = CreateValue(nextMatch.Content, "NextMatchValue", 25, FontStyle.Bold);
            OwnerWorkspaceUiFactory.SetFlexible(_nextMatchText.rectTransform, 1f, 1f);
            _nextMatchText.GetComponent<LayoutElement>().minHeight = 52f;
            _nextMatchText.resizeTextForBestFit = true;
            _nextMatchText.resizeTextMinSize = 16;
            _nextMatchText.resizeTextMaxSize = 25;
            RectTransform nextMatchActions = OwnerWorkspaceUiFactory.CreateRoot(
                nextMatch.Content, "NextMatchActions", false);
            HorizontalLayoutGroup preparationActions = OwnerWorkspaceUiFactory.AddHorizontalLayout(nextMatchActions, 10f);
            preparationActions.childForceExpandWidth = false;
            preparationActions.childAlignment = TextAnchor.MiddleRight;
            OwnerWorkspaceUiFactory.SetFlexible(nextMatchActions, 1f, 0f);
            nextMatchActions.GetComponent<LayoutElement>().minHeight = 42f;
            nextMatchActions.GetComponent<LayoutElement>().preferredHeight = 42f;
            _opponentAnalysisButton = OwnerWorkspaceUiFactory.CreateButton(
                nextMatchActions, "OpponentAnalysisButton", "상대 분석", () => OpponentAnalysisRequested?.Invoke());
            _matchPreparationButton = OwnerWorkspaceUiFactory.CreateButton(
                nextMatchActions, "MatchPreparationButton", "경기 준비", () => MatchPreparationRequested?.Invoke());

            OwnerWorkspaceUiFactory.Panel roster = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "RosterPanel", "1군 현황");
            OwnerRuntimeUiFactory.SetAnchors(
                roster.Root,
                new Vector2(0.56f, 0f),
                new Vector2(0.79f, 0f),
                Vector2.zero,
                new Vector2(-4f, 178f));
            _rosterText = CreateValue(roster.Content, "RosterValue", 18, FontStyle.Bold);

            OwnerWorkspaceUiFactory.Panel resources = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "ResourcePanel", "구단 자원");
            OwnerRuntimeUiFactory.SetAnchors(
                resources.Root,
                new Vector2(0.79f, 0f),
                new Vector2(1f, 0f),
                new Vector2(4f, 0f),
                new Vector2(0f, 178f));
            _resourceText = CreateValue(resources.Content, "ResourceValue", 18, FontStyle.Bold);

            Image clubStrip = OwnerRuntimeUiFactory.CreateImage("ClubIdentityStrip", columns, CareerUiTheme.ShellHeader);
            OwnerRuntimeUiFactory.SetAnchors(clubStrip.rectTransform,
                new Vector2(0.56f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 184f), new Vector2(0f, 240f));
            _clubText = CreateValue(clubStrip.transform, "ClubIdentity", 18, FontStyle.Bold);
            _clubText.rectTransform.offsetMin = new Vector2(14f, 4f);
            _clubText.rectTransform.offsetMax = new Vector2(-14f, -4f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerHomeActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                _actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 4, 4);
            actions.childForceExpandWidth = false;
            actions.childAlignment = TextAnchor.MiddleRight;
            _actionRoot.gameObject.AddComponent<CareerUiPreserveTextColor>();
            _feedbackText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "Feedback", string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_feedbackText.rectTransform, 1f, 0f);
            _feedbackText.GetComponent<LayoutElement>().minHeight = 36f;
            _saveButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "SaveButton", "저장", () => SaveRequested?.Invoke());
            _playNextGameButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "PlayNextGameButton", "다음 경기 진행", () => PlayNextGameRequested?.Invoke());
            _titleButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "TitleButton", "모드 선택", () => TitleRequested?.Invoke());

            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_actionRoot);
            ApplyHomeFrame(nextMatch, true);
            ApplyHomeFrame(roster, false);
            ApplyHomeFrame(resources, false);
            ApplyHomeButton(_opponentAnalysisButton, 152f);
            ApplyHomeButton(_matchPreparationButton, 152f);
            ApplyHomeButton(_saveButton, 100f);
            ApplyHomeButton(_titleButton, 116f);
            ApplyHomeButton(_playNextGameButton, 204f, true);
            // 보조 행동 뒤에 진행 버튼을 놓아 읽는 방향 끝에서 다음 경기를 선택한다.
            _playNextGameButton.transform.SetAsLastSibling();
            _clubText.color = Color.white;
            _rosterText.resizeTextForBestFit = true;
            _rosterText.resizeTextMinSize = 14;
            _rosterText.resizeTextMaxSize = 18;
            _resourceText.resizeTextForBestFit = true;
            _resourceText.resizeTextMinSize = 14;
            _resourceText.resizeTextMaxSize = 18;
        }

        private static Text CreateValue(Transform parent, string name, int fontSize, FontStyle style)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, string.Empty, fontSize, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerWorkspaceUiFactory.Stretch(text.rectTransform);
            return text;
        }

        private static void ApplyHomeFrame(OwnerWorkspaceUiFactory.Panel panel, bool isHero)
        {
            panel.Root.GetComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.TexturedPanel, isHero);
            CareerUiSkin.ApplyVisualElement(panel.Root.GetComponent<Image>());
            // 기존 평면 헤더와 외곽선은 텍스처 프레임을 덮으므로 숨긴다.
            panel.Root.Find("HeaderSurface").gameObject.SetActive(false);
            panel.Root.Find("HeaderAccent").gameObject.SetActive(false);
            panel.Root.Find("ThinBorder").gameObject.SetActive(false);
            panel.Root.gameObject.AddComponent<CareerUiPreserveTextColor>();
            Text header = panel.Root.Find("HeaderSlot").GetComponent<Text>();
            header.color = CareerUiTheme.AccentGold;
            header.rectTransform.offsetMin = new Vector2(36f, -40f);
            header.rectTransform.offsetMax = new Vector2(-36f, -8f);
            panel.Content.offsetMin = new Vector2(20f, 16f);
            panel.Content.offsetMax = new Vector2(-20f, -46f);
            foreach (Text text in panel.Content.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponentInParent<Button>() == null)
                    text.color = CareerUiTheme.TextPrimary;
            }
        }

        private static void ApplyHomeButton(Button button, float width, bool isPrimary = false)
        {
            button.GetComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.TexturedAction, isPrimary);
            button.gameObject.AddComponent<CareerUiPreserveTextColor>();
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;
            CareerUiSkin.ApplyButton(button);
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.color = isPrimary ? CareerUiTheme.AccentGold : CareerUiTheme.TextPrimary;
            label.fontSize = isPrimary ? 17 : 15;
            label.fontStyle = FontStyle.Bold;
        }

        private void EnsureBuilt()
        {
            if (_workspaceRoot == null || _actionRoot == null)
                throw new InvalidOperationException("CreateRuntime으로 Owner Home을 먼저 생성해야 합니다.");
        }
    }
}

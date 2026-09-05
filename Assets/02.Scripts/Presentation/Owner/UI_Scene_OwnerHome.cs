using System;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 홈의 다음 경기와 분석·준비·진행 행동을 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerHome : MonoBehaviour
    {
        private RectTransform _workspaceRoot;
        private Text _nextMatchText;
        private Text _feedbackText;
        private Button _opponentAnalysisButton;
        private Button _matchPreparationButton;
        private Button _playNextGameButton;

        public event Action OpponentAnalysisRequested;
        public event Action MatchPreparationRequested;
        public event Action PlayNextGameRequested;

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
            _nextMatchText.text = string.IsNullOrWhiteSpace(snapshot.NextMatchText)
                ? "남은 일정 없음"
                : snapshot.NextMatchText;
            if (!string.IsNullOrEmpty(snapshot.OpponentStrengthText))
                _nextMatchText.text += "\n<size=15><color=#B9C8D8>" + snapshot.OpponentStrengthText + "</color></size>";
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
        }

        private void OnDestroy()
        {
            if (_opponentAnalysisButton != null) _opponentAnalysisButton.onClick.RemoveAllListeners();
            if (_matchPreparationButton != null) _matchPreparationButton.onClick.RemoveAllListeners();
            if (_playNextGameButton != null) _playNextGameButton.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerHomeWorkspace", false);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "DashboardColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);

            Image dashboard = OwnerRuntimeUiFactory.CreateImage("DashboardBackplate", columns, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(dashboard.rectTransform,
                new Vector2(0.56f, 0f), new Vector2(1f, 0f),
                new Vector2(-8f, -8f), new Vector2(8f, 206f));
            dashboard.gameObject.AddComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.TexturedPanel);

            OwnerWorkspaceUiFactory.Panel nextMatch = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "NextMatchPanel", "다음 경기", true);
            OwnerRuntimeUiFactory.SetAnchors(
                nextMatch.Root,
                new Vector2(0.56f, 0f),
                new Vector2(1f, 0f),
                new Vector2(8f, 0f),
                new Vector2(-8f, 198f));
            OwnerWorkspaceUiFactory.AddVerticalLayout(nextMatch.Content, 8f);
            _nextMatchText = CreateValue(nextMatch.Content, "NextMatchValue", 22, FontStyle.Bold);
            OwnerWorkspaceUiFactory.SetFlexible(_nextMatchText.rectTransform, 1f, 1f);
            _nextMatchText.GetComponent<LayoutElement>().minHeight = 48f;
            _nextMatchText.resizeTextForBestFit = true;
            _nextMatchText.resizeTextMinSize = 16;
            _nextMatchText.resizeTextMaxSize = 22;
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

            _playNextGameButton = OwnerWorkspaceUiFactory.CreateButton(
                nextMatchActions, "PlayNextGameButton", "다음 경기 진행", () => PlayNextGameRequested?.Invoke());
            _feedbackText = OwnerWorkspaceUiFactory.CreateText(
                nextMatch.Content, "Feedback", string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            var feedbackLayout = _feedbackText.gameObject.AddComponent<LayoutElement>();
            feedbackLayout.minHeight = 26f;
            feedbackLayout.preferredHeight = 26f;

            CareerUiSkin.Apply(_workspaceRoot);
            ApplyHomeSection(nextMatch);
            ApplyHomeButton(_opponentAnalysisButton, 152f);
            ApplyHomeButton(_matchPreparationButton, 152f);
            ApplyHomeButton(_playNextGameButton, 204f, true);
        }

        private static Text CreateValue(Transform parent, string name, int fontSize, FontStyle style)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, string.Empty, fontSize, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerWorkspaceUiFactory.Stretch(text.rectTransform);
            return text;
        }

        private static void ApplyHomeSection(OwnerWorkspaceUiFactory.Panel panel)
        {
            panel.Root.GetComponent<CareerUiVisualElement>()
                .Initialize(CareerUiVisualRole.FlatSurface);
            panel.Root.GetComponent<Image>().color = new Color(0.02f, 0.045f, 0.08f, 0.46f);
            CareerUiSkin.ApplyVisualElement(panel.Root.GetComponent<Image>());
            // 외곽 프레임 하나 안에서 정보 구획만 나누어 중첩 장식을 피한다.
            panel.Root.Find("HeaderSurface").gameObject.SetActive(false);
            panel.Root.Find("HeaderAccent").gameObject.SetActive(false);
            panel.Root.Find("ThinBorder").gameObject.SetActive(false);
            panel.Root.gameObject.AddComponent<CareerUiPreserveTextColor>();
            Text header = panel.Root.Find("HeaderSlot").GetComponent<Text>();
            header.color = CareerUiTheme.AccentGold;
            header.fontSize = 14;
            header.rectTransform.offsetMin = new Vector2(20f, -32f);
            header.rectTransform.offsetMax = new Vector2(-20f, -6f);
            panel.Content.offsetMin = new Vector2(20f, 16f);
            panel.Content.offsetMax = new Vector2(-20f, -36f);
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
            if (_workspaceRoot == null)
                throw new InvalidOperationException("CreateRuntime으로 Owner Home을 먼저 생성해야 합니다.");
        }
    }
}

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
        private Button _playNextGameButton;
        private Button _saveButton;
        private Button _titleButton;

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
            _nextMatchText.text = string.IsNullOrWhiteSpace(snapshot.NextMatchText)
                ? "남은 일정 없음"
                : snapshot.NextMatchText;
            _rosterText.text =
                $"{model.RosterCountText}\n{model.RosterCompositionText}\n{model.ForeignPlayerText}\n{model.OwnedCardText}";
            _rosterText.color = model.RosterEmphasis == ShellStatusEmphasis.Critical
                ? CareerUiTheme.Error
                : CareerUiTheme.TextPrimary;
            _resourceText.text =
                $"Money  {OwnerMoneyFormatter.Format(snapshot.Money)}\nSP  {snapshot.ScoutingPoints:N0}\n" +
                $"DP  {snapshot.DevelopmentPoints:N0}\nPity  {snapshot.PityGauge:N0}";
            _playNextGameButton.interactable = canPlayNextGame && snapshot.IsRosterValid;
            _feedbackText.text = snapshot.IsRosterValid
                ? "실제 Runtime 상태와 Save를 사용합니다."
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
            if (_playNextGameButton != null) _playNextGameButton.onClick.RemoveAllListeners();
            if (_saveButton != null) _saveButton.onClick.RemoveAllListeners();
            if (_titleButton != null) _titleButton.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerHomeWorkspace", true);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "DashboardColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns, CareerUiTheme.Space4);

            OwnerWorkspaceUiFactory.Panel nextMatch = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "NextMatchPanel", "다음 경기", true);
            OwnerWorkspaceUiFactory.SetFlexible(nextMatch.Root, 1.35f);
            _nextMatchText = CreateValue(nextMatch.Content, "NextMatchValue", 27, FontStyle.Bold);

            OwnerWorkspaceUiFactory.Panel roster = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "RosterPanel", "1군 현황");
            OwnerWorkspaceUiFactory.SetFlexible(roster.Root, 1f);
            _rosterText = CreateValue(roster.Content, "RosterValue", 20, FontStyle.Bold);

            OwnerWorkspaceUiFactory.Panel resources = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "ResourcePanel", "구단 자원");
            OwnerWorkspaceUiFactory.SetFlexible(resources.Root, 1f);
            _resourceText = CreateValue(resources.Content, "ResourceValue", 20, FontStyle.Bold);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerHomeActionBar", false);
            HorizontalLayoutGroup actions = OwnerWorkspaceUiFactory.AddHorizontalLayout(
                _actionRoot, CareerUiTheme.Space3);
            actions.padding = new RectOffset(16, 16, 8, 8);
            _feedbackText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "Feedback", string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_feedbackText.rectTransform, 1f, 0f);
            _saveButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "SaveButton", "저장", () => SaveRequested?.Invoke());
            _playNextGameButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "PlayNextGameButton", "다음 경기 진행", () => PlayNextGameRequested?.Invoke());
            _titleButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "TitleButton", "모드 선택", () => TitleRequested?.Invoke());

            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private static Text CreateValue(Transform parent, string name, int fontSize, FontStyle style)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, string.Empty, fontSize, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerWorkspaceUiFactory.Stretch(text.rectTransform);
            return text;
        }

        private void EnsureBuilt()
        {
            if (_workspaceRoot == null || _actionRoot == null)
                throw new InvalidOperationException("CreateRuntime으로 Owner Home을 먼저 생성해야 합니다.");
        }
    }
}

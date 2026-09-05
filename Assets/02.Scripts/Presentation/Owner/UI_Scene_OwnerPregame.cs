using System;
using System.Text;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>SharedGameShell 슬롯 안에서 상대 분석과 경기 계획을 표시하는 구단주 경기 준비 View다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerPregame : MonoBehaviour
    {
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private Text _contentStateText;
        private Text _matchText;
        private Text _intelText;
        private Text _probableStarterText;
        private Text _recentFormText;
        private Text _managerTendencyText;
        private Text _expectedLineupText;
        private Text _bullpenText;
        private Text _threatText;
        private Text _presetText;
        private Text _readinessText;
        private Text _loadoutText;
        private Text _startStateText;
        private Button _previousPresetButton;
        private Button _nextPresetButton;
        private Button _startButton;
        private OwnerPregamePresentationModel _model;
        private int _presetIndex;

        public event Action<string> PresetSelected;
        public event Action MatchStartRequested;

        public static UI_Scene_OwnerPregame CreateRuntime(
            RectTransform workspaceHost,
            RectTransform inspectorHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (inspectorHost == null) throw new ArgumentNullException(nameof(inspectorHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));
            var owner = new GameObject(nameof(UI_Scene_OwnerPregame)).AddComponent<UI_Scene_OwnerPregame>();
            owner.Build(workspaceHost, inspectorHost, actionBarHost);
            return owner;
        }

        public void Bind(OwnerPregamePresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            EnsureBuilt();
            bool ready = model.Snapshot.ContentState.Kind == UiContentStateKind.Ready;
            _contentStateText.gameObject.SetActive(!ready);
            _contentStateText.text = ready
                ? string.Empty
                : $"{model.Snapshot.ContentState.Title}\n{model.Snapshot.ContentState.Message}";

            _matchText.text = string.IsNullOrWhiteSpace(model.Snapshot.NextMatchText)
                ? $"상대 {model.Snapshot.OpponentName}"
                : $"{model.Snapshot.NextMatchText} · {model.Snapshot.OpponentName}";
            _intelText.text = $"Intel Confidence  {model.IntelText}";
            _probableStarterText.text = $"예상 선발  {model.ProbableStarterText}";
            _recentFormText.text = $"최근 성적  {model.RecentFormText}";
            _managerTendencyText.text = $"감독 성향  {model.ManagerTendencyText}";
            _expectedLineupText.text = JoinRows(model.ExpectedLineup);
            _bullpenText.text = JoinRows(model.Bullpen);
            _threatText.text = JoinRows(model.KeyThreats);

            _presetIndex = FindSelectedPreset(model);
            RenderPreset();
            _readinessText.text = BuildReadinessText(model);
            _loadoutText.text = BuildLoadoutText(model);
            _startButton.interactable = ready && model.CanStartMatch;
            _startStateText.text = model.CanStartMatch ? "경기 시작 준비 완료" : model.MatchStartDisabledReason;
            _startStateText.color = model.CanStartMatch ? CareerUiTheme.Success : CareerUiTheme.Warning;
            _previousPresetButton.interactable = ready && model.Presets.Count > 1;
            _nextPresetButton.interactable = ready && model.Presets.Count > 1;
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>경기 준비 Command 실패를 현재 Action Bar에 즉시 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            EnsureBuilt();
            _startStateText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _startStateText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        private void OnDestroy()
        {
            if (_previousPresetButton != null) _previousPresetButton.onClick.RemoveAllListeners();
            if (_nextPresetButton != null) _nextPresetButton.onClick.RemoveAllListeners();
            if (_startButton != null) _startButton.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerPregameWorkspace", true);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "WorkspaceColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns);

            OwnerWorkspaceUiFactory.Panel intelligence = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "OpponentAnalysisPanel", "상대 분석", true);
            OwnerWorkspaceUiFactory.SetFlexible(intelligence.Root, 1.05f);
            OwnerWorkspaceUiFactory.AddVerticalLayout(intelligence.Content, CareerUiTheme.Space2);
            _matchText = AddLine(intelligence.Content, 20, FontStyle.Bold, 34f);
            _intelText = AddLine(intelligence.Content, 16, FontStyle.Bold, 28f);
            _probableStarterText = AddLine(intelligence.Content, 16, FontStyle.Normal, 28f);
            _recentFormText = AddLine(intelligence.Content, 16, FontStyle.Normal, 28f);
            _managerTendencyText = AddLine(intelligence.Content, 16, FontStyle.Normal, 44f);
            AddSectionTitle(intelligence.Content, "Key Threat");
            _threatText = AddLine(intelligence.Content, 15, FontStyle.Normal, 80f);

            OwnerWorkspaceUiFactory.Panel projections = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "ExpectedLineupPanel", "예상 타선 · 불펜");
            OwnerWorkspaceUiFactory.SetFlexible(projections.Root, 0.95f);
            OwnerWorkspaceUiFactory.AddVerticalLayout(projections.Content, CareerUiTheme.Space2);
            AddSectionTitle(projections.Content, "예상 Lineup");
            _expectedLineupText = AddLine(projections.Content, 14, FontStyle.Normal, 220f);
            AddSectionTitle(projections.Content, "Bullpen 상태");
            _bullpenText = AddLine(projections.Content, 14, FontStyle.Normal, 150f);

            _contentStateText = OwnerWorkspaceUiFactory.CreateText(
                _workspaceRoot, "ContentState", string.Empty, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.Stretch(_contentStateText.rectTransform);
            _contentStateText.gameObject.SetActive(false);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerPregameInspector", false);
            OwnerWorkspaceUiFactory.Panel plan = OwnerWorkspaceUiFactory.CreatePanel(
                _inspectorRoot, "MatchPlanPanel", "경기 계획");
            OwnerWorkspaceUiFactory.Stretch(plan.Root);
            OwnerWorkspaceUiFactory.AddVerticalLayout(plan.Content, CareerUiTheme.Space2);
            _presetText = AddLine(plan.Content, 18, FontStyle.Bold, 36f);
            AddSectionTitle(plan.Content, "Condition · Chemistry · Position Warning");
            _readinessText = AddLine(plan.Content, 13, FontStyle.Normal, 330f);
            AddSectionTitle(plan.Content, "TeamColor · Tactic 2장");
            _loadoutText = AddLine(plan.Content, 14, FontStyle.Normal, 90f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerPregameActionBar", false);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 8, 8);
            _previousPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "PreviousPresetButton", "이전 프리셋", () => SelectRelativePreset(-1));
            _nextPresetButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "NextPresetButton", "다음 프리셋", () => SelectRelativePreset(1));
            _startStateText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "StartState", string.Empty, 14, FontStyle.Normal, TextAnchor.MiddleRight,
                CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_startStateText.rectTransform, 1f, 0f);
            _startButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "StartMatchButton", "경기 시작", HandleMatchStart);
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private void SelectRelativePreset(int delta)
        {
            if (_model == null || _model.Presets.Count < 2) return;
            _presetIndex = (_presetIndex + delta + _model.Presets.Count) % _model.Presets.Count;
            RenderPreset();
            PresetSelected?.Invoke(_model.Presets[_presetIndex].PresetId);
        }

        private void RenderPreset()
        {
            if (_model == null || _model.Presets.Count == 0)
            {
                _presetText.text = "선택 가능한 프리셋 없음";
                return;
            }
            OwnerPregamePresetModel preset = _model.Presets[_presetIndex];
            _presetText.text = $"{preset.DisplayName} · {preset.StatusText}";
        }

        private void HandleMatchStart()
        {
            if (_model != null && _model.CanStartMatch) MatchStartRequested?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_workspaceRoot == null) throw new InvalidOperationException("CreateRuntime으로 View를 생성해야 합니다.");
        }

        private static int FindSelectedPreset(OwnerPregamePresentationModel model)
        {
            for (int index = 0; index < model.Presets.Count; index++)
                if (model.Presets[index].IsSelected) return index;
            return 0;
        }

        private static string BuildReadinessText(OwnerPregamePresentationModel model)
        {
            if (model.Lineup.Count == 0) return "정보 부족";
            var builder = new StringBuilder(model.Lineup.Count * 64);
            for (int index = 0; index < model.Lineup.Count; index++)
            {
                OwnerPregamePlayerModel player = model.Lineup[index];
                if (index > 0) builder.AppendLine();
                builder.Append(index + 1).Append(". ").Append(player.DisplayName).Append(" · ")
                    .Append(player.PositionText).Append(" | 기본 ").Append(player.BaseConditionText)
                    .Append(" | 타선 ").Append(player.LineupChemistryText);
                if (!string.IsNullOrEmpty(player.BatteryChemistryText))
                    builder.Append(" | 배터리 ").Append(player.BatteryChemistryText);
                builder.Append(" | 예상 ").Append(player.ExpectedConditionText);
                if (!string.IsNullOrEmpty(player.WarningText)) builder.Append(" | 경고: ").Append(player.WarningText);
            }
            return builder.ToString();
        }

        private static string BuildLoadoutText(OwnerPregamePresentationModel model)
        {
            string colors = string.Join(" / ", model.Snapshot.TeamColors);
            string tactics = model.Snapshot.Tactics.Count == 0 ? "선택 없음" : string.Join(" / ", model.Snapshot.Tactics);
            return $"TeamColor  {colors}\nTactic  {tactics}";
        }

        private static string JoinRows(System.Collections.Generic.IReadOnlyList<string> rows)
        {
            return rows.Count == 0 ? "정보 부족" : string.Join("\n", rows);
        }

        private static Text AddLine(Transform parent, int size, FontStyle style, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            return text;
        }

        private static void AddSectionTitle(Transform parent, string title)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "SectionTitle", title, 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.AccentGold);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        }
    }
}

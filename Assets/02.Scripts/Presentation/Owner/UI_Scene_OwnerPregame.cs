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
    public sealed partial class UI_Scene_OwnerPregame : MonoBehaviour
    {
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private Text _contentStateText;

        private Text _presetText;

        private Text _readinessSummaryText;
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

            RenderAnalysisBoard();

            _presetIndex = FindSelectedPreset(model);
            RenderPreset();

            _readinessSummaryText.text = BuildReadinessSummary(model);
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
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerPregameWorkspace", false);
            BuildAnalysisBoard();

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
            AddSectionTitle(plan.Content, "선발 준비 요약");
            _readinessSummaryText = AddLine(plan.Content, 14, FontStyle.Normal, 76f);
            AddSectionTitle(plan.Content, "팀컬러 · 전술카드 2장");
            _loadoutText = AddLine(plan.Content, 14, FontStyle.Normal, 110f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerPregameActionBar", false);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 4, 4);
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

        private static string BuildReadinessSummary(OwnerPregamePresentationModel model)
        {
            int warningCount = 0;
            int batteryCount = 0;
            for (int index = 0; index < model.Lineup.Count; index++)
            {
                OwnerPregamePlayerModel player = model.Lineup[index];
                if (!string.IsNullOrEmpty(player.WarningText)) warningCount++;
                if (!string.IsNullOrEmpty(player.BatteryChemistryText)) batteryCount++;
            }

            string warning = warningCount == 0 ? "구성 경고 없음" : $"구성 경고 {warningCount}건";
            return $"선발 {model.Lineup.Count}명 · 배터리 궁합 {batteryCount}명\n{warning}\n야수 탭에서 선수별 상태 확인";
        }

        private static string BuildLoadoutText(OwnerPregamePresentationModel model)
        {
            string colors = string.Join(" / ", model.Snapshot.TeamColors);
            string tactics = model.Snapshot.Tactics.Count == 0 ? "선택 없음" : string.Join(" / ", model.Snapshot.Tactics);
            return $"팀컬러  {colors}\n전술카드  {tactics}";
        }

        private static Text AddLine(
            Transform parent,
            int size,
            FontStyle style,
            float height,
            float flexibleHeight = 0f)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = flexibleHeight;
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

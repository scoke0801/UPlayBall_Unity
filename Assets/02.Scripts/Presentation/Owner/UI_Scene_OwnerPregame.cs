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
        private Text _presetStatusText;

        private Text _lineupCountText;
        private Text _batteryText;
        private Text _warningText;
        private Text _teamColorText;
        private Text _tacticText;
        private Text _startStateText;
        private Button _startButton;
        private OwnerPregamePresentationModel _model;

        public event Action MatchStartRequested;

        /// <summary>리포트 본문을 실제로 표시한 경우에만 안내 도착 대상으로 제공한다.</summary>
        public RectTransform GuideAnalysisTarget => _workspaceRoot != null && _workspaceRoot.gameObject.activeInHierarchy &&
            _model != null && _model.Snapshot.ContentState.Kind == UiContentStateKind.Ready ? _workspaceRoot : null;
        public RectTransform GuideStartTarget => _startButton != null && _startButton.gameObject.activeInHierarchy &&
            _startButton.interactable ? (RectTransform)_startButton.transform : null;

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

            RenderPreset();

            RenderReadiness(model);
            _teamColorText.text = BuildLoadoutText(model.Snapshot.TeamColors, "적용된 팀컬러 없음");
            _tacticText.text = BuildLoadoutText(model.Snapshot.Tactics, "선택한 전술카드 없음");
            _startButton.interactable = ready && model.CanStartMatch;
            _startStateText.text = model.CanStartMatch ? "경기 시작 준비 완료" : model.MatchStartDisabledReason;
            _startStateText.color = model.CanStartMatch ? CareerUiTheme.Success : CareerUiTheme.Warning;
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
            BuildMatchPlan();

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerPregameActionBar", false);
            OwnerDashboardStyle.ApplyActionBar(_actionRoot);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 4, 4);
            actionLayout.childForceExpandWidth = false;
            actionLayout.childAlignment = TextAnchor.MiddleLeft;
            _startStateText = OwnerWorkspaceUiFactory.CreateText(
                _actionRoot, "StartState", string.Empty, 14, FontStyle.Normal, TextAnchor.MiddleRight,
                CareerUiTheme.TextSecondary);
            OwnerDashboardStyle.SetDataText(_startStateText);
            LayoutElement startStateLayout = _startStateText.gameObject.AddComponent<LayoutElement>();
            startStateLayout.minHeight = 42f;
            startStateLayout.preferredHeight = 42f;
            startStateLayout.flexibleWidth = 1f;
            _startButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "StartMatchButton", "경기 시작", HandleMatchStart);
            LayoutElement startButtonLayout = _startButton.GetComponent<LayoutElement>();
            startButtonLayout.minWidth = 260f;
            startButtonLayout.preferredWidth = 260f;
            startButtonLayout.flexibleWidth = 0f;

            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
            OwnerUiButtonSkin.Apply(_startButton, OwnerButtonRole.Primary);
        }

        private void RenderPreset()
        {
            OwnerPregamePresetModel preset = FindSelectedPreset(_model);
            if (preset == null)
            {
                _presetText.text = "선택 가능한 라인업 없음";
                _presetStatusText.text = "선수단에서 라인업을 먼저 구성해 주세요";
                _presetStatusText.color = CareerUiTheme.Error;
                return;
            }
            _presetText.text = preset.DisplayName;
            _presetStatusText.text = preset.StatusText;
            _presetStatusText.color = preset.StatusText == "사용 가능"
                ? new Color32(145, 220, 181, 255)
                : new Color32(255, 166, 139, 255);
        }

        private void HandleMatchStart()
        {
            if (_model != null && _model.CanStartMatch) MatchStartRequested?.Invoke();
        }

        private void EnsureBuilt()
        {
            if (_workspaceRoot == null) throw new InvalidOperationException("CreateRuntime으로 View를 생성해야 합니다.");
        }

        private static OwnerPregamePresetModel FindSelectedPreset(OwnerPregamePresentationModel model)
        {
            if (model == null) return null;
            for (int index = 0; index < model.Presets.Count; index++)
                if (model.Presets[index].IsSelected) return model.Presets[index];
            return model.Presets.Count > 0 ? model.Presets[0] : null;
        }

        private void RenderReadiness(OwnerPregamePresentationModel model)
        {
            int warningCount = 0;
            int batteryCount = 0;
            for (int index = 0; index < model.Lineup.Count; index++)
            {
                OwnerPregamePlayerModel player = model.Lineup[index];
                if (!string.IsNullOrEmpty(player.WarningText)) warningCount++;
                if (!string.IsNullOrEmpty(player.BatteryChemistryText) &&
                    !string.Equals(player.BatteryChemistryText, "해당 없음", StringComparison.Ordinal))
                    batteryCount++;
            }

            string warning = warningCount == 0 ? "경고 없음" : $"경고 {warningCount}건";
            string battery = batteryCount == 0 ? "확인 항목 없음" : $"{batteryCount}명 확인";
            _lineupCountText.text = $"{model.Lineup.Count}명";
            _batteryText.text = battery;
            _warningText.text = warning;
            _warningText.color = warningCount == 0 ? new Color32(145, 220, 181, 255) : new Color32(255, 190, 125, 255);
        }

        private static string BuildLoadoutText(System.Collections.Generic.IReadOnlyList<string> items, string emptyText)
        {
            if (items.Count == 0) return emptyText;
            var builder = new StringBuilder();
            for (int index = 0; index < items.Count; index++)
            {
                if (index > 0) builder.AppendLine().AppendLine();
                builder.Append(index + 1).Append(".  ").Append(items[index]);
            }
            return builder.ToString().TrimEnd();
        }

        private static RectTransform CreateInformationBlock(Transform parent, string name, float height)
        {
            RectTransform block = UIClubOfficeStyle.Surface(name, parent, new Color32(31, 48, 65, 255)).rectTransform;
            OwnerDashboardStyle.ApplyInset(block.GetComponent<Image>());
            LayoutElement blockLayout = block.gameObject.AddComponent<LayoutElement>();
            blockLayout.minHeight = height;
            blockLayout.preferredHeight = height;

            VerticalLayoutGroup layout = OwnerWorkspaceUiFactory.AddVerticalLayout(block, CareerUiTheme.Space1);
            layout.padding = new RectOffset(14, 14, 10, 10);
            return block;
        }

        private static Text AddLine(
            Transform parent,
            int size,
            FontStyle style,
            float height,
            float flexibleHeight = 0f)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, Color.white);
            OwnerDashboardStyle.SetDataText(text, style == FontStyle.Bold);
            text.color = new Color32(236, 239, 240, 255);
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = flexibleHeight;
            return text;
        }

        private static void AddSectionTitle(Transform parent, string title)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "SectionTitle", title, 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white);
            OwnerDashboardStyle.SetDataText(text, true);
            text.color = new Color32(220, 189, 125, 255);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
        }

        /// <summary>기존 구단 이미지를 활용해 경기 전 확인 사항을 세로 계획 보드로 구성한다.</summary>
        private void BuildMatchPlan()
        {
            Image frame = UIClubOfficeStyle.Surface("MatchPlanPanel", _inspectorRoot, new Color32(15, 29, 43, 255));
            UIOwnerFrontOfficePanel.Apply(frame.rectTransform, "ManagerReport");
            OwnerWorkspaceUiFactory.Stretch(frame.rectTransform);
            RectTransform viewport = OwnerRuntimeUiFactory.CreateRect("PlanViewport", frame.transform);
            OwnerRuntimeUiFactory.Stretch(viewport, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = frame.gameObject.AddComponent<ScrollRect>();
            // 작은 화면에서도 마지막 전술 항목까지 읽을 수 있게 내용 높이를 유지한다.
            frame.raycastTarget = true;
            RectTransform content = OwnerRuntimeUiFactory.CreateRect("PlanItems", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.sizeDelta = Vector2.zero;
            OwnerWorkspaceUiFactory.AddVerticalLayout(content, 12f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            Image hero = UIClubOfficeStyle.Illustration(content, "PregameOfficeArtwork", 7);
            hero.gameObject.AddComponent<LayoutElement>().preferredHeight = 146f;
            Image shade = UIClubOfficeStyle.Surface("TitleShade", hero.transform, new Color32(9, 23, 38, 220));
            UIClubOfficeStyle.Place(shade.rectTransform, 0f, 0f, 1f, .47f);
            Text title = UIClubOfficeStyle.Label("PlanTitle", shade.transform, "경기 계획", 23, true);
            OwnerDashboardStyle.SetDataText(title, true);
            OwnerRuntimeUiFactory.Stretch(title.rectTransform, new Vector2(14f, 6f), new Vector2(-12f, -6f));
            Image rule = UIClubOfficeStyle.Surface("GoldRule", hero.transform, new Color32(198, 164, 101, 255));
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.offsetMin = Vector2.zero;
            rule.rectTransform.offsetMax = new Vector2(0f, 3f);

            RectTransform preset = CreateInformationBlock(content, "SelectedLineupBlock", 118f);
            AddSectionTitle(preset, "선택 라인업");
            _presetText = AddLine(preset, 20, FontStyle.Bold, 30f);
            _presetStatusText = AddLine(preset, 13, FontStyle.Normal, 32f);

            RectTransform readiness = CreateInformationBlock(content, "ReadinessBlock", 170f);
            AddSectionTitle(readiness, "선발 점검");
            _lineupCountText = AddReadinessRow(readiness, "출전 명단");
            _batteryText = AddReadinessRow(readiness, "배터리 궁합");
            _warningText = AddReadinessRow(readiness, "구성 상태");
            Text hint = AddLine(readiness, 12, FontStyle.Normal, 32f);
            hint.text = "야수·투수 탭에서 세부 상태 확인";
            hint.color = new Color32(168, 185, 199, 255);

            _teamColorText = AddLoadoutCard(content, "TeamColorBlock", "팀컬러", 5);
            _tacticText = AddLoadoutCard(content, "TacticsBlock", "전술카드", 4);
        }

        private static Text AddReadinessRow(Transform parent, string label)
        {
            RectTransform row = OwnerRuntimeUiFactory.CreateRect("ReadinessRow", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            Text key = UIClubOfficeStyle.Label("Label", row, label, 13);
            key.color = new Color32(168, 185, 199, 255);
            UIClubOfficeStyle.Place(key.rectTransform, 0f, 0f, .4f, 1f);
            Text value = UIClubOfficeStyle.Label("Value", row, string.Empty, 14, true);
            value.color = Color.white;
            OwnerDashboardStyle.SetDataText(key);
            OwnerDashboardStyle.SetDataText(value, true);
            value.alignment = TextAnchor.MiddleRight;
            UIClubOfficeStyle.Place(value.rectTransform, .4f, 0f, 1f, 1f);
            return value;
        }

        private static Text AddLoadoutCard(Transform parent, string name, string title, int artwork)
        {
            RectTransform card = CreateInformationBlock(parent, name, 138f);
            RectTransform header = OwnerRuntimeUiFactory.CreateRect("LoadoutHeader", card);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
            Image icon = UIClubOfficeStyle.Illustration(header, "LoadoutArtwork", artwork);
            icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.offsetMin = Vector2.zero;
            icon.rectTransform.offsetMax = new Vector2(58f, 0f);
            Text heading = UIClubOfficeStyle.Label("Title", header, title, 16, true);
            OwnerDashboardStyle.SetDataText(heading, true);
            heading.color = OwnerDashboardStyle.Gold;
            OwnerRuntimeUiFactory.Stretch(heading.rectTransform, new Vector2(70f, 0f), Vector2.zero);
            Text items = AddLine(card, 14, FontStyle.Normal, 70f);
            // 긴 팀컬러 이름과 최대 장착 슬롯도 잘리지 않도록 실제 텍스트 높이를 사용한다.
            items.GetComponent<LayoutElement>().preferredHeight = -1f;
            items.GetComponent<LayoutElement>().minHeight = 70f;
            card.GetComponent<LayoutElement>().preferredHeight = -1f;
            return items;
        }
    }
}

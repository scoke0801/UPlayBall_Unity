using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>선수별 Base/Assignment/Chemistry/Effective Condition 근거를 표로 보여주는 uGUI 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerConditionChemistry : MonoBehaviour
    {
        private Text _summaryText;
        private RectTransform _playerContent;
        private RectTransform _plotContent;
        private Text _detailTitle;
        private Text _detailText;
        private OwnerConditionChemistryPresentationModel _model;
        private string _selectedPlayerId = string.Empty;
        private bool _isBuilt;
        private readonly List<Button> _rowButtons = new List<Button>();

        public event Action<string> PlayerSelected;
        public event Action LineupRequested;

        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

        /// <summary>공용 Workspace 슬롯 아래에 Condition 화면을 생성한다.</summary>
        public static UI_Scene_OwnerConditionChemistry CreateRuntime(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            RectTransform rect = OwnerRuntimeUiFactory.CreateRect("UI_Scene_OwnerConditionChemistry", parent);
            OwnerRuntimeUiFactory.Stretch(rect);
            return rect.gameObject.AddComponent<UI_Scene_OwnerConditionChemistry>();
        }

        /// <summary>Simulation 결과에서 조립된 불변 행만 표시한다.</summary>
        public void Bind(OwnerConditionChemistryPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();
            _model = model;
            _summaryText.text = model.SummaryText;
            RenderPlayers(model.Players);
            if (model.Players.Count == 0)
            {
                _selectedPlayerId = string.Empty;
                RenderSelection(null);
                return;
            }
            OwnerConditionPlayerPresentationRow selected = FindPlayer(_selectedPlayerId) ?? model.Players[0];
            _selectedPlayerId = selected.Snapshot.PlayerPersonId;
            RenderSelection(selected);
        }

        /// <summary>다음 경기 Context가 없는 정상 Empty 상태를 같은 Content 영역에 표시한다.</summary>
        public void ShowUnavailable(string message)
        {
            EnsureHierarchy();
            _model = null;
            _selectedPlayerId = string.Empty;
            _summaryText.text = string.IsNullOrWhiteSpace(message)
                ? "다음 경기 일정이 없어 컨디션·궁합을 계산하지 않습니다."
                : message;
            RenderPlayers(Array.Empty<OwnerConditionPlayerPresentationRow>());
            RenderSelection(null);
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void EnsureHierarchy()
        {
            if (_isBuilt) return;
            _isBuilt = true;
            RectTransform root = GetComponent<RectTransform>();
            OwnerRuntimeUiFactory.Stretch(root);
            Image background = OwnerRuntimeUiFactory.CreateImage("Background", root, CareerUiTheme.Background);
            OwnerRuntimeUiFactory.Stretch(background.rectTransform);
            OwnerWorkspaceUiFactory.Panel panel = OwnerRuntimeUiFactory.CreatePanel(
                "ConditionPanel", root, "선수 목록", true);
            OwnerRuntimeUiFactory.SetAnchors(panel.Root, Vector2.zero, new Vector2(0.34f, 1f),
                new Vector2(12f, 12f), new Vector2(-4f, -12f));

            _summaryText = OwnerRuntimeUiFactory.CreateText(
                "Summary", panel.Content, string.Empty, 14, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(_summaryText.rectTransform, new Vector2(0f, 0.93f), Vector2.one,
                Vector2.zero, Vector2.zero);
            BuildTableHeader(panel.Content);
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "PlayerConditionList", panel.Content, out _playerContent);
            OwnerRuntimeUiFactory.SetAnchors(scroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.855f),
                Vector2.zero, new Vector2(0f, -4f));

            OwnerWorkspaceUiFactory.Panel plot = OwnerRuntimeUiFactory.CreatePanel(
                "ConditionPlotPanel", root, "10단계 컨디션", true);
            OwnerRuntimeUiFactory.SetAnchors(plot.Root, new Vector2(0.34f, 0f), new Vector2(0.66f, 1f),
                new Vector2(4f, 12f), new Vector2(-4f, -12f));
            _plotContent = plot.Content;

            OwnerWorkspaceUiFactory.Panel detail = OwnerRuntimeUiFactory.CreatePanel(
                "ConditionDetailPanel", root, "궁합 원인 · 경기 영향", true);
            OwnerRuntimeUiFactory.SetAnchors(detail.Root, new Vector2(0.66f, 0f), Vector2.one,
                new Vector2(4f, 12f), new Vector2(-12f, -12f));
            _detailTitle = OwnerRuntimeUiFactory.CreateText(
                "SelectedPlayer", detail.Content, "선수를 선택하세요.", 18, FontStyle.Bold,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.SetAnchors(_detailTitle.rectTransform, new Vector2(0f, 0.86f), Vector2.one,
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            _detailText = OwnerRuntimeUiFactory.CreateText(
                "Reasons", detail.Content, string.Empty, 15, FontStyle.Normal,
                TextAnchor.UpperLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(_detailText.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.85f),
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Button lineup = OwnerWorkspaceUiFactory.CreateButton(
                detail.Content, "OpenLineup", "라인업에서 배치 확인", () => LineupRequested?.Invoke());
            OwnerRuntimeUiFactory.SetAnchors(lineup.GetComponent<RectTransform>(), Vector2.zero,
                new Vector2(1f, 0.12f), new Vector2(8f, 8f), new Vector2(-8f, -4f));
            CareerUiSkin.Apply(root);
        }

        private void BuildTableHeader(Transform parent)
        {
            Image header = OwnerRuntimeUiFactory.CreateImage("TableHeader", parent, OwnerDashboardStyle.TableHeader);
            header.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            OwnerRuntimeUiFactory.SetAnchors(header.rectTransform, new Vector2(0f, 0.855f), new Vector2(1f, 0.925f),
                Vector2.zero, Vector2.zero);
            CreateColumnText(header.transform, "Player", "선수", 0f, 0.48f, TextAnchor.MiddleLeft);
            CreateColumnText(header.transform, "Position", "포지션", 0.48f, 0.68f);
            CreateColumnText(header.transform, "Expected", "최종", 0.68f, 1f);
        }

        private void RenderPlayers(IReadOnlyList<OwnerConditionPlayerPresentationRow> players)
        {
            OwnerRuntimeUiFactory.ClearChildren(_playerContent);
            _rowButtons.Clear();
            for (int index = 0; index < players.Count; index++)
                CreatePlayerRow(players[index], index);
        }

        private void CreatePlayerRow(OwnerConditionPlayerPresentationRow row, int index)
        {
            Color surfaceColor = index % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate;
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                string.Concat("Player_", row.Snapshot.PlayerPersonId),
                _playerContent,
                surfaceColor);
            surface.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var layout = surface.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 62f;
            layout.minHeight = 56f;
            surface.raycastTarget = true;
            Button button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            OwnerDashboardStyle.SetDataRow(button, row.Snapshot.PlayerPersonId == _selectedPlayerId, surfaceColor);
            _rowButtons.Add(button);
            string playerId = row.Snapshot.PlayerPersonId;
            button.onClick.AddListener(() => SelectPlayer(playerId));

            Text player = CreateColumnText(
                surface.transform, "Name",
                string.Concat(row.Snapshot.PlayerName, "\n", row.AvailabilityText),
                0f, 0.48f, TextAnchor.MiddleLeft);
            player.fontStyle = FontStyle.Bold;
            player.color = GetAvailabilityColor(row.Snapshot.Availability);
            CreateColumnText(surface.transform, "Position", row.Snapshot.PositionText, 0.48f, 0.68f);
            Text expected = CreateColumnText(
                surface.transform, "ExpectedCondition", row.EffectiveConditionText, 0.68f, 1f);
            expected.fontStyle = FontStyle.Bold;
            expected.color = GetConditionColor(row.EffectiveLevel);
        }

        private void SelectPlayer(string playerId)
        {
            OwnerConditionPlayerPresentationRow row = FindPlayer(playerId);
            if (row == null) return;
            _selectedPlayerId = playerId;
            RenderSelection(row);
            PlayerSelected?.Invoke(playerId);
        }

        private OwnerConditionPlayerPresentationRow FindPlayer(string playerId)
        {
            if (_model == null || string.IsNullOrEmpty(playerId)) return null;
            for (int index = 0; index < _model.Players.Count; index++)
                if (string.Equals(_model.Players[index].Snapshot.PlayerPersonId, playerId, StringComparison.Ordinal))
                    return _model.Players[index];
            return null;
        }

        private void RenderSelection(OwnerConditionPlayerPresentationRow row)
        {
            for (int index = 0; index < _rowButtons.Count; index++)
                OwnerDashboardStyle.SetDataRow(_rowButtons[index], row != null && _model != null &&
                    _model.Players[index].Snapshot.PlayerPersonId == row.Snapshot.PlayerPersonId,
                    index % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
            OwnerRuntimeUiFactory.ClearChildren(_plotContent);
            if (row == null)
            {
                _detailTitle.text = "선수를 선택하세요.";
                _detailText.text = "다음 경기에 적용될 컨디션과 궁합 근거가 이곳에 표시됩니다.";
                var empty = OwnerRuntimeUiFactory.CreateText("EmptyCondition", _plotContent,
                    "표시할 컨디션이 없습니다.\n다음 경기와 등록 선수를 확인하세요.", 16, FontStyle.Normal,
                    TextAnchor.MiddleCenter, OwnerDashboardStyle.Muted);
                OwnerRuntimeUiFactory.Stretch(empty.rectTransform, Vector2.one * 16, -Vector2.one * 16);
                return;
            }

            _detailTitle.text = row.Snapshot.PlayerName + " · " + row.Snapshot.PositionText;
            _detailText.text =
                $"상태  {row.AvailabilityText}\n\n" +
                $"기본  {row.BaseConditionText}\n" +
                $"최종  {row.EffectiveConditionText}\n\n" +
                row.ReasonText + "\n\n" + row.ImpactText;
            for (int level = 1; level <= 10; level++)
            {
                float bottom = 0.06f + (level - 1) * 0.087f;
                float top = bottom + 0.072f;
                bool isBase = level == row.BaseLevel;
                bool isEffective = level == row.EffectiveLevel;
                Color color = isEffective
                    ? GetConditionColor(level)
                    : isBase ? CareerUiTheme.ReferenceAccent : CareerUiTheme.SurfaceSubtle;
                Image step = OwnerRuntimeUiFactory.CreateImage("Level" + level, _plotContent, color);
                OwnerRuntimeUiFactory.SetAnchors(step.rectTransform,
                    new Vector2(0.08f, bottom), new Vector2(0.92f, top), Vector2.zero, Vector2.zero);
                string marker = isBase && isEffective ? "기본 = 최종" : isEffective ? "최종" : isBase ? "기본" : string.Empty;
                Text label = OwnerRuntimeUiFactory.CreateText(
                "Label", step.transform, $"{level}단계  {marker}", 13, FontStyle.Bold,
                    TextAnchor.MiddleLeft, isEffective ? Color.white : CareerUiTheme.TextSecondary);
                OwnerRuntimeUiFactory.Stretch(label.rectTransform, new Vector2(10f, 0f), new Vector2(-8f, 0f));
            }
        }

        private static Text CreateColumnText(
            Transform parent,
            string name,
            string value,
            float anchorMinX,
            float anchorMaxX,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(
                name, parent, value, 14, FontStyle.Normal, alignment, CareerUiTheme.TextSecondary);
            OwnerDashboardStyle.SetDataText(text);
            OwnerRuntimeUiFactory.SetAnchors(
                text.rectTransform,
                new Vector2(anchorMinX, 0f),
                new Vector2(anchorMaxX, 1f),
                new Vector2(anchorMinX == 0f ? 12f : 4f, 3f),
                new Vector2(anchorMaxX == 1f ? -12f : -4f, -3f));
            return text;
        }

        private static Color GetAvailabilityColor(PlayerAvailabilityStatus availability)
        {
            switch (availability)
            {
                case PlayerAvailabilityStatus.Available: return CareerUiTheme.TextPrimary;
                case PlayerAvailabilityStatus.DayToDay: return CareerUiTheme.Warning;
                case PlayerAvailabilityStatus.Unavailable: return CareerUiTheme.Error;
                default: return CareerUiTheme.TextMuted;
            }
        }

        private static Color GetModifierColor(int value)
        {
            if (value > 0) return CareerUiTheme.Success;
            if (value < 0) return CareerUiTheme.Warning;
            return CareerUiTheme.TextSecondary;
        }

        private static Color GetConditionColor(int level)
        {
            if (level >= 8) return CareerUiTheme.Success;
            if (level <= 3) return CareerUiTheme.Error;
            if (level <= 5) return CareerUiTheme.Warning;
            return CareerUiTheme.TextPrimary;
        }
    }
}

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
        private bool _isBuilt;

        public event Action<string> PlayerSelected;

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
            _summaryText.text = model.SummaryText;
            RenderPlayers(model.Players);
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
                "ConditionPanel", root, "컨디션 · 타선 · 배터리 궁합", true);
            OwnerRuntimeUiFactory.Stretch(panel.Root, new Vector2(12f, 12f), new Vector2(-12f, -12f));

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
        }

        private void BuildTableHeader(Transform parent)
        {
            Image header = OwnerRuntimeUiFactory.CreateImage("TableHeader", parent, CareerUiTheme.RoleBand);
            header.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            OwnerRuntimeUiFactory.SetAnchors(header.rectTransform, new Vector2(0f, 0.855f), new Vector2(1f, 0.925f),
                Vector2.zero, Vector2.zero);
            CreateColumnText(header.transform, "Player", "선수", 0f, 0.18f, TextAnchor.MiddleLeft);
            CreateColumnText(header.transform, "Position", "포지션", 0.18f, 0.27f);
            CreateColumnText(header.transform, "Base", "기본 컨디션", 0.27f, 0.43f);
            CreateColumnText(header.transform, "Assignment", "비주포지션", 0.43f, 0.53f);
            CreateColumnText(header.transform, "Lineup", "타선 궁합", 0.53f, 0.63f);
            CreateColumnText(header.transform, "Battery", "배터리 궁합", 0.63f, 0.74f);
            CreateColumnText(header.transform, "Expected", "경기 적용 컨디션", 0.74f, 1f);
        }

        private void RenderPlayers(IReadOnlyList<OwnerConditionPlayerPresentationRow> players)
        {
            OwnerRuntimeUiFactory.ClearChildren(_playerContent);
            for (int index = 0; index < players.Count; index++)
                CreatePlayerRow(players[index], index);
        }

        private void CreatePlayerRow(OwnerConditionPlayerPresentationRow row, int index)
        {
            Color surfaceColor = index % 2 == 0 ? CareerUiTheme.SurfaceSubtle : CareerUiTheme.PanelDark;
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
            string playerId = row.Snapshot.PlayerPersonId;
            button.onClick.AddListener(() => PlayerSelected?.Invoke(playerId));

            Text player = CreateColumnText(
                surface.transform, "Name",
                string.Concat(row.Snapshot.PlayerName, "\n", row.AvailabilityText),
                0f, 0.18f, TextAnchor.MiddleLeft);
            player.fontStyle = FontStyle.Bold;
            player.color = GetAvailabilityColor(row.Snapshot.Availability);
            CreateColumnText(surface.transform, "Position", row.Snapshot.PositionText, 0.18f, 0.27f);
            CreateColumnText(surface.transform, "BaseCondition", row.BaseConditionText, 0.27f, 0.43f);
            Text assignment = CreateColumnText(
                surface.transform, "AssignmentModifier", row.AssignmentText, 0.43f, 0.53f);
            assignment.color = GetModifierColor(row.Snapshot.EffectiveCondition.AssignmentModifier);
            Text lineup = CreateColumnText(
                surface.transform, "LineupChemistry", row.LineupChemistryText, 0.53f, 0.63f);
            lineup.color = GetModifierColor(row.Snapshot.EffectiveCondition.LineupChemistryModifier);
            Text battery = CreateColumnText(
                surface.transform, "BatteryChemistry", row.BatteryChemistryText, 0.63f, 0.74f);
            battery.color = row.Snapshot.IsPitcher
                ? GetModifierColor(row.Snapshot.EffectiveCondition.BatteryChemistryModifier)
                : CareerUiTheme.TextMuted;
            Text expected = CreateColumnText(
                surface.transform, "ExpectedCondition", row.EffectiveConditionText, 0.74f, 1f);
            expected.fontStyle = FontStyle.Bold;
            expected.color = GetConditionColor(row.EffectiveLevel);
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

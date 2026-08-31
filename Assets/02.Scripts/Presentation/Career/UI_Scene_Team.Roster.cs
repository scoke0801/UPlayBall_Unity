using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Team
    {
        private void RenderRoster(TeamOverviewView view)
        {
            RectTransform panel = CreatePanel("Roster", $"로스터 명단  {view.Roster.Length}명",
                new Vector2(650f, 558f), new Vector2(-165f, 166f));
            RenderRosterFilters(panel, view);
            CreateText("HeaderPosition", panel, "포지션", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(60f, 24f), new Vector2(-272f, 127f), MutedColor);
            CreateText("HeaderName", panel, "이름", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(150f, 24f), new Vector2(-170f, 127f), MutedColor);
            CreateText("HeaderRole", panel, "역할", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(98f, 24f), new Vector2(-47f, 127f), MutedColor);
            CreateText("HeaderOverall", panel, "OVR", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(62f, 24f), new Vector2(45f, 127f), MutedColor);
            CreateText("HeaderRecord", panel, "시즌", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 24f), new Vector2(137f, 127f), MutedColor);
            CreateText("HeaderCondition", panel, "컨디션", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(82f, 24f), new Vector2(258f, 127f), MutedColor);

            int visibleCount = CountFilteredRoster(view);
            RectTransform viewport = CreateImage("RosterViewport", panel, PanelDarkColor,
                new Vector2(600f, 286f), new Vector2(0f, -35f));
            MarkVisual(viewport, CareerUiVisualRole.FlatSurface);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            float contentHeight = Mathf.Max(286f, visibleCount * 36f);
            RectTransform content = CreateRect("RosterContent", viewport, new Vector2(600f, contentHeight), Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, contentHeight);
            content.anchoredPosition = Vector2.zero;
            ScrollRect scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 34f;
            int rowIndex = 0;
            for (int index = 0; index < view.Roster.Length; index++)
            {
                TeamRosterPlayerView player = view.Roster[index];
                if (!MatchesRosterFilter(player))
                    continue;
                RenderRosterRow(content, player, view.MyPlayerExpectedRole, rowIndex++);
            }
            CreateText("RosterGuide", panel, "선수 선택 시 아래 포지션 경쟁 현황으로 이동 · 편성은 감독 AI 소유",
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(590f, 25f), new Vector2(0f, -190f), MutedColor);
        }

        private void RenderRosterFilters(Transform panel, TeamOverviewView view)
        {
            string[] labels = { "전체", "타자", "투수" };
            RosterFilter[] filters = { RosterFilter.All, RosterFilter.Batter, RosterFilter.Pitcher };
            for (int index = 0; index < filters.Length; index++)
            {
                RosterFilter filter = filters[index];
                bool selected = filter == _rosterFilter;
                Button button = CreateFramedButton(
                    "Filter_" + labels[index], panel, labels[index], new Vector2(106f, 34f),
                    new Vector2(-248f + index * 111f, 177f),
                    selected ? new Color(0.025f, 0.25f, 0.49f, 1f) : PanelDarkColor, out Text label);
                label.fontSize = 14;
                label.color = selected ? PrimaryTextColor : SecondaryTextColor;
                button.onClick.AddListener(() =>
                {
                    _rosterFilter = filter;
                    Render();
                });
            }
            CreateText("FilteredCount", panel, $"{CountFilteredRoster(view)}명", 13, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(90f, 30f), new Vector2(230f, 177f), SecondaryTextColor);
        }

        private void RenderRosterRow(
            Transform content,
            TeamRosterPlayerView player,
            ExpectedRole myPlayerExpectedRole,
            int rowIndex)
        {
            Color background = player.IsMyPlayer
                ? new Color(0.025f, 0.22f, 0.42f, 0.96f)
                : rowIndex % 2 == 0 ? new Color(0.013f, 0.055f, 0.09f, 0.96f) : PanelDarkColor;
            RectTransform row = CreateImage("RosterPlayer_" + player.PlayerId, content, background,
                new Vector2(590f, 34f), new Vector2(0f, -18f - rowIndex * 36f));
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            MarkVisual(row, CareerUiVisualRole.FlatSurface);
            Image rowImage = row.GetComponent<Image>();
            rowImage.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = rowImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.1f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.16f);
            button.colors = colors;
            PlayerPosition position = player.Position;
            button.onClick.AddListener(() =>
            {
                _selectedPosition = position;
                Render();
            });
            CreateText("Position", row, GetPositionCode(player.Position), 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(54f, 30f), new Vector2(-272f, 0f),
                player.IsMyPlayer ? GoldColor : SecondaryTextColor);
            CreateText("Marker", row, player.IsMyPlayer ? "★" : player.IsInNextGamePlan ? "●" : string.Empty,
                14, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(24f, 28f), new Vector2(-231f, 0f),
                player.IsMyPlayer ? GoldColor : RoleColor);
            CreateText("Name", row, player.Name, 15, player.IsMyPlayer ? FontStyle.Bold : FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(136f, 30f), new Vector2(-155f, 0f), PrimaryTextColor);
            CreateText("Role", row, GetRosterRoleLabel(player, myPlayerExpectedRole), 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(96f, 30f), new Vector2(-47f, 0f),
                player.IsMyPlayer ? GoldColor : player.IsInNextGamePlan ? RoleColor : SecondaryTextColor);
            CreateText("Overall", row, player.Overall.ToString(), 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(62f, 30f), new Vector2(45f, 0f), GetRatingColor(player.Overall));
            CreateText("Record", row, GetSeasonRecord(player), 13, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(112f, 30f), new Vector2(137f, 0f), SecondaryTextColor);
            CreateText("Condition", row, player.HasCondition ? GetConditionLabel(player.Condition) : "—",
                12, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(80f, 30f), new Vector2(258f, 0f),
                player.HasCondition ? GetRatingColor(player.Condition) : MutedColor);
        }

        private int CountFilteredRoster(TeamOverviewView view)
        {
            int count = 0;
            for (int index = 0; index < view.Roster.Length; index++)
            {
                if (MatchesRosterFilter(view.Roster[index]))
                    count++;
            }
            return count;
        }

        private bool MatchesRosterFilter(TeamRosterPlayerView player)
        {
            bool isPitcher = player.Position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return _rosterFilter switch
            {
                RosterFilter.Batter => !isPitcher,
                RosterFilter.Pitcher => isPitcher,
                _ => true
            };
        }

        private enum RosterFilter
        {
            All,
            Batter,
            Pitcher
        }
    }
}

using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Team
    {
        private static string GetSeasonRecord(TeamRosterPlayerView player)
        {
            if (player.Position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
                return player.HasPitchingRecord ? $"평균자책 {player.EarnedRunAverage:0.00}" : "기록 없음";
            return player.HasBattingRecord ? $"타율 {player.BattingAverage:.000}" : "기록 없음";
        }

        private static string GetRosterRoleLabel(TeamRosterRole role)
        {
            return role switch
            {
                TeamRosterRole.Starting => "주전",
                TeamRosterRole.Rotation => "로테이션",
                TeamRosterRole.Bullpen => "불펜",
                TeamRosterRole.Competition => "경쟁",
                _ => "백업"
            };
        }

        private static string GetRosterRoleLabel(
            TeamRosterPlayerView player,
            ExpectedRole myPlayerExpectedRole)
        {
            if (!player.IsMyPlayer)
                return GetRosterRoleLabel(player.RosterRole);

            return myPlayerExpectedRole switch
            {
                ExpectedRole.StartingCompetition => "주전 경쟁",
                ExpectedRole.RosterCompetition => "로스터 경쟁",
                _ => "벤치 경쟁"
            };
        }

        private static string GetPlannedRoleLabel(
            PlayerGameRole role,
            PlayerPosition position,
            int battingOrder)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(role, position))
                return "내 선수 · " + CareerGameRoleFormatter.GetPitcherRestLabel(position);

            return role switch
            {
                PlayerGameRole.StartingBatter when battingOrder > 0 =>
                    $"내 선수 · 선발 {GetPositionCode(position)} · {battingOrder}번 타자",
                PlayerGameRole.StartingBatter => $"내 선수 · 선발 {GetPositionCode(position)}",
                PlayerGameRole.StartingPitcher => "내 선수 · 선발 등판",
                PlayerGameRole.ReliefPitcher => "내 선수 · 구원 등판 예정",
                PlayerGameRole.Bench => "내 선수 · 벤치 대기",
                _ => "확정된 다음 경기 계획 없음"
            };
        }

        private static string GetArchetypeLabel(TeamArchetype archetype)
        {
            return archetype switch
            {
                TeamArchetype.Development => "육성 중심 구단",
                TeamArchetype.Contender => "우승 경쟁 구단",
                TeamArchetype.OffenseFocused => "공격 중심 구단",
                TeamArchetype.PitchingFocused => "투수 중심 구단",
                TeamArchetype.SmallMarket => "효율 중심 소규모 구단",
                _ => "균형형 구단"
            };
        }

        private static string GetDevelopmentLabel(TeamArchetype archetype)
        {
            return archetype switch
            {
                TeamArchetype.Development => "출장 기회와 장기 성장 우선",
                TeamArchetype.Contender => "즉시 전력과 성적 우선",
                TeamArchetype.OffenseFocused => "타격 생산성 중심",
                TeamArchetype.PitchingFocused => "투수진 안정성 중심",
                TeamArchetype.SmallMarket => "저비용 유망주 발굴 중심",
                _ => "균형 성장"
            };
        }

        private static string GetPositionCode(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string GetConditionLabel(int condition)
        {
            if (condition >= 85) return "최상";
            if (condition >= 70) return "좋음";
            if (condition >= 50) return "보통";
            if (condition >= 30) return "나쁨";
            return "최악";
        }

        private static string GetRatingGrade(int rating)
        {
            if (rating >= 90) return "S";
            if (rating >= 80) return "A";
            if (rating >= 70) return "B+";
            if (rating >= 60) return "B";
            if (rating >= 50) return "C+";
            if (rating >= 40) return "C";
            return "D";
        }

        private static Color GetRatingColor(int rating)
        {
            if (rating >= 80) return RoleColor;
            if (rating >= 65) return AccentColor;
            if (rating >= 50) return new Color(0.38f, 0.67f, 0.86f, 1f);
            return WarningColor;
        }

        private static Color ToColor(TeamColor color) => new Color32(color.Red, color.Green, color.Blue, 255);

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return level switch
            {
                LeagueLevel.Rookie => "ROOKIE",
                LeagueLevel.Minor => "MINOR",
                LeagueLevel.Major => "MAJOR",
                _ => "ROOKIE"
            };
        }

        private static string GetKoreanDayOfWeek(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "월",
                DayOfWeek.Tuesday => "화",
                DayOfWeek.Wednesday => "수",
                DayOfWeek.Thursday => "목",
                DayOfWeek.Friday => "금",
                DayOfWeek.Saturday => "토",
                _ => "일"
            };
        }

        private static string FormatMoney(long amount)
        {
            return amount >= 100_000_000L
                ? $"{amount / 100_000_000d:0.##}억원"
                : $"{amount / 10_000d:N0}만원";
        }

        private static string FormatSigned(int value) => value > 0 ? "+" + value : value.ToString();

        private static RectTransform CreateSection(
            string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform frame = CreateImage(name, parent, DividerColor, size, position);
            RectTransform surface = CreateImage("Surface", frame, color, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            return frame;
        }

        private static void CreateProgressBar(
            Transform parent, float normalizedValue, Vector2 size, Vector2 position, Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            RectTransform track = CreateImage("Track", parent, new Color(0.11f, 0.16f, 0.2f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage("Fill", track, fillColor,
                new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name, Transform parent, Color color, Vector2 size, Vector2 position, bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name, Transform parent, string value, int fontSize, FontStyle style,
            TextAnchor alignment, Vector2 size, Vector2 position, Color color, bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name, Transform parent, string label, Vector2 size, Vector2 position, Color color, out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            text = CreateText("Label", rect, label, 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            return button;
        }

        private static void AddTextOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
        }
    }
}

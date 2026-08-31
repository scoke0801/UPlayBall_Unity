using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Player
    {
        private static RectTransform CreatePanel(
            string name, Transform parent, string title, Vector2 size, Vector2 position)
        {
            RectTransform frame = CreateImage(name, parent, BorderColor, size, position);
            RectTransform surface = CreateImage("Surface", frame, PanelColor,
                Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            CreateText("Title", frame, title, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 36f, 36f), new Vector2(0f, size.y * 0.5f - 27f), PrimaryTextColor);
            CreateImage("TitleDivider", frame, DividerColor, new Vector2(size.x - 28f, 1f),
                new Vector2(0f, size.y * 0.5f - 52f));
            return frame;
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
            text = CreateText("Label", rect, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            return button;
        }

        private static void CreateProgressBar(
            Transform parent, float normalizedValue, Vector2 size, Vector2 position, Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            RectTransform track = CreateImage("Track", parent, new Color(0.08f, 0.13f, 0.17f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage("Fill", track, fillColor,
                new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
            CareerUiSkin.ApplyProgressBar(track.GetComponent<Image>(), fill.GetComponent<Image>(), clamped);
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

        private static Color ToColor(TeamColor color) => new Color32(color.Red, color.Green, color.Blue, 255);

        private static Color GetRatingColor(int rating)
        {
            if (rating >= 80) return RoleColor;
            if (rating >= 65) return BrightAccentColor;
            if (rating >= 50) return new Color(0.38f, 0.67f, 0.86f, 1f);
            return WarningColor;
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

        private static string GetHandednessLabel(Handedness batting, Handedness throwing)
        {
            return $"{GetThrowingLabel(throwing)}투{GetBattingLabel(batting)}타";
        }

        private static string GetBattingLabel(Handedness hand)
        {
            return hand switch { Handedness.Left => "좌", Handedness.Switch => "양", _ => "우" };
        }

        private static string GetThrowingLabel(Handedness hand) => hand == Handedness.Left ? "좌" : "우";

        private static string GetRoleLabel(PlayerProfileView view)
        {
            if (CareerGameRoleFormatter.IsPitcherRest(view.PlannedRole, view.Position))
                return "다음 경기 " + CareerGameRoleFormatter.GetPitcherRestLabel(view.Position);

            return view.PlannedRole switch
            {
                PlayerGameRole.StartingBatter => "다음 경기 선발",
                PlayerGameRole.StartingPitcher => "다음 경기 선발 등판",
                PlayerGameRole.ReliefPitcher => "다음 경기 구원 대기",
                PlayerGameRole.Bench => "다음 경기 벤치",
                _ => view.ExpectedRole switch
                {
                    ExpectedRole.StartingCompetition => "주전 경쟁 우위",
                    ExpectedRole.RosterCompetition => "로스터 경쟁",
                    _ => "벤치 경쟁"
                }
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

        private static string GetFatigueLabel(int fatigue)
        {
            if (fatigue <= 20) return "여유";
            if (fatigue <= 45) return "보통";
            if (fatigue <= 70) return "높음";
            return "위험";
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "컨택",
                PlayerAbility.Power => "장타",
                PlayerAbility.Speed => "주루",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비",
                PlayerAbility.BatterMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구",
                PlayerAbility.PitcherMental => "위기관리",
                _ => ability.ToString()
            };
        }

        private static string GetGrowthRoomLabel(int growthRoom)
        {
            if (growthRoom >= 20) return "매우 넓음";
            if (growthRoom >= 12) return "넓음";
            if (growthRoom >= 6) return "보통";
            return "제한적";
        }

        private static string GetWorkEthicLabel(WorkEthicGrade grade)
        {
            return grade switch
            {
                WorkEthicGrade.VeryDiligent => "매우 성실",
                WorkEthicGrade.Diligent => "성실",
                WorkEthicGrade.Inconsistent => "기복 있음",
                _ => "보통"
            };
        }

        private static string GetCareerPhaseLabel(CareerPhase phase)
        {
            return phase switch
            {
                CareerPhase.Growth => "성장기",
                CareerPhase.Prime => "전성기",
                CareerPhase.Skilled => "숙련기",
                CareerPhase.Decline => "하락기",
                _ => "커리어 후반"
            };
        }

        private static string GetLeagueLabel(LeagueLevel level)
        {
            return WorldGenerationConfiguration.GetDefaultDefinition(level).UiDisplayName;
        }

        private static string GetSeasonPhaseLabel(SeasonPhase phase)
        {
            return phase switch
            {
                SeasonPhase.Preseason => "프리시즌",
                SeasonPhase.RegularSeason => "정규 시즌",
                SeasonPhase.Postseason => "포스트시즌",
                SeasonPhase.SeasonReview => "시즌 결산",
                SeasonPhase.Offseason => "오프시즌",
                _ => "시즌 완료"
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
    }
}

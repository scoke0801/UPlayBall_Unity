using System;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    /// <summary>공통 경기 HUD 구현이 모델 보관과 null 검사를 반복하지 않게 하는 기반 View다.</summary>
    public abstract class MatchHUDBase : MonoBehaviour, IMatchHudView
    {
        public MatchHudPresentationModel CurrentModel { get; private set; }

        /// <summary>새 HUD 모델을 보관한 뒤 실제 View 갱신을 요청한다.</summary>
        public void Present(MatchHudPresentationModel model)
        {
            CurrentModel = model ?? throw new ArgumentNullException(nameof(model));
            RenderModel(model);
        }

        /// <summary>파생 HUD가 Native UI 요소를 갱신한다.</summary>
        protected abstract void RenderModel(MatchHudPresentationModel model);
    }

    /// <summary>공용 경기 정보를 중립 Theme의 고밀도 상단 스코어보드로 그리는 uGUI View다.</summary>
    [DisallowMultipleComponent]
    public sealed class MatchHudView : MatchHUDBase
    {
        private static Font _font;

        private Text _awayTeam;
        private Text _awayScore;
        private Text _homeTeam;
        private Text _homeScore;
        private Text _inning;
        private Text _count;
        private Text _runners;
        private Text _matchup;

        /// <summary>지정한 경기 Workspace 상단에 공용 HUD를 생성한다.</summary>
        public static MatchHudView CreateRuntime(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var root = new GameObject(nameof(MatchHudView), typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1920f, 116f);
            rect.anchoredPosition = Vector2.zero;
            return root.AddComponent<MatchHudView>();
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        protected override void RenderModel(MatchHudPresentationModel model)
        {
            EnsureHierarchy();
            _awayTeam.text = FormatTeam(model.AwayTeam);
            _awayScore.text = model.AwayTeam.Score.ToString();
            _homeTeam.text = FormatTeam(model.HomeTeam);
            _homeScore.text = model.HomeTeam.Score.ToString();
            _inning.text = $"{model.Inning}회{(model.Half == MatchHudHalf.Top ? "초" : "말")}";
            _count.text = model.IsBetweenInnings
                ? "B 0   S 0   O 0"
                : $"B {model.Count.Balls}   S {model.Count.Strikes}   O {model.Count.Outs}";
            _runners.text = FormatRunners(model.Bases);
            _matchup.text = model.IsBetweenInnings
                ? "공수 교대"
                : $"타자 {FormatParticipant(model.Batter)}  ·  투수 {FormatParticipant(model.Pitcher)}";
        }

        private void EnsureHierarchy()
        {
            if (_awayTeam != null)
                return;

            Image background = GetComponent<Image>();
            background.color = CareerUiTheme.TopBar;
            background.raycastTarget = false;
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.Border;
            outline.effectDistance = new Vector2(0f, -1f);

            _awayTeam = CreateText("AwayTeam", 20, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(340f, 34f), new Vector2(-330f, -34f), CareerUiTheme.TextPrimary);
            _awayScore = CreateText("AwayScore", 46, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 62f), new Vector2(-140f, -34f), CareerUiTheme.Number);
            _homeScore = CreateText("HomeScore", 46, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(110f, 62f), new Vector2(140f, -34f), CareerUiTheme.Number);
            _homeTeam = CreateText("HomeTeam", 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(340f, 34f), new Vector2(330f, -34f), CareerUiTheme.TextPrimary);
            _inning = CreateText("Inning", 23, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(240f, 32f), new Vector2(0f, -23f), CareerUiTheme.PrimaryBright);
            _count = CreateText("Count", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(320f, 28f), new Vector2(0f, -75f), CareerUiTheme.TextSecondary);
            _runners = CreateText("Runners", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(300f, 28f), new Vector2(-785f, -79f), CareerUiTheme.TextSecondary);
            _matchup = CreateText("Matchup", 14, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(440f, 28f), new Vector2(735f, -79f), CareerUiTheme.TextSecondary);
        }

        private Text CreateText(
            string objectName,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            var child = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(transform, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Text text = child.GetComponent<Text>();
            text.font = _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static string FormatTeam(MatchHudTeamModel team)
        {
            return team.IsBatting ? "▶ " + team.Name : team.Name;
        }

        private static string FormatParticipant(MatchHudParticipantModel participant)
        {
            return participant.HasValue && !string.IsNullOrWhiteSpace(participant.Name)
                ? participant.Name
                : "-";
        }

        private static string FormatRunners(MatchHudBaseStateModel bases)
        {
            if (bases == null || !bases.HasAnyRunner)
                return "주자 없음";

            string result = "주자 ";
            if (bases.HasRunnerOnFirst) result += "1루";
            if (bases.HasRunnerOnSecond) result += bases.HasRunnerOnFirst ? " · 2루" : "2루";
            if (bases.HasRunnerOnThird) result += bases.HasRunnerOnFirst || bases.HasRunnerOnSecond ? " · 3루" : "3루";
            return result;
        }
    }
}

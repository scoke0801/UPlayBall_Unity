using System;
using System.Globalization;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>순위표 레퍼런스의 밝은 격자와 구단 강조를 네 개의 리그 탭에 표시한다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerLeague : MonoBehaviour
    {
        private static Color Ink => CareerUiTheme.ReferenceDataInk;
        private static Color Blue => CareerUiTheme.ReferenceDataAccent;
        private static readonly Color Red = new Color32(204, 44, 65, 255);
        private static Color Grid => CareerUiTheme.ReferenceDataGrid;
        private static Color Focus => CareerUiTheme.ReferenceDataFocus;
        private OwnerLeaguePresentationModel _model;
        private int _tab;
        private int _historyStart;
        public event Action<string> TeamSelected;

        /// <summary>공용 셸의 리그 작업 영역에 화면을 만든다.</summary>
        public static UI_Scene_OwnerLeague CreateRuntime(RectTransform host)
        {
            return OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerLeague), host)
                .gameObject.AddComponent<UI_Scene_OwnerLeague>();
        }

        /// <summary>현재 시즌 기록을 교체하고 최신 여섯 라운드를 선택한다.</summary>
        public void Bind(OwnerLeaguePresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _historyStart = Math.Max(0, model.Rounds.Count - 6);
            if (gameObject.activeSelf) Render();
        }

        /// <summary>셸의 하위 탭 선택을 본문에 반영한다.</summary>
        public void ShowTab(int tab)
        {
            _tab = tab;
            gameObject.SetActive(true);
            Render();
        }

        private void Render()
        {
            OwnerRuntimeUiFactory.ClearChildren(transform);
            if (_model == null) return;
            var root = (RectTransform)transform;
            Surface(root, "Paper", CareerUiTheme.ReferenceDataCanvas, 0, 0, 1, 1);
            Label(root, "Season", _model.SeasonLabel, .035f, .91f, .8f, .98f, 16, Ink, TextAnchor.MiddleLeft);
            Surface(root, "BlueRule", Blue, .025f, .897f, .975f, .9f);
            var table = OwnerRuntimeUiFactory.CreateRect("LeagueTable", root);
            Place(table, .035f, .12f, .965f, .87f);
            if (_model.Standings.Count == 0)
                Label(table, "Empty", "표시할 리그 일정이 없습니다.", 0, 0, 1, 1, 20, Ink);
            else if (_tab == 2) RenderMatchups(table);
            else if (_tab == 3) RenderHistory(table, root);
            else RenderStandings(table, _tab == 1);
            bool hasHistoryNavigation = _tab == 3;
            float legendBottom = hasHistoryNavigation ? .005f : .025f;
            float legendTop = hasHistoryNavigation ? .045f : .095f;
            Label(root, "Legend", hasHistoryNavigation ? "라운드 종료 기준 · 승률 → 득실차 순" :
                _tab == 2 ? "행 구단 기준  승 - 패 (무)" : "순위: 승률 → 득실차 → 구단 고유 순서",
                .035f, legendBottom, .75f, legendTop, 14, Ink, TextAnchor.MiddleLeft);
            Label(root, "FocusLegend", "■ 내 구단", .8f, legendBottom, .965f, legendTop, 14, Blue);
        }

        private void RenderStandings(RectTransform host, bool metrics)
        {
            string[] headers = metrics
                ? new[] { "순위", "구단명", "경기", "득점", "실점", "득실차", "경기당 득점", "경기당 실점" }
                : new[] { "순위", "구단명", "경기", "승", "패", "무", "승률", "승차", "득점", "실점" };
            float[] widths = metrics ? new[] { .1f, .26f, .09f, .1f, .1f, .1f, .125f, .125f }
                : new[] { .1f, .26f, .08f, .07f, .07f, .07f, .09f, .08f, .09f, .09f };
            DrawRow(host, "Header", headers, widths, 0, false, true);
            var leader = _model.Standings[0];
            for (int i = 0; i < _model.Standings.Count; i++)
            {
                var team = _model.Standings[i];
                double behind = ((leader.Wins - leader.Losses) - (team.Wins - team.Losses)) / 2d;
                string[] values = metrics
                    ? new[] { team.Rank + "위", team.Name, team.Games.ToString(), team.Runs.ToString(),
                        team.RunsAllowed.ToString(), (team.Runs - team.RunsAllowed).ToString("+0;-0;0"),
                        team.Games == 0 ? "—" : Rate((double)team.Runs / team.Games, "0.00"),
                        team.Games == 0 ? "—" : Rate((double)team.RunsAllowed / team.Games, "0.00") }
                    : new[] { team.Rank + "위", team.Name, team.Games.ToString(), team.Wins.ToString(),
                        team.Losses.ToString(), team.Ties.ToString(), team.Wins + team.Losses == 0 ? "—" : Rate(team.Percentage),
                        team.Rank == 1 ? "—" : Rate(behind, "0.0"), team.Runs.ToString(), team.RunsAllowed.ToString() };
                RectTransform row = DrawRow(host, "Team_" + i, values, widths, i + 1,
                    team.Id == _model.FocusTeamId, false, !metrics);
                AddEmblem(row, team.Name, team.EmblemId, .108f, .138f);
                var image = row.GetComponent<Image>();
                image.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                string teamId = team.Id;
                button.onClick.AddListener(() => TeamSelected?.Invoke(teamId));
            }
        }

        private void RenderMatchups(RectTransform host)
        {
            int count = _model.Standings.Count;
            var widths = new float[count + 1];
            var headers = new string[count + 1];
            widths[0] = .24f;
            headers[0] = "구단명";
            for (int i = 0; i < count; i++)
            {
                widths[i + 1] = .76f / count;
                headers[i + 1] = _model.Standings[i].Name;
            }
            DrawRow(host, "Header", headers, widths, 0, false, true);
            for (int i = 0; i < count; i++)
            {
                var team = _model.Standings[i];
                var values = new string[count + 1];
                values[0] = team.Name;
                for (int j = 0; j < count; j++)
                {
                    var record = _model.GetMatchup(team.Id, _model.Standings[j].Id);
                    values[j + 1] = i == j ? "" : $"{record.Wins} - {record.Losses} ({record.Ties})";
                }
                RectTransform row = DrawRow(host, "Matchup_" + i, values, widths, i + 1,
                    team.Id == _model.FocusTeamId, false);
                AddEmblem(row, team.Name, team.EmblemId, .012f, .042f);
                Surface(row, "Self", new Color32(231, 233, 235, 255),
                    .24f + i * .76f / count, .025f, .24f + (i + 1) * .76f / count, .975f);
            }
        }

        private void RenderHistory(RectTransform host, RectTransform footerHost)
        {
            var chart = OwnerRuntimeUiFactory.CreateRect("RankHistory", host);
            Place(chart, 0, 0, .43f, 1);
            var graphic = chart.gameObject.AddComponent<UILeagueRankChart>();
            graphic.Bind(_model, _historyStart);
            int visible = Math.Min(6, _model.Rounds.Count - _historyStart);
            for (int i = 0; i < visible; i++)
                Label(chart, "Round_" + i, _model.Rounds[_historyStart + i] + "회",
                    i / 6f, .92f, (i + 1) / 6f, 1, 15, Ink);
            var list = OwnerRuntimeUiFactory.CreateRect("RankList", host);
            Place(list, .45f, 0, 1, 1);
            var widths = new[] { .17f, .46f, .19f, .18f };
            DrawRow(list, "Header", new[] { "순위", "구단명", "순위 변화", "최근 전적" }, widths, 0, false, true);
            for (int i = 0; i < _model.Standings.Count; i++)
            {
                var team = _model.Standings[i];
                int change = team.RankHistory.Count < 2 ? 0 : team.RankHistory[team.RankHistory.Count - 2] - team.Rank;
                string streak = team.Games == 0 ? "—" : team.Streak + (team.StreakOutcome > 0 ? " 승" : team.StreakOutcome < 0 ? " 패" : " 무");
                RectTransform row = DrawRow(list, "Team_" + i, new[] { team.Rank + "위", team.Name,
                    change == 0 ? "—" : (change > 0 ? "▲ " : "▼ ") + Math.Abs(change), streak }, widths,
                    i + 1, team.Id == _model.FocusTeamId, false);
                AddEmblem(row, team.Name, team.EmblemId, .178f, .218f);
            }
            if (visible == 0)
                Label(chart, "Empty", "첫 경기 종료 후\n순위 변화가 표시됩니다.", 0, .2f, 1, .8f, 18, Ink);
            HistoryButton(footerHost, "Previous", "◀", .045f,
                () => { _historyStart = Math.Max(0, _historyStart - 6); Render(); },
                _historyStart > 0);
            HistoryButton(footerHost, "Next", "▶", .38f,
                () => { _historyStart = Math.Min(Math.Max(0, _model.Rounds.Count - 6), _historyStart + 6); Render(); },
                _historyStart + 6 < _model.Rounds.Count);
        }

        private void HistoryButton(RectTransform host, string name, string label, float x, UnityEngine.Events.UnityAction action, bool enabled)
        {
            var surface = Surface(host, name, Color.white, x, .0525f, x + .0465f, .105f);
            surface.GetComponent<Image>().raycastTarget = true;
            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface.GetComponent<Image>();
            button.interactable = enabled;
            button.onClick.AddListener(action);
            Label(surface, "Label", label, 0, 0, 1, 1, 18, enabled ? Blue : Grid);
        }

        private RectTransform DrawRow(RectTransform host, string name, string[] values, float[] widths,
            int index, bool focus, bool header, bool colorResults = false)
        {
            float height = 1f / Math.Max(11, _model.Standings.Count + 1);
            float top = 1 - index * height;
            var row = Surface(host, name, focus ? Focus : header ? new Color32(236, 238, 240, 255) :
                index % 2 == 0 ? new Color32(248, 248, 248, 255) : Color.white, 0, top - height, 1, top);
            float x = 0;
            for (int i = 0; i < values.Length; i++)
            {
                Color color = colorResults && i == 3 ? Red : colorResults && i == 5 ? Blue : Ink;
                bool isTeamName = index > 0 && ((_tab != 2 && i == 1) || (_tab == 2 && i == 0));
                Label(row, "Cell_" + i, values[i], x + (isTeamName ? .045f : .006f), .04f,
                    x + widths[i] - .006f, .96f,
                    header ? 15 : 17, color, isTeamName ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                Surface(row, "Column_" + i, Grid, x, 0, x, 1).sizeDelta = new Vector2(1, 0);
                x += widths[i];
            }
            var rule = Surface(row, "Rule", focus ? new Color32(117, 202, 231, 255) : Grid, 0, 0, 1, 0);
            rule.sizeDelta = new Vector2(0, focus ? 2 : 1);
            return row;
        }

        private static string Rate(double value, string format = "0.000") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void AddEmblem(Transform row, string teamName, int emblemId, float x0, float x1)
        {
            if (TeamEmblemSprites.ResolveEmblemId(teamName, emblemId) <= 0) return;
            Image image = OwnerRuntimeUiFactory.CreateImage("Emblem", row, Color.white);
            Place(image.rectTransform, x0, .14f, x1, .86f);
            TeamEmblemSprites.TryApply(image, emblemId, teamName);
        }

        private static RectTransform Surface(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var rect = OwnerRuntimeUiFactory.CreateImage(name, parent, color).rectTransform;
            Place(rect, x0, y0, x1, y1);
            return rect;
        }

        private static void Label(Transform parent, string name, string value, float x0, float y0, float x1, float y1,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Bold, alignment, color);
            Place(text.rectTransform, x0, y0, x1, y1);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
        }

        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1) =>
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
    }
}

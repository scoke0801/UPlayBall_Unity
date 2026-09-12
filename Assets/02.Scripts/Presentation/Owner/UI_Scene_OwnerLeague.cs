using System;
using System.Globalization;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>V2 작업면에 리그 기록과 내 구단·선택·입력 포커스를 구분해 표시한다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class UI_Scene_OwnerLeague : MonoBehaviour
    {
        private static Color Ink => CareerUiTheme.ReferenceDataInk;
        private static Color Blue => CareerUiTheme.ReferenceDataAccent;
        private static Color Grid => CareerUiTheme.ReferenceDataGrid;
        private static Color Focus => CareerUiTheme.ReferenceDataFocus;
        private OwnerLeaguePresentationModel _model;
        private int _tab;
        private int _historyStart;
        private Button _selectedTeamButton;
        private string _selectedTeamId;
        private Color _selectedTeamBaseColor;
        public event Action<string> TeamSelected;

        /// <summary>선수단 상세에서 돌아오면 선택했던 구단으로 입력 포커스를 복원한다.</summary>
        public void RestoreFocus()
        {
            if (_selectedTeamButton != null) _selectedTeamButton.Select();
            else if (_tab == 0) _sectionButton?.Select();
        }

        /// <summary>공용 셸의 리그 작업 영역에 화면을 만든다.</summary>
        public static UI_Scene_OwnerLeague CreateRuntime(RectTransform host)
        {
            return OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerLeague), host)
                .gameObject.AddComponent<UI_Scene_OwnerLeague>();
        }

        /// <summary>현재 시즌 기록을 교체하고 최신 여섯 라운드를 선택한다.</summary>
        public void Bind(OwnerLeaguePresentationModel model, Baseball.Game.Historical.OwnerSeasonReviewSnapshot postseason = null)
        {
            if (_postseason != null && postseason != null && _postseason.SeasonNumber != postseason.SeasonNumber)
                _section = StandingsSection.PennantRace;
            _postseason = postseason;
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
            _selectedTeamButton = null;
            _sectionButton = null;
            // Destroy가 지연되는 프레임에도 이전 탭의 입력과 글자가 겹치지 않게 한다.
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(false);
            OwnerRuntimeUiFactory.ClearChildren(transform);
            if (_model == null) return;
            var root = (RectTransform)transform;
            UIOwnerFrontOfficePanel.ApplyWorkspace(root);
            Label(root, "Season", _model.SeasonLabel, .035f, .91f, .36f, .98f, 18, Ink, TextAnchor.MiddleLeft);
            Surface(root, "BlueRule", OwnerDashboardStyle.Line, .025f, .897f, .975f, .899f);
            if (_tab <= 1 && !(_tab == 0 && _section == StandingsSection.Postseason)) RenderFocusSummary(root);
            if (_tab == 0) RenderSectionTabs(root);
            if (_tab == 0 && _section == StandingsSection.Postseason)
            {
                RenderPostseason(root);
                return;
            }
            var table = OwnerRuntimeUiFactory.CreateRect("LeagueTable", root);
            Place(table, .035f, .12f, .965f, _tab == 0 ? .79f : .87f);
            if (_model.Standings.Count == 0)
                Label(table, "Empty", "표시할 리그 일정이 없습니다.", 0, 0, 1, 1, 20, Ink);
            else if (_tab == 2) RenderMatchups(table);
            else if (_tab == 3) RenderHistory(table, root);
            else RenderStandings(table, _tab == 1);
            bool hasHistoryNavigation = _tab == 3;
            float legendBottom = hasHistoryNavigation ? .005f : .025f;
            float legendTop = hasHistoryNavigation ? .045f : .095f;
            Label(root, "Legend", hasHistoryNavigation ? "라운드 종료 기준 · 승률 → 득실차 순" :
                _tab == 2 ? "행 구단 기준  승 - 패 (무)" : "순위 기준: 승률 · 득실차",
                .035f, legendBottom, .75f, legendTop, 14, OwnerDashboardStyle.TableSecondary, TextAnchor.MiddleLeft);
            if (_tab <= 1)
                Label(root, "OpenTeamHint", "구단 선택 · 선수단 보기", .76f, legendBottom, .965f, legendTop,
                    13, OwnerDashboardStyle.TableSecondary, TextAnchor.MiddleRight);
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
                string teamName = team.Name;
                string[] values = metrics
                    ? new[] { team.Rank + "위", teamName, team.Games.ToString(), team.Runs.ToString(),
                        team.RunsAllowed.ToString(), (team.Runs - team.RunsAllowed).ToString("+0;-0;0"),
                        team.Games == 0 ? "—" : Rate((double)team.Runs / team.Games, "0.00"),
                        team.Games == 0 ? "—" : Rate((double)team.RunsAllowed / team.Games, "0.00") }
                    : new[] { team.Rank + "위", teamName, team.Games.ToString(), team.Wins.ToString(),
                        team.Losses.ToString(), team.Ties.ToString(), team.Wins + team.Losses == 0 ? "—" : Rate(team.Percentage),
                        team.Rank == 1 ? "—" : Rate(behind, "0.0"), team.Runs.ToString(), team.RunsAllowed.ToString() };
                RectTransform row = DrawRow(host, "Team_" + i, values, widths, i + 1,
                    team.Id == _model.FocusTeamId, false);
                AddEmblem(row, team.EmblemTeamName, team.EmblemId, .108f, .138f);
                ConfigureTeamButton(row, team.Id);
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
                AddEmblem(row, team.EmblemTeamName, team.EmblemId, .012f, .042f);
                Surface(row, "Self", OwnerDashboardStyle.Surface,
                    .24f + i * .76f / count, .025f, .24f + (i + 1) * .76f / count, .975f);
            }
        }

        private void RenderHistory(RectTransform host, RectTransform footerHost)
        {
            var chart = Surface(host, "RankHistory", OwnerDashboardStyle.TableSurface, 0, 0, .43f, 1);
            var plot = OwnerRuntimeUiFactory.CreateRect("Plot", chart);
            var graphic = plot.gameObject.AddComponent<UILeagueRankChart>();
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
                AddEmblem(row, team.EmblemTeamName, team.EmblemId, .178f, .218f);
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
            var button = OwnerRuntimeUiFactory.CreateReferenceButton(name, host, label, 18);
            Place((RectTransform)button.transform, x, .0525f, x + .0465f, .105f);
            button.interactable = enabled;
            button.onClick.AddListener(action);
            OwnerDashboardStyle.SetTypography(button.transform.Find("Label").GetComponent<Text>());
            button.GetComponent<OwnerUiButtonSkin>()?.Refresh();
        }

        private void RenderFocusSummary(RectTransform root)
        {
            foreach (var team in _model.Standings)
            {
                if (team.Id != _model.FocusTeamId) continue;
                string summary = team.Games == 0 ? "첫 경기 종료 후 내 구단 성적이 표시됩니다."
                    : $"내 구단 {team.Rank}위   ·   경기당 득점 {Rate((double)team.Runs / team.Games, "0.00")}"
                        + $"   /   실점 {Rate((double)team.RunsAllowed / team.Games, "0.00")}"
                        + $"   ·   득실차 {(team.Runs - team.RunsAllowed).ToString("+0;-0;0")}";
                Label(root, "FocusSummary", summary, .37f, .91f, .965f, .98f,
                    15, OwnerDashboardStyle.Ivory, TextAnchor.MiddleRight);
                return;
            }
        }

        private RectTransform DrawRow(RectTransform host, string name, string[] values, float[] widths,
            int index, bool focus, bool header)
        {
            float height = 1f / Math.Max(11, _model.Standings.Count + 1);
            float top = 1 - index * height;
            var row = Surface(host, name, header ? OwnerDashboardStyle.TableHeader :
                index % 2 == 0 ? OwnerDashboardStyle.TableAlternate : OwnerDashboardStyle.TableSurface,
                0, top - height, 1, top);
            row.gameObject.AddComponent<CareerUiPreserveTextColor>();
            float x = 0;
            for (int i = 0; i < values.Length; i++)
            {
                bool teamColumn = (_tab != 2 && i == 1) || (_tab == 2 && i == 0);
                bool isTeamName = !header && teamColumn;
                bool isNumeric = !teamColumn && _tab != 2 && i > 0;
                bool primary = isTeamName || (!header && ((_tab == 0 && i == 6) || (_tab == 1 && i >= 6)));
                Color color = header || (!primary && i == 2 && _tab <= 1)
                    ? OwnerDashboardStyle.TableSecondary : OwnerDashboardStyle.Ivory;
                float right = x + widths[i] - .012f;
                Label(row, "Cell_" + i, values[i], x + (teamColumn ? .045f : .012f), .04f,
                    right - (isTeamName && focus ? .055f : 0), .96f,
                    header ? 14 : 17, color, teamColumn ? TextAnchor.MiddleLeft :
                        isNumeric ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter, primary);
                if (isTeamName && focus)
                    Label(row, "MyTeamBadge", "내 구단", right - .05f, .2f, right, .8f,
                        11, OwnerDashboardStyle.Gold);
                x += widths[i];
            }
            Surface(row, "Rule", OwnerDashboardStyle.Line, 0, 0, 1, 0).sizeDelta = new Vector2(0, 1);
            return row;
        }

        private void ConfigureTeamButton(RectTransform row, string teamId)
        {
            var image = row.GetComponent<Image>();
            image.raycastTarget = true;
            Color baseColor = image.color;
            bool selected = teamId == _selectedTeamId;
            if (selected) image.color = OwnerDashboardStyle.TableSelected;
            var stripe = Surface(row, "SelectedTeam", OwnerDashboardStyle.Gold, 0, .12f, 0, .88f);
            stripe.sizeDelta = new Vector2(3, 0);
            stripe.gameObject.SetActive(selected);
            var button = row.gameObject.AddComponent<Button>();
            // 데이터 행은 장식 버튼 스킨으로 바뀌지 않으며 Hover는 표면 밝기만 올린다.
            button.targetGraphic = image;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.17f, 1.17f, 1.17f, 1);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.85f, .85f, .85f, 1);
            colors.fadeDuration = .12f;
            button.colors = colors;
            var focus = OwnerRuntimeUiFactory.CreateRect("KeyboardFocus", row);
            Surface(focus, "Top", OwnerDashboardStyle.TableSecondary, 0, 1, 1, 1).sizeDelta = new Vector2(0, 1);
            Surface(focus, "Bottom", OwnerDashboardStyle.TableSecondary, 0, 0, 1, 0).sizeDelta = new Vector2(0, 1);
            Surface(focus, "Left", OwnerDashboardStyle.TableSecondary, 0, 0, 0, 1).sizeDelta = new Vector2(1, 0);
            Surface(focus, "Right", OwnerDashboardStyle.TableSecondary, 1, 0, 1, 1).sizeDelta = new Vector2(1, 0);
            focus.gameObject.SetActive(false);
            var trigger = row.gameObject.AddComponent<EventTrigger>();
            var select = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            select.callback.AddListener(_ => focus.gameObject.SetActive(true));
            trigger.triggers.Add(select);
            var deselect = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
            deselect.callback.AddListener(_ => focus.gameObject.SetActive(false));
            trigger.triggers.Add(deselect);
            if (selected)
            {
                _selectedTeamButton = button;
                _selectedTeamBaseColor = baseColor;
            }
            button.onClick.AddListener(() =>
            {
                if (_selectedTeamButton != null)
                {
                    _selectedTeamButton.GetComponent<Image>().color = _selectedTeamBaseColor;
                    _selectedTeamButton.transform.Find("SelectedTeam")?.gameObject.SetActive(false);
                }
                _selectedTeamId = teamId;
                _selectedTeamButton = button;
                _selectedTeamBaseColor = baseColor;
                image.color = OwnerDashboardStyle.TableSelected;
                stripe.gameObject.SetActive(true);
                TeamSelected?.Invoke(teamId);
            });
        }

        private static string Rate(double value, string format = "0.000") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void AddEmblem(Transform row, string teamName, int emblemId, float x0, float x1)
        {
            if (!TeamEmblemSprites.CanResolve(teamName, emblemId)) return;
            Image image = OwnerRuntimeUiFactory.CreateImage("Emblem", row, Color.white);
            Place(image.rectTransform, x0, .14f, x1, .86f);
            TeamEmblemSprites.TryApply(image, emblemId, teamName);
        }

        private static RectTransform Surface(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var rect = OwnerRuntimeUiFactory.CreateImage(name, parent, color).rectTransform;
            // 공용 스킨 재적용이 기록표와 포커스 선을 밝은 패널로 덮지 않게 한다.
            rect.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            Place(rect, x0, y0, x1, y1);
            return rect;
        }

        private static void Label(Transform parent, string name, string value, float x0, float y0, float x1, float y1,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, bool emphasis = false)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Bold, alignment, color);
            OwnerDashboardStyle.SetTypography(text, emphasis || name == "Season");
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

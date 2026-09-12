using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerLeague
    {
        private enum StandingsSection { PennantRace, Postseason }

        private StandingsSection _section;
        private Button _sectionButton;
        private OwnerSeasonReviewSnapshot _postseason;
        private static Sprite _postseasonBackdrop;

        private void RenderSectionTabs(RectTransform root)
        {
            CreateSectionTab(root, "PennantRaceTab", "페넌트레이스", StandingsSection.PennantRace, .035f);
            CreateSectionTab(root, "PostseasonTab", "포스트시즌", StandingsSection.Postseason, .205f);
        }

        private void CreateSectionTab(RectTransform root, string name, string title, StandingsSection section, float left)
        {
            Button button = OwnerRuntimeUiFactory.CreateReferenceButton(name, root, title, 16);
            Place((RectTransform)button.transform, left, .815f, left + .16f, .88f);
            bool selected = _section == section;
            if (selected) _sectionButton = button;
            button.GetComponent<Image>().color = selected ? Focus : Color.white;
            if (selected) Surface(button.transform, "SelectionRule", Blue, .04f, .03f, .96f, .08f);
            OwnerUiButtonSkin.SetSelected(button, selected);
            button.onClick.AddListener(() =>
            {
                _section = section;
                Render();
                _sectionButton.Select();
            });
        }

        private void RenderPostseason(RectTransform root)
        {
            RectTransform board = OwnerRuntimeUiFactory.CreateRect("PostseasonBoard", root);
            Place(board, .035f, .12f, .965f, .79f);
            var art = OwnerRuntimeUiFactory.CreateImage("Backdrop", board, Color.white);
            Place(art.rectTransform, .50f, 0, 1, 1);
            art.preserveAspect = true;
            if (_postseasonBackdrop == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("UI/Generated/bg_owner_league_postseason_v1");
                if (texture != null)
                    _postseasonBackdrop = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(.5f, .5f), 100);
            }
            art.sprite = _postseasonBackdrop;
            art.raycastTarget = false;
            art.color = new Color(1, 1, 1, .42f);
            Label(board, "Status", PostseasonStatus(), .02f, .88f, .98f, .98f, 18, Ink, TextAnchor.MiddleLeft);

            if (_postseason == null)
            {
                Label(board, "Unavailable", "대진 정보를 불러오지 못했습니다.\n리그 메뉴를 다시 열어 주세요.",
                    .04f, .29f, .69f, .66f, 20, Ink);
            }
            else if (!_postseason.IsPostseasonInitialized || _postseason.Series.Count == 0)
            {
                Label(board, "PendingTitle", _postseason.IsPostseasonInitialized ? "포스트시즌 대진 준비 중" : "포스트시즌 대진 확정 전",
                    .04f, .49f, .69f, .66f, 28, Ink);
                Label(board, "PendingDescription", _postseason.IsPostseasonInitialized
                    ? "포스트시즌 경기를 진행하면 대진이 표시됩니다."
                    : "정규시즌 종료 후 진출 구단과 대진이 표시됩니다.\n페넌트레이스 탭에서 현재 순위를 확인하세요.",
                    .04f, .29f, .69f, .48f, 18, Ink);
            }
            else RenderBracket(board);

            Label(root, "Legend", "시리즈 승수 기준 · 구단을 선택하면 선수단을 확인합니다.",
                .035f, .025f, .78f, .095f, 14, Ink, TextAnchor.MiddleLeft);
            Label(root, "FocusLegend", "■ 내 구단", .8f, .025f, .965f, .095f, 14, Blue);
        }

        private string PostseasonStatus()
        {
            if (_postseason == null) return "포스트시즌 정보를 아직 불러오지 못했습니다.";
            if (!_postseason.IsPostseasonInitialized) return "페넌트레이스 진행 중 · 진출 구단 미확정";
            if (!string.IsNullOrEmpty(_postseason.ChampionTeamSeasonKey)) return "포스트시즌 종료 · 우승 구단 확정";
            return _postseason.IsQualified ? "포스트시즌 진행 중 · 내 구단 진출" : "포스트시즌 진행 중 · 내 구단 미진출";
        }

        private void RenderBracket(RectTransform board)
        {
            var rounds = new[] { OwnerPostseasonRound.WildCard, OwnerPostseasonRound.SemiPlayoff,
                OwnerPostseasonRound.Playoff, OwnerPostseasonRound.Championship };
            var titles = new[] { "와일드카드 · 4위 vs 5위", "준플레이오프 · 3위 합류", "플레이오프 · 2위 합류", "한국시리즈 · 1위 합류" };
            var names = new[] { "WildCard", "SemiPlayoff", "Playoff", "Championship" };
            int firstRound = Mathf.Max(0, 5 - _postseason.TeamCount);
            OwnerPostseasonSeriesReview previous = null;
            for (int index = firstRound; index < rounds.Length; index++)
            {
                OwnerPostseasonSeriesReview current = null;
                foreach (var series in _postseason.Series)
                    if (series.Round == rounds[index]) current = series;
                float left = index % 2 == 0 ? .02f : .53f;
                float bottom = index < 2 ? .55f : .18f;
                string higher = SeedTeamKey(4 - index);
                string lower = index == firstRound ? SeedTeamKey(5 - index) : Winner(previous);
                RenderSeries(board, names[index], titles[index], current, left, bottom, left + .45f, bottom + .29f,
                    higher, lower, rounds[index]);
                previous = current;
            }
            if (firstRound == 0) DrawConnection(board, "WildCardPath", .47f, .695f, .53f, .695f, FindRound(OwnerPostseasonRound.WildCard)?.IsCompleted == true);
            if (firstRound <= 1)
            {
                Color path = FindRound(OwnerPostseasonRound.SemiPlayoff)?.IsCompleted == true ? Blue : Grid;
                Surface(board, "SemiPlayoffExit", path, .755f, .51f, .755f, .55f).sizeDelta = new Vector2(3, 0);
                Surface(board, "SemiPlayoffPath", path, .245f, .51f, .755f, .51f).sizeDelta = new Vector2(0, 3);
                Surface(board, "PlayoffEntry", path, .245f, .47f, .245f, .51f).sizeDelta = new Vector2(3, 0);
            }
            if (firstRound <= 2) DrawConnection(board, "PlayoffPath", .47f, .325f, .53f, .325f, FindRound(OwnerPostseasonRound.Playoff)?.IsCompleted == true);
            RectTransform champion = Surface(board, "Champion", Color.white, .53f, .01f, .98f, .14f);
            Surface(champion, "GoldRule", CareerUiTheme.AccentGold, 0, .94f, 1, 1);
            Label(champion, "Title", "포스트시즌 우승", .02f, .10f, .34f, .88f, 17, Ink);
            RectTransform winnerHost = OwnerRuntimeUiFactory.CreateRect("WinnerHost", champion);
            Place(winnerHost, .35f, .04f, .99f, .91f);
            RenderBracketTeam(winnerHost, "Winner", _postseason.ChampionTeamSeasonKey, "우승 구단 대기", "", .02f, .98f, true);
        }

        private OwnerPostseasonSeriesReview FindRound(OwnerPostseasonRound round)
        {
            foreach (var series in _postseason.Series) if (series.Round == round) return series;
            return null;
        }

        private string SeedTeamKey(int rank) => rank > 0 && rank <= _model.Standings.Count
            ? _model.Standings[rank - 1].Id : null;

        private void RenderSeries(RectTransform board, string name, string title, OwnerPostseasonSeriesReview series,
            float x0, float y0, float x1, float y1, string pendingHigher = null, string pendingLower = null,
            OwnerPostseasonRound round = OwnerPostseasonRound.Championship)
        {
            RectTransform card = Surface(board, name, Grid, x0, y0, x1, y1);
            RectTransform content = Surface(card, "ContentSafeRect", Color.white, .008f, .016f, .992f, .984f);
            Label(content, "Title", title, .04f, .78f, .96f, .98f, 17, Blue, TextAnchor.MiddleLeft);
            int games = OwnerPostseasonState.GetSeriesGames(round);
            string status = series == null ? "앞선 라운드 승자 대기 · " + games + "전 " + (games / 2 + 1) + "선승제" :
                (series.IsCompleted ? "종료" : series.HigherSeedWins + series.LowerSeedWins == 0 ? "경기 예정" : "진행 중")
                + " · " + series.SeriesRule;
            Label(content, "Status", status, .04f, .58f, .96f, .78f, 14, Ink, TextAnchor.MiddleLeft);
            string winner = Winner(series);
            RenderBracketTeam(content, "HigherSeed", series?.HigherSeedTeamSeasonKey ?? pendingHigher, "상위 시드 대기",
                SeriesRecord(series, true), .30f, .57f, winner != null && winner == series?.HigherSeedTeamSeasonKey);
            RenderBracketTeam(content, "LowerSeed", series?.LowerSeedTeamSeasonKey ?? pendingLower, "앞선 라운드 승자",
                SeriesRecord(series, false), .02f, .29f, winner != null && winner == series?.LowerSeedTeamSeasonKey);
        }

        private void RenderBracketTeam(RectTransform parent, string name, string key, string placeholder, string record,
            float bottom, float top, bool winner)
        {
            OwnerLeaguePresentationModel.TeamRecord team = FindTeam(key);
            RectTransform row = Surface(parent, name, team != null && team.Id == _model.FocusTeamId ? Focus : Color.white,
                .025f, bottom, .975f, top);
            Label(row, "Name", team?.Name ?? (string.IsNullOrEmpty(key) ? placeholder : "구단 정보 없음"),
                .16f, .06f, string.IsNullOrEmpty(record) ? .97f : .72f, .94f, 16, Ink, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(record))
                Label(row, "Record", record, .73f, .06f, .98f, .94f, 14, winner ? Blue : Ink);
            if (team == null) return;
            AddEmblem(row, team.EmblemTeamName, team.EmblemId, .01f, .14f);
            Image background = row.GetComponent<Image>();
            background.raycastTarget = true;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => { _selectedTeamButton = button; TeamSelected?.Invoke(team.Id); });
        }

        private OwnerLeaguePresentationModel.TeamRecord FindTeam(string key)
        {
            foreach (var team in _model.Standings)
                if (team.Id == key) return team;
            return null;
        }

        private static string Winner(OwnerPostseasonSeriesReview series) => series?.IsCompleted == true
            ? series.WinnerTeamSeasonKey
            : null;

        private static string SeriesRecord(OwnerPostseasonSeriesReview series, bool higher)
        {
            if (series == null) return "";
            int wins = higher ? series.HigherSeedWins : series.LowerSeedWins;
            if (!series.IsCompleted) return wins + "승";
            return wins + "승" + (series.Draws > 0 ? " " + series.Draws + "무" : "") + " · " + ((higher ? series.HigherSeedTeamSeasonKey : series.LowerSeedTeamSeasonKey) == series.WinnerTeamSeasonKey
                ? (series.Round == OwnerPostseasonRound.Championship ? "우승" : "진출") : "탈락");
        }

        private void DrawConnection(RectTransform board, string name, float startX, float startY,
            float endX, float endY, bool completed)
        {
            Color color = completed ? Blue : Grid;
            float middle = (startX + endX) * .5f;
            Surface(board, name + "Start", color, startX, startY, middle, startY).sizeDelta = new Vector2(0, 3);
            Surface(board, name + "Turn", color, middle, Mathf.Min(startY, endY), middle, Mathf.Max(startY, endY))
                .sizeDelta = new Vector2(3, 0);
            Surface(board, name + "End", color, middle, endY, endX, endY).sizeDelta = new Vector2(0, 3);
        }
    }
}

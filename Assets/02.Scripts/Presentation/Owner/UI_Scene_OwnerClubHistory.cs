using System;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>시즌 전적에서 통산·최고 기록·트로피와 달성 시즌으로 이어지는 구단 기록실이다.</summary>
    public sealed partial class UI_Scene_OwnerClubHistory : MonoBehaviour
    {
        private static readonly string[] TabNames = { "시즌 전적", "통산 성적", "최고 기록", "트로피룸" };
        private static readonly string[] HonorNames = { "페넌트레이스 우승", "포스트시즌 우승", "포스트시즌 준우승" };
        private readonly Button[] _tabs = new Button[4];
        private readonly Button[] _grades = new Button[11];
        private readonly Button[] _honors = new Button[3];
        private Text _title, _description;
        private RectTransform _tableHost, _trophies;
        private RectTransform _seasonSummary;
        private readonly Text[] _summaryValues = new Text[4];
        private RecordTableView _table;
        private Button _back, _players;
        private OwnerClubHistoryPresentationModel _model;
        private OwnerClubHistorySeason _detail;
        private string _returnRow;
        private int _tab, _honor = -1;
        private Vector2 _returnScroll;
        private LeagueGrade? _grade;
        public event Action<int> SeasonPlayersRequested;

        /// <summary>기존 공용 셸 Workspace에 기록실을 한 번 생성한다.</summary>
        public static UI_Scene_OwnerClubHistory CreateRuntime(Transform parent)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerClubHistory), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Scene_OwnerClubHistory>();
            view.Build(); return view;
        }

        /// <summary>새 Snapshot에서도 기존 탭·등급 선택을 유지한다.</summary>
        public void Bind(OwnerClubHistoryPresentationModel model)
        { _model = model ?? throw new ArgumentNullException(nameof(model)); _detail = null; Render(); }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>시즌 상세에서 이전 기록표와 선택 행으로 돌아간다.</summary>
        public bool TryGoBack()
        {
            if (_detail == null) return false;
            _detail = null; Render();
            if (!string.IsNullOrEmpty(_returnRow)) _table.TrySelectRow(_returnRow, true);
            _table.ScrollRect.normalizedPosition = _returnScroll;
            if (!_table.FocusSelectedRow()) _tabs[_tab].Select(); return true;
        }

        private void OpenSeason(string row)
        {
            if (_tab == 1 || _detail != null) return;
            _detail = _model.FindSeason(row);
            if (_detail == null) return;
            _returnRow = row; _returnScroll = _table.ScrollRect.normalizedPosition; Render(); _back.Select();
        }

        private void Render()
        {
            if (_model == null) return;
            for (int i = 0; i < _tabs.Length; i++) SetSelected(_tabs[i], _tab == i);
            for (int i = 0; i < _grades.Length; i++) SetSelected(_grades[i], i == (_grade.HasValue ? (int)_grade.Value + 1 : 0));
            _back.gameObject.SetActive(_detail != null);
            _players.gameObject.SetActive(_detail != null);
            _trophies.gameObject.SetActive(_tab == 3 && _detail == null);
            UpdateTableBounds();
            RecordTableModel table;
            string empty;
            if (_detail != null)
            {
                string postseason = _detail.Postseason == OwnerTeamPostseasonResult.Champion ? "포스트시즌 우승" :
                    _detail.Postseason == OwnerTeamPostseasonResult.RunnerUp ? "포스트시즌 준우승" :
                    _detail.Postseason == OwnerTeamPostseasonResult.WildCardElimination ? "와일드카드 탈락" :
                _detail.Postseason == OwnerTeamPostseasonResult.SemiPlayoffElimination ? "준플레이오프 탈락" :
                _detail.Postseason == OwnerTeamPostseasonResult.PlayoffElimination ? "플레이오프 탈락" :
                _detail.Postseason == OwnerTeamPostseasonResult.SemifinalElimination ? "4강 탈락" :
                    _detail.Postseason == OwnerTeamPostseasonResult.DidNotQualify ? "포스트시즌 미진출" :
                    _detail.Row.IsHighlighted ? "포스트시즌 결과 미확정" : "포스트시즌 기록 없음";
                _title.text = _detail.Label + " · " + OwnerLeagueDisplayNameFormatter.FormatFull(_detail.Grade);
                _description.text = (_detail.IsCompleted ? "정규시즌 완료" : "정규시즌 진행 중") + " · " + postseason +
                    (_detail.HasPennant ? " · 페넌트레이스 우승" : "") + "\n선수 기록 보기에서 이 시즌의 리그 전체 기록을 확인합니다.";
                table = new OwnerClubHistoryPresentationModel(_model.TeamName, new[] { _detail }, _model.BuildSeasons(null).Columns).BuildTotals(null);
                empty = "시즌 기록이 없습니다.";
            }
            else
            {
                _title.text = _model.TeamName + " · " + TabNames[_tab];
                _description.text = _tab == 2 ? "완료된 정규시즌 · 비율 기록은 규정 충족자 · 공동 최고 기록 모두 표시 · 행을 눌러 달성 시즌 확인" :
                    _tab == 3 ? "우리 구단이 쌓아 온 영광 · 트로피를 선택하면 아래에서 달성 시즌을 확인합니다." :
                    _tab == 1 ? "현재 시즌 포함 · 승률은 무승부 제외 · 타율과 평균자책점은 전체 원본 기록으로 계산" :
                "역대 시즌 성적";
                table = _tab == 1 ? _model.BuildTotals(_grade) : _tab == 2 ? _model.BuildBest(_grade) :
                    _tab == 3 ? _model.BuildHonorSeasons(_grade, _honor) : _model.BuildSeasons(_grade);
                empty = _tab == 2 ? "완료된 시즌의 기록이 없습니다. 정규시즌을 마치면 최고 기록이 등록됩니다." :
                    _tab == 3 ? "이 리그에서 획득한 타이틀이 없습니다. 새로운 우승의 역사를 만들어 보세요." : "선택한 리그에서 운영한 시즌이 없습니다.";
            }
            RenderTrophies();
            RenderSeasonSummary();
            _table.AllowRowActivation = _tab != 1 && _detail == null;
            _table.HighlightBadge = "현재 시즌";
            _table.Bind(table, table.Rows.Count == 0 ? UiContentStateModel.CreateEmpty("기록 없음", empty) : UiContentStateModel.Ready);
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            UIOwnerFrontOfficePanel.ApplyWorkspace(root);
            Image navigation = OwnerRuntimeUiFactory.CreateImage("HistoryNavigation", root, OwnerDashboardStyle.TableSurface);
            Place(navigation.rectTransform, .025f, .755f, .975f, .905f);
            OwnerDashboardStyle.ApplyInset(navigation);
            OwnerDashboardStyle.Rule(root, "HistoryFilterRule", new Vector2(.03f, .83f), new Vector2(.97f, .83f),
                Vector2.zero, new Vector2(0, 1), OwnerDashboardStyle.Line);
            _title = Text("Title", root, 21, FontStyle.Bold); Place(_title.rectTransform, .03f, .91f, .70f, .99f);
            for (int i = 0; i < _tabs.Length; i++)
            {
                int captured = i;
                _tabs[i] = Button("Tab" + i, root, TabNames[i], () => { _tab = captured; _detail = null; Render(); });
                Place((RectTransform)_tabs[i].transform, .03f + i * .235f, .835f, .03f + (i + 1) * .235f - .005f, .90f);
            }
            for (int i = 0; i < _grades.Length; i++)
            {
                int captured = i;
                string label = i == 0 ? "전체" : OwnerLeagueDisplayNameFormatter.FormatFull((LeagueGrade)(i - 1)).Replace(" 리그", "");
                _grades[i] = Button("Grade" + i, root, label, () => { _grade = captured == 0 ? null : (LeagueGrade?)(captured - 1); _detail = null; Render(); });
                Place((RectTransform)_grades[i].transform, .03f + i * .08545f, .765f, .03f + (i + 1) * .08545f - .003f, .825f);
            }
            _description = Text("Description", root, 13, FontStyle.Normal); Place(_description.rectTransform, .03f, .705f, .97f, .763f);
            _seasonSummary = OwnerRuntimeUiFactory.CreateRect("SeasonSummary", root);
            Place(_seasonSummary, .03f, .605f, .97f, .70f);
            string[] captions = { "운영 시즌", "정규시즌 완료", "페넌트레이스 우승", "포스트시즌 우승" };
            for (int index = 0; index < captions.Length; index++)
            {
                var card = OwnerRuntimeUiFactory.CreateImage("Summary" + index, _seasonSummary, OwnerDashboardStyle.InsetSurface);
                Place(card.rectTransform, index * .25f, 0, (index + 1) * .25f - .008f, 1);
                OwnerDashboardStyle.ApplyInset(card);
                var caption = Text("Caption", card.transform, 13, FontStyle.Normal);
                caption.text = captions[index]; Place(caption.rectTransform, .05f, .52f, .95f, .94f);
                _summaryValues[index] = Text("Value", card.transform, 24, FontStyle.Bold);
                Place(_summaryValues[index].rectTransform, .05f, .04f, .95f, .54f);
            }
            _tableHost = OwnerRuntimeUiFactory.CreateRect("HistoryTableHost", root);
            _table = RecordTableView.CreateRuntime(_tableHost, "HistoryTable");
            _table.RowHeight = 56f;
            _table.SetVisualStyle(RecordTableVisualStyle.OwnerFrontOffice); _table.RowSelected += OpenSeason;
            BuildTrophies(root);
            _back = Button("BackToHistory", root, "기록 목록으로", () => TryGoBack());
            Place((RectTransform)_back.transform, .71f, .92f, .83f, .985f);
            _players = Button("SeasonPlayers", root, "선수 기록 보기", () => { if (_detail != null) SeasonPlayersRequested?.Invoke(_detail.Number); });
            Place((RectTransform)_players.transform, .84f, .92f, .97f, .985f);
        }

        private static Text Text(string name, Transform parent, int size, FontStyle style)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, string.Empty, size, style, TextAnchor.MiddleLeft, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetDataText(text, style == FontStyle.Bold);
            return text;
        }
        private static Button Button(string name, Transform parent, string label, UnityEngine.Events.UnityAction action)
        { var button = OwnerRuntimeUiFactory.CreateReferenceButton(name, parent, label, 14); button.onClick.AddListener(action); return button; }
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1) =>
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
        private static void SetSelected(Button button, bool selected)
        {
            OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
            OwnerUiButtonSkin.SetSelected(button, selected);
        }
        private void OnRectTransformDimensionsChange()
        {
            if (_tableHost != null) UpdateTableBounds();
        }
        private void UpdateTableBounds()
        {
            bool isTrophyRoom = _tab == 3 && _detail == null;
            Place(_tableHost, .03f, .06f, .97f, isTrophyRoom ? .25f : _tab == 0 && _detail == null ? .59f : .70f);
            if (isTrophyRoom && _trophies != null)
            {
                float trophyWidth = Mathf.Min(((RectTransform)transform).rect.width * .94f, TrophyRoomMaximumWidth);
                _trophies.anchorMin = new Vector2(.5f, .27f);
                _trophies.anchorMax = new Vector2(.5f, .70f);
                _trophies.offsetMin = new Vector2(-trophyWidth * .5f, 0f);
                _trophies.offsetMax = new Vector2(trophyWidth * .5f, 0f);
                _tableHost.anchorMin = new Vector2(.5f, .06f);
                _tableHost.anchorMax = new Vector2(.5f, .25f);
                _tableHost.offsetMin = new Vector2(-trophyWidth * .5f, 0f);
                _tableHost.offsetMax = new Vector2(trophyWidth * .5f, 0f);
            }
            if (_tab != 1 && _detail == null) return;
            // 항목/값 두 열은 울트라와이드에서 서로 멀어지지 않도록 읽기 폭을 제한한다.
            float width = Mathf.Min(((RectTransform)transform).rect.width * .94f, 1120f);
            _tableHost.anchorMin = new Vector2(.5f, .06f);
            _tableHost.anchorMax = new Vector2(.5f, .70f);
            _tableHost.offsetMin = new Vector2(-width * .5f, 0f);
            _tableHost.offsetMax = new Vector2(width * .5f, 0f);
        }
        private void RenderSeasonSummary()
        {
            _seasonSummary.gameObject.SetActive(_tab == 0 && _detail == null);
            int count = 0, completed = 0, pennants = 0, champions = 0;
            foreach (var season in _model.Seasons)
            {
                if (_grade.HasValue && season.Grade != _grade.Value) continue;
                count++;
                if (season.IsCompleted) completed++;
                if (season.HasPennant) pennants++;
                if (season.Postseason == OwnerTeamPostseasonResult.Champion) champions++;
            }
            _summaryValues[0].text = count + "시즌";
            _summaryValues[1].text = completed + "시즌";
            _summaryValues[2].text = pennants + "회";
            _summaryValues[3].text = champions + "회";
        }
        private void OnDestroy() { if (_table != null) _table.RowSelected -= OpenSeason; SeasonPlayersRequested = null; }
    }
}

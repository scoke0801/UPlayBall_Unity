using System.Collections.Generic;
using System.Globalization;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPregame
    {
        private static readonly Color Paper = OwnerDashboardStyle.TableSurface;
        private static readonly Color Ink = OwnerDashboardStyle.Ivory;
        private static readonly Color Rule = OwnerDashboardStyle.Line;
        private static readonly Color OwnBlue = CareerUiTheme.RosterAccent;
        private static readonly Color OpponentRed = CareerUiTheme.CardStatRed;
        private readonly Text[] _teamNames = new Text[2];
        private readonly Text[] _teamMonograms = new Text[2];
        private readonly Image[] _teamEmblems = new Image[2];
        private readonly Text[] _teamSides = new Text[2];
        private readonly Text[] _teamRecent = new Text[2];
        private readonly Text[][] _teamRecords = new Text[2][];
        private readonly RectTransform[] _rosterTables = new RectTransform[2];
        private readonly Button[,] _recordTabs = new Button[2, 2];
        private readonly bool[] _showPitchers = new bool[2];
        private readonly PlayerMiniCardView[] _starterCards = new PlayerMiniCardView[2];
        private readonly OwnerCollectionCardSnapshot[] _starterDetails = new OwnerCollectionCardSnapshot[2];
        private Text _analysisLeague;
        private Text _analysisMatch;
        private Text _analysisIntel;
        private Text _ownStarter;
        private Text _opponentStarter;
        private Text _scoutingNotes;
        private UIOpponentRadar _radar;
        private RectTransform _analysisBoard;

        private void BuildAnalysisBoard()
        {
            RectTransform frame = Surface(_workspaceRoot, "AnalysisFrame", Paper, 0, 0, 1, 1);
            UIOwnerFrontOfficePanel.Apply(frame, "ManagerReport");
            _analysisBoard = frame;
            RectTransform board = Rect(frame, "AnalysisContent", 0, 0, 1, 1);
            OwnerRuntimeUiFactory.Stretch(board, new Vector2(20, 16), new Vector2(-20, -16));
            _analysisLeague = Label(board, "League", "", 12, .02f, .915f, .38f, .97f);
            _analysisMatch = Label(board, "Match", "", 12, .62f, .915f, .98f, .97f, TextAnchor.MiddleRight);
            Label(board, "Title", "경기 전력 비교", 20, .38f, .925f, .62f, .985f, TextAnchor.MiddleCenter, true);
            for (int side = 0; side < 2; side++)
            {
                float left = side == 0 ? .02f : .645f;
                float right = side == 0 ? .355f : .98f;
                Color accent = side == 0 ? OwnBlue : OpponentRed;
                RectTransform team = Surface(board, "Team" + side, Paper, left, .625f, right, .90f);
                OwnerDashboardStyle.ApplyInset(team.GetComponent<Image>());
                RectTransform badge = Surface(team, "TeamBadge", OwnerDashboardStyle.TableHeader,
                    side == 0 ? 0 : .76f, .15f, side == 0 ? .24f : 1, .9f);
                Surface(badge, "AccentRail", accent, side == 0 ? 0 : .965f, 0, side == 0 ? .035f : 1, 1);
                _teamEmblems[side] = Rect(badge, "TeamEmblem", .10f, .20f, .90f, .96f).gameObject.AddComponent<Image>();
                _teamEmblems[side].color = Color.white;
                _teamEmblems[side].preserveAspect = true;
                _teamEmblems[side].raycastTarget = false;
                _teamMonograms[side] = Label(badge, "EmblemFallback", "?", 28, .10f, .20f, .90f, .96f,
                    TextAnchor.MiddleCenter, true, accent);
                Surface(badge, "ClubMarkSurface", OwnerDashboardStyle.TableSurface, .035f, 0, .965f, .18f);
                Label(badge, "ClubMark", side == 0 ? "우리 구단" : "상대 구단", 10, .035f, 0, .965f, .18f,
                    TextAnchor.MiddleCenter, true, accent);
                float textLeft = side == 0 ? .27f : 0;
                float textRight = side == 0 ? 1 : .73f;
                _teamNames[side] = Label(team, "TeamName", "", 16, textLeft, .72f, textRight, .91f, TextAnchor.MiddleLeft, true);
                _teamRecent[side] = Label(team, "RecentForm", "", 11, textLeft, .51f, textRight, .72f);
                RectTransform marker = Surface(team, "HomeAway", OwnerDashboardStyle.TableHeader, side == 0 ? .80f : 0, .91f, side == 0 ? 1 : .20f, 1);
                _teamSides[side] = Label(marker, "Side", "", 12, 0, 0, 1, 1, TextAnchor.MiddleCenter, true, accent);
                RectTransform records = Surface(team, "SeasonRecord", OwnerDashboardStyle.TableHeader, textLeft, .08f, textRight, .5f);
                CreateCells(records, new[] { "경기", "승", "패", "무", "승률" }, .52f, 1, accent, true);
                _teamRecords[side] = CreateCells(records, new[] { "—", "—", "—", "—", "—" }, 0, .52f, Ink, false);
            }
            RectTransform chart = Surface(board, "RadarPaper", OwnerDashboardStyle.InsetSurface, .37f, .615f, .63f, .915f);
            Label(chart, "ChartTitle", "로스터 기본 능력 비교", 11, 0, .86f, 1, 1, TextAnchor.MiddleCenter, true);
            RectTransform radarRect = Rect(chart, "Radar", .20f, .14f, .80f, .82f);
            _radar = radarRect.gameObject.AddComponent<UIOpponentRadar>();
            _radar.raycastTarget = false;
            _radar.SetPalette(OwnBlue, OpponentRed, OwnerDashboardStyle.Line);
            Label(chart, "Contact", "정확", 10, .37f, .76f, .63f, .9f, TextAnchor.MiddleCenter);
            Label(chart, "Power", "장타", 10, .76f, .51f, 1, .68f, TextAnchor.MiddleCenter);
            Label(chart, "Speed", "주력", 10, .63f, .03f, .90f, .20f, TextAnchor.MiddleCenter);
            Label(chart, "Defense", "수비", 10, .10f, .03f, .37f, .20f, TextAnchor.MiddleCenter);
            Label(chart, "Control", "제구", 10, 0, .51f, .24f, .68f, TextAnchor.MiddleCenter);
            _analysisIntel = Label(board, "Confidence", "", 10, .3f, .567f, .7f, .615f, TextAnchor.MiddleCenter);
            Surface(board, "StarterRule", Rule, .02f, .555f, .98f, .558f);
            _ownStarter = CreateStarter(board, "OwnStarter", .02f, .465f, OwnBlue, 0);
            _opponentStarter = CreateStarter(board, "OpponentStarter", .535f, .98f, OpponentRed, 1);
            Label(board, "Versus", "VS", 28, .465f, .38f, .535f, .55f, TextAnchor.MiddleCenter, true, Rule);
            for (int side = 0; side < 2; side++)
            {
                int teamIndex = side;
                RectTransform section = Rect(board, "RosterSection" + side, side == 0 ? .02f : .52f, .09f, side == 0 ? .48f : .98f, .38f);
                for (int tab = 0; tab < 2; tab++)
                {
                    bool pitchers = tab == 1;
                    RectTransform buttonRect = Rect(section, "RecordTab" + tab, tab * .5f, .80f, (tab + 1) * .5f, 1);
                    buttonRect.gameObject.AddComponent<Image>();
                    Button button = buttonRect.gameObject.AddComponent<Button>();
                    button.targetGraphic = buttonRect.GetComponent<Image>();
                    button.targetGraphic.raycastTarget = true;
                    Label(buttonRect, "Label", pitchers ? "투수 정보" : "야수 정보", 12, 0, 0, 1, 1, TextAnchor.MiddleCenter, true);
                    OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab);
                    button.onClick.AddListener(() =>
                    {
                        if (_showPitchers[teamIndex] == pitchers) return;
                        _showPitchers[teamIndex] = pitchers;
                        RenderRosterTable(teamIndex);
                    });
                    _recordTabs[side, tab] = button;
                }
                ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("RosterScroll" + side, section, out RectTransform content);
                Place(scroll.GetComponent<RectTransform>(), 0, 0, 1, .78f);
                OwnerDashboardStyle.SetDataSurface(scroll.GetComponent<Image>(), Paper, true);
                VerticalLayoutGroup tableLayout = content.GetComponent<VerticalLayoutGroup>();
                tableLayout.spacing = 0;
                tableLayout.padding = new RectOffset(1, 1, 1, 1);
                _rosterTables[side] = content;
            }
            Surface(board, "FooterRule", Rule, .02f, .075f, .98f, .078f);
            _scoutingNotes = Label(board, "ScoutingNotes", "", 11, .025f, .01f, .975f, .07f);
        }

        private Text CreateStarter(RectTransform board, string name, float left, float right, Color accent, int side)
        {
            RectTransform panel = Surface(board, name, Paper, left, .395f, right, .55f);
            OwnerDashboardStyle.ApplyInset(panel.GetComponent<Image>());
            RectTransform root = Rect(panel, "Content", .015f, .05f, .985f, .95f);
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(root, "PitcherCard");
            card.UseLineupSlotLayout();
            card.DetailRequested += _ => ShowStarterDetail(side);
            Place((RectTransform)card.transform, 0, 0, .15f, 1);
            _starterCards[side] = card;
            Label(root, "StarterTitle", "선발 투수 정보", 12, .18f, .70f, 1, 1, TextAnchor.MiddleLeft, true, accent);
            Text value = Label(root, "StarterValue", "", 12, .18f, .48f, 1, .7f);
            RectTransform record = Rect(root, "PitchingRecord", .18f, 0, 1, .47f);
            CreateCells(record, new[] { "평균자책점", "경기", "승", "패", "이닝", "삼진", "피안타" }, .5f, 1, accent, true);
            CreateCells(record, new[] { "—", "—", "—", "—", "—", "—", "—" }, 0, .5f, Ink, false);
            return value;
        }

        private void RenderAnalysisBoard()
        {
            var snapshot = _model.Snapshot;
            _analysisBoard.gameObject.SetActive(snapshot.ContentState.Kind == Baseball.Presentation.SharedUI.UiContentStateKind.Ready);
            _analysisLeague.text = snapshot.ResolveText("analysis.league", "리그 정보 없음");
            _analysisMatch.text = snapshot.NextMatchText;
            _teamNames[0].text = snapshot.ResolveText("analysis.own.name", "우리 구단");
            _teamNames[1].text = snapshot.OpponentName;
            int[] emblemIds = { snapshot.OwnTeamEmblemId, snapshot.OpponentTeamEmblemId };
            // 내 구단은 구단주가 붙인 이름이 아니라 원본 Identity 구단명으로 엠블렘을 찾는다.
            string[] emblemNames =
            {
                snapshot.ResolveText("analysis.own.emblemName", _teamNames[0].text),
                _teamNames[1].text
            };
            for (int side = 0; side < 2; side++)
            {
                bool hasEmblem = TeamEmblemSprites.TryApply(_teamEmblems[side], emblemIds[side], emblemNames[side]);
                _teamEmblems[side].gameObject.SetActive(hasEmblem);
                _teamMonograms[side].gameObject.SetActive(!hasEmblem);
            }
            var values = new float[2][];
            for (int side = 0; side < 2; side++)
            {
                string prefix = "analysis." + (side == 0 ? "own" : "opponent");
                _teamSides[side].text = snapshot.ResolveText(prefix + ".side", "—");
                _teamRecent[side].text = snapshot.ResolveText(prefix + ".recent", side == 0 ? "최근 전적 미집계" : _model.RecentFormText);
                string[] record = snapshot.ResolveText(prefix + ".record", "—|—|—|—|—").Split('|');
                for (int cell = 0; cell < _teamRecords[side].Length; cell++)
                    _teamRecords[side][cell].text = cell < record.Length ? record[cell] : "—";
                values[side] = new float[5];
                for (int axis = 0; axis < 5; axis++)
                    values[side][axis] = float.TryParse(snapshot.ResolveText(prefix + ".axis" + axis), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float value) ? value : float.NaN;
                RenderRosterTable(side);
            }
            _radar.Bind(values[0], values[1]);
            _analysisIntel.text = "정보 신뢰도  " + _model.IntelText;
            _ownStarter.text = snapshot.ResolveText("analysis.own.starter", "선발 확정 정보 없음");
            _opponentStarter.text = _model.ProbableStarterText;
            BindStarterCard(0, snapshot.OwnStarterCard, snapshot.OwnStarterDetail);
            BindStarterCard(1, snapshot.OpponentStarterCard, snapshot.OpponentStarterDetail);
            _scoutingNotes.text = "감독 성향  " + _model.ManagerTendencyText + "   |   " + string.Join(" · ", _model.KeyThreats);
        }

        private void BindStarterCard(
            int side,
            PlayerMiniCardModel card,
            OwnerCollectionCardSnapshot detail)
        {
            _starterDetails[side] = detail;
            PlayerMiniCardModel display = card ?? new PlayerMiniCardModel(
                "pregame:unknown-starter:" + side,
                "확인 불가",
                "선발",
                "—",
                string.Empty,
                string.Empty,
                "정보 부족",
                visualState: PlayerMiniCardVisualState.Disabled,
                isInteractable: false);
            _starterCards[side].Bind(
                display,
                PlayerPortraitSprites.GetDefault(
                    detail?.Position ?? Baseball.Core.Players.PlayerPosition.StartingPitcher));
            _starterCards[side].SetPrimaryClickForDetail(detail != null);
        }

        private void ShowStarterDetail(int side)
        {
            OwnerCollectionCardSnapshot detail = _starterDetails[side];
            ShowCardDetail(detail);
        }

        /// <summary>Canvas 아래의 실제 Workspace를 기준으로 선수 카드 상세를 연다.</summary>
        private void ShowCardDetail(OwnerCollectionCardSnapshot detail)
        {
            if (detail != null) UI_Popup_OwnerPlayerCard.Show(_workspaceRoot, detail);
        }

        private void RenderRosterTable(int side)
        {
            if (_model == null) return;
            RectTransform content = _rosterTables[side];
            content.anchoredPosition = Vector2.zero;
            for (int index = content.childCount - 1; index >= 0; index--)
            {
                GameObject child = content.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            bool pitchers = _showPitchers[side];
            Color accent = side == 0 ? OwnBlue : OpponentRed;
            for (int tab = 0; tab < 2; tab++)
            {
                bool selected = (tab == 1) == pitchers;
                OwnerUiButtonSkin.SetSelected(_recordTabs[side, tab], selected);
            }
            AddTableRow(content, new[] { "선수", "포지션", "컨디션 / 관측" }, accent, true, 0);
            IReadOnlyList<OwnerPregameRosterRowModel> rows = _model.GetRosterRows(side == 0, pitchers);
            for (int index = 0; index < rows.Count; index++)
            {
                OwnerPregameRosterRowModel row = rows[index];
                RectTransform rowRect = AddTableRow(
                    content,
                    new[] { row.NameText, row.PositionText, row.StatusText },
                    Ink,
                    false,
                    index);
                if (row.Detail == null) continue;
                OwnerCollectionCardSnapshot detail = row.Detail;
                UIRightClickDetailTrigger.Attach(rowRect, () => ShowCardDetail(detail));
            }
        }

        private static RectTransform AddTableRow(RectTransform content, string[] values, Color color, bool header, int index)
        {
            RectTransform row = Surface(content, header ? "TableHeader" : "PlayerRow" + index,
                header ? OwnerDashboardStyle.TableHeader : index % 2 == 0 ? Paper : OwnerDashboardStyle.TableAlternate, 0, 0, 1, 1);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = header ? 30 : 32;
            float[] bounds = { 0, .30f, .48f, 1 };
            for (int cell = 0; cell < 3; cell++)
            {
                Text label = Label(row, "Cell" + cell, values[cell], 11, bounds[cell] + .012f, 0, bounds[cell + 1] - .012f, 1,
                    TextAnchor.MiddleLeft, header, color);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            Surface(row, "RowRule", Rule, 0, 0, 1, .025f);
            return row;
        }

        private static Text[] CreateCells(RectTransform parent, string[] labels, float bottom, float top, Color color, bool bold)
        {
            var result = new Text[labels.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                float left = (float)index / labels.Length, right = (float)(index + 1) / labels.Length;
                result[index] = Label(parent, "Stat" + index, labels[index], 11, left, bottom, right, top, TextAnchor.MiddleCenter, bold, color);
            }
            Surface(parent, "HorizontalRule", Rule, 0, bottom, 1, bottom + .018f);
            return result;
        }

        private static Text Label(RectTransform parent, string name, string value, int size,
            float left, float bottom, float right, float top, TextAnchor alignment = TextAnchor.MiddleLeft,
            bool bold = false, Color? color = null)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size,
                bold ? FontStyle.Bold : FontStyle.Normal, alignment, color ?? Ink);
            OwnerDashboardStyle.SetDataText(text, bold);
            text.color = color ?? Ink;
            text.supportRichText = false;
            Place(text.rectTransform, left, bottom, right, top);
            return text;
        }

        private static RectTransform Surface(RectTransform parent, string name, Color color, float left, float bottom, float right, float top)
        {
            RectTransform rect = Rect(parent, name, left, bottom, right, top);
            Image image = rect.gameObject.AddComponent<Image>();
            rect.gameObject.AddComponent<Baseball.Presentation.UI.CareerUiVisualElement>()
                .Initialize(Baseball.Presentation.UI.CareerUiVisualRole.DataImage);
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform Rect(RectTransform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, left, bottom, right, top);
            return rect;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}

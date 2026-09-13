using System;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 정체성과 운영 지표를 공용 네이비 카드와 핵심 수치로 표시한다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerClubInformation : MonoBehaviour
    {
        private static readonly Color Paper = OwnerDashboardStyle.TableSurface;
        private static readonly Color White = OwnerDashboardStyle.Ivory;
        private static readonly Color Ink = OwnerDashboardStyle.Ivory;
        private static readonly Color Muted = OwnerDashboardStyle.Muted;
        private static readonly Color Grid = OwnerDashboardStyle.Line;
        private static readonly Color Blue = OwnerDashboardStyle.Gold;
        private static readonly Color Lime = OwnerDashboardStyle.Gold;
        private static readonly Color Gold = OwnerDashboardStyle.Gold;
        private OwnerClubInformationPresentationModel _model;
        private bool _showOwner = true;
        private Button _changeManagerButton;
        private Button _editMottoButton;
        private OwnerClubGuideCatalog _guideCatalog;
        private OwnerClubGuideCatalog.Entry _guide;
        public event Action EditMottoRequested;

        /// <summary>편집 종료 후 갱신된 한마디 수정 버튼으로 돌아간다.</summary>
        public void FocusMottoButton() => _editMottoButton?.Select();
        public event Action ChangeFrontManagerRequested;

        /// <summary>팝업 종료 후 다시 생성된 교체 버튼으로 포커스를 복원한다.</summary>
        public void FocusFrontManagerButton() => _changeManagerButton?.Select();

        /// <summary>공용 셸의 본문 슬롯에 구단 정보 화면을 생성한다.</summary>
        public static UI_Scene_OwnerClubInformation CreateRuntime(RectTransform host)
        {
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerClubInformation), host);
            UIOwnerFrontOfficePanel.ApplyWorkspace(root);
            return root.gameObject.AddComponent<UI_Scene_OwnerClubInformation>();
        }

        /// <summary>현재 Save에서 투영한 구단 정보로 화면을 갱신한다.</summary>
        public void Bind(OwnerClubInformationPresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (_guide != null && !_guide.IsEligible(_model)) _guide = null;
            if (gameObject.activeSelf) Render();
        }

        /// <summary>구단주 또는 구단 탭을 선택해 해당 레퍼런스 구성을 표시한다.</summary>
        public void ShowTab(bool showOwner, bool isEntering = true)
        {
            _showOwner = showOwner;
            if (showOwner && _model != null && (isEntering || _guide == null))
            {
                if (_guideCatalog == null) _guideCatalog = OwnerClubGuideCatalog.Load();
                _guide = _guideCatalog.Select(_model);
            }
            gameObject.SetActive(true);
            Render();
        }

        private void Render()
        {
            OwnerRuntimeUiFactory.ClearChildren(transform);
            if (_model == null) return;
            RectTransform root = (RectTransform)transform;
            var safe = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", root);
            OwnerRuntimeUiFactory.Stretch(safe, new Vector2(16, 16), new Vector2(-16, -16));
            root = safe;
            BuildIdentity(root);
            if (_showOwner) BuildOwnerInformation(root);
            else BuildClubInformation(root);
        }

        private void BuildIdentity(RectTransform root)
        {
            RectTransform card = Surface(root, "Identity", OwnerDashboardStyle.Surface, .025f, .69f, .49f, .955f);
            UIOwnerFrontOfficePanel.Apply(card, "ManagerReport");
            RectTransform emblemBox = Surface(card, "EmblemBox", Color.clear, .025f, .12f, .27f, .88f);
            Image emblem = OwnerRuntimeUiFactory.CreateImage("Emblem", emblemBox, Color.white);
            Place(emblem.rectTransform, .08f, .08f, .92f, .92f);
            TeamEmblemSprites.TryApply(emblem, 0, _model.EmblemTeamName);
            Label(card, "NameCaption", "구 단 명", .31f, .56f, .47f, .86f, 15, Lime, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(card, "TeamName", _model.TeamName, .47f, .56f, .96f, .86f, 22, White, TextAnchor.MiddleLeft, FontStyle.Bold);
            Surface(card, "Divider", Grid, .31f, .49f, .96f, .495f);
            Label(card, "LocationCaption", "연 고 지", .31f, .16f, .47f, .45f, 15, Lime, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(card, "Location", _model.LocationLabel, .47f, .16f, .96f, .45f, 17, White, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildOwnerInformation(RectTransform root)
        {
            RectTransform metrics = Panel(root, "Prestige", "구단 평판", .51f, .69f, .975f, .955f);
            Metric(metrics, "명성", _model.PopularityText, "현재 인기도 / 100", _model.Popularity, .04f, .48f);
            Metric(metrics, "팬 기반", _model.FanBaseText, "구단 지지 규모 / 100", _model.FanBase, .52f, .96f);

            RectTransform history = Panel(root, "OwnerHistory", "구단주 기록 정보", .025f, .08f, .49f, .665f);
            InfoBand(history, "구단주", _model.OwnerName, .82f);
            InfoBand(history, "소속 구단", _model.TeamName, .69f);
            Section(history, "현재 시즌 성적", .59f);
            SeasonHeadline(history, .43f, .59f);
            GridRow(history, .30f, new[] { "순위", "승률", "득점", "실점" }, new[]
            {
                RankText(), _model.WinningPercentage, _model.Runs.ToString(), _model.RunsAllowed.ToString()
            });
            HonorSlots(history);

            RectTransform manager = Panel(root, "FrontManager", "프런트 매니저", .51f, .08f, .975f, .665f);
            Image office = OwnerRuntimeUiFactory.CreateImage("Office", manager, Color.white);
            office.sprite = Resources.Load<Sprite>("UI/Generated/bg_owner_container_office_v2");
            office.type = Image.Type.Simple;
            office.preserveAspect = false;
            office.gameObject.AddComponent<RectMask2D>();
            Place(office.rectTransform, .02f, .28f, .98f, .87f);
            Image shade = OwnerRuntimeUiFactory.CreateImage("Shade", office.transform, new Color(0f, 0f, 0f, .38f));
            OwnerRuntimeUiFactory.Stretch(shade.rectTransform);
            Image portrait = OwnerRuntimeUiFactory.CreateImage("Portrait", office.transform, Color.white);
            if (_guideCatalog == null) _guideCatalog = OwnerClubGuideCatalog.Load();
            if (_guide == null) _guide = _guideCatalog.Select(_model);
            portrait.sprite = FrontManagerPortraitSprites.LoadForManager(_model.FrontManagerId, _guide.expression);
            portrait.preserveAspect = true;
            Place(portrait.rectTransform, .45f, .02f, 1f, .98f);
            RectTransform speech = Surface(office.rectTransform, "Speech", OwnerDashboardStyle.TableHeader, .04f, .50f, .59f, .91f);
            UIOwnerFrontOfficePanel.Apply(speech, "Speech");
            Label(speech, "Message", _guide.text,
                .10f, .18f, .83f, .82f, 15, Ink, TextAnchor.MiddleLeft);
            _changeManagerButton = OwnerWorkspaceUiFactory.CreateButton(manager, "ChangeFrontManager", "매니저 교체",
                () => ChangeFrontManagerRequested?.Invoke());
            Place((RectTransform)_changeManagerButton.transform, .65f, .17f, .96f, .265f);
            Label(manager, "Motto", "구단주의 한마디", .03f, .17f, .40f, .265f, 13, Blue, TextAnchor.MiddleLeft, FontStyle.Bold);
            var motto = Label(manager, "MottoText", _model.Motto, .03f, .015f, .79f, .155f, 15, Ink, TextAnchor.MiddleLeft);
            motto.supportRichText = false;
            _editMottoButton = OwnerWorkspaceUiFactory.CreateButton(manager, "EditMotto", "수정",
                () => EditMottoRequested?.Invoke());
            Place((RectTransform)_editMottoButton.transform, .82f, .025f, .96f, .145f);
        }

        private void BuildClubInformation(RectTransform root)
        {
            RectTransform roster = Panel(root, "Roster", "선수 구성 정보", .025f, .08f, .49f, .665f);
            InfoBand(roster, "보유 선수", _model.OwnedPlayerCount.ToString("N0"), .84f);
            InfoBand(roster, "1군 등록", _model.ActiveRosterText, .73f);
            InfoBand(roster, "구장", _model.StadiumText, .62f);
            GridRow(roster, .43f, new[] { "일반", "올스타", "골든글러브", "MVP" }, new[]
            {
                _model.NormalCardCount.ToString(), _model.AllStarCardCount.ToString(),
                _model.GoldenGloveCardCount.ToString(), _model.MvpCardCount.ToString()
            });
            Section(roster, "현재 구단 운영", .18f);
            GridRow(roster, .035f, new[] { "인기도", "팬 기반", "순위", "승률" }, new[]
            {
                _model.PopularityText, _model.FanBaseText, RankText(), _model.WinningPercentage
            });

            RectTransform season = Panel(root, "Season", "현재 리그 성적", .51f, .47f, .975f, .955f);
            InfoBand(season, "시즌 / 리그", _model.LeagueLabel, .78f);
            SeasonHeadline(season, .43f, .69f);
            GridRow(season, .22f, new[] { "순위", "승률", "득점", "실점" }, new[]
            {
                RankText(), _model.WinningPercentage, _model.Runs.ToString(), _model.RunsAllowed.ToString()
            });

            RectTransform previous = Panel(root, "Previous", "이전 리그 성적", .51f, .08f, .975f, .445f);
            var table = RecordTableView.CreateRuntime(previous, "PreviousSeasonsTable");
            Place((RectTransform)table.transform, .03f, .06f, .97f, .85f);
            table.AllowRowActivation = false;
            table.FitColumnsToViewport = true;
            table.SetVisualStyle(RecordTableVisualStyle.OwnerFrontOffice);
            table.Bind(_model.PreviousSeasons);
            if (_model.PreviousSeasons.Rows.Count == 0)
            {
                Label(previous, "HistoryEmptyTitle", _model.HasHistory ? "아직 이전 시즌 기록이 없습니다" : "시즌 기록을 불러오지 못했습니다",
                    .06f, .35f, .94f, .53f, 18, Ink);
                Label(previous, "HistoryEmptyDetail", _model.HasHistory ? "다음 시즌으로 넘어가면 이곳에 성적이 누적됩니다." : "구단 메뉴를 다시 열어 주세요.",
                    .06f, .18f, .94f, .34f, 14, Muted);
            }
        }

        private void Metric(RectTransform host, string title, string value, string note, float amount, float minX, float maxX)
        {
            RectTransform box = Surface(host, "Metric_" + title, Color.clear, minX, .08f, maxX, .84f);
            Label(box, "Title", title, .04f, .47f, .46f, .82f, 18, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(box, "Value", value, .47f, .44f, .95f, .84f, 32, Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            Label(box, "Note", note, .05f, .04f, .95f, .27f, 12, Muted);
            var gauge = UIOwnerMetricBar.Create(box, "ReputationGauge");
            Place((RectTransform)gauge.transform, .05f, .31f, .95f, .36f);
            gauge.Bind(amount, amount, 100);
        }

        private void SeasonHeadline(RectTransform parent, float bottom, float top)
        {
            Label(parent, "SeasonRecord", _model.Games == 0 ? "시즌 개막을 기다리고 있습니다" : $"{_model.Wins}승  {_model.Losses}패",
                .05f, bottom + .05f, .95f, top, _model.Games == 0 ? 20 : 32, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(parent, "SeasonRecordDetail", $"{_model.Games}경기 · {_model.Ties}무 · 승률 {_model.WinningPercentage}",
                .05f, bottom, .95f, bottom + .05f, 12, Muted, TextAnchor.MiddleLeft);
        }

        private void HonorSlots(RectTransform parent)
        {
            Label(parent, "HistoryTitle", "누적 구단 기록", .04f, .245f, .48f, .295f,
                15, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(parent, "HistoryScope", _model.HasHistory ? "전체 시즌 통산" : "기록을 확인할 수 없습니다",
                .48f, .245f, .96f, .295f, 12, Muted, TextAnchor.MiddleRight);
            string[] labels = { "우승", "준우승", "승격", "강등" };
            string[] descriptions = { "포스트시즌 정상", "포스트시즌 2위", "상위 리그 진출", "하위 리그 이동" };
            int[] counts = { _model.Championships, _model.RunnerUps, _model.Promotions, _model.Relegations };
            Color[] accents = { Gold, Ink, OwnerDashboardStyle.Info, Muted };
            for (int index = 0; index < labels.Length; index++)
            {
                float left = .04f + index * .232f;
                var slot = Surface(parent, "HonorSlot" + index, Paper, left, .035f, left + .224f, .235f);
                OwnerDashboardStyle.ApplyInset(slot.GetComponent<Image>());
                Color accent = _model.HasHistory && counts[index] > 0 ? accents[index] : Muted;
                OwnerDashboardStyle.Rule(slot, "RecordAccent", Vector2.up, Vector2.one,
                    new Vector2(0, -2), Vector2.zero, accent);
                var safe = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", slot);
                OwnerRuntimeUiFactory.Stretch(safe, new Vector2(12, 8), new Vector2(-12, -8));
                Label(safe, "Caption", labels[index], 0, .70f, 1, 1, 16, accent,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                // 단위를 함께 읽게 하고, 미연결 기록을 실제 0회와 구분한다.
                Label(safe, "Count", _model.HasHistory ? $"{counts[index]:N0}<size=15> 회</size>" : "—",
                    0, .25f, 1, .72f, 28, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                Label(safe, "Description", descriptions[index], 0, 0, 1, .25f,
                    12, Muted, TextAnchor.MiddleLeft);
            }
        }


        private static RectTransform Panel(RectTransform parent, string name, string title, float minX, float minY, float maxX, float maxY)
        {
            RectTransform panel = Surface(parent, name, White, minX, minY, maxX, maxY, true);
            UIOwnerFrontOfficePanel.Apply(panel, "ManagerReport");
            Label(panel, "Title", title, .03f, .89f, .97f, .99f, 18, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            OwnerDashboardStyle.Rule(panel, "SectionRule", new Vector2(.03f, .88f), new Vector2(.97f, .88f),
                Vector2.zero, new Vector2(0, 1), Grid);
            return panel;
        }

        private static void InfoBand(RectTransform parent, string caption, string value, float y)
        {
            RectTransform band = Surface(parent, "Info_" + caption, Color.clear, .025f, y - .09f, .975f, y);
            Label(band, "Caption_" + caption, caption, .0263f, 0f, .3316f, 1f, 14, Blue, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(band, "Value_" + caption, value, .3421f, 0f, .9632f, 1f, 16, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private static void Section(RectTransform parent, string title, float y)
        {
            RectTransform section = Surface(parent, "Section_" + title, Color.clear, .025f, y, .975f, y + .085f);
            OwnerDashboardStyle.Rule(section, "Rule", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 1), Grid);
            Label(section, "SectionLabel_" + title, title, .016f, 0f, .984f, 1f, 14, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private static void GridRow(RectTransform parent, float y, string[] captions, string[] values)
        {
            float width = .95f / captions.Length;
            for (int index = 0; index < captions.Length; index++)
            {
                float left = .025f + index * width;
                RectTransform cell = Surface(parent, "Cell_" + y + "_" + index, Color.clear, left, y, left + width - .008f, y + .12f);
                Label(cell, "Caption", captions[index], 0f, .51f, 1f, 1f, 12, Blue, TextAnchor.MiddleCenter, FontStyle.Bold);
                Label(cell, "Value", values[index], 0f, 0f, 1f, .55f, 21, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private string RankText() => _model.Games == 0 || _model.Rank <= 0 ? "—" : _model.Rank + "위";

        private static RectTransform Surface(Transform parent, string name, Color color,
            float minX, float minY, float maxX, float maxY, bool outline = false)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            Place(image.rectTransform, minX, minY, maxX, maxY);
            OwnerDashboardStyle.SetDataSurface(image, color);
            if (outline)
            {
                Outline border = image.gameObject.AddComponent<Outline>();
                border.effectColor = Grid;
                border.effectDistance = new Vector2(1f, -1f);
                border.useGraphicAlpha = false;
            }
            return image.rectTransform;
        }

        private static Text Label(Transform parent, string name, string value,
            float minX, float minY, float maxX, float maxY, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, style, alignment, color);
            Place(text.rectTransform, minX, minY, maxX, maxY);
            OwnerDashboardStyle.SetDataText(text, style == FontStyle.Bold);
            text.color = color;
            return text;
        }

        private static void Place(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

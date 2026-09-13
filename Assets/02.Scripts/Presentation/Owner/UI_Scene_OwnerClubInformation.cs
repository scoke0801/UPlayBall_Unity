using System;
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
        public event Action ChangeFrontManagerRequested;

        /// <summary>팝업 종료 후 다시 생성된 교체 버튼으로 포커스를 복원한다.</summary>
        public void FocusFrontManagerButton() => _changeManagerButton?.Select();

        /// <summary>공용 셸의 본문 슬롯에 구단 정보 화면을 생성한다.</summary>
        public static UI_Scene_OwnerClubInformation CreateRuntime(RectTransform host)
        {
            return OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerClubInformation), host)
                .gameObject.AddComponent<UI_Scene_OwnerClubInformation>();
        }

        /// <summary>현재 Save에서 투영한 구단 정보로 화면을 갱신한다.</summary>
        public void Bind(OwnerClubInformationPresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (gameObject.activeSelf) Render();
        }

        /// <summary>구단주 또는 구단 탭을 선택해 해당 레퍼런스 구성을 표시한다.</summary>
        public void ShowTab(bool showOwner)
        {
            _showOwner = showOwner;
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
            Label(root, "DataNotice", "누적 기록은 이 구단의 진행 이력 기준입니다. 자세한 시즌 성적은 구단 기록실에서 확인하세요.",
                .025f, .012f, .975f, .055f, 12, Muted, TextAnchor.MiddleLeft);
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
            Section(history, "누적 구단 기록", .20f);
            HonorSlots(history);

            RectTransform manager = Panel(root, "FrontManager", "프런트 매니저", .51f, .08f, .975f, .665f);
            Image office = OwnerRuntimeUiFactory.CreateImage("Office", manager, Color.white);
            office.sprite = Resources.Load<Sprite>("UI/Generated/bg_owner_container_office_v2");
            office.type = Image.Type.Simple;
            office.preserveAspect = false;
            office.gameObject.AddComponent<RectMask2D>();
            Place(office.rectTransform, .02f, .23f, .98f, .87f);
            Image shade = OwnerRuntimeUiFactory.CreateImage("Shade", office.transform, new Color(0f, 0f, 0f, .38f));
            OwnerRuntimeUiFactory.Stretch(shade.rectTransform);
            Image portrait = OwnerRuntimeUiFactory.CreateImage("Portrait", office.transform, Color.white);
            portrait.sprite = FrontManagerPortraitSprites.LoadForManager(_model.FrontManagerId, "FM_WELCOME");
            portrait.preserveAspect = true;
            Place(portrait.rectTransform, .45f, .02f, 1f, .98f);
            RectTransform speech = Surface(office.rectTransform, "Speech", OwnerDashboardStyle.TableHeader, .04f, .50f, .59f, .91f);
            UIOwnerFrontOfficePanel.Apply(speech, "Speech");
            Label(speech, "Message", "다음 승리를 준비해요.\n구단의 현재 상태를\n함께 확인하세요.",
                .10f, .18f, .83f, .82f, 15, Ink, TextAnchor.MiddleLeft);
            _changeManagerButton = OwnerWorkspaceUiFactory.CreateButton(manager, "ChangeFrontManager", "매니저 교체",
                () => ChangeFrontManagerRequested?.Invoke());
            Place((RectTransform)_changeManagerButton.transform, .65f, .12f, .96f, .215f);
            Label(manager, "Motto", "구단주의 한마디", .03f, .02f, .27f, .10f, 13, Blue, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(manager, "MottoText", "우리 구단의 다음 승리를 준비하자.", .28f, .02f, .97f, .10f, 15, Ink, TextAnchor.MiddleLeft);
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
            Label(roster, "RosterHint", "선수단에서 현재 편성을 확인하고\n전력보강에서 다음 성장을 준비하세요.", .04f, .25f, .96f, .37f, 15,
                Muted, TextAnchor.MiddleCenter);
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
            Label(previous, "HistoryEmptyTitle", "이전 시즌 기록 안내", .06f, .44f, .94f, .70f, 22, Ink);
            Label(previous, "HistoryEmptyDetail", "완료된 시즌의 상세 성적은 구단 기록실에서 확인하세요.", .06f, .20f, .94f, .43f, 14, Muted);
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
            string[] labels = { "우승", "준우승", "승격", "강등" };
            int[] counts = { _model.Championships, _model.RunnerUps, _model.Promotions, _model.Relegations };
            for (int index = 0; index < labels.Length; index++)
            {
                float left = .04f + index * .24f;
                var slot = OwnerRuntimeUiFactory.CreateRect("HonorSlot" + index, parent);
                Place(slot, left, .04f, left + .20f, .21f);
                var badge = UIOwnerFrontOfficeSkin.CreateBadge(slot, "Badge", "Count", 28);
                Place(badge.rectTransform, .28f, .33f, .72f, 1f);
                badge.color = counts[index] > 0 ? Color.white : OwnerDashboardStyle.Muted;
                Label(slot, "Count", _model.HasHistory ? counts[index].ToString() : "—", .28f, .33f, .72f, 1f, 17, OwnerDashboardStyle.Ivory, TextAnchor.MiddleCenter);
                Label(slot, "Caption", labels[index], 0, 0, 1, .33f, 12, Muted);
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

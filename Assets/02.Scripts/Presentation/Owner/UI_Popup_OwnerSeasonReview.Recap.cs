using System.Collections.Generic;
using System.Globalization;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerSeasonReview
    {
        private const int AwardsPerPage = 6;
        private RectTransform _recapRoot, _clubReport, _awardsReport;
        private Button _clubTab, _awardsTab, _awardFilter, _awardPrevious, _awardNext;
        private Text _recapResult, _recapTeam, _recapRecord, _recapMovement, _awardPageLabel, _awardEmpty;
        private Text _recapOwner;
        private RawImage _recapTrophy;
        private readonly Text[] _clubValues = new Text[8];
        private readonly Text[] _awardNames = new Text[AwardsPerPage];
        private readonly Text[] _awardPlayers = new Text[AwardsPerPage];
        private readonly Text[] _awardTeams = new Text[AwardsPerPage];
        private readonly Text[] _awardValues = new Text[AwardsPerPage];
        private readonly RectTransform[] _awardRows = new RectTransform[AwardsPerPage];
        private readonly List<OwnerRecordTitleReview> _visibleAwards = new();
        private bool _showAwards, _onlyOurAwards;
        private int _awardPage;

        private void BuildRecapReport()
        {
            _recapRoot = Surface(_modal, "RecapReport", Navy, Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(_recapRoot, new Vector2(32, 110), new Vector2(1128, 558));
            _clubTab = ReportButton(_recapRoot, "ClubTab", "구단", 24, 386, 172,
                () => SelectRecapTab(false), OwnerButtonRole.Tab);
            _awardsTab = ReportButton(_recapRoot, "AwardsTab", "선수 수상", 204, 386, 172,
                () => SelectRecapTab(true), OwnerButtonRole.Tab);
            ReportText(_recapRoot, "Scope", "우리 조 · 정규시즌 개인 기록 타이틀", 14,
                new Vector2(520, 386), new Vector2(1072, 426), Muted).alignment = TextAnchor.MiddleRight;

            _clubReport = OwnerRuntimeUiFactory.CreateRect("ClubReport", _recapRoot);
            SetRect(_clubReport, new Vector2(24, 24), new Vector2(1072, 374));
            var achievement = Surface(_clubReport, "Achievement", NavySoft, Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(achievement, Vector2.zero, new Vector2(288, 350));
            _recapResult = ReportText(achievement, "Result", "", 24, new Vector2(16, 294), new Vector2(272, 334), Gold);
            _recapResult.alignment = TextAnchor.MiddleCenter;
            var trophyRect = OwnerRuntimeUiFactory.CreateRect("Trophy", achievement);
            SetRect(trophyRect, new Vector2(44, 74), new Vector2(244, 274));
            _recapTrophy = trophyRect.gameObject.AddComponent<RawImage>();
            _recapTrophy.raycastTarget = false;
            ReportText(achievement, "ResultScope", "포스트시즌 최종 성적", 14,
                new Vector2(16, 18), new Vector2(272, 54), Muted).alignment = TextAnchor.MiddleCenter;
            BuildRecapIdentity();
            _recapRecord = ReportText(_clubReport, "Record", "", 22,
                new Vector2(320, 244), new Vector2(1032, 286), Gold);
            string[] labels = { "총 득점", "총 실점", "홈런", "도루", "팀 타율", "팀 평균자책점", "승률", "득실차" };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 320 + i % 2 * 364;
                float y = 190 - i / 2 * 48;
                var row = OwnerRuntimeUiFactory.CreateImage("Stat" + i, _clubReport, OwnerDashboardStyle.TableSurface);
                SetRect(row.rectTransform, new Vector2(x, y), new Vector2(x + 348, y + 44));
                OwnerDashboardStyle.SetDataSurface(row, i / 2 % 2 == 0 ? OwnerDashboardStyle.TableHeader : OwnerDashboardStyle.TableSurface);
                ReportText(row.transform, "Label", labels[i], 16, new Vector2(12, 4), new Vector2(176, 40), Muted);
                _clubValues[i] = ReportText(row.transform, "Value", "—", 22, new Vector2(184, 4), new Vector2(336, 40), Ivory);
                _clubValues[i].alignment = TextAnchor.MiddleRight;
            }
            _recapMovement = ReportText(_clubReport, "Movement", "", 16,
                new Vector2(320, 0), new Vector2(1032, 38), Gold);
            BuildAwardsReport();
            _recapRoot.gameObject.SetActive(false);
        }

        // 이름을 합친 문장 대신 각 라벨이 독립된 열을 소유해 구단과 운영자를 구분한다.
        private void BuildRecapIdentity()
        {
            ReportText(_clubReport, "ClubNameLabel", "구단명", 14,
                new Vector2(320, 328), new Vector2(744, 350), Muted);
            _recapTeam = ReportText(_clubReport, "ClubName", "", 28,
                new Vector2(320, 288), new Vector2(744, 328), Ivory);
            var divider = OwnerRuntimeUiFactory.CreateImage("IdentityDivider", _clubReport, Gold);
            SetRect(divider.rectTransform, new Vector2(768, 294), new Vector2(769, 344));
            OwnerDashboardStyle.SetDataSurface(divider, Gold);
            divider.raycastTarget = false;
            ReportText(_clubReport, "OwnerNameLabel", "구단주명", 14,
                new Vector2(792, 328), new Vector2(1032, 350), Muted);
            _recapOwner = ReportText(_clubReport, "OwnerName", "", 22,
                new Vector2(792, 288), new Vector2(1032, 328), Ivory);
            _recapTeam.supportRichText = false;
            _recapOwner.supportRichText = false;
        }

        private void BuildAwardsReport()
        {
            _awardsReport = OwnerRuntimeUiFactory.CreateRect("AwardsReport", _recapRoot);
            SetRect(_awardsReport, new Vector2(24, 24), new Vector2(1072, 374));
            _awardFilter = ReportButton(_awardsReport, "OurClubFilter", "우리 구단만", 832, 306, 216,
                () => { _onlyOurAwards = !_onlyOurAwards; _awardPage = 0; BindAwardRows(); }, OwnerButtonRole.Secondary);
            ReportText(_awardsReport, "AwardGuide", "부문별 1위 · 동률은 공동 수상", 16,
                new Vector2(8, 308), new Vector2(784, 348), Gold);
            var heading = OwnerRuntimeUiFactory.CreateImage("TableHeading", _awardsReport, OwnerDashboardStyle.TableHeader);
            SetRect(heading.rectTransform, new Vector2(0, 266), new Vector2(1048, 300));
            OwnerDashboardStyle.SetDataSurface(heading, OwnerDashboardStyle.TableHeader);
            string[] columns = { "수상 부문", "선수", "소속 구단", "기록" };
            float[] starts = { 16, 256, 500, 886 };
            float[] ends = { 244, 488, 874, 1032 };
            for (int i = 0; i < columns.Length; i++)
                ReportText(heading.transform, "Column" + i, columns[i], 14,
                    new Vector2(starts[i], 0), new Vector2(ends[i], 34), Muted);
            for (int i = 0; i < AwardsPerPage; i++)
            {
                var row = OwnerRuntimeUiFactory.CreateImage("AwardRow" + i, _awardsReport, OwnerDashboardStyle.TableSurface);
                OwnerDashboardStyle.SetDataSurface(row, i % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
                SetRect(row.rectTransform, new Vector2(0, 224 - i * 36), new Vector2(1048, 260 - i * 36));
                _awardRows[i] = row.rectTransform;
                _awardNames[i] = ReportText(row.transform, "Award", "", 16, new Vector2(16, 0), new Vector2(244, 36), Gold);
                _awardPlayers[i] = ReportText(row.transform, "Player", "", 18, new Vector2(256, 0), new Vector2(488, 36), Ivory);
                _awardTeams[i] = ReportText(row.transform, "Team", "", 15, new Vector2(500, 0), new Vector2(874, 36), Muted);
                _awardValues[i] = ReportText(row.transform, "Value", "", 18, new Vector2(886, 0), new Vector2(1032, 36), Ivory);
                _awardValues[i].alignment = TextAnchor.MiddleRight;
            }
            _awardEmpty = ReportText(_awardsReport, "Empty", "", 20, new Vector2(16, 88), new Vector2(1032, 244), Muted);
            _awardEmpty.alignment = TextAnchor.MiddleCenter;
            _awardPrevious = ReportButton(_awardsReport, "Previous", "이전", 720, 0, 92,
                () => { _awardPage--; BindAwardRows(); }, OwnerButtonRole.Navigation);
            _awardPageLabel = ReportText(_awardsReport, "Page", "", 14, new Vector2(820, 0), new Vector2(948, 40), Muted);
            _awardPageLabel.alignment = TextAnchor.MiddleCenter;
            _awardNext = ReportButton(_awardsReport, "Next", "다음", 956, 0, 92,
                () => { _awardPage++; BindAwardRows(); }, OwnerButtonRole.Navigation);
            ReportText(_awardsReport, "Qualification", "타율: 규정 타석 · 평균자책점: 규정 이닝 충족", 13,
                new Vector2(8, 0), new Vector2(696, 40), Muted);
        }

        private void BindRecapReport()
        {
            _recapTeam.text = Resolve(_snapshot.PlayerTeamSeasonKey);
            _recapOwner.text = _ownerName;
            _recapRecord.text = $"{_snapshot.Wins}승  {_snapshot.Losses}패  {_snapshot.Draws}무   ·   정규시즌 {_snapshot.Rank}위";
            bool champion = _snapshot.PostseasonResult == OwnerTeamPostseasonResult.Champion;
            bool runnerUp = _snapshot.PostseasonResult == OwnerTeamPostseasonResult.RunnerUp;
            _recapResult.text = champion ? "우승" : runnerUp ? "준우승" : $"정규시즌 {_snapshot.Rank}위";
            _recapTrophy.texture = champion ? Resources.Load<Texture2D>("UI/ClubHistory/champion") :
                runnerUp ? Resources.Load<Texture2D>("UI/ClubHistory/runner-up") : null;
            _recapTrophy.gameObject.SetActive(_recapTrophy.texture != null);
            // 우승하지 않은 구단에 우승 트로피를 재사용하지 않는다.
            var resultScope = _recapResult.transform.parent.Find("ResultScope").GetComponent<Text>();
            resultScope.text = FormatPostseasonResult(_snapshot.PostseasonResult);
            _recapResult.rectTransform.anchoredPosition = new Vector2(144, _recapTrophy.texture != null ? 314 : 196);
            _recapMovement.text = FormatLeagueMovement() + "   ·   " + FormatNextGrade();
            OwnerSeasonHonorsReview honors = _snapshot.Honors;
            string[] values = { _snapshot.Runs.ToString("N0"), _snapshot.RunsAllowed.ToString("N0"),
                honors?.HomeRuns.ToString("N0") ?? "—", honors?.StolenBases.ToString("N0") ?? "—",
                honors == null || honors.AtBats == 0 ? "—" : honors.BattingAverage.ToString(".000", CultureInfo.InvariantCulture),
                honors == null || honors.PitchingOuts == 0 ? "—" : honors.EarnedRunAverage.ToString("0.00", CultureInfo.InvariantCulture),
                _snapshot.WinningPercentage.ToString(".000", CultureInfo.InvariantCulture), FormatSigned(_snapshot.RunDifferential) };
            for (int i = 0; i < values.Length; i++) _clubValues[i].text = values[i];
            _awardPage = 0;
            SelectRecapTab(false);
        }

        private void SelectRecapTab(bool awards)
        {
            _showAwards = awards;
            _clubReport.gameObject.SetActive(!awards);
            _awardsReport.gameObject.SetActive(awards);
            OwnerUiButtonSkin.SetSelected(_clubTab, !awards);
            OwnerUiButtonSkin.SetSelected(_awardsTab, awards);
            if (awards) BindAwardRows();
            ConfigureRecapNavigation();
        }

        private void BindAwardRows()
        {
            _visibleAwards.Clear();
            if (_snapshot.Honors != null)
                foreach (var item in _snapshot.Honors.Titles)
                    if (!_onlyOurAwards || item.IsOurPlayer) _visibleAwards.Add(item);
            int pages = Mathf.Max(1, (_visibleAwards.Count + AwardsPerPage - 1) / AwardsPerPage);
            _awardPage = Mathf.Clamp(_awardPage, 0, pages - 1);
            for (int i = 0; i < AwardsPerPage; i++)
            {
                int index = _awardPage * AwardsPerPage + i;
                _awardRows[i].gameObject.SetActive(index < _visibleAwards.Count);
                if (index >= _visibleAwards.Count) continue;
                var award = _visibleAwards[index];
                _awardNames[i].text = FormatTitle(award.Metric) + (award.IsShared ? " · 공동" : "");
                _awardPlayers[i].text = award.PlayerName;
                _awardTeams[i].text = (award.IsOurPlayer ? "우리 구단 · " : "") + Resolve(award.TeamSeasonKey);
                _awardPlayers[i].color = award.IsOurPlayer ? Gold : Ivory;
                _awardValues[i].text = award.Value.ToString(award.Metric == CareerRecordMetric.BattingAverage ? ".000" :
                    award.Metric == CareerRecordMetric.EarnedRunAverage ? "0.00" : "N0", CultureInfo.InvariantCulture);
            }
            _awardEmpty.gameObject.SetActive(_visibleAwards.Count == 0);
            _awardEmpty.text = _snapshot.Honors == null ? "수상 기록을 불러오지 못했습니다.\n닫은 뒤 시즌 결산을 다시 열어 주세요." :
                !_snapshot.Honors.IsFinal ? "정규시즌 종료 후 수상이 확정됩니다." :
                _onlyOurAwards ? "이번 시즌 우리 구단의 개인 기록 타이틀 수상자는 없습니다.\n‘전체 수상자’에서 우리 조의 수상 선수를 확인하세요." :
                "이번 시즌 수상 조건을 충족한 기록이 없습니다.";
            _awardFilter.transform.Find("Label").GetComponent<Text>().text = _onlyOurAwards ? "전체 수상자" : "우리 구단만";
            OwnerUiButtonSkin.SetSelected(_awardFilter, _onlyOurAwards);
            _awardPageLabel.text = $"{_awardPage + 1} / {pages}";
            _awardPrevious.interactable = _awardPage > 0;
            _awardNext.interactable = _awardPage + 1 < pages;
            ConfigureRecapNavigation();
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && (events.currentSelectedGameObject == _awardPrevious.gameObject && !_awardPrevious.interactable ||
                events.currentSelectedGameObject == _awardNext.gameObject && !_awardNext.interactable)) _awardFilter.Select();
        }

        private void SetRecapVisible(bool visible)
        {
            _recapRoot.gameObject.SetActive(visible);
            _summary.transform.parent.gameObject.SetActive(!visible);
            _insightTitle.transform.parent.gameObject.SetActive(!visible);
            foreach (var value in _metricValues) value.transform.parent.gameObject.SetActive(!visible);
        }

        private void ConfigureRecapNavigation()
        {
            if (_page != 2 || _clubTab == null) return;
            Button active = _showAwards ? _awardsTab : _clubTab;
            Button bottom = _showAwards ? _awardFilter : _primary;
            _clubTab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _awardsTab,
                selectOnLeft = _close, selectOnUp = _tabs[2], selectOnDown = bottom };
            _awardsTab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _clubTab,
                selectOnRight = _close, selectOnUp = _tabs[2], selectOnDown = bottom };
            var tabNav = _tabs[2].navigation; tabNav.selectOnDown = active; _tabs[2].navigation = tabNav;
            var primaryNav = _primary.navigation; primaryNav.selectOnUp = bottom == _primary ? active : bottom; _primary.navigation = primaryNav;
            Button previous = _awardPrevious.interactable ? _awardPrevious : _awardFilter;
            Button next = _awardNext.interactable ? _awardNext : _awardFilter;
            _awardFilter.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = active,
                selectOnLeft = previous, selectOnRight = next, selectOnDown = _primary };
            _awardPrevious.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _awardFilter,
                selectOnLeft = _awardFilter, selectOnRight = next, selectOnDown = _primary };
            _awardNext.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = _awardFilter,
                selectOnLeft = previous, selectOnRight = _awardFilter, selectOnDown = _primary };
        }

        private static string FormatTitle(CareerRecordMetric metric) => metric switch
        {
            CareerRecordMetric.BattingAverage => "타격왕", CareerRecordMetric.HomeRuns => "홈런왕",
            CareerRecordMetric.RunsBattedIn => "타점왕", CareerRecordMetric.StolenBases => "도루왕",
            CareerRecordMetric.EarnedRunAverage => "평균자책점 1위", CareerRecordMetric.Wins => "다승왕",
            CareerRecordMetric.PitchingStrikeouts => "탈삼진왕", CareerRecordMetric.Saves => "세이브왕", _ => "개인 타이틀"
        };

        private static Text ReportText(Transform parent, string name, string text, int size, Vector2 min, Vector2 max, Color color)
        {
            Text label = Label(parent, name, text, size, FontStyle.Normal, color, min, max);
            OwnerDashboardStyle.SetTypography(label, size >= 18);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button ReportButton(Transform parent, string name, string text, float x, float y, float width,
            System.Action action, OwnerButtonRole role)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(parent, name, text, action);
            SetRect(button.GetComponent<RectTransform>(), new Vector2(x, y), new Vector2(x + width, y + 40));
            OwnerUiButtonSkin.Apply(button, role);
            return button;
        }
    }
}

using System;
using System.Globalization;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerPlayerCard
    {
        public static event Action<string> TraitTrainingRequested;
        private Button _traitTrainingButton;
        private Button _growthHistoryButton;
        private RectTransform _growthHistoryRoot;
        private CanvasGroup _historyGroup;
        private readonly Button[] _recordTabs = new Button[3];
        private readonly Text[,] _recordCells = new Text[6, 8];
        private readonly RectTransform[] _recordRows = new RectTransform[6];
        private RectTransform _recordTable;
        private RectTransform _historyBody;
        private Text _historyTitle, _historyContext, _historyEmpty, _historyPage;
        private Text _historyEntry;
        private Button _historyPrevious, _historyNext;
        private string[] _growthHistoryEntries = Array.Empty<string>();
        private int _growthHistoryIndex;
        private int _recordTab;
        private bool _isHistoryOpen;
        private float _historyProgress;
        private OwnerCollectionCardSnapshot _historyCard;
        private PlayerCompetitionStatisticsState _playerRecord;

        private void BuildGrowthHistory(RectTransform root)
        {
            _growthHistoryButton = CreateNavigationButton(root, "GrowthHistory", "성장 이력 · 기록", .5f, 0, .5f, 0, ToggleGrowthHistory);
            _growthHistoryButton.GetComponentInChildren<Text>().fontSize = 18;
            _growthHistoryRoot = Surface(root, "PlayerRecords", OwnerDashboardStyle.TableSurface, .5f, .5f, .5f, .5f);
            UIOwnerFrontOfficePanel.ApplyFramedSurface(_growthHistoryRoot);
            _growthHistoryRoot.GetComponent<Image>().raycastTarget = true;
            _historyGroup = _growthHistoryRoot.gameObject.AddComponent<CanvasGroup>();
            var safe = ContentRect(_growthHistoryRoot, "ContentSafeRect", .045f, .045f, .955f, .955f);
            _historyTitle = RecordLabel(safe, "Title", "", 0, .90f, .80f, 1, 24, true);
            var fold = CreateNavigationButton(safe, "Fold", "접기", .82f, .92f, 1, .99f, ToggleGrowthHistory);
            fold.GetComponentInChildren<Text>().fontSize = 16;
            _historyContext = RecordLabel(safe, "Context", "", 0, .83f, 1, .90f, 14);
            string[] names = { "최근 5경기", "이번 리그", "육성정보" };
            for (int i = 0; i < 3; i++)
            {
                int tab = i;
                _recordTabs[i] = CreateNavigationButton(safe, "Tab" + i, names[i],
                    i / 3f, .73f, (i + 1) / 3f - .012f, .81f, () => SelectRecordTab(tab));
                _recordTabs[i].GetComponentInChildren<Text>().fontSize = 16;
                OwnerUiButtonSkin.Apply(_recordTabs[i], OwnerButtonRole.Tab);
            }
            _recordTable = ContentRect(safe, "RecordsTable", 0, .14f, 1, .69f);
            for (int row = 0; row < 6; row++)
            {
                float top = 1 - row / 6f;
                _recordRows[row] = Surface(_recordTable, "Row" + row,
                    row == 0 ? OwnerDashboardStyle.TableHeader : row % 2 == 0
                        ? OwnerDashboardStyle.TableAlternate : OwnerDashboardStyle.TableSurface,
                    0, top - 1f / 6f, 1, top);
                OwnerDashboardStyle.SetDataSurface(_recordRows[row].GetComponent<Image>(), _recordRows[row].GetComponent<Image>().color);
                for (int col = 0; col < 8; col++)
                    _recordCells[row, col] = RecordLabel(_recordRows[row], "Cell" + col, "",
                        col / 8f, .05f, (col + 1) / 8f, .95f, row == 0 ? 13 : 16, row != 0, TextAnchor.MiddleCenter);
            }
            _historyBody = ContentRect(safe, "Development", 0, .16f, 1, .69f);
            var viewport = ContentRect(_historyBody, "Viewport", 0, 0, 1, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = _historyBody.gameObject.AddComponent<ScrollRect>();
            var content = ContentRect(viewport, "Content", 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _historyEntry = RecordLabel(content, "Entry", "", 0, 0, 1, 1, 18, true, TextAnchor.UpperLeft);
            _historyEntry.horizontalOverflow = HorizontalWrapMode.Wrap;
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _historyEmpty = RecordLabel(safe, "Empty", "", .05f, .30f, .95f, .65f, 18, false, TextAnchor.MiddleCenter);
            _historyPrevious = CreateNavigationButton(safe, "PreviousEntry", "이전", 0, .025f, .16f, .10f,
                () => { _growthHistoryIndex--; RefreshRecordPanel(); });
            _historyNext = CreateNavigationButton(safe, "NextEntry", "다음", .40f, .025f, .56f, .10f,
                () => { _growthHistoryIndex++; RefreshRecordPanel(); });
            _historyPage = RecordLabel(safe, "Page", "", .17f, .025f, .39f, .10f, 14, false, TextAnchor.MiddleCenter);
            _traitTrainingButton = CreateNavigationButton(safe, "TraitTraining", "특성훈련", .65f, .025f, 1, .10f,
                () => { string id = _historyCard.CardId; Close(); TraitTrainingRequested?.Invoke(id); });
            foreach (var button in new[] { _historyPrevious, _historyNext, _traitTrainingButton })
                button.GetComponentInChildren<Text>().fontSize = 16;
            _growthHistoryRoot.gameObject.SetActive(false);
        }

        private static Text RecordLabel(Transform parent, string name, string value,
            float x0, float y0, float x1, float y1, int size, bool primary = false,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text label = Label(parent, name, value, x0, y0, x1, y1, size, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetDataText(label, primary);
            label.alignment = alignment;
            return label;
        }

        private void BindGrowthHistory(OwnerCollectionCardSnapshot card)
        {
            _historyCard = card;
            _growthHistoryEntries = (string.IsNullOrWhiteSpace(card.GrowthHistory)
                ? "아직 완료한 성장 과정이 없습니다." : card.GrowthHistory)
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (card.GrowthBadges.HasTrait)
            {
                var entries = new string[_growthHistoryEntries.Length + 1];
                entries[0] = "현재 특성\n" + card.GrowthBadges.TraitDescription;
                Array.Copy(_growthHistoryEntries, 0, entries, 1, _growthHistoryEntries.Length);
                _growthHistoryEntries = entries;
            }
            _growthHistoryIndex = 0;
            var runtime = OwnerModeManager.Instance?.Runtime;
            _playerRecord = card.IsOwnedCard && runtime != null
                ? OwnerSeasonRecordsService.GetCurrentPlayerRecord(runtime, runtime.PlayerTeamSeasonKey, card.PlayerSeasonId) : null;
            if (!card.IsOwnedCard) _recordTab = 2;
            _growthHistoryButton.interactable = true;
            RefreshRecordPanel();
        }

        private void SelectRecordTab(int tab)
        {
            if (!_historyCard.IsOwnedCard && tab != 2) return;
            _recordTab = tab;
            RefreshRecordPanel();
        }

        private void ToggleGrowthHistory()
        {
            _isHistoryOpen = !_isHistoryOpen;
            _growthHistoryRoot.gameObject.SetActive(true);
            _growthHistoryButton.GetComponentInChildren<Text>().text = _isHistoryOpen ? "기록 접기" : "성장 이력 · 기록";
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_isHistoryOpen ? _recordTabs[_recordTab].gameObject : _growthHistoryButton.gameObject);
        }

        /// <summary>카드 팝업을 닫기 전에 펼친 기록 패널부터 접는다.</summary>
        public override bool TryHandleCancel()
        {
            if (!_isHistoryOpen) return false;
            ToggleGrowthHistory();
            return true;
        }

        private void RefreshRecordPanel()
        {
            bool growth = _recordTab == 2;
            _historyTitle.text = _historyCard.DisplayName + " · 선수 기록";
            _historyContext.text = growth ? "훈련과 성장의 발자취" :
                _historyCard.CurrentLeagueLabel + " · 정규시즌 · " + (_recordTab == 0 ? "최근 출전 순" : "이번 시즌 누적");
            for (int i = 0; i < 3; i++)
            {
                _recordTabs[i].gameObject.SetActive(_historyCard.IsOwnedCard || i == 2);
                OwnerUiButtonSkin.SetSelected(_recordTabs[i], i == _recordTab);
                if (_historyCard.IsOwnedCard)
                    OwnerRuntimeUiFactory.SetAnchors(_recordTabs[i].GetComponent<RectTransform>(),
                        new Vector2(i / 3f, .73f), new Vector2((i + 1) / 3f - .012f, .81f), Vector2.zero, Vector2.zero);
            }
            _recordTabs[2].GetComponentInChildren<Text>().text = _historyCard.IsOwnedCard ? "성장 이력" : "육성정보";
            if (!_historyCard.IsOwnedCard)
                OwnerRuntimeUiFactory.SetAnchors(_recordTabs[2].GetComponent<RectTransform>(), new Vector2(0, .73f), new Vector2(1, .81f), Vector2.zero, Vector2.zero);
            _historyBody.gameObject.SetActive(growth);
            _historyPrevious.gameObject.SetActive(growth);
            _historyNext.gameObject.SetActive(growth);
            _historyPage.gameObject.SetActive(growth);
            _traitTrainingButton.gameObject.SetActive(growth && _historyCard.IsOwnedCard && TraitTrainingRequested != null);
            _historyEmpty.gameObject.SetActive(false);
            _recordTable.gameObject.SetActive(!growth);
            if (growth)
            {
                _growthHistoryIndex = Mathf.Clamp(_growthHistoryIndex, 0, _growthHistoryEntries.Length - 1);
                _historyEntry.text = _growthHistoryEntries[_growthHistoryIndex];
                _historyBody.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
                _historyPage.text = (_growthHistoryIndex + 1) + " / " + _growthHistoryEntries.Length;
                _historyPrevious.interactable = _growthHistoryIndex > 0;
                _historyNext.interactable = _growthHistoryIndex + 1 < _growthHistoryEntries.Length;
                return;
            }
            bool pitcher = _historyCard.Position == PlayerPosition.StartingPitcher || _historyCard.Position == PlayerPosition.ReliefPitcher;
            bool empty = _playerRecord == null || (_recordTab == 0 ? _playerRecord.RecentGames.Count == 0 :
                pitcher ? _playerRecord.Pitching.Appearances == 0 : _playerRecord.Batting.Games == 0);
            if (empty)
            {
                _recordTable.gameObject.SetActive(false);
                _historyEmpty.gameObject.SetActive(true);
                _historyEmpty.text = "아직 출전 기록이 없습니다.\n정규시즌에 출전하면 성적이 표시됩니다.";
                return;
            }
            for (int row = 0; row < 6; row++) _recordRows[row].gameObject.SetActive(false);
            if (_recordTab == 0)
            {
                SetRecordRow(0, pitcher ? new[] { "출전", "이닝", "피안타", "실점", "자책", "볼넷", "삼진", "결과" }
                    : new[] { "출전", "타수", "안타", "홈런", "타점", "볼넷", "삼진", "타율" });
                var games = _playerRecord.RecentGames;
                for (int i = 0; i < games.Count; i++)
                {
                    var g = games[games.Count - 1 - i];
                    string order = i == 0 ? "최근" : i + "경기 전";
                    SetRecordRow(i + 1, pitcher
                        ? new[] { order, Innings(g.outsRecorded), N(g.hitsAllowed), N(g.runsAllowed), N(g.earnedRuns), N(g.walks), N(g.strikeouts),
                            g.wins > 0 ? "승" : g.losses > 0 ? "패" : g.saves > 0 ? "세" : g.holds > 0 ? "홀" : "—" }
                        : new[] { order, N(g.atBats), N(g.hits), N(g.homeRuns), N(g.runsBattedIn), N(g.walks), N(g.strikeouts),
                            g.atBats == 0 ? "—" : Rate(g.hits / (double)g.atBats, 3) });
                }
            }
            else if (pitcher)
            {
                var p = _playerRecord.Pitching;
                SetRecordRow(0, new[] { "등판", "선발", "이닝", "평균자책", "승", "패", "세이브", "홀드" });
                SetRecordRow(1, new[] { N(p.Appearances), N(p.Starts), Innings(p.OutsRecorded), p.OutsRecorded == 0 ? "—" : Rate(p.EarnedRunAverage, 2), N(p.Wins), N(p.Losses), N(p.Saves), N(p.Holds) });
                SetRecordRow(3, new[] { "피안타", "피홈런", "볼넷", "삼진", "실점", "자책", "WHIP", "투구수" });
                SetRecordRow(4, new[] { N(p.HitsAllowed), N(p.HomeRunsAllowed), N(p.WalksAllowed), N(p.Strikeouts), N(p.RunsAllowed), N(p.EarnedRuns), p.OutsRecorded == 0 ? "—" : Rate(p.WalksHitsPerInningPitched, 2), N(p.PitchesThrown) });
            }
            else
            {
                var b = _playerRecord.Batting;
                SetRecordRow(0, new[] { "경기", "타수", "안타", "홈런", "타점", "득점", "도루", "타율" });
                SetRecordRow(1, new[] { N(b.Games), N(b.AtBats), N(b.Hits), N(b.HomeRuns), N(b.RunsBattedIn), N(b.Runs), N(b.StolenBases), b.AtBats == 0 ? "—" : Rate(b.BattingAverage, 3) });
                SetRecordRow(3, new[] { "타석", "2루타", "3루타", "볼넷", "삼진", "출루율", "장타율", "OPS" });
                SetRecordRow(4, new[] { N(b.PlateAppearances), N(b.Doubles), N(b.Triples), N(b.Walks), N(b.Strikeouts), Rate(b.OnBasePercentage, 3), Rate(b.SluggingPercentage, 3), Rate(b.OnBasePlusSlugging, 3) });
            }
        }

        private void SetRecordRow(int row, string[] cells)
        {
            _recordRows[row].gameObject.SetActive(true);
            for (int i = 0; i < 8; i++) _recordCells[row, i].text = cells[i];
        }

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Rate(double value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        private static string Innings(int outs) => N(outs / 3) + (outs % 3 == 0 ? "" : outs % 3 == 1 ? " ⅓" : " ⅔");
    }
}

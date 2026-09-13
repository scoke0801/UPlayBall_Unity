using System;
using System.Globalization;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>페넌트레이스·포스트시즌·시즌 결산을 한 흐름으로 보여주는 구단주 전용 결과 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class UI_Popup_OwnerSeasonReview : MonoBehaviour, ICancelHandler
    {
        private const float ModalWidth = 1160f;
        private const float ModalHeight = 720f;
        private static readonly Color Navy = CareerUiTheme.ReferencePanel;
        private static readonly Color NavyPanel = CareerUiTheme.ReferencePanel;
        private static readonly Color NavySoft = CareerUiTheme.ReferencePanelHeader;
        private static readonly Color Ivory = CareerUiTheme.TextPrimary;
        private static readonly Color Gold = CareerUiTheme.AccentGold;
        private static readonly Color Muted = CareerUiTheme.TextSecondary;
        private static Sprite _backgroundSprite;

        private Text _eyebrow;
        private Text _title;
        private Text _summary;
        private Text _detailsCaption;
        private Text _details;
        private Text _status;
        private Text _worldProgress;
        private Text _insightTitle;
        private Text _insightBody;
        private Text _primaryLabel;
        private Text _hint;
        private RectTransform _modal;
        private readonly Text[] _metricValues = new Text[3];
        private readonly Text[] _metricLabels = new Text[3];
        private Button _primary;
        private Button _close;
        private GameObject _previousSelection;
        private Button[] _tabs;
        private readonly RectTransform[] _seriesCards = new RectTransform[4];
        private readonly Text[] _seriesTitles = new Text[4];
        private readonly Text[] _seriesTeams = new Text[4];
        private readonly Text[] _seriesScores = new Text[4];
        private OwnerSeasonReviewSnapshot _snapshot;
        private Func<string, string> _teamName;
        private string _ownerName;
        private int _page;

        public event Action PostseasonRequested;
        public event Action CloseRequested;

        public static UI_Popup_OwnerSeasonReview CreateRuntime(RectTransform popupHost)
        {
            if (popupHost == null) throw new ArgumentNullException(nameof(popupHost));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerSeasonReview), popupHost);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerSeasonReview>();
            // 비활성 Popup Host와 EditMode에서도 Bind 전에 계층이 준비되어야 한다.
            view.EnsureBuilt();
            return view;
        }

        public void Bind(OwnerSeasonReviewSnapshot snapshot, Func<string, string> teamNameResolver, int initialPage,
            string ownerName = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _teamName = teamNameResolver ?? throw new ArgumentNullException(nameof(teamNameResolver));
            _ownerName = string.IsNullOrWhiteSpace(ownerName) ? "미등록" : ownerName.Trim();
            SetPage(Mathf.Clamp(initialPage, 0, snapshot.IsPostseasonCompleted ? 2 : 1));
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FitModal();
            PlayBracketReveal();
            EventSystem events = EventSystem.current;
            if (events == null) return;
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform)) return;
            _previousSelection = selected;
            _tabs[_page].Select();
        }

        public void Hide()
        {
            SkipBracketReveal();
            gameObject.SetActive(false);
            if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
            _previousSelection = null;
        }

        /// <summary>공통 Cancel 입력은 결과를 진행하지 않고 팝업 닫기로 전달한다.</summary>
        public void OnCancel(BaseEventData eventData)
        {
            if (IsBracketRevealing)
            {
                SkipBracketReveal();
                eventData.Use();
                return;
            }
            CloseRequested?.Invoke();
            eventData.Use();
        }

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (_modal != null) return;
            Build();
            Hide();
        }

        private void OnRectTransformDimensionsChange() => FitModal();

        private void FitModal()
        {
            if (_modal == null) return;
            Rect bounds = GetComponent<RectTransform>().rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float scale = Mathf.Min(1f, (bounds.width - CareerUiTheme.Space6) / ModalWidth,
                (bounds.height - CareerUiTheme.Space6) / ModalHeight);
            _modal.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        private void Build()
        {
            RectTransform root = GetComponent<RectTransform>();
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0.005f, 0.01f, 0.02f, 0.88f);
            blocker.raycastTarget = true;
            root.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            RectTransform modal = Surface(root, "SeasonReview", Navy,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ModalWidth, ModalHeight));
            _modal = modal;
            UIOwnerFrontOfficePanel.Apply(modal, "ManagerReport");
            modal.gameObject.AddComponent<CareerUiPreserveTextColor>();
            AddBackgroundArt(modal);

            RectTransform readableVeil = Decoration(modal, "ReadableVeil", new Color(0.015f, 0.035f, 0.065f, 0.84f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(readableVeil, Vector2.zero, new Vector2(760f, ModalHeight));
            RectTransform footerVeil = Decoration(modal, "FooterVeil", new Color(0.015f, 0.03f, 0.05f, 0.92f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(footerVeil, Vector2.zero, new Vector2(ModalWidth, 100f));

            RectTransform header = Surface(modal, "Header", new Color(0.018f, 0.045f, 0.078f, 0.96f),
                new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 76f));
            SetRect(header, new Vector2(0f, 634f), new Vector2(ModalWidth, ModalHeight));
            _eyebrow = Label(header, "Eyebrow", "OWNER SEASON REVIEW", 13, FontStyle.Bold,
                Gold, new Vector2(32f, 48f), new Vector2(850f, 72f));
            _title = Label(header, "Title", string.Empty, 28, FontStyle.Bold,
                Ivory, new Vector2(32f, 9f), new Vector2(900f, 48f));
            Button close = OwnerWorkspaceUiFactory.CreateButton(header, "Close", "닫기", () => CloseRequested?.Invoke());
            _close = close;
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1024f, 18f), new Vector2(1128f, 66f));
            close.GetComponent<Image>().color = CareerUiTheme.ReferenceAccent;
            OwnerUiButtonSkin.Apply(close, OwnerButtonRole.Navigation);

            _tabs = new Button[3];
            string[] names = { "정규시즌", "포스트시즌", "시즌 결산" };
            for (int index = 0; index < _tabs.Length; index++)
            {
                int selected = index;
                _tabs[index] = OwnerWorkspaceUiFactory.CreateButton(modal, "Tab" + index, names[index], () => SetPage(selected));
                SetRect(_tabs[index].GetComponent<RectTransform>(),
                    new Vector2(32f + index * 196f, 574f), new Vector2(216f + index * 196f, 622f));
                OwnerUiButtonSkin.Apply(_tabs[index], OwnerButtonRole.Tab);
            }
            _worldProgress = Label(modal, "WorldProgress", string.Empty, 13, FontStyle.Bold,
                Muted, new Vector2(650f, 574f), new Vector2(1128f, 622f));
            _worldProgress.alignment = TextAnchor.MiddleRight;

            RectTransform hero = OwnerRuntimeUiFactory.CreateRect("ResultHero", modal);
            SetRect(hero, new Vector2(42f, 198f), new Vector2(724f, 554f));
            _summary = Label(hero, "Summary", string.Empty, 40, FontStyle.Bold,
                Ivory, new Vector2(24f, 280f), new Vector2(658f, 336f));
            _summary.resizeTextForBestFit = true;
            _summary.resizeTextMinSize = 28;
            _summary.resizeTextMaxSize = 40;
            _status = Label(hero, "Status", string.Empty, 18, FontStyle.Normal,
                Gold, new Vector2(24f, 232f), new Vector2(658f, 276f));
            _detailsCaption = Label(hero, "DetailsCaption", string.Empty, 12, FontStyle.Bold,
                Gold, new Vector2(22f, 194f), new Vector2(652f, 226f));
            _details = Label(hero, "Details", string.Empty, 17, FontStyle.Normal,
                Ivory, new Vector2(22f, 18f), new Vector2(652f, 194f));
            _details.alignment = TextAnchor.UpperLeft;
            _details.lineSpacing = 1.12f;

            for (int index = 0; index < _seriesCards.Length; index++)
            {
                RectTransform card = Surface(hero, "Series" + index, NavySoft,
                    Vector2.zero, Vector2.zero, Vector2.zero);
                // 네 라운드가 같은 높이를 가져야 마지막 대진도 프레임과 본문이 뒤집히지 않는다.
                float bottom = 4f + (3 - index) * 56f;
                SetRect(card, new Vector2(22f, bottom), new Vector2(658f, bottom + 52f));
                _seriesCards[index] = card;
                _seriesTitles[index] = Label(card, "Round", string.Empty, 12, FontStyle.Bold,
                    Gold, new Vector2(16f, 26f), new Vector2(620f, 46f));
                _seriesTeams[index] = Label(card, "Teams", string.Empty, 15, FontStyle.Bold,
                    Ivory, new Vector2(16f, 6f), new Vector2(526f, 26f));
                _seriesScores[index] = Label(card, "Score", string.Empty, 18, FontStyle.Bold,
                    Ivory, new Vector2(536f, 4f), new Vector2(620f, 28f));
                _seriesScores[index].alignment = TextAnchor.MiddleRight;
            }

            for (int index = 0; index < _metricValues.Length; index++)
            {
                float left = 42f + index * 366f;
                RectTransform metric = Surface(modal, "Metric" + index, NavySoft,
                    Vector2.zero, Vector2.zero, Vector2.zero);
                SetRect(metric, new Vector2(left, 110f), new Vector2(left + 354f, 190f));
                _metricValues[index] = Label(metric, "Value", string.Empty, 27, FontStyle.Bold,
                    Ivory, new Vector2(24f, 12f), new Vector2(330f, 48f));
                _metricValues[index].alignment = TextAnchor.MiddleRight;
                _metricLabels[index] = Label(metric, "Label", string.Empty, 12, FontStyle.Bold,
                    Muted, new Vector2(24f, 48f), new Vector2(330f, 72f));
                _metricLabels[index].alignment = TextAnchor.MiddleLeft;
            }

            // 설명은 기록 카드의 형제 영역을 따로 소유한다. 카드 위에 문구를 덧그리지 않는다.
            RectTransform insight = Surface(modal, "NextStep", NavyPanel,
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(insight, new Vector2(760f, 216f), new Vector2(1128f, 304f));
            UIOwnerFrontOfficePanel.Apply(insight, "CompactStrip");
            _insightTitle = Label(insight, "InsightTitle", string.Empty, 13, FontStyle.Bold,
                Gold, new Vector2(24f, 56f), new Vector2(344f, 80f));
            _insightBody = Label(insight, "InsightBody", string.Empty, 14, FontStyle.Normal,
                Ivory, new Vector2(24f, 12f), new Vector2(344f, 56f));
            _insightBody.alignment = TextAnchor.UpperLeft;

            _primary = OwnerWorkspaceUiFactory.CreateButton(modal, "Primary", "포스트시즌 확인", HandlePrimary);
            SetRect(_primary.GetComponent<RectTransform>(), new Vector2(824f, 18f), new Vector2(1128f, 82f));
            _primary.GetComponent<Image>().color = CareerUiTheme.ReferenceAccent;
            OwnerUiButtonSkin.Apply(_primary, OwnerButtonRole.Primary);
            _primaryLabel = _primary.transform.Find("Label").GetComponent<Text>();
            _primaryLabel.fontSize = 18;
            _hint = Label(modal, "Hint", string.Empty, 15, FontStyle.Normal,
                Muted, new Vector2(42f, 18f), new Vector2(790f, 82f));
            _hint.alignment = TextAnchor.MiddleLeft;
            BuildBracketMotion();
            BuildRecapReport();
            FitModal();
        }

        private void SetPage(int page)
        {
            if (_snapshot == null) return;
            SkipBracketReveal();
            _page = page;
            _details.gameObject.SetActive(page != 1);
            _detailsCaption.gameObject.SetActive(page != 1);
            for (int index = 0; index < _seriesCards.Length; index++)
                _seriesCards[index].gameObject.SetActive(page == 1);
            for (int index = 0; index < _tabs.Length; index++)
            {
                // 선택 탭을 Disabled로 만들면 회색 비활성 UI처럼 보인다. 선택은 Skin 상태로만 표현한다.
                _tabs[index].interactable = index < 2 || _snapshot.IsPostseasonCompleted;
                OwnerUiButtonSkin.SetSelected(_tabs[index], index == page);
            }
            _eyebrow.text = $"시즌 {_snapshot.SeasonNumber}  /  {OwnerLeagueDisplayNameFormatter.FormatFull(_snapshot.CurrentGrade)}";
            _worldProgress.text = _snapshot.IsPostseasonCompleted
                ? "전체 포스트시즌 종료"
                : $"종료된 조  {_snapshot.CompletedPostseasonGroups} / {_snapshot.TotalPostseasonGroups}";
            _hint.text = _snapshot.IsPostseasonCompleted
                ? "시즌 결산에서 승강 결과와 다음 시즌 등급을 확인하세요."
                : "모든 조의 포스트시즌이 끝나면 승강 결과가 확정됩니다.";
            if (page == 0) BindPennantRace();
            else if (page == 1) BindPostseason();
            else BindRecap();
            SetRecapVisible(page == 2);
            ConfigureNavigation();
            if (gameObject.activeInHierarchy) PlayBracketReveal();
        }

        private void ConfigureNavigation()
        {
            int lastTab = _snapshot.IsPostseasonCompleted ? 2 : 1;
            for (int i = 0; i <= lastTab; i++)
                _tabs[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i == 0 ? _close : _tabs[i - 1],
                    selectOnRight = i == lastTab ? _close : _tabs[i + 1],
                    selectOnUp = _close, selectOnDown = _primary };
            _close.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _tabs[lastTab], selectOnRight = _tabs[0],
                selectOnUp = _primary, selectOnDown = _tabs[_page] };
            _primary.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _tabs[_page], selectOnRight = _close,
                selectOnUp = _tabs[_page], selectOnDown = _close };
            ConfigureRecapNavigation();
        }

        private void BindPennantRace()
        {
            _title.text = "정규시즌 결과";
            _summary.text = $"정규시즌 {_snapshot.Rank}위";
            _status.text = _snapshot.IsQualified ? "포스트시즌 진출" : "이번 시즌의 경기를 모두 마쳤습니다";
            _detailsCaption.text = Resolve(_snapshot.PlayerTeamSeasonKey);
            _details.text = _snapshot.IsQualified
                ? $"{_snapshot.TeamCount}개 구단 중 {_snapshot.Rank}위로 시즌을 마쳤습니다.\n포스트시즌에서 우승에 도전합니다."
                : $"{_snapshot.TeamCount}개 구단 중 {_snapshot.Rank}위로 시즌을 마쳤습니다.\n아쉽게 포스트시즌 진출권을 얻지 못했습니다.";
            SetMetrics(_snapshot.WinningPercentage.ToString(".000", CultureInfo.InvariantCulture), "승률",
                $"{_snapshot.Wins}승 {_snapshot.Draws}무 {_snapshot.Losses}패", "정규시즌 전적",
                FormatSigned(_snapshot.RunDifferential), "득실차");
            _insightTitle.text = _snapshot.IsQualified ? "다음 단계 · 포스트시즌" : "다음 단계 · 전체 결과 확정";
            _insightBody.text = _snapshot.IsQualified
                ? "대진을 확인하고\n첫 경기를 준비하세요."
                : "타 리그 결과까지 확정해\n다음 시즌 편성을 준비합니다.";
            _primaryLabel.text = "포스트시즌 확인";
            _primary.interactable = true;
        }

        private void BindPostseason()
        {
            _title.text = "포스트시즌";
            BindSeriesCards();
            if (!_snapshot.IsPostseasonCompleted)
            {
                if (_snapshot.IsPlayerPostseasonCompleted)
                {
                    _summary.text = FormatPostseasonResult(_snapshot.PostseasonResult);
                    _status.text = "우리 조의 포스트시즌이 끝났습니다";
                    _primaryLabel.text = "남은 리그 마감";
                }
                else
                {
                    _summary.text = _snapshot.CanWatchPlayerGame ? BuildNextGameTitle() : "우리 구단의 가을 야구 종료";
                    _status.text = _snapshot.CanWatchPlayerGame
                        ? BuildSeriesStakes()
                        : "남은 포스트시즌 결과를 확인하세요.";
                    if (_snapshot.IsQualified && !_snapshot.CanWatchPlayerGame)
                        _status.text = "포스트시즌 탈락 · 남은 대진의 우승 구단을 확인하세요.";
                    _primaryLabel.text = _snapshot.CanWatchPlayerGame ? "다음 경기 관전" : "남은 리그 마감";
                }
                string currentResult = !_snapshot.IsQualified ? "포스트시즌 미진출" :
                    !_snapshot.CanWatchPlayerGame && !_snapshot.IsPlayerPostseasonCompleted ? "포스트시즌 탈락" :
                    FormatPostseasonResult(_snapshot.PostseasonResult);
                SetMetrics(_snapshot.IsQualified ? $"{_snapshot.Rank}번" : "미진출", "우리 구단 시드",
                    $"{_snapshot.CompletedPostseasonGroups}/{_snapshot.TotalPostseasonGroups}", "완료된 리그",
                    currentResult, "현재 결과");
                _insightTitle.text = "다음 단계 · 전체 결과 확정";
                _insightBody.text = "모든 리그 우승팀이 확정되어야\n승강과 다음 시즌이 열립니다.";
                if (_snapshot.CanWatchPlayerGame)
                {
                    _hint.text = "우리 구단 경기를 한 경기씩 관전합니다. 경기 종료 후 대진으로 돌아옵니다.";
                    _insightTitle.text = "다음 경기 · 출전 준비";
                    _insightBody.text = "현재 선수 오더로 출전합니다.\n변경하려면 닫고 선수단을 여세요.";
                    OwnerPostseasonSeriesReview series = _snapshot.PlayerSeries;
                    if (series != null)
                        SetMetrics($"{series.HigherSeedWins} : {series.LowerSeedWins}", "시리즈 전적",
                            $"{series.GetWinsRequired(series.HigherSeedTeamSeasonKey == _snapshot.PlayerTeamSeasonKey)}승", "시리즈 승리 조건",
                            series.Round == OwnerPostseasonRound.Championship ? "우승" : "다음 라운드 진출", "이번 시리즈 목표");
                }
                _primary.interactable = true;
                return;
            }
            string result = FormatPostseasonResult(_snapshot.PostseasonResult);
            _summary.text = result;
            _status.text = "우승 구단  ·  " + Resolve(_snapshot.ChampionTeamSeasonKey);
            SetMetrics(_snapshot.IsQualified ? $"{_snapshot.Rank}번" : "미진출", "우리 구단 시드",
                $"{_snapshot.Series.Count}", "진행 시리즈",
                $"{_snapshot.CompletedPostseasonGroups}/{_snapshot.TotalPostseasonGroups}", "완료된 리그");
            _insightTitle.text = "다음 단계 · 시즌 결산";
            _insightBody.text = "승강 결과를 확인하고\n다음 시즌을 준비하세요.";
            _primaryLabel.text = "시즌 결산 보기";
            _primary.interactable = true;
        }

        private string BuildNextGameTitle()
        {
            OwnerPostseasonSeriesReview series = _snapshot.PlayerSeries;
            if (series == null) return "가을 야구, 첫 승을 향해";
            if (series.IsCompleted) return "다음 라운드 · 상대 결정 대기";
            string round = series.RoundTitle;
            return $"{round} {series.HigherSeedWins + series.LowerSeedWins + series.Draws + 1}차전";
        }

        private string BuildSeriesStakes()
        {
            OwnerPostseasonSeriesReview series = _snapshot.PlayerSeries;
            if (series == null || series.IsCompleted) return "앞선 대진이 끝나면 우리 구단 경기로 이어집니다.";
            bool higher = series.HigherSeedTeamSeasonKey == _snapshot.PlayerTeamSeasonKey;
            int wins = higher ? series.HigherSeedWins : series.LowerSeedWins;
            int losses = higher ? series.LowerSeedWins : series.HigherSeedWins;
            string target = series.Round == OwnerPostseasonRound.Championship ? "우승" : "다음 라운드 진출";
            if (series.Round == OwnerPostseasonRound.WildCard) return higher
                ? "4위 우대 · 한 번 이기거나 비기면 준플레이오프 진출"
                : "5위 도전 · 두 경기 모두 이겨야 준플레이오프 진출";
            if (wins == series.WinsRequired - 1 && losses == series.WinsRequired - 1)
                return $"최종전 · 오늘 승리하면 {target}, 패하면 탈락";
            if (wins == series.WinsRequired - 1) return $"매치 포인트 · 한 경기만 더 이기면 {target}";
            if (losses == series.WinsRequired - 1) return "벼랑 끝 승부 · 다음 경기에서 반드시 승리해야 합니다";
            return $"{wins}승 {losses}패 · {target}까지 {series.WinsRequired - wins}승";
        }

        private void BindSeriesCards()
        {
            for (int index = 0; index < _seriesCards.Length; index++)
            {
                // 다른 탭에서 비활성이었던 카드도 어두운 Skin의 본문 대비를 복원한다.
                _seriesTeams[index].color = Ivory;
                _seriesScores[index].color = Ivory;
                if (index >= _snapshot.Series.Count)
                {
                    _seriesCards[index].gameObject.SetActive(!_snapshot.IsPlayerPostseasonCompleted &&
                        index == _snapshot.Series.Count &&
                        (_snapshot.Series.Count == 0 || _snapshot.Series[_snapshot.Series.Count - 1].Round != OwnerPostseasonRound.Championship));
                    _seriesTitles[index].text = _snapshot.Series.Count == 0 ? "포스트시즌 · 대진 대기" : "다음 라운드 · 대진 대기";
                    _seriesTeams[index].text = _snapshot.Series.Count == 0
                        ? "정규시즌 순위로 대진을 확정합니다" : "앞선 라운드 승자가 상위 시드와 만납니다";
                    _seriesScores[index].text = "—";
                    continue;
                }
                OwnerPostseasonSeriesReview item = _snapshot.Series[index];
                bool ours = item.HigherSeedTeamSeasonKey == _snapshot.PlayerTeamSeasonKey ||
                    item.LowerSeedTeamSeasonKey == _snapshot.PlayerTeamSeasonKey;
                string round = item.RoundTitle;
                string state = item.IsCompleted ? "시리즈 종료" :
                    item.HigherSeedWins + item.LowerSeedWins == 0 ? "경기 전" : "진행 중";
                _seriesTitles[index].text = $"{round} · {item.SeriesRule} · {state}" + (ours ? " · 우리 구단" : "");
                _seriesTeams[index].text = Resolve(item.HigherSeedTeamSeasonKey) + "  vs  " + Resolve(item.LowerSeedTeamSeasonKey);
                _seriesScores[index].text = $"{item.HigherSeedWins} : {item.LowerSeedWins}";
            }
        }

        private void BindRecap()
        {
            _title.text = $"시즌 {_snapshot.SeasonNumber} 결산";
            _hint.text = "보유 선수와 1군 등록은 유지됩니다. 구단 홈에서 다음 시즌을 준비하세요.";
            _primaryLabel.text = "구단 홈으로";
            _primary.interactable = true;
            BindRecapReport();
        }

        private void HandlePrimary()
        {
            SkipBracketReveal();
            if (_page == 0) { SetPage(1); return; }
            if (_page == 1 && !_snapshot.IsPostseasonCompleted) { PostseasonRequested?.Invoke(); return; }
            if (_page == 1) { SetPage(2); return; }
            CloseRequested?.Invoke();
        }

        private void SetMetrics(string value0, string label0, string value1, string label1, string value2, string label2)
        {
            string[] values = { value0, value1, value2 };
            string[] labels = { label0, label1, label2 };
            for (int index = 0; index < _metricValues.Length; index++)
            {
                _metricValues[index].text = values[index];
                _metricLabels[index].text = labels[index];
            }
        }

        private string Resolve(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "미정";
            string value = _teamName(key);
            return string.IsNullOrWhiteSpace(value) || value == key ? "구단명 확인 중" : value;
        }

        private string FormatLeagueMovement()
        {
            if (!_snapshot.NextGrade.HasValue || _snapshot.NextGrade.Value == _snapshot.CurrentGrade)
                return "다음 시즌  ·  잔류";
            return _snapshot.NextGrade.Value > _snapshot.CurrentGrade
                ? "다음 시즌  ·  승격 확정"
                : "다음 시즌  ·  강등 확정";
        }

        private string FormatNextGrade() => _snapshot.NextGrade.HasValue
            ? OwnerLeagueDisplayNameFormatter.FormatFull(_snapshot.NextGrade.Value)
            : "판정 대기";

        private static string FormatPostseasonResult(OwnerTeamPostseasonResult? result) => result switch
        {
            OwnerTeamPostseasonResult.Champion => "포스트시즌 우승",
            OwnerTeamPostseasonResult.RunnerUp => "포스트시즌 준우승",
            OwnerTeamPostseasonResult.WildCardElimination => "와일드카드 탈락",
                OwnerTeamPostseasonResult.SemiPlayoffElimination => "준플레이오프 탈락",
                OwnerTeamPostseasonResult.PlayoffElimination => "플레이오프 탈락",
                OwnerTeamPostseasonResult.SemifinalElimination => "포스트시즌 탈락",
            OwnerTeamPostseasonResult.DidNotQualify => "포스트시즌 미진출",
            _ => "포스트시즌 진행 중"
        };

        private static string FormatSigned(int value) => value > 0 ? "+" + value : value.ToString();

        private static void AddBackgroundArt(RectTransform modal)
        {
            Image art = OwnerRuntimeUiFactory.CreateImage("SeasonBackdrop", modal, Color.white);
            art.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            OwnerRuntimeUiFactory.Stretch(art.rectTransform);
            art.sprite = LoadBackgroundSprite();
            art.preserveAspect = false;
            art.color = art.sprite == null ? Navy : new Color(0.88f, 0.91f, 0.95f, 1f);
            art.transform.SetAsFirstSibling();
        }

        private static Sprite LoadBackgroundSprite()
        {
            if (_backgroundSprite != null) return _backgroundSprite;
            Texture2D texture = Resources.Load<Texture2D>("UI/Generated/bg_owner_season_review_v1");
            if (texture == null) return null;
            _backgroundSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            _backgroundSprite.name = "OwnerSeasonReviewBackdrop";
            return _backgroundSprite;
        }

        private static RectTransform Surface(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FramedSurface);
            // 모든 표면에 역할을 보존해 Theme 재적용 시에도 레거시 프레임으로 돌아가지 않는다.
            UIOwnerFrontOfficePanel.Apply(rect, "CompactStrip");
            return rect;
        }

        private static RectTransform Decoration(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            image.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            image.color = color;
            return image.rectTransform;
        }

        private static Text Label(Transform parent, string name, string value, int size, FontStyle style,
            Color color, Vector2 min, Vector2 max)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, TextAnchor.MiddleLeft, color);
            // Factory의 밝은 업무 화면 색 변환을 거치지 않고, 결산 무대의 명시적 대비를 보존한다.
            text.color = color;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}

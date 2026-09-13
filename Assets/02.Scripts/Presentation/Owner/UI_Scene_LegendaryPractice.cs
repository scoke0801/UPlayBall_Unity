using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>공용 셸에서 역사 팀 탐색·선발 확인·누적 승리·최초 보상을 제공한다.</summary>
    public sealed class UI_Scene_LegendaryPractice : MonoBehaviour
    {
        private readonly Button[] _rows = new Button[10];
        private readonly Text[] _ranks = new Text[10], _rowStates = new Text[10];
        private readonly Image[,] _winMarks = new Image[10, 3];
        private readonly List<LegendaryPracticeTeam> _filtered = new List<LegendaryPracticeTeam>();
        private Text _teamTitle, _record, _rotation, _progress, _reward, _hint, _pageLabel, _completed, _detail;
        private Button _start, _claimAll, _previous, _next, _lineup, _retry, _restart;
        private Dropdown _filter;
        private int _featuredCount;
        private CanvasGroup _interaction;
        private LegendaryPracticeCatalog _catalog;
        private LegendaryPracticeState _state;
        private Func<LegendaryPracticeTeam, string> _name;
        private string _selected;
        private int _page, _decade;
        private bool _showLineup;
        private bool _canStart = true;
        private MatchRosterSnapshot[] _opponent;
        private MatchRosterSnapshot _playerRoster;
        private OwnerTeamLineupSnapshot _lineupSnapshot;
        private UI_Scene_OwnerTeamLineup _lineupBoard;
        private RectTransform[] _panels;
        private readonly PlayerMiniCardView[] _cards = new PlayerMiniCardView[3];
        public event Action<string> SelectionRequested, StartRequested, RestartRequested;
        public event Action ClaimAllRequested, LineupRequested, RetryRequested;
        public string SelectedTeamId => _selected;

        public static UI_Scene_LegendaryPractice CreateRuntime(Transform parent)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_LegendaryPractice), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Scene_LegendaryPractice>(); view.Build(); return view;
        }

        /// <summary>메뉴를 나갈 때 탐색 상태를 지워 다음 진입을 기본 도전 목록으로 시작한다.</summary>
        public void ResetNavigation()
        {
            _filter.Hide();
            _filter.SetValueWithoutNotify(0);
            _selected = null;
            _page = 0;
            _decade = 0;
            _showLineup = false;
            _lineupBoard?.SetVisible(false);
            foreach (var panel in _panels) panel.gameObject.SetActive(true);
            RenderLineup();
        }

        /// <summary>10개 행을 재사용하며 현재 도전 구간과 선택을 유지한다.</summary>
        public void Bind(LegendaryPracticeCatalog catalog, LegendaryPracticeState state, Func<LegendaryPracticeTeam, string> name)
        {
            _catalog = catalog; _state = state; _name = name;
            _interaction.interactable = true; _retry.gameObject.SetActive(false); _filter.interactable = true;
            Place(_hint.rectTransform, .03f, .25f, .97f, .36f);
            if (_selected == null)
            {
                _selected = catalog.teams[catalog.teams.Length - 1].challengeTeamId;
                for (int i = catalog.teams.Length - 1; i >= 0; i--)
                    if (state.CanPlay(catalog, catalog.teams[i]) && !state.Get(catalog.teams[i].challengeTeamId).HasCleared)
                    { _selected = catalog.teams[i].challengeTeamId; break; }
                _page = (catalog.teams.Length - catalog.Find(_selected).rank) / 10;
            }
            RebuildList(); RenderProgress(); SelectionRequested?.Invoke(_selected);
        }

        public void BindOpponent(MatchRosterSnapshot[] opponent, OwnerTeamLineupSnapshot lineup)
        {
            _opponent = opponent;
            _lineupSnapshot = lineup ?? throw new ArgumentNullException(nameof(lineup));
            var team = _catalog.Find(_selected);
            _teamTitle.text = _name(team);
            _record.text = "역대 강팀 " + team.rank + "위\n" + team.year + " 시즌의 선수단";
            var text = new StringBuilder("선발 로테이션\n");
            int next = _state.Get(_selected).NextStarterIndex;
            for (int i = 0; i < 5; i++) text.Append(i == next ? "▶ " : "   ").Append(i + 1).Append("선발  ")
                .Append(lineup.Pitchers[i].DisplayName).Append('\n');
            text.Append("\n팀컬러  ");
            bool hasColor = false;
            foreach (var name in lineup.TeamColors)
            {
                if (hasColor) text.Append('\n');
                text.Append(name); hasColor = true;
            }
            if (!hasColor) text.Append("발동 조건 미충족");
            _rotation.text = text.ToString();
            RenderLineup(); RenderProgress();
            if (_lineupBoard != null && _lineupBoard.gameObject.activeSelf) ShowOpponentLineup();
        }

        public void BindFeaturedCards(PlayerMiniCardModel[] cards)
        {
            _featuredCount = Math.Min(cards?.Length ?? 0, _cards.Length);
            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].gameObject.SetActive(i < _featuredCount && !_showLineup);
                if (i < _featuredCount) _cards[i].Bind(cards[i]);
            }
        }
        public void BindPlayerRoster(MatchRosterSnapshot roster)
        { _playerRoster = roster; _canStart = roster != null; RenderProgress(); }

        public void ShowError(string message)
        {
            _hint.text = message; _start.interactable = false; _restart.gameObject.SetActive(false); _retry.gameObject.SetActive(true);
            Place(_hint.rectTransform, .03f, .25f, .97f, .36f);
            if (_catalog == null)
            {
                _filter.interactable = _previous.interactable = _next.interactable = _claimAll.interactable = false;
                foreach (var row in _rows) row.gameObject.SetActive(false);
            }
        }
        public void SetBusy(bool busy)
        { _interaction.interactable = !busy; if (busy) _hint.text = "경기를 준비하고 있습니다…"; else RenderProgress(); }
        public void FocusAction() { if (_start.IsInteractable()) _start.Select(); else _lineup.Select(); }
        public bool TryGoBack()
        {
            // uGUI Dropdown은 펼침 상태를 공개하지 않으므로 생성된 목록의 존재로 확인한다.
            if (_filter.transform.Find("Dropdown List") != null)
            { _filter.Hide(); _filter.Select(); return true; }
            if (_lineupBoard != null && _lineupBoard.gameObject.activeSelf)
            {
                _lineupBoard.SetVisible(false);
                foreach (var panel in _panels) panel.gameObject.SetActive(true);
                _lineup.Select(); return true;
            }
            if (!_showLineup) return false;
            _showLineup = false; RenderLineup(); _lineup.Select(); return true;
        }

        private void ShowOpponentLineup()
        {
            if (_lineupSnapshot == null) return;
            if (_lineupBoard == null)
            {
                _lineupBoard = UI_Scene_OwnerTeamLineup.CreateRuntime((RectTransform)transform);
                OwnerRuntimeUiFactory.Stretch((RectTransform)_lineupBoard.transform);
                _lineupBoard.CloseRequested += () => TryGoBack();
            }
            foreach (var panel in _panels) panel.gameObject.SetActive(false);
            _lineupBoard.SetVisible(true);
            _lineupBoard.Bind(_lineupSnapshot, true, "역대 강팀으로 돌아가기",
                "카드 우클릭: 선수 상세 · " + CreateMatchRecord() + " · 경기 중 교체에 따라 출전 선수가 달라질 수 있습니다.");
            _lineupBoard.FocusClose();
        }

        private string CreateMatchRecord()
        {
            var progress = _state.Get(_selected);
            return progress.attempts == 0 ? "아직 대전하지 않았습니다" :
                "최근 경기 " + progress.lastPlayerScore + " : " + progress.lastOpponentScore + " · " +
                (progress.attempts - progress.losses - progress.draws) + "승 " + progress.losses + "패 " + progress.draws + "무";
        }

        private void RebuildList()
        {
            if (_catalog == null) return;
            _filtered.Clear();
            for (int i = _catalog.teams.Length - 1; i >= 0; i--)
                if (_decade == 0 || _catalog.teams[i].year / 10 * 10 == _decade) _filtered.Add(_catalog.teams[i]);
            _page = Math.Min(_page, Math.Max(0, (_filtered.Count - 1) / 10));
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = _page * 10 + i; bool visible = index < _filtered.Count;
                _rows[i].gameObject.SetActive(visible); if (!visible) continue;
                var team = _filtered[index]; var progress = _state.Get(team.challengeTeamId);
                bool selected = team.challengeTeamId == _selected;
                bool available = _state.CanPlay(_catalog, team);
                string status = progress.HasCleared ? !progress.rewardClaimed ? "보상 수령 가능" : "격파 완료 · " + progress.wins + " / 3승"
                    : available ? "도전 가능 · " + progress.wins + " / 3승" : "잠김 · " + (team.rank + 1) + "위 격파 시 해금";
                _rows[i].transform.Find("Label").GetComponent<Text>().text = _name(team);
                OwnerDashboardStyle.SetDataRow(_rows[i], selected,
                    i % 2 == 0 ? OwnerDashboardStyle.TableAlternate : OwnerDashboardStyle.InsetSurface);
                _ranks[i].text = team.rank.ToString("00") + "위";
                _ranks[i].color = selected ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Ivory;
                _rowStates[i].text = (selected ? "선택 · " : "") + status;
                _rowStates[i].color = available ? OwnerDashboardStyle.Ivory : OwnerDashboardStyle.Muted;
                for (int win = 0; win < 3; win++)
                    _winMarks[i, win].color = progress.wins > win ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Line;
            }
            _pageLabel.text = _filtered.Count == 0 ? "해당 시대의 팀이 없습니다" : (_page + 1) + " / " + ((_filtered.Count + 9) / 10);
            _previous.interactable = _page > 0; _next.interactable = (_page + 1) * 10 < _filtered.Count;
        }

        private void SelectRow(int slot)
        {
            int index = _page * 10 + slot; if (index >= _filtered.Count) return;
            _selected = _filtered[index].challengeTeamId; _showLineup = false;
            RebuildList(); RenderProgress(); SelectionRequested?.Invoke(_selected);
        }

        private void RenderProgress()
        {
            if (_catalog == null || _selected == null) return;
            var team = _catalog.Find(_selected); var progress = _state.Get(_selected);
            int complete = 0, ready = 0;
            foreach (var t in _catalog.teams)
            { var p = _state.Get(t.challengeTeamId); if (p.HasCleared) { complete++; if (!p.rewardClaimed) ready++; } }
            _completed.text = "역사의 벽을 넘어\n" + complete + " / " + _catalog.teams.Length + "팀 격파";
            _progress.text = "이번 도전\n" + (progress.wins >= 1 ? "●" : "○") + "   " +
                (progress.wins >= 2 ? "●" : "○") + "   " + (progress.wins >= 3 ? "●" : "○") + "\n" + progress.wins + " / 3승";
            _reward.text = (progress.rewardClaimed ? "최초 보상 수령 완료" : "3승 최초 보상") +
                "\n\n골드  " + team.rewardMoney.ToString("N0") + "\n육성 포인트  " + team.rewardDevelopment.ToString("N0") +
                "\nSP  " + team.rewardScouting.ToString("N0");
            bool available = _state.CanPlay(_catalog, team);
            bool canClaim = progress.HasCleared && !progress.rewardClaimed;
            _restart.gameObject.SetActive(!_retry.gameObject.activeSelf);
            _restart.interactable = available && progress.attempts > 0;
            _start.interactable = available && (_canStart || canClaim);
            _start.GetComponentInChildren<Text>().text = canClaim ? "보상 받기" : "경기 시작";
            _hint.text = !available ? "먼저 " + (team.rank + 1) + "위 팀에 3승을 달성해 주세요." :
                !_canStart && !canClaim ? "우리 선수 오더에서 출전 편성을 확인해 주세요." :
                progress.attempts == 0 ? "한 경기 도전 후 재도전할 수 있습니다.\n9이닝 · 연장 없음 · 체력 소모 없음" :
                "재도전: 0승 · 1선발부터 다시 시작\n" + (progress.rewardClaimed ? "최초 보상 수령 완료 · 추가 보상 없음" :
                    canClaim ? "최초 보상은 재도전 후에도 수령 가능" : "최초 3승 보상은 구단별 한 번만 지급");
            _claimAll.interactable = ready > 0;
            _claimAll.GetComponentInChildren<Text>().text = ready > 0 ? "미수령 보상 모두 받기 · " + ready : "미수령 보상 없음";
        }

        private void RenderLineup()
        {
            _detail.gameObject.SetActive(_showLineup);
            for (int i = 0; i < _cards.Length; i++) _cards[i].gameObject.SetActive(!_showLineup && i < _featuredCount);
            if (!_showLineup || _opponent == null) return;
            _detail.text = CreateMatchRecord();
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            _interaction = gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<CareerUiPreserveTextColor>();
            var list = OwnerRuntimeUiFactory.CreatePanel("Opponents", root, "역대 강팀 · 도전 목록");
            var center = OwnerRuntimeUiFactory.CreatePanel("Opponent", root, "상대 구단");
            var reward = OwnerRuntimeUiFactory.CreatePanel("Challenge", root, "도전 현황", true);
            _panels = new[] { list.Root, center.Root, reward.Root };
            StylePanel(list.Root); StylePanel(center.Root); StylePanel(reward.Root);
            Place(list.Root, .01f, .02f, .255f, .98f); Place(center.Root, .265f, .02f, .745f, .98f);
            Place(reward.Root, .755f, .02f, .99f, .98f);
            var filterTitle = Text(list.Content, "EraTitle", 16); filterTitle.text = "시대별 탐색";
            Place(filterTitle.rectTransform, .02f, .945f, .34f, 1);
            _filter = OwnerCardFilters.CreateDropdown(list.Content, "Era",
                new List<string> { "모든 시대", "1980년대", "1990년대", "2000년대", "2010년대", "2020년대" }, 0);
            _filter.onValueChanged.AddListener(index => { _decade = index == 0 ? 0 : 1970 + index * 10; _page = 0; RebuildList(); });
            Place((RectTransform)_filter.transform, .36f, .95f, .98f, 1);
            _filter.captionText.fontSize = 16;
            OwnerDashboardStyle.SetDataDropdown(_filter);
            var guide = Text(list.Content, "ChallengeGuide", 14); guide.text = "팀별 3승 달성 시 다음 순위 해금";
            guide.color = OwnerDashboardStyle.Muted; Place(guide.rectTransform, .02f, .905f, .98f, .945f);
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                _rows[i] = Button(list.Content, "Team" + i, "상대 구단", () => SelectRow(index));
                OwnerUiButtonSkin.Apply(_rows[i], OwnerButtonRole.Quiet);
                Place((RectTransform)_rows[i].transform, 0, .817f - i * .079f, 1, .893f - i * .079f);
                BuildChallengeRow(i);
            }
            _previous = Button(list.Content, "Previous", "이전", () => { _page--; RebuildList(); });
            _next = Button(list.Content, "Next", "다음", () => { _page++; RebuildList(); });
            Place((RectTransform)_previous.transform, 0, 0, .28f, .07f); Place((RectTransform)_next.transform, .72f, 0, 1, .07f);
            _pageLabel = Text(list.Content, "Page", 14); Place(_pageLabel.rectTransform, .30f, 0, .70f, .07f);
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            var hero = OwnerRuntimeUiFactory.CreateRect("StadiumViewport", center.Content);
            Place(hero, 0, .70f, 1, 1); hero.gameObject.AddComponent<RectMask2D>();
            var artwork = OwnerRuntimeUiFactory.CreateImage("StadiumArtwork", hero, Color.white);
            artwork.sprite = Resources.Load<Sprite>("UI/LegendaryPractice/stadium");
            OwnerRuntimeUiFactory.Stretch(artwork.rectTransform);
            var aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = artwork.sprite != null ? artwork.sprite.rect.width / artwork.sprite.rect.height : 1.5f;
            artwork.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            _teamTitle = Text(center.Content, "TeamTitle", 28); _teamTitle.color = Color.white;
            Place(_teamTitle.rectTransform, .04f, .84f, .96f, .98f);
            _record = Text(center.Content, "Record", 18); _record.color = Color.white;
            Place(_record.rectTransform, .04f, .71f, .96f, .84f);
            SectionSurface(center.Content, "FeaturedSurface", .02f, .355f, .98f, .70f);
            SectionSurface(center.Content, "RotationSurface", .02f, .08f, .98f, .345f);
            var featured = Text(center.Content, "FeaturedHeading", 16);
            featured.text = "주요 선수 · 코스트 TOP 3";
            Place(featured.rectTransform, .04f, .657f, .96f, .70f);
            for (int i = 0; i < 3; i++)
            { _cards[i] = PlayerMiniCardView.CreateRuntime(center.Content); Place((RectTransform)_cards[i].transform,
                .03f + i * .325f, .36f, .31f + i * .325f, .655f);
                _cards[i].Selected += card => ShowCardDetails(card); }
            _detail = Text(center.Content, "LineupDetails", 17); Place(_detail.rectTransform, .04f, .35f, .96f, .655f);
            _detail.gameObject.SetActive(false);
            _rotation = Text(center.Content, "Rotation", 17); Place(_rotation.rectTransform, .04f, .085f, .96f, .35f);
            _lineup = Button(center.Content, "Lineup", "상대 라인업 · 경기 기록", ShowOpponentLineup);
            Place((RectTransform)_lineup.transform, 0, 0, .43f, .07f);
            var compare = Button(center.Content, "Compare", "전력 비교", ShowComparison);
            Place((RectTransform)compare.transform, .45f, 0, .68f, .07f);
            var edit = Button(center.Content, "EditOrder", "우리 선수 오더", () => LineupRequested?.Invoke());
            Place((RectTransform)edit.transform, .70f, 0, 1, .07f);
            SectionSurface(reward.Content, "ProgressSurface", .01f, .615f, .99f, 1);
            SectionSurface(reward.Content, "RewardSurface", .01f, .365f, .99f, .605f);
            _completed = Text(reward.Content, "Completion", 23); Place(_completed.rectTransform, .05f, .84f, .95f, 1);
            _progress = Text(reward.Content, "Wins", 28); Place(_progress.rectTransform, .03f, .61f, .97f, .84f);
            _progress.color = OwnerDashboardStyle.Gold;
            _reward = Text(reward.Content, "Rewards", 20); Place(_reward.rectTransform, .03f, .37f, .97f, .60f);
            _hint = Text(reward.Content, "Hint", 15); Place(_hint.rectTransform, .03f, .25f, .97f, .36f);
            _restart = Button(reward.Content, "Restart", "재도전", () => RestartRequested?.Invoke(_selected));
            Place((RectTransform)_restart.transform, 0, .18f, 1, .24f);
            _retry = Button(reward.Content, "Retry", "다시 불러오기", () => RetryRequested?.Invoke());
            Place((RectTransform)_retry.transform, 0, .18f, 1, .24f); _retry.gameObject.SetActive(false);
            _start = Button(reward.Content, "Start", "경기 시작", () => StartRequested?.Invoke(_selected));
            OwnerUiButtonSkin.Apply(_start, OwnerButtonRole.Primary); Place((RectTransform)_start.transform, 0, .085f, 1, .17f);
            _claimAll = Button(reward.Content, "ClaimAll", "미수령 보상 없음", () => ClaimAllRequested?.Invoke());
            Place((RectTransform)_claimAll.transform, 0, 0, 1, .07f);
        }

        private void BuildChallengeRow(int index)
        {
            var row = _rows[index].transform;
            var label = row.Find("Label").GetComponent<Text>();
            Place(label.rectTransform, .17f, .40f, .97f, .98f);
            label.alignment = TextAnchor.MiddleLeft;
            _ranks[index] = Text(row, "Rank", 18);
            Place(_ranks[index].rectTransform, .025f, .36f, .16f, .98f);
            _ranks[index].alignment = TextAnchor.MiddleCenter;
            OwnerDashboardStyle.SetTypography(_ranks[index], true);
            _rowStates[index] = Text(row, "ChallengeState", 13);
            Place(_rowStates[index].rectTransform, .17f, .04f, .97f, .42f);
            for (int win = 0; win < 3; win++)
            {
                var mark = OwnerRuntimeUiFactory.CreateImage("Win" + win, row, OwnerDashboardStyle.Line);
                Place(mark.rectTransform, .027f + win * .041f, .17f, .060f + win * .041f, .23f);
                mark.raycastTarget = false; _winMarks[index, win] = mark;
            }
            OwnerDashboardStyle.Rule(row, "Divider", Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0, 1), OwnerDashboardStyle.Line);
        }

        private static Text Text(Transform parent, string name, int size)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, "", size, FontStyle.Normal,
                TextAnchor.MiddleLeft, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetTypography(text, size >= 23); return text;
        }
        private static Button Button(Transform parent, string name, string label, Action action)
        {
            var button = OwnerWorkspaceUiFactory.CreateButton(parent, name, label, action);
            OwnerUiButtonSkin.SetDashboardStyle(button);
            var text = button.GetComponentInChildren<Text>(); text.fontSize = 17;
            OwnerDashboardStyle.SetTypography(text); return button;
        }
        private static void StylePanel(RectTransform root)
        {
            UIOwnerFrontOfficePanel.Apply(root, "ManagerReport");
            root.Find("ThinBorder").gameObject.SetActive(false);
            var header = root.Find("HeaderSurface").GetComponent<Image>(); header.color = OwnerDashboardStyle.Raised;
            var title = root.Find("HeaderSlot").GetComponent<Text>(); title.color = OwnerDashboardStyle.Ivory;
            OwnerDashboardStyle.SetTypography(title, true); title.fontSize = 20;
        }
        private static void SectionSurface(Transform parent, string name, float x, float y, float right, float top)
        {
            var image = OwnerRuntimeUiFactory.CreateImage(name, parent, OwnerDashboardStyle.InsetSurface);
            Place(image.rectTransform, x, y, right, top);
            OwnerDashboardStyle.ApplyInset(image);
        }
        private static void Place(RectTransform rect, float x, float y, float right, float top)
        { rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(right, top); rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private void ShowCardDetails(PlayerMiniCardModel card)
        {
            _showLineup = true; RenderLineup();
            var text = new StringBuilder(card.DisplayName).Append(" · ").Append(card.EditionLabel).Append('\n');
            foreach (var stat in card.Stats) text.Append(stat.Label).Append("  ").Append(stat.Value).Append('\n');
            _detail.text = text.ToString(); _lineup.Select();
        }

        private void ShowComparison()
        {
            if (_playerRoster == null || _opponent == null) return;
            _showLineup = true; RenderLineup();
            double Mean(MatchRosterSnapshot roster, Func<Baseball.Core.Players.BatterAttributes, int> read)
            { double total = 0; for (int i = 0; i < 9; i++) total += read(roster.StartingLineup[i].Player.BatterAttributes); return total / 9; }
            var opponent = _opponent[_state.Get(_selected).NextStarterIndex];
            string Row(string label, double player, double away) => label + "   " + player.ToString("0") + "  /  " + away.ToString("0") +
                (Math.Abs(player - away) < 1 ? "  대등" : player > away ? "  우리 우세" : "  상대 우세") + "\n";
            _detail.text = "우리 구단 / 상대 구단\n\n" + Row("교타력", Mean(_playerRoster, a => a.Contact), Mean(opponent, a => a.Contact)) +
                Row("장타력", Mean(_playerRoster, a => a.Power), Mean(opponent, a => a.Power)) +
                Row("주력", Mean(_playerRoster, a => a.Speed), Mean(opponent, a => a.Speed)) +
                Row("수비력", Mean(_playerRoster, a => a.Defense), Mean(opponent, a => a.Defense)) +
                Row("선발 제구", _playerRoster.StartingPitcher.Player.PitcherAttributes.Control, opponent.StartingPitcher.Player.PitcherAttributes.Control);
        }
    }
}

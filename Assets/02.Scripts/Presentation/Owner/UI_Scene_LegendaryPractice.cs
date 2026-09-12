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
        private readonly List<LegendaryPracticeTeam> _filtered = new List<LegendaryPracticeTeam>();
        private Text _teamTitle, _record, _rotation, _progress, _reward, _hint, _pageLabel, _completed, _detail;
        private Button _start, _claimAll, _previous, _next, _filter, _lineup, _retry;
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
        private readonly PlayerMiniCardView[] _cards = new PlayerMiniCardView[3];
        public event Action<string> SelectionRequested, StartRequested;
        public event Action ClaimAllRequested, LineupRequested, RetryRequested;
        public string SelectedTeamId => _selected;

        public static UI_Scene_LegendaryPractice CreateRuntime(Transform parent)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_LegendaryPractice), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Scene_LegendaryPractice>(); view.Build(); return view;
        }

        /// <summary>10개 행을 재사용하며 현재 도전 구간과 선택을 유지한다.</summary>
        public void Bind(LegendaryPracticeCatalog catalog, LegendaryPracticeState state, Func<LegendaryPracticeTeam, string> name)
        {
            _catalog = catalog; _state = state; _name = name;
            _interaction.interactable = true; _retry.gameObject.SetActive(false); _filter.interactable = true;
            Place(_hint.rectTransform, .03f, .18f, .97f, .32f);
            if (_selected == null)
            {
                _selected = catalog.teams[catalog.teams.Length - 1].challengeTeamId;
                for (int i = catalog.teams.Length - 1; i >= 0; i--)
                    if (state.CanPlay(catalog, catalog.teams[i]) && state.Get(catalog.teams[i].challengeTeamId).wins < 3)
                    { _selected = catalog.teams[i].challengeTeamId; break; }
                _page = (catalog.teams.Length - catalog.Find(_selected).rank) / 10;
            }
            RebuildList(); RenderProgress(); SelectionRequested?.Invoke(_selected);
        }

        public void BindOpponent(MatchRosterSnapshot[] opponent, TeamColorDefinition[] colors)
        {
            _opponent = opponent;
            var team = _catalog.Find(_selected);
            _teamTitle.text = _name(team);
            _record.text = "역대 강팀 " + team.rank + "위\n" + team.year + " 시즌의 선수단";
            var text = new StringBuilder("선발 로테이션\n");
            int next = _state.Get(_selected).NextStarterIndex;
            for (int i = 0; i < 5; i++) text.Append(i == next ? "▶ " : "   ").Append(i + 1).Append("선발  ")
                .Append(opponent[i].StartingPitcher.Player.Name).Append('\n');
            text.Append("\n팀컬러  ");
            bool hasColor = false;
            foreach (var color in colors)
                if (color != null) { if (hasColor) text.Append(" · "); text.Append(color.DisplayName); hasColor = true; }
            if (!hasColor) text.Append("발동 조건 미충족");
            _rotation.text = text.ToString();
            RenderLineup(); RenderProgress();
        }

        public void BindFeaturedCards(PlayerMiniCardModel[] cards)
        { for (int i = 0; i < _cards.Length; i++) _cards[i].Bind(cards[i]); }
        public void BindPlayerRoster(MatchRosterSnapshot roster)
        { _playerRoster = roster; _canStart = roster != null; RenderProgress(); }

        public void ShowError(string message)
        {
            _hint.text = message; _start.interactable = false; _retry.gameObject.SetActive(true);
            Place(_hint.rectTransform, .03f, .245f, .97f, .32f);
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
        { if (!_showLineup) return false; _showLineup = false; RenderLineup(); _lineup.Select(); return true; }

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
                string status = progress.wins == 3 ? "완료" : _state.CanPlay(_catalog, team) ? progress.wins + "/3승" : "잠김";
                _rows[i].GetComponentInChildren<Text>().text = team.rank + "위  " + _name(team) + "\n" + status;
                OwnerUiButtonSkin.Apply(_rows[i], team.challengeTeamId == _selected ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
                OwnerUiButtonSkin.SetDashboardStyle(_rows[i]);
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
            { var p = _state.Get(t.challengeTeamId); if (p.wins == 3) { complete++; if (!p.rewardClaimed) ready++; } }
            _completed.text = "역사의 벽을 넘어\n" + complete + " / " + _catalog.teams.Length + "팀 격파";
            _progress.text = "누적 승리\n" + (progress.wins >= 1 ? "●" : "○") + "   " +
                (progress.wins >= 2 ? "●" : "○") + "   " + (progress.wins >= 3 ? "●" : "○") + "\n" + progress.wins + " / 3승";
            _reward.text = (progress.rewardClaimed ? "최초 보상 수령 완료" : "3승 최초 보상") +
                "\n\n골드  " + team.rewardMoney.ToString("N0") + "\n육성 포인트  " + team.rewardDevelopment.ToString("N0") +
                "\nSP  " + team.rewardScouting.ToString("N0");
            bool available = _state.CanPlay(_catalog, team);
            bool canClaim = progress.wins == 3 && !progress.rewardClaimed;
            _start.interactable = available && (_canStart || canClaim);
            _start.GetComponentInChildren<Text>().text = progress.wins == 3 && !progress.rewardClaimed ? "보상 받기" :
                progress.wins == 3 ? "다시 도전" : "경기 시작";
            _hint.text = !available ? "먼저 " + (team.rank + 1) + "위 팀에 3승을 달성해 주세요." :
                !_canStart && !canClaim ? "우리 선수 오더에서 출전 편성을 확인해 주세요." :
                progress.rewardClaimed ? "재도전에서는 추가 보상이 지급되지 않습니다." : "9이닝 · 연장 없음\n패배해도 승수 유지 · 체력 소모 없음";
            _claimAll.interactable = ready > 0;
            _claimAll.GetComponentInChildren<Text>().text = ready > 0 ? "미수령 보상 모두 받기 · " + ready : "미수령 보상 없음";
        }

        private void RenderLineup()
        {
            _detail.gameObject.SetActive(_showLineup);
            foreach (var card in _cards) card.gameObject.SetActive(!_showLineup);
            if (!_showLineup || _opponent == null) return;
            var text = new StringBuilder("상대 선발 타순\n"); int order = 0;
            for (int slotIndex = 0; slotIndex < _opponent[0].StartingLineup.Count; slotIndex++)
                text.Append(++order).Append("번  ").Append(_opponent[0].StartingLineup[slotIndex].Player.Name)
                    .Append(slotIndex % 2 == 0 && slotIndex < 8 ? "     " : "\n");
            text.Append("벤치  ");
            for (int i = 0; i < _opponent[0].Bench.Count; i++)
                text.Append(_opponent[0].Bench[i].Name).Append(i % 3 == 2 ? "\n        " : "  ");
            text.Append("\n불펜  ");
            for (int i = 0; i < _opponent[0].Bullpen.Count; i++)
                text.Append(_opponent[0].Bullpen[i].Player.Name).Append(i % 3 == 2 && i < 5 ? "\n        " : "  ");
            var p = _state.Get(_selected);
            text.Append("\n최근 경기  ").Append(p.attempts == 0 ? "아직 대전하지 않았습니다" : p.lastPlayerScore + " : " + p.lastOpponentScore)
                .Append("\n").Append(p.attempts - p.losses - p.draws).Append("승 · ").Append(p.losses).Append("패 · ")
                .Append(p.draws).Append("무  |  최고 득실차 ").Append(p.bestRunDifference > 0 ? "+" : "").Append(p.bestRunDifference);
            _detail.text = text.ToString();
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            _interaction = gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<CareerUiPreserveTextColor>();
            var list = OwnerRuntimeUiFactory.CreatePanel("Opponents", root, "역대 강팀 · 도전 목록");
            var center = OwnerRuntimeUiFactory.CreatePanel("Opponent", root, "상대 구단");
            var reward = OwnerRuntimeUiFactory.CreatePanel("Challenge", root, "도전 현황", true);
            StylePanel(list.Root); StylePanel(center.Root); StylePanel(reward.Root);
            Place(list.Root, .01f, .02f, .255f, .98f); Place(center.Root, .265f, .02f, .745f, .98f);
            Place(reward.Root, .755f, .02f, .99f, .98f);
            _filter = Button(list.Content, "Era", "모든 시대", () => { _decade = _decade == 0 ? 1980 : _decade == 2020 ? 0 : _decade + 10;
                _filter.GetComponentInChildren<Text>().text = _decade == 0 ? "모든 시대" : _decade + "년대"; _page = 0; RebuildList(); });
            Place((RectTransform)_filter.transform, 0, .92f, 1, 1);
            for (int i = 0; i < 10; i++)
            { int index = i; _rows[i] = Button(list.Content, "Team" + i, "", () => SelectRow(index));
                Place((RectTransform)_rows[i].transform, 0, .83f - i * .077f, 1, .90f - i * .077f); }
            _previous = Button(list.Content, "Previous", "이전", () => { _page--; RebuildList(); });
            _next = Button(list.Content, "Next", "다음", () => { _page++; RebuildList(); });
            Place((RectTransform)_previous.transform, 0, 0, .28f, .07f); Place((RectTransform)_next.transform, .72f, 0, 1, .07f);
            _pageLabel = Text(list.Content, "Page", 14); Place(_pageLabel.rectTransform, .30f, 0, .70f, .07f);
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
            for (int i = 0; i < 3; i++)
            { _cards[i] = PlayerMiniCardView.CreateRuntime(center.Content); Place((RectTransform)_cards[i].transform,
                .03f + i * .325f, .36f, .31f + i * .325f, .68f);
                _cards[i].Selected += card => ShowCardDetails(card); }
            _detail = Text(center.Content, "LineupDetails", 17); Place(_detail.rectTransform, .04f, .35f, .96f, .69f);
            _detail.gameObject.SetActive(false);
            _rotation = Text(center.Content, "Rotation", 17); Place(_rotation.rectTransform, .04f, .085f, .96f, .35f);
            _lineup = Button(center.Content, "Lineup", "상대 라인업 · 경기 기록", () => { _showLineup = !_showLineup; RenderLineup(); });
            Place((RectTransform)_lineup.transform, 0, 0, .43f, .07f);
            var compare = Button(center.Content, "Compare", "전력 비교", ShowComparison);
            Place((RectTransform)compare.transform, .45f, 0, .68f, .07f);
            var edit = Button(center.Content, "EditOrder", "우리 선수 오더", () => LineupRequested?.Invoke());
            Place((RectTransform)edit.transform, .70f, 0, 1, .07f);
            _completed = Text(reward.Content, "Completion", 23); Place(_completed.rectTransform, .03f, .84f, .97f, 1);
            _progress = Text(reward.Content, "Wins", 28); Place(_progress.rectTransform, .03f, .61f, .97f, .84f);
            _progress.color = OwnerDashboardStyle.Gold;
            _reward = Text(reward.Content, "Rewards", 20); Place(_reward.rectTransform, .03f, .32f, .97f, .61f);
            _hint = Text(reward.Content, "Hint", 15); Place(_hint.rectTransform, .03f, .18f, .97f, .32f);
            _retry = Button(reward.Content, "Retry", "다시 불러오기", () => RetryRequested?.Invoke());
            Place((RectTransform)_retry.transform, 0, .18f, 1, .24f); _retry.gameObject.SetActive(false);
            _start = Button(reward.Content, "Start", "경기 시작", () => StartRequested?.Invoke(_selected));
            OwnerUiButtonSkin.Apply(_start, OwnerButtonRole.Primary); Place((RectTransform)_start.transform, 0, .085f, 1, .17f);
            _claimAll = Button(reward.Content, "ClaimAll", "미수령 보상 없음", () => ClaimAllRequested?.Invoke());
            Place((RectTransform)_claimAll.transform, 0, 0, 1, .07f);
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
            OwnerDashboardStyle.ApplySurface(root, true);
            root.Find("ThinBorder").gameObject.SetActive(false);
            var header = root.Find("HeaderSurface").GetComponent<Image>(); header.color = OwnerDashboardStyle.Raised;
            var title = root.Find("HeaderSlot").GetComponent<Text>(); title.color = OwnerDashboardStyle.Ivory;
            OwnerDashboardStyle.SetTypography(title, true); title.fontSize = 20;
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
            _detail.text = "우리 구단 / 상대 구단\n\n" + Row("교타", Mean(_playerRoster, a => a.Contact), Mean(opponent, a => a.Contact)) +
                Row("장타", Mean(_playerRoster, a => a.Power), Mean(opponent, a => a.Power)) +
                Row("주력", Mean(_playerRoster, a => a.Speed), Mean(opponent, a => a.Speed)) +
                Row("수비", Mean(_playerRoster, a => a.Defense), Mean(opponent, a => a.Defense)) +
                Row("선발 제구", _playerRoster.StartingPitcher.Player.PitcherAttributes.Control, opponent.StartingPitcher.Player.PitcherAttributes.Control);
        }
    }
}

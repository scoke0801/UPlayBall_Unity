using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>오프시즌 선택의 대상·비용·결과를 비교하고 저장 가능한 명령만 확정한다.</summary>
    public sealed class UI_Popup_OwnerDevelopment : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private OwnerModeManager _manager;
        private RectTransform _frame, _players, _body;
        private Text _status, _feedback;
        private Button _confirm, _close, _cancel;
        private GameObject _previousFocus;
        private string _cardId = "";
        private int _tab, _page, _option, _decrease, _increase = 1, _amount = 1, _blockIndex;
        private bool _pitchers, _researchPitchers, _isConfirming, _isSubmitting;
        private int _researchAbility, _researchRarity = 1;
        private Action _action;
        private string _actionLabel;
        private long _cost;
        private readonly List<Selectable> _focus = new List<Selectable>();
        private readonly OwnerCardFilters _origins = new OwnerCardFilters();
        private readonly List<PlayerPosition> _positions = new List<PlayerPosition>();
        private readonly Button[] _playerRows = new Button[4];
        private readonly string[] _rowCardIds = new string[4];
        private OwnerCollectionSnapshot _roster;
        private Text _playerCount, _playerEmpty, _selectionHint;
        private Button _previousPage, _nextPage, _batters, _pitcherButton;
        private int _positionFilter, _registrationFilter, _sort;
        private string _query = "";
        private readonly string[] _tabs = { "능력치 교정", "훈련 파트너", "스킬 연구·합성", "슬로건", "유학 관리" };

        public static UI_Popup_OwnerDevelopment Show(Transform parent, OwnerModeManager manager)
        {
            if (manager?.Runtime == null) throw new InvalidOperationException("구단주 진행 상태를 불러오지 못했습니다.");
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerDevelopment), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .94f);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerDevelopment>();
            view._manager = manager; view._previousFocus = EventSystem.current?.currentSelectedGameObject;
            view._roster = new OwnerModeRuntimeSnapshotFactory().CreateCollectionSummary(manager);
            view.Build(); view.Refresh(); view._close.Select(); return view;
        }
        private void Build()
        {
            _frame = OwnerDugoutDetailUiFactory.CreatePanel(transform, "DevelopmentOffice", .02f, .035f, .98f, .965f);
            UIOwnerFrontOfficePanel.Apply(_frame, "ManagerReport");
            _status = Text(_frame, "Title", "성장 관리", .025f, .935f, .81f, .98f, 23);
            Text(_frame, "Workflow", "성장 방법 선택  →  대상·효과 확인  →  비용 확인 후 실행", .025f, .885f, .81f, .93f, 15);
            _close = Button(_frame, "Close", "닫기", .865f, .905f, .975f, .975f, Close);
            for (int i = 0; i < _tabs.Length; i++)
            {
                int tab = i;
                Button(_frame, "Tab" + i, _tabs[i], .025f + i * .19f, .805f, .209f + i * .19f, .875f,
                    () => { _tab = tab; _option = 0; _isConfirming = false; Refresh(); });
            }
            _players = OwnerDugoutDetailUiFactory.CreatePanel(_frame, "Players", .025f, .17f, .345f, .785f);
            _body = OwnerDugoutDetailUiFactory.CreatePanel(_frame, "Operation", .36f, .17f, .975f, .785f);
            UIOwnerFrontOfficePanel.Apply(_players, "ManagerReport");
            UIOwnerFrontOfficePanel.Apply(_body, "ManagerReport");
            BuildPlayerControls();
            _feedback = Text(_frame, "Feedback", "", .025f, .025f, .685f, .145f, 17);
            _confirm = Button(_frame, "Confirm", "확정", .79f, .035f, .975f, .13f, Confirm);
            OwnerUiButtonSkin.Apply(_confirm, OwnerButtonRole.Primary);
            _cancel = Button(_frame, "Cancel", "취소", .69f, .035f, .775f, .13f, () => { _isConfirming = false; Refresh(); });
        }
        private void Refresh()
        {
            var focused = EventSystem.current?.currentSelectedGameObject;
            string focusName = focused != null ? focused.GetComponentInParent<Dropdown>()?.name ?? focused.name : null;
            _action = null; _cost = 0; _actionLabel = "선택 필요";
            OwnerRuntimeUiFactory.ClearChildren(_body);
            bool needsPlayer = _tab == 0 || _tab == 1 || _tab == 4;
            _players.gameObject.SetActive(needsPlayer);
            Place(_body, needsPlayer ? .36f : .025f, .17f, .975f, .785f);
            _status.text = "성장 관리 · 남은 일정 " + _manager.Runtime.PlayerGrowth.Offseason.RemainingWeeks + "주 · " + OwnerMoneyFormatter.Format(_manager.Runtime.Economy.Money);
            _feedback.text = "변화와 비용을 확인한 뒤 확정하세요.";
            try
            {
                BuildPlayers();
                if (needsPlayer && string.IsNullOrEmpty(_cardId))
                    Text(_body, "ChoosePlayer", "성장할 선수를 선택하세요.\n왼쪽에서 검색하거나 필터를 초기화할 수 있습니다.", .06f, .35f, .94f, .65f, 22);
                else switch (_tab) { case 0: BuildCorrection(); break; case 1: BuildPartner(); break;
                    case 2: BuildResearch(); break; case 3: BuildSlogan(); break; case 4: BuildStudyManagement(); break; }
            }
            catch (InvalidOperationException e) { _feedback.text = e.Message; }
            catch (Exception) { _feedback.text = "성장 정보를 읽지 못했습니다. 창을 닫고 다시 열어 주세요."; }
            bool allowed = OwnerScheduleGateService.GetPhase(_manager.Runtime) == OwnerSeasonPhase.Offseason;
            _confirm.interactable = allowed && _action != null && !_isSubmitting && _manager.Runtime.Economy.Money >= _cost;
            if (!allowed) _feedback.text = "모든 조의 포스트시즌이 끝나면 성장 관리를 실행할 수 있습니다.";
            else if (_action != null && _manager.Runtime.Economy.Money < _cost) _feedback.text = "자금이 부족합니다. 비용 " + OwnerMoneyFormatter.Format(_cost);
            else if (_action != null) _feedback.text = _actionLabel + "  ·  " + (_cost == 0 ? "자금 소모 없음" : "비용 " + OwnerMoneyFormatter.Format(_cost))
                + "\n실행을 누르면 최종 확인 후 적용합니다.";
            if (_isConfirming) _feedback.text = _actionLabel + " · 비용 " + OwnerMoneyFormatter.Format(_cost) + "\n선택한 결과를 적용하고 저장할까요?";
            _confirm.GetComponentInChildren<Text>().text = _isConfirming ? "최종 확정" : _actionLabel;
            _cancel.gameObject.SetActive(_isConfirming);
            foreach (var button in _frame.GetComponentsInChildren<Button>())
                for (int tab = 0; tab < _tabs.Length; tab++)
                    if (button.name == "Tab" + tab) { OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab); OwnerUiButtonSkin.SetSelected(button, tab == _tab); }
            _confirm.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            LinkFocus(focusName);
        }
        private void BuildPlayerControls()
        {
            Text(_players, "PlayersTitle", "대상 선수", .045f, .92f, .65f, .98f, 19);
            Button(_players, "ResetFilters", "초기화", .73f, .92f, .955f, .98f, ResetPlayerFilter);
            _batters = Button(_players, "Batters", "타자", .045f, .845f, .49f, .905f, () => ChangePlayerType(false));
            _pitcherButton = Button(_players, "Pitchers", "투수", .51f, .845f, .955f, .905f, () => ChangePlayerType(true));
            var field = DefaultControls.CreateInputField(new DefaultControls.Resources());
            field.name = "PlayerSearch"; field.transform.SetParent(_players, false);
            field.AddComponent<CareerUiPreserveTextColor>();
            Place(field.GetComponent<RectTransform>(), .045f, .765f, .955f, .83f);
            var search = field.GetComponent<InputField>(); search.characterLimit = 40;
            foreach (var label in field.GetComponentsInChildren<Text>())
            { label.font = UIProjectFonts.Body; label.fontSize = 15; label.fontStyle = FontStyle.Normal; }
            ((Text)search.placeholder).text = "선수 이름 검색";
            search.onValueChanged.AddListener(value => { _query = value; ChangeFilter(); });
            var positionLabels = new List<string> { "전체 포지션" };
            foreach (PlayerPosition position in Enum.GetValues(typeof(PlayerPosition)))
            { _positions.Add(position); positionLabels.Add(OwnerCollectionPresentationBuilder.FormatPosition(position)); }
            AddDropdown(_players, "PositionFilter", positionLabels, _positionFilter, .045f, .69f, .49f, .75f,
                value => { _positionFilter = value; ChangeFilter(); });
            AddDropdown(_players, "RegistrationFilter", new List<string> { "전체 등록 상태", "1군 등록", "1군 미등록" }, _registrationFilter,
                .51f, .69f, .955f, .75f, value => { _registrationFilter = value; ChangeFilter(); });
            var origins = OwnerDugoutDetailUiFactory.CreateRect(_players, "OriginFilters", .045f, .615f, .955f, .675f);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(origins, 8);
            _origins.Build(origins, _roster.Cards, ChangeFilter);
            _playerCount = Text(_players, "ResultCount", "", .045f, .545f, .48f, .60f, 14);
            AddDropdown(_players, "PlayerSort", new List<string> { "코스트 높은 순", "코스트 낮은 순", "이름순", "최신 연도순" }, _sort,
                .51f, .545f, .955f, .60f, value => { _sort = value; ChangeFilter(); });
            for (int i = 0; i < _playerRows.Length; i++)
            {
                int slot = i; float top = .525f - i * .103f;
                _playerRows[i] = Button(_players, "PlayerRow" + i, "선수", .045f, top - .095f, .955f, top,
                    () => { _cardId = _rowCardIds[slot]; _isConfirming = false; _option = 0; Refresh(); });
                OwnerUiButtonSkin.Apply(_playerRows[i], OwnerButtonRole.ListItem);
            }
            _playerEmpty = Text(_players, "EmptyPlayers", "조건에 맞는 선수가 없습니다.\n검색어를 바꾸거나 필터를 초기화하세요.", .06f, .22f, .94f, .46f, 16);
            _previousPage = Button(_players, "PreviousPlayers", "이전", .045f, .035f, .29f, .10f, () => { _page--; Refresh(); });
            _nextPage = Button(_players, "NextPlayers", "다음", .71f, .035f, .955f, .10f, () => { _page++; Refresh(); });
            _selectionHint = Text(_players, "SelectionHint", "", .31f, .035f, .69f, .10f, 12);
            _selectionHint.alignment = TextAnchor.MiddleCenter;
        }
        private void BuildPlayers()
        {
            var cards = new List<OwnerCollectionCardSnapshot>();
            foreach (var card in _roster.Cards)
            {
                bool pitcher = card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher;
                if (pitcher != _pitchers || !_origins.Matches(card)) continue;
                if (_positionFilter > 0 && card.Position != _positions[_positionFilter - 1]) continue;
                if (_registrationFilter == 1 && !card.IsActiveRoster || _registrationFilter == 2 && card.IsActiveRoster) continue;
                if (_query.Trim().Length > 0 && card.DisplayName.IndexOf(_query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                cards.Add(card);
            }
            cards.Sort((a, b) =>
            {
                int comparison = _sort switch { 1 => a.Cost.CompareTo(b.Cost), 2 => StringComparer.CurrentCulture.Compare(a.DisplayName, b.DisplayName),
                    3 => b.OriginYear.CompareTo(a.OriginYear), _ => b.Cost.CompareTo(a.Cost) };
                return comparison != 0 ? comparison : string.CompareOrdinal(a.CardId, b.CardId);
            });
            // 필터는 탐색 조건이다. 이미 선택한 실행 대상을 다른 선수로 몰래 바꾸지 않는다.
            if (string.IsNullOrEmpty(_cardId) && cards.Count > 0) _cardId = cards[0].CardId;
            int pages = Math.Max(1, (cards.Count + _playerRows.Length - 1) / _playerRows.Length);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _playerCount.text = cards.Count + "명 · " + (_page + 1) + "/" + pages + " 페이지";
            _playerEmpty.gameObject.SetActive(cards.Count == 0);
            _selectionHint.text = !string.IsNullOrEmpty(_cardId) && !cards.Exists(card => card.CardId == _cardId)
                ? "선택 선수는\n필터 밖에 있음" : "선수 선택 후\n오른쪽 효과 확인";
            for (int i = 0; i < _playerRows.Length; i++)
            {
                int index = _page * _playerRows.Length + i;
                _playerRows[i].gameObject.SetActive(index < cards.Count);
                if (index >= cards.Count) continue;
                var card = cards[index]; _rowCardIds[i] = card.CardId;
                _playerRows[i].GetComponentInChildren<Text>().text = (card.CardId == _cardId ? "선택 · " : "") + card.DisplayName + " · " + card.OriginYear
                    + "\n" + OwnerCollectionPresentationBuilder.FormatPosition(card.Position) + " · 코스트 " + card.Cost + " · " + (card.IsActiveRoster ? "1군" : "미등록");
                OwnerUiButtonSkin.SetSelected(_playerRows[i], card.CardId == _cardId);
            }
            _previousPage.interactable = _page > 0; _nextPage.interactable = _page + 1 < pages;
            OwnerUiButtonSkin.SetSelected(_batters, !_pitchers); OwnerUiButtonSkin.SetSelected(_pitcherButton, _pitchers);
        }
        private void ChangeFilter() { _page = 0; _isConfirming = false; Refresh(); }
        private void ChangePlayerType(bool pitchers)
        {
            _pitchers = pitchers; _cardId = ""; _positionFilter = 0;
            _players.Find("PositionFilter").GetComponent<Dropdown>().SetValueWithoutNotify(0);
            ChangeFilter();
        }
        private void ResetPlayerFilter()
        {
            _query = ""; _positionFilter = _registrationFilter = _sort = _page = 0; _origins.Reset();
            _positions.Clear(); OwnerRuntimeUiFactory.ClearChildren(_players); BuildPlayerControls(); ChangeFilter();
        }
        private static void Place(RectTransform rect, float l, float b, float r, float t) =>
            OwnerRuntimeUiFactory.SetAnchors(rect, new Vector2(l, b), new Vector2(r, t), Vector2.zero, Vector2.zero);
        private static Dropdown AddDropdown(Transform parent, string name, List<string> options, int selected,
            float l, float b, float r, float t, Action<int> changed)
        {
            var dropdown = OwnerCardFilters.CreateDropdown(parent, name, options, selected);
            Place(dropdown.GetComponent<RectTransform>(), l, b, r, t);
            dropdown.onValueChanged.AddListener(value => changed(value)); return dropdown;
        }
        private OwnedPlayerCardState Card()
        {
            if (!_manager.Runtime.TryGetOwnedCard(_cardId, out var card)) throw new InvalidOperationException("성장할 선수를 선택하세요.");
            return card;
        }
        private PlayerSeasonDefinition Season(string id)
        {
            _manager.Runtime.WorldCardCatalog.TryGetCard(id, out var card);
            return _manager.Runtime.WorldCardCatalog.GetPlayerSeason(card);
        }
        private string Name(string id) => _manager.Runtime.IdentityRegistry.GetPresentationPlayerName(Season(id).PlayerPersonId);
        private void Command(string label, long cost, Action action) { _actionLabel = label; _cost = cost; _action = action; }
        private void BuildCorrection()
        {
            var owned = Card(); int first = _pitchers ? 6 : 0;
            PlayerAbility decrease = (PlayerAbility)(first + _decrease), increase = (PlayerAbility)(first + _increase);
            Text(_body, "Title", Name(_cardId) + " · 교정 " + OwnerPermanentGrowthService.Count(owned, OwnerGrowthSource.Correction) + "/3회", .035f, .85f, .96f, .97f, 22);
            var abilityLabels = new List<string>();
            for (int i = 0; i < 6; i++) abilityLabels.Add(OwnerGrowthHistoryFormatter.GetAbilityName((PlayerAbility)(first + i)));
            Text(_body, "DecreaseLabel", "줄일 능력치", .035f, .765f, .34f, .83f, 15);
            Text(_body, "IncreaseLabel", "높일 능력치", .36f, .765f, .665f, .83f, 15);
            Text(_body, "AmountLabel", "이동할 수치", .69f, .765f, .965f, .83f, 15);
            AddDropdown(_body, "Decrease", abilityLabels, _decrease, .035f, .67f, .34f, .75f,
                value => { _decrease = value; _isConfirming = false; Refresh(); });
            AddDropdown(_body, "Increase", abilityLabels, _increase, .36f, .67f, .665f, .75f,
                value => { _increase = value; _isConfirming = false; Refresh(); });
            AddDropdown(_body, "Amount", new List<string> { "1 이동", "2 이동", "3 이동" }, _amount - 1, .69f, .67f, .965f, .75f,
                value => { _amount = value + 1; _isConfirming = false; Refresh(); });
            Text(_body, "CorrectionGuide", "능력치 총합을 유지하며 원하는 강점에 재분배합니다.", .035f, .57f, .965f, .64f, 15);
            var preview = OwnerPermanentGrowthService.PreviewCorrection(_manager.Runtime, _cardId, decrease, increase, _amount);
            var bases = Season(_cardId).CreateBaseAttributes();
            Text(_body, "ColumnAbility", "능력치", .04f, .50f, .29f, .56f, 15);
            Text(_body, "ColumnBase", "기본", .32f, .50f, .43f, .56f, 15);
            Text(_body, "ColumnCurrent", "기존 교정", .49f, .50f, .64f, .56f, 15);
            Text(_body, "ColumnDelta", "이번 변화", .67f, .50f, .82f, .56f, 15);
            Text(_body, "ColumnAfter", "교정 후", .85f, .50f, .96f, .56f, 15);
            for (int i = 0; i < 6; i++)
            {
                var ability = (PlayerAbility)(first + i); int current = owned.Training.Ledger.Get(OwnerGrowthSource.Correction, ability);
                Text(_body, "Stat" + i, OwnerGrowthHistoryFormatter.GetAbilityName(ability), .04f, .43f - i * .065f, .29f, .50f - i * .065f, 17);
                Text(_body, "Base" + i, bases.Get(ability).ToString(), .32f, .43f - i * .065f, .43f, .50f - i * .065f, 17);
                Text(_body, "Current" + i, Signed(current), .49f, .43f - i * .065f, .60f, .50f - i * .065f, 17);
                Text(_body, "Delta" + i, Signed(preview[first + i]), .67f, .43f - i * .065f, .78f, .50f - i * .065f, 17);
                Text(_body, "After" + i, (bases.Get(ability) + current + preview[first + i]).ToString(), .85f, .43f - i * .065f, .96f, .50f - i * .065f, 17);
            }
            int expected = owned.Training.Ledger.Count;
            Command("교정 · 총합 유지", _manager.GetDevelopmentBalance().correctionCost, () => _manager.CorrectCard(_cardId, decrease, increase, _amount, expected));
        }
        private void BuildPartner()
        {
            Card(); Text(_body, "Title", Name(_cardId) + " · 추천 훈련 파트너", .035f, .85f, .96f, .97f, 22);
            var candidates = OwnerPermanentGrowthService.Recommend(_manager.Runtime, _cardId, _manager.GetDevelopmentBalance().partner);
            if (candidates.Count == 0)
            {
                Text(_body, "NoPartners", "추천할 수 있는 훈련 파트너가 없습니다.\n\n같은 유형의 다른 선수를 선택해 보세요.\n이미 참여했거나 파견 중인 선수, 유효한 능력차가 없는 선수는 제외됩니다.", .06f, .20f, .94f, .76f, 18);
                throw new InvalidOperationException("다른 대상을 선택하면 참여 가능한 파트너를 다시 확인할 수 있습니다.");
            }
            _option %= Math.Min(3, candidates.Count);
            for (int i = 0; i < Math.Min(3, candidates.Count); i++)
            {
                int index = i; var candidate = candidates[i];
                var button = Button(_body, "Partner" + i, (i == _option ? "선택 · " : "") + Name(candidate.PartnerCardId) + " · 예상 성장 +" + candidate.Total,
                    .035f, .67f - i * .17f, .48f, .80f - i * .17f, () => { _option = index; _isConfirming = false; Refresh(); });
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.ListItem); OwnerUiButtonSkin.SetSelected(button, i == _option);
            }
            var selected = candidates[_option];
            Text(_body, "Preview", selected.Reason + "\n\n" + Changes(selected.Values) + "\n\n두 선수 모두 이번 오프시즌 참여를 사용합니다.\n결과는 영구 성장으로 남습니다.", .52f, .07f, .96f, .80f, 18);
            Command("파트너 훈련", _manager.GetDevelopmentBalance().partnerCost, () => _manager.TrainWithPartner(_cardId, selected.PartnerCardId));
        }
        private void BuildResearch()
        {
            var balance = _manager.GetDevelopmentBalance(); var inventory = _manager.Runtime.PlayerGrowth.Inventory;
            Text(_body, "Title", "스킬 연구소", .025f, .90f, .46f, .97f, 23);
            Text(_body, "Inventory", "누적 연구 " + inventory.ResearchCount + "회  ·  S 선택 상자 " + inventory.SelectionBoxes + "개",
                .52f, .90f, .975f, .97f, 17).alignment = TextAnchor.MiddleRight;
            string[] modes = { "블록 연구", "블록 합성", "S 상자 사용" };
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                var button = Button(_body, "ResearchMode" + i, modes[i], .025f + i * .32f, .79f, .33f + i * .32f, .875f,
                    () => { _option = mode; if (mode == 2) _researchRarity = 3; _isConfirming = false; Refresh(); });
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.Tab); OwnerUiButtonSkin.SetSelected(button, i == _option);
            }
            if (_option == 0)
            {
                Text(_body, "ResearchHeading", "새로운 스킬 블록 2개 획득", .045f, .62f, .60f, .73f, 26);
                Text(_body, "ResearchDescription", "연구한 블록은 구단 공용 인벤토리에 보관됩니다.\n획득 후 스킬 블록 배치 화면에서 선수에게 장착하세요.", .045f, .46f, .60f, .60f, 18);
                Text(_body, "ResearchRates", "획득 등급  ·  C " + balance.researchWeights[0] + "%   /   B " + balance.researchWeights[1] + "%   /   A " + balance.researchWeights[2] + "%",
                    .045f, .34f, .60f, .43f, 17);
                Text(_body, "ResearchPity", "S 선택 상자까지 " + (10 - inventory.ResearchCount % 10) + "회\n연구 10회마다 원하는 S 블록을 선택할 수 있습니다.", .045f, .12f, .60f, .29f, 18);
                var reward = OwnerDugoutDetailUiFactory.CreatePanel(_body, "ResearchReward", .64f, .13f, .955f, .73f);
                UIOwnerFrontOfficePanel.Apply(reward, "ManagerReport");
                Text(reward, "RewardTitle", "이번 연구 보상", .08f, .77f, .92f, .92f, 19);
                Text(reward, "RewardCount", "스킬 블록 2개", .08f, .51f, .92f, .70f, 26);
                Text(reward, "RewardCost", OwnerMoneyFormatter.Format(balance.researchCost) + "\n등급과 모양은 무작위로 결정됩니다.", .08f, .12f, .92f, .42f, 17);
                Command("블록 2개 연구", balance.researchCost, _manager.ResearchSkillBlocks); return;
            }
            Text(_body, "ChooseResult", "1  획득할 블록 선택", .035f, .67f, .57f, .75f, 19);
            AddDropdown(_body, "BlockType", new List<string> { "타자 스킬", "투수 스킬" }, _researchPitchers ? 1 : 0, .035f, .56f, .20f, .64f,
                value => { _researchPitchers = value == 1; _blockIndex = 0; _isConfirming = false; Refresh(); });
            var abilities = new List<string>();
            for (int i = 0; i < 6; i++) abilities.Add(OwnerGrowthHistoryFormatter.GetAbilityName((PlayerAbility)((_researchPitchers ? 6 : 0) + i)));
            AddDropdown(_body, "BlockAbility", abilities, _researchAbility, .215f, .56f, .40f, .64f,
                value => { _researchAbility = value; _blockIndex = 0; _isConfirming = false; Refresh(); });
            var rarity = AddDropdown(_body, "BlockRarity", new List<string> { "B 등급", "A 등급", "S 등급" }, _researchRarity - 1, .415f, .56f, .57f, .64f,
                value => { _researchRarity = value + 1; _blockIndex = 0; _isConfirming = false; Refresh(); });
            rarity.interactable = _option != 2;
            var filterAbility = (PlayerAbility)((_researchPitchers ? 6 : 0) + _researchAbility);
            var definitions = new List<SkillBlockDefinition>();
            foreach (var definition in _manager.Balance.Growth.SkillBlocks)
                if ((int)definition.Rarity == _researchRarity)
                    foreach (var bonus in definition.AbilityBonuses)
                        if (bonus.Ability == filterAbility) { definitions.Add(definition); break; }
            if (definitions.Count == 0) throw new InvalidOperationException("조건에 맞는 블록이 없습니다. 능력치·등급을 변경하세요.");
            definitions.Sort((a, b) => string.CompareOrdinal(a.BlockId, b.BlockId));
            _blockIndex = (_blockIndex + definitions.Count) % definitions.Count;
            var selected = definitions[_blockIndex];
            var preview = OwnerDugoutDetailUiFactory.CreatePanel(_body, "BlockPreview", .14f, .16f, .465f, .52f);
            UIOwnerFrontOfficePanel.Apply(preview, "ManagerReport");
            DrawBlock(preview, selected);
            Button(_body, "BlockPrevious", "이전 모양", .035f, .25f, .13f, .39f, () => { _blockIndex--; _isConfirming = false; Refresh(); });
            Button(_body, "BlockNext", "다음 모양", .475f, .25f, .57f, .39f, () => { _blockIndex++; _isConfirming = false; Refresh(); });
            Text(_body, "BlockPage", (_blockIndex + 1) + " / " + definitions.Count + "  ·  " + selected.ShapeCells.Length + "칸", .14f, .065f, .465f, .14f, 16).alignment = TextAnchor.MiddleCenter;
            Text(_body, "ResultHeading", "2  획득 효과·재료 확인", .63f, .67f, .955f, .75f, 19);
            var changes = new int[PlayerAbilityCatalog.AbilityCount];
            foreach (var bonus in selected.AbilityBonuses) changes[(int)bonus.Ability] += bonus.Amount;
            Text(_body, "BlockEffect", (_researchRarity == 1 ? "B" : _researchRarity == 2 ? "A" : "S") + " 등급  ·  " + Changes(changes).TrimEnd(), .63f, .49f, .955f, .62f, 25);
            Text(_body, "SetBonus", selected.AdjacencySetBonus > 0 ? "같은 계열 3블록 인접 배치 시\n세트 효과 +" + selected.AdjacencySetBonus : "획득 후 스킬 블록 배치에서 장착하세요.", .63f, .35f, .955f, .47f, 17);
            if (_option == 1)
            {
                int count = OwnerSkillResearchService.GetFusionMaterials(_manager.Runtime, _manager.Balance.Growth.SkillBlocks, selected.Category, selected.Rarity - 1).Count;
                Text(_body, "Materials", "합성 재료  " + count + " / 5개\n동일 계열 · 한 단계 낮은 등급\n미장착 블록을 보유 순서대로 5개 소모합니다.", .63f, .08f, .955f, .31f, 17);
                if (count < 5) throw new InvalidOperationException("합성 재료가 " + (5 - count) + "개 부족합니다. 같은 계열 하위 등급의 미장착 블록이 필요합니다.");
                Command("블록 5개 소모 · 합성", 0, () => _manager.FuseSkillBlocks(selected.BlockId));
            }
            else
            {
                Text(_body, "BoxCost", "S 선택 상자 1개 사용\n보유 " + inventory.SelectionBoxes + "개 · 선택한 모양을 확정 획득", .63f, .12f, .955f, .31f, 17);
                if (inventory.SelectionBoxes == 0) throw new InvalidOperationException("S 선택 상자가 없습니다. 블록 연구를 10회 완료하면 1개를 받습니다.");
                Command("S 상자 1개 사용", 0, () => _manager.OpenSkillSelectionBox(selected.BlockId));
            }
        }
        private static void DrawBlock(RectTransform parent, SkillBlockDefinition definition)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var cell in definition.ShapeCells)
            { minX = Math.Min(minX, cell.X); minY = Math.Min(minY, cell.Y); maxX = Math.Max(maxX, cell.X); maxY = Math.Max(maxY, cell.Y); }
            // 칸마다 패널 스킨을 씌우지 않는다. 카드 성장판과 동일한 정사각형 타일·등급 문양을 사용한다.
            var square = OwnerDugoutDetailUiFactory.CreateRect(parent, "SquareBounds", .10f, .10f, .90f, .90f);
            var aspect = square.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
            int extent = Math.Max(maxX - minX + 1, maxY - minY + 1);
            float size = .78f / extent;
            float left = .5f - (maxX - minX + 1) * size * .5f;
            float top = .5f + (maxY - minY + 1) * size * .5f;
            foreach (var cell in definition.ShapeCells)
            {
                float x = left + (cell.X - minX) * size, y = top - (cell.Y - minY) * size;
                var rect = OwnerDugoutDetailUiFactory.CreateRect(square, "SkillTile" + cell.X + "_" + cell.Y, x, y - size, x + size, y);
                SkillBlockVisual.ApplyDirectionalTile(rect.gameObject.AddComponent<RawImage>(), definition.Rarity,
                    definition.ShapeCells, Array.IndexOf(definition.ShapeCells, cell));
            }
        }
        private void BuildSlogan()
        {
            var definitions = _manager.GetDevelopmentBalance().slogans; _option %= definitions.Length;
            Text(_body, "Title", "시즌 방향을 정하는 슬로건", .035f, .85f, .96f, .97f, 22);
            for (int i = 0; i < definitions.Length; i++)
            {
                int index = i; var slogan = definitions[i];
                int count = OwnerSloganService.CountProgress(_manager.Runtime, slogan);
                var button = Button(_body, "Slogan" + i, (i == _option ? "선택 · " : "") + slogan.name + "\n해당 카드 수집 " + count + "장 · Lv." + OwnerSloganService.GetLevel(_manager.Runtime, slogan),
                    .035f, .61f - i * .19f, .47f, .78f - i * .19f, () => { _option = index; _isConfirming = false; Refresh(); });
                OwnerUiButtonSkin.Apply(button, OwnerButtonRole.ListItem); OwnerUiButtonSkin.SetSelected(button, i == _option);
            }
            var selected = definitions[_option]; int level = OwnerSloganService.GetLevel(_manager.Runtime, selected);
            var preview = new StringBuilder("대상: " + (selected.reliefOnly ? "불펜 " : selected.target == OwnerSupportTarget.Batter ? "타자 " : "투수 ")
                + OwnerGrowthHistoryFormatter.GetAbilityName(selected.requiredAbility) + " " + selected.minimumAbility + " 이상\n\n");
            for (int i = 0; i < 6; i++) preview.Append("Lv.").Append(i + 1).Append(" · 수집 ").Append(selected.cardsRequired[i]).Append("장 · ")
                .Append("기준 ").Append(selected.GetMinimumAbility(i + 1)).Append("+ · ")
                .Append(OwnerGrowthHistoryFormatter.GetAbilityName(selected.bonusAbility)).Append(Signed(selected.bonusByLevel[i])).Append(" / ")
                .Append(OwnerGrowthHistoryFormatter.GetAbilityName(selected.penaltyAbility)).Append(Signed(selected.penaltyByLevel[i])).Append('\n');
            preview.Append("\n1군 중 조건에 맞는 선수에게 적용합니다.\n시즌 중에는 교체하거나 강화할 수 없습니다.");
            Text(_body, "Levels", preview.ToString(), .52f, .05f, .96f, .80f, 17);
            if (level == 0) throw new InvalidOperationException("표시된 수집 조건을 달성하면 선택할 수 있습니다.");
            Command("Lv." + level + " 슬로건 선택", 0, () => _manager.SelectSlogan(selected.id));
        }
        private void BuildStudyManagement()
        {
            Card(); Text(_body, "Title", Name(_cardId) + " · 해외훈련 관리", .035f, .85f, .96f, .97f, 22);
            CardStudyProjectState project = null;
            foreach (var candidate in _manager.Runtime.PlayerGrowth.StudyProjects) if (candidate.CardId == _cardId) project = candidate;
            if (project == null)
            {
                Text(_body, "Help", "현재 유학 중인 선수가 아닙니다.\n\n창을 닫고 유학 지도에서 목적지·기간·성장 방향을 비교해 신청하세요.\n지역 1주 · 국가 2주 · 세계 3주\n\n완전 실패는 없으며, 공개된 확률로 대성공 보상이 추가됩니다.", .035f, .12f, .95f, .78f, 20);
                return;
            }
            var program = _manager.Balance.OwnerCardGrowth.GetStudyProgram(project.ProgramId);
            Text(_body, "Project", program.DisplayName + "\n" + (project.DurationWeeks - project.RemainingWeeks) + "주 진행 · " + project.RemainingWeeks + "주 후 귀환"
                + "\n\n취소 환급: " + OwnerMoneyFormatter.Format(OwnerStudyCancellationService.GetMoneyRefund(project))
                + (project.PaidDevelopmentPoints > 0 ? " · 육성 포인트 " + OwnerStudyCancellationService.GetPointRefund(project) : "")
                + "\n\n진행 전 취소는 전액, 진행 후 취소는 절반을 환급합니다.\n취소하면 이번 유학의 성장 보상을 받지 않습니다.", .035f, .12f, .95f, .78f, 20);
            Command("유학 취소·귀환", 0, () => _manager.CancelStudy(_cardId));
        }
        private void Confirm()
        {
            if (_isSubmitting || !_confirm.interactable || _action == null) return;
            if (!_isConfirming) { _isConfirming = true; Refresh(); return; }
            _isSubmitting = true; string message;
            try { _action(); _roster = new OwnerModeRuntimeSnapshotFactory().CreateCollectionSummary(_manager); message = "선택한 결과를 적용하고 저장했습니다."; }
            catch (InvalidOperationException e) { message = e.Message; }
            catch (Exception) { message = "저장하지 못했습니다. 변경 전 상태에서 다시 시도할 수 있습니다."; }
            finally { _isSubmitting = false; _isConfirming = false; }
            Refresh(); _feedback.text = message;
        }
        public bool TryHandleCancel() { if (_isConfirming) { _isConfirming = false; Refresh(); } else Close(); return true; }
        public void OnCancel(BaseEventData data) { TryHandleCancel(); data.Use(); }
        public void Close()
        {
            if (_isSubmitting) return;
            if (_previousFocus != null && _previousFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(_previousFocus);
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
        private void LateUpdate()
        {
            var current = EventSystem.current?.currentSelectedGameObject;
            if (current == null || !current.transform.IsChildOf(transform)) _close.Select();
        }
        private void LinkFocus(string name)
        {
            _focus.Clear(); foreach (var control in GetComponentsInChildren<Selectable>()) if (control.interactable && control.gameObject.activeInHierarchy) _focus.Add(control);
            for (int i = 0; i < _focus.Count; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = _focus[(i + _focus.Count - 1) % _focus.Count], selectOnLeft = _focus[(i + _focus.Count - 1) % _focus.Count],
                    selectOnDown = _focus[(i + 1) % _focus.Count], selectOnRight = _focus[(i + 1) % _focus.Count] };
                _focus[i].navigation = navigation;
                if (_focus[i].name == name && EventSystem.current?.currentSelectedGameObject != _focus[i].gameObject) _focus[i].Select();
            }
        }
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
        private static string Changes(int[] values)
        {
            var result = new StringBuilder();
            for (int i = 0; i < values.Length; i++) if (values[i] != 0) result.Append(OwnerGrowthHistoryFormatter.GetAbilityName((PlayerAbility)i)).Append(' ').Append(Signed(values[i])).Append('\n');
            return result.ToString();
        }
        private static Text Text(Transform parent, string name, string value, float l, float b, float r, float t, int size)
        {
            var text = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, value, l, b, r, t, size, FontStyle.Normal, TextAnchor.UpperLeft);
            OwnerDashboardStyle.SetTypography(text, size >= 21); text.horizontalOverflow = HorizontalWrapMode.Wrap; return text;
        }
        private static Button Button(Transform parent, string name, string text, float l, float b, float r, float t, Action action)
        {
            var button = OwnerDugoutDetailUiFactory.CreateButton(parent, name, text, l, b, r, t, action);
            OwnerDashboardStyle.SetTypography(button.GetComponentInChildren<Text>());
            return button;
        }
    }
}

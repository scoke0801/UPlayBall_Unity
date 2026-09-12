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
        private bool _pitchers, _automaticReturn = true, _isConfirming, _isSubmitting;
        private int _unlockX = -1, _unlockY = -1;
        private int _researchAbility, _researchRarity = 1;
        private Action _action;
        private string _actionLabel;
        private long _cost;
        private readonly List<Button> _focus = new List<Button>();
        private readonly string[] _tabs = { "전지훈련", "능력치 교정", "훈련 파트너", "스킬 연구·합성", "슬로건", "유학 관리" };

        public static UI_Popup_OwnerDevelopment Show(Transform parent, OwnerModeManager manager)
        {
            if (manager?.Runtime == null) throw new InvalidOperationException("구단주 진행 상태를 불러오지 못했습니다.");
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerDevelopment), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .75f);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerDevelopment>();
            view._manager = manager; view._previousFocus = EventSystem.current?.currentSelectedGameObject;
            view.Build(); view.Refresh(); view._close.Select(); return view;
        }
        private void Build()
        {
            _frame = OwnerDugoutDetailUiFactory.CreatePanel(transform, "DevelopmentOffice", .02f, .035f, .98f, .965f);
            _status = Text(_frame, "Title", "오프시즌 성장 관리", .025f, .90f, .81f, .975f, 23);
            _close = Button(_frame, "Close", "닫기", .865f, .905f, .975f, .975f, Close);
            for (int i = 0; i < _tabs.Length; i++)
            {
                int tab = i;
                Button(_frame, "Tab" + i, _tabs[i], .025f + i * .158f, .815f, .177f + i * .158f, .885f,
                    () => { _tab = tab; _option = 0; _unlockX = -1; _isConfirming = false; Refresh(); });
            }
            _players = OwnerDugoutDetailUiFactory.CreatePanel(_frame, "Players", .025f, .17f, .275f, .79f);
            _body = OwnerDugoutDetailUiFactory.CreatePanel(_frame, "Operation", .29f, .17f, .975f, .79f);
            _feedback = Text(_frame, "Feedback", "", .025f, .025f, .685f, .145f, 17);
            _confirm = Button(_frame, "Confirm", "확정", .79f, .035f, .975f, .13f, Confirm);
            OwnerUiButtonSkin.Apply(_confirm, OwnerButtonRole.Primary);
            _cancel = Button(_frame, "Cancel", "취소", .69f, .035f, .775f, .13f, () => { _isConfirming = false; _unlockX = -1; Refresh(); });
        }
        private void Refresh()
        {
            string focusName = EventSystem.current?.currentSelectedGameObject?.name;
            _action = null; _cost = 0; _actionLabel = "선택 필요";
            OwnerRuntimeUiFactory.ClearChildren(_players); OwnerRuntimeUiFactory.ClearChildren(_body);
            _status.text = "성장 관리 · 남은 일정 " + _manager.Runtime.PlayerGrowth.Offseason.RemainingWeeks + "주 · " + _manager.Runtime.Economy.Money.ToString("N0") + " PT";
            _feedback.text = "변화와 비용을 확인한 뒤 확정하세요.";
            try
            {
                BuildPlayers();
                switch (_tab) { case 0: BuildCamp(); break; case 1: BuildCorrection(); break; case 2: BuildPartner(); break;
                    case 3: BuildResearch(); break; case 4: BuildSlogan(); break; case 5: BuildStudyManagement(); break; }
            }
            catch (InvalidOperationException e) { _feedback.text = e.Message; }
            catch (Exception) { _feedback.text = "성장 정보를 읽지 못했습니다. 창을 닫고 다시 열어 주세요."; }
            bool allowed = OwnerScheduleGateService.GetPhase(_manager.Runtime) == OwnerSeasonPhase.Offseason;
            _confirm.interactable = allowed && _action != null && !_isSubmitting && _manager.Runtime.Economy.Money >= _cost;
            if (!allowed) _feedback.text = "모든 조의 포스트시즌이 끝나면 성장 관리를 실행할 수 있습니다.";
            else if (_action != null && _manager.Runtime.Economy.Money < _cost) _feedback.text = "필요 PT가 부족합니다. 비용 " + _cost.ToString("N0") + " PT";
            if (_isConfirming) _feedback.text = _actionLabel + " · 비용 " + _cost.ToString("N0") + " PT\n선택한 결과를 적용하고 저장할까요?";
            _confirm.GetComponentInChildren<Text>().text = _isConfirming ? "최종 확정" : _actionLabel;
            _cancel.gameObject.SetActive(_isConfirming);
            foreach (var button in _frame.GetComponentsInChildren<Button>())
                for (int tab = 0; tab < _tabs.Length; tab++)
                    if (button.name == "Tab" + tab) OwnerUiButtonSkin.Apply(button, tab == _tab ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
            _confirm.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            LinkFocus(focusName);
        }
        private void BuildPlayers()
        {
            Button(_players, "Batters", "타자", .04f, .90f, .47f, .98f, () => { _pitchers = false; ResetPlayerFilter(); });
            Button(_players, "Pitchers", "투수", .53f, .90f, .96f, .98f, () => { _pitchers = true; ResetPlayerFilter(); });
            var cards = new List<OwnedPlayerCardState>();
            var runtime = _manager.Runtime;
            foreach (var owned in runtime.OwnedCards)
            {
                runtime.WorldCardCatalog.TryGetCard(owned.CardId, out var definition);
                if ((runtime.WorldCardCatalog.GetPlayerSeason(definition).PlayerType == PlayerType.Pitcher) == _pitchers) cards.Add(owned);
            }
            cards.Sort((a, b) => string.CompareOrdinal(a.CardId, b.CardId));
            if (cards.Count == 0) { Text(_players, "Empty", "해당 유형의 보유 선수가 없습니다.", .05f, .3f, .95f, .7f, 18); return; }
            bool found = false; foreach (var owned in cards) if (owned.CardId == _cardId) found = true;
            if (!found) _cardId = cards[0].CardId;
            _page = Math.Max(0, Math.Min((cards.Count - 1) / 6, _page));
            for (int i = _page * 6; i < Math.Min(cards.Count, (_page + 1) * 6); i++)
            {
                var owned = cards[i]; float top = .86f - i % 6 * .12f;
                var playerButton = Button(_players, "Player" + i, Name(owned.CardId) + " · " + Season(owned.CardId).OriginYear + "\n코스트 " + Season(owned.CardId).Cost, .04f, top - .10f, .96f, top,
                    () => { _cardId = owned.CardId; _isConfirming = false; _unlockX = -1; _option = 0; Refresh(); });
                OwnerUiButtonSkin.Apply(playerButton, owned.CardId == _cardId ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
            }
            Button(_players, "PreviousPlayers", "이전", .04f, .025f, .46f, .105f, () => { _page--; Refresh(); });
            Button(_players, "NextPlayers", "다음", .54f, .025f, .96f, .105f, () => { _page++; Refresh(); });
        }
        private void ResetPlayerFilter()
        {
            _page = 0; _cardId = ""; _isConfirming = false; _unlockX = -1; Refresh();
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
        private void BuildCamp()
        {
            var owned = Card(); var balance = _manager.GetDevelopmentBalance(); var camps = balance.camps;
            _option %= camps.Length; var camp = camps[_option];
            Text(_body, "Title", Name(_cardId) + " · 성장판 개방", .035f, .86f, .95f, .97f, 22);
            Button(_body, "Camp", camp.name + "  ▶", .035f, .69f, .47f, .80f, () => { _option++; _isConfirming = false; Refresh(); });
            Button(_body, "AutoReturn", "자동 귀환 " + (_automaticReturn ? "켜짐" : "꺼짐"), .035f, .54f, .47f, .65f,
                () => { _automaticReturn = !_automaticReturn; _isConfirming = false; Refresh(); });
            long fee = Season(_cardId).Cost * camp.costPerCardCost;
            Text(_body, "Rules", "코치 반영 주간 경험치 +" + OwnerCampService.ResolveWeeklyExperience(_manager.Runtime, _cardId, camp, _manager.Balance) + " · 정원 " + camp.capacity + "명\n등록비 " + fee.ToString("N0")
                + " PT\n현재 경험치 " + owned.SkillBoard.SlotExperience + " / " + balance.slotExperienceRequired
                + "\n\n2군 선수만 등록할 수 있습니다.\n중도 귀환해도 경험치는 보존됩니다.\n등록비는 환급하지 않습니다.", .035f, .05f, .47f, .49f, 18);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
            {
                int cx = x, cy = y; bool unlocked = owned.SkillBoard.IsCellUnlocked(x, y);
                var button = Button(_body, "Cell" + x + y, unlocked ? "개방" : "잠금", .52f + x * .11f, .65f - y * .15f,
                    .62f + x * .11f, .78f - y * .15f, () =>
                    { _unlockX = cx; _unlockY = cy; _isConfirming = true; Refresh(); });
                button.interactable = !unlocked && owned.SkillBoard.CanUnlock(x, y) && owned.SkillBoard.SlotExperience >= balance.slotExperienceRequired
                    && OwnerScheduleGateService.GetPhase(_manager.Runtime) == OwnerSeasonPhase.Offseason;
            }
            if (_unlockX >= 0)
            {
                if (!owned.SkillBoard.CanUnlock(_unlockX, _unlockY) || owned.SkillBoard.SlotExperience < balance.slotExperienceRequired)
                    throw new InvalidOperationException("개방할 인접 칸과 경험치를 다시 확인하세요.");
                Command("선택한 칸 개방", 0, () => _manager.UnlockSkillCell(_cardId, _unlockX, _unlockY)); return;
            }
            foreach (var project in _manager.Runtime.PlayerGrowth.Camps)
                if (project.CardId == _cardId) { Command("중도 귀환", 0, () => _manager.ReturnFromCamp(_cardId)); return; }
            OwnerPermanentGrowthService.RequireAvailable(_manager.Runtime, _cardId, OwnerGrowthAction.TrainingCamp);
            OwnerScheduleGateService.Evaluate(_manager.Runtime, OwnerGrowthAction.TrainingCamp, 1).RequireAllowed();
            foreach (var entry in _manager.Runtime.GetRoster(_manager.Runtime.PlayerTeamSeasonKey).Entries)
                if (entry.CardId == _cardId) throw new InvalidOperationException("선수단에서 2군으로 이동한 뒤 캠프에 등록하세요.");
            if (owned.SkillBoard.UnlockedMask == OwnedCardSkillBoardState.CompleteUnlockedMask) throw new InvalidOperationException("모든 칸이 개방된 선수입니다.");
            var progress = OwnerCardStudyUnlockEvaluator.Evaluate(_manager.Runtime, camp.requiredChampionshipLeague);
            if (progress.HighestLeagueGrade < camp.requiredLeague || progress.PostseasonChampionships < camp.requiredChampionships)
                throw new InvalidOperationException("해금 조건: " + OwnerLeagueDisplayNameFormatter.FormatFull(camp.requiredLeague) + " 진입 · "
                    + OwnerLeagueDisplayNameFormatter.FormatFull(camp.requiredChampionshipLeague) + " 이상 포스트시즌 우승 " + camp.requiredChampionships + "회");
            int count = 0; foreach (var project in _manager.Runtime.PlayerGrowth.Camps) if (project.FacilityId == camp.id) count++;
            if (count >= camp.capacity) throw new InvalidOperationException("캠프 정원이 가득 찼습니다.");
            Command("캠프 등록", fee, () => _manager.StartCamp(_cardId, camp.id, _automaticReturn));
        }
        private void BuildCorrection()
        {
            var owned = Card(); int first = _pitchers ? 6 : 0;
            PlayerAbility decrease = (PlayerAbility)(first + _decrease), increase = (PlayerAbility)(first + _increase);
            Text(_body, "Title", Name(_cardId) + " · 교정 " + OwnerPermanentGrowthService.Count(owned, OwnerGrowthSource.Correction) + "/3회", .035f, .85f, .96f, .97f, 22);
            Button(_body, "Decrease", "감소 · " + OwnerGrowthHistoryFormatter.GetAbilityName(decrease), .035f, .68f, .34f, .80f,
                () => { _decrease = (_decrease + 1) % 6; _isConfirming = false; Refresh(); });
            Button(_body, "Increase", "증가 · " + OwnerGrowthHistoryFormatter.GetAbilityName(increase), .36f, .68f, .665f, .80f,
                () => { _increase = (_increase + 1) % 6; _isConfirming = false; Refresh(); });
            Button(_body, "Amount", "이동량 " + _amount, .69f, .68f, .965f, .80f,
                () => { _amount = _amount % 3 + 1; _isConfirming = false; Refresh(); });
            var preview = OwnerPermanentGrowthService.PreviewCorrection(_manager.Runtime, _cardId, decrease, increase, _amount);
            var bases = Season(_cardId).CreateBaseAttributes();
            Text(_body, "Columns", "능력치                기본      기존 교정      이번 변화      교정 후", .035f, .52f, .965f, .62f, 18);
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
            if (candidates.Count == 0) throw new InvalidOperationException("참여 가능한 같은 유형의 파트너가 없습니다. 이미 참여한 선수·파견 중인 선수·역할에 유효한 능력차가 없는 선수는 제외됩니다.");
            _option %= Math.Min(3, candidates.Count);
            for (int i = 0; i < Math.Min(3, candidates.Count); i++)
            {
                int index = i; var candidate = candidates[i];
                Button(_body, "Partner" + i, (i == _option ? "● " : "") + Name(candidate.PartnerCardId) + " · 예상 성장 +" + candidate.Total,
                    .035f, .67f - i * .17f, .48f, .80f - i * .17f, () => { _option = index; _isConfirming = false; Refresh(); });
            }
            var selected = candidates[_option];
            Text(_body, "Preview", selected.Reason + "\n\n" + Changes(selected.Values) + "\n\n두 선수 모두 이번 오프시즌 참여를 사용합니다.\n결과는 영구 성장으로 남습니다.", .52f, .07f, .96f, .80f, 18);
            Command("파트너 훈련", _manager.GetDevelopmentBalance().partnerCost, () => _manager.TrainWithPartner(_cardId, selected.PartnerCardId));
        }
        private void BuildResearch()
        {
            var balance = _manager.GetDevelopmentBalance(); var inventory = _manager.Runtime.PlayerGrowth.Inventory;
            Text(_body, "Title", "스킬 연구 " + inventory.ResearchCount + "회 · S 선택 상자 " + inventory.SelectionBoxes + "개", .035f, .85f, .96f, .97f, 22);
            Text(_body, "Rules", "연구 1회: 블록 2개 확정\nC " + balance.researchWeights[0] + "% · B " + balance.researchWeights[1] + "% · A " + balance.researchWeights[2]
                + "%\n10회마다 S 선택 상자 1개\n\n합성: 미장착 블록 5개 → 동일 계열 상위 1개\n재료는 인벤토리 등록 순서대로 소비합니다.", .035f, .33f, .48f, .80f, 18);
            Button(_body, "ResearchMode", "연구 선택", .035f, .05f, .48f, .17f,
                () => { _option = 0; _isConfirming = false; Refresh(); });
            var filterAbility = (PlayerAbility)((_pitchers ? 6 : 0) + _researchAbility);
            Button(_body, "ResearchAbility", OwnerGrowthHistoryFormatter.GetAbilityName(filterAbility) + " ▶", .035f,.20f,.25f,.31f,
                () => { _researchAbility = (_researchAbility + 1) % 6; _blockIndex = 0; _isConfirming = false; Refresh(); });
            Button(_body, "ResearchRarity", (_researchRarity == 1 ? "B" : _researchRarity == 2 ? "A" : "S") + "등급 ▶", .27f,.20f,.48f,.31f,
                () => { _researchRarity = _researchRarity % 3 + 1; _blockIndex = 0; _isConfirming = false; Refresh(); });
            var definitions = new List<SkillBlockDefinition>();
            foreach (var definition in _manager.Balance.Growth.SkillBlocks)
                if ((int)definition.Rarity == _researchRarity)
                    foreach (var bonus in definition.AbilityBonuses)
                        if (bonus.Ability == filterAbility) { definitions.Add(definition); break; }
            if (definitions.Count == 0) throw new InvalidOperationException("이 능력치와 등급의 연구 블록이 없습니다. 필터를 변경하세요.");
            definitions.Sort((a,b) => string.CompareOrdinal(a.BlockId,b.BlockId));
            _blockIndex = (_blockIndex + definitions.Count) % definitions.Count; var selected = definitions[_blockIndex];
            foreach (var cell in selected.ShapeCells)
            {
                var shape = OwnerDugoutDetailUiFactory.CreatePanel(_body, "Shape" + cell.X + cell.Y,
                    .65f + cell.X * .043f, .745f - cell.Y * .038f, .688f + cell.X * .043f, .779f - cell.Y * .038f);
                shape.GetComponent<Image>().color = selected.Rarity == SkillBlockRarity.Unique ? new Color(.65f,.82f,.9f)
                    : selected.Rarity == SkillBlockRarity.Elite ? new Color(.8f,.6f,.22f) : new Color(.54f,.62f,.72f);
            }
            Button(_body, "BlockPrevious", "◀", .52f, .67f, .61f, .80f, () => { _blockIndex--; _isConfirming = false; Refresh(); });
            Button(_body, "BlockNext", "▶", .86f, .67f, .95f, .80f, () => { _blockIndex++; _isConfirming = false; Refresh(); });
            var changes = new int[PlayerAbilityCatalog.AbilityCount]; foreach (var bonus in selected.AbilityBonuses) changes[(int)bonus.Ability] += bonus.Amount;
            Text(_body, "SelectedBlock", "선택 블록 · " + ((int)selected.Rarity == 1 ? "B" : (int)selected.Rarity == 2 ? "A" : "S") + "등급 · " + selected.ShapeCells.Length + "칸\n" + Changes(changes)
                + (selected.AdjacencySetBonus > 0 ? "같은 계열 3블록 인접: 세트 +" + selected.AdjacencySetBonus : ""), .52f, .35f, .95f, .64f, 18);
            Button(_body, "FusionMode", "이 블록으로 합성", .52f, .22f, .95f, .33f, () => { _option = 1; _isConfirming = false; Refresh(); });
            Button(_body, "BoxMode", "S 상자에서 선택", .52f, .07f, .95f, .18f, () => { _option = 2; _isConfirming = false; Refresh(); });
            if (_option == 0) Command("연구 실행", balance.researchCost, _manager.ResearchSkillBlocks);
            else if (_option == 1)
            {
                int count = OwnerSkillResearchService.GetFusionMaterials(_manager.Runtime, _manager.Balance.Growth.SkillBlocks, selected.Category, selected.Rarity - 1).Count;
                if (count < 5) throw new InvalidOperationException("같은 계열 하위 등급 재료 " + count + "/5개 · 장착 블록은 제외합니다.");
                Command("블록 5개 합성", 0, () => _manager.FuseSkillBlocks(selected.BlockId));
            }
            else
            {
                if (selected.Rarity != SkillBlockRarity.Unique || inventory.SelectionBoxes == 0) throw new InvalidOperationException("S 등급 블록과 보유한 S 선택 상자가 필요합니다.");
                Command("S 선택 상자 사용", 0, () => _manager.OpenSkillSelectionBox(selected.BlockId));
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
                Button(_body, "Slogan" + i, (i == _option ? "● " : "") + slogan.name + "\n해당 카드 수집 " + count + "장 · Lv." + OwnerSloganService.GetLevel(_manager.Runtime, slogan),
                    .035f, .61f - i * .19f, .47f, .78f - i * .19f, () => { _option = index; _isConfirming = false; Refresh(); });
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
                + "\n\n취소 환급: " + OwnerStudyCancellationService.GetMoneyRefund(project).ToString("N0") + " PT"
                + (project.PaidDevelopmentPoints > 0 ? " · 육성 포인트 " + OwnerStudyCancellationService.GetPointRefund(project) : "")
                + "\n\n진행 전 취소는 전액, 진행 후 취소는 절반을 환급합니다.\n취소하면 이번 유학의 성장 보상을 받지 않습니다.", .035f, .12f, .95f, .78f, 20);
            Command("유학 취소·귀환", 0, () => _manager.CancelStudy(_cardId));
        }
        private void Confirm()
        {
            if (_isSubmitting || !_confirm.interactable || _action == null) return;
            if (!_isConfirming) { _isConfirming = true; Refresh(); return; }
            _isSubmitting = true; string message;
            try { _action(); message = "선택한 결과를 적용하고 저장했습니다."; }
            catch (InvalidOperationException e) { message = e.Message; }
            catch (Exception) { message = "저장하지 못했습니다. 변경 전 상태에서 다시 시도할 수 있습니다."; }
            finally { _isSubmitting = false; _isConfirming = false; _unlockX = -1; }
            Refresh(); _feedback.text = message;
        }
        public bool TryHandleCancel() { if (_isConfirming) { _isConfirming = false; _unlockX = -1; Refresh(); } else Close(); return true; }
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
            _focus.Clear(); foreach (var button in GetComponentsInChildren<Button>()) if (button.interactable) _focus.Add(button);
            for (int i = 0; i < _focus.Count; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = _focus[(i + _focus.Count - 1) % _focus.Count], selectOnLeft = _focus[(i + _focus.Count - 1) % _focus.Count],
                    selectOnDown = _focus[(i + 1) % _focus.Count], selectOnRight = _focus[(i + 1) % _focus.Count] };
                _focus[i].navigation = navigation; if (_focus[i].name == name) _focus[i].Select();
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap; return text;
        }
        private static Button Button(Transform parent, string name, string text, float l, float b, float r, float t, Action action) =>
            OwnerDugoutDetailUiFactory.CreateButton(parent, name, text, l, b, r, t, action);
    }
}

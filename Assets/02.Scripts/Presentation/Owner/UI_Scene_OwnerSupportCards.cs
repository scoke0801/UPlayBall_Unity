using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 재고·슬롯·대상별 변화량을 비교하고 서포트 구매와 사용을 확정한다.</summary>
    public sealed partial class UI_Scene_OwnerSupportCards : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private RectTransform _root;
        private RectTransform _catalog;
        private RectTransform _targets;
        private Text _details;
        private Text _active;
        private Text _message;
        private Button _buy;
        private Button _equip;
        private Button _cancel;
        private Button _previous;
        private Button _next;
        private Text _page;
        private Text _targetHeading;
        private Text _selection;
        private Text _cost;
        private Text _empty;
        private readonly List<Button> _cardButtons = new List<Button>();
        private readonly PlayerMiniCardView[] _playerCards = new PlayerMiniCardView[PageSize];
        private readonly RectTransform[] _playerTiles = new RectTransform[PageSize];
        private readonly Text[] _playerChanges = new Text[PageSize];
        private readonly Text[] _playerStates = new Text[PageSize];
        private readonly string[] _visibleCardIds = new string[PageSize];
        private OwnerModeManager _manager;
        private IReadOnlyList<OwnerSupportDefinition> _definitions;
        private int _selected;
        private int _targetPage;
        private string _cardId = "";
        private int _confirmation;
        private bool _isSubmitting;
        private const int PageSize = 8;

        public static UI_Scene_OwnerSupportCards CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerSupportCards), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerSupportCards>();
            view.Build();
            return view;
        }
        public void Bind(OwnerModeManager manager)
        {
            _manager = manager;
            try { _definitions = manager.GetSupportCatalog(); Refresh(); }
            catch (Exception) { _message.text = "서포트 정보를 읽지 못했습니다. 메뉴를 다시 열어 주세요."; _buy.interactable = _equip.interactable = false; }
        }
        public void SetVisible(bool visible) { _root.gameObject.SetActive(visible); if (!visible) _confirmation = 0; }
        public bool TryHandleCancel()
        {
            if (_confirmation == 0) return false;
            var origin = _confirmation == 1 ? _buy : _equip;
            _confirmation = 0;
            Refresh();
            if (origin.interactable) origin.Select();
            return true;
        }
        public void OnCancel(BaseEventData data) { if (TryHandleCancel()) data.Use(); }
        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerSupportCardsWorkspace", true);
            OwnerDashboardStyle.Rule(_root, "BackdropShade", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f));
            Label(_root, "Heading", "서포트 카드", .025f, .925f, .4f, .985f, 28);
            Label(_root, "Introduction", "필요한 효과를 고르고, 선수에게 적용될 변화를 확인하세요.", .025f, .882f, .75f, .925f, 18).color = OwnerDashboardStyle.Muted;
            Label(_root, "Duration", "다음 2경기 적용", .79f, .925f, .975f, .975f, 20).alignment = TextAnchor.MiddleRight;
            var left = Panel(_root, "Inventory", .015f, .025f, .255f, .86f);
            var middle = Panel(_root, "Targets", .265f, .025f, .725f, .86f);
            var right = Panel(_root, "Effects", .735f, .025f, .985f, .86f);
            Label(left, "Title", "서포트 보관함", 0, .93f, 1, 1, 22);
            BuildCatalog(left);
            Label(middle, "Title", "적용 선수 · 효과 미리보기", 0, .93f, 1, 1, 22);
            _targetHeading = Label(middle, "TargetSummary", "", 0, .86f, 1, .925f, 17);
            _targets = OwnerDugoutDetailUiFactory.CreateRect(middle, "Players", 0, .105f, 1, .835f);
            for (int i = 0; i < PageSize; i++)
            {
                int slot = i;
                float leftEdge = (i % 4) / 4f;
                float top = 1 - (i / 4) * .5f;
                var tile = OwnerDugoutDetailUiFactory.CreateRect(_targets, "PlayerTile" + i,
                    leftEdge, top - .48f, leftEdge + .24f, top);
                _playerTiles[i] = tile;
                OwnerDashboardStyle.ApplyInset(tile.gameObject.AddComponent<Image>());
                _playerStates[i] = Label(tile, "State", "", .04f, .9f, .96f, 1, 14);
                _playerStates[i].alignment = TextAnchor.MiddleCenter;
                var cardHost = OwnerDugoutDetailUiFactory.CreateRect(tile, "CardBounds", .08f, .31f, .92f, .9f);
                var mini = PlayerMiniCardView.CreateRuntime(cardHost, "Player" + i);
                mini.UseLineupSlotLayout();
                mini.UseRosterPresentation();
                // 제한된 보드 높이에서도 원화와 명찰의 비율을 보존한다.
                var aspect = mini.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = PlayerMiniCardView.LineupSlotWidth / PlayerMiniCardView.LineupSlotHeight;
                _playerCards[i] = mini;
                mini.Selected += _ =>
                    {
                        if (_definitions == null || _definitions.Count == 0) return;
                        if (_definitions[_selected].scope == OwnerSupportScope.Team) return;
                        _cardId = _visibleCardIds[slot]; _confirmation = 0; Refresh();
                    };
                _playerChanges[i] = Label(tile, "Changes", "", .04f, .015f, .96f, .30f, 15);
                _playerChanges[i].alignment = TextAnchor.MiddleCenter;
            }
            _empty = Label(_targets, "Empty", "조건에 맞는 1군 선수가 없습니다.\n선수 오더에서 편성을 확인하세요.", 0, .3f, 1, .8f, 20);
            _previous = Button(middle, "Previous", "이전", 0, 0, .26f, .075f, () => { _targetPage--; _confirmation = 0; Refresh(); });
            _next = Button(middle, "Next", "다음", .74f, 0, 1, .075f, () => { _targetPage++; _confirmation = 0; Refresh(); });
            _page = Label(middle, "Page", "", .28f, 0, .72f, .075f, 17);
            _page.alignment = TextAnchor.MiddleCenter;
            Label(right, "Title", "선택한 서포트", 0, .93f, 1, 1, 22);
            _selection = Label(right, "Selection", "", 0, .82f, 1, .925f, 23);
            _selection.color = OwnerDashboardStyle.Gold;
            _details = Label(right, "Details", "", 0, .66f, 1, .81f, 16);
            OwnerDashboardStyle.Rule(right, "ActiveRule", new Vector2(0, .65f), new Vector2(1, .65f), Vector2.zero, new Vector2(0, 1), OwnerDashboardStyle.Line);
            _active = Label(right, "Active", "", 0, .40f, 1, .635f, 15);
            _cost = Label(right, "Cost", "", 0, .315f, 1, .395f, 16);
            _message = Label(right, "Feedback", "서포트 정보를 불러오는 중입니다.", 0, .235f, 1, .31f, 16);
            _message.color = OwnerDashboardStyle.Gold;
            _buy = Button(right, "Buy", "구매", 0, .15f, 1, .225f, () => Submit(1));
            _equip = Button(right, "Equip", "사용", 0, .065f, 1, .14f, () => Submit(2));
            OwnerUiButtonSkin.Apply(_equip, OwnerButtonRole.Primary);
            _cancel = Button(right, "Cancel", "확인 취소", 0, 0, 1, .055f, () => TryHandleCancel());
            OwnerUiButtonSkin.Apply(_cancel, OwnerButtonRole.Quiet);
            _cancel.gameObject.SetActive(false);
            _buy.interactable = _equip.interactable = false;
        }
        private void Refresh()
        {
            string focusName = EventSystem.current?.currentSelectedGameObject?.name;
            if (_manager?.Runtime == null || _definitions == null) return;
            var runtime = _manager.Runtime;
            if (_definitions.Count == 0)
            {
                _message.text = "등록된 서포트 카드가 없습니다. 메뉴를 다시 열어 주세요.";
                _buy.interactable = _equip.interactable = false;
                return;
            }
            _selected = Math.Max(0, Math.Min(_definitions.Count - 1, _selected));
            if (!RefreshCatalogFilter()) return;
            var selected = _definitions[_selected];
            EnsureCatalogRows();
            for (int i = 0; i < _definitions.Count; i++)
            {
                var definition = _definitions[i];
                var button = _cardButtons[i];
                button.transform.Find("Label").GetComponent<Text>().text =
                    "<size=15>" + (definition.scope == OwnerSupportScope.Team ? "팀 서포트" : "개인 서포트")
                    + (i == _selected ? "   ·   선택됨" : "") + "</size>\n<size=22>" + definition.displayName
                    + "</size>\n<size=16>" + FormatEffect(definition) + "</size>"
                    + "\n<size=15>보유 " + runtime.PlayerGrowth.Support.GetCount(definition.id)
                    + "장    ·    " + definition.price.ToString("N0") + " PT</size>"
                    + "\n<size=14>" + (OwnerSupportService.IsUnlocked(runtime, definition) ? "이용 가능" : "잠김 · " + OwnerLeagueDisplayNameFormatter.FormatFull(definition.unlockGrade) + " 진출 시 해금") + "</size>";
                OwnerUiButtonSkin.SetSelected(button, i == _selected);
                button.transform.Find("SelectionRail").gameObject.SetActive(i == _selected);
            }
            var candidates = new List<OwnedPlayerCardState>();
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
            {
                var targets = OwnerSupportService.ResolveTargets(runtime, selected, entry.CardId);
                if (selected.scope == OwnerSupportScope.Team) { candidates = targets; break; }
                if (targets.Count > 0) candidates.Add(targets[0]);
            }
            if (selected.scope == OwnerSupportScope.Player && !candidates.Exists(card => card.CardId == _cardId))
                _cardId = "";
            _targetPage = Math.Max(0, Math.Min(Math.Max(0, (candidates.Count - 1) / PageSize), _targetPage));
            var resolver = new OwnerCardAbilityResolver(_manager.Balance.Growth);
            var visibleIds = new List<string>();
            for (int i = _targetPage * PageSize; i < Math.Min(candidates.Count, (_targetPage + 1) * PageSize); i++) visibleIds.Add(candidates[i].CardId);
            var details = new OwnerModeRuntimeSnapshotFactory().CreateCollectionCardDetails(_manager, visibleIds);
            for (int i = _targetPage * PageSize; i < Math.Min(candidates.Count, (_targetPage + 1) * PageSize); i++)
            {
                var owned = candidates[i];
                runtime.WorldCardCatalog.TryGetCard(owned.CardId, out var card);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                var line = new StringBuilder();
                var bonuses = OwnerSupportService.ResolveBonuses(runtime, owned.CardId, selected);
                bool isSelected = _cardId == owned.CardId && selected.scope == OwnerSupportScope.Player;
                for (int stat = 0; stat < selected.bonuses.Length; stat++)
                {
                    if (bonuses[stat] == 0) continue;
                    var ability = (PlayerAbility)stat;
                    int before = details[i - _targetPage * PageSize].GetEffectiveAbility(ability) ?? resolver.ResolveContribution(season, card, owned, ability).Total;
                    int cap = _manager.Balance.MatchRatingCurve.Caps.HardCap;
                    AppendChange(line, OwnerGrowthHistoryFormatter.GetAbilityName(ability),
                        Math.Min(cap, before), Math.Min(cap, before + bonuses[stat]));
                }
                if (selected.conditionPoints > 0)
                {
                    int before = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).GetRequiredPlayer(season.PlayerPersonId).StoredBaseCondition
                        + ManagerModeMatchService.ResolveHeadCoachConditionBonus(runtime, runtime.PlayerTeamSeasonKey, _manager.Balance.ConditionChemistry)
                        + OwnerSupportService.GetConditionBonus(runtime, owned.CardId);
                    AppendChange(line, "컨디션", Math.Min(100, before), Math.Min(100, before + selected.conditionPoints));
                }
                int row = i % PageSize;
                _visibleCardIds[row] = owned.CardId;
                _playerTiles[row].gameObject.SetActive(true);
                var snapshot = details[row];
                _playerCards[row].Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(snapshot, isSelected),
                    PlayerPortraitSprites.GetDefault(snapshot.Position));
                _playerChanges[row].text = line.ToString();
                bool isTeam = selected.scope == OwnerSupportScope.Team;
                bool hasPersonalSupport = false;
                foreach (var assignment in runtime.PlayerGrowth.Support.Assignments)
                    if (!assignment.IsTeam && assignment.CardId == owned.CardId) hasPersonalSupport = true;
                _playerStates[row].text = isTeam ? "함께 적용" : isSelected ? "선택됨"
                    : hasPersonalSupport ? "개인 효과 사용 중" : "선수 선택";
                _playerStates[row].color = isSelected ? OwnerDashboardStyle.Gold : OwnerDashboardStyle.Muted;
                // 팀 서포트는 대상 선택이 필요 없으므로 무반응 버튼을 탐색 경로에 넣지 않는다.
                var playerButton = _playerCards[row].GetComponent<Button>();
                var colors = playerButton.colors;
                colors.disabledColor = colors.normalColor;
                playerButton.colors = colors;
                playerButton.interactable = !isTeam;
            }
            for (int i = visibleIds.Count; i < PageSize; i++) _playerTiles[i].gameObject.SetActive(false);
            _empty.gameObject.SetActive(candidates.Count == 0);
            _previous.interactable = _targetPage > 0;
            _next.interactable = (_targetPage + 1) * PageSize < candidates.Count;
            _page.text = candidates.Count == 0 ? "0명" : $"{_targetPage + 1} / {(candidates.Count + PageSize - 1) / PageSize} 페이지";
            _targetHeading.text = selected.scope == OwnerSupportScope.Team
                ? $"1군 {candidates.Count}명 전체 적용 · 선수 선택 없이 사용"
                : $"1군 대상 {candidates.Count}명 · 카드를 눌러 1명 선택";
            var active = new StringBuilder();
            int teamCount = 0, playerCount = 0;
            foreach (var assignment in runtime.PlayerGrowth.Support.Assignments)
            {
                if (assignment.IsTeam) teamCount++; else playerCount++;
                active.Append(assignment.Definition.displayName)
                    .Append(" · ").Append(assignment.RemainingGames).Append("경기");
                if (!assignment.IsTeam && runtime.WorldCardCatalog.TryGetCard(assignment.CardId, out var activeCard))
                    active.Append("\n    ").Append(runtime.IdentityRegistry.GetPresentationPlayerName(runtime.WorldCardCatalog.GetPlayerSeason(activeCard).PlayerPersonId));
                active.Append('\n');
            }
            _active.text = "사용 중  ·  팀 " + teamCount + "/1   개인 " + playerCount + "/3\n\n"
                + (active.Length == 0 ? "적용 중인 효과가 없습니다.\n팀 1장 + 개인 3장까지 사용 가능" : active.ToString());
            string targetName = "대상 선수를 선택하세요";
            if (selected.scope == OwnerSupportScope.Team) targetName = $"1군 대상 선수 {candidates.Count}명";
            else if (!string.IsNullOrEmpty(_cardId) && runtime.WorldCardCatalog.TryGetCard(_cardId, out var targetCard))
                targetName = runtime.IdentityRegistry.GetPresentationPlayerName(runtime.WorldCardCatalog.GetPlayerSeason(targetCard).PlayerPersonId);
            _selection.text = selected.displayName + "\n<size=17>" + targetName + "</size>";
            _details.text = selected.description + "\n\n"
                + (selected.scope == OwnerSupportScope.Team ? "팀·개인 효과는 함께 적용됩니다." : "2군 이동 시 개인 효과가 종료됩니다.");
            _cost.text = "보유 " + runtime.PlayerGrowth.Support.GetCount(selected.id) + "장   ·   사용 시 1장 소비\n"
                + (runtime.Economy.Money >= selected.price ? "구매 후 " + (runtime.Economy.Money - selected.price).ToString("N0") + " PT"
                    : "구매 자금  " + (selected.price - runtime.Economy.Money).ToString("N0") + " PT 부족");
            string reason = "";
            try
            {
                if (!OwnerSupportService.IsUnlocked(runtime, selected)) reason = OwnerLeagueDisplayNameFormatter.FormatFull(selected.unlockGrade) + " 진출 시 해금됩니다.";
                else if (selected.scope == OwnerSupportScope.Player && string.IsNullOrEmpty(_cardId)) reason = "대상 선수를 선택하세요.";
                else if (candidates.Count == 0) reason = "조건에 맞는 1군 선수가 없습니다.";
                else
                {
                    runtime.PlayerGrowth.Support.ValidateSlot(selected.scope == OwnerSupportScope.Team ? "" : _cardId);
                    if (runtime.PlayerGrowth.Support.GetCount(selected.id) < 1) reason = "보유 카드가 없습니다. 먼저 1장을 구매하세요.";
                }
            }
            catch (InvalidOperationException e) { reason = e.Message; }
            int ownedCount = runtime.PlayerGrowth.Support.GetCount(selected.id);
            _buy.interactable = !_isSubmitting && OwnerSupportService.IsUnlocked(runtime, selected) && ownedCount < int.MaxValue && runtime.Economy.Money >= selected.price;
            _equip.interactable = !_isSubmitting && reason.Length == 0;
            // 미보유 시 구매, 보유 시 적용으로 대표 행동을 옮겨 다음 단계를 바로 찾게 한다.
            OwnerUiButtonSkin.Apply(_buy, ownedCount == 0 ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
            OwnerUiButtonSkin.Apply(_equip, ownedCount > 0 ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
            _buy.GetComponentInChildren<Text>().text = _confirmation == 1 ? "1장 구매 확정 · " + selected.price.ToString("N0") + " PT" : "1장 구매 · " + selected.price.ToString("N0") + " PT";
            _equip.GetComponentInChildren<Text>().text = _confirmation == 2 ? "선택한 대상에게 사용 확정" : "다음 2경기에 적용";
            _cancel.gameObject.SetActive(_confirmation != 0);
            _message.text = _confirmation == 1 ? selected.price.ToString("N0") + " PT로 카드 1장을 구매할까요?"
                : _confirmation == 2 ? targetName + "에게 카드 1장을 사용합니다. 확정할까요?"
                : reason.Length > 0 ? reason : "영향 선수와 변화량을 확인한 뒤 사용하세요.";
            if (ownedCount == int.MaxValue) _cost.text = "보유 한도에 도달했습니다.\n카드를 사용한 뒤 구매하세요.";
            _buy.GetComponent<OwnerUiButtonSkin>()?.Refresh(); _equip.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            RefreshNavigation(focusName);
        }
        private void Submit(int command)
        {
            if (_isSubmitting) return;
            if (_confirmation != command) { _confirmation = command; Refresh(); return; }
            _isSubmitting = true;
            string result;
            bool succeeded = false;
            try
            {
                if (command == 1) _manager.PurchaseSupport(_definitions[_selected].id);
                else _manager.EquipSupport(_definitions[_selected].id, _cardId);
                succeeded = true;
                result = command == 1 ? "서포트 카드 1장을 구매했습니다." : "서포트가 적용되었습니다. 다음 2경기에 효과가 유지됩니다.";
            }
            catch (Exception) { result = "변경을 저장하지 못했습니다. 대상과 보유 자원을 확인한 뒤 다시 시도하세요."; }
            finally { _isSubmitting = false; _confirmation = 0; }
            Refresh(); _message.text = result;
            if (succeeded && command == 1 && _equip.interactable) _equip.Select();
        }
        private static Text Label(Transform parent, string name, string text, float l, float b, float r, float t, int size)
        {
            var label = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, text, l, b, r, t, size, FontStyle.Normal, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            OwnerDashboardStyle.SetTypography(label, size >= 22);
            label.color = OwnerDashboardStyle.Ivory;
            label.gameObject.AddComponent<CareerUiPreserveTextColor>();
            return label;
        }
        private static Button Button(Transform parent, string name, string text, float l, float b, float r, float t, Action action)
        {
            var button = OwnerDugoutDetailUiFactory.CreateButton(parent, name, text, l, b, r, t, action);
            OwnerUiButtonSkin.SetDashboardStyle(button);
            button.GetComponentInChildren<Text>().fontSize = 17;
            return button;
        }

        private static RectTransform Panel(Transform parent, string name, float l, float b, float r, float t)
        {
            var panel = OwnerDugoutDetailUiFactory.CreatePanel(parent, name, l, b, r, t);
            OwnerDashboardStyle.ApplySurface(panel);
            var safe = OwnerDugoutDetailUiFactory.CreateRect(panel, "ContentSafeRect", 0, 0, 1, 1);
            safe.offsetMin = new Vector2(CareerUiTheme.Space6, CareerUiTheme.Space6);
            safe.offsetMax = -safe.offsetMin;
            // 환경 원화의 세부 묘사가 선수·수치와 경쟁하지 않도록 본문은 불투명 작업면을 쓴다.
            OwnerDashboardStyle.ApplyInset(safe.gameObject.AddComponent<Image>());
            return safe;
        }

        private static void ConfigureRow(Button button)
        {
            var text = button.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space2);
            text.rectTransform.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space2);
            OwnerDashboardStyle.Rule(button.transform, "SelectionRail", Vector2.zero, Vector2.up,
                Vector2.zero, new Vector2(3, 0), OwnerDashboardStyle.Gold);
        }

        private void EnsureCatalogRows()
        {
            while (_cardButtons.Count < _definitions.Count)
            {
                int index = _cardButtons.Count;
                var button = Button(_catalog, "Support" + index, "서포트", 0, 0, 1, 1, () =>
                {
                    if (_selected == index) return;
                    // 호환되는 개인 대상은 유지한다. 대상 조건이 달라지면 Refresh에서 해제한다.
                    _selected = index; _confirmation = 0; _targetPage = 0; Refresh();
                });
                ConfigureRow(button);
                _cardButtons.Add(button);
            }
            LayoutCatalogRows();
        }

        private static string FormatEffect(OwnerSupportDefinition definition)
        {
            var text = new StringBuilder();
            var displayed = new HashSet<string>();
            for (int i = 0; i < definition.bonuses.Length; i++)
            {
                if (definition.bonuses[i] == 0) continue;
                // 타자·투수 정신처럼 같은 표시명을 가진 능력치는 카드 요약에 한 번만 쓴다.
                string effect = OwnerGrowthHistoryFormatter.GetAbilityName((PlayerAbility)i) + " +" + definition.bonuses[i];
                if (!displayed.Add(effect)) continue;
                if (text.Length > 0) text.Append(" · ");
                text.Append(effect);
            }
            if (definition.conditionPoints > 0)
            {
                if (text.Length > 0) text.Append(" · ");
                text.Append("컨디션 +").Append(definition.conditionPoints);
            }
            return text.ToString();
        }

        private static void AppendChange(StringBuilder text, string ability, int before, int after)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append(ability).Append(" ").Append(before).Append(" → ")
                .Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(OwnerDashboardStyle.Gold)).Append('>')
                .Append(after).Append("</color>");
            if (after == before) text.Append(" <size=12>상한</size>");
        }

        private void RefreshNavigation(string focusName)
        {
            Button restore = null;
            foreach (var button in _root.GetComponentsInChildren<Button>())
            {
                // 자동 공간 탐색으로 좌우는 패널 사이, 상하는 같은 목록으로 이동한다.
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                button.GetComponent<OwnerUiButtonSkin>()?.Refresh();
                if (button.interactable && button.name == focusName) restore = button;
            }
            if (restore != null) restore.Select();
            else if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null
                || EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_root)))
                if (_cardButtons[_selected].gameObject.activeInHierarchy) _cardButtons[_selected].Select();
        }
        private void OnDestroy() => OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    /// <summary>실제 재고·슬롯·대상별 변화량을 비교하고 서포트 구매와 사용을 확정한다.</summary>
    public sealed class UI_Scene_OwnerSupportCards : MonoBehaviour, IUiCancelHandler, ICancelHandler
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
        private OwnerModeManager _manager;
        private IReadOnlyList<OwnerSupportDefinition> _definitions;
        private int _selected;
        private int _targetPage;
        private string _cardId = "";
        private int _confirmation;
        private bool _isSubmitting;
        private const int PageSize = 6;

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
            _confirmation = 0; Refresh(); return true;
        }
        public void OnCancel(BaseEventData data) { if (TryHandleCancel()) data.Use(); }
        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerSupportCardsWorkspace", true);
            var left = OwnerDugoutDetailUiFactory.CreatePanel(_root, "Inventory", .015f, .18f, .285f, .98f);
            var middle = OwnerDugoutDetailUiFactory.CreatePanel(_root, "Targets", .30f, .18f, .65f, .98f);
            var right = OwnerDugoutDetailUiFactory.CreatePanel(_root, "Effects", .665f, .18f, .985f, .98f);
            Label(left, "Title", "서포트 카드 · 구매 / 보유", .05f, .91f, .95f, .98f, 20);
            Label(middle, "Title", "적용 대상 · 변화 미리보기", .05f, .91f, .95f, .98f, 20);
            Label(right, "Title", "현재 편성 · 다음 2경기", .05f, .91f, .95f, .98f, 20);
            _catalog = OwnerDugoutDetailUiFactory.CreateRect(left, "Cards", .05f, .12f, .95f, .88f);
            _targets = OwnerDugoutDetailUiFactory.CreateRect(middle, "Players", .05f, .13f, .95f, .88f);
            Button(middle, "Previous", "이전 선수", .05f, .025f, .45f, .095f, () => { _targetPage = Math.Max(0, _targetPage - 1); Refresh(); });
            Button(middle, "Next", "다음 선수", .55f, .025f, .95f, .095f, () => { _targetPage++; Refresh(); });
            _active = Label(right, "Active", "", .05f, .50f, .95f, .88f, 18);
            _details = Label(right, "Details", "", .05f, .05f, .95f, .46f, 18);
            _message = Label(_root, "Feedback", "서포트 카드를 선택하세요.", .025f, .085f, .61f, .16f, 17);
            _message.color = Color.white;
            _buy = Button(_root, "Buy", "구매", .63f, .045f, .79f, .13f, () => Submit(1));
            _equip = Button(_root, "Equip", "사용", .81f, .045f, .975f, .13f, () => Submit(2));
            OwnerUiButtonSkin.Apply(_equip, OwnerButtonRole.Primary);
            _cancel = Button(_root, "Cancel", "취소", .46f, .015f, .60f, .075f, () => { _confirmation = 0; Refresh(); });
            _cancel.gameObject.SetActive(false);
        }
        private void Refresh()
        {
            string focusName = EventSystem.current?.currentSelectedGameObject?.name;
            if (_manager?.Runtime == null || _definitions == null) return;
            var runtime = _manager.Runtime;
            _selected = Math.Max(0, Math.Min(_definitions.Count - 1, _selected));
            var selected = _definitions[_selected];
            OwnerRuntimeUiFactory.ClearChildren(_catalog);
            for (int i = 0; i < _definitions.Count; i++)
            {
                int index = i; var definition = _definitions[i];
                float top = 1f - i * .235f;
                var button = Button(_catalog, "Support" + i,
                    (i == _selected ? "● " : "") + definition.displayName + "\n보유 " + runtime.PlayerGrowth.Support.GetCount(definition.id)
                    + " · " + definition.price.ToString("N0") + " PT",
                    0, top - .205f, 1, top, () => { _selected = index; _confirmation = 0; _cardId = ""; _targetPage = 0; Refresh(); });
                button.GetComponentInChildren<Text>().fontSize = 18;
            }
            var candidates = new List<OwnedPlayerCardState>();
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
            {
                var targets = OwnerSupportService.ResolveTargets(runtime, selected, entry.CardId);
                if (selected.scope == OwnerSupportScope.Team) { candidates = targets; break; }
                if (targets.Count > 0) candidates.Add(targets[0]);
            }
            _targetPage = Math.Max(0, Math.Min(Math.Max(0, (candidates.Count - 1) / PageSize), _targetPage));
            OwnerRuntimeUiFactory.ClearChildren(_targets);
            var resolver = new OwnerCardAbilityResolver(_manager.Balance.Growth);
            var visibleIds = new List<string>();
            for (int i = _targetPage * PageSize; i < Math.Min(candidates.Count, (_targetPage + 1) * PageSize); i++) visibleIds.Add(candidates[i].CardId);
            var details = new OwnerModeRuntimeSnapshotFactory().CreateCollectionCardDetails(_manager, visibleIds);
            for (int i = _targetPage * PageSize; i < Math.Min(candidates.Count, (_targetPage + 1) * PageSize); i++)
            {
                var owned = candidates[i];
                runtime.WorldCardCatalog.TryGetCard(owned.CardId, out var card);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                var line = new StringBuilder(runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId));
                var bonuses = OwnerSupportService.ResolveBonuses(runtime, owned.CardId, selected);
                if (_cardId == owned.CardId && selected.scope == OwnerSupportScope.Player) line.Insert(0, "● ");
                for (int stat = 0; stat < selected.bonuses.Length; stat++)
                {
                    if (bonuses[stat] == 0) continue;
                    var ability = (PlayerAbility)stat;
                    int before = details[i - _targetPage * PageSize].GetEffectiveAbility(ability) ?? resolver.ResolveContribution(season, card, owned, ability).Total;
                    int cap = _manager.Balance.MatchRatingCurve.Caps.HardCap;
                    line.Append("\n").Append(OwnerGrowthHistoryFormatter.GetAbilityName(ability)).Append(' ')
                        .Append(Math.Min(cap, before)).Append(" → ").Append(Math.Min(cap, before + bonuses[stat]));
                }
                if (selected.conditionPoints > 0)
                {
                    int before = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).GetRequiredPlayer(season.PlayerPersonId).StoredBaseCondition
                        + ManagerModeMatchService.ResolveHeadCoachConditionBonus(runtime, runtime.PlayerTeamSeasonKey, _manager.Balance.ConditionChemistry)
                        + OwnerSupportService.GetConditionBonus(runtime, owned.CardId);
                    line.Append("\n컨디션 ").Append(Math.Min(100, before)).Append(" → ").Append(Math.Min(100, before + selected.conditionPoints));
                }
                float top = 1 - (i % PageSize) * .16f;
                Button(_targets, "Player" + i, line.ToString(), 0, top - .145f, 1, top,
                    () => { _cardId = owned.CardId; _confirmation = 0; Refresh(); });
            }
            if (candidates.Count == 0) Label(_targets, "Empty", "조건에 맞는 1군 선수가 없습니다.\n선수단 편성을 확인하세요.", 0, .4f, 1, .9f, 18);
            var active = new StringBuilder();
            int teamCount = 0, playerCount = 0;
            foreach (var assignment in runtime.PlayerGrowth.Support.Assignments)
            {
                if (assignment.IsTeam) teamCount++; else playerCount++;
                active.Append(assignment.IsTeam ? "팀 · " : "개인 · ").Append(assignment.Definition.displayName)
                    .Append(" · ").Append(assignment.RemainingGames).Append("경기\n");
                if (!assignment.IsTeam && runtime.WorldCardCatalog.TryGetCard(assignment.CardId, out var activeCard))
                    active.Append(runtime.IdentityRegistry.GetPresentationPlayerName(runtime.WorldCardCatalog.GetPlayerSeason(activeCard).PlayerPersonId)).Append('\n');
            }
            _active.text = "팀 " + teamCount + "/1 · 개인 " + playerCount + "/3\n\n" + (active.Length == 0 ? "사용 중인 서포트가 없습니다." : active.ToString());
            _details.text = selected.description + "\n\n" + (selected.scope == OwnerSupportScope.Team ? "조건에 맞는 1군 선수 모두에게 적용합니다." : "대상 선수를 선택하세요. 2군으로 이동하면 효과가 종료됩니다.")
                + "\n\n사용하면 카드 1장을 소비합니다.\n팀 효과와 개인 효과는 함께 적용됩니다.";
            string reason = "";
            try
            {
                runtime.PlayerGrowth.Support.ValidateSlot(selected.scope == OwnerSupportScope.Team ? "" : _cardId);
                if (selected.scope == OwnerSupportScope.Player && string.IsNullOrEmpty(_cardId)) reason = "대상 선수를 선택하세요.";
                else if (candidates.Count == 0) reason = "조건에 맞는 1군 선수가 없습니다.";
                else if (runtime.PlayerGrowth.Support.GetCount(selected.id) < 1) reason = "카드를 구매한 뒤 사용할 수 있습니다.";
            }
            catch (InvalidOperationException e) { reason = e.Message; }
            _buy.interactable = !_isSubmitting && runtime.Economy.Money >= selected.price;
            _equip.interactable = !_isSubmitting && reason.Length == 0;
            _buy.GetComponentInChildren<Text>().text = _confirmation == 1 ? "구매 확정" : "구매 · " + selected.price.ToString("N0") + " PT";
            _equip.GetComponentInChildren<Text>().text = _confirmation == 2 ? "사용 확정" : "서포트 사용";
            _cancel.gameObject.SetActive(_confirmation != 0);
            _message.text = _confirmation == 1 ? selected.price.ToString("N0") + " PT로 카드 1장을 구매할까요?"
                : _confirmation == 2 ? "미리 본 대상에게 카드 1장을 사용합니다. 확정할까요?"
                : reason.Length > 0 ? reason : "영향 선수와 변화량을 확인한 뒤 사용하세요.";
            _buy.GetComponent<OwnerUiButtonSkin>()?.Refresh(); _equip.GetComponent<OwnerUiButtonSkin>()?.Refresh();
            var focus = new List<Button>();
            foreach (var button in GetComponentsInChildren<Button>()) if (button.interactable) focus.Add(button);
            for (int i = 0; i < focus.Count; i++)
            {
                focus[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = focus[(i + focus.Count - 1) % focus.Count], selectOnLeft = focus[(i + focus.Count - 1) % focus.Count],
                    selectOnDown = focus[(i + 1) % focus.Count], selectOnRight = focus[(i + 1) % focus.Count] };
                if (focus[i].name == focusName) focus[i].Select();
            }
        }
        private void Submit(int command)
        {
            if (_isSubmitting) return;
            if (_confirmation != command) { _confirmation = command; Refresh(); return; }
            _isSubmitting = true;
            string result;
            try
            {
                if (command == 1) _manager.PurchaseSupport(_definitions[_selected].id);
                else _manager.EquipSupport(_definitions[_selected].id, _cardId);
                result = command == 1 ? "서포트 카드 1장을 구매했습니다." : "서포트가 적용되었습니다. 다음 2경기에 효과가 유지됩니다.";
            }
            catch (Exception) { result = "변경을 저장하지 못했습니다. 대상과 보유 자원을 확인한 뒤 다시 시도하세요."; }
            finally { _isSubmitting = false; _confirmation = 0; }
            Refresh(); _message.text = result;
        }
        private static Text Label(Transform parent, string name, string text, float l, float b, float r, float t, int size)
        {
            var label = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, text, l, b, r, t, size, FontStyle.Normal, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }
        private static Button Button(Transform parent, string name, string text, float l, float b, float r, float t, Action action) =>
            OwnerDugoutDetailUiFactory.CreateButton(parent, name, text, l, b, r, t, action);
        private void OnDestroy() => OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
    }
}

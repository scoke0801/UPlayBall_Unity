using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerSpecialRecruit
    {
        private OwnerModeManager _manager;
        private RectTransform _content;
        private Dropdown _targets;
        private readonly List<string>[] _materialIds = new List<string>[8];
        private readonly PlayerMiniCardView[] _materialViews = new PlayerMiniCardView[8];
        private readonly RectTransform[] _materialHosts = new RectTransform[8];
        private readonly Button[] _materialButtons = new Button[8];
        private IReadOnlyList<PlayerCardDefinition> _targetCards;
        private readonly string[] _selectedMaterials = new string[8];
        private PlayerCardDefinition _selectedTarget;
        private RectTransform _targetHost;
        private RectTransform _targetFront;
        private OwnerCollectionCardSnapshot _targetSnapshot;
        private Button _targetDetail;
        private Text _targetSummary;
        private Text _status;
        private Button _confirm;
        private Button _autoSelect;
        private bool _isConfirming;
        private string _transactionId;
        private RectTransform _picker;
        private RectTransform _pickerRows;
        private Text _pickerTitle;
        private Button _pickerClear;
        private readonly List<Button> _candidateButtons = new List<Button>();
        private int _pickerSlot;
        private GameObject _returnFocus;

        /// <summary>실제 대상·레시피·보유 상태를 영입 카드 보드에 표시한다.</summary>
        public void Bind(OwnerModeManager manager)
        {
            _manager = manager;
            EnsureContent();
            _content.gameObject.SetActive(true);
            ClosePicker();
            if (_manager == null || !_manager.HasActiveRuntime)
            {
                _targets.ClearOptions();
                _targets.interactable = false;
                _targetCards = Array.Empty<PlayerCardDefinition>();
                SelectTarget(-1);
                _status.text = "구단 정보를 불러오지 못했습니다. 홈으로 돌아가 다시 열어주세요.";
                return;
            }
            string previous = _selectedTarget?.CardId;
            _targetCards = _manager.Runtime.GetSpecialRecruitTargets(
                _isCareerHigh ? PlayerCardEdition.CareerHigh : PlayerCardEdition.Legend);
            var labels = new List<string>();
            int selection = -1;
            int firstUnowned = -1;
            for (int i = 0; i < _targetCards.Count; i++)
            {
                var card = _targetCards[i];
                labels.Add(Describe(card.CardId) + (_manager.Runtime.TryGetOwnedCard(card.CardId, out _) ? " · 보유" : ""));
                if (card.CardId == previous) selection = i;
                if (firstUnowned < 0 && !_manager.Runtime.TryGetOwnedCard(card.CardId, out _)) firstUnowned = i;
            }
            if (selection < 0) selection = Math.Max(0, firstUnowned);
            _targets.ClearOptions();
            _targets.AddOptions(labels);
            _targets.SetValueWithoutNotify(selection);
            _targets.interactable = labels.Count > 0;
            SelectTarget(labels.Count == 0 ? -1 : selection);
        }

        private void EnsureContent()
        {
            if (_content != null) return;
            _content = Rect("IssuedRecruitContent", _root, .018f, .11f, .982f, .91f);
            _content.gameObject.AddComponent<CanvasGroup>();
            var target = OwnerRuntimeUiFactory.CreatePanel("TargetPanel", _content, "영입 대상", true);
            Place(target.Root, 0, .13f, .32f, 1);
            var board = OwnerRuntimeUiFactory.CreatePanel("MaterialPanel", _content, "필요 선수카드 · 8장을 모아 영입");
            Place(board.Root, .332f, .13f, 1, 1);
            _targets = CreateSelector("Target", target.Content, 0, .90f, 1, 1);
            _targets.onValueChanged.AddListener(SelectTarget);
            _targetHost = Rect("TargetPreview", target.Content, 0, .10f, 1, .88f);
            var detailInput = _targetHost.gameObject.AddComponent<Image>();
            detailInput.color = Color.clear;
            _targetDetail = _targetHost.gameObject.AddComponent<Button>();
            _targetDetail.targetGraphic = detailInput;
            _targetDetail.onClick.AddListener(() => UI_Popup_OwnerPlayerCard.Show(_root, _targetSnapshot));
            _targetFront = Rect("Front", _targetHost, .5f, .5f, .5f, .5f);
            _targetSummary = Label("TargetSummary", target.Content, "영입 대상을 선택하세요", 16, Ink, 0, 0, 1, .09f);
            for (int i = 0; i < 8; i++)
            {
                int slot = i;
                float x = i % 4 * .25f;
                float y = i < 4 ? .51f : 0;
                var cell = Rect("MaterialSlot" + (i + 1), board.Content, x, y, x + .24f, y + .49f);
                _materialHosts[i] = Rect("CardHost", cell, .04f, 0, .96f, 1);
                _materialHosts[i].offsetMin = new Vector2(0, 40);
                var mini = PlayerMiniCardView.CreateRuntime(_materialHosts[i], "MaterialCard");
                _materialViews[i] = mini;
                mini.UseLineupSlotLayout();
                mini.Selected += _ => OpenPicker(slot);
                _materialButtons[i] = Button("ChooseMaterial", cell, "재료 선택", .04f, 0, .96f, 0, () => OpenPicker(slot));
                ((RectTransform)_materialButtons[i].transform).offsetMax = new Vector2(0, 32);
            }
            _status = Label("SelectionStatus", _content, "", 16, Ink, 0, 0, .55f, .10f, TextAnchor.MiddleLeft);
            _autoSelect = Button("AutoSelect", _content, "재료 자동 배치", .57f, .01f, .76f, .10f, AutoSelect);
            _confirm = Button("ConfirmRecruit", _content, "선수 영입", .78f, .01f, 1, .10f, ConfirmRecruit);
            OwnerUiButtonSkin.Apply(_confirm, OwnerButtonRole.Primary);
            BuildPicker();
        }

        private Dropdown CreateSelector(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var obj = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            Place((RectTransform)obj.transform, x0, y0, x1, y1);
            foreach (var text in obj.GetComponentsInChildren<Text>(true))
            {
                text.font = _helpText.font;
                text.fontSize = 16;
                text.color = Ink;
            }
            var dropdown = obj.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.interactable = false;
            obj.transform.Find("Arrow").GetComponent<Image>().enabled = false;
            Label("Expand", obj.transform, "▼", 14, Ink, .91f, 0, .99f, 1);
            var border = obj.AddComponent<Outline>();
            border.effectColor = CareerUiTheme.ReferenceBorder;
            border.effectDistance = new Vector2(1, -1);
            obj.GetComponent<Image>().color = CareerUiTheme.ReferencePanel;
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 280);
            return dropdown;
        }

        private string Describe(string cardId)
        {
            var runtime = _manager.Runtime;
            var season = runtime.WorldCardCatalog.GetPlayerSeason(runtime.WorldCardCatalog.GetRequiredCard(cardId));
            return season.OriginYear + " " + runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId) +
                " · " + runtime.IdentityRegistry.GetFranchiseDisplayName(season.OriginFranchiseId);
        }

        private OwnerCollectionCardSnapshot CreateCardSnapshot(PlayerCardDefinition card)
        {
            var runtime = _manager.Runtime;
            var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            return new OwnerCollectionCardSnapshot(card.CardId, season.PlayerPersonId,
                runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId), season.OriginYear,
                season.Position, season.Cost, card.Edition, 0, 0, false, false,
                new OwnerCardAbilityResolver(_manager.Balance.Growth).ResolvePermanent(season, card, null),
                playerSeasonId: season.PlayerSeasonId, pitcherRole: season.PitcherRole,
                teamDisplayName: runtime.IdentityRegistry.GetFranchiseDisplayName(season.OriginFranchiseId),
                abilityGraphMaximum: _manager.Balance.MatchRatingCurve.Caps.HardCap,
                isOwnedCard: false, preferredBattingOrder: card.PreferredBattingOrder,
                isPositionEvidenceMissing: season.IsPositionEvidenceMissing);
        }

        private void SelectTarget(int index)
        {
            _selectedTarget = _targetCards != null && index >= 0 && index < _targetCards.Count ? _targetCards[index] : null;
            _isConfirming = false;
            _transactionId = null;
            Array.Clear(_selectedMaterials, 0, 8);
            OwnerRuntimeUiFactory.ClearChildren(_targetFront);
            if (_selectedTarget == null)
            {
                _targetDetail.interactable = false;
                _targetSummary.text = "등록된 영입 대상이 없습니다.";
                for (int i = 0; i < 8; i++)
                {
                    _materialViews[i].gameObject.SetActive(false);
                    _materialButtons[i].interactable = false;
                    _materialButtons[i].GetComponentInChildren<Text>().text = "대상 없음";
                }
                _status.text = "다른 영입 메뉴에서 대상을 확인하세요.";
                _confirm.interactable = _autoSelect.interactable = false;
                return;
            }
            FitCards();
            _targetSnapshot = CreateCardSnapshot(_selectedTarget);
            _targetDetail.interactable = true;
            UI_Popup_OwnerPlayerCard.BuildFrontCard(_targetFront, _targetSnapshot);
            var recipe = _manager.Runtime.WorldCardCatalog.SpecialCards.GetRequiredRecipe(_selectedTarget.CardId);
            for (int slot = 0; slot < 8; slot++)
                _materialIds[slot] = new List<string>(recipe.MaterialGroups[slot].CandidateCardIds);
            RefreshSelection();
        }

        private bool CanSelect(string id, HashSet<string> cards, HashSet<int> years)
        {
            if (id == null || !_manager.Runtime.CanUseSpecialRecruitMaterial(id) || cards.Contains(id)) return false;
            var catalog = _manager.Runtime.WorldCardCatalog;
            int year = catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear;
            return !_isCareerHigh || !years.Contains(year);
        }

        private void AutoSelect()
        {
            if (_selectedTarget == null) return;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            for (int slot = 0; slot < 8; slot++)
            {
                _selectedMaterials[slot] = null;
                foreach (string id in _materialIds[slot])
                {
                    if (!CanSelect(id, cards, years)) continue;
                    _selectedMaterials[slot] = id;
                    cards.Add(id);
                    var catalog = _manager.Runtime.WorldCardCatalog;
                    years.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear);
                    break;
                }
            }
            _isConfirming = false;
            _transactionId = null;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_selectedTarget == null) return;
            int count = 0;
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            bool owned = _manager.Runtime.TryGetOwnedCard(_selectedTarget.CardId, out _);
            var displayedCards = new HashSet<string>(StringComparer.Ordinal);
            var displayedYears = new HashSet<int>();
            foreach (string selectedId in _selectedMaterials)
            {
                if (selectedId == null) continue;
                displayedCards.Add(selectedId);
                var catalog = _manager.Runtime.WorldCardCatalog;
                displayedYears.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(selectedId)).OriginYear);
            }
            for (int slot = 0; slot < 8; slot++)
            {
                string selected = _selectedMaterials[slot];
                bool valid = CanSelect(selected, cards, years);
                if (valid)
                {
                    cards.Add(selected);
                    var catalog = _manager.Runtime.WorldCardCatalog;
                    years.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(selected)).OriginYear);
                    count++;
                }
                RefreshMaterial(slot, selected, valid, owned, displayedCards, displayedYears);
            }
            _targetSummary.text = owned ? "보유 중 · 카드 상세보기" : "카드 상세보기 · 영입 시 잠금 지급";
            _status.text = owned ? "이미 보유한 카드입니다. 다른 영입 대상을 선택하세요." :
                count == 8 ? "재료 8 / 8 · 영입 준비 완료" :
                "재료 " + count + " / 8 · 빈 슬롯을 눌러 필요한 카드를 확인하세요.";
            _confirm.interactable = count == 8 && !owned;
            _autoSelect.interactable = !owned;
            _confirm.GetComponentInChildren<Text>().text = "선수 영입";
        }

        private void RefreshMaterial(int slot, string selected, bool valid, bool owned,
            HashSet<string> displayedCards, HashSet<int> displayedYears)
        {
            string display = selected;
            if (display == null && _materialIds[slot].Count > 0)
            {
                display = FindMaterialPreview(slot, displayedCards, displayedYears);
            }
            var view = _materialViews[slot];
            view.gameObject.SetActive(display != null);
            _materialButtons[slot].interactable = !owned && display != null;
            if (display == null) { _materialButtons[slot].GetComponentInChildren<Text>().text = "후보 없음"; return; }
            var catalog = _manager.Runtime.WorldCardCatalog;
            var card = catalog.GetRequiredCard(display);
            var season = catalog.GetPlayerSeason(card);
            displayedCards.Add(display);
            displayedYears.Add(season.OriginYear);
            view.Bind(new PlayerMiniCardModel(card.CardId,
                _manager.Runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId),
                OwnerCollectionPresentationBuilder.FormatPlayerRole(season.Position, season.PitcherRole, season.IsPositionEvidenceMissing),
                (season.OriginYear % 100).ToString("00"), "", "", portraitAssetKey: season.PlayerSeasonId,
                isInteractable: !owned, frameEdition: card.Edition, cost: season.Cost),
                PlayerPortraitSprites.GetForPlayer(season.PlayerPersonId, season.Position));
            view.SetTeamIdentity(_manager.Runtime.IdentityRegistry.GetFranchiseDisplayName(season.OriginFranchiseId));
            view.SetVisualState(valid ? PlayerMiniCardVisualState.Selected : PlayerMiniCardVisualState.Normal);
            _materialButtons[slot].GetComponentInChildren<Text>().text = valid ? "등록됨 · 변경" :
                selected != null ? "사용 불가 · 변경" : _manager.Runtime.CanUseSpecialRecruitMaterial(display) ?
                "보유 · 등록" : _manager.Runtime.TryGetOwnedCard(display, out _) ? "보호 중 · 확인" : "미보유 · 확인";
        }

        private string FindMaterialPreview(int slot, HashSet<string> cards, HashSet<int> years)
        {
            var catalog = _manager.Runtime.WorldCardCatalog;
            string firstDistinct = null;
            foreach (string id in _materialIds[slot])
            {
                int year = catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear;
                if (cards.Contains(id) || (_isCareerHigh && years.Contains(year))) continue;
                if (_manager.Runtime.CanUseSpecialRecruitMaterial(id)) return id;
                firstDistinct ??= id;
            }
            return firstDistinct ?? _materialIds[slot][0];
        }

        private string MaterialState(string id) => _manager.Runtime.CanUseSpecialRecruitMaterial(id) ? "보유 · 등록 가능" :
            _manager.Runtime.TryGetOwnedCard(id, out _) ? "보호 중" : "미보유";

        private void ConfirmRecruit()
        {
            if (_selectedTarget == null || !_confirm.interactable) return;
            if (!_isConfirming)
            {
                _isConfirming = true;
                _transactionId = Guid.NewGuid().ToString("N");
                _status.text = "8장 소모 · 마지막 장의 성장도 사라집니다.\n다시 눌러 확정하거나 뒤로 가기로 취소하세요.";
                _confirm.GetComponentInChildren<Text>().text = "8장 소모 · 영입 확정";
                return;
            }
            try
            {
                string name = Describe(_selectedTarget.CardId);
                var materials = (string[])_selectedMaterials.Clone();
                _confirm.interactable = false;
                _manager.RecruitSpecialCard(_transactionId, _selectedTarget.CardId, materials);
                _isConfirming = false;
                Array.Clear(_selectedMaterials, 0, 8);
                RefreshSelection();
                _status.text = name + " · 영입 완료!";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _isConfirming = false;
                RefreshSelection();
                _status.text = "영입하지 못했습니다. 재료의 보유·보호 상태를 확인한 뒤 다시 시도하세요.";
            }
        }

        private void BuildPicker()
        {
            _picker = Surface("MaterialPicker", _root, CareerUiTheme.InputBlocker, 0, 0, 1, 1);
            _picker.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            _picker.GetComponent<Image>().raycastTarget = true;
            var panel = OwnerRuntimeUiFactory.CreatePanel("CandidatePanel", _picker, "영입 재료 선택");
            Place(panel.Root, .15f, .14f, .85f, .88f);
            _pickerTitle = Label("Instruction", panel.Content, "", 17, Ink, 0, .84f, 1, 1, TextAnchor.MiddleLeft);
            var scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("Candidates", panel.Content, out _pickerRows);
            Place((RectTransform)scroll.transform, 0, .16f, 1, .82f);
            _pickerClear = Button("Clear", panel.Content, "등록 해제", 0, 0, .30f, .12f, () => ChooseMaterial(null));
            Button("Cancel", panel.Content, "돌아가기", .70f, 0, 1, .12f, ClosePicker);
            _picker.gameObject.SetActive(false);
        }

        private void OpenPicker(int slot)
        {
            if (_selectedTarget == null || !_materialButtons[slot].interactable) return;
            _pickerSlot = slot;
            _isConfirming = false;
            _transactionId = null;
            RefreshSelection();
            _returnFocus = _materialButtons[slot].gameObject;
            _pickerTitle.text = "재료 " + (slot + 1) + " · 사용할 카드를 선택하세요.\n미보유·보호 중인 카드와 이미 등록한 카드" +
                (_isCareerHigh ? "·같은 연도" : "") + "는 선택할 수 없습니다.";
            var cards = new HashSet<string>(StringComparer.Ordinal);
            var years = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                if (i == slot || _selectedMaterials[i] == null) continue;
                string id = _selectedMaterials[i];
                cards.Add(id);
                var catalog = _manager.Runtime.WorldCardCatalog;
                years.Add(catalog.GetPlayerSeason(catalog.GetRequiredCard(id)).OriginYear);
            }
            for (int i = 0; i < _materialIds[slot].Count; i++)
            {
                if (i == _candidateButtons.Count)
                {
                    var button = OwnerWorkspaceUiFactory.CreateButton(_pickerRows, "Candidate" + i, "재료", null);
                    button.GetComponent<LayoutElement>().preferredHeight = 56;
                    button.gameObject.AddComponent<UIRecruitCandidateFocus>().Selected =
                        () => EnsureCandidateVisible((RectTransform)button.transform);
                    _candidateButtons.Add(button);
                }
                var row = _candidateButtons[i];
                string id = _materialIds[slot][i];
                bool available = CanSelect(id, cards, years);
                string state = available ? "선택 가능" : MaterialState(id);
                if (!available && _manager.Runtime.CanUseSpecialRecruitMaterial(id)) state = "다른 슬롯에 카드·연도 등록됨";
                row.GetComponentInChildren<Text>().text = Describe(id) + " · " + state;
                row.gameObject.SetActive(true);
                row.interactable = available;
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => ChooseMaterial(id));
            }
            for (int i = _materialIds[slot].Count; i < _candidateButtons.Count; i++)
                _candidateButtons[i].gameObject.SetActive(false);
            _pickerClear.interactable = _selectedMaterials[slot] != null;
            _picker.gameObject.SetActive(true);
            _picker.SetAsLastSibling();
            SetModalActive(true);
            _picker.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 1;
            foreach (var row in _candidateButtons)
                if (row.gameObject.activeSelf && row.interactable) { row.Select(); return; }
            _picker.Find("CandidatePanel/ContentSafeRect/Cancel").GetComponent<Button>().Select();
        }

        private void ChooseMaterial(string id)
        {
            _selectedMaterials[_pickerSlot] = id;
            ClosePicker();
            RefreshSelection();
        }

        private void EnsureCandidateVisible(RectTransform row)
        {
            var scroll = _picker.GetComponentInChildren<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, row);
            Rect viewport = scroll.viewport.rect;
            float delta = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
                bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
            if (Mathf.Approximately(delta, 0)) return;
            scroll.StopMovement();
            Vector2 position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, scroll.content.rect.height - viewport.height));
            scroll.content.anchoredPosition = position;
        }

        private void ClosePicker()
        {
            if (_picker == null) return;
            bool wasOpen = _picker.gameObject.activeSelf;
            _picker.gameObject.SetActive(false);
            SetModalActive(false);
            if (wasOpen && _returnFocus != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_returnFocus);
        }

        private void LateUpdate() => FitCards();

        private void SetModalActive(bool active)
        {
            _root.GetComponent<CanvasGroup>().interactable = !active;
            _content.GetComponent<CanvasGroup>().interactable = !active;
        }

        private void FitCards()
        {
            if (_targetFront == null) return;
            var parent = (RectTransform)_root.parent;
            _root.anchorMin = new Vector2(.5f, 0);
            _root.anchorMax = new Vector2(.5f, 1);
            _root.sizeDelta = new Vector2(Mathf.Min(parent.rect.width, SharedGameShellView.ReferenceWidth), 0);
            _root.anchoredPosition = Vector2.zero;
            bool compact = _root.rect.height < 640;
            _root.Find("Title").gameObject.SetActive(!compact);
            Place((RectTransform)_root.Find("Subtitle"), .02f, compact ? .925f : .91f, .98f, compact ? .995f : .943f);
            Vector2 targetSize = _targetHost.rect.size;
            Vector2 materialSize = _materialHosts[0].rect.size;
            if (_lastTargetSize == targetSize && _lastMaterialSize == materialSize) return;
            _lastTargetSize = targetSize;
            _lastMaterialSize = materialSize;
            FitCard(_targetFront, _targetHost, 456, 640);
            for (int i = 0; i < 8; i++)
                FitCard((RectTransform)_materialViews[i].transform, _materialHosts[i], 151, 212);
        }

        private Vector2 _lastTargetSize = new Vector2(-1, -1);
        private Vector2 _lastMaterialSize = new Vector2(-1, -1);

        private static void FitCard(RectTransform card, RectTransform host, float width, float height)
        {
            // 상세 카드 제작 규격을 유지하고 완성된 카드 전체를 같은 비율로 축소한다.
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(width, height);
            card.localScale = Vector3.one * Mathf.Max(0, Mathf.Min(host.rect.width / width, host.rect.height / height));
        }
    }

    /// <summary>후보 버튼의 키보드·게임패드 포커스가 화면 밖으로 사라지지 않도록 알린다.</summary>
    internal sealed class UIRecruitCandidateFocus : MonoBehaviour, ISelectHandler
    {
        public Action Selected;
        public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
    }
}

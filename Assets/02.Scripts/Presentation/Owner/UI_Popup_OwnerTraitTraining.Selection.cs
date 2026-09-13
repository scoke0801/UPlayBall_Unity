using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerTraitTraining
    {
        private RectTransform _picker, _pickerContent, _partnerContent;
        private ScrollRect _pickerScroll, _partnerScroll;
        private InputField _pickerSearch, _partnerSearch;
        private Dropdown _typeFilter, _rankFilter, _positionFilter, _registrationFilter, _sortFilter;
        private readonly OwnerCardFilters _originFilters = new OwnerCardFilters();
        private readonly List<PlayerMiniCardView> _pickerCards = new List<PlayerMiniCardView>();
        private readonly List<Button> _partnerRows = new List<Button>();
        private readonly List<string> _partnerIds = new List<string>();
        private readonly List<Text> _partnerNames = new List<Text>(), _partnerValues = new List<Text>();
        private readonly List<Text> _partnerDetails = new List<Text>();
        private readonly Dictionary<string, int> _partnerExperience = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _partnerReasons = new Dictionary<string, string>();
        private RectTransform _partnerToolbar;
        private Button _partnerAvailability, _clearPartners;
        private Text _partnerCount;
        private bool _showUnavailable;
        private const float PartnerRowHeight = 92f;
        private const float PartnerRowSpacing = 8f;
        private Text _pickerEmpty, _partnerEmpty, _pickerCount;
        private string _pickerQuery = "", _partnerQuery = "";
        private float _pickerPosition = 1;
        private readonly List<OwnerCollectionCardSnapshot> _filteredCards = new List<OwnerCollectionCardSnapshot>();
        private readonly List<Dropdown> _pickerDropdowns = new List<Dropdown>();

        private void BuildPartnerList(RectTransform right)
        {
            _partnerSearch = Search(right, "PartnerSearch", "이름 · 구단 · 포지션 검색", .035f, .74f, .96f, .82f,
                value => { _partnerQuery = value; RefreshPartners(); _partnerScroll.verticalNormalizedPosition = 1; });
            _partnerToolbar = Rect(right, "PartnerToolbar", .035f, .655f, .965f, .73f);
            _partnerAvailability = Button(_partnerToolbar, "Availability", "참여 가능만", 0, .06f, .30f, .94f, () => {
                _showUnavailable = !_showUnavailable; RefreshPartners(); _partnerScroll.verticalNormalizedPosition = 1;
            });
            _partnerCount = Label(_partnerToolbar, "Count", "", .33f, 0, 1, 1, 15);
            _partnerCount.alignment = TextAnchor.MiddleRight;
            _partnerScroll = Scroll(right, "Partners", .035f, .255f, .965f, .645f, out _partnerContent);
            _partnerEmpty = Label(right, "PartnerEmpty", "", .08f, .44f, .94f, .66f, 17);
            Place(_partnerEmpty.rectTransform, .07f, .32f, .93f, .57f);
            _clearPartners = Button(right, "ClearPartners", "선택 해제", .77f, .175f, .965f, .24f, () => {
                _partners.Clear(); _isConfirming = false; Refresh(); _partnerSearch.Select();
            });
        }

        private void RefreshPartners()
        {
            if (_collection == null) return;
            _partnerIds.Clear();
            _partnerExperience.Clear(); _partnerReasons.Clear();
            int available = 0, unavailable = 0;
            string targetReason = Card() == null ? "훈련할 선수를 먼저 선택하세요." : "";
            if (Card() != null)
            {
                try { OwnerTraitTrainingService.RequireAvailable(_manager.Runtime, _cardId); }
                catch (InvalidOperationException e) { targetReason = e.Message; }
            }
            foreach (var card in _collection.Cards)
            {
                if (card.CardId == _cardId || !card.IsOwnedCard) continue;
                if (!string.IsNullOrWhiteSpace(_partnerQuery) &&
                    (card.DisplayName + " " + card.TeamDisplayName + " " + OwnerCollectionPresentationBuilder.FormatPosition(card.Position))
                        .IndexOf(_partnerQuery, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                string reason = targetReason; int experience = 0;
                if (reason.Length == 0)
                {
                    try { experience = OwnerTraitTrainingService.PartnerExperience(_manager.Runtime, _cardId, card.CardId, _manager.TraitBalance); }
                    catch (InvalidOperationException e) { reason = e.Message; }
                }
                if (reason.Length == 0) available++; else unavailable++;
                if (!_showUnavailable && reason.Length > 0) continue;
                _partnerIds.Add(card.CardId);
                _partnerExperience[card.CardId] = experience; _partnerReasons[card.CardId] = reason;
            }
            _partnerIds.Sort((a,b) => {
                int result = (_partnerReasons[a].Length > 0).CompareTo(_partnerReasons[b].Length > 0);
                if (result == 0) result = _partnerExperience[b].CompareTo(_partnerExperience[a]);
                return result != 0 ? result : string.CompareOrdinal(a,b);
            });
            while (_partnerRows.Count < _partnerIds.Count)
            {
                int index = _partnerRows.Count;
                var row = Button(_partnerContent, "Partner" + index, " ", 0,0,1,1, () => SelectPartner(index));
                row.GetComponentInChildren<Text>().text = "";
                _partnerNames.Add(Label(row.transform, "Name", "", .025f, .55f, .70f, .95f, 18, true));
                _partnerDetails.Add(Label(row.transform, "Details", "", .025f, .07f, .73f, .54f, 15));
                _partnerValues.Add(Label(row.transform, "Value", "", .75f, .10f, .975f, .90f, 16));
                _partnerValues[index].alignment = TextAnchor.MiddleRight;
                _partnerRows.Add(row);
                KeepFocusVisible(row,_partnerScroll);
            }
            for (int i = 0; i < _partnerRows.Count; i++)
            {
                bool visible = i < _partnerIds.Count; var row = _partnerRows[i]; row.gameObject.SetActive(visible);
                if (!visible) continue;
                string id = _partnerIds[i]; var card = Snapshot(id);
                string reason = _partnerReasons[id]; int experience = _partnerExperience[id];
                row.interactable = reason.Length == 0 && Card()?.Trait.HasCandidates != true;
                Top((RectTransform)row.transform, i * (PartnerRowHeight + PartnerRowSpacing), PartnerRowHeight, 0, 1);
                bool selected = _partners.Contains(id);
                _partnerNames[i].text = (selected ? "선택됨  ·  " : "") + card.DisplayName + "  " + card.OriginYear;
                _partnerDetails[i].text = reason.Length > 0 ? reason :
                    OwnerCollectionPresentationBuilder.FormatPosition(card.Position) + " · " + card.TeamDisplayName
                    + "\n파트너 활동 가능 " + OwnerTraitTrainingService.RemainingUses(_manager.Runtime, id, _manager.TraitBalance) + "회";
                _partnerValues[i].text = reason.Length > 0 ? "참여 불가" : $"+{experience:N0} 경험치\n" + (selected ? "눌러서 해제" : "눌러서 선택");
                _partnerValues[i].color = reason.Length > 0 ? OwnerDashboardStyle.Muted : OwnerDashboardStyle.Gold;
                OwnerDashboardStyle.SetDataRow(row, selected, i % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
            }
            _partnerContent.sizeDelta = new Vector2(0, Mathf.Max(0, _partnerIds.Count * (PartnerRowHeight + PartnerRowSpacing) - PartnerRowSpacing));
            bool pending = Card()?.Trait.HasCandidates == true;
            _partnerEmpty.gameObject.SetActive(!pending && _partnerIds.Count == 0);
            _partnerEmpty.text = targetReason.Length > 0 ? targetReason : !string.IsNullOrEmpty(_partnerQuery)
                ? "검색 결과가 없습니다. 검색어를 바꿔 주세요."
                : unavailable > 0 ? "지금 참여 가능한 파트너가 없습니다.\n‘참여 가능만’을 눌러 전체 선수와 사유를 확인하세요."
                : "함께 훈련할 보유 선수가 없습니다.";
            _partnerToolbar.gameObject.SetActive(!pending); _clearPartners.gameObject.SetActive(!pending);
            _clearPartners.interactable = _partners.Count > 0;
            SetButtonText(_partnerAvailability, _showUnavailable ? "전체 선수" : "참여 가능만");
            _partnerCount.text = $"참여 가능 {available:N0}명 · 불가 {unavailable:N0}명 · 경험치순";
            RefreshNavigation();
        }

        private void SelectPartner(int index)
        {
            string id = _partnerIds[index];
            if (!_partners.Remove(id))
            {
                int rank = Math.Min(_manager.TraitBalance.slots.Length - 1, (int)_manager.TraitBalance.GetRank(Card().Trait.experience));
                // 같은 인물의 다른 시즌 카드는 기존 선택을 교체하며 중복 지정하지 않는다.
                string person = OwnerTraitTrainingService.Season(_manager.Runtime, id).PlayerPersonId;
                _partners.RemoveAll(existing => OwnerTraitTrainingService.Season(_manager.Runtime, existing).PlayerPersonId == person);
                if (_manager.TraitBalance.slots[rank] == 1) _partners.Clear();
                if (_partners.Count >= _manager.TraitBalance.slots[rank])
                { _feedback.text = "선택 슬롯이 찼습니다. 기존 파트너를 다시 눌러 해제하세요."; return; }
                _partners.Add(id);
            }
            _isConfirming = false; Refresh(); _partnerRows[index].Select();
        }

        private void BuildPicker()
        {
            _picker = Surface(_frame, "TargetPicker", .025f, .025f, .975f, .895f);
            _picker.GetComponent<Image>().raycastTarget = true;
            Label(_picker, "Title", "훈련할 선수 선택", .025f, .90f, .60f, .98f, 24, true);
            Button(_picker, "Back", "선택 취소", .80f, .90f, .975f, .98f, ClosePicker);
            _pickerSearch = Search(_picker, "Search", "선수 이름 검색", .025f, .81f, .38f, .88f,
                value => { _pickerQuery = value; FilterCards(); });
            _typeFilter = Filter(_picker, "Type", new[] { "타자·투수 전체", "타자", "투수" }, .40f, .81f, .58f, .88f);
            _rankFilter = Filter(_picker, "Rank", new[] { "특성 전체", "특성 미보유", "C등급", "B등급", "A등급", "S등급", "후보 선택 대기" }, .60f, .81f, .78f, .88f);
            _registrationFilter = Filter(_picker, "Registration", new[] { "등록 전체", "1군", "후보" }, .80f, .81f, .975f, .88f);
            var positions = new List<string> { "포지션 전체" };
            for (int i = 1; i <= 11; i++) positions.Add(OwnerCollectionPresentationBuilder.FormatPosition((PlayerPosition)i));
            _positionFilter = Filter(_picker, "Position", positions, .025f, .72f, .20f, .79f);
            var origins = Rect(_picker, "Origins", .22f, .72f, .66f, .79f);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(origins, 8);
            _originFilters.Build(origins, _collection.Cards, FilterCards);
            foreach (var dropdown in origins.GetComponentsInChildren<Dropdown>(true)) { StyleDropdown(dropdown); _pickerDropdowns.Add(dropdown); }
            _sortFilter = Filter(_picker, "Sort", new[] { "코스트 높은 순", "이름순", "특성 등급순", "카드 등급순" }, .68f, .72f, .975f, .79f);
            _pickerCount = Label(_picker, "Count", "", .025f, .64f, .74f, .71f, 15);
            Button(_picker, "Reset", "필터 초기화", .80f, .64f, .975f, .71f, () => {
                _pickerSearch.SetTextWithoutNotify(""); _pickerQuery = ""; _originFilters.Reset();
                foreach (var dropdown in _pickerDropdowns) dropdown.SetValueWithoutNotify(0);
                FilterCards(); });
            _pickerScroll = Scroll(_picker, "PlayerGrid", .025f, .025f, .975f, .635f, out _pickerContent);
            _pickerEmpty = Label(_picker, "Empty", "검색 결과가 없습니다. 필터를 초기화하거나 검색어를 바꿔 주세요.", .10f, .25f, .9f, .48f, 20);
            _picker.gameObject.SetActive(false);
        }

        private void OpenPicker()
        {
            _picker.gameObject.SetActive(true); _picker.SetAsLastSibling(); FilterCards();
            Canvas.ForceUpdateCanvases(); _pickerScroll.verticalNormalizedPosition = _pickerPosition;
            _pickerSearch.Select(); RefreshNavigation();
        }
        private void ClosePicker()
        { _pickerPosition = _pickerScroll.verticalNormalizedPosition; _picker.gameObject.SetActive(false); _chooseTarget.Select(); RefreshNavigation(); }

        private void FilterCards()
        {
            if (_sortFilter == null) return;
            _filteredCards.Clear();
            foreach (var card in _collection.Cards)
            {
                if (!card.IsOwnedCard || !_originFilters.Matches(card) ||
                    !string.IsNullOrEmpty(_pickerQuery) && card.DisplayName.IndexOf(_pickerQuery, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                _manager.Runtime.TryGetOwnedCard(card.CardId, out var owned);
                if (OwnerTraitTrainingService.ParticipantReason(_manager.Runtime,card.CardId).Length > 0) continue;
                bool pitcher = card.Position >= PlayerPosition.StartingPitcher;
                if (_typeFilter.value == 1 && pitcher || _typeFilter.value == 2 && !pitcher) continue;
                if (_positionFilter.value != 0 && (int)card.Position != _positionFilter.value) continue;
                if (_registrationFilter.value == 1 && !card.IsActiveRoster || _registrationFilter.value == 2 && card.IsActiveRoster) continue;
                if (_rankFilter.value >= 1 && _rankFilter.value <= 5 && (int)owned.Trait.rank != _rankFilter.value - 1) continue;
                if (_rankFilter.value == 6 && !owned.Trait.HasCandidates) continue;
                _filteredCards.Add(card);
            }
            _filteredCards.Sort((a,b) => {
                int compare;
                if (_sortFilter.value == 1) compare = string.Compare(a.DisplayName,b.DisplayName,StringComparison.CurrentCulture);
                else if (_sortFilter.value == 2) compare = b.GrowthBadges.TraitRank.CompareTo(a.GrowthBadges.TraitRank);
                else if (_sortFilter.value == 3) compare = b.Edition.CompareTo(a.Edition);
                else compare = b.Cost.CompareTo(a.Cost);
                return compare != 0 ? compare : string.CompareOrdinal(a.CardId,b.CardId); });
            while (_pickerCards.Count < _filteredCards.Count)
            {
                var view = PlayerMiniCardView.CreateRuntime(_pickerContent);
                view.Selected += model => SelectTarget(model.PlayerId);
                _pickerCards.Add(view);
                KeepFocusVisible(view.GetComponent<Button>(),_pickerScroll);
            }
            for (int i = 0; i < _pickerCards.Count; i++)
            {
                bool active = i < _filteredCards.Count; _pickerCards[i].gameObject.SetActive(active);
                if (active) _pickerCards[i].Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(_filteredCards[i], _filteredCards[i].CardId == _cardId));
            }
            _pickerEmpty.gameObject.SetActive(_filteredCards.Count == 0);
            _pickerCount.text = $"참여 가능한 보유 선수 {_filteredCards.Count:N0}명 · 회복·유학·다른 훈련 참여 선수 제외";
            LayoutPicker(); _pickerScroll.verticalNormalizedPosition = 1; RefreshNavigation();
        }

        private void SelectTarget(string id)
        {
            _cardId = id; _partners.Clear(); _chosen = CardTraitKind.None; _isConfirming = false;
            ClosePicker(); Refresh();
        }
        private void LayoutPicker()
        {
            if (_pickerContent == null) return;
            float width = _pickerScroll.viewport.rect.width;
            float cardWidth = Mathf.Max(1, (width - 40) / 5);
            float cardHeight = cardWidth * 1.4f;
            for (int i = 0; i < _filteredCards.Count; i++)
            {
                var rect = (RectTransform)_pickerCards[i].transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
                rect.sizeDelta = new Vector2(cardWidth,cardHeight);
                rect.anchoredPosition = new Vector2(i % 5 * (cardWidth + 10), -(i / 5) * (cardHeight + 12));
            }
            _pickerContent.sizeDelta = new Vector2(0, Mathf.Ceil(_filteredCards.Count / 5f) * (cardHeight + 12));
        }

        private void LateUpdate()
        {
            if (_picker != null && _picker.gameObject.activeSelf) LayoutPicker();
            var focus = EventSystem.current?.currentSelectedGameObject;
            // 다른 상세 팝업이 포커스를 소유하는 동안에는 포커스를 빼앗지 않는다.
            if (focus == null)
            {
                Transform area = _help != null && _help.gameObject.activeSelf ? _help : _picker != null && _picker.gameObject.activeSelf ? _picker : transform;
                foreach (var control in area.GetComponentsInChildren<Selectable>())
                    if (control.IsInteractable()) { control.Select(); break; }
            }
        }
        private void RefreshNavigation()
        {
            Transform area = _help != null && _help.gameObject.activeSelf ? _help : _picker != null && _picker.gameObject.activeSelf ? _picker : transform;
            var controls = new List<Selectable>();
            foreach (var selectable in area.GetComponentsInChildren<Selectable>())
                if (selectable.IsInteractable() && selectable.gameObject.activeInHierarchy) controls.Add(selectable);
            for (int i = 0; i < controls.Count; i++) controls[i].navigation = new Navigation {
                mode = Navigation.Mode.Explicit, selectOnUp = controls[(i + controls.Count - 1) % controls.Count],
                selectOnLeft = controls[(i + controls.Count - 1) % controls.Count], selectOnDown = controls[(i + 1) % controls.Count], selectOnRight = controls[(i + 1) % controls.Count] };
        }
        private static void KeepFocusVisible(Selectable selectable,ScrollRect scroll)
        {
            // EventTrigger는 등록하지 않은 휠·드래그도 소비하므로 선택 전용 공용 릴레이를 쓴다.
            var relay = selectable.GetComponent<UICardGridFocusRelay>() ?? selectable.gameObject.AddComponent<UICardGridFocusRelay>();
            relay.Selected = () => {
                var corners=new Vector3[4]; ((RectTransform)selectable.transform).GetWorldCorners(corners);
                float bottom=scroll.viewport.InverseTransformPoint(corners[0]).y;
                float top=scroll.viewport.InverseTransformPoint(corners[1]).y;
                float delta=top>scroll.viewport.rect.yMax?scroll.viewport.rect.yMax-top:
                    bottom<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin-bottom:0;
                scroll.content.anchoredPosition+=new Vector2(0,delta);
            };
        }

        private static ScrollRect Scroll(Transform parent, string name, float l,float b,float r,float t, out RectTransform content)
        {
            var view = UIXScrollView.Create(parent, name, Vector2.zero, Vector2.zero, Vector2.zero,
                false, true, OwnerDashboardStyle.TableSurface, OwnerDashboardStyle.Line, OwnerDashboardStyle.Gold, 12f);
            Place(view.Root, l,b,r,t);
            content = view.Content; content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f,1);
            OwnerDashboardStyle.SetDataSurface(view.Viewport.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            return view.ScrollRect;
        }
        private static void Top(RectTransform rect, float top, float height, float left, float right)
        { rect.anchorMin = new Vector2(left,1); rect.anchorMax = new Vector2(right,1); rect.pivot = new Vector2(.5f,1);
            rect.offsetMin = new Vector2(0,-top-height); rect.offsetMax = new Vector2(0,-top); }
        private static InputField Search(Transform parent,string name,string placeholder,float l,float b,float r,float t,Action<string> changed)
        {
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent,false);
            Place((RectTransform)go.transform,l,b,r,t); var input = go.GetComponent<InputField>(); input.characterLimit = 40;
            ((Text)input.placeholder).text = placeholder; OwnerDashboardStyle.SetDataInput(input);
            foreach (var text in go.GetComponentsInChildren<Text>()) text.fontSize = 16;
            input.onValueChanged.AddListener(value => changed(value)); return input;
        }
        private Dropdown Filter(Transform parent,string name,IReadOnlyList<string> values,float l,float b,float r,float t)
        {
            var dropdown = OwnerCardFilters.CreateDropdown(parent,name,new List<string>(values),0);
            Place((RectTransform)dropdown.transform,l,b,r,t); StyleDropdown(dropdown);
            dropdown.onValueChanged.AddListener(_ => FilterCards()); _pickerDropdowns.Add(dropdown); return dropdown;
        }
        private static void StyleDropdown(Dropdown dropdown)
        {
            OwnerDashboardStyle.SetDataSurface(dropdown.GetComponent<Image>(),OwnerDashboardStyle.TableSurface,true);
            OwnerDashboardStyle.ConfigureDataControl(dropdown);
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true)) { OwnerDashboardStyle.SetDataText(text); text.fontSize = 16; }
            if (dropdown.template != null)
            {
                var background = dropdown.template.GetComponent<Image>();
                if (background != null) OwnerDashboardStyle.SetDataSurface(background,OwnerDashboardStyle.TableHeader,true);
                foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
                {
                    if (toggle.targetGraphic is Image item) OwnerDashboardStyle.SetDataSurface(item,OwnerDashboardStyle.TableSelected,true);
                    if (toggle.graphic != null) toggle.graphic.color = OwnerDashboardStyle.Gold;
                }
            }
        }
    }
}

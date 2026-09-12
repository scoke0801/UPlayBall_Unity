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
        private Text _pickerEmpty, _partnerEmpty, _pickerCount;
        private string _pickerQuery = "", _partnerQuery = "";
        private float _pickerPosition = 1;
        private readonly List<OwnerCollectionCardSnapshot> _filteredCards = new List<OwnerCollectionCardSnapshot>();
        private readonly List<Dropdown> _pickerDropdowns = new List<Dropdown>();

        private void BuildPartnerList(RectTransform right)
        {
            _partnerSearch = Search(right, "PartnerSearch", "이름 · 구단 · 포지션 검색", .035f, .74f, .96f, .82f,
                value => { _partnerQuery = value; RefreshPartners(); });
            _partnerScroll = Scroll(right, "Partners", .035f, .35f, .965f, .72f, out _partnerContent);
            _partnerEmpty = Label(right, "PartnerEmpty", "", .08f, .44f, .94f, .66f, 17);
        }

        private void RefreshPartners()
        {
            if (_collection == null) return;
            _partnerIds.Clear();
            foreach (var card in _collection.Cards)
            {
                if (card.CardId == _cardId || !card.IsOwnedCard) continue;
                if (!string.IsNullOrWhiteSpace(_partnerQuery) &&
                    (card.DisplayName + " " + card.TeamDisplayName + " " + OwnerCollectionPresentationBuilder.FormatPosition(card.Position))
                        .IndexOf(_partnerQuery, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                _partnerIds.Add(card.CardId);
            }
            _partnerIds.Sort((a,b) => { int result = Snapshot(b).Cost.CompareTo(Snapshot(a).Cost); return result != 0 ? result : string.CompareOrdinal(a,b); });
            while (_partnerRows.Count < _partnerIds.Count)
            {
                int index = _partnerRows.Count;
                var row = Button(_partnerContent, "Partner" + index, " ", 0,0,1,1, () => SelectPartner(index));
                row.GetComponentInChildren<Text>().text = "";
                _partnerNames.Add(Label(row.transform, "Name", "", .035f, .05f, .69f, .95f, 16, true));
                _partnerValues.Add(Label(row.transform, "Value", "", .70f, .05f, .965f, .95f, 16));
                _partnerValues[index].alignment = TextAnchor.MiddleRight;
                _partnerRows.Add(row);
            }
            for (int i = 0; i < _partnerRows.Count; i++)
            {
                bool visible = i < _partnerIds.Count; var row = _partnerRows[i]; row.gameObject.SetActive(visible);
                if (!visible) continue;
                string id = _partnerIds[i]; var card = Snapshot(id); string reason = ""; int experience = 0;
                try { experience = OwnerTraitTrainingService.PartnerExperience(_manager.Runtime, _cardId, id, _manager.TraitBalance); }
                catch (InvalidOperationException e) { reason = e.Message; }
                row.interactable = reason.Length == 0 && Card()?.Trait.HasCandidates != true;
                Top((RectTransform)row.transform, i * 58, 56, 0, 1);
                bool selected = _partners.Contains(id);
                _partnerNames[i].text = (selected ? "선택 · " : "보유 · ") + card.DisplayName + "  " + card.OriginYear
                    + "\n" + (reason.Length > 0 ? reason : OwnerCollectionPresentationBuilder.FormatPosition(card.Position) + " · " + card.TeamDisplayName);
                _manager.Runtime.TryGetOwnedCard(id, out var owned);
                _partnerValues[i].text = reason.Length > 0 ? "참여 불가" : $"+{experience} 경험치\n잔여 {OwnerTraitTrainingService.RemainingUses(owned, _seenSeason, _manager.TraitBalance)}회";
                OwnerDashboardStyle.SetDataRow(row, selected, i % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
            }
            _partnerContent.sizeDelta = new Vector2(0, _partnerIds.Count * 58);
            _partnerEmpty.gameObject.SetActive(_partnerIds.Count == 0);
            _partnerEmpty.text = string.IsNullOrEmpty(_partnerQuery) ? "함께 훈련할 보유 선수가 없습니다." : "검색 결과가 없습니다. 검색어를 바꿔 주세요.";
            RefreshNavigation();
        }

        private void SelectPartner(int index)
        {
            string id = _partnerIds[index];
            if (!_partners.Remove(id))
            {
                int rank = Math.Min(3, (int)_manager.TraitBalance.GetRank(Card().Trait.experience));
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
            }
            for (int i = 0; i < _pickerCards.Count; i++)
            {
                bool active = i < _filteredCards.Count; _pickerCards[i].gameObject.SetActive(active);
                if (active) _pickerCards[i].Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(_filteredCards[i], _filteredCards[i].CardId == _cardId));
            }
            _pickerEmpty.gameObject.SetActive(_filteredCards.Count == 0);
            _pickerCount.text = $"보유 {_filteredCards.Count:N0}명 · 카드 선택 후 참여 가능 여부를 확인합니다.";
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
            if (focus == null) _close?.Select();
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
        private static ScrollRect Scroll(Transform parent, string name, float l,float b,float r,float t, out RectTransform content)
        {
            var root = Rect(parent,name,l,b,r,t); var image = root.gameObject.AddComponent<Image>();
            OwnerDashboardStyle.SetDataSurface(image,OwnerDashboardStyle.TableSurface,true);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect(root,"Viewport",0,0,1,1); viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport,"Content",0,1,1,1); content.pivot = new Vector2(.5f,1);
            scroll.viewport = viewport; scroll.content = content; return scroll;
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

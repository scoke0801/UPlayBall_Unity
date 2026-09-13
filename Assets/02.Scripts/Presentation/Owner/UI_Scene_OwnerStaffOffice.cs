using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>다섯 역할의 현재 스태프와 시장 제안을 SharedGameShell 슬롯에 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerStaffOffice : MonoBehaviour, IUiCancelHandler
    {
        private readonly List<Button> _offerButtons = new List<Button>();
        private readonly List<Text> _offerLabels = new List<Text>();
        private readonly List<Text> _offerDescriptions = new List<Text>();
        private readonly List<Text> _offerEffects = new List<Text>();
        private readonly List<Text> _offerCosts = new List<Text>();
        private readonly List<Button> _slotButtons = new List<Button>();
        private readonly List<Text> _slotLabels = new List<Text>();
        private Text _summaryText;
        private Text _marketSummaryText;
        private Text _comparisonText;
        private Text _termsText;
        private Text _reviewText;
        private Button _allRolesButton;
        private InputField _search;
        private Dropdown _qualityFilter;
        private Dropdown _availabilityFilter;
        private Dropdown _termFilter;
        private Dropdown _sortFilter;
        private Button _cancelButton;
        private ScrollRect _marketScroll;
        private ScrollRect _currentScroll;
        private ScrollRect _detailScroll;
        private int _selectedSlotIndex = -1;
        private bool _isReviewing;
        private bool _isSubmitting;
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _marketList;
        private Text _contentStateText;
        private RectTransform _currentStaffList;
        private Image _portrait;
        private Text _selectedNameText;
        private Text _selectedDetailText;
        private Text _signStateText;
        private Button _signButton;
        private OwnerStaffOfficePresentationModel _model;
        private Func<string, Sprite> _portraitResolver;
        private int _selectedOfferIndex = -1;

        public event Action<string> StaffOfferSelected;
        public event Action<string> SignStaffRequested;

        public static UI_Scene_OwnerStaffOffice CreateRuntime(
            RectTransform workspaceHost,
            RectTransform inspectorHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null) throw new ArgumentNullException(nameof(workspaceHost));
            if (inspectorHost == null) throw new ArgumentNullException(nameof(inspectorHost));
            if (actionBarHost == null) throw new ArgumentNullException(nameof(actionBarHost));
            var owner = new GameObject(nameof(UI_Scene_OwnerStaffOffice)).AddComponent<UI_Scene_OwnerStaffOffice>();
            owner.Build(workspaceHost, inspectorHost, actionBarHost);
            return owner;
        }

        public void Bind(OwnerStaffOfficePresentationModel model, Func<string, Sprite> portraitResolver = null)
        {
            bool restoreFocus = _isSubmitting || _isReviewing;
            string selectedOfferId = _model != null && _selectedOfferIndex >= 0 && _selectedOfferIndex < _model.Offers.Count
                ? _model.Offers[_selectedOfferIndex].OfferId : string.Empty;
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _portraitResolver = portraitResolver;
            _isReviewing = false;
            _isSubmitting = false;
            bool ready = model.Snapshot.ContentState.Kind == UiContentStateKind.Ready;
            _contentStateText.gameObject.SetActive(!ready);
            _contentStateText.text = ready
                ? string.Empty
                : $"{model.Snapshot.ContentState.Title}\n{model.Snapshot.ContentState.Message}";
            RenderCurrentStaff(model.Slots);
            _summaryText.text = ready ? model.GetSummary() : string.Empty;
            EnsureOfferButtons(model.Offers.Count);
            for (int index = 0; index < _offerButtons.Count; index++)
            {
                bool active = index < model.Offers.Count;
                _offerButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerStaffMarketOfferModel offer = model.Offers[index];
                _offerLabels[index].text = $"{offer.Name}   <size=15>{Accent(offer.QualityText)}</size>";
                _offerDescriptions[index].text = $"{offer.RoleText}  ·  {offer.SpecialtyText}";
                _offerEffects[index].text = offer.EffectText;
                _offerCosts[index].text = $"{offer.SalaryText}  ·  {offer.TermText}" +
                    (offer.CanSign ? string.Empty : $"  ·  {offer.DisabledReason}");
                _offerButtons[index].interactable = ready;
            }
            _selectedOfferIndex = model.Offers.Count > 0 ? 0 : -1;
            for (int index = 0; index < model.Offers.Count; index++)
                if (string.Equals(model.Offers[index].OfferId, selectedOfferId, StringComparison.Ordinal))
                    _selectedOfferIndex = index;
            ApplyRoleFilter();
            if (restoreFocus)
            {
                if (_selectedOfferIndex >= 0) _offerButtons[_selectedOfferIndex].Select();
                else _allRolesButton.Select();
            }
        }

        public void SetVisible(bool visible)
        {
            if (!visible) CancelReview(false);
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>공용 뒤로 가기 입력은 계약 검토를 먼저 취소한다.</summary>
        public bool TryHandleCancel()
        {
            if (!_isReviewing || _workspaceRoot == null || !_workspaceRoot.gameObject.activeInHierarchy) return false;
            CancelReview(true);
            return true;
        }

        /// <summary>Staff 계약 Command 실패를 현재 Action Bar에 즉시 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            _isSubmitting = false;
            CancelReview(false);
            _signStateText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _signStateText.color = isError ? OwnerDashboardStyle.Danger : OwnerDashboardStyle.Success;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _offerButtons.Count; index++)
                _offerButtons[index].onClick.RemoveAllListeners();
            if (_signButton != null) _signButton.onClick.RemoveAllListeners();
            if (_cancelButton != null) _cancelButton.onClick.RemoveAllListeners();
            if (_allRolesButton != null) _allRolesButton.onClick.RemoveAllListeners();
            if (_search != null) _search.onValueChanged.RemoveAllListeners();
            if (_qualityFilter != null) _qualityFilter.onValueChanged.RemoveAllListeners();
            if (_availabilityFilter != null) _availabilityFilter.onValueChanged.RemoveAllListeners();
            if (_termFilter != null) _termFilter.onValueChanged.RemoveAllListeners();
            if (_sortFilter != null) _sortFilter.onValueChanged.RemoveAllListeners();
            for (int index = 0; index < _slotButtons.Count; index++)
                _slotButtons[index].onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionBarHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerStaffOfficeWorkspace", true);
            OwnerRuntimeUiFactory.Stretch(UIClubOfficeStyle.Surface("OfficePaper", _workspaceRoot, UIClubOfficeStyle.Paper).rectTransform);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "WorkspaceColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns);

            OwnerWorkspaceUiFactory.Panel current = UIClubOfficeStyle.CreatePanel(
                columns, "CurrentStaffPanel", "우리 구단 코칭스태프", true);
            OwnerWorkspaceUiFactory.SetFlexible(current.Root, .48f);
            _summaryText = UIClubOfficeStyle.Label("StaffSummary", current.Content, string.Empty, 18, true);
            PlaceTop(_summaryText.rectTransform, 0f, 72f);
            Text instruction = UIClubOfficeStyle.Label("RoleHint", current.Content, "역할 선택 → 후보 비교 → 계약", 14);
            PlaceTop(instruction.rectTransform, 76f, 28f);
            _currentScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("CurrentStaffRows", current.Content, out _currentStaffList);
            OwnerDashboardStyle.ApplyInset(_currentScroll.GetComponent<Image>(), true);
            OwnerRuntimeUiFactory.Stretch(_currentScroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, -116f));

            OwnerWorkspaceUiFactory.Panel market = UIClubOfficeStyle.CreatePanel(
                columns, "StaffMarketPanel", "영입 후보");
            OwnerWorkspaceUiFactory.SetFlexible(market.Root, 1f);
            BuildFilters(market.Content);
            _marketSummaryText = UIClubOfficeStyle.Label("MarketSummary", market.Content, string.Empty, 15);
            PlaceTop(_marketSummaryText.rectTransform, 140f, 48f);
            _marketScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("MarketList", market.Content, out _marketList);
            OwnerDashboardStyle.ApplyInset(_marketScroll.GetComponent<Image>(), true);
            OwnerRuntimeUiFactory.Stretch(_marketScroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, -196f));

            _contentStateText = OwnerWorkspaceUiFactory.CreateText(_marketScroll.viewport, "ContentState", string.Empty,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.Stretch(_contentStateText.rectTransform);
            _contentStateText.gameObject.SetActive(false);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerStaffOfficeInspector", false);
            OwnerWorkspaceUiFactory.Panel detail = UIClubOfficeStyle.CreatePanel(
                _inspectorRoot, "StaffDetailPanel", "영입 검토");
            OwnerWorkspaceUiFactory.Stretch(detail.Root);
            _detailScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("DetailScroll", detail.Content, out RectTransform detailContent);
            OwnerDashboardStyle.ApplyInset(_detailScroll.GetComponent<Image>(), true);
            OwnerRuntimeUiFactory.Stretch(_detailScroll.GetComponent<RectTransform>());
            RectTransform portraitRect = OwnerWorkspaceUiFactory.CreateRoot(detailContent, "StaffPortrait", false);
            _portrait = portraitRect.gameObject.AddComponent<Image>();
            _portrait.color = CareerUiTheme.PortraitBackdrop;
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            portraitRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 112f;
            _selectedNameText = AddLine(detailContent, 24, FontStyle.Bold, 64f);
            _selectedDetailText = AddLine(detailContent, 16, FontStyle.Normal, 80f);
            _comparisonText = AddLine(detailContent, 16, FontStyle.Normal, 148f);
            _termsText = AddLine(detailContent, 16, FontStyle.Normal, 168f);
            _reviewText = AddLine(detailContent, 16, FontStyle.Normal, 88f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerStaffOfficeActionBar", false);
            Image actionPaper = _actionRoot.gameObject.AddComponent<Image>();
            actionPaper.color = UIClubOfficeStyle.Paper;
            _actionRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 4, 4);
            _signStateText = OwnerWorkspaceUiFactory.CreateText(_actionRoot, "SignState", string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_signStateText.rectTransform, 1f, 0f);
            _cancelButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "CancelReview", "검토 취소", () => CancelReview(true));
            UIClubOfficeStyle.SizeAction(_cancelButton, 128f);
            _cancelButton.gameObject.SetActive(false);
            _signButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "ConfirmStaffSigningButton", "계약 검토", HandleSignRequested);
            UIClubOfficeStyle.SizeAction(_signButton, 208f, true);
            _signStateText.GetComponent<LayoutElement>().minHeight = 0f;
            actionPaper.raycastTarget = false;
            OwnerDashboardStyle.SetDataText(_signStateText);
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
            OwnerDashboardStyle.ApplyActionBar(_actionRoot);
        }

        private void EnsureOfferButtons(int count)
        {
            while (_offerButtons.Count < count)
            {
                int capturedIndex = _offerButtons.Count;
                Button button = OwnerWorkspaceUiFactory.CreateButton(
                    _marketList,
                    $"StaffOffer{capturedIndex}",
                    string.Empty,
                    () => SelectOffer(capturedIndex));
                LayoutElement layout = button.GetComponent<LayoutElement>();
                layout.preferredHeight = 156f;
                layout.minHeight = 156f;
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 22;
                button.gameObject.AddComponent<UICardGridFocusRelay>().Selected = () => Reveal(_marketScroll, button);
                UIClubOfficeStyle.Select(button, false);
                _offerButtons.Add(button);
                _offerLabels.Add(label);
                _offerDescriptions.Add(CreateOfferLine(button, "Specialty", 15, 42f, 25f));
                Text effect = CreateOfferLine(button, "Effect", 18, 71f, 26f);
                effect.color = OwnerDashboardStyle.Gold;
                _offerEffects.Add(effect);
                _offerCosts.Add(CreateOfferLine(button, "AnnualCost", 15, 102f, 44f));
            }
        }

        private static Text CreateOfferLine(Button button, string name, int size, float top, float height)
        {
            Text text = UIClubOfficeStyle.Label(name, button.transform, string.Empty, size);
            PlaceOfferLine(text.rectTransform, top, height);
            return text;
        }

        private static void PlaceOfferLine(RectTransform rect, float top, float height)
        {
            PlaceTop(rect, top, height);
            rect.offsetMin += new Vector2(16f, 0f);
            rect.offsetMax -= new Vector2(16f, 0f);
        }

        private void BuildFilters(RectTransform parent)
        {
            RectTransform searchRow = OwnerWorkspaceUiFactory.CreateRoot(parent, "SearchRow", false);
            PlaceTop(searchRow, 0f, 40f);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(searchRow, CareerUiTheme.Space2);
            GameObject field = DefaultControls.CreateInputField(new DefaultControls.Resources());
            field.name = "StaffSearch";
            field.transform.SetParent(searchRow, false);
            _search = field.GetComponent<InputField>();
            _search.characterLimit = 40;
            foreach (Text text in field.GetComponentsInChildren<Text>(true))
            { text.font = UIProjectFonts.Body; text.fontSize = 15; text.fontStyle = FontStyle.Normal; }
            ((Text)_search.placeholder).text = "스태프 검색";
            OwnerWorkspaceUiFactory.SetFlexible(field.GetComponent<RectTransform>(), 1f);
            OwnerDashboardStyle.SetDataInput(_search);
            _search.onValueChanged.AddListener(_ => ChangeFilters());
            _allRolesButton = OwnerWorkspaceUiFactory.CreateButton(searchRow, "ResetFilters", "필터 초기화", ResetFilters);
            UIClubOfficeStyle.SizeAction(_allRolesButton, 120f);
            _allRolesButton.GetComponent<LayoutElement>().flexibleWidth = 0f;

            RectTransform first = OwnerWorkspaceUiFactory.CreateRoot(parent, "QualityAndBudget", false);
            PlaceTop(first, 48f, 38f);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(first, CareerUiTheme.Space2);
            _qualityFilter = CreateFilter(first, "Quality", new[] { "모든 등급", "2등급 이상", "3등급 이상", "4등급 이상", "5등급" });
            _availabilityFilter = CreateFilter(first, "Availability", new[] { "계약 여부 전체", "지금 계약 가능" });
            RectTransform second = OwnerWorkspaceUiFactory.CreateRoot(parent, "TermAndSort", false);
            PlaceTop(second, 94f, 38f);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(second, CareerUiTheme.Space2);
            _termFilter = CreateFilter(second, "ContractTerm", new[] { "모든 계약 기간", "1시즌 계약", "2시즌 이하" });
            _sortFilter = CreateFilter(second, "Sort", new[] { "등급 높은 순", "연봉 낮은 순", "연봉 높은 순" });
        }

        private Dropdown CreateFilter(RectTransform parent, string name, string[] options)
        {
            Dropdown dropdown = OwnerCardFilters.CreateDropdown(parent, name, new List<string>(options), 0);
            dropdown.captionText.fontSize = 15;
            dropdown.itemText.fontSize = 15;
            OwnerDashboardStyle.SetDataDropdown(dropdown);
            dropdown.onValueChanged.AddListener(_ => ChangeFilters());
            return dropdown;
        }

        private void ChangeFilters()
        {
            _isReviewing = false;
            ApplyRoleFilter();
            if (_marketScroll != null) _marketScroll.verticalNormalizedPosition = 1f;
        }

        private void ResetFilters()
        {
            _selectedSlotIndex = -1;
            _search.SetTextWithoutNotify(string.Empty);
            _qualityFilter.SetValueWithoutNotify(0);
            _availabilityFilter.SetValueWithoutNotify(0);
            _termFilter.SetValueWithoutNotify(0);
            _sortFilter.SetValueWithoutNotify(0);
            ChangeFilters();
        }

        private void SelectOffer(int index)
        {
            if (_model == null || index < 0 || index >= _model.Offers.Count) return;
            _selectedOfferIndex = index;
            _isReviewing = false;
            RenderSelectedOffer();
            _detailScroll.verticalNormalizedPosition = 1f;
            StaffOfferSelected?.Invoke(_model.Offers[index].OfferId);
        }

        private void RenderSelectedOffer()
        {
            _cancelButton.gameObject.SetActive(_isReviewing);
            _reviewText.transform.parent.gameObject.SetActive(_isReviewing);
            _signButton.GetComponentInChildren<Text>().text = _isReviewing ? "계약 확정" : "계약 검토";
            for (int index = 0; index < _offerButtons.Count; index++)
            {
                UIClubOfficeStyle.Select(_offerButtons[index], index == _selectedOfferIndex);
                PlaceOfferLine(_offerLabels[index].rectTransform, 10f, 30f);
            }
            if (_model == null || _selectedOfferIndex < 0 || _selectedOfferIndex >= _model.Offers.Count)
            {
                _selectedNameText.text = "후보를 선택하세요";
                _selectedDetailText.text = "역할별 영입 후보를 선택하면 현재 담당자와 계약 조건을 비교할 수 있습니다.";
                _comparisonText.text = string.Empty;
                _termsText.text = string.Empty;
                if (_model != null && _selectedSlotIndex >= 0 && _selectedSlotIndex < _model.Slots.Count)
                {
                    OwnerStaffSlotModel slot = _model.Slots[_selectedSlotIndex];
                    _selectedNameText.text = slot.RoleText + "\n" + slot.Name;
                    _selectedDetailText.text = slot.IsVacant ? "현재 담당자가 없습니다. 다음 시장 기간에 후보를 확인하세요."
                        : slot.SpecialtyText + "\n" + slot.PhilosophyText;
                    _comparisonText.text = Accent("현재 적용 효과") + "\n" + slot.EffectText;
                    _termsText.text = slot.IsVacant ? string.Empty : Accent("현재 계약") + $"\n{slot.QualityText}\n{slot.SalaryText}\n{slot.TermText}";
                }
                _portrait.sprite = null;
                _portrait.gameObject.SetActive(false);
                _comparisonText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_comparisonText.text));
                _termsText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_termsText.text));
                _portrait.color = CareerUiTheme.PortraitBackdrop;
                _signButton.interactable = false;
                _signStateText.text = "계약 가능한 제안이 없습니다.";
                return;
            }

            OwnerStaffMarketOfferModel offer = _model.Offers[_selectedOfferIndex];
            OwnerStaffSlotModel current = _model.GetCurrentSlot(offer);
            _comparisonText.transform.parent.gameObject.SetActive(true);
            _termsText.transform.parent.gameObject.SetActive(true);
            _selectedNameText.text = $"{offer.Name}\n<size=16>{offer.RoleText} · {offer.QualityText}</size>";
            _selectedDetailText.text =
                Accent("지도 전문성") + $"\n{offer.SpecialtyText}\n{offer.PhilosophyText}";
            _comparisonText.text = Accent("현재 담당") + $"\n{current?.Name ?? "미배치"}" +
                (current != null && !current.IsVacant ? $" · {current.QualityText}\n{current.SalaryText} · {current.TermText}" : string.Empty) +
                $"\n{current?.EffectText ?? "현재 적용 효과 없음"}\n\n" + Accent("영입 후") + $"\n{offer.EffectText}";
            _termsText.text = Accent("계약 조건") + $"\n{offer.SalaryText}\n{offer.TermText}\n\n{offer.SigningCostText}";
            _reviewText.text = current != null && !current.IsVacant
                ? $"{current.Name}의 계약을 종료하고 {offer.Name}을 배치합니다. 위약금은 즉시 지출에 포함되며 연봉은 별도 정산됩니다."
                : $"확정하면 계약금을 지출하고 {offer.Name}을 즉시 배치합니다. 연봉은 시즌 급여로 별도 정산됩니다.";
            _portrait.sprite = _portraitResolver?.Invoke(offer.PortraitAssetKey);
            _portrait.gameObject.SetActive(_portrait.sprite != null);
            _portrait.color = _portrait.sprite == null ? CareerUiTheme.PortraitBackdrop : Color.white;
            _signButton.interactable = offer.CanSign && !_isSubmitting;
            _signStateText.text = offer.CanSign
                ? (_isReviewing ? $"{offer.Name} · 비용과 담당자 변경을 확인한 뒤 확정하세요." : $"{offer.Name} · 효과와 계약 조건을 비교한 뒤 검토하세요.")
                : offer.DisabledReason;
            _signStateText.color = offer.CanSign ? CareerUiTheme.TextPrimary : CareerUiTheme.Warning;
        }

        private void HandleSignRequested()
        {
            if (_model == null || _selectedOfferIndex < 0 || _selectedOfferIndex >= _model.Offers.Count) return;
            OwnerStaffMarketOfferModel offer = _model.Offers[_selectedOfferIndex];
            if (!offer.CanSign || _isSubmitting) return;
            if (!_isReviewing)
            {
                _isReviewing = true;
                RenderSelectedOffer();
                _detailScroll.verticalNormalizedPosition = 0f;
                _cancelButton.Select();
                return;
            }
            _isSubmitting = true;
            _signButton.interactable = false;
            _signStateText.text = "계약을 처리하고 있습니다…";
            SignStaffRequested?.Invoke(offer.OfferId);
        }

        private void RenderCurrentStaff(IReadOnlyList<OwnerStaffSlotModel> slots)
        {
            while (_slotButtons.Count < slots.Count)
            {
                int captured = _slotButtons.Count;
                Button button = OwnerWorkspaceUiFactory.CreateButton(_currentStaffList, "StaffRole" + captured,
                    string.Empty, () => SelectSlot(captured));
                LayoutElement layout = button.GetComponent<LayoutElement>();
                layout.minHeight = 96f;
                layout.preferredHeight = 96f;
                Text label = button.GetComponentInChildren<Text>();
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleLeft;
                button.gameObject.AddComponent<UICardGridFocusRelay>().Selected = () => Reveal(_currentScroll, button);
                _slotButtons.Add(button);
                _slotLabels.Add(label);
            }
            for (int index = 0; index < _slotButtons.Count; index++)
            {
                _slotButtons[index].gameObject.SetActive(index < slots.Count);
                if (index >= slots.Count) continue;
                OwnerStaffSlotModel slot = slots[index];
                int candidates = 0;
                for (int offer = 0; offer < _model.Offers.Count; offer++)
                    if (_model.GetCurrentSlot(_model.Offers[offer])?.Role == slot.Role) candidates++;
                _slotLabels[index].text = Accent(slot.RoleText) + $"  ·  후보 {candidates}명\n" +
                    (slot.IsVacant ? "<size=20>공석</size>   보강 가능"
                    : $"<size=20>{slot.Name}</size>  {slot.TermText}\n{slot.EffectText}");
            }
        }

        private void SelectSlot(int index)
        {
            _selectedSlotIndex = index;
            _isReviewing = false;
            ApplyRoleFilter();
            _marketScroll.verticalNormalizedPosition = 1f;
        }

        private void ApplyRoleFilter()
        {
            if (_model == null) return;
            if (_selectedSlotIndex >= _model.Slots.Count) _selectedSlotIndex = -1;
            List<int> matches = _model.FindOffers(_selectedSlotIndex, _search.text, _qualityFilter.value + 1,
                _availabilityFilter.value == 1, _termFilter.value, _sortFilter.value);
            int count = matches.Count;
            for (int index = 0; index < _offerButtons.Count; index++)
                _offerButtons[index].gameObject.SetActive(false);
            for (int position = 0; position < matches.Count; position++)
            {
                int index = matches[position];
                _offerButtons[index].gameObject.SetActive(true);
                _offerButtons[index].transform.SetSiblingIndex(position);
            }
            if (!matches.Contains(_selectedOfferIndex)) _selectedOfferIndex = count > 0 ? matches[0] : -1;
            for (int index = 0; index < _slotButtons.Count; index++)
            {
                UIClubOfficeStyle.Select(_slotButtons[index], index == _selectedSlotIndex);
                OwnerDashboardStyle.SetDataText(_slotLabels[index]);
            }
            string role = _selectedSlotIndex < 0 ? "전체 역할" : _model.Slots[_selectedSlotIndex].RoleText;
            _marketSummaryText.text = $"{role} · 후보 {count}명 · 매주 갱신";
            bool ready = _model.Snapshot.ContentState.Kind == UiContentStateKind.Ready;
            _allRolesButton.interactable = ready;
            if (ready)
            {
                _contentStateText.gameObject.SetActive(count == 0);
                _contentStateText.text = _model.Offers.Count == 0 ? "현재 영입 후보가 없습니다.\n다음 주에 다시 확인하세요."
                    : "조건에 맞는 후보가 없습니다.\n필터 초기화로 전체 후보를 확인하세요.";
            }
            RenderSelectedOffer();
        }

        private void CancelReview(bool restoreFocus)
        {
            restoreFocus |= _cancelButton != null && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == _cancelButton.gameObject;
            _isReviewing = false;
            if (_signButton == null) return;
            RenderSelectedOffer();
            if (restoreFocus) _signButton.Select();
        }

        private static string Accent(string value) => $"<color=#{ColorUtility.ToHtmlStringRGB(OwnerDashboardStyle.Gold)}>{value}</color>";

        private static void PlaceTop(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static void Reveal(ScrollRect scroll, Button button)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform row = button.GetComponent<RectTransform>();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, row);
            float delta = bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y
                : bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y : 0f;
            scroll.content.anchoredPosition += new Vector2(0f, delta);
        }

        private static Text AddLine(Transform parent, int size, FontStyle style, float height)
        {
            Image section = UIClubOfficeStyle.Surface("DetailSection", parent, UIClubOfficeStyle.Paper);
            OwnerDashboardStyle.ApplyInset(section);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            section.gameObject.AddComponent<LayoutElement>().minHeight = height;
            // 폭에 맞춘 Text의 preferredHeight를 사용해 긴 이름·금액도 상세 스크롤 안에서 늘어난다.
            Text text = OwnerWorkspaceUiFactory.CreateText(section.transform, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerDashboardStyle.SetDataText(text, style == FontStyle.Bold);
            return text;
        }
    }
}

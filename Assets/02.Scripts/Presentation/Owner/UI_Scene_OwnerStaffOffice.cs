using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>다섯 역할의 현재 스태프와 시장 제안을 SharedGameShell 슬롯에 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerStaffOffice : MonoBehaviour
    {
        private readonly List<Button> _offerButtons = new List<Button>();
        private readonly List<Text> _offerLabels = new List<Text>();
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
            string selectedOfferId = _model != null && _selectedOfferIndex >= 0 && _selectedOfferIndex < _model.Offers.Count
                ? _model.Offers[_selectedOfferIndex].OfferId : string.Empty;
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _portraitResolver = portraitResolver;
            bool ready = model.Snapshot.ContentState.Kind == UiContentStateKind.Ready;
            _contentStateText.gameObject.SetActive(!ready);
            _contentStateText.text = ready
                ? string.Empty
                : $"{model.Snapshot.ContentState.Title}\n{model.Snapshot.ContentState.Message}";
            RenderCurrentStaff(model.Slots);
            EnsureOfferButtons(model.Offers.Count);
            for (int index = 0; index < _offerButtons.Count; index++)
            {
                bool active = index < model.Offers.Count;
                _offerButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerStaffMarketOfferModel offer = model.Offers[index];
                _offerLabels[index].text = $"{offer.RoleText}  {offer.Name}\n{offer.QualityText} · {offer.SalaryText} · {offer.TermText}";
                _offerButtons[index].interactable = ready;
            }
            _selectedOfferIndex = model.Offers.Count > 0 ? 0 : -1;
            for (int index = 0; index < model.Offers.Count; index++)
                if (string.Equals(model.Offers[index].OfferId, selectedOfferId, StringComparison.Ordinal))
                    _selectedOfferIndex = index;
            RenderSelectedOffer();
            if (ready && model.Offers.Count == 0)
            {
                _contentStateText.transform.SetParent(_marketList.parent, false);
                OwnerRuntimeUiFactory.Stretch(_contentStateText.rectTransform);
                _contentStateText.gameObject.SetActive(true);
                _contentStateText.text = "현재 영입 후보가 없습니다.\n시장 기간이 열리면 계약 후보가 표시됩니다.";
                OwnerDashboardStyle.SetDataText(_contentStateText);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>Staff 계약 Command 실패를 현재 Action Bar에 즉시 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            _signStateText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _signStateText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _offerButtons.Count; index++)
                _offerButtons[index].onClick.RemoveAllListeners();
            if (_signButton != null) _signButton.onClick.RemoveAllListeners();
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
            OwnerWorkspaceUiFactory.SetFlexible(current.Root, 1f);
            Image office = UIClubOfficeStyle.Illustration(current.Content, "CoachingOffice", 7);
            UIClubOfficeStyle.Place(office.rectTransform, 0f, .77f, 1f, 1f);
            ScrollRect currentScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("CurrentStaffRows", current.Content, out _currentStaffList);
            OwnerDashboardStyle.SetDataSurface(currentScroll.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            UIClubOfficeStyle.Place(currentScroll.GetComponent<RectTransform>(), 0f, 0f, 1f, .75f);

            OwnerWorkspaceUiFactory.Panel market = UIClubOfficeStyle.CreatePanel(
                columns, "StaffMarketPanel", "영입 후보");
            OwnerWorkspaceUiFactory.SetFlexible(market.Root, 1f);
            ScrollRect marketScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("MarketList", market.Content, out _marketList);
            OwnerDashboardStyle.SetDataSurface(marketScroll.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            OwnerRuntimeUiFactory.Stretch(marketScroll.GetComponent<RectTransform>());

            _contentStateText = OwnerWorkspaceUiFactory.CreateText(_workspaceRoot, "ContentState", string.Empty,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.Stretch(_contentStateText.rectTransform);
            _contentStateText.gameObject.SetActive(false);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerStaffOfficeInspector", false);
            OwnerWorkspaceUiFactory.Panel detail = UIClubOfficeStyle.CreatePanel(
                _inspectorRoot, "StaffDetailPanel", "스태프 상세");
            OwnerWorkspaceUiFactory.Stretch(detail.Root);
            ScrollRect detailScroll = OwnerRuntimeUiFactory.CreateVerticalScroll("DetailScroll", detail.Content, out RectTransform detailContent);
            OwnerDashboardStyle.SetDataSurface(detailScroll.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            OwnerRuntimeUiFactory.Stretch(detailScroll.GetComponent<RectTransform>());
            RectTransform portraitRect = OwnerWorkspaceUiFactory.CreateRoot(detailContent, "StaffPortrait", false);
            _portrait = portraitRect.gameObject.AddComponent<Image>();
            _portrait.color = CareerUiTheme.PortraitBackdrop;
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            portraitRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 190f;
            _selectedNameText = AddLine(detailContent, 20, FontStyle.Bold, 60f);
            _selectedDetailText = AddLine(detailContent, 14, FontStyle.Normal, 310f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerStaffOfficeActionBar", false);
            Image actionPaper = _actionRoot.gameObject.AddComponent<Image>();
            actionPaper.color = UIClubOfficeStyle.Paper;
            _actionRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 4, 4);
            _signStateText = OwnerWorkspaceUiFactory.CreateText(_actionRoot, "SignState", string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleRight, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_signStateText.rectTransform, 1f, 0f);
            _signButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "ConfirmStaffSigningButton", "스태프 계약", HandleSignRequested);
            UIClubOfficeStyle.SizeAction(_signButton, 160f, true);
            _signStateText.GetComponent<LayoutElement>().minHeight = 0f;
            actionPaper.raycastTarget = false;
            OwnerDashboardStyle.SetDataText(_signStateText);
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
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
                layout.preferredHeight = 66f;
                layout.minHeight = 66f;
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UIClubOfficeStyle.Select(button, false);
                _offerButtons.Add(button);
                _offerLabels.Add(label);
            }
        }

        private void SelectOffer(int index)
        {
            if (_model == null || index < 0 || index >= _model.Offers.Count) return;
            _selectedOfferIndex = index;
            RenderSelectedOffer();
            StaffOfferSelected?.Invoke(_model.Offers[index].OfferId);
        }

        private void RenderSelectedOffer()
        {
            for (int index = 0; index < _offerButtons.Count; index++)
                UIClubOfficeStyle.Select(_offerButtons[index], index == _selectedOfferIndex);
            if (_model == null || _selectedOfferIndex < 0 || _selectedOfferIndex >= _model.Offers.Count)
            {
                _selectedNameText.text = "현재 시장 제안 없음";
                _selectedDetailText.text = "시장 기간이 열리면 역할별 계약 후보를 비교할 수 있습니다.";
                _portrait.sprite = null;
                _portrait.color = CareerUiTheme.PortraitBackdrop;
                _signButton.interactable = false;
                _signStateText.text = "계약 가능한 제안이 없습니다.";
                return;
            }

            OwnerStaffMarketOfferModel offer = _model.Offers[_selectedOfferIndex];
            _selectedNameText.text = $"{offer.Name} · {offer.RoleText}";
            _selectedDetailText.text =
                $"{offer.QualityText}\n전문 분야  {offer.SpecialtyText}\n운영 철학  {offer.PhilosophyText}\n" +
                $"예상 효과  {offer.EffectText}\n{offer.SalaryText}\n{offer.TermText}\n{offer.SigningCostText}";
            _portrait.sprite = _portraitResolver?.Invoke(offer.PortraitAssetKey) ?? UIClubOfficeStyle.LoadArtwork(7);
            _portrait.color = _portrait.sprite == null ? CareerUiTheme.PortraitBackdrop : Color.white;
            _signButton.interactable = offer.CanSign;
            _signStateText.text = offer.CanSign ? "계약 조건 확인 완료" : offer.DisabledReason;
            _signStateText.color = offer.CanSign ? CareerUiTheme.Success : CareerUiTheme.Warning;
        }

        private void HandleSignRequested()
        {
            if (_model == null || _selectedOfferIndex < 0 || _selectedOfferIndex >= _model.Offers.Count) return;
            OwnerStaffMarketOfferModel offer = _model.Offers[_selectedOfferIndex];
            if (offer.CanSign) SignStaffRequested?.Invoke(offer.OfferId);
        }

        private void RenderCurrentStaff(IReadOnlyList<OwnerStaffSlotModel> slots)
        {
            OwnerRuntimeUiFactory.ClearChildren(_currentStaffList);
            if (slots.Count == 0)
            {
                Text empty = UIClubOfficeStyle.Label("Empty", _currentStaffList, "등록된 코칭스태프 정보가 없습니다.", 14);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            }
            for (int index = 0; index < slots.Count; index++)
            {
                OwnerStaffSlotModel slot = slots[index];
                Image card = UIClubOfficeStyle.Surface("Staff_" + slot.Role, _currentStaffList, UIClubOfficeStyle.Paper);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;
                Text title = UIClubOfficeStyle.Label("Name", card.transform, slot.RoleText + "  ·  " + slot.Name, 17, true);
                UIClubOfficeStyle.Place(title.rectTransform, .04f, .72f, .96f, .97f);
                Text terms = UIClubOfficeStyle.Label("Terms", card.transform,
                    slot.QualityText + "  ·  " + slot.SalaryText + "  ·  " + slot.TermText, 13);
                UIClubOfficeStyle.Place(terms.rectTransform, .04f, .51f, .96f, .72f);
                Text effect = UIClubOfficeStyle.Label("Effect", card.transform,
                    slot.SpecialtyText + " · " + slot.PhilosophyText + "\n" + slot.EffectText, 13);
                UIClubOfficeStyle.Place(effect.rectTransform, .04f, .04f, .96f, .51f);
            }
        }

        private static Text AddLine(Transform parent, int size, FontStyle style, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            OwnerDashboardStyle.SetDataText(text, style == FontStyle.Bold);
            return text;
        }
    }
}

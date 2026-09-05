using System;
using System.Collections.Generic;
using System.Text;
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
        private Text _currentStaffText;
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
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _portraitResolver = portraitResolver;
            bool ready = model.Snapshot.ContentState.Kind == UiContentStateKind.Ready;
            _contentStateText.gameObject.SetActive(!ready);
            _contentStateText.text = ready
                ? string.Empty
                : $"{model.Snapshot.ContentState.Title}\n{model.Snapshot.ContentState.Message}";
            _currentStaffText.text = BuildCurrentStaffText(model.Slots);
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
            RenderSelectedOffer();
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
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "WorkspaceColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns);

            OwnerWorkspaceUiFactory.Panel current = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "CurrentStaffPanel", "현재 5역할 슬롯", true);
            OwnerWorkspaceUiFactory.SetFlexible(current.Root, 1f);
            _currentStaffText = OwnerWorkspaceUiFactory.CreateText(current.Content, "CurrentStaffRows", string.Empty,
                14, FontStyle.Normal, TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            OwnerWorkspaceUiFactory.Stretch(_currentStaffText.rectTransform);

            OwnerWorkspaceUiFactory.Panel market = OwnerWorkspaceUiFactory.CreatePanel(
                columns, "StaffMarketPanel", "Staff Market");
            OwnerWorkspaceUiFactory.SetFlexible(market.Root, 1f);
            _marketList = OwnerWorkspaceUiFactory.CreateRoot(market.Content, "MarketList", false);
            OwnerWorkspaceUiFactory.AddVerticalLayout(_marketList, CareerUiTheme.Space2);

            _contentStateText = OwnerWorkspaceUiFactory.CreateText(_workspaceRoot, "ContentState", string.Empty,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.Stretch(_contentStateText.rectTransform);
            _contentStateText.gameObject.SetActive(false);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerStaffOfficeInspector", false);
            OwnerWorkspaceUiFactory.Panel detail = OwnerWorkspaceUiFactory.CreatePanel(
                _inspectorRoot, "StaffDetailPanel", "스태프 상세");
            OwnerWorkspaceUiFactory.Stretch(detail.Root);
            OwnerWorkspaceUiFactory.AddVerticalLayout(detail.Content, CareerUiTheme.Space3);
            RectTransform portraitRect = OwnerWorkspaceUiFactory.CreateRoot(detail.Content, "StaffPortrait", false);
            _portrait = portraitRect.gameObject.AddComponent<Image>();
            _portrait.color = CareerUiTheme.PortraitBackdrop;
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            portraitRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 190f;
            _selectedNameText = AddLine(detail.Content, 20, FontStyle.Bold, 38f);
            _selectedDetailText = AddLine(detail.Content, 14, FontStyle.Normal, 250f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionBarHost, "OwnerStaffOfficeActionBar", false);
            HorizontalLayoutGroup actionLayout = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space3);
            actionLayout.padding = new RectOffset(16, 16, 8, 8);
            _signStateText = OwnerWorkspaceUiFactory.CreateText(_actionRoot, "SignState", string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleRight, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_signStateText.rectTransform, 1f, 0f);
            _signButton = OwnerWorkspaceUiFactory.CreateButton(
                _actionRoot, "ConfirmStaffSigningButton", "스태프 계약", HandleSignRequested);
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
            _portrait.sprite = _portraitResolver?.Invoke(offer.PortraitAssetKey);
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

        private static string BuildCurrentStaffText(IReadOnlyList<OwnerStaffSlotModel> slots)
        {
            if (slots.Count == 0) return "정보 부족";
            var builder = new StringBuilder(slots.Count * 120);
            for (int index = 0; index < slots.Count; index++)
            {
                OwnerStaffSlotModel slot = slots[index];
                if (index > 0) builder.AppendLine().AppendLine();
                builder.Append(slot.RoleText).Append("  |  ").Append(slot.Name).Append("  |  ").Append(slot.QualityText)
                    .AppendLine().Append("전문 분야  ").Append(slot.SpecialtyText)
                    .Append("  ·  철학  ").Append(slot.PhilosophyText)
                    .AppendLine().Append(slot.EffectText).Append("  ·  ").Append(slot.SalaryText)
                    .Append("  ·  ").Append(slot.TermText);
            }
            return builder.ToString();
        }

        private static Text AddLine(Transform parent, int size, FontStyle style, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Value", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.TextPrimary);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }
    }
}

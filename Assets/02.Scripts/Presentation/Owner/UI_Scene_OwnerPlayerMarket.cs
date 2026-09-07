using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>선수 계약과 1:1 트레이드를 같은 구단 메뉴 문법으로 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerPlayerMarket : MonoBehaviour, IUiCancelHandler
    {
        private readonly List<Button> _leftButtons = new List<Button>();
        private readonly List<Text> _leftLabels = new List<Text>();
        private readonly List<Button> _rightButtons = new List<Button>();
        private readonly List<Text> _rightLabels = new List<Text>();
        private readonly List<Button> _termButtons = new List<Button>();
        private readonly List<PartnerOption> _partnerOptions = new List<PartnerOption>();
        private readonly List<Button> _partnerChoiceButtons = new List<Button>();
        private readonly List<Image> _partnerChoiceBadges = new List<Image>();
        private readonly List<Text> _partnerChoiceMonograms = new List<Text>();
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _leftList;
        private RectTransform _rightList;
        private Text _leftSummary;
        private Text _rightSummary;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _feedback;
        private Button _commitButton;
        private Button _batchButton;
        private Button _partnerButton;
        private Button _partnerConfirmButton;
        private Image _background;
        private Image _contractArtwork;
        private Text _contractOverview;
        private Text _leftTitle;
        private Text _rightTitle;
        private RectTransform _partnerSelectionOverlay;
        private RectTransform _partnerChoiceList;
        private Text _partnerSelectionSummary;
        private Text _partnerSelectionCount;
        private OwnerContractSnapshot _contract;
        private OwnerTradeSnapshot _trade;
        private bool _isTrade;
        private string _pendingPartnerTeamSeasonKey = string.Empty;

        private sealed class PartnerOption
        {
            public string TeamSeasonKey;
            public string TeamName;
            public int PlayerCount;
            public int MaximumValue;
            public int TotalValue;
        }

        public event Action<string, int> ContractPreviewRequested;
        public event Action<string, int> ContractRenewalRequested;
        public event Action<int> ContractBatchRenewalRequested;
        public event Action<string, string, string> TradePreviewRequested;
        public event Action<string, string, string> TradeRequested;

        public static UI_Scene_OwnerPlayerMarket CreateRuntime(
            RectTransform workspaceHost,
            RectTransform inspectorHost,
            RectTransform actionBarHost)
        {
            if (workspaceHost == null || inspectorHost == null || actionBarHost == null)
                throw new ArgumentNullException(nameof(workspaceHost));
            var view = new GameObject(nameof(UI_Scene_OwnerPlayerMarket)).AddComponent<UI_Scene_OwnerPlayerMarket>();
            view.Build(workspaceHost, inspectorHost, actionBarHost);
            return view;
        }

        public void BindContract(OwnerContractSnapshot snapshot)
        {
            _contract = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _trade = null;
            _isTrade = false;
            ClosePartnerSelection();
            SetBackground("UI/Generated/bg_owner_contract_v1");
            _leftSummary.text = $"만료 임박 우선\n보유 자금  {FormatMoney(snapshot.Money)}\n선수단 연봉  {FormatMoney(snapshot.AnnualSalaryTotal)}";
            _leftTitle.text = "선수 계약 현황";
            _rightTitle.text = "계약 협상";
            var batch = snapshot.BatchPreview;
            _rightSummary.text = batch == null ? "계약 기간과 재정 부담을 비교하세요." :
                $"만료 임박 {batch.Renewals.Count}명 · {snapshot.SelectedTerm}년 일괄 연장\n" +
                $"총계약금 {FormatMoney(batch.SigningCost)} · 변경 후 선수단 연봉 {FormatMoney(batch.AnnualSalaryTotal)}" +
                (batch.CanCommit ? string.Empty : $"\n{batch.Reason}");
            _batchButton.gameObject.SetActive(true);
            _batchButton.interactable = batch?.CanCommit == true;
            _batchButton.GetComponentInChildren<Text>().text = $"만료 임박 {batch?.Renewals.Count ?? 0}명 연장";
            SetContractOverviewVisible(true);
            EnsureButtons(_leftButtons, _leftLabels, _leftList, snapshot.Players.Count, SelectContract);
            SetButtonsActive(_rightButtons, 0);
            for (int index = 0; index < _leftButtons.Count; index++)
            {
                bool active = index < snapshot.Players.Count;
                _leftButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerContractPlayerRow row = snapshot.Players[index];
                _leftLabels[index].text = $"{row.Name}  ·  {row.Role}  ·  비용 {row.Cost}\n잔여 {row.RemainingSeasons}년   /   연봉 {FormatMoney(row.AnnualSalary)}";
                UIClubOfficeStyle.Select(_leftButtons[index], row.CardId == snapshot.SelectedCardId);
            }
            _partnerButton.gameObject.SetActive(false);
            for (int index = 0; index < _termButtons.Count; index++)
            {
                _termButtons[index].gameObject.SetActive(true);
                _termButtons[index].interactable = snapshot.SelectedTerm != index + 1;
                UIClubOfficeStyle.Select(_termButtons[index], snapshot.SelectedTerm == index + 1);
            }
            _commitButton.GetComponentInChildren<Text>().text = "선택 선수 연장";
            RenderContractDetail();
        }

        public void BindTrade(OwnerTradeSnapshot snapshot)
        {
            _trade = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _contract = null;
            _isTrade = true;
            _batchButton.gameObject.SetActive(false);
            ClosePartnerSelection();
            SetBackground("UI/Generated/bg_owner_trade_v1");
            _leftTitle.text = "우리 구단 · 제안 선수";
            _rightTitle.text = "상대 구단 · 영입 선수";
            SetContractOverviewVisible(false);
            _leftSummary.text = $"우리 제안\n이번 시즌 {snapshot.TradesUsed}/{snapshot.TradeLimit}회 사용";
            _rightSummary.text = $"상대 제안\n{ResolvePartnerName(snapshot)}";
            EnsureButtons(_leftButtons, _leftLabels, _leftList, snapshot.OwnedPlayers.Count, SelectOutgoing);
            for (int index = 0; index < _leftButtons.Count; index++)
            {
                bool active = index < snapshot.OwnedPlayers.Count;
                _leftButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerTradePlayerRow row = snapshot.OwnedPlayers[index];
                _leftLabels[index].text = $"{row.Name}  ·  {row.Role}\n비용 {row.Cost}   /   트레이드 가치 {row.Value}";
                UIClubOfficeStyle.Select(_leftButtons[index], row.CardId == snapshot.SelectedOutgoingCardId);
            }
            int targetCount = 0;
            for (int index = 0; index < snapshot.TargetPlayers.Count; index++)
                if (string.Equals(snapshot.TargetPlayers[index].TeamSeasonKey, snapshot.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal))
                    targetCount++;
            EnsureButtons(_rightButtons, _rightLabels, _rightList, targetCount, SelectIncoming);
            int visibleIndex = 0;
            for (int index = 0; index < snapshot.TargetPlayers.Count; index++)
            {
                OwnerTradePlayerRow row = snapshot.TargetPlayers[index];
                if (!string.Equals(row.TeamSeasonKey, snapshot.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal)) continue;
                _rightLabels[visibleIndex].text = $"{row.Name}  ·  {row.Role}\n비용 {row.Cost}   /   트레이드 가치 {row.Value}";
                UIClubOfficeStyle.Select(_rightButtons[visibleIndex], row.CardId == snapshot.SelectedIncomingCardId);
                _rightButtons[visibleIndex].gameObject.SetActive(true);
                visibleIndex++;
            }
            SetButtonsActiveFrom(_rightButtons, visibleIndex);
            _partnerButton.gameObject.SetActive(true);
            _partnerButton.GetComponentInChildren<Text>().text = "상대 구단 변경";
            for (int index = 0; index < _termButtons.Count; index++) _termButtons[index].gameObject.SetActive(false);
            _commitButton.GetComponentInChildren<Text>().text = "트레이드 제안";
            RenderTradeDetail();
        }

        public void SetVisible(bool visible)
        {
            if (!visible) ClosePartnerSelection();
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
        }

        /// <summary>트레이드 상대 구단 선택 Popup이 열려 있으면 선택을 확정하지 않고 닫는다.</summary>
        public bool TryHandleCancel()
        {
            if (_partnerSelectionOverlay == null || !_partnerSelectionOverlay.gameObject.activeSelf)
                return false;
            ClosePartnerSelection();
            return true;
        }

        public void SetFeedback(string message, bool isError)
        {
            _feedback.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _feedback.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        private void Build(RectTransform workspaceHost, RectTransform inspectorHost, RectTransform actionHost)
        {
            _workspaceRoot = OwnerWorkspaceUiFactory.CreateRoot(workspaceHost, "OwnerPlayerMarketWorkspace", false);
            _background = _workspaceRoot.gameObject.AddComponent<Image>();
            _background.raycastTarget = false;
            _background.preserveAspect = false;
            OwnerRuntimeUiFactory.Stretch(UIClubOfficeStyle.Surface("OfficePaper", _workspaceRoot, UIClubOfficeStyle.Paper).rectTransform);
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "MarketColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns, CareerUiTheme.Space3);

            OwnerWorkspaceUiFactory.Panel left = UIClubOfficeStyle.CreatePanel(columns, "LeftOfferPanel", "우리 구단", true);
            _leftTitle = left.Root.Find("HeaderSurface/HeaderSlot").GetComponent<Text>();
            OwnerWorkspaceUiFactory.SetFlexible(left.Root, 1f);
            _leftSummary = CreateHeader(left.Content);
            _leftList = CreateScrollList(left.Content, "LeftList", 74f);

            OwnerWorkspaceUiFactory.Panel right = UIClubOfficeStyle.CreatePanel(columns, "RightOfferPanel", "비교 대상");
            _rightTitle = right.Root.Find("HeaderSurface/HeaderSlot").GetComponent<Text>();
            OwnerWorkspaceUiFactory.SetFlexible(right.Root, 1f);
            _rightSummary = CreateHeader(right.Content);
            _rightList = CreateScrollList(right.Content, "RightList", 74f);
            _contractArtwork = UIClubOfficeStyle.Illustration(right.Content, "NegotiationArtwork", 8);
            UIClubOfficeStyle.Place(_contractArtwork.rectTransform, 0f, .56f, 1f, .86f);
            _contractOverview = UIClubOfficeStyle.Label("ContractOverview", right.Content, string.Empty, 16);
            UIClubOfficeStyle.Place(_contractOverview.rectTransform, .04f, .02f, .96f, .52f);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerPlayerMarketInspector", false);
            OwnerWorkspaceUiFactory.Panel detail = UIClubOfficeStyle.CreatePanel(_inspectorRoot, "MarketDecisionPanel", "선택과 계약 조건");
            OwnerWorkspaceUiFactory.Stretch(detail.Root);
            ScrollRect details = OwnerRuntimeUiFactory.CreateVerticalScroll("DecisionScroll", detail.Content, out RectTransform detailContent);
            OwnerRuntimeUiFactory.Stretch(details.GetComponent<RectTransform>());
            Image art = UIClubOfficeStyle.Illustration(detailContent, "OfficeArtwork", 8);
            art.gameObject.AddComponent<LayoutElement>().preferredHeight = 140f;
            _detailTitle = AddLine(detailContent, 20, FontStyle.Bold, 68f);
            _detailBody = AddLine(detailContent, 14, FontStyle.Normal, 370f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionHost, "OwnerPlayerMarketActionBar", false);
            Image actionPaper = _actionRoot.gameObject.AddComponent<Image>();
            actionPaper.color = UIClubOfficeStyle.Paper;
            _actionRoot.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            HorizontalLayoutGroup action = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space2);
            action.padding = new RectOffset(16, 16, 4, 4);
            _feedback = OwnerWorkspaceUiFactory.CreateText(_actionRoot, "Feedback", string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_feedback.rectTransform, 1f, 0f);
            _feedback.GetComponent<LayoutElement>().minHeight = 0f;
            _partnerButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "CyclePartner", "상대 구단 변경", OpenPartnerSelection);
            UIClubOfficeStyle.SizeAction(_partnerButton, 150f);
            for (int term = 1; term <= 3; term++)
            {
                int captured = term;
                Button button = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, $"Term{term}", $"+{term}년", () => SelectTerm(captured));
                _termButtons.Add(button);
                UIClubOfficeStyle.SizeAction(button, 62f);
            }
            _commitButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "Commit", "확정", Commit);
            UIClubOfficeStyle.SizeAction(_commitButton, 140f, true);
            _batchButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "RenewExpiring", "만료 임박 일괄 연장", () =>
            {
                if (_contract?.BatchPreview?.CanCommit == true)
                    ContractBatchRenewalRequested?.Invoke(_contract.SelectedTerm);
            });
            UIClubOfficeStyle.SizeAction(_batchButton, 190f, true);
            BuildPartnerSelectionPopup();
            CareerUiSkin.Apply(_workspaceRoot);
            CareerUiSkin.Apply(_inspectorRoot);
            CareerUiSkin.Apply(_actionRoot);
        }

        private void SelectContract(int index)
        {
            if (_contract == null || index >= _contract.Players.Count) return;
            ContractPreviewRequested?.Invoke(_contract.Players[index].CardId, _contract.SelectedTerm);
        }

        private void SelectTerm(int term)
        {
            if (_contract == null) return;
            ContractPreviewRequested?.Invoke(_contract.SelectedCardId, term);
        }

        private void SelectOutgoing(int index)
        {
            if (_trade == null || index >= _trade.OwnedPlayers.Count) return;
            TradePreviewRequested?.Invoke(
                _trade.SelectedPartnerTeamSeasonKey,
                _trade.OwnedPlayers[index].CardId,
                _trade.SelectedIncomingCardId);
        }

        private void SelectIncoming(int visibleIndex)
        {
            if (_trade == null) return;
            int current = 0;
            for (int index = 0; index < _trade.TargetPlayers.Count; index++)
            {
                OwnerTradePlayerRow row = _trade.TargetPlayers[index];
                if (!string.Equals(row.TeamSeasonKey, _trade.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal)) continue;
                if (current++ != visibleIndex) continue;
                TradePreviewRequested?.Invoke(row.TeamSeasonKey, _trade.SelectedOutgoingCardId, row.CardId);
                return;
            }
        }

        private void BuildPartnerSelectionPopup()
        {
            Image shade = OwnerRuntimeUiFactory.CreateImage(
                "PartnerSelectionOverlay", _workspaceRoot, CareerUiTheme.InputBlocker);
            shade.raycastTarget = true;
            shade.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            _partnerSelectionOverlay = shade.rectTransform;
            OwnerRuntimeUiFactory.Stretch(_partnerSelectionOverlay);

            Image dialog = UIClubOfficeStyle.Surface(
                "PartnerSelectionDialog", _partnerSelectionOverlay, UIClubOfficeStyle.Paper);
            dialog.raycastTarget = true;
            OwnerRuntimeUiFactory.SetAnchors(dialog.rectTransform, new Vector2(.16f, .07f), new Vector2(.84f, .93f),
                Vector2.zero, Vector2.zero);
            var outline = dialog.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(48, 57, 68, 255);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            Image header = UIClubOfficeStyle.Surface("Header", dialog.transform, UIClubOfficeStyle.Blue);
            OwnerRuntimeUiFactory.SetAnchors(header.rectTransform, new Vector2(0f, .90f), Vector2.one,
                Vector2.zero, Vector2.zero);
            Text title = OwnerWorkspaceUiFactory.CreateText(header.transform, "Title", "트레이드 상대 구단 선택", 21,
                FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            OwnerRuntimeUiFactory.Stretch(title.rectTransform, new Vector2(22f, 0f), new Vector2(-70f, 0f));
            title.gameObject.AddComponent<CareerUiPreserveTextColor>();
            Button close = OwnerWorkspaceUiFactory.CreateButton(header.transform, "Close", "×", ClosePartnerSelection);
            OwnerRuntimeUiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(.925f, .17f),
                new Vector2(.985f, .83f), Vector2.zero, Vector2.zero);

            Image summary = UIClubOfficeStyle.Surface("SelectionSummary", dialog.transform, Color.white);
            OwnerRuntimeUiFactory.SetAnchors(summary.rectTransform, new Vector2(.025f, .76f), new Vector2(.975f, .88f),
                Vector2.zero, Vector2.zero);
            Image summaryAccent = UIClubOfficeStyle.Surface("Accent", summary.transform, UIClubOfficeStyle.Blue);
            OwnerRuntimeUiFactory.SetAnchors(summaryAccent.rectTransform, Vector2.zero, new Vector2(.006f, 1f),
                Vector2.zero, Vector2.zero);
            _partnerSelectionSummary = OwnerWorkspaceUiFactory.CreateText(summary.transform, "CurrentSelection", string.Empty,
                16, FontStyle.Bold, TextAnchor.MiddleLeft, UIClubOfficeStyle.Ink);
            OwnerRuntimeUiFactory.Stretch(_partnerSelectionSummary.rectTransform, new Vector2(22f, 5f), new Vector2(-18f, -5f));

            ScrollRect partnerScroll = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "PartnerScroll", dialog.transform, out _partnerChoiceList);
            OwnerRuntimeUiFactory.SetAnchors(partnerScroll.GetComponent<RectTransform>(), new Vector2(.025f, .17f),
                new Vector2(.975f, .74f), Vector2.zero, Vector2.zero);

            _partnerSelectionCount = OwnerWorkspaceUiFactory.CreateText(dialog.transform, "PartnerCount", string.Empty, 13,
                FontStyle.Normal, TextAnchor.MiddleLeft, UIClubOfficeStyle.Muted);
            OwnerRuntimeUiFactory.SetAnchors(_partnerSelectionCount.rectTransform, new Vector2(.035f, .035f),
                new Vector2(.49f, .14f), Vector2.zero, Vector2.zero);
            Button cancel = OwnerWorkspaceUiFactory.CreateButton(dialog.transform, "Cancel", "취소", ClosePartnerSelection);
            OwnerRuntimeUiFactory.SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(.56f, .045f),
                new Vector2(.72f, .13f), Vector2.zero, Vector2.zero);
            UIClubOfficeStyle.SizeAction(cancel, 120f);
            _partnerConfirmButton = OwnerWorkspaceUiFactory.CreateButton(
                dialog.transform, "Confirm", "이 구단 선택", ConfirmPartnerSelection);
            OwnerRuntimeUiFactory.SetAnchors(_partnerConfirmButton.GetComponent<RectTransform>(), new Vector2(.735f, .045f),
                new Vector2(.965f, .13f), Vector2.zero, Vector2.zero);
            UIClubOfficeStyle.SizeAction(_partnerConfirmButton, 170f, true);
            _partnerSelectionOverlay.gameObject.SetActive(false);
        }

        private void OpenPartnerSelection()
        {
            if (_trade == null) return;
            BuildPartnerOptions();
            _pendingPartnerTeamSeasonKey = _trade.SelectedPartnerTeamSeasonKey;
            RefreshPartnerChoices();
            _partnerSelectionOverlay.gameObject.SetActive(true);
            _partnerSelectionOverlay.SetAsLastSibling();
            _partnerButton.interactable = false;
            _commitButton.interactable = false;
        }

        private void BuildPartnerOptions()
        {
            _partnerOptions.Clear();
            for (int index = 0; index < _trade.TargetPlayers.Count; index++)
            {
                OwnerTradePlayerRow player = _trade.TargetPlayers[index];
                PartnerOption option = FindPartnerOption(player.TeamSeasonKey);
                if (option == null)
                {
                    option = new PartnerOption
                    {
                        TeamSeasonKey = player.TeamSeasonKey,
                        TeamName = player.TeamName
                    };
                    _partnerOptions.Add(option);
                }
                option.PlayerCount++;
                option.TotalValue += player.Value;
                if (player.Value > option.MaximumValue) option.MaximumValue = player.Value;
            }
        }

        private PartnerOption FindPartnerOption(string teamSeasonKey)
        {
            for (int index = 0; index < _partnerOptions.Count; index++)
                if (string.Equals(_partnerOptions[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return _partnerOptions[index];
            return null;
        }

        private void RefreshPartnerChoices()
        {
            EnsurePartnerChoiceButtons();
            for (int index = 0; index < _partnerChoiceButtons.Count; index++)
            {
                bool active = index < _partnerOptions.Count;
                _partnerChoiceButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                PartnerOption option = _partnerOptions[index];
                int averageValue = option.PlayerCount == 0 ? 0 : option.TotalValue / option.PlayerCount;
                Text label = _partnerChoiceButtons[index].GetComponentInChildren<Text>();
                label.text = $"{option.TeamName}\n영입 후보 {option.PlayerCount}명   ·   최고 가치 {option.MaximumValue}   ·   평균 가치 {averageValue}";
                bool selected = string.Equals(option.TeamSeasonKey, _pendingPartnerTeamSeasonKey, StringComparison.Ordinal);
                UIClubOfficeStyle.Select(_partnerChoiceButtons[index], selected);
                label.rectTransform.offsetMin = new Vector2(78f, 5f);
                _partnerChoiceBadges[index].color = selected ? UIClubOfficeStyle.Blue : new Color32(224, 234, 245, 255);
                _partnerChoiceMonograms[index].color = selected ? Color.white : UIClubOfficeStyle.Blue;
                _partnerChoiceMonograms[index].text = CreateTeamMonogram(option.TeamName);
            }
            PartnerOption pending = FindPartnerOption(_pendingPartnerTeamSeasonKey);
            _partnerSelectionSummary.text = pending == null
                ? "선택한 구단이 없습니다. 협상을 시작할 상대를 선택하세요."
                : $"선택한 상대   {pending.TeamName}     ·     영입 후보 {pending.PlayerCount}명";
            _partnerSelectionCount.text = $"트레이드 가능 상대 {_partnerOptions.Count}개 구단 · 구단을 고른 뒤 선택을 확정하세요.";
            _partnerConfirmButton.interactable = pending != null;
        }

        private void EnsurePartnerChoiceButtons()
        {
            while (_partnerChoiceButtons.Count < _partnerOptions.Count)
            {
                int captured = _partnerChoiceButtons.Count;
                Button button = OwnerWorkspaceUiFactory.CreateButton(
                    _partnerChoiceList, $"Partner{captured}", string.Empty, () => SelectPartner(captured));
                LayoutElement layout = button.GetComponent<LayoutElement>();
                layout.minHeight = 68f;
                layout.preferredHeight = 68f;
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                Image badge = OwnerRuntimeUiFactory.CreateImage("TeamBadge", button.transform, new Color32(224, 234, 245, 255));
                badge.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                OwnerRuntimeUiFactory.SetAnchors(badge.rectTransform, new Vector2(0f, .10f), new Vector2(0f, .90f),
                    new Vector2(18f, 0f), new Vector2(66f, 0f));
                Text monogram = OwnerWorkspaceUiFactory.CreateText(button.transform, "Monogram", string.Empty, 18,
                    FontStyle.Bold, TextAnchor.MiddleCenter, UIClubOfficeStyle.Blue);
                monogram.gameObject.AddComponent<CareerUiPreserveTextColor>();
                OwnerRuntimeUiFactory.SetAnchors(monogram.rectTransform, new Vector2(0f, .10f), new Vector2(0f, .90f),
                    new Vector2(18f, 0f), new Vector2(66f, 0f));
                _partnerChoiceButtons.Add(button);
                _partnerChoiceBadges.Add(badge);
                _partnerChoiceMonograms.Add(monogram);
            }
        }

        private void SelectPartner(int index)
        {
            if (index < 0 || index >= _partnerOptions.Count) return;
            _pendingPartnerTeamSeasonKey = _partnerOptions[index].TeamSeasonKey;
            RefreshPartnerChoices();
        }

        private void ConfirmPartnerSelection()
        {
            if (_trade == null || FindPartnerOption(_pendingPartnerTeamSeasonKey) == null) return;
            string selected = _pendingPartnerTeamSeasonKey;
            bool changed = !string.Equals(selected, _trade.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal);
            ClosePartnerSelection();
            if (changed)
                TradePreviewRequested?.Invoke(selected, _trade.SelectedOutgoingCardId, string.Empty);
        }

        private void ClosePartnerSelection()
        {
            if (_partnerSelectionOverlay != null) _partnerSelectionOverlay.gameObject.SetActive(false);
            if (_partnerButton != null) _partnerButton.interactable = true;
            if (_commitButton != null)
                _commitButton.interactable = _isTrade
                    ? _trade?.Preview?.CanCommit == true
                    : _contract?.Preview?.CanCommit == true;
        }

        private static string CreateTeamMonogram(string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return "?";
            string trimmed = teamName.Trim();
            int space = trimmed.LastIndexOf(' ');
            string nickname = space >= 0 && space < trimmed.Length - 1 ? trimmed.Substring(space + 1) : trimmed;
            return nickname.Substring(0, 1);
        }

        private void Commit()
        {
            if (_isTrade)
            {
                if (_trade?.Preview?.CanCommit == true)
                    TradeRequested?.Invoke(_trade.SelectedPartnerTeamSeasonKey, _trade.SelectedOutgoingCardId, _trade.SelectedIncomingCardId);
                return;
            }
            if (_contract?.Preview?.CanCommit == true)
                ContractRenewalRequested?.Invoke(_contract.SelectedCardId, _contract.SelectedTerm);
        }

        private void RenderContractDetail()
        {
            OwnerContractRenewalPreview preview = _contract.Preview;
            OwnerContractPlayerRow selected = null;
            for (int index = 0; index < _contract.Players.Count; index++)
                if (string.Equals(_contract.Players[index].CardId, _contract.SelectedCardId, StringComparison.Ordinal))
                    selected = _contract.Players[index];
            _detailTitle.text = selected == null ? "계약 선수를 선택하세요" : $"{selected.Name} · {selected.Role}";
            _detailBody.text = preview == null || selected == null
                ? "계약 목록이 비어 있습니다."
                : $"현재 계약  {selected.RemainingSeasons}년 / {FormatMoney(selected.AnnualSalary)}\n" +
                  $"연장 후  {selected.RemainingSeasons + preview.Seasons}년 / 연 {FormatMoney(preview.AnnualSalary)}\n" +
                  $"즉시 계약금  {FormatMoney(preview.SigningCost)}\n\n{preview.Reason}\n\n" +
                  "동일 선수의 중복·연도·등급 카드는 계약을 공유합니다. 카드 교체 시 잔여 기간과 연봉을 유지합니다.";
            _contractOverview.text = selected == null
                ? "왼쪽 목록에서 갱신할 선수를 선택하세요.\n\n계약 기간을 선택하면 연봉과 즉시 계약금을 확인할 수 있습니다."
                : $"{selected.Name} · {selected.Role}\n\n현재 잔여 계약  {selected.RemainingSeasons}년\n현재 연봉  {FormatMoney(selected.AnnualSalary)}\n\n" +
                  $"연장 기간  +{_contract.SelectedTerm}년 → 잔여 {selected.RemainingSeasons + _contract.SelectedTerm}년\n" +
                  (preview == null ? "조건을 확인하고 있습니다." : $"제안 연봉  {FormatMoney(preview.AnnualSalary)}\n즉시 계약금  {FormatMoney(preview.SigningCost)}");
            _commitButton.interactable = preview?.CanCommit == true;
            _feedback.text = preview?.Reason ?? "계약 정보를 준비하고 있습니다.";
            _feedback.color = preview?.CanCommit == true ? CareerUiTheme.Success : CareerUiTheme.Warning;
        }

        private void RenderTradeDetail()
        {
            OwnerTradePreview preview = _trade.Preview;
            _detailTitle.text = "트레이드 가치 비교";
            _detailBody.text = preview == null
                ? "양 구단에서 교환할 선수를 선택하세요."
                : $"우리 제안 가치  {preview.OutgoingValue}\n상대 제안 가치  {preview.IncomingValue}\n" +
                  $"가치 차이  {preview.ValueDifference:+#;-#;0}\n\n{preview.Reason}\n\n" +
                  "확정 전에 25인 역할 구성, 외국인 제한, 동일 인물 중복을 다시 검증합니다.";
            _commitButton.interactable = preview?.CanCommit == true;
            _feedback.text = preview?.Reason ?? "트레이드 조건을 선택하세요.";
            _feedback.color = preview?.CanCommit == true ? CareerUiTheme.Success : CareerUiTheme.Warning;
        }

        private static RectTransform CreateScrollList(Transform parent, string name, float top)
        {
            RectTransform viewport = OwnerWorkspaceUiFactory.CreateRoot(parent, name + "Viewport", false);
            Image hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = new Color(1f, 1f, 1f, .01f);
            viewport.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0f, -top);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = OwnerWorkspaceUiFactory.CreateRoot(viewport, name + "Content", false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            OwnerWorkspaceUiFactory.AddVerticalLayout(content, 4f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return content;
        }

        private static Text CreateHeader(Transform parent)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Summary", string.Empty, 14, FontStyle.Bold,
                TextAnchor.UpperLeft, CareerUiTheme.ReferenceText);
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(0f, -68f);
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private static Text AddLine(Transform parent, int size, FontStyle style, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, "Line", string.Empty, size, style,
                TextAnchor.UpperLeft, CareerUiTheme.ReferenceText);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static void EnsureButtons(
            List<Button> buttons,
            List<Text> labels,
            Transform parent,
            int count,
            Action<int> callback)
        {
            while (buttons.Count < count)
            {
                int captured = buttons.Count;
                Button button = OwnerWorkspaceUiFactory.CreateButton(parent, $"Row{captured}", string.Empty, () => callback(captured));
                LayoutElement layout = button.GetComponent<LayoutElement>();
                layout.preferredHeight = 68f;
                layout.minHeight = 68f;
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UIClubOfficeStyle.Select(button, false);
                labels.Add(label);
                buttons.Add(button);
            }
        }

        private static void SetButtonsActive(List<Button> buttons, int count)
        {
            for (int index = 0; index < buttons.Count; index++) buttons[index].gameObject.SetActive(index < count);
        }

        private static void SetButtonsActiveFrom(List<Button> buttons, int firstInactive)
        {
            for (int index = firstInactive; index < buttons.Count; index++) buttons[index].gameObject.SetActive(false);
        }

        private static string ResolvePartnerName(OwnerTradeSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.TargetPlayers.Count; index++)
                if (string.Equals(snapshot.TargetPlayers[index].TeamSeasonKey, snapshot.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal))
                    return snapshot.TargetPlayers[index].TeamName;
            return "선택 없음";
        }

        private static string FormatMoney(long value) => OwnerMoneyFormatter.Format(value);

        private void SetContractOverviewVisible(bool visible)
        {
            _contractArtwork.gameObject.SetActive(visible);
            _contractOverview.gameObject.SetActive(visible);
            _rightList.parent.gameObject.SetActive(!visible);
        }

        private void SetBackground(string resourcePath)
        {
            if (_background == null) return;
            _background.sprite = Resources.Load<Sprite>(resourcePath);
            _background.color = _background.sprite == null ? CareerUiTheme.ReferenceCanvas : new Color(1f, 1f, 1f, 0.34f);
        }

        private void OnDestroy()
        {
            for (int index = 0; index < _leftButtons.Count; index++) _leftButtons[index].onClick.RemoveAllListeners();
            for (int index = 0; index < _rightButtons.Count; index++) _rightButtons[index].onClick.RemoveAllListeners();
            for (int index = 0; index < _termButtons.Count; index++) _termButtons[index].onClick.RemoveAllListeners();
            for (int index = 0; index < _partnerChoiceButtons.Count; index++) _partnerChoiceButtons[index].onClick.RemoveAllListeners();
            _partnerButton?.onClick.RemoveAllListeners();
            _partnerConfirmButton?.onClick.RemoveAllListeners();
            _commitButton?.onClick.RemoveAllListeners();
            _batchButton?.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }
    }
}

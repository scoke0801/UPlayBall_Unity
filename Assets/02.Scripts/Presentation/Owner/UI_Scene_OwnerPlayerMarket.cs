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
    public sealed class UI_Scene_OwnerPlayerMarket : MonoBehaviour
    {
        private readonly List<Button> _leftButtons = new List<Button>();
        private readonly List<Text> _leftLabels = new List<Text>();
        private readonly List<Button> _rightButtons = new List<Button>();
        private readonly List<Text> _rightLabels = new List<Text>();
        private readonly List<Button> _termButtons = new List<Button>();
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
        private Button _partnerButton;
        private Image _background;
        private OwnerContractSnapshot _contract;
        private OwnerTradeSnapshot _trade;
        private bool _isTrade;

        public event Action<string, int> ContractPreviewRequested;
        public event Action<string, int> ContractRenewalRequested;
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
            SetBackground("UI/Generated/bg_owner_contract_v1");
            _leftSummary.text = $"만료 임박 우선\n보유 자금  {FormatMoney(snapshot.Money)}\n선수단 연봉  {FormatMoney(snapshot.AnnualSalaryTotal)}";
            _rightSummary.text = "선수  |  역할  |  비용  |  잔여  |  연봉";
            EnsureButtons(_leftButtons, _leftLabels, _leftList, snapshot.Players.Count, SelectContract);
            SetButtonsActive(_rightButtons, 0);
            for (int index = 0; index < _leftButtons.Count; index++)
            {
                bool active = index < snapshot.Players.Count;
                _leftButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerContractPlayerRow row = snapshot.Players[index];
                _leftLabels[index].text = $"{row.Name}  |  {row.Role}  |  비용 {row.Cost}  |  {row.RemainingSeasons}년  |  {FormatMoney(row.AnnualSalary)}";
            }
            _partnerButton.gameObject.SetActive(false);
            for (int index = 0; index < _termButtons.Count; index++)
            {
                _termButtons[index].gameObject.SetActive(true);
                _termButtons[index].interactable = snapshot.SelectedTerm != index + 1;
            }
            _commitButton.GetComponentInChildren<Text>().text = "계약 갱신";
            RenderContractDetail();
        }

        public void BindTrade(OwnerTradeSnapshot snapshot)
        {
            _trade = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _contract = null;
            _isTrade = true;
            SetBackground("UI/Generated/bg_owner_trade_v1");
            _leftSummary.text = $"우리 제안\n이번 시즌 {snapshot.TradesUsed}/{snapshot.TradeLimit}회 사용";
            _rightSummary.text = $"상대 제안\n{ResolvePartnerName(snapshot)}";
            EnsureButtons(_leftButtons, _leftLabels, _leftList, snapshot.OwnedPlayers.Count, SelectOutgoing);
            for (int index = 0; index < _leftButtons.Count; index++)
            {
                bool active = index < snapshot.OwnedPlayers.Count;
                _leftButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerTradePlayerRow row = snapshot.OwnedPlayers[index];
                _leftLabels[index].text = $"{row.Name}  |  {row.Role}  |  비용 {row.Cost}  |  가치 {row.Value}";
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
                _rightLabels[visibleIndex].text = $"{row.Name}  |  {row.Role}  |  비용 {row.Cost}  |  가치 {row.Value}";
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
            if (_workspaceRoot != null) _workspaceRoot.gameObject.SetActive(visible);
            if (_inspectorRoot != null) _inspectorRoot.gameObject.SetActive(visible);
            if (_actionRoot != null) _actionRoot.gameObject.SetActive(visible);
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
            RectTransform columns = OwnerWorkspaceUiFactory.CreateRoot(_workspaceRoot, "MarketColumns", false);
            columns.offsetMin = new Vector2(CareerUiTheme.Space4, CareerUiTheme.Space4);
            columns.offsetMax = new Vector2(-CareerUiTheme.Space4, -CareerUiTheme.Space4);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(columns, CareerUiTheme.Space3);

            OwnerWorkspaceUiFactory.Panel left = OwnerWorkspaceUiFactory.CreatePanel(columns, "LeftOfferPanel", "우리 구단", true);
            OwnerWorkspaceUiFactory.SetFlexible(left.Root, 1f);
            _leftSummary = CreateHeader(left.Content);
            _leftList = CreateScrollList(left.Content, "LeftList", 74f);

            OwnerWorkspaceUiFactory.Panel right = OwnerWorkspaceUiFactory.CreatePanel(columns, "RightOfferPanel", "비교 대상");
            OwnerWorkspaceUiFactory.SetFlexible(right.Root, 1f);
            _rightSummary = CreateHeader(right.Content);
            _rightList = CreateScrollList(right.Content, "RightList", 74f);

            _inspectorRoot = OwnerWorkspaceUiFactory.CreateRoot(inspectorHost, "OwnerPlayerMarketInspector", false);
            OwnerWorkspaceUiFactory.Panel detail = OwnerWorkspaceUiFactory.CreatePanel(_inspectorRoot, "MarketDecisionPanel", "의사결정 근거");
            OwnerWorkspaceUiFactory.Stretch(detail.Root);
            OwnerWorkspaceUiFactory.AddVerticalLayout(detail.Content, CareerUiTheme.Space3);
            _detailTitle = AddLine(detail.Content, 20, FontStyle.Bold, 58f);
            _detailBody = AddLine(detail.Content, 14, FontStyle.Normal, 320f);

            _actionRoot = OwnerWorkspaceUiFactory.CreateRoot(actionHost, "OwnerPlayerMarketActionBar", false);
            HorizontalLayoutGroup action = OwnerWorkspaceUiFactory.AddHorizontalLayout(_actionRoot, CareerUiTheme.Space2);
            action.padding = new RectOffset(16, 16, 4, 4);
            _feedback = OwnerWorkspaceUiFactory.CreateText(_actionRoot, "Feedback", string.Empty, 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerWorkspaceUiFactory.SetFlexible(_feedback.rectTransform, 1f, 0f);
            _partnerButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "CyclePartner", "상대 구단 변경", CyclePartner);
            for (int term = 1; term <= 3; term++)
            {
                int captured = term;
                Button button = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, $"Term{term}", $"{term}년", () => SelectTerm(captured));
                _termButtons.Add(button);
            }
            _commitButton = OwnerWorkspaceUiFactory.CreateButton(_actionRoot, "Commit", "확정", Commit);
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

        private void CyclePartner()
        {
            if (_trade == null) return;
            string next = string.Empty;
            bool takeNext = false;
            for (int index = 0; index < _trade.TargetPlayers.Count; index++)
            {
                string team = _trade.TargetPlayers[index].TeamSeasonKey;
                if (takeNext && !string.Equals(team, _trade.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal))
                {
                    next = team;
                    break;
                }
                if (string.Equals(team, _trade.SelectedPartnerTeamSeasonKey, StringComparison.Ordinal)) takeNext = true;
            }
            if (string.IsNullOrEmpty(next) && _trade.TargetPlayers.Count > 0) next = _trade.TargetPlayers[0].TeamSeasonKey;
            TradePreviewRequested?.Invoke(next, _trade.SelectedOutgoingCardId, string.Empty);
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
            _detailBody.text = preview == null
                ? "계약 목록이 비어 있습니다."
                : $"현재 계약  {selected.RemainingSeasons}년 / {FormatMoney(selected.AnnualSalary)}\n" +
                  $"갱신안  {preview.Seasons}년 / 연 {FormatMoney(preview.AnnualSalary)}\n" +
                  $"즉시 계약금  {FormatMoney(preview.SigningCost)}\n\n{preview.Reason}\n\n" +
                  "장기 계약은 연봉을 조금 낮추지만, 향후 재정 유연성을 줄입니다.";
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
                layout.preferredHeight = 38f;
                layout.minHeight = 38f;
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
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
            _partnerButton?.onClick.RemoveAllListeners();
            _commitButton?.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }
    }
}

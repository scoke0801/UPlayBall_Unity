using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using Baseball.Simulation.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 모드 선수 계약 현황과 갱신 결정을 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerPlayerMarket : MonoBehaviour
    {
        private readonly List<Button> _leftButtons = new List<Button>();
        private readonly List<Text> _leftLabels = new List<Text>();
        private readonly List<Button> _termButtons = new List<Button>();
        private RectTransform _workspaceRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _actionRoot;
        private RectTransform _leftList;
        private Text _leftSummary;
        private Text _rightSummary;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _feedback;
        private Button _commitButton;
        private Button _batchButton;
        private Image _background;
        private Image _contractArtwork;
        private Text _contractOverview;
        private Text _leftTitle;
        private Text _rightTitle;
        private OwnerContractSnapshot _contract;

        public event Action<string, int> ContractPreviewRequested;
        public event Action<string, int> ContractRenewalRequested;
        public event Action<int> ContractBatchRenewalRequested;

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
            EnsureButtons(_leftButtons, _leftLabels, _leftList, snapshot.Players.Count, SelectContract);
            for (int index = 0; index < _leftButtons.Count; index++)
            {
                bool active = index < snapshot.Players.Count;
                _leftButtons[index].gameObject.SetActive(active);
                if (!active) continue;
                OwnerContractPlayerRow row = snapshot.Players[index];
                _leftLabels[index].text = $"{row.Name}  ·  {row.Role}  ·  비용 {row.Cost}\n잔여 {row.RemainingSeasons}년   /   연봉 {FormatMoney(row.AnnualSalary)}";
                UIClubOfficeStyle.Select(_leftButtons[index], row.CardId == snapshot.SelectedCardId);
            }
            for (int index = 0; index < _termButtons.Count; index++)
            {
                _termButtons[index].gameObject.SetActive(true);
                _termButtons[index].interactable = snapshot.SelectedTerm != index + 1;
                UIClubOfficeStyle.Select(_termButtons[index], snapshot.SelectedTerm == index + 1);
            }
            _commitButton.GetComponentInChildren<Text>().text = "선택 선수 연장";
            RenderContractDetail();
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

        private void Commit()
        {
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
            for (int index = 0; index < _termButtons.Count; index++) _termButtons[index].onClick.RemoveAllListeners();
            _commitButton?.onClick.RemoveAllListeners();
            _batchButton?.onClick.RemoveAllListeners();
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_workspaceRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_inspectorRoot);
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_actionRoot);
        }
    }
}

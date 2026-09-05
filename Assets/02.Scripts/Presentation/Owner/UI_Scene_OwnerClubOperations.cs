using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>SharedGameShell Workspace에서 구장·시설·팬·재무를 함께 보여주는 구단 경영 uGUI 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerClubOperations : MonoBehaviour
    {
        private readonly Dictionary<TicketPriceTier, Button> _ticketButtons =
            new Dictionary<TicketPriceTier, Button>();
        private Text _stadiumText;
        private Text _stadiumUpgradeText;
        private Text _fanBaseText;
        private Text _popularityText;
        private Text _expectedAttendanceText;
        private Text _recentAttendanceText;
        private Text _ticketPolicyText;
        private Text _weeklyFinanceText;
        private Text _seasonFinanceText;
        private Button _stadiumUpgradeButton;
        private RectTransform _facilityContent;
        private bool _isBuilt;

        public event Action<TicketPriceTier> TicketPolicyRequested;
        public event Action<FacilityType> FacilityUpgradeRequested;
        public event Action StadiumUpgradeRequested;
        public event Action WeekAdvanceRequested;
        public event Action SaveRequested;
        public event Action LoadRequested;

        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);

        /// <summary>운영 Command 실패를 현재 구단 요약 영역에 즉시 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            EnsureHierarchy();
            _stadiumUpgradeText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _stadiumUpgradeText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
        }

        /// <summary>공용 Workspace 슬롯 아래에 기존 Owner 배경을 재사용한 화면을 생성한다.</summary>
        public static UI_Scene_OwnerClubOperations CreateRuntime(Transform parent)
        {
            RectTransform rect = OwnerWorkspaceUiFactory.CreateRoot(
                parent, "UI_Scene_OwnerClubOperations", showOwnerBackground: true);
            return rect.gameObject.AddComponent<UI_Scene_OwnerClubOperations>();
        }

        /// <summary>Presentation Builder가 만든 문구와 Command 가능 상태만 그린다.</summary>
        public void Bind(OwnerClubOperationPresentationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            EnsureHierarchy();
            _stadiumText.text = model.StadiumText;
            _stadiumUpgradeText.text = model.StadiumUpgradeText;
            _fanBaseText.text = model.FanBaseText;
            _popularityText.text = model.PopularityText;
            _expectedAttendanceText.text = model.ExpectedAttendanceText;
            _recentAttendanceText.text = model.RecentAttendanceText;
            _ticketPolicyText.text = model.TicketPolicyText;
            _stadiumUpgradeButton.interactable = model.Snapshot.CanUpgradeStadium;
            Text stadiumButtonLabel = _stadiumUpgradeButton.transform.Find("Label").GetComponent<Text>();
            stadiumButtonLabel.text = model.Snapshot.CanUpgradeStadium ? "구장 증축" : "증축 불가";
            RenderTicketSelection(model.Snapshot.TicketPriceTier);
            RenderFacilities(model.Facilities);
            _weeklyFinanceText.text = FormatFinance(model.WeeklyFinance);
            _seasonFinanceText.text = FormatFinance(model.SeasonFinance);
        }

        private void Awake()
        {
            EnsureHierarchy();
        }

        private void EnsureHierarchy()
        {
            if (_isBuilt) return;
            _isBuilt = true;
            RectTransform root = GetComponent<RectTransform>();
            OwnerRuntimeUiFactory.Stretch(root);

            Image shade = OwnerRuntimeUiFactory.CreateImage(
                "ReadabilityShade",
                root,
                new Color(CareerUiTheme.Background.r, CareerUiTheme.Background.g, CareerUiTheme.Background.b, 0.72f));
            OwnerRuntimeUiFactory.Stretch(shade.rectTransform);

            BuildClubSummary(root);
            BuildFacilityWorkspace(root);
        }

        private void BuildClubSummary(RectTransform root)
        {
            OwnerWorkspaceUiFactory.Panel summary = OwnerRuntimeUiFactory.CreatePanel(
                "ClubSummaryPanel", root, "구단 운영 현황", true);
            OwnerRuntimeUiFactory.SetAnchors(
                summary.Root,
                Vector2.zero,
                new Vector2(0.34f, 1f),
                new Vector2(12f, 12f),
                new Vector2(-6f, -12f));

            _stadiumText = CreateSummaryText(summary.Content, "Stadium", 0.88f, 1f, 19, FontStyle.Bold);
            _stadiumUpgradeText = CreateSummaryText(summary.Content, "StadiumUpgrade", 0.81f, 0.88f, 14);
            _fanBaseText = CreateSummaryText(summary.Content, "FanBase", 0.735f, 0.805f, 16, FontStyle.Bold);
            _popularityText = CreateSummaryText(summary.Content, "Popularity", 0.665f, 0.735f, 16, FontStyle.Bold);
            _expectedAttendanceText = CreateSummaryText(summary.Content, "ExpectedAttendance", 0.595f, 0.665f, 15);
            _recentAttendanceText = CreateSummaryText(summary.Content, "RecentAttendance", 0.525f, 0.595f, 15);

            _stadiumUpgradeButton = OwnerRuntimeUiFactory.CreateButton(
                "StadiumUpgrade", summary.Content, "구장 증축", CareerUiTheme.PrimaryAction, 15);
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumUpgradeButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.455f),
                new Vector2(1f, 0.52f),
                new Vector2(24f, 2f),
                new Vector2(-24f, -2f));
            _stadiumUpgradeButton.onClick.AddListener(() => StadiumUpgradeRequested?.Invoke());

            _ticketPolicyText = CreateSummaryText(summary.Content, "TicketPolicy", 0.385f, 0.45f, 16, FontStyle.Bold);
            BuildTicketButtons(summary.Content);
            _weeklyFinanceText = CreateFinancePanel("WeeklyFinance", summary.Content, 0.155f, 0.29f);
            _seasonFinanceText = CreateFinancePanel("SeasonFinance", summary.Content, 0f, 0.145f);
        }

        private void BuildTicketButtons(Transform parent)
        {
            CreateTicketButton(parent, TicketPriceTier.Cheap, "할인", 0f, 0.32f);
            CreateTicketButton(parent, TicketPriceTier.Standard, "일반", 0.34f, 0.66f);
            CreateTicketButton(parent, TicketPriceTier.Premium, "프리미엄", 0.68f, 1f);
        }

        private void CreateTicketButton(
            Transform parent,
            TicketPriceTier tier,
            string label,
            float anchorMinX,
            float anchorMaxX)
        {
            Button button = OwnerRuntimeUiFactory.CreateButton(
                string.Concat("Ticket_", tier), parent, label, CareerUiTheme.SecondaryAction, 14);
            OwnerRuntimeUiFactory.SetAnchors(
                button.GetComponent<RectTransform>(),
                new Vector2(anchorMinX, 0.32f),
                new Vector2(anchorMaxX, 0.385f),
                new Vector2(anchorMinX == 0f ? 24f : 2f, 2f),
                new Vector2(anchorMaxX == 1f ? -24f : -2f, -2f));
            button.onClick.AddListener(() => TicketPolicyRequested?.Invoke(tier));
            _ticketButtons.Add(tier, button);
        }

        private Text CreateFinancePanel(string name, Transform parent, float anchorMinY, float anchorMaxY)
        {
            Image panel = OwnerRuntimeUiFactory.CreateImage(name, parent, CareerUiTheme.SurfaceSubtle);
            panel.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            OwnerRuntimeUiFactory.SetAnchors(
                panel.rectTransform,
                new Vector2(0f, anchorMinY),
                new Vector2(1f, anchorMaxY),
                new Vector2(24f, 2f),
                new Vector2(-24f, -2f));
            Text text = OwnerRuntimeUiFactory.CreateText(
                "Summary", panel.transform, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            return text;
        }

        private void BuildFacilityWorkspace(RectTransform root)
        {
            OwnerWorkspaceUiFactory.Panel facilities = OwnerRuntimeUiFactory.CreatePanel(
                "FacilityPanel", root, "시설 투자와 운영 효과");
            OwnerRuntimeUiFactory.SetAnchors(
                facilities.Root,
                new Vector2(0.34f, 0f),
                Vector2.one,
                new Vector2(6f, 12f),
                new Vector2(-12f, -12f));
            Text help = OwnerRuntimeUiFactory.CreateText(
                "Help", facilities.Content,
                "시설은 BaseStat을 올리지 않고 SP/DP 생산과 회복·분석·전술 Context에만 연결됩니다.",
                13, FontStyle.Normal, TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(help.rectTransform, new Vector2(0f, 0.91f), new Vector2(0.54f, 1f),
                Vector2.zero, Vector2.zero);
            CreateOperationButton(facilities.Content, "AdvanceWeek", "주간 진행", 0.55f, 0.70f,
                () => WeekAdvanceRequested?.Invoke());
            CreateOperationButton(facilities.Content, "Save", "Save", 0.71f, 0.84f,
                () => SaveRequested?.Invoke());
            CreateOperationButton(facilities.Content, "Load", "Load", 0.85f, 1f,
                () => LoadRequested?.Invoke());
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "FacilityList", facilities.Content, out _facilityContent);
            OwnerRuntimeUiFactory.SetAnchors(scroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.90f),
                Vector2.zero, new Vector2(0f, -4f));
        }

        private static void CreateOperationButton(
            Transform parent,
            string name,
            string label,
            float anchorMinX,
            float anchorMaxX,
            UnityEngine.Events.UnityAction action)
        {
            Button button = OwnerRuntimeUiFactory.CreateButton(
                name, parent, label, CareerUiTheme.SecondaryAction, 13);
            OwnerRuntimeUiFactory.SetAnchors(
                button.GetComponent<RectTransform>(),
                new Vector2(anchorMinX, 0.92f),
                new Vector2(anchorMaxX, 0.99f),
                new Vector2(2f, 0f),
                new Vector2(-2f, 0f));
            button.onClick.AddListener(action);
        }

        private void RenderTicketSelection(TicketPriceTier selected)
        {
            foreach (KeyValuePair<TicketPriceTier, Button> pair in _ticketButtons)
            {
                Image image = pair.Value.GetComponent<Image>();
                image.color = pair.Key == selected ? CareerUiTheme.PrimaryAction : CareerUiTheme.SecondaryAction;
            }
        }

        private void RenderFacilities(IReadOnlyList<OwnerFacilityPresentationRow> facilities)
        {
            OwnerRuntimeUiFactory.ClearChildren(_facilityContent);
            for (int index = 0; index < facilities.Count; index++)
                CreateFacilityRow(facilities[index]);
        }

        private void CreateFacilityRow(OwnerFacilityPresentationRow row)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(
                string.Concat("Facility_", row.FacilityType),
                _facilityContent,
                CareerUiTheme.SurfaceSubtle);
            surface.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            var layout = surface.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 92f;
            layout.minHeight = 82f;

            Text name = OwnerRuntimeUiFactory.CreateText(
                "Name", surface.transform, row.Name, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.TextPrimary);
            OwnerRuntimeUiFactory.SetAnchors(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.24f, 1f),
                new Vector2(16f, 0f), new Vector2(-4f, 0f));
            Text level = OwnerRuntimeUiFactory.CreateText(
                "Level", surface.transform, row.LevelText, 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.AccentGold);
            OwnerRuntimeUiFactory.SetAnchors(level.rectTransform, new Vector2(0f, 0f), new Vector2(0.24f, 0.5f),
                new Vector2(16f, 0f), new Vector2(-4f, 0f));
            Text effect = OwnerRuntimeUiFactory.CreateText(
                "EffectPreview", surface.transform, row.EffectPreviewText, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(effect.rectTransform, new Vector2(0.24f, 0f), new Vector2(0.69f, 1f),
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Text cost = OwnerRuntimeUiFactory.CreateText(
                "UpgradeCost", surface.transform,
                row.CanUpgrade ? string.Concat("업그레이드 ", row.UpgradeCostText) : row.UpgradeDisabledReason,
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                row.CanUpgrade ? CareerUiTheme.TextSecondary : CareerUiTheme.Warning);
            OwnerRuntimeUiFactory.SetAnchors(cost.rectTransform, new Vector2(0.69f, 0.46f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), new Vector2(-12f, 0f));
            Button upgrade = OwnerRuntimeUiFactory.CreateButton(
                "Upgrade", surface.transform, row.CanUpgrade ? "시설 업그레이드" : "업그레이드 불가",
                row.CanUpgrade ? CareerUiTheme.PrimaryAction : CareerUiTheme.SecondaryAction, 13);
            OwnerRuntimeUiFactory.SetAnchors(
                upgrade.GetComponent<RectTransform>(),
                new Vector2(0.71f, 0.06f),
                new Vector2(0.98f, 0.46f),
                Vector2.zero,
                Vector2.zero);
            upgrade.interactable = row.CanUpgrade;
            FacilityType type = row.FacilityType;
            upgrade.onClick.AddListener(() => FacilityUpgradeRequested?.Invoke(type));
        }

        private Text CreateSummaryText(
            Transform parent,
            string name,
            float anchorMinY,
            float anchorMaxY,
            int fontSize,
            FontStyle fontStyle = FontStyle.Normal)
        {
            Text text = OwnerRuntimeUiFactory.CreateText(
                name, parent, string.Empty, fontSize, fontStyle,
                TextAnchor.MiddleLeft, CareerUiTheme.TextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(text.rectTransform,
                new Vector2(0f, anchorMinY), new Vector2(1f, anchorMaxY),
                new Vector2(24f, 0f), new Vector2(-24f, 0f));
            return text;
        }

        private static string FormatFinance(OwnerFinancePresentationModel finance)
        {
            return string.Concat(
                finance.Title, "\n",
                finance.IncomeText, " · ", finance.ExpenseText, " · ", finance.NetText, "\n",
                finance.ProductionText, " · ", finance.AttendanceText);
        }
    }
}

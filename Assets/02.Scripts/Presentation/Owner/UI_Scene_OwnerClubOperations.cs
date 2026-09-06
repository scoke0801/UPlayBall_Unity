using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단 메뉴의 재정과 시설을 공용 Shell 문법으로 구분해 표시하는 uGUI 화면이다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class UI_Scene_OwnerClubOperations : MonoBehaviour, IUiCancelHandler
    {
        private readonly Dictionary<TicketPriceTier, Button> _ticketButtons =
            new Dictionary<TicketPriceTier, Button>();
        private readonly List<GameObject> _financeOnlyObjects = new List<GameObject>();
        private Text _stadiumText;
        private Text _stadiumUpgradeText;
        private Text _fanBaseText;
        private Text _popularityText;
        private Text _expectedAttendanceText;
        private Text _recentAttendanceText;
        private Text _ticketPolicyText;
        private Text _weeklyFinanceText;
        private Text _seasonFinanceText;
        private Text _feedbackText;
        private Text _facilityFeedbackText;
        private Button _stadiumUpgradeButton;
        private RectTransform _facilityContent;
        private RectTransform _summaryRoot;
        private RectTransform _facilityRoot;
        private Image _background;
        private Image _stadiumArtwork;
        private GridLayoutGroup _facilityGrid;
        private Image _readabilityCanvas;
        private bool _isBuilt;

        public event Action<TicketPriceTier> TicketPolicyRequested;
        public event Action<FacilityType> FacilityUpgradeRequested;
        public event Action StadiumUpgradeRequested;
        public event Action WeekAdvanceRequested;
        public event Action SaveRequested;
        public event Action LoadRequested;

        public void SetVisible(bool isVisible)
        {
            if (!isVisible) CloseStadiumSelection();
            gameObject.SetActive(isVisible);
        }

        /// <summary>구장 외형 선택 Popup이 열려 있으면 임시 선택을 버리고 닫는다.</summary>
        public bool TryHandleCancel()
        {
            if (_stadiumPopupRoot == null || !_stadiumPopupRoot.gameObject.activeSelf)
                return false;
            CloseStadiumSelection();
            return true;
        }

        /// <summary>재정과 시설이 서로의 정보 밀도를 빼앗지 않도록 Route별 작업면을 분리한다.</summary>
        public void ShowRoute(string routeId)
        {
            EnsureHierarchy();
            bool showFinance = string.Equals(routeId, OwnerManagementRoutes.ClubFinance, StringComparison.Ordinal);
            if (_background != null)
            {
                _background.sprite = Resources.Load<Sprite>(showFinance
                    ? OwnerUiAssetIds.HomeBackgroundResourcePath
                    : "UI/Generated/bg_owner_club_facilities_v1");
                _background.color = _background.sprite == null ? CareerUiTheme.ReferenceCanvas : Color.white;
            }
            if (_readabilityCanvas != null)
            {
                float alpha = showFinance || !_isStadiumSectionSelected ? 1f : 0f;
                _readabilityCanvas.color = new Color(
                    CareerUiTheme.ReferenceCanvas.r,
                    CareerUiTheme.ReferenceCanvas.g,
                    CareerUiTheme.ReferenceCanvas.b,
                    alpha);
            }
            _summaryRoot.gameObject.SetActive(showFinance);
            _stadiumArtwork.gameObject.SetActive(false);
            _facilityNavigationRoot.gameObject.SetActive(!showFinance);
            _stadiumSceneRoot.gameObject.SetActive(!showFinance && _isStadiumSectionSelected);
            _facilityRoot.gameObject.SetActive(!showFinance && !_isStadiumSectionSelected);
            for (int index = 0; index < _financeOnlyObjects.Count; index++)
                _financeOnlyObjects[index].SetActive(showFinance);
            if (showFinance)
            {
                OwnerRuntimeUiFactory.SetAnchors(_summaryRoot, Vector2.zero, Vector2.one,
                    new Vector2(12f, 12f), new Vector2(-12f, -12f));
                ApplyFinanceLayout();
            }
            else
            {
                OwnerRuntimeUiFactory.SetAnchors(_facilityRoot, Vector2.zero, new Vector2(1f, 0.93f),
                    new Vector2(12f, 12f), new Vector2(-12f, -4f));
                ApplyFacilityLayout();
                RefreshFacilitySection();
            }
        }

        /// <summary>운영 Command 실패를 현재 구단 요약 영역에 즉시 표시한다.</summary>
        public void SetFeedback(string message, bool isError)
        {
            EnsureHierarchy();
            _feedbackText.text = string.IsNullOrWhiteSpace(message) ? "작업 결과가 없습니다." : message;
            _feedbackText.color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
            _facilityFeedbackText.text = _feedbackText.text;
            _facilityFeedbackText.color = _feedbackText.color;
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
            SetButtonStyle(_stadiumUpgradeButton, model.Snapshot.CanUpgradeStadium);
            RenderTicketSelection(model.Snapshot.TicketPriceTier);
            RenderFacilities(model.Facilities);
            BindFinanceDashboard(model);
            BindStadiumWorkspace(model);
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
            _background = root.GetComponent<Image>();

            _readabilityCanvas = UIClubOfficeStyle.Surface(
                "ReadabilityCanvas",
                root,
                new Color(
                    CareerUiTheme.ReferenceCanvas.r,
                    CareerUiTheme.ReferenceCanvas.g,
                    CareerUiTheme.ReferenceCanvas.b,
                    0.90f));
            OwnerRuntimeUiFactory.Stretch(_readabilityCanvas.rectTransform);

            BuildClubSummary(root);
            BuildFinanceDashboard();
            _stadiumArtwork = UIClubOfficeStyle.Illustration(_summaryRoot.Find("ContentSafeRect"), "StadiumArtwork", 6);
            UIClubOfficeStyle.Place(_stadiumArtwork.rectTransform, 0f, .69f, 1f, 1f);
            BuildFacilityWorkspace(root);
            BuildStadiumWorkspace(root);
            ShowRoute(OwnerManagementRoutes.ClubFacility);
        }

        private void BuildClubSummary(RectTransform root)
        {
            OwnerWorkspaceUiFactory.Panel summary = UIClubOfficeStyle.CreatePanel(
                "ClubSummaryPanel", root, "구단 운영 현황", true);
            _summaryRoot = summary.Root;
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
                "StadiumUpgrade", summary.Content, "구장 증축", CareerUiTheme.ReferenceAccent, 15);
            OwnerRuntimeUiFactory.SetAnchors(
                _stadiumUpgradeButton.GetComponent<RectTransform>(),
                new Vector2(0f, 0.455f),
                new Vector2(1f, 0.52f),
                new Vector2(24f, 2f),
                new Vector2(-24f, -2f));
            _stadiumUpgradeButton.onClick.AddListener(() => StadiumUpgradeRequested?.Invoke());
            SetButtonStyle(_stadiumUpgradeButton, true);

            _ticketPolicyText = CreateSummaryText(summary.Content, "TicketPolicy", 0.385f, 0.45f, 16, FontStyle.Bold);
            _financeOnlyObjects.Add(_ticketPolicyText.gameObject);
            BuildTicketButtons(summary.Content);
            _feedbackText = CreateSummaryText(summary.Content, "Feedback", 0.292f, 0.32f, 12);
            _feedbackText.text = "티켓 정책을 선택하고 주간 결산을 확인하세요.";
            _weeklyFinanceText = CreateFinancePanel("WeeklyFinance", summary.Content, 0.17f, 0.285f);
            _seasonFinanceText = CreateFinancePanel("SeasonFinance", summary.Content, 0.08f, 0.16f);
            _financeOnlyObjects.Add(_weeklyFinanceText.transform.parent.gameObject);
            _financeOnlyObjects.Add(_seasonFinanceText.transform.parent.gameObject);
            _financeOnlyObjects.Add(CreateOperationButton(
                summary.Content, "FinanceAdvanceWeek", "주간 진행", 0f, 0.46f,
                () => WeekAdvanceRequested?.Invoke(), 0.01f, 0.07f).gameObject);
            _financeOnlyObjects.Add(CreateOperationButton(
                summary.Content, "FinanceSave", "저장", 0.47f, 0.72f,
                () => SaveRequested?.Invoke(), 0.01f, 0.07f).gameObject);
            _financeOnlyObjects.Add(CreateOperationButton(
                summary.Content, "FinanceLoad", "불러오기", 0.73f, 1f,
                () => LoadRequested?.Invoke(), 0.01f, 0.07f).gameObject);
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
                string.Concat("Ticket_", tier), parent, label, CareerUiTheme.ReferenceButton, 14);
            OwnerRuntimeUiFactory.SetAnchors(
                button.GetComponent<RectTransform>(),
                new Vector2(anchorMinX, 0.32f),
                new Vector2(anchorMaxX, 0.385f),
                new Vector2(anchorMinX == 0f ? 24f : 2f, 2f),
                new Vector2(anchorMaxX == 1f ? -24f : -2f, -2f));
            button.onClick.AddListener(() => TicketPolicyRequested?.Invoke(tier));
            _ticketButtons.Add(tier, button);
            _financeOnlyObjects.Add(button.gameObject);
            SetButtonStyle(button, false);
        }

        private Text CreateFinancePanel(string name, Transform parent, float anchorMinY, float anchorMaxY)
        {
            Image panel = OwnerRuntimeUiFactory.CreateImage(name, parent, CareerUiTheme.ReferencePanel);
            panel.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            OwnerRuntimeUiFactory.SetAnchors(
                panel.rectTransform,
                new Vector2(0f, anchorMinY),
                new Vector2(1f, anchorMaxY),
                new Vector2(24f, 2f),
                new Vector2(-24f, -2f));
            Text text = OwnerRuntimeUiFactory.CreateText(
                "Summary", panel.transform, string.Empty, 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
            OwnerRuntimeUiFactory.Stretch(text.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            return text;
        }

        private void BuildFacilityWorkspace(RectTransform root)
        {
            OwnerWorkspaceUiFactory.Panel facilities = UIClubOfficeStyle.CreatePanel(
                "FacilityPanel", root, "시설 투자와 운영 효과");
            _facilityRoot = facilities.Root;
            OwnerRuntimeUiFactory.SetAnchors(
                facilities.Root,
                new Vector2(0.34f, 0f),
                Vector2.one,
                new Vector2(6f, 12f),
                new Vector2(-12f, -12f));
            Text help = OwnerRuntimeUiFactory.CreateText(
                "Help", facilities.Content,
                "시설 투자로 구단의 운영 환경을 개선하세요.\n스카우트·육성 포인트 생산 / 회복·분석·전술 지원",
                13, FontStyle.Normal, TextAnchor.MiddleLeft, UIClubOfficeStyle.Muted);
            OwnerRuntimeUiFactory.SetAnchors(help.rectTransform, new Vector2(0f, 0.91f), new Vector2(0.54f, 1f),
                Vector2.zero, Vector2.zero);
            CreateOperationButton(facilities.Content, "AdvanceWeek", "주간 진행", 0.55f, 0.70f,
                () => WeekAdvanceRequested?.Invoke());
            CreateOperationButton(facilities.Content, "Save", "저장", 0.71f, 0.84f,
                () => SaveRequested?.Invoke());
            CreateOperationButton(facilities.Content, "Load", "불러오기", 0.85f, 1f,
                () => LoadRequested?.Invoke());
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalGridScroll(
                "FacilityList", facilities.Content, 2, new Vector2(380f, 224f), 12f, out _facilityContent);
            _facilityGrid = _facilityContent.GetComponent<GridLayoutGroup>();
            _facilityFeedbackText = UIClubOfficeStyle.Label("FacilityFeedback", facilities.Content,
                "시설별 운영 효과와 투자 비용을 비교한 뒤 업그레이드하세요.", 13);
            UIClubOfficeStyle.Place(_facilityFeedbackText.rectTransform, .01f, .855f, .99f, .905f);
            OwnerRuntimeUiFactory.SetAnchors(scroll.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.85f),
                Vector2.zero, new Vector2(0f, -4f));
        }

        private static Button CreateOperationButton(
            Transform parent,
            string name,
            string label,
            float anchorMinX,
            float anchorMaxX,
            UnityEngine.Events.UnityAction action,
            float anchorMinY = 0.92f,
            float anchorMaxY = 0.99f)
        {
            Button button = OwnerRuntimeUiFactory.CreateButton(
                name, parent, label, CareerUiTheme.ReferenceButton, 13);
            OwnerRuntimeUiFactory.SetAnchors(
                button.GetComponent<RectTransform>(),
                new Vector2(anchorMinX, anchorMinY),
                new Vector2(anchorMaxX, anchorMaxY),
                new Vector2(2f, 0f),
                new Vector2(-2f, 0f));
            button.onClick.AddListener(action);
            SetButtonStyle(button, false);
            return button;
        }

        private void RenderTicketSelection(TicketPriceTier selected)
        {
            foreach (KeyValuePair<TicketPriceTier, Button> pair in _ticketButtons)
                StyleFinanceTicket(pair.Value, pair.Key == selected);
        }

        private void RenderFacilities(IReadOnlyList<OwnerFacilityPresentationRow> facilities)
        {
            OwnerRuntimeUiFactory.ClearChildren(_facilityContent);
            for (int index = 0; index < facilities.Count; index++)
                CreateFacilityRow(facilities[index], index);
        }

        private void CreateFacilityRow(OwnerFacilityPresentationRow row, int index)
        {
            Image surface = UIClubOfficeStyle.Surface(
                string.Concat("Facility_", row.FacilityType),
                _facilityContent,
                CareerUiTheme.ReferencePanel);
            var layout = surface.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 92f;
            layout.minHeight = 82f;

            Text name = OwnerRuntimeUiFactory.CreateText(
                "Name", surface.transform, row.Name, 18, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceText);
            OwnerRuntimeUiFactory.SetAnchors(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.24f, 1f),
                new Vector2(16f, 0f), new Vector2(-4f, 0f));
            Text level = OwnerRuntimeUiFactory.CreateText(
                "Level", surface.transform, row.LevelText, 14, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceAccent);
            OwnerRuntimeUiFactory.SetAnchors(level.rectTransform, new Vector2(0f, 0f), new Vector2(0.24f, 0.5f),
                new Vector2(16f, 0f), new Vector2(-4f, 0f));
            Text effect = OwnerRuntimeUiFactory.CreateText(
                "EffectPreview", surface.transform, row.EffectPreviewText, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceTextSecondary);
            OwnerRuntimeUiFactory.SetAnchors(effect.rectTransform, new Vector2(0.24f, 0f), new Vector2(0.69f, 1f),
                new Vector2(8f, 4f), new Vector2(-8f, -4f));
            Text cost = OwnerRuntimeUiFactory.CreateText(
                "UpgradeCost", surface.transform,
                row.CanUpgrade ? string.Concat("업그레이드 ", row.UpgradeCostText) : row.UpgradeDisabledReason,
                13, FontStyle.Normal, TextAnchor.MiddleCenter,
                row.CanUpgrade ? CareerUiTheme.ReferenceTextSecondary : CareerUiTheme.Warning);
            OwnerRuntimeUiFactory.SetAnchors(cost.rectTransform, new Vector2(0.69f, 0.46f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), new Vector2(-12f, 0f));
            Button upgrade = OwnerRuntimeUiFactory.CreateButton(
                "Upgrade", surface.transform, row.CanUpgrade ? "시설 업그레이드" : "업그레이드 불가",
                row.CanUpgrade ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceButton, 13);
            OwnerRuntimeUiFactory.SetAnchors(
                upgrade.GetComponent<RectTransform>(),
                new Vector2(0.71f, 0.06f),
                new Vector2(0.98f, 0.46f),
                Vector2.zero,
                Vector2.zero);
            upgrade.interactable = row.CanUpgrade;
            SetButtonStyle(upgrade, row.CanUpgrade);
            Image artwork = UIClubOfficeStyle.Illustration(surface.transform, "Artwork", (int)row.FacilityType);
            UIClubOfficeStyle.Place(artwork.rectTransform, 0f, .54f, .36f, 1f);
            UIClubOfficeStyle.Place(name.rectTransform, .40f, .75f, .97f, .98f);
            UIClubOfficeStyle.Place(level.rectTransform, .40f, .56f, .97f, .75f);
            UIClubOfficeStyle.Place(effect.rectTransform, .04f, .28f, .96f, .53f);
            UIClubOfficeStyle.Place(cost.rectTransform, .04f, .03f, .57f, .25f);
            UIClubOfficeStyle.Place(upgrade.GetComponent<RectTransform>(), .60f, .05f, .96f, .24f);
            name.fontSize = 18;
            effect.fontSize = 14;
            cost.alignment = TextAnchor.MiddleLeft;
            effect.color = UIClubOfficeStyle.Muted;
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
                TextAnchor.MiddleLeft, UIClubOfficeStyle.Muted);
            OwnerRuntimeUiFactory.SetAnchors(text.rectTransform,
                new Vector2(0f, anchorMinY), new Vector2(1f, anchorMaxY),
                new Vector2(24f, 0f), new Vector2(-24f, 0f));
            return text;
        }

        private void ApplyFinanceLayout()
        {
            SetLayout(_stadiumText.rectTransform, .01f, .69f, .38f, .76f, 18f);
            _stadiumText.color = Color.white;
            SetLayout(_stadiumUpgradeText.rectTransform, .01f, .60f, .38f, .68f, 18f);
            SetLayout(_stadiumUpgradeButton.GetComponent<RectTransform>(), .24f, .53f, .37f, .59f, 0f);
            SetLayout(_fanBaseText.rectTransform, .41f, .85f, .68f, .94f, 16f);
            SetLayout(_popularityText.rectTransform, .70f, .85f, .98f, .94f, 16f);
            SetLayout(_expectedAttendanceText.rectTransform, .41f, .67f, .68f, .76f, 16f);
            SetLayout(_recentAttendanceText.rectTransform, .70f, .67f, .98f, .76f, 16f);
            SetLayout(_ticketPolicyText.rectTransform, .42f, .55f, .61f, .62f, 6f);
            SetLayout(_ticketButtons[TicketPriceTier.Cheap].GetComponent<RectTransform>(),
                .61f, .54f, .73f, .61f, 3f);
            SetLayout(_ticketButtons[TicketPriceTier.Standard].GetComponent<RectTransform>(),
                .73f, .54f, .85f, .61f, 3f);
            SetLayout(_ticketButtons[TicketPriceTier.Premium].GetComponent<RectTransform>(),
                .85f, .54f, .98f, .61f, 3f);
            SetLayout(_weeklyFinanceText.transform.parent.GetComponent<RectTransform>(),
                .01f, .12f, .49f, .49f, 0f);
            SetLayout(_seasonFinanceText.transform.parent.GetComponent<RectTransform>(),
                .51f, .12f, .99f, .49f, 0f);
            SetLayout(_feedbackText.rectTransform, .01f, .015f, .53f, .095f, 12f);
        }

        private void ApplyFacilityLayout()
        {
            SetLayout(_stadiumText.rectTransform, 0f, .60f, 1f, .68f, 12f);
            _stadiumText.color = UIClubOfficeStyle.Ink;
            SetLayout(_stadiumUpgradeText.rectTransform, 0f, .51f, 1f, .60f, 12f);
            SetLayout(_stadiumUpgradeButton.GetComponent<RectTransform>(), 0f, .43f, 1f, .50f, 12f);
            SetLayout(_fanBaseText.rectTransform, 0f, .34f, 1f, .41f, 12f);
            SetLayout(_popularityText.rectTransform, 0f, .27f, 1f, .34f, 12f);
            SetLayout(_expectedAttendanceText.rectTransform, 0f, .20f, 1f, .27f, 12f);
            SetLayout(_recentAttendanceText.rectTransform, 0f, .13f, 1f, .20f, 12f);
            SetLayout(_feedbackText.rectTransform, 0f, 0f, 1f, .12f, 12f);
        }

        private void LateUpdate()
        {
            if (_facilityGrid == null || !_facilityRoot.gameObject.activeInHierarchy) return;
            float width = _facilityContent.rect.width;
            int columns = width < 640f ? 1 : 2;
            Vector2 size = new Vector2(Mathf.Max(240f, (width - 16f - (columns - 1) * 12f) / columns), 224f);
            if (_facilityGrid.constraintCount == columns && _facilityGrid.cellSize == size) return;
            _facilityGrid.constraintCount = columns;
            _facilityGrid.cellSize = size;
        }

        private static void SetLayout(
            RectTransform rect,
            float minX,
            float minY,
            float maxX,
            float maxY,
            float horizontalPadding)
        {
            OwnerRuntimeUiFactory.SetAnchors(
                rect,
                new Vector2(minX, minY),
                new Vector2(maxX, maxY),
                new Vector2(horizontalPadding, 0f),
                new Vector2(-horizontalPadding, 0f));
        }

        private static void SetButtonStyle(Button button, bool isPrimary)
        {
            button.GetComponent<Image>().color = isPrimary
                ? CareerUiTheme.ReferenceAccent
                : CareerUiTheme.ReferenceButton;
            button.transform.Find("Label").GetComponent<Text>().color = isPrimary
                ? Color.white
                : CareerUiTheme.ReferenceText;
        }

    }
}

using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Shop;
using Baseball.Simulation.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단 운영과 Condition 화면이 Resolver Snapshot만 표시하고 SharedGameShell에 합성되는지 검증한다.</summary>
    public sealed class OwnerManagementPresentationTests
    {
        private GameObject _root;

        [Test]
        public void OwnerPowerUpView_선택탭만조회하고왕복은재사용하며새상태는갱신한다()
        {
            int scoutQueries = 0, trainingQueries = 0, enhancementQueries = 0;
            OwnerPowerUpSnapshot CreateSnapshot() => new OwnerPowerUpSnapshot(
                () => { scoutQueries++; return new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"); },
                () => { trainingQueries++; return new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0); },
                () => { enhancementQueries++; return new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0); });
            var view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            try
            {
                view.Bind(CreateSnapshot());
                Assert.That(new[] { scoutQueries, trainingQueries, enhancementQueries }, Is.EqualTo(new[] { 1, 0, 0 }));
                view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
                view.ShowRoute(OwnerNavigationRoutes.PowerUpScout);
                view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
                Assert.That(new[] { scoutQueries, trainingQueries, enhancementQueries }, Is.EqualTo(new[] { 1, 1, 0 }));
                view.Bind(CreateSnapshot(), OwnerNavigationRoutes.PowerUpEnhancementSale);
                Assert.That(new[] { scoutQueries, trainingQueries, enhancementQueries }, Is.EqualTo(new[] { 1, 1, 1 }));
                view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
                Assert.That(trainingQueries, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void OwnerPowerUpView_대량상품도선택확률만조회하고페이지버튼수를제한한다()
        {
            int queries = 0;
            var products = new OwnerScoutProductSnapshot[1200];
            for (int index = 0; index < products.Length; index++)
                products[index] = new OwnerScoutProductSnapshot(
                    "product" + index, "스카우트", "범위" + index, "SP 100", true, "",
                    1, 0, 100, 1, 8, null, () =>
                    {
                        queries++;
                        return new[] { new OwnerScoutProbabilitySnapshot("Cost 5 · 일반", 1d) };
                    });
            Assert.That(queries, Is.Zero);
            var view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            var snapshot = new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(products, "SP 1000"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            view.Bind(snapshot);
            watch.Stop();
            TestContext.WriteLine($"전력보강 1,200상품 Bind: {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.That(queries, Is.EqualTo(1));
            Transform scout = _root.transform.Find(
                "OwnerPowerUpWorkspace/PowerUpPanel/ContentSafeRect/ScoutContent/ScoutReference");
            Transform pins = scout.Find("KoreaMap/ScoutProductPins");
            Assert.That(pins.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(8));
            pins.Find("NextMapPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(pins.Find("Scout_product6"), Is.Not.Null);
            pins.Find("Scout_product6").GetComponent<Button>().onClick.Invoke();
            Assert.That(queries, Is.EqualTo(2));
            Assert.That(products[6].Probabilities[0].Probability, Is.EqualTo(1d));
            Assert.That(queries, Is.EqualTo(2));
            scout.Find("ScoutPolicy").GetComponent<Button>().onClick.Invoke();
            Transform options = scout.GetComponentInChildren<Transform>(true);
            foreach (Transform child in scout.GetComponentsInChildren<Transform>(true))
                if (child.name == "PolicyOptions") options = child;
            Assert.That(options.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(12));
            options.Find("NextPolicyPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(options.Find("PolicyChoice10"), Is.Not.Null);
            Assert.That(queries, Is.EqualTo(2));
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void TeamColorView_스킨재적용후에도슬롯과동적목록의카드대비를보존한다()
        {
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011)[0];
            var candidate = new OwnerTeamColorCandidateSnapshot(
                definition, definition.RequiredCount, new[] { "김수비" }, true);
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋", new[] { definition.TeamColorId, null }, new[] { candidate });
            var view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);
            view.Bind(snapshot);
            Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");

            for (int pass = 0; pass < 2; pass++)
            {
                CareerUiSkin.Apply(view.transform);
                string[] paths = { "EquippedSlots/ContentSafeRect/Slot0", "EquippedSlots/ContentSafeRect/Slot1",
                    "CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate0" };
                foreach (string path in paths)
                {
                    Transform card = workspace.Find(path);
                    Button button = card.GetComponent<Button>();
                    OwnerUiButtonSkin.Apply(button);
                    CareerUiSkin.ApplyButton(button);
                    RawImage artwork = card.Find("Artwork").GetComponent<RawImage>();
                    Assert.That(button.targetGraphic, Is.SameAs(artwork), path);
                    Assert.That(artwork.texture, Is.Not.Null, path);
                    Assert.That(artwork.color.a, Is.EqualTo(1f), path);
                    Assert.That(button.colors.disabledColor, Is.EqualTo(Color.white), path);
                    Assert.That(card.Find("Label").GetComponent<Text>().color, Is.EqualTo(Color.white), path);
                    Assert.That(card.GetComponent<Image>().color.a, Is.Zero, path);
                    Transform frame = card.Find("OwnerButtonFrame");
                    if (frame != null) Assert.That(frame.gameObject.activeSelf, Is.False, path);
                }
                // 필터가 새로 생성한 목록에도 동일한 시각 계약이 적용되어야 한다.
                workspace.Find("CandidateList/ContentSafeRect/Active").GetComponent<Button>().onClick.Invoke();
            }
            Assert.That(workspace.Find("Actions/ContentSafeRect/Confirm").GetComponent<Button>().interactable, Is.False);
            Assert.That(workspace.Find("EquippedSlots/ContentSafeRect/Slot0/State").GetComponent<Text>().text, Is.EqualTo("발동"));
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OwnerManagementPresentationTests_Root", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void ClubBuilder_구장팬관중티켓여섯시설과주간시즌재무를완성한다()
        {
            OwnerClubOperationPresentationModel model =
                OwnerClubOperationPresentationBuilder.Build(CreateClubSnapshot());

            Assert.That(model.StadiumText, Does.Contain("15,000석"));
            Assert.That(model.FanBaseText, Is.EqualTo("팬 기반 62.5"));
            Assert.That(model.PopularityText, Is.EqualTo("인기도 71.0"));
            Assert.That(model.ExpectedAttendanceText, Does.Contain("12,340명"));
            Assert.That(model.RecentAttendanceText, Does.Contain("정보 부족"));
            Assert.That(model.TicketPolicyText, Does.Contain("프리미엄"));
            Assert.That(model.Facilities.Count, Is.EqualTo(6));
            Assert.That(model.Facilities[(int)FacilityType.ScoutingCenter].EffectPreviewText,
                Does.Contain("스카우트 포인트 +25"));
            Assert.That(model.Facilities[(int)FacilityType.TrainingCenter].EffectPreviewText,
                Does.Contain("육성 포인트 +12"));
            Assert.That(model.Facilities[(int)FacilityType.RecoveryCenter].EffectPreviewText,
                Does.Contain("회복 효율 +5"));
            Assert.That(model.Facilities[(int)FacilityType.DataAnalysisCenter].EffectPreviewText,
                Does.Contain("분석 신뢰도 +3"));
            Assert.That(model.Facilities[(int)FacilityType.TacticLab].EffectPreviewText,
                Does.Contain("전술 연구 효율 +5"));
            Assert.That(model.Facilities[(int)FacilityType.FanShop].EffectPreviewText,
                Does.Contain("1인당 +700원"));
            Assert.That(model.WeeklyFinance.NetText, Does.Contain("+80만원"));
            Assert.That(model.SeasonFinance.AttendanceText, Does.Contain("관중 210,000명"));
        }

        [Test]
        public void ConditionBuilder_Resolver합성값을열단계와모든근거열로표시한다()
        {
            ConditionPresentationTable presentation = ConditionChemistryBalanceTable.CreateDefault().Presentation;
            var players = new[]
            {
                new OwnerConditionPlayerSnapshot(
                    "pitcher-1",
                    "김선발",
                    "SP",
                    true,
                    PlayerAvailabilityStatus.DayToDay,
                    new EffectiveMatchCondition(67, -10, 10, 10, 0)),
                new OwnerConditionPlayerSnapshot(
                    "batter-1",
                    "이중견",
                    "CF",
                    false,
                    PlayerAvailabilityStatus.Available,
                    new EffectiveMatchCondition(82, 0, -10, 0, 0))
            };

            OwnerConditionChemistryPresentationModel model =
                OwnerConditionChemistryPresentationBuilder.Build(players, presentation);

            Assert.That(model.Players.Count, Is.EqualTo(2));
            Assert.That(model.Players[0].BaseLevel, Is.EqualTo(7));
            Assert.That(model.Players[0].EffectiveLevel, Is.EqualTo(8));
            Assert.That(model.Players[0].BaseConditionText, Does.Contain("좋음"));
            Assert.That(model.Players[0].AssignmentText, Is.EqualTo("-10"));
            Assert.That(model.Players[0].LineupChemistryText, Is.EqualTo("+10"));
            Assert.That(model.Players[0].BatteryChemistryText, Is.EqualTo("+10"));
            Assert.That(model.Players[0].EffectiveConditionText, Is.EqualTo("매우 좋음 · Lv.8"));
            Assert.That(model.Players[0].EffectiveConditionText, Does.Not.Contain("77"));
            Assert.That(model.Players[1].BatteryChemistryText, Is.EqualTo("해당 없음"));
            Assert.That(model.Players[1].EffectiveConditionText, Is.EqualTo("매우 좋음 · Lv.8"));
            Assert.That(model.SummaryText, Does.Contain("출전 가능 1명"));
        }

        [Test]
        public void ConditionSnapshot_타자에게BatteryModifier를적용한잘못된입력을거부한다()
        {
            Assert.Throws<ArgumentException>(() => new OwnerConditionPlayerSnapshot(
                "batter-invalid",
                "잘못된 타자",
                "RF",
                false,
                PlayerAvailabilityStatus.Available,
                new EffectiveMatchCondition(80, 0, 0, 10, 0)));
        }

        [Test]
        public void ClubView_시설작업면의중복Command를제거하고시설목록을확장한다()
        {
            UI_Scene_OwnerClubOperations view = UI_Scene_OwnerClubOperations.CreateRuntime(_root.transform);
            OwnerClubOperationPresentationModel model =
                OwnerClubOperationPresentationBuilder.Build(CreateClubSnapshot());
            FacilityType? requestedFacility = null;
            TicketPriceTier? requestedTicket = null;
            int weekAdvanceRequests = 0;
            view.FacilityUpgradeRequested += type => requestedFacility = type;
            view.TicketPolicyRequested += tier => requestedTicket = tier;
            view.WeekAdvanceRequested += () => weekAdvanceRequests++;

            view.Bind(model);

            Transform stadiumScene = view.transform.Find("StadiumScene");
            Assert.That(stadiumScene, Is.Not.Null);
            Assert.That(stadiumScene.gameObject.activeSelf, Is.True);
            Assert.That(stadiumScene.GetComponent<Image>().sprite, Is.Not.Null);
            stadiumScene.Find("InformationBar/OpenStadiumSelection").GetComponent<Button>().onClick.Invoke();
            Transform popup = view.transform.Find("StadiumSelectionPopup");
            Assert.That(popup.gameObject.activeSelf, Is.True);
            popup.Find("Panel/ChoiceArea/StadiumChoice3").GetComponent<Button>().onClick.Invoke();
            popup.Find("Panel/Confirm").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.SelectedStadiumIndex, Is.EqualTo(3));
            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(stadiumScene.Find("InformationBar/StadiumName").GetComponent<Text>().text,
                Does.Contain("스카이 돔"));

            view.transform.Find("FacilityNavigation/AdditionalFacilityTab")
                .GetComponent<Button>().onClick.Invoke();
            Transform facility = view.transform.Find(
                "FacilityPanel/ContentSafeRect/FacilityList/Viewport/Content/Facility_ScoutingCenter");
            Assert.That(facility, Is.Not.Null);
            Assert.That(view.transform.Find("FacilityPanel").gameObject.activeSelf, Is.True);
            Assert.That(facility.Find("EffectPreview").GetComponent<Text>().text, Does.Contain("스카우트 포인트 +25"));
            Assert.That(view.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(view.transform.Find("ReadabilityCanvas").GetComponent<Image>().color.r,
                Is.EqualTo(CareerUiTheme.ReferenceCanvas.r).Within(0.001f));
            Assert.That(facility.GetComponent<Image>().color.r,
                Is.EqualTo(CareerUiTheme.ReferencePanel.r).Within(0.001f));
            Transform facilityContent = view.transform.Find("FacilityPanel/ContentSafeRect");
            Assert.That(facilityContent.Find("AdvanceWeek"), Is.Null);
            Assert.That(facilityContent.Find("Save"), Is.Null);
            Assert.That(facilityContent.Find("Load"), Is.Null);
            RectTransform facilityList = facilityContent.Find("FacilityList").GetComponent<RectTransform>();
            Assert.That(facilityList.anchorMax.y, Is.EqualTo(0.93f).Within(0.001f));
            GridLayoutGroup facilityGrid = facilityList.Find("Viewport/Content").GetComponent<GridLayoutGroup>();
            Assert.That(facilityGrid.cellSize.y, Is.EqualTo(244f).Within(0.001f));
            facility.Find("Upgrade").GetComponent<Button>().onClick.Invoke();
            view.transform.Find("ClubSummaryPanel/ContentSafeRect/Ticket_Cheap").GetComponent<Button>().onClick.Invoke();

            view.ShowRoute(OwnerManagementRoutes.ClubFinance);
            Assert.That(view.transform.Find("FacilityPanel").gameObject.activeSelf, Is.False);
            Assert.That(view.transform.Find("ClubSummaryPanel").GetComponent<RectTransform>().anchorMax,
                Is.EqualTo(Vector2.one));
            Button selectedTicket = view.transform.Find(
                "ClubSummaryPanel/ContentSafeRect/Ticket_Premium").GetComponent<Button>();
            Assert.That(selectedTicket.GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferenceAccent));
            Assert.That(selectedTicket.transform.Find("Label").GetComponent<Text>().color, Is.EqualTo(Color.white));
            Transform finance = view.transform.Find("ClubSummaryPanel/ContentSafeRect");
            Assert.That(finance.Find("FinanceBallpark").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(finance.Find("WeeklyFinance/NetValue").GetComponent<Text>().text,
                Is.EqualTo(OwnerMoneyFormatter.FormatSigned(
                    model.Snapshot.WeeklyFinance.MoneyIncome - model.Snapshot.WeeklyFinance.MoneyExpense)));
            Assert.That(finance.Find("SeasonFinance/Attendance").GetComponent<Text>().text,
                Is.EqualTo(model.SeasonFinance.AttendanceText));
            Assert.That(finance.Find("FanBaseMetric/MeterTrack/Fill").GetComponent<RectTransform>().anchorMax.x,
                Is.EqualTo((float)model.Snapshot.FanBase / 100f).Within(.001f));
            Assert.That(finance.Find("FinanceSave"), Is.Null);
            Assert.That(finance.Find("FinanceLoad"), Is.Null);
            Button settlement = finance.Find("FinanceAdvanceWeek").GetComponent<Button>();
            Assert.That(settlement.transform.Find("Label").GetComponent<Text>().text, Is.EqualTo("결산"));
            settlement.onClick.Invoke();

            Assert.That(requestedFacility, Is.EqualTo(FacilityType.ScoutingCenter));
            Assert.That(requestedTicket, Is.EqualTo(TicketPriceTier.Cheap));
            Assert.That(weekAdvanceRequests, Is.EqualTo(1));
        }

        [Test]
        public void ConditionView_선수행에모든Modifier를표시하고선택의도만전달한다()
        {
            UI_Scene_OwnerConditionChemistry view =
                UI_Scene_OwnerConditionChemistry.CreateRuntime(_root.transform);
            OwnerConditionChemistryPresentationModel model = OwnerConditionChemistryPresentationBuilder.Build(
                new[]
                {
                    new OwnerConditionPlayerSnapshot(
                        "pitcher-1",
                        "김선발",
                        "SP",
                        true,
                        PlayerAvailabilityStatus.Available,
                        new EffectiveMatchCondition(67, -10, 10, 10, 0))
                },
                ConditionChemistryBalanceTable.CreateDefault().Presentation);
            string requestedPlayerId = null;
            view.PlayerSelected += id => requestedPlayerId = id;

            view.Bind(model);

            Transform row = view.transform.Find(
                "ConditionPanel/ContentSafeRect/PlayerConditionList/Viewport/Content/Player_pitcher-1");
            Assert.That(row, Is.Not.Null);
            Assert.That(row.Find("BaseCondition").GetComponent<Text>().text, Does.Contain("Lv.7"));
            Assert.That(row.Find("AssignmentModifier").GetComponent<Text>().text, Is.EqualTo("-10"));
            Assert.That(row.Find("LineupChemistry").GetComponent<Text>().text, Is.EqualTo("+10"));
            Assert.That(row.Find("BatteryChemistry").GetComponent<Text>().text, Is.EqualTo("+10"));
            Assert.That(row.Find("ExpectedCondition").GetComponent<Text>().text, Is.EqualTo("매우 좋음 · Lv.8"));
            row.GetComponent<Button>().onClick.Invoke();

            Assert.That(requestedPlayerId, Is.EqualTo("pitcher-1"));
        }

        [Test]
        public void WorkspaceRouter_SharedShell슬롯안에서등록된세Route만전환한다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            OwnerManagementWorkspaceView workspace = OwnerManagementWorkspaceView.CreateRuntime(shell);

            Assert.That(workspace.transform.parent, Is.EqualTo(shell.MainWorkspaceHost));
            Assert.That(workspace.ShowRoute(OwnerManagementRoutes.ClubFacility), Is.True);
            Assert.That(workspace.ClubOperations.gameObject.activeSelf, Is.True);
            Assert.That(workspace.ConditionChemistry.gameObject.activeSelf, Is.False);
            Assert.That(workspace.ShowRoute(OwnerManagementRoutes.RosterCondition), Is.True);
            Assert.That(workspace.ClubOperations.gameObject.activeSelf, Is.False);
            Assert.That(workspace.ConditionChemistry.gameObject.activeSelf, Is.True);
            Assert.That(workspace.ShowRoute("Owner.Unknown"), Is.False);
            Assert.That(workspace.ActiveRouteId, Is.EqualTo(OwnerManagementRoutes.RosterCondition));
        }

        [Test]
        public void ExpansionWorkspaceCoordinator_실제Club과ConditionRoute를같은Shell에서전환한다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            OwnerExpansionWorkspaceCoordinator coordinator =
                shell.gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            coordinator.Initialize(shell);
            coordinator.BindClubOperation(CreateClubSnapshot());
            coordinator.BindConditionChemistry(
                new[]
                {
                    new OwnerConditionPlayerSnapshot(
                        "pitcher-route",
                        "라우트 선발",
                        "SP",
                        true,
                        PlayerAvailabilityStatus.Available,
                        new EffectiveMatchCondition(70, 0, 0, 0, 0))
                },
                ConditionChemistryBalanceTable.CreateDefault().Presentation);

            Assert.That(coordinator.TryShowRoute(OwnerManagementRoutes.ClubFinance), Is.True);
            Assert.That(coordinator.ActiveRouteId, Is.EqualTo(OwnerManagementRoutes.ClubFinance));
            Assert.That(shell.transform.Find("ContextHeader/Title").GetComponent<Text>().text,
                Is.EqualTo("구단 / 구단 재정"));
            Assert.That(coordinator.TryShowRoute(OwnerManagementRoutes.RosterCondition), Is.True);
            Assert.That(coordinator.ActiveRouteId, Is.EqualTo(OwnerManagementRoutes.RosterCondition));
            Assert.That(coordinator.TryShowRoute(
                OwnerManagementRoutes.RosterCondition,
                OwnerNavigationRoutes.MatchCenterCondition), Is.True);
            Assert.That(coordinator.ActiveRouteId, Is.EqualTo(OwnerNavigationRoutes.MatchCenterCondition));
            Assert.That(shell.transform.Find("ContextHeader/Back").gameObject.activeSelf, Is.True);

            coordinator.HideAll();
            Assert.That(coordinator.ActiveRouteId, Is.Empty);
        }

        [Test]
        public void ExpansionWorkspaceCoordinator_전력보강Primary는Home대신잠김Workspace를표시한다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            OwnerExpansionWorkspaceCoordinator coordinator =
                shell.gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            coordinator.Initialize(shell);

            Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.PowerUp), Is.True);
            Assert.That(coordinator.ActiveRouteId, Is.EqualTo(OwnerNavigationRoutes.PowerUp));
            Transform workspace = shell.MainWorkspaceHost.Find("OwnerPowerUpWorkspace");
            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void OwnerPowerUpView_지도카드그리드와합성대가Route별로분리된다()
        {
            var host = _root.GetComponent<RectTransform>();
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(host);
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0)));

            Transform workspace = host.Find("OwnerPowerUpWorkspace/PowerUpPanel/ContentSafeRect");
            Transform mapGraphic = workspace.Find(
                "ScoutContent/ScoutReference/KoreaMap");
            Assert.That(mapGraphic.GetComponent<Graphic>(), Is.Not.Null);
            Assert.That(mapGraphic.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(mapGraphic.GetComponent<Graphic>().raycastTarget, Is.False);
            Transform trainingCardGrid = workspace.Find(
                "TrainingContent/TrainingTargets/ContentSafeRect/CardScroll/Viewport/Content");
            Assert.That(trainingCardGrid.GetComponent<GridLayoutGroup>(), Is.Not.Null);
            Assert.That(trainingCardGrid.GetComponent<VerticalLayoutGroup>(), Is.Null);
            Transform trainingProgramGrid = workspace.Find(
                "TrainingContent/TrainingPrograms/ContentSafeRect/ProgramScroll/Viewport/Content");
            Assert.That(trainingProgramGrid.GetComponent<GridLayoutGroup>(), Is.Null);
            Assert.That(trainingProgramGrid.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
            Assert.That(workspace.Find("EnhancementSaleContent/ReferenceReinforcement/RegistrationFrame/RegistrationSlot0/TargetCard")
                .GetComponent<Baseball.Presentation.SharedUI.PlayerMiniCardView>(), Is.Not.Null);
            Assert.That(workspace.Find("EnhancementSaleContent/ReferenceReinforcement/RegistrationFrame/RegistrationSlot1/MaterialCard")
                .GetComponent<Baseball.Presentation.SharedUI.PlayerMiniCardView>(), Is.Not.Null);

            view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            Assert.That(workspace.Find("ScoutContent").gameObject.activeSelf, Is.False);
            Assert.That(workspace.Find("TrainingContent").gameObject.activeSelf, Is.False);
            Assert.That(workspace.Find("EnhancementSaleContent").gameObject.activeSelf, Is.True);
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void OwnerPowerUpView_훈련목록은MiniCard이고선택선수는MainFrame앞면이다()
        {
            var card = new OwnerCollectionCardSnapshot(
                "training-card", "training-person", "훈련선수", 2025,
                PlayerPosition.Shortstop, 7, PlayerCardEdition.Normal, 1, 1, false, false);
            var target = new OwnerCardTrainingTargetSnapshot(
                card, Array.Empty<OwnerCardTrainingProgramSnapshot>());
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(
                _root.GetComponent<RectTransform>());
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"),
                new OwnerCardTrainingScreenSnapshot(new[] { target }, 100),
                new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0)));

            Transform training = _root.transform.Find(
                "OwnerPowerUpWorkspace/PowerUpPanel/ContentSafeRect/TrainingContent");
            view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
            Transform miniCard = training.Find(
                "TrainingTargets/ContentSafeRect/CardScroll/Viewport/Content/Card_training-card");
            Transform selectedCard = training.Find(
                "TrainingCard/ContentSafeRect/SelectedTrainingCard");
            VerticalLayoutGroup selectedCardLayout = selectedCard.parent.GetComponent<VerticalLayoutGroup>();

            Assert.That(miniCard.GetComponent<PlayerMiniCardView>(), Is.Not.Null);
            Assert.That(miniCard.Find("Portrait").GetComponent<Image>().sprite,
                Is.Not.Null);
            Assert.That(miniCard.Find("Portrait").gameObject.activeSelf, Is.True);
            Assert.That(miniCard.Find("LineupSubFrame").GetComponent<Image>().sprite.name,
                Is.EqualTo("PlayerCard_Mini_Reference"));
            Assert.That(selectedCard.GetComponent<PlayerMiniCardView>(), Is.Null);
            Assert.That(selectedCardLayout.padding.top, Is.EqualTo((int)(CareerUiTheme.Space4 * 3f)));
            Transform selectedCardFront = selectedCard.Find("CardFront");
            Assert.That(selectedCardFront.GetComponent<AspectRatioFitter>().aspectRatio, Is.EqualTo(2f / 3f));
            CareerUiSkin.Apply(training);
            Assert.That(selectedCardFront.Find("MainFrame").GetComponent<Image>().sprite.name,
                Is.EqualTo("PlayerCard_Front_Reference"));
            Assert.That(selectedCardFront.Find("Name").GetComponent<Text>().text, Is.EqualTo("훈련선수"));
            Assert.That(selectedCardFront.Find("StatsPanel").GetComponent<Image>().color,
                Is.EqualTo(new Color32(10, 10, 12, 255)));
            Assert.That(selectedCardFront.Find("Ability0").GetComponent<Text>().color, Is.EqualTo(Color.white));

            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void OwnerPowerUpView_훈련비용과차단사유를표시하고확인후에만실행한다()
        {
            var card = new OwnerCollectionCardSnapshot("training-card", "person", "훈련선수", 2025,
                PlayerPosition.Shortstop, 7, PlayerCardEdition.Normal, 1, 1, false, false);
            var target = new OwnerCardTrainingTargetSnapshot(card, new[]
            {
                new OwnerCardTrainingProgramSnapshot("available", "교타력", default, 58, 61, 1, 12, true, ""),
                new OwnerCardTrainingProgramSnapshot("blocked", "장타력", default, 70, 70, 0, 12, false, "훈련 상한 도달")
            });
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            try
            {
                var scout = new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0");
                var enhancement = new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0);
                view.Bind(new OwnerPowerUpSnapshot(scout, new OwnerCardTrainingScreenSnapshot(new[] { target }, 100), enhancement));
                view.ShowRoute(OwnerNavigationRoutes.PowerUpTraining);
                Transform workspace = _root.transform.Find("OwnerPowerUpWorkspace");
                Transform programs = workspace.Find("PowerUpPanel/ContentSafeRect/TrainingContent/TrainingPrograms/ContentSafeRect");
                Transform list = programs.Find("ProgramScroll/Viewport/Content");
                Assert.That(list.Find("Program_available/GrowthPreview").GetComponent<Text>().text, Does.Contain("58 → 59"));
                Assert.That(list.Find("Program_available/TrainingCost").GetComponent<Text>().text, Does.Contain("12"));
                Assert.That(list.Find("Program_blocked/TrainingCost").GetComponent<Text>().text, Is.EqualTo("훈련 상한 도달"));
                var execute = programs.Find("TrainingExecute").GetComponent<Button>();
                int requests = 0;
                view.TrainingRequested += (cardId, programId) =>
                {
                    Assert.That(cardId, Is.EqualTo(card.CardId));
                    Assert.That(programId, Is.EqualTo("available"));
                    requests++;
                };
                execute.onClick.Invoke();
                Assert.That(requests, Is.Zero);
                workspace.Find("PowerUpConfirmation/ConfirmationCard/ConfirmationActions/Confirm")
                    .GetComponent<Button>().onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1));
                list.Find("Program_blocked").GetComponent<Button>().onClick.Invoke();
                Assert.That(execute.interactable, Is.False);
                view.Bind(new OwnerPowerUpSnapshot(scout,
                    new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0), enhancement));
                Assert.That(list.childCount, Is.Zero);
                Assert.That(execute.interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void OwnerPowerUpView_스카우터실루엣과영입확정경계를유지한다()
        {
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(new[]
                {
                    new OwnerScoutProductSnapshot("scout", "일반", "전체", "SP 100", true, "", 1,
                        40, 100, 10, 7, Array.Empty<OwnerScoutProbabilitySnapshot>())
                }, "SP 100"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(Array.Empty<OwnerEnhancementSaleTargetSnapshot>(), 0)));
            Transform workspace = _root.transform.Find("OwnerPowerUpWorkspace");
            Transform board = workspace.Find("PowerUpPanel/ContentSafeRect/ScoutContent/ScoutReference");
            RawImage portrait = board.Find("ScoutPortrait").GetComponent<RawImage>();
            Assert.That(portrait.texture, Is.Not.Null);
            Assert.That(portrait.texture.name, Is.EqualTo("OwnerScout_Silhouette_V1"));
            Assert.That(board.Find("SelectScout"), Is.Null);
            Assert.That(board.Find("ScoutSelectionShade"), Is.Null);
            board.Find("ScoutPolicy").GetComponent<Button>().onClick.Invoke();
            Transform policy = board.Find("ScoutPolicyShade/ScoutPolicyWindow");
            Assert.That(policy.gameObject.activeInHierarchy, Is.True);
            Assert.That(policy.Find("PolicyScoutPortrait").GetComponent<RawImage>().texture.name,
                Is.EqualTo("OwnerScout_Silhouette_V1"));
            Assert.That(policy.Find("PolicyOptions/PolicyChoice0"), Is.Not.Null);
            policy.Find("CancelPolicy").GetComponent<Button>().onClick.Invoke();
            Assert.That(board.Find("ScoutPolicyShade").gameObject.activeSelf, Is.False);
            int requests = 0;
            view.ScoutPurchaseRequested += id => { Assert.That(id, Is.EqualTo("scout")); requests++; };
            board.Find("ScoutPurchase").GetComponent<Button>().onClick.Invoke();
            Assert.That(requests, Is.Zero);
            workspace.Find("PowerUpConfirmation/ConfirmationCard/ConfirmationActions/Confirm")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            view.ShowScoutReveal(ShopPurchaseResult.Success(new[] { new ShopGrantedItem("test", "검증선수", "일반", true) }));
            Assert.That(board.Find("ScoutCards/CardSlot0/GrantedName").GetComponent<Text>().text, Does.Contain("검증선수"));
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void OwnerPowerUpView_등록후확정해야강화하고등록취소하면실행을차단한다()
        {
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            var card = new OwnerCollectionCardSnapshot("card", "person", "검증선수", 2025,
                PlayerPosition.Shortstop, 7, PlayerCardEdition.Normal, 0, 1, false, false);
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(new[]
                {
                    new OwnerEnhancementSaleTargetSnapshot(card,
                        new CardEnhancementPreview(0, 1, 1, CardEnhancementResult.Enhanced),
                        Array.Empty<CardSalePreview>())
                }, 0)));
            view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);
            Transform workspace = _root.transform.Find("OwnerPowerUpWorkspace");
            Transform board = workspace.Find("PowerUpPanel/ContentSafeRect/EnhancementSaleContent/ReferenceReinforcement");
            Button enhance = board.Find("Enhance").GetComponent<Button>();
            int requests = 0;
            view.EnhancementRequested += id => { Assert.That(id, Is.EqualTo("card")); requests++; };
            Assert.That(enhance.interactable, Is.False);
            board.Find("RegistrationFrame/Register").GetComponent<Button>().onClick.Invoke();
            Assert.That(enhance.interactable, Is.True);
            enhance.onClick.Invoke();
            Assert.That(requests, Is.Zero);
            workspace.Find("PowerUpConfirmation/ConfirmationCard/ConfirmationActions/Confirm")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            board.Find("RegistrationFrame/Unregister").GetComponent<Button>().onClick.Invoke();
            Assert.That(enhance.interactable, Is.False);
            enhance.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void OwnerPowerUpView_합성목록카드를좌클릭하면즉시등록한다()
        {
            var card = new OwnerCollectionCardSnapshot("card", "person", "검증선수", 2025,
                PlayerPosition.Shortstop, 7, PlayerCardEdition.Normal, 0, 1, false, false);
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(new[]
                {
                    new OwnerEnhancementSaleTargetSnapshot(card,
                        new CardEnhancementPreview(0, 1, 1, CardEnhancementResult.Enhanced),
                        Array.Empty<CardSalePreview>())
                }, 0)));
            view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);

            Transform board = _root.transform.Find(
                "OwnerPowerUpWorkspace/PowerUpPanel/ContentSafeRect/EnhancementSaleContent/ReferenceReinforcement");
            PlayerMiniCardView inventoryCard = board.Find(
                "EnhancementInventory/Viewport/Content/Card_card").GetComponent<PlayerMiniCardView>();

            inventoryCard.GetComponent<Button>().onClick.Invoke();

            Assert.That(board.Find("Enhance").GetComponent<Button>().interactable, Is.True);
            Assert.That(board.Find("RegistrationFrame/RegistrationSlot0/TargetCard").gameObject.activeSelf, Is.True);
            Assert.That(board.Find("RegistrationFrame/RegistrationSlot1/MaterialCard").gameObject.activeSelf, Is.True);
            Assert.That(board.Find("EnhancementDetails").GetComponent<Text>().text, Does.Contain("+0 → +1"));
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void OwnerPowerUpView_합성카드에초상화를표시하고우클릭상세를연다()
        {
            _root.AddComponent<Canvas>();
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.transform.SetParent(_root.transform, false);
            var card = new OwnerCollectionCardSnapshot("card-face", "person-face", "얼굴검증선수", 2025,
                PlayerPosition.Shortstop, 7, PlayerCardEdition.Normal, 0, 1, false, false);
            UI_Scene_OwnerPowerUp view = UI_Scene_OwnerPowerUp.CreateRuntime(_root.GetComponent<RectTransform>());
            view.Bind(new OwnerPowerUpSnapshot(
                new OwnerScoutScreenSnapshot(Array.Empty<OwnerScoutProductSnapshot>(), "SP 0"),
                new OwnerCardTrainingScreenSnapshot(Array.Empty<OwnerCardTrainingTargetSnapshot>(), 0),
                new OwnerEnhancementSaleScreenSnapshot(new[]
                {
                    new OwnerEnhancementSaleTargetSnapshot(card,
                        new CardEnhancementPreview(0, 1, 1, CardEnhancementResult.Enhanced),
                        Array.Empty<CardSalePreview>())
                }, 0)));
            view.ShowRoute(OwnerNavigationRoutes.PowerUpEnhancementSale);

            Transform board = _root.transform.Find(
                "OwnerPowerUpWorkspace/PowerUpPanel/ContentSafeRect/EnhancementSaleContent/ReferenceReinforcement");
            PlayerMiniCardView inventoryCard = board.Find(
                "EnhancementInventory/Viewport/Content/Card_card-face").GetComponent<PlayerMiniCardView>();
            Assert.That(inventoryCard.transform.Find("Portrait").GetComponent<Image>().sprite, Is.Not.Null);

            var rightClick = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Right
            };
            inventoryCard.OnPointerClick(rightClick);
            Transform popup = _root.transform.Find(nameof(UI_Popup_OwnerPlayerCard));
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.Find("CardDetail/Front/Name").GetComponent<Text>().text, Is.EqualTo("얼굴검증선수"));

            board.Find("RegistrationFrame/Register").GetComponent<Button>().onClick.Invoke();
            PlayerMiniCardView targetCard = board.Find(
                "RegistrationFrame/RegistrationSlot0/TargetCard").GetComponent<PlayerMiniCardView>();
            PlayerMiniCardView materialCard = board.Find(
                "RegistrationFrame/RegistrationSlot1/MaterialCard").GetComponent<PlayerMiniCardView>();
            Assert.That(targetCard.transform.Find("Portrait").GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(materialCard.transform.Find("Portrait").GetComponent<Image>().sprite, Is.Not.Null);
            targetCard.OnPointerClick(rightClick);
            Assert.That(_root.transform.Find(nameof(UI_Popup_OwnerPlayerCard)), Is.Not.Null);
            materialCard.OnPointerClick(rightClick);
            Assert.That(_root.transform.Find(nameof(UI_Popup_OwnerPlayerCard)), Is.Not.Null);

            _root.transform.Find(nameof(UI_Popup_OwnerPlayerCard))
                .GetComponent<UI_Popup_OwnerPlayerCard>().Close();
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void ExpansionWorkspaceCoordinator_서포트카드는레퍼런스편성UI와잠금사유를표시한다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            shell.BindProfile(OwnerModeUiProfileFactory.Create());
            var coordinator = shell.gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            coordinator.Initialize(shell);

            Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.RosterSupportCards), Is.True);

            Transform workspace = shell.MainWorkspaceHost.Find("OwnerSupportCardsWorkspace");
            Transform subTabs = shell.transform.Find("ContextHeader/SubTabs");
            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.gameObject.activeSelf, Is.True);
            Assert.That(workspace.Find("SelectedCards/SelectedSlot0/Label").GetComponent<Text>().text,
                Is.EqualTo("EMPTY"));
            Assert.That(workspace.Find("CardInformation/LockedAction").GetComponent<Button>().interactable,
                Is.False);
            Assert.That(workspace.Find("CardInformation/Message").GetComponent<Text>().text,
                Does.Contain("저장 Command가 없습니다"));
            Assert.That(subTabs.childCount, Is.EqualTo(4));
            Assert.That(subTabs.GetChild(0).Find("Label").GetComponent<Text>().text, Is.EqualTo("선수 오더"));
            Assert.That(subTabs.GetChild(1).Find("Label").GetComponent<Text>().text, Is.EqualTo("작전 카드"));
            Assert.That(subTabs.GetChild(2).Find("Label").GetComponent<Text>().text, Is.EqualTo("서포트 카드"));
            Assert.That(subTabs.GetChild(3).Find("Label").GetComponent<Text>().text, Is.EqualTo("팀 컬러"));
        }

        [Test]
        public void TeamColorView_상단장착과목록및상세영역을구분한다()
        {
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011)[0];
            var candidate = new OwnerTeamColorCandidateSnapshot(
                definition,
                definition.RequiredCount,
                new[] { "김수비", "이수비" },
                true);
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋",
                new[] { definition.TeamColorId, null },
                new[] { candidate });
            UI_Scene_OwnerTeamColor view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);

            view.Bind(snapshot);

            Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
            Assert.That(workspace.Find("EquippedSlots"), Is.Not.Null);
            Assert.That(workspace.Find("CandidateList"), Is.Not.Null);
            Assert.That(workspace.Find("Actions"), Is.Not.Null);
            Assert.That(workspace.Find("DetailPanel"), Is.Not.Null);
            Assert.That(workspace.Find("EquippedSlots/ContentSafeRect/Slot0/Label").GetComponent<Text>().text,
                Does.Contain(definition.DisplayName));
            Assert.That(workspace.Find("DetailPanel").GetComponent<RectTransform>().anchorMax.y,
                Is.LessThan(workspace.Find("EquippedSlots").GetComponent<RectTransform>().anchorMin.y));
        }

        [Test]
        public void TeamColorView_현재발동중인두슬롯효과를정보영역에합산표시한다()
        {
            TeamColorStatBonus noBonus = TeamColorStatBonus.Create();
            var first = new TeamColorDefinition(
                "ACTIVE_HITTER",
                TeamColorFamily.HitterProfile,
                1,
                TeamColorStatBonus.Create(
                    new AbilityBonus(PlayerAbility.Contact, 2),
                    new AbilityBonus(PlayerAbility.Power, 1)),
                noBonus,
                displayName: "정교한 타선");
            var second = new TeamColorDefinition(
                "ACTIVE_COMMON",
                TeamColorFamily.Generation,
                1,
                TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Contact, 1)),
                TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Control, 3)),
                displayName: "안정된 세대");
            var inactive = new TeamColorDefinition(
                "INACTIVE_PITCHER",
                TeamColorFamily.PitcherProfile,
                2,
                noBonus,
                TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Velocity, 9)),
                displayName: "미발동 투수진");
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋",
                new[] { first.TeamColorId, second.TeamColorId },
                new[]
                {
                    new OwnerTeamColorCandidateSnapshot(first, 1, new[] { "김타자" }, true),
                    new OwnerTeamColorCandidateSnapshot(second, 1, new[] { "이선수" }, true),
                    new OwnerTeamColorCandidateSnapshot(inactive, 1, new[] { "박투수" }, false)
                });
            UI_Scene_OwnerTeamColor view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);

            view.Bind(snapshot);

            Text detail = view.transform.Find("OwnerTeamColorWorkspace/DetailPanel/ContentSafeRect/EffectScroll/Viewport/Content/Description").GetComponent<Text>();
            Assert.That(detail.text, Does.Contain("현재 활성 효과 2개"));
            Assert.That(detail.text, Does.Contain("정교한 타선 + 안정된 세대"));
            Assert.That(detail.text, Does.Contain("컨택 +3"));
            Assert.That(detail.text, Does.Contain("장타 +1"));
            Assert.That(detail.text, Does.Contain("제구 +3"));
            Assert.That(detail.text, Does.Not.Contain("구속 +9"));
        }

        [Test]
        public void TeamColorSummary_역할의모든능력치가같이오르면올스탯으로축약한다()
        {
            var first = new TeamColorDefinition(
                "ALL_STATS_FIRST",
                TeamColorFamily.Year,
                1,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 5),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2),
                originYear: 2025,
                displayName: "같은 계절의 시작");
            var second = new TeamColorDefinition(
                "ALL_STATS_SECOND",
                TeamColorFamily.Generation,
                1,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 3),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 3),
                displayName: "이어지는 유니폼");
            var candidates = new[]
            {
                new OwnerTeamColorCandidateSnapshot(first, 1, new[] { "김선수" }, true),
                new OwnerTeamColorCandidateSnapshot(second, 1, new[] { "이선수" }, true)
            };

            string result = OwnerDugoutLoadoutPresentationBuilder.DescribeActiveTeamColorEffects(
                new[] { first.TeamColorId, second.TeamColorId },
                candidates);

            Assert.That(result, Does.Contain("야수: 올 스탯 +8"));
            Assert.That(result, Does.Contain("투수: 올 스탯 +5"));
            Assert.That(result, Does.Not.Contain("야수 체력"));
            Assert.That(result, Does.Not.Contain("투수 체력"));
        }

        [Test]
        public void TeamColorDetail_합계대신타자와투수1명당효과를표시한다()
        {
            var definition = new TeamColorDefinition(
                "PER_PLAYER_EFFECT",
                TeamColorFamily.Year,
                25,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 10),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 7),
                originYear: 2004);

            string result = OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(definition);

            Assert.That(result, Does.Contain("타자 1명당 전체 능력치 +10"));
            Assert.That(result, Does.Contain("투수 1명당 전체 능력치 +7"));
            Assert.That(result, Does.Not.Contain("보너스 합"));
            Assert.That(result, Does.Not.Contain("60"));
            Assert.That(result, Does.Not.Contain("42"));
        }

        [Test]
        public void TeamColorDetail_복합효과는능력치별상승량을표시한다()
        {
            var definition = new TeamColorDefinition(
                "MIXED_PER_PLAYER_EFFECT",
                TeamColorFamily.Generation,
                6,
                TeamColorStatBonus.Create(
                    new AbilityBonus(PlayerAbility.Contact, 2),
                    new AbilityBonus(PlayerAbility.Power, 1)),
                TeamColorStatBonus.Create(new AbilityBonus(PlayerAbility.Control, 3)));

            string result = OwnerDugoutLoadoutPresentationBuilder.DescribeTeamColorEffect(definition);

            Assert.That(result, Does.Contain("타자 1명당 컨택 +2 · 장타 +1"));
            Assert.That(result, Does.Contain("투수 1명당 제구 +3"));
        }

        [Test]
        public void TeamColorCandidate_표시명이없어도내부키를노출하지않는다()
        {
            TeamColorDefinition definition = new TeamColorDefinition(
                "Year:2020:25",
                TeamColorFamily.Year,
                25,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 2),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 2),
                originYear: 2020);

            var candidate = new OwnerTeamColorCandidateSnapshot(
                definition,
                12,
                new[] { "김선수" },
                false);

            Assert.That(candidate.Name, Is.EqualTo("동시대의 야구"));
            Assert.That(candidate.Name, Does.Not.Contain(definition.TeamColorId));
            Assert.That(candidate.Grade, Is.EqualTo("B"));
        }

        [Test]
        public void TeamColorView_카드에등급을표시하고내부키를숨긴다()
        {
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011)[0];
            var candidate = new OwnerTeamColorCandidateSnapshot(
                definition,
                definition.RequiredCount,
                new[] { "김수비", "이수비" },
                true);
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋",
                new[] { definition.TeamColorId, null },
                new[] { candidate });
            UI_Scene_OwnerTeamColor view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);

            view.Bind(snapshot);

            Transform workspace = view.transform.Find("OwnerTeamColorWorkspace");
            Text candidateGrade = workspace.Find("CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate0/Grade").GetComponent<Text>();
            Text activeGrade = workspace.Find("EquippedSlots/ContentSafeRect/Slot0/Grade").GetComponent<Text>();
            Assert.That(candidateGrade.text, Is.EqualTo(candidate.Grade));
            Assert.That(activeGrade.text, Is.EqualTo(candidate.Grade));
            foreach (Text text in workspace.GetComponentsInChildren<Text>(true))
                Assert.That(text.text, Does.Not.Contain(definition.TeamColorId));
        }

        [Test]
        public void TeamColorView_플레이트실물영역과등급홈을기준으로카드를배치한다()
        {
            TeamColorDefinition definition = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011)[0];
            var candidate = new OwnerTeamColorCandidateSnapshot(
                definition,
                definition.RequiredCount,
                new[] { "김수비", "이수비" },
                true);
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋",
                new[] { definition.TeamColorId, null },
                new[] { candidate });
            UI_Scene_OwnerTeamColor view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);

            view.Bind(snapshot);

            Transform card = view.transform.Find(
                "OwnerTeamColorWorkspace/CandidateList/ContentSafeRect/Scroll/Viewport/Content/Candidate0");
            RawImage artwork = card.Find("Artwork").GetComponent<RawImage>();
            RectTransform grade = card.Find("Grade").GetComponent<RectTransform>();
            RectTransform title = card.Find("Label").GetComponent<RectTransform>();
            RectTransform meta = card.Find("Meta").GetComponent<RectTransform>();
            RectTransform cardRect = card.GetComponent<RectTransform>();

            Assert.That(card.GetComponent<Image>().sprite, Is.Null);
            Assert.That(artwork.texture, Is.Not.Null);
            Assert.That(artwork.uvRect.y, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(artwork.uvRect.height, Is.EqualTo(0.50f).Within(0.001f));
            Assert.That((grade.anchorMin.x + grade.anchorMax.x) * 0.5f, Is.EqualTo(0.192f).Within(0.001f));
            Assert.That(grade.anchorMin.y + grade.anchorMax.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(title.anchorMin.x, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(meta.anchorMin.x, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(cardRect.anchorMax.y - cardRect.anchorMin.y, Is.GreaterThan(0.9f));
        }

        [Test]
        public void TeamColorView_공통타자투수효과에맞는서로다른플레이트를사용한다()
        {
            TeamColorStatBonus noBonus = TeamColorStatBonus.Create();
            var common = new TeamColorDefinition(
                "VISUAL_COMMON",
                TeamColorFamily.Generation,
                1,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1),
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 1));
            var hitter = new TeamColorDefinition(
                "VISUAL_HITTER",
                TeamColorFamily.HitterProfile,
                1,
                TeamColorStatBonus.AllForRole(PlayerRole.Hitter, 1),
                noBonus);
            var pitcher = new TeamColorDefinition(
                "VISUAL_PITCHER",
                TeamColorFamily.PitcherProfile,
                1,
                noBonus,
                TeamColorStatBonus.AllForRole(PlayerRole.Pitcher, 1));
            var snapshot = new OwnerTeamColorSnapshot(
                "기본 프리셋",
                new string[] { null, null },
                new[]
                {
                    new OwnerTeamColorCandidateSnapshot(common, 1, new[] { "공통 선수" }, true),
                    new OwnerTeamColorCandidateSnapshot(hitter, 1, new[] { "타자 선수" }, true),
                    new OwnerTeamColorCandidateSnapshot(pitcher, 1, new[] { "투수 선수" }, true)
                });
            UI_Scene_OwnerTeamColor view = UI_Scene_OwnerTeamColor.CreateRuntime(_root.transform);

            view.Bind(snapshot);

            Transform content = view.transform.Find("OwnerTeamColorWorkspace/CandidateList/ContentSafeRect/Scroll/Viewport/Content");
            Assert.That(content.Find("Candidate0/Artwork").GetComponent<RawImage>().texture.name,
                Is.EqualTo("team_color_card_plate_common_v2"));
            Assert.That(content.Find("Candidate1/Artwork").GetComponent<RawImage>().texture.name,
                Is.EqualTo("team_color_card_plate_hitter_v2"));
            Assert.That(content.Find("Candidate2/Artwork").GetComponent<RawImage>().texture.name,
                Is.EqualTo("team_color_card_plate_pitcher_v2"));
        }

        [Test]
        public void ExpansionWorkspaceCoordinator_덕아웃은데이터없이열리고이탈시선택창을닫는다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            var coordinator = shell.gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            coordinator.Initialize(shell);

            Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.DugoutLineupNotes), Is.True);
            Transform workspace = shell.MainWorkspaceHost.Find("OwnerDugoutWorkspace");
            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferenceCanvas));
            Assert.That(workspace.Find("DugoutBoard/PolicyPanel").GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferencePanel));
            Assert.That(workspace.Find("DugoutBoard/CardSlots/SummaryCard0").GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferencePanel));
            Assert.That(workspace.Find("Confirm").GetComponent<CareerUiVisualElement>().Role,
                Is.EqualTo(CareerUiVisualRole.FlatSurface));
            Assert.That(shell.MainWorkspaceHost.Find("OwnerRosterLineupWorkspace"), Is.Null);
            Assert.That(
                workspace.Find("DugoutBoard/StaffColumn/Manager/StaffCard/Portrait")
                    .GetComponent<UnityEngine.UI.Image>().sprite,
                Is.Not.Null);
            Assert.That(
                workspace.Find("DugoutBoard/StaffColumn/HeadCoach/StaffCard/Portrait")
                    .GetComponent<UnityEngine.UI.Image>().sprite,
                Is.Not.Null);
            workspace.Find("DugoutBoard/StaffColumn/Manager/Select").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Transform overlay = workspace.Find("StaffSelectionOverlay");
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(workspace.Find("Confirm").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);

            Transform policySteps = workspace.Find("DugoutBoard/PolicyPanel/PolicyRow0/PolicySteps");
            Assert.That(policySteps.GetComponentsInChildren<Slider>(true), Is.Empty);
            Assert.That(policySteps.childCount, Is.EqualTo(5));
            Assert.That(policySteps.Find("Step0").GetComponent<Image>().sprite, Is.Null);
            policySteps.Find("Step4").GetComponent<Button>().onClick.Invoke();
            Assert.That(policySteps.Find("Step4").GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferenceAccent));
            workspace.Find("Cancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(policySteps.Find("Step2").GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferenceAccent));
            Assert.That(policySteps.Find("Step4").GetComponent<Image>().color,
                Is.EqualTo(CareerUiTheme.ReferenceButton));

            coordinator.HideAll();
            Assert.That(workspace.gameObject.activeSelf, Is.False);
            Assert.That(overlay.gameObject.activeSelf, Is.False);
            Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.DugoutLineupNotes), Is.True);
            Assert.That(workspace.gameObject.activeSelf, Is.True);
            Assert.That(overlay.gameObject.activeSelf, Is.False);
        }

        private static OwnerClubOperationSnapshot CreateClubSnapshot()
        {
            return new OwnerClubOperationSnapshot(
                2,
                15_000,
                7_000_000L,
                true,
                string.Empty,
                62.5d,
                71d,
                12_340,
                null,
                TicketPriceTier.Premium,
                new[]
                {
                    new OwnerFacilitySnapshot(
                        FacilityType.ScoutingCenter, 1, 3, 1_200_000L, true, string.Empty,
                        weeklyScoutingPointProduction: 25, scoutingPointStorageCapacity: 250),
                    new OwnerFacilitySnapshot(
                        FacilityType.TrainingCenter, 1, 3, 1_200_000L, true, string.Empty,
                        weeklyDevelopmentPointProduction: 12, developmentPointStorageCapacity: 120),
                    new OwnerFacilitySnapshot(
                        FacilityType.RecoveryCenter, 1, 3, 1_100_000L, false, "리그 승격 필요",
                        conditionRecoveryEfficiencyModifier: 0.05d),
                    new OwnerFacilitySnapshot(
                        FacilityType.DataAnalysisCenter, 1, 3, 1_100_000L, true, string.Empty,
                        scoutingConfidenceModifier: 0.03d),
                    new OwnerFacilitySnapshot(
                        FacilityType.TacticLab, 1, 3, 1_000_000L, true, string.Empty,
                        tacticResearchEfficiencyModifier: 0.05d),
                    new OwnerFacilitySnapshot(
                        FacilityType.FanShop, 1, 3, 850_000L, true, string.Empty,
                        fanShopRevenuePerAttendee: 700L, fanShopPopularityRetention: 0.03d)
                },
                new OwnerFinanceSnapshot(1_500_000L, 700_000L, 25, 12, 2, 23_000L),
                new OwnerFinanceSnapshot(18_000_000L, 8_200_000L, 225, 108, 18, 210_000L));
        }
    }
}

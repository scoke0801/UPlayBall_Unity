using System;
using Baseball.Core.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단 운영과 Condition 화면이 Resolver Snapshot만 표시하고 SharedGameShell에 합성되는지 검증한다.</summary>
    public sealed class OwnerManagementPresentationTests
    {
        private GameObject _root;

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
        public void ClubView_Bind후시설행과재무를표시하고Command의도만전달한다()
        {
            UI_Scene_OwnerClubOperations view = UI_Scene_OwnerClubOperations.CreateRuntime(_root.transform);
            OwnerClubOperationPresentationModel model =
                OwnerClubOperationPresentationBuilder.Build(CreateClubSnapshot());
            FacilityType? requestedFacility = null;
            TicketPriceTier? requestedTicket = null;
            int weekAdvanceRequests = 0;
            int saveRequests = 0;
            int loadRequests = 0;
            view.FacilityUpgradeRequested += type => requestedFacility = type;
            view.TicketPolicyRequested += tier => requestedTicket = tier;
            view.WeekAdvanceRequested += () => weekAdvanceRequests++;
            view.SaveRequested += () => saveRequests++;
            view.LoadRequested += () => loadRequests++;

            view.Bind(model);

            Transform facility = view.transform.Find(
                "FacilityPanel/ContentSafeRect/FacilityList/Viewport/Content/Facility_ScoutingCenter");
            Assert.That(facility, Is.Not.Null);
            Assert.That(facility.Find("EffectPreview").GetComponent<Text>().text, Does.Contain("스카우트 포인트 +25"));
            Assert.That(view.GetComponent<Image>().sprite, Is.Not.Null);
            facility.Find("Upgrade").GetComponent<Button>().onClick.Invoke();
            view.transform.Find("ClubSummaryPanel/ContentSafeRect/Ticket_Cheap").GetComponent<Button>().onClick.Invoke();
            view.transform.Find("FacilityPanel/ContentSafeRect/AdvanceWeek").GetComponent<Button>().onClick.Invoke();
            view.transform.Find("FacilityPanel/ContentSafeRect/Save").GetComponent<Button>().onClick.Invoke();
            view.transform.Find("FacilityPanel/ContentSafeRect/Load").GetComponent<Button>().onClick.Invoke();

            Assert.That(requestedFacility, Is.EqualTo(FacilityType.ScoutingCenter));
            Assert.That(requestedTicket, Is.EqualTo(TicketPriceTier.Cheap));
            Assert.That(weekAdvanceRequests, Is.EqualTo(1));
            Assert.That(saveRequests, Is.EqualTo(1));
            Assert.That(loadRequests, Is.EqualTo(1));
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
        public void ExpansionWorkspaceCoordinator_덕아웃은데이터없이열리고이탈시선택창을닫는다()
        {
            SharedGameShellView shell = SharedGameShellView.CreateRuntime(_root.transform);
            var coordinator = shell.gameObject.AddComponent<OwnerExpansionWorkspaceCoordinator>();
            coordinator.Initialize(shell);

            Assert.That(coordinator.TryShowRoute(OwnerNavigationRoutes.DugoutLineupNotes), Is.True);
            Transform workspace = shell.MainWorkspaceHost.Find("OwnerDugoutWorkspace");
            Assert.That(workspace, Is.Not.Null);
            Assert.That(shell.MainWorkspaceHost.Find("OwnerRosterLineupWorkspace"), Is.Null);
            workspace.Find("DugoutBoard/StaffColumn/Manager/Select").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Transform overlay = workspace.Find("StaffSelectionOverlay");
            Assert.That(overlay.gameObject.activeSelf, Is.True);
            Assert.That(workspace.Find("Confirm").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);

            var slider = workspace.Find("DugoutBoard/PolicyPanel/PolicyRow0/PolicySlider")
                .GetComponent<UnityEngine.UI.Slider>();
            slider.value = 4f;
            workspace.Find("Cancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(slider.value, Is.EqualTo(2f));

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

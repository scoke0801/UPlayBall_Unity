using System;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>구단주 선수단 화면이 Resolver 근거를 표시하고 실제 프리셋 Command만 요청하는지 검증한다.</summary>
    public sealed class OwnerRosterLineupPresentationTests
    {
        [Test]
        public void Builder_로스터요약과Resolver경고를역할슬롯에표시한다()
        {
            var issue = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.OffPositionAssignment,
                LineupPresetIssueSeverity.Warning,
                LineupPresetAssignmentGroup.StartingLineup,
                0,
                "H0",
                "비주포지션 배치",
                2,
                1.35d);

            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(
                CreateSnapshot(new LineupPresetValidationResult("default", new[] { issue })));

            Assert.That(model.RosterSummaryText, Is.EqualTo("1군 25/25 · 야수 14/14 · 투수 11/11"));
            Assert.That(model.DefensiveLineup.Count, Is.EqualTo(9));
            Assert.That(model.Bench.Count, Is.EqualTo(5));
            Assert.That(model.StarterRotation.Count, Is.EqualTo(5));
            Assert.That(model.ReliefPitching.Count, Is.EqualTo(6));
            Assert.That(model.DefensiveLineup[0].WarningText, Does.Contain("컨디션 -2"));
            Assert.That(model.DefensiveLineup[0].WarningText, Does.Contain("실책 위험 증가"));
            Assert.That(model.DefensiveLineup[0].WarningText, Does.Not.Contain("1.35"));
            Assert.That(model.CanSave, Is.True, "Warning만 있는 배치는 저장할 수 있어야 합니다.");
            Assert.That(model.CreatePendingChangeMessage(), Does.Contain("배치 저장"));
            Assert.That(model.CreatePendingChangeMessage(), Does.Not.Contain("저장할 수 없습니다"));
        }

        [Test]
        public void Builder_레전드와커리어하이사용수및선택팀컬러를요약한다()
        {
            OwnerRosterLineupSnapshot source = CreateSnapshot(Valid("default"));
            LineupPresetState preset = source.Preset;
            var selectedPreset = new LineupPresetState(
                preset.PresetId,
                preset.Name,
                preset.StartingLineupSlots,
                preset.BattingOrderCardIds,
                preset.BenchPriorityCardIds,
                preset.StarterRotationCardIds,
                preset.BullpenAssignmentCardIds,
                preset.SetupPitcherCardId,
                preset.CloserPitcherCardId,
                new[] { "TC_A", "TC_B" },
                preset.DefaultTacticCardIds);
            var players = new[]
            {
                new OwnerRosterPlayerSnapshot("H0", "레전드 선수", 2024, PlayerPosition.Catcher,
                    PitcherRole.Starter, PlayerCardEdition.Legend, 10, RegistrationType.Domestic,
                    ActiveRosterRole.StartingCatcher, PlayerAvailabilityStatus.Available),
                new OwnerRosterPlayerSnapshot("H1", "커리어 하이 선수", 2023, PlayerPosition.FirstBase,
                    PitcherRole.Starter, PlayerCardEdition.CareerHigh, 10, RegistrationType.Domestic,
                    ActiveRosterRole.StartingFirstBase, PlayerAvailabilityStatus.Available),
                new OwnerRosterPlayerSnapshot("H2", "일반 선수", 2022, PlayerPosition.SecondBase,
                    PitcherRole.Starter, PlayerCardEdition.Normal, 5, RegistrationType.Domestic,
                    ActiveRosterRole.StartingSecondBase, PlayerAvailabilityStatus.Available)
            };
            var snapshot = new OwnerRosterLineupSnapshot(
                source.RosterStatus,
                players,
                new[] { new OwnerRosterPresetSnapshot(selectedPreset, Valid(selectedPreset.PresetId)) },
                selectedPreset.PresetId,
                new[]
                {
                    new OwnerLoadoutCandidateSnapshot("TC_A", "연도·구단 20명 · 효과 4"),
                    new OwnerLoadoutCandidateSnapshot("TC_B", "구단 20명 · 효과 3")
                },
                source.TacticCandidates);

            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(snapshot);

            Assert.That(model.RosterCardAndTeamColorSummaryText,
                Is.EqualTo("레전드 1장 · 커리어 하이 1장 · 합계 2/4\n" +
                           "팀컬러 · 연도·구단 20명 / 구단 20명"));
            Assert.That(model.RosterCardAndTeamColorSummaryText, Does.Not.Contain("기본 프리셋"));
        }

        [Test]
        public void Builder_내부역할전이와Preview용어를플레이어안내에노출하지않는다()
        {
            var issue = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.PitcherRoleMismatch,
                LineupPresetIssueSeverity.Incomplete,
                LineupPresetAssignmentGroup.Setup,
                0,
                "S0",
                "Starter->MiddleRelief",
                5);

            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(
                CreateSnapshot(new LineupPresetValidationResult("default", new[] { issue })));
            string message = model.CreatePendingChangeMessage(1);

            Assert.That(message, Does.Contain("1군 교체 1건"));
            Assert.That(message, Does.Contain("익숙하지 않은 투수 역할"));
            Assert.That(message, Does.Contain("컨디션 -5"));
            Assert.That(message, Does.Not.Contain("Starter"));
            Assert.That(message, Does.Not.Contain("->"));
            Assert.That(message, Does.Not.Contain("Preview"));
            Assert.That(message, Does.Not.Contain("검증"));
        }

        [Test]
        public void Builder_경고뒤에차단문제가있어도저장불가사유를먼저안내한다()
        {
            var warning = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.PitcherRoleMismatch,
                LineupPresetIssueSeverity.Warning,
                LineupPresetAssignmentGroup.StarterRotation,
                0,
                "S0",
                "Reliever->Starter",
                5);
            var blocker = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.TeamColorUnavailable,
                LineupPresetIssueSeverity.Incomplete,
                LineupPresetAssignmentGroup.TeamColor,
                0,
                "TC_OLD",
                "현재 선택 가능한 항목이 아닙니다.");

            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(
                CreateSnapshot(new LineupPresetValidationResult("default", new[] { warning, blocker })));
            string message = model.CreatePendingChangeMessage(1);

            Assert.That(model.CanSave, Is.False);
            Assert.That(message, Does.Contain("사용할 수 없는 팀컬러"));
            Assert.That(message, Does.Not.Contain("경고"), "요약은 첫 Warning보다 실제 차단 사유를 우선해야 합니다.");
            Assert.That(model.ValidationText.IndexOf("사용할 수 없는 팀컬러", StringComparison.Ordinal),
                Is.LessThan(model.ValidationText.IndexOf("경고", StringComparison.Ordinal)));
        }

        [Test]
        public void CommandBuilder_선택한두슬롯만교환하고나머지프리셋을보존한다()
        {
            LineupPresetState source = CreatePreset();

            LineupPresetState result = OwnerLineupPresetCommandBuilder.Swap(
                source, OwnerLineupSwapGroup.DefensiveLineup, 0, 1);

            Assert.That(result.StartingLineupSlots[0].Position, Is.EqualTo(PlayerPosition.Catcher));
            Assert.That(result.StartingLineupSlots[0].CardId, Is.EqualTo("H1"));
            Assert.That(result.StartingLineupSlots[1].Position, Is.EqualTo(PlayerPosition.FirstBase));
            Assert.That(result.StartingLineupSlots[1].CardId, Is.EqualTo("H0"));
            Assert.That(result.BattingOrderCardIds, Is.EqualTo(source.BattingOrderCardIds));
            Assert.That(result.TeamColorIds, Is.EqualTo(source.TeamColorIds));
        }

        [Test]
        public void CommandBuilder_미등록카드교체는수비와타순의동일선수를함께바꾼다()
        {
            LineupPresetState source = CreatePreset();

            LineupPresetState result = OwnerLineupPresetCommandBuilder.ReplaceCard(source, "H0", "NEW");

            Assert.That(result.StartingLineupSlots[0].CardId, Is.EqualTo("NEW"));
            Assert.That(result.BattingOrderCardIds[0], Is.EqualTo("NEW"));
            Assert.That(result.BenchPriorityCardIds, Is.EqualTo(source.BenchPriorityCardIds));
            Assert.That(result.TeamColorIds, Is.EqualTo(source.TeamColorIds));
        }

        [Test]
        public void CommandBuilder_주전슬롯에벤치선수를놓으면두선수의전체역할을맞바꾼다()
        {
            LineupPresetState source = CreatePreset();

            LineupPresetState result = OwnerLineupPresetCommandBuilder.AssignCard(
                source,
                OwnerLineupSwapGroup.BattingOrder,
                0,
                "B0");

            Assert.That(result.StartingLineupSlots[0].CardId, Is.EqualTo("B0"));
            Assert.That(result.BattingOrderCardIds[0], Is.EqualTo("B0"));
            Assert.That(result.BenchPriorityCardIds[0], Is.EqualTo("H0"));
        }

        [Test]
        public void Builder_모든저장프리셋과각각의현재Validator상태를표시한다()
        {
            LineupPresetState selected = CreatePreset();
            LineupPresetState alternate = CopyPreset(selected, "alternate", "대체 프리셋");
            var invalidIssue = new LineupPresetValidationIssue(
                LineupPresetValidationIssueCode.CardUnavailable,
                LineupPresetIssueSeverity.Incomplete,
                LineupPresetAssignmentGroup.StartingLineup,
                0,
                "H0",
                "출전 불가");
            OwnerRosterLineupSnapshot snapshot = CreateSnapshot(
                new[]
                {
                    new OwnerRosterPresetSnapshot(
                        selected,
                        new LineupPresetValidationResult(selected.PresetId, Array.Empty<LineupPresetValidationIssue>())),
                    new OwnerRosterPresetSnapshot(
                        alternate,
                        new LineupPresetValidationResult(alternate.PresetId, new[] { invalidIssue }))
                },
                selected.PresetId);

            OwnerRosterLineupPresentationModel model = OwnerRosterLineupPresentationBuilder.Build(snapshot);

            Assert.That(model.Presets.Count, Is.EqualTo(2));
            Assert.That(model.Presets[0].IsSelected, Is.True);
            Assert.That(model.Presets[0].StatusText, Is.EqualTo("사용 가능"));
            Assert.That(model.Presets[1].StatusText, Is.EqualTo("수정 필요"));
        }

        [Test]
        public void CommandBuilder_TeamColor와Tactic은실제후보에서중복없이한슬롯만원자변경한다()
        {
            LineupPresetState source = CreatePreset();
            LineupPresetState firstColor = OwnerLineupPresetCommandBuilder.CycleTeamColor(
                source, 0, new[] { "TC_A", "TC_B", "TC_C" });
            LineupPresetState secondColor = OwnerLineupPresetCommandBuilder.CycleTeamColor(
                firstColor, 1, new[] { "TC_A", "TC_B", "TC_C" });
            LineupPresetState tactic = OwnerLineupPresetCommandBuilder.CycleTactic(
                secondColor, 0, new[] { "T0", "T1", "T2" });

            Assert.That(firstColor.TeamColorIds, Is.EqualTo(new[] { "TC_A", null }));
            Assert.That(secondColor.TeamColorIds, Is.EqualTo(new[] { "TC_A", "TC_B" }));
            Assert.That(tactic.DefaultTacticCardIds, Is.EqualTo(new[] { "T2", "T1" }));
            Assert.That(tactic.BattingOrderCardIds, Is.EqualTo(source.BattingOrderCardIds));
            Assert.That(tactic.StartingLineupSlots[0].CardId, Is.EqualTo(source.StartingLineupSlots[0].CardId));
        }

        [Test]
        public void CommandBuilder_빈TacticPreset도두슬롯을차례로선택할수있다()
        {
            LineupPresetState source = CreatePreset();
            var empty = new LineupPresetState(
                source.PresetId,
                source.Name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                source.TeamColorIds,
                Array.Empty<string>());

            LineupPresetState first = OwnerLineupPresetCommandBuilder.CycleTactic(
                empty, 0, new[] { "T0", "T1" });
            LineupPresetState completed = OwnerLineupPresetCommandBuilder.CycleTactic(
                first, 1, new[] { "T0", "T1" });

            Assert.That(first.DefaultTacticCardIds, Is.EqualTo(new[] { "T0" }));
            Assert.That(completed.DefaultTacticCardIds, Is.EqualTo(new[] { "T0", "T1" }));
        }

        [Test]
        public void View_배치편집에서같은구역두슬롯은Swap요청한다()
        {
            var root = new GameObject("OwnerRosterLineupTestRoot", typeof(RectTransform));
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(
                    shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(CreateSnapshot(
                    new LineupPresetValidationResult("default", Array.Empty<LineupPresetValidationIssue>()))));
                OwnerLineupSwapGroup? requestedGroup = null;
                int first = -1;
                int second = -1;
                view.SwapRequested += (group, firstIndex, secondIndex) =>
                {
                    requestedGroup = group;
                    first = firstIndex;
                    second = secondIndex;
                };

                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PlayerGroupTabs/PlacementEditMode")
                    .onClick.Invoke();
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0")
                    .onClick.Invoke();
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_1")
                    .onClick.Invoke();

                Assert.That(requestedGroup, Is.EqualTo(OwnerLineupSwapGroup.BattingOrder));
                Assert.That(first, Is.EqualTo(0));
                Assert.That(second, Is.EqualTo(1));
                view.SetFeedback("프리셋 적용 실패", true);
                Assert.That(shell.transform.Find(
                        "MainWorkspaceHost/OwnerRosterLineupWorkspace/RosterActions/ValidationMessages")
                    .GetComponent<Text>().text, Is.EqualTo("프리셋 적용 실패"));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_배치슬롯과보유선수를선택하면교체요청과명확한안내를낸다()
        {
            var root = new GameObject("OwnerRosterAssignmentTestRoot", typeof(RectTransform));
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(
                    shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                OwnerRosterLineupSnapshot source = CreateSnapshot(Valid("default"));
                var owned = new[]
                {
                    new OwnerCollectionCardSnapshot("H0", "P0", "가상 포수", 2026,
                        PlayerPosition.Catcher, 5, PlayerCardEdition.Normal, 0, 0, false, false),
                    new OwnerCollectionCardSnapshot("NEW", "P1", "교체 포수", 2025,
                        PlayerPosition.Catcher, 6, PlayerCardEdition.Normal, 0, 0, false, false)
                };
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(new OwnerRosterLineupSnapshot(
                    source.RosterStatus,
                    source.Players,
                    source.Presets,
                    source.Preset.PresetId,
                    source.TeamColorCandidates,
                    source.TacticCandidates,
                    owned)));
                OwnerLineupSwapGroup? requestedGroup = null;
                int requestedIndex = -1;
                string requestedCardId = null;
                int requestCount = 0;
                view.AssignmentRequested += (group, index, cardId) =>
                {
                    requestCount++;
                    requestedGroup = group;
                    requestedIndex = index;
                    requestedCardId = cardId;
                };

                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PlayerGroupTabs/PlacementEditMode")
                    .onClick.Invoke();
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0")
                    .onClick.Invoke();
                Text instruction = shell.transform.Find(
                        "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PlayerOrderStatusStrip/PreviewState")
                    .GetComponent<Text>();
                Assert.That(instruction.text, Does.Contain("교체할 보유 선수를 선택"));
                Array.Find(shell.GetComponentsInChildren<PlayerMiniCardView>(),
                    card => card.name.StartsWith("Owned_", StringComparison.Ordinal) && card.Model.PlayerId == "NEW")
                    .GetComponent<Button>().onClick.Invoke();

                Assert.That(requestedGroup, Is.EqualTo(OwnerLineupSwapGroup.BattingOrder));
                Assert.That(requestedIndex, Is.EqualTo(0));
                Assert.That(requestedCardId, Is.EqualTo("NEW"));

                view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(new OwnerRosterLineupSnapshot(
                    source.RosterStatus,
                    source.Players,
                    source.Presets,
                    source.Preset.PresetId,
                    source.TeamColorCandidates,
                    source.TacticCandidates,
                    owned)), "1군 교체 1건 검증 통과");
                Assert.That(FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/RosterActions/ConfirmLineupPreview")
                    .interactable, Is.True);
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_1")
                    .onClick.Invoke();
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/OwnedPlayerPanel/ContentSafeRect/RoleScroll/Viewport/Content/OwnedGrid/Owned_1")
                    .onClick.Invoke();

                Assert.That(requestCount, Is.EqualTo(2), "Preview 갱신 뒤에도 편집 모드가 유지되어야 합니다.");
                Assert.That(requestedIndex, Is.EqualTo(1));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Coordinator_투수탭편집Preview에서도선택한목록을유지한다()
        {
            var root = new GameObject("OwnerRosterCoordinatorTestRoot", typeof(RectTransform));
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                OwnerExpansionWorkspaceCoordinator coordinator =
                    root.AddComponent<OwnerExpansionWorkspaceCoordinator>();
                coordinator.Initialize(shell);
                OwnerRosterLineupSnapshot snapshot = CreateSnapshot(Valid("default"));
                coordinator.BindRosterLineup(snapshot);
                Assert.That(coordinator.TryShowRoute(OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId), Is.True);

                Transform lineupRoot = shell.transform.Find(
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard");
                FindButton(lineupRoot, "PlayerGroupTabs/PitcherTab").onClick.Invoke();
                const string StarterPath =
                    "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/StarterRotation_0";
                Transform starterBeforePreview = lineupRoot.Find(StarterPath);
                Assert.That(starterBeforePreview, Is.Not.Null);

                coordinator.BindRosterLineupPreview(OwnerRosterLineupPresentationBuilder.Build(snapshot), "변경 내용을 확인해 주세요.");

                Assert.That(lineupRoot.Find(StarterPath), Is.SameAs(starterBeforePreview),
                    "Preview마다 작은 카드 계층을 파괴·재생성하면 편집 입력이 지연됩니다.");
                Assert.That(lineupRoot.Find(
                    "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0"),
                    Is.Null);

                coordinator.BindRosterLineup(snapshot);
                Assert.That(coordinator.TryShowRoute(
                    OwnerExpansionWorkspaceCoordinator.RosterLineupRouteId), Is.True);
                Assert.That(lineupRoot.Find(StarterPath), Is.SameAs(starterBeforePreview),
                    "저장 후 같은 Route를 Refresh할 때 투수 탭과 카드 인스턴스를 유지해야 합니다.");
                Assert.That(lineupRoot.Find(
                    "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0"),
                    Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_상단Toolbar에서저장프리셋선택의도를전달한다()
        {
            var root = new GameObject("OwnerRosterLoadoutTestRoot", typeof(RectTransform));
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(
                    shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                LineupPresetState selected = CreatePreset();
                LineupPresetState alternate = CopyPreset(selected, "alternate", "대체 프리셋");
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(CreateSnapshot(
                    new[]
                    {
                        new OwnerRosterPresetSnapshot(selected, Valid(selected.PresetId)),
                        new OwnerRosterPresetSnapshot(alternate, Valid(alternate.PresetId))
                    },
                    selected.PresetId)));
                string selectedPresetId = null;
                view.PresetSelected += id => selectedPresetId = id;

                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PlayerGroupTabs/NextPresetButton")
                    .onClick.Invoke();

                Assert.That(selectedPresetId, Is.EqualTo("alternate"));
                Text summary = shell.transform.Find(
                        "MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard/PlayerGroupTabs/RosterRuleSummary")
                    .GetComponent<Text>();
                Assert.That(summary.text, Does.Contain("레전드 0장 · 커리어 하이 0장 · 합계 0/4"));
                Assert.That(summary.text, Does.Contain("팀컬러 · 선택 없음"));
                Assert.That(summary.text, Does.Not.Contain("기본 프리셋").And.Not.Contain("사용 가능"));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_연도구단필터를교차적용하고배치상태와포지션을구분한다()
        {
            var root = new GameObject("RosterFilterTest", typeof(RectTransform));
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                OwnerRosterLineupSnapshot source = CreateSnapshot(Valid("default"));
                var cards = new OwnerCollectionCardSnapshot[4];
                for (int i = 0; i < 4; i++)
                    cards[i] = new OwnerCollectionCardSnapshot(i == 0 ? "H0" : "owned" + i, "P" + i, "선수" + i,
                        i % 2 == 0 ? 2024 : 2023, PlayerPosition.Catcher, 5, PlayerCardEdition.Normal,
                        0, 0, false, false, teamDisplayName: i < 2 ? "대구 포지" : "청주 레이더스");
                var snapshot = new OwnerRosterLineupSnapshot(source.RosterStatus, source.Players, source.Presets,
                    source.Preset.PresetId, source.TeamColorCandidates, source.TacticCandidates, cards);
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(snapshot));
                Func<string, Dropdown> dropdown = name => Array.Find(shell.GetComponentsInChildren<Dropdown>(true), d => d.name == name);
                Func<PlayerMiniCardView[]> owned = () => Array.FindAll(shell.GetComponentsInChildren<PlayerMiniCardView>(), c => c.name.StartsWith("Owned_"));
                Assert.That(owned().Length, Is.EqualTo(4));
                dropdown("YearFilter").value = 1;
                Assert.That(owned().Length, Is.EqualTo(2));
                dropdown("TeamFilter").value = 1;
                Assert.That(owned().Length, Is.EqualTo(1));
                PlayerMiniCardView assigned = owned()[0];
                Assert.That(assigned.Model.PlayerId, Is.EqualTo("H0"));
                Sprite portrait = assigned.transform.Find("Portrait").GetComponent<Image>().sprite;
                Assert.That(portrait, Is.Not.Null);
                Assert.That(portrait.name, Does.Not.Contain("Silhouette"),
                    "Lineup 작은 카드도 선수 타입별 초상화를 표시해야 합니다.");
                Assert.That(assigned.transform.Find("AssignmentBadge/AssignmentLabel").GetComponent<Text>().text,
                    Does.StartWith("배치 중 · "));
                Assert.That(assigned.transform.Find("TeamEmblem").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(assigned.Model.EditionLabel, Is.Empty);
                Assert.That(assigned.Model.StatusLabel, Is.EqualTo("포수"));
                dropdown("YearFilter").value = 0;
                Assert.That(owned().Length, Is.EqualTo(2));
                dropdown("TeamFilter").value = 0;
                Assert.That(owned().Length, Is.EqualTo(4));
                PlayerMiniCardView unassigned = Array.Find(owned(), c => c.Model.PlayerId == "owned1");
                Assert.That(unassigned.Model.PositionLabel, Is.Empty);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void View_필터고정과교체스크롤유지및동일선수사용불가를검증한다(int width, int height)
        {
            var root = new GameObject("RosterScrollCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("RosterScrollCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.SetInspectorVisible(false);
                shell.SetActionBarVisible(false);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                OwnerRosterLineupSnapshot source = CreateSnapshot(Valid("default"));
                var cards = new OwnerCollectionCardSnapshot[36];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = new OwnerCollectionCardSnapshot(i == 0 ? "H0" : "owned" + i,
                        i == 1 ? "P0" : "P" + i, "동명이인 선수", 2024, PlayerPosition.Catcher, 5,
                        i == 1 ? PlayerCardEdition.CareerHigh : PlayerCardEdition.Normal, 0, 0, false, false);
                var snapshot = new OwnerRosterLineupSnapshot(source.RosterStatus, source.Players, source.Presets,
                    source.Preset.PresetId, source.TeamColorCandidates, source.TacticCandidates, cards);
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(snapshot));
                Transform board = shell.transform.Find("MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard");
                Transform safe = board.Find("OwnedPlayerPanel/ContentSafeRect");
                ScrollRect scroll = safe.Find("RoleScroll").GetComponent<ScrollRect>();
                RectTransform header = (RectTransform)safe.Find("OwnedPlayerHeader");
                Action layout = () =>
                {
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerRosterLineup).GetMethod("LateUpdate",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                    Canvas.ForceUpdateCanvases();
                };
                layout(); layout();
                Text rosterRuleSummary = board.Find("PlayerGroupTabs/RosterRuleSummary").GetComponent<Text>();
                RectTransform rosterRuleRect = rosterRuleSummary.rectTransform;
                Assert.That(rosterRuleSummary.preferredHeight, Is.LessThanOrEqualTo(rosterRuleRect.rect.height + 1f),
                    $"{width}px에서 특수 카드·팀컬러 요약이 잘려서는 안 됩니다.");
                var summaryBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(board, rosterRuleRect);
                Rect boardRect = board.GetComponent<RectTransform>().rect;
                Assert.That(summaryBounds.min.x, Is.GreaterThanOrEqualTo(boardRect.xMin - 1f));
                Assert.That(summaryBounds.min.y, Is.GreaterThanOrEqualTo(boardRect.yMin - 1f));
                Assert.That(summaryBounds.max.x, Is.LessThanOrEqualTo(boardRect.xMax + 1f));
                Assert.That(summaryBounds.max.y, Is.LessThanOrEqualTo(boardRect.yMax + 1f));
                Assert.That(header.Find("OriginFilters"), Is.Not.Null);
                Assert.That(header.IsChildOf(scroll.content), Is.False);
                Assert.That(scroll.content.childCount, Is.EqualTo(1), "카드 Grid만 스크롤해야 합니다.");
                var headerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, header);
                var scrollBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, scroll.transform);
                Assert.That(headerBounds.min.y, Is.GreaterThanOrEqualTo(scrollBounds.max.y - 1f));
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(40f));
                Transform duplicate = scroll.content.Find("OwnedGrid/Owned_1");
                Assert.That(duplicate.GetComponent<PlayerMiniCardView>().Model.PositionLabel, Is.Empty,
                    "동일 선수 교체 안내는 선택 시 표시하고 초상 위에 반복하지 않습니다.");
                Assert.That(scroll.content.Find("OwnedGrid/Owned_2").GetComponent<PlayerMiniCardView>().Model.PositionLabel,
                    Is.Empty, "동명이인을 막아서는 안 됩니다.");
                int requests = 0;
                view.AssignmentRequested += (_, __, ___) => requests++;
                FindButton(board, "PlayerGroupTabs/PlacementEditMode").onClick.Invoke();
                FindButton(board, "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0").onClick.Invoke();
                duplicate.GetComponent<Button>().onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1), "배치된 동일 선수의 다른 카드를 교체할 수 있어야 합니다.");
                FindButton(board, "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0").onClick.Invoke();
                scroll.content.Find("OwnedGrid/Owned_2").GetComponent<Button>().onClick.Invoke();
                Assert.That(requests, Is.EqualTo(2));
                scroll.verticalNormalizedPosition = 0.25f;
                Vector3 headerPosition = header.position;
                float offset = scroll.content.anchoredPosition.y;
                var preview = snapshot.CreatePreview(OwnerLineupPresetCommandBuilder.Swap(snapshot.Preset,
                    OwnerLineupSwapGroup.BattingOrder, 0, 1), Valid("default"));
                view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(preview), "교체 확인");
                layout();
                Assert.That(scroll.content.Find("OwnedGrid/Owned_1"), Is.SameAs(duplicate));
                Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(offset).Within(1f));
                Assert.That(header.position, Is.EqualTo(headerPosition));
                camera.Render();
                CapturePositionView(camera, target, System.IO.Path.Combine(Application.dataPath, "../RosterScreenshots/Hitters"), width, height);
                view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(snapshot.CreatePreview(
                    OwnerLineupPresetCommandBuilder.ReplaceCard(snapshot.Preset, "H0", "owned2"), Valid("default"))), "동일 선수 제외");
                Assert.That(duplicate.GetComponent<PlayerMiniCardView>().Model.PositionLabel, Is.Empty);
                Assert.That(duplicate.Find("AssignmentBadge") == null || !duplicate.Find("AssignmentBadge").gameObject.activeSelf, Is.True);
                FindButton(header, "OwnedPageControls/NextOwnedPage").onClick.Invoke();
                layout();
                Transform secondPage = scroll.content.Find("OwnedGrid/Owned_18");
                Assert.That(secondPage, Is.Not.Null);
                view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(preview), "교체 확인");
                Assert.That(scroll.content.Find("OwnedGrid/Owned_18"), Is.SameAs(secondPage));
                view.SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
                layout(); layout();
                Assert.That(header.Find("OriginFilters"), Is.Not.Null);
                Assert.That(scroll.content.childCount, Is.EqualTo(1));
                camera.Render();
                CapturePositionView(camera, target, System.IO.Path.Combine(Application.dataPath, "../RosterScreenshots/Pitchers"), width, height);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Button FindButton(Transform root, string path) => root.Find(path).GetComponent<Button>();

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        public void View_포지션선택은수비슬롯을교환하고타순과저장값을보존한다(int width, int height)
        {
            var root = new GameObject("PositionCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("PositionCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.SetInspectorVisible(false);
                shell.SetActionBarVisible(false);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(
                    shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                var players = new OwnerRosterPlayerSnapshot[9];
                for (int index = 0; index < players.Length; index++)
                    players[index] = new OwnerRosterPlayerSnapshot($"H{index}", "선수" + index, 2026,
                        (PlayerPosition)(index + 1), PitcherRole.Starter, PlayerCardEdition.Normal, 5,
                        RegistrationType.Domestic, ActiveRosterRole.StartingCatcher, PlayerAvailabilityStatus.Available);
                // 1번 타자가 수비 슬롯 8번에 있어도 타순 인덱스를 수비 인덱스로 오인하지 않아야 한다.
                LineupPresetState preset = OwnerLineupPresetCommandBuilder.Swap(CreatePreset(),
                    OwnerLineupSwapGroup.BattingOrder, 0, 8);
                var snapshot = new OwnerRosterLineupSnapshot(CreateValidRosterStatus(), players, preset, Valid("default"), string.Empty);
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(snapshot));
                int requests = 0;
                LineupPresetState candidate = null;
                view.SwapRequested += (group, first, second) =>
                {
                    requests++;
                    Assert.That(group, Is.EqualTo(OwnerLineupSwapGroup.DefensiveLineup));
                    Assert.That(first, Is.EqualTo(8));
                    Assert.That(second, Is.EqualTo(0));
                    candidate = OwnerLineupPresetCommandBuilder.Swap(preset, group, first, second);
                    view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(snapshot.CreatePreview(candidate, Valid("default"))), "배치 저장으로 확정하세요.");
                };
                Transform board = shell.transform.Find("MainWorkspaceHost/OwnerRosterLineupWorkspace/PlayerOrderBoard");
                string positionPath = "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/DefensivePositions/Position_0";
                FindButton(board, positionPath).onClick.Invoke();
                Button choose = Array.Find(board.GetComponentsInChildren<Button>(), b => b.name == "ChoosePosition_0");
                Button apply = Array.Find(board.GetComponentsInChildren<Button>(), b => b.name == "ApplyPosition");
                Assert.That(apply.interactable, Is.False);
                choose.onClick.Invoke();
                Assert.That(requests, Is.Zero, "선택만으로 변경안을 반영하지 않습니다.");
                Assert.That(apply.interactable, Is.True);
                Canvas.ForceUpdateCanvases();
                typeof(UI_Scene_OwnerRosterLineup).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                Canvas.ForceUpdateCanvases();
                RectTransform positionRect = FindButton(board, positionPath).GetComponent<RectTransform>();
                Assert.That(positionRect.rect.height, Is.GreaterThanOrEqualTo(24f));
                var actionCorners = new Vector3[4];
                apply.GetComponent<RectTransform>().GetWorldCorners(actionCorners);
                RectTransform analysisPanel = (RectTransform)board.Find("ConditionAnalysisPanel");
                Assert.That(analysisPanel.rect.Contains(analysisPanel.InverseTransformPoint(actionCorners[0])), Is.True,
                    "적용 버튼은 스크롤 위치와 관계없이 분석 패널 안에 있어야 합니다.");
                Assert.That(analysisPanel.rect.Contains(analysisPanel.InverseTransformPoint(actionCorners[2])), Is.True);
                string output = Environment.GetEnvironmentVariable("BASEBALL_POSITION_CAPTURE");
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, output, width, height);
                apply.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(candidate.BattingOrderCardIds, Is.EqualTo(preset.BattingOrderCardIds));
                Assert.That(candidate.StartingLineupSlots[0].CardId, Is.EqualTo("H8"));
                Assert.That(preset.StartingLineupSlots[0].CardId, Is.EqualTo("H0"));
                Assert.That(FindButton(board, positionPath).GetComponentInChildren<Text>().text, Does.Contain("포수"));
                FindButton(board, positionPath).onClick.Invoke();
                Assert.That(view.TryHandleCancel(), Is.True, "ESC는 열린 포지션 선택만 닫습니다.");
                Assert.That(FindButton(board.parent, "RosterActions/ConfirmLineupPreview").interactable, Is.True,
                    "포지션 선택 창 닫기는 기존 변경안을 버리지 않습니다.");
                Assert.That(requests, Is.EqualTo(1));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void View_편성보드의경고행동과하단저장및카드명찰을검증한다(int width, int height)
        {
            var root = new GameObject("RosterBoardCanvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("RosterBoardCamera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            UI_Scene_OwnerRosterLineup view = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = target;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                SharedGameShellView shell = SharedGameShellView.CreateRuntime(root.transform);
                shell.SetInspectorVisible(false);
                shell.SetActionBarVisible(false);
                view = UI_Scene_OwnerRosterLineup.CreateRuntime(shell.MainWorkspaceHost, shell.RightInspectorHost, shell.ContextActionBarHost);
                var players = new System.Collections.Generic.List<OwnerRosterPlayerSnapshot>();
                var cards = new System.Collections.Generic.List<OwnerCollectionCardSnapshot>();
                shell.BindProfile(OwnerModeUiProfileFactory.Create());
                shell.BindContext(new ShellContextModel("Owner.Roster.Lineup", "선수 오더"));
                shell.BindStatus(new ShellStatusModel("2008 시즌", "1주차", "루키 리그", "서울 웨이브스", "1위", "", new[]
                {
                    new ShellStatusSlotModel("money", "자금", "10억원"),
                    new ShellStatusSlotModel("scout", "스카우트 포인트", "10,000"),
                    new ShellStatusSlotModel("growth", "육성 포인트", "3,000"),
                    new ShellStatusSlotModel("roster", "1군", "25/25")
                }));
                string[] names = { "김민준", "이서준", "박도윤", "최시우", "정지호", "강하준", "조우진", "윤건우", "장선우" };
                for (int index = 0; index < 36; index++)
                {
                    string id = index < 9 ? "H" + index : index < 14 ? "B" + (index - 9) :
                        index < 19 ? "S" + (index - 14) : index < 23 ? "R" + (index - 19) :
                        index == 23 ? "SU" : index == 24 ? "CL" : "owned" + index;
                    PlayerPosition position = index >= 14 && index < 25 ? PlayerPosition.StartingPitcher : (PlayerPosition)(index % 9 + 1);
                    PlayerCardEdition edition = index % 5 == 0 ? PlayerCardEdition.CareerHigh :
                        index % 5 == 1 ? PlayerCardEdition.Legend : PlayerCardEdition.Normal;
                    string name = names[index % names.Length];
                    if (index < 25) players.Add(new OwnerRosterPlayerSnapshot(id, name, 2008, position,
                        PitcherRole.Starter, edition, 5 + index % 5, RegistrationType.Domestic,
                        ActiveRosterRole.StartingCatcher, PlayerAvailabilityStatus.Available, 80, 8, "양호"));
                    var ratings = new Baseball.Core.Growth.AbilityRatings(60 + index % 25);
                    ratings.AddClamped(Baseball.Core.Growth.PlayerAbility.Power, index % 3 * 10);
                    cards.Add(new OwnerCollectionCardSnapshot(id, "person" + index, name, 2008, position,
                        5 + index % 5, edition, 0, 0, false, false, abilities: ratings));
                }
                var issue = new LineupPresetValidationIssue(LineupPresetValidationIssueCode.OffPositionAssignment,
                    LineupPresetIssueSeverity.Warning, LineupPresetAssignmentGroup.StartingLineup, 0, "H0", "익숙하지 않은 수비 위치", 6, 1.35d);
                var snapshot = new OwnerRosterLineupSnapshot(CreateValidRosterStatus(), players,
                    new[] { new OwnerRosterPresetSnapshot(CreatePreset(), new LineupPresetValidationResult("default", new[] { issue })) },
                    "default", Array.Empty<OwnerLoadoutCandidateSnapshot>(), Array.Empty<OwnerLoadoutCandidateSnapshot>(), cards);
                view.Bind(OwnerRosterLineupPresentationBuilder.Build(snapshot));
                Action layout = () =>
                {
                    for (int step = 0; step < 3; step++)
                    {
                        Canvas.ForceUpdateCanvases();
                        typeof(UI_Scene_OwnerRosterLineup).GetMethod("LateUpdate",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                    }
                    foreach (OwnerUiButtonSkin skin in root.GetComponentsInChildren<OwnerUiButtonSkin>()) skin.Refresh();
                    Canvas.ForceUpdateCanvases();
                };
                layout();
                Transform workspace = shell.MainWorkspaceHost.Find("OwnerRosterLineupWorkspace");
                Transform board = workspace.Find("PlayerOrderBoard");
                Button save = FindButton(workspace, "RosterActions/ConfirmLineupPreview");
                // Scroll Content는 Mask 밖에도 존재하므로 자식 합집합 대신 두 소유 Rect를 비교한다.
                var boardCorners = new Vector3[4];
                var actionCorners = new Vector3[4];
                ((RectTransform)board).GetWorldCorners(boardCorners);
                ((RectTransform)workspace.Find("RosterActions")).GetWorldCorners(actionCorners);
                Assert.That(actionCorners[2].y, Is.LessThanOrEqualTo(boardCorners[0].y));
                Assert.That(save.interactable, Is.False);
                Transform analysis = board.Find("ConditionAnalysisPanel/ContentSafeRect/RoleScroll/Viewport/Content");
                ScrollRect ownedScroll = board.Find("OwnedPlayerPanel/ContentSafeRect/RoleScroll").GetComponent<ScrollRect>();
                GridLayoutGroup ownedGrid = ownedScroll.content.GetComponentInChildren<GridLayoutGroup>();
                foreach (GridLayoutGroup grid in board.GetComponentsInChildren<GridLayoutGroup>())
                {
                    var viewport = (RectTransform)grid.transform.parent.parent;
                    var corners = new Vector3[4];
                    foreach (RectTransform card in grid.transform)
                    {
                        card.GetWorldCorners(corners);
                        Assert.That(viewport.InverseTransformPoint(corners[0]).x, Is.GreaterThanOrEqualTo(viewport.rect.xMin - 1f));
                        Assert.That(viewport.InverseTransformPoint(corners[2]).x, Is.LessThanOrEqualTo(viewport.rect.xMax + 1f),
                            "화면 폭이 줄어도 9번 타자와 마지막 벤치 카드까지 패널 안에 있어야 합니다.");
                    }
                }
                Assert.That(ownedScroll.viewport.rect.height, Is.GreaterThanOrEqualTo(ownedGrid.cellSize.y),
                    "최초 진입 시 보유 카드 한 줄의 이름과 역할을 스크롤 없이 읽을 수 있어야 합니다.");
                Assert.That(analysis.GetChild(0).GetComponent<Text>().text, Does.Contain("수비 배치 주의"));
                Transform conditionPlot = analysis.Find("RosterChart/Plot");
                Assert.That(conditionPlot, Is.Not.Null, "타순별 저장 컨디션 그래프가 있어야 합니다.");
                Assert.That(conditionPlot.GetComponent<Graphic>(), Is.Not.Null,
                    "타순별 저장 컨디션은 기존 막대 그래프로 표시합니다.");
                PlayerMiniCardView assigned = Array.Find(board.GetComponentsInChildren<PlayerMiniCardView>(), c => c.name == "Owned_0");
                Assert.That(assigned.transform.Find("AssignmentBadge").GetComponent<RectTransform>().anchorMin.y, Is.GreaterThanOrEqualTo(.89f));
                string output = Environment.GetEnvironmentVariable("BASEBALL_ROSTER_CAPTURE");
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, System.IO.Path.Combine(output, "hitters"), width, height);
                FindButton(analysis, "ReviewPosition_0").onClick.Invoke();
                Assert.That(Array.Find(board.GetComponentsInChildren<Button>(), b => b.name == "ApplyPosition"), Is.Not.Null);
                Assert.That(view.TryHandleCancel(), Is.True);
                view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(snapshot), "수비 위치 변경 · 저장 전입니다.");
                Assert.That(save.interactable, Is.True);
                int cancelCount = 0;
                view.LineupChangeCancelled += () => cancelCount++;
                FindButton(workspace, "RosterActions/CancelLineupPreview").onClick.Invoke();
                Assert.That(cancelCount, Is.EqualTo(1));
                view.AssignmentRequested += (group, slotIndex, incoming) =>
                {
                    var preview = snapshot.CreatePreview(OwnerLineupPresetCommandBuilder.Swap(snapshot.Preset,
                        OwnerLineupSwapGroup.BattingOrder, 0, 1), Valid("default"));
                    view.BindPreview(OwnerRosterLineupPresentationBuilder.Build(preview), "교체 변경안");
                };
                FindButton(board, "PlayerGroupTabs/PlacementEditMode").onClick.Invoke();
                PlayerMiniCardView selectedCard = Array.Find(board.GetComponentsInChildren<PlayerMiniCardView>(), c => c.name == "Owned_1");
                selectedCard.GetComponent<Button>().onClick.Invoke();
                layout();
                Assert.That(selectedCard.transform.Find("SelectionOverlay").gameObject.activeSelf, Is.True);
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, System.IO.Path.Combine(output, "selected"), width, height);
                selectedCard.GetComponent<Button>().onClick.Invoke();
                FindButton(board, "PrimaryAssignedPanel/ContentSafeRect/RoleScroll/Viewport/Content/AssignedGrid/BattingOrder_0").onClick.Invoke();
                Array.Find(board.GetComponentsInChildren<PlayerMiniCardView>(),
                    c => c.name.StartsWith("Owned_", StringComparison.Ordinal) && c.Model.PlayerId == "H1").GetComponent<Button>().onClick.Invoke();
                Assert.That(board.Find("ConditionAnalysisPanel/HeaderSlot").GetComponent<Text>().text, Is.EqualTo("교체 전후 비교"));
                layout();
                Transform comparisonPanel = board.Find("ConditionAnalysisPanel/ContentSafeRect");
                Assert.That(comparisonPanel.Find("ComparisonScroll").gameObject.activeSelf, Is.True);
                Assert.That(comparisonPanel.GetComponentInChildren<UIOpponentRadar>(), Is.Not.Null);
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, System.IO.Path.Combine(output, "comparison"), width, height);
                ScrollRect comparisonScroll = comparisonPanel.Find("ComparisonScroll").GetComponent<ScrollRect>();
                comparisonScroll.verticalNormalizedPosition = 0f;
                layout();
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, System.IO.Path.Combine(output, "comparison-stats"), width, height);
                FindButton(comparisonPanel, "AnalysisSubTabs/ConditionAnalysisTab").onClick.Invoke();
                Assert.That(comparisonPanel.Find("RoleScroll").gameObject.activeSelf, Is.True);
                FindButton(comparisonPanel, "AnalysisSubTabs/PlayerComparisonTab").onClick.Invoke();
                Assert.That(comparisonPanel.GetComponentInChildren<UIOpponentRadar>(), Is.Not.Null);
                Assert.That(save.interactable, Is.True);
                InputField search = Array.Find(board.GetComponentsInChildren<InputField>(true), field => field.name == "PlayerSearch");
                search.onEndEdit.Invoke("없는 선수");
                Button reset = Array.Find(board.GetComponentsInChildren<Button>(true), button => button.name == "ResetPlayerFilters");
                Assert.That(reset, Is.Not.Null);
                reset.onClick.Invoke();
                Assert.That(Array.Find(board.GetComponentsInChildren<PlayerMiniCardView>(), c => c.name == "Owned_0"), Is.Not.Null);
                view.SetWorkspaceMode(OwnerRosterWorkspaceMode.Pitching);
                layout();
                if (!string.IsNullOrEmpty(output)) CapturePositionView(camera, target, System.IO.Path.Combine(output, "pitchers"), width, height);
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void CapturePositionView(Camera camera, RenderTexture target, string output, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, $"positions-{width}.png"), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static OwnerRosterLineupSnapshot CreateSnapshot(LineupPresetValidationResult validation)
        {
            OwnerModeRosterStatus rosterStatus = CreateValidRosterStatus();
            var players = new[]
            {
                new OwnerRosterPlayerSnapshot(
                    "H0", "가상 포수", 2026, PlayerPosition.Catcher, PitcherRole.Starter,
                    PlayerCardEdition.Normal, 5, RegistrationType.Domestic,
                    ActiveRosterRole.StartingCatcher, PlayerAvailabilityStatus.Available)
            };
            return new OwnerRosterLineupSnapshot(rosterStatus, players, CreatePreset(), validation, string.Empty);
        }

        private static OwnerRosterLineupSnapshot CreateSnapshot(
            System.Collections.Generic.IReadOnlyList<OwnerRosterPresetSnapshot> presets,
            string selectedPresetId)
        {
            return new OwnerRosterLineupSnapshot(
                CreateValidRosterStatus(),
                new[]
                {
                    new OwnerRosterPlayerSnapshot(
                        "H0", "가상 포수", 2026, PlayerPosition.Catcher, PitcherRole.Starter,
                        PlayerCardEdition.Normal, 5, RegistrationType.Domestic,
                        ActiveRosterRole.StartingCatcher, PlayerAvailabilityStatus.Available)
                },
                presets,
                selectedPresetId,
                new[]
                {
                    new OwnerLoadoutCandidateSnapshot("TC_A", "연도·구단 20명"),
                    new OwnerLoadoutCandidateSnapshot("TC_B", "구단 20명"),
                    new OwnerLoadoutCandidateSnapshot("TC_C", "연도 20명")
                },
                new[]
                {
                    new OwnerLoadoutCandidateSnapshot("T0", "초구 신중"),
                    new OwnerLoadoutCandidateSnapshot("T1", "존 승부"),
                    new OwnerLoadoutCandidateSnapshot("T2", "강공")
                });
        }

        private static LineupPresetState CreatePreset()
        {
            var defense = new LineupPresetSlot[9];
            var batting = new string[9];
            for (int index = 0; index < 9; index++)
            {
                defense[index] = new LineupPresetSlot($"H{index}", (PlayerPosition)(index + 1));
                batting[index] = $"H{index}";
            }
            return new LineupPresetState(
                "default",
                "기본 프리셋",
                defense,
                batting,
                new[] { "B0", "B1", "B2", "B3", "B4" },
                new[] { "S0", "S1", "S2", "S3", "S4" },
                new[] { "R0", "R1", "R2", "R3" },
                "SU",
                "CL",
                new string[] { null, null },
                new[] { "T0", "T1" });
        }

        private static LineupPresetState CopyPreset(LineupPresetState source, string presetId, string name)
        {
            return new LineupPresetState(
                presetId,
                name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                source.TeamColorIds,
                source.DefaultTacticCardIds);
        }

        private static LineupPresetValidationResult Valid(string presetId) =>
            new LineupPresetValidationResult(presetId, Array.Empty<LineupPresetValidationIssue>());

        private static OwnerModeRosterStatus CreateValidRosterStatus()
        {
            Type issueType = Type.GetType(
                "Baseball.Simulation.Historical.RosterValidationIssue, Baseball.Simulation", true);
            Type validationType = Type.GetType(
                "Baseball.Simulation.Historical.RosterValidationResult, Baseball.Simulation", true);
            Array noIssues = Array.CreateInstance(issueType, 0);
            object validation = Activator.CreateInstance(validationType, new object[] { noIssues });
            return (OwnerModeRosterStatus)Activator.CreateInstance(
                typeof(OwnerModeRosterStatus),
                new[] { (object)25, 14, 11, 3, validation, null, null });
        }
    }
}

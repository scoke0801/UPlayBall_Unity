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

            Assert.That(model.RosterSummaryText, Is.EqualTo("1군 25/25 · 야수 14/14 · 투수 11/11 · 외국인 3/3"));
            Assert.That(model.DefensiveLineup.Count, Is.EqualTo(9));
            Assert.That(model.Bench.Count, Is.EqualTo(5));
            Assert.That(model.StarterRotation.Count, Is.EqualTo(5));
            Assert.That(model.ReliefPitching.Count, Is.EqualTo(6));
            Assert.That(model.DefensiveLineup[0].WarningText, Does.Contain("Condition -2"));
            Assert.That(model.DefensiveLineup[0].WarningText, Does.Contain("실책 위험 ×1.35"));
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
        public void View_같은구역두슬롯은Swap요청하고1군등록변경은비활성이다()
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
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/LineupColumns/HitterRolePanel/ContentSafeRect/RoleScroll/Viewport/Content/DefensiveLineup_0").onClick.Invoke();
                FindButton(shell.transform,
                    "MainWorkspaceHost/OwnerRosterLineupWorkspace/LineupColumns/HitterRolePanel/ContentSafeRect/RoleScroll/Viewport/Content/DefensiveLineup_1").onClick.Invoke();

                Assert.That(requestedGroup, Is.EqualTo(OwnerLineupSwapGroup.DefensiveLineup));
                Assert.That(first, Is.EqualTo(0));
                Assert.That(second, Is.EqualTo(1));
                Button activeRoster = FindButton(shell.transform,
                    "ContextActionBar/OwnerRosterLineupActionBar/ActiveRosterEditDisabled");
                Assert.That(activeRoster.interactable, Is.False);
                Assert.That(activeRoster.GetComponentInChildren<Text>().text, Does.Contain("변경 미제공"));
                view.SetFeedback("프리셋 적용 실패", true);
                Assert.That(shell.transform.Find(
                        "RightInspectorHost/OwnerRosterLineupInspector/ValidationPanel/ContentSafeRect/ValidationMessages")
                    .GetComponent<Text>().text, Is.EqualTo("프리셋 적용 실패"));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void View_저장프리셋과TeamColorTactic슬롯선택의도를전달한다()
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
                int teamColorSlot = -1;
                int tacticSlot = -1;
                view.PresetSelected += id => selectedPresetId = id;
                view.TeamColorSlotCycleRequested += index => teamColorSlot = index;
                view.TacticSlotCycleRequested += index => tacticSlot = index;

                FindButton(shell.transform,
                    "ContextActionBar/OwnerRosterLineupActionBar/NextPresetButton").onClick.Invoke();
                FindButton(shell.transform,
                    "ContextActionBar/OwnerRosterLineupActionBar/TeamColorSlot0").onClick.Invoke();
                FindButton(shell.transform,
                    "ContextActionBar/OwnerRosterLineupActionBar/TacticSlot1").onClick.Invoke();

                Assert.That(selectedPresetId, Is.EqualTo("alternate"));
                Assert.That(teamColorSlot, Is.EqualTo(0));
                Assert.That(tacticSlot, Is.EqualTo(1));
                Assert.That(shell.transform.Find(
                        "ContextActionBar/OwnerRosterLineupActionBar/PresetState").GetComponent<Text>().text,
                    Does.Contain("기본 프리셋 · 사용 가능"));
            }
            finally
            {
                if (view != null) UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Button FindButton(Transform root, string path) => root.Find(path).GetComponent<Button>();

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
                new[] { (object)25, 14, 11, 3, validation });
        }
    }
}

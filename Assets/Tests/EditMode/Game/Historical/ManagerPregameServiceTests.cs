using System;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerPregameServiceTests
    {
        [Test]
        public void PrepareNextGame_PublicEvidence와현재Preset으로Preview를완성한다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out _);
            var balance = BalanceTable.CreateDefault();
            var service = new ManagerPregameService(balance, provider);

            ManagerPregamePreparation result = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(result.CanStartGame, Is.True);
            Assert.That(result.PlanSnapshot, Is.Not.Null);
            Assert.That(result.PlanSnapshot.ScheduledGameId, Is.EqualTo(result.ScheduledGame.GameId));
            Assert.That(result.LineupChemistry, Is.Not.Null);
            Assert.That(result.BatteryChemistry.HasValue, Is.True);
            Assert.That(result.ScoutingReport.ProbableStarter.HasValue, Is.True);
            Assert.That(result.ScoutingReport.ExpectedLineup.Count,
                Is.EqualTo(ActiveRosterCompositionRule.StartingHitterCount));
            Assert.That(result.ScoutingReport.BullpenReadiness.Count,
                Is.EqualTo(
                    ActiveRosterCompositionRule.BullpenPitcherCount +
                    ActiveRosterCompositionRule.SetupPitcherCount +
                    ActiveRosterCompositionRule.CloserPitcherCount));
            Assert.That(
                result.ScoutingReport.GeneratedAtGameDate,
                Is.EqualTo(new DateTime(
                        runtime.ManagerMode.LiveSeason.OriginYear,
                        3,
                        1)
                    .AddDays(result.ScheduledGame.Round - 1)));
        }

        [Test]
        public void PrepareNextGame_HiddenAi판정은Unknown으로남고동일상태에서결정론적이다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out _);
            var service = new ManagerPregameService(BalanceTable.CreateDefault(), provider);

            ManagerPregamePreparation first = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());
            ManagerPregamePreparation second = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(first.ScoutingReport.RecentForm.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(first.ScoutingReport.OffenseProfile.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(first.ScoutingReport.PitchingProfile.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(first.ScoutingReport.DefenseProfile.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(first.ScoutingReport.ManagerTendencyEstimate.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(first.ScoutingReport.RecentTacticPatternSummary, Is.Empty);
            Assert.That(first.ReportEvidence.TacticPatterns, Is.Empty);
            Assert.That(second.ScoutingReport.ProbableStarter.Value.Player.CardId,
                Is.EqualTo(first.ScoutingReport.ProbableStarter.Value.Player.CardId));
            Assert.That(second.ScoutingReport.ProbableStarter.Confidence01,
                Is.EqualTo(first.ScoutingReport.ProbableStarter.Confidence01));
            Assert.That(second.ScoutingReport.GeneratedAtGameDate,
                Is.EqualTo(first.ScoutingReport.GeneratedAtGameDate));
            for (int index = 0; index < first.ScoutingReport.ExpectedLineup.Count; index++)
            {
                Assert.That(second.ScoutingReport.ExpectedLineup[index].Value.Player.CardId,
                    Is.EqualTo(first.ScoutingReport.ExpectedLineup[index].Value.Player.CardId));
            }
        }

        [Test]
        public void PrepareNextGame_PublicWorkload를불펜등급으로만노출한다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out _);
            var balance = BalanceTable.CreateDefault();
            ScheduledGameState game = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            string opponentKey = ResolveOpponentTeamSeasonKey(runtime, game);
            CurrentRosterState opponentRoster = runtime.GetRoster(opponentKey);
            ActiveRosterEntry bullpenEntry = FindFirstBullpen(opponentRoster);
            TeamSeasonPlayerStatus status = runtime.ManagerMode
                .GetPlayerStatus(opponentKey)
                .GetRequiredPlayer(bullpenEntry.PlayerPersonId);
            status.AdvancePitchingWorkload(
                balance.ScoutingConfidence.BullpenVeryTiredMinimumRecentPitches);
            var service = new ManagerPregameService(balance, provider);

            ManagerPregamePreparation tired = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(FindBullpen(tired.ScoutingReport, bullpenEntry.CardId).Readiness,
                Is.EqualTo(BullpenReadiness.VeryTired));

            status.SetAvailability(PlayerAvailabilityStatus.Unavailable);
            ManagerPregamePreparation unavailable = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(FindBullpen(unavailable.ScoutingReport, bullpenEntry.CardId).Readiness,
                Is.EqualTo(BullpenReadiness.Unavailable));
        }

        [Test]
        public void PrepareNextGame_StalePreset의Unavailable선수를경기직전에차단한다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out _);
            LineupPresetState preset = runtime.ManagerMode.GetSelectedLineupPreset();
            string staleCardId = preset.BattingOrderCardIds[0];
            Assert.That(runtime.WorldCardCatalog.TryGetCard(staleCardId, out PlayerCardDefinition card), Is.True);
            string personId = runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerPersonId;
            runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey)
                .GetRequiredPlayer(personId)
                .SetAvailability(PlayerAvailabilityStatus.Unavailable);
            var service = new ManagerPregameService(BalanceTable.CreateDefault(), provider);

            ManagerPregamePreparation result = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(result.PresetValidation.CanStartGame, Is.False);
            Assert.That(result.CanStartGame, Is.False);
            Assert.That(result.PlanSnapshot, Is.Null);
            Assert.That(result.LineupChemistry, Is.Null);
            Assert.That(result.BatteryChemistry.HasValue, Is.False);
            Assert.That(HasIssue(result.PresetValidation, LineupPresetValidationIssueCode.CardUnavailable), Is.True);
        }

        [Test]
        public void PrepareNextGame_Facility와StaffModifier를합친배율을정확히한번적용한다()
        {
            CreateFixture(
                out ManagerHistoricalRuntimeState state,
                out IHistoricalContentProvider provider,
                out ManagerHistoricalSaveAdapter adapter);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(state);
            SetFacilityLevel(save.managerMode, FacilityType.DataAnalysisCenter, 1);
            string staffId = StaffCatalogGenerator.CreateStableStaffId(
                save.managerMode.staffCatalogSeed,
                StaffRole.ScoutingDirector,
                0);
            save.managerMode.staffContracts = new[]
            {
                new StaffContractSaveData
                {
                    contractId = "contract:pregame-scouting",
                    staffId = staffId,
                    teamSeasonKey = save.playerTeamSeasonKey,
                    startSeason = 1,
                    remainingSeasons = 1,
                    annualSalary = 1L
                }
            };
            save.managerMode.staffAssignment.scoutingDirectorStaffId = staffId;
            ManagerHistoricalRuntimeState runtime = adapter.Restore(save);
            var balance = BalanceTable.CreateDefault();
            var service = new ManagerPregameService(balance, provider);

            ManagerPregamePreparation result = service.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                Array.Empty<string>());

            ScoutingConfidenceDefinition definition = balance.ScoutingConfidence;
            double cappedMultiplier = Math.Min(
                result.ConfidenceContext.CombinedMultiplier,
                definition.MaximumCombinedModifier);
            double expected = Math.Min(
                definition.PublicRosterEvidenceQuality *
                definition.PublicRosterRecencyFactor *
                definition.PublicRosterSampleFactor *
                cappedMultiplier,
                definition.MaximumInferredConfidence);
            Assert.That(result.ConfidenceContext.FacilityModifier, Is.GreaterThan(0d));
            Assert.That(result.ConfidenceContext.StaffModifier, Is.GreaterThan(0d));
            Assert.That(result.ScoutingReport.ProbableStarter.Confidence01,
                Is.EqualTo(expected).Within(1e-12d));
            Assert.That(result.ScoutingReport.ExpectedLineup[0].Confidence01,
                Is.EqualTo(expected).Within(1e-12d));
            Assert.That(result.ScoutingReport.BullpenReadiness[0].Confidence01,
                Is.EqualTo(expected).Within(1e-12d));
        }

        [Test]
        public void ValidateLineupPreset_선택상태를바꾸지않고모든저장Preset을현재후보로검증할수있다()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out IHistoricalContentProvider provider,
                out _);
            LineupPresetState source = runtime.ManagerMode.GetSelectedLineupPreset();
            var candidate = new LineupPresetState(
                "alternate",
                "대체 프리셋",
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                new[] { "UNAVAILABLE_COLOR", null },
                source.DefaultTacticCardIds);
            string selectedId = runtime.ManagerMode.SelectedLineupPresetId;
            var service = new ManagerPregameService(BalanceTable.CreateDefault(), provider);

            LineupPresetValidationResult result = service.ValidateLineupPreset(
                runtime,
                candidate,
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(result.PresetId, Is.EqualTo(candidate.PresetId));
            Assert.That(HasIssue(result, LineupPresetValidationIssueCode.TeamColorUnavailable), Is.True);
            Assert.That(runtime.ManagerMode.SelectedLineupPresetId, Is.EqualTo(selectedId));
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider,
            out ManagerHistoricalSaveAdapter adapter)
        {
            CreateFixture(out ManagerHistoricalRuntimeState state, out provider, out adapter);
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }

        private static void CreateFixture(
            out ManagerHistoricalRuntimeState state,
            out IHistoricalContentProvider provider,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            provider = (IHistoricalContentProvider)fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
        }

        private static string ResolveOpponentTeamSeasonKey(
            ManagerHistoricalRuntimeState runtime,
            ScheduledGameState game)
        {
            int playerTeamId = runtime.ManagerMode.LiveSeason.PlayerTeamId;
            int opponentId = game.HomeTeamId == playerTeamId ? game.AwayTeamId : game.HomeTeamId;
            return runtime.ManagerMode.LiveSeason.GetTeamSeasonKey(opponentId);
        }

        private static ActiveRosterEntry FindFirstBullpen(CurrentRosterState roster)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (ActiveRosterCompositionRule.Standard.IsBullpenRole(entry.Role)) return entry;
            }
            throw new AssertionException("테스트 상대 로스터에 불펜 투수가 없습니다.");
        }

        private static BullpenReadinessEntry FindBullpen(OpponentScoutingReport report, string cardId)
        {
            for (int index = 0; index < report.BullpenReadiness.Count; index++)
            {
                ScoutedValue<BullpenReadinessEntry> item = report.BullpenReadiness[index];
                if (item.HasValue && string.Equals(item.Value.Player.CardId, cardId, StringComparison.Ordinal))
                    return item.Value;
            }
            throw new AssertionException($"{cardId} 불펜 Report 항목이 없습니다.");
        }

        private static bool HasIssue(
            LineupPresetValidationResult validation,
            LineupPresetValidationIssueCode expected)
        {
            for (int index = 0; index < validation.Issues.Count; index++)
                if (validation.Issues[index].Code == expected) return true;
            return false;
        }

        private static void SetFacilityLevel(
            ManagerModeSaveData managerMode,
            FacilityType type,
            int level)
        {
            for (int index = 0; index < managerMode.clubOperation.facilities.Length; index++)
            {
                FacilitySaveData facility = managerMode.clubOperation.facilities[index];
                if (facility.type != (int)type) continue;
                facility.level = level;
                return;
            }
            throw new AssertionException($"{type} 시설 Save 항목이 없습니다.");
        }
    }
}

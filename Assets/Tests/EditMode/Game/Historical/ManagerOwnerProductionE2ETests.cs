using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단 운영부터 실제 경기와 Save/Load까지 구단주 Production 서비스 경로를 검증한다.</summary>
    public sealed class ManagerOwnerProductionE2ETests
    {
        [Test]
        public void OwnerLoop_OperationStaffPregameMatchFinanceAndLoad_RemainsOneTransactionChain()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out ManagerHistoricalSaveAdapter adapter,
                out IHistoricalContentProvider provider);
            BalanceTable balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);

            UpgradeAllInitialFacilities(runtime, coordinator);
            StaffMarketOffer conditioningOffer = FindOffer(
                runtime,
                balance,
                StaffRole.ConditioningCoach);
            StaffSigningResult signing = coordinator.SignStaff(runtime, conditioningOffer, 1);
            Assert.That(signing.IsSuccess, Is.True);

            TeamSeasonPlayerStatus recoveryTarget = runtime.ManagerMode
                .GetPlayerStatus(runtime.PlayerTeamSeasonKey)
                .Players[0];
            recoveryTarget.SetCondition(45);
            int conditionBeforeWeek = recoveryTarget.StoredBaseCondition;
            int scoutingPointsBefore = runtime.Economy.ScoutingPoints;
            int developmentPointsBefore = runtime.Economy.DevelopmentPoints;
            ManagerWeeklyAdvanceResult weekly = coordinator.AdvanceWeek(runtime);
            Assert.That(weekly.Status, Is.EqualTo(ManagerModeTransactionStatus.Applied));
            Assert.That(runtime.Economy.ScoutingPoints, Is.GreaterThan(scoutingPointsBefore));
            Assert.That(runtime.Economy.DevelopmentPoints, Is.GreaterThan(developmentPointsBefore));
            Assert.That(recoveryTarget.StoredBaseCondition, Is.GreaterThan(conditionBeforeWeek));

            long moneyBeforeSalary = runtime.Economy.Money;
            StaffSalarySettlementResult salary = coordinator.SettleStaffSalary(runtime);
            Assert.That(salary.IsSuccess, Is.True);
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBeforeSalary - salary.TotalSalary));

            TacticCardDefinition[] tactics = CreateStarterTactics();
            EquipOffPositionWarningPreset(runtime, tactics);
            var pregameService = new ManagerPregameService(balance, provider);
            ManagerPregamePreparation preparation = pregameService.PrepareNextGame(
                runtime,
                Array.Empty<string>(),
                new[] { tactics[0].CardId, tactics[1].CardId });
            Assert.That(preparation.CanStartGame, Is.True);
            Assert.That(preparation.PlanSnapshot.TacticCardIds.Count, Is.EqualTo(2));
            Assert.That(preparation.ScoutingReport.ProbableStarter.State, Is.Not.EqualTo(IntelState.Confirmed));
            Assert.That(preparation.ScoutingReport.ExpectedLineup.Count, Is.EqualTo(9));
            Assert.That(preparation.ScoutingReport.BullpenReadiness.Count, Is.GreaterThan(0));
            Assert.That(preparation.ScoutingReport.ManagerTendencyEstimate.State, Is.EqualTo(IntelState.Unknown));
            Assert.That(HasIssue(
                preparation.PresetValidation,
                LineupPresetValidationIssueCode.OffPositionAssignment), Is.True);
            Assert.That(preparation.LineupChemistry, Is.Not.Null);
            Assert.That(preparation.BatteryChemistry.HasValue, Is.True);

            var matchService = new ManagerModeMatchService(provider, balance, tacticCards: tactics);
            ManagerModeMatchResult homeMatch = PlayThroughNextPlayerHomeGame(runtime, matchService);
            Assert.That(homeMatch.PlayerPlan.TacticCardIds.Count, Is.EqualTo(2));
            Assert.That(homeMatch.Match.Input.RulesVersion, Is.EqualTo(Core.Rules.SimulationRulesVersion.DetailedV2));
            Assert.That(homeMatch.HomeFinanceStatus, Is.EqualTo(ManagerModeTransactionStatus.Applied));
            Assert.That(homeMatch.HomeFinance.Attendance, Is.InRange(0, homeMatch.HomeFinance.Capacity));
            Assert.That(runtime.ManagerMode.ClubOperation.CurrentSeason.HomeGames, Is.EqualTo(1));
            Assert.That(runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey).Entries.Count, Is.GreaterThan(0));

            long moneyAtSave = runtime.Economy.Money;
            int scoutingPointsAtSave = runtime.Economy.ScoutingPoints;
            int developmentPointsAtSave = runtime.Economy.DevelopmentPoints;
            int conditionAtSave = recoveryTarget.StoredBaseCondition;
            int familiarityCountAtSave = runtime.ManagerMode
                .GetFamiliarity(runtime.PlayerTeamSeasonKey)
                .Entries.Count;
            ManagerHistoricalSaveData save = adapter.CreateSaveData(runtime);
            ManagerHistoricalRuntimeState restored = adapter.Restore(save);

            Assert.That(restored.Economy.Money, Is.EqualTo(moneyAtSave));
            Assert.That(restored.Economy.ScoutingPoints, Is.EqualTo(scoutingPointsAtSave));
            Assert.That(restored.Economy.DevelopmentPoints, Is.EqualTo(developmentPointsAtSave));
            Assert.That(restored.ManagerMode.GetPlayerStatus(restored.PlayerTeamSeasonKey)
                .GetRequiredPlayer(recoveryTarget.PlayerPersonId).StoredBaseCondition, Is.EqualTo(conditionAtSave));
            Assert.That(restored.ManagerMode.GetFamiliarity(restored.PlayerTeamSeasonKey).Entries.Count,
                Is.EqualTo(familiarityCountAtSave));
            Assert.That(restored.ManagerMode.GetSelectedLineupPreset().DefaultTacticCardIds.Count, Is.EqualTo(2));
            Assert.That(restored.ManagerMode.ClubOperation.TryApplyHomeGame(homeMatch.HomeFinance), Is.False);
            Assert.That(restored.Economy.Money, Is.EqualTo(moneyAtSave));

            WeeklyFacilityProductionResult duplicateProduction = new WeeklyFacilityProductionResolver(
                balance.ClubOperation).Resolve(
                restored.ManagerMode.ClubOperation,
                new WeeklyFacilityProductionContext(
                    restored.ManagerMode.LiveSeason.SeasonId,
                    0,
                    restored.League.Grade,
                    restored.Economy.Money,
                    restored.Economy.ScoutingPoints,
                    restored.Economy.DevelopmentPoints));
            Assert.That(duplicateProduction.Status, Is.EqualTo(WeeklyFacilityProductionStatus.AlreadyApplied));
            StaffSalarySettlementResult duplicateSalary = coordinator.SettleStaffSalary(restored);
            Assert.That(duplicateSalary.Status, Is.EqualTo(StaffServiceStatus.NoChange));
            Assert.That(restored.Economy.Money, Is.EqualTo(moneyAtSave));
        }

        [Test]
        public void StaffTraining_ProductionCoordinatorConsumesCoachEfficiencyAndKeepsCeiling()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out _,
                out _);
            BalanceTable balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);
            Assert.That(coordinator.SignStaff(
                runtime,
                FindOffer(runtime, balance, StaffRole.HittingCoach),
                1).IsSuccess, Is.True);
            Assert.That(coordinator.SignStaff(
                runtime,
                FindOffer(runtime, balance, StaffRole.DevelopmentCoach),
                1).IsSuccess, Is.True);
            Assert.That(coordinator.UpgradeFacility(
                runtime,
                FacilityType.TrainingCenter,
                "staff-training:test").IsApproved, Is.True);
            Assert.That(coordinator.AdvanceWeek(runtime).Status, Is.EqualTo(ManagerModeTransactionStatus.Applied));

            PlayerCardDefinition targetCard = null;
            PlayerSeasonDefinition targetSeason = null;
            for (int index = 0; index < runtime.OwnedCards.Count; index++)
            {
                if (!runtime.WorldCardCatalog.TryGetCard(runtime.OwnedCards[index].CardId, out PlayerCardDefinition card))
                    continue;
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                if (season.CreateTrainingCeiling().Get(PlayerAbility.Contact) <=
                    season.CreateBaseAttributes().Get(PlayerAbility.Contact))
                {
                    continue;
                }
                targetCard = card;
                targetSeason = season;
                break;
            }
            Assert.That(targetCard, Is.Not.Null, "Contact 성장 여지가 있는 소유 타자 카드가 필요합니다.");

            const int baseDpCost = 10;
            CardTrainingResult result = coordinator.TrainOwnedCard(
                runtime,
                targetCard.CardId,
                new CardTrainingProgramDefinition("owner.contact", PlayerAbility.Contact, baseDpCost, 99));
            Assert.That(result.GainedPoints, Is.GreaterThan(0));
            Assert.That(result.SpentDp, Is.LessThan(result.GainedPoints * baseDpCost));
            Assert.That(
                targetSeason.CreateBaseAttributes().Get(PlayerAbility.Contact) +
                runtime.OwnedCards[FindOwnedCardIndex(runtime, targetCard.CardId)]
                    .Training.GetBonus(PlayerAbility.Contact),
                Is.LessThanOrEqualTo(targetSeason.CreateTrainingCeiling().Get(PlayerAbility.Contact)));
        }

        [Test]
        public void AdvanceWeek_RecoversEveryTeamThroughOnePlayerOrAiStaffContext()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out _,
                out _);
            BalanceTable balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);
            StaffDefinition conditioningCoach = FindStaff(runtime.ManagerMode.StaffCatalog, StaffRole.ConditioningCoach);
            var playerContract = new StaffContractState(
                "contract:weekly-recovery",
                conditioningCoach.StaffId,
                runtime.PlayerTeamSeasonKey,
                1,
                2,
                1L);
            runtime.ManagerMode.ReplaceStaffState(
                new[] { playerContract },
                new TeamStaffAssignmentState(
                    runtime.PlayerTeamSeasonKey,
                    conditioningCoachStaffId: conditioningCoach.StaffId));
            Assert.That(coordinator.UpgradeFacility(
                runtime,
                FacilityType.RecoveryCenter,
                "weekly-recovery:facility").IsApproved, Is.True);

            var startingConditions = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < runtime.ManagerMode.PlayerStatuses.Count; index++)
            {
                TeamSeasonPlayerStatusState status = runtime.ManagerMode.PlayerStatuses[index];
                status.Players[0].SetCondition(20);
                startingConditions.Add(status.TeamSeasonKey, status.Players[0].StoredBaseCondition);
            }

            ManagerWeeklyAdvanceResult result = coordinator.AdvanceWeek(runtime);

            Assert.That(result.Status, Is.EqualTo(ManagerModeTransactionStatus.Applied));
            Assert.That(result.TeamRecoveries.Count, Is.EqualTo(runtime.ManagerMode.LiveSeason.Teams.Count));
            for (int index = 0; index < result.TeamRecoveries.Count; index++)
            {
                ManagerTeamRecoveryResult teamResult = result.TeamRecoveries[index];
                ConditionRecoveryContext context = teamResult.IsPlayerTeam
                    ? coordinator.CreateRecoveryContext(runtime.ManagerMode)
                    : coordinator.CreateAiRecoveryContext(runtime, teamResult.TeamSeasonKey);
                int expectedRecovery = new ConditionRecoveryResolver().ResolveRecovery(context);
                Assert.That(teamResult.Recovery, Is.EqualTo(expectedRecovery), teamResult.TeamSeasonKey);
                Assert.That(runtime.ManagerMode.GetPlayerStatus(teamResult.TeamSeasonKey)
                    .Players[0].StoredBaseCondition,
                    Is.EqualTo(startingConditions[teamResult.TeamSeasonKey] + expectedRecovery),
                    teamResult.TeamSeasonKey);
            }

            string aiTeamSeasonKey = FindFirstAiTeam(runtime);
            TeamStaffEffectProfile first = coordinator.ResolveAiStaffEffects(runtime, aiTeamSeasonKey);
            TeamStaffEffectProfile second = coordinator.ResolveAiStaffEffects(runtime, aiTeamSeasonKey);
            Assert.That(second.ConditionRecoveryEfficiency,
                Is.EqualTo(first.ConditionRecoveryEfficiency).Within(0.0000000001d));
        }

        [Test]
        public void AdvanceSeason_SettlesSalaryOnceExpiresContractsAndCreatesConfiguredSchedule()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out ManagerHistoricalSaveAdapter adapter,
                out _);
            BalanceTable balance = BalanceTable.CreateDefault();
            var coordinator = new ManagerModeCoordinator(balance);
            StaffDefinition conditioningCoach = FindStaff(runtime.ManagerMode.StaffCatalog, StaffRole.ConditioningCoach);
            StaffDefinition hittingCoach = FindStaff(runtime.ManagerMode.StaffCatalog, StaffRole.HittingCoach);
            var oneYear = new StaffContractState(
                "contract:one-year",
                conditioningCoach.StaffId,
                runtime.PlayerTeamSeasonKey,
                1,
                1,
                1_000L);
            var twoYear = new StaffContractState(
                "contract:two-year",
                hittingCoach.StaffId,
                runtime.PlayerTeamSeasonKey,
                1,
                2,
                2_000L);
            runtime.ManagerMode.ReplaceStaffState(
                new[] { oneYear, twoYear },
                new TeamStaffAssignmentState(
                    runtime.PlayerTeamSeasonKey,
                    hittingCoachStaffId: hittingCoach.StaffId,
                    conditioningCoachStaffId: conditioningCoach.StaffId));

            int configuredGames = ResolveCompatibleGamesPerTeam(
                runtime.ManagerMode.LiveSeason.Teams.Count,
                balance.CareerSeason.RegularSeasonGamesPerTeam);
            Assert.That(CountPlayerGames(runtime.ManagerMode.LiveSeason), Is.EqualTo(configuredGames));
            ulong firstSeasonFirstGameSeed = runtime.ManagerMode.LiveSeason.Schedule.Games[0].RandomSeed;
            ManagerSeasonAdvanceResult inProgress = coordinator.AdvanceSeason(runtime);
            Assert.That(inProgress.Status, Is.EqualTo(ManagerSeasonAdvanceStatus.SeasonInProgress));
            Assert.That(oneYear.LastSalaryPaidSeason, Is.Null);

            CompletePlayerSchedule(runtime.ManagerMode.LiveSeason);
            SeasonFinanceSummary completedFinance = runtime.ManagerMode.ClubOperation.CurrentSeason;
            long moneyBefore = runtime.Economy.Money;
            ManagerSeasonAdvanceResult result = coordinator.AdvanceSeason(runtime);

            Assert.That(result.IsApplied, Is.True);
            Assert.That(result.CompletedFinance, Is.SameAs(completedFinance));
            Assert.That(result.SalarySettlement.TotalSalary, Is.EqualTo(3_000L));
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore - 3_000L));
            Assert.That(runtime.ManagerMode.LiveSeason.SeasonNumber, Is.EqualTo(2));
            Assert.That(runtime.ManagerMode.LiveSeason.CurrentWeekIndex, Is.Zero);
            Assert.That(runtime.ManagerMode.ClubOperation.CurrentSeason.SeasonId,
                Is.EqualTo(runtime.ManagerMode.LiveSeason.SeasonId));
            Assert.That(runtime.ManagerMode.ClubOperation.CurrentSeason.HomeGames, Is.Zero);
            Assert.That(CountPlayerGames(runtime.ManagerMode.LiveSeason), Is.EqualTo(configuredGames));
            Assert.That(runtime.ManagerMode.LiveSeason.Schedule.Games[0].RandomSeed,
                Is.Not.EqualTo(firstSeasonFirstGameSeed));
            Assert.That(runtime.ManagerMode.StaffAssignment.ConditioningCoachStaffId, Is.Null);
            Assert.That(runtime.ManagerMode.StaffAssignment.HittingCoachStaffId, Is.EqualTo(hittingCoach.StaffId));
            Assert.That(FindContract(runtime, oneYear.ContractId).IsActive, Is.False);
            Assert.That(FindContract(runtime, twoYear.ContractId).RemainingSeasons, Is.EqualTo(1));
            Assert.That(coordinator.AdvanceSeason(runtime).Status,
                Is.EqualTo(ManagerSeasonAdvanceStatus.SeasonInProgress));

            long moneyBeforeSecondSeasonSalary = runtime.Economy.Money;
            StaffSalarySettlementResult secondSeasonSalary = coordinator.SettleStaffSalary(runtime);
            Assert.That(secondSeasonSalary.Status, Is.EqualTo(StaffServiceStatus.Succeeded));
            Assert.That(secondSeasonSalary.TotalSalary, Is.EqualTo(twoYear.AnnualSalary));
            Assert.That(coordinator.SettleStaffSalary(runtime).Status, Is.EqualTo(StaffServiceStatus.NoChange));
            Assert.That(runtime.Economy.Money,
                Is.EqualTo(moneyBeforeSecondSeasonSalary - twoYear.AnnualSalary));

            ManagerHistoricalRuntimeState restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(restored.ManagerMode.LiveSeason.SeasonNumber, Is.EqualTo(2));
            Assert.That(restored.ManagerMode.ClubOperation.CurrentSeason.SeasonId,
                Is.EqualTo(runtime.ManagerMode.ClubOperation.CurrentSeason.SeasonId));
            Assert.That(restored.ManagerMode.LiveSeason.Schedule.Games.Count,
                Is.EqualTo(runtime.ManagerMode.LiveSeason.Schedule.Games.Count));
            Assert.That(restored.ManagerMode.LiveSeason.Schedule.Games[0].RandomSeed,
                Is.EqualTo(runtime.ManagerMode.LiveSeason.Schedule.Games[0].RandomSeed));
            Assert.That(restored.ManagerMode.LiveSeason.Schedule.Games[0].AwayTeamId,
                Is.EqualTo(runtime.ManagerMode.LiveSeason.Schedule.Games[0].AwayTeamId));
            Assert.That(restored.ManagerMode.LiveSeason.Schedule.Games[0].HomeTeamId,
                Is.EqualTo(runtime.ManagerMode.LiveSeason.Schedule.Games[0].HomeTeamId));
            Assert.That(coordinator.SettleStaffSalary(restored).Status, Is.EqualTo(StaffServiceStatus.NoChange));
        }

        [Test]
        public void AdvanceSeason_WhenSalaryCannotBePaid_LeavesEveryStateUntouched()
        {
            CreateRuntime(
                out ManagerHistoricalRuntimeState runtime,
                out ManagerHistoricalSaveAdapter adapter,
                out _);
            StaffDefinition coach = FindStaff(runtime.ManagerMode.StaffCatalog, StaffRole.ConditioningCoach);
            var contract = new StaffContractState(
                "contract:unpayable",
                coach.StaffId,
                runtime.PlayerTeamSeasonKey,
                1,
                1,
                10_000L);
            runtime.ManagerMode.ReplaceStaffState(
                new[] { contract },
                new TeamStaffAssignmentState(
                    runtime.PlayerTeamSeasonKey,
                    conditioningCoachStaffId: coach.StaffId));
            CompletePlayerSchedule(runtime.ManagerMode.LiveSeason);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(runtime);
            save.economy.money = 0L;
            runtime = adapter.Restore(save);
            ClubOperationState operationBefore = runtime.ManagerMode.ClubOperation;

            ManagerSeasonAdvanceResult result = new ManagerModeCoordinator(BalanceTable.CreateDefault())
                .AdvanceSeason(runtime);

            Assert.That(result.Status, Is.EqualTo(ManagerSeasonAdvanceStatus.InsufficientMoney));
            Assert.That(runtime.Economy.Money, Is.Zero);
            Assert.That(runtime.ManagerMode.LiveSeason.SeasonNumber, Is.EqualTo(1));
            Assert.That(runtime.ManagerMode.ClubOperation, Is.SameAs(operationBefore));
            StaffContractState unchanged = FindContract(runtime, contract.ContractId);
            Assert.That(unchanged.RemainingSeasons, Is.EqualTo(1));
            Assert.That(unchanged.LastSalaryPaidSeason, Is.Null);
            Assert.That(runtime.ManagerMode.StaffAssignment.ConditioningCoachStaffId, Is.EqualTo(coach.StaffId));
        }

        private static void UpgradeAllInitialFacilities(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeCoordinator coordinator)
        {
            foreach (FacilityType facilityType in Enum.GetValues(typeof(FacilityType)))
            {
                FacilityUpgradeResult result = coordinator.UpgradeFacility(
                    runtime,
                    facilityType,
                    $"e2e:{facilityType}");
                Assert.That(result.IsApproved, Is.True, facilityType.ToString());
                Assert.That(runtime.ManagerMode.ClubOperation.GetFacility(facilityType).Level, Is.EqualTo(1));
            }
        }

        private static StaffMarketOffer FindOffer(
            ManagerHistoricalRuntimeState runtime,
            BalanceTable balance,
            StaffRole role)
        {
            IReadOnlyList<StaffMarketOffer> offers = new StaffMarketResolver().CreateOffers(
                runtime.ManagerMode.StaffCatalog,
                runtime.ManagerMode.StaffContracts,
                runtime.PlayerTeamSeasonKey,
                "e2e:season-1",
                StaffMarketKind.Offseason,
                runtime.League.Grade,
                runtime.WorldHistory.WorldHistorySeed,
                balance.Staff);
            for (int index = 0; index < offers.Count; index++)
            {
                if (runtime.ManagerMode.StaffCatalog.Get(offers[index].StaffId).Role == role)
                    return offers[index];
            }
            throw new AssertionException($"{role} 시장 제안이 생성되지 않았습니다.");
        }

        private static int FindOwnedCardIndex(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            for (int index = 0; index < runtime.OwnedCards.Count; index++)
            {
                if (string.Equals(runtime.OwnedCards[index].CardId, cardId, StringComparison.Ordinal))
                    return index;
            }
            throw new AssertionException($"{cardId} 소유 카드를 찾지 못했습니다.");
        }

        private static StaffDefinition FindStaff(StaffCatalog catalog, StaffRole role)
        {
            for (int index = 0; index < catalog.Staff.Count; index++)
                if (catalog.Staff[index].Role == role) return catalog.Staff[index];
            throw new AssertionException($"{role} Staff를 찾지 못했습니다.");
        }

        private static StaffContractState FindContract(
            ManagerHistoricalRuntimeState runtime,
            string contractId)
        {
            for (int index = 0; index < runtime.ManagerMode.StaffContracts.Count; index++)
            {
                StaffContractState contract = runtime.ManagerMode.StaffContracts[index];
                if (string.Equals(contract.ContractId, contractId, StringComparison.Ordinal)) return contract;
            }
            throw new AssertionException($"{contractId} Staff 계약을 찾지 못했습니다.");
        }

        private static string FindFirstAiTeam(ManagerHistoricalRuntimeState runtime)
        {
            for (int index = 0; index < runtime.ManagerMode.LiveSeason.Teams.Count; index++)
            {
                string teamSeasonKey = runtime.ManagerMode.LiveSeason.Teams[index].TeamSeasonKey;
                if (!runtime.HasOwnedEconomy(teamSeasonKey)) return teamSeasonKey;
            }
            throw new AssertionException("AI 구단을 찾지 못했습니다.");
        }

        private static int CountPlayerGames(ManagerLiveSeasonState season)
        {
            int count = 0;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
                if (season.Schedule.Games[index].IncludesTeam(season.PlayerTeamId)) count++;
            return count;
        }

        private static void CompletePlayerSchedule(ManagerLiveSeasonState season)
        {
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = season.Schedule.Games[index];
                if (!game.IsCompleted && game.IncludesTeam(season.PlayerTeamId)) game.Complete(0, 0);
            }
            Assert.That(season.NextPlayerGame, Is.Null);
        }

        private static int ResolveCompatibleGamesPerTeam(int teamCount, int configuredGames)
        {
            if ((teamCount & 1) == 0) return configuredGames;
            return configuredGames - configuredGames % (teamCount - 1);
        }

        private static void EquipOffPositionWarningPreset(
            ManagerHistoricalRuntimeState runtime,
            IReadOnlyList<TacticCardDefinition> tactics)
        {
            LineupPresetState original = runtime.ManagerMode.GetSelectedLineupPreset();
            var slots = new LineupPresetSlot[original.StartingLineupSlots.Count];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = original.StartingLineupSlots[index];
            slots[0] = new LineupPresetSlot(slots[0].CardId, original.StartingLineupSlots[1].Position);
            slots[1] = new LineupPresetSlot(slots[1].CardId, original.StartingLineupSlots[0].Position);
            var updated = new LineupPresetState(
                original.PresetId,
                original.Name,
                slots,
                original.BattingOrderCardIds,
                original.BenchPriorityCardIds,
                original.StarterRotationCardIds,
                original.BullpenAssignmentCardIds,
                original.SetupPitcherCardId,
                original.CloserPitcherCardId,
                original.TeamColorIds,
                new[] { tactics[0].CardId, tactics[1].CardId });
            runtime.ManagerMode.UpsertLineupPreset(updated);
        }

        private static ManagerModeMatchResult PlayThroughNextPlayerHomeGame(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeMatchService matchService)
        {
            while (runtime.ManagerMode.LiveSeason.NextPlayerGame.HomeTeamId !=
                   runtime.ManagerMode.LiveSeason.PlayerTeamId)
            {
                ManagerModeMatchResult away = matchService.PlayNextGame(runtime);
                Assert.That(away.HomeFinance.Status, Is.EqualTo(HomeGameFinanceStatus.NotHomeGame));
            }
            return matchService.PlayNextGame(runtime);
        }

        private static bool HasIssue(
            LineupPresetValidationResult validation,
            LineupPresetValidationIssueCode code)
        {
            for (int index = 0; index < validation.Issues.Count; index++)
                if (validation.Issues[index].Code == code) return true;
            return false;
        }

        private static TacticCardDefinition[] CreateStarterTactics()
        {
            return new[]
            {
                new TacticCardDefinition(
                    "tactic.owner.contact",
                    "집중 타격",
                    TacticCardCategory.Batting,
                    TacticTier.Normal,
                    "7회 공격 집중",
                    "Contact +2",
                    new[] { new TacticTriggerCondition(TacticTriggerField.Inning, TacticComparison.Equal, 7) },
                    TacticTargetRule.BattingTeam,
                    new[] { new TacticStatModifier(PlayerAbility.Contact, 2) },
                    Array.Empty<TacticBehaviorModifier>(),
                    TacticDurationRule.UntilInningEnd,
                    Array.Empty<string>(),
                    false),
                new TacticCardDefinition(
                    "tactic.owner.control",
                    "마운드 정비",
                    TacticCardCategory.Pitching,
                    TacticTier.Normal,
                    "8회 투수 집중",
                    "Control +2",
                    new[] { new TacticTriggerCondition(TacticTriggerField.Inning, TacticComparison.Equal, 8) },
                    TacticTargetRule.PitchingTeam,
                    new[] { new TacticStatModifier(PlayerAbility.Control, 2) },
                    Array.Empty<TacticBehaviorModifier>(),
                    TacticDurationRule.UntilInningEnd,
                    Array.Empty<string>(),
                    false)
            };
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out ManagerHistoricalSaveAdapter adapter,
            out IHistoricalContentProvider provider)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            ManagerHistoricalRuntimeState original = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            provider = (IHistoricalContentProvider)fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            ManagerHistoricalSaveData save = adapter.CreateSaveData(original);
            save.economy.money = MoneyAmount.FromTenThousandWon(500_000L);
            runtime = adapter.Restore(save);
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>Game에서 확정된 상태와 Resolver 결과를 A/B/C/D 불변 UI Snapshot으로 투영한다.</summary>
    public sealed class OwnerModeRuntimeSnapshotFactory
    {
        public OwnerHomeSnapshot CreateHome(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            OwnerModeRosterStatus roster = manager.BuildRosterStatus();
            ScheduledGameState game = mode.LiveSeason.NextPlayerGame;
            string nextMatch = game == null ? "시즌 일정 종료" : CreateNextMatchText(manager, mode, game);
            return new OwnerHomeSnapshot(
                mode.LiveSeason.OriginYear + " 시즌",
                $"{mode.LiveSeason.CurrentWeekIndex + 1}주차",
                runtime.League.Grade.ToString(),
                manager.GetTeamDisplayName(runtime.PlayerTeamSeasonKey),
                string.Empty,
                nextMatch,
                runtime.Economy.Money,
                runtime.Economy.ScoutingPoints,
                runtime.Economy.DevelopmentPoints,
                runtime.Economy.PityGauge,
                roster.ActiveRosterCount,
                roster.ActiveRosterCapacity,
                roster.HitterCount,
                roster.RequiredHitterCount,
                roster.PitcherCount,
                roster.RequiredPitcherCount,
                roster.ForeignPlayerCount,
                roster.ForeignPlayerLimit,
                runtime.OwnedCards.Count,
                roster.Validation.IsValid,
                roster.Validation.IsValid ? string.Empty : FormatRosterIssue(roster.Validation.Issues[0]));
        }

        /// <summary>현재 1군·선택 프리셋·Resolver 검증을 규칙 재계산 없이 선수단 화면에 투영한다.</summary>
        public OwnerRosterLineupSnapshot CreateRosterLineup(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            TeamSeasonPlayerStatusState statuses = mode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            var players = new OwnerRosterPlayerSnapshot[roster.Entries.Count];
            for (int index = 0; index < players.Length; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!runtime.WorldCardCatalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {entry.CardId} 원본이 없습니다.");
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                players[index] = new OwnerRosterPlayerSnapshot(
                    entry.CardId,
                    runtime.IdentityRegistry.GetPlayerDisplayName(entry.PlayerPersonId),
                    season.OriginYear,
                    season.Position,
                    season.PitcherRole,
                    card.Edition,
                    season.Cost,
                    entry.RegistrationType,
                    entry.Role,
                    statuses.GetRequiredPlayer(entry.PlayerPersonId).Availability);
            }

            ManagerPregamePreparation preparation = null;
            string unavailableReason = string.Empty;
            if (mode.LiveSeason.NextPlayerGame == null)
            {
                unavailableReason = "시즌 일정이 종료되어 다음 경기 프리셋 검증을 실행하지 않았습니다.";
            }
            else
            {
                preparation = manager.CurrentPregame ?? manager.PrepareNextGame();
            }

            var presetSnapshots = new OwnerRosterPresetSnapshot[mode.LineupPresets.Count];
            for (int index = 0; index < presetSnapshots.Length; index++)
            {
                LineupPresetState preset = mode.LineupPresets[index];
                LineupPresetValidationResult validation = preparation == null
                    ? null
                    : string.Equals(preset.PresetId, mode.SelectedLineupPresetId, StringComparison.Ordinal)
                        ? preparation.PresetValidation
                        : manager.ValidateLineupPreset(preset);
                presetSnapshots[index] = new OwnerRosterPresetSnapshot(preset, validation, unavailableReason);
            }

            IReadOnlyList<TeamColorDefinition> teamColors = manager.GetAvailableTeamColors();
            var teamColorCandidates = new OwnerLoadoutCandidateSnapshot[teamColors.Count];
            for (int index = 0; index < teamColorCandidates.Length; index++)
            {
                TeamColorDefinition definition = teamColors[index];
                teamColorCandidates[index] = new OwnerLoadoutCandidateSnapshot(
                    definition.TeamColorId,
                    FormatTeamColor(definition));
            }
            IReadOnlyList<TacticCardDefinition> tactics = manager.GetAvailableTacticCards();
            var tacticCandidates = new OwnerLoadoutCandidateSnapshot[tactics.Count];
            for (int index = 0; index < tacticCandidates.Length; index++)
                tacticCandidates[index] = new OwnerLoadoutCandidateSnapshot(tactics[index].CardId, tactics[index].Name);

            return new OwnerRosterLineupSnapshot(
                manager.BuildRosterStatus(),
                players,
                presetSnapshots,
                mode.SelectedLineupPresetId,
                teamColorCandidates,
                tacticCandidates);
        }

        /// <summary>현재 Save의 OwnedCards와 WorldCardCatalog를 보유 선수 화면 Snapshot으로 투영한다.</summary>
        public OwnerCollectionSnapshot CreateCollection(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            var cards = new OwnerCollectionCardSnapshot[runtime.OwnedCards.Count];
            for (int index = 0; index < cards.Length; index++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[index];
                if (!runtime.WorldCardCatalog.TryGetCard(owned.CardId, out PlayerCardDefinition card))
                    throw new InvalidOperationException($"CardId {owned.CardId} 원본이 없습니다.");
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                cards[index] = new OwnerCollectionCardSnapshot(
                    owned.CardId,
                    season.PlayerPersonId,
                    runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId),
                    season.OriginYear,
                    season.Position,
                    season.Cost,
                    card.Edition,
                    owned.EnhancementLevel,
                    owned.DuplicateCount,
                    owned.IsLocked,
                    owned.IsFavorite);
            }
            return new OwnerCollectionSnapshot(cards);
        }

        public OwnerClubOperationSnapshot CreateClubOperation(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ClubOperationState operation = runtime.ManagerMode.ClubOperation;
            ClubOperationBalanceTable balance = manager.Balance.ClubOperation;
            FacilityType[] types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            var facilities = new OwnerFacilitySnapshot[types.Length];
            for (int index = 0; index < types.Length; index++)
            {
                FacilityType type = types[index];
                FacilityState state = operation.GetFacility(type);
                FacilityLevelDefinition definition = balance.GetFacilityLevel(type, state.Level);
                FacilityUpgradeResult preview = manager.PreviewFacilityUpgrade(type);
                int maximumLevel = GetMaximumFacilityLevel(balance, type, state.Level);
                facilities[index] = new OwnerFacilitySnapshot(
                    type,
                    state.Level,
                    maximumLevel,
                    preview.Status == ClubUpgradeStatus.MaximumLevel ? null : preview.MoneyCost,
                    preview.IsApproved,
                    preview.IsApproved ? string.Empty : FormatUpgradeStatus(preview.Status),
                    definition.WeeklyScoutingPointProduction,
                    definition.ScoutingPointStorageCapacity,
                    definition.WeeklyDevelopmentPointProduction,
                    definition.DevelopmentPointStorageCapacity,
                    definition.ConditionRecoveryEfficiencyModifier,
                    definition.ScoutingConfidenceModifier,
                    definition.TacticResearchEfficiencyModifier,
                    definition.FanShopRevenuePerAttendee,
                    definition.FanShopPopularityRetention);
            }

            StadiumUpgradeResult stadiumPreview = manager.PreviewStadiumUpgrade();
            WeeklyOperationLedger week = operation.CurrentWeek;
            SeasonFinanceSummary season = operation.CurrentSeason;
            int? recentAttendance = week.HomeGames == 0
                ? null
                : checked((int)(week.Attendance / week.HomeGames));
            return new OwnerClubOperationSnapshot(
                operation.Stadium.Level,
                operation.Stadium.Capacity,
                stadiumPreview.Status == ClubUpgradeStatus.MaximumLevel ? null : stadiumPreview.MoneyCost,
                stadiumPreview.IsApproved,
                stadiumPreview.IsApproved ? string.Empty : FormatUpgradeStatus(stadiumPreview.Status),
                operation.FanBase,
                operation.Popularity,
                manager.PreviewNextHomeAttendance(),
                recentAttendance,
                operation.TicketPolicy.PriceTier,
                facilities,
                new OwnerFinanceSnapshot(
                    week.MoneyIncome,
                    week.MoneyExpense,
                    week.ScoutingPointProduction,
                    week.DevelopmentPointProduction,
                    week.HomeGames,
                    week.Attendance),
                new OwnerFinanceSnapshot(
                    season.MoneyIncome,
                    season.MoneyExpense,
                    season.ScoutingPointProduction,
                    season.DevelopmentPointProduction,
                    season.HomeGames,
                    season.Attendance));
        }

        public OwnerStaffOfficeSnapshot CreateStaffOffice(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            IReadOnlyList<StaffMarketOffer> offers = manager.GetStaffMarketOffers();
            var snapshots = new OwnerStaffMarketOfferSnapshot[offers.Count];
            for (int index = 0; index < offers.Count; index++)
            {
                StaffMarketOffer offer = offers[index];
                StaffSigningResult signing = manager.PreviewStaffSigning(offer);
                TeamStaffEffectProfile effects = manager.PreviewStaffEffects(signing);
                snapshots[index] = new OwnerStaffMarketOfferSnapshot(
                    offer,
                    signing.IsSuccess,
                    signing.IsSuccess ? string.Empty : FormatStaffStatus(signing.Status),
                    FormatStaffEffect(mode.StaffCatalog.Get(offer.StaffId).Role, effects));
            }
            return new OwnerStaffOfficeSnapshot(
                UiContentStateModel.Ready,
                mode.StaffCatalog,
                mode.StaffContracts,
                mode.StaffAssignment,
                manager.GetStaffEffects(),
                snapshots);
        }

        public OwnerPregameSnapshot CreatePregame(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            ManagerPregamePreparation preparation = manager.CurrentPregame ?? manager.PrepareNextGame();
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            LineupPresetState preset = mode.GetSelectedLineupPreset();
            IReadOnlyList<OwnerModeConditionEntry> conditionEntries = manager.BuildConditionEntries();
            var conditions = new Dictionary<string, OwnerModeConditionEntry>(StringComparer.Ordinal);
            for (int index = 0; index < conditionEntries.Count; index++)
                conditions.Add(conditionEntries[index].PlayerPersonId, conditionEntries[index]);

            ConditionPresentationTable presentation = manager.Balance.ConditionChemistry.Presentation;
            var lineup = new OwnerPregamePlayerSnapshot[preset.StartingLineupSlots.Count];
            for (int index = 0; index < lineup.Length; index++)
            {
                LineupPresetSlot slot = preset.StartingLineupSlots[index];
                PlayerSeasonDefinition season = GetPlayerSeason(runtime, slot.CardId);
                OwnerModeConditionEntry condition = conditions[season.PlayerPersonId];
                EffectiveMatchCondition effective = condition.EffectiveCondition;
                lineup[index] = new OwnerPregamePlayerSnapshot(
                    slot.CardId,
                    condition.DisplayName,
                    FormatPosition(slot.Position),
                    OwnerConditionChemistryPresentationBuilder.FormatCondition(
                        effective.StoredBaseCondition,
                        presentation),
                    FormatModifier(effective.LineupChemistryModifier),
                    condition.IsPitcher ? FormatModifier(effective.BatteryChemistryModifier) : "해당 없음",
                    OwnerConditionChemistryPresentationBuilder.FormatCondition(effective.Value, presentation));
            }

            var displayTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            AddRosterDisplayNames(runtime, runtime.GetRoster(runtime.PlayerTeamSeasonKey), displayTexts);
            AddRosterDisplayNames(runtime, runtime.GetRoster(preparation.OpponentTeamSeasonKey), displayTexts);
            var tactics = new string[preset.DefaultTacticCardIds.Count];
            for (int index = 0; index < tactics.Length; index++)
                tactics[index] = manager.GetTacticDisplayName(preset.DefaultTacticCardIds[index]);
            string[] selectedTeamColors = CreateSelectedTeamColorTexts(manager, preset);
            var presetSnapshots = new OwnerPregamePresetSnapshot[mode.LineupPresets.Count];
            for (int index = 0; index < presetSnapshots.Length; index++)
            {
                LineupPresetState candidate = mode.LineupPresets[index];
                LineupPresetValidationResult validation = string.Equals(
                        candidate.PresetId,
                        preset.PresetId,
                        StringComparison.Ordinal)
                    ? preparation.PresetValidation
                    : manager.ValidateLineupPreset(candidate);
                presetSnapshots[index] = new OwnerPregamePresetSnapshot(
                    candidate.PresetId,
                    candidate.Name,
                    validation);
            }
            return new OwnerPregameSnapshot(
                UiContentStateModel.Ready,
                CreateNextMatchText(manager, mode, preparation.ScheduledGame),
                manager.GetTeamDisplayName(preparation.OpponentTeamSeasonKey),
                preparation.ScoutingReport,
                presetSnapshots,
                preset.PresetId,
                lineup,
                selectedTeamColors,
                tactics,
                displayTexts,
                preparation.CanStartGame,
                preparation.CanStartGame ? string.Empty : "현재 로스터·프리셋 검증을 통과하지 못했습니다.");
        }

        public IReadOnlyList<OwnerConditionPlayerSnapshot> CreateConditionChemistry(OwnerModeManager manager)
        {
            IReadOnlyList<OwnerModeConditionEntry> entries = manager.BuildConditionEntries();
            var result = new OwnerConditionPlayerSnapshot[entries.Count];
            for (int index = 0; index < result.Length; index++)
            {
                OwnerModeConditionEntry entry = entries[index];
                result[index] = new OwnerConditionPlayerSnapshot(
                    entry.PlayerPersonId,
                    entry.DisplayName,
                    FormatPosition(entry.NaturalPosition),
                    entry.IsPitcher,
                    entry.Availability,
                    entry.EffectiveCondition);
            }
            return result;
        }

        private static ManagerHistoricalRuntimeState RequireRuntime(OwnerModeManager manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.Runtime ?? throw new InvalidOperationException("활성 구단주 Runtime이 없습니다.");
        }

        private static string CreateNextMatchText(
            OwnerModeManager manager,
            ManagerModeRuntimeState mode,
            ScheduledGameState game)
        {
            int opponentId = game.AwayTeamId == mode.LiveSeason.PlayerTeamId
                ? game.HomeTeamId
                : game.AwayTeamId;
            string opponentKey = mode.LiveSeason.GetTeamSeasonKey(opponentId);
            bool isHome = game.HomeTeamId == mode.LiveSeason.PlayerTeamId;
            return $"{game.Round}R · {(isHome ? "홈" : "원정")} vs {manager.GetTeamDisplayName(opponentKey)}";
        }

        private static PlayerSeasonDefinition GetPlayerSeason(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException($"CardId {cardId} 원본이 없습니다.");
            return runtime.WorldCardCatalog.GetPlayerSeason(card);
        }

        private static void AddRosterDisplayNames(
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster,
            IDictionary<string, string> output)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                output[entry.CardId] = runtime.IdentityRegistry.GetPlayerDisplayName(entry.PlayerPersonId);
            }
        }

        private static int GetMaximumFacilityLevel(
            ClubOperationBalanceTable balance,
            FacilityType type,
            int currentLevel)
        {
            int maximum = currentLevel;
            while (balance.TryGetNextFacilityLevel(type, maximum, out FacilityLevelDefinition next))
                maximum = next.Level;
            return maximum;
        }

        private static string FormatUpgradeStatus(ClubUpgradeStatus status)
        {
            return status switch
            {
                ClubUpgradeStatus.MaximumLevel => "최대 레벨",
                ClubUpgradeStatus.InsufficientMoney => "Money 부족",
                ClubUpgradeStatus.LeagueGradeLocked => "리그 등급 조건 미달",
                ClubUpgradeStatus.FanBaseLocked => "FanBase 조건 미달",
                ClubUpgradeStatus.SeasonAttendanceLocked => "누적 관중 조건 미달",
                ClubUpgradeStatus.AlreadyApplied => "이미 반영됨",
                _ => "현재 상태에서 업그레이드 불가"
            };
        }

        private static string FormatStaffStatus(StaffServiceStatus status)
        {
            return status switch
            {
                StaffServiceStatus.InsufficientMoney => "Money 부족",
                StaffServiceStatus.StaffUnavailable => "Staff가 이미 계약 중입니다.",
                StaffServiceStatus.SalaryNotSettled => "기존 급여 정산 필요",
                _ => "현재 계약 불가"
            };
        }

        private static string FormatStaffEffect(StaffRole role, TeamStaffEffectProfile effects)
        {
            return role switch
            {
                StaffRole.HittingCoach => $"타자 훈련 효율 {(effects.HittingTrainingEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.PitchingCoach => $"투수 훈련 효율 {(effects.PitchingTrainingEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.DevelopmentCoach => $"DP 사용 효율 {(effects.DevelopmentPointEfficiency - 1d):+0%;-0%;0%}",
                StaffRole.ConditioningCoach => $"회복 효율 {(effects.ConditionRecoveryEfficiency - 1d):+0%;-0%;0%}",
                _ => $"상대 분석 신뢰도 {effects.ScoutingConfidenceModifier:+0%;-0%;0%}"
            };
        }

        private static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string FormatTeamColor(TeamColorDefinition definition)
        {
            string family = definition.Family switch
            {
                TeamColorFamily.YearFranchise => "연도·구단",
                TeamColorFamily.Franchise => "구단",
                TeamColorFamily.Year => "연도",
                TeamColorFamily.AllStar => "올스타",
                TeamColorFamily.GoldenGlove => "골든글러브",
                TeamColorFamily.Mvp => "MVP",
                _ => "팀컬러"
            };
            return $"{family} {definition.RequiredCount}명 · 효과 {definition.StrengthScore}";
        }

        private static string[] CreateSelectedTeamColorTexts(
            OwnerModeManager manager,
            LineupPresetState preset)
        {
            IReadOnlyList<TeamColorDefinition> candidates = manager.GetAvailableTeamColors();
            var result = new string[LineupPresetState.TeamColorSlotCount];
            for (int slotIndex = 0; slotIndex < result.Length; slotIndex++)
            {
                string selectedId = preset.TeamColorIds[slotIndex];
                result[slotIndex] = "선택 없음";
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    if (!string.Equals(candidates[candidateIndex].TeamColorId, selectedId,
                            StringComparison.Ordinal))
                        continue;
                    result[slotIndex] = FormatTeamColor(candidates[candidateIndex]);
                    break;
                }
                if (!string.IsNullOrEmpty(selectedId) && result[slotIndex] == "선택 없음")
                    result[slotIndex] = $"{selectedId} · 사용 불가";
            }
            return result;
        }

        private static string FormatModifier(int value) => value > 0 ? "+" + value : value.ToString();

        private static string FormatRosterIssue(RosterValidationIssue issue)
        {
            return $"{issue.Code}: 필요 {issue.Expected}, 현재 {issue.Actual}";
        }
    }
}

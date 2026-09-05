using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 Runtime 상태와 버전이 명시된 저장 DTO를 손실 없이 변환한다.</summary>
    public sealed class ManagerHistoricalSaveAdapter
    {
        public const int CurrentSaveVersion = 4;
        private const int FirstSupportedSaveVersion = 1;

        private readonly IHistoricalContentProvider _contentProvider;
        private readonly CardEditionBalanceTable _cardEditionBalance;
        private readonly WorldHistorySaveMapper _worldHistoryMapper;
        private readonly BalanceTable _balance;

        public ManagerHistoricalSaveAdapter(
            IHistoricalContentProvider contentProvider,
            CardEditionBalanceTable cardEditionBalance,
            WorldHistorySaveMapper worldHistoryMapper = null,
            BalanceTable balance = null)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _cardEditionBalance = cardEditionBalance ?? throw new ArgumentNullException(nameof(cardEditionBalance));
            _worldHistoryMapper = worldHistoryMapper ?? new WorldHistorySaveMapper();
            _balance = balance ?? BalanceTable.CreateDefault();
        }

        public ManagerHistoricalSaveData CreateSaveData(ManagerHistoricalRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            ManagerModeRuntimeState managerMode = state.ManagerMode ?? CreateInitialManagerMode(state);
            return new ManagerHistoricalSaveData
            {
                saveVersion = CurrentSaveVersion,
                contentReference = HistoricalContentReferenceMapper.CreateSaveData(state.ContentReference),
                playerTeamSeasonKey = state.PlayerTeamSeasonKey,
                identityRegistry = CreateIdentityRegistry(state.IdentityRegistry),
                worldHistory = _worldHistoryMapper.CreateSaveData(state.WorldHistory),
                league = CreateLeague(state.League),
                rosters = CreateRosters(state.Rosters),
                ownedCards = CreateOwnedCards(state.OwnedCards),
                economy = new ManagerEconomySaveData
                {
                    money = state.Economy.Money,
                    scoutingPoints = state.Economy.ScoutingPoints,
                    developmentPoints = state.Economy.DevelopmentPoints,
                    pityGauge = state.Economy.PityGauge
                },
                managerMode = CreateManagerMode(managerMode, state.WorldHistory.WorldHistorySeed)
            };
        }

        public ManagerHistoricalRuntimeState Restore(ManagerHistoricalSaveData saveData)
        {
            if (saveData == null)
                throw new ArgumentNullException(nameof(saveData));
            if (saveData.saveVersion < FirstSupportedSaveVersion || saveData.saveVersion > CurrentSaveVersion)
                throw new InvalidOperationException(
                    $"Manager Historical SaveVersion {saveData.saveVersion}은 지원 범위 {FirstSupportedSaveVersion}~{CurrentSaveVersion}과 호환되지 않습니다.");

            HistoricalContentReference contentReference = HistoricalContentReferenceMapper.Restore(
                Require(saveData.contentReference, nameof(saveData.contentReference)));
            HistoricalBakedContent bakedContent = _contentProvider.Load()
                ?? throw new InvalidOperationException("Runtime Historical Content Provider가 null을 반환했습니다.");
            contentReference.EnsureMatches(bakedContent.Manifest);

            WorldHistorySnapshot history = _worldHistoryMapper.Restore(
                Require(saveData.worldHistory, nameof(saveData.worldHistory)));
            ValidateWorldHistoryReferences(history, bakedContent);
            WorldIdentityRegistry identityRegistry = saveData.saveVersion == 1
                ? new WorldIdentityGenerator().Generate(
                    bakedContent.PlayerPersons,
                    bakedContent.TeamSeasons,
                    bakedContent.IdentityNameCatalog,
                    history.WorldHistorySeed)
                : RestoreIdentityRegistry(
                    Require(saveData.identityRegistry, nameof(saveData.identityRegistry)));
            ValidateIdentityReferences(identityRegistry, bakedContent);
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                bakedContent.PlayerSeasons,
                history.Awards,
                _cardEditionBalance);
            LeagueInstance league = RestoreLeague(Require(saveData.league, nameof(saveData.league)));
            CurrentRosterState[] rosters = RestoreRosters(Require(saveData.rosters, nameof(saveData.rosters)));
            OwnedPlayerCardState[] ownedCards = RestoreOwnedCards(Require(saveData.ownedCards, nameof(saveData.ownedCards)));
            ManagerEconomySaveData economyData = Require(saveData.economy, nameof(saveData.economy));
            ManagerModeRuntimeState managerMode = saveData.saveVersion < CurrentSaveVersion
                ? CreateInitialManagerMode(
                    saveData.playerTeamSeasonKey,
                    FindOriginYear(saveData.playerTeamSeasonKey, bakedContent),
                    history.WorldHistorySeed,
                    league,
                    rosters,
                    identityRegistry)
                : RestoreManagerMode(
                    Require(saveData.managerMode, nameof(saveData.managerMode)),
                    identityRegistry,
                    history.WorldHistorySeed);

            return new ManagerHistoricalRuntimeState(
                saveData.playerTeamSeasonKey,
                contentReference,
                identityRegistry,
                history,
                catalog,
                league,
                rosters,
                ownedCards,
                new ManagerEconomyState(
                    economyData.money,
                    economyData.scoutingPoints,
                    economyData.developmentPoints,
                    economyData.pityGauge),
                managerMode);
        }

        private ManagerModeRuntimeState CreateInitialManagerMode(ManagerHistoricalRuntimeState state)
        {
            int originYear = FindOriginYear(state.GetRoster(state.PlayerTeamSeasonKey), state.WorldCardCatalog);
            return CreateInitialManagerMode(
                state.PlayerTeamSeasonKey,
                originYear,
                state.WorldHistory.WorldHistorySeed,
                state.League,
                state.Rosters,
                state.IdentityRegistry);
        }

        private ManagerModeRuntimeState CreateInitialManagerMode(
            string playerTeamSeasonKey,
            int originYear,
            ulong worldSeed,
            LeagueInstance league,
            IReadOnlyList<CurrentRosterState> rosters,
            WorldIdentityRegistry identities)
        {
            int countPerRole = _balance.Staff.Market.OffseasonOfferCount;
            StaffCatalog staffCatalog = CreateStaffCatalog(identities, worldSeed, countPerRole);
            return ManagerModeRuntimeFactory.CreateInitial(
                playerTeamSeasonKey,
                originYear,
                worldSeed,
                league,
                rosters,
                staffCatalog,
                _balance);
        }

        private ManagerModeSaveData CreateManagerMode(ManagerModeRuntimeState source, ulong worldSeed)
        {
            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            if (source.StaffCatalog.Staff.Count % roleCount != 0)
                throw new InvalidOperationException("StaffCatalog 수가 역할 수의 배수가 아닙니다.");

            return new ManagerModeSaveData
            {
                staffCatalogVersion = StaffCatalogGenerator.CurrentVersion,
                staffCatalogSeed = worldSeed,
                staffCountPerRole = source.StaffCatalog.Staff.Count / roleCount,
                clubOperation = CreateClubOperation(source.ClubOperation),
                staffContracts = CreateStaffContracts(source.StaffContracts),
                staffAssignment = CreateStaffAssignment(source.StaffAssignment),
                lineupPresets = CreateLineupPresets(source.LineupPresets),
                selectedLineupPresetId = source.SelectedLineupPresetId,
                playerStatuses = CreatePlayerStatuses(source.PlayerStatuses),
                familiarities = CreateFamiliarities(source.Familiarities),
                liveSeason = CreateLiveSeason(source.LiveSeason)
            };
        }

        private ManagerModeRuntimeState RestoreManagerMode(
            ManagerModeSaveData source,
            WorldIdentityRegistry identities,
            ulong expectedWorldSeed)
        {
            if (!string.Equals(
                    source.staffCatalogVersion,
                    StaffCatalogGenerator.CurrentVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"StaffCatalog version {source.staffCatalogVersion}은 현재 {StaffCatalogGenerator.CurrentVersion}과 호환되지 않습니다.");
            }
            if (source.staffCountPerRole <= 0)
                throw new ArgumentOutOfRangeException(nameof(source.staffCountPerRole));
            if (source.staffCatalogSeed != expectedWorldSeed)
                throw new InvalidOperationException("StaffCatalog seed가 WorldHistory seed와 다릅니다.");

            StaffCatalog catalog = CreateStaffCatalog(
                identities,
                source.staffCatalogSeed,
                source.staffCountPerRole);
            return new ManagerModeRuntimeState(
                RestoreClubOperation(Require(source.clubOperation, nameof(source.clubOperation))),
                catalog,
                RestoreStaffContracts(Require(source.staffContracts, nameof(source.staffContracts))),
                RestoreStaffAssignment(Require(source.staffAssignment, nameof(source.staffAssignment))),
                RestoreLineupPresets(Require(source.lineupPresets, nameof(source.lineupPresets))),
                source.selectedLineupPresetId,
                RestorePlayerStatuses(Require(source.playerStatuses, nameof(source.playerStatuses))),
                RestoreFamiliarities(Require(source.familiarities, nameof(source.familiarities))),
                RestoreLiveSeason(Require(source.liveSeason, nameof(source.liveSeason))));
        }

        private StaffCatalog CreateStaffCatalog(
            WorldIdentityRegistry identities,
            ulong worldSeed,
            int countPerRole)
        {
            int requiredNameCount = checked(Enum.GetValues(typeof(StaffRole)).Length * countPerRole);
            if (identities.PlayerIdentities.Count < requiredNameCount)
                throw new InvalidOperationException("가상 스태프 생성에 필요한 이름 후보가 부족합니다.");
            var sorted = new WorldPlayerIdentity[identities.PlayerIdentities.Count];
            for (int index = 0; index < sorted.Length; index++)
                sorted[index] = identities.PlayerIdentities[index];
            Array.Sort(sorted, (left, right) => string.CompareOrdinal(left.PlayerPersonId, right.PlayerPersonId));
            var names = new string[requiredNameCount];
            for (int index = 0; index < names.Length; index++) names[index] = sorted[index].DisplayName;
            return new StaffCatalogGenerator().Generate(
                new StaffNameCatalog(names),
                countPerRole,
                worldSeed,
                _balance.Staff);
        }

        private static ClubOperationSaveData CreateClubOperation(ClubOperationState source)
        {
            var facilities = new FacilitySaveData[source.Facilities.Count];
            for (int index = 0; index < facilities.Length; index++)
            {
                FacilityState facility = source.Facilities[index];
                facilities[index] = new FacilitySaveData { type = (int)facility.Type, level = facility.Level };
            }
            var receipts = new OperationReceiptSaveData[source.Receipts.Count];
            for (int index = 0; index < receipts.Length; index++)
            {
                OperationReceipt receipt = source.Receipts[index];
                receipts[index] = new OperationReceiptSaveData
                {
                    receiptId = receipt.ReceiptId,
                    kind = (int)receipt.Kind,
                    seasonId = receipt.SeasonId,
                    weekIndex = receipt.WeekIndex,
                    sourceId = receipt.SourceId,
                    money = receipt.ResourceDelta.Money,
                    scoutingPoints = receipt.ResourceDelta.ScoutingPoints,
                    developmentPoints = receipt.ResourceDelta.DevelopmentPoints
                };
            }
            Array.Sort(receipts, (left, right) => string.CompareOrdinal(left.receiptId, right.receiptId));
            WeeklyOperationLedger week = source.CurrentWeek;
            SeasonFinanceSummary season = source.CurrentSeason;
            return new ClubOperationSaveData
            {
                teamSeasonKey = source.TeamSeasonKey,
                fanBase = source.FanBase,
                popularity = source.Popularity,
                attendanceMomentum = source.AttendanceMomentum,
                stadiumLevel = source.Stadium.Level,
                stadiumCapacity = source.Stadium.Capacity,
                facilities = facilities,
                ticketPriceTier = (int)source.TicketPolicy.PriceTier,
                currentWeek = new WeeklyOperationLedgerSaveData
                {
                    seasonId = week.SeasonId,
                    weekIndex = week.WeekIndex,
                    moneyIncome = week.MoneyIncome,
                    moneyExpense = week.MoneyExpense,
                    scoutingPointProduction = week.ScoutingPointProduction,
                    developmentPointProduction = week.DevelopmentPointProduction,
                    homeGames = week.HomeGames,
                    attendance = week.Attendance,
                    receiptCount = week.ReceiptCount
                },
                currentSeason = new SeasonFinanceSummarySaveData
                {
                    seasonId = season.SeasonId,
                    homeGames = season.HomeGames,
                    attendance = season.Attendance,
                    ticketRevenue = season.TicketRevenue,
                    fanShopRevenue = season.FanShopRevenue,
                    otherGameRevenue = season.OtherGameRevenue,
                    gameOperatingCost = season.GameOperatingCost,
                    moneyIncome = season.MoneyIncome,
                    moneyExpense = season.MoneyExpense,
                    scoutingPointProduction = season.ScoutingPointProduction,
                    developmentPointProduction = season.DevelopmentPointProduction
                },
                receipts = receipts
            };
        }

        private static ClubOperationState RestoreClubOperation(ClubOperationSaveData source)
        {
            FacilitySaveData[] facilityData = Require(source.facilities, nameof(source.facilities));
            var facilities = new FacilityState[facilityData.Length];
            for (int index = 0; index < facilities.Length; index++)
            {
                FacilitySaveData facility = Require(facilityData[index], nameof(source.facilities));
                ValidateEnum<FacilityType>(facility.type, nameof(facility.type));
                facilities[index] = new FacilityState((FacilityType)facility.type, facility.level);
            }
            OperationReceiptSaveData[] receiptData = Require(source.receipts, nameof(source.receipts));
            var receipts = new OperationReceipt[receiptData.Length];
            for (int index = 0; index < receipts.Length; index++)
            {
                OperationReceiptSaveData receipt = Require(receiptData[index], nameof(source.receipts));
                ValidateEnum<OperationReceiptKind>(receipt.kind, nameof(receipt.kind));
                receipts[index] = new OperationReceipt(
                    receipt.receiptId,
                    (OperationReceiptKind)receipt.kind,
                    receipt.seasonId,
                    receipt.weekIndex,
                    receipt.sourceId,
                    new OperationResourceDelta(receipt.money, receipt.scoutingPoints, receipt.developmentPoints));
            }
            ValidateEnum<TicketPriceTier>(source.ticketPriceTier, nameof(source.ticketPriceTier));
            WeeklyOperationLedgerSaveData week = Require(source.currentWeek, nameof(source.currentWeek));
            SeasonFinanceSummarySaveData season = Require(source.currentSeason, nameof(source.currentSeason));
            return new ClubOperationState(
                source.teamSeasonKey,
                source.fanBase,
                source.popularity,
                source.attendanceMomentum,
                new StadiumState(source.stadiumLevel, source.stadiumCapacity),
                facilities,
                new TicketPolicy((TicketPriceTier)source.ticketPriceTier),
                new WeeklyOperationLedger(
                    week.seasonId,
                    week.weekIndex,
                    week.moneyIncome,
                    week.moneyExpense,
                    week.scoutingPointProduction,
                    week.developmentPointProduction,
                    week.homeGames,
                    week.attendance,
                    week.receiptCount),
                new SeasonFinanceSummary(
                    season.seasonId,
                    season.homeGames,
                    season.attendance,
                    season.ticketRevenue,
                    season.fanShopRevenue,
                    season.otherGameRevenue,
                    season.gameOperatingCost,
                    season.moneyIncome,
                    season.moneyExpense,
                    season.scoutingPointProduction,
                    season.developmentPointProduction),
                receipts);
        }

        private static StaffContractSaveData[] CreateStaffContracts(IReadOnlyList<StaffContractState> source)
        {
            var result = new StaffContractSaveData[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                StaffContractState contract = source[index];
                result[index] = new StaffContractSaveData
                {
                    contractId = contract.ContractId,
                    staffId = contract.StaffId,
                    teamSeasonKey = contract.TeamSeasonKey,
                    startSeason = contract.StartSeason,
                    remainingSeasons = contract.RemainingSeasons,
                    annualSalary = contract.AnnualSalary,
                    hasLastSalaryPaidSeason = contract.LastSalaryPaidSeason.HasValue,
                    lastSalaryPaidSeason = contract.LastSalaryPaidSeason.GetValueOrDefault()
                };
            }
            return result;
        }

        private static StaffContractState[] RestoreStaffContracts(StaffContractSaveData[] source)
        {
            var result = new StaffContractState[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                StaffContractSaveData contract = Require(source[index], nameof(source));
                result[index] = new StaffContractState(
                    contract.contractId,
                    contract.staffId,
                    contract.teamSeasonKey,
                    contract.startSeason,
                    contract.remainingSeasons,
                    contract.annualSalary,
                    contract.hasLastSalaryPaidSeason ? contract.lastSalaryPaidSeason : (int?)null);
            }
            return result;
        }

        private static TeamStaffAssignmentSaveData CreateStaffAssignment(TeamStaffAssignmentState source)
        {
            return new TeamStaffAssignmentSaveData
            {
                teamSeasonKey = source.TeamSeasonKey,
                hittingCoachStaffId = source.HittingCoachStaffId,
                pitchingCoachStaffId = source.PitchingCoachStaffId,
                developmentCoachStaffId = source.DevelopmentCoachStaffId,
                conditioningCoachStaffId = source.ConditioningCoachStaffId,
                scoutingDirectorStaffId = source.ScoutingDirectorStaffId
            };
        }

        private static TeamStaffAssignmentState RestoreStaffAssignment(TeamStaffAssignmentSaveData source)
        {
            return new TeamStaffAssignmentState(
                source.teamSeasonKey,
                source.hittingCoachStaffId,
                source.pitchingCoachStaffId,
                source.developmentCoachStaffId,
                source.conditioningCoachStaffId,
                source.scoutingDirectorStaffId);
        }

        private static LineupPresetSaveData[] CreateLineupPresets(IReadOnlyList<LineupPresetState> source)
        {
            var result = new LineupPresetSaveData[source.Count];
            for (int presetIndex = 0; presetIndex < result.Length; presetIndex++)
            {
                LineupPresetState preset = source[presetIndex];
                var slots = new LineupPresetSlotSaveData[preset.StartingLineupSlots.Count];
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    LineupPresetSlot slot = preset.StartingLineupSlots[slotIndex];
                    slots[slotIndex] = new LineupPresetSlotSaveData
                    {
                        cardId = slot.CardId,
                        position = (int)slot.Position
                    };
                }
                result[presetIndex] = new LineupPresetSaveData
                {
                    presetId = preset.PresetId,
                    name = preset.Name,
                    startingLineupSlots = slots,
                    battingOrderCardIds = CopyIds(preset.BattingOrderCardIds),
                    benchPriorityCardIds = CopyIds(preset.BenchPriorityCardIds),
                    starterRotationCardIds = CopyIds(preset.StarterRotationCardIds),
                    bullpenAssignmentCardIds = CopyIds(preset.BullpenAssignmentCardIds),
                    setupPitcherCardId = preset.SetupPitcherCardId,
                    closerPitcherCardId = preset.CloserPitcherCardId,
                    teamColorIds = CopyIds(preset.TeamColorIds),
                    defaultTacticCardIds = CopyIds(preset.DefaultTacticCardIds)
                };
            }
            return result;
        }

        private static LineupPresetState[] RestoreLineupPresets(LineupPresetSaveData[] source)
        {
            var result = new LineupPresetState[source.Length];
            for (int presetIndex = 0; presetIndex < result.Length; presetIndex++)
            {
                LineupPresetSaveData preset = Require(source[presetIndex], nameof(source));
                LineupPresetSlotSaveData[] slotData = Require(
                    preset.startingLineupSlots,
                    nameof(preset.startingLineupSlots));
                var slots = new LineupPresetSlot[slotData.Length];
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    LineupPresetSlotSaveData slot = Require(slotData[slotIndex], nameof(slotData));
                    ValidateEnum<PlayerPosition>(slot.position, nameof(slot.position));
                    slots[slotIndex] = new LineupPresetSlot(slot.cardId, (PlayerPosition)slot.position);
                }
                result[presetIndex] = new LineupPresetState(
                    preset.presetId,
                    preset.name,
                    slots,
                    Require(preset.battingOrderCardIds, nameof(preset.battingOrderCardIds)),
                    Require(preset.benchPriorityCardIds, nameof(preset.benchPriorityCardIds)),
                    Require(preset.starterRotationCardIds, nameof(preset.starterRotationCardIds)),
                    Require(preset.bullpenAssignmentCardIds, nameof(preset.bullpenAssignmentCardIds)),
                    preset.setupPitcherCardId,
                    preset.closerPitcherCardId,
                    Require(preset.teamColorIds, nameof(preset.teamColorIds)),
                    Require(preset.defaultTacticCardIds, nameof(preset.defaultTacticCardIds)));
            }
            return result;
        }

        private static TeamSeasonPlayerStatusSaveData[] CreatePlayerStatuses(
            IReadOnlyList<TeamSeasonPlayerStatusState> source)
        {
            var result = new TeamSeasonPlayerStatusSaveData[source.Count];
            for (int teamIndex = 0; teamIndex < result.Length; teamIndex++)
            {
                TeamSeasonPlayerStatusState team = source[teamIndex];
                var players = new PlayerStatusSaveData[team.Players.Count];
                for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
                {
                    TeamSeasonPlayerStatus player = team.Players[playerIndex];
                    players[playerIndex] = new PlayerStatusSaveData
                    {
                        playerPersonId = player.PlayerPersonId,
                        storedBaseCondition = player.StoredBaseCondition,
                        availability = (int)player.Availability,
                        previousDayPitches = player.PitchingWorkload.PreviousDayPitches,
                        twoDaysAgoPitches = player.PitchingWorkload.TwoDaysAgoPitches,
                        threeDaysAgoPitches = player.PitchingWorkload.ThreeDaysAgoPitches
                    };
                }
                Array.Sort(players, (left, right) => string.CompareOrdinal(left.playerPersonId, right.playerPersonId));
                result[teamIndex] = new TeamSeasonPlayerStatusSaveData
                {
                    teamSeasonKey = team.TeamSeasonKey,
                    players = players
                };
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.teamSeasonKey, right.teamSeasonKey));
            return result;
        }

        private static TeamSeasonPlayerStatusState[] RestorePlayerStatuses(
            TeamSeasonPlayerStatusSaveData[] source)
        {
            var result = new TeamSeasonPlayerStatusState[source.Length];
            for (int teamIndex = 0; teamIndex < result.Length; teamIndex++)
            {
                TeamSeasonPlayerStatusSaveData team = Require(source[teamIndex], nameof(source));
                PlayerStatusSaveData[] playerData = Require(team.players, nameof(team.players));
                var players = new TeamSeasonPlayerStatus[playerData.Length];
                for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
                {
                    PlayerStatusSaveData player = Require(playerData[playerIndex], nameof(team.players));
                    ValidateEnum<PlayerAvailabilityStatus>(player.availability, nameof(player.availability));
                    players[playerIndex] = new TeamSeasonPlayerStatus(
                        player.playerPersonId,
                        player.storedBaseCondition,
                        (PlayerAvailabilityStatus)player.availability,
                        new PitchingWorkloadState(
                            player.previousDayPitches,
                            player.twoDaysAgoPitches,
                            player.threeDaysAgoPitches));
                }
                result[teamIndex] = new TeamSeasonPlayerStatusState(team.teamSeasonKey, players);
            }
            return result;
        }

        private static TeamChemistryFamiliaritySaveData[] CreateFamiliarities(
            IReadOnlyList<TeamChemistryFamiliarityState> source)
        {
            var result = new TeamChemistryFamiliaritySaveData[source.Count];
            for (int teamIndex = 0; teamIndex < result.Length; teamIndex++)
            {
                TeamChemistryFamiliarityState team = source[teamIndex];
                var entries = new ChemistryFamiliarityEntrySaveData[team.Entries.Count];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ChemistryFamiliarityEntry entry = team.Entries[entryIndex];
                    entries[entryIndex] = new ChemistryFamiliarityEntrySaveData
                    {
                        firstPlayerPersonId = entry.Pair.FirstPlayerPersonId,
                        secondPlayerPersonId = entry.Pair.SecondPlayerPersonId,
                        lineupFamiliarity = entry.LineupFamiliarity,
                        batteryFamiliarity = entry.BatteryFamiliarity
                    };
                }
                result[teamIndex] = new TeamChemistryFamiliaritySaveData
                {
                    teamSeasonKey = team.TeamSeasonKey,
                    entries = entries
                };
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.teamSeasonKey, right.teamSeasonKey));
            return result;
        }

        private static TeamChemistryFamiliarityState[] RestoreFamiliarities(
            TeamChemistryFamiliaritySaveData[] source)
        {
            var result = new TeamChemistryFamiliarityState[source.Length];
            for (int teamIndex = 0; teamIndex < result.Length; teamIndex++)
            {
                TeamChemistryFamiliaritySaveData team = Require(source[teamIndex], nameof(source));
                ChemistryFamiliarityEntrySaveData[] entryData = Require(team.entries, nameof(team.entries));
                var entries = new ChemistryFamiliarityEntry[entryData.Length];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ChemistryFamiliarityEntrySaveData entry = Require(entryData[entryIndex], nameof(team.entries));
                    entries[entryIndex] = new ChemistryFamiliarityEntry(
                        new PlayerPersonPairKey(entry.firstPlayerPersonId, entry.secondPlayerPersonId),
                        entry.lineupFamiliarity,
                        entry.batteryFamiliarity);
                }
                result[teamIndex] = new TeamChemistryFamiliarityState(team.teamSeasonKey, entries);
            }
            return result;
        }

        private static ManagerLiveSeasonSaveData CreateLiveSeason(ManagerLiveSeasonState source)
        {
            var teams = new ManagerTeamReferenceSaveData[source.Teams.Count];
            for (int index = 0; index < teams.Length; index++)
            {
                ManagerTeamReference team = source.Teams[index];
                teams[index] = new ManagerTeamReferenceSaveData
                {
                    teamId = team.TeamId,
                    teamSeasonKey = team.TeamSeasonKey
                };
            }

            IReadOnlyList<ScheduledGameState> schedule = source.Schedule.Games;
            var games = new ManagerScheduledGameSaveData[schedule.Count];
            for (int index = 0; index < games.Length; index++)
            {
                ScheduledGameState game = schedule[index];
                ManagerScheduledGameSaveData saved = new ManagerScheduledGameSaveData
                {
                    gameId = game.GameId,
                    round = game.Round,
                    randomSeed = game.RandomSeed,
                    awayTeamId = game.AwayTeamId,
                    homeTeamId = game.HomeTeamId,
                    isCompleted = game.IsCompleted,
                    awayRuns = game.AwayRuns,
                    homeRuns = game.HomeRuns,
                    hasPlayerRolePlan = game.HasPlayerRolePlan,
                    plannedPlayerRole = (int)game.PlannedPlayerRole,
                    hasPlayerRoleDecision = game.HasPlayerRoleDecision
                };
                if (game.HasPlayerRoleDecision)
                {
                    saved.playerRoleDecisionReason = (int)game.PlayerRoleDecision.Reason;
                    saved.conditionAdjustment = game.PlayerRoleDecision.ConditionAdjustment;
                    saved.managerEvaluationAdjustment = game.PlayerRoleDecision.ManagerEvaluationAdjustment;
                    saved.decisionScore = game.PlayerRoleDecision.DecisionScore;
                    saved.requiredScore = game.PlayerRoleDecision.RequiredScore;
                }
                games[index] = saved;
            }
            return new ManagerLiveSeasonSaveData
            {
                seasonId = source.SeasonId,
                seasonNumber = source.SeasonNumber,
                originYear = source.OriginYear,
                currentWeekIndex = source.CurrentWeekIndex,
                playerTeamId = source.PlayerTeamId,
                teams = teams,
                games = games
            };
        }

        private static ManagerLiveSeasonState RestoreLiveSeason(ManagerLiveSeasonSaveData source)
        {
            ManagerTeamReferenceSaveData[] teamData = Require(source.teams, nameof(source.teams));
            var teams = new ManagerTeamReference[teamData.Length];
            for (int index = 0; index < teams.Length; index++)
            {
                ManagerTeamReferenceSaveData team = Require(teamData[index], nameof(source.teams));
                teams[index] = new ManagerTeamReference(team.teamId, team.teamSeasonKey);
            }

            ManagerScheduledGameSaveData[] gameData = Require(source.games, nameof(source.games));
            var games = new ScheduledGameState[gameData.Length];
            for (int index = 0; index < games.Length; index++)
            {
                ManagerScheduledGameSaveData saved = Require(gameData[index], nameof(source.games));
                var game = new ScheduledGameState(
                    saved.gameId,
                    saved.round,
                    saved.randomSeed,
                    saved.awayTeamId,
                    saved.homeTeamId);
                if (saved.hasPlayerRolePlan)
                {
                    ValidateEnum<PlayerGameRole>(saved.plannedPlayerRole, nameof(saved.plannedPlayerRole));
                    if (saved.hasPlayerRoleDecision)
                    {
                        ValidateEnum<ManagerUsageDecisionReason>(
                            saved.playerRoleDecisionReason,
                            nameof(saved.playerRoleDecisionReason));
                        game.PlanPlayerRole(new ManagerUsageDecision(
                            (PlayerGameRole)saved.plannedPlayerRole,
                            (ManagerUsageDecisionReason)saved.playerRoleDecisionReason,
                            saved.conditionAdjustment,
                            saved.managerEvaluationAdjustment,
                            saved.decisionScore,
                            saved.requiredScore));
                    }
                    else
                    {
                        game.PlanPlayerRole((PlayerGameRole)saved.plannedPlayerRole);
                    }
                }
                else if (saved.hasPlayerRoleDecision)
                {
                    throw new InvalidOperationException("Player role decision에는 role plan이 필요합니다.");
                }
                if (saved.isCompleted) game.Complete(saved.awayRuns, saved.homeRuns);
                games[index] = game;
            }
            return new ManagerLiveSeasonState(
                source.seasonId,
                source.seasonNumber,
                source.originYear,
                source.currentWeekIndex,
                source.playerTeamId,
                teams,
                new SeasonScheduleState(games));
        }

        private static string[] CopyIds(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static int FindOriginYear(string teamSeasonKey, HistoricalBakedContent content)
        {
            if (!content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                throw new InvalidOperationException($"{teamSeasonKey} TeamSeason 원본을 찾을 수 없습니다.");
            return team.OriginYear;
        }

        private static int FindOriginYear(CurrentRosterState roster, WorldCardCatalog catalog)
        {
            if (roster.Entries.Count == 0)
                throw new InvalidOperationException("플레이어 로스터가 비어 있습니다.");
            if (!catalog.TryGetCard(roster.Entries[0].CardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("플레이어 로스터 카드 원본을 찾을 수 없습니다.");
            return catalog.GetPlayerSeason(card).OriginYear;
        }

        private static WorldIdentityRegistrySaveData CreateIdentityRegistry(WorldIdentityRegistry source)
        {
            var players = new WorldPlayerIdentitySaveData[source.PlayerIdentities.Count];
            for (int index = 0; index < players.Length; index++)
            {
                WorldPlayerIdentity identity = source.PlayerIdentities[index];
                players[index] = new WorldPlayerIdentitySaveData
                {
                    playerPersonId = identity.PlayerPersonId,
                    displayName = identity.DisplayName
                };
            }
            Array.Sort(players, (left, right) =>
                StringComparer.Ordinal.Compare(left.playerPersonId, right.playerPersonId));

            var franchises = new WorldFranchiseIdentitySaveData[source.FranchiseIdentities.Count];
            for (int index = 0; index < franchises.Length; index++)
            {
                WorldFranchiseIdentity identity = source.FranchiseIdentities[index];
                franchises[index] = new WorldFranchiseIdentitySaveData
                {
                    franchiseId = identity.FranchiseId,
                    displayName = identity.DisplayName
                };
            }
            Array.Sort(franchises, (left, right) =>
                StringComparer.Ordinal.Compare(left.franchiseId, right.franchiseId));
            return new WorldIdentityRegistrySaveData
            {
                identityGeneratorVersion = source.IdentityGeneratorVersion,
                identitySeed = source.IdentitySeed,
                players = players,
                franchises = franchises
            };
        }

        private static WorldIdentityRegistry RestoreIdentityRegistry(WorldIdentityRegistrySaveData source)
        {
            WorldPlayerIdentitySaveData[] playerData = Require(source.players, nameof(source.players));
            var players = new WorldPlayerIdentity[playerData.Length];
            for (int index = 0; index < players.Length; index++)
            {
                WorldPlayerIdentitySaveData identity = Require(playerData[index], nameof(source.players));
                players[index] = new WorldPlayerIdentity(identity.playerPersonId, identity.displayName);
            }

            WorldFranchiseIdentitySaveData[] franchiseData = Require(source.franchises, nameof(source.franchises));
            var franchises = new WorldFranchiseIdentity[franchiseData.Length];
            for (int index = 0; index < franchises.Length; index++)
            {
                WorldFranchiseIdentitySaveData identity = Require(franchiseData[index], nameof(source.franchises));
                franchises[index] = new WorldFranchiseIdentity(identity.franchiseId, identity.displayName);
            }
            return new WorldIdentityRegistry(
                source.identityGeneratorVersion,
                source.identitySeed,
                players,
                franchises);
        }

        private static void ValidateIdentityReferences(
            WorldIdentityRegistry identities,
            HistoricalBakedContent content)
        {
            if (identities.PlayerIdentities.Count != content.PlayerPersons.Count)
                throw new InvalidOperationException("저장된 Player Identity 수가 현재 Canonical Person 수와 다릅니다.");
            for (int index = 0; index < content.PlayerPersons.Count; index++)
                identities.GetPlayerDisplayName(content.PlayerPersons[index].PlayerPersonId);

            var franchiseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < content.TeamSeasons.Count; index++)
                franchiseIds.Add(content.TeamSeasons[index].FranchiseId);
            if (identities.FranchiseIdentities.Count != franchiseIds.Count)
                throw new InvalidOperationException("저장된 Franchise Identity 수가 현재 Canonical Franchise 수와 다릅니다.");
            foreach (string franchiseId in franchiseIds)
                identities.GetFranchiseDisplayName(franchiseId);
        }

        /// <summary>저장된 파생 기록이 현재 고정 Content의 동일 선수·구단·연도를 가리키는지 검증한다.</summary>
        private static void ValidateWorldHistoryReferences(
            WorldHistorySnapshot history,
            HistoricalBakedContent bakedContent)
        {
            for (int index = 0; index < history.Statistics.Count; index++)
            {
                SeasonStatistics statistics = history.Statistics[index];
                if (!bakedContent.TryGetPlayerSeason(
                        statistics.PlayerSeasonId,
                        out PlayerSeasonDefinition playerSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History가 현재 Content에 없는 PlayerSeasonId를 참조합니다: " +
                        $"{statistics.PlayerSeasonId}");
                }
                if (!bakedContent.TryGetTeamSeason(
                        statistics.TeamSeasonKey,
                        out TeamSeasonDefinition teamSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History가 현재 Content에 없는 TeamSeasonKey를 참조합니다: " +
                        $"{statistics.TeamSeasonKey}");
                }
                if (statistics.SeasonYear != playerSeason.OriginYear ||
                    statistics.SeasonYear != teamSeason.OriginYear)
                {
                    throw new InvalidOperationException(
                        $"저장된 World History의 SeasonYear가 Baked Content와 다릅니다: " +
                        $"playerSeasonId={statistics.PlayerSeasonId}, teamSeasonKey={statistics.TeamSeasonKey}, " +
                        $"saved={statistics.SeasonYear}, player={playerSeason.OriginYear}, team={teamSeason.OriginYear}");
                }
                if (!string.Equals(
                        statistics.TeamSeasonKey,
                        playerSeason.OriginTeamSeasonKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History의 TeamSeasonKey가 PlayerSeason 원소속과 다릅니다: " +
                        $"playerSeasonId={statistics.PlayerSeasonId}, saved={statistics.TeamSeasonKey}, " +
                        $"expected={playerSeason.OriginTeamSeasonKey}");
                }
            }

            for (int index = 0; index < history.Awards.Entries.Count; index++)
            {
                WorldAwardEntry award = history.Awards.Entries[index];
                if (!bakedContent.TryGetPlayerSeason(
                        award.PlayerSeasonId,
                        out PlayerSeasonDefinition playerSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World Award가 현재 Content에 없는 PlayerSeasonId를 참조합니다: " +
                        $"{award.PlayerSeasonId}");
                }
                if (award.SeasonYear != playerSeason.OriginYear)
                {
                    throw new InvalidOperationException(
                        $"저장된 World Award의 SeasonYear가 Baked PlayerSeason과 다릅니다: " +
                        $"playerSeasonId={award.PlayerSeasonId}, saved={award.SeasonYear}, " +
                        $"expected={playerSeason.OriginYear}");
                }
            }

            for (int index = 0; index < history.TeamStatistics.Count; index++)
            {
                TeamSeasonStatistics statistics = history.TeamStatistics[index];
                ValidateTeamSeasonReference(
                    statistics.TeamSeasonKey,
                    statistics.SeasonYear,
                    bakedContent,
                    "팀 통계");
            }

            for (int index = 0; index < history.Standings.Count; index++)
            {
                HistoricalStandingEntry standing = history.Standings[index];
                ValidateTeamSeasonReference(
                    standing.TeamSeasonKey,
                    standing.SeasonYear,
                    bakedContent,
                    "순위");
            }

            for (int index = 0; index < history.PostseasonResults.Count; index++)
            {
                HistoricalPostseasonResult postseason = history.PostseasonResults[index];
                for (int qualifierIndex = 0;
                     qualifierIndex < postseason.QualifiedTeamSeasonKeys.Count;
                     qualifierIndex++)
                {
                    ValidateTeamSeasonReference(
                        postseason.QualifiedTeamSeasonKeys[qualifierIndex],
                        postseason.SeasonYear,
                        bakedContent,
                        "Postseason 진출 구단");
                }
                ValidateTeamSeasonReference(
                    postseason.ChampionTeamSeasonKey,
                    postseason.SeasonYear,
                    bakedContent,
                    "Champion");
            }
        }

        private static void ValidateTeamSeasonReference(
            string teamSeasonKey,
            int seasonYear,
            HistoricalBakedContent bakedContent,
            string recordName)
        {
            if (!bakedContent.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition teamSeason))
            {
                throw new InvalidOperationException(
                    $"저장된 World History {recordName}이 현재 Content에 없는 TeamSeasonKey를 참조합니다: " +
                    teamSeasonKey);
            }
            if (teamSeason.OriginYear != seasonYear)
            {
                throw new InvalidOperationException(
                    $"저장된 World History {recordName}의 SeasonYear가 Baked TeamSeason과 다릅니다: " +
                    $"teamSeasonKey={teamSeasonKey}, saved={seasonYear}, expected={teamSeason.OriginYear}");
            }
        }

        private static LeagueInstanceSaveData CreateLeague(LeagueInstance league)
        {
            var regular = new string[league.RegularTeamSeasonKeys.Count];
            for (int index = 0; index < regular.Length; index++)
                regular[index] = league.RegularTeamSeasonKeys[index];
            var special = new SpecialCompositeTeamRegistrationSaveData[league.SpecialCompositeTeams.Count];
            for (int index = 0; index < special.Length; index++)
            {
                SpecialCompositeTeamRegistration registration = league.SpecialCompositeTeams[index];
                special[index] = new SpecialCompositeTeamRegistrationSaveData
                {
                    teamSeasonKey = registration.TeamSeasonKey,
                    originYear = registration.OriginYear,
                    teamType = (int)registration.TeamType
                };
            }
            Array.Sort(special, (left, right) => left.teamType.CompareTo(right.teamType));
            return new LeagueInstanceSaveData
            {
                leagueInstanceId = league.LeagueInstanceId,
                grade = (int)league.Grade,
                regularTeamSeasonKeys = regular,
                specialCompositeTeams = special
            };
        }

        private static LeagueInstance RestoreLeague(LeagueInstanceSaveData source)
        {
            ValidateEnum<LeagueGrade>(source.grade, nameof(source.grade));
            string[] regular = Require(source.regularTeamSeasonKeys, nameof(source.regularTeamSeasonKeys));
            SpecialCompositeTeamRegistrationSaveData[] specialData =
                Require(source.specialCompositeTeams, nameof(source.specialCompositeTeams));
            var special = new SpecialCompositeTeamRegistration[specialData.Length];
            for (int index = 0; index < special.Length; index++)
            {
                SpecialCompositeTeamRegistrationSaveData registration =
                    Require(specialData[index], nameof(source.specialCompositeTeams));
                ValidateEnum<SpecialCompositeTeamType>(registration.teamType, nameof(registration.teamType));
                special[index] = new SpecialCompositeTeamRegistration(
                    registration.teamSeasonKey,
                    registration.originYear,
                    (SpecialCompositeTeamType)registration.teamType);
            }
            return new LeagueInstance(source.leagueInstanceId, (LeagueGrade)source.grade, regular, special);
        }

        private static CurrentRosterSaveData[] CreateRosters(IReadOnlyList<CurrentRosterState> source)
        {
            var rosters = new CurrentRosterSaveData[source.Count];
            for (int rosterIndex = 0; rosterIndex < source.Count; rosterIndex++)
            {
                CurrentRosterState roster = source[rosterIndex];
                var entries = new ActiveRosterEntrySaveData[roster.Entries.Count];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ActiveRosterEntry entry = roster.Entries[entryIndex];
                    entries[entryIndex] = new ActiveRosterEntrySaveData
                    {
                        cardId = entry.CardId,
                        playerSeasonId = entry.PlayerSeasonId,
                        playerPersonId = entry.PlayerPersonId,
                        registrationType = (int)entry.RegistrationType,
                        role = (int)entry.Role
                    };
                }
                Array.Sort(entries, CompareRosterEntries);
                rosters[rosterIndex] = new CurrentRosterSaveData
                {
                    teamSeasonKey = roster.TeamSeasonKey,
                    entries = entries
                };
            }
            Array.Sort(rosters, (left, right) => StringComparer.Ordinal.Compare(left.teamSeasonKey, right.teamSeasonKey));
            return rosters;
        }

        private static CurrentRosterState[] RestoreRosters(CurrentRosterSaveData[] source)
        {
            var rosters = new CurrentRosterState[source.Length];
            for (int rosterIndex = 0; rosterIndex < source.Length; rosterIndex++)
            {
                CurrentRosterSaveData roster = Require(source[rosterIndex], nameof(source));
                ActiveRosterEntrySaveData[] entriesData = Require(roster.entries, nameof(roster.entries));
                var entries = new ActiveRosterEntry[entriesData.Length];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ActiveRosterEntrySaveData entry = Require(entriesData[entryIndex], nameof(roster.entries));
                    ValidateEnum<RegistrationType>(entry.registrationType, nameof(entry.registrationType));
                    ValidateEnum<ActiveRosterRole>(entry.role, nameof(entry.role));
                    entries[entryIndex] = new ActiveRosterEntry(
                        entry.cardId,
                        entry.playerSeasonId,
                        entry.playerPersonId,
                        (RegistrationType)entry.registrationType,
                        (ActiveRosterRole)entry.role);
                }
                rosters[rosterIndex] = new CurrentRosterState(roster.teamSeasonKey, entries);
            }
            return rosters;
        }

        private static OwnedPlayerCardSaveData[] CreateOwnedCards(IReadOnlyList<OwnedPlayerCardState> source)
        {
            var result = new OwnedPlayerCardSaveData[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                OwnedPlayerCardState card = source[index];
                var training = new int[PlayerAbilityCatalog.AbilityCount];
                for (int abilityIndex = 0; abilityIndex < training.Length; abilityIndex++)
                    training[abilityIndex] = card.Training.GetBonus((PlayerAbility)abilityIndex);
                result[index] = new OwnedPlayerCardSaveData
                {
                    cardId = card.CardId,
                    enhancementLevel = card.EnhancementLevel,
                    duplicateCount = card.DuplicateCount,
                    isLocked = card.IsLocked,
                    isFavorite = card.IsFavorite,
                    trainingBonuses = training
                };
            }
            Array.Sort(result, (left, right) => StringComparer.Ordinal.Compare(left.cardId, right.cardId));
            return result;
        }

        private static OwnedPlayerCardState[] RestoreOwnedCards(OwnedPlayerCardSaveData[] source)
        {
            var result = new OwnedPlayerCardState[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                OwnedPlayerCardSaveData card = Require(source[index], nameof(source));
                result[index] = new OwnedPlayerCardState(
                    card.cardId,
                    card.enhancementLevel,
                    card.duplicateCount,
                    card.isLocked,
                    card.isFavorite,
                    new CardTrainingState(Require(card.trainingBonuses, nameof(card.trainingBonuses))));
            }
            return result;
        }

        private static int CompareRosterEntries(ActiveRosterEntrySaveData left, ActiveRosterEntrySaveData right)
        {
            int comparison = left.role.CompareTo(right.role);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
        }

        private static T Require<T>(T value, string parameterName) where T : class
        {
            if (value == null)
                throw new ArgumentException("세이브 필수 값이 없습니다.", parameterName);
            return value;
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName, value, "저장된 enum 값이 유효하지 않습니다.");
        }
    }
}

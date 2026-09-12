using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Shop;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 Runtime 상태와 버전이 명시된 저장 DTO를 손실 없이 변환한다.</summary>
    public sealed partial class ManagerHistoricalSaveAdapter
    {
        public const int CurrentSaveVersion = 28;
        private const int OwnerPostseasonSaveVersion = 18;
        private const int ManagerModeSaveVersion = 4;
        // v5까지는 전술 수집·상점 이력이 없었고, v6부터 현재 시즌 개인 기록이 추가됐다.
        // 개인 기록은 없으면 빈 상태로 복원되므로 별도 버전 분기가 필요 없다.
        private const int TacticAndShopSaveVersion = 5;
        private const int OwnerProfileSaveVersion = 7;
        private const int OwnerGrowthSaveVersion = 8;
        private const int DugoutManagementSaveVersion = 9;
        private const int CompletedSeasonsSaveVersion = 10;
        private const int OwnerPlayerContractSaveVersion = 11;
        private const int ScheduledTacticsSaveVersion = 12;
        private const int GrowthSourceBreakdownSaveVersion = 13;
        private const int ActiveRosterContractSyncSaveVersion = 14;
        private const int CollectionWishlistSaveVersion = 15;
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
            if (managerMode.PlayerContracts.Count == 0)
            {
                var marketResolver = new OwnerPlayerMarketResolver(_balance.OwnerPlayerMarket);
                managerMode.ReplacePlayerContractState(
                    marketResolver.CreateInitialContracts(
                        state.GetRoster(state.PlayerTeamSeasonKey),
                        state.WorldCardCatalog,
                        managerMode.LiveSeason.SeasonNumber));
            }
            else
            {
                RepairKnownActiveRosterContractMismatch(
                    managerMode,
                    state.GetRoster(state.PlayerTeamSeasonKey),
                    state.WorldCardCatalog);
            }
            return new ManagerHistoricalSaveData
            {
                saveVersion = CurrentSaveVersion,
                contentReference = HistoricalContentReferenceMapper.CreateSaveData(state.ContentReference),
                playerTeamSeasonKey = state.PlayerTeamSeasonKey,
                identityRegistry = CreateIdentityRegistry(state.IdentityRegistry),
                worldHistory = _worldHistoryMapper.CreateSaveData(state.WorldHistory),
                league = CreateLeague(state.League),
                leagueWorld = CreateLeagueWorld(state),
                rosters = CreateRosters(state.Rosters),
                ownedCards = CreateOwnedCards(state.OwnedCards),
                cardCollectionHistory = CreateCardCollectionHistory(state.CollectionHistory),
                wishlist = CreateWishlist(state.Wishlist),
                specialCardTransactions = state.CreateSpecialCardTransactionsSave(),
                economy = new ManagerEconomySaveData
                {
                    money = state.Economy.Money,
                    contractArrears = state.Economy.ContractArrears,
                    scoutingPoints = state.Economy.ScoutingPoints,
                    developmentPoints = state.Economy.DevelopmentPoints,
                    pityGauge = state.Economy.PityGauge
                },
                managerMode = CreateManagerMode(managerMode, state.WorldHistory.WorldHistorySeed),
                tacticCollection = CreateTacticCollection(state.TacticCollection),
                shopPurchaseHistory = CreateShopPurchaseHistory(state.ShopPurchaseHistory),
                guideRepeatState = state.GuideRepeatState,
                guideProgress = state.GuideProgress.Capture(),
                legendaryPractice = state.LegendaryPractice.Capture(),
                ownerProfile = new OwnerProfileSaveData
                {
                    clubName = state.OwnerProfile.ClubName,
                    nickname = state.OwnerProfile.Nickname,
                    frontManagerId = state.OwnerProfile.FrontManagerId
                },
                newGameReceipt = state.NewGameReceipt == null ? null : new OwnerNewGameReceiptSaveData
                {
                    mainCardIds = CopyIds(state.NewGameReceipt.MainCardIds),
                    fillerCardIds = CopyIds(state.NewGameReceipt.FillerCardIds),
                    fillerRerollCount = state.NewGameReceipt.FillerRerollCount,
                    starterRosterSeed = state.NewGameReceipt.StarterRosterSeed
                },
                onboarding = new OwnerOnboardingSaveData
                {
                    currentStep = state.Onboarding.CurrentStep,
                    isCompleted = state.Onboarding.IsCompleted
                },
                playerGrowth = CreatePlayerGrowth(state.PlayerGrowth)
            };
        }

        /// <summary>가변 진행 상태를 복제하면서 AI 벤치 기용에 영향을 주는 현재 로스터 순서도 보존한다.</summary>
        public ManagerHistoricalRuntimeState CreateSimulationCopy(ManagerHistoricalRuntimeState state)
        {
            ManagerHistoricalSaveData data = CreateSaveData(state);
            var guideEntries = data.guideRepeatState?.entries ?? Array.Empty<Baseball.Game.Guide.GuideRepeatStateEntryData>();
            data.guideRepeatState = new Baseball.Game.Guide.GuideRepeatStateData
            {
                entries = Array.ConvertAll(guideEntries, entry => entry == null ? null : new Baseball.Game.Guide.GuideRepeatStateEntryData
                {
                    dedupeKey = entry.dedupeKey,
                    displays = entry.displays
                })
            };
            return Restore(data);
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
                _cardEditionBalance,
                bakedContent.PlayerPersons, bakedContent.TeamSeasons, bakedContent.SpecialCards);
            LeagueInstance league = RestoreLeague(Require(saveData.league, nameof(saveData.league)));
            CurrentRosterState[] rosters = RestoreRosters(Require(saveData.rosters, nameof(saveData.rosters)));
            OwnedPlayerCardState[] ownedCards = RestoreOwnedCards(
                Require(saveData.ownedCards, nameof(saveData.ownedCards)), saveData.saveVersion);
            CardCollectionHistoryState collectionHistory = saveData.saveVersion < CollectionWishlistSaveVersion
                ? CreateCollectionHistoryFromOwnedCards(ownedCards)
                : RestoreCardCollectionHistory(
                    Require(saveData.cardCollectionHistory, nameof(saveData.cardCollectionHistory)));
            WishlistState wishlist = saveData.saveVersion < CollectionWishlistSaveVersion
                ? new WishlistState()
                : RestoreWishlist(Require(saveData.wishlist, nameof(saveData.wishlist)));
            ManagerEconomySaveData economyData = Require(saveData.economy, nameof(saveData.economy));
            ManagerModeRuntimeState managerMode = saveData.saveVersion < ManagerModeSaveVersion
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
                    history.WorldHistorySeed,
                    saveData.saveVersion);
            if (saveData.saveVersion >= OwnerPlayerContractSaveVersion &&
                saveData.saveVersion < ActiveRosterContractSyncSaveVersion)
            {
                RepairKnownActiveRosterContractMismatch(
                    managerMode,
                    FindRoster(rosters, saveData.playerTeamSeasonKey),
                    catalog);
            }

            var runtime = new ManagerHistoricalRuntimeState(
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
                    economyData.pityGauge,
                    economyData.contractArrears),
                managerMode,
                saveData.saveVersion < TacticAndShopSaveVersion
                    ? new TacticCollectionState()
                    : RestoreTacticCollection(Require(saveData.tacticCollection, nameof(saveData.tacticCollection))),
                saveData.saveVersion < TacticAndShopSaveVersion
                    ? new ShopPurchaseHistoryState()
                    : RestoreShopPurchaseHistory(Require(saveData.shopPurchaseHistory, nameof(saveData.shopPurchaseHistory))),
                saveData.guideRepeatState ?? new Baseball.Game.Guide.GuideRepeatStateData(),
                saveData.saveVersion < OwnerProfileSaveVersion || saveData.ownerProfile == null
                    ? OwnerProfileState.CreateLegacyDefault()
                    : new OwnerProfileState(
                        saveData.ownerProfile.nickname,
                        saveData.ownerProfile.frontManagerId,
                        string.IsNullOrWhiteSpace(saveData.ownerProfile.clubName)
                            ? null
                            : saveData.ownerProfile.clubName),
                saveData.saveVersion < OwnerProfileSaveVersion || saveData.newGameReceipt == null
                    ? null
                    : new OwnerNewGameReceipt(
                        Require(saveData.newGameReceipt.mainCardIds, nameof(saveData.newGameReceipt.mainCardIds)),
                        Require(saveData.newGameReceipt.fillerCardIds, nameof(saveData.newGameReceipt.fillerCardIds)),
                        saveData.newGameReceipt.fillerRerollCount,
                        saveData.newGameReceipt.starterRosterSeed),
                saveData.saveVersion < OwnerProfileSaveVersion || saveData.onboarding == null
                    ? new OwnerOnboardingState(4, true)
                    : new OwnerOnboardingState(saveData.onboarding.currentStep, saveData.onboarding.isCompleted),
                saveData.saveVersion < OwnerGrowthSaveVersion || saveData.playerGrowth == null
                    ? new OwnerPlayerGrowthState()
                    : RestorePlayerGrowth(saveData.playerGrowth),
                collectionHistory,
                wishlist);
            runtime.RestoreGuideProgress(saveData.guideProgress);
            runtime.RestoreLegendaryPractice(saveData.legendaryPractice);
            runtime.RestoreSpecialCardTransactions(saveData.saveVersion < 19
                ? Array.Empty<SpecialCardTransactionSaveData>()
                : Require(saveData.specialCardTransactions, nameof(saveData.specialCardTransactions)));
            if (saveData.saveVersion >= 16 && saveData.leagueWorld != null)
                RestoreLeagueWorld(runtime, saveData.leagueWorld, saveData.saveVersion);
            else
                new OwnerLeagueWorldService(_balance).Initialize(runtime, bakedContent);
            return runtime;
        }

        /// <summary>v11~v13의 1군 교체 누락으로 계약 수만 25인 상태를 해당 로스터 CardId에 맞춰 이행한다.</summary>
        private void RepairKnownActiveRosterContractMismatch(
            ManagerModeRuntimeState managerMode,
            CurrentRosterState roster,
            WorldCardCatalog catalog)
        {
            if (managerMode.PlayerContracts.Count != roster.Entries.Count ||
                HasContractCoverage(managerMode.PlayerContracts, roster))
                return;

            var resolver = new OwnerPlayerMarketResolver(_balance.OwnerPlayerMarket);
            managerMode.ReplacePlayerContractState(
                resolver.CreateActiveRosterContracts(
                    roster,
                    catalog,
                    managerMode.LiveSeason.SeasonNumber,
                    managerMode.PlayerContracts));
        }

        private static bool HasContractCoverage(
            IReadOnlyList<OwnerPlayerContractState> contracts,
            CurrentRosterState roster)
        {
            for (int rosterIndex = 0; rosterIndex < roster.Entries.Count; rosterIndex++)
            {
                bool found = false;
                for (int contractIndex = 0; contractIndex < contracts.Count; contractIndex++)
                {
                    if (!string.Equals(
                            roster.Entries[rosterIndex].CardId,
                            contracts[contractIndex].CardId,
                            StringComparison.Ordinal))
                        continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static CurrentRosterState FindRoster(
            IReadOnlyList<CurrentRosterState> rosters,
            string teamSeasonKey)
        {
            for (int index = 0; index < rosters.Count; index++)
                if (string.Equals(rosters[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return rosters[index];
            throw new KeyNotFoundException($"TeamSeasonKey {teamSeasonKey}의 로스터가 없습니다.");
        }

        private static TacticCollectionSaveData CreateTacticCollection(TacticCollectionState source)
        {
            var entries = new TacticCollectionEntrySaveData[source.CardIds.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                string cardId = source.CardIds[index];
                entries[index] = new TacticCollectionEntrySaveData
                {
                    cardId = cardId,
                    count = source.GetCount(cardId)
                };
            }
            Array.Sort(entries, (left, right) => StringComparer.Ordinal.Compare(left.cardId, right.cardId));
            return new TacticCollectionSaveData { entries = entries };
        }

        private static TacticCollectionState RestoreTacticCollection(TacticCollectionSaveData source)
        {
            TacticCollectionEntrySaveData[] entries = Require(source.entries, nameof(source.entries));
            var state = new TacticCollectionState();
            for (int index = 0; index < entries.Length; index++)
            {
                TacticCollectionEntrySaveData entry = Require(entries[index], nameof(source.entries));
                if (string.IsNullOrWhiteSpace(entry.cardId) || entry.count < 1)
                    throw new ArgumentException("저장된 전술 카드 보유 상태가 잘못되었습니다.", nameof(source));
                if (state.Contains(entry.cardId))
                    throw new ArgumentException("저장된 전술 카드가 중복되었습니다.", nameof(source));
                for (int count = 0; count < entry.count; count++) state.Acquire(entry.cardId);
            }
            return state;
        }

        private static ShopPurchaseHistorySaveData CreateShopPurchaseHistory(ShopPurchaseHistoryState source)
        {
            var entries = new ShopPurchaseCountSaveData[source.PurchaseCounts.Count];
            int index = 0;
            foreach (KeyValuePair<string, int> pair in source.PurchaseCounts)
            {
                entries[index++] = new ShopPurchaseCountSaveData
                {
                    productId = pair.Key,
                    count = pair.Value
                };
            }
            Array.Sort(entries, (left, right) => StringComparer.Ordinal.Compare(left.productId, right.productId));
            return new ShopPurchaseHistorySaveData
            {
                totalPurchaseCount = source.TotalPurchaseCount,
                entries = entries
            };
        }

        private static ShopPurchaseHistoryState RestoreShopPurchaseHistory(ShopPurchaseHistorySaveData source)
        {
            ShopPurchaseCountSaveData[] entries = Require(source.entries, nameof(source.entries));
            var counts = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
            for (int index = 0; index < entries.Length; index++)
            {
                ShopPurchaseCountSaveData entry = Require(entries[index], nameof(source.entries));
                if (string.IsNullOrWhiteSpace(entry.productId) || entry.count < 1 ||
                    !counts.TryAdd(entry.productId.Trim(), entry.count))
                {
                    throw new ArgumentException("저장된 상점 구매 이력이 잘못되었습니다.", nameof(source));
                }
            }
            return new ShopPurchaseHistoryState(counts, source.totalPurchaseCount);
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
                liveSeason = CreateLiveSeason(source.LiveSeason),
                completedSeasons = CreateCompletedSeasons(source.CompletedSeasons),
                dugout = CreateDugout(source.Dugout),
                playerContracts = CreatePlayerContracts(source.PlayerContracts)
            };
        }

        private static OwnerPlayerContractSaveData[] CreatePlayerContracts(
            IReadOnlyList<OwnerPlayerContractState> source)
        {
            var result = new OwnerPlayerContractSaveData[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                OwnerPlayerContractState contract = source[index];
                result[index] = new OwnerPlayerContractSaveData
                {
                    contractId = contract.ContractId,
                    cardId = contract.CardId,
                    startSeason = contract.StartSeason,
                    remainingSeasons = contract.RemainingSeasons,
                    annualSalary = contract.AnnualSalary,
                    hasLastSalaryPaidSeason = contract.LastSalaryPaidSeason.HasValue,
                    lastSalaryPaidSeason = contract.LastSalaryPaidSeason ?? 0
                };
            }
            return result;
        }

        private static DugoutManagementSaveData CreateDugout(DugoutManagementState source)
        {
            return new DugoutManagementSaveData
            {
                managerId = source.ManagerId,
                headCoachId = source.HeadCoachId,
                managerTrust = source.ManagerTrust,
                battingApproach = source.Policy.BattingApproach,
                runningAggression = source.Policy.RunningAggression,
                smallBallPreference = source.Policy.SmallBallPreference,
                pinchHitAggression = source.Policy.PinchHitAggression,
                hookSpeed = source.Policy.HookSpeed,
                bullpenAggression = source.Policy.BullpenAggression
            };
        }

        private ManagerModeRuntimeState RestoreManagerMode(
            ManagerModeSaveData source,
            WorldIdentityRegistry identities,
            ulong expectedWorldSeed,
            int saveVersion)
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
            var result = new ManagerModeRuntimeState(
                RestoreClubOperation(Require(source.clubOperation, nameof(source.clubOperation))),
                catalog,
                RestoreStaffContracts(Require(source.staffContracts, nameof(source.staffContracts))),
                RestoreStaffAssignment(Require(source.staffAssignment, nameof(source.staffAssignment))),
                RestoreLineupPresets(Require(source.lineupPresets, nameof(source.lineupPresets))),
                source.selectedLineupPresetId,
                RestorePlayerStatuses(Require(source.playerStatuses, nameof(source.playerStatuses))),
                RestoreFamiliarities(Require(source.familiarities, nameof(source.familiarities))),
                RestoreLiveSeason(Require(source.liveSeason, nameof(source.liveSeason)), saveVersion),
                saveVersion < DugoutManagementSaveVersion || source.dugout == null
                    ? DugoutManagementState.CreateDefault()
                    : RestoreDugout(source.dugout),
                RestoreCompletedSeasons(source.completedSeasons, saveVersion));
            if (saveVersion >= OwnerPlayerContractSaveVersion)
            {
                result.ReplacePlayerContractState(
                    RestorePlayerContracts(Require(source.playerContracts, nameof(source.playerContracts))));
            }
            return result;
        }

        private static OwnerPlayerContractState[] RestorePlayerContracts(OwnerPlayerContractSaveData[] source)
        {
            var result = new OwnerPlayerContractState[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                OwnerPlayerContractSaveData contract = Require(source[index], nameof(source));
                result[index] = new OwnerPlayerContractState(
                    contract.contractId,
                    contract.cardId,
                    contract.startSeason,
                    contract.remainingSeasons,
                    contract.annualSalary,
                    contract.hasLastSalaryPaidSeason ? contract.lastSalaryPaidSeason : (int?)null);
            }
            return result;
        }

        private static ManagerCompletedSeasonSaveData[] CreateCompletedSeasons(
            IReadOnlyList<ManagerCompletedSeasonState> seasons)
        {
            var result = new ManagerCompletedSeasonSaveData[seasons.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = new ManagerCompletedSeasonSaveData
                {
                    leagueGrade = (int)seasons[index].LeagueGrade,
                    season = CreateLiveSeason(seasons[index].Season)
                };
            return result;
        }

        private static ManagerCompletedSeasonState[] RestoreCompletedSeasons(
            ManagerCompletedSeasonSaveData[] seasons, int saveVersion)
        {
            // v9까지 저장되지 않았던 완료 기록을 추정하거나 과거 WorldHistory로 대체하지 않는다.
            if (saveVersion < CompletedSeasonsSaveVersion) return Array.Empty<ManagerCompletedSeasonState>();
            Require(seasons, nameof(seasons));
            var result = new ManagerCompletedSeasonState[seasons.Length];
            for (int index = 0; index < result.Length; index++)
            {
                ManagerCompletedSeasonSaveData saved = Require(seasons[index], nameof(seasons));
                ValidateEnum<LeagueGrade>(saved.leagueGrade, nameof(saved.leagueGrade));
                result[index] = new ManagerCompletedSeasonState(
                    RestoreLiveSeason(Require(saved.season, nameof(saved.season)), saveVersion),
                    (LeagueGrade)saved.leagueGrade);
            }
            return result;
        }

        private static DugoutManagementState RestoreDugout(DugoutManagementSaveData source)
        {
            var state = new DugoutManagementState(
                source.managerId,
                source.headCoachId,
                new DugoutPolicySettings(
                    source.battingApproach,
                    source.runningAggression,
                    source.smallBallPreference,
                    source.pinchHitAggression,
                    source.hookSpeed,
                    source.bullpenAggression),
                source.managerTrust);
            var catalog = DugoutStaffCatalog.CreateDefault();
            catalog.GetManager(state.ManagerId);
            catalog.GetHeadCoach(state.HeadCoachId);
            return state;
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
                    hasPlayerRoleDecision = game.HasPlayerRoleDecision,
                    hasTacticPlan = game.HasTacticPlan,
                    plannedTacticCardIds = game.HasTacticPlan
                        ? CopyIds(game.PlannedTacticCardIds)
                        : Array.Empty<string>()
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
                games = games,
                statistics = LeagueSeasonStatisticsSaveMapper.CreateSaveData(source.Statistics)
            };
        }

        private static ManagerLiveSeasonState RestoreLiveSeason(ManagerLiveSeasonSaveData source, int saveVersion)
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
                if (saveVersion >= ScheduledTacticsSaveVersion && saved.hasTacticPlan)
                    game.PlanTactics(Require(saved.plannedTacticCardIds, nameof(saved.plannedTacticCardIds)));
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
                new SeasonScheduleState(games),
                LeagueSeasonStatisticsSaveMapper.Restore(source.statistics));
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

        private static OwnerLeagueWorldSaveData CreateLeagueWorld(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime.LeagueWorld == null) return null;
            var groups = new List<OwnerLeagueGroupSaveData>();
            var localKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roster in runtime.Rosters) localKeys.Add(roster.TeamSeasonKey);
            var rosters = new List<CurrentRosterState>();
            foreach (var roster in runtime.WorldRosters)
                if (!localKeys.Contains(roster.TeamSeasonKey)) rosters.Add(roster);
            foreach (var group in runtime.LeagueWorld.Groups)
                if (!ReferenceEquals(group.Season, runtime.ManagerMode.LiveSeason))
                    groups.Add(new OwnerLeagueGroupSaveData
                    {
                        league = CreateLeague(group.League),
                        season = CreateLiveSeason(group.Season),
                        postseason = CreateOwnerPostseason(group.Postseason)
                    });
            var playerIds = new List<OwnerLeaguePlayerIdSaveData>();
            foreach (var entry in ManagerModeMatchService.PlayerIdMap.Create(runtime).Entries)
                playerIds.Add(new OwnerLeaguePlayerIdSaveData { key = entry.Key, playerId = entry.Value });
            playerIds.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            var history = new List<OwnerLeagueGroupSaveData>();
            foreach (var group in runtime.LeagueWorld.CompletedGroups)
                history.Add(new OwnerLeagueGroupSaveData
                {
                    league = CreateLeague(group.League),
                    season = CreateLiveSeason(group.Season),
                    postseason = CreateOwnerPostseason(group.Postseason)
                });
            OwnerLeagueGroupState playerGroup = runtime.LeagueWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            return new OwnerLeagueWorldSaveData
            {
                groups = groups.ToArray(),
                completedGroups = history.ToArray(),
                playerPostseason = CreateOwnerPostseason(playerGroup.Postseason),
                rosters = CreateRosters(rosters),
                playerIds = playerIds.ToArray()
            };
        }

        private static void RestoreLeagueWorld(ManagerHistoricalRuntimeState runtime, OwnerLeagueWorldSaveData source, int saveVersion)
        {
            var groups = new List<OwnerLeagueGroupState>
            {
                new OwnerLeagueGroupState(runtime.League, runtime.ManagerMode.LiveSeason,
                    saveVersion < OwnerPostseasonSaveVersion ? null : RestoreOwnerPostseason(source.playerPostseason))
            };
            foreach (var group in Require(source.groups, nameof(source.groups)))
                groups.Add(new OwnerLeagueGroupState(RestoreLeague(Require(group.league, nameof(group.league))),
                    RestoreLiveSeason(Require(group.season, nameof(group.season)), saveVersion),
                    saveVersion < OwnerPostseasonSaveVersion ? null : RestoreOwnerPostseason(group.postseason)));
            var rosters = new List<CurrentRosterState>(runtime.Rosters);
            var validator = new ActiveRosterValidator();
            foreach (var roster in RestoreRosters(Require(source.rosters, nameof(source.rosters))))
            {
                if (!validator.Validate(roster).IsValid) throw new ArgumentException("월드 AI 로스터 구성이 올바르지 않습니다.");
                ManagerHistoricalRuntimeState.ValidateRosterCards(roster, runtime.WorldCardCatalog);
                runtime.ManagerMode.GetPlayerStatus(roster.TeamSeasonKey);
                runtime.ManagerMode.GetFamiliarity(roster.TeamSeasonKey);
                rosters.Add(roster);
            }
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in Require(source.playerIds, nameof(source.playerIds))) ids.Add(entry.key, entry.playerId);
            var history = new List<OwnerLeagueGroupState>();
            foreach (var group in Require(source.completedGroups, nameof(source.completedGroups)))
                history.Add(new OwnerLeagueGroupState(RestoreLeague(Require(group.league, nameof(group.league))),
                    RestoreLiveSeason(Require(group.season, nameof(group.season)), saveVersion),
                    saveVersion < OwnerPostseasonSaveVersion ? null : RestoreOwnerPostseason(group.postseason)));
            var world = new OwnerLeagueWorldState(groups, rosters, history) { PlayerIds = ManagerModeMatchService.PlayerIdMap.Restore(ids) };
            foreach (var roster in world.Rosters)
                foreach (var entry in roster.Entries)
                    if (!world.PlayerIds.TryGet(roster.TeamSeasonKey, entry.PlayerSeasonId, out _))
                        throw new ArgumentException("월드 선수 ID 원장에 로스터 선수가 없습니다.");
            runtime.SetLeagueWorld(world);
        }

        private static OwnerPostseasonSaveData CreateOwnerPostseason(OwnerPostseasonState source)
        {
            if (source == null) return null;
            var series = new OwnerPostseasonSeriesSaveData[source.Series.Count];
            for (int index = 0; index < series.Length; index++)
            {
                OwnerPostseasonSeriesState item = source.Series[index];
                var games = new ManagerScheduledGameSaveData[item.Games.Count];
                for (int gameIndex = 0; gameIndex < games.Length; gameIndex++)
                {
                    ScheduledGameState game = item.Games[gameIndex];
                    games[gameIndex] = new ManagerScheduledGameSaveData
                    {
                        gameId = game.GameId,
                        round = game.Round,
                        randomSeed = game.RandomSeed,
                        awayTeamId = game.AwayTeamId,
                        homeTeamId = game.HomeTeamId,
                        isCompleted = game.IsCompleted,
                        awayRuns = game.AwayRuns,
                        homeRuns = game.HomeRuns
                    };
                }
                series[index] = new OwnerPostseasonSeriesSaveData
                {
                    seriesId = item.SeriesId,
                    round = (int)item.Round,
                    higherSeedTeamId = item.HigherSeedTeamId,
                    lowerSeedTeamId = item.LowerSeedTeamId,
                    seriesGames = item.SeriesGames,
                    higherSeedWins = item.HigherSeedWins,
                    lowerSeedWins = item.LowerSeedWins,
                    games = games
                };
            }
            var seeds = new int[source.SeedTeamIds.Count];
            for (int index = 0; index < seeds.Length; index++) seeds[index] = source.SeedTeamIds[index];
            return new OwnerPostseasonSaveData { seasonId = source.SeasonId, seedTeamIds = seeds, series = series };
        }

        private static OwnerPostseasonState RestoreOwnerPostseason(OwnerPostseasonSaveData source)
        {
            if (source == null) return null;
            // JsonUtility는 아직 생성하지 않은 중첩 상태의 null을 빈 DTO로 복원할 수 있다.
            // 내용이 있는 잘못된 포스트시즌은 아래 검증에서 계속 거부한다.
            if (string.IsNullOrEmpty(source.seasonId) &&
                (source.seedTeamIds == null || source.seedTeamIds.Length == 0) &&
                (source.series == null || source.series.Length == 0)) return null;
            int[] seeds = Require(source.seedTeamIds, nameof(source.seedTeamIds));
            OwnerPostseasonSeriesSaveData[] savedSeries = Require(source.series, nameof(source.series));
            var series = new OwnerPostseasonSeriesState[savedSeries.Length];
            for (int index = 0; index < series.Length; index++)
            {
                OwnerPostseasonSeriesSaveData saved = Require(savedSeries[index], nameof(source.series));
                ValidateEnum<OwnerPostseasonRound>(saved.round, nameof(saved.round));
                ManagerScheduledGameSaveData[] savedGames = Require(saved.games, nameof(saved.games));
                var games = new ScheduledGameState[savedGames.Length];
                for (int gameIndex = 0; gameIndex < games.Length; gameIndex++)
                {
                    ManagerScheduledGameSaveData savedGame = Require(savedGames[gameIndex], nameof(saved.games));
                    var game = new ScheduledGameState(savedGame.gameId, savedGame.round, savedGame.randomSeed,
                        savedGame.awayTeamId, savedGame.homeTeamId);
                    if (savedGame.isCompleted) game.Complete(savedGame.awayRuns, savedGame.homeRuns);
                    games[gameIndex] = game;
                }
                series[index] = new OwnerPostseasonSeriesState(saved.seriesId,
                    (OwnerPostseasonRound)saved.round, saved.higherSeedTeamId, saved.lowerSeedTeamId,
                    saved.seriesGames, games, saved.higherSeedWins, saved.lowerSeedWins);
            }
            return new OwnerPostseasonState(source.seasonId, seeds, series);
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
                isPooledGroup = league.IsPooledGroup,
                grade = (int)league.Grade,
                regularTeamSeasonKeys = regular,
                specialCompositeTeams = special,
                fillerTeamSeasonKeys = CopyStrings(league.FillerTeamSeasonKeys)
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
            // CPU 임시 구단 이전 저장본은 이 필드가 없으므로 임시 구단이 없는 조로 복원한다.
            return new LeagueInstance(source.leagueInstanceId, (LeagueGrade)source.grade, regular, special,
                source.isPooledGroup, source.fillerTeamSeasonKeys ?? Array.Empty<string>());
        }

        private static string[] CopyStrings(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
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
                // 같은 역할의 배열 순서는 AI 벤치 기용 우선순위이므로 저장에서도 그대로 보존한다.
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
                var study = new int[PlayerAbilityCatalog.AbilityCount];
                for (int abilityIndex = 0; abilityIndex < training.Length; abilityIndex++)
                {
                    training[abilityIndex] = card.Training.GetBonus((PlayerAbility)abilityIndex);
                    study[abilityIndex] = card.Training.GetStudyBonus((PlayerAbility)abilityIndex);
                }
                result[index] = new OwnedPlayerCardSaveData
                {
                    cardId = card.CardId,
                    enhancementLevel = card.EnhancementLevel,
                    duplicateCount = card.DuplicateCount,
                    isLocked = card.IsLocked,
                    isFavorite = card.IsFavorite,
                    trainingBonuses = training,
                    studyBonuses = study,
                    growthModifiers = CreateGrowthLedger(card.Training.Ledger),
                    unlockedSkillMask = card.SkillBoard.UnlockedMask,
                    slotExperience = card.SkillBoard.SlotExperience,
                    skillBoard = CreateSkillBoard(card.SkillBoard),
                    lastStudySeason = card.LastStudySeason
                };
            }
            Array.Sort(result, (left, right) => StringComparer.Ordinal.Compare(left.cardId, right.cardId));
            return result;
        }

        private static OwnedPlayerCardState[] RestoreOwnedCards(OwnedPlayerCardSaveData[] source, int saveVersion)
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
                    new CardTrainingState(
                        Require(card.trainingBonuses, nameof(card.trainingBonuses)),
                        saveVersion < GrowthSourceBreakdownSaveVersion
                            ? null
                            : Require(card.studyBonuses, nameof(card.studyBonuses)),
                        saveVersion < 28 ? null : RestoreGrowthLedger(Require(card.growthModifiers, nameof(card.growthModifiers)))),
                    saveVersion < OwnerGrowthSaveVersion
                        ? new OwnedCardSkillBoardState()
                        : RestoreSkillBoard(card.skillBoard, saveVersion < 28 ? OwnedCardSkillBoardState.CompleteUnlockedMask : card.unlockedSkillMask, card.slotExperience),
                    saveVersion < OwnerGrowthSaveVersion ? -1 : card.lastStudySeason);
            }
            return result;
        }

        private static CardCollectionHistorySaveData CreateCardCollectionHistory(
            CardCollectionHistoryState source)
        {
            var cardIds = new string[source.EverAcquiredCardIds.Count];
            for (int index = 0; index < cardIds.Length; index++)
                cardIds[index] = source.EverAcquiredCardIds[index];
            Array.Sort(cardIds, StringComparer.Ordinal);
            return new CardCollectionHistorySaveData { everAcquiredCardIds = cardIds };
        }

        private static CardCollectionHistoryState RestoreCardCollectionHistory(
            CardCollectionHistorySaveData source)
        {
            return new CardCollectionHistoryState(
                Require(source.everAcquiredCardIds, nameof(source.everAcquiredCardIds)));
        }

        private static CardCollectionHistoryState CreateCollectionHistoryFromOwnedCards(
            IReadOnlyList<OwnedPlayerCardState> ownedCards)
        {
            var cardIds = new string[ownedCards.Count];
            for (int index = 0; index < cardIds.Length; index++)
                cardIds[index] = ownedCards[index].CardId;
            return new CardCollectionHistoryState(cardIds);
        }

        private static WishlistSaveData CreateWishlist(WishlistState source)
        {
            WishlistEntry[] entries = source.GetOldestFirst();
            var result = new WishlistEntrySaveData[entries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                result[index] = new WishlistEntrySaveData
                {
                    cardId = entries[index].CardId,
                    addedSequence = entries[index].AddedSequence
                };
            }
            return new WishlistSaveData
            {
                entries = result,
                nextAddedSequence = source.NextAddedSequence
            };
        }

        private static WishlistState RestoreWishlist(WishlistSaveData source)
        {
            WishlistEntrySaveData[] savedEntries = Require(source.entries, nameof(source.entries));
            var entries = new WishlistEntry[savedEntries.Length];
            for (int index = 0; index < entries.Length; index++)
            {
                WishlistEntrySaveData entry = Require(savedEntries[index], nameof(source.entries));
                entries[index] = new WishlistEntry(entry.cardId, entry.addedSequence);
            }
            return new WishlistState(entries, source.nextAddedSequence);
        }

        private static OwnerPlacedSkillBlockSaveData[] CreateSkillBoard(OwnedCardSkillBoardState source)
        {
            var result = new OwnerPlacedSkillBlockSaveData[source.Placements.Count];
            for (int index = 0; index < result.Length; index++)
            {
                PlacedSkillBlock placement = source.Placements[index];
                result[index] = new OwnerPlacedSkillBlockSaveData
                {
                    instanceId = placement.Instance.InstanceId,
                    definitionId = placement.Instance.DefinitionId,
                    originX = placement.OriginX,
                    originY = placement.OriginY,
                    rotationQuarterTurns = placement.RotationQuarterTurns
                };
            }
            return result;
        }

        private static OwnedCardSkillBoardState RestoreSkillBoard(OwnerPlacedSkillBlockSaveData[] source,
            int unlockedMask, int slotExperience)
        {
            var result = new OwnedCardSkillBoardState(unlockedMask, slotExperience);
            if (source == null) return result;
            for (int index = 0; index < source.Length; index++)
            {
                OwnerPlacedSkillBlockSaveData placement = Require(source[index], nameof(source));
                result.Add(new PlacedSkillBlock(
                    new SkillBlockInstance(placement.instanceId, placement.definitionId),
                    placement.originX,
                    placement.originY,
                    placement.rotationQuarterTurns));
            }
            return result;
        }

        private static OwnerPlayerGrowthSaveData CreatePlayerGrowth(OwnerPlayerGrowthState source)
        {
            var blocks = new OwnerSkillBlockInstanceSaveData[source.Inventory.Blocks.Count];
            for (int index = 0; index < blocks.Length; index++)
            {
                SkillBlockInstance block = source.Inventory.Blocks[index];
                blocks[index] = new OwnerSkillBlockInstanceSaveData
                    { instanceId = block.InstanceId, definitionId = block.DefinitionId };
            }
            var projects = new CardStudyProjectSaveData[source.StudyProjects.Count];
            for (int index = 0; index < projects.Length; index++)
            {
                CardStudyProjectState project = source.StudyProjects[index];
                projects[index] = new CardStudyProjectSaveData
                {
                    cardId = project.CardId,
                    programId = project.ProgramId,
                    startedSeason = project.StartedSeason,
                    remainingWeeks = project.RemainingWeeks,
                    durationWeeks = project.DurationWeeks,
                    paidMoney = project.PaidMoney,
                    paidDevelopmentPoints = project.PaidDevelopmentPoints,
                    resultSeed = project.ResultSeed,
                    resultBonus = project.ResultBonus
                };
            }
            return new OwnerPlayerGrowthSaveData
            {
                offseasonCompletedWeeks = source.Offseason.CompletedWeeks,
                studySequence = source.StudySequence,
                support = CreateSupport(source.Support),
                camps = CreateCamps(source.Camps),
                slogan = source.Slogan?.Definition.Copy(),
                sloganLevel = source.Slogan?.Level ?? 0,
                sloganRevision = source.Slogan?.Revision ?? 0,
                inventory = new OwnerSkillBlockInventorySaveData
                {
                    blocks = blocks,
                    researchCount = source.Inventory.ResearchCount,
                    nextInstanceId = source.Inventory.NextInstanceId,
                    selectionBoxes = source.Inventory.SelectionBoxes,
                    pityEliteCount = source.Inventory.PityEliteCount,
                    pityUniqueCount = source.Inventory.PityUniqueCount,
                    pityLegendaryCount = source.Inventory.PityLegendaryCount,
                    totalPullCount = source.Inventory.TotalPullCount
                },
                studyProjects = projects
            };
        }

        private static OwnerPlayerGrowthState RestorePlayerGrowth(OwnerPlayerGrowthSaveData source)
        {
            OwnerSkillBlockInventorySaveData inventoryData = Require(source.inventory, nameof(source.inventory));
            var inventory = new OwnerSkillBlockInventoryState();
            OwnerSkillBlockInstanceSaveData[] blocks = Require(inventoryData.blocks, nameof(inventoryData.blocks));
            for (int index = 0; index < blocks.Length; index++)
            {
                OwnerSkillBlockInstanceSaveData block = Require(blocks[index], nameof(inventoryData.blocks));
                inventory.Restore(new SkillBlockInstance(block.instanceId, block.definitionId));
            }
            inventory.RestorePity(
                inventoryData.pityEliteCount, inventoryData.pityUniqueCount,
                inventoryData.pityLegendaryCount, inventoryData.totalPullCount);
            inventory.RestoreResearch(inventoryData.researchCount, inventoryData.selectionBoxes);
            if (inventoryData.nextInstanceId > 0) inventory.RestoreNextInstanceId(inventoryData.nextInstanceId);
            var result = new OwnerPlayerGrowthState(inventory, new OwnerOffseasonState(source.offseasonCompletedWeeks));
            result.RestoreStudySequence(source.studySequence);
            result.Support = RestoreSupport(source.support);
            if (source.slogan != null) result.Slogan = new OwnerSloganState(source.slogan, source.sloganLevel, source.sloganRevision);
            if (source.camps != null)
                foreach (var camp in source.camps)
                    result.Camps.Add(new OwnerCampProject(camp.cardId, camp.facilityId, camp.weeklyExperience, camp.requiredExperience, camp.automaticReturn));
            CardStudyProjectSaveData[] projects = Require(source.studyProjects, nameof(source.studyProjects));
            for (int index = 0; index < projects.Length; index++)
            {
                CardStudyProjectSaveData project = Require(projects[index], nameof(source.studyProjects));
                result.AddStudy(new CardStudyProjectState(
                    project.cardId, project.programId, project.startedSeason, project.remainingWeeks,
                    project.durationWeeks, project.paidMoney, project.paidDevelopmentPoints, project.resultSeed, project.resultBonus));
            }
            return result;
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

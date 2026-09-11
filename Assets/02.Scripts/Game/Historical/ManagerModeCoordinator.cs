using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    public enum ManagerModeTransactionStatus
    {
        Applied,
        AlreadyApplied,
        Rejected,
        InsufficientMoney
    }

    /// <summary>주간 생산·회복을 같은 멱등 경계에서 반영한 결과다.</summary>
    public sealed class ManagerWeeklyAdvanceResult
    {
        public ManagerWeeklyAdvanceResult(
            ManagerModeTransactionStatus status,
            WeeklyFacilityProductionResult facilityProduction,
            IReadOnlyList<ManagerTeamRecoveryResult> teamRecoveries)
        {
            Status = status;
            FacilityProduction = facilityProduction;
            if (teamRecoveries == null) throw new ArgumentNullException(nameof(teamRecoveries));
            var copied = new ManagerTeamRecoveryResult[teamRecoveries.Count];
            int playerRecovery = 0;
            for (int index = 0; index < copied.Length; index++)
            {
                copied[index] = teamRecoveries[index]
                    ?? throw new ArgumentException("null Team recovery가 있습니다.", nameof(teamRecoveries));
                if (copied[index].IsPlayerTeam) playerRecovery = copied[index].Recovery;
            }
            TeamRecoveries = copied;
            ConditionRecovery = playerRecovery;
        }

        public ManagerModeTransactionStatus Status { get; }
        public WeeklyFacilityProductionResult FacilityProduction { get; }
        public int ConditionRecovery { get; }
        public IReadOnlyList<ManagerTeamRecoveryResult> TeamRecoveries { get; }
    }

    /// <summary>주간 회복에 실제 사용한 구단별 단일 Context 결과다.</summary>
    public sealed class ManagerTeamRecoveryResult
    {
        public ManagerTeamRecoveryResult(string teamSeasonKey, bool isPlayerTeam, int recovery)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey가 필요합니다.", nameof(teamSeasonKey));
            if (recovery < 0) throw new ArgumentOutOfRangeException(nameof(recovery));
            TeamSeasonKey = teamSeasonKey.Trim();
            IsPlayerTeam = isPlayerTeam;
            Recovery = recovery;
        }

        public string TeamSeasonKey { get; }
        public bool IsPlayerTeam { get; }
        public int Recovery { get; }
    }

    public enum ManagerSeasonAdvanceStatus
    {
        Applied,
        SeasonInProgress,
        InsufficientMoney,
        InvalidStaffState,
        ContractRenewalRequired
    }

    /// <summary>시즌 재무 마감, 연봉, 계약 만료와 다음 일정 교체의 단일 결과다.</summary>
    public sealed class ManagerSeasonAdvanceResult
    {
        public ManagerSeasonAdvanceResult(
            ManagerSeasonAdvanceStatus status,
            SeasonFinanceSummary completedFinance,
            StaffSalarySettlementResult salarySettlement,
            StaffContractAdvanceResult staffAdvance,
            ManagerLiveSeasonState nextSeason,
            LeagueGrade? previousLeagueGrade = null,
            LeagueGrade? nextLeagueGrade = null)
        {
            Status = status;
            CompletedFinance = completedFinance;
            SalarySettlement = salarySettlement;
            StaffAdvance = staffAdvance;
            NextSeason = nextSeason;
            PreviousLeagueGrade = previousLeagueGrade;
            NextLeagueGrade = nextLeagueGrade;
        }

        public ManagerSeasonAdvanceStatus Status { get; }
        public SeasonFinanceSummary CompletedFinance { get; }
        public StaffSalarySettlementResult SalarySettlement { get; }
        public StaffContractAdvanceResult StaffAdvance { get; }
        public ManagerLiveSeasonState NextSeason { get; }
        public LeagueGrade? PreviousLeagueGrade { get; }
        public LeagueGrade? NextLeagueGrade { get; }
        public bool IsApplied => Status == ManagerSeasonAdvanceStatus.Applied;
    }

    /// <summary>네 확장 시스템의 Money·주간 Tick·Modifier 합성을 한 Production 경로에서 조정한다.</summary>
    public sealed class ManagerModeCoordinator
    {
        private readonly BalanceTable _balance;
        private readonly WeeklyFacilityProductionResolver _weeklyProductionResolver;
        private readonly HomeGameFinanceResolver _homeGameFinanceResolver;
        private readonly ClubFacilityEffectResolver _facilityEffectResolver;
        private readonly TeamStaffEffectResolver _staffEffectResolver;
        private readonly ConditionRecoveryResolver _conditionRecoveryResolver;
        private readonly StaffContractService _staffContractService;
        private readonly ClubUpgradeResolver _upgradeResolver;
        private readonly AiStaffProfileResolver _aiStaffProfileResolver;
        private readonly CardSaleBalanceTable _cardSaleBalance;
        private readonly OwnerPlayerMarketService _playerMarketService;

        private const ulong AiStaffSeasonStream = 0x4149535441464653UL;
        private const double NeutralAiManagerQuality = 50d;
        private const double NeutralClubDnaRating = 50d;

        public ManagerModeCoordinator(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _weeklyProductionResolver = new WeeklyFacilityProductionResolver(balance.ClubOperation);
            _homeGameFinanceResolver = new HomeGameFinanceResolver(balance.ClubOperation);
            _facilityEffectResolver = new ClubFacilityEffectResolver(balance.ClubOperation);
            _staffEffectResolver = new TeamStaffEffectResolver();
            _conditionRecoveryResolver = new ConditionRecoveryResolver();
            _staffContractService = new StaffContractService();
            _upgradeResolver = new ClubUpgradeResolver(balance.ClubOperation);
            _aiStaffProfileResolver = new AiStaffProfileResolver();
            _cardSaleBalance = CardSaleBalanceTable.CreateInitial();
            _playerMarketService = new OwnerPlayerMarketService(balance);
        }

        public TeamStaffEffectProfile ResolvePlayerStaffEffects(ManagerModeRuntimeState mode)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            return _staffEffectResolver.Resolve(
                mode.StaffCatalog,
                mode.StaffContracts,
                mode.StaffAssignment,
                _balance.Staff);
        }

        /// <summary>Hitting/Pitching Coach와 Development Coach 효율을 기존 CardTraining 한 경로에만 적용한다.</summary>
        public CardTrainingResult TrainOwnedCard(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            CardTrainingProgramDefinition program)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            if (program == null) throw new ArgumentNullException(nameof(program));
            ManagerModeRuntimeState mode = RequireMode(runtime);
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 훈련할 수 없습니다.");
            if (IsStudying(runtime, cardId))
                throw new InvalidOperationException("유학 중인 카드는 카드 훈련할 수 없습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("WorldCardCatalog에 훈련 카드가 없습니다.");
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            ValidateTrainingDiscipline(season, program.Ability);
            StaffTrainingDiscipline discipline = PlayerAbilityCatalog.IsBatterAbility(program.Ability)
                ? StaffTrainingDiscipline.Hitting
                : StaffTrainingDiscipline.Pitching;
            StaffTrainingEfficiencyResult staffEfficiency = StaffTrainingEfficiencyResolver.Resolve(
                ResolvePlayerStaffEffects(mode),
                new StaffTrainingEfficiencyContext(discipline, includeDevelopmentCoach: true));
            return CardTrainingResolver.Train(
                ownedCard,
                season,
                program,
                runtime.Economy,
                staffEfficiency);
        }

        /// <summary>실제 훈련과 같은 스태프 효율·상한·DP를 사용해 적용 전 결과를 계산한다.</summary>
        public CardTrainingPreview PreviewOwnedCardTraining(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            CardTrainingProgramDefinition program)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 훈련할 수 없습니다.");
            if (IsStudying(runtime, cardId))
                throw new InvalidOperationException("유학 중인 카드는 카드 훈련할 수 없습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("WorldCardCatalog에 훈련 카드가 없습니다.");
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            ValidateTrainingDiscipline(season, program.Ability);
            StaffTrainingDiscipline discipline = PlayerAbilityCatalog.IsBatterAbility(program.Ability)
                ? StaffTrainingDiscipline.Hitting
                : StaffTrainingDiscipline.Pitching;
            StaffTrainingEfficiencyResult efficiency = StaffTrainingEfficiencyResolver.Resolve(
                ResolvePlayerStaffEffects(RequireMode(runtime)),
                new StaffTrainingEfficiencyContext(discipline, includeDevelopmentCoach: true));
            return CardTrainingResolver.Preview(
                ownedCard, season, program, runtime.Economy, efficiency);
        }

        /// <summary>1군 밖 보유 카드의 유학을 TrainingCenter 정원과 시즌 1회 규칙으로 시작한다.</summary>
        public void StartOwnedCardStudy(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            CardStudyProgramDefinition program)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (ContainsCard(runtime.GetRoster(runtime.PlayerTeamSeasonKey), cardId))
                throw new InvalidOperationException("1군 등록 카드는 유학을 시작할 수 없습니다.");
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState owned))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 유학할 수 없습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("WorldCardCatalog에 유학 카드가 없습니다.");
            int capacity = _balance.OwnerCardGrowth.GetStudyCapacity(
                RequireMode(runtime).ClubOperation.GetFacility(FacilityType.TrainingCenter).Level);
            OwnerCardStudyResolver.Start(
                runtime.PlayerGrowth, owned, runtime.WorldCardCatalog.GetPlayerSeason(card), program,
                runtime.Economy, runtime.ManagerMode.LiveSeason.SeasonNumber, capacity);
        }

        /// <summary>공유 인벤토리 블록을 다른 카드와 중복하지 않게 지정 좌표에 장착한다.</summary>
        public void PlaceOwnedCardSkillBlock(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int instanceId,
            int originX,
            int originY,
            int rotationQuarterTurns)
        {
            OwnedPlayerCardState card = GetOwnedCard(runtime, cardId);
            EnsureBlockIsNotEquipped(runtime, instanceId, cardId);
            new OwnerSkillBoardService(_balance.Growth).Place(
                runtime.PlayerGrowth.Inventory, card.SkillBoard, instanceId, originX, originY, rotationQuarterTurns);
        }

        public bool AutoPlaceOwnedCardSkillBlock(ManagerHistoricalRuntimeState runtime, string cardId, int instanceId)
        {
            OwnedPlayerCardState card = GetOwnedCard(runtime, cardId);
            EnsureBlockIsNotEquipped(runtime, instanceId, cardId);
            return new OwnerSkillBoardService(_balance.Growth).TryPlaceFirstAvailable(
                runtime.PlayerGrowth.Inventory, card.SkillBoard, instanceId);
        }

        public bool RemoveOwnedCardSkillBlock(ManagerHistoricalRuntimeState runtime, string cardId, int instanceId) =>
            new OwnerSkillBoardService(_balance.Growth).Remove(GetOwnedCard(runtime, cardId).SkillBoard, instanceId);

        /// <summary>보유 중복 카드 한 장을 실패 없이 +5 상한까지 강화한다.</summary>
        public CardEnhancementResult EnhanceOwnedCard(
            ManagerHistoricalRuntimeState runtime,
            string cardId)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (runtime.IsCardReserved(cardId) ||
                (runtime.WorldCardCatalog.TryGetCard(cardId, out var definition) && definition.IsUniqueOwnedCard))
                throw new InvalidOperationException("예약 재료와 특수 영입 카드는 강화할 수 없습니다.");
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 강화할 수 없습니다.");
            return CardEnhancementResolver.Enhance(ownedCard);
        }

        /// <summary>중복을 소비하지 않고 강화 단계와 차단 사유를 조회한다.</summary>
        public CardEnhancementPreview PreviewOwnedCardEnhancement(
            ManagerHistoricalRuntimeState runtime,
            string cardId)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 강화할 수 없습니다.");
            return CardEnhancementResolver.Preview(ownedCard);
        }

        /// <summary>보유 중복 카드를 Cost·Edition 판매가로 정산해 SP에 반영한다.</summary>
        public int SellOwnedCardDuplicates(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int count)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (runtime.IsCardReserved(cardId) ||
                (runtime.WorldCardCatalog.TryGetCard(cardId, out var definition) && definition.IsUniqueOwnedCard))
                throw new InvalidOperationException("예약 재료와 특수 영입 카드는 판매할 수 없습니다.");
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 판매할 수 없습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("WorldCardCatalog에 판매 카드가 없습니다.");
            return CardSaleResolver.SellDuplicates(
                ownedCard,
                card,
                runtime.WorldCardCatalog.GetPlayerSeason(card),
                _cardSaleBalance,
                runtime.Economy,
                count);
        }

        /// <summary>중복을 소비하지 않고 선택 수량의 판매 SP와 가능 여부를 조회한다.</summary>
        public CardSalePreview PreviewOwnedCardSale(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int count)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState ownedCard))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드는 판매할 수 없습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException("WorldCardCatalog에 판매 카드가 없습니다.");
            return CardSaleResolver.Preview(
                ownedCard,
                card,
                runtime.WorldCardCatalog.GetPlayerSeason(card),
                _cardSaleBalance,
                count);
        }

        /// <summary>RecoveryCenter와 ConditioningCoach를 별도 Tick이 아닌 단일 회복 Context로 합성한다.</summary>
        public ConditionRecoveryContext CreateRecoveryContext(ManagerModeRuntimeState mode)
        {
            ClubFacilityEffectProfile facility = _facilityEffectResolver.Resolve(mode.ClubOperation);
            TeamStaffEffectProfile staff = ResolvePlayerStaffEffects(mode);
            return new ConditionRecoveryContext(
                _balance.ConditionChemistry.WeeklyBaseRecovery,
                1d + facility.ConditionRecoveryEfficiencyModifier,
                staff.ConditionRecoveryEfficiency);
        }

        /// <summary>저장 계약 경제가 없는 AI 구단의 효과를 리그·구단 키·시즌 Seed로 결정론적으로 만든다.</summary>
        public TeamStaffEffectProfile ResolveAiStaffEffects(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            ManagerModeRuntimeState mode = RequireMode(runtime);
            if (runtime.HasOwnedEconomy(teamSeasonKey))
                throw new ArgumentException("플레이어 구단은 실제 Staff 계약 효과를 사용해야 합니다.", nameof(teamSeasonKey));
            mode.GetPlayerStatus(teamSeasonKey);
            ulong seasonSeed = Baseball.Simulation.Random.DeterministicSeed.Derive(
                Baseball.Simulation.Random.DeterministicSeed.Derive(
                    runtime.WorldHistory.WorldHistorySeed,
                    AiStaffSeasonStream),
                unchecked((ulong)mode.LiveSeason.SeasonNumber));
            return _aiStaffProfileResolver.Resolve(
                runtime.LeagueWorld?.GetGroup(teamSeasonKey).League.Grade ?? runtime.League.Grade,
                NeutralAiManagerQuality,
                CreateNeutralClubState(teamSeasonKey),
                seasonSeed,
                _balance.Staff);
        }

        /// <summary>AI Staff 효과를 시설 없는 하나의 회복 Context로 합성한다.</summary>
        public ConditionRecoveryContext CreateAiRecoveryContext(
            ManagerHistoricalRuntimeState runtime,
            string teamSeasonKey)
        {
            TeamStaffEffectProfile staff = ResolveAiStaffEffects(runtime, teamSeasonKey);
            return new ConditionRecoveryContext(
                _balance.ConditionChemistry.WeeklyBaseRecovery,
                1d,
                staff.ConditionRecoveryEfficiency);
        }

        /// <summary>DataAnalysisCenter와 ScoutingDirector를 단일 Intel Context로 합성한다.</summary>
        public ScoutingConfidenceContext CreateScoutingConfidenceContext(ManagerModeRuntimeState mode)
        {
            ClubFacilityEffectProfile facility = _facilityEffectResolver.Resolve(mode.ClubOperation);
            TeamStaffEffectProfile staff = ResolvePlayerStaffEffects(mode);
            return new ScoutingConfidenceContext(
                1d,
                facility.ScoutingConfidenceModifier,
                staff.ScoutingConfidenceModifier);
        }

        /// <summary>한 주의 시설 생산과 선수 회복을 같은 영수증 경계에서 정확히 한 번 처리한다.</summary>
        public ManagerWeeklyAdvanceResult AdvanceWeek(
            ManagerHistoricalRuntimeState runtime)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            ManagerLiveSeasonState season = mode.LiveSeason;
            WeeklyFacilityProductionResult production = _weeklyProductionResolver.Resolve(
                mode.ClubOperation,
                new WeeklyFacilityProductionContext(
                    season.SeasonId,
                    season.CurrentWeekIndex,
                    runtime.League.Grade,
                    runtime.Economy.Money,
                    runtime.Economy.ScoutingPoints,
                    runtime.Economy.DevelopmentPoints));

            IReadOnlyList<ManagerTeamRecoveryResult> recoveries = Array.Empty<ManagerTeamRecoveryResult>();
            ManagerModeTransactionStatus status;
            if (production.Status == WeeklyFacilityProductionStatus.AlreadyApplied)
            {
                status = ManagerModeTransactionStatus.AlreadyApplied;
            }
            else
            {
                if (!CanApply(runtime.Economy, production.Receipt.ResourceDelta))
                    return new ManagerWeeklyAdvanceResult(
                        ManagerModeTransactionStatus.InsufficientMoney,
                        production,
                        recoveries);
                if (!mode.ClubOperation.TryApplyWeeklyProduction(production))
                    throw new InvalidOperationException("검증된 주간 생산 결과를 ClubOperationState가 거부했습니다.");
                Apply(runtime.Economy, production.Receipt.ResourceDelta);
                recoveries = ApplyTeamRecoveries(runtime, mode);
                status = ManagerModeTransactionStatus.Applied;
            }

            season.AdvanceWeek();
            AdvanceStudies(runtime);
            mode.ClubOperation.BeginWeek(season.CurrentWeekIndex);
            return new ManagerWeeklyAdvanceResult(status, production, recoveries);
        }

        private void AdvanceStudies(ManagerHistoricalRuntimeState runtime)
        {
            for (int index = runtime.PlayerGrowth.StudyProjects.Count - 1; index >= 0; index--)
            {
                CardStudyProjectState project = runtime.PlayerGrowth.StudyProjects[index];
                if (!project.AdvanceWeek()) continue;
                OwnedPlayerCardState owned = GetOwnedCard(runtime, project.CardId);
                PlayerCardDefinition card = runtime.WorldCardCatalog.TryGetCard(project.CardId, out PlayerCardDefinition found)
                    ? found
                    : throw new InvalidOperationException("유학 카드 원본이 없습니다.");
                OwnerCardStudyResolver.Complete(
                    owned,
                    runtime.WorldCardCatalog.GetPlayerSeason(card),
                    _balance.OwnerCardGrowth.GetStudyProgram(project.ProgramId));
                runtime.PlayerGrowth.RemoveStudyAt(index);
            }
        }

        private static OwnedPlayerCardState GetOwnedCard(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.TryGetOwnedCard(cardId, out OwnedPlayerCardState card))
                throw new InvalidOperationException("플레이어 구단이 소유하지 않은 카드입니다.");
            return card;
        }

        private static bool ContainsCard(CurrentRosterState roster, string cardId)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
                if (string.Equals(roster.Entries[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsStudying(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            for (int index = 0; index < runtime.PlayerGrowth.StudyProjects.Count; index++)
                if (string.Equals(runtime.PlayerGrowth.StudyProjects[index].CardId, cardId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void EnsureBlockIsNotEquipped(
            ManagerHistoricalRuntimeState runtime, int instanceId, string targetCardId)
        {
            runtime.PlayerGrowth.Inventory.GetRequired(instanceId);
            for (int cardIndex = 0; cardIndex < runtime.OwnedCards.Count; cardIndex++)
            {
                OwnedPlayerCardState owned = runtime.OwnedCards[cardIndex];
                for (int blockIndex = 0; blockIndex < owned.SkillBoard.Placements.Count; blockIndex++)
                {
                    if (owned.SkillBoard.Placements[blockIndex].Instance.InstanceId != instanceId) continue;
                    if (string.Equals(owned.CardId, targetCardId, StringComparison.Ordinal))
                        throw new InvalidOperationException("이 블록은 이미 해당 카드에 장착되어 있습니다.");
                    throw new InvalidOperationException("이 블록은 다른 카드에 장착되어 있습니다.");
                }
            }
        }

        private static void ValidateTrainingDiscipline(PlayerSeasonDefinition season, PlayerAbility ability)
        {
            bool isBatterAbility = PlayerAbilityCatalog.IsBatterAbility(ability);
            if ((season.PlayerType == Baseball.Core.Players.PlayerType.Batter) != isBatterAbility)
                throw new InvalidOperationException("선수 유형에 맞는 능력치만 카드 훈련할 수 있습니다.");
        }

        /// <summary>연봉·계약 마감 후 전체 구단의 순위 승강과 조 재추첨을 적용하고 다음 시즌을 연다.</summary>
        public ManagerSeasonAdvanceResult AdvanceSeason(ManagerHistoricalRuntimeState runtime)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            if (!mode.LiveSeason.IsCompleted || runtime.LeagueWorld != null && !runtime.LeagueWorld.IsCompleted)
            {
                return new ManagerSeasonAdvanceResult(
                    ManagerSeasonAdvanceStatus.SeasonInProgress,
                    mode.ClubOperation.CurrentSeason,
                    null,
                    null,
                    null);
            }

            _playerMarketService.EnsureInitialized(runtime);
            if (mode.HasExpiringPlayerContracts())
            {
                return new ManagerSeasonAdvanceResult(
                    ManagerSeasonAdvanceStatus.ContractRenewalRequired,
                    mode.ClubOperation.CurrentSeason,
                    null,
                    null,
                    null);
            }

            int completedSeasonNumber = mode.LiveSeason.SeasonNumber;
            string salaryTransactionId =
                $"staff-salary:{runtime.PlayerTeamSeasonKey}:{completedSeasonNumber:D4}";
            var salaryCommand = new StaffSalarySettlementCommand(
                salaryTransactionId,
                runtime.PlayerTeamSeasonKey,
                completedSeasonNumber,
                runtime.Economy.Money);
            StaffSalarySettlementResult salary = _staffContractService.SettleSalaries(
                salaryCommand,
                mode.StaffContracts,
                ContractPaymentMode.AllowArrears);
            if (!salary.IsSuccess)
            {
                return new ManagerSeasonAdvanceResult(
                    ManagerSeasonAdvanceStatus.InvalidStaffState,
                    mode.ClubOperation.CurrentSeason,
                    salary,
                    null,
                    null);
            }

            StaffContractAdvanceResult staffAdvance = _staffContractService.AdvanceSeason(
                runtime.PlayerTeamSeasonKey,
                completedSeasonNumber,
                salary.Contracts,
                mode.StaffAssignment);
            if (!staffAdvance.IsSuccess)
            {
                return new ManagerSeasonAdvanceResult(
                    ManagerSeasonAdvanceStatus.InvalidStaffState,
                    mode.ClubOperation.CurrentSeason,
                    salary,
                    staffAdvance,
                    null);
            }

            OwnerLeagueWorldState nextWorld = new OwnerLeagueWorldService(_balance).PlanNextSeason(runtime);
            OwnerLeagueGroupState nextGroup = nextWorld.GetGroup(runtime.PlayerTeamSeasonKey);
            ManagerLiveSeasonState nextSeason = nextGroup.Season;
            ClubOperationState nextOperation = ManagerModeRuntimeFactory.CreateNextClubOperation(
                mode.ClubOperation,
                nextSeason.SeasonId);
            SeasonFinanceSummary completedFinance = mode.ClubOperation.CurrentSeason;
            LeagueGrade previousGrade = runtime.League.Grade;
            var completedSeasonState = new ManagerCompletedSeasonState(mode.LiveSeason, previousGrade);
            LeagueGrade nextGrade = nextGroup.League.Grade;

            long playerSalary = mode.GetAnnualPlayerSalaryTotal();
            long totalSalary = checked(salary.TotalSalary + playerSalary);
            runtime.Economy.SettleContractPayment(totalSalary);
            mode.SettleAndAdvancePlayerContracts(completedSeasonNumber);
            mode.AdvanceSeason(
                nextOperation,
                nextSeason,
                staffAdvance.Contracts,
                staffAdvance.Assignment,
                completedSeasonState);
            runtime.SetLeagueWorld(nextWorld);
            runtime.ShopPurchaseHistory.ResetPeriod();
            return new ManagerSeasonAdvanceResult(
                ManagerSeasonAdvanceStatus.Applied,
                completedFinance,
                salary,
                staffAdvance,
                nextSeason,
                previousGrade,
                nextGrade);
        }

        public ManagerModeTransactionStatus ApplyHomeGameFinance(
            ManagerHistoricalRuntimeState runtime,
            HomeGameContext context,
            Baseball.Simulation.Random.IRandomSource random,
            out HomeGameFinanceResult result)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            result = _homeGameFinanceResolver.Resolve(context, mode.ClubOperation, random);
            if (result.Status == HomeGameFinanceStatus.NotHomeGame)
                return ManagerModeTransactionStatus.Rejected;
            if (result.Status == HomeGameFinanceStatus.AlreadyApplied)
                return ManagerModeTransactionStatus.AlreadyApplied;
            if (!CanApply(runtime.Economy, result.Receipt.ResourceDelta))
                return ManagerModeTransactionStatus.InsufficientMoney;
            if (!mode.ClubOperation.TryApplyHomeGame(result))
                throw new InvalidOperationException("검증된 홈 경기 재무 결과를 ClubOperationState가 거부했습니다.");
            Apply(runtime.Economy, result.Receipt.ResourceDelta);
            return ManagerModeTransactionStatus.Applied;
        }

        public StaffSigningResult SignStaff(
            ManagerHistoricalRuntimeState runtime,
            StaffMarketOffer offer,
            int contractSequence)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            ManagerModeRuntimeState mode = RequireMode(runtime);
            string contractId = StaffContractService.CreateStableContractId(
                runtime.PlayerTeamSeasonKey,
                offer.StaffId,
                mode.LiveSeason.SeasonNumber,
                contractSequence);
            var command = new StaffSigningCommand(
                contractId,
                $"staff-sign:{contractId}",
                runtime.PlayerTeamSeasonKey,
                mode.LiveSeason.SeasonNumber,
                runtime.Economy.Money);
            StaffSigningResult result = _staffContractService.TrySign(
                command,
                offer,
                mode.StaffCatalog,
                mode.StaffContracts,
                mode.StaffAssignment,
                _balance.Staff);
            if (!result.IsSuccess)
                return result;
            if (result.MoneyCommand != null && !runtime.Economy.TrySpendMoney(result.MoneyCommand.Amount))
                throw new InvalidOperationException("검증된 Staff 계약 비용을 반영할 수 없습니다.");
            mode.ReplaceStaffState(result.Contracts, result.Assignment);
            return result;
        }

        public StaffSalarySettlementResult SettleStaffSalary(ManagerHistoricalRuntimeState runtime)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            string transactionId = $"staff-salary:{runtime.PlayerTeamSeasonKey}:{mode.LiveSeason.SeasonNumber:D4}";
            var command = new StaffSalarySettlementCommand(
                transactionId,
                runtime.PlayerTeamSeasonKey,
                mode.LiveSeason.SeasonNumber,
                runtime.Economy.Money);
            StaffSalarySettlementResult result = _staffContractService.SettleSalaries(
                command, mode.StaffContracts, ContractPaymentMode.AllowArrears);
            if (!result.IsSuccess || result.Status == StaffServiceStatus.NoChange)
                return result;
            runtime.Economy.SettleContractPayment(result.MoneyCommand.Amount);
            mode.ReplaceStaffState(result.Contracts, mode.StaffAssignment);
            return result;
        }

        public FacilityUpgradeResult UpgradeFacility(
            ManagerHistoricalRuntimeState runtime,
            FacilityType facilityType,
            string operationId)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            ClubUpgradeContext context = CreateUpgradeContext(runtime, operationId);
            FacilityUpgradeResult result = _upgradeResolver.ResolveFacilityUpgrade(
                mode.ClubOperation,
                facilityType,
                context);
            if (!result.IsApproved)
                return result;
            if (!runtime.Economy.TrySpendMoney(result.MoneyCost))
                throw new InvalidOperationException("검증된 시설 업그레이드 비용을 반영할 수 없습니다.");
            if (!mode.ClubOperation.TryApplyFacilityUpgrade(result))
                throw new InvalidOperationException("검증된 시설 업그레이드를 상태가 거부했습니다.");
            return result;
        }

        public StadiumUpgradeResult UpgradeStadium(
            ManagerHistoricalRuntimeState runtime,
            string operationId)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            ClubUpgradeContext context = CreateUpgradeContext(runtime, operationId);
            StadiumUpgradeResult result = _upgradeResolver.ResolveStadiumUpgrade(
                mode.ClubOperation,
                context);
            if (!result.IsApproved)
                return result;
            if (!runtime.Economy.TrySpendMoney(result.MoneyCost))
                throw new InvalidOperationException("검증된 구장 증축 비용을 반영할 수 없습니다.");
            if (!mode.ClubOperation.TryApplyStadiumUpgrade(result))
                throw new InvalidOperationException("검증된 구장 증축을 상태가 거부했습니다.");
            return result;
        }

        private static ClubUpgradeContext CreateUpgradeContext(
            ManagerHistoricalRuntimeState runtime,
            string operationId)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            return new ClubUpgradeContext(
                operationId,
                mode.LiveSeason.SeasonId,
                mode.LiveSeason.CurrentWeekIndex,
                runtime.League.Grade,
                mode.ClubOperation.FanBase,
                mode.ClubOperation.CurrentSeason.Attendance,
                runtime.Economy.Money);
        }

        private IReadOnlyList<ManagerTeamRecoveryResult> ApplyTeamRecoveries(
            ManagerHistoricalRuntimeState runtime,
            ManagerModeRuntimeState mode)
        {
            var results = new ManagerTeamRecoveryResult[runtime.WorldRosters.Count];
            for (int index = 0; index < runtime.WorldRosters.Count; index++)
            {
                string teamSeasonKey = runtime.WorldRosters[index].TeamSeasonKey;
                bool isPlayerTeam = runtime.HasOwnedEconomy(teamSeasonKey);
                ConditionRecoveryContext context = isPlayerTeam
                    ? CreateRecoveryContext(mode)
                    : CreateAiRecoveryContext(runtime, teamSeasonKey);
                int recovery = _conditionRecoveryResolver.ApplyRecovery(
                    mode.GetPlayerStatus(teamSeasonKey),
                    context);
                results[index] = new ManagerTeamRecoveryResult(teamSeasonKey, isPlayerTeam, recovery);
            }
            return results;
        }

        private static TeamSeasonClubState CreateNeutralClubState(string teamSeasonKey)
        {
            // Owner runtime에는 아직 정본 TeamSeasonClubState/ManagerQuality가 저장되지 않는다.
            // 임의의 숨은 상태를 새로 만들지 않고 중립 입력을 사용하며, 구단별 차이는 Resolver의 stable key Seed가 낸다.
            return new TeamSeasonClubState(
                teamSeasonKey,
                new ClubDnaRatings(
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating,
                    NeutralClubDnaRating));
        }

        private static ManagerModeRuntimeState RequireMode(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("구형 구단주 Save에는 v4 ManagerMode migration이 필요합니다.");
            return runtime.ManagerMode;
        }

        private static bool CanApply(ManagerEconomyState economy, OperationResourceDelta delta)
        {
            if (delta.Money < 0L && economy.Money < -delta.Money)
                return false;
            checked
            {
                if (delta.Money > 0L) _ = economy.Money + delta.Money;
                _ = economy.ScoutingPoints + delta.ScoutingPoints;
                _ = economy.DevelopmentPoints + delta.DevelopmentPoints;
            }
            return true;
        }

        private static void Apply(ManagerEconomyState economy, OperationResourceDelta delta)
        {
            if (delta.Money < 0L)
            {
                if (!economy.TrySpendMoney(-delta.Money))
                    throw new InvalidOperationException("검증된 Money 지출을 반영할 수 없습니다.");
            }
            else if (delta.Money > 0L)
            {
                economy.AddMoney(delta.Money);
            }
            if (delta.ScoutingPoints > 0) economy.AddScoutingPoints(delta.ScoutingPoints);
            if (delta.DevelopmentPoints > 0) economy.AddDevelopmentPoints(delta.DevelopmentPoints);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Data;
using Baseball.Game.Diagnostics;
using Baseball.Game.Manager;
using Baseball.Game.Unity.Persistence;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;

namespace Baseball.Game.Historical
{
    /// <summary>UI가 재계산하지 않도록 Game 경계에서 확정한 선수별 Condition Snapshot이다.</summary>
    public sealed class OwnerModeConditionEntry
    {
        public OwnerModeConditionEntry(
            string playerPersonId,
            string displayName,
            PlayerPosition naturalPosition,
            bool isPitcher,
            PlayerAvailabilityStatus availability,
            EffectiveMatchCondition effectiveCondition)
        {
            PlayerPersonId = playerPersonId ?? throw new ArgumentNullException(nameof(playerPersonId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            NaturalPosition = naturalPosition;
            IsPitcher = isPitcher;
            Availability = availability;
            EffectiveCondition = effectiveCondition;
        }

        public string PlayerPersonId { get; }
        public string DisplayName { get; }
        public PlayerPosition NaturalPosition { get; }
        public bool IsPitcher { get; }
        public PlayerAvailabilityStatus Availability { get; }
        public EffectiveMatchCondition EffectiveCondition { get; }
    }

    /// <summary>Simulation 검증과 공통 로스터 규칙에서 확정한 구단주 1군 요약이다.</summary>
    public sealed class OwnerModeRosterStatus
    {
        public OwnerModeRosterStatus(
            int activeRosterCount,
            int hitterCount,
            int pitcherCount,
            int foreignPlayerCount,
            RosterValidationResult validation,
            RosterStrengthBreakdown strength = null,
            RosterCostBreakdown? cost = null)
        {
            if (activeRosterCount < 0 || hitterCount < 0 || pitcherCount < 0 || foreignPlayerCount < 0)
                throw new ArgumentOutOfRangeException(nameof(activeRosterCount));

            ActiveRosterCount = activeRosterCount;
            HitterCount = hitterCount;
            PitcherCount = pitcherCount;
            ForeignPlayerCount = foreignPlayerCount;
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            Strength = strength;
            Cost = cost;
        }

        public int ActiveRosterCount { get; }
        public int ActiveRosterCapacity => ActiveRosterCompositionRule.ActiveRosterSize;
        public int HitterCount { get; }
        public int RequiredHitterCount => ActiveRosterCompositionRule.HitterCount;
        public int PitcherCount { get; }
        public int RequiredPitcherCount => ActiveRosterCompositionRule.PitcherCount;
        public int ForeignPlayerCount { get; }
        public int ForeignPlayerLimit => ActiveRosterCompositionRule.MaxForeignPlayers;
        public RosterValidationResult Validation { get; }
        public RosterStrengthBreakdown Strength { get; }
        public RosterCostBreakdown? Cost { get; }
    }

    /// <summary>구단주 Production Runtime과 저장·운영·경기 Command를 영속 GameRoot에서 소유한다.</summary>
    public sealed class OwnerModeManager : ManagerBehaviour<OwnerModeManager>
    {
        private string[] _availableTeamColorIds = Array.Empty<string>();
        private string[] _availableTacticCardIds = Array.Empty<string>();
        private TeamColorDefinition[] _teamColors = Array.Empty<TeamColorDefinition>();
        private TacticCardDefinition[] _tacticCards = Array.Empty<TacticCardDefinition>();
        private IHistoricalContentProvider _contentProvider;
        private BalanceTable _balance;
        private OwnerModeNewGameConfiguration _newGameConfiguration;
        private ManagerHistoricalSaveAdapter _saveAdapter;
        private ManagerHistoricalSaveJsonStore _saveStore;
        private ManagerModeCoordinator _coordinator;
        private ManagerPregameService _pregameService;
        private ManagerModeMatchService _matchService;
        private StaffMarketResolver _staffMarketResolver;
        private IBakedWorldHistorySource _bakedWorldHistorySource;
        private HistoricalWorldRuntimeBuilder _worldBuilder;

        public override int InitializationOrder => -20;
        public ManagerHistoricalRuntimeState Runtime { get; private set; }
        public ManagerPregamePreparation CurrentPregame { get; private set; }
        public ManagerModeMatchResult LastMatch { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool HasActiveRuntime => Runtime != null;
        public BalanceTable Balance => _balance;
        public string SaveFilePath => _saveStore?.FilePath ?? string.Empty;
        public bool HasSave => _saveStore != null && _saveStore.Exists;

        public event Action RuntimeChanged;

        protected override void OnInitialize()
        {
            ConfigureServices(
                NewGameDefinition.LoadHistoricalContentProvider(),
                NewGameDefinition.LoadOwnerModeBalanceTable(),
                NewGameDefinition.LoadOwnerModeConfiguration(),
                new ManagerHistoricalSaveJsonStore(ManagerHistoricalSavePath.GetDefaultFilePath()),
                NewGameDefinition.LoadBakedWorldHistorySource());
        }

        protected override void OnShutdown()
        {
            RuntimeChanged = null;
            Runtime = null;
            CurrentPregame = null;
            LastMatch = null;
        }

        /// <summary>직렬화된 기본 Seed와 첫 유효 정규구단으로 새 구단주 Runtime을 만든다.</summary>
        public bool StartNewGame()
        {
            try
            {
                HistoricalBakedContent content = _contentProvider.Load()
                    ?? throw new InvalidOperationException("Historical Content가 없습니다.");
                HistoricalYearContentDefinition year = content.GetYear(_newGameConfiguration.OriginYear);
                string teamSeasonKey = ResolvePlayerTeamSeasonKey(year, content, _newGameConfiguration.PlayerTeamSeasonKey);
                OwnerModeEntryProfiler.Mark("콘텐츠 로드·팀 결정");

                // World 자체는 워밍업이 만들어 뒀더라도 Card Catalog·합성팀은 지연 생성이라
                // 여기서 처음 만들어질 수 있다. 어느 쪽이 비용인지 구분해서 남긴다.
                PrewarmNewGameWorld();
                OwnerModeEntryProfiler.Mark("World·파생물 확보");

                var service = new ManagerHistoricalNewGameService(
                    _contentProvider,
                    _worldBuilder,
                    _balance);
                Runtime = service.Create(new ManagerHistoricalNewGameRequest(
                    WorldRecordMode.SimulatedHistory,
                    _newGameConfiguration.WorldSeed,
                    _newGameConfiguration.OriginYear,
                    _newGameConfiguration.LeagueInstanceId,
                    teamSeasonKey,
                    new ManagerEconomyState(
                        _newGameConfiguration.InitialMoney,
                        _newGameConfiguration.InitialScoutingPoints,
                        _newGameConfiguration.InitialDevelopmentPoints)));
                OwnerModeEntryProfiler.Mark("Runtime 생성(리그·로스터·스태프)");

                RosterValidationResult rosterValidation = new ActiveRosterValidator().Validate(
                    Runtime.GetRoster(teamSeasonKey));
                if (!rosterValidation.IsValid)
                    throw new InvalidOperationException("첫 유효 정규구단의 ActiveRoster 검증에 실패했습니다.");
                OwnerModeEntryProfiler.Mark("로스터 검증");

                ConfigureTeamColors(content, teamSeasonKey);
                ApplyStarterLoadout(Runtime.ManagerMode);
                OwnerModeEntryProfiler.Mark("팀 컬러·스타터 로드아웃");

                CurrentPregame = null;
                LastMatch = null;
                LastError = string.Empty;
                NotifyRuntimeChanged();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                LastError = exception.Message;
                Runtime = null;
                CurrentPregame = null;
                return false;
            }
        }

        /// <summary>
        /// Bake TextAsset의 바이트를 미리 확보한다. 워밍업이 워커 스레드에서 World를 만들려면
        /// 그 전에 메인 스레드에서 이것을 호출해야 한다. TextAsset은 워커에서 읽을 수 없다.
        /// </summary>
        public void CacheBakedWorldHistoryBytesOnMainThread()
        {
            (_bakedWorldHistorySource as UnityBakedWorldHistorySource)?.CacheAssetBytesOnMainThread();
        }

        /// <summary>확보해 둔 Bake 바이트를 놓아준다. 복원된 World는 Builder가 그대로 들고 있다.</summary>
        public void ReleaseBakedWorldHistoryByteCache()
        {
            (_bakedWorldHistorySource as UnityBakedWorldHistorySource)?.ReleaseAssetByteCache();
        }

        /// <summary>
        /// 지금 조건에 맞는 Bake가 있는지 미리 확인한다. 적중하면 복원 결과가 Source에 캐시되므로
        /// 뒤이은 World 생성이 그것을 그대로 쓴다. 미스면 44시즌을 실제로 시뮬레이션하게 된다.
        /// 로딩 화면이 남은 시간을 안내하려면 이 구분이 필요하다.
        /// </summary>
        public bool HasMatchingBakedWorldHistory()
        {
            if (_bakedWorldHistorySource == null)
                return false;

            HistoricalBakedContent content = _contentProvider.Load();
            if (content == null)
                return false;
            return _bakedWorldHistorySource.TryLoad(
                HistoricalWorldRuntimeBuilder.CreateBakeKey(
                    content, _newGameConfiguration.WorldSeed, _balance),
                out _);
        }

        /// <summary>
        /// 로딩 화면에서 새 게임에 필요한 World를 미리 만들어 둔다.
        /// UnityHistoricalContentProvider가 바이트를 미리 확보했다면 워커 스레드에서 호출해도 된다.
        /// Runtime 상태를 만들지는 않으므로, 실제 새 게임 시작 전까지 게임 상태는 바뀌지 않는다.
        /// </summary>
        public void PrewarmNewGameWorld(CancellationToken cancellationToken = default)
        {
            HistoricalBakedContent content = _contentProvider.Load()
                ?? throw new InvalidOperationException("Historical Content가 없습니다.");
            HistoricalWorldRuntimeContent world = _worldBuilder.GetOrBuild(
                content,
                WorldRecordMode.SimulatedHistory,
                _newGameConfiguration.WorldSeed,
                cancellationToken);

            // World를 만들어 둬도 Card Catalog와 합성팀은 지연 생성이라, 새 게임을 시작하는 순간
            // 처음 만들어진다. 그 비용까지 여기서 치러야 로딩 화면이 실제로 다 기다린 것이 된다.
            // 새 게임은 시작 연도 한 해의 합성팀만 쓰므로 44년치를 만들지 않는다.
            cancellationToken.ThrowIfCancellationRequested();
            _ = world.WorldCardCatalog;
            cancellationToken.ThrowIfCancellationRequested();
            _ = world.GetSpecialCompositeTeamSet(_newGameConfiguration.OriginYear);
        }

        public void Save()
        {
            RequireRuntime();
            _saveStore.Save(_saveAdapter.CreateSaveData(Runtime));
            LastError = string.Empty;
            NotifyRuntimeChanged();
        }

        public void Load()
        {
            ManagerHistoricalSaveData saveData = _saveStore.Load();
            OwnerModeEntryProfiler.Mark("세이브 파일 읽기·역직렬화");

            Runtime = new ManagerHistoricalLoadService(_saveAdapter).Restore(saveData);
            OwnerModeEntryProfiler.Mark("Runtime 복원");

            ConfigureTeamColors(_contentProvider.Load(), Runtime.PlayerTeamSeasonKey);
            CurrentPregame = null;
            LastMatch = null;
            LastError = string.Empty;
            OwnerModeEntryProfiler.Mark("팀 컬러 적용");
            NotifyRuntimeChanged();
        }

        public ManagerWeeklyAdvanceResult AdvanceWeek()
        {
            ManagerWeeklyAdvanceResult result = _coordinator.AdvanceWeek(RequireRuntime());
            InvalidatePregame();
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>남은 구단 경기가 없을 때 급여·계약·재무를 마감하고 다음 운영 시즌을 연다.</summary>
        public ManagerSeasonAdvanceResult AdvanceSeason()
        {
            ManagerSeasonAdvanceResult result = _coordinator.AdvanceSeason(RequireRuntime());
            if (result.IsApplied)
            {
                CurrentPregame = null;
                LastMatch = null;
            }
            NotifyRuntimeChanged();
            return result;
        }

        public FacilityUpgradeResult UpgradeFacility(FacilityType facilityType)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            string operationId = $"facility:{runtime.PlayerTeamSeasonKey}:{runtime.ManagerMode.LiveSeason.SeasonId}:" +
                                 $"{runtime.ManagerMode.LiveSeason.CurrentWeekIndex}:{(int)facilityType}:" +
                                 $"{runtime.ManagerMode.ClubOperation.GetFacility(facilityType).Level + 1}";
            FacilityUpgradeResult result = _coordinator.UpgradeFacility(runtime, facilityType, operationId);
            NotifyRuntimeChanged();
            return result;
        }

        public StadiumUpgradeResult UpgradeStadium()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            string operationId = $"stadium:{runtime.PlayerTeamSeasonKey}:{runtime.ManagerMode.LiveSeason.SeasonId}:" +
                                 $"{runtime.ManagerMode.LiveSeason.CurrentWeekIndex}:" +
                                 $"{runtime.ManagerMode.ClubOperation.Stadium.Level + 1}";
            StadiumUpgradeResult result = _coordinator.UpgradeStadium(runtime, operationId);
            NotifyRuntimeChanged();
            return result;
        }

        public void SetTicketPolicy(TicketPriceTier priceTier)
        {
            RequireRuntime().ManagerMode.ClubOperation.SetTicketPolicy(new TicketPolicy(priceTier));
            NotifyRuntimeChanged();
        }

        public IReadOnlyList<StaffMarketOffer> GetStaffMarketOffers()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            string periodId = $"{mode.LiveSeason.SeasonId}:W{mode.LiveSeason.CurrentWeekIndex:D3}";
            return _staffMarketResolver.CreateOffers(
                mode.StaffCatalog,
                mode.StaffContracts,
                runtime.PlayerTeamSeasonKey,
                periodId,
                StaffMarketKind.MidseasonReplacement,
                runtime.League.Grade,
                runtime.WorldHistory.WorldHistorySeed,
                _balance.Staff);
        }

        public StaffSigningResult SignStaff(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                throw new ArgumentException("OfferId가 필요합니다.", nameof(offerId));
            IReadOnlyList<StaffMarketOffer> offers = GetStaffMarketOffers();
            for (int index = 0; index < offers.Count; index++)
            {
                if (!string.Equals(offers[index].OfferId, offerId, StringComparison.Ordinal)) continue;
                StaffSigningResult result = _coordinator.SignStaff(
                    RequireRuntime(),
                    offers[index],
                    RequireRuntime().ManagerMode.StaffContracts.Count + 1);
                InvalidatePregame();
                NotifyRuntimeChanged();
                return result;
            }
            throw new InvalidOperationException("현재 시장에 없는 Staff Offer입니다.");
        }

        public StaffSigningResult PreviewStaffSigning(StaffMarketOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            int sequence = runtime.ManagerMode.StaffContracts.Count + 1;
            string contractId = StaffContractService.CreateStableContractId(
                runtime.PlayerTeamSeasonKey,
                offer.StaffId,
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                sequence);
            return new StaffContractService().TrySign(
                new StaffSigningCommand(
                    contractId,
                    $"preview-staff:{contractId}",
                    runtime.PlayerTeamSeasonKey,
                    runtime.ManagerMode.LiveSeason.SeasonNumber,
                    runtime.Economy.Money),
                offer,
                runtime.ManagerMode.StaffCatalog,
                runtime.ManagerMode.StaffContracts,
                runtime.ManagerMode.StaffAssignment,
                _balance.Staff);
        }

        public TeamStaffEffectProfile PreviewStaffEffects(StaffSigningResult signing)
        {
            if (signing == null) throw new ArgumentNullException(nameof(signing));
            ManagerModeRuntimeState mode = RequireRuntime().ManagerMode;
            return new TeamStaffEffectResolver().Resolve(
                mode.StaffCatalog,
                signing.Contracts,
                signing.Assignment,
                _balance.Staff);
        }

        public StaffSalarySettlementResult SettleStaffSalary()
        {
            StaffSalarySettlementResult result = _coordinator.SettleStaffSalary(RequireRuntime());
            NotifyRuntimeChanged();
            return result;
        }

        public void SelectLineupPreset(string presetId)
        {
            RequireRuntime().ManagerMode.SelectLineupPreset(presetId);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        public void UpsertLineupPreset(LineupPresetState preset)
        {
            RequireRuntime().ManagerMode.UpsertLineupPreset(preset);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        /// <summary>UI가 저장 상태를 바꾸지 않고 임의 프리셋의 현재 경기 유효성을 확인한다.</summary>
        public LineupPresetValidationResult ValidateLineupPreset(LineupPresetState preset)
        {
            return _pregameService.ValidateLineupPreset(
                RequireRuntime(),
                preset,
                _availableTeamColorIds,
                _availableTacticCardIds);
        }

        /// <summary>현재 로스터에서 활성화된 TeamColor 후보 Definition을 안정된 순서로 반환한다.</summary>
        public IReadOnlyList<TeamColorDefinition> GetAvailableTeamColors()
        {
            var result = new List<TeamColorDefinition>(_availableTeamColorIds.Length);
            for (int availableIndex = 0; availableIndex < _availableTeamColorIds.Length; availableIndex++)
            {
                for (int definitionIndex = 0; definitionIndex < _teamColors.Length; definitionIndex++)
                {
                    TeamColorDefinition definition = _teamColors[definitionIndex];
                    if (!string.Equals(definition.TeamColorId, _availableTeamColorIds[availableIndex],
                            StringComparison.Ordinal))
                        continue;
                    result.Add(definition);
                    break;
                }
            }
            return result;
        }

        /// <summary>현재 구단주 Save가 실제 경기에서 장착할 수 있는 전술카드 Definition을 반환한다.</summary>
        public IReadOnlyList<TacticCardDefinition> GetAvailableTacticCards()
        {
            var result = new TacticCardDefinition[_tacticCards.Length];
            Array.Copy(_tacticCards, result, result.Length);
            return result;
        }

        public ManagerPregamePreparation PrepareNextGame()
        {
            CurrentPregame = _pregameService.PrepareNextGame(
                RequireRuntime(),
                _availableTeamColorIds,
                _availableTacticCardIds);
            return CurrentPregame;
        }

        public ManagerModeMatchResult PlayNextGame(
            IMatchEventSink eventSink = null,
            MatchExecutionProfile? executionProfile = null)
        {
            ManagerPregamePreparation preparation = CurrentPregame ?? PrepareNextGame();
            if (!preparation.CanStartGame)
                throw new InvalidOperationException("현재 경기 준비 상태로 경기를 시작할 수 없습니다.");
            LastMatch = _matchService.PlayNextGame(RequireRuntime(), eventSink, executionProfile);
            CurrentPregame = null;
            NotifyRuntimeChanged();
            return LastMatch;
        }

        public TeamStaffEffectProfile GetStaffEffects()
        {
            return _coordinator.ResolvePlayerStaffEffects(RequireRuntime().ManagerMode);
        }

        public CardTrainingResult TrainOwnedCard(string cardId, CardTrainingProgramDefinition program)
        {
            CardTrainingResult result = _coordinator.TrainOwnedCard(RequireRuntime(), cardId, program);
            InvalidatePregame();
            NotifyRuntimeChanged();
            return result;
        }

        public ClubFacilityEffectProfile GetFacilityEffects()
        {
            return new ClubFacilityEffectResolver(_balance.ClubOperation)
                .Resolve(RequireRuntime().ManagerMode.ClubOperation);
        }

        public FacilityUpgradeResult PreviewFacilityUpgrade(FacilityType facilityType)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            return new ClubUpgradeResolver(_balance.ClubOperation).ResolveFacilityUpgrade(
                runtime.ManagerMode.ClubOperation,
                facilityType,
                CreateUpgradeContext(runtime, $"preview-facility:{(int)facilityType}"));
        }

        public StadiumUpgradeResult PreviewStadiumUpgrade()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            return new ClubUpgradeResolver(_balance.ClubOperation).ResolveStadiumUpgrade(
                runtime.ManagerMode.ClubOperation,
                CreateUpgradeContext(runtime, "preview-stadium"));
        }

        /// <summary>다음 경기가 홈일 때 실제 관중 Resolver와 동일한 예상 관중을 반환한다.</summary>
        public int? PreviewNextHomeAttendance()
        {
            AttendanceResult? result = _matchService.PreviewNextHomeAttendance(RequireRuntime());
            return result.HasValue ? result.Value.Attendance : null;
        }

        /// <summary>경기 준비 Resolver 결과를 선수 원본 Condition에 한 번 합성해 UI용 행을 만든다.</summary>
        public IReadOnlyList<OwnerModeConditionEntry> BuildConditionEntries()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            ManagerPregamePreparation preparation = CurrentPregame ?? PrepareNextGame();
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            TeamSeasonPlayerStatusState statuses = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            LineupPresetState preset = runtime.ManagerMode.GetSelectedLineupPreset();
            string activePitcherCardId = ResolveActivePitcherCardId(preparation, preset);
            var resolver = new EffectiveMatchConditionResolver();
            var result = new OwnerModeConditionEntry[roster.Entries.Count];
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                PlayerCardDefinition card = runtime.WorldCardCatalog.TryGetCard(entry.CardId, out PlayerCardDefinition found)
                    ? found
                    : throw new InvalidOperationException($"CardId {entry.CardId} 원본이 없습니다.");
                PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                TeamSeasonPlayerStatus status = statuses.GetRequiredPlayer(entry.PlayerPersonId);
                bool isPitcher = season.PlayerType == PlayerType.Pitcher;
                int assignmentModifier = ResolveAssignmentModifier(preparation.PresetValidation, entry.CardId);
                int lineupModifier = preparation.LineupChemistry?.GetConditionModifier(entry.PlayerPersonId) ?? 0;
                int batteryModifier = isPitcher &&
                    string.Equals(activePitcherCardId, entry.CardId, StringComparison.Ordinal) &&
                    preparation.BatteryChemistry.HasValue
                        ? preparation.BatteryChemistry.Value.PitcherConditionModifier
                        : 0;
                result[index] = new OwnerModeConditionEntry(
                    entry.PlayerPersonId,
                    runtime.IdentityRegistry.GetPlayerDisplayName(entry.PlayerPersonId),
                    season.Position,
                    isPitcher,
                    status.Availability,
                    resolver.Resolve(
                        status.StoredBaseCondition,
                        assignmentModifier,
                        lineupModifier,
                        batteryModifier,
                        0));
            }
            return result;
        }

        /// <summary>UI가 로스터 규칙을 복제하지 않도록 현재 1군의 인원 요약과 Resolver 결과를 함께 반환한다.</summary>
        public OwnerModeRosterStatus BuildRosterStatus()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            ActiveRosterCompositionRule rule = ActiveRosterCompositionRule.Standard;
            int hitters = 0;
            int pitchers = 0;
            int foreignPlayers = 0;
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (rule.IsHitterRole(entry.Role)) hitters++;
                if (rule.IsPitcherRole(entry.Role)) pitchers++;
                if (entry.RegistrationType == RegistrationType.Foreign) foreignPlayers++;
            }

            return new OwnerModeRosterStatus(
                roster.Entries.Count,
                hitters,
                pitchers,
                foreignPlayers,
                new ActiveRosterValidator(rule).Validate(roster),
                BuildTeamStrength(runtime.PlayerTeamSeasonKey),
                new RosterCostResolver(rule).Resolve(roster, runtime.WorldCardCatalog));
        }

        /// <summary>지정 구단의 현재 등록 선수 시즌 기본 능력을 평가하며 저장 상태와 경기 준비를 변경하지 않는다.</summary>
        public RosterStrengthBreakdown BuildTeamStrength(string teamSeasonKey)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            return new RosterStrengthResolver().Resolve(runtime.GetRoster(teamSeasonKey), runtime.WorldCardCatalog);
        }

        public string GetTeamDisplayName(string teamSeasonKey)
        {
            HistoricalBakedContent content = _contentProvider.Load();
            if (!content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                return teamSeasonKey ?? string.Empty;
            return Runtime == null
                ? team.FranchiseId
                : Runtime.IdentityRegistry.GetFranchiseDisplayName(team.FranchiseId);
        }

        public string GetTacticDisplayName(string tacticCardId)
        {
            for (int index = 0; index < _tacticCards.Length; index++)
                if (string.Equals(_tacticCards[index].CardId, tacticCardId, StringComparison.Ordinal))
                    return _tacticCards[index].Name;
            return tacticCardId ?? string.Empty;
        }

        private void ConfigureServices(
            IHistoricalContentProvider contentProvider,
            BalanceTable balance,
            OwnerModeNewGameConfiguration newGameConfiguration,
            ManagerHistoricalSaveJsonStore saveStore,
            IBakedWorldHistorySource bakedWorldHistorySource = null)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _newGameConfiguration = newGameConfiguration;
            _bakedWorldHistorySource = bakedWorldHistorySource;
            // 로딩 화면이 미리 만들어 둔 World를 새 게임에서 그대로 쓰려면 Builder 인스턴스를 유지해야 한다.
            _worldBuilder = new HistoricalWorldRuntimeBuilder(
                _balance,
                bakedHistorySource: _bakedWorldHistorySource);
            _saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
            _saveAdapter = new ManagerHistoricalSaveAdapter(
                _contentProvider,
                CardEditionBalanceTable.CreateInitial(),
                balance: _balance);
            _coordinator = new ManagerModeCoordinator(_balance);
            _pregameService = new ManagerPregameService(_balance, _contentProvider);
            _tacticCards = CopyTactics(newGameConfiguration.StarterTacticCards);
            _availableTacticCardIds = new string[_tacticCards.Length];
            for (int index = 0; index < _tacticCards.Length; index++)
                _availableTacticCardIds[index] = _tacticCards[index].CardId;
            _matchService = new ManagerModeMatchService(
                _contentProvider,
                _balance,
                teamColors: _teamColors,
                tacticCards: _tacticCards);
            _staffMarketResolver = new StaffMarketResolver();
        }

        private ManagerHistoricalRuntimeState RequireRuntime()
        {
            return Runtime ?? throw new InvalidOperationException("활성 구단주 Runtime이 없습니다.");
        }

        private void InvalidatePregame()
        {
            CurrentPregame = null;
        }

        private void NotifyRuntimeChanged()
        {
            RuntimeChanged?.Invoke();
        }

        private static string ResolvePlayerTeamSeasonKey(
            HistoricalYearContentDefinition year,
            HistoricalBakedContent content,
            string configuredTeamSeasonKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredTeamSeasonKey))
            {
                for (int index = 0; index < year.TeamSeasons.Count; index++)
                    if (string.Equals(year.TeamSeasons[index].TeamSeasonKey, configuredTeamSeasonKey, StringComparison.Ordinal) &&
                        IsValidRegularTeam(year.TeamSeasons[index], content))
                        return configuredTeamSeasonKey.Trim();
                throw new InvalidOperationException("설정된 구단이 해당 연도의 유효 정규구단이 아닙니다.");
            }

            for (int index = 0; index < year.TeamSeasons.Count; index++)
                if (IsValidRegularTeam(year.TeamSeasons[index], content))
                    return year.TeamSeasons[index].TeamSeasonKey;
            throw new InvalidOperationException("선택 가능한 유효 정규구단이 없습니다.");
        }

        private void ApplyStarterLoadout(ManagerModeRuntimeState mode)
        {
            LineupPresetState source = mode.GetSelectedLineupPreset();
            var tacticIds = new string[_tacticCards.Length];
            for (int index = 0; index < tacticIds.Length; index++) tacticIds[index] = _tacticCards[index].CardId;
            string[] teamColorIds = SelectStarterTeamColorIds();
            mode.UpsertLineupPreset(new LineupPresetState(
                source.PresetId,
                source.Name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                teamColorIds,
                tacticIds));
        }

        private void ConfigureTeamColors(HistoricalBakedContent content, string teamSeasonKey)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                throw new InvalidOperationException("플레이어 구단의 TeamSeasonDefinition이 없습니다.");

            var definitions = new List<TeamColorDefinition>();
            AddDefinitions(definitions,
                InitialTeamColorDefinitionFactory.CreateYearFranchise(
                    Runtime.ManagerMode.LiveSeason.OriginYear,
                    team.FranchiseId));
            AddDefinitions(definitions, InitialTeamColorDefinitionFactory.CreateFranchise(team.FranchiseId));
            definitions.Add(InitialTeamColorDefinitionFactory.CreateYear(
                Runtime.ManagerMode.LiveSeason.OriginYear));
            _teamColors = definitions.ToArray();

            IReadOnlyList<TeamColorCandidate> candidates = new TeamColorResolver().Resolve(
                Runtime.GetRoster(teamSeasonKey),
                Runtime.WorldCardCatalog,
                _teamColors);
            _availableTeamColorIds = new string[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
                _availableTeamColorIds[index] = candidates[index].Definition.TeamColorId;

            _matchService = new ManagerModeMatchService(
                _contentProvider,
                _balance,
                teamColors: _teamColors,
                tacticCards: _tacticCards);
        }

        private string[] SelectStarterTeamColorIds()
        {
            var candidates = new List<TeamColorDefinition>();
            for (int definitionIndex = 0; definitionIndex < _teamColors.Length; definitionIndex++)
            {
                TeamColorDefinition definition = _teamColors[definitionIndex];
                for (int availableIndex = 0; availableIndex < _availableTeamColorIds.Length; availableIndex++)
                {
                    if (!string.Equals(definition.TeamColorId, _availableTeamColorIds[availableIndex],
                            StringComparison.Ordinal))
                        continue;
                    candidates.Add(definition);
                    break;
                }
            }
            candidates.Sort(CompareTeamColorStrength);

            var selected = new string[LineupPresetState.TeamColorSlotCount];
            var selectedFamilies = new HashSet<TeamColorFamily>();
            int selectedCount = 0;
            for (int index = 0; index < candidates.Count && selectedCount < selected.Length; index++)
            {
                TeamColorDefinition candidate = candidates[index];
                if (!selectedFamilies.Add(candidate.Family))
                    continue;
                selected[selectedCount++] = candidate.TeamColorId;
            }
            return selected;
        }

        private static int CompareTeamColorStrength(TeamColorDefinition left, TeamColorDefinition right)
        {
            int comparison = right.StrengthScore.CompareTo(left.StrengthScore);
            if (comparison != 0) return comparison;
            comparison = right.RequiredCount.CompareTo(left.RequiredCount);
            if (comparison != 0) return comparison;
            comparison = right.Priority.CompareTo(left.Priority);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.TeamColorId, right.TeamColorId);
        }

        private static void AddDefinitions(
            ICollection<TeamColorDefinition> target,
            IReadOnlyList<TeamColorDefinition> definitions)
        {
            for (int index = 0; index < definitions.Count; index++)
                target.Add(definitions[index]);
        }

        private static TacticCardDefinition[] CopyTactics(IReadOnlyList<TacticCardDefinition> source)
        {
            var result = new TacticCardDefinition[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

        private static bool IsValidRegularTeam(TeamSeasonDefinition team, HistoricalBakedContent content)
        {
            if (team == null || team.Core25CardIds.Count != ActiveRosterCompositionRule.ActiveRosterSize)
                return false;
            for (int index = 0; index < team.Core25CardIds.Count; index++)
                if (!content.TryGetNormalCard(team.Core25CardIds[index], out PlayerCardDefinition card) ||
                    card.Edition != PlayerCardEdition.Normal)
                    return false;
            return true;
        }

        private static ClubUpgradeContext CreateUpgradeContext(
            ManagerHistoricalRuntimeState runtime,
            string operationId)
        {
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            return new ClubUpgradeContext(
                operationId,
                mode.LiveSeason.SeasonId,
                mode.LiveSeason.CurrentWeekIndex,
                runtime.League.Grade,
                mode.ClubOperation.FanBase,
                mode.ClubOperation.CurrentSeason.Attendance,
                runtime.Economy.Money);
        }

        private static int ResolveAssignmentModifier(LineupPresetValidationResult validation, string cardId)
        {
            int penalty = 0;
            for (int index = 0; index < validation.Issues.Count; index++)
            {
                LineupPresetValidationIssue issue = validation.Issues[index];
                if (string.Equals(issue.CardId, cardId, StringComparison.Ordinal))
                    penalty = Math.Max(penalty, issue.ConditionPenalty);
            }
            return -penalty;
        }

        private static string ResolveActivePitcherCardId(
            ManagerPregamePreparation preparation,
            LineupPresetState preset)
        {
            if (!preparation.CanStartGame || preset.StarterRotationCardIds.Count == 0)
                return string.Empty;
            int index = (preparation.ScheduledGame.Round - 1) % preset.StarterRotationCardIds.Count;
            return preset.StarterRotationCardIds[index] ?? string.Empty;
        }
    }
}

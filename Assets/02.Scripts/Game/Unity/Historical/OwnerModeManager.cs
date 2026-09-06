using System;
using System.Collections.Generic;
using System.Threading;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Shop;
using Baseball.Core.Teams;
using Baseball.Game.Data;
using Baseball.Game.Career;
using Baseball.Game.Diagnostics;
using Baseball.Game.Guide;
using Baseball.Game.Manager;
using Baseball.Game.Shop;
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
    public sealed partial class OwnerModeManager : ManagerBehaviour<OwnerModeManager>
    {
        private const string LosingStreakSignatureCardId = "OWNER-TACTIC-BREAK-LOSING-STREAK";
        public const int MaximumTacticPlanningGames = 10;
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
        private readonly DugoutStaffCatalog _dugoutCatalog = DugoutStaffCatalog.CreateDefault();
        private readonly DugoutTacticalProfileResolver _dugoutResolver = new DugoutTacticalProfileResolver();

        public override int InitializationOrder => -20;
        public ManagerHistoricalRuntimeState Runtime { get; private set; }
        public OwnerNewGameFlow NewGameFlow { get; private set; }
        public ManagerPregamePreparation CurrentPregame { get; private set; }
        public ManagerModeMatchResult LastMatch { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool HasActiveRuntime => Runtime != null;
        public BalanceTable Balance => _balance;
        public string SaveFilePath => _saveStore?.FilePath ?? string.Empty;
        public bool HasSave => _saveStore != null && _saveStore.Exists;
        public string LastUnlockedSignatureCardId { get; private set; } = string.Empty;

        /// <summary>표시 Snapshot이 Canonical 선수의 투타 손 정보를 읽도록 Baked Person을 제공한다.</summary>
        public bool TryGetPlayerPerson(string playerPersonId, out PlayerPersonDefinition person)
        {
            HistoricalBakedContent content = _contentProvider?.Load();
            if (content == null)
            {
                person = null;
                return false;
            }
            return content.TryGetPlayerPerson(playerPersonId, out person);
        }

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
            NewGameFlow = null;
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
                EnsureStarterTacticCollection();
                RefreshAvailableTacticCards();
                ApplyStarterLoadout(Runtime.ManagerMode);
                OwnerModeEntryProfiler.Mark("팀 컬러·스타터 로드아웃");

                CurrentPregame = null;
                LastMatch = null;
                LastUnlockedSignatureCardId = string.Empty;
                LastError = string.Empty;
                if (GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
                    GuideManager.Instance.RestoreRepeatState(Runtime.GuideRepeatState);
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
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            if (GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
                runtime.SetGuideRepeatState(GuideManager.Instance.CaptureRepeatState());
            _saveStore.Save(_saveAdapter.CreateSaveData(Runtime));
            LastError = string.Empty;
            NotifyRuntimeChanged();
        }

        public void Load()
        {
            ManagerHistoricalSaveData saveData = _saveStore.Load();
            OwnerModeEntryProfiler.Mark("세이브 파일 읽기·역직렬화");

            Runtime = new ManagerHistoricalLoadService(_saveAdapter).Restore(saveData);
            if (GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
                GuideManager.Instance.RestoreRepeatState(Runtime.GuideRepeatState);
            OwnerModeEntryProfiler.Mark("Runtime 복원");

            ConfigureTeamColors(_contentProvider.Load(), Runtime.PlayerTeamSeasonKey);
            // 전술 수집이 없던 v1~v4에만 지급한다. 이후 schema 추가가 무료 카드 지급을 반복하면 안 된다.
            if (saveData.saveVersion < 5)
                EnsureStarterTacticCollection();
            RefreshAvailableTacticCards();
            CurrentPregame = null;
            LastMatch = null;
            LastError = string.Empty;
            OwnerModeEntryProfiler.Mark("팀 컬러 적용");
            NotifyRuntimeChanged();
        }

        /// <summary>사용자 확인을 받은 구단주 모드 디스크 저장을 삭제한다. 현재 Runtime은 별도의 진행 상태이므로 유지한다.</summary>
        public void DeleteSave()
        {
            _saveStore.Delete();
            LastError = string.Empty;
            NotifyRuntimeChanged();
        }

        /// <summary>타이틀의 구단주 저장 슬롯을 삭제하고 메모리에 남은 진행 세션도 함께 폐기한다.</summary>
        public void DeleteSaveAndDiscardRuntime()
        {
            _saveStore.Delete();
            Runtime = null;
            NewGameFlow = null;
            CurrentPregame = null;
            LastMatch = null;
            LastUnlockedSignatureCardId = string.Empty;
            LastError = string.Empty;
            NotifyRuntimeChanged();
        }

        public ManagerWeeklyAdvanceResult AdvanceWeek()
        {
            var studiesBefore = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < RequireRuntime().PlayerGrowth.StudyProjects.Count; index++)
                studiesBefore.Add(RequireRuntime().PlayerGrowth.StudyProjects[index].CardId);
            ManagerWeeklyAdvanceResult result = _coordinator.AdvanceWeek(RequireRuntime());
            PublishCompletedStudyFacts(studiesBefore);
            InvalidatePregame();
            NotifyRuntimeChanged();
            return result;
        }

        /// <summary>남은 구단 경기가 없을 때 급여·계약·재무를 마감하고 다음 운영 시즌을 연다.</summary>
        public ManagerSeasonAdvanceResult AdvanceSeason()
        {
            EnsureRegularSeasonSimulationIsNotRunning();
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

        /// <summary>보유 카드의 1군 등록과 선택 프리셋 변경을 저장하지 않고 함께 검증한다.</summary>
        public OwnerActiveRosterChangePreview PreviewActiveRosterChange(
            string outgoingCardId,
            string incomingCardId,
            LineupPresetState preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            return BuildActiveRosterChangePreview(
                new[] { new OwnerActiveRosterReplacement(outgoingCardId, incomingCardId) },
                preset);
        }

        /// <summary>기존 저장 전 후보에 1군 교체 한 건을 더 누적하고 전체 후보를 다시 검증한다.</summary>
        public OwnerActiveRosterChangePreview AppendActiveRosterChange(
            OwnerActiveRosterChangePreview preview,
            string outgoingCardId,
            string incomingCardId,
            LineupPresetState preset)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var replacements = new OwnerActiveRosterReplacement[preview.ReplacementCount + 1];
            for (int index = 0; index < preview.ReplacementCount; index++)
                replacements[index] = preview.Replacements[index];
            replacements[replacements.Length - 1] =
                new OwnerActiveRosterReplacement(outgoingCardId, incomingCardId);
            return BuildActiveRosterChangePreview(
                replacements,
                preset,
                preview.ClearedTeamColorCount);
        }

        /// <summary>누적한 1군 교체는 유지하고 역할 배치만 바꾼 후보를 전체 규칙으로 다시 검증한다.</summary>
        public OwnerActiveRosterChangePreview UpdateActiveRosterChangePreset(
            OwnerActiveRosterChangePreview preview,
            LineupPresetState preset)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            return BuildActiveRosterChangePreview(
                preview.Replacements,
                preset,
                preview.ClearedTeamColorCount);
        }

        /// <summary>재검증된 1군 카드 교체와 선택 프리셋을 한 번의 사용자 확정으로 적용한다.</summary>
        public void ApplyActiveRosterChange(OwnerActiveRosterChangePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            OwnerActiveRosterChangePreview validated = BuildActiveRosterChangePreview(
                preview.Replacements,
                preview.Preset);
            if (validated.Validation.Status != LineupPresetValidationStatus.Valid)
                throw new InvalidOperationException("현재 규칙을 통과하지 못한 1군 교체는 저장할 수 없습니다.");

            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            runtime.ApplyPlayerActiveRosterChange(validated.Roster, validated.PlayerStatus);
            runtime.ManagerMode.UpsertLineupPreset(validated.Preset);
            ConfigureTeamColors(_contentProvider.Load(), runtime.PlayerTeamSeasonKey);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        private OwnerActiveRosterChangePreview BuildActiveRosterChangePreview(
            IReadOnlyList<OwnerActiveRosterReplacement> replacements,
            LineupPresetState preset,
            int previouslyClearedTeamColorCount = 0)
        {
            if (replacements == null) throw new ArgumentNullException(nameof(replacements));
            if (replacements.Count == 0)
                throw new ArgumentException("1군 교체 후보가 한 건 이상 필요합니다.", nameof(replacements));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (previouslyClearedTeamColorCount < 0 ||
                previouslyClearedTeamColorCount > LineupPresetState.TeamColorSlotCount)
                throw new ArgumentOutOfRangeException(nameof(previouslyClearedTeamColorCount));

            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            CurrentRosterState candidateRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            TeamSeasonPlayerStatusState candidateStatus =
                runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            for (int replacementIndex = 0; replacementIndex < replacements.Count; replacementIndex++)
            {
                OwnerActiveRosterReplacement replacement = replacements[replacementIndex] ??
                    throw new ArgumentException("1군 교체 후보는 null일 수 없습니다.", nameof(replacements));
                if (!runtime.TryGetOwnedCard(replacement.IncomingCardId, out _))
                    throw new InvalidOperationException("보유하지 않은 카드는 1군에 등록할 수 없습니다.");
                if (!runtime.WorldCardCatalog.TryGetCard(
                        replacement.IncomingCardId,
                        out PlayerCardDefinition incomingCard))
                    throw new InvalidOperationException("등록할 카드 원본을 찾을 수 없습니다.");

                ActiveRosterEntry outgoing = FindRosterEntry(candidateRoster, replacement.OutgoingCardId);
                PlayerSeasonDefinition incomingSeason = runtime.WorldCardCatalog.GetPlayerSeason(incomingCard);
                candidateRoster = OwnerActiveRosterChangeBuilder.ReplaceCard(
                    candidateRoster,
                    outgoing.CardId,
                    incomingCard,
                    incomingSeason);
                candidateStatus = OwnerActiveRosterChangeBuilder.ReplacePlayerStatus(
                    candidateStatus,
                    outgoing.PlayerPersonId,
                    incomingSeason.PlayerPersonId,
                    Balance.ConditionChemistry.NeutralMatchCondition);
            }

            string[] candidateTeamColorIds = ResolveAvailableTeamColorIds(candidateRoster);
            LineupPresetState candidatePreset = OwnerActiveRosterChangeBuilder.ClearUnavailableTeamColors(
                preset,
                candidateTeamColorIds,
                out int newlyClearedTeamColorCount);
            LineupPresetValidationResult validation = _pregameService.ValidateLineupPreset(
                runtime,
                candidatePreset,
                candidateRoster,
                candidateStatus,
                candidateTeamColorIds,
                _availableTacticCardIds);
            return new OwnerActiveRosterChangePreview(
                replacements,
                candidateRoster,
                candidateStatus,
                candidatePreset,
                validation,
                previouslyClearedTeamColorCount + newlyClearedTeamColorCount);
        }

        private static ActiveRosterEntry FindRosterEntry(CurrentRosterState roster, string cardId)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (string.Equals(entry.CardId, cardId, StringComparison.Ordinal)) return entry;
            }
            throw new InvalidOperationException("교체할 1군 카드를 찾을 수 없습니다.");
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

        /// <summary>상세 화면이 발동 전 단계까지 설명할 수 있도록 현재 로스터용 TeamColor 전체 정의를 반환한다.</summary>
        public IReadOnlyList<TeamColorDefinition> GetTeamColorCatalog()
        {
            var result = new TeamColorDefinition[_teamColors.Length];
            Array.Copy(_teamColors, result, _teamColors.Length);
            return result;
        }

        /// <summary>현재 구단주 Save가 실제 경기에서 장착할 수 있는 전술카드 Definition을 반환한다.</summary>
        public IReadOnlyList<TacticCardDefinition> GetAvailableTacticCards()
        {
            var result = new TacticCardDefinition[_availableTacticCardIds.Length];
            int resultIndex = 0;
            for (int idIndex = 0; idIndex < _availableTacticCardIds.Length; idIndex++)
            {
                for (int cardIndex = 0; cardIndex < _tacticCards.Length; cardIndex++)
                {
                    if (!string.Equals(_availableTacticCardIds[idIndex], _tacticCards[cardIndex].CardId,
                            StringComparison.Ordinal))
                        continue;
                    result[resultIndex++] = _tacticCards[cardIndex];
                    break;
                }
            }
            return result;
        }

        /// <summary>두 TeamColor 슬롯을 발동·중첩 규칙으로 검증한 뒤 선택 프리셋에 한 번에 적용한다.</summary>
        public void ConfigureSelectedPresetTeamColors(IReadOnlyList<string> teamColorIds)
        {
            if (teamColorIds == null || teamColorIds.Count != LineupPresetState.TeamColorSlotCount)
                throw new ArgumentException("팀컬러 슬롯은 정확히 두 칸이어야 합니다.", nameof(teamColorIds));

            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            TeamColorDefinition first = ResolveAvailableTeamColor(teamColorIds[0]);
            TeamColorDefinition second = ResolveAvailableTeamColor(teamColorIds[1]);
            new TeamColorResolver().ApplyEquipped(
                runtime.GetRoster(runtime.PlayerTeamSeasonKey),
                runtime.WorldCardCatalog,
                _teamColors,
                first,
                second);
            runtime.ManagerMode.UpsertLineupPreset(CopySelectedPreset(teamColorIds, null));
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        /// <summary>보유 수량과 카드 조합 규칙을 검증한 뒤 선택 프리셋의 기본 작전을 한 번에 적용한다.</summary>
        public void ConfigureSelectedPresetTactics(IReadOnlyList<string> tacticCardIds)
        {
            tacticCardIds ??= Array.Empty<string>();
            if (tacticCardIds.Count > LineupPresetState.MaximumTacticCardCount)
                throw new ArgumentException("작전카드는 최대 두 장까지 장착할 수 있습니다.", nameof(tacticCardIds));

            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            if (!runtime.TacticCollection.CanConsume(tacticCardIds))
                throw new InvalidOperationException("보유 수량이 부족한 작전카드는 장착할 수 없습니다.");
            var definitions = new TacticCardDefinition[tacticCardIds.Count];
            for (int index = 0; index < definitions.Length; index++)
                definitions[index] = ResolveAvailableTacticCard(tacticCardIds[index]);
            _ = new TacticLoadoutState(definitions);
            ScheduledGameState nextGame = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            if (nextGame != null)
            {
                ValidateScheduledTacticInventory(runtime, nextGame, tacticCardIds);
                nextGame.PlanTactics(tacticCardIds);
            }
            runtime.ManagerMode.UpsertLineupPreset(CopySelectedPreset(null, tacticCardIds));
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        /// <summary>앞으로 열릴 최대 10경기 중 한 경기의 작전카드를 보유 수량까지 예약 검증해 저장한다.</summary>
        public void ConfigureScheduledGameTactics(int gameId, IReadOnlyList<string> tacticCardIds)
        {
            tacticCardIds ??= Array.Empty<string>();
            if (tacticCardIds.Count > LineupPresetState.MaximumTacticCardCount)
                throw new ArgumentException("작전카드는 최대 두 장까지 장착할 수 있습니다.", nameof(tacticCardIds));

            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            ScheduledGameState target = FindConfigurableTacticGame(mode.LiveSeason, gameId);
            var definitions = new TacticCardDefinition[tacticCardIds.Count];
            for (int index = 0; index < definitions.Length; index++)
                definitions[index] = ResolveAvailableTacticCard(tacticCardIds[index]);
            _ = new TacticLoadoutState(definitions);
            ValidateScheduledTacticInventory(runtime, target, tacticCardIds);

            target.PlanTactics(tacticCardIds);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        private static ScheduledGameState FindConfigurableTacticGame(ManagerLiveSeasonState season, int gameId)
        {
            int futureIndex = 0;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = season.Schedule.Games[index];
                if (game.IsCompleted || !game.IncludesTeam(season.PlayerTeamId)) continue;
                if (futureIndex >= MaximumTacticPlanningGames) break;
                if (game.GameId == gameId) return game;
                futureIndex++;
            }
            throw new InvalidOperationException("작전카드는 앞으로 열릴 최대 10경기에만 미리 배치할 수 있습니다.");
        }

        private void ValidateScheduledTacticInventory(
            ManagerHistoricalRuntimeState runtime,
            ScheduledGameState target,
            IReadOnlyList<string> candidateIds)
        {
            var reserved = new Dictionary<string, int>(StringComparer.Ordinal);
            ManagerModeRuntimeState mode = runtime.ManagerMode;
            IReadOnlyList<ScheduledGameState> games = mode.LiveSeason.Schedule.Games;
            for (int gameIndex = 0; gameIndex < games.Count; gameIndex++)
            {
                ScheduledGameState game = games[gameIndex];
                if (game.IsCompleted || ReferenceEquals(game, target) ||
                    !game.IncludesTeam(mode.LiveSeason.PlayerTeamId)) continue;
                IReadOnlyList<string> ids = game.HasTacticPlan
                    ? game.PlannedTacticCardIds
                    : ReferenceEquals(game, mode.LiveSeason.NextPlayerGame)
                        ? mode.GetSelectedLineupPreset().DefaultTacticCardIds
                        : Array.Empty<string>();
                AddReservedTactics(reserved, ids);
            }
            AddReservedTactics(reserved, candidateIds);

            foreach (KeyValuePair<string, int> pair in reserved)
                if (pair.Value > runtime.TacticCollection.GetCount(pair.Key))
                    throw new InvalidOperationException(
                        $"{GetTacticDisplayName(pair.Key)} 작전카드는 예정 경기 배치 수보다 보유 수량이 부족합니다.");
        }

        private static void AddReservedTactics(Dictionary<string, int> reserved, IReadOnlyList<string> ids)
        {
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                reserved.TryGetValue(id, out int count);
                reserved[id] = count + 1;
            }
        }

        /// <summary>구단 선택부터 25인 스타터 로스터 확인까지 새 게임 Draft를 시작한다.</summary>
        public OwnerNewGameFlow BeginNewGameFlow()
        {
            LastError = string.Empty;
            NewGameFlow = new OwnerNewGameFlow(
                _contentProvider,
                _worldBuilder,
                _newGameConfiguration.OriginYear,
                _newGameConfiguration.WorldSeed,
                _newGameConfiguration.StarterRosterRule);
            NewGameFlow.GetTeamCandidates();
            return NewGameFlow;
        }

        /// <summary>아직 Runtime을 만들지 않은 새 게임 Draft를 버리고 타이틀로 돌아간다.</summary>
        public void CancelNewGameFlow()
        {
            NewGameFlow = null;
            LastError = string.Empty;
        }

        /// <summary>화면에서 확인한 Draft만 사용해 Runtime을 만들고 첫 배정 튜토리얼을 연다.</summary>
        public bool CompleteNewGameFlow()
        {
            try
            {
                OwnerNewGameFlow flow = NewGameFlow
                    ?? throw new InvalidOperationException("진행 중인 구단주 새 게임 Draft가 없습니다.");
                OwnerStarterRosterResult starter = flow.StarterRoster
                    ?? throw new InvalidOperationException("스타터 로스터를 먼저 확인해야 합니다.");
                OwnerProfileState profile = flow.CreateProfile();
                OwnerNewGameReceipt receipt = flow.CreateReceipt();
                HistoricalBakedContent content = _contentProvider.Load()
                    ?? throw new InvalidOperationException("Historical Content가 없습니다.");

                var service = new ManagerHistoricalNewGameService(
                    _contentProvider,
                    _worldBuilder,
                    _balance);
                Runtime = service.Create(new ManagerHistoricalNewGameRequest(
                    WorldRecordMode.SimulatedHistory,
                    _newGameConfiguration.WorldSeed,
                    _newGameConfiguration.OriginYear,
                    _newGameConfiguration.LeagueInstanceId,
                    flow.SelectedTeamSeasonKey,
                    new ManagerEconomyState(
                        _newGameConfiguration.InitialMoney,
                        _newGameConfiguration.InitialScoutingPoints,
                        _newGameConfiguration.InitialDevelopmentPoints),
                    starter.Roster,
                    profile,
                    receipt));

                RosterValidationResult validation = new ActiveRosterValidator().Validate(starter.Roster);
                if (!validation.IsValid)
                    throw new InvalidOperationException("생성한 25인 스타터 로스터가 ActiveRoster 계약을 위반했습니다.");
                ConfigureTeamColors(content, flow.SelectedTeamSeasonKey);
                EnsureStarterTacticCollection();
                RefreshAvailableTacticCards();
                ApplyStarterLoadout(Runtime.ManagerMode);
                CurrentPregame = null;
                LastMatch = null;
                LastUnlockedSignatureCardId = string.Empty;
                LastError = string.Empty;
                flow.Complete();
                NewGameFlow = null;
                if (GuideManager.Instance != null && GuideManager.Instance.IsAvailable)
                    GuideManager.Instance.RestoreRepeatState(Runtime.GuideRepeatState);
                NotifyRuntimeChanged();
                Save();
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

        /// <summary>첫 진입 배정 가이드의 다음 단계로 이동하고 즉시 저장한다.</summary>
        public void AdvanceOnboarding()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            runtime.Onboarding.Advance();
            Save();
            NotifyRuntimeChanged();
        }

        /// <summary>첫 진입 배정 가이드를 건너뛰고 완료 상태를 저장한다.</summary>
        public void SkipOnboarding()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            runtime.Onboarding.Skip();
            Save();
            NotifyRuntimeChanged();
        }

        /// <summary>상점 연구와 AI 선택이 사용하는 전체 작전 카드 정의를 안정된 저작 순서로 반환한다.</summary>
        public IReadOnlyList<TacticCardDefinition> GetTacticCardCatalog() => _tacticCards;

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
            EnsureRegularSeasonSimulationIsNotRunning();
            ManagerPregamePreparation preparation = CurrentPregame ?? PrepareNextGame();
            if (!preparation.CanStartGame)
                throw new InvalidOperationException("현재 경기 준비 상태로 경기를 시작할 수 없습니다.");
            LastMatch = _matchService.PlayNextGame(RequireRuntime(), eventSink, executionProfile);
            LastUnlockedSignatureCardId = TryUnlockLosingStreakSignature()
                ? LosingStreakSignatureCardId
                : string.Empty;
            RefreshAvailableTacticCards();
            CurrentPregame = null;
            NotifyRuntimeChanged();
            return LastMatch;
        }

        /// <summary>남은 정규시즌을 중계 없이 완주하되 경기별 후처리와 Signature 해금은 그대로 적용한다.</summary>
        public ManagerRegularSeasonCompletionResult CompleteRegularSeason()
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            CurrentPregame = null;
            LastUnlockedSignatureCardId = string.Empty;
            bool hasUnlockedSignature = false;
            try
            {
                ManagerRegularSeasonCompletionResult result = _matchService.CompleteRegularSeason(
                    runtime,
                    matchResult =>
                    {
                        LastMatch = matchResult;
                        if (!TryUnlockLosingStreakSignature()) return;
                        hasUnlockedSignature = true;
                        LastUnlockedSignatureCardId = LosingStreakSignatureCardId;
                    });
                LastUnlockedSignatureCardId = hasUnlockedSignature
                    ? LosingStreakSignatureCardId
                    : string.Empty;
                return result;
            }
            finally
            {
                RefreshAvailableTacticCards();
                CurrentPregame = null;
                NotifyRuntimeChanged();
            }
        }

        public TeamStaffEffectProfile GetStaffEffects()
        {
            return _coordinator.ResolvePlayerStaffEffects(RequireRuntime().ManagerMode);
        }

        /// <summary>덕아웃 화면과 경기 입력이 공유하는 가상 감독·수석코치 정의를 반환한다.</summary>
        public DugoutStaffCatalog GetDugoutStaffCatalog() => _dugoutCatalog;

        /// <summary>현재 선택을 경기 시작 시 동결될 실제 감독 판단값으로 합성한다.</summary>
        public ManagerTacticalProfile GetEffectiveManagerProfile()
        {
            return _dugoutResolver.Resolve(RequireRuntime().ManagerMode.Dugout, _dugoutCatalog);
        }

        /// <summary>감독·수석코치·여섯 방침을 원자적으로 적용하고 다음 경기 준비를 무효화한다.</summary>
        public void ConfigureDugout(
            string managerId,
            string headCoachId,
            DugoutPolicySettings policy)
        {
            RequireRuntime().ManagerMode.Dugout.Configure(managerId, headCoachId, policy, _dugoutCatalog);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        public CardTrainingResult TrainOwnedCard(string cardId, CardTrainingProgramDefinition program)
        {
            CardTrainingResult result = _coordinator.TrainOwnedCard(RequireRuntime(), cardId, program);
            InvalidatePregame();
            NotifyRuntimeChanged();
            PublishGrowthFact("CardTrainingCompleted", cardId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ability"] = result.Ability.ToString(),
                    ["gained"] = result.GainedPoints.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return result;
        }

        public CardTrainingResult TrainOwnedCard(string cardId, string programId) =>
            TrainOwnedCard(cardId, _balance.OwnerCardGrowth.GetTrainingProgram(programId));

        public CardTrainingPreview PreviewOwnedCardTraining(string cardId, string programId) =>
            _coordinator.PreviewOwnedCardTraining(
                RequireRuntime(), cardId, _balance.OwnerCardGrowth.GetTrainingProgram(programId));

        public IReadOnlyList<CardTrainingProgramDefinition> GetCardTrainingPrograms() =>
            _balance.OwnerCardGrowth.TrainingPrograms;

        public IReadOnlyList<CardStudyProgramDefinition> GetCardStudyPrograms() =>
            _balance.OwnerCardGrowth.StudyPrograms;

        public bool HasAvailableCardStudySlot()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            int capacity = _balance.OwnerCardGrowth.GetStudyCapacity(
                runtime.ManagerMode.ClubOperation.GetFacility(FacilityType.TrainingCenter).Level);
            if (runtime.PlayerGrowth.StudyProjects.Count >= capacity) return false;
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            int season = runtime.ManagerMode.LiveSeason.SeasonNumber;
            for (int cardIndex = 0; cardIndex < runtime.OwnedCards.Count; cardIndex++)
            {
                OwnedPlayerCardState card = runtime.OwnedCards[cardIndex];
                if (card.LastStudySeason == season) continue;
                bool registered = false;
                for (int rosterIndex = 0; rosterIndex < roster.Entries.Count; rosterIndex++)
                    if (string.Equals(roster.Entries[rosterIndex].CardId, card.CardId, StringComparison.Ordinal)) registered = true;
                if (!registered) return true;
            }
            return false;
        }

        public void StartOwnedCardStudy(string cardId, string programId)
        {
            CardStudyProgramDefinition program = _balance.OwnerCardGrowth.GetStudyProgram(programId);
            _coordinator.StartOwnedCardStudy(RequireRuntime(), cardId, program);
            PublishGrowthFact("CardStudyStarted", cardId,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["program"] = program.DisplayName });
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        public void PlaceOwnedCardSkillBlock(
            string cardId, int instanceId, int originX, int originY, int rotationQuarterTurns)
        {
            string[] traitsBefore = GetActiveOwnerCardTraits(cardId);
            _coordinator.PlaceOwnedCardSkillBlock(
                RequireRuntime(), cardId, instanceId, originX, originY, rotationQuarterTurns);
            PublishGrowthFact("SkillBlockPlaced", cardId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                    { ["instanceId"] = instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            PublishNewTraitFacts(cardId, traitsBefore);
            InvalidatePregame();
            NotifyRuntimeChanged();
        }

        public bool AutoPlaceOwnedCardSkillBlock(string cardId, int instanceId)
        {
            string[] traitsBefore = GetActiveOwnerCardTraits(cardId);
            bool placed = _coordinator.AutoPlaceOwnedCardSkillBlock(RequireRuntime(), cardId, instanceId);
            if (!placed) return false;
            PublishGrowthFact("SkillBlockPlaced", cardId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                    { ["instanceId"] = instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            PublishNewTraitFacts(cardId, traitsBefore);
            InvalidatePregame();
            NotifyRuntimeChanged();
            return true;
        }

        public bool RemoveOwnedCardSkillBlock(string cardId, int instanceId)
        {
            bool removed = _coordinator.RemoveOwnedCardSkillBlock(RequireRuntime(), cardId, instanceId);
            if (!removed) return false;
            InvalidatePregame();
            NotifyRuntimeChanged();
            return true;
        }

        public bool AutoPlaceFirstAvailableSkillBlock(string cardId)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            for (int index = 0; index < runtime.PlayerGrowth.Inventory.Blocks.Count; index++)
            {
                int instanceId = runtime.PlayerGrowth.Inventory.Blocks[index].InstanceId;
                try
                {
                    if (AutoPlaceOwnedCardSkillBlock(cardId, instanceId)) return true;
                }
                catch (InvalidOperationException)
                {
                }
            }
            return false;
        }

        public bool RemoveLastOwnedCardSkillBlock(string cardId)
        {
            if (!RequireRuntime().TryGetOwnedCard(cardId, out OwnedPlayerCardState card) ||
                card.SkillBoard.Placements.Count == 0) return false;
            return RemoveOwnedCardSkillBlock(
                cardId, card.SkillBoard.Placements[card.SkillBoard.Placements.Count - 1].Instance.InstanceId);
        }

        /// <summary>구단주 저장 상태를 사용하는 상점 서비스를 만든다.</summary>
        public ShopService CreateShopService() => OwnerShopComposer.Create(this);

        /// <summary>스카우트·전술 연구를 Game 계층 단일 Command로 실행한다.</summary>
        public ShopPurchaseResult PurchaseShopProduct(string productId)
        {
            ShopService shop = CreateShopService();
            shop.Catalog.TryGetProduct(productId, out ShopProductDefinition product);
            ShopPurchaseResult result = shop.Purchase(productId);
            if (result.IsSuccess)
            {
                PublishSkillBlockAcquisitionFacts(product, result);
                RefreshAvailableTacticCards();
                InvalidatePregame();
                NotifyRuntimeChanged();
            }
            return result;
        }

        public CardEnhancementResult EnhanceOwnedCard(string cardId)
        {
            CardEnhancementResult result = _coordinator.EnhanceOwnedCard(RequireRuntime(), cardId);
            if (result == CardEnhancementResult.Enhanced)
            {
                PublishGrowthFact("CardEnhanced", cardId, null);
                InvalidatePregame();
                NotifyRuntimeChanged();
            }
            return result;
        }

        public CardEnhancementPreview PreviewOwnedCardEnhancement(string cardId) =>
            _coordinator.PreviewOwnedCardEnhancement(RequireRuntime(), cardId);

        private void PublishSkillBlockAcquisitionFacts(ShopProductDefinition product, ShopPurchaseResult result)
        {
            if (product == null || product.Kind != ShopProductKind.SkillBlockPack) return;
            if (result.Items == null || result.Items.Length == 0) return;
            // 개별 획득 내역은 결과 화면에서 보여 준다. 가이드는 사용법만 게임당 한 번 안내한다.
            PublishGrowthFact("SkillBlockAcquired", string.Empty, null);
        }

        private void PublishCompletedStudyFacts(HashSet<string> studiesBefore)
        {
            foreach (string cardId in studiesBefore)
            {
                bool remains = false;
                for (int index = 0; index < Runtime.PlayerGrowth.StudyProjects.Count; index++)
                    if (string.Equals(Runtime.PlayerGrowth.StudyProjects[index].CardId, cardId, StringComparison.Ordinal))
                        remains = true;
                if (!remains) PublishGrowthFact("CardStudyCompleted", cardId, null);
            }
        }

        private void PublishGrowthFact(string factType, string cardId, Dictionary<string, string> payload)
        {
            GuideManager guide = GuideManager.Instance;
            if (guide == null || !guide.IsAvailable) return;
            payload ??= new Dictionary<string, string>(StringComparer.Ordinal);
            payload["cardId"] = cardId ?? string.Empty;
            guide.PublishOwnerFact(
                factType,
                $"owner-growth:{factType}:{Runtime.ManagerMode.LiveSeason.SeasonNumber}:{cardId}:{Runtime.ManagerMode.LiveSeason.CurrentWeekIndex}",
                payload);
        }

        private string[] GetActiveOwnerCardTraits(string cardId)
        {
            if (!RequireRuntime().TryGetOwnedCard(cardId, out OwnedPlayerCardState card)) return Array.Empty<string>();
            return new OwnerCardAbilityResolver(_balance.Growth).ResolveActiveTraitIds(card);
        }

        private void PublishNewTraitFacts(string cardId, string[] before)
        {
            string[] after = GetActiveOwnerCardTraits(cardId);
            for (int index = 0; index < after.Length; index++)
            {
                bool existed = false;
                for (int previous = 0; previous < before.Length; previous++)
                    if (string.Equals(before[previous], after[index], StringComparison.Ordinal)) existed = true;
                if (!existed)
                    PublishGrowthFact("SkillBoardTraitActivated", cardId,
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["traitId"] = after[index] });
            }
        }

        public int SellOwnedCardDuplicates(string cardId, int count = 1)
        {
            int earnedSp = _coordinator.SellOwnedCardDuplicates(RequireRuntime(), cardId, count);
            InvalidatePregame();
            NotifyRuntimeChanged();
            return earnedSp;
        }

        public CardSalePreview PreviewOwnedCardSale(string cardId, int count = 1) =>
            _coordinator.PreviewOwnedCardSale(RequireRuntime(), cardId, count);

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
            return BuildRosterStatus(runtime.GetRoster(runtime.PlayerTeamSeasonKey));
        }

        /// <summary>저장 전 후보 1군을 실제 Resolver로 평가해 Preview에 제공한다.</summary>
        public OwnerModeRosterStatus BuildRosterStatus(CurrentRosterState roster)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            if (!string.Equals(roster.TeamSeasonKey, runtime.PlayerTeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("플레이어 구단 로스터만 평가할 수 있습니다.", nameof(roster));
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
                new RosterStrengthResolver().Resolve(roster, runtime.WorldCardCatalog),
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
            // 합성 참가팀은 Franchise TeamSeason 정의가 없으므로 Key에서 직접 이름을 만든다.
            if (SpecialCompositeTeamDefinition.TryCreateDisplayName(teamSeasonKey, out string compositeName))
                return compositeName;

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
            _availableTacticCardIds = Array.Empty<string>();
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

        private TeamColorDefinition ResolveAvailableTeamColor(string teamColorId)
        {
            if (string.IsNullOrWhiteSpace(teamColorId)) return null;
            for (int index = 0; index < _availableTeamColorIds.Length; index++)
            {
                if (!string.Equals(_availableTeamColorIds[index], teamColorId, StringComparison.Ordinal)) continue;
                for (int definitionIndex = 0; definitionIndex < _teamColors.Length; definitionIndex++)
                    if (string.Equals(_teamColors[definitionIndex].TeamColorId, teamColorId, StringComparison.Ordinal))
                        return _teamColors[definitionIndex];
            }
            throw new InvalidOperationException("현재 25인 로스터에서 발동하지 않은 팀컬러는 장착할 수 없습니다.");
        }

        private TacticCardDefinition ResolveAvailableTacticCard(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("작전카드 ID는 비어 있을 수 없습니다.", nameof(cardId));
            for (int index = 0; index < _availableTacticCardIds.Length; index++)
            {
                if (!string.Equals(_availableTacticCardIds[index], cardId, StringComparison.Ordinal)) continue;
                for (int definitionIndex = 0; definitionIndex < _tacticCards.Length; definitionIndex++)
                    if (string.Equals(_tacticCards[definitionIndex].CardId, cardId, StringComparison.Ordinal))
                        return _tacticCards[definitionIndex];
            }
            throw new InvalidOperationException("보유하지 않은 작전카드는 장착할 수 없습니다.");
        }

        private LineupPresetState CopySelectedPreset(
            IReadOnlyList<string> teamColorIds,
            IReadOnlyList<string> tacticCardIds)
        {
            LineupPresetState source = RequireRuntime().ManagerMode.GetSelectedLineupPreset();
            return new LineupPresetState(
                source.PresetId,
                source.Name,
                source.StartingLineupSlots,
                source.BattingOrderCardIds,
                source.BenchPriorityCardIds,
                source.StarterRotationCardIds,
                source.BullpenAssignmentCardIds,
                source.SetupPitcherCardId,
                source.CloserPitcherCardId,
                teamColorIds ?? source.TeamColorIds,
                tacticCardIds ?? source.DefaultTacticCardIds);
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
                Array.Empty<string>()));
        }

        private void EnsureStarterTacticCollection()
        {
            if (_tacticCards.Length < LineupPresetState.MaximumTacticCardCount)
                throw new InvalidOperationException("구단주 모드 Starter Tactic 두 장이 필요합니다.");
            for (int index = 0; index < LineupPresetState.MaximumTacticCardCount; index++)
                Runtime.TacticCollection.Acquire(_tacticCards[index].CardId);
        }

        /// <summary>3연패를 직접 끊은 순간에만 업적 전용 Signature 전술을 한 번 지급한다.</summary>
        private bool TryUnlockLosingStreakSignature()
        {
            if (Runtime.TacticCollection.Contains(LosingStreakSignatureCardId))
                return false;

            IReadOnlyList<ScheduledGameState> games = Runtime.ManagerMode.LiveSeason.Schedule.Games;
            var latestResults = new bool[4];
            int resultCount = 0;
            int playerTeamId = Runtime.ManagerMode.LiveSeason.PlayerTeamId;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (!game.IsCompleted || !game.IncludesTeam(playerTeamId))
                    continue;
                bool isWin = game.AwayTeamId == playerTeamId
                    ? game.AwayRuns > game.HomeRuns
                    : game.HomeRuns > game.AwayRuns;
                if (resultCount < latestResults.Length)
                    latestResults[resultCount++] = isWin;
                else
                {
                    latestResults[0] = latestResults[1];
                    latestResults[1] = latestResults[2];
                    latestResults[2] = latestResults[3];
                    latestResults[3] = isWin;
                }
            }
            if (resultCount < latestResults.Length || latestResults[0] || latestResults[1] || latestResults[2] || !latestResults[3])
                return false;
            Runtime.TacticCollection.Acquire(LosingStreakSignatureCardId);
            return true;
        }

        private void RefreshAvailableTacticCards()
        {
            if (Runtime == null)
            {
                _availableTacticCardIds = Array.Empty<string>();
                return;
            }
            var ids = new List<string>();
            for (int index = 0; index < _tacticCards.Length; index++)
                if (Runtime.TacticCollection.Contains(_tacticCards[index].CardId))
                    ids.Add(_tacticCards[index].CardId);
            ids.Sort(StringComparer.Ordinal);
            _availableTacticCardIds = ids.ToArray();
        }

        private void ConfigureTeamColors(HistoricalBakedContent content, string teamSeasonKey)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!content.TryGetTeamSeason(teamSeasonKey, out TeamSeasonDefinition team))
                throw new InvalidOperationException("플레이어 구단의 TeamSeasonDefinition이 없습니다.");

            CurrentRosterState roster = Runtime.GetRoster(teamSeasonKey);
            IReadOnlyList<TeamColorRosterCard> rosterCards = TeamColorResolver.CreateRosterCards(
                roster,
                Runtime.WorldCardCatalog);
            IReadOnlyList<TeamColorDefinition> definitions = InitialTeamColorDefinitionFactory.CreateForRoster(
                rosterCards,
                _balance.TeamColor);
            _teamColors = new TeamColorDefinition[definitions.Count];
            for (int index = 0; index < _teamColors.Length; index++)
                _teamColors[index] = definitions[index];

            IReadOnlyList<TeamColorCandidate> candidates = new TeamColorResolver().Resolve(rosterCards, _teamColors);
            _availableTeamColorIds = new string[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
                _availableTeamColorIds[index] = candidates[index].Definition.TeamColorId;

            _matchService = new ManagerModeMatchService(
                _contentProvider,
                _balance,
                teamColors: _teamColors,
                tacticCards: _tacticCards);
        }

        private string[] ResolveAvailableTeamColorIds(CurrentRosterState roster)
        {
            IReadOnlyList<TeamColorRosterCard> rosterCards = TeamColorResolver.CreateRosterCards(
                roster,
                Runtime.WorldCardCatalog);
            IReadOnlyList<TeamColorCandidate> candidates = new TeamColorResolver().Resolve(rosterCards, _teamColors);
            var ids = new string[candidates.Count];
            for (int index = 0; index < ids.Length; index++)
                ids[index] = candidates[index].Definition.TeamColorId;
            return ids;
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
            int selectedCount = 0;
            for (int index = 0; index < candidates.Count && selectedCount < selected.Length; index++)
            {
                TeamColorDefinition candidate = candidates[index];
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

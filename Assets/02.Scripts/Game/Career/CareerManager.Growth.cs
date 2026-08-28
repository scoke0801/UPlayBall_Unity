using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 성장판·스킬 상점·오프시즌 활동 진행을 CareerState에 연결한다.
    /// </summary>
    public sealed partial class CareerManager
    {
        private const ulong SkillPullStream = 0x534B494C4C50554CUL;

        private CareerState _growthBoundCareer;
        private SkillBoardService _skillBoardService;
        private SkillGachaService _skillGachaService;
        private CareerGrowthService _careerGrowthService;
        private GrowthPreviewCalculator _growthPreviewCalculator;
        private string _selectedGrowthProgramId = string.Empty;
        private TrainingIntensity _selectedTrainingIntensity = TrainingIntensity.Standard;
        private SkillBlockInstance[] _lastPulledBlocks = Array.Empty<SkillBlockInstance>();

        public CareerGrowthView GrowthDashboard => BuildGrowthDashboard();

        /// <summary>
        /// 확정된 시즌 기록을 자연 성장·노쇠·수입으로 결산하고 선택 가능한 오프시즌을 연다.
        /// </summary>
        public bool SettleSeasonAndBeginOffseason()
        {
            if (!TryGetGrowthRuntime(out _))
                return false;

            SeasonState season = CurrentCareer.CurrentLeague.CurrentSeason;
            if (season.Phase != SeasonPhase.SeasonReview)
                return FailGrowth("시즌 결산 단계에서만 오프시즌을 시작할 수 있습니다.");

            try
            {
                var usageBuilder = new CareerSeasonUsageSummaryBuilder(
                    _balance.PlayerEvaluation,
                    _balance.CareerSeason.StartingRotationSize);
                SeasonUsageSummary usage = usageBuilder.Build(
                    CurrentCareer.MyPlayer.PrimaryPosition,
                    season.PlayerStatistics);
                _careerGrowthService.SettleSeasonAndBeginOffseason(usage);
                _selectedGrowthProgramId = string.Empty;
                _selectedTrainingIntensity = TrainingIntensity.Standard;
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 오프시즌 액션 카드 선택을 저장하고 주차 진행 전 예상 변화를 갱신한다.
        /// </summary>
        public bool SelectGrowthProgram(string programId)
        {
            return SelectGrowthProgram(programId, TrainingIntensity.Standard);
        }

        /// <summary>
        /// 개인 훈련의 강도까지 포함해 확정 전 선택을 저장한다.
        /// </summary>
        public bool SelectGrowthProgram(string programId, TrainingIntensity intensity)
        {
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return false;
            if (!IsOffseason())
                return FailGrowth("오프시즌에만 성장 활동을 선택할 수 있습니다.");

            TrainingProgramDefinition baseProgram = _balance.Growth.FindProgram(programId);
            if (baseProgram == null || !baseProgram.CanUse(growth.PlayerType))
                return FailGrowth("선택할 수 없는 성장 프로그램입니다.");
            if (!baseProgram.SupportsIntensity && intensity != TrainingIntensity.Standard)
                return FailGrowth("개인 훈련만 강도를 조절할 수 있습니다.");
            TrainingProgramDefinition program = _balance.Growth.GetProgram(programId, intensity);
            if (growth.Condition < program.MinimumCondition)
                return FailGrowth("현재 컨디션으로 시작할 수 없는 프로그램입니다.");
            if (program.IsStudy &&
                (CurrentCareer.CurrentOffseason.StudyUsed ||
                 CurrentCareer.MyPlayer.StudyState.StudyUsedThisOffseason))
            {
                return FailGrowth("유학은 오프시즌당 한 번만 가능합니다.");
            }

            PlannedOffseasonActivity active = FindInProgressActivity(CurrentCareer.CurrentOffseason);
            if (active != null &&
                (!string.Equals(active.ProgramId, programId, StringComparison.Ordinal) ||
                 active.Intensity != intensity))
            {
                return FailGrowth("진행 중인 활동을 먼저 마쳐야 합니다.");
            }

            _selectedGrowthProgramId = programId;
            _selectedTrainingIntensity = intensity;
            return CompleteGrowthCommand();
        }

        /// <summary>
        /// 선택한 활동을 기존 계획의 마지막 주 다음에 배치한다.
        /// </summary>
        public bool AddGrowthProgramToPlan(
            string programId,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            try
            {
                PlanGrowthActivityCore(programId, intensity);
                _selectedGrowthProgramId = string.Empty;
                _selectedTrainingIntensity = TrainingIntensity.Standard;
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 계획에 담긴 모든 활동을 타임라인 순서대로 실행한다.
        /// </summary>
        public bool ExecuteGrowthPlan()
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            if (!IsOffseason())
                return FailGrowth("오프시즌에만 성장 계획을 실행할 수 있습니다.");

            try
            {
                OffseasonState offseason = CurrentCareer.CurrentOffseason;
                PlannedOffseasonActivity active = FindInProgressActivity(offseason);
                int plannedCount = CountPlannedActivities(offseason);
                if (active == null && plannedCount == 0)
                    return FailGrowth("성장 계획에 담긴 활동이 없습니다.");
                if (active != null)
                    _careerGrowthService.CompleteActivity(active.ActivityId);
                _careerGrowthService.ExecutePlannedActivities();
                _selectedGrowthProgramId = string.Empty;
                _selectedTrainingIntensity = TrainingIntensity.Standard;
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 현재 활동을 계획의 마지막에 추가한 뒤 누적 계획 전체를 실행한다.
        /// </summary>
        public bool AddAndExecuteGrowthPlan(
            string programId,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            try
            {
                PlanGrowthActivityCore(programId, intensity);
                _careerGrowthService.ExecutePlannedActivities();
                _selectedGrowthProgramId = string.Empty;
                _selectedTrainingIntensity = TrainingIntensity.Standard;
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 계획에서 아직 시작하지 않은 활동 하나를 제거한다.
        /// </summary>
        public bool CancelGrowthPlanActivity(int activityId)
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            try
            {
                _careerGrowthService.CancelActivity(activityId);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 선택한 활동 또는 이미 담긴 계획 전체를 한 번에 진행한다.
        /// </summary>
        public bool ExecuteSelectedGrowthProgram()
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            if (!IsOffseason())
                return FailGrowth("오프시즌에만 성장 활동을 진행할 수 있습니다.");

            try
            {
                OffseasonState offseason = CurrentCareer.CurrentOffseason;
                PlannedOffseasonActivity activity = FindInProgressActivity(offseason);
                bool completedActive = false;
                if (activity != null)
                {
                    _careerGrowthService.CompleteActivity(activity.ActivityId);
                    completedActive = true;
                }
                else if (!string.IsNullOrEmpty(_selectedGrowthProgramId))
                {
                    PlanGrowthActivityCore(
                        _selectedGrowthProgramId,
                        _selectedTrainingIntensity);
                }
                if (CountPlannedActivities(offseason) == 0)
                {
                    if (!completedActive)
                        return FailGrowth("먼저 진행할 성장 활동을 선택해 주세요.");
                    _selectedGrowthProgramId = string.Empty;
                    _selectedTrainingIntensity = TrainingIntensity.Standard;
                    return CompleteGrowthCommand();
                }
                _careerGrowthService.ExecutePlannedActivities();

                _selectedGrowthProgramId = string.Empty;
                _selectedTrainingIntensity = TrainingIntensity.Standard;
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 공개된 계통을 선택해 커리어 Seed와 누적 뽑기 순번으로 블록 하나를 구매한다.
        /// </summary>
        public bool PurchaseSkillBlock(SkillBlockCategory category)
        {
            return PurchaseSkillBlock(category, SkillGachaPurchaseTier.Normal);
        }

        /// <summary>
        /// 선택한 가격 등급의 공개 확률로 스킬 블록 하나를 구매한다.
        /// </summary>
        public bool PurchaseSkillBlock(
            SkillBlockCategory category,
            SkillGachaPurchaseTier tier)
        {
            return PurchaseSkillBlocks(category, tier, 1);
        }

        /// <summary>
        /// 선택 등급의 최소 보장과 계통 필터를 적용해 한 개 또는 다섯 개를 구매한다.
        /// </summary>
        public bool PurchaseSkillBlocks(
            SkillBlockCategory? category,
            SkillGachaPurchaseTier tier,
            int count)
        {
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return false;
            if (count != 1 && count != 5)
                return FailGrowth("블록은 1회 또는 5회 단위로만 뽑을 수 있습니다.");
            if (category.HasValue && !IsCategoryAvailable(growth.PlayerType, category.Value))
                return FailGrowth("선수 유형에 맞지 않는 스킬 블록 계통입니다.");
            string unavailableReason = GetGachaUnavailableReason(tier);
            if (!string.IsNullOrEmpty(unavailableReason))
                return FailGrowth(unavailableReason);

            try
            {
                SkillBoardState board = CurrentCareer.MyPlayer.SkillBoardState;
                ulong stream = SkillPullStream ^
                               ((ulong)(uint)CurrentCareer.CurrentLeague.CurrentSeason.SeasonId << 32) ^
                               (uint)(board.TotalPullCount + 1);
                ulong seed = DeterministicSeed.Derive(CurrentCareer.CurrentLeague.RandomSeed, stream);
                var random = new Pcg32Random(seed);
                SkillBlockCategory[] categories = category.HasValue
                    ? new[] { category.Value }
                    : GetAvailableCategories(growth.PlayerType);
                int year = CurrentCareer.CurrentLeague.CurrentSeason.Year;
                if (count == 1)
                {
                    int categoryIndex = categories.Length == 1
                        ? 0
                        : Math.Min((int)(random.NextDouble() * categories.Length), categories.Length - 1);
                    SkillBlockInstance result = _skillGachaService.PullSingle(
                        CurrentCareer.Economy,
                        board,
                        categories[categoryIndex],
                        tier,
                        year,
                        random);
                    _lastPulledBlocks = new[] { result };
                }
                else
                {
                    _lastPulledBlocks = _skillGachaService.PullBundle(
                        CurrentCareer.Economy,
                        board,
                        categories,
                        tier,
                        year,
                        random);
                }
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        public bool SetSkillBlockLocked(int instanceId, bool isLocked)
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            try
            {
                CurrentCareer.MyPlayer.SkillBoardState.SetBlockLocked(instanceId, isLocked);
                return CompleteGrowthCommand();
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// Presentation의 임시 보드 전체를 검증하고 하나의 확정 명령으로 적용한다.
        /// </summary>
        public bool ApplySkillBoardLayout(GrowthBoardLayoutPlacement[] layout)
        {
            if (!RequireBoardEditing())
                return false;
            if (layout == null)
                return FailGrowth("적용할 성장 보드 정보가 없습니다.");
            try
            {
                SkillBoardState board = CurrentCareer.MyPlayer.SkillBoardState;
                var placements = new PlacedSkillBlock[layout.Length];
                for (int index = 0; index < layout.Length; index++)
                {
                    SkillBlockInstance instance = FindSkillBlockInstance(board, layout[index].InstanceId);
                    if (instance.InstanceId == 0)
                        return FailGrowth("보드에 배치할 블록을 찾을 수 없습니다.");
                    placements[index] = new PlacedSkillBlock(
                        instance,
                        layout[index].OriginX,
                        layout[index].OriginY,
                        layout[index].RotationQuarterTurns);
                }

                _skillBoardService.ApplyLayout(
                    board,
                    placements,
                    CurrentCareer.Economy,
                    CurrentCareer.CurrentOffseason,
                    CurrentCareer.CurrentLeague.CurrentSeason.Year,
                    _balance.Growth.SkillBoardRedesignCost);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        public bool PlaceSkillBlock(int instanceId, int x, int y, int rotationQuarterTurns)
        {
            if (!RequireBoardEditing())
                return false;
            try
            {
                _skillBoardService.PlaceBlock(
                    CurrentCareer.MyPlayer.SkillBoardState,
                    instanceId,
                    x,
                    y,
                    rotationQuarterTurns);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 성장판을 변경하지 않고 선택 블록의 배치 미리보기를 만든다.
        /// </summary>
        public GrowthBlockPlacementPreviewView GetSkillBlockPlacementPreview(
            int instanceId,
            int x,
            int y,
            int rotationQuarterTurns)
        {
            if (!TryGetGrowthRuntime(out _) || !IsOffseason())
                return new GrowthBlockPlacementPreviewView(Array.Empty<BoardCell>(), false);

            SkillBlockPlacementPreview preview = _skillBoardService.GetPlacementPreview(
                CurrentCareer.MyPlayer.SkillBoardState,
                instanceId,
                x,
                y,
                rotationQuarterTurns);
            return new GrowthBlockPlacementPreviewView(preview.Cells, preview.CanPlace);
        }

        public bool RemoveSkillBlock(int instanceId)
        {
            if (!RequireBoardEditing())
                return false;
            try
            {
                _skillBoardService.RemoveBlock(CurrentCareer.MyPlayer.SkillBoardState, instanceId);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        public bool SellOwnedSkillBlock(int instanceId)
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            try
            {
                _skillGachaService.SellOwnedBlock(
                    CurrentCareer.Economy,
                    CurrentCareer.MyPlayer.SkillBoardState,
                    instanceId,
                    CurrentCareer.CurrentLeague.CurrentSeason.Year);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        public bool RedesignSkillBoard()
        {
            if (!RequireBoardEditing())
                return false;
            try
            {
                int year = CurrentCareer.CurrentLeague.CurrentSeason.Year;
                _skillBoardService.Redesign(
                    CurrentCareer.MyPlayer.SkillBoardState,
                    CurrentCareer.Economy,
                    CurrentCareer.CurrentOffseason,
                    year,
                    _balance.Growth.SkillBoardRedesignCost);
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
            {
                return FailGrowth(exception.Message);
            }
        }

        /// <summary>
        /// 장착 보너스를 포함한 현재 선수 입력을 감독 평가와 경기 시뮬레이션에 제공한다.
        /// </summary>
        internal Player BuildStablePlayer()
        {
            EnsureGrowthRuntime();
            return CurrentCareer.MyPlayer.ToPlayer(_skillBoardService);
        }

        private CareerGrowthView BuildGrowthDashboard()
        {
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return null;

            SkillBoardState board = CurrentCareer.MyPlayer.SkillBoardState;
            SkillBoardDefinition boardDefinition = _balance.Growth.SkillBoard;
            OffseasonState offseason = CurrentCareer.CurrentOffseason;
            PlannedOffseasonActivity active = FindInProgressActivity(offseason);
            int remainingWeeks = offseason == null || offseason.IsCompleted
                ? 0
                : offseason.TotalWeeks - offseason.CurrentWeek + 1;
            GrowthPlanItemView[] plannedActivities = BuildPlannedActivityViews(offseason);
            int plannedWeeks = GetPlannedScheduleWeeks(offseason);
            long plannedCost = GetPlannedActivityCost(offseason);
            int projectedConditionAfterPlan = GetProjectedConditionAfterPlan(
                offseason,
                growth.Condition);

            var view = new CareerGrowthView
            {
                PlayerType = growth.PlayerType,
                BaseAbilities = growth.BaseAbilities.ToArray(),
                StableAbilities = BuildStableAbilityValues(growth, board),
                BoardBonuses = BuildBoardBonuses(board),
                BoardWidth = boardDefinition.Width,
                BoardHeight = boardDefinition.Height,
                BoardCells = BuildBoardCells(board, boardDefinition),
                OwnedBlocks = BuildSkillBlockViews(board.OwnedBlocks),
                PlacedBlocks = BuildPlacedSkillBlockViews(board.PlacedBlocks),
                AppliedLayout = BuildAppliedLayout(board.PlacedBlocks),
                LastPulledBlocks = BuildSkillBlockViews(_lastPulledBlocks),
                ShopCategories = BuildShopCategories(growth.PlayerType, board),
                GachaOffers = BuildGachaOffers(),
                GachaPool = BuildGachaPool(growth.PlayerType),
                Programs = BuildPrograms(growth, remainingWeeks),
                PlannedActivities = plannedActivities,
                RecentGrowth = BuildRecentGrowth(growth),
                IsOffseason = IsOffseason(),
                CanEditBoard = IsOffseason(),
                CanRedesignBoard = IsOffseason() &&
                                   !offseason.BoardRedesignUsed &&
                                   board.PlacedBlocks.Count > 0 &&
                                   CurrentCareer.AvailableMoney >= _balance.Growth.SkillBoardRedesignCost,
                IsBoardRedesignUsed = offseason?.BoardRedesignUsed ?? false,
                IsActivityInProgress = active != null,
                CanCompleteOffseason = IsOffseason() &&
                                       active == null &&
                                       plannedActivities.Length == 0,
                ActiveProgramId = active?.ProgramId ?? string.Empty,
                SelectedProgramId = active?.ProgramId ?? _selectedGrowthProgramId,
                SelectedTrainingIntensity = active?.Intensity ?? _selectedTrainingIntensity,
                CurrentWeek = offseason?.CurrentWeek ?? 0,
                TotalWeeks = offseason?.TotalWeeks ?? _balance.Growth.OffseasonWeeks,
                RemainingWeeks = remainingWeeks,
                PlannedWeeks = plannedWeeks,
                PlannedCost = plannedCost,
                ProjectedConditionAfterPlan = projectedConditionAfterPlan,
                ActiveActivityEndWeek = active?.EndWeek ?? 0,
                SinglePullPrice = _balance.Growth.SkillGacha.SinglePrice,
                BundlePullPrice = _balance.Growth.SkillGacha.GetFivePullPrice(
                    SkillGachaPurchaseTier.Normal),
                BoardRedesignCost = _balance.Growth.SkillBoardRedesignCost,
                ElitePityCount = board.PityEliteCount,
                UniquePityCount = board.PityUniqueCount,
                LegendaryPityCount = board.PityLegendaryCount,
                ElitePityTarget = _balance.Growth.SkillGacha.ElitePity,
                UniquePityTarget = _balance.Growth.SkillGacha.UniquePity,
                LegendaryPityTarget = _balance.Growth.SkillGacha.LegendaryPity
            };
            return view;
        }

        private int[] BuildStableAbilityValues(PlayerGrowthState growth, SkillBoardState board)
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = _skillBoardService.GetStableAbility(
                    board,
                    growth,
                    (PlayerAbility)index);
            }
            return values;
        }

        private int[] BuildBoardBonuses(SkillBoardState board)
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++)
                values[index] = _skillBoardService.GetAbilityBonus(board, (PlayerAbility)index);
            return values;
        }

        private GrowthBoardCellView[] BuildBoardCells(
            SkillBoardState board,
            SkillBoardDefinition definition)
        {
            var cells = new GrowthBoardCellView[definition.Width * definition.Height];
            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    cells[y * definition.Width + x] = new GrowthBoardCellView(
                        x,
                        y,
                        IsTraitSocket(definition, x, y),
                        0,
                        default,
                        default);
                }
            }

            for (int index = 0; index < board.PlacedBlocks.Count; index++)
            {
                PlacedSkillBlock placement = board.PlacedBlocks[index];
                SkillBlockDefinition block = FindBlockDefinition(placement.Instance.DefinitionId);
                BoardCell[] occupied = _skillBoardService.GetOccupiedCells(placement);
                for (int cellIndex = 0; cellIndex < occupied.Length; cellIndex++)
                {
                    BoardCell cell = occupied[cellIndex];
                    cells[cell.Y * definition.Width + cell.X] = new GrowthBoardCellView(
                        cell.X,
                        cell.Y,
                        IsTraitSocket(definition, cell.X, cell.Y),
                        placement.Instance.InstanceId,
                        block.Category,
                        block.Rarity);
                }
            }
            return cells;
        }

        private GrowthSkillBlockView[] BuildSkillBlockViews(IReadOnlyList<SkillBlockInstance> source)
        {
            SkillBoardState board = CurrentCareer.MyPlayer.SkillBoardState;
            var result = new GrowthSkillBlockView[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = new GrowthSkillBlockView(
                    source[index],
                    FindBlockDefinition(source[index].DefinitionId),
                    isLocked: board.IsBlockLocked(source[index].InstanceId));
            }
            return result;
        }

        private GrowthSkillBlockView[] BuildPlacedSkillBlockViews(IReadOnlyList<PlacedSkillBlock> source)
        {
            var result = new GrowthSkillBlockView[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                SkillBlockInstance instance = source[index].Instance;
                result[index] = new GrowthSkillBlockView(
                    instance,
                    FindBlockDefinition(instance.DefinitionId),
                    source[index].RotationQuarterTurns,
                    CurrentCareer.MyPlayer.SkillBoardState.IsBlockLocked(instance.InstanceId));
            }
            return result;
        }

        private static GrowthBoardLayoutPlacement[] BuildAppliedLayout(
            IReadOnlyList<PlacedSkillBlock> source)
        {
            var result = new GrowthBoardLayoutPlacement[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = new GrowthBoardLayoutPlacement(
                    source[index].Instance.InstanceId,
                    source[index].OriginX,
                    source[index].OriginY,
                    source[index].RotationQuarterTurns);
            }
            return result;
        }

        private GrowthBlockShopView[] BuildShopCategories(PlayerType playerType, SkillBoardState board)
        {
            SkillBlockCategory[] categories = GetAvailableCategories(playerType);
            var result = new GrowthBlockShopView[categories.Length];
            bool canPurchase = CurrentCareer.AvailableMoney >= _balance.Growth.SkillGacha.SinglePrice;
            for (int index = 0; index < categories.Length; index++)
            {
                result[index] = new GrowthBlockShopView(
                    categories[index],
                    FindShopPreviewBlock(categories[index]).ShapeCells,
                    CountBlocks(board, categories[index]),
                    canPurchase);
            }
            return result;
        }

        private GrowthGachaOfferView[] BuildGachaOffers()
        {
            SkillGachaBalanceTable balance = _balance.Growth.SkillGacha;
            return new[]
            {
                BuildGachaOffer(balance, SkillGachaPurchaseTier.Normal),
                BuildGachaOffer(balance, SkillGachaPurchaseTier.Rare),
                BuildGachaOffer(balance, SkillGachaPurchaseTier.Elite),
                BuildGachaOffer(balance, SkillGachaPurchaseTier.Unique),
                BuildGachaOffer(balance, SkillGachaPurchaseTier.Legendary)
            };
        }

        private GrowthGachaOfferView BuildGachaOffer(
            SkillGachaBalanceTable balance,
            SkillGachaPurchaseTier tier)
        {
            SkillGachaOfferBalance offer = balance.GetOffer(tier);
            long price = balance.GetPrice(tier);
            int year = CurrentCareer.CurrentLeague.CurrentSeason.Year;
            int purchasesUsed = CurrentCareer.MyPlayer.SkillBoardState.GetLimitedPurchaseCount(tier, year);
            string unavailableReason = GetGachaUnavailableReason(tier);
            bool isUnlocked = string.IsNullOrEmpty(unavailableReason);
            bool hasRemainingPurchase = offer.MaxPurchasesPerOffseason == 0 ||
                                        purchasesUsed < offer.MaxPurchasesPerOffseason;
            long fivePullPrice = balance.GetFivePullPrice(tier);
            return new GrowthGachaOfferView(
                tier,
                offer.MinimumRarity,
                price,
                fivePullPrice,
                balance.FivePullDiscountRate,
                balance.GetProbability(tier, SkillBlockRarity.Normal),
                balance.GetProbability(tier, SkillBlockRarity.Rare),
                balance.GetProbability(tier, SkillBlockRarity.Elite),
                balance.GetProbability(tier, SkillBlockRarity.Unique),
                balance.GetProbability(tier, SkillBlockRarity.Legendary),
                offer.MaxPurchasesPerOffseason,
                purchasesUsed,
                isUnlocked,
                unavailableReason,
                isUnlocked && hasRemainingPurchase && CurrentCareer.AvailableMoney >= price,
                isUnlocked && offer.SupportsFivePull && CurrentCareer.AvailableMoney >= fivePullPrice);
        }

        private GrowthGachaPoolItemView[] BuildGachaPool(PlayerType playerType)
        {
            SkillBlockCategory[] categories = GetAvailableCategories(playerType);
            SkillBlockDefinition[] definitions = _balance.Growth.SkillBlocks;
            var result = new List<GrowthGachaPoolItemView>();
            for (int index = 0; index < definitions.Length; index++)
            {
                for (int categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
                {
                    if (definitions[index].Category != categories[categoryIndex])
                        continue;
                    result.Add(new GrowthGachaPoolItemView(definitions[index]));
                    break;
                }
            }
            return result.ToArray();
        }

        private GrowthPlanItemView[] BuildPlannedActivityViews(OffseasonState offseason)
        {
            if (offseason == null)
                return Array.Empty<GrowthPlanItemView>();
            int count = CountPlannedActivities(offseason);
            var result = new GrowthPlanItemView[count];
            int writeIndex = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                TrainingProgramDefinition program = _balance.Growth.GetProgram(
                    activity.ProgramId,
                    activity.Intensity);
                result[writeIndex++] = new GrowthPlanItemView(activity, program);
            }
            for (int index = 1; index < result.Length; index++)
            {
                GrowthPlanItemView item = result[index];
                int target = index - 1;
                while (target >= 0 && result[target].StartWeek > item.StartWeek)
                {
                    result[target + 1] = result[target];
                    target--;
                }
                result[target + 1] = item;
            }
            return result;
        }

        private int GetPlannedScheduleWeeks(OffseasonState offseason)
        {
            if (offseason == null)
                return 0;
            return Math.Max(0, GetNextPlanStartWeek(offseason) - offseason.CurrentWeek);
        }

        private long GetPlannedActivityCost(OffseasonState offseason)
        {
            if (offseason == null)
                return 0L;
            long total = 0L;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                total = checked(total + _balance.Growth.GetProgram(
                    activity.ProgramId,
                    activity.Intensity).MoneyCost);
            }
            return total;
        }

        private int GetProjectedConditionAfterPlan(
            OffseasonState offseason,
            int currentCondition)
        {
            GrowthPlanItemView[] planned = BuildPlannedActivityViews(offseason);
            int condition = currentCondition;
            for (int index = 0; index < planned.Length; index++)
                condition = Math.Max(0, Math.Min(100, condition + planned[index].ConditionChange));
            return condition;
        }

        private GrowthProgramView[] BuildPrograms(PlayerGrowthState growth, int remainingWeeks)
        {
            OffseasonState offseason = CurrentCareer.CurrentOffseason;
            // 휴식은 1주·무료·조건 없음이라 항상 선택 가능하다. 다른 활동을 모두 소화했거나 자금이 없을 때
            // 남은 주를 소화할 방법이 사라져 오프시즌이 끝나지 않는 막다른 상태를 막는 역할을 한다.
            string[] preferredIds = growth.PlayerType == PlayerType.Batter
                ? new[]
                {
                    "personal_batting", "bat_balance_training", "bat_power_camp",
                    "bat_contact_training", "bat_speed_defense_camp", "bat_elite_hitting_lab",
                    "partner_batter_default", "private_batting_coach",
                    "japan_batting_camp", "usa_power_center", "usa_elite_batting_academy",
                    "caribbean_batting_league", "europe_batting_balance",
                    "rehab_general", "sports_science_recovery", "rest"
                }
                : new[]
                {
                    "personal_pitching", "pitch_velocity_camp", "pitch_control_training",
                    "pitch_stamina_camp", "pitch_breaking_training", "pitch_elite_biomechanics",
                    "partner_pitcher_default", "private_pitching_coach",
                    "japan_pitch_design", "usa_velocity_center", "usa_elite_pitching_academy",
                    "caribbean_pitch_league", "europe_pitch_balance",
                    "rehab_general", "sports_science_recovery", "rest"
                };
            var result = new GrowthProgramView[preferredIds.Length];
            for (int index = 0; index < preferredIds.Length; index++)
            {
                TrainingIntensity intensity = string.Equals(
                    _selectedGrowthProgramId,
                    preferredIds[index],
                    StringComparison.Ordinal)
                    ? _selectedTrainingIntensity
                    : TrainingIntensity.Standard;
                result[index] = BuildProgramView(
                    growth,
                    preferredIds[index],
                    intensity,
                    remainingWeeks);
            }
            return result;
        }

        /// <summary>
        /// 팝업이 강도 변경 직후 실제 실행 계약과 같은 값으로 다시 그릴 미리보기를 만든다.
        /// </summary>
        public GrowthProgramView BuildGrowthProgramPreview(
            string programId,
            TrainingIntensity intensity)
        {
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return default;
            int remainingWeeks = CurrentCareer.CurrentOffseason == null ||
                                 CurrentCareer.CurrentOffseason.IsCompleted
                ? 0
                : CurrentCareer.CurrentOffseason.TotalWeeks -
                  CurrentCareer.CurrentOffseason.CurrentWeek + 1;
            return BuildProgramView(growth, programId, intensity, remainingWeeks);
        }

        private GrowthProgramView BuildProgramView(
            PlayerGrowthState growth,
            string programId,
            TrainingIntensity intensity,
            int remainingWeeks)
        {
            TrainingProgramDefinition baseProgram = _balance.Growth.FindProgram(programId) ??
                                                    throw new ArgumentException(
                                                        "존재하지 않는 성장 프로그램입니다.",
                                                        nameof(programId));
            if (!baseProgram.SupportsIntensity)
                intensity = TrainingIntensity.Standard;
            OffseasonState offseason = CurrentCareer.CurrentOffseason;
            int plannedWeeks = GetPlannedScheduleWeeks(offseason);
            long plannedCost = GetPlannedActivityCost(offseason);
            int projectedCondition = GetProjectedConditionAfterPlan(
                offseason,
                growth.Condition);
            int startWeek = offseason == null ? 0 : GetNextPlanStartWeek(offseason);
            int priorSelections = GetPriorProgramSelections(offseason, baseProgram);
            TrainingFitGrade fit = growth.GetTrainingFit(baseProgram.Category);
            GrowthProgramPreview preview = _growthPreviewCalculator.Build(
                growth,
                baseProgram,
                intensity,
                priorSelections,
                fit,
                projectedCondition);
            preview = BuildDisplayedGrowthPreview(
                preview,
                CurrentCareer.MyPlayer.SkillBoardState);
            TrainingProgramDefinition program = preview.Program;
            bool canUseThisOffseason = !program.IsStudy ||
                                       offseason != null &&
                                       !offseason.StudyUsed &&
                                       !CurrentCareer.MyPlayer.StudyState.StudyUsedThisOffseason &&
                                       !HasPlannedStudy(offseason);
            bool isSelected = string.Equals(
                                  _selectedGrowthProgramId,
                                  program.ProgramId,
                                  StringComparison.Ordinal) &&
                              _selectedTrainingIntensity == intensity;
            return new GrowthProgramView(
                preview,
                fit,
                CurrentCareer.AvailableMoney,
                plannedCost,
                remainingWeeks,
                plannedWeeks,
                startWeek,
                growth.Condition,
                CurrentCareer.AvailableMoney >= plannedCost + program.MoneyCost,
                IsOffseason() && remainingWeeks >= plannedWeeks + program.DurationWeeks,
                projectedCondition >= program.MinimumCondition,
                canUseThisOffseason,
                isSelected,
                _balance.Growth.Condition.ReducedMinimum,
                _balance.Growth.Condition.WarningMinimum);
        }

        private GrowthProgramPreview BuildDisplayedGrowthPreview(
            GrowthProgramPreview preview,
            SkillBoardState board)
        {
            var ranges = new AbilityGrowthRange[preview.AbilityRanges.Length];
            for (int index = 0; index < ranges.Length; index++)
            {
                AbilityGrowthRange range = preview.AbilityRanges[index];
                int bonus = _skillBoardService.GetAbilityBonus(board, range.Ability);
                int displayedCurrent = Math.Min(
                    AbilityRatings.Maximum,
                    range.CurrentValue + bonus);
                int visibleCapacity = AbilityRatings.Maximum - displayedCurrent;
                int minimumGain = Math.Min(range.MinimumGain, visibleCapacity);
                int maximumGain = Math.Min(range.MaximumGain, visibleCapacity);
                ranges[index] = new AbilityGrowthRange(
                    range.Ability,
                    displayedCurrent,
                    minimumGain,
                    maximumGain);
            }
            return new GrowthProgramPreview(
                preview.Program,
                ranges,
                preview.ConditionBefore,
                preview.ConditionAfter,
                preview.ConditionAfterWithDiscomfort,
                preview.PriorSelections,
                preview.RepetitionMultiplier);
        }

        private int GetPriorProgramSelections(
            OffseasonState offseason,
            TrainingProgramDefinition program)
        {
            if (program.IsStudy)
                return CurrentCareer.MyPlayer.StudyState.GetConsecutiveVisits(program.ProgramId);
            if (offseason == null)
                return 0;

            int count = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Completed &&
                    activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                if (_balance.Growth.GetProgram(
                        activity.ProgramId,
                        activity.Intensity).Category == program.Category)
                {
                    count++;
                }
            }
            return count;
        }

        private bool HasPlannedStudy(OffseasonState offseason)
        {
            if (offseason == null)
                return false;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                if (_balance.Growth.GetProgram(
                        activity.ProgramId,
                        activity.Intensity).IsStudy)
                {
                    return true;
                }
            }
            return false;
        }

        private static GrowthResultRecord[] BuildRecentGrowth(PlayerGrowthState growth)
        {
            int count = Math.Min(4, growth.GrowthHistory.Count);
            var result = new GrowthResultRecord[count];
            for (int index = 0; index < count; index++)
                result[index] = growth.GrowthHistory[growth.GrowthHistory.Count - 1 - index];
            return result;
        }

        private bool TryGetGrowthRuntime(out PlayerGrowthState growth)
        {
            growth = CurrentCareer?.MyPlayer?.GrowthState;
            if (CurrentCareer == null || _balance == null || growth == null)
            {
                FailGrowth("진행 중인 커리어의 성장 상태가 없습니다.");
                return false;
            }
            EnsureGrowthRuntime();
            return true;
        }

        private void EnsureGrowthRuntime()
        {
            if (_growthBoundCareer == CurrentCareer && _skillBoardService != null)
                return;
            _growthBoundCareer = CurrentCareer;
            _skillBoardService = new SkillBoardService(
                _balance.Growth.SkillBoard,
                _balance.Growth.SkillBlocks);
            _skillGachaService = new SkillGachaService(
                _balance.Growth.SkillGacha,
                _balance.Growth.SkillBlocks);
            _careerGrowthService = new CareerGrowthService(CurrentCareer, _balance);
            _growthPreviewCalculator = new GrowthPreviewCalculator(_balance.Growth);
            _selectedGrowthProgramId = string.Empty;
            _selectedTrainingIntensity = TrainingIntensity.Standard;
            _lastPulledBlocks = Array.Empty<SkillBlockInstance>();
        }

        private void ResetGrowthRuntime()
        {
            _growthBoundCareer = null;
            _skillBoardService = null;
            _skillGachaService = null;
            _careerGrowthService = null;
            _growthPreviewCalculator = null;
            _selectedGrowthProgramId = string.Empty;
            _selectedTrainingIntensity = TrainingIntensity.Standard;
            _lastPulledBlocks = Array.Empty<SkillBlockInstance>();
        }

        private bool RequireBoardEditing()
        {
            if (!TryGetGrowthRuntime(out _))
                return false;
            if (!IsOffseason())
                return FailGrowth("성장판 배치는 오프시즌에만 변경할 수 있습니다.");
            return true;
        }

        private bool IsOffseason()
        {
            return CurrentCareer?.CurrentLeague?.CurrentSeason?.Phase == SeasonPhase.Offseason &&
                   CurrentCareer.CurrentOffseason != null;
        }

        private bool CompleteGrowthCommand()
        {
            LastError = string.Empty;
            CareerChanged?.Invoke();
            return true;
        }

        private bool FailGrowth(string message)
        {
            LastError = message;
            CareerChanged?.Invoke();
            return false;
        }

        private SkillBlockDefinition FindBlockDefinition(string definitionId)
        {
            SkillBlockDefinition[] definitions = _balance.Growth.SkillBlocks;
            for (int index = 0; index < definitions.Length; index++)
            {
                if (string.Equals(definitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return definitions[index];
            }
            throw new InvalidOperationException("스킬 블록 정의를 찾을 수 없습니다.");
        }

        private SkillBlockDefinition FindShopPreviewBlock(SkillBlockCategory category)
        {
            SkillBlockDefinition[] definitions = _balance.Growth.SkillBlocks;
            for (int index = 0; index < definitions.Length; index++)
            {
                if (definitions[index].Category == category &&
                    definitions[index].Rarity == SkillBlockRarity.Normal)
                {
                    return definitions[index];
                }
            }
            throw new InvalidOperationException("스킬 상점 미리보기 블록 정의를 찾을 수 없습니다.");
        }

        private static SkillBlockInstance FindSkillBlockInstance(SkillBoardState board, int instanceId)
        {
            SkillBlockInstance owned = board.FindOwnedBlock(instanceId);
            if (owned.InstanceId > 0)
                return owned;
            for (int index = 0; index < board.PlacedBlocks.Count; index++)
            {
                if (board.PlacedBlocks[index].Instance.InstanceId == instanceId)
                    return board.PlacedBlocks[index].Instance;
            }
            return default;
        }

        private string GetGachaUnavailableReason(SkillGachaPurchaseTier tier)
        {
            SkillGachaBalanceTable balance = _balance.Growth.SkillGacha;
            if (balance.HighTierPurchasesRequireOffseason &&
                tier >= SkillGachaPurchaseTier.Unique &&
                !IsOffseason())
            {
                return "Unique 이상 뽑기는 오프시즌에만 구매할 수 있습니다.";
            }
            if (tier != SkillGachaPurchaseTier.Legendary)
                return string.Empty;
            if (CurrentCareer.CurrentExpectedRole != Baseball.Core.Teams.ExpectedRole.StartingCompetition)
                return "Legendary 해금에는 1군 주전 등급이 필요합니다.";
            int awardCount = CountCareerAwards();
            if (awardCount < balance.LegendaryMinimumCareerAwards)
                return $"Legendary 해금에는 개인 수상 {balance.LegendaryMinimumCareerAwards}회가 필요합니다.";
            return string.Empty;
        }

        private int CountCareerAwards()
        {
            int count = CountPlayerAwards(CurrentCareer.CurrentLeague.CurrentSeason.Awards);
            for (int index = 0; index < CurrentCareer.SeasonHistory.Count; index++)
                count += CountPlayerAwards(CurrentCareer.SeasonHistory[index].Awards);
            return count;
        }

        private int CountPlayerAwards(SeasonAwardsState awards)
        {
            if (awards == null)
                return 0;
            int count = 0;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                if (awards.Results[index].IncludesWinner(CurrentCareer.MyPlayer.PlayerId))
                    count++;
            }
            return count;
        }

        private SkillBlockCategory[] GetAvailableCategories(PlayerType playerType)
        {
            return playerType == PlayerType.Batter
                ? new[]
                {
                    SkillBlockCategory.Contact,
                    SkillBlockCategory.Power,
                    SkillBlockCategory.Defense,
                    SkillBlockCategory.BatterMental
                }
                : new[]
                {
                    SkillBlockCategory.Velocity,
                    SkillBlockCategory.Control,
                    SkillBlockCategory.Breaking,
                    SkillBlockCategory.PitcherMental
                };
        }

        private bool IsCategoryAvailable(PlayerType playerType, SkillBlockCategory category)
        {
            SkillBlockCategory[] categories = GetAvailableCategories(playerType);
            for (int index = 0; index < categories.Length; index++)
            {
                if (categories[index] == category)
                    return true;
            }
            return false;
        }

        private int CountBlocks(SkillBoardState board, SkillBlockCategory category)
        {
            int count = 0;
            for (int index = 0; index < board.OwnedBlocks.Count; index++)
            {
                if (FindBlockDefinition(board.OwnedBlocks[index].DefinitionId).Category == category)
                    count++;
            }
            for (int index = 0; index < board.PlacedBlocks.Count; index++)
            {
                if (FindBlockDefinition(board.PlacedBlocks[index].Instance.DefinitionId).Category == category)
                    count++;
            }
            return count;
        }

        private static bool IsTraitSocket(SkillBoardDefinition definition, int x, int y)
        {
            for (int index = 0; index < definition.TraitSockets.Length; index++)
            {
                if (definition.TraitSockets[index].X == x && definition.TraitSockets[index].Y == y)
                    return true;
            }
            return false;
        }

        private PlannedOffseasonActivity PlanGrowthActivityCore(
            string programId,
            TrainingIntensity intensity)
        {
            if (!IsOffseason())
                throw new InvalidOperationException("오프시즌에만 성장 활동을 계획할 수 있습니다.");
            PlayerGrowthState growth = CurrentCareer.MyPlayer.GrowthState;
            TrainingProgramDefinition baseProgram = _balance.Growth.FindProgram(programId);
            if (baseProgram == null || !baseProgram.CanUse(growth.PlayerType))
                throw new ArgumentException("선택할 수 없는 성장 프로그램입니다.", nameof(programId));
            if (!baseProgram.SupportsIntensity && intensity != TrainingIntensity.Standard)
                throw new InvalidOperationException("개인 훈련만 강도를 조절할 수 있습니다.");
            if (baseProgram.IsStudy &&
                (CurrentCareer.CurrentOffseason.StudyUsed ||
                 CurrentCareer.MyPlayer.StudyState.StudyUsedThisOffseason))
            {
                throw new InvalidOperationException("유학은 오프시즌당 한 번만 가능합니다.");
            }
            if (FindInProgressActivity(CurrentCareer.CurrentOffseason) != null)
                throw new InvalidOperationException("진행 중인 활동을 먼저 마쳐야 합니다.");

            int startWeek = GetNextPlanStartWeek(CurrentCareer.CurrentOffseason);
            return _careerGrowthService.PlanActivity(
                programId,
                startWeek,
                intensity);
        }

        private static int GetNextPlanStartWeek(OffseasonState offseason)
        {
            int startWeek = offseason.CurrentWeek;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                startWeek = Math.Max(startWeek, activity.EndWeek + 1);
            }
            return startWeek;
        }

        private static int CountPlannedActivities(OffseasonState offseason)
        {
            int count = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                if (offseason.Activities[index].Status == OffseasonActivityStatus.Planned)
                    count++;
            }
            return count;
        }

        private static PlannedOffseasonActivity FindInProgressActivity(OffseasonState offseason)
        {
            if (offseason == null)
                return null;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                if (offseason.Activities[index].Status == OffseasonActivityStatus.InProgress)
                    return offseason.Activities[index];
            }
            return null;
        }
    }
}

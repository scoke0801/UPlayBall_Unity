using System;
using System.Collections.Generic;
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
        private string _selectedGrowthProgramId = string.Empty;
        private SkillBlockInstance[] _lastPulledBlocks = Array.Empty<SkillBlockInstance>();

        public CareerGrowthView GrowthDashboard => BuildGrowthDashboard();

        /// <summary>
        /// 확정된 시즌 기록을 자연 성장·노쇠·수입으로 결산하고 선택 가능한 오프시즌을 연다.
        /// </summary>
        public bool SettleSeasonAndBeginOffseason()
        {
            if (!TryGetGrowthRuntime(out _))
                return false;

            SeasonState season = CurrentCareer.League.CurrentSeason;
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
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return false;
            if (!IsOffseason())
                return FailGrowth("오프시즌에만 성장 활동을 선택할 수 있습니다.");

            TrainingProgramDefinition program = _balance.Growth.FindProgram(programId);
            if (program == null || !program.CanUse(growth.PlayerType))
                return FailGrowth("선택할 수 없는 성장 프로그램입니다.");
            if (growth.Condition < program.MinimumCondition)
                return FailGrowth("현재 컨디션으로 시작할 수 없는 프로그램입니다.");
            if (program.IsStudy &&
                (CurrentCareer.CurrentOffseason.StudyUsed ||
                 CurrentCareer.MyPlayer.StudyState.StudyUsedThisOffseason))
            {
                return FailGrowth("유학은 오프시즌당 한 번만 가능합니다.");
            }

            PlannedOffseasonActivity active = FindInProgressActivity(CurrentCareer.CurrentOffseason);
            if (active != null && !string.Equals(active.ProgramId, programId, StringComparison.Ordinal))
                return FailGrowth("진행 중인 활동을 먼저 마쳐야 합니다.");

            _selectedGrowthProgramId = programId;
            return CompleteGrowthCommand();
        }

        /// <summary>
        /// 선택한 활동의 전체 기간을 한 번에 진행하고 결과를 즉시 반영한다.
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
                if (activity == null)
                {
                    if (string.IsNullOrEmpty(_selectedGrowthProgramId))
                        return FailGrowth("먼저 진행할 성장 활동을 선택해 주세요.");
                    _careerGrowthService.ExecuteActivity(_selectedGrowthProgramId);
                }
                else
                {
                    _careerGrowthService.CompleteActivity(activity.ActivityId);
                }

                _selectedGrowthProgramId = string.Empty;
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
            if (!TryGetGrowthRuntime(out PlayerGrowthState growth))
                return false;
            if (!IsCategoryAvailable(growth.PlayerType, category))
                return FailGrowth("선수 유형에 맞지 않는 스킬 블록 계통입니다.");

            try
            {
                SkillBoardState board = CurrentCareer.MyPlayer.SkillBoardState;
                ulong stream = SkillPullStream ^
                               ((ulong)(uint)CurrentCareer.League.CurrentSeason.SeasonId << 32) ^
                               (uint)(board.TotalPullCount + 1);
                ulong seed = DeterministicSeed.Derive(CurrentCareer.League.RandomSeed, stream);
                SkillBlockInstance result = _skillGachaService.PullSingle(
                    CurrentCareer.Economy,
                    board,
                    category,
                    CurrentCareer.League.CurrentSeason.Year,
                    new Pcg32Random(seed));
                _lastPulledBlocks = new[] { result };
                return CompleteGrowthCommand();
            }
            catch (InvalidOperationException exception)
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
                    CurrentCareer.League.CurrentSeason.Year);
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
                int year = CurrentCareer.League.CurrentSeason.Year;
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
                LastPulledBlocks = BuildSkillBlockViews(_lastPulledBlocks),
                ShopCategories = BuildShopCategories(growth.PlayerType, board),
                Programs = BuildPrograms(growth, remainingWeeks),
                RecentGrowth = BuildRecentGrowth(growth),
                IsOffseason = IsOffseason(),
                CanEditBoard = IsOffseason(),
                CanRedesignBoard = IsOffseason() &&
                                   !offseason.BoardRedesignUsed &&
                                   board.PlacedBlocks.Count > 0 &&
                                   CurrentCareer.AvailableMoney >= _balance.Growth.SkillBoardRedesignCost,
                IsBoardRedesignUsed = offseason?.BoardRedesignUsed ?? false,
                IsActivityInProgress = active != null,
                CanCompleteOffseason = IsOffseason() && active == null,
                ActiveProgramId = active?.ProgramId ?? string.Empty,
                SelectedProgramId = active?.ProgramId ?? _selectedGrowthProgramId,
                CurrentWeek = offseason?.CurrentWeek ?? 0,
                TotalWeeks = offseason?.TotalWeeks ?? _balance.Growth.OffseasonWeeks,
                RemainingWeeks = remainingWeeks,
                ActiveActivityEndWeek = active?.EndWeek ?? 0,
                SinglePullPrice = _balance.Growth.SkillGacha.SinglePrice,
                BundlePullPrice = _balance.Growth.SkillGacha.BundlePrice,
                BoardRedesignCost = _balance.Growth.SkillBoardRedesignCost,
                RarePityCount = board.PityRareCount,
                EpicPityCount = board.PityEpicCount,
                RarePityTarget = _balance.Growth.SkillGacha.RarePity,
                EpicPityTarget = _balance.Growth.SkillGacha.EpicPity,
                CommonProbability = _balance.Growth.SkillGacha.CommonProbability,
                UncommonProbability = _balance.Growth.SkillGacha.UncommonProbability,
                RareProbability = _balance.Growth.SkillGacha.RareProbability,
                EpicProbability = _balance.Growth.SkillGacha.EpicProbability
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
            var result = new GrowthSkillBlockView[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = new GrowthSkillBlockView(source[index], FindBlockDefinition(source[index].DefinitionId));
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

        private GrowthProgramView[] BuildPrograms(PlayerGrowthState growth, int remainingWeeks)
        {
            OffseasonState offseason = CurrentCareer.CurrentOffseason;
            // 휴식은 1주·무료·조건 없음이라 항상 선택 가능하다. 다른 활동을 모두 소화했거나 자금이 없을 때
            // 남은 주를 소화할 방법이 사라져 오프시즌이 끝나지 않는 막다른 상태를 막는 역할을 한다.
            string[] preferredIds = growth.PlayerType == PlayerType.Batter
                ? new[] { "personal_batting", "partner_batter_default", "japan_batting_camp", "rehab_general", "rest" }
                : new[] { "personal_pitching", "partner_pitcher_default", "japan_pitch_design", "rehab_general", "rest" };
            var result = new GrowthProgramView[preferredIds.Length];
            for (int index = 0; index < preferredIds.Length; index++)
            {
                TrainingProgramDefinition program = _balance.Growth.FindProgram(preferredIds[index]);
                bool canUseThisOffseason = !program.IsStudy ||
                                           offseason != null &&
                                           !offseason.StudyUsed &&
                                           !CurrentCareer.MyPlayer.StudyState.StudyUsedThisOffseason;
                result[index] = new GrowthProgramView(
                    program,
                    growth.GetTrainingFit(program.Category),
                    CurrentCareer.AvailableMoney >= program.MoneyCost,
                    IsOffseason() && remainingWeeks >= program.DurationWeeks,
                    growth.Condition >= program.MinimumCondition,
                    canUseThisOffseason,
                    string.Equals(
                        _selectedGrowthProgramId,
                        program.ProgramId,
                        StringComparison.Ordinal));
            }
            return result;
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
            _selectedGrowthProgramId = string.Empty;
            _lastPulledBlocks = Array.Empty<SkillBlockInstance>();
        }

        private void ResetGrowthRuntime()
        {
            _growthBoundCareer = null;
            _skillBoardService = null;
            _skillGachaService = null;
            _careerGrowthService = null;
            _selectedGrowthProgramId = string.Empty;
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
            return CurrentCareer?.League?.CurrentSeason?.Phase == SeasonPhase.Offseason &&
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
                    definitions[index].Rarity == SkillBlockRarity.Common)
                {
                    return definitions[index];
                }
            }
            throw new InvalidOperationException("스킬 상점 미리보기 블록 정의를 찾을 수 없습니다.");
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

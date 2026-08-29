using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Growth;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 성장판 한 칸의 소켓·점유 블록 정보를 Presentation에 전달한다.
    /// </summary>
    public readonly struct GrowthBoardCellView
    {
        public GrowthBoardCellView(
            int x,
            int y,
            bool isTraitSocket,
            int instanceId,
            SkillBlockCategory category,
            SkillBlockRarity rarity)
        {
            X = x;
            Y = y;
            IsTraitSocket = isTraitSocket;
            InstanceId = instanceId;
            Category = category;
            Rarity = rarity;
        }

        public int X { get; }
        public int Y { get; }
        public bool IsTraitSocket { get; }
        public int InstanceId { get; }
        public bool IsOccupied => InstanceId > 0;
        public SkillBlockCategory Category { get; }
        public SkillBlockRarity Rarity { get; }
    }

    /// <summary>
    /// 보유하거나 최근 획득한 한 스킬 블록의 표시용 정의다.
    /// </summary>
    public readonly struct GrowthSkillBlockView
    {
        public GrowthSkillBlockView(
            SkillBlockInstance instance,
            SkillBlockDefinition definition,
            int rotationQuarterTurns = 0,
            bool isLocked = false)
        {
            InstanceId = instance.InstanceId;
            DefinitionId = definition.BlockId;
            Rarity = definition.Rarity;
            Category = definition.Category;
            ShapeCells = definition.ShapeCells;
            RotationQuarterTurns = rotationQuarterTurns;
            CanRotate = definition.CanRotate;
            AbilityBonuses = definition.AbilityBonuses;
            SellValue = definition.SellValue;
            IsLocked = isLocked;
        }

        public int InstanceId { get; }
        public string DefinitionId { get; }
        public SkillBlockRarity Rarity { get; }
        public SkillBlockCategory Category { get; }
        public BoardCell[] ShapeCells { get; }
        public int RotationQuarterTurns { get; }
        public bool CanRotate { get; }
        public AbilityChange[] AbilityBonuses { get; }
        public long SellValue { get; }
        public bool IsLocked { get; }
        public int CellCount => ShapeCells?.Length ?? 0;
    }

    /// <summary>
    /// Presentation에서 확정 요청한 한 블록의 보드 배치다.
    /// </summary>
    public readonly struct GrowthBoardLayoutPlacement
    {
        public GrowthBoardLayoutPlacement(int instanceId, int originX, int originY, int rotationQuarterTurns)
        {
            InstanceId = instanceId;
            OriginX = originX;
            OriginY = originY;
            RotationQuarterTurns = rotationQuarterTurns;
        }

        public int InstanceId { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int RotationQuarterTurns { get; }
    }

    /// <summary>
    /// 선택 블록을 특정 칸에 놓을 때 차지할 칸과 배치 가능 여부다.
    /// </summary>
    public readonly struct GrowthBlockPlacementPreviewView
    {
        public GrowthBlockPlacementPreviewView(BoardCell[] cells, bool canPlace)
        {
            Cells = cells ?? Array.Empty<BoardCell>();
            CanPlace = canPlace;
        }

        public BoardCell[] Cells { get; }
        public bool CanPlace { get; }
    }

    /// <summary>
    /// 스킬 상점의 계통별 보유량과 구매 가능 여부다.
    /// </summary>
    public readonly struct GrowthBlockShopView
    {
        public GrowthBlockShopView(
            SkillBlockCategory category,
            BoardCell[] previewShapeCells,
            int ownedCount,
            bool canPurchase)
        {
            Category = category;
            PreviewShapeCells = previewShapeCells;
            OwnedCount = ownedCount;
            CanPurchase = canPurchase;
        }

        public SkillBlockCategory Category { get; }
        public BoardCell[] PreviewShapeCells { get; }
        public int OwnedCount { get; }
        public bool CanPurchase { get; }
    }

    /// <summary>
    /// 뽑기 오버레이가 선택 등급·계통별 획득 가능 블록을 미리 보여주는 항목이다.
    /// </summary>
    public readonly struct GrowthGachaPoolItemView
    {
        public GrowthGachaPoolItemView(SkillBlockDefinition definition)
        {
            DefinitionId = definition.BlockId;
            Rarity = definition.Rarity;
            Category = definition.Category;
            ShapeCells = definition.ShapeCells;
            AbilityBonuses = definition.AbilityBonuses;
        }

        public string DefinitionId { get; }
        public SkillBlockRarity Rarity { get; }
        public SkillBlockCategory Category { get; }
        public BoardCell[] ShapeCells { get; }
        public AbilityChange[] AbilityBonuses { get; }
    }

    /// <summary>
    /// 한 스킬 블록 구매 등급의 가격·공개 확률·구매 가능 여부다.
    /// </summary>
    public readonly struct GrowthGachaOfferView
    {
        public GrowthGachaOfferView(
            SkillGachaPurchaseTier tier,
            SkillBlockRarity minimumRarity,
            long price,
            long fivePullPrice,
            double fivePullDiscountRate,
            double normalProbability,
            double rareProbability,
            double eliteProbability,
            double uniqueProbability,
            double legendaryProbability,
            int maxPurchasesPerOffseason,
            int purchasesUsed,
            bool isUnlocked,
            string unavailableReason,
            bool canPurchaseOne,
            bool canPurchaseFive)
        {
            Tier = tier;
            MinimumRarity = minimumRarity;
            Price = price;
            FivePullPrice = fivePullPrice;
            FivePullDiscountRate = fivePullDiscountRate;
            NormalProbability = normalProbability;
            RareProbability = rareProbability;
            EliteProbability = eliteProbability;
            UniqueProbability = uniqueProbability;
            LegendaryProbability = legendaryProbability;
            MaxPurchasesPerOffseason = maxPurchasesPerOffseason;
            PurchasesUsed = purchasesUsed;
            IsUnlocked = isUnlocked;
            UnavailableReason = unavailableReason ?? string.Empty;
            CanPurchaseOne = canPurchaseOne;
            CanPurchaseFive = canPurchaseFive;
        }

        public SkillGachaPurchaseTier Tier { get; }
        public SkillBlockRarity MinimumRarity { get; }
        public long Price { get; }
        public long FivePullPrice { get; }
        public double FivePullDiscountRate { get; }
        public double NormalProbability { get; }
        public double RareProbability { get; }
        public double EliteProbability { get; }
        public double UniqueProbability { get; }
        public double LegendaryProbability { get; }
        public int MaxPurchasesPerOffseason { get; }
        public int PurchasesUsed { get; }
        public int RemainingPurchases => MaxPurchasesPerOffseason == 0
            ? int.MaxValue
            : Math.Max(0, MaxPurchasesPerOffseason - PurchasesUsed);
        public bool IsUnlocked { get; }
        public string UnavailableReason { get; }
        public bool CanPurchaseOne { get; }
        public bool CanPurchaseFive { get; }
        public bool CanPurchase => CanPurchaseOne;
    }

    /// <summary>
    /// 오프시즌 타임라인에 확정 대기 중인 한 성장 활동이다.
    /// </summary>
    public readonly struct GrowthPlanItemView
    {
        public GrowthPlanItemView(
            PlannedOffseasonActivity activity,
            TrainingProgramDefinition program)
        {
            ActivityId = activity.ActivityId;
            ProgramId = activity.ProgramId;
            ActivityType = program.ActivityType;
            Intensity = activity.Intensity;
            StartWeek = activity.StartWeek;
            EndWeek = activity.EndWeek;
            DurationWeeks = activity.DurationWeeks;
            MoneyCost = program.MoneyCost;
            ConditionChange = program.ConditionChange;
        }

        public int ActivityId { get; }
        public string ProgramId { get; }
        public OffseasonActivityType ActivityType { get; }
        public TrainingIntensity Intensity { get; }
        public int StartWeek { get; }
        public int EndWeek { get; }
        public int DurationWeeks { get; }
        public long MoneyCost { get; }
        public int ConditionChange { get; }
    }

    /// <summary>
    /// 오프시즌 액션 카드가 표시할 비용·기간·예상 성장 근거다.
    /// </summary>
    public readonly struct GrowthProgramView
    {
        public GrowthProgramView(
            GrowthProgramPreview preview,
            TrainingFitGrade fit,
            long moneyBefore,
            long plannedCost,
            int remainingWeeks,
            int plannedWeeks,
            int startWeek,
            int currentCondition,
            bool canAfford,
            bool canFitSchedule,
            bool canMeetCondition,
            bool canUseThisOffseason,
            bool isSelected,
            int conditionWarningMinimum,
            int conditionDangerMinimum,
            double potentialBreakthroughProbability)
        {
            TrainingProgramDefinition definition = preview.Program;
            ProgramId = definition.ProgramId;
            ActivityType = definition.ActivityType;
            Category = definition.Category;
            Intensity = definition.Intensity;
            SupportsIntensity = definition.SupportsIntensity;
            DurationWeeks = definition.DurationWeeks;
            MoneyCost = definition.MoneyCost;
            AbilityWeights = definition.TargetAbilityWeights;
            AbilityRanges = preview.AbilityRanges;
            ConditionChange = definition.ConditionChange;
            ConditionBefore = preview.ConditionBefore;
            ConditionAfter = preview.ConditionAfter;
            ConditionAfterWithDiscomfort = preview.ConditionAfterWithDiscomfort;
            InjuryRisk = definition.InjuryRisk;
            MinimumGuaranteedGain = definition.MinimumGuaranteedGain;
            MaxTotalGain = definition.MaxTotalGain;
            MinimumCondition = definition.MinimumCondition;
            CanRaisePotential = definition.CanRaisePotential;
            PotentialBreakthroughProbability = potentialBreakthroughProbability;
            MinimumPotentialBreakthroughsWhenCapped =
                definition.MinimumPotentialBreakthroughsWhenCapped;
            Fit = fit;
            MoneyBefore = moneyBefore;
            PlannedCost = plannedCost;
            MoneyAfter = Math.Max(0L, moneyBefore - plannedCost - definition.MoneyCost);
            MoneyShortfall = Math.Max(0L, plannedCost + definition.MoneyCost - moneyBefore);
            RemainingWeeksBefore = remainingWeeks;
            PlannedWeeks = plannedWeeks;
            RemainingWeeksAfter = Math.Max(0, remainingWeeks - plannedWeeks - definition.DurationWeeks);
            WeeksShortfall = Math.Max(0, plannedWeeks + definition.DurationWeeks - remainingWeeks);
            StartWeek = startWeek;
            EndWeek = startWeek + definition.DurationWeeks - 1;
            CurrentCondition = currentCondition;
            PriorSelections = preview.PriorSelections;
            RepetitionMultiplier = preview.RepetitionMultiplier;
            CanAfford = canAfford;
            CanFitSchedule = canFitSchedule;
            CanMeetCondition = canMeetCondition;
            CanUseThisOffseason = canUseThisOffseason;
            IsSelected = isSelected;
            UsesMajorityOfRemainingTime = remainingWeeks > 0 &&
                                          definition.DurationWeeks * 2 >= remainingWeeks;
            IsConditionWarning = preview.ConditionAfter < conditionWarningMinimum;
            IsConditionDanger = preview.ConditionAfter < conditionDangerMinimum;
        }

        public string ProgramId { get; }
        public OffseasonActivityType ActivityType { get; }
        public TrainingCategory Category { get; }
        public TrainingIntensity Intensity { get; }
        public bool SupportsIntensity { get; }
        public int DurationWeeks { get; }
        public long MoneyCost { get; }
        public AbilityWeight[] AbilityWeights { get; }
        public AbilityGrowthRange[] AbilityRanges { get; }
        public int ConditionChange { get; }
        public int ConditionBefore { get; }
        public int ConditionAfter { get; }
        public int ConditionAfterWithDiscomfort { get; }
        public double InjuryRisk { get; }
        public int MinimumGuaranteedGain { get; }
        public int MaxTotalGain { get; }
        public int MinimumCondition { get; }
        public bool CanRaisePotential { get; }
        public double PotentialBreakthroughProbability { get; }
        public int MinimumPotentialBreakthroughsWhenCapped { get; }
        public TrainingFitGrade Fit { get; }
        public long MoneyBefore { get; }
        public long PlannedCost { get; }
        public long MoneyAfter { get; }
        public long MoneyShortfall { get; }
        public int RemainingWeeksBefore { get; }
        public int PlannedWeeks { get; }
        public int RemainingWeeksAfter { get; }
        public int WeeksShortfall { get; }
        public int StartWeek { get; }
        public int EndWeek { get; }
        public int CurrentCondition { get; }
        public int PriorSelections { get; }
        public double RepetitionMultiplier { get; }
        public bool CanAfford { get; }
        public bool CanFitSchedule { get; }
        public bool CanMeetCondition { get; }
        public bool CanUseThisOffseason { get; }
        public bool IsSelected { get; }
        public bool UsesMajorityOfRemainingTime { get; }
        public bool IsConditionWarning { get; }
        public bool IsConditionDanger { get; }
        public bool CanSelect => CanAfford && CanFitSchedule && CanMeetCondition && CanUseThisOffseason;
    }

    /// <summary>
    /// 성장 화면 한 번의 Render가 소비하는 읽기 전용 상태다.
    /// </summary>
    public sealed class CareerGrowthView
    {
        public PlayerType PlayerType { get; internal set; }
        public int[] BaseAbilities { get; internal set; }
        public int[] StableAbilities { get; internal set; }
        public int[] BoardBonuses { get; internal set; }
        public int BoardWidth { get; internal set; }
        public int BoardHeight { get; internal set; }
        public GrowthBoardCellView[] BoardCells { get; internal set; }
        public GrowthSkillBlockView[] OwnedBlocks { get; internal set; }
        public GrowthSkillBlockView[] PlacedBlocks { get; internal set; }
        public GrowthBoardLayoutPlacement[] AppliedLayout { get; internal set; }
        public GrowthSkillBlockView[] LastPulledBlocks { get; internal set; }
        public GrowthBlockShopView[] ShopCategories { get; internal set; }
        public GrowthGachaOfferView[] GachaOffers { get; internal set; }
        public GrowthGachaPoolItemView[] GachaPool { get; internal set; }
        public GrowthProgramView[] Programs { get; internal set; }
        public GrowthPlanItemView[] PlannedActivities { get; internal set; }
        public GrowthResultRecord[] RecentGrowth { get; internal set; }
        public bool IsOffseason { get; internal set; }
        public bool CanEditBoard { get; internal set; }
        public bool CanRedesignBoard { get; internal set; }
        public bool IsBoardRedesignUsed { get; internal set; }
        public bool IsActivityInProgress { get; internal set; }
        public bool CanCompleteOffseason { get; internal set; }
        public string ActiveProgramId { get; internal set; }
        public string SelectedProgramId { get; internal set; }
        public TrainingIntensity SelectedTrainingIntensity { get; internal set; }
        public int CurrentWeek { get; internal set; }
        public int TotalWeeks { get; internal set; }
        public int RemainingWeeks { get; internal set; }
        public int PlannedWeeks { get; internal set; }
        public long PlannedCost { get; internal set; }
        public int ProjectedConditionAfterPlan { get; internal set; }
        public int ActiveActivityEndWeek { get; internal set; }
        public long SinglePullPrice { get; internal set; }
        public long BundlePullPrice { get; internal set; }
        public long BoardRedesignCost { get; internal set; }
        public int ElitePityCount { get; internal set; }
        public int UniquePityCount { get; internal set; }
        public int LegendaryPityCount { get; internal set; }
        public int ElitePityTarget { get; internal set; }
        public int UniquePityTarget { get; internal set; }
        public int LegendaryPityTarget { get; internal set; }
    }
}

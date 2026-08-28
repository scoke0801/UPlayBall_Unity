using Baseball.Core.Growth;
using Baseball.Core.Players;

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
            int rotationQuarterTurns = 0)
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
    /// 오프시즌 액션 카드가 표시할 비용·기간·예상 성장 근거다.
    /// </summary>
    public readonly struct GrowthProgramView
    {
        public GrowthProgramView(
            TrainingProgramDefinition definition,
            TrainingFitGrade fit,
            bool canAfford,
            bool canFitSchedule,
            bool canMeetCondition,
            bool canUseThisOffseason,
            bool isSelected)
        {
            ProgramId = definition.ProgramId;
            ActivityType = definition.ActivityType;
            Category = definition.Category;
            DurationWeeks = definition.DurationWeeks;
            MoneyCost = definition.MoneyCost;
            AbilityWeights = definition.TargetAbilityWeights;
            ConditionChange = definition.ConditionChange;
            InjuryRisk = definition.InjuryRisk;
            MinimumGuaranteedGain = definition.MinimumGuaranteedGain;
            MaxTotalGain = definition.MaxTotalGain;
            Fit = fit;
            CanAfford = canAfford;
            CanFitSchedule = canFitSchedule;
            CanMeetCondition = canMeetCondition;
            CanUseThisOffseason = canUseThisOffseason;
            IsSelected = isSelected;
        }

        public string ProgramId { get; }
        public OffseasonActivityType ActivityType { get; }
        public TrainingCategory Category { get; }
        public int DurationWeeks { get; }
        public long MoneyCost { get; }
        public AbilityWeight[] AbilityWeights { get; }
        public int ConditionChange { get; }
        public double InjuryRisk { get; }
        public int MinimumGuaranteedGain { get; }
        public int MaxTotalGain { get; }
        public TrainingFitGrade Fit { get; }
        public bool CanAfford { get; }
        public bool CanFitSchedule { get; }
        public bool CanMeetCondition { get; }
        public bool CanUseThisOffseason { get; }
        public bool IsSelected { get; }
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
        public GrowthSkillBlockView[] LastPulledBlocks { get; internal set; }
        public GrowthBlockShopView[] ShopCategories { get; internal set; }
        public GrowthProgramView[] Programs { get; internal set; }
        public GrowthResultRecord[] RecentGrowth { get; internal set; }
        public bool IsOffseason { get; internal set; }
        public bool CanEditBoard { get; internal set; }
        public bool CanRedesignBoard { get; internal set; }
        public bool IsBoardRedesignUsed { get; internal set; }
        public bool IsActivityInProgress { get; internal set; }
        public bool CanCompleteOffseason { get; internal set; }
        public string ActiveProgramId { get; internal set; }
        public string SelectedProgramId { get; internal set; }
        public int CurrentWeek { get; internal set; }
        public int TotalWeeks { get; internal set; }
        public int RemainingWeeks { get; internal set; }
        public int ActiveActivityEndWeek { get; internal set; }
        public long SinglePullPrice { get; internal set; }
        public long BundlePullPrice { get; internal set; }
        public long BoardRedesignCost { get; internal set; }
        public int RarePityCount { get; internal set; }
        public int EpicPityCount { get; internal set; }
        public int RarePityTarget { get; internal set; }
        public int EpicPityTarget { get; internal set; }
        public double CommonProbability { get; internal set; }
        public double UncommonProbability { get; internal set; }
        public double RareProbability { get; internal set; }
        public double EpicProbability { get; internal set; }
    }
}

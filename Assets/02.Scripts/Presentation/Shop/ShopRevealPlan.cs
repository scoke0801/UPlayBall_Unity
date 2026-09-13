using System;
using Baseball.Core.Growth;
using Baseball.Core.Shop;

namespace Baseball.Presentation.Shop
{
    /// <summary>결과 공개에 사용할 연출 양이다. 확정 결과와 RNG에는 영향을 주지 않는다.</summary>
    public enum ShopRevealPresentationMode
    {
        Full,
        HighlightsOnly,
        Minimal
    }

    public enum ShopRevealTheme
    {
        ScoutingReport,
        DevelopmentAnalysis,
        TacticalLab,
        ConditionCare
    }

    /// <summary>지급된 스킬 블록의 실제 테트로미노 형상을 결과 카드에 전달하는 표시 스냅샷이다.</summary>
    public sealed class ShopSkillBlockRevealModel
    {
        /// <summary>정적 블록 정의에서 결과 카드가 필요한 식별자·등급·형상만 복사한다.</summary>
        public ShopSkillBlockRevealModel(SkillBlockDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            DefinitionId = definition.BlockId;
            Rarity = definition.Rarity;
            Category = definition.Category;
            ShapeCells = new BoardCell[definition.ShapeCells.Length];
            Array.Copy(definition.ShapeCells, ShapeCells, ShapeCells.Length);
        }

        public string DefinitionId { get; }
        public SkillBlockRarity Rarity { get; }
        public SkillBlockCategory Category { get; }
        public BoardCell[] ShapeCells { get; }
    }

    /// <summary>SFX 시스템이 연결될 때 상품 연출 단계별 소리를 재생하기 위한 표시 계층 신호다.</summary>
    public enum ShopRevealAudioCue
    {
        ScoutingPaper,
        DevelopmentScan,
        TacticalAnalysis,
        ApprovalStamp,
        CardReveal,
        ExceptionalPause,
        Summary
    }

    /// <summary>구매 결과 하나를 어떤 Tempo로 공개할지 나타내는 표시 전용 계획이다.</summary>
    public readonly struct ShopRevealItemPlan
    {
        public ShopRevealItemPlan(ShopGrantedItem item, bool usesFullSequence)
        {
            Item = item;
            UsesFullSequence = usesFullSequence;
        }

        public ShopGrantedItem Item { get; }
        public bool UsesFullSequence { get; }
        public bool IsHighlighted => Item.HighestIntensity >= ShopRevealIntensity.Rare;
    }

    /// <summary>이미 확정된 결과를 타입별 연출과 MultiReveal 순서로 변환한다.</summary>
    public sealed class ShopRevealPlan
    {
        public ShopRevealPlan(
            ShopRevealTheme theme,
            string stageTitle,
            string centerpieceArtworkKey,
            ShopRevealItemPlan[] items)
        {
            Theme = theme;
            StageTitle = stageTitle ?? string.Empty;
            CenterpieceArtworkKey = centerpieceArtworkKey ?? string.Empty;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public ShopRevealTheme Theme { get; }
        public string StageTitle { get; }
        public string CenterpieceArtworkKey { get; }
        public ShopRevealItemPlan[] Items { get; }
        public bool IsMultiReveal => Items.Length > 1;
    }

    /// <summary>상품 종류와 결과 강도만 읽어 재현 가능한 Reveal 계획을 만든다.</summary>
    public static class ShopRevealPlanBuilder
    {
        public static ShopRevealPlan Build(
            ShopPurchaseResult result,
            ShopProductDetailsSnapshot details,
            ShopRevealPresentationMode mode)
        {
            if (!result.IsSuccess || result.Items == null || result.Items.Length == 0)
                throw new ArgumentException("성공한 구매 결과가 필요합니다.", nameof(result));
            if (details == null)
                throw new ArgumentNullException(nameof(details));

            var items = new ShopRevealItemPlan[result.Items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                ShopGrantedItem item = result.Items[index];
                bool usesFullSequence = details.Kind != ShopProductKind.ConditionItem && details.Kind != ShopProductKind.StudyReset && (mode == ShopRevealPresentationMode.Full ||
                    mode == ShopRevealPresentationMode.HighlightsOnly &&
                    item.HighestIntensity >= ShopRevealIntensity.Rare);
                items[index] = new ShopRevealItemPlan(item, usesFullSequence);
            }

            switch (details.Kind)
            {
                case ShopProductKind.StudyReset:
                    return new ShopRevealPlan(ShopRevealTheme.ConditionCare, "유학 초기화 완료 · 다시 유학할 수 있습니다",
                        ShopArtwork.SkillAnalysisRevealKey, items);
                case ShopProductKind.ConditionItem:
                    return new ShopRevealPlan(ShopRevealTheme.ConditionCare, "선수단 컨디션 적용 완료",
                        ShopArtwork.SkillAnalysisRevealKey, items);
                case ShopProductKind.PlayerCardPack:
                    return new ShopRevealPlan(
                        ShopRevealTheme.ScoutingReport,
                        "스카우팅 보고서 검토",
                        ShopArtwork.ScoutReportRevealKey,
                        items);
                case ShopProductKind.SkillBlockPack:
                    return new ShopRevealPlan(
                        ShopRevealTheme.DevelopmentAnalysis,
                        "선수 성장 분석",
                        ShopArtwork.SkillAnalysisRevealKey,
                        items);
                case ShopProductKind.TacticCardPack:
                    return new ShopRevealPlan(
                        ShopRevealTheme.TacticalLab,
                        "전술 연구 분석",
                        ShopArtwork.TacticLabRevealKey,
                        items);
                default:
                    throw new ArgumentOutOfRangeException(nameof(details.Kind));
            }
        }
    }
}

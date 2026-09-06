using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Shop;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 스킬 블록 상품을 기존 <see cref="SkillGachaService"/>로 지급한다.
    /// 결제·오프시즌 구매 제한·보장 카운트는 모두 그 서비스가 이미 소유하므로
    /// 상점은 다시 계산하지 않고 그대로 위임한다.
    /// </summary>
    public sealed class SkillBlockPackFulfillment : IShopProductFulfillment
    {
        private readonly SkillGachaService _gachaService;
        private readonly IReadOnlyList<SkillBlockDefinition> _definitions;
        private readonly Func<CareerEconomyState> _economyProvider;
        private readonly Func<SkillBoardState> _boardProvider;
        private readonly Func<SkillBlockCategory[]> _categoryProvider;
        private readonly Func<int> _seasonYearProvider;
        private readonly Func<IRandomSource> _randomFactory;

        public SkillBlockPackFulfillment(
            SkillGachaService gachaService,
            IReadOnlyList<SkillBlockDefinition> definitions,
            Func<CareerEconomyState> economyProvider,
            Func<SkillBoardState> boardProvider,
            Func<SkillBlockCategory[]> categoryProvider,
            Func<int> seasonYearProvider,
            Func<IRandomSource> randomFactory)
        {
            _gachaService = gachaService ?? throw new ArgumentNullException(nameof(gachaService));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _economyProvider = economyProvider ?? throw new ArgumentNullException(nameof(economyProvider));
            _boardProvider = boardProvider ?? throw new ArgumentNullException(nameof(boardProvider));
            _categoryProvider = categoryProvider ?? throw new ArgumentNullException(nameof(categoryProvider));
            _seasonYearProvider = seasonYearProvider ?? throw new ArgumentNullException(nameof(seasonYearProvider));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        public ShopProductKind Kind => ShopProductKind.SkillBlockPack;

        public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (!Enum.TryParse(product.SourceId, out SkillGachaPurchaseTier tier))
                return ShopFulfillmentResult.Failure("알 수 없는 스킬 블록 등급입니다.");

            SkillBlockCategory[] categories = _categoryProvider();
            if (categories == null || categories.Length == 0)
                return ShopFulfillmentResult.Failure("구매 가능한 스킬 블록 계통이 없습니다.");

            try
            {
                CareerEconomyState economy = _economyProvider();
                SkillBoardState board = _boardProvider();
                int seasonYear = _seasonYearProvider();
                IRandomSource random = _randomFactory();

                SkillBlockInstance[] pulled = product.DrawCount == 1
                    ? new[]
                    {
                        _gachaService.PullSingle(
                            economy, board, PickCategory(categories, random), tier, seasonYear, random)
                    }
                    : _gachaService.PullBundle(economy, board, categories, tier, seasonYear, random);

                return ShopFulfillmentResult.Success(Describe(pulled));
            }
            catch (InvalidOperationException exception)
            {
                return ShopFulfillmentResult.Failure(exception.Message);
            }
        }

        private static SkillBlockCategory PickCategory(SkillBlockCategory[] categories, IRandomSource random)
        {
            if (categories.Length == 1)
                return categories[0];
            int index = Math.Min((int)(random.NextDouble() * categories.Length), categories.Length - 1);
            return categories[index];
        }

        private ShopGrantedItem[] Describe(SkillBlockInstance[] pulled)
        {
            var items = new ShopGrantedItem[pulled.Length];
            for (int index = 0; index < pulled.Length; index++)
            {
                SkillBlockInstance instance = pulled[index];
                SkillBlockRarity rarity = FindRarity(instance.DefinitionId);
                items[index] = new ShopGrantedItem(
                    instance.DefinitionId,
                    instance.DefinitionId,
                    DescribeRarity(rarity),
                    // 스킬 블록은 같은 정의를 여러 개 보유할 수 있어 중복 개념이 없다. 항상 새 블록이다.
                    true);
            }
            return items;
        }

        private SkillBlockRarity FindRarity(string definitionId)
        {
            for (int index = 0; index < _definitions.Count; index++)
            {
                if (string.Equals(_definitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return _definitions[index].Rarity;
            }
            return SkillBlockRarity.Normal;
        }

        private static string DescribeRarity(SkillBlockRarity rarity)
        {
            switch (rarity)
            {
                case SkillBlockRarity.Normal: return "일반";
                case SkillBlockRarity.Rare: return "희귀";
                case SkillBlockRarity.Elite: return "정예";
                case SkillBlockRarity.Unique: return "고유";
                case SkillBlockRarity.Legendary: return "전설";
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }
    }
}

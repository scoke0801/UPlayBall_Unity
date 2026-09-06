using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Historical;
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
                SkillBlockDefinition definition = FindDefinition(instance.DefinitionId);
                items[index] = new ShopGrantedItem(
                    instance.DefinitionId,
                    SkillBlockShopText.DescribeAbilityBonuses(definition),
                    DescribeRarity(definition.Rarity),
                    // 스킬 블록은 같은 정의를 여러 개 보유할 수 있어 중복 개념이 없다. 항상 새 블록이다.
                    true);
            }
            return items;
        }

        private SkillBlockDefinition FindDefinition(string definitionId)
        {
            for (int index = 0; index < _definitions.Count; index++)
            {
                if (string.Equals(_definitions[index].BlockId, definitionId, StringComparison.Ordinal))
                    return _definitions[index];
            }
            throw new InvalidOperationException("지급된 스킬 블록 정의를 찾을 수 없습니다.");
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

    /// <summary>구단주 지갑을 한 번 결제하고 공유 스킬 블록 인벤토리에 결과를 지급한다.</summary>
    public sealed class OwnerSkillBlockPackFulfillment : IShopProductFulfillment
    {
        private readonly OwnerSkillGachaResolver _resolver;
        private readonly SkillBlockDefinition[] _definitions;
        private readonly IShopWallet _wallet;
        private readonly Func<OwnerSkillBlockInventoryState> _inventoryProvider;
        private readonly Func<IRandomSource> _randomFactory;

        public OwnerSkillBlockPackFulfillment(
            OwnerSkillGachaResolver resolver,
            SkillBlockDefinition[] definitions,
            IShopWallet wallet,
            Func<OwnerSkillBlockInventoryState> inventoryProvider,
            Func<IRandomSource> randomFactory)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _inventoryProvider = inventoryProvider ?? throw new ArgumentNullException(nameof(inventoryProvider));
            _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
        }

        public ShopProductKind Kind => ShopProductKind.SkillBlockPack;

        public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (product.Currency != ShopCurrency.Money ||
                !Enum.TryParse(product.SourceId, out SkillGachaPurchaseTier tier))
                return ShopFulfillmentResult.Failure("스킬 블록 상품 정의가 올바르지 않습니다.");
            if (!_wallet.TrySpend(product.Currency, product.Price))
                return ShopFulfillmentResult.Failure("스킬 블록 구매 자금이 부족합니다.");

            SkillBlockInstance[] blocks = _resolver.Draw(
                _inventoryProvider(), tier, product.DrawCount, _randomFactory());
            var items = new ShopGrantedItem[blocks.Length];
            for (int index = 0; index < items.Length; index++)
            {
                SkillBlockDefinition definition = FindDefinition(blocks[index].DefinitionId);
                items[index] = new ShopGrantedItem(
                    blocks[index].DefinitionId,
                    SkillBlockShopText.DescribeAbilityBonuses(definition),
                    DescribeRarity(definition.Rarity),
                    true,
                    primaryIntensity: DescribeRarityIntensity(definition.Rarity));
            }
            return ShopFulfillmentResult.Success(items);
        }

        private SkillBlockDefinition FindDefinition(string definitionId)
        {
            for (int index = 0; index < _definitions.Length; index++)
                if (string.Equals(_definitions[index].BlockId, definitionId, StringComparison.Ordinal)) return _definitions[index];
            throw new InvalidOperationException("지급된 스킬 블록 정의를 찾을 수 없습니다.");
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

        private static ShopRevealIntensity DescribeRarityIntensity(SkillBlockRarity rarity)
        {
            switch (rarity)
            {
                case SkillBlockRarity.Normal: return ShopRevealIntensity.Standard;
                case SkillBlockRarity.Rare: return ShopRevealIntensity.Notable;
                case SkillBlockRarity.Elite: return ShopRevealIntensity.Rare;
                case SkillBlockRarity.Unique:
                case SkillBlockRarity.Legendary: return ShopRevealIntensity.Exceptional;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }
    }

    /// <summary>내부 스킬 블록 ID를 노출하지 않고 실제 보너스를 한국어 능력치명으로 표시한다.</summary>
    internal static class SkillBlockShopText
    {
        public static string DescribeAbilityBonuses(SkillBlockDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            AbilityChange[] bonuses = definition.AbilityBonuses;
            if (bonuses.Length == 0)
                return DescribeCategory(definition.Category);

            var builder = new StringBuilder(24);
            for (int index = 0; index < bonuses.Length; index++)
            {
                if (index > 0)
                    builder.Append(" · ");
                builder.Append(DescribeAbility(bonuses[index].Ability));
                builder.Append(' ');
                if (bonuses[index].Amount > 0)
                    builder.Append('+');
                builder.Append(bonuses[index].Amount);
            }
            return builder.ToString();
        }

        private static string DescribeCategory(SkillBlockCategory category)
        {
            return category switch
            {
                SkillBlockCategory.Contact => "교타력",
                SkillBlockCategory.Power => "장타력",
                SkillBlockCategory.Baserunning => "주력",
                SkillBlockCategory.Defense => "수비력",
                SkillBlockCategory.BatterMental => "정신력",
                SkillBlockCategory.Velocity => "구속",
                SkillBlockCategory.Control => "제구력",
                SkillBlockCategory.Breaking => "변화구",
                SkillBlockCategory.PitcherPhysical => "체력",
                SkillBlockCategory.PitcherMental => "위기관리",
                SkillBlockCategory.Arm => "송구",
                SkillBlockCategory.Stuff => "구위",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        }

        private static string DescribeAbility(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "교타력",
                PlayerAbility.Power => "장타력",
                PlayerAbility.Speed => "주력",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비력",
                PlayerAbility.BatterMental => "정신력",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구력",
                PlayerAbility.PitcherMental => "위기관리",
                _ => throw new ArgumentOutOfRangeException(nameof(ability))
            };
        }
    }
}

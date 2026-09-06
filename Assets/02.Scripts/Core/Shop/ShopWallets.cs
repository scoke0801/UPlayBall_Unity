using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;

namespace Baseball.Core.Shop
{
    /// <summary>구단주 모드 경제(Money/SP/DP)를 상점 지갑으로 노출한다.</summary>
    public sealed class ManagerEconomyShopWallet : IShopWallet
    {
        private readonly ManagerEconomyState _economy;

        public ManagerEconomyShopWallet(ManagerEconomyState economy)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        public ShopWalletBalance GetBalance()
        {
            return new ShopWalletBalance(
                _economy.Money,
                _economy.ScoutingPoints,
                _economy.DevelopmentPoints);
        }

        public bool TrySpend(ShopCurrency currency, long amount)
        {
            if (amount < 0L)
                throw new ArgumentOutOfRangeException(nameof(amount));
            switch (currency)
            {
                case ShopCurrency.Money:
                    return _economy.TrySpendMoney(amount);
                case ShopCurrency.ScoutingPoint:
                    return amount <= int.MaxValue && _economy.TrySpendScoutingPoints((int)amount);
                case ShopCurrency.DevelopmentPoint:
                    return amount <= int.MaxValue && _economy.TrySpendDevelopmentPoints((int)amount);
                default:
                    throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }
    }

    /// <summary>
    /// 선수 커리어 경제를 상점 지갑으로 노출한다. 커리어에는 SP/DP가 없으므로 항상 0이며,
    /// 그 결과 SP/DP 상품은 잔액 부족으로 자연히 구매 불가가 된다.
    /// </summary>
    public sealed class CareerEconomyShopWallet : IShopWallet
    {
        private readonly CareerEconomyState _economy;
        private readonly Func<int> _seasonYearProvider;

        public CareerEconomyShopWallet(CareerEconomyState economy, Func<int> seasonYearProvider)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _seasonYearProvider = seasonYearProvider ?? throw new ArgumentNullException(nameof(seasonYearProvider));
        }

        public ShopWalletBalance GetBalance()
        {
            return new ShopWalletBalance(_economy.Money, 0, 0);
        }

        public bool TrySpend(ShopCurrency currency, long amount)
        {
            if (amount < 0L)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (currency != ShopCurrency.Money)
                return false;
            if (_economy.Money < amount)
                return false;
            _economy.Spend(
                _seasonYearProvider(),
                MoneyTransactionType.SkillBlockPurchase,
                "Shop",
                amount);
            return true;
        }
    }
}

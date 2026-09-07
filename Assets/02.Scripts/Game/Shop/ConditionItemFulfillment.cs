using System;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>구매 즉시 등록 선수 전원의 컨디션을 올리고 자금을 한 번 차감한다.</summary>
    public sealed class ConditionItemFulfillment : IShopProductFulfillment
    {
        public const string ProductId = "shop.condition.team";
        private readonly IShopWallet _wallet;
        private readonly Func<ManagerHistoricalRuntimeState> _runtime;
        private readonly ConditionChemistryBalanceTable _balance;

        public ConditionItemFulfillment(IShopWallet wallet, Func<ManagerHistoricalRuntimeState> runtime,
            ConditionChemistryBalanceTable balance)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public ShopProductKind Kind => ShopProductKind.ConditionItem;

        /// <summary>대상과 효과를 먼저 검증해 실패 시 자금·컨디션 모두 보존한다.</summary>
        public ShopFulfillmentResult Fulfill(ShopProductDefinition product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (product.Kind != Kind || product.SourceId != ProductId || product.Currency != ShopCurrency.Money ||
                product.Price != _balance.ConditionItemPrice || product.DrawCount != 1)
                return ShopFulfillmentResult.Failure("컨디션 상품 정보가 올바르지 않습니다.");
            var runtime = _runtime();
            var roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            var status = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            var players = new TeamSeasonPlayerStatus[roster.Entries.Count];
            bool canImprove = false;
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = status.GetRequiredPlayer(roster.Entries[index].PlayerPersonId);
                canImprove |= players[index].StoredBaseCondition < 100;
            }
            if (!canImprove) return ShopFulfillmentResult.Failure("선수단 전원의 컨디션이 이미 100입니다.");
            if (!_wallet.TrySpend(ShopCurrency.Money, product.Price))
                return ShopFulfillmentResult.Failure("컨디션 키트를 구매할 자금이 부족합니다.");
            foreach (var player in players) player.ChangeCondition(_balance.ConditionItemBoost);
            return ShopFulfillmentResult.Success(new[]
            {
                new ShopGrantedItem(ProductId, "선수단 컨디션 +" + _balance.ConditionItemBoost, "즉시 적용 완료", false)
            });
        }
    }
}

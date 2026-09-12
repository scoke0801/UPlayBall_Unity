using System;
using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Shop;
using Baseball.Game.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>지정한 보유 선수의 완료 유학을 결제와 함께 초기화한다.</summary>
    public sealed class StudyResetFulfillment : ITargetedShopProductFulfillment
    {
        public const string ProductId = "shop.study.reset";
        private readonly IShopWallet _wallet;
        private readonly Func<ManagerHistoricalRuntimeState> _runtime;
        private readonly long _price;
        private readonly Func<string, string> _displayName;

        public StudyResetFulfillment(IShopWallet wallet, Func<ManagerHistoricalRuntimeState> runtime, long price,
            Func<string, string> displayName = null)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            _price = price;
            _displayName = displayName ?? (_ => "선택 선수");
        }

        public ShopProductKind Kind => ShopProductKind.StudyReset;

        /// <summary>대상 없는 구매는 결제하지 않는다.</summary>
        public ShopFulfillmentResult Fulfill(ShopProductDefinition product) => Fulfill(product, null);

        /// <summary>소유권·진행 상태를 결제 직전에 다시 확인한다.</summary>
        public ShopFulfillmentResult Fulfill(ShopProductDefinition product, string targetCardId)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (product.Kind != Kind || product.SourceId != ProductId || product.Price != _price ||
                product.Currency != ShopCurrency.DevelopmentPoint || product.DrawCount != 1)
                return ShopFulfillmentResult.Failure("유학 초기화권 상품 정보가 올바르지 않습니다.");
            var runtime = _runtime();
            string reason = GetBlockedReason(runtime, targetCardId);
            if (reason.Length > 0) return ShopFulfillmentResult.Failure(reason);
            runtime.TryGetOwnedCard(targetCardId, out var card);
            var season = runtime.WorldCardCatalog.GetPlayerSeason(runtime.WorldCardCatalog.GetRequiredCard(card.CardId));
            string name = _displayName(season.PlayerPersonId);
            var items = new[] { new ShopGrantedItem(card.CardId, name + " 유학 초기화", "다시 유학 가능", false) };
            if (!_wallet.TrySpend(product.Currency, product.Price))
                return ShopFulfillmentResult.Failure("육성 포인트가 부족합니다.");
            card.ResetStudy();
            return ShopFulfillmentResult.Success(items);
        }

        /// <summary>구매 화면과 실제 결제가 같은 대상 조건을 사용한다.</summary>
        public static string GetBlockedReason(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (runtime == null || !runtime.TryGetOwnedCard(cardId, out var card))
                return "유학을 초기화할 보유 선수를 선택하세요.";
            foreach (var project in runtime.PlayerGrowth.StudyProjects)
                if (project.CardId == card.CardId) return "유학 중인 선수는 완료 후 초기화할 수 있습니다.";
            if (card.LastStudySeason >= 0) return string.Empty;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
                if (card.Training.GetStudyBonus((PlayerAbility)index) > 0) return string.Empty;
            return "초기화할 유학 이력이 없습니다.";
        }
    }
}

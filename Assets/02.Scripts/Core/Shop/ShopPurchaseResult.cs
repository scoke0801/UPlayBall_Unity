using System;

namespace Baseball.Core.Shop
{
    /// <summary>
    /// 구매로 지급된 항목 하나를 표현 레이어가 읽을 수 있는 형태로 요약한다.
    /// 카드·블록 타입을 상점이 알 필요가 없도록 문자열 요약만 담는다.
    /// </summary>
    public readonly struct ShopGrantedItem
    {
        public ShopGrantedItem(
            string itemId,
            string displayName,
            string gradeLabel,
            bool isNew,
            string artworkKey = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("ItemId는 비어 있을 수 없습니다.", nameof(itemId));
            ItemId = itemId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? itemId.Trim() : displayName.Trim();
            GradeLabel = gradeLabel == null ? string.Empty : gradeLabel.Trim();
            IsNew = isNew;
            ArtworkKey = artworkKey == null ? string.Empty : artworkKey.Trim();
        }

        public string ItemId { get; }
        public string DisplayName { get; }
        public string GradeLabel { get; }
        public string ArtworkKey { get; }

        /// <summary>이미 보유한 항목이면 false다. 중복 처리 UI가 이 값을 읽는다.</summary>
        public bool IsNew { get; }
    }

    /// <summary>지급 처리 결과다. 재화 차감까지 포함한 트랜잭션 전체의 결과를 나타낸다.</summary>
    public readonly struct ShopFulfillmentResult
    {
        private static readonly ShopGrantedItem[] EmptyItems = new ShopGrantedItem[0];

        private ShopFulfillmentResult(bool isSuccess, string failureMessage, ShopGrantedItem[] items)
        {
            IsSuccess = isSuccess;
            FailureMessage = failureMessage ?? string.Empty;
            Items = items ?? EmptyItems;
        }

        public bool IsSuccess { get; }
        public string FailureMessage { get; }
        public ShopGrantedItem[] Items { get; }

        public static ShopFulfillmentResult Success(ShopGrantedItem[] items)
        {
            if (items == null || items.Length == 0)
                throw new ArgumentException("지급 항목이 없는 성공은 있을 수 없습니다.", nameof(items));
            return new ShopFulfillmentResult(true, string.Empty, items);
        }

        public static ShopFulfillmentResult Failure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("실패 사유는 비어 있을 수 없습니다.", nameof(message));
            return new ShopFulfillmentResult(false, message.Trim(), EmptyItems);
        }
    }

    /// <summary>상점 구매 한 건의 최종 결과다.</summary>
    public readonly struct ShopPurchaseResult
    {
        private static readonly ShopGrantedItem[] EmptyItems = new ShopGrantedItem[0];

        private ShopPurchaseResult(
            bool isSuccess,
            ShopPurchaseFailureReason failureReason,
            string failureMessage,
            ShopGrantedItem[] items)
        {
            IsSuccess = isSuccess;
            FailureReason = failureReason;
            FailureMessage = failureMessage ?? string.Empty;
            Items = items ?? EmptyItems;
        }

        public bool IsSuccess { get; }
        public ShopPurchaseFailureReason FailureReason { get; }
        public string FailureMessage { get; }

        /// <summary>지급 결과다. 연출은 이미 확정된 이 목록을 표현할 뿐 결과를 다시 뽑지 않는다.</summary>
        public ShopGrantedItem[] Items { get; }

        public static ShopPurchaseResult Success(ShopGrantedItem[] items)
        {
            return new ShopPurchaseResult(true, ShopPurchaseFailureReason.None, string.Empty, items);
        }

        public static ShopPurchaseResult Failure(ShopPurchaseFailureReason reason, string message)
        {
            return new ShopPurchaseResult(false, reason, message, EmptyItems);
        }
    }

    /// <summary>상점이 재화를 읽고 차감하는 유일한 통로다. 모드별 경제 상태를 이 뒤로 숨긴다.</summary>
    public interface IShopWallet
    {
        ShopWalletBalance GetBalance();

        bool TrySpend(ShopCurrency currency, long amount);
    }

    /// <summary>
    /// 한 상품 종류의 지급을 담당한다.
    /// <para>
    /// 재화 차감은 지급 구현이 소유한다. 스킬 블록처럼 기존 서비스가 이미 결제·구매 제한을
    /// 함께 처리하는 경로가 있어서, 상점이 따로 차감하면 이중 결제가 되기 때문이다.
    /// </para>
    /// </summary>
    public interface IShopProductFulfillment
    {
        ShopProductKind Kind { get; }

        ShopFulfillmentResult Fulfill(ShopProductDefinition product);
    }
}

using System;
using System.Collections.Generic;

namespace Baseball.Core.Shop
{
    /// <summary>탭이 잠긴 이유다. UI는 잠긴 탭을 숨기지 않고 이 사유를 그대로 표시한다.</summary>
    public enum ShopLockReason
    {
        None,
        LockedByGameMode,
        LockedByProgress,
        LockedByLeague
    }

    /// <summary>
    /// 탭 하나의 개방 여부와 사유다. 모드 분기를 UI에 심지 않기 위해,
    /// "선수 모드는 스킬 블록만"이라는 규칙도 코드가 아니라 이 설정값으로 표현한다.
    /// </summary>
    public readonly struct ShopCategoryAvailability
    {
        private ShopCategoryAvailability(ShopTab tab, ShopLockReason lockReason, string lockDescription)
        {
            Tab = tab;
            LockReason = lockReason;
            LockDescription = lockDescription ?? string.Empty;
        }

        public ShopTab Tab { get; }
        public ShopLockReason LockReason { get; }

        /// <summary>플레이어에게 보여줄 잠금 사유 문구다. 열린 탭에서는 빈 문자열이다.</summary>
        public string LockDescription { get; }

        public bool IsUnlocked => LockReason == ShopLockReason.None;

        public static ShopCategoryAvailability Unlocked(ShopTab tab)
        {
            return new ShopCategoryAvailability(tab, ShopLockReason.None, string.Empty);
        }

        public static ShopCategoryAvailability Locked(ShopTab tab, ShopLockReason reason, string description)
        {
            if (reason == ShopLockReason.None)
                throw new ArgumentException("잠금 사유가 None이면 Unlocked를 사용하세요.", nameof(reason));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("잠금 사유 문구는 비어 있을 수 없습니다.", nameof(description));
            return new ShopCategoryAvailability(tab, reason, description.Trim());
        }
    }

    /// <summary>모든 탭의 개방 상태를 모은 설정값이다. 명시되지 않은 탭은 열린 것으로 본다.</summary>
    public sealed class ShopAvailabilityTable
    {
        private readonly Dictionary<ShopTab, ShopCategoryAvailability> _availabilities;

        public ShopAvailabilityTable(IReadOnlyList<ShopCategoryAvailability> availabilities)
        {
            if (availabilities == null)
                throw new ArgumentNullException(nameof(availabilities));

            _availabilities = new Dictionary<ShopTab, ShopCategoryAvailability>(availabilities.Count);
            for (int index = 0; index < availabilities.Count; index++)
            {
                ShopCategoryAvailability availability = availabilities[index];
                if (_availabilities.ContainsKey(availability.Tab))
                    throw new ArgumentException("같은 탭이 두 번 정의됐습니다.", nameof(availabilities));
                _availabilities.Add(availability.Tab, availability);
            }
        }

        public ShopCategoryAvailability Get(ShopTab tab)
        {
            return _availabilities.TryGetValue(tab, out ShopCategoryAvailability availability)
                ? availability
                : ShopCategoryAvailability.Unlocked(tab);
        }

        public bool IsUnlocked(ShopTab tab) => Get(tab).IsUnlocked;

        /// <summary>상품이 속한 탭이 잠겨 있으면 구매도 불가능하다. Featured 노출과 무관하게 원 탭을 따른다.</summary>
        public bool IsPurchasable(ShopProductDefinition product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return IsUnlocked(product.Tab);
        }

        public static ShopAvailabilityTable AllUnlocked()
        {
            return new ShopAvailabilityTable(new ShopCategoryAvailability[0]);
        }
    }
}

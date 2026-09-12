using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Game.Career;

namespace Baseball.Game.Shop
{
    /// <summary>
    /// 모드별 상점 개방 범위를 설정값으로 만든다.
    /// 선수 모드에서 선수 카드·작전 카드가 잠기는 이유는 재화가 없어서가 아니라
    /// 로스터·전술 편성이 감독 AI의 권한이기 때문이며, 그 사유를 문구로 남긴다.
    /// </summary>
    public static class ShopAvailabilityFactory
    {
        /// <summary>구단주 스킬 상점의 조회와 구매에 같은 오프시즌 잠금을 표시한다.</summary>
        public static ShopAvailabilityTable CreateForOwner(Baseball.Game.Historical.OwnerSchedulePermission permission)
        {
            return new ShopAvailabilityTable(new[]
            {
                permission.IsAllowed ? ShopCategoryAvailability.Unlocked(ShopTab.SkillBlock)
                    : ShopCategoryAvailability.Locked(ShopTab.SkillBlock, ShopLockReason.LockedByProgress, permission.Reason)
            });
        }
        public static ShopAvailabilityTable CreateFor(GameMode mode)
        {
            return mode == GameMode.PlayerCareer
                ? CreatePlayerCareerTable()
                : CreateOwnerCareerTable();
        }

        private static ShopAvailabilityTable CreatePlayerCareerTable()
        {
            var availabilities = new List<ShopCategoryAvailability>
            {
                ShopCategoryAvailability.Unlocked(ShopTab.Featured),
                ShopCategoryAvailability.Unlocked(ShopTab.SkillBlock),
                ShopCategoryAvailability.Locked(
                    ShopTab.PlayerCard,
                    ShopLockReason.LockedByGameMode,
                    "선수 모드에서는 선수단 구성이 감독의 권한입니다."),
                ShopCategoryAvailability.Locked(
                    ShopTab.TacticCard,
                    ShopLockReason.LockedByGameMode,
                    "선수 모드에서는 작전 지시가 감독의 권한입니다.")
            };
            return new ShopAvailabilityTable(availabilities);
        }

        /// <summary>
        /// 구단주 모드는 선수 카드·작전 카드와 카드별 성장판에 쓸 스킬 블록을 연다.
        /// </summary>
        private static ShopAvailabilityTable CreateOwnerCareerTable()
        {
            var availabilities = new List<ShopCategoryAvailability>
            {
                ShopCategoryAvailability.Unlocked(ShopTab.Featured),
                ShopCategoryAvailability.Unlocked(ShopTab.PlayerCard),
                ShopCategoryAvailability.Unlocked(ShopTab.TacticCard),
                ShopCategoryAvailability.Unlocked(ShopTab.SkillBlock)
            };
            return new ShopAvailabilityTable(availabilities);
        }
    }
}

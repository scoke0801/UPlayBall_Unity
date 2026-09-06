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
        private const string OwnerSkillBoardUnavailable =
            "구단주 모드에는 스킬 블록을 장착할 카드 보드가 아직 없습니다.";

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
        /// 구단주 모드는 선수 카드·작전 카드를 연다. 스킬 블록은 진열은 하되,
        /// 구단주 쪽 장착 보드가 생기기 전까지 사유를 밝혀 잠근다 — 살 수는 있는데
        /// 쓸 데가 없는 상품을 파는 것이 플레이어를 속이는 일이기 때문이다.
        /// </summary>
        private static ShopAvailabilityTable CreateOwnerCareerTable()
        {
            var availabilities = new List<ShopCategoryAvailability>
            {
                ShopCategoryAvailability.Unlocked(ShopTab.Featured),
                ShopCategoryAvailability.Unlocked(ShopTab.PlayerCard),
                ShopCategoryAvailability.Unlocked(ShopTab.TacticCard),
                ShopCategoryAvailability.Locked(
                    ShopTab.SkillBlock,
                    ShopLockReason.LockedByProgress,
                    OwnerSkillBoardUnavailable)
            };
            return new ShopAvailabilityTable(availabilities);
        }
    }
}

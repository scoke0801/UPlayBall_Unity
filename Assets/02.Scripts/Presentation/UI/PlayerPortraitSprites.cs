using Baseball.Core.Players;
using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 선수 카드용 기본 초상 스프라이트(투수/타자)를 Resources에서 불러와 캐시한다.
    /// 프리팹 없이 런타임 생성되는 화면들이므로 인스펙터 직렬화 대신 Resources.Load를 쓴다.
    /// </summary>
    internal static class PlayerPortraitSprites
    {
        private const string PitcherPath = "UI/Portraits/img_pitcher_default";
        private const string HitterPath = "UI/Portraits/img_hitter_default";

        private static Sprite _pitcher;
        private static Sprite _hitter;

        public static Sprite GetDefault(PlayerPosition position)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return isPitcher
                ? _pitcher ??= LoadSprite(PitcherPath)
                : _hitter ??= LoadSprite(HitterPath);
        }

        /// <summary>
        /// 텍스처 임포트 설정이 Multiple 스프라이트 모드로 자동 슬라이스되면
        /// 안티에일리어싱 경계의 작은 조각들이 함께 생기므로, 가장 넓은 조각을 실제 인물로 간주한다.
        /// </summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            Sprite[] subSprites = Resources.LoadAll<Sprite>(path);
            if (subSprites.Length == 0)
                return null;

            Sprite largest = subSprites[0];
            for (int i = 1; i < subSprites.Length; i++)
            {
                if (subSprites[i].rect.width * subSprites[i].rect.height
                    > largest.rect.width * largest.rect.height)
                {
                    largest = subSprites[i];
                }
            }
            return largest;
        }
    }
}

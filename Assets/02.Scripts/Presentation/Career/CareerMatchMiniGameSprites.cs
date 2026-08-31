using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>런타임 생성 미니게임 화면에서 공통으로 사용하는 일러스트를 캐시한다.</summary>
    internal static class CareerMatchMiniGameSprites
    {
        private const string PitchFieldIllustrationPath = "UI/MiniGame/img_pitcher_field";
        private const string BaseballBatIllustrationPath = "UI/MiniGame/img_baseball_bat";
        private const string BaseballBallIllustrationPath = "UI/MiniGame/img_baseball_ball";
        private const string BroadcastFieldIllustrationPath = "UI/MiniGame/img_broadcast_field";

        private const float BaseballBatCropLeft = 0.0166f;
        private const float BaseballBatCropBottom = 0.3605f;
        private const float BaseballBatCropWidth = 0.966f;
        private const float BaseballBatCropHeight = 0.2915f;

        private static Sprite _pitchFieldIllustration;
        private static Sprite _baseballBatIllustration;
        private static Sprite _baseballBatCursorIllustration;
        private static Sprite _baseballBallIllustration;
        private static Sprite _broadcastFieldIllustration;

        public static Sprite GetPitchFieldIllustration()
        {
            return _pitchFieldIllustration ??= Resources.Load<Sprite>(PitchFieldIllustrationPath);
        }

        /// <summary>타격 결과 연출용 야구 방망이 스프라이트를 반환한다.</summary>
        public static Sprite GetBaseballBatIllustration()
        {
            return _baseballBatIllustration ??= Resources.Load<Sprite>(BaseballBatIllustrationPath);
        }

        /// <summary>투구 궤적과 타격 결과 연출에 사용하는 야구공 스프라이트를 반환한다.</summary>
        public static Sprite GetBaseballBallIllustration()
        {
            return _baseballBallIllustration ??= Resources.Load<Sprite>(BaseballBallIllustrationPath);
        }

        /// <summary>중계와 Field View가 공유하는 야구장 스프라이트를 반환한다.</summary>
        public static Sprite GetBroadcastFieldIllustration()
        {
            return _broadcastFieldIllustration ??= Resources.Load<Sprite>(BroadcastFieldIllustrationPath);
        }

        /// <summary>투명 여백을 제외해 타격 조준점에서 선명하게 보이는 방망이 스프라이트를 반환한다.</summary>
        public static Sprite GetBaseballBatCursorIllustration()
        {
            if (_baseballBatCursorIllustration != null)
                return _baseballBatCursorIllustration;

            Sprite source = GetBaseballBatIllustration();
            if (source == null)
                return null;

            Rect sourceRect = source.rect;
            Rect cropRect = new Rect(
                sourceRect.x + sourceRect.width * BaseballBatCropLeft,
                sourceRect.y + sourceRect.height * BaseballBatCropBottom,
                sourceRect.width * BaseballBatCropWidth,
                sourceRect.height * BaseballBatCropHeight);
            _baseballBatCursorIllustration = Sprite.Create(
                source.texture,
                cropRect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _baseballBatCursorIllustration.name = "BaseballBatCursorIllustration";
            _baseballBatCursorIllustration.hideFlags = HideFlags.HideAndDontSave;
            return _baseballBatCursorIllustration;
        }
    }
}

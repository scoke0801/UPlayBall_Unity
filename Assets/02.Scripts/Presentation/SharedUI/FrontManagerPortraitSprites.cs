using System;
using System.Collections.Generic;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>프런트 매니저 초상화를 Sprite 또는 원본 Texture Import 상태와 무관하게 재사용한다.</summary>
    public static class FrontManagerPortraitSprites
    {
        private const string ResourceRoot = "FrontManager/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            Cache.Clear();
        }

        public static Sprite Load(string assetKey, string fallbackAssetKey = "FM_NEUTRAL")
        {
            Sprite sprite = LoadSingle(assetKey);
            return sprite != null || string.IsNullOrWhiteSpace(fallbackAssetKey)
                ? sprite
                : LoadSingle(fallbackAssetKey);
        }

        /// <summary>선택한 매니저의 표정을 불러오고, 없으면 같은 매니저의 기본 표정을 사용한다.</summary>
        public static Sprite LoadForManager(string managerId, string expressionAssetKey)
        {
            string prefix = managerId == FrontManagerIds.DefaultTest ? "FM_02_" : "FM_01_";
            string expression = expressionAssetKey ?? string.Empty;
            if (expression.StartsWith("FM_01_", StringComparison.Ordinal) ||
                expression.StartsWith("FM_02_", StringComparison.Ordinal))
                expression = expression.Substring(6);
            else if (expression.StartsWith("FM_", StringComparison.Ordinal))
                expression = expression.Substring(3);

            // 누락된 표정 때문에 새 게임에서 선택한 인물 자체가 바뀌어서는 안 된다.
            return Load(prefix + expression, prefix + "NEUTRAL");
        }

        private static Sprite LoadSingle(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
                return null;
            if (Cache.TryGetValue(assetKey, out Sprite cached))
                return cached;

            string path = ResourceRoot + assetKey;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    sprite.name = assetKey;
                }
            }
            // Domain Reload를 끈 Editor에서 Import 전 null을 기억하면 이후에도 다른 매니저가
            // 기본 초상화로 대체된다. 성공한 로드만 캐시한다.
            if (sprite != null)
                Cache[assetKey] = sprite;
            return sprite;
        }
    }
}

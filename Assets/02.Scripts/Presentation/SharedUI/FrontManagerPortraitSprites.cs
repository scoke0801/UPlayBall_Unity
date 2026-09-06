using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>프런트 매니저 초상화를 Sprite 또는 원본 Texture Import 상태와 무관하게 재사용한다.</summary>
    public static class FrontManagerPortraitSprites
    {
        private const string ResourceRoot = "FrontManager/";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string assetKey, string fallbackAssetKey = "FM_NEUTRAL")
        {
            Sprite sprite = LoadSingle(assetKey);
            return sprite != null || string.IsNullOrWhiteSpace(fallbackAssetKey)
                ? sprite
                : LoadSingle(fallbackAssetKey);
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
            Cache[assetKey] = sprite;
            return sprite;
        }
    }
}

using System;
using Baseball.Core.Shop;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    /// <summary>상점 전용 Pack과 결과 배경을 Resources 키로 불러온다.</summary>
    public static class ShopArtwork
    {
        public const string PlayerPackKey = "shop-player-pack";
        public const string SkillPackKey = "shop-skill-pack";
        public const string RevealBackgroundKey = "shop-reveal-background";

        private static Texture2D _playerPack;
        private static Texture2D _skillPack;
        private static Texture2D _revealBackground;

        public static string GetProductKey(ShopProductKind kind, string sourceId)
        {
            switch (kind)
            {
                case ShopProductKind.PlayerCardPack:
                    return PlayerPackKey;
                case ShopProductKind.SkillBlockPack:
                    return SkillPackKey;
                case ShopProductKind.TacticCardPack:
                    if (string.Equals(sourceId, "batting", StringComparison.Ordinal))
                        return TacticCardArtwork.BattingKey;
                    if (string.Equals(sourceId, "pitching", StringComparison.Ordinal))
                        return TacticCardArtwork.PitchingKey;
                    return TacticCardArtwork.CommonKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static Texture2D Load(string artworkKey)
        {
            switch (artworkKey)
            {
                case PlayerPackKey:
                    return _playerPack != null
                        ? _playerPack
                        : _playerPack = Resources.Load<Texture2D>("UI/Shop/shop_player_pack");
                case SkillPackKey:
                    return _skillPack != null
                        ? _skillPack
                        : _skillPack = Resources.Load<Texture2D>("UI/Shop/shop_skill_pack");
                case RevealBackgroundKey:
                    return _revealBackground != null
                        ? _revealBackground
                        : _revealBackground = Resources.Load<Texture2D>("UI/Shop/shop_reveal_background");
                default:
                    return TacticCardArtwork.Load(artworkKey);
            }
        }

        public static RawImage Create(Transform parent, string name, string artworkKey, Color tint)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            value.transform.SetParent(parent, false);
            RawImage image = value.GetComponent<RawImage>();
            image.texture = Load(artworkKey);
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }
    }
}

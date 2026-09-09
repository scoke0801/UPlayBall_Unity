using System;
using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    public enum FieldAnchor
    {
        HomePlate, BatterBoxL, BatterBoxR, PitcherMound, FirstBase, SecondBase, ThirdBase,
        LeftField, CenterField, RightField, Shortstop, SecondBaseman, ThirdBaseman, FirstBaseman, Catcher
    }

    /// <summary>배경 원본의 좌상단 기준 정규화 위치다.</summary>
    [Serializable]
    public sealed class FieldAnchorDefinition
    {
        public FieldAnchor id;
        public Vector2 normalizedPosition;
    }

    /// <summary>경기장별 위치·깊이·접점 보정을 판정 데이터에서 분리한다.</summary>
    public sealed class FieldLayoutDefinition : ScriptableObject
    {
        public Texture2D background;
        public FieldAnchorDefinition[] anchors = Array.Empty<FieldAnchorDefinition>();
        public float foregroundHeight = 300f;
        public float depthScale = 0.2f;
        public float ballHeightScale = 0.25f;
        public float duelZoom = 1f;
        public float fieldZoom = 1.08f;
        public float highlightZoom = 1.25f;
        public Vector2 pitcherReleaseOffset = new Vector2(0, 0.055f);
        public Vector2 batContactOffset = new Vector2(0, 0.04f);

        /// <summary>미보정 위치는 빈 경기장의 초기 프리셋으로 반환한다.</summary>
        public Vector2 GetAnchor(FieldAnchor id)
        {
            if (anchors != null)
                for (int i = 0; i < anchors.Length; i++)
                    if (anchors[i] != null && anchors[i].id == id) return anchors[i].normalizedPosition;
            return id switch
            {
                FieldAnchor.HomePlate => new Vector2(0.5f, 0.80f),
                FieldAnchor.BatterBoxL => new Vector2(0.585f, 0.84f),
                FieldAnchor.BatterBoxR => new Vector2(0.414f, 0.84f),
                FieldAnchor.PitcherMound => new Vector2(0.5f, 0.457f),
                FieldAnchor.FirstBase => new Vector2(0.889f, 0.459f),
                FieldAnchor.SecondBase => new Vector2(0.5f, 0.366f),
                FieldAnchor.ThirdBase => new Vector2(0.111f, 0.459f),
                FieldAnchor.LeftField => new Vector2(0.20f, 0.29f),
                FieldAnchor.CenterField => new Vector2(0.5f, 0.25f),
                FieldAnchor.RightField => new Vector2(0.80f, 0.29f),
                FieldAnchor.Shortstop => new Vector2(0.33f, 0.37f),
                FieldAnchor.SecondBaseman => new Vector2(0.67f, 0.37f),
                FieldAnchor.ThirdBaseman => new Vector2(0.17f, 0.43f),
                FieldAnchor.FirstBaseman => new Vector2(0.83f, 0.43f),
                _ => new Vector2(0.5f, 0.90f)
            };
        }
    }
}

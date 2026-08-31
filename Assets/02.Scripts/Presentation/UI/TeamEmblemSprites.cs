using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 4개 Resources 아틀라스의 128개 구단 엠블럼을 ID로 잘라 생성하고 재사용한다.
    /// </summary>
    internal static class TeamEmblemSprites
    {
        public const int EmblemCount = 128;
        private const int AtlasCount = 4;
        private const int EmblemsPerAtlas = 32;
        private const int Columns = 8;
        private const int SourceSize = 1254;

        private static readonly string[] AtlasPaths =
        {
            "TeamEmblems/TeamEmblems_Atlas_01",
            "TeamEmblems/TeamEmblems_Atlas_02",
            "TeamEmblems/TeamEmblems_Atlas_03",
            "TeamEmblems/TeamEmblems_Atlas_04"
        };

        // imagegen 결과의 실제 alpha 행 범위다. 정사각 캔버스의 불규칙한 상하 여백을 Sprite에 포함하지 않는다.
        private static readonly int[,] RowTops =
        {
            { 173, 372, 597, 830 },
            { 163, 366, 646, 902 },
            { 156, 395, 648, 888 },
            { 210, 403, 605, 846 }
        };

        private static readonly int[,] RowBottoms =
        {
            { 353, 573, 799, 1031 },
            { 355, 594, 855, 1085 },
            { 360, 605, 857, 1086 },
            { 391, 596, 811, 1025 }
        };

        private static readonly Texture2D[] Atlases = new Texture2D[AtlasCount];
        private static readonly Sprite[] Sprites = new Sprite[EmblemCount];

        /// <summary>유효한 엠블럼을 찾으면 Image에 적용하고 true를 반환한다.</summary>
        public static bool TryApply(Image image, int emblemId)
        {
            if (image == null)
                return false;
            Sprite sprite = Get(emblemId);
            if (sprite == null)
                return false;

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return true;
        }

        public static Sprite Get(int emblemId)
        {
            if (emblemId <= 0 || emblemId > EmblemCount)
                return null;

            int spriteIndex = emblemId - 1;
            if (Sprites[spriteIndex] != null)
                return Sprites[spriteIndex];

            int atlasIndex = spriteIndex / EmblemsPerAtlas;
            Texture2D atlas = Atlases[atlasIndex] ??= Resources.Load<Texture2D>(AtlasPaths[atlasIndex]);
            if (atlas == null)
                return null;

            int cellIndex = spriteIndex % EmblemsPerAtlas;
            int column = cellIndex % Columns;
            int row = cellIndex / Columns;
            int left = column * atlas.width / Columns;
            int right = (column + 1) * atlas.width / Columns;
            float verticalScale = atlas.height / (float)SourceSize;
            int top = Mathf.RoundToInt(RowTops[atlasIndex, row] * verticalScale);
            int bottom = Mathf.RoundToInt(RowBottoms[atlasIndex, row] * verticalScale);
            top = Mathf.Clamp(top, 0, atlas.height - 1);
            bottom = Mathf.Clamp(bottom, top, atlas.height - 1);

            var rect = new Rect(
                left,
                atlas.height - bottom - 1,
                right - left,
                bottom - top + 1);
            Sprite sprite = Sprite.Create(
                atlas,
                rect,
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = $"TeamEmblem_{emblemId:000}";
            Sprites[spriteIndex] = sprite;
            return sprite;
        }
    }
}

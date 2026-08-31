using Baseball.Core.Growth;
using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>ShapeCells로 식별한 표준 테트로미노를 공용 UI Sprite에 연결한다.</summary>
    internal static class TetrominoSpriteResolver
    {
        private const string AtlasPath = "UI/Growth/skill_block_tetromino_neutral_atlas";
        private static readonly Sprite[] Sprites = new Sprite[7];
        private static bool _isLoaded;

        /// <summary>기본 ShapeCells와 일치하는 캐시된 공용 Sprite를 반환한다.</summary>
        public static Sprite Resolve(BoardCell[] shapeCells)
        {
            if (!TryResolveShape(shapeCells, out TetrominoShape shape))
                return null;

            EnsureLoaded();
            return Sprites[(int)shape];
        }

        /// <summary>좌표 순서와 무관하게 표준 7종 Shape를 식별한다.</summary>
        public static bool TryResolveShape(BoardCell[] shapeCells, out TetrominoShape shape)
        {
            shape = default;
            if (shapeCells == null || shapeCells.Length != TetrominoShapeCatalog.CellCount)
                return false;

            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int index = 0; index < shapeCells.Length; index++)
            {
                minimumX = Mathf.Min(minimumX, shapeCells[index].X);
                minimumY = Mathf.Min(minimumY, shapeCells[index].Y);
            }

            int mask = 0;
            for (int index = 0; index < shapeCells.Length; index++)
            {
                int x = shapeCells[index].X - minimumX;
                int y = shapeCells[index].Y - minimumY;
                if (x < 0 || x >= 4 || y < 0 || y >= 4)
                    return false;
                mask |= 1 << (y * 4 + x);
            }

            switch (mask)
            {
                case 0x000F:
                    shape = TetrominoShape.I;
                    return true;
                case 0x0033:
                    shape = TetrominoShape.O;
                    return true;
                case 0x0027:
                    shape = TetrominoShape.T;
                    return true;
                case 0x0036:
                    shape = TetrominoShape.S;
                    return true;
                case 0x0063:
                    shape = TetrominoShape.Z;
                    return true;
                case 0x0071:
                    shape = TetrominoShape.J;
                    return true;
                case 0x0074:
                    shape = TetrominoShape.L;
                    return true;
                default:
                    return false;
            }
        }

        private static void EnsureLoaded()
        {
            if (_isLoaded)
                return;

            _isLoaded = true;
            Sprite[] loadedSprites = Resources.LoadAll<Sprite>(AtlasPath);
            for (int index = 0; index < loadedSprites.Length; index++)
            {
                Sprite sprite = loadedSprites[index];
                switch (sprite.name)
                {
                    case "SkillBlock_I":
                        Sprites[(int)TetrominoShape.I] = sprite;
                        break;
                    case "SkillBlock_O":
                        Sprites[(int)TetrominoShape.O] = sprite;
                        break;
                    case "SkillBlock_T":
                        Sprites[(int)TetrominoShape.T] = sprite;
                        break;
                    case "SkillBlock_S":
                        Sprites[(int)TetrominoShape.S] = sprite;
                        break;
                    case "SkillBlock_Z":
                        Sprites[(int)TetrominoShape.Z] = sprite;
                        break;
                    case "SkillBlock_J":
                        Sprites[(int)TetrominoShape.J] = sprite;
                        break;
                    case "SkillBlock_L":
                        Sprites[(int)TetrominoShape.L] = sprite;
                        break;
                }
            }
        }
    }
}

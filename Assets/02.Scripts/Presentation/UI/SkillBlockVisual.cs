using System;
using Baseball.Core.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>ShapeCells의 크기와 회전을 유지하며 공용 테트로미노 Sprite를 Tint해 표시한다.</summary>
    internal static class SkillBlockVisual
    {
        private static Texture2D _circleTile;
        private static Texture2D _starTile;

        private static readonly Texture2D[] DirectionalTiles = new Texture2D[16];

        /// <summary>실제 인접 방향의 연결선이 포함된 타일 이미지를 선택한다.</summary>
        public static void ApplyDirectionalTile(RawImage image, SkillBlockRarity rarity,
            BoardCell[] cells, int cellIndex, int rotationQuarterTurns = 0)
        {
            int mask = GetConnectionMask(cells, cellIndex, rotationQuarterTurns);
            if (DirectionalTiles[mask] == null)
                DirectionalTiles[mask] = Resources.Load<Texture2D>("UI/OwnerPowerUp/skill_direction_" + mask);
            image.texture = DirectionalTiles[mask];
            image.color = GetRarityColor(rarity);
            image.raycastTarget = false;
            image.enabled = image.texture != null;
        }

        /// <summary>오른쪽·아래·왼쪽·위 비트로 같은 블록 내부의 연결만 구한다.</summary>
        internal static int GetConnectionMask(BoardCell[] cells, int cellIndex, int rotationQuarterTurns = 0)
        {
            int mask = 0;
            int rotation = NormalizeRotation(rotationQuarterTurns);
            GetRotatedCoordinates(cells[cellIndex], rotation, out int x, out int y);
            for (int index = 0; index < cells.Length; index++)
            {
                GetRotatedCoordinates(cells[index], rotation, out int otherX, out int otherY);
                int dx = otherX - x, dy = otherY - y;
                if (dy == 0 && dx == 1) mask |= 1;
                if (dx == 0 && dy == 1) mask |= 2;
                if (dy == 0 && dx == -1) mask |= 4;
                if (dx == 0 && dy == -1) mask |= 8;
            }
            return mask;
        }

        /// <summary>작은 목록과 카드 뒷면 모두 같은 원형·상위 등급 별 문양을 사용한다.</summary>
        public static void ApplyTile(RawImage image, SkillBlockRarity rarity)
        {
            bool hasStar = rarity == SkillBlockRarity.Elite || rarity == SkillBlockRarity.Unique ||
                           rarity == SkillBlockRarity.Legendary;
            if (hasStar && _starTile == null)
                _starTile = Resources.Load<Texture2D>("UI/OwnerPowerUp/skill_tile_star_v3");
            if (!hasStar && _circleTile == null)
                _circleTile = Resources.Load<Texture2D>("UI/OwnerPowerUp/skill_tile_circle_v3");
            image.texture = hasStar ? _starTile : _circleTile;
            image.color = GetRarityColor(rarity);
            image.raycastTarget = false;
        }

        /// <summary>구단주 카드와 카드훈련에서 같은 등급 색상을 사용한다.</summary>
        public static Color GetRarityColor(SkillBlockRarity rarity) => rarity switch
        {
            SkillBlockRarity.Normal => new Color32(99, 165, 68, 255),
            SkillBlockRarity.Rare => new Color32(61, 139, 210, 255),
            SkillBlockRarity.Elite => new Color32(177, 83, 185, 255),
            SkillBlockRarity.Unique => new Color32(224, 160, 44, 255),
            _ => new Color32(217, 79, 102, 255)
        };

        /// <summary>주어진 표시 영역에 공용 Sprite와 회전·Tint를 적용한 블록 외형을 만든다.</summary>
        public static RectTransform Create(
            Transform parent,
            BoardCell[] shapeCells,
            int rotationQuarterTurns,
            Color tint,
            Vector2 position,
            Vector2 bounds,
            float maximumCellSize,
            string namePrefix)
        {
            if (shapeCells == null || shapeCells.Length == 0)
                return null;

            int rotation = NormalizeRotation(rotationQuarterTurns);
            GetBounds(shapeCells, 0, out int baseWidth, out int baseHeight);
            GetBounds(shapeCells, rotation, out int rotatedWidth, out int rotatedHeight);
            float cellSize = Mathf.Min(
                maximumCellSize,
                bounds.x / rotatedWidth,
                bounds.y / rotatedHeight);

            Sprite sprite = TetrominoSpriteResolver.Resolve(shapeCells);
            return sprite != null
                ? CreateSpriteVisual(
                    parent,
                    sprite,
                    rotation,
                    tint,
                    position,
                    new Vector2(baseWidth * cellSize, baseHeight * cellSize),
                    namePrefix)
                : CreateCellFallback(
                    parent,
                    shapeCells,
                    rotation,
                    tint,
                    position,
                    cellSize,
                    namePrefix);
        }

        private static RectTransform CreateSpriteVisual(
            Transform parent,
            Sprite sprite,
            int rotation,
            Color tint,
            Vector2 position,
            Vector2 size,
            string namePrefix)
        {
            RectTransform rect = CreateRect(namePrefix + "Sprite", parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation * 90f);
            return rect;
        }

        private static RectTransform CreateCellFallback(
            Transform parent,
            BoardCell[] shapeCells,
            int rotation,
            Color tint,
            Vector2 position,
            float cellSize,
            string namePrefix)
        {
            GetRotatedExtents(
                shapeCells,
                rotation,
                out int minimumX,
                out int minimumY,
                out int maximumX,
                out int maximumY);
            float totalWidth = (maximumX - minimumX + 1) * cellSize;
            float totalHeight = (maximumY - minimumY + 1) * cellSize;
            RectTransform root = CreateRect(
                namePrefix + "Fallback",
                parent,
                new Vector2(totalWidth, totalHeight),
                position);

            for (int index = 0; index < shapeCells.Length; index++)
            {
                GetRotatedCoordinates(shapeCells[index], rotation, out int x, out int y);
                float cellX = -totalWidth * 0.5f + cellSize * 0.5f +
                              (x - minimumX) * cellSize;
                float cellY = totalHeight * 0.5f - cellSize * 0.5f -
                              (y - minimumY) * cellSize;
                RectTransform cell = CreateRect(
                    namePrefix + "Cell_" + index,
                    root,
                    new Vector2(cellSize - 2f, cellSize - 2f),
                    new Vector2(cellX, cellY));
                Image image = cell.gameObject.AddComponent<Image>();
                image.color = tint;
                image.raycastTarget = false;
            }
            return root;
        }

        private static void GetBounds(
            BoardCell[] shapeCells,
            int rotation,
            out int width,
            out int height)
        {
            GetRotatedExtents(
                shapeCells,
                rotation,
                out int minimumX,
                out int minimumY,
                out int maximumX,
                out int maximumY);
            width = maximumX - minimumX + 1;
            height = maximumY - minimumY + 1;
        }

        private static void GetRotatedExtents(
            BoardCell[] shapeCells,
            int rotation,
            out int minimumX,
            out int minimumY,
            out int maximumX,
            out int maximumY)
        {
            minimumX = int.MaxValue;
            minimumY = int.MaxValue;
            maximumX = int.MinValue;
            maximumY = int.MinValue;
            for (int index = 0; index < shapeCells.Length; index++)
            {
                GetRotatedCoordinates(shapeCells[index], rotation, out int x, out int y);
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
        }

        private static void GetRotatedCoordinates(
            BoardCell cell,
            int rotation,
            out int x,
            out int y)
        {
            switch (rotation)
            {
                case 1:
                    x = cell.Y;
                    y = -cell.X;
                    break;
                case 2:
                    x = -cell.X;
                    y = -cell.Y;
                    break;
                case 3:
                    x = -cell.Y;
                    y = cell.X;
                    break;
                default:
                    x = cell.X;
                    y = cell.Y;
                    break;
            }
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static int NormalizeRotation(int rotationQuarterTurns)
        {
            return ((rotationQuarterTurns % 4) + 4) % 4;
        }
    }

    /// <summary>가리킨 보드 칸을 블록의 실제 점유 칸에 맞춰 유효한 배치 원점으로 변환한다.</summary>
    internal static class SkillBlockPlacementTargetResolver
    {
        /// <summary>가리킨 칸을 포함하는 후보 중 첫 번째로 배치 가능한 원점을 반환한다.</summary>
        public static bool TryResolveOrigin(
            BoardCell[] localCells,
            int targetX,
            int targetY,
            Func<int, int, bool> canPlaceAtOrigin,
            out int originX,
            out int originY)
        {
            if (localCells == null) throw new ArgumentNullException(nameof(localCells));
            if (canPlaceAtOrigin == null) throw new ArgumentNullException(nameof(canPlaceAtOrigin));

            for (int index = 0; index < localCells.Length; index++)
            {
                int candidateX = targetX - localCells[index].X;
                int candidateY = targetY - localCells[index].Y;
                if (!canPlaceAtOrigin(candidateX, candidateY))
                    continue;

                originX = candidateX;
                originY = candidateY;
                return true;
            }

            originX = targetX;
            originY = targetY;
            return false;
        }
    }
}

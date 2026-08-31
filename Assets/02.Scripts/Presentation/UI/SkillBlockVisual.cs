using System;
using Baseball.Core.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>ShapeCells의 크기와 회전을 유지하며 공용 테트로미노 Sprite를 Tint해 표시한다.</summary>
    internal static class SkillBlockVisual
    {
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
}

using System;
using Baseball.Core.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>ImageGen 야구공·계통색 바탕·등급 문자를 실제 블록 연결 방향으로 표시한다.</summary>
    internal static class SkillBlockVisual
    {
        private static readonly System.Collections.Generic.Dictionary<(Color32, int), Texture2D> DirectionalTiles = new();
        private static Texture2D _plate;
        private static Texture2D _ball;

        /// <summary>실제 인접 방향의 연결선이 포함된 타일 이미지를 선택한다.</summary>
        public static void ApplyDirectionalTile(RawImage image, SkillBlockRarity rarity,
            BoardCell[] cells, int cellIndex, int rotationQuarterTurns = 0, Color? typeColor = null)
        {
            int mask = GetConnectionMask(cells, cellIndex, rotationQuarterTurns);
            image.texture = GetTile(typeColor ?? GetCategoryColor(SkillBlockCategory.Contact), mask);
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = image.texture != null;
            ApplyGradeLabel(image, rarity);
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

        /// <summary>연결 없는 칸에도 같은 야구공과 등급 표시를 적용한다.</summary>
        public static void ApplyTile(RawImage image, SkillBlockRarity rarity, Color? typeColor = null)
        {
            image.texture = GetTile(typeColor ?? GetCategoryColor(SkillBlockCategory.Contact), 0);
            image.color = Color.white;
            image.raycastTarget = false;
            image.enabled = image.texture != null;
            ApplyGradeLabel(image, rarity);
        }

        private static void ApplyGradeLabel(RawImage image, SkillBlockRarity rarity)
        {
            UISkillTileGradeLabel label = image.GetComponentInChildren<UISkillTileGradeLabel>(true);
            if (label == null)
            {
                var rect = CreateRect("GradeLabel", image.transform, Vector2.zero, Vector2.zero);
                rect.anchorMin = new Vector2(.08f, .07f);
                rect.anchorMax = new Vector2(.92f, .37f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                label = rect.gameObject.AddComponent<UISkillTileGradeLabel>();
            }
            label.Initialize(image, SkillBlockGradeCatalog.GetLabel(rarity));
        }

        /// <summary>계통 색은 등급과 분리하며 두 모드의 블록과 범례가 함께 사용한다.</summary>
        public static Color GetCategoryColor(SkillBlockCategory category) => category switch
        {
            SkillBlockCategory.Contact => new Color32(38, 122, 209, 255),
            SkillBlockCategory.Power => new Color32(209, 122, 31, 255),
            SkillBlockCategory.Baserunning => new Color32(61, 168, 79, 255),
            SkillBlockCategory.Defense => new Color32(46, 143, 148, 255),
            SkillBlockCategory.BatterMental => new Color32(125, 97, 196, 255),
            SkillBlockCategory.Velocity => new Color32(199, 66, 56, 255),
            SkillBlockCategory.Control => new Color32(41, 117, 194, 255),
            SkillBlockCategory.Breaking => new Color32(115, 87, 186, 255),
            SkillBlockCategory.PitcherPhysical => new Color32(163, 115, 41, 255),
            SkillBlockCategory.PitcherMental => new Color32(61, 148, 133, 255),
            SkillBlockCategory.Bunt => new Color32(179, 107, 41, 255),
            SkillBlockCategory.Stuff => new Color32(153, 61, 61, 255),
            _ => new Color32(38, 122, 209, 255)
        };

        // ImageGen 원화를 계통별로 한 번 합성한다. 흰 공·붉은 실밥에 계통 Tint가 묻지 않으며
        // 기존 RawImage의 비활성·미리보기 알파·Outline 계약도 한 장의 텍스처로 유지한다.
        private static Texture2D GetTile(Color typeColor, int mask)
        {
            typeColor.a = 1f;
            var key = ((Color32)typeColor, mask);
            if (DirectionalTiles.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            if (_plate == null) _plate = Resources.Load<Texture2D>("UI/OwnerPowerUp/SkillBaseball/Plate");
            if (_ball == null) _ball = Resources.Load<Texture2D>("UI/OwnerPowerUp/SkillBaseball/Ball");
            if (_plate == null || _ball == null) return null;
            const int size = 128;
            var pixels = new Color32[size * size];
            Color railOutline = new Color32(15, 35, 58, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                Color color = _plate.GetPixelBilinear(u, v) * typeColor;
                bool horizontal = ((mask & 1) != 0 && u >= .5f || (mask & 4) != 0 && u <= .5f);
                bool vertical = ((mask & 2) != 0 && v <= .57f || (mask & 8) != 0 && v >= .57f);
                if (horizontal && Mathf.Abs(v - .57f) < .055f || vertical && Mathf.Abs(u - .5f) < .055f)
                    color = railOutline;
                if (horizontal && Mathf.Abs(v - .57f) < .025f || vertical && Mathf.Abs(u - .5f) < .025f)
                    color = Color.white;
                if (u >= .29f && u <= .71f && v >= .39f && v <= .81f)
                {
                    Color ball = _ball.GetPixelBilinear((u - .29f) / .42f, (v - .39f) / .42f);
                    float alpha = color.a + ball.a * (1f - color.a);
                    color = Color.Lerp(color, ball, ball.a);
                    color.a = alpha;
                }
                pixels[y * size + x] = color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SkillBaseball_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            DirectionalTiles[key] = texture;
            return texture;
        }

        /// <summary>구단주 카드와 카드훈련에서 같은 등급 색상을 사용한다.</summary>
        public static Color GetRarityColor(SkillBlockRarity rarity) => rarity switch
        {
            SkillBlockRarity.Normal => new Color32(191, 194, 201, 255),
            SkillBlockRarity.Rare => new Color32(61, 139, 210, 255),
            SkillBlockRarity.Elite => new Color32(177, 83, 185, 255),
            SkillBlockRarity.Unique => new Color32(224, 160, 44, 255),
            SkillBlockRarity.Legendary => new Color32(217, 79, 102, 255),
            _ => new Color32(151, 205, 255, 255)
        };

        /// <summary>주어진 표시 영역에 등급별 방향 타일과 회전을 적용한 블록 외형을 만든다.</summary>
        public static RectTransform Create(
            Transform parent,
            BoardCell[] shapeCells,
            int rotationQuarterTurns,
            SkillBlockRarity rarity,
            Vector2 position,
            Vector2 bounds,
            float maximumCellSize,
            string namePrefix, Color? typeColor = null)
        {
            if (shapeCells == null || shapeCells.Length == 0)
                return null;

            int rotation = NormalizeRotation(rotationQuarterTurns);
            GetBounds(shapeCells, rotation, out int width, out int height);
            float cellSize = Mathf.Min(maximumCellSize, bounds.x / width, bounds.y / height);
            return CreateCells(parent, shapeCells, rotation, rarity, position, cellSize, namePrefix, typeColor);
        }
        private static RectTransform CreateCells(
            Transform parent,
            BoardCell[] shapeCells,
            int rotation,
            SkillBlockRarity rarity,
            Vector2 position,
            float cellSize,
            string namePrefix, Color? typeColor)
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
                namePrefix + "Tiles",
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
                RawImage image = cell.gameObject.AddComponent<RawImage>();
                ApplyDirectionalTile(image, rarity, shapeCells, index, rotation, typeColor);
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

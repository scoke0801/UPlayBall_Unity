using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    internal static partial class MatchUniformMaterials
    {
        [Serializable] private sealed class IllustrationCatalog { public Illustration[] images; }
        [Serializable] private sealed class Illustration { public string resourcePath; public Region[] regions; }
        [Serializable] private sealed class Region { public float[] points; }
        private static IllustrationCatalog _illustrations;
        private static readonly Dictionary<string, Texture2D> Masks = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Material> IllustrationMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);

        /// <summary>배경이 포함된 일러스트는 저작된 의상 영역 안에서만 경기와 같은 색상 교체를 적용한다.</summary>
        public static Material GetForIllustration(string franchiseId, string resourcePath)
        {
            Material source = GetForFranchise(franchiseId);
            if (source == null || string.IsNullOrEmpty(resourcePath)) return null;
            string key = source.name + "/" + resourcePath;
            if (IllustrationMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            _illustrations ??= JsonUtility.FromJson<IllustrationCatalog>(Resources.Load<TextAsset>("UI/SpriteMatch/IllustrationUniformRegions").text);
            foreach (Illustration definition in _illustrations.images)
            {
                if (definition.resourcePath != resourcePath) continue;
                // 선수가 없는 담장·공 장면은 의상 영역을 비워 배경 원화를 보존한다.
                if (definition.regions.Length == 0) return null;
                if (!Masks.TryGetValue(resourcePath, out Texture2D mask))
                    Masks[resourcePath] = mask = CreateMask(definition);
                var material = new Material(source) { name = key, hideFlags = HideFlags.HideAndDontSave };
                material.SetTexture("_UniformMask", mask);
                IllustrationMaterials[key] = material;
                return material;
            }
            // 새 일러스트의 영역이 검수되기 전에는 전체 배경을 염색하지 않는다.
            return null;
        }

        private static Texture2D CreateMask(Illustration definition)
        {
            const int width = 1024, height = 576;
            var pixels = new Color32[width * height];
            var intersections = new List<float>();
            foreach (Region region in definition.regions)
            {
                float[] points = region.points;
                if (points == null || points.Length < 6 || points.Length % 2 != 0)
                    throw new InvalidOperationException("의상 영역 다각형이 잘못되었습니다: " + definition.resourcePath);
                for (int row = 0; row < height; row++)
                {
                    float y = (row + 0.5f) / height;
                    intersections.Clear();
                    for (int i = 0, j = points.Length - 2; i < points.Length; j = i, i += 2)
                    {
                        float yi = points[i + 1], yj = points[j + 1];
                        if ((yi > y) == (yj > y)) continue;
                        intersections.Add(points[i] + (y - yi) * (points[j] - points[i]) / (yj - yi));
                    }
                    intersections.Sort();
                    for (int i = 0; i + 1 < intersections.Count; i += 2)
                    {
                        int first = Mathf.Clamp(Mathf.CeilToInt(intersections[i] * width - 0.5f), 0, width);
                        int last = Mathf.Clamp(Mathf.CeilToInt(intersections[i + 1] * width - 0.5f), 0, width);
                        for (int x = first; x < last; x++)
                            pixels[(height - 1 - row) * width + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }
            var mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "UniformRegion-" + definition.resourcePath,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            mask.SetPixels32(pixels);
            mask.Apply(false, true);
            return mask;
        }

        private static void ResetIllustrations()
        {
            foreach (Material material in IllustrationMaterials.Values)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            foreach (Texture2D mask in Masks.Values)
                if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
            IllustrationMaterials.Clear();
            Masks.Clear();
            _illustrations = null;
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>카드의 구단 계보와 유니폼 원색을 공유하는 경기용 색상 교체 재질 캐시다.</summary>
    internal static partial class MatchUniformMaterials
    {
        [Serializable] private sealed class Catalog { public Definition[] uniforms; }
        [Serializable] private sealed class Definition { public string Id; public string Cap; }
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private static Catalog _catalog;

        /// <summary>공수나 홈·원정에 따라 다른 색을 만들지 않고 같은 구단에는 같은 재질을 반환한다.</summary>
        public static Material GetForFranchise(string franchiseId)
        {
            string uniform = PlayerPortraitSprites.GetUniformForFranchise(franchiseId);
            if (uniform == null) return null;
            if (Materials.TryGetValue(uniform, out Material material) && material != null) return material;
            _catalog ??= JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("UI/SpriteMatch/UniformColors").text);
            foreach (Definition definition in _catalog.uniforms)
            {
                if (definition.Id != uniform) continue;
                if (!ColorUtility.TryParseHtmlString(definition.Cap, out Color color))
                    throw new InvalidOperationException("경기 유니폼 색상 정의가 잘못되었습니다: " + uniform);
                var shader = Resources.Load<Shader>("UI/SpriteMatch/MatchUniform");
                if (shader == null) throw new InvalidOperationException("경기 유니폼 셰이더가 없습니다.");
                material = new Material(shader) { name = "MatchUniform-" + uniform, hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_UniformColor", color);
                Materials[uniform] = material;
                return material;
            }
            throw new InvalidOperationException("카드에 대응하는 경기 유니폼 정의가 없습니다: " + uniform);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            ResetIllustrations();
            foreach (Material material in Materials.Values)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            Materials.Clear();
            _catalog = null;
        }
    }
}

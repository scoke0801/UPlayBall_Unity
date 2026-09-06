using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 구단 별칭 카탈로그로 의미가 맞는 엠블럼을 선택하고 아틀라스 Sprite를 재사용한다.
    /// </summary>
    internal static class TeamEmblemSprites
    {
        public const int EmblemCount = 136;
        private const int LegacyEmblemCount = 128;
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
        private static Dictionary<string, int> _identityEmblems;
        private static Texture2D _identityAtlas;

        [Serializable]
        private sealed class IdentityCatalog
        {
            public IdentityEntry[] entries;
        }

        [Serializable]
        private sealed class IdentityEntry
        {
            public string[] names;
            public int emblemId;
        }

        /// <summary>지역·리그 접두사와 독립적인 구단 별칭으로 표시 엠블럼을 조회한다.</summary>
        public static int ResolveEmblemId(string teamName, int fallbackId = 0)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return fallbackId;
            if (_identityEmblems == null)
                _identityEmblems = LoadIdentityEmblems();

            // 과거 월드의 EmblemId는 중복 방지용 슬롯이다. 표시만 정본 이미지로 교정해 세이브를 바꾸지 않는다.
            string normalized = teamName.Trim();
            string nickname = normalized.Substring(normalized.LastIndexOf(' ') + 1);
            return _identityEmblems.TryGetValue(nickname, out int emblemId) ? emblemId : fallbackId;
        }

        private static Dictionary<string, int> LoadIdentityEmblems()
        {
            TextAsset asset = Resources.Load<TextAsset>("TeamEmblems/TeamEmblemIdentities");
            if (asset == null)
                throw new InvalidOperationException("구단 엠블럼 카탈로그가 없습니다.");
            IdentityCatalog catalog = JsonUtility.FromJson<IdentityCatalog>(asset.text);
            if (catalog?.entries == null)
                throw new InvalidOperationException("구단 엠블럼 카탈로그 항목이 없습니다.");
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (IdentityEntry entry in catalog.entries)
            {
                if (entry == null || entry.emblemId <= 0 || entry.emblemId > EmblemCount ||
                    entry.names == null || entry.names.Length == 0)
                    throw new InvalidOperationException("구단 엠블럼 카탈로그 항목이 올바르지 않습니다.");
                foreach (string name in entry.names)
                {
                    if (string.IsNullOrWhiteSpace(name) || result.ContainsKey(name))
                        throw new InvalidOperationException("구단 엠블럼 별칭이 비어 있거나 중복됩니다.");
                    result.Add(name, entry.emblemId);
                }
            }
            return result;
        }

        /// <summary>구단 이름에 등록된 이미지를 우선 적용하고 미등록 구단은 지정 ID를 유지한다.</summary>
        public static bool TryApply(Image image, int emblemId, string teamName)
        {
            return TryApply(image, ResolveEmblemId(teamName, emblemId));
        }

        /// <summary>유효한 엠블럼을 찾으면 Image에 적용하고 true를 반환한다.</summary>
        public static bool TryApply(Image image, int emblemId)
        {
            if (image == null)
                return false;
            Sprite sprite = Get(emblemId);
            if (sprite == null)
            {
                image.sprite = null;
                image.color = Color.clear;
                return false;
            }

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

            if (spriteIndex >= LegacyEmblemCount)
                return GetIdentitySprite(spriteIndex);

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

        private static Sprite GetIdentitySprite(int spriteIndex)
        {
            _identityAtlas ??= Resources.Load<Texture2D>("TeamEmblems/TeamEmblems_Identity_01");
            if (_identityAtlas == null)
                return null;
            int cellIndex = spriteIndex - LegacyEmblemCount;
            float width = _identityAtlas.width / 4f;
            // 추가 아틀라스의 실제 alpha 행 범위로 잘라 기존 엠블럼과 표시 크기를 맞춘다.
            float scale = _identityAtlas.height / (float)SourceSize;
            int top = Mathf.RoundToInt((cellIndex < 4 ? 116 : 647) * scale);
            int bottom = Mathf.RoundToInt((cellIndex < 4 ? 562 : 1071) * scale);
            var rect = new Rect(cellIndex % 4 * width, _identityAtlas.height - bottom - 1,
                width, bottom - top + 1);
            Sprite sprite = Sprite.Create(_identityAtlas, rect, new Vector2(.5f, .5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = $"TeamEmblem_{spriteIndex + 1:000}";
            Sprites[spriteIndex] = sprite;
            return sprite;
        }
    }
}

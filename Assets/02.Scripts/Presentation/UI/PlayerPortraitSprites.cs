using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 선수 인물별 고정 초상을 Resources에서 불러와 캐시한다.
    /// 프리팹 없이 런타임 생성되는 화면들이므로 인스펙터 직렬화 대신 Resources.Load를 쓴다.
    /// </summary>
    internal static class PlayerPortraitSprites
    {
        private const string RepresentativePath = "UI/Portraits/img_player_representative";
        private const string PitcherPath = "UI/Portraits/img_pitcher_default";
        private const string HitterPath = "UI/Portraits/img_hitter_default";

        private static Sprite _representative;
        private static Sprite _pitcher;
        private static Sprite _hitter;
        private static Dictionary<string, int> _appearances;
        private static Sprite[] _portraits;
        private static Dictionary<string, string> _seasonUniforms;
        private static Dictionary<string, string> _franchiseUniforms;
        private static readonly Dictionary<string, Sprite> UniformPortraits = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        [Serializable]
        private sealed class UniformCatalog
        {
            public UniformLineage[] lineages;
            public UniformSeason[] seasons;
        }

        [Serializable]
        private sealed class UniformLineage
        {
            public string uniform;
            public string[] franchises;
        }

        [Serializable]
        private sealed class UniformSeason
        {
            public string id;
            public string franchise;
        }

        [Serializable]
        private sealed class AssignmentCatalog
        {
            public int portraitCount;
            public PersonAssignment[] persons;
            public SeasonAlias[] aliases;
        }

        [Serializable]
        private sealed class PersonAssignment
        {
            public string id;
            public int appearance;
        }

        [Serializable]
        private sealed class SeasonAlias
        {
            public string id;
            public string person;
        }

        /// <summary>얼굴은 인물별로 유지하고 시즌·카드 ID에는 원본 구단 계보의 유니폼을 적용한다.</summary>
        public static Sprite GetAssigned(string playerId)
        {
            EnsureAssignments();
            if (string.IsNullOrEmpty(playerId)) return null;
            if (int.TryParse(playerId, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int careerPlayerId))
                return GetForPlayer(careerPlayerId, PlayerPosition.DesignatedHitter);
            if (!_appearances.TryGetValue(playerId, out int appearance))
            {
                // 모든 카드 등급은 시즌 ID 뒤에 붙으며 얼굴의 정체성을 바꾸지 않는다.
                int separator = playerId.IndexOf(':');
                if (separator < 0) return null;
                playerId = playerId.Substring(0, separator);
                if (!_appearances.TryGetValue(playerId, out appearance))
                    return null;
            }
            EnsureUniforms();
            if (_seasonUniforms.TryGetValue(playerId, out string uniform))
            {
                string key = $"{uniform}/face-{appearance:D4}";
                if (!UniformPortraits.TryGetValue(key, out Sprite portrait))
                {
                    portrait = LoadSprite("UI/Portraits/Uniforms/" + key);
                    if (portrait == null)
                        throw new InvalidOperationException("발급된 선수 유니폼 이미지가 없습니다: " + key);
                    UniformPortraits.Add(key, portrait);
                }
                return portrait;
            }
            return LoadAppearance(appearance);
        }

        private static void EnsureUniforms()
        {
            if (_seasonUniforms != null) return;
            EnsureAssignments();
            TextAsset asset = Resources.Load<TextAsset>("UI/Portraits/player_uniform_assignments");
            if (asset == null) throw new InvalidOperationException("선수 유니폼 발급 카탈로그가 없습니다.");
            UniformCatalog catalog = JsonUtility.FromJson<UniformCatalog>(asset.text);
            var franchises = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UniformLineage lineage in catalog.lineages)
                foreach (string franchise in lineage.franchises)
                    franchises.Add(franchise, lineage.uniform);
            var seasons = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UniformSeason season in catalog.seasons)
            {
                if (!_appearances.ContainsKey(season.id))
                    throw new InvalidOperationException("얼굴이 발급되지 않은 시즌의 유니폼입니다: " + season.id);
                seasons.Add(season.id, franchises[season.franchise]);
            }
            _seasonUniforms = seasons;
            _franchiseUniforms = franchises;
        }

        /// <summary>카드 초상과 경기 의상이 동일한 구단 계보 발급표를 사용한다.</summary>
        public static string GetUniformForFranchise(string franchiseId)
        {
            EnsureUniforms();
            return !string.IsNullOrEmpty(franchiseId) && _franchiseUniforms.TryGetValue(franchiseId, out string uniform)
                ? uniform : null;
        }

        /// <summary>역사 선수는 발급 정본, 생성 선수는 안정 ID의 고정 해시로 초상을 조회한다.</summary>
        public static Sprite GetForPlayer(int playerId, PlayerPosition position)
        {
            return GetForPlayer("career:" + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture), position);
        }

        /// <summary>역사 인물 ID를 우선 사용하고 콘텐츠 밖의 생성 선수도 고정 외형으로 표시한다.</summary>
        public static Sprite GetForPlayer(string playerId, PlayerPosition position)
        {
            Sprite assigned = GetAssigned(playerId);
            if (assigned != null) return assigned;
            if (string.IsNullOrEmpty(playerId) || _portraits.Length == 0) return GetDefault(position);
            // 경기 RNG를 소비하지 않고 프로세스가 달라져도 같은 외형을 유지한다.
            uint hash = 2166136261;
            unchecked
            {
                for (int i = 0; i < playerId.Length; i++) hash = (hash ^ playerId[i]) * 16777619;
            }
            return LoadAppearance((int)(hash % (uint)_portraits.Length) + 1) ?? GetDefault(position);
        }

        private static Sprite LoadAppearance(int appearance)
        {
            int index = appearance - 1;
            return _portraits[index] ??= LoadSprite($"UI/Portraits/Players/face-{appearance:D4}");
        }

        private static void EnsureAssignments()
        {
            if (_appearances != null) return;
            var appearances = new Dictionary<string, int>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>("UI/Portraits/player_portrait_assignments");
            if (asset == null)
            {
                _portraits = Array.Empty<Sprite>();
                _appearances = appearances;
                return;
            }
            AssignmentCatalog catalog = JsonUtility.FromJson<AssignmentCatalog>(asset.text);
            foreach (PersonAssignment person in catalog.persons)
            {
                if (person.appearance < 1 || person.appearance > catalog.portraitCount)
                    throw new InvalidOperationException("선수 초상 발급 범위를 벗어났습니다.");
                appearances.Add(person.id, person.appearance);
            }
            foreach (SeasonAlias alias in catalog.aliases)
                appearances.Add(alias.id, appearances[alias.person]);
            _portraits = new Sprite[catalog.portraitCount];
            _appearances = appearances;
        }

        /// <summary>선수 ID가 없는 생성 미리보기나 누락된 자산에 대표 초상을 사용한다.</summary>
        public static Sprite GetDefault(PlayerPosition position)
        {
            _representative ??= LoadSprite(RepresentativePath);
            if (_representative != null)
                return _representative;

            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return isPitcher
                ? _pitcher ??= LoadSprite(PitcherPath)
                : _hitter ??= LoadSprite(HitterPath);
        }

        /// <summary>
        /// 텍스처 임포트 설정이 Multiple 스프라이트 모드로 자동 슬라이스되면
        /// 안티에일리어싱 경계의 작은 조각들이 함께 생기므로, 가장 넓은 조각을 실제 인물로 간주한다.
        /// </summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            Sprite[] subSprites = Resources.LoadAll<Sprite>(path);
            if (subSprites.Length == 0)
                return null;

            Sprite largest = subSprites[0];
            for (int i = 1; i < subSprites.Length; i++)
            {
                if (subSprites[i].rect.width * subSprites[i].rect.height
                    > largest.rect.width * largest.rect.height)
                {
                    largest = subSprites[i];
                }
            }
            return largest;
        }
    }
}

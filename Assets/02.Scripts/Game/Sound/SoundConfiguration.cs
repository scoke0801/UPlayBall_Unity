using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Baseball.Game.Sound
{
    /// <summary>
    /// 사운드 시스템이 런타임에 읽는 유일한 정적 정의다.
    /// SoundManager가 Resources에서 이 Asset 하나만 읽고, 믹서와 플레이리스트는 여기서 따라간다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundConfiguration", menuName = "Baseball/사운드/사운드 설정")]
    public sealed class SoundConfiguration : ScriptableObject
    {
        /// <summary>SoundManager가 이 Asset을 찾는 Resources 경로다.</summary>
        public const string ResourcePath = "Sound/SoundConfiguration";

        [Serializable]
        public struct SituationRoute
        {
            public BgmSituation situation;
            public BgmPlaylistDefinition playlist;
        }

        [Tooltip("Master / BGM / SFX / UI 버스를 가진 AudioMixer. 그룹 이름으로 자동 매핑한다.")]
        [SerializeField] private AudioMixer _mixer;

        [Tooltip("게임 국면별 BGM. 같은 국면이 여러 번 등록되면 첫 항목을 쓴다.")]
        [SerializeField] private List<SituationRoute> _routes = new List<SituationRoute>();

        [Tooltip("국면 전환 시 기본 크로스페이드 시간(초). 플레이리스트가 값을 가지면 그쪽이 우선한다.")]
        [Min(0f)][SerializeField] private float _situationFadeSeconds = 1.5f;

        public AudioMixer Mixer => _mixer;
        public float SituationFadeSeconds => Mathf.Max(0f, _situationFadeSeconds);

        public bool TryGetPlaylist(BgmSituation situation, out BgmPlaylistDefinition playlist)
        {
            for (int index = 0; index < _routes.Count; index++)
            {
                if (_routes[index].situation != situation)
                    continue;

                playlist = _routes[index].playlist;
                return playlist != null;
            }

            playlist = null;
            return false;
        }
    }
}

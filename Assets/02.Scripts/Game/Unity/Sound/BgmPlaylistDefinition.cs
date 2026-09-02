using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Game.Sound
{
    /// <summary>
    /// 한 국면에서 재생할 BGM 묶음이다.
    /// 곡이 하나면 그 곡을 무한 반복하고, 둘 이상이면 한 곡을 끝까지 재생한 뒤
    /// 곡 사이에 짧은 무음을 두고 다음 곡으로 넘어간다.
    /// </summary>
    [CreateAssetMenu(fileName = "BgmPlaylist_", menuName = "Baseball/사운드/BGM 플레이리스트")]
    public sealed class BgmPlaylistDefinition : ScriptableObject
    {
        /// <summary>여러 곡을 이어 재생할 때의 곡 선택 방식이다.</summary>
        public enum PlaybackMode
        {
            Sequential = 0,
            Shuffle = 1
        }

        [Tooltip("재생할 BGM 클립. 순서대로 또는 무작위로 이어 재생한다.")]
        [SerializeField] private List<AudioClip> _tracks = new List<AudioClip>();

        [SerializeField] private PlaybackMode _mode = PlaybackMode.Shuffle;

        [Tooltip("이 플레이리스트의 재생 음량. 곡별 녹음 레벨 차이를 보정한다.")]
        [Range(0f, 1f)][SerializeField] private float _volume = 1f;

        [Tooltip("곡을 전환할 때의 크로스페이드 시간(초).")]
        [Min(0f)][SerializeField] private float _fadeSeconds = 1.5f;

        [Header("곡 사이 무음 (곡이 2개 이상일 때만 적용)")]
        [Min(0f)][SerializeField] private float _gapMinSeconds;
        [Min(0f)][SerializeField] private float _gapMaxSeconds = 3f;

        public int TrackCount => _tracks?.Count ?? 0;
        public float Volume => Mathf.Clamp01(_volume);
        public float FadeSeconds => Mathf.Max(0f, _fadeSeconds);

        /// <summary>곡이 하나뿐이면 무음 구간 없이 그 곡을 계속 반복해야 한다.</summary>
        public bool IsSingleLoopingTrack => TrackCount == 1;

        public AudioClip GetTrack(int index)
        {
            if (_tracks == null || index < 0 || index >= _tracks.Count)
                return null;

            return _tracks[index];
        }

        /// <summary>곡 사이에 둘 무음 시간을 정한다. 최소·최대가 같으면 고정 간격이다.</summary>
        public float GetGapSeconds()
        {
            float min = Mathf.Max(0f, Mathf.Min(_gapMinSeconds, _gapMaxSeconds));
            float max = Mathf.Max(0f, Mathf.Max(_gapMinSeconds, _gapMaxSeconds));
            return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
        }

        /// <summary>
        /// 다음에 재생할 곡의 인덱스를 반환한다. current가 음수면 첫 곡을 고르는 경우다.
        /// 첫 곡은 모드와 무관하게 무작위로 골라, 로비에 들어올 때마다 같은 곡으로 시작하지 않게 한다.
        /// </summary>
        public int GetNextIndex(int current)
        {
            int count = TrackCount;
            if (count <= 0)
                return -1;

            if (count == 1)
                return 0;

            if (current < 0)
                return Random.Range(0, count);

            if (_mode == PlaybackMode.Shuffle)
            {
                // 직전 곡을 제외한 범위에서 고른다(같은 곡이 연달아 나오는 것을 막는다).
                return (current + Random.Range(1, count)) % count;
            }

            return (current + 1) % count;
        }
    }
}

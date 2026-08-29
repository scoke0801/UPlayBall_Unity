using Baseball.Game.Manager;
using UnityEngine;
using UnityEngine.Audio;

namespace Baseball.Game.Sound
{
    /// <summary>
    /// BGM 재생을 소유한다. 호출부는 "지금 어떤 국면인가"만 알려주고,
    /// 어떤 곡을 어떻게 잇고 언제 페이드할지는 전부 이 매니저가 결정한다.
    /// </summary>
    public sealed class SoundManager : ManagerBehaviour<SoundManager>, IUpdatableManager
    {
        /// <summary>AudioMixer에 노출된 볼륨 파라미터 이름이다. 믹서 Asset과 문자열이 일치해야 한다.</summary>
        public const string MasterVolumeParameter = "MasterVolume";
        public const string BgmVolumeParameter = "BGMVolume";

        private const string BgmMixerGroupName = "BGM";

        /// <summary>선형 볼륨 0을 dB로 바꾸면 음의 무한대가 되므로, 믹서가 정의한 최소값으로 대체한다.</summary>
        private const float MinimumDecibel = -80f;

        /// <summary>클립 길이 그대로 기다리면 마지막 프레임에 한 번 무음이 새므로 살짝 앞당겨 넘긴다.</summary>
        private const float TrackEndMargin = 0.05f;

        private enum BgmPhase
        {
            Idle,
            Playing,
            Gap
        }

        private SoundConfiguration _configuration;
        private AudioMixerGroup _bgmGroup;

        private AudioSource _sourceA;
        private AudioSource _sourceB;

        /// <summary>가장 최근에 재생을 시작한 소스. 다음 곡은 반대쪽 소스로 넘어간다.</summary>
        private AudioSource _currentSource;

        /// <summary>볼륨을 올리는 중인 소스. 정지 페이드 중에는 null이다.</summary>
        private AudioSource _fadeInSource;

        /// <summary>볼륨을 내리는 중인 소스. 첫 곡을 시작할 때는 null이다.</summary>
        private AudioSource _fadeOutSource;

        private float _fadeDuration;
        private float _fadeElapsed;
        private float _fadeOutStartVolume;
        private float _fadeInTargetVolume;
        private bool _isFading;

        private BgmPlaylistDefinition _playlist;
        private BgmSituation? _situation;
        private BgmPhase _phase = BgmPhase.Idle;
        private int _trackIndex = -1;
        private float _trackRemainingSeconds;
        private float _gapRemainingSeconds;

        public override int InitializationOrder => -40;

        /// <summary>현재 재생 중인 국면. 아직 아무것도 재생하지 않았으면 null이다.</summary>
        public BgmSituation? CurrentSituation => _situation;

        protected override void OnInitialize()
        {
            _configuration = Resources.Load<SoundConfiguration>(SoundConfiguration.ResourcePath);
            if (_configuration == null)
            {
                Debug.LogWarning(
                    $"[SoundManager] '{SoundConfiguration.ResourcePath}' 설정 Asset을 찾을 수 없어 BGM이 재생되지 않습니다.");
                return;
            }

            ResolveMixerGroup();
            CreateBgmSources();
        }

        protected override void OnShutdown()
        {
            StopImmediately();
            _configuration = null;
            _bgmGroup = null;
        }

        public void Tick(float deltaTime)
        {
            UpdateFade(deltaTime);
            UpdatePlaylist(deltaTime);
        }

        /// <summary>
        /// 지정한 국면의 BGM을 재생한다. 이미 같은 국면이면 아무 것도 하지 않아 곡이 끊기지 않는다.
        /// 국면에 연결된 플레이리스트가 없으면 BGM을 정지한다.
        /// </summary>
        public void PlaySituation(BgmSituation situation)
        {
            if (_configuration == null || _sourceA == null)
                return;

            if (_situation == situation)
                return;

            if (!_configuration.TryGetPlaylist(situation, out BgmPlaylistDefinition playlist))
            {
                Debug.LogWarning($"[SoundManager] '{situation}' 국면에 연결된 BGM 플레이리스트가 없습니다.");
                // StopBgm이 국면을 지우므로 그 뒤에 기록해, 같은 국면에서 경고가 반복되지 않게 한다.
                StopBgm();
                _situation = situation;
                return;
            }

            _situation = situation;
            _playlist = playlist;
            _trackIndex = -1;
            AdvanceTrack(_configuration.SituationFadeSeconds);
        }

        /// <summary>BGM을 페이드아웃하며 정지한다. 이후 PlaySituation을 호출하면 다시 시작한다.</summary>
        public void StopBgm(float? fadeSeconds = null)
        {
            if (_sourceA == null)
                return;

            float fade = fadeSeconds ?? (_configuration != null ? _configuration.SituationFadeSeconds : 1f);

            _playlist = null;
            _situation = null;
            _phase = BgmPhase.Idle;
            _trackIndex = -1;

            StartFade(null, 0f, fade);
        }

        /// <summary>전체 볼륨을 0~1 선형 값으로 설정한다.</summary>
        public void SetMasterVolume(float linearVolume) => SetMixerVolume(MasterVolumeParameter, linearVolume);

        /// <summary>BGM 볼륨을 0~1 선형 값으로 설정한다.</summary>
        public void SetBgmVolume(float linearVolume) => SetMixerVolume(BgmVolumeParameter, linearVolume);

        private void SetMixerVolume(string exposedParameter, float linearVolume)
        {
            if (_configuration == null || _configuration.Mixer == null)
                return;

            float clamped = Mathf.Clamp01(linearVolume);
            float decibel = clamped <= 0.0001f ? MinimumDecibel : Mathf.Log10(clamped) * 20f;
            _configuration.Mixer.SetFloat(exposedParameter, decibel);
        }

        private void ResolveMixerGroup()
        {
            AudioMixer mixer = _configuration.Mixer;
            if (mixer == null)
            {
                Debug.LogWarning("[SoundManager] AudioMixer가 비어 있어 BGM이 기본 출력으로 재생됩니다.");
                return;
            }

            // FindMatchingGroups는 경로 부분 일치라 여러 개가 나올 수 있으므로 정확한 이름을 우선한다.
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(BgmMixerGroupName);
            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarning($"[SoundManager] AudioMixer에 '{BgmMixerGroupName}' 그룹이 없습니다.");
                return;
            }

            for (int index = 0; index < groups.Length; index++)
            {
                if (groups[index] != null && groups[index].name == BgmMixerGroupName)
                {
                    _bgmGroup = groups[index];
                    return;
                }
            }

            _bgmGroup = groups[0];
        }

        private void CreateBgmSources()
        {
            _sourceA = CreateBgmSource("BGM Source A");
            _sourceB = CreateBgmSource("BGM Source B");
        }

        private AudioSource CreateBgmSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 0;
            source.outputAudioMixerGroup = _bgmGroup;
            // 게임이 일시정지(AudioListener.pause)돼도 BGM은 계속 흐르는 편이 자연스럽다.
            source.ignoreListenerPause = true;
            return source;
        }

        /// <summary>플레이리스트의 다음 곡으로 넘어간다.</summary>
        private void AdvanceTrack(float fadeSeconds)
        {
            if (_playlist == null)
                return;

            _trackIndex = _playlist.GetNextIndex(_trackIndex);
            AudioClip clip = _playlist.GetTrack(_trackIndex);
            if (clip == null)
            {
                // 클립이 비어 있으면 즉시 다음 곡으로 넘어가는 대신 잠깐 쉬어 무한 루프를 막는다.
                Debug.LogWarning($"[SoundManager] 플레이리스트 '{_playlist.name}'의 {_trackIndex}번 클립이 비어 있습니다.");
                _phase = BgmPhase.Gap;
                _gapRemainingSeconds = 1f;
                return;
            }

            AudioSource next = _currentSource == _sourceA ? _sourceB : _sourceA;
            next.clip = clip;
            // 곡이 하나뿐인 플레이리스트는 AudioSource 자체 루프로 재생해 이음매를 없앤다.
            next.loop = _playlist.IsSingleLoopingTrack;
            next.outputAudioMixerGroup = _bgmGroup;
            next.volume = 0f;
            next.Play();

            StartFade(next, _playlist.Volume, fadeSeconds);

            _phase = BgmPhase.Playing;
            _trackRemainingSeconds = next.loop
                ? float.PositiveInfinity
                : Mathf.Max(0f, clip.length - TrackEndMargin);
        }

        /// <summary>
        /// next를 targetVolume까지 올리고 직전 소스를 0까지 내린다.
        /// next가 null이면 정지 목적의 페이드아웃이다.
        /// </summary>
        private void StartFade(AudioSource next, float targetVolume, float fadeSeconds)
        {
            // 이전 페이드가 끝나기 전에 다시 전환되면 그때까지 내려가던 소스를 즉시 정리한다.
            // 소스가 A/B 두 개뿐이라 정리하지 않으면 곧 재사용되며 재생 중인 곡이 끊긴다.
            if (_fadeOutSource != null && _fadeOutSource != next)
                StopSource(_fadeOutSource);

            _fadeOutSource = _currentSource != null && _currentSource != next && _currentSource.isPlaying
                ? _currentSource
                : null;

            _fadeInSource = next;
            _fadeOutStartVolume = _fadeOutSource != null ? _fadeOutSource.volume : 0f;
            _fadeInTargetVolume = Mathf.Clamp01(targetVolume);
            _fadeDuration = Mathf.Max(0f, fadeSeconds);
            _fadeElapsed = 0f;
            _isFading = true;

            if (next != null)
                _currentSource = next;

            if (_fadeDuration <= 0f)
                CompleteFade();
        }

        private void UpdateFade(float deltaTime)
        {
            if (!_isFading)
                return;

            _fadeElapsed += deltaTime;
            float progress = Mathf.Clamp01(_fadeElapsed / _fadeDuration);

            if (_fadeOutSource != null)
                _fadeOutSource.volume = Mathf.Lerp(_fadeOutStartVolume, 0f, progress);

            if (_fadeInSource != null)
                _fadeInSource.volume = Mathf.Lerp(0f, _fadeInTargetVolume, progress);

            if (progress >= 1f)
                CompleteFade();
        }

        private void CompleteFade()
        {
            if (_fadeOutSource != null)
            {
                StopSource(_fadeOutSource);
                _fadeOutSource = null;
            }

            if (_fadeInSource != null)
                _fadeInSource.volume = _fadeInTargetVolume;

            _fadeInSource = null;
            _isFading = false;
        }

        private void UpdatePlaylist(float deltaTime)
        {
            if (_playlist == null)
                return;

            switch (_phase)
            {
                case BgmPhase.Playing:
                    _trackRemainingSeconds -= deltaTime;
                    if (_trackRemainingSeconds > 0f)
                        return;

                    float gapSeconds = _playlist.GetGapSeconds();
                    if (gapSeconds > 0f)
                    {
                        _phase = BgmPhase.Gap;
                        _gapRemainingSeconds = gapSeconds;
                        return;
                    }

                    AdvanceTrack(_playlist.FadeSeconds);
                    return;

                case BgmPhase.Gap:
                    _gapRemainingSeconds -= deltaTime;
                    if (_gapRemainingSeconds <= 0f)
                        AdvanceTrack(_playlist.FadeSeconds);
                    return;
            }
        }

        private void StopImmediately()
        {
            _isFading = false;
            _phase = BgmPhase.Idle;
            _playlist = null;
            _situation = null;
            _trackIndex = -1;
            _fadeInSource = null;
            _fadeOutSource = null;

            StopSource(_sourceA);
            StopSource(_sourceB);
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.volume = 0f;
            source.clip = null;
            source.loop = false;
        }
    }
}

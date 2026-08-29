using UnityEngine;

namespace Baseball.Presentation.Career
{
    public enum CareerPresentationType
    {
        RegularSeasonFirst,
        GoldenGlove,
        RegularSeasonMvp,
        PostseasonChampion,
        PostseasonMvp,
        Training,
        OverseasTraining,
        Rest
    }

    public enum CareerMotionPreset
    {
        RankUp,
        Award,
        Championship,
        Training,
        Travel,
        Rest
    }

    /// <summary>커리어 챕터 컷의 이미지와 모션·재생 정책을 읽기 전용 정의로 보관한다.</summary>
    [CreateAssetMenu(menuName = "Baseball/Career Presentation", fileName = "CareerPresentationData")]
    public sealed class CareerPresentationData : ScriptableObject
    {
        [SerializeField] private CareerPresentationType _type;
        [SerializeField] private Sprite _illustration;
        [SerializeField] private CareerMotionPreset _motionPreset;
        [SerializeField] private string _categoryLocalizationKey = string.Empty;
        [SerializeField] private string _titleLocalizationKey = string.Empty;
        [SerializeField] private string _descriptionLocalizationKey = string.Empty;
        [SerializeField] private AudioClip _stinger;
        [SerializeField] private GameObject _particlePrefab;
        [SerializeField] private float _minimumViewTime = 1f;
        [SerializeField] private float _defaultDuration = 4.5f;
        [SerializeField] private bool _allowSkip = true;
        [SerializeField] private bool _replayOncePerSeason = true;

        public CareerPresentationType Type => _type;
        public Sprite Illustration => _illustration;
        public CareerMotionPreset MotionPreset => _motionPreset;
        public string CategoryLocalizationKey => _categoryLocalizationKey;
        public string TitleLocalizationKey => _titleLocalizationKey;
        public string DescriptionLocalizationKey => _descriptionLocalizationKey;
        public AudioClip Stinger => _stinger;
        public GameObject ParticlePrefab => _particlePrefab;
        public float MinimumViewTime => Mathf.Max(0f, _minimumViewTime);
        public float DefaultDuration => Mathf.Max(MinimumViewTime, _defaultDuration);
        public bool AllowSkip => _allowSkip;
        public bool ReplayOncePerSeason => _replayOncePerSeason;
    }
}

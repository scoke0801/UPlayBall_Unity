using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>구단주 모드 Production 새 게임이 사용할 직렬화된 초기 값의 순수 값 계약이다.</summary>
    public readonly struct OwnerModeNewGameConfiguration
    {
        private readonly TacticCardDefinition[] _starterTacticCards;

        public OwnerModeNewGameConfiguration(
            ulong worldSeed,
            int originYear,
            string playerTeamSeasonKey,
            string leagueInstanceId,
            long initialMoney,
            int initialScoutingPoints,
            int initialDevelopmentPoints,
            IReadOnlyList<TacticCardDefinition> starterTacticCards)
        {
            if (worldSeed == 0UL) throw new ArgumentOutOfRangeException(nameof(worldSeed));
            if (originYear <= 0) throw new ArgumentOutOfRangeException(nameof(originYear));
            if (string.IsNullOrWhiteSpace(leagueInstanceId))
                throw new ArgumentException("LeagueInstanceId가 필요합니다.", nameof(leagueInstanceId));
            if (initialMoney < 0L || initialScoutingPoints < 0 || initialDevelopmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(initialMoney));

            WorldSeed = worldSeed;
            OriginYear = originYear;
            PlayerTeamSeasonKey = string.IsNullOrWhiteSpace(playerTeamSeasonKey)
                ? string.Empty
                : playerTeamSeasonKey.Trim();
            LeagueInstanceId = leagueInstanceId.Trim();
            InitialMoney = initialMoney;
            InitialScoutingPoints = initialScoutingPoints;
            InitialDevelopmentPoints = initialDevelopmentPoints;
            if (starterTacticCards == null || starterTacticCards.Count != LineupPresetState.MaximumTacticCardCount)
                throw new ArgumentException("Starter Tactic은 정확히 두 장이어야 합니다.", nameof(starterTacticCards));
            _starterTacticCards = new TacticCardDefinition[starterTacticCards.Count];
            for (int index = 0; index < _starterTacticCards.Length; index++)
            {
                _starterTacticCards[index] = starterTacticCards[index] ??
                    throw new ArgumentException("null Starter Tactic이 있습니다.", nameof(starterTacticCards));
                for (int previous = 0; previous < index; previous++)
                    if (string.Equals(_starterTacticCards[previous].CardId, _starterTacticCards[index].CardId, StringComparison.Ordinal))
                        throw new ArgumentException("Starter Tactic CardId는 중복될 수 없습니다.", nameof(starterTacticCards));
            }
        }

        public ulong WorldSeed { get; }
        public int OriginYear { get; }
        public string PlayerTeamSeasonKey { get; }
        public string LeagueInstanceId { get; }
        public long InitialMoney { get; }
        public int InitialScoutingPoints { get; }
        public int InitialDevelopmentPoints { get; }
        public IReadOnlyList<TacticCardDefinition> StarterTacticCards => _starterTacticCards;
    }

    /// <summary>
    /// 새 게임의 구단 후보와 조정 가능한 생성 계수를 보관하는 읽기 전용 정적 정의다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGameDefinition", menuName = "Baseball/Data/New Game Definition")]
    public sealed class NewGameDefinition : ScriptableObject
    {
        private const string ResourcePath = "NewGame/NewGameDefinition";

        [Serializable]
        private struct OwnerTacticTriggerData
        {
            [SerializeField] private TacticTriggerField _field;
            [SerializeField] private TacticComparison _comparison;
            [SerializeField] private int _value;
            [SerializeField] private int _maximumValue;

            public TacticTriggerCondition ToDefinition()
            {
                return new TacticTriggerCondition(_field, _comparison, _value, _maximumValue);
            }
        }

        [Serializable]
        private struct OwnerTacticStatModifierData
        {
            [SerializeField] private PlayerAbility _ability;
            [SerializeField] private int _amount;

            public TacticStatModifier ToDefinition() => new TacticStatModifier(_ability, _amount);
        }

        [Serializable]
        private struct OwnerStarterTacticData
        {
            [SerializeField] private string _cardId;
            [SerializeField] private string _name;
            [SerializeField] private TacticCardCategory _category;
            [SerializeField] private TacticTier _tier;
            [SerializeField] private string _referenceBehavior;
            [SerializeField] private string _projectBalanceValue;
            [SerializeField] private OwnerTacticTriggerData[] _triggers;
            [SerializeField] private TacticTargetRule _target;
            [SerializeField] private OwnerTacticStatModifierData[] _statModifiers;
            [SerializeField] private TacticDurationRule _duration;
            [SerializeField] private bool _isDisruption;

            public TacticCardDefinition ToDefinition()
            {
                var triggers = new TacticTriggerCondition[_triggers?.Length ?? 0];
                for (int index = 0; index < triggers.Length; index++)
                    triggers[index] = _triggers[index].ToDefinition();
                var modifiers = new TacticStatModifier[_statModifiers?.Length ?? 0];
                for (int index = 0; index < modifiers.Length; index++)
                    modifiers[index] = _statModifiers[index].ToDefinition();
                return new TacticCardDefinition(
                    _cardId,
                    _name,
                    _category,
                    _tier,
                    _referenceBehavior,
                    _projectBalanceValue,
                    triggers,
                    _target,
                    modifiers,
                    Array.Empty<TacticBehaviorModifier>(),
                    _duration,
                    Array.Empty<string>(),
                    _isDisruption);
            }
        }

        [Header("Historical Runtime Content")]
        [SerializeField] private HistoricalRuntimeContentCatalog _historicalContentCatalog;
        [SerializeField, Tooltip("비워 두면 새 게임마다 44시즌을 실제로 시뮬레이션한다. 툴 런처의 World History Bake로 채운다.")]
        private BakedWorldHistoryCatalog _bakedWorldHistoryCatalog;
        [SerializeField, Tooltip(
            "커리어 새 게임이 고를 월드 Seed 후보다. 비워 두면 시작할 때마다 임의 Seed를 뽑으므로 Bake가 적중하지 않는다. " +
            "여기에 넣은 Seed만큼 서로 다른 월드가 만들어지고, 그 전부를 미리 구울 수 있다.")]
        private long[] _careerWorldSeedPool = Array.Empty<long>();
        [SerializeField] private int[] _historicalLeagueSeasonYears =
        {
            2016, 2017, 2018, 2019, 2020,
            2021, 2022, 2023, 2024, 2025
        };

        [Serializable]
        private struct TeamIdentityData
        {
            [SerializeField] private string _name;
            [SerializeField] private Color32 _primaryColor;

            public TeamIdentityData(string name, byte red, byte green, byte blue)
            {
                _name = name;
                _primaryColor = new Color32(red, green, blue, byte.MaxValue);
            }

            public TeamIdentityDefinition ToDefinition()
            {
                return new TeamIdentityDefinition(
                    _name,
                    new TeamColor(_primaryColor.r, _primaryColor.g, _primaryColor.b));
            }
        }

        [Serializable]
        private struct TeamArchetypeData
        {
            [SerializeField] private TeamArchetype _archetype;
            [SerializeField, Range(0, 100)] private int _budget;
            [SerializeField, Range(0, 100)] private int _development;
            [SerializeField, Range(0, 100)] private int _rosterDepth;
            [SerializeField, Range(0, 100)] private int _scouting;

            public TeamArchetypeData(
                TeamArchetype archetype,
                int budget,
                int development,
                int rosterDepth,
                int scouting)
            {
                _archetype = archetype;
                _budget = budget;
                _development = development;
                _rosterDepth = rosterDepth;
                _scouting = scouting;
            }

            public TeamArchetypeProfile ToProfile()
            {
                return new TeamArchetypeProfile(
                    _archetype,
                    _budget,
                    _development,
                    _rosterDepth,
                    _scouting);
            }
        }

        [Serializable]
        private struct LineupScoreWeightData
        {
            [SerializeField, Range(0f, 1f)] private double _contact;
            [SerializeField, Range(0f, 1f)] private double _power;
            [SerializeField, Range(0f, 1f)] private double _speed;
            [SerializeField, Range(0f, 1f)] private double _mental;

            public LineupScoreWeightData(double contact, double power, double speed, double mental)
            {
                _contact = contact;
                _power = power;
                _speed = speed;
                _mental = mental;
            }

            public BattingOrderScoreWeights ToBalance()
            {
                return new BattingOrderScoreWeights(_contact, _power, _speed, _mental);
            }
        }

        [Header("League")]
        [SerializeField, Min(1)] private int _teamCount = 8;
        [SerializeField, Min(1)] private int _firstSeasonYear = 2028;
        [SerializeField, Range(16, 25)] private int _startingAge = 18;
        [SerializeField, Range(1, 128)] private int _teamEmblemCount = 128;
        [SerializeField] private GrowthBalanceAsset _growthBalance;
        [SerializeField] private TextAsset _ownerExpansionBalanceConfig;

        [Header("Owner Mode New Game")]
        [SerializeField, Min(1)] private long _ownerWorldSeed = 20_260_905L;
        [SerializeField, Min(1)] private int _ownerOriginYear = 2024;
        [SerializeField] private string _ownerPlayerTeamSeasonKey = string.Empty;
        [SerializeField] private string _ownerLeagueInstanceId = "OWNER-ROOKIE-01";
        [SerializeField, Min(0)] private long _ownerInitialMoney = 1_000_000_000L;
        [SerializeField, Min(0)] private int _ownerInitialScoutingPoints = 100;
        [SerializeField, Min(0)] private int _ownerInitialDevelopmentPoints = 100;
        [SerializeField] private OwnerStarterTacticData[] _ownerStarterTactics = Array.Empty<OwnerStarterTacticData>();
        [SerializeField] private TeamIdentityData[] _teamIdentities =
        {
            new("서울 블루윙스", 45, 105, 210),
            new("부산 마리너스", 25, 92, 138),
            new("인천 웨이브", 32, 156, 168),
            new("광주 레드폭스", 202, 62, 71),
            new("수원 스타즈", 113, 83, 171),
            new("대전 호크스", 224, 139, 47),
            new("대구 크라운", 195, 166, 52),
            new("창원 블레이즈", 216, 76, 43),
            new("울산 가디언즈", 52, 133, 89),
            new("전주 팔콘스", 103, 119, 138),
            new("제주 돌핀스", 38, 171, 197),
            new("춘천 스톰", 96, 108, 145)
        };
        [SerializeField] private TeamArchetypeData[] _archetypes =
        {
            new(TeamArchetype.Development, 45, 85, 40, 70),
            new(TeamArchetype.Contender, 85, 55, 80, 60),
            new(TeamArchetype.OffenseFocused, 60, 65, 55, 55),
            new(TeamArchetype.PitchingFocused, 60, 65, 55, 55),
            new(TeamArchetype.SmallMarket, 30, 45, 35, 40)
        };
        [SerializeField] private string[] _playerNamePool =
        {
            "김도윤", "이준서", "박시우", "최민재", "정우진", "강현우", "조성민", "윤태호",
            "장민준", "임재현", "한승우", "오지훈", "서동현", "신예준", "권민성", "황준혁",
            "안지환", "송재원", "전성훈", "홍민기", "유건우", "고은찬", "문태윤", "양시현",
            "배준영", "백승현", "허도현", "남시우", "심건호", "노재민", "하윤성", "곽준호"
        };

        [Header("World Generation")]
        [SerializeField, Range(0, 30)] private int _minorOverallBonus = 4;
        [SerializeField, Range(1, 40)] private int _majorOverallBonus = 8;
        [SerializeField] private string _minorTeamNamePrefix = "마이너 ";
        [SerializeField] private string _majorTeamNamePrefix = "메이저 ";
        [SerializeField, Range(16, 40)] private int _rookieMinimumAge = 18;
        [SerializeField, Range(16, 40)] private int _rookieMaximumAge = 24;
        [SerializeField, Range(16, 45)] private int _minorMinimumAge = 20;
        [SerializeField, Range(16, 45)] private int _minorMaximumAge = 29;
        [SerializeField, Range(16, 50)] private int _majorMinimumAge = 23;
        [SerializeField, Range(16, 50)] private int _majorMaximumAge = 35;

        [Header("World Player Lifecycle")]
        [SerializeField, Range(18, 50)] private int _retirementMinimumAge = 34;
        [SerializeField, Range(19, 55)] private int _guaranteedRetirementAge = 43;
        [SerializeField, Range(0f, 1f)] private double _retirementBaseProbability = 0.04d;
        [SerializeField, Range(0f, 0.25f)] private double _retirementAgeWeight = 0.08d;
        [SerializeField, Range(0, 100)] private int _retirementLowAbilityThreshold = 55;
        [SerializeField, Range(0f, 0.1f)] private double _retirementLowAbilityWeight = 0.01d;
        [SerializeField, Range(16, 25)] private int _rookieEntryMinimumAge = 18;
        [SerializeField, Range(16, 25)] private int _rookieEntryMaximumAge = 22;
        [SerializeField, Range(0, 100)] private int _rookieEntryMinimumOverall = 38;
        [SerializeField, Range(0, 100)] private int _rookieEntryMaximumOverall = 58;
        [SerializeField, Min(1)] private long _rookieBaseSalary = 30_000_000L;
        [SerializeField, Min(1)] private long _minorBaseSalary = 90_000_000L;
        [SerializeField, Min(1)] private long _majorBaseSalary = 300_000_000L;
        [SerializeField, Min(1)] private int _rookieAiContractYears = 1;
        [SerializeField, Min(1)] private int _minorAiContractYears = 1;
        [SerializeField, Min(1)] private int _majorAiContractYears = 1;

        [Header("Player League Movement")]
        [SerializeField, Range(0, 20)] private int _upperLeagueOverallPenalty = 2;
        [SerializeField, Range(0f, 1f)] private double _leaguePerformanceWeight = 0.20d;
        [SerializeField, Range(0f, 1f)] private double _leaguePotentialWeight = 0.08d;
        [SerializeField, Min(1)] private int _reliablePromotionPlateAppearances = 300;
        [SerializeField, Min(1)] private int _reliablePromotionPitchingOuts = 300;
        [SerializeField, Range(0f, 100f)] private double _minorMinimumProjectedOverall = 47d;
        [SerializeField, Range(0f, 100f)] private double _majorMinimumProjectedOverall = 60d;
        [SerializeField, Range(0f, 20f)] private double _promotionCompetitorMargin = 15d;
        [SerializeField, Range(0, 100)] private int _promotionMinimumTeamBudget = 35;
        [SerializeField, Min(0.1f)] private double _promotionInterestScoreThreshold = 0.95d;
        [SerializeField, Range(1, 5)] private int _maximumPromotionOffers = 2;
        [SerializeField, Range(1, 5)] private int _maximumRehabilitationOffers = 2;
        [SerializeField, Range(1, 5)] private int _minorPlayerContractYears = 2;
        [SerializeField, Range(1, 5)] private int _majorPlayerContractYears = 3;

        [Header("Character Creation")]
        [SerializeField, Range(0, 100)] private int _careerBaseAttributeValue = 50;
        [SerializeField, Min(0)] private int _careerBatterBonusPoints = 60;
        [SerializeField, Min(0)] private int _careerPitcherBonusPoints = 40;
        [SerializeField, Range(0, 100)] private int _careerMaximumAttributeValue = 75;

        [Header("Contract Offers")]
        [SerializeField, Min(0f)] private double _offerScoreThreshold = 1d;
        [SerializeField, Range(0.5f, 1.5f)] private double _scoutVarianceMinimum = 0.85d;
        [SerializeField, Range(0.5f, 1.5f)] private double _scoutVarianceMaximum = 1.15d;
        [SerializeField, Range(0f, 1f)] private double _preferredPositionBonus = 0.15d;
        [SerializeField, Min(0)] private long _baseSigningBonus = 20_000_000L;
        [SerializeField, Min(0)] private long _baseAnnualSalary = 30_000_000L;
        [SerializeField, Min(1)] private int _minimumOfferCount = 3;
        [SerializeField, Min(1)] private int _maximumOfferCount = 5;
        [SerializeField, Range(0, 100)] private int _startingCompetitionNeed = 55;
        [SerializeField, Range(0, 100)] private int _rosterCompetitionNeed = 40;
        [SerializeField, Min(1f)] private double _ratingBaseline = 50d;
        [SerializeField, Min(1)] private int _contractYears = 3;

        [Header("Team Generation")]
        [SerializeField, Range(0, 30)] private int _archetypeVariation = 8;
        [SerializeField] private double _positionNeedBase = 70d;
        [SerializeField] private double _rosterDepthNeedWeight = 0.5d;
        [SerializeField, Min(0f)] private double _positionNeedVariance = 30d;
        [SerializeField, Range(0, 100)] private int _minimumPositionNeed = 5;
        [SerializeField, Range(0, 100)] private int _maximumPositionNeed = 95;
        [SerializeField, Range(1, 4)] private int _competitorsPerPosition = 2;
        [SerializeField] private double _competitorOverallBase = 70d;
        [SerializeField] private double _positionNeedCompetitorWeight = 0.22d;
        [SerializeField, Min(0f)] private double _competitorOverallVariance = 10d;
        [SerializeField, Range(0, 100)] private int _minimumCompetitorOverall = 38;
        [SerializeField, Range(0, 100)] private int _maximumCompetitorOverall = 72;
        [SerializeField, Range(0, 30)] private int _competitorAttributeProfileSpread = 10;
        [SerializeField, Range(0, 30)] private int _competitorAttributeVariance = 8;

        [Header("Player Evaluation")]
        [SerializeField, Min(0.1f)] private double _keyAttributeWeight = 2d;
        [SerializeField, Min(0.1f)] private double _supportingAttributeWeight = 1.35d;
        [SerializeField, Min(0.1f)] private double _generalAttributeWeight = 1d;
        [SerializeField, Range(0f, 1f)] private double _teamPreferenceInfluence = 0.15d;

        [Header("Manager Lineup")]
        [SerializeField] private LineupScoreWeightData _leadoffLineupWeights =
            new(0.45d, 0d, 0.30d, 0.25d);
        [SerializeField] private LineupScoreWeightData _tableSetterLineupWeights =
            new(0.50d, 0.10d, 0.15d, 0.25d);
        [SerializeField] private LineupScoreWeightData _runProducerLineupWeights =
            new(0.35d, 0.45d, 0d, 0.20d);
        [SerializeField] private LineupScoreWeightData _cleanupLineupWeights =
            new(0.25d, 0.60d, 0d, 0.15d);
        [SerializeField] private LineupScoreWeightData _lowerOrderLineupWeights =
            new(0.35d, 0.35d, 0.15d, 0.15d);

        [Header("Career Season")]
        [SerializeField, Min(1)] private int _regularSeasonGamesPerTeam = 80;
        [SerializeField, Min(1)] private int _startingRotationSize = 5;
        [SerializeField, Range(2, 9)] private int _reliefStartInning = 7;
        [SerializeField, Min(0f)] private double _managerDecisionVariance = 7d;
        [SerializeField] private int _startingCompetitionBonus = 9;
        [SerializeField] private int _rosterCompetitionBonus = 4;
        [SerializeField] private int _benchCompetitionBonus = -1;
        [SerializeField, Min(0)] private int _reliefOpportunityMargin = 4;
        [SerializeField, Range(0f, 1f)] private double _benchSubstitutionOpportunityProbability = 0.35d;
        [SerializeField, Range(1, 9)] private int _benchSubstitutionEarliestInning = 7;
        [SerializeField, Min(0)] private int _benchSubstitutionMaximumScoreDifference = 3;
        [SerializeField, Min(1)] private int _startingCompetitionEvaluationInterval = 1;
        [SerializeField, Min(1)] private int _rosterCompetitionEvaluationInterval = 2;
        [SerializeField, Min(1)] private int _benchCompetitionEvaluationInterval = 3;
        [SerializeField, Range(0, 100)] private int _evaluationOpportunityMinimumCondition = 70;
        [SerializeField, Range(0, 100)] private int _initialCondition = 90;
        [SerializeField, Range(0, 100)] private int _initialManagerEvaluation = 50;
        [SerializeField, Min(0)] private int _playingConditionCost = 2;
        [SerializeField, Min(0)] private int _restingConditionRecovery = 1;
        [SerializeField, Range(0, 100)] private int _minimumCondition = 55;
        [SerializeField, Min(1)] private int _maximumManagerEvaluationChange = 3;
        [SerializeField, Min(0f)] private double _conditionDecisionWeight = 0.30d;
        [SerializeField, Min(0f)] private double _managerEvaluationDecisionWeight = 0.10d;
        [SerializeField, Min(1)] private int _productiveBattingHits = 2;
        [SerializeField, Min(1)] private int _excellentBattingHits = 3;
        [SerializeField, Min(1)] private int _poorBattingAtBats = 4;
        [SerializeField, Min(0)] private int _qualityPitchingMaximumEarnedRuns = 1;
        [SerializeField, Min(1)] private int _poorPitchingMinimumEarnedRuns = 4;
        [SerializeField, Min(1)] private int _positiveEvaluationChange = 1;
        [SerializeField, Min(1)] private int _excellentEvaluationChange = 2;
        [SerializeField] private int _poorEvaluationChange = -1;
        [SerializeField] private int _veryPoorEvaluationChange = -2;
        [SerializeField, Range(1, 12)] private int _seasonOpeningMonth = 4;
        [SerializeField, Range(1, 28)] private int _seasonOpeningDay = 1;
        [SerializeField, Min(1)] private int _gamesBetweenRestDays = 6;

        private static readonly object SharedContentProviderLock = new object();
        private static HistoricalRuntimeContentCatalog SharedContentProviderCatalog;
        private static UnityHistoricalContentProvider SharedContentProvider;

        /// <summary>Domain Reload를 끈 Play Mode에서 이전 세션의 Provider가 남지 않게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedContentProvider()
        {
            lock (SharedContentProviderLock)
            {
                SharedContentProvider = null;
                SharedContentProviderCatalog = null;
            }
        }

        /// <summary>
        /// Resources의 Production 정적 정의를 읽으며 누락 시 Synthetic으로 대체하지 않는다.
        /// </summary>
        public static NewGameConfiguration LoadConfiguration()
        {
            return LoadDefinition().ToConfiguration();
        }

        /// <summary>Resources 정의에 직렬화된 Runtime Catalog로 Production 역사 Provider를 만든다.</summary>
        public static IHistoricalContentProvider LoadHistoricalContentProvider()
        {
            return LoadDefinition().CreateHistoricalContentProvider();
        }

        /// <summary>Resources 정의의 구단주 모드 Seed·연도·구단·초기 3자원을 읽는다.</summary>
        public static OwnerModeNewGameConfiguration LoadOwnerModeConfiguration()
        {
            return LoadDefinition().ToOwnerModeConfiguration();
        }

        /// <summary>구단주 모드에만 09~12 저작 Config를 합성한 BalanceTable을 제공한다.</summary>
        public static BalanceTable LoadOwnerModeBalanceTable()
        {
            return LoadDefinition().ToOwnerModeBalanceTable();
        }

        /// <summary>
        /// 명시적으로 연결된 Runtime Catalog만 사용하며 누락 시 Synthetic으로 대체하지 않는다.
        /// Provider는 23MB 역사 payload를 인스턴스 단위로 캐시하므로, 같은 Catalog에는 같은 인스턴스를 돌려준다.
        /// 그렇지 않으면 구단주 모드와 선수 모드가 같은 데이터를 각자 한 번씩 파싱한다.
        /// </summary>
        public IHistoricalContentProvider CreateHistoricalContentProvider()
        {
            if (_historicalContentCatalog == null)
            {
                throw new InvalidOperationException(
                    "Production NewGameDefinition에 HistoricalRuntimeContentCatalog가 연결되지 않았습니다.");
            }
            lock (SharedContentProviderLock)
            {
                if (SharedContentProvider != null &&
                    ReferenceEquals(SharedContentProviderCatalog, _historicalContentCatalog))
                {
                    return SharedContentProvider;
                }
                SharedContentProviderCatalog = _historicalContentCatalog;
                SharedContentProvider = new UnityHistoricalContentProvider(_historicalContentCatalog);
                return SharedContentProvider;
            }
        }

        public HistoricalRuntimeContentCatalog HistoricalContentCatalog => _historicalContentCatalog;
        public BakedWorldHistoryCatalog BakedWorldHistoryCatalog => _bakedWorldHistoryCatalog;
        public IReadOnlyList<long> CareerWorldSeedPool => _careerWorldSeedPool ?? Array.Empty<long>();

        /// <summary>Editor Baker가 산출물 Catalog를 연결한다.</summary>
        public void ConfigureBakedWorldHistoryCatalog(BakedWorldHistoryCatalog catalog)
        {
            _bakedWorldHistoryCatalog = catalog;
        }

        /// <summary>Editor Baker가 구울 대상 Seed를 확정한다.</summary>
        public void ConfigureCareerWorldSeedPool(IReadOnlyList<long> seeds)
        {
            if (seeds == null)
                throw new ArgumentNullException(nameof(seeds));
            var unique = new HashSet<long>();
            var result = new long[seeds.Count];
            for (int index = 0; index < seeds.Count; index++)
            {
                if (seeds[index] <= 0L)
                    throw new ArgumentException("World Seed는 양수여야 합니다.", nameof(seeds));
                if (!unique.Add(seeds[index]))
                    throw new ArgumentException($"World Seed {seeds[index]}가 중복되었습니다.", nameof(seeds));
                result[index] = seeds[index];
            }
            _careerWorldSeedPool = result;
        }

        /// <summary>
        /// Pool이 비어 있지 않으면 그중 하나를 골라 Bake가 적중하게 한다.
        /// Pool이 비어 있으면 호출자가 오늘처럼 임의 Seed를 쓰도록 false를 돌려준다.
        /// </summary>
        public bool TrySelectCareerWorldSeed(ulong selector, out ulong worldSeed)
        {
            IReadOnlyList<long> pool = CareerWorldSeedPool;
            if (pool.Count == 0)
            {
                worldSeed = 0UL;
                return false;
            }
            worldSeed = unchecked((ulong)pool[(int)(selector % (ulong)pool.Count)]);
            return true;
        }

        /// <summary>Bake Catalog가 없으면 null을 돌려주고, Builder는 실제 시뮬레이션 경로를 쓴다.</summary>
        public IBakedWorldHistorySource CreateBakedWorldHistorySource()
        {
            return _bakedWorldHistoryCatalog == null
                ? null
                : new UnityBakedWorldHistorySource(
                    _bakedWorldHistoryCatalog,
                    new UnityBakedWorldHistoryDiagnostics());
        }

        /// <summary>
        /// 워밍업이 메인 스레드에서 바이트를 미리 확보하려면 구현 타입이 필요하다.
        /// 인스턴스는 CreateHistoricalContentProvider와 동일하게 공유된다.
        /// </summary>
        public static UnityHistoricalContentProvider LoadSharedHistoricalContentProvider()
        {
            return (UnityHistoricalContentProvider)LoadHistoricalContentProvider();
        }

        /// <summary>Resources 정의에서 Bake Source를 만든다.</summary>
        public static IBakedWorldHistorySource LoadBakedWorldHistorySource()
        {
            return LoadDefinition().CreateBakedWorldHistorySource();
        }

        /// <summary>Resources 정의의 Career World Seed Pool에서 하나를 고른다.</summary>
        public static bool TrySelectCareerWorldSeedFromResources(ulong selector, out ulong worldSeed)
        {
            return LoadDefinition().TrySelectCareerWorldSeed(selector, out worldSeed);
        }

        private sealed class UnityBakedWorldHistoryDiagnostics : UnityBakedWorldHistorySource.ILoadDiagnostics
        {
            private const string LogPrefix = "[WorldHistoryBake] ";

            public void ReportBakeIgnored(string message) => Debug.LogWarning(LogPrefix + message);

            public void ReportBakeHit(string message) => Debug.Log(LogPrefix + message);

            // 미스는 결과를 틀리게 하지 않지만 새 게임 시작이 44시즌 실시뮬레이션으로 떨어진다.
            // 경고로 남겨야 성능 문제를 추적할 때 눈에 띈다.
            public void ReportBakeMissed(string message) => Debug.LogWarning(LogPrefix + message);
        }

        /// <summary>비어 있는 TeamSeasonKey는 Runtime에서 첫 유효 정규구단을 선택한다.</summary>
        public OwnerModeNewGameConfiguration ToOwnerModeConfiguration()
        {
            var tactics = new TacticCardDefinition[_ownerStarterTactics?.Length ?? 0];
            for (int index = 0; index < tactics.Length; index++)
                tactics[index] = _ownerStarterTactics[index].ToDefinition();
            return new OwnerModeNewGameConfiguration(
                checked((ulong)_ownerWorldSeed),
                _ownerOriginYear,
                _ownerPlayerTeamSeasonKey,
                _ownerLeagueInstanceId,
                _ownerInitialMoney,
                _ownerInitialScoutingPoints,
                _ownerInitialDevelopmentPoints,
                tactics);
        }

        /// <summary>공통 경기 Balance를 보존하면서 구단주 전용 시스템 표만 교체한다.</summary>
        public BalanceTable ToOwnerModeBalanceTable()
        {
            if (_ownerExpansionBalanceConfig == null)
            {
                throw new InvalidOperationException(
                    "Production NewGameDefinition에 OwnerExpansionBalance Config가 연결되지 않았습니다.");
            }
            OwnerExpansionBalanceTables ownerExpansion =
                OwnerExpansionBalanceConfig.Parse(_ownerExpansionBalanceConfig.text);
            BalanceTable common = ToConfiguration().Balance;
            return new BalanceTable(
                checked(common.Version + OwnerExpansionBalanceConfig.CurrentSchemaVersion),
                common.PlateDiscipline,
                common.BattedBall,
                common.BaseRunning,
                common.ContractOffer,
                common.TeamGeneration,
                common.PlayerEvaluation,
                common.CareerSeason,
                common.Growth,
                common.Injury,
                common.ManagerRoleEvaluation,
                common.ContractMarket,
                common.RosterTurnover,
                common.Postseason,
                common.BattingApproach,
                common.ContractBonus,
                common.ContractRenewal,
                common.TradeMarket,
                common.PlayerLifecycle,
                common.LeagueMovement,
                common.ManagerLineup,
                common.Match,
                common.MiniGame,
                common.HistoricalAssignment,
                ownerExpansion.ConditionChemistry,
                ownerExpansion.ClubOperation,
                ownerExpansion.Staff,
                ownerExpansion.ScoutingConfidence,
                $"{common.ContentHash}:{ownerExpansion.ContentHash}");
        }

        /// <summary>
        /// Unity 직렬화 데이터를 Core/Simulation이 소비할 수 있는 순수 값으로 변환한다.
        /// </summary>
        public NewGameConfiguration ToConfiguration()
        {
            var identities = new TeamIdentityDefinition[_teamIdentities.Length];
            for (int index = 0; index < identities.Length; index++)
                identities[index] = _teamIdentities[index].ToDefinition();

            var archetypes = new TeamArchetypeProfile[_archetypes.Length];
            for (int index = 0; index < archetypes.Length; index++)
                archetypes[index] = _archetypes[index].ToProfile();

            BalanceTable matchDefaults = BalanceTable.CreateDefault();
            GrowthBalanceTable growthBalance = _growthBalance != null
                ? _growthBalance.Build()
                : matchDefaults.Growth;
            string contentHash = _growthBalance != null
                ? _growthBalance.CreateContentHash()
                : matchDefaults.ContentHash;
            var balance = new BalanceTable(
                version: 3,
                matchDefaults.PlateDiscipline,
                matchDefaults.BattedBall,
                matchDefaults.BaseRunning,
                new ContractOfferBalance(
                    _offerScoreThreshold,
                    _scoutVarianceMinimum,
                    _scoutVarianceMaximum,
                    _preferredPositionBonus,
                    _baseSigningBonus,
                    _baseAnnualSalary,
                    _minimumOfferCount,
                    _maximumOfferCount,
                    _startingCompetitionNeed,
                    _rosterCompetitionNeed,
                    _ratingBaseline,
                    _contractYears),
                new TeamGenerationBalance(
                    _archetypeVariation,
                    _positionNeedBase,
                    _rosterDepthNeedWeight,
                    _positionNeedVariance,
                    _minimumPositionNeed,
                    _maximumPositionNeed,
                    _competitorsPerPosition,
                    _competitorOverallBase,
                    _positionNeedCompetitorWeight,
                    _competitorOverallVariance,
                    _minimumCompetitorOverall,
                    _maximumCompetitorOverall,
                    _competitorAttributeProfileSpread,
                    _competitorAttributeVariance),
                new PlayerEvaluationBalance(
                    _keyAttributeWeight,
                    _supportingAttributeWeight,
                    _generalAttributeWeight,
                    _teamPreferenceInfluence),
                new CareerSeasonBalance(
                    _regularSeasonGamesPerTeam,
                    _startingRotationSize,
                    _reliefStartInning,
                    _managerDecisionVariance,
                    _startingCompetitionBonus,
                    _rosterCompetitionBonus,
                    _benchCompetitionBonus,
                    _reliefOpportunityMargin,
                    _benchSubstitutionOpportunityProbability,
                    _benchSubstitutionEarliestInning,
                    _benchSubstitutionMaximumScoreDifference,
                    _startingCompetitionEvaluationInterval,
                    _rosterCompetitionEvaluationInterval,
                    _benchCompetitionEvaluationInterval,
                    _evaluationOpportunityMinimumCondition,
                    _initialCondition,
                    _initialManagerEvaluation,
                    _playingConditionCost,
                    _restingConditionRecovery,
                    _minimumCondition,
                    _maximumManagerEvaluationChange,
                    _conditionDecisionWeight,
                    _managerEvaluationDecisionWeight,
                    _productiveBattingHits,
                    _excellentBattingHits,
                    _poorBattingAtBats,
                    _qualityPitchingMaximumEarnedRuns,
                    _poorPitchingMinimumEarnedRuns,
                    _positiveEvaluationChange,
                    _excellentEvaluationChange,
                    _poorEvaluationChange,
                    _veryPoorEvaluationChange,
                    _seasonOpeningMonth,
                    _seasonOpeningDay,
                    _gamesBetweenRestDays),
                playerLifecycle: new PlayerLifecycleBalance(
                    _retirementMinimumAge,
                    _guaranteedRetirementAge,
                    _retirementBaseProbability,
                    _retirementAgeWeight,
                    _retirementLowAbilityThreshold,
                    _retirementLowAbilityWeight,
                    _rookieEntryMinimumAge,
                    _rookieEntryMaximumAge,
                    _rookieEntryMinimumOverall,
                    _rookieEntryMaximumOverall,
                    _rookieBaseSalary,
                    _minorBaseSalary,
                    _majorBaseSalary,
                    _rookieAiContractYears,
                    _minorAiContractYears,
                    _majorAiContractYears),
                leagueMovement: new LeagueMovementBalance(
                    _upperLeagueOverallPenalty,
                    _leaguePerformanceWeight,
                    _leaguePotentialWeight,
                    _reliablePromotionPlateAppearances,
                    _reliablePromotionPitchingOuts,
                    _minorMinimumProjectedOverall,
                    _majorMinimumProjectedOverall,
                    _promotionCompetitorMargin,
                    _promotionMinimumTeamBudget,
                    _promotionInterestScoreThreshold,
                    _maximumPromotionOffers,
                    _maximumRehabilitationOffers,
                    _minorPlayerContractYears,
                    _majorPlayerContractYears),
                managerLineup: new ManagerLineupBalance(
                    _leadoffLineupWeights.ToBalance(),
                    _tableSetterLineupWeights.ToBalance(),
                    _runProducerLineupWeights.ToBalance(),
                    _cleanupLineupWeights.ToBalance(),
                    _lowerOrderLineupWeights.ToBalance()),
                growth: growthBalance,
                contentHash: contentHash);

            var bakedContentProvider = new HistoricalCareerBakedContentProvider(
                CreateHistoricalContentProvider(),
                balance,
                _historicalLeagueSeasonYears,
                CreateBakedWorldHistorySource());

            return new NewGameConfiguration(
                balance,
                _teamCount,
                _firstSeasonYear,
                _startingAge,
                archetypes,
                identities,
                _playerNamePool,
                new WorldGenerationConfiguration(
                    _minorOverallBonus,
                    _majorOverallBonus,
                    _minorTeamNamePrefix,
                    _majorTeamNamePrefix,
                    _rookieMinimumAge,
                    _rookieMaximumAge,
                    _minorMinimumAge,
                    _minorMaximumAge,
                    _majorMinimumAge,
                    _majorMaximumAge),
                new CareerCreationRules(
                    new CareerAttributeAllocationRule(
                        attributeCount: 6,
                        _careerBaseAttributeValue,
                        _careerBatterBonusPoints,
                        _careerMaximumAttributeValue),
                    new CareerAttributeAllocationRule(
                        attributeCount: 4,
                        _careerBaseAttributeValue,
                        _careerPitcherBonusPoints,
                        _careerMaximumAttributeValue)),
                _teamEmblemCount,
                NewGameContentSource.BakedHistorical,
                bakedContentProvider,
                WorldRecordMode.SimulatedHistory);
        }

        private static NewGameDefinition LoadDefinition()
        {
            NewGameDefinition definition = Resources.Load<NewGameDefinition>(ResourcePath);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"Production NewGameDefinition을 찾을 수 없습니다: Resources/{ResourcePath}");
            }
            return definition;
        }
    }
}

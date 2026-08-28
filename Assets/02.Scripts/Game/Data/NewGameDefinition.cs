using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>
    /// 새 게임의 구단 후보와 조정 가능한 생성 계수를 보관하는 읽기 전용 정적 정의다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGameDefinition", menuName = "Baseball/Data/New Game Definition")]
    public sealed class NewGameDefinition : ScriptableObject
    {
        private const string ResourcePath = "NewGame/NewGameDefinition";

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

        [Header("League")]
        [SerializeField, Min(1)] private int _teamCount = 8;
        [SerializeField, Min(1)] private int _firstSeasonYear = 2028;
        [SerializeField, Range(16, 25)] private int _startingAge = 18;
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

        [Header("Character Creation")]
        [SerializeField, Range(0, 100)] private int _baseAttributeValue = 40;
        [SerializeField, Min(0)] private int _bonusPoints = 72;
        [SerializeField, Range(0, 100)] private int _maximumAttributeValue = 65;

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

        [Header("Player Evaluation")]
        [SerializeField, Min(0.1f)] private double _keyAttributeWeight = 2d;
        [SerializeField, Min(0.1f)] private double _supportingAttributeWeight = 1.35d;
        [SerializeField, Min(0.1f)] private double _generalAttributeWeight = 1d;
        [SerializeField, Range(0f, 1f)] private double _teamPreferenceInfluence = 0.15d;

        [Header("Career Season")]
        [SerializeField, Min(1)] private int _regularSeasonGamesPerTeam = 80;
        [SerializeField, Min(1)] private int _startingRotationSize = 5;
        [SerializeField, Range(2, 9)] private int _reliefStartInning = 7;
        [SerializeField, Min(0f)] private double _managerDecisionVariance = 7d;
        [SerializeField] private int _startingCompetitionBonus = 9;
        [SerializeField] private int _rosterCompetitionBonus = 4;
        [SerializeField] private int _benchCompetitionBonus = -1;
        [SerializeField, Min(0)] private int _reliefOpportunityMargin = 4;
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
        [SerializeField, Min(0f)] private double _conditionDecisionWeight = 0.12d;
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

        /// <summary>
        /// Resources의 정적 정의를 읽고 없으면 명시적인 코드 기본값으로 대체한다.
        /// </summary>
        public static NewGameConfiguration LoadConfiguration()
        {
            NewGameDefinition definition = Resources.Load<NewGameDefinition>(ResourcePath);
            return definition != null
                ? definition.ToConfiguration()
                : NewGameConfiguration.CreateDefault();
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
            var balance = new BalanceTable(
                version: 1,
                matchDefaults.PlateDiscipline,
                matchDefaults.BattedBall,
                matchDefaults.BaseRunning,
                new CharacterCreationBalance(_baseAttributeValue, _bonusPoints, _maximumAttributeValue),
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
                    _maximumCompetitorOverall),
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
                    _gamesBetweenRestDays));

            return new NewGameConfiguration(
                balance,
                _teamCount,
                _firstSeasonYear,
                _startingAge,
                archetypes,
                identities,
                _playerNamePool);
        }
    }
}

using System;
using Baseball.Game.Career.News;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>뉴스 점수·트리거·한국어 템플릿을 Resources에서 저작하는 읽기 전용 정의다.</summary>
    [CreateAssetMenu(fileName = "CareerNewsDefinition", menuName = "Baseball/Data/Career News Definition")]
    public sealed class CareerNewsDefinition : ScriptableObject
    {
        private const string ResourcePath = "CareerNews/CareerNewsDefinition";

        [Serializable]
        private struct TemplateConditionData
        {
            [SerializeField] private NewsFactKey _key;
            [SerializeField] private NewsFactComparison _comparison;
            [SerializeField] private double _value;

            public NewsTemplateCondition ToDefinition() => new(_key, _comparison, _value);
        }

        [Serializable]
        private sealed class TemplateData
        {
            [SerializeField] private string _templateId;
            [SerializeField] private NewsEventType _eventType;
            [SerializeField] private NewsCategory _category;
            [SerializeField] private NewsArticleLength _length;
            [SerializeField] private NewsSourceType _defaultSource;
            [SerializeField] private TemplateConditionData[] _conditions = Array.Empty<TemplateConditionData>();
            [SerializeField] private string[] _headlineVariants = Array.Empty<string>();
            [SerializeField] private string[] _leadVariants = Array.Empty<string>();
            [SerializeField] private string[] _bodyVariants = Array.Empty<string>();
            [SerializeField] private string _cooldownGroup;
            [SerializeField, Min(0)] private int _cooldownCycles;

            public NewsTemplateDefinition ToDefinition()
            {
                var conditions = new NewsTemplateCondition[_conditions.Length];
                for (int index = 0; index < conditions.Length; index++)
                    conditions[index] = _conditions[index].ToDefinition();
                return new NewsTemplateDefinition(
                    _templateId,
                    _eventType,
                    _category,
                    _length,
                    _defaultSource,
                    conditions,
                    _headlineVariants,
                    _leadVariants,
                    _bodyVariants,
                    _cooldownGroup,
                    _cooldownCycles);
            }
        }

        [Header("Generation")]
        [SerializeField, Min(1)] private int _generationVersion = 1;

        [Header("Priority")]
        [SerializeField] private int _playerRelevance = 40;
        [SerializeField] private int _playerTeamRelevance = 20;
        [SerializeField] private int _leagueRelevance = 5;
        [SerializeField] private int _storylineContinuation = 10;
        [SerializeField] private int _repeatPenalty = 25;
        [SerializeField] private int _sThreshold = 90;
        [SerializeField] private int _aThreshold = 70;
        [SerializeField] private int _bThreshold = 50;
        [SerializeField] private int _cThreshold = 30;
        [SerializeField, Range(1, 4)] private int _maximumArticlesPerCycle = 4;
        [SerializeField, Range(0, 2)] private int _maximumStandardArticles = 2;
        [SerializeField, Range(0, 1)] private int _maximumBriefings = 1;
        [SerializeField, Range(1, 2)] private int _maximumArticlesPerPlayer = 2;
        [SerializeField, Min(0)] private int _defaultCooldownCycles = 4;

        [Header("Game Triggers")]
        [SerializeField, Min(1)] private int _notableHits = 3;
        [SerializeField, Min(1)] private int _notableHomeRuns = 2;
        [SerializeField, Min(1)] private int _notableRunsBattedIn = 4;
        [SerializeField, Min(1)] private int _scorelessPitchingOuts = 18;
        [SerializeField, Min(1)] private int _notableStrikeouts = 10;
        [SerializeField] private int[] _hittingStreakMilestones = { 5, 10, 15, 20 };
        [SerializeField] private int[] _homeRunMilestones = { 10, 20, 30 };
        [SerializeField] private int[] _teamStreakMilestones = { 3, 5, 8 };

        [Header("Templates")]
        [SerializeField] private TemplateData[] _templates = Array.Empty<TemplateData>();

        /// <summary>Resources 정의가 없으면 테스트된 기본 설정으로 대체한다.</summary>
        public static CareerNewsConfiguration LoadConfiguration()
        {
            CareerNewsDefinition definition = Resources.Load<CareerNewsDefinition>(ResourcePath);
            return definition != null
                ? definition.ToConfiguration()
                : CareerNewsConfiguration.CreateDefault();
        }

        public CareerNewsConfiguration ToConfiguration()
        {
            NewsTemplateDefinition[] templates;
            if (_templates == null || _templates.Length == 0)
            {
                templates = DefaultNewsTemplateLibrary.Create();
            }
            else
            {
                templates = new NewsTemplateDefinition[_templates.Length];
                for (int index = 0; index < templates.Length; index++)
                    templates[index] = _templates[index].ToDefinition();
            }

            return new CareerNewsConfiguration(
                _generationVersion,
                new NewsPriorityDefinition(
                    _playerRelevance,
                    _playerTeamRelevance,
                    _leagueRelevance,
                    _storylineContinuation,
                    _repeatPenalty,
                    _sThreshold,
                    _aThreshold,
                    _bThreshold,
                    _cThreshold,
                    _maximumArticlesPerCycle,
                    _maximumStandardArticles,
                    _maximumBriefings,
                    _maximumArticlesPerPlayer,
                    _defaultCooldownCycles),
                new NewsTriggerDefinition(
                    _notableHits,
                    _notableHomeRuns,
                    _notableRunsBattedIn,
                    _scorelessPitchingOuts,
                    _notableStrikeouts,
                    _hittingStreakMilestones,
                    _homeRunMilestones,
                    _teamStreakMilestones),
                templates);
        }
    }
}

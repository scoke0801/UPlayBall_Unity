using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>오프시즌 프로그램을 기획자가 저작하고 순수 C# 정의로 변환한다.</summary>
    [CreateAssetMenu(fileName = "TrainingProgramCatalog", menuName = "Baseball/Data/Growth/Training Program Catalog")]
    public sealed class TrainingProgramCatalogAsset : ScriptableObject
    {
        [Serializable]
        private struct AbilityWeightData
        {
            [SerializeField] private PlayerAbility _ability;
            [SerializeField, Min(0.0001f)] private double _weight;

            public AbilityWeight ToDefinition() => new AbilityWeight(_ability, _weight);
            public void AppendContent(StringBuilder builder)
            {
                builder.Append((int)_ability).Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _weight);
                builder.Append(';');
            }
        }

        [Serializable]
        private struct ProgramData
        {
            [SerializeField] private string _programId;
            [SerializeField] private OffseasonActivityType _activityType;
            [SerializeField] private TrainingCategory _category;
            [SerializeField] private bool _appliesToAllPlayers;
            [SerializeField] private PlayerType _targetPlayerType;
            [SerializeField, Range(0, 12)] private int _durationWeeks;
            [SerializeField, Min(0)] private long _moneyCost;
            [SerializeField, Min(0f)] private double _programPower;
            [SerializeField] private AbilityWeightData[] _targetAbilities;
            [SerializeField, Range(0, 100)] private int _minimumCondition;
            [SerializeField, Range(0f, 1f)] private double _injuryRisk;
            [SerializeField, Min(0)] private int _maxTotalGain;
            [SerializeField, Min(0)] private int _maxGainPerAbility;
            [SerializeField] private int _conditionChange;
            [SerializeField, Min(0)] private int _minimumGuaranteedGain;
            [SerializeField] private string _partnerId;
            [SerializeField] private bool _canRaisePotential;
            [SerializeField] private TrainingAccessTier _minimumAccessTier;
            [SerializeField, Min(0f)] private double _potentialBreakthroughChanceMultiplier;
            [SerializeField, Min(0)] private int _minimumPotentialBreakthroughsWhenCapped;

            public TrainingProgramDefinition ToDefinition()
            {
                var weights = new AbilityWeight[_targetAbilities?.Length ?? 0];
                for (int index = 0; index < weights.Length; index++)
                    weights[index] = _targetAbilities[index].ToDefinition();
                return new TrainingProgramDefinition(
                    _programId,
                    _activityType,
                    _category,
                    _appliesToAllPlayers ? null : _targetPlayerType,
                    _durationWeeks,
                    _moneyCost,
                    _programPower,
                    weights,
                    _minimumCondition,
                    _injuryRisk,
                    _maxTotalGain,
                    _maxGainPerAbility,
                    _conditionChange,
                    _minimumGuaranteedGain,
                    _partnerId,
                    _canRaisePotential,
                    minimumAccessTier: _minimumAccessTier,
                    potentialBreakthroughChanceMultiplier:
                        _potentialBreakthroughChanceMultiplier <= 0d
                            ? 1d
                            : _potentialBreakthroughChanceMultiplier,
                    minimumPotentialBreakthroughsWhenCapped:
                        _minimumPotentialBreakthroughsWhenCapped);
            }

            public void AppendContent(StringBuilder builder)
            {
                builder.Append(_programId).Append('|').Append((int)_activityType).Append('|')
                    .Append((int)_category).Append('|').Append(_appliesToAllPlayers).Append('|')
                    .Append((int)_targetPlayerType).Append('|').Append(_durationWeeks).Append('|')
                    .Append(_moneyCost).Append('|');
                GrowthContentHashFormatting.AppendDouble(builder, _programPower);
                builder.Append('|').Append(_minimumCondition).Append('|');
                GrowthContentHashFormatting.AppendDouble(builder, _injuryRisk);
                builder.Append('|')
                    .Append(_maxTotalGain).Append('|').Append(_maxGainPerAbility).Append('|')
                    .Append(_conditionChange).Append('|').Append(_minimumGuaranteedGain).Append('|')
                    .Append(_partnerId).Append('|').Append(_canRaisePotential).Append('|')
                    .Append((int)_minimumAccessTier).Append('|');
                GrowthContentHashFormatting.AppendDouble(
                    builder,
                    _potentialBreakthroughChanceMultiplier);
                builder.Append('|').Append(_minimumPotentialBreakthroughsWhenCapped).Append('|');
                for (int index = 0; index < (_targetAbilities?.Length ?? 0); index++)
                    _targetAbilities[index].AppendContent(builder);
            }
        }

        [SerializeField] private bool _replaceBuiltInPrograms;
        [SerializeField] private ProgramData[] _programs = Array.Empty<ProgramData>();

        public TrainingProgramDefinition[] Build(TrainingProgramDefinition[] builtIn)
        {
            if (_programs == null || _programs.Length == 0)
                return builtIn;
            var result = _replaceBuiltInPrograms
                ? new List<TrainingProgramDefinition>(_programs.Length)
                : new List<TrainingProgramDefinition>(builtIn);
            for (int index = 0; index < _programs.Length; index++)
            {
                TrainingProgramDefinition authored = _programs[index].ToDefinition();
                int existingIndex = FindProgram(result, authored.ProgramId);
                if (existingIndex >= 0)
                    result[existingIndex] = authored;
                else
                    result.Add(authored);
            }
            return result.ToArray();
        }

        private static int FindProgram(List<TrainingProgramDefinition> programs, string programId)
        {
            for (int index = 0; index < programs.Count; index++)
                if (string.Equals(programs[index].ProgramId, programId, StringComparison.Ordinal)) return index;
            return -1;
        }

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("programs:").Append(_replaceBuiltInPrograms).Append('|');
            for (int index = 0; index < (_programs?.Length ?? 0); index++)
                _programs[index].AppendContent(builder);
        }
    }
}

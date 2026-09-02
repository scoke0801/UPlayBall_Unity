using System;
using System.Text;
using Baseball.Core.Growth;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>Rookie부터 Galaxy까지의 성장 접근 단계와 뽑기 해금 리그를 저작한다.</summary>
    [CreateAssetMenu(fileName = "TrainingAccessTier", menuName = "Baseball/Data/Growth/Training Access Tier")]
    public sealed class TrainingAccessTierAsset : ScriptableObject
    {
        [SerializeField] private TrainingAccessTier[] _leagueAccessTiers =
        {
            TrainingAccessTier.Foundation,
            TrainingAccessTier.Advanced,
            TrainingAccessTier.Professional,
            TrainingAccessTier.International,
            TrainingAccessTier.International,
            TrainingAccessTier.Elite,
            TrainingAccessTier.Elite,
            TrainingAccessTier.Championship,
            TrainingAccessTier.Championship,
            TrainingAccessTier.Legacy
        };

        [Tooltip("Normal, Rare, Elite, Unique, Legendary 순서의 최소 리그 단계(0=Rookie).")]
        [SerializeField] private int[] _minimumGachaLeagueLevels = { 0, 1, 2, 5, 7 };

        public TrainingAccessTier[] BuildAccessTiers()
        {
            if (_leagueAccessTiers == null || _leagueAccessTiers.Length != 10)
                throw new InvalidOperationException("10개 리그의 성장 접근 단계가 모두 필요합니다.");
            return (TrainingAccessTier[])_leagueAccessTiers.Clone();
        }

        public int[] BuildMinimumGachaLevels()
        {
            if (_minimumGachaLeagueLevels == null || _minimumGachaLeagueLevels.Length != 5)
                throw new InvalidOperationException("다섯 뽑기 등급의 최소 리그 단계가 필요합니다.");
            return (int[])_minimumGachaLeagueLevels.Clone();
        }

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("access:");
            for (int index = 0; index < (_leagueAccessTiers?.Length ?? 0); index++)
                builder.Append((int)_leagueAccessTiers[index]).Append(',');
            builder.Append('|');
            for (int index = 0; index < (_minimumGachaLeagueLevels?.Length ?? 0); index++)
                builder.Append(_minimumGachaLeagueLevels[index]).Append(',');
        }
    }
}

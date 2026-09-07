using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>10팀 조의 순위 구간과 목적 리그를 명시한 구단주 승강 규칙이다.</summary>
    public sealed class OwnerLeagueRankRule
    {
        public OwnerLeagueRankRule(LeagueGrade grade, int promotionLastRank, LeagueGrade? promotionTarget,
            int relegationFirstRank, LeagueGrade? relegationTarget)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), grade) || promotionLastRank < 0 || relegationFirstRank < 0 ||
                (promotionLastRank > 0) != promotionTarget.HasValue || (relegationFirstRank > 0) != relegationTarget.HasValue ||
                relegationFirstRank > 0 && promotionLastRank >= relegationFirstRank)
                throw new ArgumentException("리그 승강 순위 구간이 올바르지 않습니다.");
            if (promotionTarget.HasValue && (!Enum.IsDefined(typeof(LeagueGrade), promotionTarget.Value) || promotionTarget.Value == grade) ||
                relegationTarget.HasValue && (!Enum.IsDefined(typeof(LeagueGrade), relegationTarget.Value) || relegationTarget.Value == grade))
                throw new ArgumentException("리그 승강 목적지가 올바르지 않습니다.");
            Grade = grade;
            PromotionLastRank = promotionLastRank;
            PromotionTarget = promotionTarget;
            RelegationFirstRank = relegationFirstRank;
            RelegationTarget = relegationTarget;
        }

        public LeagueGrade Grade { get; }
        public int PromotionLastRank { get; }
        public LeagueGrade? PromotionTarget { get; }
        public int RelegationFirstRank { get; }
        public LeagueGrade? RelegationTarget { get; }

        /// <summary>챔피언에서 마스터로의 자동 승격을 포함한 등급별 순위 구간을 생성한다.</summary>
        public static IReadOnlyList<OwnerLeagueRankRule> CreateInitial()
        {
            return new[]
            {
                new OwnerLeagueRankRule(LeagueGrade.Rookie, 6, LeagueGrade.Minor, 0, null),
                new OwnerLeagueRankRule(LeagueGrade.Minor, 6, LeagueGrade.Major, 9, LeagueGrade.Rookie),
                new OwnerLeagueRankRule(LeagueGrade.Major, 6, LeagueGrade.World, 9, LeagueGrade.Minor),
                new OwnerLeagueRankRule(LeagueGrade.World, 4, LeagueGrade.AllStar, 9, LeagueGrade.Major),
                new OwnerLeagueRankRule(LeagueGrade.AllStar, 4, LeagueGrade.Classic, 8, LeagueGrade.World),
                new OwnerLeagueRankRule(LeagueGrade.Classic, 4, LeagueGrade.Winners, 7, LeagueGrade.AllStar),
                new OwnerLeagueRankRule(LeagueGrade.Winners, 4, LeagueGrade.Champion, 7, LeagueGrade.Classic),
                new OwnerLeagueRankRule(LeagueGrade.Champion, 4, LeagueGrade.Master, 7, LeagueGrade.Winners),
                new OwnerLeagueRankRule(LeagueGrade.Master, 4, LeagueGrade.Galaxy, 9, LeagueGrade.Champion),
                new OwnerLeagueRankRule(LeagueGrade.Galaxy, 0, null, 7, LeagueGrade.Master)
            };
        }
    }
}

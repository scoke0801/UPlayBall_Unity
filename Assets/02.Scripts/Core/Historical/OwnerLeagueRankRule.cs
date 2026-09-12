using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>10팀 조의 순위 구간과 목적 리그, 빈 자리를 채울 CPU 임시 구단 덱을 명시한 구단주 승강 규칙이다.</summary>
    public sealed class OwnerLeagueRankRule
    {
        public OwnerLeagueRankRule(LeagueGrade grade, int promotionLastRank, LeagueGrade? promotionTarget,
            int relegationFirstRank, LeagueGrade? relegationTarget,
            LeagueFillerDeckType fillerDeck = LeagueFillerDeckType.YearTeam, double fillerTargetCost = 0d)
        {
            if (double.IsNaN(fillerTargetCost) || fillerTargetCost < 0d || fillerTargetCost > 10d)
                throw new ArgumentOutOfRangeException(nameof(fillerTargetCost));
            if (!Enum.IsDefined(typeof(LeagueGrade), grade) || promotionLastRank < 0 || relegationFirstRank < 0 ||
                (promotionLastRank > 0) != promotionTarget.HasValue || (relegationFirstRank > 0) != relegationTarget.HasValue ||
                relegationFirstRank > 0 && promotionLastRank >= relegationFirstRank)
                throw new ArgumentException("리그 승강 순위 구간이 올바르지 않습니다.");
            if (promotionTarget.HasValue && (!Enum.IsDefined(typeof(LeagueGrade), promotionTarget.Value) || promotionTarget.Value == grade) ||
                relegationTarget.HasValue && (!Enum.IsDefined(typeof(LeagueGrade), relegationTarget.Value) || relegationTarget.Value == grade))
                throw new ArgumentException("리그 승강 목적지가 올바르지 않습니다.");
            if (!Enum.IsDefined(typeof(LeagueFillerDeckType), fillerDeck))
                throw new ArgumentOutOfRangeException(nameof(fillerDeck));
            Grade = grade;
            PromotionLastRank = promotionLastRank;
            PromotionTarget = promotionTarget;
            RelegationFirstRank = relegationFirstRank;
            RelegationTarget = relegationTarget;
            FillerDeck = fillerDeck;
            FillerTargetCost = fillerTargetCost;
        }

        public LeagueGrade Grade { get; }
        public int PromotionLastRank { get; }
        public LeagueGrade? PromotionTarget { get; }
        public int RelegationFirstRank { get; }
        public LeagueGrade? RelegationTarget { get; }
        public LeagueFillerDeckType FillerDeck { get; }

        /// <summary>특수 덱 CPU 구단 25인의 평균 Cost 상한이다. 0이면 상한 없이 해당 Edition만으로 채운다. 연도 구단 덱은 쓰지 않는다.</summary>
        public double FillerTargetCost { get; }

        /// <summary>
        /// 챔피언에서 마스터로의 자동 승격을 포함한 등급별 순위 구간을 생성한다.
        /// CPU 덱은 하위 리그의 단일 연도 구단에서 시작해 등급이 오를수록 EX→올스타→MVP→커리어하이→레전드로 바뀐다.
        /// 목표 Cost는 상세 경기에서 CPU 덱의 승률이 실제 역사 구단 강도 백분위 World p50·AllStar p60·Classic p70·
        /// Winners p80·Champion p85·Master p90·Galaxy p95와 같아지도록 보정한 값이다(OwnerExpansionBalance.json과 같다).
        /// </summary>
        public static IReadOnlyList<OwnerLeagueRankRule> CreateInitial()
        {
            return new[]
            {
                new OwnerLeagueRankRule(LeagueGrade.Rookie, 6, LeagueGrade.Minor, 0, null, LeagueFillerDeckType.YearTeam),
                new OwnerLeagueRankRule(LeagueGrade.Minor, 6, LeagueGrade.Major, 9, LeagueGrade.Rookie, LeagueFillerDeckType.YearTeam),
                new OwnerLeagueRankRule(LeagueGrade.Major, 6, LeagueGrade.World, 9, LeagueGrade.Minor, LeagueFillerDeckType.YearTeam),
                new OwnerLeagueRankRule(LeagueGrade.World, 4, LeagueGrade.AllStar, 9, LeagueGrade.Major, LeagueFillerDeckType.Ex, 5.80),
                new OwnerLeagueRankRule(LeagueGrade.AllStar, 4, LeagueGrade.Classic, 8, LeagueGrade.World, LeagueFillerDeckType.Ex, 5.95),
                new OwnerLeagueRankRule(LeagueGrade.Classic, 4, LeagueGrade.Winners, 7, LeagueGrade.AllStar, LeagueFillerDeckType.AllStar, 6.40),
                new OwnerLeagueRankRule(LeagueGrade.Winners, 4, LeagueGrade.Champion, 7, LeagueGrade.Classic, LeagueFillerDeckType.AllStar, 6.65),
                new OwnerLeagueRankRule(LeagueGrade.Champion, 4, LeagueGrade.Master, 7, LeagueGrade.Winners, LeagueFillerDeckType.Mvp, 6.85),
                new OwnerLeagueRankRule(LeagueGrade.Master, 4, LeagueGrade.Galaxy, 9, LeagueGrade.Champion, LeagueFillerDeckType.CareerHigh, 7.05),
                new OwnerLeagueRankRule(LeagueGrade.Galaxy, 0, null, 7, LeagueGrade.Master, LeagueFillerDeckType.Legend, 7.70)
            };
        }
    }
}

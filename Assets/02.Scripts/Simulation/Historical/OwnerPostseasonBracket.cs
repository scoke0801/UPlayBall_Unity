using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Historical
{
    /// <summary>구단주 조의 최종 순위를 포스트시즌 시드와 대진으로 변환한다.</summary>
    public static class OwnerPostseasonBracket
    {
        /// <summary>상위 5팀이 진출하며 작은 조는 존재하는 시드부터 시작한다.</summary>
        public static int[] SelectSeeds(
            IReadOnlyList<OwnerLeagueStanding> standings,
            IReadOnlyDictionary<string, int> teamIds)
        {
            if (standings == null) throw new ArgumentNullException(nameof(standings));
            if (teamIds == null) throw new ArgumentNullException(nameof(teamIds));
            if (standings.Count < 2) throw new ArgumentException("포스트시즌에는 두 구단 이상이 필요합니다.", nameof(standings));

            OwnerLeagueStanding[] ranked = new OwnerLeagueAllocationResolver().Rank(standings);
            int seedCount = Math.Min(ranked.Length, 5);
            var seeds = new int[seedCount];
            var unique = new HashSet<int>();
            for (int index = 0; index < seedCount; index++)
            {
                if (!teamIds.TryGetValue(ranked[index].TeamKey, out int teamId) || teamId <= 0)
                    throw new ArgumentException("순위 구단의 TeamId가 없습니다.", nameof(teamIds));
                if (!unique.Add(teamId))
                    throw new ArgumentException("포스트시즌 TeamId가 중복됐습니다.", nameof(teamIds));
                seeds[index] = teamId;
            }
            return seeds;
        }

    }
}

using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Historical
{
    /// <summary>구단주 조의 최종 순위를 포스트시즌 시드와 대진으로 변환한다.</summary>
    public static class OwnerPostseasonBracket
    {
        /// <summary>4팀 이상은 상위 4팀, 2~3팀 조는 상위 2팀을 진출시킨다.</summary>
        public static int[] SelectSeeds(
            IReadOnlyList<OwnerLeagueStanding> standings,
            IReadOnlyDictionary<string, int> teamIds)
        {
            if (standings == null) throw new ArgumentNullException(nameof(standings));
            if (teamIds == null) throw new ArgumentNullException(nameof(teamIds));
            if (standings.Count < 2) throw new ArgumentException("포스트시즌에는 두 구단 이상이 필요합니다.", nameof(standings));

            OwnerLeagueStanding[] ranked = new OwnerLeagueAllocationResolver().Rank(standings);
            int seedCount = ranked.Length >= 4 ? 4 : 2;
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

        /// <summary>시드 순서를 보존해 4강 대진의 두 TeamId를 반환한다.</summary>
        public static void GetSemifinalPair(int[] seeds, int seriesIndex, out int higherSeedTeamId, out int lowerSeedTeamId)
        {
            if (seeds == null || seeds.Length != 4) throw new ArgumentException("4개 시드가 필요합니다.", nameof(seeds));
            if (seriesIndex == 0) { higherSeedTeamId = seeds[0]; lowerSeedTeamId = seeds[3]; return; }
            if (seriesIndex == 1) { higherSeedTeamId = seeds[1]; lowerSeedTeamId = seeds[2]; return; }
            throw new ArgumentOutOfRangeException(nameof(seriesIndex));
        }
    }
}

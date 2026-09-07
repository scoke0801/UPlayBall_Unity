using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>시즌 최종 순위를 정하는 구단별 정규시즌 집계 입력이다.</summary>
    public sealed class OwnerLeagueStanding
    {
        public OwnerLeagueStanding(string teamKey, int wins, int losses, int runDifferential)
        {
            if (string.IsNullOrWhiteSpace(teamKey) || wins < 0 || losses < 0) throw new ArgumentException("순위 입력이 올바르지 않습니다.");
            TeamKey = teamKey;
            Wins = wins;
            Losses = losses;
            RunDifferential = runDifferential;
        }
        public string TeamKey { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int RunDifferential { get; }
    }

    /// <summary>구단별 순위 승강과 같은 등급 내 조 추첨을 분리하여 결정론적으로 계산한다.</summary>
    public sealed class OwnerLeagueAllocationResolver
    {
        /// <summary>승률·득실차·영속 ID 순으로 최종 순위를 확정한다.</summary>
        public OwnerLeagueStanding[] Rank(IReadOnlyList<OwnerLeagueStanding> standings)
        {
            var sorted = new List<OwnerLeagueStanding>(standings ?? throw new ArgumentNullException(nameof(standings)));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var team in sorted)
                if (team == null || !keys.Add(team.TeamKey)) throw new ArgumentException("순위 구단이 중복되거나 누락됐습니다.");
            sorted.Sort((a, b) =>
            {
                // 분수 교차 곱으로 부동소수점 오차 없는 승률 비교를 한다. 전 경기 무승부는 승률 0이다.
                int order = ((long)b.Wins * Math.Max(1, a.Wins + a.Losses)).CompareTo((long)a.Wins * Math.Max(1, b.Wins + b.Losses));
                if (order == 0) order = b.RunDifferential.CompareTo(a.RunDifferential);
                return order != 0 ? order : string.CompareOrdinal(a.TeamKey, b.TeamKey);
            });
            return sorted.ToArray();
        }

        /// <summary>등급별 순위표를 적용하며 소수 구단 조에는 최소 두 잔류 슬롯을 남긴다.</summary>
        public LeagueGrade ResolveGrade(LeagueGrade grade, int rank, int teamCount, LeagueDefinition rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (!Enum.IsDefined(typeof(LeagueGrade), grade) || rank < 1 || rank > teamCount) throw new ArgumentOutOfRangeException(nameof(rank));
            int available = Math.Max(0, teamCount - 2);
            OwnerLeagueRankRule rule = rules.GetRankRule(grade);
            int promotion = Math.Min(rule.PromotionLastRank, available);
            int relegation = rule.RelegationTarget.HasValue
                ? Math.Min(Math.Max(0, teamCount - rule.RelegationFirstRank + 1), available - promotion) : 0;
            if (rank <= promotion) return rule.PromotionTarget.Value;
            if (rank > teamCount - relegation) return rule.RelegationTarget.Value;
            return grade;
        }

        /// <summary>정렬한 전체 후보를 Fisher–Yates로 섞은 뒤 한 구단만 남는 조 없이 배분한다.</summary>
        public string[][] DrawGroups(IReadOnlyList<string> teamKeys, int targetSize, IRandomSource random)
        {
            if (teamKeys == null || random == null) throw new ArgumentNullException();
            if (targetSize < 2 || teamKeys.Count == 1) throw new ArgumentException("한 구단만으로 조를 만들 수 없습니다.");
            var shuffled = new List<string>(teamKeys);
            shuffled.Sort(StringComparer.Ordinal);
            for (int index = 0; index < shuffled.Count; index++)
                if (string.IsNullOrWhiteSpace(shuffled[index]) || index > 0 && shuffled[index] == shuffled[index - 1])
                    throw new ArgumentException("추첨 후보가 중복되거나 누락됐습니다.");
            for (int index = shuffled.Count - 1; index > 0; index--)
            {
                int selected = (int)(random.NextDouble() * (index + 1));
                (shuffled[index], shuffled[selected]) = (shuffled[selected], shuffled[index]);
            }
            var groups = new List<string[]>();
            int count = (shuffled.Count + targetSize - 1) / targetSize;
            if (count > 1 && shuffled.Count / count < 2) count--;
            int offset = 0;
            for (int index = 0; index < count; index++)
            {
                int size = shuffled.Count / count + (index < shuffled.Count % count ? 1 : 0);
                groups.Add(shuffled.GetRange(offset, size).ToArray());
                offset += size;
            }
            return groups.ToArray();
        }
    }
}

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

        /// <summary>
        /// 정렬한 전체 후보를 Fisher–Yates로 섞은 뒤 조마다 균등하게 배분한다. 조의 빈 자리는 호출자가 CPU 임시 구단으로
        /// 채우므로 한 구단뿐인 등급도 한 조로 만든다. 조가 여럿이면 한 구단만 남는 조는 만들지 않는다.
        /// </summary>
        public string[][] DrawGroups(IReadOnlyList<string> teamKeys, int targetSize, IRandomSource random)
            => DrawGroups(teamKeys, targetSize, random, null, 0d);

        /// <summary>
        /// 직전 시즌 같은 조였던 구단끼리 다시 묶이는 것을 확률적으로 피하며 조를 추첨한다.
        /// 섞은 순서대로 구단을 배치하되 <paramref name="repeatAvoidanceChance"/> 확률로 직전 조 동료가 가장 적은 조만
        /// 후보로 삼는다. 조 수와 크기는 기존 추첨과 같으므로 한 조뿐인 등급처럼 선택지가 없으면 재회를 피하지 못한다.
        /// </summary>
        public string[][] DrawGroups(IReadOnlyList<string> teamKeys, int targetSize, IRandomSource random,
            IReadOnlyDictionary<string, int> previousGroupByTeam, double repeatAvoidanceChance)
        {
            if (teamKeys == null || random == null) throw new ArgumentNullException();
            if (targetSize < 2) throw new ArgumentException("조 목표 크기는 2 이상이어야 합니다.", nameof(targetSize));
            if (teamKeys.Count == 0) return Array.Empty<string[]>();
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
            int count = (shuffled.Count + targetSize - 1) / targetSize;
            if (count > 1 && shuffled.Count / count < 2) count--;
            var sizes = new int[count];
            for (int index = 0; index < count; index++)
                sizes[index] = shuffled.Count / count + (index < shuffled.Count % count ? 1 : 0);
            if (previousGroupByTeam == null || count == 1 || repeatAvoidanceChance <= 0d)
                return SliceGroups(shuffled, sizes);
            return AssignAvoidingRepeats(shuffled, sizes, random, previousGroupByTeam, repeatAvoidanceChance);
        }

        private static string[][] SliceGroups(List<string> shuffled, int[] sizes)
        {
            var groups = new string[sizes.Length][];
            int offset = 0;
            for (int index = 0; index < sizes.Length; index++)
            {
                groups[index] = shuffled.GetRange(offset, sizes[index]).ToArray();
                offset += sizes[index];
            }
            return groups;
        }

        private static string[][] AssignAvoidingRepeats(List<string> shuffled, int[] sizes, IRandomSource random,
            IReadOnlyDictionary<string, int> previousGroupByTeam, double repeatAvoidanceChance)
        {
            var members = new List<string>[sizes.Length];
            for (int index = 0; index < members.Length; index++) members[index] = new List<string>(sizes[index]);
            var candidates = new List<int>(sizes.Length);
            foreach (string team in OrderCohortTeamsFirst(shuffled, previousGroupByTeam))
            {
                bool isAvoiding = random.NextDouble() < repeatAvoidanceChance;
                bool hasPrevious = previousGroupByTeam.TryGetValue(team, out int previousGroup);
                int fewestRepeats = int.MaxValue;
                candidates.Clear();
                for (int group = 0; group < members.Length; group++)
                {
                    if (members[group].Count >= sizes[group]) continue;
                    int repeats = isAvoiding && hasPrevious ? CountRepeats(members[group], previousGroup, previousGroupByTeam) : 0;
                    if (repeats > fewestRepeats) continue;
                    if (repeats < fewestRepeats) { fewestRepeats = repeats; candidates.Clear(); }
                    candidates.Add(group);
                }
                members[candidates[(int)(random.NextDouble() * candidates.Count)]].Add(team);
            }
            var groups = new string[members.Length][];
            for (int index = 0; index < members.Length; index++) groups[index] = members[index].ToArray();
            return groups;
        }

        /// <summary>
        /// 직전 조 동료가 같은 후보에 있는 구단을 먼저 배치한다. 동료 없는 구단이 조 정원을 먼저 채우면
        /// 뒤늦게 남은 동료끼리 빈 조 하나에 강제로 몰리기 때문이다. 각 무리 안의 순서는 섞은 순서를 유지한다.
        /// </summary>
        private static List<string> OrderCohortTeamsFirst(List<string> shuffled, IReadOnlyDictionary<string, int> previousGroupByTeam)
        {
            var cohortSizes = new Dictionary<int, int>();
            foreach (string team in shuffled)
                if (previousGroupByTeam.TryGetValue(team, out int group))
                    cohortSizes[group] = cohortSizes.TryGetValue(group, out int size) ? size + 1 : 1;
            var ordered = new List<string>(shuffled.Count);
            var solos = new List<string>();
            foreach (string team in shuffled)
            {
                bool hasCohort = previousGroupByTeam.TryGetValue(team, out int group) && cohortSizes[group] > 1;
                (hasCohort ? ordered : solos).Add(team);
            }
            ordered.AddRange(solos);
            return ordered;
        }

        private static int CountRepeats(List<string> members, int previousGroup, IReadOnlyDictionary<string, int> previousGroupByTeam)
        {
            int repeats = 0;
            foreach (string member in members)
                if (previousGroupByTeam.TryGetValue(member, out int group) && group == previousGroup) repeats++;
            return repeats;
        }
    }
}

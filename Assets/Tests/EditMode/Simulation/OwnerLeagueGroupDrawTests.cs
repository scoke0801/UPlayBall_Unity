using System.Collections.Generic;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>구단주 모드 승강 후 조 추첨이 직전 시즌 조 동료와의 재회를 피하는지 검증한다.</summary>
    public sealed class OwnerLeagueGroupDrawTests
    {
        private const int SeedCount = 300;

        [Test]
        public void DrawGroups_직전조동료를함께승격해도대부분다른조로흩어진다()
        {
            // 세 조에서 세 구단씩 승격해 온 상황이다. 조가 셋이므로 이상적으로는 재회가 0이다.
            var keys = new List<string>();
            var previous = new Dictionary<string, int>();
            for (int cohort = 0; cohort < 3; cohort++)
                for (int member = 0; member < 3; member++)
                {
                    string key = $"promoted-{cohort}-{member}";
                    keys.Add(key);
                    previous.Add(key, cohort);
                }
            for (int index = 0; index < 21; index++)
            {
                string key = $"stay-{index:00}";
                keys.Add(key);
                previous.Add(key, 10 + index);
            }

            var resolver = new OwnerLeagueAllocationResolver();
            int randomRepeats = 0;
            int avoidedRepeats = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                randomRepeats += CountRepeatPairs(resolver.DrawGroups(keys, 10, new Pcg32Random((ulong)seed)), previous);
                string[][] drawn = resolver.DrawGroups(keys, 10, new Pcg32Random((ulong)seed), previous, 0.9d);
                AssertAllTeamsOnce(drawn, keys, 10);
                avoidedRepeats += CountRepeatPairs(drawn, previous);
            }

            Assert.That(randomRepeats, Is.GreaterThan(SeedCount), "기존 무작위 추첨은 재회가 흔해야 비교가 의미 있다.");
            Assert.That(avoidedRepeats, Is.LessThan(randomRepeats / 5));
        }

        [Test]
        public void DrawGroups_회피확률1이면선택지가있는한재회하지않는다()
        {
            var keys = new List<string>();
            var previous = new Dictionary<string, int>();
            for (int cohort = 0; cohort < 3; cohort++)
                for (int member = 0; member < 3; member++)
                {
                    string key = $"t{cohort}{member}";
                    keys.Add(key);
                    previous.Add(key, cohort);
                }

            var resolver = new OwnerLeagueAllocationResolver();
            for (int seed = 0; seed < SeedCount; seed++)
                Assert.That(CountRepeatPairs(resolver.DrawGroups(keys, 3, new Pcg32Random((ulong)seed), previous, 1d), previous), Is.Zero);
        }

        [Test]
        public void DrawGroups_같은Seed와직전조면같은결과를낸다()
        {
            var keys = new List<string>();
            var previous = new Dictionary<string, int>();
            for (int index = 0; index < 25; index++)
            {
                keys.Add($"team-{index:00}");
                previous.Add($"team-{index:00}", index % 4);
            }
            var resolver = new OwnerLeagueAllocationResolver();

            string[][] first = resolver.DrawGroups(keys, 10, new Pcg32Random(77UL), previous, 0.9d);
            string[][] second = resolver.DrawGroups(keys, 10, new Pcg32Random(77UL), previous, 0.9d);

            Assert.That(second, Is.EqualTo(first));
        }

        private static int CountRepeatPairs(string[][] groups, IReadOnlyDictionary<string, int> previous)
        {
            int pairs = 0;
            foreach (string[] group in groups)
                for (int left = 0; left < group.Length; left++)
                    for (int right = left + 1; right < group.Length; right++)
                        if (previous[group[left]] == previous[group[right]]) pairs++;
            return pairs;
        }

        private static void AssertAllTeamsOnce(string[][] groups, List<string> keys, int targetSize)
        {
            var seen = new HashSet<string>();
            foreach (string[] group in groups)
            {
                Assert.That(group.Length, Is.InRange(2, targetSize));
                foreach (string key in group) Assert.That(seen.Add(key), Is.True);
            }
            Assert.That(seen.Count, Is.EqualTo(keys.Count));
        }
    }
}

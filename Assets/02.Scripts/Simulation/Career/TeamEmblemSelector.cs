using System;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 월드 Seed와 분리된 난수 스트림으로 중복 없는 구단 엠블럼 순서를 만든다.
    /// </summary>
    public static class TeamEmblemSelector
    {
        private const ulong EmblemStream = 0x454D424C454D444BUL;

        /// <summary>1부터 emblemCount까지의 ID를 결정론적으로 섞어 반환한다.</summary>
        public static int[] CreateShuffledIds(int emblemCount, ulong worldSeed)
        {
            if (emblemCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(emblemCount));

            var result = new int[emblemCount];
            for (int index = 0; index < result.Length; index++)
                result[index] = index + 1;

            var random = new Pcg32Random(DeterministicSeed.Derive(worldSeed, EmblemStream));
            for (int index = result.Length - 1; index > 0; index--)
            {
                int selected = Math.Min((int)(random.NextDouble() * (index + 1)), index);
                (result[index], result[selected]) = (result[selected], result[index]);
            }
            return result;
        }
    }
}

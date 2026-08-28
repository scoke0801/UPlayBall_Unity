namespace Baseball.Simulation.Random
{
    /// <summary>
    /// 저장된 기준 Seed에서 용도별 독립 Seed를 결정론적으로 파생한다.
    /// </summary>
    public static class DeterministicSeed
    {
        /// <summary>
        /// SplitMix64 혼합으로 기준 Seed와 streamId를 하나의 재현 가능한 Seed로 만든다.
        /// </summary>
        public static ulong Derive(ulong baseSeed, ulong streamId)
        {
            ulong value = baseSeed + 0x9E3779B97F4A7C15UL * (streamId + 1UL);
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

namespace Baseball.Simulation.Random
{
    /// <summary>
    /// 플랫폼에 관계없이 같은 Seed에서 같은 수열을 만드는 PCG32 난수 공급자다.
    /// </summary>
    public sealed class Pcg32Random : IRandomSource
    {
        private const double UIntToDouble = 1d / 4294967296d;
        private ulong _state;
        private readonly ulong _increment;

        /// <summary>
        /// 저장 가능한 Seed와 독립 수열 번호로 난수 공급자를 생성한다.
        /// </summary>
        public Pcg32Random(ulong seed, ulong sequence = 54UL)
        {
            _state = 0UL;
            _increment = (sequence << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>
        /// 0 이상 1 미만의 다음 난수를 반환한다.
        /// </summary>
        public double NextDouble()
        {
            return NextUInt() * UIntToDouble;
        }

        private uint NextUInt()
        {
            ulong previousState = _state;
            _state = previousState * 6364136223846793005UL + _increment;
            uint xorShifted = (uint)(((previousState >> 18) ^ previousState) >> 27);
            int rotation = (int)(previousState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }
    }
}

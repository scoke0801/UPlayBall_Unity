namespace Baseball.Simulation.Random
{
    /// <summary>
    /// 시뮬레이션에 주입되는 결정론적 난수 공급자 계약이다.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// 0 이상 1 미만의 다음 난수를 반환한다.
        /// </summary>
        double NextDouble();
    }
}

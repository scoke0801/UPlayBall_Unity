using System;

namespace Baseball.Simulation.Random
{
    /// <summary>
    /// 경기 루트 Seed에서 파생한 도메인별 독립 PCG 수열을 묶어 주입한다.
    /// </summary>
    public sealed class MatchRandomStreams
    {
        public MatchRandomStreams(
            IRandomSource pitchOutcome,
            IRandomSource swingDecision,
            IRandomSource contact,
            IRandomSource battedBall,
            IRandomSource fielding,
            IRandomSource baserunning,
            IRandomSource injury)
        {
            PitchOutcome = pitchOutcome ?? throw new ArgumentNullException(nameof(pitchOutcome));
            SwingDecision = swingDecision ?? throw new ArgumentNullException(nameof(swingDecision));
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
            BattedBall = battedBall ?? throw new ArgumentNullException(nameof(battedBall));
            Fielding = fielding ?? throw new ArgumentNullException(nameof(fielding));
            Baserunning = baserunning ?? throw new ArgumentNullException(nameof(baserunning));
            Injury = injury ?? throw new ArgumentNullException(nameof(injury));
        }

        public IRandomSource PitchOutcome { get; }
        public IRandomSource SwingDecision { get; }
        public IRandomSource Contact { get; }
        public IRandomSource BattedBall { get; }
        public IRandomSource Fielding { get; }
        public IRandomSource Baserunning { get; }
        public IRandomSource Injury { get; }

        /// <summary>
        /// 저장된 경기 Seed 하나에서 변경 영향이 분리된 일곱 개 수열을 만든다.
        /// </summary>
        public static MatchRandomStreams Create(ulong matchSeed)
        {
            return new MatchRandomStreams(
                CreateStream(matchSeed, 0x5049544348UL),
                CreateStream(matchSeed, 0x5357494E47UL),
                CreateStream(matchSeed, 0x434F4E54414354UL),
                CreateStream(matchSeed, 0x424154544544UL),
                CreateStream(matchSeed, 0x4649454C44UL),
                CreateStream(matchSeed, 0x52554E4E4552UL),
                CreateStream(matchSeed, 0x494E4A555259UL));
        }

        /// <summary>
        /// 스크립트 RNG를 사용하는 규칙 테스트에서 기존 한 수열을 모든 도메인에 공유한다.
        /// </summary>
        public static MatchRandomStreams Shared(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            return new MatchRandomStreams(random, random, random, random, random, random, random);
        }

        private static Pcg32Random CreateStream(ulong matchSeed, ulong domainId)
        {
            ulong seed = DeterministicSeed.Derive(matchSeed, domainId);
            ulong sequence = DeterministicSeed.Derive(domainId, matchSeed) | 1UL;
            return new Pcg32Random(seed, sequence);
        }
    }
}

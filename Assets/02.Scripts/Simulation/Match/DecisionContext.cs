using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    public enum LeverageTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum DefensiveAlignment
    {
        Standard = 0,
        PullShift = 1,
        DoublePlayDepth = 2,
        InfieldIn = 3,
        GuardLines = 4
    }

    /// <summary>
    /// AI 판단에 필요한 베이스 점유를 객체 참조 없이 고정한다.
    /// </summary>
    public readonly struct BaseStateSnapshot
    {
        public BaseStateSnapshot(bool first, bool second, bool third)
        {
            HasRunnerOnFirst = first;
            HasRunnerOnSecond = second;
            HasRunnerOnThird = third;
        }

        public bool HasRunnerOnFirst { get; }
        public bool HasRunnerOnSecond { get; }
        public bool HasRunnerOnThird { get; }
        public int OccupancyMask =>
            (HasRunnerOnFirst ? 1 : 0) |
            (HasRunnerOnSecond ? 2 : 0) |
            (HasRunnerOnThird ? 4 : 0);
    }

    /// <summary>
    /// 감독·타자·투수 AI가 공유하는 한 시점의 불변 판단 입력이다.
    /// </summary>
    public sealed class DecisionContext
    {
        public DecisionContext(
            int inning,
            InningHalf half,
            int scoreDifference,
            int outs,
            BaseStateSnapshot bases,
            Player batter,
            Player pitcher,
            Player onDeckBatter,
            LeverageTier leverage,
            PitcherGameState pitcherState,
            MatchRules rules,
            ManagerTacticalProfile managerProfile)
        {
            if (inning <= 0) throw new ArgumentOutOfRangeException(nameof(inning));
            if (outs < 0 || outs > 2) throw new ArgumentOutOfRangeException(nameof(outs));
            Inning = inning;
            Half = half;
            ScoreDifference = scoreDifference;
            Outs = outs;
            Bases = bases;
            Batter = batter ?? throw new ArgumentNullException(nameof(batter));
            Pitcher = pitcher ?? throw new ArgumentNullException(nameof(pitcher));
            OnDeckBatter = onDeckBatter ?? throw new ArgumentNullException(nameof(onDeckBatter));
            Leverage = leverage;
            PitcherState = pitcherState ?? throw new ArgumentNullException(nameof(pitcherState));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            ManagerProfile = managerProfile;
        }

        public int Inning { get; }
        public InningHalf Half { get; }
        public int ScoreDifference { get; }
        public int Outs { get; }
        public BaseStateSnapshot Bases { get; }
        public Player Batter { get; }
        public Player Pitcher { get; }
        public Player OnDeckBatter { get; }
        public LeverageTier Leverage { get; }
        public PitcherGameState PitcherState { get; }
        public MatchRules Rules { get; }
        public ManagerTacticalProfile ManagerProfile { get; }
    }
}

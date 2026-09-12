using System;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    public enum InterventionLevel
    {
        Auto = 0,
        KeyMoments = 1,
        FullControl = 2
    }

    public interface IBattingDecisionProvider
    {
        BattingApproach GetApproach(DecisionContext context);
    }

    public interface IPitchingDecisionProvider
    {
        PitchingApproach GetApproach(DecisionContext context);
    }

    /// <summary>
    /// 통계 회귀나 명시적 선수 정책에서만 균형 타격을 고정한다.
    /// </summary>
    public sealed class FixedBalancedDecisionProvider : IBattingDecisionProvider, IPitchingDecisionProvider
    {
        public BattingApproach GetApproach(DecisionContext context) => BattingApproach.Balanced;
        PitchingApproach IPitchingDecisionProvider.GetApproach(DecisionContext context) => PitchingApproach.Balanced;
    }

    /// <summary>
    /// 선수 능력·경기 상황·상대 투수를 점수화해 가장 적합한 타격 접근법을 결정한다.
    /// </summary>
    public sealed class SituationalBattingDecisionProvider : IBattingDecisionProvider
    {
        public BattingApproach GetApproach(DecisionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            BatterAttributes batter = context.Batter.BatterAttributes;
            PitcherAttributes pitcher = context.Pitcher.PitcherAttributes;
            double balanced = 20d;
            double aggressive = (batter.Contact + batter.Power) * 0.10d - pitcher.Stuff * 0.05d;
            double patient = (100d - pitcher.Control) * 0.22d + batter.Mental * 0.08d;
            double contact = batter.Contact * 0.12d + batter.Mental * 0.06d - pitcher.Velocity * 0.04d;
            double power = batter.Power * 0.16d - pitcher.Breaking * 0.08d;

            // 방침은 능력치를 바꾸지 않고 AI가 이미 계산한 타격 접근법의 선택 점수만 이동시킨다.
            double approachBias = (context.ManagerProfile.BattingApproach - 50d) * 0.18d;
            contact -= approachBias;
            power += approachBias;

            if (context.Bases.HasRunnerOnThird && context.Outs < 2) contact += 12d;
            if (context.Inning >= 7 && context.ScoreDifference <= -2) power += 11d;
            if (context.Inning >= 7 && context.ScoreDifference == 0) { patient += 6d; contact += 5d; }
            if (pitcher.Control < 42) patient += 12d;
            if (pitcher.Stuff >= 70 && pitcher.Control < 50) { patient += 7d; contact += 5d; }
            if (pitcher.Stuff < 42 && pitcher.Control >= 58) { aggressive += 9d; power += 5d; }

            return SelectHighest(balanced, aggressive, patient, contact, power);
        }

        private static BattingApproach SelectHighest(
            double balanced,
            double aggressive,
            double patient,
            double contact,
            double power)
        {
            double best = balanced;
            BattingApproach result = BattingApproach.Balanced;
            if (aggressive > best) { best = aggressive; result = BattingApproach.Aggressive; }
            if (patient > best) { best = patient; result = BattingApproach.Patient; }
            if (contact > best) { best = contact; result = BattingApproach.Contact; }
            if (power > best) result = BattingApproach.Power;
            return result;
        }
    }

    /// <summary>
    /// 투수 능력과 현재 위험을 바탕으로 존 공략·유인·삼진·땅볼 방침을 결정한다.
    /// </summary>
    public sealed class SituationalPitchingDecisionProvider : IPitchingDecisionProvider
    {
        public PitchingApproach GetApproach(DecisionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            PitcherAttributes pitcher = context.Pitcher.PitcherAttributes;
            BatterAttributes batter = context.Batter.BatterAttributes;
            if (context.Bases.HasRunnerOnFirst && context.Outs < 2 && pitcher.Breaking >= 55)
                return PitchingApproach.GroundBall;
            // 제구가 부족한 투수에게 삼진 방침의 추가 제구 손실을 강요하지 않는다.
            if (context.Leverage >= LeverageTier.High && pitcher.Stuff >= 62 && pitcher.Control >= 55)
                return PitchingApproach.Strikeout;
            // 빈 베이스의 강타자에게 출루를 무상 제공하지 않는다. 득점권 위기에서 다음 타자가
            // 더 쉬울 때만 1루를 활용한다. 현재/다음 타자의 비교는 같은 경기 능력치를 사용한다.
            if (batter.Power >= 75 && !context.Bases.HasRunnerOnFirst &&
                (context.Bases.HasRunnerOnSecond || context.Bases.HasRunnerOnThird) &&
                context.Leverage >= LeverageTier.High &&
                context.OnDeckBatter.BatterAttributes.Power < batter.Power &&
                context.OnDeckBatter.BatterAttributes.Contact < batter.Contact)
                return PitchingApproach.PitchAround;
            if (pitcher.Control >= 65 && batter.Contact < 55)
                return PitchingApproach.AttackZone;
            if (pitcher.Breaking >= 65 && pitcher.Control >= 50)
                return PitchingApproach.Nibble;
            return PitchingApproach.Balanced;
        }
    }

    /// <summary>
    /// 자동 AI와 향후 선수 입력 공급자를 한곳에서 조정한다.
    /// </summary>
    public sealed class MatchDecisionCoordinator
    {
        private readonly IBattingDecisionProvider _batting;
        private readonly IPitchingDecisionProvider _pitching;

        public MatchDecisionCoordinator(
            IBattingDecisionProvider batting,
            IPitchingDecisionProvider pitching,
            InterventionLevel interventionLevel = InterventionLevel.Auto)
        {
            _batting = batting ?? throw new ArgumentNullException(nameof(batting));
            _pitching = pitching ?? throw new ArgumentNullException(nameof(pitching));
            InterventionLevel = interventionLevel;
        }

        public InterventionLevel InterventionLevel { get; }
        public BattingApproach GetBattingApproach(DecisionContext context) => _batting.GetApproach(context);
        public PitchingApproach GetPitchingApproach(DecisionContext context) => _pitching.GetApproach(context);

        public static MatchDecisionCoordinator CreateAutomatic()
        {
            return new MatchDecisionCoordinator(
                new SituationalBattingDecisionProvider(),
                new SituationalPitchingDecisionProvider());
        }
    }
}

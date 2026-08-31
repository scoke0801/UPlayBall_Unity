using Baseball.Core.Players;
using Baseball.Simulation.Match;

namespace Baseball.Presentation.Career
{
    /// <summary>Plate/Field Presentation이 공유하는 2D 야구장 위치 계약이다.</summary>
    public static class PlayResolutionFieldLayout
    {
        public static NormalizedFieldPoint Home => new(0.5d, 0.08d);

        public static NormalizedFieldPoint GetBasePoint(int baseNumber)
        {
            return baseNumber switch
            {
                1 => new NormalizedFieldPoint(0.73d, 0.28d),
                2 => new NormalizedFieldPoint(0.5d, 0.49d),
                3 => new NormalizedFieldPoint(0.27d, 0.28d),
                4 => Home,
                _ => Home
            };
        }

        public static NormalizedFieldPoint GetFielderPoint(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => new NormalizedFieldPoint(0.5d, 0.11d),
                PlayerPosition.FirstBase => new NormalizedFieldPoint(0.70d, 0.35d),
                PlayerPosition.SecondBase => new NormalizedFieldPoint(0.61d, 0.49d),
                PlayerPosition.ThirdBase => new NormalizedFieldPoint(0.30d, 0.35d),
                PlayerPosition.Shortstop => new NormalizedFieldPoint(0.39d, 0.49d),
                PlayerPosition.LeftField => new NormalizedFieldPoint(0.24d, 0.72d),
                PlayerPosition.CenterField => new NormalizedFieldPoint(0.5d, 0.84d),
                PlayerPosition.RightField => new NormalizedFieldPoint(0.76d, 0.72d),
                _ => new NormalizedFieldPoint(0.5d, 0.36d)
            };
        }

        public static NormalizedFieldPoint GetBattedBallTarget(in BattedBallDescriptor ball)
        {
            NormalizedFieldPoint point = ball.FieldZone switch
            {
                FieldZone.Pitcher => new NormalizedFieldPoint(0.5d, 0.39d),
                FieldZone.Catcher => new NormalizedFieldPoint(0.5d, 0.13d),
                FieldZone.FirstBase => new NormalizedFieldPoint(0.72d, 0.39d),
                FieldZone.SecondBase => new NormalizedFieldPoint(0.61d, 0.51d),
                FieldZone.ThirdBase => new NormalizedFieldPoint(0.28d, 0.39d),
                FieldZone.Shortstop => new NormalizedFieldPoint(0.39d, 0.51d),
                FieldZone.LeftField => new NormalizedFieldPoint(0.23d, 0.75d),
                FieldZone.CenterField => new NormalizedFieldPoint(0.5d, 0.87d),
                FieldZone.RightField => new NormalizedFieldPoint(0.77d, 0.75d),
                FieldZone.LeftFieldLine => new NormalizedFieldPoint(0.11d, 0.76d),
                FieldZone.RightFieldLine => new NormalizedFieldPoint(0.89d, 0.76d),
                _ => new NormalizedFieldPoint(0.5d, 0.65d)
            };

            if (!ball.IsHomeRun)
                return point;
            return new NormalizedFieldPoint(point.X, 1.04d);
        }
    }
}

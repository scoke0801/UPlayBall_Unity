using System;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>Plate View와 Field View 결과 연출 시간을 판정 수치와 분리해 보관한다.</summary>
    [Serializable]
    public sealed class PlayResolutionPresentationConfig
    {
        [SerializeField, Min(0.05f)] private float batterResponseSeconds = 0.22f;
        [SerializeField, Min(0.05f)] private float impactHoldSeconds = 0.14f;
        [SerializeField, Min(0.1f)] private float plateCallHoldSeconds = 0.42f;
        [SerializeField, Min(0.05f)] private float fieldTransitionSeconds = 0.18f;
        [SerializeField, Min(0.2f)] private float groundBallFlightSeconds = 0.58f;
        [SerializeField, Min(0.2f)] private float lineDriveFlightSeconds = 0.48f;
        [SerializeField, Min(0.2f)] private float flyBallFlightSeconds = 0.82f;
        [SerializeField, Min(0.2f)] private float fielderMoveSeconds = 0.56f;
        [SerializeField, Min(0.05f)] private float pickupHoldSeconds = 0.16f;
        [SerializeField, Min(0.15f)] private float throwFlightSeconds = 0.38f;
        [SerializeField, Min(0.15f)] private float callHoldSeconds = 0.42f;
        [SerializeField, Min(0.2f)] private float resultHoldSeconds = 0.72f;

        public PlayResolutionTiming CreateTiming()
        {
            return new PlayResolutionTiming(
                batterResponseSeconds,
                impactHoldSeconds,
                plateCallHoldSeconds,
                fieldTransitionSeconds,
                groundBallFlightSeconds,
                lineDriveFlightSeconds,
                flyBallFlightSeconds,
                fielderMoveSeconds,
                pickupHoldSeconds,
                throwFlightSeconds,
                callHoldSeconds,
                resultHoldSeconds);
        }

        public double ResolveBattedBallFlightSeconds(in BattedBallDescriptor ball)
        {
            double seconds = ball.Type switch
            {
                BattedBallType.GroundBall or BattedBallType.Bunt => groundBallFlightSeconds,
                BattedBallType.LineDrive => lineDriveFlightSeconds,
                _ => flyBallFlightSeconds
            };
            double paceScale = ball.Pace switch
            {
                BallPaceBand.Fast => 0.84d,
                BallPaceBand.Slow => 1.18d,
                _ => 1d
            };
            return seconds * paceScale;
        }
    }
}

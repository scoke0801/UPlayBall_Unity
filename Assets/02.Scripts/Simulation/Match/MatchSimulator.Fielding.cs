using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    public sealed partial class MatchSimulator
    {
        private static readonly PlayerPosition[] GroundBallPositions =
        {
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop
        };

        private static readonly PlayerPosition[] FlyBallPositions =
        {
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField
        };

        /// <summary>
        /// 인플레이 결과를 실제 야수의 수비 기회로 귀속해 능력치가 아닌 시즌 결과로 수비상을 평가하게 한다.
        /// </summary>
        private static void RecordFieldingOpportunity(
            MatchSimulationState state,
            int inning,
            TeamMatchState defense,
            LineupSlotReference batter,
            PlateAppearanceResult result,
            int outs)
        {
            bool isSuccessful;
            PlayerPosition[] positionPool;
            switch (result)
            {
                case PlateAppearanceResult.GroundOut:
                    isSuccessful = true;
                    positionPool = GroundBallPositions;
                    break;
                case PlateAppearanceResult.FlyOut:
                    isSuccessful = true;
                    positionPool = FlyBallPositions;
                    break;
                case PlateAppearanceResult.Single:
                    isSuccessful = false;
                    positionPool = GroundBallPositions;
                    break;
                case PlateAppearanceResult.Double:
                case PlateAppearanceResult.Triple:
                    isSuccessful = false;
                    positionPool = FlyBallPositions;
                    break;
                default:
                    return;
            }

            uint hash = unchecked((uint)(batter.Player.PlayerId * 397) ^
                                  (uint)(inning * 31) ^
                                  (uint)(outs * 17) ^
                                  (uint)state.NextEventSequence);
            PlayerPosition position = positionPool[hash % (uint)positionPool.Length];
            PlayerFieldingLine line = defense.BoxScore.GetFieldingLine(position);
            double difficulty = CalculateDifficulty(hash, isSuccessful);
            double expectedSuccessRate = ClampExpectedSuccess(
                0.92d - difficulty * 0.72d +
                (GetFielderDefense(defense, position) - 50d) * 0.003d);
            double runValue = GetFieldingRunValue(result);

            line.Opportunities++;
            line.ExpectedOuts += expectedSuccessRate;
            line.EstimatedRunsSaved += ((isSuccessful ? 1d : 0d) - expectedSuccessRate) * runValue;
            if (difficulty >= 0.55d)
                line.DifficultPlayAttempts++;
            if (!isSuccessful)
                return;

            line.SuccessfulPlays++;
            line.Putouts++;
            if (difficulty >= 0.55d)
                line.DifficultPlaysMade++;
        }

        private static int GetFielderDefense(TeamMatchState defense, PlayerPosition position)
        {
            for (int index = 0; index < defense.Team.Lineup.Count; index++)
            {
                if (defense.Team.Lineup[index].FieldingPosition == position)
                    return defense.Team.Lineup[index].Player.BatterAttributes.Defense;
            }

            return 50;
        }

        private static double CalculateDifficulty(uint hash, bool isSuccessful)
        {
            double variation = ((hash >> 8) & 1023U) / 1023d;
            return isSuccessful
                ? 0.12d + variation * 0.63d
                : 0.38d + variation * 0.57d;
        }

        private static double GetFieldingRunValue(PlateAppearanceResult result)
        {
            return result switch
            {
                PlateAppearanceResult.Single => 0.47d,
                PlateAppearanceResult.Double => 0.78d,
                PlateAppearanceResult.Triple => 1.05d,
                _ => 0.30d
            };
        }

        private static double ClampExpectedSuccess(double value)
        {
            if (value < 0.05d) return 0.05d;
            if (value > 0.98d) return 0.98d;
            return value;
        }
    }
}

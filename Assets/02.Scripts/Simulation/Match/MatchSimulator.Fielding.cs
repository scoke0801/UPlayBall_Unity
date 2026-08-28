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
            bool pitcherPlay = positionPool == GroundBallPositions && hash % 10U == 0U;
            bool catcherPlay = positionPool == GroundBallPositions && !pitcherPlay && hash % 20U == 1U;
            PlayerPosition position = pitcherPlay
                ? defense.ActivePitcher.PrimaryPosition
                : catcherPlay
                    ? PlayerPosition.Catcher
                    : SelectFielderPosition(defense, positionPool, hash, isSuccessful);
            PlayerFieldingLine line = pitcherPlay
                ? defense.BoxScore.GetFieldingLineByPlayer(defense.ActivePitcher.PlayerId)
                : defense.BoxScore.GetFieldingLine(position);
            double difficulty = CalculateDifficulty(hash);
            double expectedSuccessRate = ClampExpectedSuccess(0.92d - difficulty * 0.72d);
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

        /// <summary>
        /// 팀 단위 인플레이 결과를 개별 야수에게 귀속한다. 좋은 야수는 아웃 타구를,
        /// 약한 야수는 안타 타구를 조금 더 자주 맡되 수상 점수에는 능력치를 직접 넣지 않는다.
        /// </summary>
        private static PlayerPosition SelectFielderPosition(
            TeamMatchState defense,
            PlayerPosition[] positionPool,
            uint hash,
            bool isSuccessful)
        {
            double totalWeight = 0d;
            for (int index = 0; index < positionPool.Length; index++)
                totalWeight += GetAttributionWeight(
                    GetFielderDefense(defense, positionPool[index]),
                    isSuccessful);

            double unit = ((hash >> 12) & 65535U) / 65536d;
            double selectedWeight = unit * totalWeight;
            for (int index = 0; index < positionPool.Length; index++)
            {
                selectedWeight -= GetAttributionWeight(
                    GetFielderDefense(defense, positionPool[index]),
                    isSuccessful);
                if (selectedWeight < 0d)
                    return positionPool[index];
            }
            return positionPool[positionPool.Length - 1];
        }

        private static double GetAttributionWeight(int defenseRating, bool isSuccessful)
        {
            return isSuccessful
                ? 0.75d + defenseRating * 0.005d
                : 1.25d - defenseRating * 0.005d;
        }

        private static int GetFielderDefense(TeamMatchState defense, PlayerPosition position)
        {
            if (position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
                return defense.ActivePitcher.BatterAttributes.Defense;
            for (int index = 0; index < defense.Team.Lineup.Count; index++)
            {
                if (defense.Team.Lineup[index].FieldingPosition == position)
                    return defense.Team.Lineup[index].Player.BatterAttributes.Defense;
            }

            return 50;
        }

        private static double CalculateDifficulty(uint hash)
        {
            double variation = ((hash >> 8) & 1023U) / 1023d;
            return 0.05d + variation * 0.90d;
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

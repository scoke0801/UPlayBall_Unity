using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 타자의 능력치 조합을 역할별로 평가해 결정론적인 아홉 명 타순을 편성한다.
    /// </summary>
    public sealed class ManagerLineupAi
    {
        private readonly ManagerLineupBalance _balance;

        public ManagerLineupAi(ManagerLineupBalance balance)
        {
            _balance = balance;
        }

        /// <summary>
        /// 수비 위치가 확정된 아홉 선수를 상위·중심·하위 타선으로 재배치한다.
        /// </summary>
        public Lineup BuildLineup(LineupSlot[] fieldingAssignments)
        {
            if (fieldingAssignments == null)
                throw new ArgumentNullException(nameof(fieldingAssignments));
            if (fieldingAssignments.Length != BaseballRules.BattingOrderSize)
                throw new ArgumentException("타순 편성에는 정확히 9명의 야수가 필요합니다.", nameof(fieldingAssignments));

            int assignedMask = 0;
            var battingOrder = new LineupSlot[fieldingAssignments.Length];

            // 가장 대체하기 어려운 4번 역할을 먼저 배정해야 출루형 선수가 장타자 자리를 선점하지 않는다.
            AssignBest(fieldingAssignments, ref assignedMask, battingOrder, 3, _balance.Cleanup);
            AssignBest(fieldingAssignments, ref assignedMask, battingOrder, 0, _balance.Leadoff);
            AssignBest(fieldingAssignments, ref assignedMask, battingOrder, 1, _balance.TableSetter);
            AssignBest(fieldingAssignments, ref assignedMask, battingOrder, 2, _balance.RunProducer);
            AssignBest(fieldingAssignments, ref assignedMask, battingOrder, 4, _balance.RunProducer);

            for (int battingOrderIndex = 5; battingOrderIndex < battingOrder.Length; battingOrderIndex++)
            {
                AssignBest(
                    fieldingAssignments,
                    ref assignedMask,
                    battingOrder,
                    battingOrderIndex,
                    _balance.LowerOrder);
            }

            return new Lineup(battingOrder);
        }

        private static void AssignBest(
            LineupSlot[] candidates,
            ref int assignedMask,
            LineupSlot[] battingOrder,
            int battingOrderIndex,
            BattingOrderScoreWeights weights)
        {
            int bestIndex = -1;
            double bestScore = double.MinValue;
            int bestPlayerId = int.MaxValue;

            for (int index = 0; index < candidates.Length; index++)
            {
                if ((assignedMask & (1 << index)) != 0)
                    continue;

                Player player = candidates[index].Player;
                if (player == null)
                    throw new ArgumentException("타순 후보 선수는 비어 있을 수 없습니다.", nameof(candidates));

                double score = CalculateScore(player.BatterAttributes, weights);
                if (score > bestScore ||
                    Math.Abs(score - bestScore) <= 0.000001d && player.PlayerId < bestPlayerId)
                {
                    bestIndex = index;
                    bestScore = score;
                    bestPlayerId = player.PlayerId;
                }
            }

            if (bestIndex < 0)
                throw new InvalidOperationException("배치할 수 있는 타순 후보가 없습니다.");

            assignedMask |= 1 << bestIndex;
            battingOrder[battingOrderIndex] = candidates[bestIndex];
        }

        private static double CalculateScore(
            BatterAttributes attributes,
            BattingOrderScoreWeights weights)
        {
            // 현재 타석 모델에서 Bunt는 전술 실행 능력이고 Defense는 수비 가치이므로 타순 평가에서 제외한다.
            return attributes.Contact * weights.Contact +
                   attributes.Power * weights.Power +
                   attributes.Speed * weights.Speed +
                   attributes.Mental * weights.Mental;
        }
    }
}

using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 아홉 명의 타순과 중복 없는 수비 포지션 배치를 보관한다.
    /// </summary>
    public sealed class Lineup
    {
        private readonly LineupSlot[] _slots;

        /// <summary>
        /// 완전한 9인 타순을 복사하고 야구 규칙상 유효성을 검증한다.
        /// </summary>
        public Lineup(LineupSlot[] slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));
            if (slots.Length != BaseballRules.BattingOrderSize)
                throw new ArgumentException("Lineup은 정확히 9개의 타순을 가져야 합니다.", nameof(slots));

            _slots = new LineupSlot[slots.Length];
            Array.Copy(slots, _slots, slots.Length);
            ValidateSlots();
        }

        public int Count => _slots.Length;

        public LineupSlot this[int battingOrderIndex] => _slots[battingOrderIndex];

        /// <summary>
        /// 현재 배치와 포지션 적응도를 반영한 팀 평균 수비력을 계산한다.
        /// </summary>
        public double CalculateDefenseRating()
        {
            double total = 0d;
            int fielderCount = 0;

            for (int index = 0; index < _slots.Length; index++)
            {
                LineupSlot slot = _slots[index];
                if (slot.FieldingPosition == PlayerPosition.DesignatedHitter)
                    continue;

                int proficiency = slot.Player.GetPositionProficiency(slot.FieldingPosition);
                total += slot.Player.BatterAttributes.Defense * proficiency / 100d;
                fielderCount++;
            }

            return fielderCount == 0 ? 0d : total / fielderCount;
        }

        private void ValidateSlots()
        {
            var occupiedPositions = new bool[BaseballRules.BattingOrderSize + 1];

            for (int index = 0; index < _slots.Length; index++)
            {
                LineupSlot slot = _slots[index];
                int positionIndex = (int)slot.FieldingPosition;
                if (positionIndex < (int)PlayerPosition.Catcher ||
                    positionIndex > (int)PlayerPosition.DesignatedHitter)
                {
                    throw new ArgumentException("Lineup에는 야수 포지션만 배치할 수 있습니다.", nameof(_slots));
                }

                if (occupiedPositions[positionIndex])
                    throw new ArgumentException("Lineup의 수비 포지션은 중복될 수 없습니다.", nameof(_slots));

                occupiedPositions[positionIndex] = true;

                for (int previous = 0; previous < index; previous++)
                {
                    if (_slots[previous].Player.PlayerId == slot.Player.PlayerId)
                        throw new ArgumentException("한 선수는 Lineup에 한 번만 배치할 수 있습니다.", nameof(_slots));
                }
            }
        }
    }
}

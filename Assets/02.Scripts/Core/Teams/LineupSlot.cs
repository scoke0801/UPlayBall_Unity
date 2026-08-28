using System;
using Baseball.Core.Players;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 타순 한 자리의 선수와 실제 수비 포지션을 묶는다.
    /// </summary>
    public readonly struct LineupSlot
    {
        /// <summary>
        /// 타순에 배치할 선수와 수비 포지션을 생성한다.
        /// </summary>
        public LineupSlot(Player player, PlayerPosition fieldingPosition)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            FieldingPosition = fieldingPosition;
        }

        public Player Player { get; }
        public PlayerPosition FieldingPosition { get; }
    }
}

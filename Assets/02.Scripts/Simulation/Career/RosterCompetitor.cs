using System;
using Baseball.Core.Players;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 새 게임 계약 비교에 필요한 기존 로스터 경쟁자의 최소 정보를 보관한다.
    /// </summary>
    public readonly struct RosterCompetitor
    {
        public RosterCompetitor(int playerId, string name, PlayerPosition position, int overall)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("선수 이름은 비어 있을 수 없습니다.", nameof(name));
            if (position == PlayerPosition.Unknown)
                throw new ArgumentException("경쟁자 포지션이 필요합니다.", nameof(position));
            if (overall < 0 || overall > 100)
                throw new ArgumentOutOfRangeException(nameof(overall));

            PlayerId = playerId;
            Name = name;
            Position = position;
            Overall = overall;
        }

        public int PlayerId { get; }
        public string Name { get; }
        public PlayerPosition Position { get; }
        public int Overall { get; }
    }
}

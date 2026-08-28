using System;
using Baseball.Core.Players;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 한 경기에 참가하는 구단의 라인업과 선발 투수를 보관한다.
    /// </summary>
    public sealed class Team
    {
        /// <summary>
        /// 경기 가능한 구단 입력을 생성한다.
        /// </summary>
        public Team(int teamId, string name, Lineup lineup, Player startingPitcher)
            : this(teamId, name, lineup, startingPitcher, null, 0)
        {
        }

        /// <summary>
        /// 선발과 지정 이닝부터 등판할 구원투수를 포함한 경기 입력을 생성한다.
        /// </summary>
        public Team(
            int teamId,
            string name,
            Lineup lineup,
            Player startingPitcher,
            Player reliefPitcher,
            int reliefStartInning)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId), "TeamId는 양수여야 합니다.");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(name));

            TeamId = teamId;
            Name = name;
            Lineup = lineup ?? throw new ArgumentNullException(nameof(lineup));
            StartingPitcher = startingPitcher ?? throw new ArgumentNullException(nameof(startingPitcher));
            ReliefPitcher = reliefPitcher;
            ReliefStartInning = reliefStartInning;

            if (reliefPitcher != null)
            {
                if (reliefStartInning < 2)
                    throw new ArgumentOutOfRangeException(nameof(reliefStartInning));
                if (reliefPitcher.PlayerId == startingPitcher.PlayerId)
                    throw new ArgumentException("선발과 구원투수는 서로 달라야 합니다.", nameof(reliefPitcher));
            }
            else if (reliefStartInning != 0)
            {
                throw new ArgumentException("구원투수가 없으면 교체 이닝도 지정할 수 없습니다.", nameof(reliefStartInning));
            }

            for (int index = 0; index < lineup.Count; index++)
            {
                int playerId = lineup[index].Player.PlayerId;
                if (playerId == startingPitcher.PlayerId || playerId == reliefPitcher?.PlayerId)
                    throw new ArgumentException("투수를 타순에 중복 배치할 수 없습니다.", nameof(startingPitcher));
            }
        }

        public int TeamId { get; }
        public string Name { get; }
        public Lineup Lineup { get; }
        public Player StartingPitcher { get; }
        public Player ReliefPitcher { get; }
        public int ReliefStartInning { get; }

        /// <summary>
        /// 지정 이닝 전에는 선발, 이후에는 등록된 구원투수를 반환한다.
        /// </summary>
        public Player GetPitcherForInning(int inning)
        {
            return ReliefPitcher != null && inning >= ReliefStartInning
                ? ReliefPitcher
                : StartingPitcher;
        }
    }
}

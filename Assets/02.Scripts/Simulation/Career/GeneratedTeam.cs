using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 새 게임에서 Seed로 생성된 구단 하나와 포지션별 필요도를 보관한다.
    /// </summary>
    public sealed class GeneratedTeam
    {
        private readonly int[] _positionNeedRatings;
        private readonly RosterCompetitor[][] _competitorsByPosition;

        /// <summary>
        /// 생성된 구단 정보를 구성한다. positionNeedRatings는 PlayerPosition 값을 인덱스로 사용한다.
        /// </summary>
        public GeneratedTeam(int teamId, string name, TeamArchetypeProfile archetype, int[] positionNeedRatings)
            : this(
                teamId,
                name,
                archetype,
                new TeamColor(128, 128, 128),
                positionNeedRatings,
                Array.Empty<RosterCompetitor>())
        {
        }

        /// <summary>
        /// 대표색과 포지션 경쟁자까지 포함한 생성 구단 정보를 구성한다.
        /// </summary>
        public GeneratedTeam(
            int teamId,
            string name,
            TeamArchetypeProfile archetype,
            TeamColor primaryColor,
            int[] positionNeedRatings,
            RosterCompetitor[] competitors)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId), "TeamId는 양수여야 합니다.");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(name));

            TeamId = teamId;
            Name = name;
            Archetype = archetype;
            PrimaryColor = primaryColor;
            if (positionNeedRatings == null || positionNeedRatings.Length <= (int)PlayerPosition.ReliefPitcher)
                throw new ArgumentException("모든 포지션 필요도가 필요합니다.", nameof(positionNeedRatings));

            _positionNeedRatings = (int[])positionNeedRatings.Clone();
            _competitorsByPosition = BuildCompetitorIndex(competitors);
        }

        public int TeamId { get; }
        public string Name { get; }
        public TeamArchetypeProfile Archetype { get; }
        public TeamColor PrimaryColor { get; }

        /// <summary>
        /// 0~100 범위의 포지션 필요도를 반환한다. 값이 높을수록 그 포지션 자원이 부족한 구단이다.
        /// </summary>
        public int GetPositionNeed(PlayerPosition position)
        {
            return _positionNeedRatings[(int)position];
        }

        /// <summary>
        /// 계약 비교 화면에 표시할 같은 포지션 경쟁자 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<RosterCompetitor> GetPositionCompetitors(PlayerPosition position)
        {
            return _competitorsByPosition[(int)position];
        }

        private static RosterCompetitor[][] BuildCompetitorIndex(RosterCompetitor[] competitors)
        {
            int positionCount = (int)PlayerPosition.ReliefPitcher + 1;
            var counts = new int[positionCount];
            if (competitors != null)
            {
                for (int index = 0; index < competitors.Length; index++)
                    counts[(int)competitors[index].Position]++;
            }

            var result = new RosterCompetitor[positionCount][];
            for (int position = 0; position < positionCount; position++)
                result[position] = new RosterCompetitor[counts[position]];

            Array.Clear(counts, 0, counts.Length);
            if (competitors == null)
                return result;

            for (int index = 0; index < competitors.Length; index++)
            {
                RosterCompetitor competitor = competitors[index];
                int position = (int)competitor.Position;
                result[position][counts[position]++] = competitor;
            }

            return result;
        }
    }
}

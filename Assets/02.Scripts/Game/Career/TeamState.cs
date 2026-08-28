using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 대상이 되는 구단의 커리어 런타임 상태다. 새 게임에서 생성된 `GeneratedTeam`을
    /// 세이브 가능한 형태로 옮겨 담는다.
    /// </summary>
    public sealed class TeamState
    {
        private readonly int[] _positionNeedRatings;
        private readonly RosterCompetitorState[] _rosterCompetitors;

        /// <summary>
        /// 새 게임에서 확정된 구단 상태를 생성한다.
        /// </summary>
        public TeamState(int saveVersion, int teamId, string name, TeamArchetypeProfile archetype)
            : this(
                saveVersion,
                teamId,
                name,
                archetype,
                new TeamColor(128, 128, 128),
                new int[(int)PlayerPosition.ReliefPitcher + 1],
                Array.Empty<RosterCompetitorState>())
        {
        }

        /// <summary>
        /// 새 게임에서 생성된 구단의 대표색·필요도·초기 경쟁자까지 고정해 보관한다.
        /// </summary>
        public TeamState(
            int saveVersion,
            int teamId,
            string name,
            TeamArchetypeProfile archetype,
            TeamColor primaryColor,
            int[] positionNeedRatings,
            RosterCompetitorState[] rosterCompetitors)
        {
            SaveVersion = saveVersion;
            TeamId = teamId;
            Name = name;
            Archetype = archetype;
            PrimaryColor = primaryColor;
            _positionNeedRatings = (int[])positionNeedRatings.Clone();
            _rosterCompetitors = (RosterCompetitorState[])rosterCompetitors.Clone();
        }

        public int SaveVersion { get; }
        public int TeamId { get; }
        public string Name { get; }
        public TeamArchetypeProfile Archetype { get; }
        public TeamColor PrimaryColor { get; }
        public IReadOnlyList<RosterCompetitorState> RosterCompetitors => _rosterCompetitors;

        /// <summary>
        /// 저장 시점에 고정된 포지션 필요도를 반환한다.
        /// </summary>
        public int GetPositionNeed(PlayerPosition position)
        {
            return _positionNeedRatings[(int)position];
        }

        /// <summary>
        /// 감독 기용 판단에 사용할 같은 포지션 최고 경쟁자 Overall을 반환한다.
        /// </summary>
        public int GetStrongestCompetitorOverall(PlayerPosition position)
        {
            int strongest = 0;
            for (int index = 0; index < _rosterCompetitors.Length; index++)
            {
                RosterCompetitorState competitor = _rosterCompetitors[index];
                if (competitor.Position == position && competitor.Overall > strongest)
                    strongest = competitor.Overall;
            }
            return strongest;
        }

        /// <summary>
        /// 지정 포지션에서 Overall이 가장 높은 기존 로스터 선수를 반환한다.
        /// </summary>
        public RosterCompetitorState GetStrongestCompetitor(PlayerPosition position)
        {
            bool found = false;
            RosterCompetitorState strongest = default;
            for (int index = 0; index < _rosterCompetitors.Length; index++)
            {
                RosterCompetitorState competitor = _rosterCompetitors[index];
                if (competitor.Position != position)
                    continue;
                if (!found || competitor.Overall > strongest.Overall)
                {
                    strongest = competitor;
                    found = true;
                }
            }

            if (!found)
                throw new InvalidOperationException($"{position} 경쟁자가 없습니다.");
            return strongest;
        }

        /// <summary>
        /// 시즌 전환으로 갱신된 로스터를 반영한, 같은 정체성의 다음 시즌 구단 상태를 만든다.
        /// </summary>
        public TeamState WithRoster(RosterCompetitorState[] rosterCompetitors)
        {
            return new TeamState(
                SaveVersion,
                TeamId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                rosterCompetitors);
        }

        /// <summary>
        /// 경기 입력 생성 시 지정 포지션의 순번에 해당하는 경쟁자를 반환한다.
        /// </summary>
        public RosterCompetitorState GetCompetitor(PlayerPosition position, int selectionIndex)
        {
            int count = 0;
            for (int index = 0; index < _rosterCompetitors.Length; index++)
            {
                if (_rosterCompetitors[index].Position != position)
                    continue;
                if (count == selectionIndex)
                    return _rosterCompetitors[index];
                count++;
            }

            if (selectionIndex > 0)
                return GetCompetitor(position, 0);
            throw new InvalidOperationException($"{position} 경쟁자가 없습니다.");
        }
    }

    /// <summary>
    /// 생성된 기존 로스터에서 계약 비교와 이후 경쟁 로직에 필요한 상태다.
    /// </summary>
    public readonly struct RosterCompetitorState
    {
        public RosterCompetitorState(int playerId, string name, PlayerPosition position, int overall)
            : this(
                playerId,
                name,
                position,
                overall,
                careerPlateAppearances: 0,
                careerPitchingOuts: 0,
                registeredSeasons: 0)
        {
        }

        public RosterCompetitorState(
            int playerId,
            string name,
            PlayerPosition position,
            int overall,
            int careerPlateAppearances,
            int careerPitchingOuts,
            int registeredSeasons)
        {
            if (careerPlateAppearances < 0) throw new ArgumentOutOfRangeException(nameof(careerPlateAppearances));
            if (careerPitchingOuts < 0) throw new ArgumentOutOfRangeException(nameof(careerPitchingOuts));
            if (registeredSeasons < 0) throw new ArgumentOutOfRangeException(nameof(registeredSeasons));
            PlayerId = playerId;
            Name = name;
            Position = position;
            Overall = overall;
            CareerPlateAppearances = careerPlateAppearances;
            CareerPitchingOuts = careerPitchingOuts;
            RegisteredSeasons = registeredSeasons;
        }

        public int PlayerId { get; }
        public string Name { get; }
        public PlayerPosition Position { get; }
        public int Overall { get; }
        public int CareerPlateAppearances { get; }
        public int CareerPitchingOuts { get; }
        public int RegisteredSeasons { get; }
    }
}

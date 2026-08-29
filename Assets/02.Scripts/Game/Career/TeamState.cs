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
        private readonly int[] _rosterPlayerIds;

        /// <summary>
        /// 새 게임에서 확정된 구단 상태를 생성한다.
        /// </summary>
        public TeamState(int saveVersion, int teamId, string name, TeamArchetypeProfile archetype)
            : this(
                saveVersion,
                teamId,
                LeagueId.Unassigned,
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
            : this(
                saveVersion,
                teamId,
                LeagueId.Unassigned,
                name,
                archetype,
                primaryColor,
                positionNeedRatings,
                rosterCompetitors)
        {
        }

        /// <summary>
        /// 월드에서 영구 소속 리그까지 확정된 구단 상태를 생성한다.
        /// </summary>
        public TeamState(
            int saveVersion,
            int teamId,
            LeagueId leagueId,
            string name,
            TeamArchetypeProfile archetype,
            TeamColor primaryColor,
            int[] positionNeedRatings,
            RosterCompetitorState[] rosterCompetitors)
            : this(
                saveVersion,
                teamId,
                leagueId,
                name,
                archetype,
                primaryColor,
                positionNeedRatings,
                rosterCompetitors,
                rosterPlayerIds: null)
        {
        }

        private TeamState(
            int saveVersion,
            int teamId,
            LeagueId leagueId,
            string name,
            TeamArchetypeProfile archetype,
            TeamColor primaryColor,
            int[] positionNeedRatings,
            RosterCompetitorState[] rosterCompetitors,
            int[] rosterPlayerIds)
        {
            SaveVersion = saveVersion;
            TeamId = teamId;
            LeagueId = leagueId;
            Name = name;
            Archetype = archetype;
            PrimaryColor = primaryColor;
            _positionNeedRatings = (int[])positionNeedRatings.Clone();
            _rosterCompetitors = (RosterCompetitorState[])rosterCompetitors.Clone();
            _rosterPlayerIds = rosterPlayerIds == null
                ? BuildRosterPlayerIds(_rosterCompetitors)
                : (int[])rosterPlayerIds.Clone();
        }

        public int SaveVersion { get; }
        public int TeamId { get; }
        public LeagueId LeagueId { get; }
        public string Name { get; }
        public TeamArchetypeProfile Archetype { get; }
        public TeamColor PrimaryColor { get; }
        public IReadOnlyList<RosterCompetitorState> RosterCompetitors => _rosterCompetitors;
        public IReadOnlyList<int> RosterPlayerIds => _rosterPlayerIds;

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
                LeagueId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                rosterCompetitors,
                MergePersistentRosterIds(rosterCompetitors));
        }

        public TeamState WithRosterAndPlayerIds(
            RosterCompetitorState[] rosterCompetitors,
            int[] rosterPlayerIds)
        {
            return new TeamState(
                SaveVersion,
                TeamId,
                LeagueId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                rosterCompetitors,
                rosterPlayerIds);
        }

        public TeamState WithRosteredPlayer(int playerId)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            for (int index = 0; index < _rosterPlayerIds.Length; index++)
            {
                if (_rosterPlayerIds[index] == playerId)
                    return this;
            }
            var rosterPlayerIds = new int[_rosterPlayerIds.Length + 1];
            Array.Copy(_rosterPlayerIds, rosterPlayerIds, _rosterPlayerIds.Length);
            rosterPlayerIds[^1] = playerId;
            Array.Sort(rosterPlayerIds);
            return new TeamState(
                SaveVersion,
                TeamId,
                LeagueId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                _rosterCompetitors,
                rosterPlayerIds);
        }

        /// <summary>은퇴·방출된 선수를 경쟁자 스냅샷과 영구 로스터 ID에서 함께 제거한다.</summary>
        public TeamState WithoutRosteredPlayer(int playerId)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));

            int competitorCount = 0;
            for (int index = 0; index < _rosterCompetitors.Length; index++)
            {
                if (_rosterCompetitors[index].PlayerId != playerId)
                    competitorCount++;
            }
            int rosterCount = 0;
            for (int index = 0; index < _rosterPlayerIds.Length; index++)
            {
                if (_rosterPlayerIds[index] != playerId)
                    rosterCount++;
            }
            if (rosterCount == _rosterPlayerIds.Length)
                throw new InvalidOperationException($"PlayerId {playerId}가 구단 로스터에 없습니다.");

            var competitors = new RosterCompetitorState[competitorCount];
            var rosterPlayerIds = new int[rosterCount];
            int competitorIndex = 0;
            int rosterIndex = 0;
            for (int index = 0; index < _rosterCompetitors.Length; index++)
            {
                if (_rosterCompetitors[index].PlayerId != playerId)
                    competitors[competitorIndex++] = _rosterCompetitors[index];
            }
            for (int index = 0; index < _rosterPlayerIds.Length; index++)
            {
                if (_rosterPlayerIds[index] != playerId)
                    rosterPlayerIds[rosterIndex++] = _rosterPlayerIds[index];
            }
            return new TeamState(
                SaveVersion,
                TeamId,
                LeagueId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                competitors,
                rosterPlayerIds);
        }

        /// <summary>
        /// v7 단일 리그 구단에 마이그레이션으로 영구 리그 ID를 부여한다.
        /// </summary>
        public TeamState WithLeague(LeagueId leagueId)
        {
            if (!leagueId.IsAssigned)
                throw new ArgumentException("구단에는 유효한 LeagueId가 필요합니다.", nameof(leagueId));
            return new TeamState(
                SaveVersion,
                TeamId,
                leagueId,
                Name,
                Archetype,
                PrimaryColor,
                _positionNeedRatings,
                _rosterCompetitors,
                _rosterPlayerIds);
        }

        private int[] MergePersistentRosterIds(RosterCompetitorState[] rosterCompetitors)
        {
            int persistentCount = 0;
            for (int rosterIndex = 0; rosterIndex < _rosterPlayerIds.Length; rosterIndex++)
            {
                if (!ContainsCompetitor(_rosterCompetitors, _rosterPlayerIds[rosterIndex]))
                    persistentCount++;
            }

            var result = new int[rosterCompetitors.Length + persistentCount];
            int resultIndex = 0;
            for (int index = 0; index < rosterCompetitors.Length; index++)
                result[resultIndex++] = rosterCompetitors[index].PlayerId;
            for (int index = 0; index < _rosterPlayerIds.Length; index++)
            {
                int playerId = _rosterPlayerIds[index];
                if (!ContainsCompetitor(_rosterCompetitors, playerId))
                    result[resultIndex++] = playerId;
            }
            Array.Sort(result);
            return result;
        }

        private static int[] BuildRosterPlayerIds(RosterCompetitorState[] rosterCompetitors)
        {
            var result = new int[rosterCompetitors.Length];
            for (int index = 0; index < rosterCompetitors.Length; index++)
                result[index] = rosterCompetitors[index].PlayerId;
            Array.Sort(result);
            return result;
        }

        private static bool ContainsCompetitor(RosterCompetitorState[] competitors, int playerId)
        {
            for (int index = 0; index < competitors.Length; index++)
            {
                if (competitors[index].PlayerId == playerId)
                    return true;
            }
            return false;
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

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 최근 3일의 투구 수를 경기 입력에 고정해 불펜 가용성을 재현 가능하게 만든다.
    /// </summary>
    public readonly struct RecentPitchingWorkload
    {
        public RecentPitchingWorkload(int previousDayPitches, int twoDaysAgoPitches, int threeDaysAgoPitches)
        {
            if (previousDayPitches < 0) throw new ArgumentOutOfRangeException(nameof(previousDayPitches));
            if (twoDaysAgoPitches < 0) throw new ArgumentOutOfRangeException(nameof(twoDaysAgoPitches));
            if (threeDaysAgoPitches < 0) throw new ArgumentOutOfRangeException(nameof(threeDaysAgoPitches));
            PreviousDayPitches = previousDayPitches;
            TwoDaysAgoPitches = twoDaysAgoPitches;
            ThreeDaysAgoPitches = threeDaysAgoPitches;
        }

        public int PreviousDayPitches { get; }
        public int TwoDaysAgoPitches { get; }
        public int ThreeDaysAgoPitches { get; }
        public bool HasAnyWork => PreviousDayPitches + TwoDaysAgoPitches + ThreeDaysAgoPitches > 0;
    }

    /// <summary>
    /// 한 투수의 당일 상태·역할·최근 부하를 경기 시작 시점에 잠근다.
    /// </summary>
    public sealed class PitcherRosterEntry
    {
        public PitcherRosterEntry(
            Player player,
            PitcherRole role,
            int condition = 100,
            RecentPitchingWorkload recentWorkload = default,
            int pitchLimit = 0,
            PitcherRole? naturalRole = null,
            ActiveRosterRole? activeRosterRole = null,
            string playerSeasonId = null)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(nameof(condition));
            if (pitchLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(pitchLimit));
            if (player.PrimaryPosition != PlayerPosition.StartingPitcher &&
                player.PrimaryPosition != PlayerPosition.ReliefPitcher)
            {
                throw new ArgumentException("투수 포지션 선수만 투수 엔트리에 등록할 수 있습니다.", nameof(player));
            }

            Player = player;
            Role = role;
            NaturalRole = naturalRole ?? role;
            ActiveRosterRole = activeRosterRole;
            PlayerSeasonId = playerSeasonId?.Trim() ?? string.Empty;
            if (activeRosterRole.HasValue)
            {
                ActiveRosterRole rosterRole = activeRosterRole.Value;
                bool isSupportedRole = ActiveRosterCompositionRule.Standard.IsBullpenRole(rosterRole) ||
                                       rosterRole is Baseball.Core.Historical.ActiveRosterRole.Setup or
                                           Baseball.Core.Historical.ActiveRosterRole.Closer;
                if (!isSupportedRole)
                    throw new ArgumentException("경기 투수 엔트리에는 Bullpen 1~4, Setup, Closer만 지정할 수 있습니다.", nameof(activeRosterRole));
                if (PlayerSeasonId.Length == 0)
                    throw new ArgumentException("ActiveRosterRole을 연결하려면 PlayerSeasonId가 필요합니다.", nameof(playerSeasonId));
            }
            Condition = condition;
            RecentWorkload = recentWorkload;
            PitchLimit = pitchLimit;
        }

        public Player Player { get; }
        public PitcherRole Role { get; }
        public PitcherRole NaturalRole { get; }
        public ActiveRosterRole? ActiveRosterRole { get; }
        public string PlayerSeasonId { get; }
        public int Condition { get; }
        public RecentPitchingWorkload RecentWorkload { get; }
        public int PitchLimit { get; }
    }

    /// <summary>
    /// 시즌 로스터에서 경기 당일에 사용할 라인업·벤치·불펜·감독 성향을 불변으로 복사한다.
    /// </summary>
    public sealed class MatchRosterSnapshot
    {
        private readonly Player[] _bench;
        private readonly PitcherRosterEntry[] _bullpen;

        public MatchRosterSnapshot(
            int teamId,
            string teamName,
            Lineup startingLineup,
            PitcherRosterEntry startingPitcher,
            IReadOnlyList<PitcherRosterEntry> bullpen,
            IReadOnlyList<Player> bench,
            ManagerTacticalProfile managerProfile,
            RunningApproach runningApproach,
            int playerCharacterId = 0)
        {
            if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));
            if (string.IsNullOrWhiteSpace(teamName)) throw new ArgumentException("구단 이름이 필요합니다.", nameof(teamName));
            TeamId = teamId;
            TeamName = teamName;
            StartingLineup = startingLineup ?? throw new ArgumentNullException(nameof(startingLineup));
            StartingPitcher = startingPitcher ?? throw new ArgumentNullException(nameof(startingPitcher));
            if (startingPitcher.Role != PitcherRole.Starter)
                throw new ArgumentException("선발투수 엔트리의 역할은 Starter여야 합니다.", nameof(startingPitcher));

            _bullpen = CopyPitchers(bullpen);
            _bench = CopyPlayers(bench);
            ManagerProfile = managerProfile;
            RunningApproach = runningApproach;
            PlayerCharacterId = playerCharacterId;
            ValidateUniquePlayers();
        }

        public int TeamId { get; }
        public string TeamName { get; }
        public Lineup StartingLineup { get; }
        public PitcherRosterEntry StartingPitcher { get; }
        public IReadOnlyList<PitcherRosterEntry> Bullpen => _bullpen;
        public IReadOnlyList<Player> Bench => _bench;
        public ManagerTacticalProfile ManagerProfile { get; }
        public RunningApproach RunningApproach { get; }
        public int PlayerCharacterId { get; }

        /// <summary>
        /// 기존 Team 입력을 V2 경기 스냅샷으로 변환한다.
        /// </summary>
        public static MatchRosterSnapshot FromTeam(Team team)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            PitcherRosterEntry[] bullpen = team.ReliefPitcher == null
                ? Array.Empty<PitcherRosterEntry>()
                : new[]
                {
                    new PitcherRosterEntry(team.ReliefPitcher, PitcherRole.MiddleRelief)
                };
            Player[] bench = team.PositionPlayerSubstitution == null
                ? Array.Empty<Player>()
                : new[] { team.PositionPlayerSubstitution.Player };
            return new MatchRosterSnapshot(
                team.TeamId,
                team.Name,
                team.Lineup,
                new PitcherRosterEntry(team.StartingPitcher, PitcherRole.Starter),
                bullpen,
                bench,
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        /// <summary>
        /// 아직 Team을 읽는 표현·테스트 호출부를 위한 손실 허용 어댑터를 만든다.
        /// </summary>
        public Team ToCompatibilityTeam()
        {
            Player relief = _bullpen.Length == 0 ? null : _bullpen[0].Player;
            return new Team(
                TeamId,
                TeamName,
                StartingLineup,
                StartingPitcher.Player,
                relief,
                relief == null ? 0 : 7);
        }

        private static PitcherRosterEntry[] CopyPitchers(IReadOnlyList<PitcherRosterEntry> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PitcherRosterEntry>();
            var result = new PitcherRosterEntry[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("불펜 엔트리가 비어 있습니다.", nameof(source));
            return result;
        }

        private static Player[] CopyPlayers(IReadOnlyList<Player> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<Player>();
            var result = new Player[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("벤치 엔트리가 비어 있습니다.", nameof(source));
            return result;
        }

        private void ValidateUniquePlayers()
        {
            int totalCount = StartingLineup.Count + 1 + _bullpen.Length + _bench.Length;
            var ids = new int[totalCount];
            int idIndex = 0;
            for (int index = 0; index < StartingLineup.Count; index++)
                ids[idIndex++] = StartingLineup[index].Player.PlayerId;
            ids[idIndex++] = StartingPitcher.Player.PlayerId;
            for (int index = 0; index < _bullpen.Length; index++)
                ids[idIndex++] = _bullpen[index].Player.PlayerId;
            for (int index = 0; index < _bench.Length; index++)
                ids[idIndex++] = _bench[index].PlayerId;
            Array.Sort(ids);
            for (int index = 1; index < ids.Length; index++)
            {
                if (ids[index] == ids[index - 1])
                    throw new ArgumentException($"PlayerId {ids[index]}가 경기 로스터에 중복 등록되었습니다.");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>한 선수에게 경기 시작 시 동결된 파생 Condition과 근거를 연결한다.</summary>
    public readonly struct MatchPlayerConditionEntry
    {
        public MatchPlayerConditionEntry(int playerId, EffectiveMatchCondition condition)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
            Condition = condition;
        }

        public int PlayerId { get; }
        public EffectiveMatchCondition Condition { get; }
    }

    /// <summary>투수·포수 교체 때 누적 없이 교체할 Battery Condition 변경량이다.</summary>
    public readonly struct MatchBatteryConditionEntry
    {
        public MatchBatteryConditionEntry(int pitcherPlayerId, int catcherPlayerId, int conditionModifier)
        {
            if (pitcherPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(pitcherPlayerId));
            if (catcherPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(catcherPlayerId));
            PitcherPlayerId = pitcherPlayerId;
            CatcherPlayerId = catcherPlayerId;
            ConditionModifier = conditionModifier;
        }

        public int PitcherPlayerId { get; }
        public int CatcherPlayerId { get; }
        public int ConditionModifier { get; }
    }

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
            string playerSeasonId = null,
            PitcherRoleConfidence naturalRoleConfidence = PitcherRoleConfidence.High)
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
            NaturalRoleConfidence = naturalRoleConfidence;
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
        public PitcherRoleConfidence NaturalRoleConfidence { get; }
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
        private readonly MatchPlayerConditionEntry[] _playerConditions;
        private readonly MatchBatteryConditionEntry[] _batteryConditions;

        public MatchRosterSnapshot(
            int teamId,
            string teamName,
            Lineup startingLineup,
            PitcherRosterEntry startingPitcher,
            IReadOnlyList<PitcherRosterEntry> bullpen,
            IReadOnlyList<Player> bench,
            ManagerTacticalProfile managerProfile,
            RunningApproach runningApproach,
            int playerCharacterId = 0,
            IReadOnlyList<MatchPlayerConditionEntry> playerConditions = null,
            IReadOnlyList<MatchBatteryConditionEntry> batteryConditions = null)
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
            _playerConditions = CopyPlayerConditions(playerConditions);
            _batteryConditions = CopyBatteryConditions(batteryConditions);
            ManagerProfile = managerProfile;
            RunningApproach = runningApproach;
            PlayerCharacterId = playerCharacterId;
            ValidateUniquePlayers();
            ValidatePlayerConditions();
            ValidateBatteryConditions();
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
        public IReadOnlyList<MatchPlayerConditionEntry> PlayerConditions => _playerConditions;
        public IReadOnlyList<MatchBatteryConditionEntry> BatteryConditions => _batteryConditions;

        /// <summary>경기 시작 때 동결된 선수별 Condition이 있으면 반환한다.</summary>
        public bool TryGetEffectiveCondition(int playerId, out EffectiveMatchCondition condition)
        {
            for (int index = 0; index < _playerConditions.Length; index++)
            {
                if (_playerConditions[index].PlayerId == playerId)
                {
                    condition = _playerConditions[index].Condition;
                    return true;
                }
            }
            condition = default;
            return false;
        }

        /// <summary>현재 투수-포수 Pair의 Battery 변경량이 있으면 반환한다.</summary>
        public bool TryGetBatteryConditionModifier(
            int pitcherPlayerId,
            int catcherPlayerId,
            out int conditionModifier)
        {
            int low = 0;
            int high = _batteryConditions.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                MatchBatteryConditionEntry entry = _batteryConditions[middle];
                if (entry.PitcherPlayerId == pitcherPlayerId && entry.CatcherPlayerId == catcherPlayerId)
                {
                    conditionModifier = entry.ConditionModifier;
                    return true;
                }
                if (entry.PitcherPlayerId < pitcherPlayerId ||
                    entry.PitcherPlayerId == pitcherPlayerId && entry.CatcherPlayerId < catcherPlayerId)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            conditionModifier = 0;
            return false;
        }

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

        private static MatchPlayerConditionEntry[] CopyPlayerConditions(
            IReadOnlyList<MatchPlayerConditionEntry> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<MatchPlayerConditionEntry>();
            var result = new MatchPlayerConditionEntry[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private static MatchBatteryConditionEntry[] CopyBatteryConditions(
            IReadOnlyList<MatchBatteryConditionEntry> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<MatchBatteryConditionEntry>();
            var result = new MatchBatteryConditionEntry[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            Array.Sort(result, CompareBatteryCondition);
            return result;
        }

        private static int CompareBatteryCondition(
            MatchBatteryConditionEntry left,
            MatchBatteryConditionEntry right)
        {
            int pitcher = left.PitcherPlayerId.CompareTo(right.PitcherPlayerId);
            return pitcher != 0 ? pitcher : left.CatcherPlayerId.CompareTo(right.CatcherPlayerId);
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

        private void ValidatePlayerConditions()
        {
            for (int index = 0; index < _playerConditions.Length; index++)
            {
                int playerId = _playerConditions[index].PlayerId;
                if (!ContainsRosterPlayer(playerId))
                    throw new ArgumentException($"PlayerId {playerId}의 Condition이 경기 로스터 밖 선수를 참조합니다.");
                for (int previous = 0; previous < index; previous++)
                {
                    if (_playerConditions[previous].PlayerId == playerId)
                        throw new ArgumentException($"PlayerId {playerId}의 Condition은 중복될 수 없습니다.");
                }
            }
        }

        private bool ContainsRosterPlayer(int playerId)
        {
            for (int index = 0; index < StartingLineup.Count; index++)
                if (StartingLineup[index].Player.PlayerId == playerId) return true;
            if (StartingPitcher.Player.PlayerId == playerId) return true;
            for (int index = 0; index < _bullpen.Length; index++)
                if (_bullpen[index].Player.PlayerId == playerId) return true;
            for (int index = 0; index < _bench.Length; index++)
                if (_bench[index].PlayerId == playerId) return true;
            return false;
        }

        private void ValidateBatteryConditions()
        {
            for (int index = 0; index < _batteryConditions.Length; index++)
            {
                MatchBatteryConditionEntry entry = _batteryConditions[index];
                if (!ContainsPitcher(entry.PitcherPlayerId))
                    throw new ArgumentException("Battery Condition의 투수가 경기 투수진에 없습니다.");
                if (!ContainsRosterPlayer(entry.CatcherPlayerId))
                    throw new ArgumentException("Battery Condition의 포수가 경기 로스터에 없습니다.");
                for (int previous = 0; previous < index; previous++)
                {
                    MatchBatteryConditionEntry other = _batteryConditions[previous];
                    if (other.PitcherPlayerId == entry.PitcherPlayerId &&
                        other.CatcherPlayerId == entry.CatcherPlayerId)
                    {
                        throw new ArgumentException("같은 투수-포수 Battery Condition은 중복될 수 없습니다.");
                    }
                }
            }
        }

        private bool ContainsPitcher(int playerId)
        {
            if (StartingPitcher.Player.PlayerId == playerId) return true;
            for (int index = 0; index < _bullpen.Length; index++)
                if (_bullpen[index].Player.PlayerId == playerId) return true;
            return false;
        }
    }
}

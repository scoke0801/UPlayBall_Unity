using System;
using System.Collections.Generic;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 한 시즌 경기의 Seed·대진·완료 결과를 세이브 가능한 형태로 보관한다.
    /// </summary>
    public sealed class ScheduledGameState
    {
        public ScheduledGameState(
            int gameId,
            int round,
            ulong randomSeed,
            int awayTeamId,
            int homeTeamId)
        {
            if (gameId <= 0 || round <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameId));
            if (awayTeamId <= 0 || homeTeamId <= 0 || awayTeamId == homeTeamId)
                throw new ArgumentException("서로 다른 두 구단의 TeamId가 필요합니다.");

            GameId = gameId;
            Round = round;
            RandomSeed = randomSeed;
            AwayTeamId = awayTeamId;
            HomeTeamId = homeTeamId;
        }

        public int GameId { get; }
        public int Round { get; }
        public ulong RandomSeed { get; }
        public int AwayTeamId { get; }
        public int HomeTeamId { get; }
        public bool IsCompleted { get; private set; }
        public int AwayRuns { get; private set; }
        public int HomeRuns { get; private set; }
        public bool HasPlayerRolePlan { get; private set; }
        public PlayerGameRole PlannedPlayerRole { get; private set; }

        /// <summary>
        /// 화면 표시와 실제 경기 입력이 같은 판단을 쓰도록 기용 결정을 경기 상태에 고정한다.
        /// </summary>
        public void PlanPlayerRole(PlayerGameRole role)
        {
            if (IsCompleted)
                throw new InvalidOperationException("완료된 경기의 기용 계획은 바꿀 수 없습니다.");
            PlannedPlayerRole = role;
            HasPlayerRolePlan = true;
        }

        /// <summary>
        /// 시뮬레이션 결과를 한 번만 기록한다.
        /// </summary>
        public void Complete(int awayRuns, int homeRuns)
        {
            if (IsCompleted)
                throw new InvalidOperationException("이미 완료된 경기입니다.");
            if (awayRuns < 0 || homeRuns < 0)
                throw new ArgumentOutOfRangeException(nameof(awayRuns));

            AwayRuns = awayRuns;
            HomeRuns = homeRuns;
            IsCompleted = true;
        }

        public bool IncludesTeam(int teamId) => AwayTeamId == teamId || HomeTeamId == teamId;
    }

    /// <summary>
    /// 한 시즌의 모든 경기를 GameId 순서로 보관하고 다음 경기를 찾는다.
    /// </summary>
    public sealed class SeasonScheduleState
    {
        private readonly ScheduledGameState[] _games;

        public SeasonScheduleState(ScheduledGameState[] games)
        {
            if (games == null || games.Length == 0)
                throw new ArgumentException("시즌 일정이 비어 있습니다.", nameof(games));
            _games = (ScheduledGameState[])games.Clone();
        }

        public IReadOnlyList<ScheduledGameState> Games => _games;

        public ScheduledGameState GetNextGameForTeam(int teamId)
        {
            for (int index = 0; index < _games.Length; index++)
            {
                ScheduledGameState game = _games[index];
                if (!game.IsCompleted && game.IncludesTeam(teamId))
                    return game;
            }
            return null;
        }
    }

    /// <summary>
    /// 정규 시즌 구단 순위 계산에 필요한 누적 승패와 득실점을 보관한다.
    /// </summary>
    public sealed class TeamSeasonRecordState
    {
        private readonly Dictionary<int, HeadToHeadRecordState> _headToHead = new();

        public TeamSeasonRecordState(int teamId)
            : this(teamId, 0UL)
        {
        }

        public TeamSeasonRecordState(int teamId, ulong fixedTiebreaker)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            TeamId = teamId;
            FixedTiebreaker = fixedTiebreaker;
        }

        public int TeamId { get; }
        public ulong FixedTiebreaker { get; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Ties { get; private set; }
        public int Draws => Ties;
        public int RunsScored { get; private set; }
        public int RunsAllowed { get; private set; }
        public int GamesPlayed => Wins + Losses + Ties;
        public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);

        public void RecordGame(int runsScored, int runsAllowed)
        {
            RecordGame(opponentTeamId: 0, runsScored, runsAllowed);
        }

        public void RecordGame(int opponentTeamId, int runsScored, int runsAllowed)
        {
            if (runsScored < 0 || runsAllowed < 0)
                throw new ArgumentOutOfRangeException(nameof(runsScored));

            RunsScored += runsScored;
            RunsAllowed += runsAllowed;
            if (runsScored > runsAllowed)
                Wins++;
            else if (runsScored < runsAllowed)
                Losses++;
            else
                Ties++;

            if (opponentTeamId > 0)
            {
                if (!_headToHead.TryGetValue(opponentTeamId, out HeadToHeadRecordState record))
                    record = default;
                if (runsScored > runsAllowed) record.Wins++;
                else if (runsScored < runsAllowed) record.Losses++;
                _headToHead[opponentTeamId] = record;
            }
        }

        public HeadToHeadEntry[] GetHeadToHeadEntries()
        {
            var entries = new HeadToHeadEntry[_headToHead.Count];
            int index = 0;
            foreach (KeyValuePair<int, HeadToHeadRecordState> pair in _headToHead)
            {
                entries[index++] = new HeadToHeadEntry(pair.Key, pair.Value.Wins, pair.Value.Losses);
            }
            Array.Sort(entries, (left, right) => left.OpponentTeamId.CompareTo(right.OpponentTeamId));
            return entries;
        }

        private struct HeadToHeadRecordState
        {
            public int Wins;
            public int Losses;
        }
    }
}

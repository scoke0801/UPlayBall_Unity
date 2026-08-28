using System;
using System.Collections.Generic;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 계단식 포스트시즌의 각 단계를 구분한다. 값이 클수록 나중 라운드다.
    /// </summary>
    public enum PostseasonRound
    {
        WildCard,
        Playoff,
        ChampionshipSeries,
        Semifinal
    }

    public enum PostseasonSeriesId
    {
        SemifinalA,
        SemifinalB,
        Championship
    }

    /// <summary>한 구단의 특정 상대 전적을 순수 값으로 전달한다.</summary>
    public readonly struct HeadToHeadEntry
    {
        public HeadToHeadEntry(int opponentTeamId, int wins, int losses)
        {
            OpponentTeamId = opponentTeamId;
            Wins = wins;
            Losses = losses;
        }

        public int OpponentTeamId { get; }
        public int Wins { get; }
        public int Losses { get; }
        public double WinningPercentage => Wins + Losses == 0 ? 0.5d : Wins / (double)(Wins + Losses);
    }

    /// <summary>
    /// 시드 계산에 필요한 한 구단의 정규 시즌 성적이다. Game 레이어의 순위 상태를
    /// Simulation이 참조하지 않도록 순수 값으로 옮겨 담는다.
    /// </summary>
    public readonly struct TeamStandingEntry
    {
        private readonly HeadToHeadEntry[] _headToHead;

        public TeamStandingEntry(int teamId, int wins, int losses, int runsScored, int runsAllowed)
            : this(teamId, wins, losses, runsScored, runsAllowed, 0UL, Array.Empty<HeadToHeadEntry>())
        {
        }

        public TeamStandingEntry(
            int teamId,
            int wins,
            int losses,
            int runsScored,
            int runsAllowed,
            ulong fixedTiebreaker,
            HeadToHeadEntry[] headToHead)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));

            TeamId = teamId;
            Wins = wins;
            Losses = losses;
            RunsScored = runsScored;
            RunsAllowed = runsAllowed;
            FixedTiebreaker = fixedTiebreaker;
            _headToHead = headToHead ?? Array.Empty<HeadToHeadEntry>();
        }

        public int TeamId { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int RunsScored { get; }
        public int RunsAllowed { get; }
        public ulong FixedTiebreaker { get; }
        public int RunDifferential => RunsScored - RunsAllowed;
        public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);

        public double GetHeadToHeadWinningPercentage(int opponentTeamId)
        {
            for (int index = 0; index < _headToHead.Length; index++)
            {
                if (_headToHead[index].OpponentTeamId == opponentTeamId)
                    return _headToHead[index].WinningPercentage;
            }
            return 0.5d;
        }
    }

    /// <summary>
    /// 정규 시즌 순위에서 포스트시즌 시드를 정하고, 계단식 대진의 라운드 구성을 알려준다.
    /// </summary>
    public static class PostseasonBracket
    {
        /// <summary>
        /// 승률 → 승수 → 득실차 → TeamId 순으로 완전 순서를 만들어 상위 playoffTeamCount팀을 반환한다.
        /// 마지막에 TeamId로 끊어 같은 성적이어도 항상 같은 시드가 나오게 한다(결정론).
        /// </summary>
        public static int[] SelectSeeds(IReadOnlyList<TeamStandingEntry> standings, int playoffTeamCount)
        {
            if (standings == null)
                throw new ArgumentNullException(nameof(standings));
            if (playoffTeamCount <= 0 || playoffTeamCount > standings.Count)
                throw new ArgumentOutOfRangeException(nameof(playoffTeamCount));

            var ordered = new TeamStandingEntry[standings.Count];
            for (int index = 0; index < standings.Count; index++)
                ordered[index] = standings[index];
            Array.Sort(ordered, CompareStanding);

            var seeds = new int[playoffTeamCount];
            for (int index = 0; index < playoffTeamCount; index++)
                seeds[index] = ordered[index].TeamId;
            return seeds;
        }

        /// <summary>
        /// 라운드에 맞는 시리즈 경기 수를 반환한다.
        /// </summary>
        public static int GetSeriesGames(
            PostseasonRound round,
            int wildCardSeriesGames,
            int playoffSeriesGames,
            int championshipSeriesGames)
        {
            return round switch
            {
                PostseasonRound.WildCard => wildCardSeriesGames,
                PostseasonRound.Playoff => playoffSeriesGames,
                PostseasonRound.ChampionshipSeries => championshipSeriesGames,
                _ => throw new ArgumentOutOfRangeException(nameof(round))
            };
        }

        /// <summary>
        /// 계단식 대진에서 해당 라운드에 기다리고 있던 상위 시드를 반환한다.
        /// WildCard는 3위(seedIndex 2)와 4위(seedIndex 3)가 맞붙으므로 상위 시드가 3위다.
        /// </summary>
        public static int GetHigherSeedIndex(PostseasonRound round)
        {
            return round switch
            {
                PostseasonRound.WildCard => 2,
                PostseasonRound.Playoff => 1,
                PostseasonRound.ChampionshipSeries => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(round))
            };
        }

        /// <summary>
        /// 시리즈를 이기는 데 필요한 승수를 반환한다. 무승부는 승수로 세지 않는다.
        /// </summary>
        public static int GetWinsRequired(int seriesGames) => seriesGames / 2 + 1;

        /// <summary>
        /// 상위 시드가 홀수 번째 경기를 홈에서 치른다. 어떤 홀수 시리즈 길이에서도
        /// 상위 시드의 홈 경기 수가 과반이 되는 가장 단순한 규칙이다.
        /// </summary>
        public static bool IsHigherSeedHome(int gameNumber) => gameNumber % 2 == 1;

        /// <summary>준결승은 1-1-1, 결승은 2-2-1 순서로 상위 시드 홈을 배정한다.</summary>
        public static bool IsHigherSeedHome(PostseasonRound round, int gameNumber)
        {
            if (gameNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameNumber));
            if (round == PostseasonRound.Semifinal)
                return gameNumber % 2 == 1;
            if (round == PostseasonRound.ChampionshipSeries)
                return gameNumber is 1 or 2 or 5;
            return IsHigherSeedHome(gameNumber);
        }

        public static int GetHigherSeedIndex(PostseasonSeriesId seriesId)
        {
            return seriesId switch
            {
                PostseasonSeriesId.SemifinalA => 0,
                PostseasonSeriesId.SemifinalB => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(seriesId))
            };
        }

        public static int GetLowerSeedIndex(PostseasonSeriesId seriesId)
        {
            return seriesId switch
            {
                PostseasonSeriesId.SemifinalA => 3,
                PostseasonSeriesId.SemifinalB => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(seriesId))
            };
        }

        private static int CompareStanding(TeamStandingEntry left, TeamStandingEntry right)
        {
            int byWinningPercentage = right.WinningPercentage.CompareTo(left.WinningPercentage);
            if (byWinningPercentage != 0)
                return byWinningPercentage;

            int byHeadToHead = right.GetHeadToHeadWinningPercentage(left.TeamId)
                .CompareTo(left.GetHeadToHeadWinningPercentage(right.TeamId));
            if (byHeadToHead != 0)
                return byHeadToHead;

            int byRunDifferential = right.RunDifferential.CompareTo(left.RunDifferential);
            if (byRunDifferential != 0)
                return byRunDifferential;
            int byRunsScored = right.RunsScored.CompareTo(left.RunsScored);
            if (byRunsScored != 0)
                return byRunsScored;
            int byRunsAllowed = left.RunsAllowed.CompareTo(right.RunsAllowed);
            if (byRunsAllowed != 0)
                return byRunsAllowed;
            int byFixedTiebreaker = right.FixedTiebreaker.CompareTo(left.FixedTiebreaker);
            return byFixedTiebreaker != 0 ? byFixedTiebreaker : left.TeamId.CompareTo(right.TeamId);
        }
    }
}

using System;
using System.Collections.Generic;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    public enum PlayerTeamPostseasonResult
    {
        DidNotQualify,
        SemifinalElimination,
        RunnerUp,
        Champion
    }

    /// <summary>
    /// 4강 토너먼트 한 시리즈의 대진과 진행 상태를 세이브 가능한 형태로 보관한다.
    /// </summary>
    public sealed class PostseasonSeriesState
    {
        private readonly List<ScheduledGameState> _games = new();

        public PostseasonSeriesState(
            PostseasonSeriesId seriesId,
            PostseasonRound round,
            int higherSeedTeamId,
            int lowerSeedTeamId,
            int seriesGames)
        {
            if (higherSeedTeamId <= 0 || lowerSeedTeamId <= 0 || higherSeedTeamId == lowerSeedTeamId)
                throw new ArgumentException("서로 다른 두 구단의 TeamId가 필요합니다.");
            if (seriesGames <= 0 || seriesGames % 2 == 0)
                throw new ArgumentOutOfRangeException(nameof(seriesGames));

            SeriesId = seriesId;
            Round = round;
            HigherSeedTeamId = higherSeedTeamId;
            LowerSeedTeamId = lowerSeedTeamId;
            SeriesGames = seriesGames;
            WinsRequired = PostseasonBracket.GetWinsRequired(seriesGames);
        }

        public PostseasonSeriesId SeriesId { get; }
        public PostseasonRound Round { get; }
        public int HigherSeedTeamId { get; }
        public int LowerSeedTeamId { get; }
        public int SeriesGames { get; }

        public int WinsRequired { get; }
        public int HigherSeedWins { get; private set; }
        public int LowerSeedWins { get; private set; }
        public int WinnerTeamId { get; private set; }
        public bool IsCompleted => WinnerTeamId != 0;
        public IReadOnlyList<ScheduledGameState> Games => _games;

        public bool IncludesTeam(int teamId) => HigherSeedTeamId == teamId || LowerSeedTeamId == teamId;

        /// <summary>
        /// 아직 시리즈가 끝나지 않았다면 다음 경기를 만들어 붙인다. 끝났으면 null을 반환한다.
        /// </summary>
        public ScheduledGameState AppendNextGame(int gameId, ulong randomSeed)
        {
            if (IsCompleted)
                return null;

            int gameNumber = _games.Count + 1;
            bool higherSeedHome = PostseasonBracket.IsHigherSeedHome(Round, gameNumber);
            var game = new ScheduledGameState(
                gameId,
                gameNumber,
                randomSeed,
                higherSeedHome ? LowerSeedTeamId : HigherSeedTeamId,
                higherSeedHome ? HigherSeedTeamId : LowerSeedTeamId);
            _games.Add(game);
            return game;
        }

        /// <summary>
        /// 완료된 경기의 결과를 시리즈 승수에 반영하고, 승부가 갈렸으면 승자를 확정한다.
        /// 포스트시즌 경기 입력은 승자 필수이므로 무승부는 상태에 기록하지 않는다.
        /// </summary>
        public void RecordGameResult(int winnerTeamId)
        {
            if (IsCompleted)
                throw new InvalidOperationException("이미 끝난 시리즈입니다.");

            if (winnerTeamId == HigherSeedTeamId)
                HigherSeedWins++;
            else if (winnerTeamId == LowerSeedTeamId)
                LowerSeedWins++;
            else if (winnerTeamId == 0)
                throw new InvalidOperationException("포스트시즌에는 무승부가 없습니다.");
            else
                throw new ArgumentException("이 시리즈에 속하지 않은 구단입니다.", nameof(winnerTeamId));

            if (HigherSeedWins >= WinsRequired)
                WinnerTeamId = HigherSeedTeamId;
            else if (LowerSeedWins >= WinsRequired)
                WinnerTeamId = LowerSeedTeamId;
        }
    }

    /// <summary>
    /// 한 시즌 포스트시즌 전체의 시드·시리즈 진행·우승 구단을 소유한다.
    /// </summary>
    public sealed class PostseasonState
    {
        private readonly int[] _seedTeamIds;
        private readonly List<PostseasonSeriesState> _series = new();

        public PostseasonState(int saveVersion, int[] seedTeamIds)
        {
            if (seedTeamIds == null || seedTeamIds.Length != 4)
                throw new ArgumentException("4강 토너먼트는 4개 시드가 필요합니다.", nameof(seedTeamIds));

            SaveVersion = saveVersion;
            _seedTeamIds = (int[])seedTeamIds.Clone();
        }

        public int SaveVersion { get; }
        public IReadOnlyList<int> SeedTeamIds => _seedTeamIds;
        public IReadOnlyList<PostseasonSeriesState> Series => _series;
        public int ChampionTeamId { get; private set; }
        public int RunnerUpTeamId { get; private set; }
        public PlayerTeamPostseasonResult PlayerTeamResult { get; private set; }
        public bool IsCompleted => ChampionTeamId != 0;

        /// <summary>
        /// 지정 시드 순위(0부터)의 구단 Id를 반환한다.
        /// </summary>
        public int GetSeedTeamId(int seedIndex) => _seedTeamIds[seedIndex];

        /// <summary>
        /// 아직 끝나지 않은 시리즈를 반환한다. 전부 끝났으면 null이다.
        /// </summary>
        public PostseasonSeriesState CurrentSeries
        {
            get
            {
                for (int index = 0; index < _series.Count; index++)
                {
                    if (!_series[index].IsCompleted)
                        return _series[index];
                }
                return null;
            }
        }

        public void AddSeries(PostseasonSeriesState series)
        {
            if (series == null)
                throw new ArgumentNullException(nameof(series));
            for (int index = 0; index < _series.Count; index++)
            {
                if (_series[index].SeriesId == series.SeriesId)
                    throw new InvalidOperationException("같은 시리즈가 이미 생성되었습니다.");
            }
            _series.Add(series);
        }

        public PostseasonSeriesState GetSeries(PostseasonSeriesId seriesId)
        {
            for (int index = 0; index < _series.Count; index++)
            {
                if (_series[index].SeriesId == seriesId)
                    return _series[index];
            }
            return null;
        }

        /// <summary>
        /// 한국시리즈 승자를 우승 구단으로 확정한다.
        /// </summary>
        public void CompleteWithChampion(int championTeamId)
        {
            PostseasonSeriesState championship = GetSeries(PostseasonSeriesId.Championship);
            int runnerUpTeamId = championship == null
                ? 0
                : championship.HigherSeedTeamId == championTeamId
                    ? championship.LowerSeedTeamId
                    : championship.HigherSeedTeamId;
            CompleteWithChampion(championTeamId, runnerUpTeamId, playerTeamId: 0);
        }

        public void CompleteWithChampion(int championTeamId, int runnerUpTeamId, int playerTeamId)
        {
            if (championTeamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(championTeamId));
            if (IsCompleted)
                throw new InvalidOperationException("이미 우승 구단이 확정되었습니다.");
            ChampionTeamId = championTeamId;
            RunnerUpTeamId = runnerUpTeamId;
            PlayerTeamResult = ResolvePlayerTeamResult(playerTeamId);
        }

        private PlayerTeamPostseasonResult ResolvePlayerTeamResult(int playerTeamId)
        {
            if (playerTeamId == ChampionTeamId) return PlayerTeamPostseasonResult.Champion;
            if (playerTeamId == RunnerUpTeamId) return PlayerTeamPostseasonResult.RunnerUp;
            bool qualified = false;
            for (int index = 0; index < _seedTeamIds.Length; index++)
            {
                if (_seedTeamIds[index] == playerTeamId) qualified = true;
            }
            return qualified
                ? PlayerTeamPostseasonResult.SemifinalElimination
                : PlayerTeamPostseasonResult.DidNotQualify;
        }
    }
}

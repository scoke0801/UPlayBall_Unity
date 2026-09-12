using System;
using System.Collections.Generic;
using Baseball.Game.Career;
using Baseball.Simulation.Career;

namespace Baseball.Game.Historical
{
    public enum OwnerPostseasonRound
    {
        Semifinal,
        Championship
    }

    public enum OwnerTeamPostseasonResult
    {
        DidNotQualify,
        SemifinalElimination,
        RunnerUp,
        Champion
    }

    /// <summary>구단주 포스트시즌 한 시리즈의 일정과 승수를 저장한다.</summary>
    public sealed class OwnerPostseasonSeriesState
    {
        private readonly List<ScheduledGameState> _games;

        public OwnerPostseasonSeriesState(string seriesId, OwnerPostseasonRound round,
            int higherSeedTeamId, int lowerSeedTeamId, int seriesGames,
            IReadOnlyList<ScheduledGameState> games = null, int higherSeedWins = 0, int lowerSeedWins = 0)
        {
            if (string.IsNullOrWhiteSpace(seriesId)) throw new ArgumentException("SeriesId가 필요합니다.", nameof(seriesId));
            if (!Enum.IsDefined(typeof(OwnerPostseasonRound), round)) throw new ArgumentOutOfRangeException(nameof(round));
            if (higherSeedTeamId <= 0 || lowerSeedTeamId <= 0 || higherSeedTeamId == lowerSeedTeamId)
                throw new ArgumentException("서로 다른 두 구단이 필요합니다.");
            if (seriesGames <= 0 || (seriesGames & 1) == 0) throw new ArgumentOutOfRangeException(nameof(seriesGames));
            WinsRequired = seriesGames / 2 + 1;
            if (higherSeedWins < 0 || lowerSeedWins < 0 || higherSeedWins > WinsRequired || lowerSeedWins > WinsRequired ||
                higherSeedWins == WinsRequired && lowerSeedWins == WinsRequired)
                throw new ArgumentException("시리즈 승수가 올바르지 않습니다.");

            SeriesId = seriesId.Trim();
            Round = round;
            HigherSeedTeamId = higherSeedTeamId;
            LowerSeedTeamId = lowerSeedTeamId;
            SeriesGames = seriesGames;
            HigherSeedWins = higherSeedWins;
            LowerSeedWins = lowerSeedWins;
            _games = games == null ? new List<ScheduledGameState>() : new List<ScheduledGameState>(games);
            int completedGames = 0;
            for (int index = 0; index < _games.Count; index++)
            {
                if (_games[index] == null || !_games[index].IncludesTeam(higherSeedTeamId) || !_games[index].IncludesTeam(lowerSeedTeamId))
                    throw new ArgumentException("시리즈 일정이 대진과 다릅니다.", nameof(games));
                if (_games[index].IsCompleted) completedGames++;
                else if (index + 1 < _games.Count) throw new ArgumentException("미완료 경기 뒤에 다음 경기가 있습니다.", nameof(games));
            }
            if (completedGames != higherSeedWins + lowerSeedWins)
                throw new ArgumentException("완료 경기 수와 시리즈 승수가 다릅니다.", nameof(games));
        }

        public string SeriesId { get; }
        public OwnerPostseasonRound Round { get; }
        public int HigherSeedTeamId { get; }
        public int LowerSeedTeamId { get; }
        public int SeriesGames { get; }
        public int WinsRequired { get; }
        public int HigherSeedWins { get; private set; }
        public int LowerSeedWins { get; private set; }
        public int WinnerTeamId => HigherSeedWins == WinsRequired ? HigherSeedTeamId : LowerSeedWins == WinsRequired ? LowerSeedTeamId : 0;
        public bool IsCompleted => WinnerTeamId != 0;
        public IReadOnlyList<ScheduledGameState> Games => _games;

        public bool IncludesTeam(int teamId) => teamId == HigherSeedTeamId || teamId == LowerSeedTeamId;

        /// <summary>승부가 나지 않은 시리즈의 다음 경기 하나를 고정한다.</summary>
        public ScheduledGameState AppendNextGame(int gameId, ulong randomSeed)
        {
            if (IsCompleted) return null;
            if (_games.Count > 0 && !_games[_games.Count - 1].IsCompleted)
                throw new InvalidOperationException("현재 경기를 완료한 뒤 다음 경기를 만들 수 있습니다.");
            int gameNumber = _games.Count + 1;
            bool higherSeedHome = Round == OwnerPostseasonRound.Semifinal
                ? PostseasonBracket.IsHigherSeedHome(PostseasonRound.Semifinal, gameNumber)
                : PostseasonBracket.IsHigherSeedHome(PostseasonRound.ChampionshipSeries, gameNumber);
            var game = new ScheduledGameState(gameId, gameNumber, randomSeed,
                higherSeedHome ? LowerSeedTeamId : HigherSeedTeamId,
                higherSeedHome ? HigherSeedTeamId : LowerSeedTeamId);
            _games.Add(game);
            return game;
        }

        /// <summary>완료된 승자 필수 경기 한 건을 시리즈에 정확히 한 번 반영한다.</summary>
        public void RecordCompletedGame(ScheduledGameState game)
        {
            if (game == null || !game.IsCompleted) throw new ArgumentException("완료된 경기가 필요합니다.", nameof(game));
            if (IsCompleted) throw new InvalidOperationException("이미 끝난 시리즈입니다.");
            if (_games.Count == 0 || !ReferenceEquals(_games[_games.Count - 1], game))
                throw new InvalidOperationException("현재 시리즈의 다음 경기가 아닙니다.");
            if (HigherSeedWins + LowerSeedWins != _games.Count - 1)
                throw new InvalidOperationException("현재 경기 결과가 이미 반영됐거나 앞선 결과가 누락됐습니다.");
            if (game.AwayRuns == game.HomeRuns) throw new InvalidOperationException("포스트시즌 경기는 승자가 필요합니다.");
            int winner = game.AwayRuns > game.HomeRuns ? game.AwayTeamId : game.HomeTeamId;
            if (winner == HigherSeedTeamId) HigherSeedWins++;
            else if (winner == LowerSeedTeamId) LowerSeedWins++;
            else throw new InvalidOperationException("시리즈 대진에 없는 승자입니다.");
        }
    }

    /// <summary>한 조의 포스트시즌 시드·시리즈·최종 결과를 저장한다.</summary>
    public sealed class OwnerPostseasonState
    {
        private readonly int[] _seedTeamIds;
        private readonly List<OwnerPostseasonSeriesState> _series;

        public OwnerPostseasonState(string seasonId, int[] seedTeamIds,
            IReadOnlyList<OwnerPostseasonSeriesState> series = null)
        {
            if (string.IsNullOrWhiteSpace(seasonId)) throw new ArgumentException("SeasonId가 필요합니다.", nameof(seasonId));
            if (seedTeamIds == null || seedTeamIds.Length != 2 && seedTeamIds.Length != 4)
                throw new ArgumentException("포스트시즌 시드는 2개 또는 4개여야 합니다.", nameof(seedTeamIds));
            var unique = new HashSet<int>();
            for (int index = 0; index < seedTeamIds.Length; index++)
                if (seedTeamIds[index] <= 0 || !unique.Add(seedTeamIds[index])) throw new ArgumentException("시드가 올바르지 않습니다.");
            SeasonId = seasonId.Trim();
            _seedTeamIds = (int[])seedTeamIds.Clone();
            _series = series == null ? new List<OwnerPostseasonSeriesState>() : new List<OwnerPostseasonSeriesState>(series);
            ValidateSeries();
        }

        public string SeasonId { get; }
        public IReadOnlyList<int> SeedTeamIds => _seedTeamIds;
        public IReadOnlyList<OwnerPostseasonSeriesState> Series => _series;
        public int ChampionTeamId => _series.Count > 0 && _series[_series.Count - 1].Round == OwnerPostseasonRound.Championship
            ? _series[_series.Count - 1].WinnerTeamId : 0;
        public bool IsCompleted => ChampionTeamId != 0;

        public OwnerPostseasonSeriesState CurrentSeries
        {
            get
            {
                for (int index = 0; index < _series.Count; index++) if (!_series[index].IsCompleted) return _series[index];
                return null;
            }
        }

        /// <summary>필요한 준결승 또는 결승 대진을 생성하고 현재 시리즈를 반환한다.</summary>
        public OwnerPostseasonSeriesState EnsureCurrentSeries(int semifinalGames, int championshipGames)
        {
            OwnerPostseasonSeriesState current = CurrentSeries;
            if (current != null || IsCompleted) return current;
            if (_series.Count == 0)
            {
                if (_seedTeamIds.Length == 2)
                    _series.Add(new OwnerPostseasonSeriesState("championship", OwnerPostseasonRound.Championship,
                        _seedTeamIds[0], _seedTeamIds[1], championshipGames));
                else
                {
                    _series.Add(new OwnerPostseasonSeriesState("semifinal-a", OwnerPostseasonRound.Semifinal,
                        _seedTeamIds[0], _seedTeamIds[3], semifinalGames));
                    _series.Add(new OwnerPostseasonSeriesState("semifinal-b", OwnerPostseasonRound.Semifinal,
                        _seedTeamIds[1], _seedTeamIds[2], semifinalGames));
                }
                return CurrentSeries;
            }
            if (_seedTeamIds.Length == 4 && _series.Count == 2 && _series[0].IsCompleted && _series[1].IsCompleted)
            {
                int first = _series[0].WinnerTeamId;
                int second = _series[1].WinnerTeamId;
                if (GetSeedIndex(second) < GetSeedIndex(first)) (first, second) = (second, first);
                _series.Add(new OwnerPostseasonSeriesState("championship", OwnerPostseasonRound.Championship,
                    first, second, championshipGames));
                return _series[2];
            }
            throw new InvalidOperationException("포스트시즌 시리즈 상태가 올바르지 않습니다.");
        }

        public bool IsQualified(int teamId) => GetSeedIndex(teamId) >= 0;

        /// <summary>대진 상대 결정 대기를 포함해 해당 구단의 남은 포스트시즌 출전 여부를 반환한다.</summary>
        public bool HasRemainingGames(int teamId)
        {
            if (IsCompleted || !IsQualified(teamId)) return false;
            for (int index = 0; index < _series.Count; index++)
            {
                OwnerPostseasonSeriesState series = _series[index];
                if (series.IncludesTeam(teamId) && series.IsCompleted && series.WinnerTeamId != teamId)
                    return false;
            }
            return true;
        }

        public OwnerTeamPostseasonResult GetTeamResult(int teamId)
        {
            if (!IsQualified(teamId)) return OwnerTeamPostseasonResult.DidNotQualify;
            if (!IsCompleted) throw new InvalidOperationException("완료 전에는 최종 결과를 판정할 수 없습니다.");
            if (ChampionTeamId == teamId) return OwnerTeamPostseasonResult.Champion;
            OwnerPostseasonSeriesState final = _series[_series.Count - 1];
            if (final.IncludesTeam(teamId)) return OwnerTeamPostseasonResult.RunnerUp;
            return OwnerTeamPostseasonResult.SemifinalElimination;
        }

        private int GetSeedIndex(int teamId)
        {
            for (int index = 0; index < _seedTeamIds.Length; index++) if (_seedTeamIds[index] == teamId) return index;
            return -1;
        }

        private void ValidateSeries()
        {
            if (_series.Count > (_seedTeamIds.Length == 4 ? 3 : 1)) throw new ArgumentException("시리즈 수가 올바르지 않습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _series.Count; index++)
                if (_series[index] == null || !ids.Add(_series[index].SeriesId)) throw new ArgumentException("시리즈가 중복되거나 누락됐습니다.");
            if (_seedTeamIds.Length == 2)
            {
                if (_series.Count == 1 &&
                    (_series[0].Round != OwnerPostseasonRound.Championship ||
                     _series[0].HigherSeedTeamId != _seedTeamIds[0] ||
                     _series[0].LowerSeedTeamId != _seedTeamIds[1]))
                    throw new ArgumentException("챔피언십 대진이 시드와 다릅니다.");
                return;
            }
            if (_series.Count == 1) throw new ArgumentException("4강 두 시리즈 중 하나가 누락됐습니다.");
            if (_series.Count >= 2)
            {
                ValidatePair(_series[0], OwnerPostseasonRound.Semifinal, _seedTeamIds[0], _seedTeamIds[3]);
                ValidatePair(_series[1], OwnerPostseasonRound.Semifinal, _seedTeamIds[1], _seedTeamIds[2]);
            }
            if (_series.Count == 3)
            {
                if (!_series[0].IsCompleted || !_series[1].IsCompleted)
                    throw new ArgumentException("준결승 완료 전에 챔피언십이 생성됐습니다.");
                int first = _series[0].WinnerTeamId;
                int second = _series[1].WinnerTeamId;
                if (GetSeedIndex(second) < GetSeedIndex(first)) (first, second) = (second, first);
                ValidatePair(_series[2], OwnerPostseasonRound.Championship, first, second);
            }
        }

        private static void ValidatePair(OwnerPostseasonSeriesState series, OwnerPostseasonRound round,
            int higherSeedTeamId, int lowerSeedTeamId)
        {
            if (series.Round != round || series.HigherSeedTeamId != higherSeedTeamId ||
                series.LowerSeedTeamId != lowerSeedTeamId)
                throw new ArgumentException("포스트시즌 대진이 시드와 다릅니다.");
        }
    }
}
